using System.Security.Cryptography;
using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Application.Transfer;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>What working with a Blender on another machine rests on, against a real one: files in and out through the link, the machine named in ping, and the GPU chosen from here and carried to the job worker.</summary>
[Collection("Loopback")]
public sealed class RemoteLiveTests : IAsyncLifetime
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
    public async Task Files_UploadedInChunks_ComeBackIdentical_ThroughTheListing()
    {
        var bytes = RandomNumberGenerator.GetBytes(FileTransfer.UploadChunkBytes + FileTransfer.DownloadChunkBytes + 123);
        var local = Path.Combine(_workDirectory, "plate.exr");
        await File.WriteAllBytesAsync(local, bytes);
        var transfer = new FileTransfer();

        var up = await transfer.UploadAsync(local, "plates/plate.exr", false, chunk => _link.SendAsync(BridgeCommands.FilePut, chunk), CancellationToken.None);
        var down = await transfer.DownloadAsync("plates", Path.Combine(_workDirectory, "back"), false,
            listing => _link.SendAsync(BridgeCommands.FileList, listing), chunk => _link.SendAsync(BridgeCommands.FileGet, chunk), CancellationToken.None);

        Assert.Null(up.Error);
        Assert.EndsWith(Path.Combine("files", "plates", "plate.exr"), up.Files.Single().To, StringComparison.Ordinal);
        Assert.Null(down.Error);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(_workDirectory, "back", "plate.exr")));
        Assert.Equal(up.Files.Single().Sha256, down.Files.Single().Sha256);
    }

    [BlenderFact]
    public async Task Files_ARelativePathThatClimbsOut_IsRefused_AndAnExistingFileIsKept()
    {
        var climbing = await _link.SendAsync(BridgeCommands.FilePut, new JsonObject { ["path"] = "../escape.txt", ["offset"] = 0, ["data"] = string.Empty, ["done"] = true });
        var first = await _link.SendAsync(BridgeCommands.FilePut, new JsonObject { ["path"] = "keep.txt", ["offset"] = 0, ["data"] = Convert.ToBase64String("one"u8.ToArray()), ["done"] = true });
        var second = await _link.SendAsync(BridgeCommands.FilePut, new JsonObject { ["path"] = "keep.txt", ["offset"] = 0, ["data"] = Convert.ToBase64String("two"u8.ToArray()), ["done"] = true });

        Assert.Equal("BadRequest", climbing.Error!.Type);
        Assert.True(first.IsOk, $"{first.Error?.Type} {first.Error?.Message}");
        Assert.Equal("Exists", second.Error!.Type);
    }

    [BlenderFact]
    public async Task Ping_NamesTheMachineBlenderRunsOn()
    {
        var ping = await _link.SendAsync(BridgeCommands.Ping);
        var machine = ping.Result!["machine"]!;

        Assert.False(string.IsNullOrWhiteSpace(machine["platform"]!.ToString()));
        Assert.EndsWith("files", machine["files"]!.ToString(), StringComparison.Ordinal);
        Assert.True(machine["background"]!.GetValue<bool>());
    }

    [BlenderFact]
    public async Task Cycles_BackendNone_UsesTheCpu_AndAnUnknownBackendNamesWhatThisMachineHas()
    {
        var none = await _link.SendAsync(BridgeCommands.SetCycles, new JsonObject { ["backend"] = "NONE" });
        var unknown = await _link.SendAsync(BridgeCommands.SetCycles, new JsonObject { ["backend"] = "NOPE" });

        Assert.True(none.IsOk, $"{none.Error?.Type} {none.Error?.Message}");
        Assert.Equal("NONE", none.Result!["compute"]!["backend"]!.ToString());
        Assert.Equal("BadRequest", unknown.Error!.Type);
        Assert.Contains("NONE", unknown.Error.Details!["known"]!.AsArray().Select(value => value!.ToString()));
    }

    /// <summary>A worker reads only its own saved preferences, so the job carries the backend of the Blender that queued it; with no directory named, the job lives in the add-on's own data directory.</summary>
    [BlenderFact]
    public async Task RenderJob_WithoutADirectory_LivesWithTheAddOn_AndCarriesTheGpuChoice()
    {
        await _link.SendAsync(BridgeCommands.SetCycles, new JsonObject { ["backend"] = "NONE", ["samples"] = 1 });

        var started = await _link.SendAsync(BridgeCommands.RenderJob,
            new JsonObject { ["output_path"] = Path.Combine(_workDirectory, "job", "f_####"), ["frames"] = "1", ["resolution_x"] = 32, ["resolution_y"] = 32 }, TimeSpan.FromSeconds(120));

        Assert.True(started.IsOk, $"{started.Error?.Type} {started.Error?.Message}");

        var id = started.Result!["id"]!.ToString();
        var spec = Path.Combine(_blender.DataDirectory, "jobs", id, "spec.json");

        Assert.True(File.Exists(spec), spec);
        Assert.Equal("NONE", JsonNode.Parse(await File.ReadAllTextAsync(spec))!["gpu"]!["backend"]!.ToString());

        await _link.SendAsync(BridgeCommands.RenderJobCancel, new JsonObject { ["id"] = id });
    }
}
