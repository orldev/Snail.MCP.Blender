using System.ComponentModel;

namespace Snail.MCP.Blender.Tools.Rendering;

/// <summary>Which collections a view layer renders and how.</summary>
public sealed record LayerCollections : Block
{
    [Description("Collections rendered normally; also clears holdout and indirect-only on them.")]
    public string[]? Include { get; init; }

    [Description("Collections left out of this layer.")]
    public string[]? Exclude { get; init; }

    [Description("Collections cut as transparent holes, for plates.")]
    public string[]? Holdout { get; init; }

    [Description("Collections that only contribute bounce light and reflections.")]
    public string[]? IndirectOnly { get; init; }

    [Description("Clear every exclusion, holdout and indirect-only first.")]
    public bool? Reset { get; init; }
}

/// <summary>Cryptomatte passes of a view layer.</summary>
public sealed record Cryptomatte : Block
{
    [Description("Matte by object.")]
    public bool? Object { get; init; }

    [Description("Matte by material.")]
    public bool? Material { get; init; }

    [Description("Matte by asset (the linked file).")]
    public bool? Asset { get; init; }

    [Description("Levels, an even number 2 to 16; 6 keeps three overlapping objects per pixel.")]
    public int? Levels { get; init; }

    [Description("Accurate mode for exact coverage.")]
    public bool? Accurate { get; init; }
}
