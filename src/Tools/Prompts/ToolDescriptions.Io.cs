namespace Snail.MCP.Blender.Tools.Prompts;

public static partial class ToolDescriptions
{
    /// <summary><c>IoTools</c>.</summary>
    public static class Io
    {
        /// <summary><c>blender_import_file</c></summary>
        public const string Import = "Imports a model or scene file into the current scene and lists the objects it added; the format follows the extension.";

        /// <summary><c>blender_export_file</c></summary>
        public const string Export = "Exports the scene or the selected objects to a file; the format follows the extension. glb packs everything into one file.";

        /// <summary><c>blender_list_assets</c></summary>
        public const string ListAssets = "Lists what another .blend file holds by type: objects, collections, materials, node groups, worlds, images and more.";

        /// <summary><c>blender_append_assets</c></summary>
        public const string AppendAssets =
            "Appends (copies) or links objects, collections, materials, node groups, worlds or images from another .blend " +
            "file into the scene, optionally into a collection; linked collections arrive as instances.";
    }
}
