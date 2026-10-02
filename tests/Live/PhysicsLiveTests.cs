using Snail.MCP.Blender.Adapters.Blender;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>Physics against a real Blender: rigid bodies and hair added and baked, and a simulation removed again.</summary>
public sealed class PhysicsLiveTests : IAsyncLifetime
{
    private HeadlessBlender _blender = null!;
    private BlenderConnection _link = null!;
    private string _workDirectory = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _link = new BlenderConnection(_blender.Link, NullLogger<BlenderConnection>.Instance);
        _workDirectory = Path.Combine(Path.GetTempPath(), "snail-blender-live", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
    }

    public async Task DisposeAsync()
    {
        if (_link is not null) await _link.DisposeAsync();

        _blender?.Dispose();

        if (_workDirectory is not null && Directory.Exists(_workDirectory)) Directory.Delete(_workDirectory, recursive: true);
    }

    [BlenderFact]
    public async Task Physics_RigidBodiesAndHair_AreAddedAndBaked()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Falling", ["location"] = new JsonArray(0, 0, 5) });
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "plane", ["name"] = "Ground", ["size"] = 10 });
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "uv_sphere", ["name"] = "Head" });

        var active = await Send(BridgeCommands.AddPhysics, new JsonObject { ["name"] = "Falling", ["type"] = "rigid_body", ["settings"] = new JsonObject { ["mass"] = 2 } });
        Assert.Equal(2, active["rigid_body"]!["mass"]!.GetValue<double>());
        await Send(BridgeCommands.AddPhysics, new JsonObject { ["name"] = "Ground", ["type"] = "rigid_body_passive" });

        var hair = await Send(BridgeCommands.AddParticles, new JsonObject { ["name"] = "Head", ["type"] = "HAIR", ["count"] = 500, ["hair_length"] = 0.3 });
        Assert.Equal("HAIR", hair["type"]!.ToString());
        Assert.Equal(500, hair["count"]!.GetValue<int>());

        var baked = await Send(BridgeCommands.BakePhysics, new JsonObject { ["frame_end"] = 12 }, TimeSpan.FromSeconds(120));
        Assert.True(baked["baked"]!.GetValue<bool>());

        await Send(BridgeCommands.SetFrame, new JsonObject { ["frame"] = 12 });
        var fallen = await Send(BridgeCommands.MeshGeometry, new JsonObject { ["name"] = "Falling", ["world"] = true, ["limit"] = 1 });
        Assert.True(fallen["vertices"]![0]![3]!.GetValue<double>() < 5.5, "the rigid body did not fall after the bake");
    }

    [BlenderFact]
    public async Task Physics_Removed_LeavesTheObjectWithoutItsSimulation()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "uv_sphere", ["name"] = "Cloth" });
        await Send(BridgeCommands.AddPhysics, new JsonObject { ["name"] = "Cloth", ["type"] = "cloth" });

        var removed = await Send(BridgeCommands.RemovePhysics, new JsonObject { ["name"] = "Cloth", ["type"] = "cloth" });

        Assert.NotEmpty(removed["removed"]!.AsArray());
        Assert.DoesNotContain(removed["modifiers"]!.AsArray(), modifier => modifier!["type"]!.ToString() == "CLOTH");
    }

    private async Task<JsonNode> Send(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await _link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
