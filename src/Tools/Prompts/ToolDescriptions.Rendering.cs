namespace Snail.MCP.Blender.Tools.Prompts;

public static partial class ToolDescriptions
{
    /// <summary><c>RenderTools</c> and <c>CompositorTools</c>.</summary>
    public static class Rendering
    {
        /// <summary><c>blender_render_settings</c></summary>
        public const string Settings =
            "Sets the render engine, resolution, samples, denoising, background transparency, pixel filter width (sharpness) " +
            "and camera; every value optional. Returns the effective settings. Output path, format, depth, codecs and stamps " +
            "live in blender_set_output; engine details in blender_set_cycles and blender_set_eevee; passes in blender_view_layer.";

        /// <summary><c>blender_render_image</c></summary>
        public const string Image =
            "Renders the current or a given frame of a scene and view layer to an image file and returns its path, size, " +
            "duration, peak memory, a preview to look at and exposure statistics. Blender is busy until it finishes and the render " +
            "cannot be interrupted once it starts — cancelling the call stops the waiting, not the render. Lower the resolution " +
            "percentage or samples for previews, and use blender_render_job for anything long, which renders in a second Blender " +
            "and can be cancelled.";

        /// <summary><c>blender_render_animation</c></summary>
        public const string Animation =
            "Renders the frame range as an image sequence or a video. In the background by default: a second Blender renders the " +
            "same range to the same path, this one stays free, and the reply is a job id that blender_render_job_status follows and " +
            "blender_render_job_cancel stops. Pass background=false to render here instead, which holds Blender until the last frame " +
            "and cannot be interrupted — a cancelled tool call stops the waiting, not the render.";

        /// <summary><c>blender_set_world</c></summary>
        public const string World =
            "Sets the world: background colour and strength, an HDRI environment with rotation, and mist. The first thing to " +
            "fix when renders look dark or flat.";

        /// <summary><c>blender_render_object</c></summary>
        public const string RenderObject =
            "Renders one object (or a few) alone: a temporary camera frames them from a chosen angle, everything else is " +
            "hidden from the render, the background is transparent. A product shot in one call.";

        /// <summary><c>blender_viewport_capture</c></summary>
        public const string ViewportCapture =
            "Grabs the 3D viewport as the user sees it, in its current shading, to a file; fast feedback without a render. Needs Blender's window.";

        /// <summary><c>blender_bake_texture</c></summary>
        public const string BakeTexture =
            "Bakes a material channel (colour, normal, AO, roughness, combined) into a PNG through the mesh's UVs with Cycles; " +
            "the way to take procedural materials into game engines.";

        /// <summary><c>blender_cryptomatte_matte</c></summary>
        public const string CryptomatteMatte =
            "Adds a Cryptomatte node that mattes the named objects, materials or assets, switching the cryptomatte pass on, " +
            "and optionally writes the matte through a File Output slot. Its Matte output isolates them for grading or replacement.";

        /// <summary><c>blender_inspect_image</c></summary>
        public const string InspectImage =
            "Looks at an image file: a preview the model can see, plus exposure statistics (mean luminance, clipped highlights and " +
            "shadows, a histogram). For frames a background job wrote, textures, or any render on disk.";

        /// <summary><c>blender_compositor</c></summary>
        public const string Compositor =
            "Reads or edits the compositor by hand: list nodes, reset to Render Layers → output, add a node with inputs and " +
            "settings, link sockets, remove a node. Render passes appear as outputs of the Render Layers node. Grain, glare " +
            "and the rest of the post stack come from blender_lens_effects. Applies to every render from then on.";
    }

    /// <summary><c>OutputTools</c>.</summary>
    public static class Output
    {
        /// <summary><c>blender_set_color_management</c></summary>
        public const string ColorManagement =
            "Sets colour management: display device, view transform (AgX, Filmic, Standard, ACES, Khronos PBR Neutral), look, " +
            "exposure, gamma, white balance and the sequencer colour space. Returns the effective values and what this Blender offers.";

        /// <summary><c>blender_set_output</c></summary>
        public const string Settings =
            "Sets the render output: path template with #### for the frame number, format, colour depth, EXR codec, PNG " +
            "compression, JPEG quality, overwrite and placeholder for farm-style rendering, metadata stamps, a render " +
            "region, a resolution preset and stereo or multi-view. Returns the effective settings and the first file name Blender would write.";

        /// <summary><c>blender_file_output</c></summary>
        public const string FileOutput =
            "Configures a File Output node in the compositor: a folder, a format with its depth and codec, and one slot per " +
            "render pass wired from the Render Layers node, each with an optional format of its own. Passes that are off get " +
            "switched on. A multilayer EXR keeps every slot in one file; other formats write one file per slot.";
    }

    /// <summary><c>EngineTools</c>.</summary>
    public static class Engines
    {
        /// <summary><c>blender_set_cycles</c></summary>
        public const string Cycles =
            "Switches to Cycles and sets its sampling in depth: samples, adaptive sampling with threshold and minimum, a time " +
            "limit, the denoiser with prefilter, passes and quality, GPU denoising, device and compute backend (CUDA, OptiX, HIP, " +
            "oneAPI, Metal) with the devices this machine has, light paths per bounce type, caustics, clamping, light tree, seed " +
            "and persistent data; anything else by Python name.";

        /// <summary><c>blender_set_eevee</c></summary>
        public const string Eevee =
            "Switches to EEVEE and sets its quality: samples, shadows with rays, steps and resolution, ray tracing with its " +
            "options, fast GI, volumetrics, clamping, depth-of-field bokeh, overscan and high quality normals; anything else by Python name.";

        /// <summary><c>blender_set_freestyle</c></summary>
        public const string Freestyle =
            "Freestyle line rendering: on or off, line thickness and mode, crease angle, and line sets with their edge " +
            "types, visibility, colour and thickness; for toon, technical and ink looks.";

        /// <summary><c>blender_set_motion_blur</c></summary>
        public const string MotionBlur =
            "Sets motion blur for renders: on or off, shutter time in frames, shutter position, Cycles rolling shutter, EEVEE " +
            "steps and maximum blur, and whether the view layers use it.";
    }

    /// <summary><c>LayerTools</c>.</summary>
    public static class Layers
    {
        /// <summary><c>blender_view_layer</c></summary>
        public const string ViewLayer =
            "Creates, configures, lists, activates or removes view layers: which collections are excluded, holdout or " +
            "indirect-only, the render passes, cryptomatte (object, material, asset, levels), AOVs, light groups and their " +
            "members, material and world overrides, per-layer samples. Returns the layer with its collection tree.";

        /// <summary><c>blender_light_linking</c></summary>
        public const string LightLinking =
            "Cycles light and shadow linking: which objects or collections a light illuminates (include: only these; " +
            "exclude: all but these) and which cast its shadow. Returns the receiver and blocker lists with their states.";
    }

    /// <summary><c>OpticsTools</c> and <c>PostTools</c>.</summary>
    public static class Lens
    {
        /// <summary><c>blender_set_camera_optics</c></summary>
        public const string Optics =
            "The physical camera: sensor size and fit, focal length or field of view, lens shift, clip range, perspective, " +
            "orthographic or panoramic type, aperture with f-stop, blade count, rotation and anamorphic ratio, focus by " +
            "distance, object or bone. Returns the optics and the hyperfocal distance. Motion blur lives in blender_set_motion_blur " +
            "and, per object, in blender_object_render_flags.";

        /// <summary><c>blender_lens_effects</c></summary>
        public const string Effects =
            "Rebuilds the post stack on the beauty pass in a compositor's order: denoise (with the denoising data passes), " +
            "depth of field or motion blur from passes, glare, halation, chromatic aberration, vignette, film grain " +
            "weighted by luminance and animated per frame, in display space. Each block optional, but at least one is asked for: " +
            "a call naming none is refused rather than read as clear. A repeated call replaces the stack, clear removes it and " +
            "wires Render Layers straight to the output. To see the chain that is there, read it with blender_compositor and " +
            "action info. Data passes stay raw for blender_file_output. Returns the chain and warnings.";
    }

    /// <summary><c>RenderFlagTools</c>.</summary>
    public static class Flags
    {
        /// <summary><c>blender_object_render_flags</c></summary>
        public const string ObjectRenderFlags =
            "Per-object rendering flags for integration and passes: holdout, shadow catcher, visibility per ray type, pass " +
            "index for the object index pass, light group, caustics role, per-object motion blur, shadow terminator offset. " +
            "Objects or whole collections. Returns each object's effective flags.";
    }

    /// <summary><c>StagingTools</c>.</summary>
    public static class Staging
    {
        /// <summary><c>blender_camera_move</c></summary>
        public const string CameraMove =
            "Animates a camera move around a subject over a frame range: turntable, orbit, dolly, push-in or crane, " +
            "with the camera aimed at the subject. Product videos and look-development spins in one call.";

        /// <summary><c>blender_light_rig</c></summary>
        public const string LightRig =
            "Builds a three-point rig of area lights around a subject: key, fill and rim at studio angles, sized and " +
            "powered from the subject's size, each with a colour temperature and aimed at the subject. Replaces a rig of the same name.";
    }
}
