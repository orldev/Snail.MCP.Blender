using Snail.MCP.Blender.Tools.Animation;

namespace Snail.MCP.Blender.Tests.Tools;

public class AnimationToolsTests
{
    private readonly ScriptedBridge _bridge = new();

    [Fact]
    public async Task InsertKeyframe_ValuesAndChannels_UseWireNames()
    {
        var tools = new KeyframeTools(_bridge);
        var values = new JsonObject { ["rotation_euler"] = new JsonArray(0, 0, 90) };

        (await tools.InsertAsync("Cube", frame: 24, values: values, interpolation: "LINEAR")).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.InsertKeyframe, _bridge.Sent.Single().Command);
        Assert.Equal(90, parameters["values"]!["rotation_euler"]![2]!.GetValue<int>());
        Assert.Equal("LINEAR", parameters["interpolation"]!.ToString());
        Assert.False(parameters.ContainsKey("channels"));
    }

    [Fact]
    public async Task SetVertexWeights_Pairs_BecomeNestedArrays()
    {
        var tools = new RigTools(_bridge);

        (await tools.VertexWeightsAsync("Body", "Spine", weights: [[0, 1], [3, 0.5]], mode: "ADD")).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(0.5, parameters["weights"]![1]![1]!.GetValue<double>());
        Assert.Equal("ADD", parameters["mode"]!.ToString());
        Assert.False(parameters.ContainsKey("all"));
    }

    [Fact]
    public async Task AddDriver_DefaultExpression_IsFrame()
    {
        var tools = new MotionTools(_bridge);

        (await tools.AddDriverAsync("Cube", "location", index: 2)).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal("frame", parameters["expression"]!.ToString());
        Assert.Equal(2, parameters["index"]!.GetValue<int>());
        Assert.Equal("location", parameters["data_path"]!.ToString());
    }

    [Fact]
    public async Task AddConstraint_OnABone_SendsBoneAndSettings()
    {
        var tools = new ConstraintTools(_bridge);

        (await tools.AddAsync("Rig", "IK", new JsonObject { ["target"] = "Rig", ["subtarget"] = "Hand.IK" }, bone: "Forearm")).Text();

        var parameters = _bridge.LastParameters!;

        Assert.Equal(BridgeCommands.AddConstraint, _bridge.Sent.Single().Command);
        Assert.Equal("Forearm", parameters["bone"]!.ToString());
        Assert.Equal("Hand.IK", parameters["settings"]!["subtarget"]!.ToString());
    }

    [Fact]
    public async Task ShapeKey_VertexOffsets_BecomeNestedArrays()
    {
        var tools = new ShapeKeyTools(_bridge);

        (await tools.AddAsync("Face", "Smile", vertices: [[3, 0, 0, 0.2]])).Text();

        Assert.Equal(0.2, _bridge.LastParameters!["vertices"]![0]![3]!.GetValue<double>());
        Assert.True(_bridge.LastParameters["relative"]!.GetValue<bool>());
    }
}
