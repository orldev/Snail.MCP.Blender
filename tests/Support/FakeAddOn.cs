using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>The add-on's socket side on a free loopback port: reads one JSON line, answers with whatever the test scripted, so <c>BlenderConnection</c> is exercised over a real TCP link.</summary>
public sealed class FakeAddOn : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Func<JsonObject, Task<string?>> _answer;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _serving;
    private readonly bool _isConcurrent;

    /// <param name="answer">The reply to each request line.</param>
    /// <param name="isConcurrent">Serve every connection at once, as the add-on does; a server over HTTP keeps its main, monitor and volume links open side by side.</param>
    public FakeAddOn(Func<JsonObject, Task<string?>> answer, bool isConcurrent = false)
    {
        _answer = answer;
        _isConcurrent = isConcurrent;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _serving = ServeAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public List<JsonObject> Received { get; } = [];

    /// <summary>The requests exactly as they arrived, before anything parsed them: what the wire carried, escaping and all.</summary>
    public List<string> Lines { get; } = [];

    public int Connections { get; private set; }

    /// <summary>Echoes a success reply carrying the request's command and params under result.</summary>
    public static FakeAddOn Echo() => new(request => Task.FromResult<string?>(
        new JsonObject
        {
            ["id"] = request["id"]?.DeepClone(),
            ["ok"] = true,
            ["result"] = new JsonObject { ["command"] = request["command"]?.DeepClone(), ["params"] = request["params"]?.DeepClone() },
        }.ToJsonString()));

    public static string ErrorReply(JsonObject request, string type, string message) =>
        new JsonObject
        {
            ["id"] = request["id"]?.DeepClone(),
            ["ok"] = false,
            ["error"] = new JsonObject { ["type"] = type, ["message"] = message, ["details"] = new JsonObject { ["known"] = new JsonArray("ping") } },
        }.ToJsonString();

    private async Task ServeAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
                Connections++;

                if (_isConcurrent)
                {
                    _ = Task.Run(async () =>
                    {
                        using (client)
                        {
                            await TalkAsync(client);
                        }
                    });

                    continue;
                }

                using (client)
                {
                    await TalkAsync(client);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TalkAsync(TcpClient client)
    {
        var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        try
        {
            while (await reader.ReadLineAsync(_stopping.Token) is { } line)
            {
                var request = (JsonObject)JsonNode.Parse(line)!;

                lock (Received)
                {
                    Received.Add(request);
                    Lines.Add(line);
                }

                var reply = await _answer(request);

                if (reply is not null)
                {
                    await writer.WriteLineAsync(reply.AsMemory(), _stopping.Token);
                }
            }
        }
        catch (Exception failure) when (failure is IOException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();

        try
        {
            await _serving;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
