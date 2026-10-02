namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/world.py</c>: the world: background colour or HDRI, strength and mist.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand SetWorld = new("set_world", AddOnModules.World);
}
