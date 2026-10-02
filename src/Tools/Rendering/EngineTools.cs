using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>The render engines in depth: Cycles sampling and light paths, EEVEE quality, motion blur.</summary>
[McpServerToolType]
[Skill(Skills.Rendering, SkillDescriptions.Rendering)]
public sealed class EngineTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_set_cycles", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Engines.Cycles)]
    public Task<CallToolResult> CyclesAsync(
        [Description("Switch the scene to Cycles; false only changes the settings.")] bool activate = true,
        [Description("Maximum samples per pixel; 64 to 256 for previews, 1024 to 4096 for finals with adaptive sampling.")] int? samples = null,
        [Description("Adaptive sampling on or off; it stops sampling pixels that converged.")] bool? adaptiveSampling = null,
        [Description("Noise threshold for adaptive sampling; 0.01 is the default, 0.001 cleaner and slower.")] double? adaptiveThreshold = null,
        [Description("Minimum samples before adaptive sampling may stop; 0 lets Blender choose.")] int? adaptiveMinSamples = null,
        [Description("Seconds per frame after which sampling stops; 0 is no limit.")] double? timeLimit = null,
        [Description("Denoise the final render.")] bool? denoise = null,
        [Description("OPENIMAGEDENOISE (every platform) or OPTIX (NVIDIA only); the reply lists what this machine has.")] string? denoiser = null,
        [Description("Denoiser prefilter: ACCURATE (the default), FAST or NONE.")] string? denoisePrefilter = null,
        [Description("Passes the denoiser uses: RGB, RGB_ALBEDO or RGB_ALBEDO_NORMAL (the default, best).")] string? denoisePasses = null,
        [Description("Denoiser quality: HIGH, BALANCED or FAST.")] string? denoiseQuality = null,
        [Description("Run the denoiser on the GPU.")] bool? denoiseGpu = null,
        [Description("CPU or GPU; GPU needs a compute backend, which backend sets.")] string? device = null,
        [Description("Compute backend for GPU rendering: OPTIX or CUDA (NVIDIA), HIP (AMD), ONEAPI (Intel), METAL (Apple), NONE for the CPU alone. Enables every device of that backend and sets the device to GPU; the reply lists the devices this machine has, and render jobs take the choice to their worker.")] string? backend = null,
        [Description("Light path bounces per type.")] Bounces? bounces = null,
        [Description("Caustics; off with glossy blur reduces fireflies.")] Caustics? caustics = null,
        [Description("Clamp sample brightness; 0 is no clamp.")] Clamp? clamp = null,
        [Description("Light tree for many-light scenes.")] bool? lightTree = null,
        [Description("Noise seed; animatedSeed changes it per frame.")] int? seed = null,
        [Description("Change the seed every frame so noise does not stick in animations.")] bool? animatedSeed = null,
        [Description("Keep scene data between frames; faster animations, more memory.")] bool? persistentData = null,
        [Description("Any other scene.cycles setting by Python name, e.g. {\"use_guiding\": true, \"volume_step_rate\": 0.5, \"texture_limit_render\": \"2048\"}.")] JsonObject? settings = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetCycles,
            new JsonObject().With("activate", activate).With("samples", samples).With("adaptive_sampling", adaptiveSampling).With("adaptive_threshold", adaptiveThreshold)
                .With("adaptive_min_samples", adaptiveMinSamples).With("time_limit", timeLimit).With("denoise", denoise).With("denoiser", denoiser)
                .With("denoise_prefilter", denoisePrefilter).With("denoise_passes", denoisePasses).With("denoise_quality", denoiseQuality).With("denoise_gpu", denoiseGpu)
                .With("device", device).With("backend", backend).With("bounces", bounces).With("caustics", caustics).With("clamp", clamp).With("light_tree", lightTree).With("seed", seed)
                .With("animated_seed", animatedSeed).With("persistent_data", persistentData).With("settings", settings).With("scene", scene),
            cancellationToken);

    [McpServerTool(Name = "blender_set_eevee", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Engines.Eevee)]
    public Task<CallToolResult> EeveeAsync(
        [Description("Switch the scene to EEVEE; false only changes the settings.")] bool activate = true,
        [Description("Render samples; 64 is the default, 16 for previews, 256 and up for clean soft shadows.")] int? samples = null,
        [Description("Viewport samples.")] int? viewportSamples = null,
        [Description("Shadows on or off.")] bool? shadows = null,
        [Description("Shadow rays per sample, 1 to 16; more is smoother.")] int? shadowRays = null,
        [Description("Shadow steps per ray, 1 to 16.")] int? shadowSteps = null,
        [Description("Shadow map resolution scale, 0.25 to 4.")] double? shadowResolution = null,
        [Description("Screen-space ray tracing for reflections and refractions.")] bool? raytracing = null,
        [Description("Ray tracing options.")] RaytracingOptions? raytracingOptions = null,
        [Description("Fast global illumination approximation for rough surfaces.")] bool? fastGi = null,
        [Description("Fast GI options.")] FastGiOptions? fastGiOptions = null,
        [Description("Volumetrics quality and range.")] Volumetrics? volumetrics = null,
        [Description("Clamp light per surface and volume.")] EeveeClamp? clamp = null,
        [Description("Depth of field bokeh.")] Bokeh? bokeh = null,
        [Description("Render a margin outside the frame so screen-space effects do not fade at the edges.")] bool? overscan = null,
        [Description("Overscan size in percent of the frame, 0 to 50.")] double? overscanSize = null,
        [Description("High quality normals for smooth shading.")] bool? highQualityNormals = null,
        [Description("Any other scene.eevee setting by Python name, e.g. {\"use_taa_reprojection\": true, \"shadow_pool_size\": \"1024\"}.")] JsonObject? settings = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetEevee,
            new JsonObject().With("activate", activate).With("samples", samples).With("viewport_samples", viewportSamples).With("shadows", shadows).With("shadow_rays", shadowRays)
                .With("shadow_steps", shadowSteps).With("shadow_resolution", shadowResolution).With("raytracing", raytracing).With("raytracing_options", raytracingOptions)
                .With("fast_gi", fastGi).With("fast_gi_options", fastGiOptions).With("volumetrics", volumetrics).With("clamp", clamp).With("bokeh", bokeh)
                .With("overscan", overscan).With("overscan_size", overscanSize).With("high_quality_normals", highQualityNormals).With("settings", settings).With("scene", scene),
            cancellationToken);

    [McpServerTool(Name = "blender_set_motion_blur", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Engines.MotionBlur)]
    public Task<CallToolResult> MotionBlurAsync(
        [Description("Motion blur on or off.")] bool? enabled = null,
        [Description("Shutter time in frames; 0.5 is a 180° shutter, the film look.")] double? shutter = null,
        [Description("Where the shutter opens relative to the frame: START, CENTER (the default) or END.")] string? position = null,
        [Description("Cycles rolling shutter: true (TOP), false (NONE).")] bool? rollingShutter = null,
        [Description("Cycles rolling shutter duration as a fraction of the frame, 0 to 1.")] double? rollingShutterDuration = null,
        [Description("EEVEE motion steps, 1 and up; more steps blur curved motion correctly.")] int? steps = null,
        [Description("EEVEE maximum blur in pixels.")] int? maxBlur = null,
        [Description("EEVEE depth scale that separates foreground from background blur.")] double? depthScale = null,
        [Description("Apply motion blur on every view layer of the scene, or not.")] bool? viewLayerEnabled = null,
        [Description("Shutter opening over the exposure as [[t, open], ...] from t 0 to 1; [[0,0],[0.1,1],[0.9,1],[1,0]] imitates a mechanical shutter, a flat 1 is the default.")] JsonArray? shutterCurve = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetMotionBlur,
            new JsonObject().With("enabled", enabled).With("shutter", shutter).With("position", position).With("rolling_shutter", rollingShutter)
                .With("rolling_shutter_duration", rollingShutterDuration).With("steps", steps).With("max_blur", maxBlur).With("depth_scale", depthScale)
                .With("view_layer_enabled", viewLayerEnabled).With("shutter_curve", shutterCurve).With("scene", scene),
            cancellationToken);

    [McpServerTool(Name = "blender_set_freestyle", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Engines.Freestyle)]
    public Task<CallToolResult> FreestyleAsync(
        [Description("Freestyle on or off for the scene and view layer.")] bool? enabled = null,
        [Description("Base line thickness in pixels.")] double? lineThickness = null,
        [Description("ABSOLUTE or RELATIVE to the resolution.")] string? thicknessMode = null,
        [Description("Angle in degrees above which an edge counts as a crease.")] double? creaseAngle = null,
        [Description("Cull lines outside the view.")] bool? culling = null,
        [Description("Line sets to create or update.")] Lineset[]? linesets = null,
        [Description("Line set names to remove.")] string[]? removeLinesets = null,
        [Description("View layer; the active one otherwise.")] string? viewLayer = null,
        [Description(ToolDescriptions.Parameters.Scene)] string? scene = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SetFreestyle,
            new JsonObject().With("enabled", enabled).With("line_thickness", lineThickness).With("thickness_mode", thicknessMode).With("crease_angle", creaseAngle)
                .With("culling", culling).With("linesets", linesets is null ? null : new JsonArray([.. linesets.Select(lineset => lineset.ToWire())])).With("remove_linesets", removeLinesets)
                .With("view_layer", viewLayer).With("scene", scene),
            cancellationToken);
}
