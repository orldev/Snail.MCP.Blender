using System.Security.Cryptography;

namespace Snail.MCP.Blender.Application.Transfer;

/// <summary>One file moved between the two machines, with the digest both ends agreed on.</summary>
public sealed record Transferred(string From, string To, long Size, string Sha256);

/// <summary>What a transfer did: the files that made it, and the error that stopped it when one did.</summary>
public sealed record TransferOutcome(IReadOnlyList<Transferred> Files, BridgeError? Error);

/// <summary>Moves files and folders between this machine and the one Blender runs on, a chunk per request, each end checking the other's SHA-256.</summary>
/// <remarks>The chunks are sized by the add-on's line limits: 16 MB a request and 4 MB a reply, both carrying base64. The exchange itself is
/// handed in by the tool, which names the bridge command, so this class knows files and digests and nothing of the link. A file lands under a
/// partial name and takes its own only once the digest matches, so an interrupted transfer never leaves a truncated file behind — including
/// a transfer the caller cancelled, which used to leave one: the partial was swept on a failed reply and not on the exception a cancellation
/// throws through the same code.</remarks>
public sealed class FileTransfer
{
    public const int UploadChunkBytes = 8 * 1024 * 1024;

    public const int DownloadChunkBytes = 2 * 1024 * 1024;

    /// <summary>Files one transfer will carry; past that the folder is asked for in parts, because one command should not run for an hour.</summary>
    public const int MostFiles = 20_000;

    private const string PartialSuffix = ".part";

    /// <summary>A local file, or every file under a local folder, to the given path on Blender's machine.</summary>
    public async Task<TransferOutcome> UploadAsync(string localPath, string remotePath, bool overwrite, Func<JsonObject, Task<BridgeReply>> put, CancellationToken cancellationToken)
    {
        if (File.Exists(localPath))
        {
            var (file, error) = await PutAsync(localPath, remotePath, overwrite, put, cancellationToken).ConfigureAwait(false);

            return new TransferOutcome(file is null ? [] : [file], error);
        }

        if (!Directory.Exists(localPath))
        {
            return new TransferOutcome([], Missing(localPath));
        }

        var done = new List<Transferred>();

        foreach (var local in Directory.EnumerateFiles(localPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(localPath, local).Replace(Path.DirectorySeparatorChar, '/');
            var (file, error) = await PutAsync(local, RemotePaths.Join(remotePath, relative), overwrite, put, cancellationToken).ConfigureAwait(false);

            if (file is null)
            {
                return new TransferOutcome(done, error);
            }

            done.Add(file);
        }

        return new TransferOutcome(done, null);
    }

    /// <summary>A file, or every file under a folder, from Blender's machine to the given local path.</summary>
    public async Task<TransferOutcome> DownloadAsync(
        string remotePath, string localPath, bool overwrite, Func<JsonObject, Task<BridgeReply>> list, Func<JsonObject, Task<BridgeReply>> get, CancellationToken cancellationToken)
    {
        var listing = await list(new JsonObject { ["path"] = remotePath }).ConfigureAwait(false);

        if (!listing.IsOk || listing.Result is not JsonObject found || found["files"] is not JsonArray files)
        {
            return new TransferOutcome([], listing.Error ?? Unreadable("file_list"));
        }

        if (found["total"]?.GetValue<int>() > MostFiles)
        {
            return new TransferOutcome([], TooManyFiles(remotePath, found["total"]!.GetValue<int>()));
        }

        var (named, unreadable) = await PagesAsync(remotePath, found, files, list).ConfigureAwait(false);

        if (unreadable is not null)
        {
            return new TransferOutcome([], unreadable);
        }

        var root = found["root"]?.GetValue<string>() ?? remotePath;
        var single = found["kind"]?.GetValue<string>() == "file";
        var done = new List<Transferred>();

        foreach (var relative in named)
        {
            var local = single ? localPath : Path.Combine(localPath, relative.Replace('/', Path.DirectorySeparatorChar));
            var (file, error) = await GetAsync(RemotePaths.Join(root, relative), local, overwrite, get, cancellationToken).ConfigureAwait(false);

            if (file is null)
            {
                return new TransferOutcome(done, error);
            }

            done.Add(file);
        }

        return new TransferOutcome(done, null);
    }

    private static async Task<(Transferred? File, BridgeError? Error)> PutAsync(
        string local, string remote, bool overwrite, Func<JsonObject, Task<BridgeReply>> put, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(local);
        var digest = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        stream.Position = 0;

        var buffer = new byte[(int)Math.Clamp(stream.Length, 1, UploadChunkBytes)];
        long offset = 0;

        while (true)
        {
            var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
            var last = offset + read >= stream.Length;
            var chunk = new JsonObject
            {
                ["path"] = remote,
                ["offset"] = offset,
                ["data"] = Convert.ToBase64String(buffer, 0, read),
                ["done"] = last,
                ["overwrite"] = overwrite,
            };

            if (last)
            {
                chunk["sha256"] = digest;
            }

            var reply = await put(chunk).ConfigureAwait(false);

            if (!reply.IsOk)
            {
                return (null, reply.Error);
            }

            offset += read;

            if (last)
            {
                return (new Transferred(local, reply.Result?["path"]?.GetValue<string>() ?? remote, offset, digest), null);
            }
        }
    }

    private static async Task<(Transferred? File, BridgeError? Error)> GetAsync(
        string remote, string local, bool overwrite, Func<JsonObject, Task<BridgeReply>> get, CancellationToken cancellationToken)
    {
        if (File.Exists(local) && !overwrite)
        {
            return (null, new BridgeError("Exists", $"'{local}' already exists on this machine; pass overwrite to replace it", new JsonObject { ["path"] = local }));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(local))!);

        var partial = $"{local}{PartialSuffix}";

        try
        {
            var (size, expected, error) = await ReceiveAsync(remote, partial, get, cancellationToken).ConfigureAwait(false);

            if (error is not null)
            {
                return (null, error);
            }

            var actual = await DigestAsync(partial, cancellationToken).ConfigureAwait(false);

            if (expected is not null && !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                return (null, new BridgeError("Corrupt", $"'{remote}' arrived different from what was sent", new JsonObject { ["expected"] = expected, ["received"] = actual }));
            }

            File.Move(partial, local, overwrite: true);

            return (new Transferred(remote, local, size, actual), null);
        }
        finally
        {
            if (File.Exists(partial))
            {
                File.Delete(partial);
            }
        }
    }

    /// <summary>Writes the chunks in order into the partial file; the last one carries the digest of the whole file on the other machine.</summary>
    private static async Task<(long Size, string? Expected, BridgeError? Error)> ReceiveAsync(
        string remote, string partial, Func<JsonObject, Task<BridgeReply>> get, CancellationToken cancellationToken)
    {
        using var output = File.Create(partial);
        long offset = 0;

        while (true)
        {
            var reply = await get(new JsonObject { ["path"] = remote, ["offset"] = offset, ["length"] = DownloadChunkBytes }).ConfigureAwait(false);

            if (!reply.IsOk || reply.Result is not JsonObject chunk || chunk["data"]?.GetValue<string>() is not { } data)
            {
                return (offset, null, reply.Error ?? Unreadable("file_get"));
            }

            var bytes = Convert.FromBase64String(data);
            await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            offset += bytes.Length;

            if (chunk["eof"]?.GetValue<bool>() == true)
            {
                return (offset, chunk["sha256"]?.GetValue<string>(), null);
            }

            if (bytes.Length == 0)
            {
                return (offset, null, new BridgeError("Stalled", $"'{remote}' stopped growing at {offset} bytes before its end"));
            }
        }
    }

    private static async Task<string> DigestAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);

        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static BridgeError Missing(string path) =>
        new("NotFound", $"no file or folder at '{path}' on this machine", new JsonObject { ["path"] = path });

    /// <summary>A folder the add-on lists only in part; over HTTP the same folder is refused as too large for one archive.</summary>
    /// <summary>Every name under the folder, asking for the next page until the add-on says there is none.</summary>
    /// <remarks>One listing used to be the whole answer, so a folder past the add-on's page refused to download at all rather than being
    /// fetched a page at a time.
    /// <para>A page is asked for only while the add-on echoes the offset it was given: an older one, which knows nothing of pages, would
    /// otherwise answer the same first page to every request and be asked for ever.</para></remarks>
    private static async Task<(IReadOnlyList<string> Files, BridgeError? Error)> PagesAsync(
        string remotePath, JsonObject first, JsonArray files, Func<JsonObject, Task<BridgeReply>> list)
    {
        var named = files.Select(entry => entry?["path"]?.GetValue<string>()).OfType<string>().ToList();
        var page = first;

        while (page["truncated"]?.GetValue<bool>() == true && page["offset"] is not null && named.Count < MostFiles)
        {
            var asked = named.Count;
            var next = await list(new JsonObject { ["path"] = remotePath, ["offset"] = asked }).ConfigureAwait(false);

            if (!next.IsOk || next.Result is not JsonObject following || following["files"] is not JsonArray more)
            {
                return ([], next.Error ?? Unreadable("file_list"));
            }

            if (more.Count == 0 || following["offset"]?.GetValue<int>() != asked)
            {
                return (named, null);
            }

            named.AddRange(more.Select(entry => entry?["path"]?.GetValue<string>()).OfType<string>());
            page = following;
        }

        return (named, null);
    }

    private static BridgeError TooManyFiles(string path, int listed) =>
        new("TooManyFiles", $"'{path}' holds {listed} files, more than the {MostFiles} one transfer carries; download its folders one by one", new JsonObject { ["path"] = path });

    private static BridgeError Unreadable(string command) =>
        new(BridgeError.MalformedType, $"the add-on answered {command} without the fields a transfer needs; blender_diagnose tells whether it is the build this server ships");
}
