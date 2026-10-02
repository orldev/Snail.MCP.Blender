using System.Globalization;
using System.Text.RegularExpressions;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>The Python add-on is code this suite cannot execute, so its contract is checked from the text: the manifest Blender reads, the command registry the C# bridge relies on, and the bundling that puts it next to the binary.</summary>
public class AddOnContractTests
{
    private static readonly Regex CommandRegistration = new(@"^@(?:immediate_)?command\(""([a-z_]+)""\)", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex AddOnErrors = new(@"CommandError\(""([A-Za-z]+)""", RegexOptions.Compiled);

    private static readonly Regex ScriptedErrors = new(@"new BridgeError\(""([A-Za-z]+)""", RegexOptions.Compiled);

    private static readonly Regex ImmediateRegistration = new(@"^@immediate_command\(""([a-z_]+)""\)", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex WireKey = new(@"\.With\(""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex ManifestEntry = new(@"^(?<key>[a-z_]+)\s*=\s*""(?<value>[^""]*)""", RegexOptions.Multiline | RegexOptions.Compiled);

    [Fact]
    public void AddOn_IsBundled_NextToTheServerBinary()
    {
        Assert.True(File.Exists(ServerPaths.BundledAddOnManifest), $"missing {ServerPaths.BundledAddOnManifest}");
        Assert.True(File.Exists(Path.Combine(ServerPaths.BundledAddOnDirectory, "__init__.py")));
    }

    [Fact]
    public void Manifest_DeclaresAnExtension_ForBlender42OrNewer()
    {
        var manifest = Manifest();

        Assert.Equal("snail_bridge", manifest["id"]);
        Assert.Equal("add-on", manifest["type"]);
        Assert.True(Version.Parse(manifest["blender_version_min"]) >= new Version(4, 2, 0));
        Assert.Matches(@"^\d+\.\d+\.\d+$", manifest["version"]);
    }

    [Fact]
    public void CommandRegistry_NamesAreUnique_AndCoverTheBaseline()
    {
        var names = RegisteredCommands();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Superset(
            new HashSet<string> { "ping", "python", "run_operator", "describe_operator", "scene_info", "list_objects", "object_info" },
            names.ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryCommandModule_IsImportedByThePackage()
    {
        var modules = Directory.EnumerateFiles(AddOnSource(), "*.py")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(module => CommandRegistration.IsMatch(File.ReadAllText(Path.Combine(AddOnSource(), $"{module}.py"))))
            .Order()
            .ToList();

        Assert.Equal(modules, ImportedModules().Where(modules.Contains).Order());
    }

    [Fact]
    public void SocketThreads_NeverTouchBpy_OutsideTheImmediateHandlers()
    {
        var server = File.ReadAllText(Path.Combine(AddOnSource(), "server.py"));
        var bpyUses = Regex.Count(server, @"\bbpy\.(?!app\.timers\b)[a-z_]");

        Assert.Equal(0, bpyUses);
    }

    /// <summary>The command schema names every command the add-on registers and no others.</summary>
    /// <remarks>Everything read from it — what only reads, what destroys, which parameters name objects, which name paths — is read for
    /// commands by name, so a schema that drifts from the registry silently stops guarding the commands it has lost and guards names that
    /// do not exist.</remarks>
    [Fact]
    public void CommandSchema_NamesEveryRegisteredCommand_AndNoOther()
    {
        var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AddOnSource(), "commands.json")))!;
        var described = schema["commands"]!.AsObject().Select(command => command.Key).ToHashSet(StringComparer.Ordinal);
        var registered = RegisteredCommands().ToHashSet(StringComparer.Ordinal);
        var named = schema["named_after_something_else"]!.AsArray().Select(name => name!.GetValue<string>()).ToList();

        Assert.Equal(registered.Order(StringComparer.Ordinal), described.Order(StringComparer.Ordinal));
        Assert.All(named, name => Assert.Contains(name, registered));
        Assert.NotEmpty(schema["target_keys"]!.AsArray());
    }

    /// <summary>Every command says which parameters it takes, and the paths it names are among them.</summary>
    /// <remarks>The add-on refuses a parameter a command does not read, and learns which those are from this schema alone. A command with
    /// no list is a command whose callers can misspell anything and hear nothing back, which is the silence this whole guard exists to end:
    /// set_material once took an 'objects' list, answered ok and assigned nothing. Whether each list matches the handler that reads it is
    /// checked by <c>tools/parameters_from_source.py</c>, which reads the Python this suite cannot run; what is checked here is that a list
    /// exists at all and that the rest of the schema does not contradict it.</remarks>
    [Fact]
    public void CommandSchema_DeclaresTheParameters_OfEveryCommand()
    {
        var commands = JsonNode.Parse(File.ReadAllText(Path.Combine(AddOnSource(), "commands.json")))!["commands"]!.AsObject();

        var silent = commands.Where(command => command.Value?["params"] is not JsonArray).Select(command => command.Key).ToList();
        var strays = commands
            .Where(command => command.Value?["paths"] is JsonArray)
            .SelectMany(command => command.Value!["paths"]!.AsArray()
                .Select(path => path!.GetValue<string>())
                .Where(path => !Parameters(command.Value!).Contains(path, StringComparer.Ordinal))
                .Select(path => $"{command.Key}.{path}"))
            .ToList();

        Assert.True(silent.Count == 0, $"commands that declare no parameters, so nothing they are sent can be refused: {string.Join(", ", silent)}");
        Assert.True(strays.Count == 0, $"paths that are not parameters of their own command: {string.Join(", ", strays)}");
        Assert.Contains(commands, command => Parameters(command.Value!).Count > 0);
    }

    /// <summary>Where a command names an object, it names it through a parameter the same command declares.</summary>
    /// <remarks>The lease guard walks these paths to learn what a command will touch before it touches it, and a path that starts at a
    /// parameter no command takes walks into nothing and refuses nobody. Whether each path matches the handler that reads it is checked by
    /// <c>tools/parameters_from_source.py</c>; what is checked here is that the two lists of the same command agree with each other.</remarks>
    [Fact]
    public void EveryPlaceACommandNamesAnObject_IsOneOfItsOwnParameters()
    {
        var commands = JsonNode.Parse(File.ReadAllText(Path.Combine(AddOnSource(), "commands.json")))!["commands"]!.AsObject();

        var rootless = commands
            .Where(command => command.Value?["targets"] is JsonArray)
            .SelectMany(command => command.Value!["targets"]!.AsArray()
                .Select(target => target!.GetValue<string>())
                .Where(target => !Parameters(command.Value!).Contains(target.Split('.')[0].Replace("[]", string.Empty), StringComparer.Ordinal))
                .Select(target => $"{command.Key}: {target}"))
            .ToList();

        Assert.True(rootless.Count == 0, $"places named for the lease guard that no parameter of the command leads to: {string.Join(", ", rootless)}");
        Assert.Contains(commands, command => command.Value?["targets"] is JsonArray);
    }

    /// <summary>The guard reads those paths, rather than guessing from a list of likely parameter names.</summary>
    /// <remarks>The guess is still made — it is generous on purpose, and the journal wants it — but on its own it missed an object named
    /// inside a block, in a key of its own or as the key of a block, and missing one means a command touches what another agent holds
    /// while neither the refusal nor the journal says so.</remarks>
    [Fact]
    public void TheLeaseGuard_ReadsWhereACommandReallyNamesAnObject()
    {
        var guard = File.ReadAllText(Path.Combine(AddOnSource(), "team.py"));

        Assert.Contains("catalog.targets_of(name)", guard, StringComparison.Ordinal);
        Assert.Contains("def _names_at(value, path)", guard, StringComparison.Ordinal);
        var refusing = guard[guard.IndexOf("def guard(", StringComparison.Ordinal)..];
        refusing = refusing[..refusing.IndexOf("\ndef ", StringComparison.Ordinal)];

        Assert.Contains("_addressed(", refusing, StringComparison.Ordinal);
        Assert.DoesNotContain("_targets(", refusing, StringComparison.Ordinal);
    }

    /// <summary>The gate that refuses an unknown parameter is part of the add-on, not an idea in a document.</summary>
    /// <remarks>It guards by being registered: a module the package does not import registers nothing, and a gate outside <c>core.GATES</c>
    /// is never called. Both were true of this check before it existed, and the failure mode is the one it was written against — everything
    /// passes, quietly.</remarks>
    [Fact]
    public void TheParameterGate_IsImportedByThePackage_AndRegisteredWithTheDispatcher()
    {
        var gate = File.ReadAllText(Path.Combine(AddOnSource(), "parameters.py"));

        Assert.Contains("parameters", ImportedModules());
        Assert.Contains("core.GATES.append(refuse)", gate, StringComparison.Ordinal);
        Assert.Contains("CARRIED_BY_THE_LINK = (\"agent\",)", gate, StringComparison.Ordinal);
    }

    /// <summary>Every parameter the server writes onto the wire is one some command declares.</summary>
    /// <remarks>Now that the add-on refuses what it does not read, a key the server invents is no longer dropped in silence — it is a refusal
    /// the caller cannot fix, which is worse. The tools were measured against the schema when the gate went in and none was wrong; this keeps
    /// it that way from the side where a new tool is written. A key that belongs to a nested block does not appear here: blocks travel as typed
    /// records in snake case, which <c>NestedParameters_AreTypedBlocks_ExceptTheFreeFormPassThroughs</c> requires.</remarks>
    [Fact]
    public void EveryParameterTheServerSends_IsOneSomeCommandDeclares()
    {
        var declared = JsonNode.Parse(File.ReadAllText(Path.Combine(AddOnSource(), "commands.json")))!["commands"]!.AsObject()
            .SelectMany(command => Parameters(command.Value!))
            .Append("agent")
            .ToHashSet(StringComparer.Ordinal);

        var invented = Directory
            .EnumerateFiles(Path.Combine(Repository.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => WireKey.Matches(File.ReadAllText(file)).Select(match => (File: Path.GetFileName(file), Key: match.Groups[1].Value)))
            .Where(written => !declared.Contains(written.Key))
            .Distinct()
            .ToList();

        Assert.True(
            invented.Count == 0,
            $"parameters no command takes, which the add-on now refuses: {string.Join(", ", invented.Select(written => $"'{written.Key}' in {written.File}"))}");
    }

    /// <summary>The three readings of a channel agree: the schema, the add-on's own registration and the server's catalog.</summary>
    /// <remarks>The channel is what sends a command down one socket rather than another, and only the add-on decides which thread answers it.
    /// A command registered with <c>@immediate_command</c> but left on the control channel would queue behind a render for no reason; one the
    /// other way round would be sent down the data link and wait there while the main thread renders, with the download it was meant to keep
    /// clear waiting behind it. Neither shows up as a failure — only as a link that is slow when Blender is busy.</remarks>
    [Fact]
    public void Channels_AreTheSame_InTheSchema_InTheAddOn_AndInTheServersCatalog()
    {
        var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(AddOnSource(), "commands.json")))!;
        var known = schema["channels"]!.AsObject().Select(channel => channel.Key).ToList();
        var offTheMainThread = schema["commands"]!.AsObject()
            .Where(command => command.Value?["channel"] is not null)
            .ToDictionary(command => command.Key, command => command.Value!["channel"]!.GetValue<string>(), StringComparer.Ordinal);

        Assert.Equal(Enum.GetNames<BridgeChannel>().Select(name => name.ToLowerInvariant()).Order(StringComparer.Ordinal), known.Order(StringComparer.Ordinal));
        Assert.All(offTheMainThread, command => Assert.Contains(command.Value, known));
        Assert.DoesNotContain(nameof(BridgeChannel.Control).ToLowerInvariant(), offTheMainThread.Values);
        Assert.Equal(
            offTheMainThread.Keys.Order(StringComparer.Ordinal),
            ImmediateCommands().Order(StringComparer.Ordinal));
        Assert.Equal(
            offTheMainThread.Select(command => $"{command.Key}: {command.Value}").Order(StringComparer.Ordinal),
            BridgeCommands.All
                .Where(command => command.Channel != BridgeChannel.Control)
                .Select(command => $"{command.Name}: {command.Channel.ToString().ToLowerInvariant()}")
                .Order(StringComparer.Ordinal));
    }

    /// <summary>The protocol and the abilities are one number and one list, written on both sides and equal.</summary>
    /// <remarks>They are what a server reads to say what an older add-on will not do for it, so a name that exists on one side only says
    /// nothing: the server would report an add-on as lacking something it has, or count on something it does not.</remarks>
    [Fact]
    public void Protocol_AndCapabilities_AreTheSame_OnBothSides()
    {
        var diagnostics = File.ReadAllText(Path.Combine(AddOnSource(), "diagnostics.py"));
        var spoken = int.Parse(Regex.Match(diagnostics, @"^PROTOCOL = (\d+)", RegexOptions.Multiline).Groups[1].Value, CultureInfo.InvariantCulture);
        var able = Regex.Matches(Regex.Match(diagnostics, @"^CAPABILITIES = \(([^)]*)\)", RegexOptions.Multiline).Groups[1].Value, @"""([a-z-]+)""")
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.Equal(AddOnProtocol.Version, spoken);
        Assert.Equal(AddOnProtocol.Required.Order(StringComparer.Ordinal), able.Order(StringComparer.Ordinal));
    }

    /// <summary>A refusal a test hands to a fake is one Blender really gives: the add-on's own error types come from the recording, never
    /// from the test's imagination.</summary>
    /// <remarks>The type, the message and the details decide the hint a tool gives and whether a step recovers, and none of it can be seen
    /// from this side. A test that writes its own <c>NotFound</c> proves only that the code reads what its author imagined; the same test
    /// replaying <c>tests/Contracts/addon-answers.json</c> fails when Blender starts answering something else, because the live test that
    /// recorded it fails first. Types the server itself makes — a timeout, an unreachable link — are its own to write.</remarks>
    [Fact]
    public void Tests_HandingARefusalToAFake_TakeTheAddOnsOwnTypesFromTheRecording()
    {
        var addOn = ImportedModules()
            .SelectMany(module => AddOnErrors.Matches(File.ReadAllText(Path.Combine(AddOnSource(), $"{module}.py"))).Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        var invented = Directory
            .EnumerateFiles(Path.Combine(Repository.Root, "tests"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => ScriptedErrors.Matches(File.ReadAllText(file)).Select(match => (File: Path.GetFileName(file), Type: match.Groups[1].Value)))
            .Where(scripted => addOn.Contains(scripted.Type))
            .ToList();

        Assert.NotEmpty(RecordedAnswers.All);
        Assert.True(
            invented.Count == 0,
            $"the add-on's own refusals belong in addon-answers.json, not in a test: {string.Join(", ", invented.Select(scripted => $"{scripted.Type} in {scripted.File}"))}");
    }

    /// <summary>The parameters one command entry of the schema declares.</summary>
    private static IReadOnlyList<string> Parameters(JsonNode command) =>
        command["params"] is JsonArray declared ? [.. declared.Select(name => name!.GetValue<string>())] : [];

    /// <summary>The commands the add-on answers from the socket thread rather than queueing for Blender's main thread.</summary>
    private static IReadOnlyList<string> ImmediateCommands() =>
        [.. ImportedModules().SelectMany(module => ImmediateRegistration.Matches(File.ReadAllText(Path.Combine(AddOnSource(), $"{module}.py"))).Select(match => match.Groups[1].Value))];

    /// <summary>Every module of the add-on registers commands; a module the package does not import registers nothing, so only imported modules count.</summary>
    internal static IReadOnlyList<string> RegisteredCommands() =>
        [.. ImportedModules().SelectMany(module => CommandRegistration.Matches(File.ReadAllText(Path.Combine(AddOnSource(), $"{module}.py"))).Select(match => match.Groups[1].Value))];

    /// <summary>The import may be one line or a parenthesised block, which is how an import sorter leaves it.</summary>
    private static IReadOnlyList<string> ImportedModules() =>
        Regex.Match(File.ReadAllText(Path.Combine(AddOnSource(), "__init__.py")), @"^from \. import (?<modules>\([^)]*\)|[^\n]+)", RegexOptions.Multiline)
            .Groups["modules"].Value
            .Trim('(', ')')
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static Dictionary<string, string> Manifest() =>
        ManifestEntry.Matches(File.ReadAllText(Path.Combine(AddOnSource(), ServerPaths.AddOnManifestFileName)))
            .ToDictionary(match => match.Groups["key"].Value, match => match.Groups["value"].Value, StringComparer.Ordinal);

    internal static string AddOnSource() => Path.Combine(FindRepositoryRoot(), ServerPaths.AddOnFolderName);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ServerPaths.AddOnFolderName, ServerPaths.AddOnManifestFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"addon/{ServerPaths.AddOnManifestFileName} not found above {AppContext.BaseDirectory}");
    }
}
