using Snail.MCP.Blender.Tools.Modeling;

namespace Snail.MCP.Blender.Tests.Tools;

public class ModelingToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    [Fact]
    public async Task AddModifier_SettingsObject_IsForwardedUntouched()
    {
        var tools = new ModifierTools(_bridge);
        var settings = new JsonObject { ["operation"] = "DIFFERENCE", ["object"] = "Cutter" };

        (await tools.AddAsync("Cube", "BOOLEAN", settings, apply: true)).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.AddModifier, _bridge.Sent.Single().Command);
        Assert.Equal("Cutter", parameters["settings"]!["object"]!.ToString());
        Assert.True(parameters["apply"]!.GetValue<bool>());
        Assert.False(parameters.ContainsKey("modifier_name"));
    }

    [Fact]
    public async Task MeshSelect_ByIndex_SendsIndexArraysAndSkipsAbsentOnes()
    {
        var tools = new MeshTools(_bridge);

        (await tools.SelectAsync("Cube", "by_index", faces: [0, 5])).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal([0, 5], parameters["faces"]!.AsArray().Select(node => node!.GetValue<int>()));
        Assert.False(parameters.ContainsKey("vertices"));
        Assert.False(parameters["extend"]!.GetValue<bool>());
    }

    [Fact]
    public async Task AddCurve_Points_BecomeNestedArrays()
    {
        var tools = new CurveTools(_bridge);

        (await tools.AddAsync("poly", points: [[0, 0, 0], [1, 2, 3]], closed: true, bevelDepth: 0.1)).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(3, parameters["points"]![1]![2]!.GetValue<double>());
        Assert.Equal(0.1, parameters["bevel_depth"]!.GetValue<double>());
        Assert.True(parameters["closed"]!.GetValue<bool>());
    }

    [Fact]
    public async Task JoinObjects_WithoutTarget_LeavesTargetOut()
    {
        var tools = new GeometryTools(_bridge);

        (await tools.JoinAsync(["A", "B"])).Text();

        Assert.Equal(["A", "B"], _bridge.LastParameters!["names"]!.AsArray().Select(node => node!.ToString()));
        Assert.False(_bridge.LastParameters.ContainsKey("target"));
    }

    [Fact]
    public async Task GeometryNodes_SpecAndInputs_AreForwarded()
    {
        var tools = new GeometryNodeTools(_bridge);
        var nodes = new JsonArray(new JsonObject { ["id"] = "sub", ["type"] = "SubdivideMesh" });

        (await tools.BuildAsync("Cube", nodes, inputs: new JsonObject { ["Count"] = 3 }, clear: true)).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal("SubdivideMesh", parameters["nodes"]![0]!["type"]!.ToString());
        Assert.Equal(3, parameters["inputs"]!["Count"]!.GetValue<int>());
        Assert.True(parameters["clear"]!.GetValue<bool>());
    }
}
