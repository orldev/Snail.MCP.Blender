namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/scenes.py</c>: scenes and snapshots of the file.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand Scenes = new("scenes", AddOnModules.Scenes);

    public static readonly BridgeCommand Snapshot = new("snapshot", AddOnModules.Scenes);
}
