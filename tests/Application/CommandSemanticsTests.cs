using Snail.MCP.Blender.Application.Discovery;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>What a command means while the server runs, read from the schema the add-on and the server share.</summary>
public class CommandSemanticsTests
{
    private readonly CommandSemantics _semantics = new();

    [Fact]
    public void TheMeaningOfACommand_IsReadFromTheBundledSchema()
    {
        Assert.True(_semantics.IsKnown);
        Assert.True(_semantics.IsReadOnly(BridgeCommands.SceneInfo.Name));
        Assert.False(_semantics.IsReadOnly(BridgeCommands.DeleteObjects.Name));
        Assert.True(_semantics.IsDestructive(BridgeCommands.DeleteObjects.Name));
        Assert.False(_semantics.IsDestructive(BridgeCommands.SceneInfo.Name));
    }

    /// <summary>Reaching past Blender is its own question: a command may be harmless to the scene and still write to the file system or start a process.</summary>
    [Fact]
    public void ACommandThatReachesPastBlender_IsOpenWorld_WhereAnOrdinaryOneIsNot()
    {
        Assert.True(_semantics.IsOpenWorld(BridgeCommands.Python.Name));
        Assert.False(_semantics.IsOpenWorld(BridgeCommands.SceneInfo.Name));
    }

    /// <summary>Without the schema every command is unknown, and the server says so rather than refusing to start or inventing an answer.</summary>
    [Fact]
    public void WithoutTheSchema_NothingIsKnown_AndEveryFlagIsFalse()
    {
        var semantics = new CommandSemantics(Path.Combine(TestFolder.Create(), "no-schema"));

        Assert.False(semantics.IsKnown);
        Assert.False(semantics.IsReadOnly(BridgeCommands.SceneInfo.Name));
        Assert.False(semantics.IsDestructive(BridgeCommands.DeleteObjects.Name));
        Assert.False(semantics.IsOpenWorld(BridgeCommands.Python.Name));
    }

    [Fact]
    public void ACommandTheSchemaDoesNotName_CarriesNoMeaning()
    {
        Assert.False(_semantics.IsReadOnly("no_such_command"));
        Assert.False(_semantics.IsDestructive("no_such_command"));
        Assert.False(_semantics.IsOpenWorld("no_such_command"));
    }
}
