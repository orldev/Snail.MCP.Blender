using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Base;

namespace Snail.MCP.Blender.Tests.Tools;

/// <summary>What a key opens is asked of the work, not of the tool that carries it.</summary>
public class ToolScopesTests
{
    private readonly ScriptedBridge _bridge = new ScriptedBridge()
        .Answer(BridgeCommands.Run, new JsonObject { ["steps"] = new JsonArray() })
        .Answer(BridgeCommands.BatchJob, new JsonObject { ["id"] = "batch-1" })
        .Answer(BridgeCommands.AddPrimitive, new JsonObject { ["name"] = "Crate" });

    /// <summary>blender_run is one tool with one annotation and carries whatever it is handed: a key without the Python scope used to run Python
    /// by sending it as a step.</summary>
    [Fact]
    public async Task Run_CarryingAPythonStep_IsRefusedForAKeyThatDoesNotOpenPython()
    {
        var (clients, serving) = Access.Serving("lighting", Scopes.Read, Scopes.Write, Scopes.Delete);
        using var _ = serving;
        var tools = new SequenceTools(_bridge, new ServerConfig { Python = PythonAccess.Allow }, Advice(), Access.Scopes, clients);

        var refused = await tools.RunAsync([
            new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cube" } },
            new JsonObject { ["command"] = "python", ["params"] = new JsonObject { ["code"] = "import os" } },
        ]);

        Assert.True(refused.Failed());
        Assert.Contains("does not open it", refused.Text(), StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    /// <summary>A batch runs its steps in a Blender of its own, which is no reason for its steps to be anyone's to run.</summary>
    [Fact]
    public async Task Batch_CarryingADeleteStep_IsRefusedForAKeyThatOnlyWrites()
    {
        var (clients, serving) = Access.Serving("modeling", Scopes.Read, Scopes.Write, Scopes.Farm);
        using var _ = serving;
        var tools = new TeamTools(_bridge, new ServerConfig(), Advice(), Access.Scopes, clients);

        var refused = await tools.BatchAsync([new JsonObject { ["command"] = "delete_objects", ["params"] = new JsonObject { ["names"] = new JsonArray("Cube") } }]);

        Assert.True(refused.Failed());
        Assert.Contains("delete work", refused.Text(), StringComparison.Ordinal);
        Assert.Empty(_bridge.Sent);
    }

    /// <summary>A key that opens everything the steps need still goes through; the check is about the work, not about the tool.</summary>
    [Fact]
    public async Task Run_CarryingOnlyWork_TheKeyOpens_IsSent()
    {
        var (clients, serving) = Access.Serving("lighting", Scopes.Read, Scopes.Write);
        using var _ = serving;
        var tools = new SequenceTools(_bridge, new ServerConfig(), Advice(), Access.Scopes, clients);

        var ran = await tools.RunAsync([new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cube" } }]);

        Assert.False(ran.Failed());
        Assert.Equal([BridgeCommands.Run.Name], _bridge.Sent.Select(sent => sent.Command.Name));
    }

    /// <summary>Clearing another agent's lease takes work away, which is what the delete scope is named for.</summary>
    [Fact]
    public void Lease_IsDeleteWork_NotWrite()
    {
        Assert.Equal(Scopes.Delete, Access.Scopes.For(BridgeCommands.Lease.Name));
        Assert.Equal(Scopes.Python, Access.Scopes.For(BridgeCommands.Python.Name));
        Assert.Equal(Scopes.Python, Access.Scopes.For(BridgeCommands.RunOperator.Name));
        Assert.Equal(Scopes.Farm, Access.Scopes.For(BridgeCommands.RenderJob.Name));
        Assert.Equal(Scopes.Read, Access.Scopes.For(BridgeCommands.SceneInfo.Name));
        Assert.Equal(Scopes.Delete, Access.Scopes.For(BridgeCommands.DeleteObjects.Name));
        Assert.Equal(Scopes.Write, Access.Scopes.For(BridgeCommands.AddPrimitive.Name));
    }

    private static ScriptAdvice Advice() => new(new ToolIndex(typeof(SequenceTools).Assembly));
}
