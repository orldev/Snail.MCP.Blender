namespace Snail.MCP.Blender.Tools.Prompts;

/// <summary>Pipeline recipes served as MCP prompts: the order of tools a studio would follow; a contract with the agent, guarded like the descriptions.</summary>
public static class PromptTexts
{
    public const string ProductShotName = "photoreal_product_shot";

    public const string ProductShotDescription = "Set up, light, render and check a photoreal product shot of one object, with the post stack a compositor would build.";

    public const string ProductShot =
        """
        Goal: a photoreal still of {subject}, delivered as {output}.
        Work in this order and read every reply before the next call; stop and ask when a reply is not ok.
        1. blender_enable_skill rendering, post and farm. blender_scene_info, then blender_object_info for {subject}: confirm it has a material and a sensible scale in metres.
        2. Light: blender_set_world with an HDRI when one is available (hdriPath, strength 1, rotation to taste), otherwise a neutral grey world at strength 0.3 plus blender_light_rig around {subject} (key 5600 K, fill and rim), or blender_add_light AREA by hand with temperature in kelvin.
        3. Camera: blender_set_camera_optics with a real sensor (36 mm full frame), lens 50 to 85 mm for products, aperture fstop 4 to 8 with focus on {subject}; blender_render_object frames it automatically when a camera is not placed yet.
        4. Engine: blender_set_cycles with samples 512 to 1024, adaptiveThreshold 0.01, denoiser OPENIMAGEDENOISE with denoisePrefilter ACCURATE, lightTree true, clamp indirect 10 to tame fireflies.
        5. Colour: blender_set_color_management viewTransform AgX, look Base Contrast or Punchy, exposure 0.
        6. Post: blender_lens_effects with denoise useDataPasses true, glare BLOOM threshold 1 strength 0.1, chromaticAberration dispersion 0.01, vignette amount 0.25, grain strength 0.05 size 1.5; nothing stronger, restraint reads as real.
        7. Preview: blender_render_settings percentage 25, then blender_render_image or blender_render_object; look at the returned picture and stats: mean_luminance near 0.18 to 0.3, clipped_high under 0.01. Fix exposure, light energy or fstop and repeat until it reads right.
        8. Final: blender_render_check must be ready; blender_set_output with the delivery format ({output}), OPEN_EXR 32 bit ZIP when compositing follows; percentage 100; blender_render_job for anything longer than a minute and poll blender_render_job_status, or blender_render_image when it is quick.
        9. Report the file path, the settings that mattered and what you would still change.
        """;

    public const string FarmDeliveryName = "farm_exr_delivery";

    public const string FarmDeliveryDescription = "Prepare a scene for a multi-layer EXR render with passes, cryptomatte and light groups, check it, and run it as a background job or hand it to a farm.";

    public const string FarmDelivery =
        """
        Goal: render frames {frames} of the current scene as multi-layer EXR for compositing, into {directory}.
        1. blender_enable_skill rendering, post and farm. blender_snapshot save first. blender_scene_info; blender_render_check with outputPath {directory}/beauty_#### and fix every error it lists before anything else.
        2. Layers: blender_view_layer with passes z true, normal true, vector true (only if scene motion blur is off, blender_set_motion_blur reports it), mist true, cryptomatte object and material true with levels 6, and lightGroups per light family (key, fill, rim, env) with lightGroupMembers. Holdout and indirect-only collections through collections when plates are involved; per-object flags through blender_object_render_flags.
        3. Output: blender_set_output fileFormat OPEN_EXR_MULTILAYER, colorDepth 32 for data passes (16 when only beauty matters), exrCodec ZIP (never DWAA on data), overwrite false and placeholder true when several machines share the range, stamp with note and frame for review copies only.
        4. Engine: blender_set_cycles with the final samples, adaptive sampling, denoise false and denoising data through blender_lens_effects denoise useDataPasses true so the compositor denoises; persistentData true for animations.
        5. Budget: blender_render_budget with a probe at 10 percent; apply textureLimit or simplify when the verdict is tight or exceeds; packForFarm with packExternal true, relativePaths true and copyTo when the file leaves this machine.
        6. Run: blender_render_job with frames {frames}, outputPath {directory}/{scene}_{camera}_####, output carrying the format of step 3, queue with chunks 2 to 4 on a multi-core CPU machine, maxParallel 1 on one GPU and retries 1. Poll blender_render_job_status; report eta_s and peak_memory_mb; blender_render_job_cancel when something is wrong. blender_inspect_image on a finished frame to confirm the passes landed.
        7. Report the frame paths, the layer and pass list, and the budget verdict.
        """;

    public const string VideoDeliveryName = "vse_grade_and_deliver";

    public const string VideoDeliveryDescription = "Cut, grade and encode a timeline in the Video Sequence Editor to a delivery codec, with a separate audio mixdown.";

    public const string VideoDelivery =
        """
        Goal: assemble {sources} into a graded cut delivered as {delivery}.
        1. blender_enable_skill video. blender_sequencer_timing with the delivery frame rate (23.976, 24, 25, 29.97 or 30), resolution and syncMode AUDIO_SYNC.
        2. Bring in sources with blender_sequencer_add_strip: movies with their sound, image sequences by folder, titles as text strips; blender_sequencer_info after each to confirm channels and ranges.
        3. Cut with blender_sequencer_split and trims in blender_sequencer_update_strip; transitions with blender_sequencer_add_effect CROSS or GAMMA_CROSS over the overlaps; fades through the fade parameter.
        4. Heavy footage: blender_sequencer_proxy sizes 25 for editing; it builds only with the sequencer editor open in Blender.
        5. Grade each strip with blender_sequencer_grade: COLOR_BALANCE OFFSET_POWER_SLOPE for the primary, CURVES for contrast, WHITE_BALANCE when sources differ; blender_set_color_management for the sequencer colour space when footage is log.
        6. Encode settings with blender_sequencer_encode: ProRes 422 HQ in a mov for masters, H264 crf 18 gop 12 in an mp4 for review, audio AAC 256 kbit or PCM for masters. Then blender_sequencer_render with the delivery path; blender_sequencer_mixdown to a wav next to it.
        7. Report the file paths, the codec settings and the strips that were graded.
        """;
}
