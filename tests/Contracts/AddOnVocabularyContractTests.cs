using System.Text.RegularExpressions;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>Closed vocabularies live twice, in the add-on and in the C# catalog the descriptions are checked against; this keeps both copies equal.</summary>
public partial class AddOnVocabularyContractTests
{
    [Fact]
    public void PrimitiveKinds_MatchTheAddOnTable()
    {
        var table = PrimitivesTable().Match(AddOnFile("objects")).Groups["body"].Value;
        var keys = TableKey().Matches(table).Select(match => match.Groups[1].Value);

        Assert.Equal(Primitives.Kinds, keys);
    }

    [Fact]
    public void LightTypes_MatchTheAddOnTuple()
    {
        var tuple = LightsTuple().Match(AddOnFile("lights")).Groups["body"].Value;
        var types = UpperQuoted().Matches(tuple).Select(match => match.Groups[1].Value);

        Assert.Equal(Lights.Kinds, types);
    }

    [Fact]
    public void LightShapes_MatchTheAddOnTuple()
    {
        var tuple = LightShapesTuple().Match(AddOnFile("lights")).Groups["body"].Value;

        Assert.Equal(Lights.Shapes, UpperQuoted().Matches(tuple).Select(match => match.Groups[1].Value));
    }

    [Fact]
    public void ResolutionPresets_MatchTheAddOnTable()
    {
        var source = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "output.py"));
        var table = ResolutionPresetsTable().Match(source).Groups["body"].Value;

        Assert.Equal(Output.ResolutionPresets, PresetKey().Matches(table).Select(match => match.Groups[1].Value));
    }

    [Fact]
    public void CameraMoves_MatchTheAddOnTuple()
    {
        var source = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "moves.py"));
        var tuple = MovesTuple().Match(source).Groups["body"].Value;

        Assert.Equal(CameraMoves.All, SnakeQuoted().Matches(tuple).Select(match => match.Groups[1].Value));
    }

    [Fact]
    public void VolumeAreas_MatchTheAddOnTuple()
    {
        var tuple = AreasTuple().Match(AddOnFile("transfer")).Groups["body"].Value;

        Assert.Equal(VolumeAreas.All, LowerQuoted().Matches(tuple).Select(match => match.Groups[1].Value));
    }

    /// <summary>The states of a render job are the add-on's; the server reads them in four places and writing one of them by hand is how a
    /// page came to call a job that ended "running".</summary>
    [Fact]
    public void JobStates_MatchTheAddOnTuple()
    {
        var tuple = StatesTuple().Match(AddOnFile("jobs")).Groups["body"].Value;

        Assert.Equal(JobStates.All, LowerQuoted().Matches(tuple).Select(match => match.Groups[1].Value));
    }

    [Fact]
    public void ClosedVocabularies_MatchTheAddOnTables()
    {
        var physics = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "physics.py"));
        var objects = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), "objects.py"));
        var modifierPhysics = TableKey().Matches(ModifierPhysicsTable().Match(physics).Groups["body"].Value)
            .Select(match => match.Groups[1].Value);
        var empties = UpperSnakeQuoted().Matches(EmptyTypesTuple().Match(objects).Groups["body"].Value).Select(match => match.Groups[1].Value);

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
        FormatKey().Matches(Regex.Match(source, $@"{table} = \{{(?<body>.*?)\n\}}", RegexOptions.Singleline).Groups["body"].Value)
            .Select(match => match.Groups[1].Value);

    private static IEnumerable<string> Tuple(string source, string name) =>
        AnyQuoted().Matches(Regex.Match(source, $@"{name} = \((?<body>.*?)\)", RegexOptions.Singleline).Groups["body"].Value)
            .Select(match => match.Groups[1].Value);

    [GeneratedRegex(@"PRIMITIVES = \{(?<body>.*?)\n\}", RegexOptions.Singleline)]
    private static partial Regex PrimitivesTable();

    [GeneratedRegex(@"RESOLUTION_PRESETS = \{(?<body>.*?)\n\}", RegexOptions.Singleline)]
    private static partial Regex ResolutionPresetsTable();

    [GeneratedRegex(@"MODIFIER_PHYSICS = \{(?<body>.*?)\n\}", RegexOptions.Singleline)]
    private static partial Regex ModifierPhysicsTable();

    [GeneratedRegex(@"LIGHTS = \((?<body>[^)]*)\)")]
    private static partial Regex LightsTuple();

    [GeneratedRegex(@"LIGHT_SHAPES = \((?<body>[^)]*)\)")]
    private static partial Regex LightShapesTuple();

    [GeneratedRegex(@"MOVES = \((?<body>[^)]*)\)")]
    private static partial Regex MovesTuple();

    [GeneratedRegex(@"AREAS = \((?<body>[^)]*)\)")]
    private static partial Regex AreasTuple();

    [GeneratedRegex(@"STATES = \((?<body>[^)]*)\)")]
    private static partial Regex StatesTuple();

    [GeneratedRegex(@"EMPTY_TYPES = \((?<body>[^)]*)\)")]
    private static partial Regex EmptyTypesTuple();

    [GeneratedRegex(@"^\s*""([a-z_]+)"":", RegexOptions.Multiline)]
    private static partial Regex TableKey();

    [GeneratedRegex(@"""([A-Z0-9_]+)"": \(")]
    private static partial Regex PresetKey();

    [GeneratedRegex(@"""([a-z0-9]+)"":\s")]
    private static partial Regex FormatKey();

    [GeneratedRegex(@"""([A-Z]+)""")]
    private static partial Regex UpperQuoted();

    [GeneratedRegex(@"""([A-Z_]+)""")]
    private static partial Regex UpperSnakeQuoted();

    [GeneratedRegex(@"""([a-z]+)""")]
    private static partial Regex LowerQuoted();

    [GeneratedRegex(@"""([a-z_]+)""")]
    private static partial Regex SnakeQuoted();

    [GeneratedRegex(@"""([A-Za-z0-9_]+)""")]
    private static partial Regex AnyQuoted();
}
