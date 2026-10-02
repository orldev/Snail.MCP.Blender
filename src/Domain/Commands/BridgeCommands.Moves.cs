namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/moves.py</c>: camera moves and light rigs built around a subject.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand CameraMove = new("camera_move", AddOnModules.Moves);

    public static readonly BridgeCommand LightRig = new("light_rig", AddOnModules.Moves);
}
