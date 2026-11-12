using ModelContextProtocol.Protocol;
using Snail.MCP.Blender.Tools.Prompts;
using Snail.Sdk.Mcp.Hosting.Jobs;

namespace Snail.MCP.Blender.Tools;

/// <summary>What the add-on answered, as the envelope every Snail server answers in: <see cref="ToolAnswer"/> over a bridge reply.</summary>
/// <remarks>The add-on's error type becomes the envelope's <c>type</c> as the add-on spells it (<c>Unavailable</c>, <c>Leased</c>), and its
/// details become the failure's <c>data</c>; the hint is the one that fits the type.</remarks>
public static class BridgeAnswers
{
    /// <summary>A bridge reply as a tool result: the result as is, or the add-on's error with the hint that fits its type.</summary>
    public static CallToolResult From(BridgeReply reply) =>
        reply.IsOk
            ? ToolAnswer.Ok(reply.Result ?? new JsonObject())
            : Failed(reply.Error!);

    /// <summary>A bridge reply as the answer a job ends with: the result, or the add-on's error with its hint and type.</summary>
    public static Answer<JsonObject> Answered(BridgeReply reply) =>
        reply.IsOk
            ? Answer.Ok(reply.Result as JsonObject ?? [])
            : Answer.Failed(reply.Error!.Message, Messages.HintFor(reply.Error), reply.Error.Type, reply.Error.Details?.DeepClone());

    /// <summary>The add-on's error as a failed answer.</summary>
    public static CallToolResult Failed(BridgeError error) =>
        ToolAnswer.Failed(error.Message, Messages.HintFor(error), error.Type, error.Details?.DeepClone());

    /// <summary>A reply whose result may carry a preview: the JSON as text, plus the picture as image content the model can look at.</summary>
    /// <remarks>The base64 leaves the JSON so the picture travels once; width, height and the exposure statistics stay in the text.</remarks>
    public static CallToolResult Pictured(BridgeReply reply)
    {
        if (!reply.IsOk || reply.Result is not JsonObject result || result["preview"] is not JsonObject preview || preview["base64"]?.GetValue<string>() is not { } data)
        {
            return From(reply);
        }

        var text = result.DeepClone().AsObject();
        text["preview"]!.AsObject().Remove("base64");

        return ToolAnswer.Ok(text).WithImage(Convert.FromBase64String(data), preview["mime"]?.GetValue<string>() ?? "image/jpeg");
    }

    /// <summary>A job's report as a tool result, with a note beside it when the tool has one.</summary>
    public static CallToolResult Job(JobReport report, string? note = null) =>
        JobAnswer.Envelope(report, note is null ? null : new JsonObject { ["note"] = note });
}
