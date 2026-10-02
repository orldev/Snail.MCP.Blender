using System.Diagnostics;
using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tests.Contracts;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>What a Blender in a container rests on, against a real one: the command line it starts with, the project folder it works in and the volume kept clean from a page.</summary>
[Collection("Loopback")]
public sealed class StorageLiveTests : IAsyncLifetime
{
    private HeadlessBlender _blender = null!;
    private BlenderConnection _link = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _link = new BlenderConnection(_blender.Link, NullLogger<BlenderConnection>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_link is not null) await _link.DisposeAsync();

        _blender?.Dispose();
    }

    /// <summary>A container starts Blender with its address, the token file both containers mount and the GPU backend; a request without that token is turned away.</summary>
    [BlenderFact]
    public async Task CommandLine_WithHostTokenFileAndBackend_ServesOnlyThatToken()
    {
        var tokenFile = Path.Combine(Path.GetTempPath(), $"snail-token-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(tokenFile, "container-secret\n");

        using var blender = await HeadlessBlender.StartAsync(commandLine: ["--host", "127.0.0.1", "--token-file", tokenFile, "--backend", "NONE"]);
        await using var trusted = new BlenderConnection(new BlenderLinkOptions { Port = blender.Port, Token = "container-secret", RequestTimeoutSeconds = 60 }, NullLogger<BlenderConnection>.Instance);
        await using var stranger = new BlenderConnection(new BlenderLinkOptions { Port = blender.Port, Token = "guess", RequestTimeoutSeconds = 60 }, NullLogger<BlenderConnection>.Instance);

        var cycles = await trusted.SendAsync(BridgeCommands.SetCycles, new JsonObject { ["activate"] = false });
        var refused = await stranger.SendAsync(BridgeCommands.Ping);

        File.Delete(tokenFile);
        Assert.True(cycles.IsOk, $"{cycles.Error?.Type} {cycles.Error?.Message}");
        Assert.Equal("NONE", cycles.Result!["compute"]!["backend"]!.ToString());
        Assert.Equal("Unauthorized", refused.Error!.Type);
    }

    [BlenderFact]
    public async Task OpenProject_ANewName_CreatesTheFolder_AndTheSecondTimeOpensIt()
    {
        var created = await _link.SendAsync(BridgeCommands.OpenProject, new JsonObject { ["name"] = "robot" });
        var saved = await _link.SendAsync(BridgeCommands.SaveFile);
        var opened = await _link.SendAsync(BridgeCommands.OpenProject, new JsonObject { ["name"] = "robot" });

        var directory = Path.Combine(_blender.DataDirectory, "files", "robot");

        Assert.True(created.IsOk, $"{created.Error?.Type} {created.Error?.Message}");
        Assert.True(created.Result!["created"]!.GetValue<bool>());
        Assert.True(File.Exists(Path.Combine(directory, "robot.blend")));
        Assert.True(Directory.Exists(Path.Combine(directory, "renders")));
        Assert.True(Directory.Exists(Path.Combine(directory, "textures")));
        Assert.True(saved.IsOk, $"{saved.Error?.Type} {saved.Error?.Message}");
        Assert.False(opened.Result!["created"]!.GetValue<bool>());
    }

    [BlenderFact]
    public async Task OpenProject_WithUnsavedChanges_IsRefused_UntilTheyAreDiscarded()
    {
        await _link.SendAsync(BridgeCommands.OpenProject, new JsonObject { ["name"] = "first" });
        await _link.SendAsync(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Unsaved" });

        var refused = await _link.SendAsync(BridgeCommands.OpenProject, new JsonObject { ["name"] = "second" });
        var discarded = await _link.SendAsync(BridgeCommands.OpenProject, new JsonObject { ["name"] = "second", ["discard"] = true });

        Assert.Equal("Unsaved", refused.Error!.Type);
        Assert.True(discarded.IsOk, $"{discarded.Error?.Type} {discarded.Error?.Message}");
        Assert.Equal(0, discarded.Result!["objects"]!.GetValue<int>());
    }

    [BlenderFact]
    public async Task OpenProject_FromCurrent_KeepsTheScene_AndANameWithASlashIsRefused()
    {
        await _link.SendAsync(BridgeCommands.NewFile, new JsonObject { ["empty"] = true });
        await _link.SendAsync(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Kept" });

        var kept = await _link.SendAsync(BridgeCommands.OpenProject, new JsonObject { ["name"] = "kept", ["from"] = "current" });
        var slashed = await _link.SendAsync(BridgeCommands.OpenProject, new JsonObject { ["name"] = "a/b" });

        Assert.True(kept.IsOk, $"{kept.Error?.Type} {kept.Error?.Message}");
        Assert.Equal(1, kept.Result!["objects"]!.GetValue<int>());
        Assert.Equal("BadRequest", slashed.Error!.Type);
    }

    [BlenderFact]
    public async Task Storage_MeasuresTheAreas_ListsAFolder_AndDeletesAFile()
    {
        await _link.SendAsync(BridgeCommands.FilePut, new JsonObject { ["area"] = "files", ["path"] = "shots/renders/a.png", ["offset"] = 0, ["data"] = Convert.ToBase64String(new byte[1000]), ["done"] = true });

        var measured = await _link.SendAsync(BridgeCommands.Storage);
        var listed = await _link.SendAsync(BridgeCommands.StorageList, new JsonObject { ["area"] = "files", ["path"] = "shots" });
        var deleted = await _link.SendAsync(BridgeCommands.StorageDelete, new JsonObject { ["area"] = "files", ["path"] = "shots/renders/a.png" });

        var files = measured.Result!["areas"]!.AsArray().Single(area => area!["area"]!.ToString() == "files")!;
        var renders = listed.Result!["entries"]!.AsArray().Single()!;

        Assert.True(files["bytes"]!.GetValue<long>() >= 1000);
        Assert.True(measured.Result["disk"]!["free_bytes"]!.GetValue<long>() > 0);
        Assert.Equal("renders", renders["name"]!.ToString());
        Assert.Equal("directory", renders["kind"]!.ToString());
        Assert.Equal(1000, renders["bytes"]!.GetValue<long>());
        Assert.Equal(1, deleted.Result!["deleted_files"]!.GetValue<int>());
    }

    /// <summary>A page cannot see what Blender is doing, so the add-on refuses to delete the open file, and a path from a page never leaves its area.</summary>
    [BlenderFact]
    public async Task StorageDelete_TheOpenProject_IsInUse_AndAPathOutOfTheArea_IsRefused()
    {
        await _link.SendAsync(BridgeCommands.OpenProject, new JsonObject { ["name"] = "busy" });

        var busy = await _link.SendAsync(BridgeCommands.StorageDelete, new JsonObject { ["area"] = "files", ["path"] = "busy" });
        var climbing = await _link.SendAsync(BridgeCommands.StorageDelete, new JsonObject { ["area"] = "files", ["path"] = "../state" });
        var absolute = await _link.SendAsync(BridgeCommands.FileGet, new JsonObject { ["area"] = "files", ["path"] = Path.Combine(_blender.DataDirectory, "state", "token") });

        Assert.Equal("InUse", busy.Error!.Type);
        Assert.Equal("BadRequest", climbing.Error!.Type);
        Assert.Equal("BadRequest", absolute.Error!.Type);
    }

    [BlenderFact]
    public async Task Snapshots_AreListedByName_AndDeletedWithTheirNote()
    {
        await _link.SendAsync(BridgeCommands.Snapshot, new JsonObject { ["action"] = "save", ["name"] = "before", ["note"] = "clean scene" });

        var listed = await _link.SendAsync(BridgeCommands.StorageList, new JsonObject { ["area"] = "snapshots" });
        var deleted = await _link.SendAsync(BridgeCommands.StorageDelete, new JsonObject { ["area"] = "snapshots", ["path"] = "before" });

        var snapshot = listed.Result!["entries"]!.AsArray().Single()!;

        Assert.Equal("before", snapshot["name"]!.ToString());
        Assert.Equal("clean scene", snapshot["snapshot"]!["note"]!.ToString());
        Assert.Equal(2, deleted.Result!["deleted_files"]!.GetValue<int>());
        Assert.False(File.Exists(Path.Combine(_blender.DataDirectory, "snapshots", "before.blend")));
    }

    [BlenderFact]
    public async Task Jobs_ARunningJob_IsInUse_AndOnceCancelled_IsDeleted()
    {
        await _link.SendAsync(BridgeCommands.NewFile);
        await _link.SendAsync(BridgeCommands.SetCycles, new JsonObject { ["backend"] = "NONE", ["samples"] = 4096 });
        var output = Path.Combine(_blender.DataDirectory, "files", "job", "f_####");
        var started = await _link.SendAsync(BridgeCommands.RenderJob, new JsonObject { ["output_path"] = output, ["frames"] = "1-50", ["resolution_x"] = 256, ["resolution_y"] = 256 }, TimeSpan.FromSeconds(120));
        var id = started.Result!["id"]!.ToString();

        var listed = await _link.SendAsync(BridgeCommands.StorageList, new JsonObject { ["area"] = "jobs" });
        var busy = await _link.SendAsync(BridgeCommands.StorageDelete, new JsonObject { ["area"] = "jobs", ["path"] = id });
        await _link.SendAsync(BridgeCommands.RenderJobCancel, new JsonObject { ["id"] = id });
        var deleted = await DeleteOnceSettledAsync(id);

        Assert.Contains(listed.Result!["entries"]!.AsArray(), entry => entry!["name"]!.ToString() == id && entry["job"]!["state"] is not null);
        Assert.Equal("InUse", busy.Error!.Type);
        Assert.True(deleted.IsOk, $"{deleted.Error?.Type} {deleted.Error?.Message}");
        Assert.False(Directory.Exists(Path.Combine(_blender.DataDirectory, "jobs", id)));
    }

    /// <summary>The add-on trims a path and then resolves it, so ".. " after a folder named the area root, and the running-job check read only the
    /// first segment of what it was given: "x/.. " deleted the whole jobs area with every job in it.</summary>
    [BlenderFact]
    public async Task StorageDelete_ADotSegmentPaddedWithSpaces_IsRefused_AndTheAreaStays()
    {
        var jobs = Path.Combine(_blender.DataDirectory, "jobs");
        Directory.CreateDirectory(Path.Combine(jobs, "job-20260101-000000-001"));

        var climbing = await _link.SendAsync(BridgeCommands.StorageDelete, new JsonObject { ["area"] = "jobs", ["path"] = "x/.. " });
        var aside = await _link.SendAsync(BridgeCommands.StorageDelete, new JsonObject { ["area"] = "jobs", ["path"] = "x/../job-20260101-000000-001" });

        Assert.Equal("BadRequest", climbing.Error?.Type);
        Assert.Equal("BadRequest", aside.Error?.Type);
        Assert.True(Directory.Exists(Path.Combine(jobs, "job-20260101-000000-001")));
    }

    /// <summary>A job another Blender session started reads "running (started by another Blender session)", which the in-use check did not count
    /// as running, so its folder was deleted under the worker still writing into it. The stand-in worker carries the spec path on its command
    /// line the way a real one does; "; true" keeps the shell from replacing itself with sleep and dropping it.</summary>
    [BlenderFact]
    public async Task StorageDelete_AJobAnotherSessionRuns_IsInUse()
    {
        var directory = Path.Combine(_blender.DataDirectory, "jobs", "job-20260101-000000-002");
        var spec = Path.Combine(directory, "spec.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(spec, new JsonObject { ["blend"] = "missing.blend", ["attempts"] = 1, ["retries"] = 0 }.ToJsonString());
        using var worker = Process.Start("/bin/sh", ["-c", "sleep 60; true", spec])!;
        await File.WriteAllTextAsync(Path.Combine(directory, "status.json"), new JsonObject { ["state"] = "running", ["pid"] = worker.Id }.ToJsonString());

        var busy = await _link.SendAsync(BridgeCommands.StorageDelete, new JsonObject { ["area"] = "jobs", ["path"] = "job-20260101-000000-002" });
        worker.Kill();

        Assert.Equal("InUse", busy.Error?.Type);
        Assert.True(Directory.Exists(directory));
    }

    /// <summary>The setting that keeps Python out belongs where the power is. A server may be configured to refuse scripts, but anything with
    /// the token can speak to the add-on directly, so the add-on started with --python off refuses them itself.</summary>
    [BlenderFact]
    public async Task PythonOff_IsTheAddOnsOwnRule_NotOnlyTheServers()
    {
        var tokenFile = Path.Combine(Path.GetTempPath(), $"snail-token-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(tokenFile, "closed-secret\n");

        using var closed = await HeadlessBlender.StartAsync(commandLine: ["--host", "127.0.0.1", "--token-file", tokenFile, "--backend", "NONE", "--python", "off"]);
        await using var link = new BlenderConnection(new BlenderLinkOptions { Port = closed.Port, Token = "closed-secret", RequestTimeoutSeconds = 60 }, NullLogger<BlenderConnection>.Instance);

        var script = await link.SendAsync(BridgeCommands.Python, new JsonObject { ["code"] = "result = 1" });
        var installer = await link.SendAsync(BridgeCommands.RunOperator, new JsonObject { ["name"] = "preferences.addon_install", ["params"] = new JsonObject { ["filepath"] = "/tmp/x.zip" } });
        var outside = Path.Combine(Path.GetTempPath(), $"snail-should-not-exist-{Guid.NewGuid():N}.py");
        var elsewhere = await link.SendAsync(BridgeCommands.FilePut, new JsonObject { ["path"] = outside, ["offset"] = 0, ["data"] = Convert.ToBase64String("x"u8.ToArray()), ["done"] = true });
        var allowed = await link.SendAsync(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Crate" });

        File.Delete(tokenFile);
        Assert.Equal("PythonDisabled", script.Error?.Type);
        Assert.Equal("PythonDisabled", installer.Error?.Type);
        Assert.Equal("PythonDisabled", elsewhere.Error?.Type);
        Assert.False(File.Exists(outside));
        Assert.True(allowed.IsOk, $"{allowed.Error?.Type} {allowed.Error?.Message}");
    }

    /// <summary>A batch runs its steps in a Blender of its own, and that Blender has to be told what this one was told: the setting used to stop
    /// at the process that was asked, so one batch step ran arbitrary code on a Blender started with python off.</summary>
    [BlenderFact]
    public async Task PythonOff_ReachesTheBatchWorker_NotOnlyTheBlenderThatWasAsked()
    {
        var tokenFile = Path.Combine(Path.GetTempPath(), $"snail-token-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(tokenFile, "closed-secret\n");

        using var closed = await HeadlessBlender.StartAsync(commandLine: ["--host", "127.0.0.1", "--token-file", tokenFile, "--backend", "NONE", "--python", "off"]);
        await using var link = new BlenderConnection(new BlenderLinkOptions { Port = closed.Port, Token = "closed-secret", RequestTimeoutSeconds = 120 }, NullLogger<BlenderConnection>.Instance);
        var written = Path.Combine(Path.GetTempPath(), $"snail-should-not-exist-{Guid.NewGuid():N}.txt");
        var steps = new JsonArray(new JsonObject
        {
            ["command"] = "python",
            ["params"] = new JsonObject { ["code"] = $"open({Quoted(written)}, 'w').write('here')" },
        });

        var started = await link.SendAsync(BridgeCommands.BatchJob, new JsonObject { ["commands"] = steps, ["continue_on_error"] = true }, TimeSpan.FromSeconds(120));
        var id = started.Result?["id"]?.GetValue<string>();
        var settled = id is null ? started : await BatchSettledAsync(link, id);

        File.Delete(tokenFile);
        Assert.False(File.Exists(written), "a batch step ran Python on a Blender started with python off");
        Assert.True(started.IsOk || started.Error?.Type == "PythonDisabled", $"{started.Error?.Type} {started.Error?.Message}");

        if (settled.IsOk && settled.Result?["steps"] is JsonArray ran && ran.Count > 0)
        {
            Assert.Contains("PythonDisabled", ran[0]!["error"]?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }
    }

    private static string Quoted(string path) => $"'{path.Replace("\\", "\\\\", StringComparison.Ordinal)}'";

    /// <summary>Waits for a batch to stop running, so the test reads what it did rather than what it had started to do.</summary>
    private static async Task<BridgeReply> BatchSettledAsync(BlenderConnection link, string id)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        var status = await link.SendAsync(BridgeCommands.BatchStatus, new JsonObject { ["id"] = id });

        while (DateTimeOffset.UtcNow < deadline && status.Result?["state"]?.ToString() is "queued" or "running")
        {
            await Task.Delay(500);
            status = await link.SendAsync(BridgeCommands.BatchStatus, new JsonObject { ["id"] = id });
        }

        return status;
    }

    /// <summary>Which parameters of a command name a path is written down once, in the command schema, so the rule reaches every command that
    /// writes a file — an export, a render output, an image loaded from disk — and not only the transfers.</summary>
    [BlenderFact]
    public async Task PythonOff_ConfinesEveryPathTheSchemaNames_NotOnlyTransfers()
    {
        var tokenFile = Path.Combine(Path.GetTempPath(), $"snail-token-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(tokenFile, "closed-secret\n");

        using var closed = await HeadlessBlender.StartAsync(commandLine: ["--host", "127.0.0.1", "--token-file", tokenFile, "--backend", "NONE", "--python", "off"]);
        await using var link = new BlenderConnection(new BlenderLinkOptions { Port = closed.Port, Token = "closed-secret", RequestTimeoutSeconds = 60 }, NullLogger<BlenderConnection>.Instance);
        var outside = Path.Combine(Path.GetTempPath(), $"snail-should-not-exist-{Guid.NewGuid():N}.obj");

        await link.SendAsync(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Crate" });
        var away = await link.SendAsync(BridgeCommands.ExportFile, new JsonObject { ["path"] = outside, ["format"] = "obj" });
        var inside = await link.SendAsync(BridgeCommands.ExportFile, new JsonObject { ["path"] = Path.Combine(closed.DataDirectory, "files", "crate.obj"), ["format"] = "obj" });

        File.Delete(tokenFile);
        Assert.Equal("PythonDisabled", away.Error?.Type);
        Assert.False(File.Exists(outside));
        Assert.True(inside.IsOk, $"{inside.Error?.Type} {inside.Error?.Message}");
    }

    /// <summary>The add-on's own reading of a path, against the cases the server is checked with: one file, both sides, so the two cannot drift
    /// apart again. They ran in Blender through transfer.inside, which is what every area path passes through.</summary>
    [BlenderFact]
    public async Task AreaPaths_AsTheAddOnReadsThem_MatchTheSharedCases()
    {
        var expected = AreaPathContractTests.Vectors;
        var paths = new JsonArray([.. expected.Select(vector => JsonValue.Create(vector.Path))]);
        var code = $$"""
            import json, sys
            transfer = next(module for name, module in list(sys.modules.items()) if name.endswith(".transfer") and hasattr(module, "inside"))
            failure = next(module for name, module in list(sys.modules.items()) if name.endswith(".server") and hasattr(module, "CommandError")).CommandError
            read = []
            for path in json.loads({{JsonValue.Create(paths.ToJsonString())!.ToJsonString()}}):
                try:
                    transfer.inside("files", path)
                    read.append(True)
                except failure:
                    read.append(False)
            result = read
            """;

        var answered = await _link.SendAsync(BridgeCommands.Python, new JsonObject { ["code"] = code });

        Assert.True(answered.IsOk, $"{answered.Error?.Type} {answered.Error?.Message}");
        var read = answered.Result!["result"]!.AsArray().Select(value => value!.GetValue<bool>()).ToList();
        var differing = expected.Where((vector, index) => read[index] != vector.IsConfined).Select(vector => $"'{vector.Path}' ({vector.Why})").ToList();

        Assert.True(differing.Count == 0, $"the add-on reads these differently from the server: {string.Join("; ", differing)}");
    }

    /// <summary>A folder is read a page at a time: the listing says how many files there are in all and whether this page is the last, so a
    /// folder of a hundred thousand frames is reachable rather than cut off at the add-on's limit.</summary>
    [BlenderFact]
    public async Task FileList_AskedForAPage_GivesThatPage_AndSaysWhatIsLeft()
    {
        foreach (var name in (string[])["a.png", "b.png", "c.png", "d.png", "e.png"])
        {
            await _link.SendAsync(BridgeCommands.FilePut, new JsonObject
            {
                ["area"] = VolumeAreas.Files,
                ["path"] = $"pages/{name}",
                ["offset"] = 0,
                ["data"] = Convert.ToBase64String(new byte[8]),
                ["done"] = true,
            });
        }

        var first = await Listed(offset: 0, limit: 2);
        var second = await Listed(offset: 2, limit: 2);
        var last = await Listed(offset: 4, limit: 2);

        Assert.Equal(5, first["total"]!.GetValue<int>());
        Assert.Equal(["a.png", "b.png"], Names(first));
        Assert.True(first["truncated"]!.GetValue<bool>());
        Assert.Equal(["c.png", "d.png"], Names(second));
        Assert.Equal(["e.png"], Names(last));
        Assert.False(last["truncated"]!.GetValue<bool>());
    }

    private static IEnumerable<string> Names(JsonObject listing) =>
        listing["files"]!.AsArray().Select(entry => entry!["path"]!.ToString());

    private async Task<JsonObject> Listed(int offset, int limit)
    {
        var listing = await _link.SendAsync(BridgeCommands.FileList, new JsonObject
        {
            ["area"] = VolumeAreas.Files,
            ["path"] = "pages",
            ["offset"] = offset,
            ["limit"] = limit,
        });

        Assert.True(listing.IsOk, listing.Error?.Message);

        return (JsonObject)listing.Result!;
    }

    private async Task<BridgeReply> DeleteOnceSettledAsync(string id)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (true)
        {
            var reply = await _link.SendAsync(BridgeCommands.StorageDelete, new JsonObject { ["area"] = "jobs", ["path"] = id });

            if (reply.Error?.Type != "InUse" || DateTimeOffset.UtcNow > deadline)
            {
                return reply;
            }

            await Task.Delay(500);
        }
    }
}
