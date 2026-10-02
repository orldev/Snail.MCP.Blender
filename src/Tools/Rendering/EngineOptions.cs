using System.ComponentModel;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>Cycles light path bounces.</summary>
public sealed record Bounces : Block
{
    [Description("Total bounces; 12 is the default.")]
    public int? Total { get; init; }

    public int? Diffuse { get; init; }

    public int? Glossy { get; init; }

    public int? Transmission { get; init; }

    public int? Volume { get; init; }

    [Description("Transparent bounces; raise for leaves and hair with alpha.")]
    public int? Transparent { get; init; }
}

/// <summary>Cycles caustics.</summary>
public sealed record Caustics : Block
{
    public bool? Reflective { get; init; }

    public bool? Refractive { get; init; }

    [Description("Blur glossy reflections after blurry bounces to kill fireflies; 1 is the default.")]
    public double? BlurGlossy { get; init; }
}

/// <summary>Cycles sample clamping.</summary>
public sealed record Clamp : Block
{
    [Description("Clamp direct light; 0 is none.")]
    public double? Direct { get; init; }

    [Description("Clamp indirect light; 10 removes fireflies with little energy loss.")]
    public double? Indirect { get; init; }
}

/// <summary>EEVEE screen-space ray tracing options.</summary>
public sealed record RaytracingOptions : Block
{
    [Description("Trace resolution: \"1\", \"2\" or \"4\" (divisor).")]
    public string? Resolution { get; init; }

    [Description("Roughness above which reflections fall back to probes.")]
    public double? MaxRoughness { get; init; }

    [Description("Trace precision 0 to 1.")]
    public double? Quality { get; init; }

    [Description("Surface thickness assumed by the screen trace.")]
    public double? Thickness { get; init; }

    public bool? Denoise { get; init; }

    public bool? DenoiseSpatial { get; init; }

    public bool? DenoiseTemporal { get; init; }

    public bool? DenoiseBilateral { get; init; }
}

/// <summary>EEVEE fast global illumination.</summary>
public sealed record FastGiOptions : Block
{
    [Description("AMBIENT_OCCLUSION_ONLY or GLOBAL_ILLUMINATION.")]
    public string? Method { get; init; }

    public double? Quality { get; init; }

    public int? Rays { get; init; }

    public int? Steps { get; init; }

    [Description("Distance in metres; 0 is unlimited.")]
    public double? Distance { get; init; }

    [Description("\"1\", \"2\" or \"4\".")]
    public string? Resolution { get; init; }

    public double? Bias { get; init; }

    public double? Thickness { get; init; }
}

/// <summary>EEVEE volumetrics.</summary>
public sealed record Volumetrics : Block
{
    [Description("\"2\", \"4\", \"8\" or \"16\"; smaller is finer and slower.")]
    public string? TileSize { get; init; }

    public int? Samples { get; init; }

    [Description("Sample distribution 0 to 1; higher packs samples near the camera.")]
    public double? Distribution { get; init; }

    public double? Start { get; init; }

    public double? End { get; init; }

    public bool? CustomRange { get; init; }

    public bool? Shadows { get; init; }

    public int? ShadowSamples { get; init; }

    public int? RayDepth { get; init; }

    public double? LightClamp { get; init; }
}

/// <summary>EEVEE light clamping.</summary>
public sealed record EeveeClamp : Block
{
    public double? SurfaceDirect { get; init; }

    public double? SurfaceIndirect { get; init; }

    public double? VolumeDirect { get; init; }

    public double? VolumeIndirect { get; init; }
}

/// <summary>A Freestyle line set: which edges draw and how.</summary>
public sealed record Lineset : Block
{
    [Description("Line set name; LineSet is Blender's default one.")]
    public string? Name { get; init; }

    public bool? Enabled { get; init; }

    [Description("Edge types that draw: silhouette, border, crease, contour, external_contour, material_boundary, edge_mark, suggestive_contour, ridge_valley.")]
    public string[]? Edges { get; init; }

    [Description("VISIBLE, HIDDEN or RANGE.")]
    public string? Visibility { get; init; }

    [Description("Line colour [r, g, b].")]
    public double[]? Color { get; init; }

    [Description("Line thickness in pixels.")]
    public double? Thickness { get; init; }

    public double? Alpha { get; init; }
}

/// <summary>EEVEE depth-of-field bokeh.</summary>
public sealed record Bokeh : Block
{
    [Description("Largest bokeh in pixels.")]
    public double? MaxSize { get; init; }

    public double? Threshold { get; init; }

    public double? NeighborMax { get; init; }

    public bool? Jittered { get; init; }

    public double? Overblur { get; init; }
}
