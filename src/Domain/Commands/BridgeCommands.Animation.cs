namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/animation.py</c>: keyframes, F-Curves, armatures, drivers, shape keys and markers.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand SetFrame = new("set_frame", AddOnModules.Animation);

    public static readonly BridgeCommand InsertKeyframe = new("insert_keyframe", AddOnModules.Animation);

    public static readonly BridgeCommand DeleteKeyframes = new("delete_keyframes", AddOnModules.Animation);

    public static readonly BridgeCommand ListKeyframes = new("list_keyframes", AddOnModules.Animation);

    public static readonly BridgeCommand SetInterpolation = new("set_interpolation", AddOnModules.Animation);

    public static readonly BridgeCommand MoveKeyframes = new("move_keyframes", AddOnModules.Animation);

    public static readonly BridgeCommand AddFcurveModifier = new("add_fcurve_modifier", AddOnModules.Animation);

    public static readonly BridgeCommand AddArmature = new("add_armature", AddOnModules.Animation);

    public static readonly BridgeCommand ParentToArmature = new("parent_to_armature", AddOnModules.Animation);

    public static readonly BridgeCommand PoseBone = new("pose_bone", AddOnModules.Animation);

    public static readonly BridgeCommand SetVertexWeights = new("set_vertex_weights", AddOnModules.Animation);

    public static readonly BridgeCommand FollowPath = new("follow_path", AddOnModules.Animation);

    public static readonly BridgeCommand AddDriver = new("add_driver", AddOnModules.Animation);

    public static readonly BridgeCommand AddShapeKey = new("add_shape_key", AddOnModules.Animation);

    public static readonly BridgeCommand SetShapeKey = new("set_shape_key", AddOnModules.Animation);

    public static readonly BridgeCommand AddMarker = new("add_marker", AddOnModules.Animation);
}
