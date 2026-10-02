namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/sequence.py</c>: a list of commands run one after another in the Blender that is already open.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand Run = new("run", AddOnModules.Sequence);
}
