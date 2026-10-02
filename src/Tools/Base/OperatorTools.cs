using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>The generic layer: any bpy operator, its documentation, and Python itself, as far as the configured access allows.</summary>
[McpServerToolType]
public sealed class OperatorTools(IBlenderBridge bridge, ServerConfig config, ScriptAdvice advice) : BridgeToolBase(bridge)
{

    [McpServerTool(Name = "blender_run_operator", Destructive = true, OpenWorld = true)]
    [Description(ToolDescriptions.Operators.RunOperator)]
    public Task<CallToolResult> RunOperatorAsync(
        [Description(ToolDescriptions.Parameters.OperatorName)] string name,
        [Description("Operator properties as a JSON object, e.g. {\"size\": 2, \"location\": [0, 0, 1]}. Unknown names are rejected with the list of known ones.")]
        JsonObject? parameters = null,
        [Description("Editor type to run inside, e.g. VIEW_3D for mesh editing operators that need a 3D viewport; empty for operators that work anywhere.")]
        string? area = null,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 30,
        CancellationToken cancellationToken = default) =>
        config.Python != PythonAccess.Off || !ScriptAccess.RunsScripts(name, parameters)
            ? WithinAsync(timeoutSeconds, timeout => SendAsync(BridgeCommands.RunOperator,
                new JsonObject().With("name", name).With("params", parameters).With("area", area), cancellationToken, timeout))
            : Task.FromResult(ToolResponse.Failure(Messages.ScriptOperatorDisabled, Messages.PythonDisabledHint));

    [McpServerTool(Name = "blender_describe_operator", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Operators.DescribeOperator)]
    public Task<CallToolResult> DescribeOperatorAsync(
        [Description(ToolDescriptions.Parameters.OperatorName)] string name,
        CancellationToken cancellationToken = default) =>
        SendAsync(BridgeCommands.DescribeOperator, new JsonObject().With("name", name), cancellationToken);

    [McpServerTool(Name = "blender_python", Destructive = true, OpenWorld = true)]
    [Description(ToolDescriptions.Operators.Python)]
    public async Task<CallToolResult> PythonAsync(
        [Description("Python source. bpy, bmesh, mathutils and math are imported; assign to a variable named result to return a value.")]
        string code,
        [Description(ToolDescriptions.Parameters.TimeoutSeconds)] int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        if (config.Python == PythonAccess.Off)
        {
            return ToolResponse.Failure(Messages.PythonDisabled, Messages.PythonDisabledHint);
        }

        var covered = advice.Read(code);

        if (config.Python == PythonAccess.Fallback && covered.Count >= ScriptAccess.CoveredLimit)
        {
            return ToolResponse.Failure(Messages.PythonCovered(covered.Count), Messages.PythonCoveredHint, Advice(covered));
        }

        return await WithinAsync(timeoutSeconds, async timeout =>
                Advised(await ExchangeAsync(BridgeCommands.Python, new JsonObject().With("code", code), timeout, cancellationToken).ConfigureAwait(false), covered))
            .ConfigureAwait(false);
    }

    /// <summary>Why a list of steps may not run under the Python setting, or null when it may: off refuses any script, fallback a script whose
    /// work the tools already do.</summary>
    /// <remarks>A python step is the same script a blender_python call would carry, so fallback reads it the same way; otherwise a sequence or a
    /// batch would be the way around the setting.</remarks>
    /// <summary>The first step this client's key does not open, as a refusal; null when it opens them all.</summary>
    /// <remarks>A tool that carries commands is one tool with one annotation, and what it does is whatever is inside it — so the key is asked
    /// about the steps, not about the tool. Checked before the Python setting, because "your key does not open this" is the plainer answer of
    /// the two and the one that does not depend on what the server was configured to allow.</remarks>
    public static CallToolResult? RefusalFor(JsonArray commands, ToolScopes scopes, ClientSession client) =>
        scopes.Refused(commands, client) is { } denied
            ? ToolResponse.Failure(ToolScopes.Refusal(denied.Command, denied.Scope, client.Name), Messages.ScopeHint)
            : null;

    public static CallToolResult? RefusalFor(JsonArray commands, PythonAccess access, ScriptAdvice advice, string offMessage)
    {
        if (access == PythonAccess.Off)
        {
            return ScriptAccess.ContainsPython(commands) ? ToolResponse.Failure(offMessage, Messages.PythonDisabledHint) : null;
        }

        if (access != PythonAccess.Fallback)
        {
            return null;
        }

        var scripts = commands.Select((step, index) => (Step: index + 1, Covered: CoveredBy(step, advice)));

        return scripts.FirstOrDefault(script => script.Covered.Count >= ScriptAccess.CoveredLimit) is { Covered.Count: > 0 } refused
            ? ToolResponse.Failure(Messages.StepPythonCovered(refused.Step, refused.Covered.Count), Messages.PythonCoveredHint, Advice(refused.Covered))
            : null;
    }

    private static IReadOnlyList<ScriptFinding> CoveredBy(JsonNode? step, ScriptAdvice advice) =>
        step is JsonObject command && command["command"]?.GetValue<string>() == BridgeCommands.Python.Name
            ? advice.Read(command["params"]?["code"]?.GetValue<string>())
            : [];

    /// <summary>The reply with what the script did that a tool already does; a script that used no covered API is returned untouched.</summary>
    private static CallToolResult Advised(BridgeReply reply, IReadOnlyList<ScriptFinding> covered)
    {
        if (!reply.IsOk || covered.Count == 0 || reply.Result is not JsonObject result)
        {
            return ToolResponse.From(reply);
        }

        var advised = result.DeepClone().AsObject();
        advised["advice"] = Advice(covered);

        return ToolResponse.Success(advised);
    }

    private static JsonNode Advice(IReadOnlyList<ScriptFinding> covered) => new JsonObject
    {
        ["note"] = Messages.PythonAdviceNote,
        ["tools"] = new JsonArray([.. covered.Select(finding => (JsonNode)new JsonObject
        {
            ["operation"] = finding.Operation,
            ["tool"] = finding.Tool,
            ["skill"] = finding.Skill,
        })]),
    };
}
