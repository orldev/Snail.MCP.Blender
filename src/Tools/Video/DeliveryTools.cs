using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Video;

/// <summary>Delivery: how the timeline is encoded, and the sound on its own.</summary>
[McpServerToolType]
[Skill(Skills.Video, SkillDescriptions.Video)]
public sealed class DeliveryTools(IBlenderBridge bridge) : BridgeToolBase(bridge)
{
    [McpServerTool(Name = "blender_sequencer_encode", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Delivery.Encode)]
    public Task<CallToolResult> EncodeAsync(
        [Description("mp4, mkv, webm, mov, avi or ogv; the extension of the render path can also decide.")] string? container = null,
        [Description("H264, H265, AV1, WEBM (VP9), PRORES, DNXHD, FFV1 (lossless), HUFFYUV, MPEG4, THEORA, PNG, QTRLE.")] string? codec = null,
        [Description(ToolDescriptions.Parameters.ProresProfile)] string? proresProfile = null,
        [Description("LOSSLESS, PERC_LOSSLESS, HIGH, MEDIUM, LOW, VERYLOW, LOWEST; constant quality instead of a bitrate.")] string? quality = null,
        [Description("Exact constant rate factor 0 to 51 for H.264/H.265/AV1; 18 is visually lossless, 23 the default.")] int? crf = null,
        [Description("Target bitrate in kbit/s; switches rate control from quality to bitrate.")] int? bitrate = null,
        [Description("Maximum bitrate in kbit/s.")] int? maxBitrate = null,
        [Description("Minimum bitrate in kbit/s.")] int? minBitrate = null,
        [Description("Rate control buffer in kbit.")] int? bufferSize = null,
        [Description("Keyframe interval in frames; smaller seeks better, larger compresses better.")] int? gop = null,
        [Description("B-frames between keyframes; 0 disables them.")] int? maxBFrames = null,
        [Description("Encoder speed preset: BEST (slowest, smallest), GOOD or REALTIME.")] string? preset = null,
        [Description("Lossless output for codecs that support it.")] bool? lossless = null,
        [Description("Split the file every 2 GB.")] bool? autosplit = null,
        [Description("8, 10 or 12 bits per channel for codecs that support it.")] int? colorDepth = null,
        [Description("Audio codec, bitrate, sample rate and channels.")] EncodeAudio? audio = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.SequencerEncode,
            new JsonObject().With("container", container).With("codec", codec).With("prores_profile", proresProfile).With("quality", quality).With("crf", crf)
                .With("bitrate", bitrate).With("max_bitrate", maxBitrate).With("min_bitrate", minBitrate).With("buffer_size", bufferSize).With("gop", gop)
                .With("max_b_frames", maxBFrames).With("preset", preset).With("lossless", lossless).With("autosplit", autosplit).With("color_depth", colorDepth).With("audio", audio),
            cancellationToken);

    [McpServerTool(Name = "blender_sequencer_mixdown", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Delivery.Mixdown)]
    public Task<CallToolResult> MixdownAsync(
        [Description("Absolute path of the audio file; the extension picks the container: .wav, .flac, .mp3, .ogg, .aac, .ac3.")] string path,
        [Description("PCM, FLAC, MP3, VORBIS, AAC, AC3 or MP2; the container's natural codec otherwise.")] string? codec = null,
        [Description("Sample format: U8, S16, S24, S32, F32 or F64; F32 for WAV, S16 otherwise.")] string? format = null,
        [Description("Bitrate in kbit/s for lossy codecs; 192 is the default.")] int? bitrate = null,
        [Description("MONO, STEREO, STEREO_LFE, SURROUND4, SURROUND5, SURROUND51, SURROUND61 or SURROUND71.")] string? channels = null,
        [Description("Write each channel to its own file.")] bool splitChannels = false,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 300,
        CancellationToken cancellationToken = default) =>
        WithinAsync(timeoutSeconds, timeout => SendAsync(BridgeCommands.SequencerMixdown,
            new JsonObject().With("path", path).With("codec", codec).With("format", format).With("bitrate", bitrate).With("channels", channels).With("split_channels", splitChannels), cancellationToken, timeout));
}
