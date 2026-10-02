using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Application.Storage;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>Uploads large enough to leave the size a test usually bothers with, against a real add-on.</summary>
[Collection("Loopback")]
public sealed class UploadSizeLiveTests : IAsyncLifetime
{
    private const int PartBytes = 16 * 1024 * 1024;

    private HeadlessBlender _blender = null!;
    private BlenderLinks _links = null!;
    private Volume _volume = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _links = new BlenderLinks(_blender.Link, NullLogger<BlenderConnection>.Instance);
        _volume = new Volume(new BlenderVolume(_links));
        _app = await InProcessHttpServer.StartAsync(_blender.Port, bridgeTokenFile: _blender.Link.TokenFile);
        _client = new HttpClient
        {
            BaseAddress = new Uri(_app.Urls.First()),
            Timeout = TimeSpan.FromMinutes(10),
            DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Bearer", InProcessHttpServer.Token) },
        };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        if (_app is not null) await _app.DisposeAsync();

        if (_links is not null) await _links.DisposeAsync();

        _blender?.Dispose();
    }

    /// <summary>The same PUTs the command from blender_upload makes, through the real HTTP endpoint rather than around it.</summary>
    [BlenderFact]
    public async Task File_OfFortyMegabytes_ArrivesThroughTheEndpoint()
    {
        var bytes = Noise(40 * 1024 * 1024);
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var answers = await PutPartsAsync("raw/files/http/archive.tar", bytes, digest, format: null);

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.OK, answer.Status));
    }

    [BlenderFact]
    public async Task Folder_OfFiftyNineMegabytes_ArrivesThroughTheEndpoint()
    {
        var tar = Tarred(59 * 1024 * 1024, files: 12);
        var digest = Convert.ToHexStringLower(SHA256.HashData(tar));

        var answers = await PutPartsAsync("raw/files/http/folder", tar, digest, format: "tar");

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.OK, answer.Status));
    }

    private async Task<List<(HttpStatusCode Status, string Body)>> PutPartsAsync(string path, byte[] bytes, string digest, string? format)
    {
        var answers = new List<(HttpStatusCode Status, string Body)>();

        for (var offset = 0; offset < bytes.Length; offset += PartBytes)
        {
            var length = Math.Min(PartBytes, bytes.Length - offset);
            var last = offset + length >= bytes.Length;
            var packed = format is null ? string.Empty : $"&format={format}";
            var query = $"{path}?overwrite=true&offset={offset}&last={last.ToString().ToLowerInvariant()}&sha256={digest}{packed}";
            using var content = new ByteArrayContent(bytes, offset, length);
            using var answer = await _client.PutAsync(query, content, CancellationToken.None);

            answers.Add((answer.StatusCode, await answer.Content.ReadAsStringAsync(CancellationToken.None)));

            if (answer.StatusCode != HttpStatusCode.OK)
            {
                break;
            }
        }

        return answers;
    }

    [BlenderFact]
    public async Task File_OfFortyMegabytes_ArrivesInParts()
    {
        var bytes = Noise(40 * 1024 * 1024);
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var reply = await SendPartsAsync("big/archive.tar", bytes, digest, format: null);

        Assert.True(reply.IsOk, $"{reply.Error?.Type}: {reply.Error?.Message}");
    }

    [BlenderFact]
    public async Task Folder_OfFiftyNineMegabytes_ArrivesAndUnpacks()
    {
        var tar = Tarred(59 * 1024 * 1024, files: 12);
        var digest = Convert.ToHexStringLower(SHA256.HashData(tar));
        var staged = Volume.StagingPath("big/folder", "tester");

        var gathered = await SendPartsAsync(staged, tar, digest, format: "tar");
        Assert.True(gathered.IsOk, $"gathering: {gathered.Error?.Type}: {gathered.Error?.Message}");

        var unpacked = await _volume.UnpackStagedTarAsync(VolumeAreas.Files, "big/folder", staged, overwrite: true, CancellationToken.None);

        Assert.True(unpacked.Error is null, $"unpacking: {unpacked.Error?.Type}: {unpacked.Error?.Message}");
        Assert.Equal(12, unpacked.Files.Count);
    }

    /// <summary>The parts the upload command sends: sixteen megabytes each, the whole file's digest on every one.</summary>
    private async Task<BridgeReply> SendPartsAsync(string path, byte[] bytes, string digest, string? format)
    {
        var reply = BridgeReply.Ok(new JsonObject());

        for (var offset = 0; offset < bytes.Length; offset += PartBytes)
        {
            var length = Math.Min(PartBytes, bytes.Length - offset);
            var last = offset + length >= bytes.Length;
            await using var body = new MemoryStream(bytes, offset, length, writable: false);

            reply = await _volume.WritePartAsync(
                VolumeAreas.Files, path, body, new UploadPart(offset, last, digest), overwrite: true, CancellationToken.None);

            if (!reply.IsOk)
            {
                return reply;
            }
        }

        return reply;
    }

    private static byte[] Noise(int bytes)
    {
        var made = new byte[bytes];
        new Random(7).NextBytes(made);
        return made;
    }

    private static byte[] Tarred(int bytes, int files)
    {
        using var memory = new MemoryStream();

        using (var writer = new System.Formats.Tar.TarWriter(memory, leaveOpen: true))
        {
            for (var index = 0; index < files; index++)
            {
                var entry = new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, $"frame_{index:D3}.bin");
                entry.DataStream = new MemoryStream(Noise(bytes / files));
                writer.WriteEntry(entry);
            }
        }

        return memory.ToArray();
    }
}
