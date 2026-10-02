using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Application.Transfer;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Application.Diagnostics;

/// <summary>One report of the server, its configuration, whether the add-on inside Blender answers, and the traffic over the link so far.</summary>
/// <remarks>The ping goes over the monitor link, which the add-on answers from its socket thread: on the control link the report would wait
/// for a running render to finish, and a busy Blender would look like a dead one. The agent named is the asking client's, which over HTTP is
/// not the server's: it is the name the add-on's leases and journal will show this client under.</remarks>
public sealed class ServerHealth(ServerConfig config, IBlenderMonitor monitor, BridgeTraffic traffic, AddOnFreshness freshness, ITunnel tunnel, LocalProject project, ClientSessions? clients = null)
{
    /// <summary>Seconds without a tick of Blender's queue, while nothing runs, after which the dispatcher is called stalled rather than idle.</summary>
    public const int StalledAfterSeconds = 60;

    public async Task<JsonObject> ReportAsync(CancellationToken cancellationToken)
    {
        var ping = await monitor.PingAsync(new JsonObject { ["commands"] = true }, cancellationToken).ConfigureAwait(false);

        var report = new JsonObject
        {
            ["server"] = new JsonObject
            {
                ["name"] = ThisAssembly.Name,
                ["version"] = ThisAssembly.InformationalVersion,
            },
            ["blender"] = Describe(ping),
            ["bridge"] = new JsonObject
            {
                ["host"] = config.Bridge.Host,
                ["port"] = config.Bridge.Port,
                ["connectTimeoutSeconds"] = config.Bridge.ConnectTimeoutSeconds,
                ["requestTimeoutSeconds"] = config.Bridge.RequestTimeoutSeconds,
                ["agent"] = clients?.Current.Agent ?? config.Bridge.Agent,
                ["token"] = TokenSource(config.Bridge),
            },
            ["transport"] = Transport(),
            ["tunnel"] = Tunnel(),
            ["project"] = Project(ping),
            ["traffic"] = traffic.Report(),
            ["addOn"] = freshness.Describe(ping),
            ["dataDirectory"] = config.DataDirectory,
        };

        if (config.Transport is Configuration.Transport.Stdio)
        {
            report["idleTimeoutMinutes"] = config.IdleTimeoutMinutes;
        }

        return report;
    }

    /// <summary>Whether this server answers and whether the Blender behind it does: the one question a container health check and an uptime monitor ask.</summary>
    public async Task<JsonObject> LivenessAsync(CancellationToken cancellationToken)
    {
        var ping = await monitor.PingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return new JsonObject { ["status"] = "ok", ["blender"] = Alive(ping) };
    }

    /// <summary>Whether Blender is answering, and whether the thread that runs the commands is still going.</summary>
    /// <remarks>Ping is answered from the add-on's socket thread, so a Blender whose dispatcher has died answers it exactly like a healthy
    /// one: up, quiet, and never running anything again. The pulse tells them apart — but only while nothing is running, because a long
    /// render holds the main thread on purpose and the queue is not drained until it ends.</remarks>
    private static string Alive(BridgeReply ping)
    {
        if (!ping.IsOk || ping.Result is not JsonObject answer)
        {
            return "down";
        }

        var quiet = answer["executing"]?.GetValueKind() is null or System.Text.Json.JsonValueKind.Null;

        return quiet && answer["pumped_s_ago"]?.GetValue<double>() > StalledAfterSeconds ? "stalled" : "up";
    }

    /// <summary>The project folder and its twin on Blender's machine, which the add-on's ping names; a note when the server runs where no project is.</summary>
    private JsonObject Project(BridgeReply ping)
    {
        var state = project.Describe((ping.Result as JsonObject)?["machine"]?["files"]?.GetValue<string>());

        if (project.Directory is null)
        {
            state["note"] = config.Transport is Configuration.Transport.Http ? Messages.HttpProjectNote : Messages.NoProjectNote;
        }

        return state;
    }

    /// <summary>How clients reach this server; over HTTP also the address links are built on and how long they last.</summary>
    private JsonObject Transport() =>
        config.Transport is Configuration.Transport.Http
            ? new JsonObject { ["kind"] = "http", ["publicUrl"] = config.Http.PublicUrl ?? config.Http.Url, ["linkMinutes"] = config.Http.LinkMinutes }
            : new JsonObject { ["kind"] = "stdio" };

    /// <summary>The tunnel to a Blender on another machine, with a hint when it is down; absent when Blender runs here.</summary>
    private JsonObject? Tunnel()
    {
        if (tunnel.Describe() is not { } state)
        {
            return null;
        }

        if (state["up"]?.GetValue<bool>() != true)
        {
            state["hint"] = Messages.TunnelDownHint;
        }

        return state;
    }

    /// <summary>configured when the server carries its own token, file when it reads the one the add-on generated, none otherwise.</summary>
    private static string TokenSource(BlenderLinkOptions link) =>
        !string.IsNullOrWhiteSpace(link.Token) ? "configured" : File.Exists(link.TokenFile) ? "file" : "none";

    private static JsonObject Describe(BridgeReply ping)
    {
        if (ping.IsOk)
        {
            var answer = ping.Result as JsonObject ?? [];
            answer["reachable"] = true;

            if (answer["busy"]?.GetValue<bool>() == true && answer["executing"]?.GetValue<string>() is { } running)
            {
                answer["note"] = Messages.BusyWith(running);
            }

            return answer;
        }

        return new JsonObject
        {
            ["reachable"] = false,
            ["error"] = ping.Error!.Message,
            ["hint"] = Messages.HintFor(ping.Error),
        };
    }
}
