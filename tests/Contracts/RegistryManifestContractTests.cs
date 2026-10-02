using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>The registry entry is where a client learns the settings without reading the documentation, so it names every one the server binds,
/// and a default it states is the one the code starts from.</summary>
public partial class RegistryManifestContractTests
{
    [Fact]
    public void EnvironmentVariables_NameEverySetting_WithTheDefaultTheCodeStartsFrom()
    {
        var manifest = JsonNode.Parse(File.ReadAllText(ManifestPath()))!;
        var listed = manifest["packages"]![0]!["environmentVariables"]!.AsArray()
            .ToDictionary(variable => variable!["name"]!.ToString(), variable => variable!["default"]?.ToString(), StringComparer.Ordinal);
        var settings = Settings(new ServerConfig(), []).ToDictionary(setting => setting.Variable, setting => setting.Default, StringComparer.Ordinal);

        Assert.Equal(settings.Keys.Append(EnvironmentSettings.ConfigPathVariable).Order(StringComparer.Ordinal), listed.Keys.Order(StringComparer.Ordinal));
        Assert.All(listed.Where(variable => variable.Value is not null), variable => Assert.Equal(settings[variable.Key], variable.Value, ignoreCase: true));
    }

    /// <summary>The registry lists the package by the version NuGet gets, so the manifest a package carries names the version it was packed with.</summary>
    /// <remarks>Neither file holds a release: both keep the placeholder a checkout builds with, and the release workflow writes the version of the
    /// tag into the assembly, the package and this manifest together. A package packed from the checkout carries the manifest as it stands, which
    /// is why the placeholders must agree.</remarks>
    [Fact]
    public void Version_IsTheVersionOfTheServerPackage()
    {
        var manifest = JsonNode.Parse(File.ReadAllText(ManifestPath()))!;
        var project = XDocument.Load(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(ManifestPath()))!, "src", "Snail.MCP.Blender.csproj"));
        var version = project.Descendants("Version").Single().Value;

        Assert.Equal(version, manifest["version"]!.ToString());
        Assert.All(manifest["packages"]!.AsArray(), package => Assert.Equal(version, package!["version"]!.ToString()));
    }

    /// <summary>Every property a settings file or a variable can set, walked into the option sections, as its variable name and its starting value.</summary>
    private static IEnumerable<(string Variable, string? Default)> Settings(object section, IReadOnlyList<string> path)
    {
        foreach (var property in section.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.SetMethod?.IsPublic == true))
        {
            var value = property.GetValue(section);
            var segments = path.Append(Screaming().Replace(property.Name, "_").ToUpperInvariant()).ToList();

            if (property.PropertyType.Namespace == typeof(ServerConfig).Namespace && property.PropertyType.IsClass)
            {
                foreach (var nested in Settings(value!, segments))
                {
                    yield return nested;
                }

                continue;
            }

            yield return ($"{EnvironmentSettings.Prefix}{string.Join("__", segments)}", value?.ToString());
        }
    }

    /// <summary>The manifest in the checkout, found above the test binary; the binary's folder carries a copy of the add-on but not the manifest.</summary>
    private static string ManifestPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, ".mcp", "server.json")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new FileNotFoundException($".mcp/server.json not found above {AppContext.BaseDirectory}")
            : Path.Combine(directory.FullName, ".mcp", "server.json");
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex Screaming();
}
