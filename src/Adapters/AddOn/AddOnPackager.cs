using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Adapters.AddOn;

/// <summary>Zips the bundled add-on into the data directory with the manifest at the zip root, the layout Blender's Install from Disk expects.</summary>
public sealed partial class AddOnPackager(ServerConfig config) : IAddOnPackager
{
    private readonly Lock _packing = new();

    /// <summary>Writes the zip once at a time, through a name of its own, so two clients asking at once do not read half of one.</summary>
    /// <remarks>The zip is one path shared by every client of a server over HTTP. Deleting and rebuilding it in place handed one caller an
    /// IOException and the other a file that was still being written.</remarks>
    public AddOnPackage Pack()
    {
        var version = ReadVersion(File.ReadAllText(ServerPaths.BundledAddOnManifest));
        var directory = Path.Combine(config.DataDirectory, ServerPaths.AddOnFolderName);
        var zipPath = Path.Combine(directory, $"snail_bridge-{version}.zip");

        lock (_packing)
        {
            var pending = Path.Combine(directory, $"snail_bridge-{version}.{Environment.ProcessId}.zip");

            Directory.CreateDirectory(directory);
            File.Delete(pending);
            ZipFile.CreateFromDirectory(ServerPaths.BundledAddOnDirectory, pending, CompressionLevel.Optimal, includeBaseDirectory: false);
            File.Move(pending, zipPath, overwrite: true);
        }

        return new AddOnPackage(zipPath, version);
    }

    public AddOnFingerprint Describe() => Fingerprint(ServerPaths.BundledAddOnDirectory);

    /// <summary>Hashed the way the add-on hashes itself: the top-level .py and .json files in name order, each as its name in UTF-8 then its bytes.</summary>
    /// <remarks>The command schema is one of those files: it says what each command means, and a server reading one set of meanings while
    /// Blender acts on another is what this check exists to catch.</remarks>
    internal static AddOnFingerprint Fingerprint(string directory)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                     .Where(file => Path.GetExtension(file) is ".py" or ".json")
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            digest.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(file)));
            digest.AppendData(File.ReadAllBytes(file));
        }

        var manifest = Path.Combine(directory, ServerPaths.AddOnManifestFileName);

        return new AddOnFingerprint(
            File.Exists(manifest) ? ReadVersion(File.ReadAllText(manifest)) : "0.0.0",
            Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant()[..12]);
    }

    internal static string ReadVersion(string manifest) =>
        ManifestVersion().Match(manifest) is { Success: true } match ? match.Groups[1].Value : "0.0.0";

    [GeneratedRegex(@"^version\s*=\s*""([^""]+)""", RegexOptions.Multiline)]
    private static partial Regex ManifestVersion();
}
