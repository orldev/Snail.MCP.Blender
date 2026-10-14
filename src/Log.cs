namespace Snail.MCP.Blender;

/// <summary>What the server says about the add-on, the SSH tunnel, the commands it sends and the skills it unloads.</summary>
/// <remarks>
/// Generated rather than written through the logging extensions: a debug line costs nothing while Debug is off, where
/// the extensions boxed and formatted their arguments first. The messages are the ones the server has always written.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "ssh: {Line}")]
    internal static partial void SshDebug(ILogger logger, string line);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "ssh: {Line}")]
    internal static partial void SshSaid(ILogger logger, string line);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "The SSH tunnel to {Host} ended ({Reason}); reopening in {Retry}")]
    internal static partial void TunnelEnded(ILogger logger, string? host, string reason, TimeSpan retry);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Skipping a stale reply {ReplyId} while waiting for {Id}")]
    internal static partial void StaleReplySkipped(ILogger logger, JsonNode? replyId, string id);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Connected to the Blender add-on on {Host}:{Port} with {TokenSource} token")]
    internal static partial void AddOnConnected(ILogger logger, string host, int port, string tokenSource);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "No MCP tool has been called for {IdleFor}; the process looks orphaned, shutting down")]
    internal static partial void ShuttingDownOrphaned(ILogger logger, TimeSpan idleFor);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "{Command} was cancelled after {ElapsedMs} ms; Blender is still running it — a command already on the main thread cannot be interrupted, and a render finishes whatever the caller does")]
    internal static partial void CommandCancelled(ILogger logger, string command, double elapsedMs);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "{Command} answered in {ElapsedMs} ms")]
    internal static partial void CommandAnswered(ILogger logger, string command, double elapsedMs);

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "{Command} is unknown to the add-on inside Blender, which is therefore older than this server; run blender_install_addon and reinstall the zip")]
    internal static partial void CommandUnknownToAddOn(ILogger logger, string command);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "{Command} failed after {ElapsedMs} ms: {ErrorType}: {Message}")]
    internal static partial void CommandFailed(ILogger logger, string command, double elapsedMs, string errorType, string message);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning, Message = "The add-on installed in Blender ({Installed}) is not the one this server ships ({Bundled}); run blender_install_addon and reinstall the zip. Commands it does not have: {Missing}")]
    internal static partial void AddOnStale(ILogger logger, string installed, string bundled, string missing);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Unloaded idle skills: {Skills}")]
    internal static partial void SkillsUnloaded(ILogger logger, string skills);

    [LoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "Blender answers from a different process than before ({Previous} → {Pid}): it restarted, its scene came back from the file on disk, and anything unsaved is gone. Crash report: {CrashReport}")]
    internal static partial void BlenderRestarted(ILogger logger, int previous, int pid, string crashReport);
}
