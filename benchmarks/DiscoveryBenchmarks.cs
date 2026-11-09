using System.Reflection;
using BenchmarkDotNet.Attributes;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Domain;
using Snail.Sdk.Mcp.Hosting.Discovery;

namespace Snail.MCP.Blender.Benchmarks;

/// <summary>
/// find_tool, the call an agent makes before most others: the whole catalog read as Snail.Sdk's discovery reads it, then
/// searched by the words of an intent with the server's own words dropped.
/// </summary>
[MemoryDiagnoser]
public class DiscoveryBenchmarks
{
    private static readonly DiscoveryOptions Options = new()
    {
        Prefix = "blender_",
        Noise = new HashSet<string>(["bpy", "ops"], StringComparer.OrdinalIgnoreCase),
    };

    private readonly IReadOnlyList<ToolEntry> _catalog = Catalog();

    /// <summary>The catalog read from the server's tool classes.</summary>
    [Benchmark]
    public int Index() => Catalog().Count;

    /// <summary>One search in plain words.</summary>
    [Benchmark]
    public int Search() => ToolCatalog.Find(_catalog, "add a cube and give it a red glossy material", 5, Options).Count;

    private static IReadOnlyList<ToolEntry> Catalog() =>
    [
        .. typeof(Scopes).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => ToolCatalog.Of(type)),
    ];
}
