using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>The answers the unit tests are built on, asked of a real Blender again.</summary>
/// <remarks>Everything a tool does with a refusal — the hint it gives, the step it abandons, the name it reports — is decided by the type, the
/// message and the details the add-on sends. A unit test cannot see any of that; it replays what was recorded here. This is the test that keeps
/// the recording honest: it puts every scenario to a live add-on and fails when one of them is answered differently, which is the moment the
/// fakes stopped standing for Blender. Run with <c>SNAIL_TEST_RECORD=1</c> to write the new answers down instead.</remarks>
[Collection("Loopback")]
public sealed class AddOnAnswersLiveTests : IAsyncLifetime
{
    private const string Note =
        "What a real add-on answered, scenario by scenario, so a unit test can refuse the way Blender does. " +
        "Recorded by tests/Live/AddOnAnswersLiveTests.cs, which runs them all again and fails on a difference; " +
        "run it with SNAIL_TEST_RECORD=1 to record. Fields listed as volatile belong to the machine, not the add-on.";

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

    [BlenderFact]
    public async Task RecordedAnswers_AreStillWhatTheAddOnSays()
    {
        var heard = new List<RecordedAnswer>();
        var differences = new List<string>();
        var recording = Environment.GetEnvironmentVariable("SNAIL_TEST_RECORD") is "1";

        foreach (var (scenario, command, parameters, volatiles) in Scenarios())
        {
            var reply = await Prepared(scenario, command, parameters);

            heard.Add(new RecordedAnswer(scenario, command.Name, reply, volatiles));

            if (recording)
            {
                continue;
            }

            differences.AddRange(RecordedAnswers.Of(scenario).Differences(reply).Select(difference => $"{scenario} — {difference}"));
        }

        if (recording)
        {
            RecordedAnswers.Write(Note, heard);
        }

        Assert.Equal(RecordedAnswers.All.Select(answer => answer.Scenario).Order(StringComparer.Ordinal), heard.Select(answer => answer.Scenario).Order(StringComparer.Ordinal));
        Assert.True(differences.Count == 0, $"the add-on no longer answers what the fakes replay:\n{string.Join("\n", differences)}");
    }

    /// <summary>The scenarios, each a refusal or an answer a unit test leans on; the ones that need a scene set up first say so here.</summary>
    private static IEnumerable<(string Scenario, BridgeCommand Command, JsonObject Parameters, string[] Volatile)> Scenarios() =>
    [
        ("a scene that has just been opened", BridgeCommands.SceneInfo, [], []),
        ("object_info for a name no object has", BridgeCommands.ObjectInfo, new JsonObject { ["name"] = "Ghost" }, []),
        ("delete_objects naming one that is not there", BridgeCommands.DeleteObjects, new JsonObject { ["names"] = new JsonArray("Ghost") }, []),
        ("an operator Blender does not have", BridgeCommands.RunOperator, new JsonObject { ["name"] = "mesh.primitive_teapot_add" }, []),
        ("an operator given a property it does not take", BridgeCommands.RunOperator, new JsonObject { ["name"] = "mesh.primitive_cube_add", ["params"] = new JsonObject { ["radius"] = 2 } }, ["message"]),
        ("an operator that cannot run in this context", BridgeCommands.RunOperator, new JsonObject { ["name"] = "mesh.extrude_region_move" }, []),
        ("a primitive of a kind there is none of", BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "dodecahedron" }, []),
        ("a material asked for by a name no material has", BridgeCommands.MaterialInfo, new JsonObject { ["name"] = "Ghost" }, []),
        ("a parameter the command does not read", BridgeCommands.SetCameraOptics, new JsonObject { ["shift_x"] = 0.1 }, []),
        ("the status of a render job that was never queued", BridgeCommands.RenderJobStatus, new JsonObject { ["id"] = "job-that-never-was", ["directory"] = "jobs" }, ["message"]),
        ("a file asked for where there is none", BridgeCommands.FileGet, new JsonObject { ["area"] = VolumeAreas.Files, ["path"] = "nothing/here.png" }, ["message"]),
        ("a path that climbs out of its area", BridgeCommands.FileList, new JsonObject { ["area"] = VolumeAreas.Files, ["path"] = "../.." }, ["message"]),
        ("an area of the data directory there is none of", BridgeCommands.StorageList, new JsonObject { ["area"] = "secrets", ["path"] = "." }, []),
        ("a lease on an object another agent holds", BridgeCommands.TransformObject, new JsonObject { ["name"] = "Leased", ["location"] = new JsonArray(1, 0, 0) }, ["message"]),
        ("a file put where one already is", BridgeCommands.FilePut, Chunk("answers/twice.txt"), ["message", "details"]),
        ("a command of a name the add-on has never registered", Unknown, [], []),
    ];

    /// <summary>Puts Blender in the state the scenario needs, then sends it.</summary>
    /// <remarks>Only two scenarios need one: a lease has to be held by somebody else first, and a file put twice has to be put once.</remarks>
    private async Task<BridgeReply> Prepared(string scenario, BridgeCommand command, JsonObject parameters)
    {
        if (scenario.StartsWith("a lease", StringComparison.Ordinal))
        {
            await using var holder = new BlenderConnection(Signed("lighting"), NullLogger<BlenderConnection>.Instance);

            await holder.SendAsync(BridgeCommands.AddPrimitive, new JsonObject { ["kind"] = "cube", ["name"] = "Leased" });
            await holder.SendAsync(BridgeCommands.Lease, new JsonObject { ["action"] = "claim", ["names"] = new JsonArray("Leased"), ["ttl_seconds"] = 300 });

            await using var stranger = new BlenderConnection(Signed("modeling"), NullLogger<BlenderConnection>.Instance);

            return await stranger.SendAsync(command, parameters, TimeSpan.FromSeconds(30));
        }

        if (scenario.StartsWith("a file put", StringComparison.Ordinal))
        {
            await _link.SendAsync(BridgeCommands.FilePut, Chunk("answers/twice.txt"));
        }

        return await _link.SendAsync(command, parameters, TimeSpan.FromSeconds(30));
    }

    /// <summary>A command name the catalog does not hold, which is the one thing a test cannot ask for through the catalog.</summary>
    private static BridgeCommand Unknown { get; } = new("no_such_command", AddOnModules.Scene);

    private static JsonObject Chunk(string path) =>
        new()
        {
            ["area"] = VolumeAreas.Files,
            ["path"] = path,
            ["data"] = Convert.ToBase64String("twice"u8.ToArray()),
            ["offset"] = 0,
            ["done"] = true,
        };

    private BlenderLinkOptions Signed(string agent) =>
        new() { Port = _blender.Port, RequestTimeoutSeconds = 30, Agent = agent, TokenFile = _blender.Link.TokenFile };
}
