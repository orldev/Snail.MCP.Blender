using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Farm;

namespace Snail.MCP.Blender.Tests.Tools;

public class FarmToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    private static RenderJobs Jobs() =>
        new(new ClientSessions([], [], agent: null, new ServiceCollection().BuildServiceProvider(), TimeProvider.System));

    [Fact]
    public async Task RenderJob_LeavesTheJobsDirectoryToTheAddOn()
    {
        var config = new ServerConfig { DataDirectory = "/data/snail" };
        var tools = new JobTools(_bridge, config, Jobs());

        (await tools.StartAsync("/renders/{camera}_####", frames: "1-48x2", cameras: ["CamA", "CamB"], output: new JobOutput { FileFormat = "OPEN_EXR", ColorDepth = 16 })).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.RenderJob, _bridge.Sent.Single().Command);
        Assert.False(parameters.ContainsKey("directory"));
        Assert.Equal("1-48x2", parameters["frames"]!.ToString());
        Assert.Equal("CamB", parameters["cameras"]![1]!.ToString());
        Assert.True(parameters["overwrite"]!.GetValue<bool>());
    }

    [Fact]
    public async Task RenderJob_ResolutionAboveTheLimit_IsRefused()
    {
        var tools = new JobTools(_bridge, new ServerConfig(), Jobs());

        var response = JsonNode.Parse((await tools.StartAsync("/renders/f_####", output: new JobOutput { ResolutionX = 9000 })).Text())!;

        Assert.False(response["ok"]!.GetValue<bool>());
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task RenderJobStatus_WithoutAnId_ListsJobs_AndNeitherCallNamesADirectory()
    {
        var config = new ServerConfig { DataDirectory = "/data/snail" };
        var tools = new JobTools(_bridge, config, Jobs());

        (await tools.StatusAsync()).Text();
        (await tools.CancelAsync("job-1")).Text();

        Assert.All(_bridge.Sent, sent => Assert.False(sent.Parameters!.ContainsKey("directory")));
        Assert.False(_bridge.Sent[0].Parameters!.ContainsKey("id"));
        Assert.Equal("job-1", _bridge.Sent[1].Parameters!["id"]!.ToString());
    }

    [Fact]
    public async Task RenderJob_UsesTheConfiguredOcio_WhenNoneIsPassed()
    {
        var tools = new JobTools(_bridge, new ServerConfig { OcioConfig = "/ocio/config.ocio" }, Jobs());

        (await tools.StartAsync("/renders/f_####")).Text();

        Assert.Equal("/ocio/config.ocio", _bridge.LastParameters!["ocio_config"]!.ToString());
    }

    [Fact]
    public async Task RenderBudget_ProbeAndLimits_TravelWithARenderTimeout()
    {
        var tools = new BudgetTools(_bridge);

        (await tools.EstimateAsync(probe: new Probe { Percentage = 10 }, apply: new Limits { TextureLimit = "2048", Simplify = new Simplify { Subdivision = 2 } }, memoryLimitMb: 8000)).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.RenderBudget, _bridge.Sent.Single().Command);
        Assert.Equal("2048", parameters["apply"]!["texture_limit"]!.ToString());
        Assert.Equal(2, parameters["apply"]!["simplify"]!["subdivision"]!.GetValue<int>());
        Assert.Equal(8000, parameters["memory_limit_mb"]!.GetValue<int>());
        Assert.Equal(TimeSpan.FromSeconds(300), _bridge.Sent.Single().Timeout);
    }
}
