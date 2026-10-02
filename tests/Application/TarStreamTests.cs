using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using Snail.MCP.Blender.Application.Storage;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>The tar written as it streams is read back by an independent reader: headers, padding, the end blocks and PAX names alike.</summary>
public class TarStreamTests
{
    /// <summary>A render folder named in Cyrillic, or nested deeper than ustar's 100 bytes, still unpacks under its own name.</summary>
    [Fact]
    public async Task Archive_WithShortLongAndNonAsciiNames_ReadsBackWithEveryNameAndByte()
    {
        var files = new Dictionary<string, byte[]>
        {
            ["renders/f_0001.png"] = RandomNumberGenerator.GetBytes(1300),
            ["рендеры/кадр_0001.png"] = RandomNumberGenerator.GetBytes(512),
            [$"{new string('d', 60)}/{new string('e', 60)}/frame.exr"] = [],
        };
        using var archive = new MemoryStream();
        var tar = new TarStream(archive);

        foreach (var (name, bytes) in files)
        {
            await tar.WriteFileAsync(name, bytes.Length, 1_789_000_000, new MemoryStream(bytes), CancellationToken.None);
        }

        await tar.EndAsync(CancellationToken.None);
        archive.Position = 0;

        var read = new Dictionary<string, byte[]>();
        await using var reader = new TarReader(archive);

        while (await reader.GetNextEntryAsync(copyData: true) is { } entry)
        {
            using var content = new MemoryStream();

            if (entry.DataStream is not null)
            {
                await entry.DataStream.CopyToAsync(content);
            }

            read[entry.Name] = content.ToArray();
        }

        Assert.Equal(0, archive.Length % 512);
        Assert.Equal(files.Keys.Order(), read.Keys.Order());
        Assert.All(files, file => Assert.Equal(file.Value, read[file.Key]));
    }

    /// <summary>ustar keeps a size in eleven octal digits, 8 GiB less a byte at most; a larger render needs a PAX size record, or a reader takes
    /// the truncated number and reads the rest of the archive as the file.</summary>
    [Theory]
    [InlineData(8_589_934_591, false)]
    [InlineData(8_589_934_592, true)]
    public async Task SizeRecord_AtAndAboveTheUstarLimit_IsWrittenOnlyAboveIt(long size, bool expectsRecord)
    {
        using var archive = new MemoryStream();
        var tar = new TarStream(archive);

        await tar.WriteFileAsync("frames/beauty.exr", size, 1_789_000_000, Stream.Null, CancellationToken.None);

        var written = archive.ToArray();

        Assert.Equal(expectsRecord ? (byte)'x' : (byte)'0', written[156]);
        Assert.Equal(expectsRecord, Encoding.ASCII.GetString(written).Contains($" size={size}\n", StringComparison.Ordinal));
    }
}
