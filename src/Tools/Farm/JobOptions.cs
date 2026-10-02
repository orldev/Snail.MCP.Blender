using System.ComponentModel;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Farm;

/// <summary>What a background render job writes: format, depth, codec, size and samples for this job only.</summary>
public sealed record JobOutput : Block
{
    [Description("PNG, JPEG, OPEN_EXR, OPEN_EXR_MULTILAYER, TIFF, WEBP or FFMPEG; the scene's format otherwise.")]
    public string? FileFormat { get; init; }

    [Description(ToolDescriptions.Parameters.ColorDepth)]
    public int? ColorDepth { get; init; }

    [Description(ToolDescriptions.Parameters.ExrCodec)]
    public string? ExrCodec { get; init; }

    [Description("BW, RGB or RGBA.")]
    public string? ColorMode { get; init; }

    [Description("Width in pixels, up to 4096.")]
    public int? ResolutionX { get; init; }

    [Description("Height in pixels, up to 4096.")]
    public int? ResolutionY { get; init; }

    [Description("Resolution percentage 1 to 100.")]
    public int? Percentage { get; init; }

    [Description("Samples per pixel for this job only.")]
    public int? Samples { get; init; }

    [Description("Overwrite frames that exist; false skips them.")]
    public bool? Overwrite { get; init; }

    [Description("Write placeholders so several machines can share the range.")]
    public bool? Placeholder { get; init; }
}

/// <summary>How a job runs on this machine: how many processes, how they share the range, what happens after a crash.</summary>
public sealed record JobRun : Block
{
    [Description("Processes to split the frame range over; each renders every nth frame, so the sequence fills in evenly. 1 keeps one process.")]
    public int? Chunks { get; init; }

    [Description("Jobs allowed to run at once on this machine; the rest wait in the queue. 1 keeps one GPU to one render.")]
    public int? MaxParallel { get; init; }

    [Description("Restarts after a crash before the job counts as failed.")]
    public int? Retries { get; init; }

    [Description("Queue priority; higher starts first.")]
    public int? Priority { get; init; }

    [Description("CPU threads for the job; 0 uses every core.")]
    public int? Threads { get; init; }

    [Description("OpenColorIO config file the job renders under; the server's configured one otherwise, else Blender's own.")]
    public string? OcioConfig { get; init; }
}
