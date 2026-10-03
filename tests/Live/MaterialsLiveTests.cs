using Snail.MCP.Blender.Adapters.Blender;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>The rest of the materials skill against a real Blender: a material renamed and assigned by slot, the node graph edited node by node and in one call, and an image texture loaded from disk.</summary>
[Collection("Loopback")]
public sealed class MaterialsLiveTests : IAsyncLifetime
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
    public async Task Material_RenamedAndAssignedToASlot_IsListedWithItsUsers()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Body" });
        await Send(BridgeCommands.CreateMaterial, new JsonObject { ["name"] = "Paint" });

        var renamed = await Send(BridgeCommands.SetMaterial, new JsonObject { ["name"] = "Paint", ["new_name"] = "Coat", ["roughness"] = 0.25 });
        Assert.Equal("Coat", renamed["name"]!.ToString());
        Assert.Equal(0.25, renamed["principled"]!["roughness"]!.GetValue<double>(), 3);

        var assigned = await Send(BridgeCommands.AssignMaterial, new JsonObject { ["object"] = "Body", ["material"] = "Coat", ["slot"] = 0 });
        Assert.Equal(0, assigned["slot"]!.GetValue<int>());
        Assert.Equal(["Coat"], assigned["slots"]!.AsArray().Select(slot => slot!.ToString()));

        var listed = await Send(BridgeCommands.ListMaterials);
        Assert.Contains(listed["materials"]!.AsArray(), material => material!["name"]!.ToString() == "Coat" && material["users"]!.GetValue<int>() > 0);
    }

    /// <summary>Assigned without a slot, the object becomes the material: the faces point at it, and the render changes.</summary>
    /// <remarks>The call used to add a slot at the end and leave every face on slot 0. It answered ok, the slot list grew by one and nothing
    /// about the object looked different, which is the hardest kind of wrong answer to find. Growing the list is still what append asks for,
    /// and then the faces must stay where they were — checked here through the mesh itself, because the reply is what was doubted.</remarks>
    [BlenderFact]
    public async Task Material_AssignedWithoutASlot_MovesTheFacesOntoIt_AndAppendLeavesThemAlone()
    {
        await Send(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Crate" });
        await Send(BridgeCommands.CreateMaterial, new JsonObject { ["name"] = "Steel" });
        await Send(BridgeCommands.CreateMaterial, new JsonObject { ["name"] = "Rust" });

        var steel = await Send(BridgeCommands.AssignMaterial, new JsonObject { ["object"] = "Crate", ["material"] = "Steel" });
        var wearingSteel = await FaceSlots("Crate");

        Assert.Equal(0, steel["slot"]!.GetValue<int>());
        Assert.Equal(6, steel["faces_pointed_at_it"]!.GetValue<int>());
        Assert.Equal(["Steel"], steel["slots"]!.AsArray().Select(slot => slot!.ToString()));
        Assert.Equal([0], wearingSteel);

        var rust = await Send(BridgeCommands.AssignMaterial, new JsonObject { ["object"] = "Crate", ["material"] = "Rust", ["append"] = true });
        var stillWearingSteel = await FaceSlots("Crate");

        Assert.Equal(1, rust["slot"]!.GetValue<int>());
        Assert.Equal(0, rust["faces_pointed_at_it"]!.GetValue<int>());
        Assert.Equal(["Steel", "Rust"], rust["slots"]!.AsArray().Select(slot => slot!.ToString()));
        Assert.Equal([0], stillWearingSteel);
    }

    /// <summary>A Math node's three inputs are all called Value; the second and the third are reached by index or by identifier.</summary>
    /// <remarks>Looked up by name alone, only the first could ever be set, and the node's summary reported the three as one. An agent asked for
    /// input '1' three times on the production server and was refused each time. MULTIPLY_ADD is the operation that uses all three.</remarks>
    [BlenderFact]
    public async Task Node_InputsSharingAName_AreReachedByIndexAndByIdentifier()
    {
        await Send(BridgeCommands.CreateMaterial, new JsonObject { ["name"] = "Mathy" });
        await Send(BridgeCommands.AddNode, new JsonObject { ["material"] = "Mathy", ["type"] = "Math", ["name"] = "Power" });

        var set = await Send(BridgeCommands.SetNode, new JsonObject
        {
            ["material"] = "Mathy",
            ["node"] = "Power",
            ["properties"] = new JsonObject { ["operation"] = "MULTIPLY_ADD" },
            ["inputs"] = new JsonObject { ["Value"] = 2.0, ["1"] = 0.25, ["Value_002"] = 0.75 },
        });
        var inputs = set["inputs"]!.AsObject();

        Assert.Equal(2.0, inputs["Value"]!.GetValue<double>(), 3);
        Assert.Equal(0.25, inputs["Value_001"]!.GetValue<double>(), 3);
        Assert.Equal(0.75, inputs["Value_002"]!.GetValue<double>(), 3);

        var refused = await _link.SendAsync(BridgeCommands.SetNode, new JsonObject { ["material"] = "Mathy", ["node"] = "Power", ["inputs"] = new JsonObject { ["7"] = 1.0 } });

        Assert.Equal("UnknownParameter", refused.Error?.Type);
        Assert.Contains("Value (Value_001)", refused.Error!.Details!.ToJsonString(), StringComparison.Ordinal);
    }

    [BlenderFact]
    public async Task NodeGraph_NodeAddedRenamedLinkedAndRemoved_FollowsTheEdits()
    {
        await Send(BridgeCommands.CreateMaterial, new JsonObject { ["name"] = "Checked" });
        var principled = await Principled("Checked");

        var node = await Send(BridgeCommands.AddNode, new JsonObject { ["material"] = "Checked", ["type"] = "TexChecker", ["name"] = "Checks" });
        Assert.Equal("ShaderNodeTexChecker", node["type"]!.ToString());

        var renamed = await Send(BridgeCommands.SetNode, new JsonObject { ["material"] = "Checked", ["node"] = "Checks", ["new_name"] = "Checker", ["location"] = new JsonArray(-500, 200) });
        Assert.Equal("Checker", renamed["name"]!.ToString());
        Assert.Equal(-500, renamed["location"]![0]!.GetValue<int>());

        var linked = await Send(BridgeCommands.LinkNodes, new JsonObject
        {
            ["material"] = "Checked",
            ["from_node"] = "Checker",
            ["from_socket"] = "Color",
            ["to_node"] = principled,
            ["to_socket"] = "Base Color",
        });
        Assert.Contains("Base Color", linked["link"]!.ToString(), StringComparison.Ordinal);

        var removed = await Send(BridgeCommands.RemoveNode, new JsonObject { ["material"] = "Checked", ["node"] = "Checker" });
        Assert.Equal("Checker", removed["removed"]!.ToString());
        Assert.DoesNotContain(removed["nodes"]!.AsArray(), name => name!.ToString() == "Checker");
    }

    [BlenderFact]
    public async Task NodeGraph_BuiltInOneCall_CarriesItsOwnLinks()
    {
        await Send(BridgeCommands.CreateMaterial, new JsonObject { ["name"] = "Procedural" });

        var built = await Send(BridgeCommands.BuildNodeGraph, new JsonObject
        {
            ["material"] = "Procedural",
            ["nodes"] = new JsonArray(
                new JsonObject { ["id"] = "noise", ["type"] = "TexNoise", ["inputs"] = new JsonObject { ["Scale"] = 12 } },
                new JsonObject { ["id"] = "ramp", ["type"] = "ValToRGB" }),
            ["links"] = new JsonArray(new JsonArray("noise", "Fac", "ramp", "Fac")),
        });

        Assert.Equal(2, built["nodes"]!.AsArray().Count);
        Assert.Single(built["links"]!.AsArray());
        Assert.True(built["total_nodes"]!.GetValue<int>() >= 4);
    }

    [BlenderFact]
    public async Task ImageTexture_LoadedFromARenderedFile_IsWiredIntoBaseColour()
    {
        await Send(BridgeCommands.CreateMaterial, new JsonObject { ["name"] = "Printed" });
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 32, ["resolution_y"] = 32, ["samples"] = 1 });
        var rendered = await Send(BridgeCommands.RenderImage, new JsonObject { ["path"] = Path.Combine(_workDirectory, "texture.png") }, TimeSpan.FromSeconds(120));

        var texture = await Send(BridgeCommands.LoadImageTexture, new JsonObject
        {
            ["material"] = "Printed",
            ["path"] = rendered["path"]!.ToString(),
            ["colorspace"] = "sRGB",
            ["connect_to"] = "Base Color",
        });

        Assert.Equal("ShaderNodeTexImage", texture["node"]!["type"]!.ToString());
        Assert.Equal([32, 32], texture["image"]!["size"]!.AsArray().Select(size => size!.GetValue<int>()));
        Assert.Contains("Base Color", texture["link"]!.ToString(), StringComparison.Ordinal);
    }

    private async Task<string> Principled(string material)
    {
        var info = await Send(BridgeCommands.MaterialInfo, new JsonObject { ["name"] = material });

        return info["graph"]!["nodes"]!.AsArray().Single(node => node!["type"]!.ToString() == "ShaderNodeBsdfPrincipled")!["name"]!.ToString();
    }

    /// <summary>material_info counts as a read, so neither the journal nor the unsaved-changes flag sees it; it used to add a Principled BSDF to a
    /// material without one and wire it into the surface, disconnecting the shader the material had.</summary>
    [BlenderFact]
    public async Task MaterialInfo_OfAnEmissionShader_LeavesTheGraphAsItWas()
    {
        const string code = """
            import bpy
            material = bpy.data.materials.new("Glow")
            material.use_nodes = True
            tree = material.node_tree
            for node in list(tree.nodes):
                if node.type != "OUTPUT_MATERIAL":
                    tree.nodes.remove(node)
            emission = tree.nodes.new("ShaderNodeEmission")
            output = next(node for node in tree.nodes if node.type == "OUTPUT_MATERIAL")
            tree.links.new(emission.outputs["Emission"], output.inputs["Surface"])
            """;
        await Send(BridgeCommands.Python, new JsonObject { ["code"] = code });

        var info = await Send(BridgeCommands.MaterialInfo, new JsonObject { ["name"] = "Glow" });
        var graph = await Send(BridgeCommands.Python, new JsonObject { ["code"] = "import bpy\nresult = sorted(node.type for node in bpy.data.materials['Glow'].node_tree.nodes)" });

        Assert.Equal(["EMISSION", "OUTPUT_MATERIAL"], graph["result"]!.AsArray().Select(node => node!.ToString()));
        Assert.Null(info["principled"]);
    }

    /// <summary>The slots the object's faces point at, read from the mesh rather than from the reply that is being checked.</summary>
    private async Task<int[]> FaceSlots(string name)
    {
        var read = await Send(BridgeCommands.Python, new JsonObject
        {
            ["code"] = $"result = sorted({{polygon.material_index for polygon in bpy.data.objects['{name}'].data.polygons}})",
        });

        return [.. read["result"]!.AsArray().Select(slot => slot!.GetValue<int>())];
    }

    private async Task<JsonNode> Send(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await _link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
