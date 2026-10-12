using BenchmarkDotNet.Attributes;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Domain;

namespace Snail.MCP.Blender.Benchmarks;

/// <summary>
/// find_tool, the call an agent makes before most others: the whole catalog read from the server's attributes once at
/// start, then searched by the words of an intent.
/// </summary>
[MemoryDiagnoser]
public class DiscoveryBenchmarks
{
    private readonly ToolIndex _index = new(typeof(Skill).Assembly);

    /// <summary>The catalog read from the assembly, as the server does once at start.</summary>
    [Benchmark]
    public int Index() => new ToolIndex(typeof(Skill).Assembly).Tools.Count;

    /// <summary>One search in plain words.</summary>
    [Benchmark]
    public int Search() => _index.Search("add a cube and give it a red glossy material", 5).Count;
}
