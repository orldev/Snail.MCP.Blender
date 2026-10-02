using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Application.Access;

namespace Snail.MCP.Blender.Application.Transfer;

/// <summary>A shell command for the client's machine, and the moment the link inside it stops working.</summary>
public sealed record ClientCommand(string Command, DateTimeOffset Expires);

/// <summary>The commands a client runs to move files through the HTTP server, which sees none of the client's folders.</summary>
/// <remarks>curl, tar and dd ship with macOS, Linux and Windows 10 and later. A folder travels as one tar, so a sequence of frames is one
/// upload instead of one per frame. Paths are single-quoted for the POSIX shell an agent's shell tool runs.</remarks>
public sealed class ClientCommands(FileLinks links, ServerConfig config)
{
    /// <summary>The size of one request of an upload, as the settings ask for it.</summary>
    /// <remarks>A reverse proxy may stop reading a body after a minute — Traefik's default — and a 160 MB upload over a 2 MB/s uplink broke
    /// off at exactly 60 seconds. At 16 MB a part arrives within 32 seconds even at half a megabyte per second, which is the default; a
    /// client on a slower uplink needs a smaller one, and that is a property of the network rather than of this code.</remarks>
    public int UploadPartBytes => config.Http.UploadPartMb * 1024 * 1024;

    /// <summary>A file or a folder of the client's into a path of the files area, in parts, the whole checked by SHA-256 at the end; the command
    /// itself tells a file from a folder, which it packs into a temporary tar first.</summary>
    public ClientCommand Upload(string localPath, string remotePath, bool overwrite)
    {
        var file = links.For("PUT", VolumeAreas.Files, remotePath, overwrite: overwrite);
        var folder = links.For("PUT", VolumeAreas.Files, remotePath, FileLinks.TarFormat, overwrite);

        var part = UploadPartBytes;

        string[] script =
        [
            $"s={Quote(localPath)}",
            "t=''",
            $"if [ -d \"$s\" ]; then t=$(mktemp) && COPYFILE_DISABLE=1 tar -C \"$s\" -cf \"$t\" . || exit 1; f=$t; u={Quote(folder.Url)}; else f=$s; u={Quote(file.Url)}; fi",
            "size=$(wc -c < \"$f\" | tr -d ' ')",
            "sum=$( (sha256sum \"$f\" 2>/dev/null || shasum -a 256 \"$f\") | cut -d ' ' -f 1)",
            "o=0",
            $"while :; do last=false; [ $((o + {part})) -ge \"$size\" ] && last=true; " +
            $"dd if=\"$f\" bs={part} skip=$((o / {part})) count=1 2>/dev/null | curl -fsS -T - \"$u&offset=$o&last=$last&sha256=$sum\" > /dev/null || {{ [ -n \"$t\" ] && rm -f \"$t\"; exit 1; }}; " +
            $"[ \"$last\" = true ] && break; o=$((o + {part})); done",
            "[ -n \"$t\" ] && rm -f \"$t\"",
            "echo \"uploaded $size bytes\"",
        ];

        return new ClientCommand($"({string.Join("; ", script)})", file.Expires);
    }

    /// <summary>A file of the files area to a local file, or a folder of it unpacked into a local folder; files that exist are kept unless
    /// <paramref name="overwrite"/>.</summary>
    /// <remarks>The server breaks a transfer off when a file differs from the digest the add-on computed, so the command must fail on curl's exit
    /// and not only on tar's: a file lands under a partial name first, and a folder's pipe remembers curl's status. A folder unpacks into a
    /// hidden folder inside the target and its files move in only once curl and tar have both succeeded: unpacked in place, a file cut short
    /// stayed under its own name, and a retry that keeps existing files kept it and reported success.</remarks>
    public ClientCommand Download(string remotePath, string localPath, bool isFolder, bool overwrite)
    {
        if (!isFolder)
        {
            var file = links.For("GET", VolumeAreas.Files, remotePath);

            string[] single =
            [
                $"t={Quote(localPath)}",
                overwrite ? ":" : "if [ -e \"$t\" ]; then echo \"$t already exists; call blender_download again with overwrite to replace it\" >&2; exit 1; fi",
                "mkdir -p \"$(dirname \"$t\")\" || exit 1",
                $"curl -fsSL -o \"$t.part\" {Quote(file.Url)} || {{ rm -f \"$t.part\"; exit 1; }}",
                "mv -f \"$t.part\" \"$t\"",
                "echo \"downloaded $t\"",
            ];

            return new ClientCommand($"({string.Join("; ", single)})", file.Expires);
        }

        var folder = links.For("GET", VolumeAreas.Files, remotePath, FileLinks.TarFormat);

        string[] script =
        [
            $"d={Quote(localPath)}",
            "mkdir -p \"$d\" || exit 1",
            "w=$(mktemp -d \"$d/.snail-download-XXXXXX\") || exit 1",
            "s=$(mktemp)",
            $"{{ curl -fsSL {Quote(folder.Url)}; echo $? > \"$s\"; }} | tar -xf - -C \"$w\"",
            "r=$?; c=$(cat \"$s\"); rm -f \"$s\"",
            "[ \"$c\" = 0 ] || { rm -rf \"$w\"; echo \"the download stopped: curl exit $c\" >&2; exit 1; }",
            "[ \"$r\" = 0 ] || { rm -rf \"$w\"; exit 1; }",
            $"(cd \"$w\" && find . -type f) | while IFS= read -r f; do f=${{f#./}}; mkdir -p \"$d/$(dirname \"$f\")\" && {(overwrite ? "mv -f \"$w/$f\" \"$d/$f\"" : "{ [ -e \"$d/$f\" ] || mv \"$w/$f\" \"$d/$f\"; }")}; done",
            "rm -rf \"$w\"",
            "echo \"downloaded into $d\"",
        ];

        return new ClientCommand($"({string.Join("; ", script)})", folder.Expires);
    }

    /// <summary>Where a download lands when the caller names no place: the path under the project's folder, relative to the client's folder of the same name.</summary>
    public static string LocalTarget(string remotePath, bool isFolder)
    {
        var segments = remotePath.Trim('/').Split('/');

        return segments.Length > 1 ? string.Join('/', segments[1..]) : isFolder ? "." : segments[0];
    }

    private static string Quote(string value) => $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
}
