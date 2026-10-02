namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/cameras.py</c>: camera objects and their lens settings.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand AddCamera = new("add_camera", AddOnModules.Cameras);

    public static readonly BridgeCommand SetCamera = new("set_camera", AddOnModules.Cameras);
}
