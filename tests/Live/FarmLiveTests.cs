using System.Diagnostics;
using Snail.MCP.Blender.Adapters.Blender;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>The farm skill against a real Blender: background jobs finished, cancelled, queued and split into chunks, the pre-flight check and the render budget.</summary>
public sealed class FarmLiveTests : IAsyncLifetime
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
    public async Task RenderCheck_ReportsTheUnsavedFile_AndIsReady()
    {
        var check = await Send(BridgeCommands.RenderCheck, new JsonObject { ["output_path"] = Path.Combine(_workDirectory, "check_####") });

        Assert.True(check["ready"]!.GetValue<bool>(), check.ToJsonString());
        Assert.Contains("unsaved_file", check["issues"]!.AsArray().Select(issue => issue!["code"]!.ToString()));
        Assert.Equal(250, check["frames"]!["count"]!.GetValue<int>());
    }

    [BlenderFact]
    public async Task RenderCheck_LossyExrOnDataPasses_IsAWarning()
    {
        await Send(BridgeCommands.SetOutput, new JsonObject { ["file_format"] = "OPEN_EXR", ["color_depth"] = 32, ["exr_codec"] = "DWAA" });
        await Send(BridgeCommands.ViewLayer, new JsonObject { ["passes"] = new JsonObject { ["z"] = true } });

        var check = await Send(BridgeCommands.RenderCheck, new JsonObject { ["output_path"] = Path.Combine(_workDirectory, "lossy_####") });

        Assert.Contains("lossy_exr", check["issues"]!.AsArray().Select(issue => issue!["code"]!.ToString()));
        Assert.EndsWith("lossy_0001.exr", check["output"]!["first_file"]!.ToString());
    }

    [BlenderFact]
    public async Task RenderJob_EverySecondFrameInABackgroundBlender_FinishesWithFiles()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["samples"] = 1 });
        var jobs = Path.Combine(_workDirectory, "jobs");

        var started = await Send(BridgeCommands.RenderJob, new JsonObject { ["output_path"] = Path.Combine(_workDirectory, "out", "{scene}_{camera}_####"), ["frames"] = "1-5x2", ["directory"] = jobs, ["file_format"] = "PNG", ["resolution_x"] = 32, ["resolution_y"] = 32 }, TimeSpan.FromSeconds(120));
        var id = started["id"]!.ToString();
        Assert.Equal(3, started["frames_total"]!.GetValue<int>());

        JsonNode status = started;
        for (var attempt = 0; attempt < 90 && status["state"]!.ToString() is "queued" or "running"; attempt++)
        {
            await Task.Delay(1000);
            status = await Send(BridgeCommands.RenderJobStatus, new JsonObject { ["id"] = id, ["directory"] = jobs });
        }

        Assert.Equal("finished", status["state"]!.ToString());
        Assert.Equal(3, status["frames_done"]!.GetValue<int>());
        Assert.Equal(1.0, status["progress"]!.GetValue<double>());
        Assert.True(File.Exists(Path.Combine(_workDirectory, "out", "Scene_Camera_0005.png")));

        var listed = await Send(BridgeCommands.RenderJobStatus, new JsonObject { ["directory"] = jobs });
        Assert.Contains(listed["jobs"]!.AsArray(), job => job!["id"]!.ToString() == id);
    }

    /// <summary>Cancel leaves a marker the worker reads between frames and signals it to break: the job stops of its own accord, writes its own last status, and does not flap back to running.</summary>
    [BlenderFact]
    public async Task RenderJob_Cancelled_StopsAndKeepsWrittenFrames()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["samples"] = 1 });
        var jobs = Path.Combine(_workDirectory, "jobs");

        var started = await Send(BridgeCommands.RenderJob, new JsonObject { ["output_path"] = Path.Combine(_workDirectory, "long", "f_####"), ["frames"] = "1-400", ["directory"] = jobs, ["resolution_x"] = 64, ["resolution_y"] = 64 }, TimeSpan.FromSeconds(120));
        await Task.Delay(3000);

        var cancelled = await Send(BridgeCommands.RenderJobCancel, new JsonObject { ["id"] = started["id"]!.ToString(), ["directory"] = jobs });

        Assert.Equal("cancelled", cancelled["state"]!.ToString());
        Assert.True(cancelled["frames_done"]!.GetValue<int>() < 400);

        var directory = Path.Combine(jobs, started["id"]!.ToString());
        Assert.True(File.Exists(Path.Combine(directory, "cancel")), "the worker is asked to stop by a marker it reads between frames");

        await Task.Delay(2000);

        var settled = await Send(BridgeCommands.RenderJobStatus, new JsonObject { ["id"] = started["id"]!.ToString(), ["directory"] = jobs });

        Assert.Equal("cancelled", settled["state"]!.ToString());
    }

    [BlenderFact]
    public async Task RenderJobs_QueueOneAtATime_AndChunksFillTheRange()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["samples"] = 1 });
        var jobs = Path.Combine(_workDirectory, "jobs");

        var first = await Send(BridgeCommands.RenderJob, new JsonObject { ["output_path"] = Path.Combine(_workDirectory, "q", "a_####"), ["frames"] = "1-4", ["directory"] = jobs, ["resolution_x"] = 32, ["resolution_y"] = 32, ["max_parallel"] = 1 }, TimeSpan.FromSeconds(120));
        var second = await Send(BridgeCommands.RenderJob, new JsonObject { ["output_path"] = Path.Combine(_workDirectory, "q", "b_####"), ["frames"] = "1-4", ["directory"] = jobs, ["resolution_x"] = 32, ["resolution_y"] = 32, ["max_parallel"] = 1 }, TimeSpan.FromSeconds(120));
        var chunked = await Send(BridgeCommands.RenderJob, new JsonObject { ["output_path"] = Path.Combine(_workDirectory, "q", "c_####"), ["frames"] = "1-6", ["directory"] = jobs, ["resolution_x"] = 32, ["resolution_y"] = 32, ["max_parallel"] = 2, ["chunks"] = 2 }, TimeSpan.FromSeconds(120));

        Assert.Equal("running", first["state"]!.ToString());
        Assert.Equal("queued", second["state"]!.ToString());
        Assert.Equal(2, chunked["chunks"]!.AsArray().Count);

        JsonNode listed = new JsonObject();
        for (var attempt = 0; attempt < 120; attempt++)
        {
            await Task.Delay(1000);
            listed = await Send(BridgeCommands.RenderJobStatus, new JsonObject { ["directory"] = jobs });

            if (listed["jobs"]!.AsArray().All(job => job!["state"]!.ToString() is "finished" or "failed"))
            {
                break;
            }
        }

        Assert.All(listed["jobs"]!.AsArray(), job => Assert.Equal("finished", job!["state"]!.ToString()));

        var whole = await Send(BridgeCommands.RenderJobStatus, new JsonObject { ["id"] = chunked["id"]!.ToString(), ["directory"] = jobs });
        Assert.Equal(6, whole["frames_done"]!.GetValue<int>());
        Assert.Equal(6, Directory.EnumerateFiles(Path.Combine(_workDirectory, "q"), "c_*.png").Count());
    }

    [BlenderFact]
    public async Task RenderJob_FrameListInChunks_SplitsTheListRoundRobin()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["samples"] = 1, ["resolution_x"] = 32, ["resolution_y"] = 32 });
        var jobs = Path.Combine(_workDirectory, "jobs-list");

        var started = await Send(BridgeCommands.RenderJob, new JsonObject { ["output_path"] = Path.Combine(_workDirectory, "list", "f_####"), ["frames"] = "1,3,5,7", ["directory"] = jobs, ["max_parallel"] = 2, ["chunks"] = 2 }, TimeSpan.FromSeconds(120));
        Assert.Equal(2, started["chunks"]!.AsArray().Count);
        Assert.All(started["chunks"]!.AsArray(), chunk => Assert.Equal(2, chunk!["frames_total"]!.GetValue<int>()));

        JsonNode status = started;
        for (var attempt = 0; attempt < 90 && status["state"]!.ToString() is "queued" or "running"; attempt++)
        {
            await Task.Delay(1000);
            status = await Send(BridgeCommands.RenderJobStatus, new JsonObject { ["id"] = started["id"]!.ToString(), ["directory"] = jobs });
        }

        Assert.Equal("finished", status["state"]!.ToString());
        Assert.Equal(["f_0001.png", "f_0003.png", "f_0005.png", "f_0007.png"], Directory.EnumerateFiles(Path.Combine(_workDirectory, "list")).Select(Path.GetFileName).Order());
    }

    [BlenderFact]
    public async Task RenderBudget_ProbeApplyAndPack_ReportAVerdict()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 64, ["resolution_y"] = 64, ["samples"] = 1 });

        var budget = await Send(BridgeCommands.RenderBudget, new JsonObject
        {
            ["probe"] = new JsonObject { ["percentage"] = 25 },
            ["apply"] = new JsonObject { ["texture_limit"] = "2048", ["simplify"] = new JsonObject { ["subdivision"] = 2 } },
            ["pack_for_farm"] = new JsonObject { ["pack_external"] = true, ["copy_to"] = Path.Combine(_workDirectory, "farm", "scene.blend") },
        }, TimeSpan.FromSeconds(120));

        Assert.Equal("fits", budget["verdict"]!.ToString());
        Assert.Equal(6, budget["geometry"]!["polygons_render"]!.GetValue<int>());
        Assert.Equal("2048", budget["applied"]!["texture_limit"]!.ToString());
        Assert.True(budget["probe"]!["seconds_per_frame_estimate"]!.GetValue<double>() >= 0);
        Assert.True(File.Exists(budget["packed"]!["copy"]!.ToString()));
    }

    /// <summary>A frame costs a fixed part and a part that grows with the pixels: the probe renders twice and reads both out of the pair,
    /// rather than treating one small render as if all of it scaled with area — which under-reports every frame.</summary>
    [BlenderFact]
    public async Task RenderBudget_Probe_SeparatesTheFixedCostFromTheCostPerPixel()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 320, ["resolution_y"] = 240, ["samples"] = 1 });

        var budget = await Send(BridgeCommands.RenderBudget, new JsonObject { ["probe"] = new JsonObject { ["percentage"] = 20 } }, TimeSpan.FromSeconds(180));
        var probe = budget["probe"]!;
        var runs = probe["runs"]!.AsArray();

        Assert.Equal("two renders", probe["method"]!.ToString());
        Assert.Equal([10, 20], runs.Select(run => run!["percentage"]!.GetValue<int>()));
        Assert.True(probe["fixed_seconds"]!.GetValue<double>() >= 0);
        Assert.True(
            probe["seconds_per_frame_estimate"]!.GetValue<double>() >= probe["fixed_seconds"]!.GetValue<double>(),
            "a whole frame cannot cost less than the part of it that does not scale");
    }

    /// <summary>The peak a probe reports already holds the textures and the geometry; adding them to it counted them twice and called scenes
    /// too large for a machine they fit on. Where the render reports no memory at all — some builds and engines do not — the parts are the
    /// whole answer and nothing is invented on top of them.</summary>
    [BlenderFact]
    public async Task RenderBudget_Estimate_DoesNotCountWhatTheProbeAlreadyMeasured()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 64, ["resolution_y"] = 64, ["samples"] = 1 });
        await Send(BridgeCommands.Python, new JsonObject
        {
            ["code"] = "import bpy\nimage = bpy.data.images.new('Heavy', 2048, 2048, float_buffer=True)\nimage.use_fake_user = True\nresult = image.size[0]",
        });

        var budget = await Send(BridgeCommands.RenderBudget, new JsonObject { ["probe"] = new JsonObject { ["percentage"] = 20 } }, TimeSpan.FromSeconds(180));
        var textures = budget["textures"]!["mb"]!.GetValue<double>();
        var geometry = budget["geometry"]!["mb"]!.GetValue<double>();
        var peak = budget["probe"]!["peak_memory_mb"]!.GetValue<double>();
        var estimate = budget["estimate_mb"]!.GetValue<double>();

        Assert.True(textures > 32, $"the scene was meant to hold a heavy texture, and holds {textures} MB");

        if (peak > 0)
        {
            Assert.Equal("the probe, plus the frame buffer it did not fill", budget["estimate_from"]!.ToString());
            Assert.True(estimate < textures + geometry + peak, $"the estimate {estimate} counts the {textures} MB of textures the probe's {peak} MB peak already held");
            Assert.True(estimate >= peak, $"the estimate {estimate} is below what the probe measured, {peak}");
        }
        else
        {
            Assert.Equal("textures and geometry", budget["estimate_from"]!.ToString());
            Assert.Equal(Math.Round(textures + geometry, 1), estimate);
        }
    }

    /// <summary>A job that has ended keeps its status and its log and loses the copy of the scene it rendered: that copy is the whole file,
    /// and a job is retried only while it is still queued or running. Its frames are named in files.txt, one line each, while the status
    /// keeps the last few — a status that carried every path was rewritten whole after every frame.</summary>
    [BlenderFact]
    public async Task Job_ThatHasEnded_KeepsItsStatusAndLog_AndLosesTheCopyOfTheScene()
    {
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["samples"] = 1, ["resolution_x"] = 32, ["resolution_y"] = 32 });
        var jobs = Path.Combine(_workDirectory, "kept");
        var started = await Send(BridgeCommands.RenderJob, new JsonObject
        {
            ["output_path"] = Path.Combine(_workDirectory, "kept-renders", "f_####"),
            ["frames"] = "1-3",
            ["directory"] = jobs,
        }, TimeSpan.FromSeconds(120));
        var id = started["id"]!.ToString();

        var finished = await SettledAsync(jobs, id);
        var directory = Path.Combine(jobs, id);

        Assert.Equal("finished", finished["state"]!.ToString());
        Assert.Equal(3, finished["files_written"]!.GetValue<int>());
        Assert.False(File.Exists(Path.Combine(directory, "scene.blend")), "the copy of the scene stayed after the job ended");
        Assert.True(File.Exists(Path.Combine(directory, "status.json")));
        Assert.True(File.Exists(Path.Combine(directory, "log.txt")));
        Assert.Equal(3, File.ReadAllLines(Path.Combine(directory, "files.txt")).Length);
        Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
    }

    /// <summary>Waits for a job to stop running, then asks once more: what a job leaves behind is tidied on the pass that notices it has
    /// ended, and the reply that first says "finished" is the one that read the worker's last word just after that pass.</summary>
    private async Task<JsonNode> SettledAsync(string jobs, string id)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);

        while (true)
        {
            var status = await Send(BridgeCommands.RenderJobStatus, new JsonObject { ["directory"] = jobs, ["id"] = id });

            if (status["state"]!.ToString() is not ("queued" or "running") || DateTimeOffset.UtcNow > deadline)
            {
                return await Send(BridgeCommands.RenderJobStatus, new JsonObject { ["directory"] = jobs, ["id"] = id });
            }

            await Task.Delay(1000);
        }
    }

    /// <summary>The probe renders into a file of its own, then puts the scene's output path back; it used to delete whatever file that restored path
    /// named, a finished render of the user's, and leave its own probe behind.</summary>
    [BlenderFact]
    public async Task Budget_WithAProbe_KeepsTheScenesOutputFile_AndRemovesItsOwn()
    {
        var output = Path.Combine(_workDirectory, "hero.png");
        await File.WriteAllBytesAsync(output, [1, 2, 3]);
        await Send(BridgeCommands.RenderSettings, new JsonObject { ["engine"] = "EEVEE", ["resolution_x"] = 32, ["resolution_y"] = 32, ["samples"] = 1, ["output_path"] = output });
        var before = DateTime.UtcNow.AddSeconds(-1);

        await Send(BridgeCommands.RenderBudget, new JsonObject { ["probe"] = new JsonObject { ["percentage"] = 25 } }, TimeSpan.FromSeconds(120));

        Assert.True(File.Exists(output), "the probe deleted the scene's own output file");
        Assert.DoesNotContain(Directory.EnumerateFiles(Path.GetTempPath(), "snail-probe-*"), file => File.GetLastWriteTimeUtc(file) >= before);
    }

    /// <summary>After a restart a job is known only by the pid in its status; here its worker is gone, and reading that status used to raise
    /// UnboundLocalError from a local variable named like the module it meant to call.</summary>
    [BlenderFact]
    public async Task Status_OfARunningJobWhoseWorkerIsGone_ReadsInterrupted()
    {
        var jobs = Path.Combine(_workDirectory, "planted");
        Plant(jobs, "job-20260101-000000-001", new JsonObject { ["state"] = "running", ["pid"] = GonePid() });

        var status = await Send(BridgeCommands.RenderJobStatus, new JsonObject { ["directory"] = jobs, ["id"] = "job-20260101-000000-001" });

        Assert.Equal("interrupted", status["state"]!.ToString());
    }

    /// <summary>A pid outlives its worker: after a restart in a fresh PID namespace, the number an old job recorded names some other program, and a
    /// finished job keeps its pid. Counting either as running stalled a queue of max_parallel 1 for good.</summary>
    [BlenderFact]
    public async Task Status_OfJobsWhosePidsNowNameAnotherProgram_ShowsNoneRunning()
    {
        using var stranger = Stranger();
        var jobs = Path.Combine(_workDirectory, "planted");
        Plant(jobs, "job-20260101-000000-002", new JsonObject { ["state"] = "finished", ["pid"] = stranger.Id });
        Plant(jobs, "job-20260101-000000-003", new JsonObject { ["state"] = "running", ["pid"] = stranger.Id });

        var status = await Send(BridgeCommands.RenderJobStatus, new JsonObject { ["directory"] = jobs });

        Assert.Empty(status["running"]!.AsArray());
        Assert.Equal("interrupted", status["jobs"]!.AsArray().Single(job => job!["id"]!.ToString() == "job-20260101-000000-003")!["state"]!.ToString());
    }

    /// <summary>Cancelling a job that has ended used to signal whatever process now holds its old pid and rewrite finished as cancelled.</summary>
    [BlenderFact]
    public async Task Cancel_OfAFinishedJob_LeavesItAndTheProgramItsPidNowNames()
    {
        using var stranger = Stranger();
        var jobs = Path.Combine(_workDirectory, "planted");
        Plant(jobs, "job-20260101-000000-004", new JsonObject { ["state"] = "finished", ["pid"] = stranger.Id });

        var cancelled = await Send(BridgeCommands.RenderJobCancel, new JsonObject { ["directory"] = jobs, ["id"] = "job-20260101-000000-004" }, TimeSpan.FromSeconds(30));

        Assert.Equal("finished", cancelled["state"]!.ToString());
        Assert.False(stranger.HasExited, "the cancel signalled a process that was never its worker");
    }

    /// <summary>Each chunk's cancel used to end by advancing the queue, which started the next queued chunk only for the loop to cancel it.</summary>
    [BlenderFact]
    public async Task Cancel_OfAChunkedJob_StartsNoneOfItsQueuedChunks()
    {
        var jobs = Path.Combine(_workDirectory, "planted");
        string[] children = ["job-20260101-000000-005-c1", "job-20260101-000000-005-c2", "job-20260101-000000-005-c3"];
        Directory.CreateDirectory(Path.Combine(jobs, "job-20260101-000000-005"));
        await File.WriteAllTextAsync(Path.Combine(jobs, "job-20260101-000000-005", "chunks.json"), new JsonObject { ["children"] = new JsonArray([.. children.Select(child => JsonValue.Create(child))]) }.ToJsonString());

        foreach (var child in children)
        {
            Plant(jobs, child, new JsonObject { ["state"] = "queued" }, attempts: 0);
        }

        await Send(BridgeCommands.RenderJobCancel, new JsonObject { ["directory"] = jobs, ["id"] = "job-20260101-000000-005" }, TimeSpan.FromSeconds(60));

        Assert.All(children, child => Assert.Equal(0, JsonNode.Parse(File.ReadAllText(Path.Combine(jobs, child, "spec.json")))!["attempts"]!.GetValue<int>()));
    }

    /// <summary>With the interface open the queue advances on a timer. A new closure per call was never "registered", so every tick and every status
    /// poll added one more timer and their number doubled every three seconds while a job waited.</summary>
    [BlenderFact]
    public async Task Schedule_OfOneQueueTwice_RegistersOneTimer()
    {
        const string code = """
            import sys
            jobs = next(module for name, module in list(sys.modules.items()) if name.endswith(".jobs") and hasattr(module, "_schedule"))
            registered = []
            real = jobs.bpy

            class Timers:
                @staticmethod
                def is_registered(function):
                    return any(function is known for known in registered)

                @staticmethod
                def register(function, first_interval=0.0, persistent=False):
                    registered.append(function)

            class App:
                timers = Timers
                background = False

                def __getattr__(self, name):
                    return getattr(real.app, name)

            class Bpy:
                app = App()

                def __getattr__(self, name):
                    return getattr(real, name)

            jobs.bpy = Bpy()
            try:
                jobs._schedule("/snail-test/queue")
                jobs._schedule("/snail-test/queue")
            finally:
                jobs.bpy = real
            result = len(registered)
            """;

        var scheduled = await Send(BridgeCommands.Python, new JsonObject { ["code"] = code });

        Assert.Equal(1, scheduled["result"]!.GetValue<int>());
    }

    private static void Plant(string root, string id, JsonObject status, int attempts = 1)
    {
        var directory = Path.Combine(root, id);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "status.json"), status.ToJsonString());
        File.WriteAllText(Path.Combine(directory, "spec.json"), new JsonObject
        {
            ["blend"] = Path.Combine(directory, "missing.blend"),
            ["max_parallel"] = 1,
            ["retries"] = 0,
            ["attempts"] = attempts,
        }.ToJsonString());
    }

    private static int GonePid()
    {
        using var ended = Process.Start("/usr/bin/true")!;
        ended.WaitForExit();

        return ended.Id;
    }

    private static Process Stranger() => Process.Start("/bin/sleep", "60")!;

    private async Task<JsonNode> Send(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await _link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
