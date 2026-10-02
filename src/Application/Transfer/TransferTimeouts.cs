namespace Snail.MCP.Blender.Application.Transfer;

/// <summary>How long one request of a transfer is given, from the bytes the add-on has to read or hash to answer it.</summary>
/// <remarks>The add-on hashes the whole file when the last chunk of an upload arrives, and reads it whole while a download leaves, so the last
/// request of a large file takes far longer than any other. Under the link's flat timeout a large upload timed out on its last chunk while the
/// add-on went on to commit the file, and the retry was refused as already there. The rate is a deliberate under-estimate rather than a
/// measurement: hashing a file from network storage at 50 MB a second.</remarks>
public static class TransferTimeouts
{
    private const long HashedBytesPerSecond = 50L * 1024 * 1024;

    private static readonly TimeSpan Basis = TimeSpan.FromSeconds(30);

    public static TimeSpan For(long bytes, TimeSpan? basis = null) =>
        (basis ?? Basis) + TimeSpan.FromSeconds(Math.Max(bytes, 0) / (double)HashedBytesPerSecond);

    /// <summary>The time a chunk is given, read from the chunk itself: everything up to and including it may have to be hashed for it.</summary>
    public static TimeSpan ForChunk(JsonObject chunk, TimeSpan? basis = null) =>
        For(Bytes(chunk["offset"]) + Payload(chunk), basis);

    private static long Payload(JsonObject chunk) =>
        chunk["length"] is { } length ? Bytes(length) : (chunk["data"]?.GetValue<string>()?.Length ?? 0) / 4 * 3;

    /// <summary>A number a chunk carries, whichever width it was written with.</summary>
    private static long Bytes(JsonNode? value)
    {
        if (value is not JsonValue number)
        {
            return 0;
        }

        return number.TryGetValue<long>(out var bytes) ? bytes : number.TryGetValue<int>(out var narrow) ? narrow : 0;
    }
}
