using System.ComponentModel;

namespace Snail.MCP.Blender.Tools.Video;

/// <summary>Where a strip's picture sits in the frame.</summary>
public sealed record StripTransform : Block
{
    [Description("[x, y] offset in pixels.")]
    public double[]? Offset { get; init; }

    [Description("[sx, sy] scale factors.")]
    public double[]? Scale { get; init; }

    [Description("Rotation in degrees.")]
    public double? Rotation { get; init; }

    [Description("[left, right, bottom, top] crop in pixels.")]
    public int[]? Crop { get; init; }

    [Description("[x, y] mirror flags.")]
    public bool[]? Flip { get; init; }
}

/// <summary>Fades in frames: opacity for pictures, volume for sound.</summary>
public sealed record StripFade : Block
{
    [Description("Frames of fade-in.")]
    public int? In { get; init; }

    [Description("Frames of fade-out.")]
    public int? Out { get; init; }
}

/// <summary>Primary grade: lift, gamma, gain or offset, power, slope; 1 is neutral.</summary>
public sealed record ColorBalance : Block
{
    [Description("LIFT_GAMMA_GAIN or OFFSET_POWER_SLOPE.")]
    public string? Method { get; init; }

    public double[]? Lift { get; init; }

    public double[]? Gamma { get; init; }

    public double[]? Gain { get; init; }

    public double[]? Offset { get; init; }

    public double[]? Power { get; init; }

    public double[]? Slope { get; init; }
}

/// <summary>Audio of the encode.</summary>
public sealed record EncodeAudio : Block
{
    [Description("AAC, MP3, VORBIS, OPUS, FLAC, PCM, AC3 or NONE.")]
    public string? Codec { get; init; }

    [Description("kbit/s for lossy codecs.")]
    public int? Bitrate { get; init; }

    [Description("Sample rate in Hz; 48000 for video.")]
    public int? SampleRate { get; init; }

    [Description("MONO, STEREO, STEREO_LFE, SURROUND4, SURROUND5, SURROUND51, SURROUND61 or SURROUND71.")]
    public string? Channels { get; init; }

    [Description("Volume; 1 keeps the mix level.")]
    public double? Volume { get; init; }
}
