using System.Security.Cryptography;
using Snail.MCP.Blender.Application.Storage;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>One file of the volume read in the chunks the add-on sends: a stream that only goes forward, and that fails rather than hand over half a file.</summary>
public class VolumeReadStreamTests
{
    private const string Path = "robot/robot.blend";

    /// <summary>The add-on's side of a chunked read, as scripted by the test: what each offset answers.</summary>
    private sealed class Chunks(params Func<long, JsonObject?>[] answers) : IBlenderVolume
    {
        private int _asked;

        public Task<BridgeReply> SendAsync(BridgeCommand command, JsonObject parameters, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var answer = answers[Math.Min(_asked++, answers.Length - 1)](parameters["offset"]!.GetValue<long>());

            return Task.FromResult(answer is null
                ? BridgeReply.Failed(RecordedAnswers.Error("a file asked for where there is none"))
                : BridgeReply.Ok(answer));
        }
    }

    private static JsonObject Chunk(byte[] data, bool eof, string? sha256 = null)
    {
        var chunk = new JsonObject { ["data"] = Convert.ToBase64String(data), ["eof"] = eof };

        if (sha256 is not null)
        {
            chunk["sha256"] = sha256;
        }

        return chunk;
    }

    private static Stream Open(IBlenderVolume link, long size) => new Volume(link).OpenRead(VolumeAreas.Files, Path, size);

    [Fact]
    public async Task AFileSentInChunks_ArrivesWhole_AndItsDigestIsChecked()
    {
        var first = "the first half "u8.ToArray();
        var second = "and the second"u8.ToArray();
        var whole = (byte[])[.. first, .. second];
        var link = new Chunks(
            _ => Chunk(first, eof: false),
            _ => Chunk(second, eof: true, Convert.ToHexStringLower(SHA256.HashData(whole))));
        await using var stream = Open(link, whole.Length);
        using var read = new MemoryStream();

        await stream.CopyToAsync(read);

        Assert.Equal(whole, read.ToArray());
        Assert.Equal(whole.Length, stream.Position);
    }

    /// <summary>A reader that hands over an array and a range is the older half of the Stream contract, and a tar writer still uses it.</summary>
    [Fact]
    public async Task ReadingIntoAnArrayAndRange_FillsItFromTheCurrentPosition()
    {
        var data = "a small file"u8.ToArray();
        var link = new Chunks(_ => Chunk(data, eof: true, Convert.ToHexStringLower(SHA256.HashData(data))));
        await using var stream = Open(link, data.Length);
        var buffer = new byte[64];

        var count = await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);

        Assert.Equal(data.Length, count);
        Assert.Equal(data, buffer[..count]);
    }

    [Fact]
    public async Task AReadTheAddOnRefuses_SaysWhereItStopped()
    {
        var link = new Chunks(_ => Chunk("the first half"u8.ToArray(), eof: false), _ => null);
        await using var stream = Open(link, 1024);
        using var read = new MemoryStream();

        var failure = await Assert.ThrowsAsync<IOException>(() => stream.CopyToAsync(read));

        Assert.Contains("stopped at 14 bytes", failure.Message, StringComparison.Ordinal);
        Assert.Contains(RecordedAnswers.Error("a file asked for where there is none").Message, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A chunk of nothing that is not the end would otherwise be read as the end of the file, and the copy would be short and silent.</summary>
    [Fact]
    public async Task AFileThatStopsGrowingBeforeItsEnd_FailsTheRead()
    {
        var link = new Chunks(_ => Chunk("the first half"u8.ToArray(), eof: false), _ => Chunk([], eof: false));
        await using var stream = Open(link, 1024);
        using var read = new MemoryStream();

        var failure = await Assert.ThrowsAsync<IOException>(() => stream.CopyToAsync(read));

        Assert.Contains("stopped growing at 14 bytes", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFileThatArrivedDifferentFromTheVolume_FailsTheRead()
    {
        var data = "a small file"u8.ToArray();
        var link = new Chunks(_ => Chunk(data, eof: true, Convert.ToHexStringLower(SHA256.HashData("something else"u8.ToArray()))));
        await using var stream = Open(link, data.Length);
        using var read = new MemoryStream();

        var failure = await Assert.ThrowsAsync<IOException>(() => stream.CopyToAsync(read));

        Assert.Contains("arrived different", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The bytes arrive once, in order, from a machine that may be on the other side of a tunnel: anything that would ask for them twice is refused rather than quietly re-fetched.</summary>
    [Fact]
    public async Task TheStream_OnlyGoesForward_AndIsNeverWritten()
    {
        var link = new Chunks(_ => Chunk([], eof: true));
        await using var stream = Open(link, 42);

        await stream.FlushAsync();

        Assert.True(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(42, stream.Length);
        Assert.Equal(0, stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 8);
        Assert.Throws<NotSupportedException>(() => stream.Seek(8, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(8));
        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[8], 0, 8));
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[8], 0, 8));
    }
}
