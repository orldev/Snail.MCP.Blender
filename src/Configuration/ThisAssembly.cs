using System.Reflection;

namespace Snail.MCP.Blender.Configuration;

/// <summary>Assembly name and version, computed once at startup.</summary>
internal static class ThisAssembly
{
    public static string Name { get; } = typeof(ThisAssembly).Assembly.GetName().Name ?? "Snail.MCP.Blender";

    public static string InformationalVersion { get; } =
        typeof(ThisAssembly).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? "0.0.0";
}
