using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;
using Snail.MCP.Blender.Tools;
using Snail.MCP.Blender.Tools.Farm;
using Snail.MCP.Blender.Tools.Post;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>Prompt catalog guards: descriptions are hardcoded consts that drift silently as the code evolves; these tests catch drift at build time.</summary>
public class ToolCatalogConventionTests
{
    private static readonly Regex ToolNamePattern = new("^blender_[a-z0-9_]+$", RegexOptions.Compiled);

    private static readonly Regex ToolReference = new(@"\bblender_[a-z0-9_]+", RegexOptions.Compiled);

    private static IReadOnlyList<MethodInfo> ToolMethods() =>
        [.. typeof(ToolDescriptions).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)];

    private static string ToolName(MethodInfo method) => method.GetCustomAttribute<McpServerToolAttribute>()!.Name!;

    private static string? DescriptionOf(ICustomAttributeProvider member) =>
        member.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .OfType<DescriptionAttribute>()
            .FirstOrDefault()?.Description;

    /// <summary>Catalog constants except the Parameters group, which holds parameter descriptions.</summary>
    private static Dictionary<string, string> CatalogEntries() =>
        typeof(ToolDescriptions).GetNestedTypes()
            .Where(group => group.Name != nameof(ToolDescriptions.Parameters) && group.Name != nameof(ToolDescriptions.Resources))
            .SelectMany(group => group.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .ToDictionary(field => $"{field.DeclaringType!.Name}.{field.Name}", field => (string)field.GetRawConstantValue()!);

    [Fact]
    public void EveryTool_HasItsDescription_InTheCatalog()
    {
        var catalog = CatalogEntries().Values.ToHashSet(StringComparer.Ordinal);

        var offenders = ToolMethods()
            .Where(method => DescriptionOf(method) is not { Length: > 0 } description || !catalog.Contains(description))
            .Select(ToolName)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"description missing or not from ToolDescriptions: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Catalog_HasNoEntry_WithoutATool()
    {
        var used = ToolMethods().Select(DescriptionOf).ToHashSet(StringComparer.Ordinal);

        var orphans = CatalogEntries()
            .Where(entry => !used.Contains(entry.Value))
            .Select(entry => entry.Key)
            .ToList();

        Assert.True(orphans.Count == 0, $"constants without a tool: {string.Join(", ", orphans)}");
    }

    [Fact]
    public void EveryParameter_IsDescribed()
    {
        var offenders = ToolMethods()
            .SelectMany(method => method.GetParameters()
                .Where(parameter => parameter.ParameterType != typeof(CancellationToken) && parameter.ParameterType != typeof(IProgress<ProgressNotificationValue>) && parameter.ParameterType != typeof(McpServer))
                .Where(parameter => string.IsNullOrWhiteSpace(DescriptionOf(parameter)))
                .Select(parameter => $"{ToolName(method)}({parameter.Name})"))
            .ToList();

        Assert.True(offenders.Count == 0, $"parameters without a description: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void ToolNames_FollowThePrefixConvention_AndAreUnique()
    {
        var names = ToolMethods().Select(ToolName).ToList();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Matches(ToolNamePattern, name));
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Descriptions_ReferenceOnlyExistingTools()
    {
        var known = ToolMethods().Select(ToolName).ToHashSet(StringComparer.Ordinal);

        var dangling = ToolMethods()
            .SelectMany(method => ToolReference.Matches(DescriptionOf(method) ?? string.Empty)
                .Select(match => match.Value)
                .Where(reference => !known.Contains(reference))
                .Select(reference => $"{ToolName(method)} → {reference}"))
            .Distinct()
            .ToList();

        Assert.True(dangling.Count == 0, $"references to non-existent tools: {string.Join(", ", dangling)}");
    }

    [Fact]
    public void ServerGuidance_ReferencesOnlyExistingTools()
    {
        var known = ToolMethods().Select(ToolName).ToHashSet(StringComparer.Ordinal);

        var dangling = ToolReference.Matches(ServerGuidance.Instructions)
            .Select(match => match.Value)
            .Where(reference => !known.Contains(reference))
            .Distinct()
            .ToList();

        Assert.True(dangling.Count == 0, $"guidance names tools that do not exist: {string.Join(", ", dangling)}");
    }

    /// <summary>Every tool class has a family in the documentation generator, or its tools reach no page: blender_program was written, registered
    /// and tested before anyone noticed the reference had never heard of it.</summary>
    [Fact]
    public void EveryToolType_HasAFamilyInTheReference()
    {
        var families = File.ReadAllText(Path.Combine(Repository.Root, "docs", "generator", "ToolCatalog.cs"));
        var missing = typeof(ToolResponse).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .Select(type => type.Name)
            .Where(name => !families.Contains($"(\"{name}\"", StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0, $"tool types the reference does not list: {string.Join(", ", missing)}");
    }

    /// <summary>A bare JSON object hides its fields from the model; only pass-throughs that are free-form by design may stay untyped.</summary>
    [Fact]
    public void NestedParameters_AreTypedBlocks_ExceptTheFreeFormPassThroughs()
    {
        var freeForm = new HashSet<string>(StringComparer.Ordinal)
        {
            "settings", "inputs", "properties", "options", "parameters", "passes", "values", "curves", "lightGroupMembers", "objectMotionBlur",
            "shutterCurve", "slots", "aovs", "nodes", "links", "bones", "variables", "interfaceInputs", "colorRamp", "commands", "input",
        };

        var untyped = ToolMethods()
            .SelectMany(method => method.GetParameters()
                .Where(parameter => parameter.ParameterType == typeof(JsonObject) || parameter.ParameterType == typeof(JsonNode) || parameter.ParameterType == typeof(JsonArray))
                .Where(parameter => !freeForm.Contains(parameter.Name!))
                .Select(parameter => $"{ToolName(method)}({parameter.Name})"))
            .ToList();

        Assert.True(untyped.Count == 0, $"nested parameters without a typed block: {string.Join(", ", untyped)}");
    }

    [Fact]
    public void Blocks_TravelInSnakeCase_WithoutNulls()
    {
        var wire = new Grain { Strength = 0.1, Response = new GrainResponse { Highlights = 0.2 } }.ToWire().AsObject();

        Assert.Equal(0.1, wire["strength"]!.GetValue<double>());
        Assert.Equal(0.2, wire["response"]!["highlights"]!.GetValue<double>());
        Assert.False(wire.ContainsKey("size"));
        Assert.False(wire["response"]!.AsObject().ContainsKey("shadows"));
        Assert.Equal(0.5, new Defocus { MaxBlur = 0.5 }.ToWire()["max_blur"]!.GetValue<double>());
    }

    [Fact]
    public void Prompts_ReferenceOnlyExistingTools()
    {
        var known = ToolMethods().Select(ToolName).ToHashSet(StringComparer.Ordinal);
        var texts = typeof(PromptTexts).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!);

        var dangling = texts
            .SelectMany(text => ToolReference.Matches(text).Select(match => match.Value))
            .Where(reference => !known.Contains(reference))
            .Distinct()
            .ToList();

        Assert.True(dangling.Count == 0, $"prompts name tools that do not exist: {string.Join(", ", dangling)}");
    }

    [Fact]
    public void Prompts_CarryNamesAndDescriptions()
    {
        var prompts = typeof(PipelinePrompts).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerPromptAttribute>() is not null)
            .ToList();

        Assert.Equal(3, prompts.Count);
        Assert.All(prompts, prompt => Assert.Matches("^[a-z_]+$", prompt.GetCustomAttribute<McpServerPromptAttribute>()!.Name!));
        Assert.All(prompts, prompt => Assert.False(string.IsNullOrWhiteSpace(DescriptionOf(prompt))));
        Assert.All(prompts.SelectMany(prompt => prompt.GetParameters()), parameter => Assert.False(string.IsNullOrWhiteSpace(DescriptionOf(parameter))));
    }

    [Fact]
    public void PrimitiveKindParameter_NamesEveryKind()
    {
        var parameters = ParametersNamed("kind").Where(parameter => ToolName((MethodInfo)parameter.Member) == "blender_add_primitive").ToList();

        Assert.NotEmpty(parameters);
        Assert.All(parameters, parameter =>
            Assert.All(Primitives.Kinds, kind => Assert.Contains(kind, DescriptionOf(parameter)!, StringComparison.Ordinal)));
    }

    [Fact]
    public void LightTypeParameter_NamesEveryType()
    {
        var parameters = ParametersNamed("type").Where(parameter => ToolName((MethodInfo)parameter.Member) == "blender_add_light").ToList();

        Assert.NotEmpty(parameters);
        Assert.All(parameters, parameter =>
            Assert.All(Lights.Kinds, kind => Assert.Contains(kind, DescriptionOf(parameter)!, StringComparison.Ordinal)));
    }

    [Fact]
    public void TimeoutParameters_StateTheLimit()
    {
        var parameters = ParametersNamed("timeoutSeconds");

        Assert.NotEmpty(parameters);
        Assert.All(parameters, parameter =>
            Assert.Contains(ToolLimits.MaxTimeoutSeconds.ToString(), DescriptionOf(parameter)!, StringComparison.Ordinal));
    }

    /// <summary>The read-only hint must say the same as the command schema the add-on reads: a tool whose commands are all read-only there is
    /// read-only here, and no other is.</summary>
    [Fact]
    public void ReadOnlyAnnotations_AgreeWithTheCommandSchema()
    {
        var readOnly = CommandsWhere("read_only");
        var mismatched = ToolMethods()
            .Select(method => (Tool: method.GetCustomAttribute<McpServerToolAttribute>()!, Commands: CommandsOf(method)))
            .Where(pair => pair.Commands.Count > 0)
            .Where(pair => pair.Tool.ReadOnly != pair.Commands.All(readOnly.Contains))
            .Select(pair => pair.Tool.Name)
            .ToList();

        Assert.True(mismatched.Count == 0, $"read-only hint disagrees with the command schema for: {string.Join(", ", mismatched)}");
    }

    /// <summary>Every tool states its destructive hint, and the command schema says which commands destroy and which reach beyond Blender, so
    /// adding a command forces the question there rather than inheriting the protocol's default of destructive here.</summary>
    [Fact]
    public void DestructiveAnnotations_FollowTheCommandSchema()
    {
        var destructive = CommandsWhere("destructive");
        var openWorld = CommandsWhere("open_world");
        var mismatched = ToolMethods()
            .Select(method => (Tool: method.GetCustomAttribute<McpServerToolAttribute>()!, Commands: SendableBy(method)))
            .Where(pair => !pair.Tool.ReadOnly)
            .Where(pair => pair.Tool.Destructive != pair.Commands.Any(destructive.Contains) || pair.Tool.OpenWorld != pair.Commands.Any(openWorld.Contains))
            .Select(pair => pair.Tool.Name)
            .ToList();

        Assert.True(mismatched.Count == 0, $"destructive or open-world hint disagrees with the policy for: {string.Join(", ", mismatched)}");
    }

    /// <summary>The commands a tool can send: the ones its source names, and for blender_program the whole catalog, since a program picks its
    /// commands while it runs.</summary>
    private static IReadOnlyList<string> SendableBy(MethodInfo method) =>
        ToolName(method) == "blender_program"
            ? [.. BridgeCommands.All.Select(command => command.Name)]
            : CommandsOf(method);

    /// <summary>The commands the schema marks with a flag; a command read-only only for some of its actions is not one of them, since a tool
    /// carries every action of its command.</summary>
    private static HashSet<string> CommandsWhere(string flag)
    {
        var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(Contracts.AddOnContractTests.AddOnSource(), "commands.json")))!;

        return [.. schema["commands"]!.AsObject()
            .Where(command => command.Value?[flag] is JsonValue value && value.TryGetValue<bool>(out var set) && set)
            .Select(command => command.Key)];
    }

    /// <summary>The bridge commands a tool method sends, read from its source: the attribute cannot name them, and the method body is the only place they appear.</summary>
    private static IReadOnlyList<string> CommandsOf(MethodInfo method)
    {
        var source = SourceOf(method.DeclaringType!);
        var name = method.GetCustomAttribute<McpServerToolAttribute>()!.Name!;
        var start = source.IndexOf($"Name = \"{name}\"", StringComparison.Ordinal);
        var next = Regex.Match(source[(start + 1)..], @"\[McpServerTool\(");
        var body = next.Success ? source[start..(start + 1 + next.Index)] : source[start..];
        var fields = Regex.Matches(body, @"BridgeCommands\.(\w+)").Select(match => match.Groups[1].Value).Distinct();

        return [.. fields.Select(field => ((BridgeCommand)typeof(BridgeCommands).GetField(field)!.GetValue(null)!).Name)];
    }

    private static readonly Dictionary<Type, string> Sources = [];

    private static string SourceOf(Type type)
    {
        lock (Sources)
        {
            if (!Sources.TryGetValue(type, out var text))
            {
                var file = Directory.EnumerateFiles(Path.Combine(Repository.Root, "src", "Tools"), $"{type.Name}.cs", SearchOption.AllDirectories).Single();
                text = File.ReadAllText(file);
                Sources[type] = text;
            }

            return text;
        }
    }


    private static IReadOnlyList<ParameterInfo> ParametersNamed(string name) =>
        [.. ToolMethods().SelectMany(method => method.GetParameters().Where(parameter => parameter.Name == name))];

    [Fact]
    public void SkillClasses_CarryANonEmptyDescription()
    {
        var skills = typeof(ToolDescriptions).Assembly.GetTypes()
            .Select(type => type.GetCustomAttribute<SkillAttribute>())
            .OfType<SkillAttribute>()
            .ToList();

        Assert.All(skills, skill => Assert.False(string.IsNullOrWhiteSpace(skill.Description), $"skill {skill.Name} has no description"));
        Assert.All(skills, skill => Assert.Matches("^[a-z]+$", skill.Name));
    }

    [Fact]
    public void ResolutionParameters_StateTheLimit()
    {
        var parameters = ToolMethods().SelectMany(method => method.GetParameters().Where(parameter => parameter.Name is "resolutionX" or "resolutionY")).ToList();

        Assert.NotEmpty(parameters);
        Assert.All(parameters, parameter =>
            Assert.Contains(ToolLimits.MaxRenderResolution.ToString(), DescriptionOf(parameter)!, StringComparison.Ordinal));
    }

    [Fact]
    public void FormatParameters_NameEveryFormat()
    {
        var import = Assert.Single(ParametersNamed("format"), parameter => ToolName((MethodInfo)parameter.Member) == "blender_import_file");
        var export = Assert.Single(ParametersNamed("format"), parameter => ToolName((MethodInfo)parameter.Member) == "blender_export_file");

        Assert.All(FileFormats.Import, format => Assert.Contains(format, DescriptionOf(import)!, StringComparison.Ordinal));
        Assert.All(FileFormats.Export, format => Assert.Contains(format, DescriptionOf(export)!, StringComparison.Ordinal));
    }

    [Fact]
    public void PhysicsAndEmptyTypeParameters_NameEveryValue()
    {
        var physics = Assert.Single(ParametersNamed("type"), parameter => ToolName((MethodInfo)parameter.Member) == "blender_add_physics");
        var empty = Assert.Single(ParametersNamed("type"), parameter => ToolName((MethodInfo)parameter.Member) == "blender_add_empty");

        Assert.All(PhysicsKinds.All, kind => Assert.Contains(kind, DescriptionOf(physics)!, StringComparison.Ordinal));
        Assert.All(EmptyTypes.All, kind => Assert.Contains(kind, DescriptionOf(empty)!, StringComparison.Ordinal));
    }

    [Fact]
    public void ShapePresetAndMoveParameters_NameEveryValue()
    {
        var shapes = ParametersNamed("shape").Where(parameter => ToolName((MethodInfo)parameter.Member) is "blender_add_light" or "blender_set_light").ToList();
        var preset = Assert.Single(ParametersNamed("preset"), parameter => ToolName((MethodInfo)parameter.Member) == "blender_set_output");
        var move = Assert.Single(ParametersNamed("type"), parameter => ToolName((MethodInfo)parameter.Member) == "blender_camera_move");

        Assert.Equal(2, shapes.Count);
        Assert.All(shapes, parameter => Assert.All(Lights.Shapes, shape => Assert.Contains(shape, DescriptionOf(parameter)!, StringComparison.Ordinal)));
        Assert.All(Output.ResolutionPresets, name => Assert.Contains(name, DescriptionOf(preset)!, StringComparison.Ordinal));
        Assert.All(CameraMoves.All, name => Assert.Contains(name, DescriptionOf(move)!, StringComparison.Ordinal));
    }

    [Fact]
    public void OutputDepthAndCodecParameters_NameEveryValue()
    {
        var depths = ParametersNamed("colorDepth").Where(parameter => ToolName((MethodInfo)parameter.Member) is "blender_set_output" or "blender_file_output").Select(DescriptionOf).ToList();
        var codecs = ParametersNamed("exrCodec").Select(DescriptionOf).ToList();
        depths.Add(typeof(JobOutput).GetProperty(nameof(JobOutput.ColorDepth))!.GetCustomAttribute<DescriptionAttribute>()!.Description);
        codecs.Add(typeof(JobOutput).GetProperty(nameof(JobOutput.ExrCodec))!.GetCustomAttribute<DescriptionAttribute>()!.Description);

        Assert.Equal(3, depths.Count);
        Assert.All(depths, description => Assert.All(Output.ColorDepths, depth => Assert.Contains(depth, description!, StringComparison.Ordinal)));
        Assert.All(codecs, description => Assert.All(Output.ExrCodecs, codec => Assert.Contains(codec, description!, StringComparison.Ordinal)));
    }

    [Fact]
    public void GradeAndProresParameters_NameEveryValue()
    {
        var modifier = Assert.Single(ParametersNamed("type"), parameter => ToolName((MethodInfo)parameter.Member) == "blender_sequencer_grade");
        var profile = Assert.Single(ParametersNamed("proresProfile"));

        Assert.All(Sequencer.ModifierTypes, kind => Assert.Contains(kind, DescriptionOf(modifier)!, StringComparison.Ordinal));
        Assert.All(Sequencer.ProresProfiles, kind => Assert.Contains(kind, DescriptionOf(profile)!, StringComparison.Ordinal));
    }

    [Fact]
    public void SequencerTypeParameters_NameEveryValue()
    {
        var strip = Assert.Single(ParametersNamed("type"), parameter => ToolName((MethodInfo)parameter.Member) == "blender_sequencer_add_strip");
        var effect = Assert.Single(ParametersNamed("type"), parameter => ToolName((MethodInfo)parameter.Member) == "blender_sequencer_add_effect");

        Assert.All(Sequencer.StripKinds, kind => Assert.Contains(kind, DescriptionOf(strip)!, StringComparison.Ordinal));
        Assert.All(Sequencer.EffectTypes, kind => Assert.Contains(kind, DescriptionOf(effect)!, StringComparison.Ordinal));
    }
}
