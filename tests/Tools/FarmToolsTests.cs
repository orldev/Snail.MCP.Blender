using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.MCP.Blender.Tools.Farm;
using Snail.Sdk.Mcp.Hosting.Discovery;

namespace Snail.MCP.Blender.Tests.Tools;

public class FarmToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    private JobTools Tools(ServerConfig config) => new(_bridge, config, TestJobs.AddOn(_bridge));

    [Fact]
    public async Task RenderJob_LeavesTheJobsDirectoryToTheAddOn()
    {
        var config = new ServerConfig { DataDirectory = "/data/snail" };
        var tools = Tools(config);

        (await tools.StartAsync("/renders/{camera}_####", frames: "1-48x2", cameras: ["CamA", "CamB"], output: new JobOutput { FileFormat = "OPEN_EXR", ColorDepth = 16 })).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.RenderJob, _bridge.Sent.Single().Command);
        Assert.False(parameters.ContainsKey("directory"));
        Assert.Equal("1-48x2", parameters["frames"]!.ToString());
        Assert.Equal("CamB", parameters["cameras"]![1]!.ToString());
        Assert.True(parameters["overwrite"]!.GetValue<bool>());
    }

    /// <summary>A render outlives any idle time the skills are given, and the model still polls and cancels it with the farm's tools: the farm
    /// stays loaded while a render the server follows runs, and no other skill does.</summary>
    [Fact]
    public async Task FarmSkill_IsBusy_WhileARenderRuns_AndNoLongerOnceItEnds()
    {
        var skills = new SkillOptions();
        TransportRegistrationExtensions.Loading(skills, new ServerConfig { SkillIdleMinutes = 10 });
        var running = TestJobs.AddOn(new ScriptedBridge().Answer(BridgeCommands.RenderJob, new JsonObject { ["id"] = "job-1", ["state"] = "running" }));
        var ended = TestJobs.AddOn(new ScriptedBridge().Answer(BridgeCommands.RenderJob, new JsonObject { ["id"] = "job-2", ["state"] = "done" }));

        await running.StartRenderAsync(BridgeCommands.RenderJob, BridgeCommands.RenderJobStatus, [], CancellationToken.None);
        var finished = await ended.StartRenderAsync(BridgeCommands.RenderJob, BridgeCommands.RenderJobStatus, [], CancellationToken.None);
        await finished.Job!.ForAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        var whileRunning = new ServiceCollection().AddSingleton(running).BuildServiceProvider();
        var onceEnded = new ServiceCollection().AddSingleton(ended).BuildServiceProvider();

        Assert.True(SpinWait.SpinUntil(() => skills.IsBusy!(Skills.Farm, whileRunning), TimeSpan.FromSeconds(10)));
        Assert.False(skills.IsBusy!(Skills.Rendering, whileRunning));
        Assert.False(skills.IsBusy!(Skills.Farm, onceEnded));
        Assert.Equal("blender", skills.Prefix);
    }

    [Fact]
    public async Task RenderJob_ResolutionAboveTheLimit_IsRefused()
    {
        var tools = Tools(new ServerConfig());

        var response = JsonNode.Parse((await tools.StartAsync("/renders/f_####", output: new JobOutput { ResolutionX = 9000 })).Text())!;

        Assert.False(response["ok"]!.GetValue<bool>());
        Assert.Empty(_bridge.Sent);
    }

    [Fact]
    public async Task RenderJobs_ListsJobs_AndNeitherCallNamesADirectory()
    {
        var config = new ServerConfig { DataDirectory = "/data/snail" };
        var tools = Tools(config);

        (await tools.ListAsync()).Text();
        (await tools.CancelAsync("job-1")).Text();

        Assert.All(_bridge.Sent, sent => Assert.False(sent.Parameters!.ContainsKey("directory")));
        Assert.False(_bridge.Sent[0].Parameters!.ContainsKey("id"));
        Assert.Equal("job-1", _bridge.Sent[1].Parameters!["id"]!.ToString());
    }

    [Fact]
    public async Task RenderJob_UsesTheConfiguredOcio_WhenNoneIsPassed()
    {
        var tools = Tools(new ServerConfig { OcioConfig = "/ocio/config.ocio" });

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
