using System.Text.Json;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>What the add-on really answered, recorded from a Blender and replayed into the fakes.</summary>
/// <remarks>A test that writes the reply it expects proves only that the code reads what the test's author imagined. The failure paths are
/// where that costs most: the type, the message and the details of an error decide the hint a tool gives and whether a step recovers, and
/// none of it is visible from this side. Each answer here was taken from a live Blender by <c>AddOnAnswersLiveTests</c>, which runs the same
/// scenarios again and fails when the add-on has started answering something else.</remarks>
public static class RecordedAnswers
{
    private static readonly Lazy<IReadOnlyDictionary<string, RecordedAnswer>> Answers = new(Read);

    public static string File => Path.Combine(Repository.Root, "tests", "Contracts", "addon-answers.json");

    public static IReadOnlyList<RecordedAnswer> All => [.. Answers.Value.Values];

    public static RecordedAnswer Of(string scenario) =>
        Answers.Value.TryGetValue(scenario, out var answer)
            ? answer
            : throw new InvalidOperationException($"no answer recorded for '{scenario}'; run the live tests with SNAIL_TEST_RECORD=1 to record it");

    /// <summary>The reply the add-on gave, as the bridge hands it on.</summary>
    public static BridgeReply Reply(string scenario) => Of(scenario).Reply;

    /// <summary>The error the add-on answered with, for a fake that has to refuse the way Blender does.</summary>
    public static BridgeError Error(string scenario) =>
        Reply(scenario).Error ?? throw new InvalidOperationException($"'{scenario}' was recorded as a success, not a refusal");

    /// <summary>The result the add-on returned, for a fake that has to answer the way Blender does.</summary>
    public static JsonNode? Result(string scenario) => Reply(scenario).Result?.DeepClone();

    private static IReadOnlyDictionary<string, RecordedAnswer> Read()
    {
        var recorded = JsonNode.Parse(System.IO.File.ReadAllText(File))!["answers"]!.AsArray();

        return recorded
            .OfType<JsonObject>()
            .Select(RecordedAnswer.From)
            .ToDictionary(answer => answer.Scenario, StringComparer.Ordinal);
    }

    /// <summary>Writes the answers back, in the order they were recorded; the live test does this when it is asked to record.</summary>
    public static void Write(string note, IEnumerable<RecordedAnswer> answers)
    {
        var document = new JsonObject
        {
            ["note"] = note,
            ["answers"] = new JsonArray([.. answers.Select(answer => answer.ToJson())]),
        };

        System.IO.File.WriteAllText(File, $"{document.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}\n");
    }
}

/// <summary>One scenario and the reply a real add-on gave it.</summary>
/// <param name="Scenario">What was asked of Blender, in words, which is how a test names the answer it wants.</param>
/// <param name="Command">The bridge command that was sent.</param>
/// <param name="Reply">What the add-on answered.</param>
/// <param name="Volatile">Fields of the reply that differ from one machine to the next — a path, a name, a count — and are not compared.</param>
public sealed record RecordedAnswer(string Scenario, string Command, BridgeReply Reply, IReadOnlyList<string> Volatile)
{
    public static RecordedAnswer From(JsonObject recorded)
    {
        var reply = recorded["ok"]!.GetValue<bool>()
            ? BridgeReply.Ok(recorded["result"]?.DeepClone())
            : BridgeReply.Failed(new BridgeError(
                recorded["error"]!["type"]!.GetValue<string>(),
                recorded["error"]!["message"]!.GetValue<string>(),
                recorded["error"]!["details"]?.DeepClone()));

        return new RecordedAnswer(
            recorded["scenario"]!.GetValue<string>(),
            recorded["command"]!.GetValue<string>(),
            reply,
            [.. recorded["volatile"]?.AsArray().Select(field => field!.GetValue<string>()) ?? []]);
    }

    public JsonObject ToJson()
    {
        var recorded = new JsonObject
        {
            ["scenario"] = Scenario,
            ["command"] = Command,
            ["ok"] = Reply.IsOk,
        };

        if (Reply.IsOk)
        {
            recorded["result"] = Reply.Result?.DeepClone();
        }
        else
        {
            recorded["error"] = new JsonObject
            {
                ["type"] = Reply.Error!.Type,
                ["message"] = Reply.Error.Message,
                ["details"] = Reply.Error.Details?.DeepClone(),
            };
        }

        if (Volatile.Count > 0)
        {
            recorded["volatile"] = new JsonArray([.. Volatile.Select(field => (JsonNode)JsonValue.Create(field)!)]);
        }

        return recorded;
    }

    /// <summary>How this answer differs from one just heard, ignoring what was recorded as volatile; empty when they agree.</summary>
    public IReadOnlyList<string> Differences(BridgeReply heard)
    {
        var differences = new List<string>();

        if (Reply.IsOk != heard.IsOk)
        {
            differences.Add($"recorded {(Reply.IsOk ? "a result" : $"'{Reply.Error!.Type}'")}, heard {(heard.IsOk ? "a result" : $"'{heard.Error!.Type}'")}");

            return differences;
        }

        if (!Reply.IsOk)
        {
            Compare(differences, "type", Reply.Error!.Type, heard.Error!.Type);
            Compare(differences, "message", Reply.Error.Message, heard.Error.Message);
            Compare(differences, "details", Shape(Reply.Error.Details), Shape(heard.Error.Details));

            return differences;
        }

        Compare(differences, "result", Shape(Reply.Result), Shape(heard.Result));

        return differences;
    }

    private void Compare(List<string> differences, string field, string recorded, string heard)
    {
        if (!Volatile.Contains(field, StringComparer.Ordinal) && !string.Equals(recorded, heard, StringComparison.Ordinal))
        {
            differences.Add($"{field}: recorded \"{recorded}\", heard \"{heard}\"");
        }
    }

    /// <summary>The keys of a reply rather than its values: a name, a path and a frame count belong to the Blender that answered, the keys to
    /// the add-on.</summary>
    private static string Shape(JsonNode? node) =>
        node switch
        {
            JsonObject entries => $"{{{string.Join(", ", entries.Select(entry => $"{entry.Key}: {Shape(entry.Value)}").Order(StringComparer.Ordinal))}}}",
            JsonArray items => items.Count == 0 ? "[]" : $"[{Shape(items[0])}]",
            null => "null",
            _ => node.GetValueKind().ToString().ToLowerInvariant(),
        };
}
