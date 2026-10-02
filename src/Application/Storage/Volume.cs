using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Snail.MCP.Blender.Application.Transfer;

namespace Snail.MCP.Blender.Application.Storage;

/// <summary>A file under a path of an area, relative to that path, with its size and its modification time in Unix seconds.</summary>
public sealed record VolumeFile(string Path, long Size, long Modified);

/// <summary>What a path of an area holds: one file, or a folder and every file under it.</summary>
public sealed record VolumeItem(bool IsFolder, IReadOnlyList<VolumeFile> Files, long Bytes, bool Truncated);

/// <summary>A path of an area looked up: what it holds, or why it could not be read.</summary>
public sealed record VolumeLookup(VolumeItem? Item, BridgeError? Error);

/// <summary>Where the bytes of one request land in a file sent in several: at an offset, and whether this request ends the file, checked
/// against the digest the client computed of the whole.</summary>
public sealed record UploadPart(long Offset, bool IsLast, string? Sha256);

/// <summary>Files written into an area, and the error that stopped the writing when one did.</summary>
public sealed record VolumeWrite(IReadOnlyList<Transferred> Files, BridgeError? Error);

/// <summary>The add-on's data directory as the HTTP server reaches it: its areas measured, listed, read, written and deleted over the volume link.</summary>
/// <remarks>Bytes move in the chunks file transfers use, each file checked against the digest the other end computed. Nothing here touches this
/// machine's disk: a download streams from the add-on into the response, an upload from the request into the add-on.</remarks>
public sealed class Volume(IBlenderVolume link)
{
    public Task<BridgeReply> MeasureAsync(CancellationToken cancellationToken) =>
        link.SendAsync(BridgeCommands.Storage, [], cancellationToken: cancellationToken);

    public Task<BridgeReply> ListAsync(string area, string path, CancellationToken cancellationToken) =>
        link.SendAsync(BridgeCommands.StorageList, new JsonObject { ["area"] = area, ["path"] = path }, cancellationToken: cancellationToken);

    public Task<BridgeReply> DeleteAsync(string area, string path, CancellationToken cancellationToken) =>
        link.SendAsync(BridgeCommands.StorageDelete, new JsonObject { ["area"] = area, ["path"] = path }, cancellationToken: cancellationToken);

    public async Task<VolumeLookup> FindAsync(string area, string path, CancellationToken cancellationToken)
    {
        var listing = await link.SendAsync(BridgeCommands.FileList, new JsonObject { ["area"] = area, ["path"] = path }, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!listing.IsOk || listing.Result is not JsonObject found || found["files"] is not JsonArray files)
        {
            return new VolumeLookup(null, listing.Error ?? Unreadable);
        }

        var entries = files
            .Select(entry => new VolumeFile(entry?["path"]?.GetValue<string>() ?? string.Empty, entry?["size"]?.GetValue<long>() ?? 0, entry?["modified"]?.GetValue<long>() ?? 0))
            .ToList();

        return new VolumeLookup(
            new VolumeItem(found["kind"]?.GetValue<string>() == "directory", entries, found["total_bytes"]?.GetValue<long>() ?? 0, found["truncated"]?.GetValue<bool>() == true),
            null);
    }

    /// <summary>A file of an area as a stream read chunk by chunk as it is consumed; a failure or a digest that does not match surfaces as an <see cref="IOException"/>.</summary>
    public Stream OpenRead(string area, string path, long size) => new VolumeReadStream(link, area, path, size);

    /// <summary>A folder as a tar of every file under it, written as it is read.</summary>
    public async Task WriteTarAsync(string area, string path, VolumeItem folder, Stream destination, CancellationToken cancellationToken)
    {
        var tar = new TarStream(destination);

        foreach (var file in folder.Files)
        {
            await using var content = OpenRead(area, RemotePaths.Join(path, file.Path), file.Size);

            await tar.WriteFileAsync(file.Path, file.Size, file.Modified, content, cancellationToken).ConfigureAwait(false);
        }

        await tar.EndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A folder as a zip of every file under it, for a browser that opens archives with a double click.</summary>
    public async Task WriteZipAsync(string area, string path, VolumeItem folder, Stream destination, CancellationToken cancellationToken)
    {
        await using var archive = await ZipArchive.CreateAsync(destination, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, cancellationToken).ConfigureAwait(false);

        foreach (var file in folder.Files)
        {
            await using var content = OpenRead(area, RemotePaths.Join(path, file.Path), file.Size);
            await using var target = await archive.CreateEntry(file.Path, CompressionLevel.Fastest).OpenAsync(cancellationToken).ConfigureAwait(false);

            await content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>A stream into one file of an area, sent in chunks while it is read and checked against its digest at the end.</summary>
    public Task<BridgeReply> WriteAsync(string area, string path, Stream source, bool overwrite, CancellationToken cancellationToken) =>
        WritePartAsync(area, path, source, new UploadPart(0, IsLast: true, Sha256: null), overwrite, cancellationToken);

    /// <summary>One request of a file sent in several: its bytes continue the partial file at the part's offset, and the last part gives the
    /// file its name once the digest matches — the client's whenever it sent one, since only that one speaks for the file on its disk, and the
    /// one computed here for a file that came whole without it.</summary>
    /// <remarks>A reverse proxy may read a request body for a minute at most, and Traefik's default does exactly that; a client on a slow
    /// uplink therefore sends a large file as parts that each arrive well inside that minute.</remarks>
    public async Task<BridgeReply> WritePartAsync(string area, string path, Stream source, UploadPart part, bool overwrite, CancellationToken cancellationToken)
    {
        var isWhole = part.Offset == 0 && part.IsLast;
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var current = new byte[FileTransfer.UploadChunkBytes];
        var next = new byte[FileTransfer.UploadChunkBytes];
        var currentLength = await source.ReadAtLeastAsync(current, current.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        var offset = part.Offset;

        while (true)
        {
            var nextLength = currentLength == current.Length
                ? await source.ReadAtLeastAsync(next, next.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false)
                : 0;
            var isFinalChunk = nextLength == 0;

            if (isFinalChunk && currentLength == 0 && !part.IsLast)
            {
                return BridgeReply.Ok(new JsonObject { ["path"] = path, ["received"] = offset });
            }

            digest.AppendData(current, 0, currentLength);

            var chunk = new JsonObject
            {
                ["area"] = area,
                ["path"] = path,
                ["offset"] = offset,
                ["data"] = Convert.ToBase64String(current, 0, currentLength),
                ["done"] = isFinalChunk && part.IsLast,
                ["overwrite"] = overwrite,
            };

            if (isFinalChunk && part.IsLast && (part.Sha256 ?? (isWhole ? Convert.ToHexStringLower(digest.GetHashAndReset()) : null)) is { } expected)
            {
                chunk["sha256"] = expected;
            }

            var reply = await link.SendAsync(BridgeCommands.FilePut, chunk, TransferTimeouts.ForChunk(chunk), cancellationToken).ConfigureAwait(false);

            if (!reply.IsOk || isFinalChunk)
            {
                return reply;
            }

            offset += currentLength;
            (current, next, currentLength) = (next, current, nextLength);
        }
    }

    /// <summary>A tar that arrived in parts into a staging file, unpacked into its folder and then deleted, whether the unpacking succeeded or not.</summary>
    public async Task<VolumeWrite> UnpackStagedTarAsync(string area, string path, string staged, bool overwrite, CancellationToken cancellationToken)
    {
        try
        {
            var lookup = await FindAsync(area, staged, cancellationToken).ConfigureAwait(false);

            if (lookup.Item is not { IsFolder: false } tar)
            {
                return new VolumeWrite([], lookup.Error ?? new BridgeError("NotFound", $"the staged tar '{staged}' is gone"));
            }

            await using var source = OpenRead(area, staged, tar.Bytes);

            return await UnpackTarAsync(area, path, source, overwrite, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await DeleteAsync(area, staged, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>The file a tar sent in parts gathers in until its last part: one per client and target folder, at the top of the area, named so
    /// it cannot collide with a project.</summary>
    /// <remarks>The client belongs in the name. Two clients uploading into one folder — the ordinary case, since the folder is the default
    /// target — wrote into the same file at their own offsets, and whichever sent its last part first unpacked whatever was in it and deleted
    /// it under the other.</remarks>
    public static string StagingPath(string path, string? client = null) =>
        $".snail-upload-{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{client}\n{path}")))[..16]}.tar";

    /// <summary>A tar unpacked into a folder of an area, file by file; folders arrive with their files, and macOS resource forks are left out.</summary>
    public async Task<VolumeWrite> UnpackTarAsync(string area, string path, Stream source, bool overwrite, CancellationToken cancellationToken)
    {
        var written = new List<Transferred>();
        await using var reader = new TarReader(source, leaveOpen: true);

        while (await reader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false) is { } entry)
        {
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || Relative(entry.Name) is not { } relative)
            {
                continue;
            }

            if (relative.Length == 0 || !AreaPaths.IsConfined(relative))
            {
                return new VolumeWrite(written, new BridgeError("BadRequest", $"the tar entry '{entry.Name}' leaves the folder it unpacks into"));
            }

            var target = path.Length == 0 ? relative : RemotePaths.Join(path, relative);
            var reply = await WriteAsync(area, target, entry.DataStream ?? Stream.Null, overwrite, cancellationToken).ConfigureAwait(false);

            if (!reply.IsOk)
            {
                return new VolumeWrite(written, reply.Error);
            }

            written.Add(new Transferred(entry.Name, target, reply.Result?["size"]?.GetValue<long>() ?? 0, reply.Result?["sha256"]?.GetValue<string>() ?? string.Empty));
        }

        return new VolumeWrite(written, null);
    }

    /// <summary>The entry's path inside the folder, or null for what a tar made on macOS adds beside real files.</summary>
    private static string? Relative(string name)
    {
        var relative = name.Replace('\\', '/');

        while (relative.StartsWith("./", StringComparison.Ordinal))
        {
            relative = relative[2..];
        }

        if (relative.StartsWith('/') || relative.Contains(':', StringComparison.Ordinal))
        {
            return "..";
        }

        return relative.Split('/')[^1].StartsWith("._", StringComparison.Ordinal) ? null : relative;
    }

    private static BridgeError Unreadable =>
        new(BridgeError.MalformedType, "the add-on answered file_list without the fields a download needs; blender_diagnose tells whether it is the build this server ships");
}
