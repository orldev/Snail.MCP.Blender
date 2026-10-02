namespace Snail.MCP.Blender.Tools.Prompts;

public static partial class ToolDescriptions
{
    /// <summary><c>SequencerTools</c>.</summary>
    public static class Video
    {
        /// <summary><c>blender_sequencer_info</c></summary>
        public const string Info = "The timeline: every strip with its channel, frames, trims, blend and source, plus the frame range, fps, resolution and output format.";

        /// <summary><c>blender_sequencer_add_strip</c></summary>
        public const string AddStrip =
            "Adds a strip to the timeline: a movie (with its sound), an image or an image sequence from a folder or file list, " +
            "a sound file, a 3D scene, a solid colour or a text title, placed on a channel from a frame, with optional fades, transform and blend.";

        /// <summary><c>blender_sequencer_add_effect</c></summary>
        public const string AddEffect =
            "Adds a transition or effect strip: a cross-fade or wipe between two strips where they overlap, a blur, glow or " +
            "speed change on one, or a generated colour or text.";

        /// <summary><c>blender_sequencer_update_strip</c></summary>
        public const string UpdateStrip =
            "Moves, trims, mutes, fades, re-blends, transforms or renames a strip; changes text, colour or volume.";

        /// <summary><c>blender_sequencer_remove_strip</c></summary>
        public const string RemoveStrip = "Removes one strip, or every strip with all=true.";

        /// <summary><c>blender_sequencer_render</c></summary>
        public const string Render =
            "Encodes the timeline to a video file with FFmpeg: container by extension, codec, quality and audio codec, or " +
            "whatever blender_sequencer_encode prepared. The frame range is fitted to the strips unless given. Long for big " +
            "timelines: set a timeout that fits.";
    }

    /// <summary><c>EditTools</c>.</summary>
    public static class Cuts
    {
        /// <summary><c>blender_sequencer_split</c></summary>
        public const string Split =
            "Cuts a strip at a frame into two strips on the same channel, keeping both or one side; movie, sound, image, " +
            "scene, colour and text strips. Effects and transitions are cut by cutting their inputs.";

        /// <summary><c>blender_sequencer_meta</c></summary>
        public const string Meta =
            "Groups strips into a meta strip, or separates one, through Blender's own operators; needs the Video Sequencer editor open in Blender.";

        /// <summary><c>blender_sequencer_proxy</c></summary>
        public const string Proxy =
            "Sets up proxies (25, 50, 75, 100 percent) for smooth playback of heavy footage, per strip or for all, with " +
            "optional project-level storage, and builds them when the Video Sequencer editor is open in Blender.";

        /// <summary><c>blender_sequencer_timing</c></summary>
        public const string Timing =
            "Sets the project timing: frame rate including fractional rates (23.976, 29.97, 59.94), sync mode for playback " +
            "(audio sync, frame drop), pixel aspect, resolution and the frame range.";

        /// <summary><c>blender_sequencer_grade</c></summary>
        public const string Grade =
            "Grades a strip with modifiers: colour balance (lift/gamma/gain or offset/power/slope), curves by channel, " +
            "hue correct, brightness and contrast, white balance, tonemap, optionally masked by another strip; lists, " +
            "updates or removes them.";
    }

    /// <summary><c>DeliveryTools</c>.</summary>
    public static class Delivery
    {
        /// <summary><c>blender_sequencer_encode</c></summary>
        public const string Encode =
            "Sets the encode without rendering: container, codec (H264, H265, AV1, ProRes with profile, DNxHD, FFV1, WebM), " +
            "quality by constant rate factor or a target bitrate with limits, GOP, B-frames, encoder preset, colour depth " +
            "and audio codec, bitrate, sample rate and channels. blender_sequencer_render then uses these settings.";

        /// <summary><c>blender_sequencer_mixdown</c></summary>
        public const string Mixdown =
            "Mixes every sound strip of the timeline down to one audio file: WAV, FLAC, MP3, OGG, AAC or AC3, with sample " +
            "format, bitrate and channel layout.";
    }
}
