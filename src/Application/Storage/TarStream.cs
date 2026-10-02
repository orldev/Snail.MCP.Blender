using System.Globalization;
using System.Text;

namespace Snail.MCP.Blender.Application.Storage;

/// <summary>A tar written as it goes into a stream that cannot seek, one file after another, each header carrying a size known in advance.</summary>
/// <remarks>System.Formats.Tar refuses to write a data stream that cannot seek into an archive that cannot seek, and both ends are exactly that
/// here: a file read chunk by chunk from Blender and an HTTP response. A ustar header holds a name of up to 100 ASCII bytes and a size below
/// 8 GiB; a longer or non-ASCII name, or a larger file, adds a PAX header in front, which every tar since 2001 reads.</remarks>
internal sealed class TarStream(Stream destination)
{
    private const int BlockSize = 512;

    /// <summary>Eleven octal digits of the ustar size field: 8 GiB less a byte. C# has no octal literals, so it is written out in decimal.</summary>
    private const long LargestUstarSize = 8_589_934_591;

    public async Task WriteFileAsync(string name, long size, long modified, Stream content, CancellationToken cancellationToken)
    {
        var isPortable = name.Length <= 100 && Encoding.UTF8.GetByteCount(name) == name.Length && size <= LargestUstarSize;

        if (!isPortable)
        {
            var records = Record("path", name) + (size > LargestUstarSize ? Record("size", size.ToString(CultureInfo.InvariantCulture)) : string.Empty);
            var body = Encoding.UTF8.GetBytes(records);

            await destination.WriteAsync(Header("PaxHeader", body.Length, modified, 'x'), cancellationToken).ConfigureAwait(false);
            await destination.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            await PadAsync(body.Length, cancellationToken).ConfigureAwait(false);
        }

        await destination.WriteAsync(Header(isPortable ? name : "file", Math.Min(size, LargestUstarSize), modified, '0'), cancellationToken).ConfigureAwait(false);
        await content.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        await PadAsync(size, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Two empty blocks: the end of the archive.</summary>
    public async Task EndAsync(CancellationToken cancellationToken) =>
        await destination.WriteAsync(new byte[2 * BlockSize], cancellationToken).ConfigureAwait(false);

    private async Task PadAsync(long size, CancellationToken cancellationToken)
    {
        var padding = (int)((BlockSize - size % BlockSize) % BlockSize);

        if (padding > 0)
        {
            await destination.WriteAsync(new byte[padding], cancellationToken).ConfigureAwait(false);
        }
    }

    private static byte[] Header(string name, long size, long modified, char type)
    {
        var header = new byte[BlockSize];

        Put(header, 0, 100, name);
        Put(header, 100, 8, "0000644");
        Put(header, 108, 8, "0000000");
        Put(header, 116, 8, "0000000");
        Put(header, 124, 12, Convert.ToString(size, 8).PadLeft(11, '0'));
        Put(header, 136, 12, Convert.ToString(Math.Max(0, modified), 8).PadLeft(11, '0'));
        Array.Fill(header, (byte)' ', 148, 8);
        header[156] = (byte)type;
        Put(header, 257, 6, "ustar");
        Put(header, 263, 2, "00");

        var checksum = header.Sum(value => value);
        Put(header, 148, 7, $"{Convert.ToString(checksum, 8).PadLeft(6, '0')}\0");

        return header;
    }

    private static void Put(byte[] header, int offset, int length, string value) =>
        Encoding.ASCII.GetBytes(value.Length > length ? value[..length] : value).CopyTo(header, offset);

    /// <summary>One PAX record, whose leading length counts its own digits.</summary>
    private static string Record(string key, string value)
    {
        var content = $" {key}={value}\n";
        var bytes = Encoding.UTF8.GetByteCount(content);
        var length = bytes + bytes.ToString(CultureInfo.InvariantCulture).Length;

        if (length.ToString(CultureInfo.InvariantCulture).Length != bytes.ToString(CultureInfo.InvariantCulture).Length)
        {
            length++;
        }

        return $"{length}{content}";
    }
}
