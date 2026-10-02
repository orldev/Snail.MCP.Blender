using System.Text.RegularExpressions;

namespace Snail.MCP.Blender.Application.Discovery;

/// <summary>One thing a script does that a tool of this server already does, and the skill that tool arrives with.</summary>
public sealed record ScriptFinding(string Operation, string Tool, string? Skill);

/// <summary>Reads a Python script the way a reviewer would: which of its operations this server already has a tool for.</summary>
/// <remarks>The measured reason blender_python wins is not that the tools are missing but that they are invisible: two thirds of the
/// mutating commands in a working session were scripts, and what those scripts did — resolution, samples, look, materials, lights —
/// every one of them has a tool. The table below is read from that evidence and is checked against the catalog by a test, so a tool
/// that gets renamed cannot leave a promise behind. Operators are matched by name instead, which needs no table.</remarks>
public sealed partial class ScriptAdvice(ToolIndex index)
{
    /// <summary>Marks of the Blender API that a dedicated tool covers: the text to find in the script, and the tool that does the same thing.</summary>
    private static readonly (string Operation, string Tool, string[] Marks)[] Api =
    [
        ("resolution, frame rate and output format", "blender_set_output", ["render.resolution_x", "render.resolution_y", "resolution_percentage", "render.fps", "render.filepath", "image_settings."]),
        ("Cycles sampling and device", "blender_set_cycles", ["cycles.samples", "cycles.device", "cycles.adaptive", "cycles.use_denoising", "cycles.max_bounces", "cycles.preview_samples"]),
        ("EEVEE settings", "blender_set_eevee", ["scene.eevee.", "eevee.taa_render_samples", "eevee.use_raytracing"]),
        ("view transform, look and exposure", "blender_set_color_management", ["view_settings.look", "view_settings.view_transform", "view_settings.exposure", "display_settings.display_device"]),
        ("the world and its background", "blender_set_world", ["bpy.data.worlds", "world.node_tree", "scene.world ", "scene.world="]),
        ("a new material", "blender_create_material", ["bpy.data.materials.new"]),
        ("the principled shader's inputs", "blender_set_material", ["BsdfPrincipled", "Principled BSDF"]),
        ("a material on an object", "blender_assign_material", ["data.materials.append", "material_slots"]),
        ("a shader or compositor node", "blender_add_node", ["node_tree.nodes.new"]),
        ("a link between nodes", "blender_link_nodes", ["node_tree.links.new"]),
        ("the scene's compositor", "blender_compositor", ["scene.node_tree", "CompositorNode"]),
        ("inputs of a geometry nodes modifier", "blender_set_geometry_inputs", ["properties.inputs", "GeometryNodeTree"]),
        ("a light", "blender_add_light", ["bpy.data.lights.new", "ops.object.light_add"]),
        ("a light's power or colour", "blender_set_light", [".data.energy", ".data.color", "light.energy"]),
        ("a camera and its lens", "blender_set_camera", ["bpy.data.cameras.new", ".data.lens", ".data.dof"]),
        ("a keyframe", "blender_insert_keyframe", ["keyframe_insert"]),
        ("a driver", "blender_add_driver", ["driver_add"]),
        ("a modifier", "blender_add_modifier", ["modifiers.new", "modifier_add("]),
        ("a constraint", "blender_add_constraint", ["constraints.new", "constraint_add("]),
        ("an armature", "blender_add_armature", ["bpy.data.armatures.new", "ops.object.armature_add"]),
        ("a shape key", "blender_add_shape_key", ["shape_key_add"]),
        ("moving, rotating or scaling an object", "blender_transform_object", [".location =", ".location=", ".rotation_euler =", ".rotation_euler=", ".scale =", ".scale="]),
        ("the current frame and the frame range", "blender_set_frame", ["frame_start", "frame_end", "frame_current", "frame_set("]),
        ("a primitive", "blender_add_primitive", ["ops.mesh.primitive_", "bpy.data.objects.new"]),
        ("a collection", "blender_create_collection", ["bpy.data.collections.new"]),
        ("visibility in renders and viewport", "blender_object_render_flags", ["hide_render", "hide_viewport", "visible_camera", "visible_shadow"]),
        ("smooth or flat shading", "blender_mesh_shade", ["shade_smooth", "shade_flat"]),
        ("a UV unwrap", "blender_unwrap_uv", ["ops.uv.", "uv_layers.new"]),
        ("a texture bake", "blender_bake_texture", ["ops.object.bake"]),
        ("deleting objects", "blender_delete_objects", ["bpy.data.objects.remove", "ops.object.delete"]),
        ("selecting objects", "blender_select_objects", ["select_set(", "ops.object.select_all"]),
        ("assets from another file", "blender_append_assets", ["ops.wm.append", "ops.wm.link", "libraries.load"]),
        ("saving the file", "blender_save_file", ["wm.save_as_mainfile", "wm.save_mainfile"]),
        ("opening a file", "blender_open_file", ["wm.open_mainfile"]),
        ("starting a new file", "blender_new_file", ["wm.read_homefile", "wm.read_factory_settings"]),
        ("importing a file", "blender_import_file", ["ops.import_scene", "ops.import_mesh", "wm.obj_import", "wm.usd_import"]),
        ("exporting a file", "blender_export_file", ["ops.export_scene", "ops.export_mesh", "wm.obj_export", "wm.usd_export"]),
        ("a render", "blender_render_image", ["ops.render.render"]),
    ];

    /// <summary>The tools the table promises; a test checks them against the catalogue, so a renamed tool cannot leave a promise behind.</summary>
    public static IReadOnlyList<string> Covers { get; } = [.. Api.Select(entry => entry.Tool)];

    /// <summary>Every operation of the script a tool already covers, each named once, in the order the table and then the script give them.</summary>
    public IReadOnlyList<ScriptFinding> Read(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return [];
        }

        var text = AroundDots().Replace(code, ".");
        var found = new List<ScriptFinding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (operation, tool, marks) in Api)
        {
            if (marks.Any(mark => text.Contains(mark, StringComparison.Ordinal)) && index.Named(tool) is { } entry && seen.Add(tool))
            {
                found.Add(new ScriptFinding(operation, entry.Name, entry.Skill));
            }
        }

        foreach (var (name, entry) in Operators(text))
        {
            if (seen.Add(entry.Name))
            {
                found.Add(new ScriptFinding($"bpy.ops.{name}", entry.Name, entry.Skill));
            }
        }

        return found;
    }

    /// <summary>The operators the script calls that a tool of the same name covers.</summary>
    private IEnumerable<(string Name, ToolEntry Tool)> Operators(string code)
    {
        foreach (var name in Called(code))
        {
            if (index.Covering(name[(name.IndexOf('.', StringComparison.Ordinal) + 1)..]) is { } entry)
            {
                yield return (name, entry);
            }
        }
    }

    /// <summary>Every <c>bpy.ops.module.operator</c> the text calls, in the order it calls them.</summary>
    private static IEnumerable<string> Called(string code)
    {
        const string prefix = "bpy.ops.";

        for (var index = code.IndexOf(prefix, StringComparison.Ordinal); index >= 0; index = code.IndexOf(prefix, index + 1, StringComparison.Ordinal))
        {
            var start = index + prefix.Length;
            var end = start;

            while (end < code.Length && (char.IsLetterOrDigit(code[end]) || code[end] is '_' or '.'))
            {
                end++;
            }

            var name = code[start..end].Trim('.');

            if (name.Contains('.', StringComparison.Ordinal))
            {
                yield return name;
            }
        }
    }

    /// <summary>Whitespace on either side of a dot, which Python ignores and a reading of text would not.</summary>
    [GeneratedRegex(@"\s*\.\s*")]
    private static partial Regex AroundDots();
}
