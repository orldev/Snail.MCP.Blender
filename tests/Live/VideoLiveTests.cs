using Snail.MCP.Blender.Adapters.Blender;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>The sequencer against a real Blender: strips, a transition, a fade, and an MP4 out of FFmpeg; and a professional cut: splits, an image sequence, fractional timing, grading, a ProRes encode setting, an H.264 encode and an audio mixdown.</summary>
[Collection("Loopback")]
public sealed class VideoLiveTests : IAsyncLifetime
{
    private HeadlessBlender _blender = null!;
    private BlenderConnection _link = null!;
    private string _workDirectory = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _link = new BlenderConnection(_blender.Link, NullLogger<BlenderConnection>.Instance);
        _workDirectory = Path.Combine(Path.GetTempPath(), "snail-blender-live", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
    }

    public async Task DisposeAsync()
    {
        if (_link is not null) await _link.DisposeAsync();

        _blender?.Dispose();

        if (_workDirectory is not null && Directory.Exists(_workDirectory)) Directory.Delete(_workDirectory, recursive: true);
    }

    [BlenderFact]
    public async Task Timeline_ColourTextCrossAndFade_EncodesToMp4()
    {
        var red = await Send(BridgeCommands.SequencerAddStrip, new JsonObject { ["type"] = "color", ["name"] = "Red", ["color"] = new JsonArray(0.8, 0.1, 0.1), ["channel"] = 1, ["frame_start"] = 1, ["length"] = 24 });
        Assert.Equal(24, red["duration"]!.GetValue<int>());

        await Send(BridgeCommands.SequencerAddStrip, new JsonObject { ["type"] = "color", ["name"] = "Blue", ["color"] = new JsonArray(0.1, 0.1, 0.8), ["channel"] = 2, ["frame_start"] = 13, ["length"] = 24 });
        var title = await Send(BridgeCommands.SequencerAddStrip, new JsonObject { ["type"] = "text", ["name"] = "Title", ["text"] = "Snail", ["font_size"] = 90, ["channel"] = 3, ["frame_start"] = 1, ["length"] = 36, ["fade"] = new JsonObject { ["in"] = 6, ["out"] = 6 } });
        Assert.Equal("Snail", title["text"]!.ToString());

        var cross = await Send(BridgeCommands.SequencerAddEffect, new JsonObject { ["type"] = "CROSS", ["inputs"] = new JsonArray("Red", "Blue") });
        Assert.Equal(13, cross["start"]!.GetValue<int>());
        Assert.Equal(12, cross["duration"]!.GetValue<int>());

        var moved = await Send(BridgeCommands.SequencerUpdateStrip, new JsonObject { ["name"] = "Title", ["transform"] = new JsonObject { ["offset"] = new JsonArray(0, 40) }, ["opacity"] = 0.9 });
        Assert.Equal(40, moved["transform"]!["offset"]![1]!.GetValue<double>());

        var info = await Send(BridgeCommands.SequencerInfo);
        Assert.Equal(4, info["strips"]!.AsArray().Count);

        var path = Path.Combine(_workDirectory, "cut.mp4");
        var encoded = await Send(BridgeCommands.SequencerRender, new JsonObject { ["path"] = path, ["resolution_x"] = 320, ["resolution_y"] = 180, ["fps"] = 24 }, TimeSpan.FromSeconds(300));

        Assert.True(File.Exists(encoded["path"]!.ToString()), encoded.ToJsonString());
        Assert.True(encoded["bytes"]!.GetValue<long>() > 1000);
        Assert.Equal("MPEG4", encoded["container"]!.ToString());
        Assert.Equal([1, 36], encoded["frames"]!.AsArray().Select(node => node!.GetValue<int>()));

        var removed = await Send(BridgeCommands.SequencerRemoveStrip, new JsonObject { ["all"] = true });
        Assert.Equal(4, removed["removed"]!.AsArray().Count);
    }

    [BlenderFact]
    public async Task Cut_SplitSequenceTimingGradeEncodeAndMixdown_AllLand()
    {
        await Send(BridgeCommands.SequencerAddStrip, new JsonObject { ["type"] = "color", ["name"] = "Red", ["color"] = new JsonArray(0.8, 0.1, 0.1), ["channel"] = 1, ["frame_start"] = 1, ["length"] = 48 });

        var split = await Send(BridgeCommands.SequencerSplit, new JsonObject { ["name"] = "Red", ["frame"] = 20 });
        Assert.Equal(20, split["left"]!["end"]!.GetValue<int>());
        Assert.Equal(20, split["right"]!["start"]!.GetValue<int>());
        Assert.Equal(1, split["right"]!["channel"]!.GetValue<int>());

        var frames = Path.Combine(_workDirectory, "frames");
        Directory.CreateDirectory(frames);
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 32, ["resolution_y"] = 32, ["samples"] = 1 });
        await Send(BridgeCommands.RenderAnimation, new JsonObject { ["output_path"] = Path.Combine(frames, "f_####"), ["start"] = 1, ["end"] = 3, ["file_format"] = "PNG" }, TimeSpan.FromSeconds(120));

        var sequence = await Send(BridgeCommands.SequencerAddStrip, new JsonObject { ["type"] = "image", ["name"] = "Seq", ["path"] = frames, ["channel"] = 2, ["frame_start"] = 1 });
        Assert.Equal(3, sequence["frames"]!.GetValue<int>());
        Assert.Equal(3, sequence["duration"]!.GetValue<int>());

        var timing = await Send(BridgeCommands.SequencerTiming, new JsonObject { ["fps"] = 23.976, ["sync_mode"] = "AUDIO_SYNC" });
        Assert.Equal(24, timing["fps"]!.GetValue<int>());
        Assert.Equal(23.976, timing["effective_fps"]!.GetValue<double>());

        var graded = await Send(BridgeCommands.SequencerGrade, new JsonObject { ["name"] = "Red", ["type"] = "COLOR_BALANCE", ["color_balance"] = new JsonObject { ["method"] = "OFFSET_POWER_SLOPE", ["slope"] = new JsonArray(1.1, 1.0, 0.9) } });
        Assert.Equal("OFFSET_POWER_SLOPE", graded["modifiers"]![0]!["color_balance"]!["method"]!.ToString());

        var curved = await Send(BridgeCommands.SequencerGrade, new JsonObject { ["name"] = "Red", ["type"] = "CURVES", ["curves"] = new JsonObject { ["C"] = new JsonArray(new JsonArray(0, 0), new JsonArray(0.3, 0.2), new JsonArray(1, 1)) } });
        Assert.Equal(2, curved["modifiers"]!.AsArray().Count);

        var prores = await Send(BridgeCommands.SequencerEncode, new JsonObject { ["container"] = "mov", ["prores_profile"] = "422_HQ", ["audio"] = new JsonObject { ["codec"] = "PCM" } });
        Assert.Equal("PRORES", prores["codec"]!.ToString());
        Assert.Equal("422_HQ", prores["prores_profile"]!.ToString());

        var h264 = await Send(BridgeCommands.SequencerEncode, new JsonObject { ["codec"] = "H264", ["crf"] = 18, ["gop"] = 12, ["audio"] = new JsonObject { ["codec"] = "AAC", ["bitrate"] = 256 } });
        Assert.Equal(18, h264["crf"]!.GetValue<int>());

        var wav = Path.Combine(_workDirectory, "tone.wav");
        await File.WriteAllBytesAsync(wav, SineWave());
        await Send(BridgeCommands.SequencerAddStrip, new JsonObject { ["type"] = "sound", ["name"] = "Tone", ["path"] = wav, ["channel"] = 3, ["frame_start"] = 1 });

        var encoded = await Send(BridgeCommands.SequencerRender, new JsonObject { ["path"] = Path.Combine(_workDirectory, "cut.mp4"), ["resolution_x"] = 64, ["resolution_y"] = 64 }, TimeSpan.FromSeconds(300));
        Assert.True(File.Exists(encoded["path"]!.ToString()));
        Assert.Equal("H264", encoded["codec"]!.ToString());
        Assert.Equal(18, encoded["crf"]!.GetValue<int>());

        var mixed = await Send(BridgeCommands.SequencerMixdown, new JsonObject { ["path"] = Path.Combine(_workDirectory, "mix.wav") }, TimeSpan.FromSeconds(120));
        Assert.True(mixed["bytes"]!.GetValue<long>() > 1000, mixed.ToJsonString());
        Assert.Equal("WAV", mixed["container"]!.ToString());
    }

    private static byte[] SineWave()
    {
        const int rate = 48000;
        var samples = new short[rate];

        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)(Math.Sin(2 * Math.PI * 440 * index / rate) * 12000);
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var dataBytes = samples.Length * 2;

        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);

        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();

        return stream.ToArray();
    }

    /// <summary>sequencer_info counts as a read, yet it created a sequence editor on a scene that had none, a change nothing recorded. A new scene
    /// in Blender 5.2 already carries an editor, so the test clears it first.</summary>
    [BlenderFact]
    public async Task SequencerInfo_OfASceneWithoutAnEditor_CreatesNone()
    {
        await Send(BridgeCommands.Python, new JsonObject { ["code"] = "import bpy\nbpy.context.scene.sequence_editor_clear()" });

        var info = await Send(BridgeCommands.SequencerInfo);
        var editor = await Send(BridgeCommands.Python, new JsonObject { ["code"] = "import bpy\nresult = bpy.context.scene.sequence_editor is None" });

        Assert.Empty(info["strips"]!.AsArray());
        Assert.True(editor["result"]!.GetValue<bool>(), "sequencer_info created a sequence editor");
    }

    private async Task<JsonNode> Send(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await _link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
