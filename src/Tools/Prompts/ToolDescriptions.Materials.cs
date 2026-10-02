namespace Snail.MCP.Blender.Tools.Prompts;

public static partial class ToolDescriptions
{
    /// <summary><c>MaterialTools</c>.</summary>
    public static class Materials
    {
        /// <summary><c>blender_create_material</c></summary>
        public const string Create =
            "Creates a node material with Principled BSDF settings and optionally assigns it to an object. Every value is " +
            "optional; the rest keep Blender's defaults. Returns the effective settings.";

        /// <summary><c>blender_set_material</c></summary>
        public const string Set = "Changes Principled BSDF settings of an existing material, or renames it. It edits the material itself and " +
            "touches no object: putting a material on an object is blender_assign_material.";

        /// <summary><c>blender_assign_material</c></summary>
        public const string Assign =
            "Assigns a material to an object: by default the object becomes that material, which fills slot 0 and points every " +
            "face at it. A given slot is filled instead, and the faces still follow. selectedFaces gives the material a slot of " +
            "its own and points only the mesh's selected faces at it; append adds the slot and leaves the faces alone. The reply " +
            "names the slot, the slot list and how many faces now point at it.";

        /// <summary><c>blender_list_materials</c></summary>
        public const string List = "Every material in the file with its user count.";

        /// <summary><c>blender_material_info</c></summary>
        public const string Info =
            "A material in depth: Principled settings and the full node graph with node types, input values and links.";

        /// <summary><c>blender_set_viewport_shading</c></summary>
        public const string ViewportShading = "Switches every 3D viewport to wireframe, solid, material preview or rendered shading.";
    }

    /// <summary><c>NodeTools</c>.</summary>
    public static class Nodes
    {
        /// <summary><c>blender_add_node</c></summary>
        public const string Add = "Adds a shader node to a material with input values and settings; link it with blender_link_nodes.";

        /// <summary><c>blender_set_node</c></summary>
        public const string Set = "Changes input values, settings, name or label of a node.";

        /// <summary><c>blender_link_nodes</c></summary>
        public const string Link = "Connects an output socket of one node to an input socket of another, replacing any existing link there.";

        /// <summary><c>blender_remove_node</c></summary>
        public const string Remove = "Deletes a node and its links.";

        /// <summary><c>blender_build_node_graph</c></summary>
        public const string Build =
            "Builds several nodes and their links in one call from a specification; the way to assemble a whole shader " +
            "(texture → ramp → bump → Principled) at once.";
    }

    /// <summary><c>TextureTools</c>.</summary>
    public static class Textures
    {
        /// <summary><c>blender_load_image_texture</c></summary>
        public const string LoadImage =
            "Loads an image file into an Image Texture node and wires it into the Principled BSDF. The mesh needs UVs: " +
            "blender_unwrap_uv makes them.";

        /// <summary><c>blender_procedural_texture</c></summary>
        public const string Procedural =
            "Adds a procedural texture (noise, voronoi, brick and the rest), optionally through a colour ramp, and wires " +
            "it into a Principled BSDF input; Normal goes through a bump node.";

        /// <summary><c>blender_unwrap_uv</c></summary>
        public const string UnwrapUv = "Unwraps a mesh's UVs by Smart UV Project, seams, cube, sphere or cylinder projection.";
    }
}
