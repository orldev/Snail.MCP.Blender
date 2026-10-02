using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools;

/// <summary>Uniform tool results: the payload as text and as structured content, a failure flagged with <c>isError</c> so clients see it without reading the text.</summary>
/// <remarks>The text keeps the ok flag the model has always read; the structured copy and the error flag are what an orchestrator or an
/// evaluation harness reads, because a failure that looks like a success in the protocol is invisible to anything but the model.</remarks>
public static class ToolResponse
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The text of a successful response.</summary>
    public static string Ok(JsonNode data) => new JsonObject { ["ok"] = true, ["data"] = data }.ToJsonString(Options);

    /// <summary>The text of a failure in a shape the model can act on; <paramref name="data"/> carries per-item breakdowns of partially failed batches.</summary>
    public static string Error(string message, string? hint = null, JsonNode? data = null)
    {
        var envelope = new JsonObject
        {
            ["ok"] = false,
            ["error"] = message,
            ["hint"] = hint,
        };

        if (data is not null)
        {
            envelope["data"] = data;
        }

        return envelope.ToJsonString(Options);
    }

    /// <summary>A successful result: the payload as text and as structured content.</summary>
    public static CallToolResult Success(JsonNode data) => new()
    {
        Content = [new TextContentBlock { Text = Ok(data) }],
        StructuredContent = Element(data),
    };

    /// <summary>A failed result: flagged as an error for the protocol, explained with a hint for the model.</summary>
    public static CallToolResult Failure(string message, string? hint = null, JsonNode? data = null) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = Error(message, hint, data?.DeepClone()) }],
        StructuredContent = Element(new JsonObject { ["error"] = message, ["hint"] = hint, ["data"] = data?.DeepClone() }),
    };

    /// <summary>A bridge reply as a tool result: the result as is, or the add-on's error with the hint that fits its type.</summary>
    public static CallToolResult From(BridgeReply reply) =>
        reply.IsOk
            ? Success(reply.Result ?? new JsonObject())
            : Failure(reply.Error!.Message, Messages.HintFor(reply.Error), Decorate(reply.Error));

    /// <summary>A reply whose result may carry a preview: the JSON as text, plus the picture as image content the model can look at.</summary>
    /// <remarks>The base64 leaves the JSON so the picture travels once; width, height and the exposure statistics stay in the text.
    /// The SDK's image block carries the base64 text itself as UTF-8 bytes, not the decoded picture: decoding here put raw JPEG
    /// bytes on the wire, which the client rejected as invalid base64.</remarks>
    public static CallToolResult Pictured(BridgeReply reply)
    {
        if (!reply.IsOk || reply.Result is not JsonObject result || result["preview"] is not JsonObject preview || preview["base64"]?.GetValue<string>() is not { } data)
        {
            return From(reply);
        }

        var text = result.DeepClone().AsObject();
        text["preview"]!.AsObject().Remove("base64");

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock { Text = Ok(text) },
                new ImageContentBlock { Data = Encoding.UTF8.GetBytes(data), MimeType = preview["mime"]?.GetValue<string>() ?? "image/jpeg" },
            ],
            StructuredContent = Element(text),
        };
    }

    private static JsonElement Element(JsonNode node) => JsonSerializer.SerializeToElement(node, Options);

    private static JsonNode? Decorate(BridgeError error)
    {
        if (error.Details is null)
        {
            return new JsonObject { ["type"] = error.Type };
        }

        var details = error.Details.DeepClone();
        details["type"] = error.Type;

        return details;
    }
}
