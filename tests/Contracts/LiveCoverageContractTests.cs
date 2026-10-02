using System.Reflection;
using System.Text.RegularExpressions;
using Snail.MCP.Blender.Application.Skills;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>Every command of the catalog is sent at least once against a real Blender; a command no live scenario touches is a command that can break without a test noticing.</summary>
public class LiveCoverageContractTests
{
    /// <summary>These need Blender's interface — a 3D viewport or the Video Sequencer editor — which a headless Blender does not have, so they answer NoArea instead of working.</summary>
    private static readonly HashSet<string> NeedsTheInterface = new(StringComparer.Ordinal)
    {
        "viewport_capture",
        "set_viewport_shading",
        "sequencer_meta",
        "sequencer_proxy",
    };

    [Fact]
    public void EveryCommand_IsSentByALiveScenario_UnlessItNeedsTheInterface()
    {
        var scenarios = LiveSources();

        var untouched = Catalog()
            .Where(entry => !NeedsTheInterface.Contains(entry.Value.Name))
            .Where(entry => !scenarios.Contains(entry.Key))
            .Select(entry => entry.Value.Name)
            .Order()
            .ToList();

        Assert.Empty(untouched);
    }

    /// <summary>An exemption outlives the reason for it: a command that grew testable would keep its excuse unless the list is checked against the scenarios too.</summary>
    [Fact]
    public void ExemptedCommands_AreRealCommands_AndAreNotSentAnyway()
    {
        var scenarios = LiveSources();
        var names = Catalog().ToDictionary(entry => entry.Value.Name, entry => entry.Key, StringComparer.Ordinal);

        Assert.All(NeedsTheInterface, name => Assert.Contains(name, names.Keys));
        Assert.All(NeedsTheInterface, name => Assert.DoesNotContain(names[name], scenarios));
    }

    [Fact]
    public void EverySkill_HasALiveScenario()
    {
        var scenarios = LiveSources();

        Assert.All(
            SkillCatalog.Discover(typeof(SkillAttribute).Assembly),
            skill => Assert.Contains(CommandsOfSkill(skill), scenarios.Contains));
    }

    private static IReadOnlyList<string> CommandsOfSkill(Skill skill) =>
        [.. skill.ToolTypes
            .Select(type => File.ReadAllText(Directory.EnumerateFiles(Path.Combine(Repository.Root, "src", "Tools"), $"{type.Name}.cs", SearchOption.AllDirectories).Single()))
            .SelectMany(source => CommandReference.Matches(source).Select(match => match.Groups["constant"].Value))
            .Distinct(StringComparer.Ordinal)];

    private static Dictionary<string, BridgeCommand> Catalog() =>
        typeof(BridgeCommands)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(BridgeCommand))
            .ToDictionary(field => field.Name, field => (BridgeCommand)field.GetValue(null)!, StringComparer.Ordinal);

    /// <summary>The constants the live scenarios name. Read as whole words rather than searched for as text: <c>BridgeCommands.Run</c>
    /// is a prefix of <c>BridgeCommands.RunOperator</c>, so a substring search would count a command nobody sends as covered.</summary>
    private static HashSet<string> LiveSources()
    {
        var sources = string.Join('\n', Directory.EnumerateFiles(Path.Combine(Repository.Root, "tests", "Live"), "*.cs").Select(File.ReadAllText));

        return [.. CommandReference.Matches(sources).Select(match => match.Groups["constant"].Value)];
    }


    private static readonly Regex CommandReference = new(@"BridgeCommands\.(?<constant>\w+)", RegexOptions.Compiled);
}
