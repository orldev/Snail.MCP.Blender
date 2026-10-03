using Snail.MCP.Blender.Adapters.Blender;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>The rest of the animation skill against a real Blender: an armature with skinning and a posed bone, keys edited after the fact, an F-Curve modifier, a driver, a path to follow, a constraint, shape keys and markers.</summary>
[Collection("Loopback")]
public sealed class AnimationLiveTests : IAsyncLifetime
{
    private HeadlessBlender _blender = null!;
    private BlenderConnection _link = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _link = new BlenderConnection(_blender.Link, NullLogger<BlenderConnection>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_link is not null) await _link.DisposeAsync();

        _blender?.Dispose();
    }

    [BlenderFact]
    public async Task Armature_TwoBonesSkinnedWeightedAndPosed_MoveTheMesh()
    {
        var armature = await Send(BridgeCommands.AddArmature, new JsonObject
        {
            ["name"] = "Rig",
            ["bones"] = new JsonArray(
                new JsonObject { ["name"] = "Root", ["head"] = new JsonArray(0, 0, 0), ["tail"] = new JsonArray(0, 0, 1) },
                new JsonObject { ["name"] = "Tip", ["head"] = new JsonArray(0, 0, 1), ["tail"] = new JsonArray(0, 0, 2), ["parent"] = "Root", ["connected"] = true }),
        });
        Assert.Equal(["Root", "Tip"], armature["bones"]!.AsArray().Select(bone => bone!["name"]!.ToString()).Order());

        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cylinder", ["name"] = "Skin", ["location"] = new JsonArray(0, 0, 1) });
        var parented = await Send(BridgeCommands.ParentToArmature, new JsonObject { ["name"] = "Skin", ["armature"] = "Rig", ["method"] = "automatic" });
        Assert.NotEmpty(parented["vertex_groups"]!.AsArray());

        var weighted = await Send(BridgeCommands.SetVertexWeights, new JsonObject { ["name"] = "Skin", ["group"] = "Root", ["all"] = 0.5 });
        Assert.True(weighted["assigned"]!.GetValue<int>() > 0);
        Assert.Contains(weighted["groups"]!.AsArray(), group => group!.ToString() == "Root");

        var posed = await Send(BridgeCommands.PoseBone, new JsonObject { ["armature"] = "Rig", ["bone"] = "Tip", ["rotation"] = new JsonArray(0, 30, 0), ["frame"] = 5 });
        Assert.Equal(30, posed["rotation"]![1]!.GetValue<double>(), 1);
        Assert.Contains(posed["keyed"]!.AsArray(), path => path!.ToString() == "rotation_euler");
    }

    [BlenderFact]
    public async Task Keys_InterpolatedMovedAndDeleted_LeaveTheCurvesEmpty()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Shifted" });
        await Send(BridgeCommands.InsertKeyframe, new JsonObject { ["name"] = "Shifted", ["frame"] = 1, ["values"] = new JsonObject { ["location"] = new JsonArray(0, 0, 0) } });
        await Send(BridgeCommands.InsertKeyframe, new JsonObject { ["name"] = "Shifted", ["frame"] = 10, ["values"] = new JsonObject { ["location"] = new JsonArray(3, 0, 0) } });

        var eased = await Send(BridgeCommands.SetInterpolation, new JsonObject { ["name"] = "Shifted", ["interpolation"] = "BEZIER", ["easing"] = "EASE_IN_OUT", ["channels"] = new JsonArray("location") });
        Assert.Equal(6, eased["keyframes"]!.GetValue<int>());

        var moved = await Send(BridgeCommands.MoveKeyframes, new JsonObject { ["name"] = "Shifted", ["offset"] = 12, ["channels"] = new JsonArray("location") });
        Assert.Equal(6, moved["moved"]!.GetValue<int>());

        var listed = await Send(BridgeCommands.ListKeyframes, new JsonObject { ["name"] = "Shifted", ["channels"] = new JsonArray("location") });
        Assert.Equal(13, listed["curves"]![0]!["keyframes"]![0]!["frame"]!.GetValue<double>(), 1);

        var deleted = await Send(BridgeCommands.DeleteKeyframes, new JsonObject { ["name"] = "Shifted", ["frame"] = 13, ["channels"] = new JsonArray("location") });
        Assert.Equal(3, deleted["removed"]!.GetValue<int>());

        var cleared = await Send(BridgeCommands.DeleteKeyframes, new JsonObject { ["name"] = "Shifted" });
        Assert.True(cleared["cleared"]!.GetValue<bool>());
    }

    [BlenderFact]
    public async Task Cycles_DriverAndFollowPath_KeepMovingWithoutMoreKeys()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Looper" });
        await Send(BridgeCommands.InsertKeyframe, new JsonObject { ["name"] = "Looper", ["frame"] = 1, ["values"] = new JsonObject { ["location"] = new JsonArray(0, 0, 0) } });
        await Send(BridgeCommands.InsertKeyframe, new JsonObject { ["name"] = "Looper", ["frame"] = 20, ["values"] = new JsonObject { ["location"] = new JsonArray(0, 0, 2) } });

        var cycled = await Send(BridgeCommands.AddFcurveModifier, new JsonObject { ["name"] = "Looper", ["type"] = "CYCLES", ["channels"] = new JsonArray("location") });
        Assert.Equal("CYCLES", cycled["type"]!.ToString());
        Assert.Equal(3, cycled["curves"]!.AsArray().Count);

        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Driven" });
        var driver = await Send(BridgeCommands.AddDriver, new JsonObject
        {
            ["name"] = "Driven",
            ["data_path"] = "location",
            ["index"] = 2,
            ["expression"] = "var * 2",
            ["variables"] = new JsonArray(new JsonObject { ["name"] = "var", ["object"] = "Looper", ["data_path"] = "location[2]" }),
        });
        Assert.Equal("var * 2", driver["expression"]!.ToString());
        Assert.Equal(["var"], driver["variables"]!.AsArray().Select(variable => variable!.ToString()));

        await Send(BridgeCommands.AddCurve, new JsonObject { ["kind"] = "path", ["name"] = "Track", ["size"] = 4 });
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cone", ["name"] = "Rider" });
        var following = await Send(BridgeCommands.FollowPath, new JsonObject { ["name"] = "Rider", ["curve"] = "Track", ["start"] = 1, ["duration"] = 48, ["forward_axis"] = "FORWARD_Y" });

        Assert.Equal(48, following["duration"]!.GetValue<int>());
        Assert.Equal("Track", following["curve"]!.ToString());
    }

    [BlenderFact]
    public async Task Constraints_TrackToCamera_IsListedOnTheObject()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cone", ["name"] = "Pointer" });
        await Send(BridgeCommands.AddCamera, new JsonObject { ["name"] = "Eye" });

        var added = await Send(BridgeCommands.AddConstraint, new JsonObject { ["name"] = "Pointer", ["type"] = "TRACK_TO", ["settings"] = new JsonObject { ["target"] = "Eye", ["track_axis"] = "TRACK_Z" } });
        Assert.Equal("Eye", added["settings"]!["target"]!.ToString());

        var info = await Send(BridgeCommands.ObjectInfo, new JsonObject { ["name"] = "Pointer" });
        Assert.Equal("TRACK_TO", info["constraints"]![0]!["type"]!.ToString());

        var described = await Send(BridgeCommands.DescribeConstraint, new JsonObject { ["type"] = "TRACK_TO" });
        Assert.Contains(described["settings"]!.AsArray(), setting => setting!["name"]!.ToString() == "track_axis");

        var updated = await Send(BridgeCommands.UpdateConstraint, new JsonObject
        {
            ["name"] = "Pointer",
            ["constraint"] = added["name"]!.ToString(),
            ["new_name"] = "Aim",
            ["influence"] = 0.5,
            ["enabled"] = false,
        });
        Assert.Equal("Aim", updated["name"]!.ToString());
        Assert.Equal(0.5, updated["influence"]!.GetValue<double>(), 3);
        Assert.False(updated["enabled"]!.GetValue<bool>());

        var removed = await Send(BridgeCommands.RemoveConstraint, new JsonObject { ["name"] = "Pointer", ["constraint"] = "Aim" });
        Assert.Equal("Aim", removed["removed"]!.ToString());
        Assert.Empty(removed["constraints"]!.AsArray());
    }

    [BlenderFact]
    public async Task ShapeKeysMarkersAndVertices_ChangeTheData()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Face" });

        var moved = await Send(BridgeCommands.MeshSetVertices, new JsonObject { ["name"] = "Face", ["vertices"] = new JsonArray(new JsonArray(0, 0, 0, 1)), ["relative"] = true });
        Assert.Equal(1, moved["moved"]!.GetValue<int>());
        var geometry = await Send(BridgeCommands.MeshGeometry, new JsonObject { ["name"] = "Face", ["limit"] = 8 });
        Assert.Equal(0, geometry["vertices"]![0]![3]!.GetValue<double>(), 3);

        var key = await Send(BridgeCommands.AddShapeKey, new JsonObject { ["name"] = "Face", ["key_name"] = "Smile", ["vertices"] = new JsonArray(new JsonArray(1, 0, 0, 0.3)) });
        Assert.Equal(["Basis", "Smile"], key["keys"]!.AsArray().Select(block => block!["name"]!.ToString()));

        var set = await Send(BridgeCommands.SetShapeKey, new JsonObject { ["name"] = "Face", ["key_name"] = "Smile", ["value"] = 1, ["frame"] = 10 });
        Assert.Equal(1, set["keys"]![1]!["value"]!.GetValue<double>());

        var markers = await Send(BridgeCommands.AddMarker, new JsonObject { ["frame"] = 10, ["marker_name"] = "Smile" });
        Assert.Equal("Smile", markers["markers"]![0]!["name"]!.ToString());
    }

    /// <summary>Every key of a channel goes in one call, however many there are on each curve.</summary>
    /// <remarks>The keys were removed while walking a list taken before the first removal; removing one moves the rest, and the second removal
    /// found its key gone and raised "Keyframe not in F-Curve". It surfaced twice on the production server.</remarks>
    [BlenderFact]
    public async Task Keyframes_ManyOnOneCurve_AreDeletedTogether()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Busy" });
        foreach (var frame in new[] { 1, 5, 10 })
        {
            await Send(BridgeCommands.InsertKeyframe, new JsonObject { ["name"] = "Busy", ["frame"] = frame, ["values"] = new JsonObject { ["location"] = new JsonArray(frame, 0, 0) } });
        }

        var deleted = await Send(BridgeCommands.DeleteKeyframes, new JsonObject { ["name"] = "Busy", ["channels"] = new JsonArray("location") });

        Assert.Equal(9, deleted["removed"]!.GetValue<int>());
        Assert.Equal(0, deleted["keyframes"]!.GetValue<int>());
    }

    private async Task<JsonNode> Send(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await _link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
