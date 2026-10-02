using Snail.MCP.Blender.Adapters.Blender;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>The rest of the modeling skill against a real Blender: the bmesh edits, the modifier stack, curves, and the object operations that rewrite data rather than transforms, and a geometry-nodes tree.</summary>
[Collection("Loopback")]
public sealed class ModelingLiveTests : IAsyncLifetime
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
    public async Task MeshEdits_InsetBevelSubdivideNormalsShadeAndMerge_RewriteTheMesh()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Block" });
        await Send(BridgeCommands.MeshSelect, new JsonObject { ["name"] = "Block", ["mode"] = "by_normal", ["axis"] = "+z" });

        var inset = await Send(BridgeCommands.MeshInset, new JsonObject { ["name"] = "Block", ["thickness"] = 0.2, ["depth"] = 0.05 });
        Assert.True(inset["inset_faces"]!.GetValue<int>() > 0);

        var bevelled = await Send(BridgeCommands.MeshBevel, new JsonObject { ["name"] = "Block", ["width"] = 0.05, ["segments"] = 2 });
        Assert.True(bevelled["bevel_faces"]!.GetValue<int>() > 0);

        var subdivided = await Send(BridgeCommands.MeshSubdivide, new JsonObject { ["name"] = "Block", ["cuts"] = 1 });
        Assert.True(subdivided["split_edges"]!.GetValue<int>() > 0);

        var normals = await Send(BridgeCommands.MeshNormals, new JsonObject { ["name"] = "Block", ["inside"] = true });
        Assert.True(normals["inside"]!.GetValue<bool>());
        Assert.True(normals["recalculated"]!.GetValue<int>() > 0);

        var shaded = await Send(BridgeCommands.MeshShade, new JsonObject { ["name"] = "Block", ["smooth"] = true, ["angle"] = 30 });
        Assert.True(shaded["smooth"]!.GetValue<bool>());

        var merged = await Send(BridgeCommands.MeshMerge, new JsonObject { ["name"] = "Block", ["distance"] = 1.0 });
        Assert.True(merged["merged_vertices"]!.GetValue<int>() > 0);
    }

    [BlenderFact]
    public async Task Modifiers_UpdatedRenamedAndRemoved_AreDescribedByTheirType()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Rod" });
        var added = await Send(BridgeCommands.AddModifier, new JsonObject { ["name"] = "Rod", ["type"] = "SUBSURF", ["settings"] = new JsonObject { ["levels"] = 1 } });

        var updated = await Send(BridgeCommands.UpdateModifier, new JsonObject
        {
            ["name"] = "Rod",
            ["modifier"] = added["name"]!.ToString(),
            ["new_name"] = "Smoothing",
            ["settings"] = new JsonObject { ["levels"] = 2 },
        });
        Assert.Equal("Smoothing", updated["name"]!.ToString());
        Assert.Equal(2, updated["settings"]!["levels"]!.GetValue<int>());

        var described = await Send(BridgeCommands.DescribeModifier, new JsonObject { ["type"] = "SUBSURF" });
        Assert.Contains(described["settings"]!.AsArray(), setting => setting!["name"]!.ToString() == "levels");

        var types = await Send(BridgeCommands.DescribeModifier);
        Assert.Contains(types["types"]!.AsArray(), entry => entry!["type"]!.ToString() == "SUBSURF");

        var removed = await Send(BridgeCommands.RemoveModifier, new JsonObject { ["name"] = "Rod", ["modifier"] = "Smoothing" });
        Assert.Equal("Smoothing", removed["removed"]!.ToString());
        Assert.Empty(removed["modifiers"]!.AsArray());
    }

    [BlenderFact]
    public async Task Curves_CircleShapedAndPolyFromPoints_ConvertIntoAMesh()
    {
        var ring = await Send(BridgeCommands.AddCurve, new JsonObject { ["kind"] = "circle", ["name"] = "Ring", ["size"] = 2, ["bevel_depth"] = 0.1 });
        Assert.Equal("Ring", ring["name"]!.ToString());
        Assert.Equal(0.1, ring["bevel_depth"]!.GetValue<double>(), 3);

        var shaped = await Send(BridgeCommands.SetCurve, new JsonObject { ["name"] = "Ring", ["extrude"] = 0.2, ["resolution_u"] = 6, ["fill_mode"] = "FULL" });
        Assert.Equal(6, shaped["resolution_u"]!.GetValue<int>());
        Assert.Equal(0.2, shaped["extrude"]!.GetValue<double>(), 3);

        var rail = await Send(BridgeCommands.AddCurve, new JsonObject
        {
            ["kind"] = "poly",
            ["name"] = "Rail",
            ["points"] = new JsonArray(new JsonArray(0, 0, 0), new JsonArray(2, 0, 0), new JsonArray(2, 2, 0)),
        });
        Assert.Equal(3, rail["splines"]![0]!["points"]!.GetValue<int>());

        var mesh = await Send(BridgeCommands.ConvertToMesh, new JsonObject { ["name"] = "Ring" });
        Assert.True(mesh["vertices"]!.GetValue<int>() > 0);
        Assert.True(mesh["faces"]!.GetValue<int>() > 0);
    }

    [BlenderFact]
    public async Task Objects_JoinedOriginRecentredAndScaleApplied_LandInTheData()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Left" });
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Right", ["location"] = new JsonArray(3, 0, 0) });

        var joined = await Send(BridgeCommands.JoinObjects, new JsonObject { ["names"] = new JsonArray("Left", "Right"), ["target"] = "Left" });
        Assert.Equal(16, joined["vertices"]!.GetValue<int>());

        var recentred = await Send(BridgeCommands.SetOrigin, new JsonObject { ["name"] = "Left", ["type"] = "ORIGIN_GEOMETRY", ["center"] = "MEDIAN" });
        Assert.Equal(1.5, recentred["location"]![0]!.GetValue<double>(), 1);

        await Send(BridgeCommands.TransformObject, new JsonObject { ["name"] = "Left", ["scale"] = new JsonArray(2, 2, 2) });
        var applied = await Send(BridgeCommands.ApplyTransforms, new JsonObject { ["name"] = "Left", ["scale"] = true, ["rotation"] = false, ["location"] = false });
        Assert.Equal(1, applied["scale"]![0]!.GetValue<double>(), 3);
        Assert.Equal(0, applied["rotation_euler"]![2]!.GetValue<double>(), 3);
    }

    [BlenderFact]
    public async Task GeometryNodes_AnInterfaceInput_IsSetOnTheModifier()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Gen" });
        await Send(BridgeCommands.BuildGeometryNodes, new JsonObject
        {
            ["name"] = "Gen",
            ["interface_inputs"] = new JsonArray(new JsonObject { ["name"] = "Level", ["socket_type"] = "NodeSocketInt" }),
        });

        var inputs = await Send(BridgeCommands.SetGeometryInputs, new JsonObject { ["name"] = "Gen", ["inputs"] = new JsonObject { ["Level"] = 3 } });

        Assert.Equal(3, inputs["inputs"]!["Level"]!.GetValue<int>());
    }

    [BlenderFact]
    public async Task GeometryNodes_SubdivideTree_IsBuiltAndLinked()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Procedural" });

        var built = await Send(BridgeCommands.BuildGeometryNodes, new JsonObject
        {
            ["name"] = "Procedural",
            ["nodes"] = new JsonArray(new JsonObject { ["id"] = "sub", ["type"] = "SubdivideMesh", ["inputs"] = new JsonObject { ["Level"] = 2 } }),
            ["links"] = new JsonArray(new JsonArray("Group Input", "Geometry", "sub", "Mesh"), new JsonArray("sub", "Mesh", "Group Output", "Geometry")),
            ["clear"] = true,
        });

        Assert.Equal(["sub"], built["created"]!.AsArray().Select(node => node!.ToString()));
        Assert.Equal(2, built["links"]!.AsArray().Count);
        Assert.Contains(built["nodes"]!.AsArray(), node => node!["type"]!.ToString() == "GeometryNodeSubdivideMesh");
    }

    private async Task<JsonNode> Send(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await _link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
