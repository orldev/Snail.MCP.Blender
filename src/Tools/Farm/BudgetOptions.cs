using System.ComponentModel;

namespace Snail.MCP.Blender.Tools.Farm;

/// <summary>A probe render that times the frame at low resolution.</summary>
public sealed record Probe : Block
{
    [Description("Off skips the probe; on when any other field is given.")]
    public bool? Enabled { get; init; }

    [Description("Resolution percentage of the probe, 1 to 50; 10 by default.")]
    public int? Percentage { get; init; }

    [Description("Samples for the probe; the scene's otherwise.")]
    public int? Samples { get; init; }
}

/// <summary>Simplify: caps that keep heavy scenes renderable.</summary>
public sealed record Simplify : Block
{
    public bool? Enabled { get; init; }

    [Description("Maximum subdivision level at render time.")]
    public int? Subdivision { get; init; }

    [Description("Fraction of child particles rendered, 0 to 1.")]
    public double? ChildParticles { get; init; }

    [Description("Volume resolution fraction, 0 to 1.")]
    public double? Volumes { get; init; }
}

/// <summary>Limits applied right away.</summary>
public sealed record Limits : Block
{
    [Description("Largest texture edge at render time: OFF, 128, 256, 512, 1024, 2048, 4096 or 8192.")]
    public string? TextureLimit { get; init; }

    public Simplify? Simplify { get; init; }

    [Description("Cycles tile size in pixels; smaller lowers peak memory.")]
    public int? TileSize { get; init; }

    [Description("CPU, or a device name or type from the reply's devices list.")]
    public string? Device { get; init; }
}

/// <summary>Preparing the file to leave this machine.</summary>
public sealed record FarmPack : Block
{
    public bool? Enabled { get; init; }

    [Description("Pack external images and files into the .blend.")]
    public bool? PackExternal { get; init; }

    [Description("Make every path relative to the file; needs a saved file.")]
    public bool? RelativePaths { get; init; }

    [Description("Absolute path for a self-contained copy.")]
    public string? CopyTo { get; init; }
}
