using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.MCP.Blender.Tools;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tests.Tools;

/// <summary>Every tool, called with every argument it takes, against an add-on that answers anything.</summary>
/// <remarks>The static guard reads the whole of src for wire keys and asks whether some command declares them; it cannot ask whether the
/// command this tool sends declares them, because the key and the command only meet when the tool runs. A key copied from a neighbouring
/// tool therefore passes the static reading and is refused by the add-on's parameter gate at the client's expense. These calls put the two
/// together: the command that left, and the keys it left with.</remarks>
public class ToolInvocationTests
{
    private const string Agent = "agent";

    /// <summary>Tools that answer from the server and never reach Blender; a tool that joins them silently is a tool that stopped working.</summary>
    private static readonly string[] AnsweredWithoutBlender =
    [
        "blender_diagnose",
        "blender_disable_skill",
        "blender_enable_skill",
        "blender_install_addon",
        "blender_skills",
        "blender_upload",
        "find_tool",
        "run_program",
    ];

    private static readonly Dictionary<string, Dictionary<string, object?>> Arguments = new(StringComparer.Ordinal)
    {
        ["blender_enable_skill"] = new(StringComparer.Ordinal) { ["name"] = "modeling" },
        ["blender_disable_skill"] = new(StringComparer.Ordinal) { ["name"] = "modeling" },
        ["blender_find_tool"] = new(StringComparer.Ordinal) { ["intent"] = "render the scene" },
        ["blender_run_tools"] = new(StringComparer.Ordinal) { ["steps"] = new JsonArray(new JsonObject { ["command"] = "scene_info" }) },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_CalledWithEveryArgument_SendsNoKeyItsCommandDoesNotDeclare(string tool)
    {
        var bridge = new ScriptedBridge().AnswerAnything(new JsonObject { ["ok"] = true });

        await CallAsync(tool, bridge);

        var strays = bridge.Sent
            .SelectMany(sent => Keys(sent.Parameters).Where(key => !Declared(sent.Command.Name).Contains(key))
                .Select(key => $"{sent.Command.Name}: {key}"))
            .ToList();

        Assert.True(strays.Count == 0, $"{tool} sends keys the add-on refuses, because the command does not declare them: {string.Join(", ", strays)}");
    }

    /// <summary>A tool that answers without asking Blender is a tool whose answer is the server's own; the list says which those are.</summary>
    [Fact]
    public async Task ToolsThatNeverReachBlender_AreTheOnesThatAnswerForThemselves()
    {
        var silent = new List<string>();

        foreach (var tool in Catalog().Keys)
        {
            var bridge = new ScriptedBridge().AnswerAnything(new JsonObject { ["ok"] = true });

            await CallAsync(tool, bridge);

            if (bridge.Sent.Count == 0)
            {
                silent.Add(tool);
            }
        }

        var answering = string.Join(", ", silent.Order(StringComparer.Ordinal));

        Assert.True(answering == string.Join(", ", AnsweredWithoutBlender), $"tools that answered without reaching Blender: {answering}");
    }

    public static TheoryData<string> Tools()
    {
        var tools = new TheoryData<string>();

        foreach (var tool in Catalog().Keys.Order(StringComparer.Ordinal))
        {
            tools.Add(tool);
        }

        return tools;
    }

    private static async Task CallAsync(string tool, ScriptedBridge bridge)
    {
        var method = Catalog()[tool];
        var provider = Provider(bridge);
        var instance = ActivatorUtilities.CreateInstance(provider, method.DeclaringType!);
        var arguments = method.GetParameters().Select(parameter => Sample(tool, parameter)).ToArray();

        var answer = method.Invoke(instance, arguments);

        if (answer is Task task)
        {
            await task;
        }
    }

    private static ServiceProvider Provider(ScriptedBridge bridge)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(new ServerConfig { DataDirectory = TestFolder.Create() });
        services.AddSingleton(provider => provider.GetRequiredService<ServerConfig>().Bridge);
        services.AddBlenderBridge().AddSkills().AddMcpTransport();
        services.AddSingleton<IBlenderBridge>(bridge);

        return services.BuildServiceProvider();
    }

    private static Dictionary<string, MethodInfo> Catalog() =>
        typeof(ToolDescriptions).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .ToDictionary(method => method.GetCustomAttribute<McpServerToolAttribute>()!.Name!, method => method, StringComparer.Ordinal);

    private static IEnumerable<string> Keys(JsonObject? parameters) =>
        parameters is null ? [] : parameters.Select(pair => pair.Key).Where(key => key != Agent);

    private static HashSet<string> Declared(string command)
    {
        var commands = JsonNode.Parse(File.ReadAllText(Path.Combine(Repository.Root, "addon", "commands.json")))!["commands"]!.AsObject();

        return commands[command]?["params"] is JsonArray declared
            ? [.. declared.Select(name => name!.GetValue<string>())]
            : [];
    }

    private static object? Sample(string tool, ParameterInfo parameter)
    {
        if (Arguments.TryGetValue(tool, out var overrides) && overrides.TryGetValue(parameter.Name!, out var value))
        {
            return value;
        }

        return Sample(parameter.ParameterType, parameter.Name!);
    }

    private static object? Sample(Type declared, string name)
    {
        var type = Nullable.GetUnderlyingType(declared) ?? declared;

        if (type == typeof(CancellationToken))
        {
            return CancellationToken.None;
        }

        if (type == typeof(McpServer) || type == typeof(IProgress<ProgressNotificationValue>))
        {
            return null;
        }

        if (type == typeof(string))
        {
            return Text(name);
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(int))
        {
            return Whole(name);
        }

        if (type == typeof(double))
        {
            return 1.0;
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type == typeof(JsonObject))
        {
            return new JsonObject { ["key"] = "value" };
        }

        if (type == typeof(JsonArray) || type == typeof(JsonNode))
        {
            return new JsonArray(1, 2);
        }

        if (type.IsArray)
        {
            return Items(type.GetElementType()!, name);
        }

        if (typeof(Block).IsAssignableFrom(type))
        {
            return Filled(type);
        }

        throw new NotSupportedException($"no sample for {type} ({name})");
    }

    private static Array Items(Type element, string name)
    {
        var items = Array.CreateInstance(element, 3);

        for (var index = 0; index < items.Length; index++)
        {
            items.SetValue(Sample(element, name), index);
        }

        return items;
    }

    private static object Filled(Type block)
    {
        var instance = Activator.CreateInstance(block)!;

        foreach (var property in block.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite))
        {
            property.SetValue(instance, Sample(property.PropertyType, property.Name));
        }

        return instance;
    }

    private static string Text(string name) =>
        name.Contains("path", StringComparison.OrdinalIgnoreCase) ? "scene.blend" : "Cube";

    private static int Whole(string name) =>
        name.Contains("timeout", StringComparison.OrdinalIgnoreCase) ? 10
            : name.Contains("resolution", StringComparison.OrdinalIgnoreCase) ? 640
            : 1;
}
