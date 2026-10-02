namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/scene.py</c>: the scene at a glance and the objects in it.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand SceneInfo = new("scene_info", AddOnModules.Scene);

    public static readonly BridgeCommand ListObjects = new("list_objects", AddOnModules.Scene);
}
