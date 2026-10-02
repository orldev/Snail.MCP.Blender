namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/optics.py</c>: the physical camera: sensor, aperture, focus and object motion.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand SetCameraOptics = new("set_camera_optics", AddOnModules.Optics);
}
