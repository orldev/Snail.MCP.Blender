using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.MCP.Blender.Tools;
using Snail.Sdk.Mcp.Programs;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>A program against a real Blender: the build that would be twenty calls and twenty replies is one of each.</summary>
[Collection("Loopback")]
public sealed class ProgramLiveTests : IAsyncLifetime
{
    private HeadlessBlender _blender = null!;
    private BlenderConnection _link = null!;
    private ProgramRunner _runner = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _link = new BlenderConnection(_blender.Link, NullLogger<BlenderConnection>.Instance);
        var options = new ProgramOptions();
        TransportRegistrationExtensions.Programming(options);
        _runner = new ProgramRunner(options);
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

        var built = await RunAsync(code);

        Assert.True(built.Failure is null, built.Failure?.Error);
        Assert.Equal(5, built.Result!["made"]!.AsArray().Count);
        Assert.Equal(7, built.Calls.Count);
        Assert.Equal(["built 5"], built.Log);
    }

    /// <summary>The add-on answers a program's commands the way it answers a tool's, so a lease another agent holds stops it at that step.</summary>
    [BlenderFact]
    public async Task Program_TouchingALeasedObject_StopsThere_AndSaysWhichCommandFailed()
    {
        await using var lighting = new BlenderConnection(new BlenderLinkOptions { Port = _blender.Port, RequestTimeoutSeconds = 60, Agent = "lighting", TokenFile = _blender.Link.TokenFile }, NullLogger<BlenderConnection>.Instance);
        await lighting.SendAsync(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Hero" });
        await lighting.SendAsync(BridgeCommands.Lease, new JsonObject { ["action"] = "claim", ["names"] = new JsonArray("Hero") });

        var stopped = await RunAsync("""
            blender.add_primitive({ kind: "cube", name: "Mine" });
            blender.transform_object({ name: "Hero", location: [5, 0, 0] });
            return "moved";
            """);

        Assert.Equal("Leased", stopped.Failure!.Type);
        Assert.Equal(["add_primitive", "transform_object"], stopped.Calls.Select(call => call.Command));
    }

    private Task<ProgramRun> RunAsync(string code) =>
        _runner.RunAsync(new ProgramCommands(_link, new ServerConfig(), Access.Advice()), code, timeout: TimeSpan.FromSeconds(120));
}
