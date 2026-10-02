namespace Snail.MCP.Blender.Tools.Prompts;

public static partial class ToolDescriptions
{
    /// <summary><c>KeyframeTools</c>.</summary>
    public static class Keyframes
    {
        /// <summary><c>blender_set_frame</c></summary>
        public const string SetFrame = "Sets the current frame, the scene's frame range and the frame rate; returns the effective values.";

        /// <summary><c>blender_insert_keyframe</c></summary>
        public const string Insert =
            "Keys properties of an object on a frame, optionally setting their values first (rotation in degrees). " +
            "Without channels or values, keys location, rotation and scale.";

        /// <summary><c>blender_delete_keyframes</c></summary>
        public const string Delete = "Deletes keys on a frame or channels; with neither, strips all animation from the object.";

        /// <summary><c>blender_list_keyframes</c></summary>
        public const string List = "Every animation curve of an object with its keys, interpolation and modifiers, plus its drivers.";

        /// <summary><c>blender_set_interpolation</c></summary>
        public const string Interpolation = "Sets the interpolation and easing of keys, on all curves or a frame range.";

        /// <summary><c>blender_move_keyframes</c></summary>
        public const string Move = "Shifts keys in time and/or stretches their timing around a pivot frame.";

        /// <summary><c>blender_add_fcurve_modifier</c></summary>
        public const string FcurveModifier = "Adds an F-Curve modifier to the object's animation curves: repeat with CYCLES, jitter with NOISE, and the rest.";

        /// <summary><c>blender_add_marker</c></summary>
        public const string Marker = "Adds a timeline marker, optionally binding a camera to it so the render switches cameras there.";
    }

    /// <summary><c>RigTools</c>.</summary>
    public static class Rig
    {
        /// <summary><c>blender_add_armature</c></summary>
        public const string AddArmature = "Creates an armature from a list of bones with heads, tails and parents.";

        /// <summary><c>blender_parent_to_armature</c></summary>
        public const string ParentToArmature = "Skins a mesh to an armature: parents it and creates vertex groups, with automatic weights by default.";

        /// <summary><c>blender_pose_bone</c></summary>
        public const string PoseBone = "Moves, rotates or scales a bone in pose mode and optionally keys it on a frame.";

        /// <summary><c>blender_set_vertex_weights</c></summary>
        public const string VertexWeights = "Sets weights of a vertex group by index list, for all vertices or for the selected ones.";
    }

    /// <summary><c>MotionTools</c>.</summary>
    public static class Motion
    {
        /// <summary><c>blender_follow_path</c></summary>
        public const string FollowPath = "Makes an object travel along a curve over a frame range with a Follow Path constraint and a linear path animation.";

        /// <summary><c>blender_add_driver</c></summary>
        public const string AddDriver = "Drives a property with a Python expression over variables read from other objects and the frame.";
    }

    /// <summary><c>ConstraintTools</c>.</summary>
    public static class Constraints
    {
        /// <summary><c>blender_add_constraint</c></summary>
        public const string Add =
            "Adds a constraint of any type to an object or a bone with its settings: aim a camera with TRACK_TO, follow with " +
            "COPY_LOCATION, limit motion, IK for rigs. Returns the effective settings.";

        /// <summary><c>blender_update_constraint</c></summary>
        public const string Update = "Changes settings, influence, enabled state or name of a constraint.";

        /// <summary><c>blender_remove_constraint</c></summary>
        public const string Remove = "Removes a constraint from an object or a bone.";

        /// <summary><c>blender_describe_constraint</c></summary>
        public const string Describe = "Lists every constraint type, or documents one type's settings with types, defaults and enum values.";
    }

    /// <summary><c>ShapeKeyTools</c>.</summary>
    public static class ShapeKeys
    {
        /// <summary><c>blender_add_shape_key</c></summary>
        public const string Add =
            "Adds a shape key (blend shape), optionally shaping it by vertex offsets; blender_mesh_geometry gives the indices.";

        /// <summary><c>blender_set_shape_key</c></summary>
        public const string Set = "Sets a shape key's value and slider range, optionally keying the value on a frame.";
    }
}
