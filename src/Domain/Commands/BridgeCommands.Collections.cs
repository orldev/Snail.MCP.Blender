namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/collections.py</c>: collections and their view-layer state.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand CreateCollection = new("create_collection", AddOnModules.Collections);

    public static readonly BridgeCommand MoveToCollection = new("move_to_collection", AddOnModules.Collections);

    public static readonly BridgeCommand UpdateCollection = new("update_collection", AddOnModules.Collections);
}
