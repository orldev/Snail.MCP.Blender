using Snail.MCP.Blender.Application.Rendering;
using Snail.Sdk.Mcp.Hosting.Jobs;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>The jobs of a test server: the add-on's jobs followed over the scripted bridge, asked again only after an hour, so a test sees the first answer alone.</summary>
public static class TestJobs
{
    public static AddOnJobs AddOn(IBlenderBridge bridge, TimeProvider? clock = null) =>
        new(bridge, new Jobs("blender", clock), clock ?? TimeProvider.System) { Interval = TimeSpan.FromHours(1) };
}
