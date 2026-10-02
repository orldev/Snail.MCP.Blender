using Microsoft.Extensions.Hosting;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Adapters.Ssh;

/// <summary>Keeps an SSH tunnel from this machine's link port to the add-on's port on Blender's machine for as long as the server runs.</summary>
/// <remarks>The server is started by the client and exits with it, so a tunnel owned by the server is up exactly while someone works with Blender
/// and closed otherwise. ssh runs with BatchMode, so it never waits for a password or a host key on a terminal nobody sees, and with
/// ExitOnForwardFailure, so a port it cannot forward ends it instead of leaving a tunnel that carries nothing. A drop is reopened after a pause
/// that doubles up to half a minute and starts over once a tunnel has held for a minute.</remarks>
public sealed class SshTunnel(BlenderLinkOptions link, ISshProcessLauncher launcher, TimeProvider timeProvider, ILogger<SshTunnel> logger)
    : BackgroundService, ITunnel
{
    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan LongestRetry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Settled = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private ISshProcess? _current;
    private DateTimeOffset? _upSince;
    private string? _lastError;
    private int _starts;

    /// <summary>The ssh command line: the forward, the key when one is set, and the options that keep it from ever asking for anything.</summary>
    public static IReadOnlyList<string> Arguments(BlenderLinkOptions link)
    {
        List<string> arguments =
        [
            "-N", "-n", "-T",
            "-o", "BatchMode=yes",
            "-o", "ExitOnForwardFailure=yes",
            "-o", "ServerAliveInterval=15",
            "-o", "ServerAliveCountMax=3",
            "-L", $"127.0.0.1:{link.Port}:127.0.0.1:{link.Tunnel.RemotePort}",
        ];

        if (!string.IsNullOrWhiteSpace(link.Tunnel.Key))
        {
            arguments.AddRange(["-i", link.Tunnel.Key, "-o", "IdentitiesOnly=yes"]);
        }

        arguments.Add(link.Tunnel.Host!);

        return arguments;
    }

    public JsonObject? Describe()
    {
        if (!link.Tunnel.IsEnabled)
        {
            return null;
        }

        lock (_gate)
        {
            return new JsonObject
            {
                ["host"] = link.Tunnel.Host,
                ["forward"] = $"127.0.0.1:{link.Port} -> {link.Tunnel.RemotePort}",
                ["up"] = _current is not null,
                ["pid"] = _current?.Id,
                ["upSince"] = _upSince?.ToString("O"),
                ["starts"] = _starts,
                ["lastError"] = _lastError,
            };
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!link.Tunnel.IsEnabled)
        {
            return;
        }

        var retry = FirstRetry;

        while (!stoppingToken.IsCancellationRequested)
        {
            var started = timeProvider.GetUtcNow();
            var reason = await RunOnceAsync(stoppingToken).ConfigureAwait(false);

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            retry = timeProvider.GetUtcNow() - started >= Settled ? FirstRetry : retry;
            logger.LogWarning("The SSH tunnel to {Host} ended ({Reason}); reopening in {Retry}", link.Tunnel.Host, reason, retry);

            try
            {
                await Task.Delay(retry, timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            retry = TimeSpan.FromTicks(Math.Min(retry.Ticks * 2, LongestRetry.Ticks));
        }
    }

    /// <summary>Runs one ssh until it ends or the server stops, and says why it ended.</summary>
    private async Task<string> RunOnceAsync(CancellationToken stoppingToken)
    {
        ISshProcess process;

        try
        {
            process = launcher.Start(Arguments(link));
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return Ended(null, $"ssh could not be started: {error.Message}");
        }

        lock (_gate)
        {
            _current = process;
            _upSince = timeProvider.GetUtcNow();
            _starts++;
        }

        using (process)
        {
            try
            {
                var code = await process.WaitForExitAsync(stoppingToken).ConfigureAwait(false);

                return Ended(process, process.LastLine ?? $"ssh exited with code {code}");
            }
            catch (OperationCanceledException)
            {
                process.Kill();

                return Ended(process, null);
            }
        }
    }

    private string Ended(ISshProcess? process, string? reason)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_current, process) || process is null)
            {
                _current = null;
                _upSince = null;
            }

            _lastError = reason ?? _lastError;
        }

        return reason ?? "stopped";
    }
}
