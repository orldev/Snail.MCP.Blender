namespace Snail.MCP.Blender.Domain;

/// <summary>Why a command did not produce a result: the add-on's own error, or a failure of the link itself.</summary>
public sealed record BridgeError(string Type, string Message, JsonNode? Details = null)
{
    public const string UnavailableType = "Unavailable";

    public const string TimeoutType = "Timeout";

    public const string DroppedType = "Dropped";

    public const string MalformedType = "Malformed";

    public const string BusyType = "Busy";

    public const string TokenUnreadableType = "TokenUnreadable";

    public static BridgeError Unavailable(string host, int port, string reason) =>
        new(UnavailableType, $"The Blender add-on is not answering on {host}:{port} ({reason}).");

    /// <summary>The command is carried as data, not only inside the sentence: the hint that follows a timeout depends on whether Blender is still rendering.</summary>
    public static BridgeError Timeout(BridgeCommand command, TimeSpan waited) =>
        new(TimeoutType, $"Blender did not answer '{command.Name}' within {waited.TotalSeconds:0.#} s; the link was reset.", Of(command));

    /// <summary>The turn on the link did not come within the call's own timeout; the command was never sent, so nothing of it ran.</summary>
    public static BridgeError Queued(BridgeCommand command, TimeSpan waited) =>
        new(BusyType, $"The link to Blender stayed busy with another request for {waited.TotalSeconds:0.#} s; '{command.Name}' was never sent.", Of(command));

    public static BridgeError TokenUnreadable(string file, string reason) =>
        new(TokenUnreadableType, $"The add-on's token file {file} cannot be read ({reason}).");

    /// <summary>JSON has no NaN or infinity, so a request carrying one cannot be written at all.</summary>
    public static BridgeError NotFinite(BridgeCommand command) =>
        new("BadRequest", $"A number sent with '{command.Name}' is not finite (NaN or infinity), which JSON cannot carry.", Of(command));

    public static BridgeError Dropped(BridgeCommand command) =>
        new(DroppedType, $"Blender closed the link while '{command.Name}' was running.", Of(command));

    private static JsonObject Of(BridgeCommand command) => new() { ["command"] = command.Name };

    public static BridgeError Malformed(string line) =>
        new(MalformedType, $"The add-on answered with something that is not a reply: {Truncate(line)}");

    private static string Truncate(string text) => text.Length <= 200 ? text : $"{text[..200]}…";
}
