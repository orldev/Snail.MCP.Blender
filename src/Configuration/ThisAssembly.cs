using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Blender.Configuration;

/// <summary>The name and version this server announces, the same in initialize and in blender_diagnose, computed once at startup.</summary>
internal static class ThisAssembly
{
    /// <summary>The server's command, the name a client knows it by in <c>initialize</c>.</summary>
    public static string Name { get; } = ServerIdentity.NameOf(typeof(ThisAssembly).Assembly);

    /// <summary>The version of the build that is running, without the commit a build may append.</summary>
    public static string InformationalVersion { get; } = ServerIdentity.VersionOf(typeof(ThisAssembly).Assembly);
}
