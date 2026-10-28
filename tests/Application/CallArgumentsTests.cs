using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>The gate in front of every tool call, asked with the schemas the server really publishes.</summary>
/// <remarks>The three wrong names are the ones models sent in a month of logs; each was answered with "An error occurred invoking" and cost a
/// round trip to guess again.</remarks>
public class CallArgumentsTests
{
    private static readonly IReadOnlyDictionary<string, JsonElement> Schemas = Published();

    [Theory]
    [InlineData("find_tool", "query", "intent")]
    [InlineData("find_tool", "task", "intent")]
    [InlineData("run_program", "program", "code")]
    [InlineData("blender_download", "path", "remotePath")]
    public void ANameTheToolDoesNotTake_IsRefused_WithTheNameItDoes(string tool, string sent, string taken)
    {
        var refusal = CallArguments.Refusal(tool, Schemas[tool], [sent]);

        Assert.NotNull(refusal);
        Assert.Contains($"takes no '{sent}'", refusal, StringComparison.Ordinal);
        Assert.Contains($"'{taken}'", refusal, StringComparison.Ordinal);
    }

    /// <summary>A short form of a name the tool takes is a guess worth offering, although the edits between them are many.</summary>
    [Fact]
    public void AShortFormOfADeclaredName_IsOfferedAsTheGuess()
    {
        var refusal = CallArguments.Refusal("run_program", Schemas["run_program"], ["code", "timeout"]);

        Assert.NotNull(refusal);
        Assert.Contains("takes no 'timeout' (did you mean 'timeoutSeconds'?)", refusal, StringComparison.Ordinal);
    }

    /// <summary>A name close to nothing the tool takes offers no guess: a wrong suggestion is worse than none.</summary>
    [Fact]
    public void ANameLikeNothingTheToolTakes_IsRefused_WithoutAGuess()
    {
        var refusal = CallArguments.Refusal("find_tool", Schemas["find_tool"], ["wildfire"]);

        Assert.NotNull(refusal);
        Assert.Contains("takes no 'wildfire'", refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("did you mean", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void AParameterTheToolNeeds_IsNamedWhenTheCallLeavesItOut()
    {
        var refusal = CallArguments.Refusal("find_tool", Schemas["find_tool"], ["limit"]);

        Assert.NotNull(refusal);
        Assert.Contains("needs 'intent'", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void ACallThatFitsTheSchema_PassesThroughUntouched()
    {
        Assert.Null(CallArguments.Refusal("find_tool", Schemas["find_tool"], ["intent", "limit"]));
        Assert.Null(CallArguments.Refusal("blender_scene_info", Schemas["blender_scene_info"], []));
        Assert.Null(CallArguments.Refusal("run_program", Schemas["run_program"], ["code", "input", "timeoutSeconds"]));
    }

    /// <summary>Every tool called the way its own schema describes passes the gate; a gate that refused a correct call would be worse than none.</summary>
    [Fact]
    public void EveryTool_CalledWithEveryParameterItDeclares_IsLetThrough()
    {
        var refused = Schemas
            .Select(tool => (tool.Key, Refusal: CallArguments.Refusal(tool.Key, tool.Value, Names(tool.Value))))
            .Where(tool => tool.Refusal is not null)
            .ToList();

        Assert.True(refused.Count == 0, $"the gate turned away calls that fit the schema: {string.Join("; ", refused.Select(tool => tool.Refusal))}");
    }

    private static IEnumerable<string> Names(JsonElement schema) =>
        schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object
            ? properties.EnumerateObject().Select(property => property.Name)
            : [];

    /// <summary>The schemas as a client receives them: every tool of the server, skills loaded.</summary>
    private static IReadOnlyDictionary<string, JsonElement> Published()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new ServerConfig());
        services.AddSingleton(provider => provider.GetRequiredService<ServerConfig>().Bridge);
        services.AddBlenderBridge().AddSkills().AddMcpTransport();

        var provider = services.BuildServiceProvider();
        var tools = provider.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection!;
        var catalog = provider.GetRequiredService<Snail.MCP.Blender.Application.Skills.SkillCatalog>();

        foreach (var skill in catalog.Skills)
        {
            catalog.Enable(skill.Name);
        }

        return tools.ToDictionary(tool => tool.ProtocolTool.Name, tool => tool.ProtocolTool.InputSchema, StringComparer.Ordinal);
    }
}
