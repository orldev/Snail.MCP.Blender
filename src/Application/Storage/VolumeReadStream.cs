using System.Security.Cryptography;
using Snail.MCP.Blender.Application.Transfer;

namespace Snail.MCP.Blender.Application.Storage;

/// <summary>One file of an area read in the chunks the add-on sends, fetched only as the reader asks for bytes.</summary>
/// <remarks>The length is known from the listing, which is what a tar header needs before the bytes arrive. The last chunk carries the digest
/// of the whole file; a mismatch fails the read rather than hand over a file that differs from the one on the volume.</remarks>
internal sealed class VolumeReadStream(IBlenderVolume link, string area, string path, long size) : Stream
{
    private readonly IncrementalHash _digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private byte[] _chunk = [];
    private int _chunkPosition;
    private long _position;
    private bool _ended;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => size;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_chunkPosition == _chunk.Length && !_ended)
        {
            await FetchAsync(cancellationToken).ConfigureAwait(false);
        }

        var count = Math.Min(buffer.Length, _chunk.Length - _chunkPosition);
        _chunk.AsMemory(_chunkPosition, count).CopyTo(buffer);
        _chunkPosition += count;
        _position += count;

        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _digest.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task FetchAsync(CancellationToken cancellationToken)
    {
        var reply = await link.SendAsync(
            BridgeCommands.FileGet,
            new JsonObject { ["area"] = area, ["path"] = path, ["offset"] = _position, ["length"] = FileTransfer.DownloadChunkBytes },
            TransferTimeouts.For(size),
            cancellationToken).ConfigureAwait(false);

        if (!reply.IsOk || reply.Result is not JsonObject chunk || chunk["data"]?.GetValue<string>() is not { } data)
        {
            throw new IOException($"'{area}/{path}' stopped at {_position} bytes: {reply.Error?.Message ?? "the add-on answered without data"}");
        }

        _chunk = Convert.FromBase64String(data);
        _chunkPosition = 0;
        _digest.AppendData(_chunk);
        _ended = chunk["eof"]?.GetValue<bool>() == true;

        if (_ended && chunk["sha256"]?.GetValue<string>() is { } expected && !string.Equals(expected, Convert.ToHexStringLower(_digest.GetHashAndReset()), StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"'{area}/{path}' arrived different from the file on the volume");
        }

        if (_chunk.Length == 0 && !_ended)
        {
            throw new IOException($"'{area}/{path}' stopped growing at {_position} bytes before its end");
        }
    }
}
