using BenchmarkDotNet.Running;
using Snail.MCP.Blender.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(DiscoveryBenchmarks).Assembly).Run(args);
