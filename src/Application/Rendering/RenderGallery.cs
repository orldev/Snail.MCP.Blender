namespace Snail.MCP.Blender.Application.Rendering;

/// <summary>The last picture a tool returned, kept so a client can read it again as a resource without another render.</summary>
/// <remarks>One picture is enough: the model looks at the latest render, and a client with resource support shows it in its own
/// viewer instead of spending context on the image block a second time.</remarks>
public sealed class RenderGallery
{
    private readonly Lock _gate = new();
    private Picture? _last;

    /// <summary>Keeps the preview a reply carries, if any, and hands the reply back untouched.</summary>
    public BridgeReply Remember(BridgeReply reply)
    {
        if (reply.IsOk && reply.Result is JsonObject result && result["preview"] is JsonObject preview && preview["base64"]?.GetValue<string>() is { } data)
        {
            lock (_gate)
            {
                _last = new Picture(data, preview["mime"]?.GetValue<string>() ?? "image/jpeg", result["path"]?.GetValue<string>(), DateTimeOffset.UtcNow);
            }
        }

        return reply;
    }

    public Picture? Last
    {
        get
        {
            lock (_gate)
            {
                return _last;
            }
        }
    }

    /// <summary>A picture as it travelled: the base64 text, its media type and the file it came from.</summary>
    public sealed record Picture(string Base64, string MimeType, string? Path, DateTimeOffset At);
}
