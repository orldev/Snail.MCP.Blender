using System.Diagnostics;

namespace Snail.MCP.Blender.Adapters.Ssh;

/// <summary>One running ssh: what the tunnel needs to watch it end and to end it.</summary>
public interface ISshProcess : IDisposable
{
    int Id { get; }

    /// <summary>The last thing ssh said, which is why it ended when it did.</summary>
    string? LastLine { get; }

    Task<int> WaitForExitAsync(CancellationToken cancellationToken);

    void Kill();
}

/// <summary>Starts ssh; a seam so the tunnel's supervision is tested without a network.</summary>
public interface ISshProcessLauncher
{
    ISshProcess Start(IReadOnlyList<string> arguments);
}

/// <summary>Starts the system's ssh with none of this server's standard streams: stdin and stdout carry JSON-RPC here, and ssh reading or writing them would corrupt it.</summary>
public sealed class SshProcessLauncher(ILogger<SshProcessLauncher> logger) : ISshProcessLauncher
{
    public ISshProcess Start(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("ssh")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        var process = Process.Start(start) ?? throw new InvalidOperationException("ssh did not start");
        process.StandardInput.Close();

        return new RunningSsh(process, logger);
    }

    private sealed class RunningSsh : ISshProcess
    {
        private readonly Process _process;
        private readonly ILogger _logger;
        private string? _lastLine;

        public RunningSsh(Process process, ILogger logger)
        {
            _process = process;
            _logger = logger;
            process.ErrorDataReceived += (_, line) => Note(line.Data);
            process.OutputDataReceived += (_, line) => Note(line.Data);
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }

        public int Id => _process.Id;

        public string? LastLine => Volatile.Read(ref _lastLine);

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return _process.ExitCode;
        }

        public void Kill()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }

        public void Dispose() => _process.Dispose();

        /// <summary>Every line goes to the log; the banner lines OpenSSH marks with ** are warnings about the connection, not the reason it ended.</summary>
        private void Note(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (line.StartsWith("**", StringComparison.Ordinal))
            {
                _logger.LogDebug("ssh: {Line}", line);

                return;
            }

            _logger.LogInformation("ssh: {Line}", line);
            Volatile.Write(ref _lastLine, line.Trim());
        }
    }
}
