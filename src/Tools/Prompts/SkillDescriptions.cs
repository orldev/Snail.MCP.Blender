namespace Snail.MCP.Blender.Tools.Prompts;

/// <summary>What each skill covers, shown by blender_skills so the model knows which one to load; a wire contract, edited with caution.</summary>
public static class SkillDescriptions
{
    public const string Modeling =
        "Mesh editing on a selection (select by index, normal or position; extrude, inset, bevel, subdivide, merge, " +
        "normals, smooth shading), modifiers of every type with their settings, Geometry Nodes trees and their inputs, curves " +
        "and their conversion to meshes, joining objects, origins and applying transforms.";

    public const string Materials =
        "Materials with Principled BSDF settings (colour, metallic, roughness, emission, transmission, alpha), " +
        "assignment to objects and faces, the shader node graph by node and socket names or as one JSON spec, image " +
        "and procedural textures wired into the shader, UV unwrapping and the viewport shading mode.";

    public const string Animation =
        "Frame range and playhead, keyframes on any property with interpolation and easing, moving and scaling keys, " +
        "F-Curve modifiers (cycles, noise, envelope), armatures with bones, skinning with automatic weights and " +
        "vertex groups, posing bones with keys, following a curve path, drivers with Python expressions, constraints, shape " +
        "keys and timeline markers.";

    public const string Rendering =
        "Render engine, resolution, samples, denoising, output format and camera; rendering a still or an animation with " +
        "a picture and exposure statistics in the reply; render passes; colour management (view transform, look, exposure), " +
        "output depth and codecs, presets, stereo, stamps and regions; the physical camera; Cycles and EEVEE in depth, motion " +
        "blur, Freestyle; view layers with collections, cryptomatte, AOVs and light groups; light linking; per-object render " +
        "flags; camera moves and light rigs; the world and HDRI lighting; product shots, viewport captures, texture bakes and " +
        "inspecting a rendered image.";

    public const string Farm =
        "Renders that outlive a tool call: background render jobs in a second Blender with a queue, chunks, retries, " +
        "progress and cancel; the pre-flight check; the memory budget with a probe render and farm packing.";

    public const string Post =
        "Post-processing after the render: the compositor node tree, File Output nodes with one slot per pass, " +
        "cryptomatte mattes, and the lens-effects stack (denoise, defocus, vector blur, glare, halation, chromatic " +
        "aberration, vignette, film grain).";

    public const string Io =
        "Importing and exporting scenes and models: FBX, OBJ, STL, PLY, glTF/GLB, Collada, USD, Alembic, X3D, SVG, DXF, 3DS; " +
        "listing the assets of other .blend files and appending or linking them.";

    public const string Physics =
        "Simulation: rigid bodies, cloth, soft bodies, collisions, fluid domains and flows, dynamic paint, particle and " +
        "hair systems, and baking the caches.";

    public const string Video =
        "The Video Sequence Editor: movie, image sequence, sound, scene, colour and text strips on channels, trims and fades, " +
        "transitions and effects (cross, wipe, glow, blur, speed), transforms and crops, cuts, proxies, frame rate and sync, " +
        "grading with strip modifiers, encode settings (H.264, H.265, AV1, ProRes, bitrates), the encode itself and the audio mixdown.";
}
