using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Sessions;
using Snail.Sdk.Mcp.Hosting.Jobs;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>The jobs of a test server: the add-on's jobs followed over the scripted bridge, asked again only after an hour, so a test sees the first answer alone.</summary>
public static class TestJobs
{
    public static RenderJobs Tracked() =>
        new(new ClientSessions([], [], agent: null, new ServiceCollection().BuildServiceProvider(), TimeProvider.System));

    public static AddOnJobs AddOn(IBlenderBridge bridge, RenderJobs? tracked = null, TimeProvider? clock = null) =>
        new(bridge, new Jobs("blender", clock), tracked ?? Tracked(), clock ?? TimeProvider.System) { Interval = TimeSpan.FromHours(1) };
}
