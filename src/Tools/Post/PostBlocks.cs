using System.ComponentModel;

namespace Snail.MCP.Blender.Tools.Post;

/// <summary>The Denoise node at the head of the post stack.</summary>
public sealed record Denoise : Block
{
    [Description("Off removes the block; on when any other field is given.")]
    public bool? Enabled { get; init; }

    [Description("Feed albedo and normal from the Cycles denoising data pass and switch Cycles' own denoising off; the studio way.")]
    public bool? UseDataPasses { get; init; }

    [Description("ACCURATE (the default), FAST or NONE.")]
    public string? Prefilter { get; init; }

    [Description("FOLLOW_SCENE, HIGH, BALANCED or FAST.")]
    public string? Quality { get; init; }

    [Description("Treat the image as HDR; on by default.")]
    public bool? Hdr { get; init; }
}

/// <summary>Depth of field from the Z pass instead of the camera.</summary>
public sealed record Defocus : Block
{
    public bool? Enabled { get; init; }

    [Description("f-number of the simulated lens; lower blurs more.")]
    public double? Fstop { get; init; }

    [Description("Largest blur radius in pixels.")]
    public double? MaxBlur { get; init; }

    [Description("CIRCLE, TRIANGLE, SQUARE, PENTAGON, HEXAGON, HEPTAGON or OCTAGON.")]
    public string? Bokeh { get; init; }

    [Description("Bokeh rotation in degrees.")]
    public double? Rotation { get; init; }

    [Description("Scale of the Z values; 1 for metres.")]
    public double? ZScale { get; init; }
}

/// <summary>Motion blur from the Vector pass; scene motion blur must be off.</summary>
public sealed record VectorBlur : Block
{
    public bool? Enabled { get; init; }

    [Description("Samples along the motion; 32 is smooth.")]
    public int? Samples { get; init; }

    [Description("Shutter time in frames; 0.5 is a 180° shutter.")]
    public double? Shutter { get; init; }
}

/// <summary>Glare: bloom, streaks, ghosts and their kin.</summary>
public sealed record Glare : Block
{
    public bool? Enabled { get; init; }

    [Description("BLOOM, GHOSTS, STREAKS, FOG_GLOW, SIMPLE_STAR or SUN_BEAMS.")]
    public string? Type { get; init; }

    [Description("Scene-linear value above which pixels glow; 1 is display white.")]
    public double? Threshold { get; init; }

    [Description("Softness of the threshold.")]
    public double? Smoothness { get; init; }

    [Description("Mix of the glare over the image, 0 to 1; 0.1 is subtle.")]
    public double? Strength { get; init; }

    [Description("Saturation of the glare; 1 keeps the source colour.")]
    public double? Saturation { get; init; }

    [Description("Tint [r, g, b].")]
    public double[]? Tint { get; init; }

    [Description("Spread of the glare; bloom 4 to 9, streaks in pixels.")]
    public double? Size { get; init; }

    [Description("Number of streaks.")]
    public int? Streaks { get; init; }

    [Description("Streak angle offset in degrees.")]
    public double? Angle { get; init; }

    [Description("Iterations for streaks and ghosts.")]
    public int? Iterations { get; init; }

    [Description("Fade of streaks along their length.")]
    public double? Fade { get; init; }

    [Description("HIGH, MEDIUM or LOW.")]
    public string? Quality { get; init; }
}

/// <summary>Halation: the red halo film puts around highlights.</summary>
public sealed record Halation : Block
{
    public bool? Enabled { get; init; }

    [Description("Scene-linear value above which the halo forms.")]
    public double? Threshold { get; init; }

    [Description("Halo radius; 6 is the default.")]
    public double? Size { get; init; }

    [Description("Mix of the halo, 0 to 1; 0.15 is film.")]
    public double? Strength { get; init; }

    [Description("Halo colour [r, g, b]; warm red by default.")]
    public double[]? Tint { get; init; }
}

/// <summary>Chromatic aberration and barrel distortion of a lens.</summary>
public sealed record ChromaticAberration : Block
{
    public bool? Enabled { get; init; }

    [Description("Colour fringing 0 to 1; 0.02 subtle, 0.1 a cheap lens.")]
    public double? Dispersion { get; init; }

    [Description("Barrel above 0, pincushion below.")]
    public double? Distortion { get; init; }

    [Description("Jitter samples to hide banding.")]
    public bool? Jitter { get; init; }

    [Description("Scale to keep the frame filled.")]
    public bool? Fit { get; init; }
}

/// <summary>Darkened corners.</summary>
public sealed record Vignette : Block
{
    public bool? Enabled { get; init; }

    [Description("Darkening 0 to 1; 0.3 is gentle.")]
    public double? Amount { get; init; }

    [Description("Edge softness 0 to 1.")]
    public double? Softness { get; init; }

    [Description("1 is an ellipse matching the frame, below 1 rounder.")]
    public double? Roundness { get; init; }
}

/// <summary>How grain weighs by luminance: film shows it in the mids, little in clipped highlights.</summary>
public sealed record GrainResponse : Block
{
    [Description("Weight in the shadows; 0.6 by default.")]
    public double? Shadows { get; init; }

    [Description("Weight in the midtones; 1 by default.")]
    public double? Midtones { get; init; }

    [Description("Weight in the highlights; 0.2 by default.")]
    public double? Highlights { get; init; }
}

/// <summary>Film grain, animated per frame and weighted by luminance.</summary>
public sealed record Grain : Block
{
    public bool? Enabled { get; init; }

    [Description("Amount 0 to 1; 0.05 to 0.15 reads as film.")]
    public double? Strength { get; init; }

    [Description("Grain size in pixels at output resolution; 1.5 for 35 mm at 4K.")]
    public double? Size { get; init; }

    [Description("0 monochrome grain, 1 full colour grain; 0.3 by default.")]
    public double? Chroma { get; init; }

    [Description("A new pattern every frame; on by default.")]
    public bool? Animated { get; init; }

    [Description("DISPLAY adds the grain after the view transform so clipped highlights stay clean; LINEAR before it.")]
    public string? Space { get; init; }

    public GrainResponse? Response { get; init; }
}
