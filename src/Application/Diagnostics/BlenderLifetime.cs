using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Application.Diagnostics;

/// <summary>Which run of Blender this server is talking to, and what became of the run before it.</summary>
/// <remarks>A Blender that dies and comes back answers like the one that died: the link is remade on the next command, the add-on reports
/// the same version and digest, and the scene is quietly back at its last save with everything since it gone. The process is what differs,
/// so the pid its ping carries is compared with the pid of the ping before — the first ping after a restart is the only moment at which the
/// loss can be named. This outlives the link on purpose: the connection itself is replaced by the reconnect and remembers nothing across it.</remarks>
public sealed class BlenderLifetime(TimeProvider time, ILogger<BlenderLifetime> logger)
{
    private int? _pid;
    private JsonObject? _restart;

    /// <summary>Reads the process out of a ping: a pid that differs from the one before it is a Blender that restarted, and is reported until this server stops.</summary>
    public void Seen(BridgeReply ping)
    {
        if ((ping.Result as JsonObject)?["machine"] is not JsonObject machine || machine["pid"]?.GetValue<int>() is not { } pid)
        {
            return;
        }

        if (_pid is { } previous && previous != pid)
        {
            var crash = machine["crash_report"] as JsonObject;

            _restart = new JsonObject
            {
                ["at"] = time.GetUtcNow().ToString("O"),
                ["previousPid"] = previous,
                ["pid"] = pid,
                ["crashReport"] = crash?.DeepClone(),
                ["note"] = Messages.BlenderRestartedNote,
            };

            Log.BlenderRestarted(logger, previous, pid, crash?["path"]?.GetValue<string>() ?? "none");
        }

        _pid = pid;
    }

    /// <summary>The restart this server saw, for the report; null while the Blender it first spoke to is still the one answering.</summary>
    public JsonObject? Describe() => _restart?.DeepClone() as JsonObject;
}
