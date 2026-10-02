using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Adapters.Programs;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Base;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>A program against a real Blender: the build that would be twenty calls and twenty replies is one of each.</summary>
[Collection("Loopback")]
public sealed class ProgramLiveTests : IAsyncLifetime
{
    private HeadlessBlender _blender = null!;
    private BlenderConnection _link = null!;
    private ProgramTools _tools = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _link = new BlenderConnection(_blender.Link, NullLogger<BlenderConnection>.Instance);
        _tools = new ProgramTools(new JavaScriptPrograms(_link, new ServerConfig(), new ScriptAdvice(new ToolIndex(typeof(ProgramTools).Assembly)), Access.Scopes, Access.Sessions()), new BridgeTraffic());
    }

    public async Task DisposeAsync()
    {
        if (_link is not null) await _link.DisposeAsync();

        _blender?.Dispose();
    }

    [BlenderFact]
    public async Task Program_BuildingARowOfCubesAndReadingTheScene_IsOneCall()
    {
        const string code = """
            const made = [];
            for (let index = 0; index < 5; index++) {
              const cube = blender.add_primitive({ kind: "cube", name: "Crate" + index, location: [index * 2, 0, 0] });
              made.push(cube.name);
            }
            blender.select_objects({ names: made });
            const scene = blender.scene_info();
            log("built " + made.length);
            return { made: made, objects: scene.objects.length };
            """;

        var built = JsonNode.Parse((await _tools.ProgramAsync(code)).Text())!;

        Assert.True(built["ok"]!.GetValue<bool>(), built.ToJsonString());
        Assert.Equal(5, built["data"]!["result"]!["made"]!.AsArray().Count);
        Assert.Equal(7, built["data"]!["calls"]!.GetValue<int>());
        Assert.Equal(["built 5"], built["data"]!["log"]!.AsArray().Select(line => line!.ToString()));
    }

    /// <summary>The add-on answers a program's commands the way it answers a tool's, so a lease another agent holds stops it at that step.</summary>
    [BlenderFact]
    public async Task Program_TouchingALeasedObject_StopsThere_AndSaysWhichCommandFailed()
    {
        await using var lighting = new BlenderConnection(new BlenderLinkOptions { Port = _blender.Port, RequestTimeoutSeconds = 60, Agent = "lighting", TokenFile = _blender.Link.TokenFile }, NullLogger<BlenderConnection>.Instance);
        await lighting.SendAsync(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Hero" });
        await lighting.SendAsync(BridgeCommands.Lease, new JsonObject { ["action"] = "claim", ["names"] = new JsonArray("Hero") });

        var stopped = JsonNode.Parse((await _tools.ProgramAsync("""
            blender.add_primitive({ kind: "cube", name: "Mine" });
            blender.transform_object({ name: "Hero", location: [5, 0, 0] });
            return "moved";
            """)).Text())!;

        Assert.False(stopped["ok"]!.GetValue<bool>());
        Assert.Contains("Leased", stopped["error"]!.ToString(), StringComparison.Ordinal);
        Assert.Equal(["add_primitive", "transform_object"], stopped["data"]!["commands"]!.AsArray().Select(call => call!["command"]!.ToString()));
    }
}
