namespace Snail.MCP.Blender.Tools.Prompts;

public static partial class ToolDescriptions
{
    /// <summary><c>MeshTools</c>.</summary>
    public static class Mesh
    {
        /// <summary><c>blender_mesh_select</c></summary>
        public const string Select =
            "Selects vertices, edges and faces of a mesh: all, none, invert, by index, by the direction faces point " +
            "(by_normal with an axis), by coordinate range (by_position), or by material slot. The selection is stored in " +
            "the mesh and drives every other mesh tool. Returns the counts.";

        /// <summary><c>blender_mesh_extrude</c></summary>
        public const string Extrude =
            "Extrudes the selected faces along their normal by a distance; the new faces become the selection, so " +
            "repeated calls build a tower. Select faces first, e.g. by_normal +z for the top.";

        /// <summary><c>blender_mesh_inset</c></summary>
        public const string Inset = "Insets the selected faces by a thickness, optionally pushing the inset in or out.";

        /// <summary><c>blender_mesh_bevel</c></summary>
        public const string Bevel =
            "Bevels the selected edges (or vertices) with a width and segment count; with nothing selected, every edge.";

        /// <summary><c>blender_mesh_subdivide</c></summary>
        public const string Subdivide = "Subdivides the selected edges (or all) with a number of cuts and optional smoothing.";

        /// <summary><c>blender_mesh_merge</c></summary>
        public const string Merge = "Merges vertices closer than a distance (Merge by Distance); the selection or all vertices.";

        /// <summary><c>blender_mesh_normals</c></summary>
        public const string Normals = "Recalculates face normals to point outside, or inside on request.";

        /// <summary><c>blender_mesh_shade</c></summary>
        public const string Shade =
            "Sets smooth or flat shading on the whole mesh; with an angle, edges sharper than it stay flat.";

        /// <summary><c>blender_mesh_geometry</c></summary>
        public const string Geometry =
            "Reads the mesh: vertex positions with indices and faces as vertex index lists, in local or world space, " +
            "limited to a count; the counts cover the whole mesh.";

        /// <summary><c>blender_mesh_set_vertices</c></summary>
        public const string SetVertices = "Moves vertices by index to new positions or by offsets; the direct way to sculpt exact shapes.";
    }

    /// <summary><c>ModifierTools</c>.</summary>
    public static class Modifiers
    {
        /// <summary><c>blender_add_modifier</c></summary>
        public const string Add =
            "Adds a modifier of any type to an object with its settings, optionally applying it at once. " +
            "Returns the modifier with the effective values of every setting.";

        /// <summary><c>blender_update_modifier</c></summary>
        public const string Update = "Changes settings of an existing modifier, or renames it.";

        /// <summary><c>blender_remove_modifier</c></summary>
        public const string Remove = "Removes a modifier from the stack without applying it.";

        /// <summary><c>blender_apply_modifier</c></summary>
        public const string Apply = "Bakes a modifier into the mesh and removes it from the stack; irreversible except through undo.";

        /// <summary><c>blender_describe_modifier</c></summary>
        public const string Describe =
            "Lists every modifier type, or documents one type's settings with types, defaults, limits and enum values " +
            "for blender_add_modifier and blender_update_modifier.";
    }

    /// <summary><c>CurveTools</c>.</summary>
    public static class Curves
    {
        /// <summary><c>blender_add_curve</c></summary>
        public const string Add =
            "Adds a curve: bezier, nurbs or poly drawn through given points, or a circle or path primitive, with optional " +
            "thickness (bevelDepth) that turns it into a tube.";

        /// <summary><c>blender_set_curve</c></summary>
        public const string Set =
            "Changes a curve's thickness, extrusion, resolution, fill, and the objects used as bevel profile or taper.";

        /// <summary><c>blender_convert_to_mesh</c></summary>
        public const string ConvertToMesh = "Converts a curve, text or metaball object into a mesh, keeping the original on request.";
    }

    /// <summary><c>GeometryTools</c>.</summary>
    public static class Geometry
    {
        /// <summary><c>blender_join_objects</c></summary>
        public const string Join = "Joins several objects into one; the target keeps its name, origin and modifiers.";

        /// <summary><c>blender_set_origin</c></summary>
        public const string SetOrigin = "Moves an object's origin to its geometry, the 3D cursor or its centre of mass, or moves the geometry to the origin.";

        /// <summary><c>blender_apply_transforms</c></summary>
        public const string ApplyTransforms =
            "Bakes location, rotation and scale into the mesh data, resetting them to identity; needed before booleans, " +
            "arrays and exports that expect unit scale.";
    }

    /// <summary><c>GeometryNodeTools</c>.</summary>
    public static class GeometryNodes
    {
        /// <summary><c>blender_build_geometry_nodes</c></summary>
        public const string Build =
            "Builds a Geometry Nodes tree on an object from a specification of nodes and links; Group Input and Group Output " +
            "exist already, so link the geometry through them. Any of Blender's geometry, function and math nodes by name.";

        /// <summary><c>blender_set_geometry_inputs</c></summary>
        public const string SetInputs = "Sets the exposed inputs of a Geometry Nodes modifier by name.";
    }
}
