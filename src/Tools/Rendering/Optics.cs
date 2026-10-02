using System.ComponentModel;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>The sensor behind the lens.</summary>
public sealed record Sensor : Block
{
    [Description("Width in mm: 36 full frame, 23.5 APS-C, 24.89 Super 35, 6.17 phone.")]
    public double? Width { get; init; }

    [Description("Height in mm; 24 for full frame.")]
    public double? Height { get; init; }

    [Description("AUTO, HORIZONTAL or VERTICAL: which sensor edge the field of view follows.")]
    public string? Fit { get; init; }
}

/// <summary>The iris: how much light and what shape of bokeh.</summary>
public sealed record Aperture : Block
{
    [Description("Depth of field on or off; on when any other field is given.")]
    public bool? Enabled { get; init; }

    [Description("f-number: 1.4 shallow, 8 deep.")]
    public double? Fstop { get; init; }

    [Description("Iris blades: 0 is a perfect circle, 5 to 9 give polygonal bokeh.")]
    public int? Blades { get; init; }

    [Description("Blade rotation in degrees.")]
    public double? Rotation { get; init; }

    [Description("Anamorphic squeeze: 1 spherical, 2 for 2x anamorphic bokeh.")]
    public double? Ratio { get; init; }
}

/// <summary>Where the lens focuses.</summary>
public sealed record Focus : Block
{
    [Description("Focus distance in metres; cleared when an object is given.")]
    public double? Distance { get; init; }

    [Description("Object to keep in focus.")]
    public string? Object { get; init; }

    [Description("Bone of the focus object, e.g. an eye, for rigs.")]
    public string? Bone { get; init; }
}

/// <summary>Cycles motion blur of one object.</summary>
public sealed record ObjectMotion : Block
{
    [Description("Blur this object's motion.")]
    public bool? Enabled { get; init; }

    [Description("Motion steps 1 to 7; more steps blur curved motion correctly.")]
    public int? Steps { get; init; }

    [Description("Blur deformation as well as transforms.")]
    public bool? Deform { get; init; }
}
