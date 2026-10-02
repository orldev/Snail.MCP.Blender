namespace Snail.MCP.Blender.Configuration;

/// <summary>Paths where the server looks for its config, keeps its data and ships the add-on.</summary>
public static class ServerPaths
{
    public const string DataDirectoryName = ".snail-mcp-blender";

    public const string AddOnFolderName = "addon";

    public const string AddOnManifestFileName = "blender_manifest.toml";

    public static string Home => Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile,
        Environment.SpecialFolderOption.DoNotVerify);

    public static string DefaultDataDirectory => Path.Combine(Home, DataDirectoryName);

    /// <summary>The Blender extension shipped next to the server binary; installed into Blender by the user from a zip.</summary>
    public static string BundledAddOnDirectory => Path.Combine(AppContext.BaseDirectory, AddOnFolderName);

    public static string BundledAddOnManifest => Path.Combine(BundledAddOnDirectory, AddOnManifestFileName);

    /// <summary>Config lookup chain, highest priority first; a named path is the only candidate, so naming one that does not exist reads no file.</summary>
    public static IEnumerable<string> ConfigCandidates(string? named = null)
    {
        var fromEnvironment = named ?? Environment.GetEnvironmentVariable(EnvironmentSettings.ConfigPathVariable);

        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            yield return fromEnvironment;

            yield break;
        }

        var cwd = Directory.GetCurrentDirectory();

        yield return Path.Combine(cwd, ".snail-mcp-blender.json");

        yield return Path.Combine(cwd, "snail-mcp-blender.json");

        yield return Path.Combine(Home, ".config", "snail-mcp-blender", "config.json");

        yield return Path.Combine(Home, ".snail-mcp-blender.json");
    }

    /// <summary>First existing file from <see cref="ConfigCandidates"/>, or <c>null</c>.</summary>
    public static string? FindConfigFile(string? named = null) => ConfigCandidates(named).FirstOrDefault(File.Exists);
}
