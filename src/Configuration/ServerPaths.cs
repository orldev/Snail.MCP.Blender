using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Blender.Configuration;

/// <summary>Paths where the server keeps its data and ships the add-on; its settings file is found by Snail.Sdk's <see cref="ServerSettings"/>.</summary>
public static class ServerPaths
{
    /// <summary>The server's short name, which names its folders: ~/.s3l-mcp-blender and ~/.config/s3l-mcp-blender.</summary>
    public const string ShortName = "blender";

    public const string AddOnFolderName = "addon";

    public const string AddOnManifestFileName = "blender_manifest.toml";

    public static string Home => Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile,
        Environment.SpecialFolderOption.DoNotVerify);

    /// <summary><c>~/.s3l-mcp-blender</c>, moved there from <c>~/.snail-mcp-blender</c> when only that one exists.</summary>
    public static string DefaultDataDirectory => ServerFolders.DataDirectory(ShortName);

    /// <summary>The Blender extension shipped next to the server binary; installed into Blender by the user from a zip.</summary>
    public static string BundledAddOnDirectory => Path.Combine(AppContext.BaseDirectory, AddOnFolderName);

    public static string BundledAddOnManifest => Path.Combine(BundledAddOnDirectory, AddOnManifestFileName);
}
