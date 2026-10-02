using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Transfer;

namespace Snail.MCP.Blender.Tests.Web;

/// <summary>The volume's files over HTTP against an add-on that keeps them in memory: reads, archives, uploads, deletions and who may do each.</summary>
public sealed class RawFilesTests : IAsyncLifetime
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-09-16T12:00:00Z"));
    private readonly InMemoryVolume _volume = new()
    {
        Files =
        {
            ["files/robot/renders/a.png"] = RandomNumberGenerator.GetBytes(3 * FileTransfer.DownloadChunkBytes + 17),
            ["files/robot/renders/b.png"] = "second frame"u8.ToArray(),
            ["files/robot/robot.blend"] = "blend"u8.ToArray(),
        },
        OpenFile = "files/robot/robot.blend",
    };

    private WebApplication _app = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _app = await InProcessHttpServer.StartAsync(_volume.AddOn.Port, _time);
        _anonymous = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
        _client = new HttpClient { BaseAddress = _anonymous.BaseAddress, DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Bearer", InProcessHttpServer.Token) } };
    }

    public async Task DisposeAsync()
    {
        _anonymous.Dispose();
        _client.Dispose();
        await _app.DisposeAsync();
        await _volume.DisposeAsync();
    }

    [Fact]
    public async Task Read_AFileWithTheToken_ComesBackWhole_AndWithoutAnyAccess_IsUnauthorized()
    {
        var file = await _client.GetAsync("/raw/files/robot/renders/a.png");
        var refused = await _anonymous.GetAsync("/raw/files/robot/renders/a.png");

        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal(_volume.Files["files/robot/renders/a.png"], await file.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task Read_AFolder_ComesAsATarOfItsFiles_OrAsAZipOnAsking()
    {
        var tar = await _client.GetStreamAsync("/raw/files/robot/renders");
        var zip = await _client.GetByteArrayAsync("/raw/files/robot?format=zip");

        var unpacked = new Dictionary<string, byte[]>();
        await using var reader = new TarReader(tar);

        while (await reader.GetNextEntryAsync(copyData: true) is { } entry)
        {
            using var content = new MemoryStream();
            await entry.DataStream!.CopyToAsync(content);
            unpacked[entry.Name] = content.ToArray();
        }

        using var archive = new ZipArchive(new MemoryStream(zip));

        Assert.Equal(["a.png", "b.png"], unpacked.Keys.Order());
        Assert.Equal(_volume.Files["files/robot/renders/a.png"], unpacked["a.png"]);
        Assert.Equal(["renders/a.png", "renders/b.png", "robot.blend"], archive.Entries.Select(entry => entry.FullName).Order());
    }

    /// <summary>A link lands in the agent's transcript, so it opens exactly one path for one method and stops working on its own.</summary>
    [Fact]
    public async Task Link_OpensOnlyItsOwnPathAndMethod_UntilItExpires()
    {
        var links = _app.Services.GetRequiredService<FileLinks>();
        var link = new Uri(links.For("GET", "files", "robot/renders/b.png").Url);
        var query = link.Query;

        var opened = await _anonymous.GetAsync(link.PathAndQuery);
        var otherPath = await _anonymous.GetAsync($"/raw/files/robot/renders/a.png{query}");
        var otherMethod = await _anonymous.DeleteAsync(link.PathAndQuery);
        _time.Advance(TimeSpan.FromMinutes(16));
        var expired = await _anonymous.GetAsync(link.PathAndQuery);

        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal("second frame", await opened.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, otherPath.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherMethod.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [Fact]
    public async Task Write_AFileLargerThanAChunk_ArrivesWhole_AndATarUnpacksIntoTheFolder_WithoutMacResourceForks()
    {
        var texture = RandomNumberGenerator.GetBytes(FileTransfer.UploadChunkBytes + 1234);
        var put = await _client.PutAsync("/raw/files/robot/textures/wood.png", new ByteArrayContent(texture));

        using var tar = new MemoryStream();
        await using (var writer = new TarWriter(tar, TarEntryFormat.Pax, leaveOpen: true))
        {
            await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.Directory, "./"));
            await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, "./stone.png") { DataStream = new MemoryStream("stone"u8.ToArray()) });
            await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, "./._stone.png") { DataStream = new MemoryStream("fork"u8.ToArray()) });
            await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, "./moss/moss.png") { DataStream = new MemoryStream("moss"u8.ToArray()) });
        }

        tar.Position = 0;
        var unpacked = await _client.PutAsync("/raw/files/robot/textures?format=tar", new StreamContent(tar));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(texture, _volume.Files["files/robot/textures/wood.png"]);
        Assert.Equal(HttpStatusCode.OK, unpacked.StatusCode);
        Assert.Equal("stone"u8.ToArray(), _volume.Files["files/robot/textures/stone.png"]);
        Assert.Equal("moss"u8.ToArray(), _volume.Files["files/robot/textures/moss/moss.png"]);
        Assert.DoesNotContain("files/robot/textures/._stone.png", _volume.Files.Keys);
    }

    /// <summary>A proxy that reads a body for a minute at most cannot carry a large upload in one request, so it comes in parts; the last part
    /// checks the whole file against the digest the client computed.</summary>
    [Fact]
    public async Task Write_InParts_ArrivesWhole_AndADigestThatDoesNotMatch_IsRefused()
    {
        var model = RandomNumberGenerator.GetBytes(FileTransfer.UploadChunkBytes + 5000);
        var digest = Convert.ToHexStringLower(SHA256.HashData(model));
        var cut = FileTransfer.UploadChunkBytes / 2;

        var first = await _client.PutAsync($"/raw/files/robot/model.fbx?offset=0&last=false&sha256={digest}", new ByteArrayContent(model[..cut]));
        var second = await _client.PutAsync($"/raw/files/robot/model.fbx?offset={cut}&last=true&sha256={digest}", new ByteArrayContent(model[cut..]));
        await _client.PutAsync("/raw/files/robot/broken.fbx?offset=0&last=false", new ByteArrayContent(model[..cut]));
        var broken = await _client.PutAsync($"/raw/files/robot/broken.fbx?offset={cut}&last=true&sha256={new string('0', 64)}", new ByteArrayContent(model[cut..]));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(model, _volume.Files["files/robot/model.fbx"]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, broken.StatusCode);
        Assert.DoesNotContain("files/robot/broken.fbx", _volume.Files.Keys);
    }

    /// <summary>A file small enough for one part was checked against the digest of whatever body arrived, not the one the client computed from
    /// the file, so a short read on the client's side went through as the file.</summary>
    [Fact]
    public async Task Write_InOnePart_WhoseBodyDiffersFromTheClientsDigest_IsRefused()
    {
        var model = RandomNumberGenerator.GetBytes(5000);
        var digest = Convert.ToHexStringLower(SHA256.HashData(model));

        var shortRead = await _client.PutAsync($"/raw/files/robot/short.fbx?offset=0&last=true&sha256={digest}", new ByteArrayContent(model[..4000]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, shortRead.StatusCode);
        Assert.DoesNotContain("files/robot/short.fbx", _volume.Files.Keys);
    }

    [Fact]
    public async Task Write_ATarInParts_UnpacksOnItsLastPart_AndLeavesNoStagingFile()
    {
        using var tar = new MemoryStream();
        await using (var writer = new TarWriter(tar, TarEntryFormat.Pax, leaveOpen: true))
        {
            await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, "./brick.png") { DataStream = new MemoryStream(RandomNumberGenerator.GetBytes(40_000)) });
            await writer.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile, "./tile.png") { DataStream = new MemoryStream("tile"u8.ToArray()) });
        }

        var bytes = tar.ToArray();
        var cut = bytes.Length / 2;

        var first = await _client.PutAsync("/raw/files/robot/textures?format=tar&offset=0&last=false", new ByteArrayContent(bytes[..cut]));
        var staged = _volume.Files.Keys.Where(key => key.StartsWith("files/.snail-upload-", StringComparison.Ordinal)).ToList();
        var last = await _client.PutAsync($"/raw/files/robot/textures?format=tar&offset={cut}&last=true&sha256={Convert.ToHexStringLower(SHA256.HashData(bytes))}", new ByteArrayContent(bytes[cut..]));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Empty(staged);
        Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        Assert.Equal("tile"u8.ToArray(), _volume.Files["files/robot/textures/tile.png"]);
        Assert.Contains("files/robot/textures/brick.png", _volume.Files.Keys);
        Assert.DoesNotContain(_volume.Files.Keys, key => key.Contains(".snail-upload-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Paths_OutsideTheArea_AreRefusedBeforeBlender_AndOnlyTheFilesAreaTakesWrites()
    {
        var drive = await _client.GetAsync("/raw/files/C:/Windows/win.ini");
        var unknownArea = await _client.GetAsync("/raw/state/token");
        var intoJobs = await _client.PutAsync("/raw/jobs/job-1/spec.json", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.BadRequest, drive.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownArea.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, intoJobs.StatusCode);
        Assert.Empty(_volume.AddOn.Received);
    }

    /// <summary>The add-on trims a path before resolving it, so a segment of dots and a space became "..": the whole jobs area went through a
    /// name this check let pass because it looked only for exactly "..".</summary>
    [Fact]
    public async Task Paths_WithADotSegmentPaddedWithSpaces_AreRefusedBeforeBlender()
    {
        var climbing = await _client.DeleteAsync("/raw/jobs/job-1/..%20");
        var here = await _client.DeleteAsync("/raw/files/robot/%20.");

        Assert.Equal(HttpStatusCode.BadRequest, climbing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, here.StatusCode);
        Assert.Empty(_volume.AddOn.Received);
    }

    [Fact]
    public async Task Delete_WhatBlenderStillUses_IsAConflict_AndAnythingElseIsGone()
    {
        var busy = await _client.DeleteAsync("/raw/files/robot/robot.blend");
        var gone = await _client.DeleteAsync("/raw/files/robot/renders/b.png");

        Assert.Equal(HttpStatusCode.Conflict, busy.StatusCode);
        Assert.Equal(HttpStatusCode.OK, gone.StatusCode);
        Assert.DoesNotContain("files/robot/renders/b.png", _volume.Files.Keys);
    }
}
