using System.ComponentModel;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>Metadata burnt into the frame.</summary>
public sealed record Stamp : Block
{
    [Description("Stamps on or off.")]
    public bool? Enabled { get; init; }

    [Description("Free text, e.g. the version; switches the note field on.")]
    public string? Note { get; init; }

    public int? FontSize { get; init; }

    public bool? Time { get; init; }

    public bool? Date { get; init; }

    public bool? Frame { get; init; }

    public bool? FrameRange { get; init; }

    public bool? Camera { get; init; }

    public bool? Lens { get; init; }

    public bool? Scene { get; init; }

    public bool? Marker { get; init; }

    public bool? Filename { get; init; }

    public bool? RenderTime { get; init; }

    public bool? Memory { get; init; }

    public bool? Hostname { get; init; }
}

/// <summary>Stereoscopic and multi-view rendering.</summary>
public sealed record Stereo : Block
{
    [Description("Multi-view rendering on or off.")]
    public bool? Enabled { get; init; }

    [Description("STEREO_3D (left and right eye) or MULTIVIEW (any number of cameras).")]
    public string? Mode { get; init; }

    [Description("File layout: INDIVIDUAL files per view or STEREO_3D packed (anaglyph, interlace, side by side).")]
    public string? Format { get; init; }

    [Description("OFFAXIS (the default), PARALLEL or TOE_IN.")]
    public string? ConvergenceMode { get; init; }

    [Description("Distance in metres where the eyes converge.")]
    public double? ConvergenceDistance { get; init; }

    [Description("Distance between the eyes in metres; 0.065 is human.")]
    public double? InterocularDistance { get; init; }

    [Description("CENTER, LEFT or RIGHT: which eye the camera object stands for.")]
    public string? Pivot { get; init; }
}

/// <summary>White balance of the view.</summary>
public sealed record WhiteBalance : Block
{
    [Description("On or off; on when temperature or tint is given.")]
    public bool? Enabled { get; init; }

    [Description("Kelvin; 6500 is neutral daylight, lower warms.")]
    public double? Temperature { get; init; }

    [Description("Green to magenta shift.")]
    public double? Tint { get; init; }
}
