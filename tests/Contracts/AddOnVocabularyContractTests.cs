using System.Text.RegularExpressions;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>Closed vocabularies live twice, in the add-on and in the C# catalog the descriptions are checked against; this keeps both copies equal.</summary>
public class AddOnVocabularyContractTests
{
    [Fact]
    public void PrimitiveKinds_MatchTheAddOnTable()
    {
        var table = Regex.Match(AddOnFile("objects"), @"PRIMITIVES = \{(?<body>.*?)\n\}", RegexOptions.Singleline).Groups["body"].Value;
        var keys = Regex.Matches(table, @"^\s*""([a-z_]+)"":", RegexOptions.Multiline).Select(match => match.Groups[1].Value);

        Assert.Equal(Primitives.Kinds, keys);
    }

    [Fact]
    public void LightTypes_MatchTheAddOnTuple()
    {
        var tuple = Regex.Match(AddOnFile("lights"), @"LIGHTS = \((?<body>[^)]*)\)").Groups["body"].Value;
        var types = Regex.Matches(tuple, @"""([A-Z]+)""").Select(match => match.Groups[1].Value);

        Assert.Equal(Lights.Kinds, types);
    }

    [Fact]
    public void LightShapes_MatchTheAddOnTuple()
    {
        var tuple = Regex.Match(AddOnFile("lights"), @"LIGHT_SHAPES = \((?<body>[^)]*)\)").Groups["body"].Value;

        Assert.Equal(Lights.Shapes, Regex.Matches(tuple, @"""([A-Z]+)""").Select(match => match.Groups[1].Value));
    }

    [Fact]
    public void ResolutionPresets_MatchTheAddOnTable()
    {
        var source = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "output.py"));
        var table = Regex.Match(source, @"RESOLUTION_PRESETS = \{(?<body>.*?)\n\}", RegexOptions.Singleline).Groups["body"].Value;

        Assert.Equal(Output.ResolutionPresets, Regex.Matches(table, @"""([A-Z0-9_]+)"": \(").Select(match => match.Groups[1].Value));
    }

    [Fact]
    public void CameraMoves_MatchTheAddOnTuple()
    {
        var source = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "moves.py"));
        var tuple = Regex.Match(source, @"MOVES = \((?<body>[^)]*)\)").Groups["body"].Value;

        Assert.Equal(CameraMoves.All, Regex.Matches(tuple, @"""([a-z_]+)""").Select(match => match.Groups[1].Value));
    }

    [Fact]
    public void VolumeAreas_MatchTheAddOnTuple()
    {
        var tuple = Regex.Match(AddOnFile("transfer"), @"AREAS = \((?<body>[^)]*)\)").Groups["body"].Value;

        Assert.Equal(VolumeAreas.All, Regex.Matches(tuple, @"""([a-z]+)""").Select(match => match.Groups[1].Value));
    }

    /// <summary>The states of a render job are the add-on's; the server reads them in four places and writing one of them by hand is how a
    /// page came to call a job that ended "running".</summary>
    [Fact]
    public void JobStates_MatchTheAddOnTuple()
    {
        var tuple = Regex.Match(AddOnFile("jobs"), @"STATES = \((?<body>[^)]*)\)").Groups["body"].Value;

        Assert.Equal(JobStates.All, Regex.Matches(tuple, @"""([a-z]+)""").Select(match => match.Groups[1].Value));
    }

    [Fact]
    public void ClosedVocabularies_MatchTheAddOnTables()
    {
        var physics = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "physics.py"));
        var objects = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "objects.py"));
        var modifierPhysics = Regex.Matches(Regex.Match(physics, @"MODIFIER_PHYSICS = \{(?<body>.*?)\n\}", RegexOptions.Singleline).Groups["body"].Value, @"^\s*""([a-z_]+)"":", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value);
        var empties = Regex.Matches(Regex.Match(objects, @"EMPTY_TYPES = \((?<body>[^)]*)\)").Groups["body"].Value, @"""([A-Z_]+)""").Select(match => match.Groups[1].Value);

        Assert.Equal(PhysicsKinds.All.Skip(2), modifierPhysics);
        Assert.Contains("PHYSICS_KINDS = (\"rigid_body\", \"rigid_body_passive\", *MODIFIER_PHYSICS)", physics, StringComparison.Ordinal);
        Assert.Equal(EmptyTypes.All, empties);
    }

    [Fact]
    public void FileFormats_MatchTheAddOnTables()
    {
        var source = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "interchange.py"));

        Assert.Equal(FileFormats.Import, Keys(source, "IMPORTERS"));
        Assert.Equal(FileFormats.Export, Keys(source, "EXPORTERS"));
    }

    [Fact]
    public void OutputVocabularies_MatchTheAddOnTuples()
    {
        var source = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "output.py"));

        Assert.Equal(Output.ColorDepths, Tuple(source, "COLOR_DEPTHS"));
        Assert.Equal(Output.ExrCodecs, Tuple(source, "EXR_CODECS"));
    }

    [Fact]
    public void SequencerVocabularies_MatchTheAddOnTuples()
    {
        var source = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "sequencer.py"));

        Assert.Equal(Sequencer.StripKinds, Tuple(source, "STRIP_KINDS"));
        Assert.Equal(Sequencer.EffectTypes, Tuple(source, "EFFECT_TYPES"));
        Assert.Equal(Sequencer.ModifierTypes, Tuple(source, "STRIP_MODIFIERS"));
        Assert.Equal(Sequencer.ProresProfiles, Tuple(source, "PRORES_PROFILES"));
    }

    private static string AddOnFile(string module) => File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), $"{module}.py"));

    private static IEnumerable<string> Keys(string source, string table) =>
        Regex.Matches(Regex.Match(source, $@"{table} = \{{(?<body>.*?)\n\}}", RegexOptions.Singleline).Groups["body"].Value, @"""([a-z0-9]+)"":\s")
            .Select(match => match.Groups[1].Value);

    private static IEnumerable<string> Tuple(string source, string name) =>
        Regex.Matches(Regex.Match(source, $@"{name} = \((?<body>.*?)\)", RegexOptions.Singleline).Groups["body"].Value, @"""([A-Za-z0-9_]+)""")
            .Select(match => match.Groups[1].Value);
}
