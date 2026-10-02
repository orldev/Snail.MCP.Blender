namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/lights.py</c>: light objects: type, energy, colour in kelvin or RGB, shape and IES profiles.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand AddLight = new("add_light", AddOnModules.Lights);

    public static readonly BridgeCommand SetLight = new("set_light", AddOnModules.Lights);
}
