using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>Two agents in one Blender: a lease keeps the other out, the journal names both, and a batch runs in a separate Blender and lands in a file.</summary>
[Collection("Loopback")]
public sealed class TeamLiveTests : IAsyncLifetime
{
    private HeadlessBlender _blender = null!;
    private BlenderConnection _lighting = null!;
    private BlenderConnection _modeling = null!;
    private string _workDirectory = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _lighting = new BlenderConnection(Link("lighting"), NullLogger<BlenderConnection>.Instance);
        _modeling = new BlenderConnection(Link("modeling"), NullLogger<BlenderConnection>.Instance);
        _workDirectory = Path.Combine(Path.GetTempPath(), "snail-blender-live", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
    }

    private BlenderLinkOptions Link(string agent) => new() { Port = _blender.Port, ConnectTimeoutSeconds = 5, RequestTimeoutSeconds = 120, Agent = agent, TokenFile = _blender.Link.TokenFile };

    public async Task DisposeAsync()
    {
        if (_lighting is not null) await _lighting.DisposeAsync();
        if (_modeling is not null) await _modeling.DisposeAsync();

        _blender?.Dispose();

        if (_workDirectory is not null && Directory.Exists(_workDirectory)) Directory.Delete(_workDirectory, recursive: true);
    }

    [BlenderFact]
    public async Task Lease_HeldByOneAgent_RefusesTheOtherUntilReleased()
    {
        var claimed = await Send(_lighting, BridgeCommands.Lease, new JsonObject { ["action"] = "claim", ["names"] = new JsonArray("Cube"), ["note"] = "relighting" });
        Assert.Equal(["Cube"], claimed["claimed"]!.AsArray().Select(node => node!.ToString()));

        var refused = await _modeling.SendAsync(BridgeCommands.TransformObject, new JsonObject { ["name"] = "Cube", ["location"] = new JsonArray(1, 0, 0) });
        Assert.False(refused.IsOk);
        Assert.Equal("Leased", refused.Error!.Type);
        Assert.Equal("lighting", refused.Error.Details!["agent"]!.ToString());

        var seen = await Send(_modeling, BridgeCommands.ObjectInfo, new JsonObject { ["name"] = "Cube" });
        Assert.Equal("Cube", seen["name"]!.ToString());

        var stolen = await _modeling.SendAsync(BridgeCommands.Lease, new JsonObject { ["action"] = "release", ["names"] = new JsonArray("Cube") });
        Assert.Equal("Leased", stolen.Error!.Type);

        await Send(_lighting, BridgeCommands.Lease, new JsonObject { ["action"] = "release", ["names"] = new JsonArray("Cube") });
        var moved = await Send(_modeling, BridgeCommands.TransformObject, new JsonObject { ["name"] = "Cube", ["location"] = new JsonArray(1, 0, 0) });
        Assert.Equal(1, moved["location"]![0]!.GetValue<double>());

        var journal = await Send(_lighting, BridgeCommands.Journal);
        var entries = journal["entries"]!.AsArray();
        Assert.True(entries.Any(entry => entry!["agent"]!.ToString() == "modeling" && entry["command"]!.ToString() == "transform_object" && entry["ok"]!.GetValue<bool>()), journal.ToJsonString());
        Assert.True(entries.Any(entry => entry!["agent"]!.ToString() == "modeling" && !entry["ok"]!.GetValue<bool>()), journal.ToJsonString());
        Assert.Equal(["lighting", "modeling"], journal["agents"]!.AsArray().Select(node => node!.ToString()));
    }

    /// <summary>The guard read the targets of a command from a handful of parameter names; a command that names its object as "object" or acts on
    /// the selection went past a lease nobody could see it break.</summary>
    [BlenderFact]
    public async Task Lease_OnAnObject_AlsoStopsCommandsThatNameItOtherwise()
    {
        await Send(_modeling, BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Hero" });
        await Send(_modeling, BridgeCommands.CreateMaterial, new JsonObject { ["name"] = "Paint" });
        await Send(_modeling, BridgeCommands.SelectObjects, new JsonObject { ["names"] = new JsonArray("Hero") });
        await Send(_lighting, BridgeCommands.Lease, new JsonObject { ["action"] = "claim", ["names"] = new JsonArray("Hero") });

        var assigned = await _modeling.SendAsync(BridgeCommands.AssignMaterial, new JsonObject { ["object"] = "Hero", ["material"] = "Paint" });
        var deleted = await _modeling.SendAsync(BridgeCommands.DeleteObjects, new JsonObject { ["selected"] = true });

        Assert.Equal("Leased", assigned.Error?.Type);
        Assert.Equal("Leased", deleted.Error?.Type);
    }

    /// <summary>A name inside a block, a name in a key of its own and a name that is the key of a block are all the object they name.</summary>
    /// <remarks>The guard used to look for a handful of likely parameter names — name, target, objects and their kin — so an object named
    /// anywhere else was invisible to it: set_camera_optics pointed focus at a leased object and moved on, add_curve took a leased one as its
    /// bevel profile, camera_move pivoted around one. Nothing refused and nothing was written down, which is worse than a refusal: the holder
    /// had no way to learn their object had been used. The paths are now read out of the handlers themselves, so the guard looks where the
    /// command really looks, and the journal records it under the same reading.</remarks>
    [BlenderFact]
    public async Task Lease_OnAnObject_StopsACommandThatNamesItInsideABlock()
    {
        await Send(_modeling, BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Focus" });
        await Send(_modeling, BridgeCommands.AddCamera, new JsonObject { ["name"] = "Shot" });
        await Send(_lighting, BridgeCommands.Lease, new JsonObject { ["action"] = "claim", ["names"] = new JsonArray("Focus") });

        var focused = await _modeling.SendAsync(BridgeCommands.SetCameraOptics,
            new JsonObject { ["camera"] = "Shot", ["focus"] = new JsonObject { ["object"] = "Focus" } });
        var blurred = await _modeling.SendAsync(BridgeCommands.SetCameraOptics,
            new JsonObject { ["camera"] = "Shot", ["object_motion_blur"] = new JsonObject { ["Focus"] = new JsonObject { ["enabled"] = true } } });
        var pivoted = await _modeling.SendAsync(BridgeCommands.CameraMove,
            new JsonObject { ["camera"] = "Shot", ["type"] = "orbit", ["pivot_name"] = "Focus", ["start"] = 1, ["end"] = 8 });
        var elsewhere = await _modeling.SendAsync(BridgeCommands.SetCameraOptics, new JsonObject { ["camera"] = "Shot", ["lens"] = 50 });

        Assert.Equal("Leased", focused.Error?.Type);
        Assert.Equal("Leased", blurred.Error?.Type);
        Assert.Equal("Leased", pivoted.Error?.Type);
        Assert.True(elsewhere.IsOk, $"{elsewhere.Error?.Type} {elsewhere.Error?.Message}");

        var journal = await Send(_modeling, BridgeCommands.Journal, new JsonObject { ["limit"] = 20 });
        var refusals = journal["entries"]!.AsArray().Where(entry => entry!["error"] is not null).ToList();

        Assert.Contains(refusals, entry => entry!["targets"]!.AsArray().Any(target => target!.ToString() == "Focus"));
    }

    /// <summary>A lease is on an object, not on a name: a material, a node or a strip called the same is nobody's to refuse.</summary>
    /// <remarks>The guard used to read the parameters that usually carry an object — and 'name' is also the parameter that carries a
    /// material, a compositor node, a sequencer strip, an operator and the name of a thing about to be created. Only the handler knows
    /// which of those it looks up, so a lease on an object called Steel refused a material called Steel, and a lease on Crate refused
    /// making a second object by that name, which Blender would have called Crate.001 and left the held one alone. The journal is not
    /// narrowed with the guard: it still records every name a command carried, because it answers what an agent did rather than
    /// refusing anybody. The two commands checked at the end are the ones whose objects are named in ways only the handler shows —
    /// object_render_flags hands a whole list to a helper that resolves each item, export_file resolves nothing at all and picks
    /// objects by testing their names — so they are what a narrowing of the guard would lose first.</remarks>
    [BlenderFact]
    public async Task Lease_OnAnObject_LeavesAMaterialOfTheSameNameAlone()
    {
        await Send(_modeling, BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Steel" });
        await Send(_modeling, BridgeCommands.CreateMaterial, new JsonObject { ["name"] = "Steel" });
        await Send(_lighting, BridgeCommands.Lease, new JsonObject { ["action"] = "claim", ["names"] = new JsonArray("Steel") });

        var material = await _modeling.SendAsync(BridgeCommands.SetMaterial, new JsonObject { ["name"] = "Steel", ["roughness"] = 0.4 });
        var namesake = await _modeling.SendAsync(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Steel" });
        var theObject = await _modeling.SendAsync(BridgeCommands.TransformObject, new JsonObject { ["name"] = "Steel", ["location"] = new JsonArray(1, 0, 0) });

        Assert.True(material.IsOk, $"a material is not the object of the same name: {material.Error?.Type} {material.Error?.Message}");
        Assert.True(namesake.IsOk, $"a new object gets a name of its own: {namesake.Error?.Type} {namesake.Error?.Message}");
        Assert.Equal("Leased", theObject.Error?.Type);

        var flagged = await _modeling.SendAsync(BridgeCommands.ObjectRenderFlags, new JsonObject { ["name"] = "Steel", ["holdout"] = true });
        var exported = await _modeling.SendAsync(BridgeCommands.ExportFile,
            new JsonObject { ["path"] = Path.Combine(_workDirectory, "held.obj"), ["selected_only"] = true, ["names"] = new JsonArray("Steel") });

        Assert.Equal("Leased", flagged.Error?.Type);
        Assert.Equal("Leased", exported.Error?.Type);

        var journal = await Send(_modeling, BridgeCommands.Journal, new JsonObject { ["limit"] = 20 });
        var wrote = journal["entries"]!.AsArray().First(entry => entry!["command"]!.ToString() == "set_material")!;

        Assert.Contains("Steel", wrote["targets"]!.AsArray().Select(target => target!.ToString()));
    }

    /// <summary>Releasing another agent's lease needs force, but clear dropped every agent's leases for the asking.</summary>
    [BlenderFact]
    public async Task Lease_ClearedWithoutForce_LeavesAnotherAgentsLeasesAlone()
    {
        await Send(_lighting, BridgeCommands.Lease, new JsonObject { ["action"] = "claim", ["names"] = new JsonArray("Cube") });

        var cleared = await _modeling.SendAsync(BridgeCommands.Lease, new JsonObject { ["action"] = "clear" });
        var forced = await Send(_modeling, BridgeCommands.Lease, new JsonObject { ["action"] = "clear", ["force"] = true });

        Assert.Equal("Leased", cleared.Error?.Type);
        Assert.Empty(forced["leases"]!.AsArray());
    }

    /// <summary>The journal's filter by agent travelled under the same key the link signs its requests with, so it was replaced on the way and
    /// never filtered anything.</summary>
    [BlenderFact]
    public async Task Journal_AskedForOneAgent_LeavesTheOthersOut()
    {
        await Send(_lighting, BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Lamp" });
        await Send(_modeling, BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Crate" });

        var mine = await Send(_modeling, BridgeCommands.Journal, new JsonObject { ["author"] = "lighting" });

        Assert.NotEmpty(mine["entries"]!.AsArray());
        Assert.All(mine["entries"]!.AsArray(), entry => Assert.Equal("lighting", entry!["agent"]!.ToString()));
    }

    /// <summary>Leases and the journal live in files under the data directory, so a worker Blender started for a batch sees the lease the main one holds and its steps land in the same journal.</summary>
    [BlenderFact]
    public async Task Lease_HeldInOneBlender_IsHonouredByABatchWorkerAndBothWriteOneJournal()
    {
        await Send(_lighting, BridgeCommands.Lease, new JsonObject { ["action"] = "claim", ["names"] = new JsonArray("Cube"), ["note"] = "relighting" });
        var started = await Send(_modeling, BridgeCommands.BatchJob, new JsonObject
        {
            ["directory"] = Path.Combine(_workDirectory, "leased-batches"),
            ["output"] = Path.Combine(_workDirectory, "leased.blend"),
            ["continue_on_error"] = true,
            ["commands"] = new JsonArray(
                new JsonObject { ["command"] = "transform_object", ["params"] = new JsonObject { ["name"] = "Cube", ["location"] = new JsonArray(2, 0, 0) } },
                new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "uv_sphere", ["name"] = "Free" } }),
        }, TimeSpan.FromSeconds(120));

        JsonNode status = started;
        for (var attempt = 0; attempt < 90 && status["state"]!.ToString() is "queued" or "running"; attempt++)
        {
            await Task.Delay(1000);
            status = await Send(_modeling, BridgeCommands.BatchStatus, new JsonObject { ["id"] = started["id"]!.ToString(), ["directory"] = Path.Combine(_workDirectory, "leased-batches") });
        }

        var steps = status["steps"]!.AsArray();
        Assert.Equal("finished", status["state"]!.ToString());
        Assert.False(steps[0]!["ok"]!.GetValue<bool>(), status.ToJsonString());
        Assert.Contains("Leased", steps[0]!["error"]!.ToString(), StringComparison.Ordinal);
        Assert.True(steps[1]!["ok"]!.GetValue<bool>(), status.ToJsonString());

        var journal = await Send(_lighting, BridgeCommands.Journal);
        var entries = journal["entries"]!.AsArray();
        Assert.True(File.Exists(Path.Combine(_blender.DataDirectory, "state", "journal.jsonl")));
        Assert.True(entries.Any(entry => entry!["command"]!.ToString() == "add_primitive" && entry["targets"]!.AsArray().Any(target => target!.ToString() == "Free")), journal.ToJsonString());
        Assert.True(entries.Select(entry => entry!["pid"]!.GetValue<int>()).Distinct().Count() >= 2, journal.ToJsonString());
        Assert.True(entries.Any(entry => entry!["command"]!.ToString() == "batch_job" && entry["targets"]!.AsArray().Count == 0), journal.ToJsonString());
    }

    [BlenderFact]
    public async Task Link_WithoutTheAddOnToken_IsRefusedUntilItCarriesOne()
    {
        using var guarded = await HeadlessBlender.StartAsync(token: "studio-42");
        await using var anonymous = new BlenderConnection(new BlenderLinkOptions { Port = guarded.Port, ConnectTimeoutSeconds = 5, RequestTimeoutSeconds = 30 }, NullLogger<BlenderConnection>.Instance);
        await using var trusted = new BlenderConnection(new BlenderLinkOptions { Port = guarded.Port, ConnectTimeoutSeconds = 5, RequestTimeoutSeconds = 30, Token = "studio-42" }, NullLogger<BlenderConnection>.Instance);

        var refused = await anonymous.SendAsync(BridgeCommands.SceneInfo);
        var answered = await trusted.SendAsync(BridgeCommands.SceneInfo);

        Assert.Equal("Unauthorized", refused.Error!.Type);
        Assert.True(answered.IsOk, $"{answered.Error?.Type} {answered.Error?.Message}");
    }

    /// <summary>An installed extension lives in a folder named snail_bridge, not addon; the batch worker imports the package by that folder name.</summary>
    [BlenderFact]
    public async Task Batch_UnderTheExtensionFolderName_StillFindsThePackage()
    {
        using var extension = await HeadlessBlender.StartAsync("snail_bridge");
        await using var link = new BlenderConnection(extension.Link, NullLogger<BlenderConnection>.Instance);
        var output = Path.Combine(_workDirectory, "ext.blend");

        var started = await Send(link, BridgeCommands.BatchJob, new JsonObject
        {
            ["directory"] = Path.Combine(_workDirectory, "ext-batches"),
            ["output"] = output,
            ["commands"] = new JsonArray(new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cone", ["name"] = "Ext" } }),
        }, TimeSpan.FromSeconds(120));

        JsonNode status = started;
        for (var attempt = 0; attempt < 90 && status["state"]!.ToString() is "queued" or "running"; attempt++)
        {
            await Task.Delay(1000);
            status = await Send(link, BridgeCommands.BatchStatus, new JsonObject { ["id"] = started["id"]!.ToString(), ["directory"] = Path.Combine(_workDirectory, "ext-batches") });
        }

        Assert.Equal("finished", status["state"]!.ToString());
        Assert.True(File.Exists(output));
    }

    [BlenderFact]
    public async Task Batch_InASeparateBlender_LandsInAFileTheSceneCanLink()
    {
        var output = Path.Combine(_workDirectory, "crate.blend");
        var started = await Send(_modeling, BridgeCommands.BatchJob, new JsonObject
        {
            ["directory"] = Path.Combine(_workDirectory, "batches"),
            ["output"] = output,
            ["commands"] = new JsonArray(
                new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cube", ["name"] = "Crate", ["location"] = new JsonArray(0, 0, 2) } },
                new JsonObject { ["command"] = "add_modifier", ["params"] = new JsonObject { ["name"] = "Crate", ["type"] = "BEVEL" } },
                new JsonObject { ["command"] = "object_info", ["params"] = new JsonObject { ["name"] = "Crate" } }),
        }, TimeSpan.FromSeconds(120));

        JsonNode status = started;
        for (var attempt = 0; attempt < 90 && status["state"]!.ToString() is "queued" or "running"; attempt++)
        {
            await Task.Delay(1000);
            status = await Send(_modeling, BridgeCommands.BatchStatus, new JsonObject { ["id"] = started["id"]!.ToString(), ["directory"] = Path.Combine(_workDirectory, "batches") });
        }

        Assert.Equal("finished", status["state"]!.ToString());
        Assert.Equal(3, status["steps_done"]!.GetValue<int>());
        Assert.True(File.Exists(output));

        var listed = await Send(_lighting, BridgeCommands.ListAssets, new JsonObject { ["path"] = output, ["types"] = new JsonArray("objects") });
        Assert.Contains("Crate", listed["contents"]!["objects"]!.AsArray().Select(node => node!.ToString()));
    }

    /// <summary>The point of a sequence over a script: every step is a real command, so each one is guarded, journalled with the objects it touched and reported on its own.</summary>
    [BlenderFact]
    public async Task Sequence_RunsEveryStepInThisBlender_AndTheJournalNamesEachCommandWithItsTargets()
    {
        var answer = await Send(_modeling, BridgeCommands.Run, new JsonObject
        {
            ["commands"] = new JsonArray(
                new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cube", ["name"] = "Panel", ["location"] = new JsonArray(0, 0, 3) } },
                new JsonObject { ["command"] = "transform_object", ["params"] = new JsonObject { ["name"] = "Panel", ["scale"] = new JsonArray(2, 1, 0.1) } },
                new JsonObject { ["command"] = "add_modifier", ["params"] = new JsonObject { ["name"] = "Panel", ["type"] = "BEVEL" } },
                new JsonObject { ["command"] = "object_info", ["params"] = new JsonObject { ["name"] = "Panel" } }),
        }, TimeSpan.FromSeconds(120));

        var steps = answer["steps"]!.AsArray();
        Assert.Equal(4, answer["ran"]!.GetValue<int>());
        Assert.Empty(answer["failed"]!.AsArray());
        Assert.All(steps, step => Assert.True(step!["ok"]!.GetValue<bool>(), answer.ToJsonString()));
        Assert.Equal(2, steps[3]!["result"]!["scale"]![0]!.GetValue<double>());

        var journal = await Send(_modeling, BridgeCommands.Journal);
        var entries = journal["entries"]!.AsArray();
        Assert.True(entries.Any(entry => entry!["command"]!.ToString() == "add_modifier" && entry["agent"]!.ToString() == "modeling"
            && entry["targets"]!.AsArray().Any(target => target!.ToString() == "Panel")), journal.ToJsonString());
        Assert.DoesNotContain(entries, entry => entry!["command"]!.ToString() == "object_info");
    }

    /// <summary>A step on another agent's object is refused inside a sequence exactly as a direct call would be; the steps before it stand, and the reply says which one stopped.</summary>
    [BlenderFact]
    public async Task Sequence_TouchingALeasedObject_StopsThere_AndKeepsWhatRanBefore()
    {
        await Send(_lighting, BridgeCommands.Lease, new JsonObject { ["action"] = "claim", ["names"] = new JsonArray("Cube"), ["note"] = "relighting" });

        var refused = await _modeling.SendAsync(BridgeCommands.Run, new JsonObject
        {
            ["commands"] = new JsonArray(
                new JsonObject { ["command"] = "add_primitive", ["params"] = new JsonObject { ["kind"] = "cone", ["name"] = "Spire" } },
                new JsonObject { ["command"] = "transform_object", ["params"] = new JsonObject { ["name"] = "Cube", ["location"] = new JsonArray(5, 0, 0) } }),
        }, TimeSpan.FromSeconds(60));

        Assert.False(refused.IsOk);
        Assert.Equal("StepFailed", refused.Error!.Type);
        Assert.Contains("Leased", refused.Error.Details!["steps"]![1]!["error"]!.ToString(), StringComparison.Ordinal);
        Assert.True(refused.Error.Details["steps"]![0]!["ok"]!.GetValue<bool>());
        Assert.Equal([2], refused.Error.Details["failed"]!.AsArray().Select(index => index!.GetValue<int>()));

        var spire = await Send(_modeling, BridgeCommands.ObjectInfo, new JsonObject { ["name"] = "Spire" });
        Assert.Equal("Spire", spire["name"]!.ToString());

        await Send(_lighting, BridgeCommands.Lease, new JsonObject { ["action"] = "release", ["names"] = new JsonArray("Cube") });
    }

    private static async Task<JsonNode> Send(BlenderConnection link, BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
