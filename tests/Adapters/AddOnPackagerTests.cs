using System.IO.Compression;
using Snail.MCP.Blender.Adapters.AddOn;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Adapters;

public class AddOnPackagerTests
{
    /// <summary>Blender's Install from Disk reads the manifest at the zip root; a zip with a top-level folder is refused.</summary>
    [Fact]
    public void Pack_BundledAddOn_ZipsWithTheManifestAtTheRoot()
    {
        var config = new ServerConfig { DataDirectory = Path.Combine(Path.GetTempPath(), "snail-blender-tests", Guid.NewGuid().ToString("N")) };

        try
        {
            var package = new AddOnPackager(config).Pack();

            using var zip = ZipFile.OpenRead(package.ZipPath);
            var entries = zip.Entries.Select(entry => entry.FullName).ToList();

            Assert.Contains(ServerPaths.AddOnManifestFileName, entries);
            Assert.Contains("__init__.py", entries);
            Assert.EndsWith($"snail_bridge-{package.Version}.zip", package.ZipPath, StringComparison.Ordinal);
            Assert.Matches(@"^\d+\.\d+\.\d+$", package.Version);
        }
        finally
        {
            if (Directory.Exists(config.DataDirectory)) Directory.Delete(config.DataDirectory, recursive: true);
        }
    }

    [Fact]
    public void ReadVersion_ManifestText_PicksTheVersionLine() =>
        Assert.Equal("1.4.2", AddOnPackager.ReadVersion("schema_version = \"1.0.0\"\nid = \"x\"\nversion = \"1.4.2\"\n"));
}
