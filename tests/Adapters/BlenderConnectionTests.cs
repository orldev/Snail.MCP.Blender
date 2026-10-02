using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Net.Sockets;
using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Adapters;

[Collection("Loopback")]
public class BlenderConnectionTests
{
    private static BlenderConnection Connect(int port, int requestTimeoutSeconds = 5) =>
        new(new BlenderLinkOptions { Port = port, ConnectTimeoutSeconds = 2, RequestTimeoutSeconds = requestTimeoutSeconds },
            NullLogger<BlenderConnection>.Instance);

    [Fact]
    public async Task Send_AddOnAnswers_ReturnsTheResultOfTheMatchingId()
    {
        await using var addOn = FakeAddOn.Echo();
        await using var link = Connect(addOn.Port);

        var reply = await link.SendAsync(BridgeCommands.ObjectInfo, new JsonObject { ["name"] = "Cube" });

        Assert.True(reply.IsOk);
        Assert.Equal("object_info", reply.Result!["command"]!.ToString());
        Assert.Equal("Cube", reply.Result["params"]!["name"]!.ToString());
        Assert.True(link.IsConnected);
    }

    /// <summary>A chunk of a transfer goes out as the bytes it is, with nothing escaped that JSON does not require.</summary>
    /// <remarks>The default encoder escapes what a browser could misread, and two of the sixty-four characters of base64 — '+' and '/' — are
    /// among them, so every chunk of every transfer took the escaping path. That path reserves six bytes per character before counting how many
    /// it needs: one eight-megabyte chunk allocated 225 MB, and a server held to a gigabyte died of OutOfMemoryException in the middle of an
    /// upload, answering 500 and then 502 while the add-on never heard of the request at all. Nothing reading this is a browser; it is Python's
    /// json at the other end of a socket. The escaped spelling is what this watches, because the memory was only its consequence.</remarks>
    [Fact]
    public async Task Send_AChunkOfATransfer_EscapesNothingBase64NeedsEscaping()
    {
        await using var addOn = FakeAddOn.Echo();
        await using var link = Connect(addOn.Port, requestTimeoutSeconds: 60);
        var payload = Convert.ToBase64String(RandomNumberGenerator.GetBytes(512 * 1024));

        var reply = await link.SendAsync(BridgeCommands.FilePut, new JsonObject { ["path"] = "big.bin", ["offset"] = 0, ["data"] = payload });
        var line = addOn.Lines.Single();

        Assert.True(reply.IsOk, $"{reply.Error?.Type}: {reply.Error?.Message}");
        Assert.Equal(payload, addOn.Received.Single()["params"]!["data"]!.GetValue<string>());
        Assert.DoesNotContain("\\u002B", line, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u002F", line, StringComparison.Ordinal);
        Assert.True(line.Length < payload.Length + 512, $"a line of {line.Length:N0} characters carried {payload.Length:N0} of payload");
    }

    [Fact]
    public async Task Send_WithAnAgentName_SignsEveryRequest()
    {
        await using var addOn = FakeAddOn.Echo();
        await using var link = new BlenderConnection(new BlenderLinkOptions { Port = addOn.Port, ConnectTimeoutSeconds = 2, RequestTimeoutSeconds = 5, Agent = "lighting" }, NullLogger<BlenderConnection>.Instance);

        var reply = await link.SendAsync(BridgeCommands.TransformObject, new JsonObject { ["name"] = "Cube" });

        Assert.Equal("lighting", reply.Result!["params"]!["agent"]!.ToString());
        Assert.Equal("Cube", reply.Result["params"]!["name"]!.ToString());
    }

    [Fact]
    public async Task Send_WithAToken_CarriesItOnEveryRequest()
    {
        await using var addOn = FakeAddOn.Echo();
        await using var link = new BlenderConnection(new BlenderLinkOptions { Port = addOn.Port, ConnectTimeoutSeconds = 2, RequestTimeoutSeconds = 5, Token = "s3cret" }, NullLogger<BlenderConnection>.Instance);

        await link.SendAsync(BridgeCommands.Ping);
        await link.SendAsync(BridgeCommands.SceneInfo);

        Assert.All(addOn.Received, request => Assert.Equal("s3cret", request["token"]!.ToString()));
        Assert.All(addOn.Received, request => Assert.Null(request["params"]!["token"]));
    }

    /// <summary>The add-on writes the token it generated next to its state; a server with no token configured reads that file on connect, so a fresh install needs no shared secret typed anywhere.</summary>
    [Fact]
    public async Task Send_WithoutAConfiguredToken_ReadsTheAddOnsTokenFileOnConnect()
    {
        var file = Path.Combine(Path.GetTempPath(), $"snail-token-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(file, "from-file\n");
        await using var addOn = FakeAddOn.Echo();
        await using var link = new BlenderConnection(new BlenderLinkOptions { Port = addOn.Port, ConnectTimeoutSeconds = 2, RequestTimeoutSeconds = 5, TokenFile = file }, NullLogger<BlenderConnection>.Instance);

        await link.SendAsync(BridgeCommands.Ping);

        Assert.Equal("from-file", addOn.Received.Single()["token"]!.ToString());
        Assert.Equal("file", link.TokenSource);
        File.Delete(file);
    }

    /// <summary>A cancelled request may have left half a line in the socket; keeping the link would glue the next request to it.</summary>
    [Fact]
    public async Task Send_CancelledByTheClient_DropsTheLinkAndReconnectsNextTime()
    {
        await using var addOn = new FakeAddOn(async request =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));

            return new JsonObject { ["id"] = request["id"]?.DeepClone(), ["ok"] = true, ["result"] = "late" }.ToJsonString();
        });
        await using var link = Connect(addOn.Port);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => link.SendAsync(BridgeCommands.Python, cancellationToken: cancellation.Token));
        Assert.False(link.IsConnected);

        var next = await link.SendAsync(BridgeCommands.Ping);

        Assert.True(next.IsOk);
        Assert.Equal(2, addOn.Connections);
    }

    /// <summary>A line that does not parse means the stream is no longer at a message boundary, so the link is reset rather than read further.</summary>
    [Fact]
    public async Task Send_GarbledReply_ReportsMalformedAndDropsTheLink()
    {
        await using var addOn = new FakeAddOn(_ => Task.FromResult<string?>("{not json"));
        await using var link = Connect(addOn.Port);

        var reply = await link.SendAsync(BridgeCommands.Ping);

        Assert.Equal(BridgeError.MalformedType, reply.Error!.Type);
        Assert.False(link.IsConnected);
    }

    [Fact]
    public async Task Send_NothingListens_ReturnsUnavailableWithoutThrowing()
    {
        await using var link = Connect(1);

        var reply = await link.SendAsync(BridgeCommands.Ping);

        Assert.False(reply.IsOk);
        Assert.Equal(BridgeError.UnavailableType, reply.Error!.Type);
        Assert.Contains(":1", reply.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_AddOnReportsAnError_ReturnsItsTypeMessageAndDetails()
    {
        await using var addOn = new FakeAddOn(request => Task.FromResult<string?>(FakeAddOn.ErrorReply(request, "UnknownCommand", "'nope' is not a bridge command")));
        await using var link = Connect(addOn.Port);

        var reply = await link.SendAsync(new BridgeCommand("nope", AddOnModules.Operators));

        Assert.False(reply.IsOk);
        Assert.Equal("UnknownCommand", reply.Error!.Type);
        Assert.Equal("'nope' is not a bridge command", reply.Error.Message);
        Assert.Equal("ping", reply.Error.Details!["known"]![0]!.ToString());
    }

    /// <summary>A reply that arrives after the deadline would otherwise be read as the answer to the next request, so the timeout resets the link and the next call opens a fresh connection.</summary>
    [Fact]
    public async Task Send_ReplyLaterThanTheTimeout_ResetsTheLinkAndReconnectsNextTime()
    {
        var slow = true;
        await using var addOn = new FakeAddOn(async request =>
        {
            if (slow) await Task.Delay(TimeSpan.FromSeconds(5));

            return new JsonObject { ["id"] = request["id"]?.DeepClone(), ["ok"] = true, ["result"] = "late" }.ToJsonString();
        });
        await using var link = Connect(addOn.Port);

        var timedOut = await link.SendAsync(BridgeCommands.Python, timeout: TimeSpan.FromMilliseconds(300));
        slow = false;
        var next = await link.SendAsync(BridgeCommands.Ping);

        Assert.Equal(BridgeError.TimeoutType, timedOut.Error!.Type);
        Assert.True(next.IsOk);
        Assert.Equal(2, addOn.Connections);
    }

    [Fact]
    public async Task Send_TwoCallsInARow_ReuseOneConnection()
    {
        await using var addOn = FakeAddOn.Echo();
        await using var link = Connect(addOn.Port);

        await link.SendAsync(BridgeCommands.Ping);
        await link.SendAsync(BridgeCommands.SceneInfo);

        Assert.Equal(1, addOn.Connections);
        Assert.Equal(["ping", "scene_info"], addOn.Received.Select(request => request["command"]!.ToString()));
    }

    /// <summary>The deadline used to start only once the turn was had, so a call with 30 s to spare waited out a 600 s render before it timed out.</summary>
    [Fact]
    public async Task Send_BehindARequestThatOutlastsItsTimeout_GivesUpUnsent()
    {
        await using var addOn = new FakeAddOn(async request =>
        {
            if (request["command"]!.ToString() == "python") await Task.Delay(TimeSpan.FromSeconds(3));

            return new JsonObject { ["id"] = request["id"]?.DeepClone(), ["ok"] = true, ["result"] = "done" }.ToJsonString();
        });
        await using var link = Connect(addOn.Port, requestTimeoutSeconds: 10);

        var holding = link.SendAsync(BridgeCommands.Python);
        await Until(() => addOn.Received.Count == 1);
        var waiting = Stopwatch.StartNew();
        var queued = await link.SendAsync(BridgeCommands.Ping, timeout: TimeSpan.FromMilliseconds(500));
        var waited = waiting.Elapsed;
        await holding;

        Assert.Equal(BridgeError.BusyType, queued.Error?.Type);
        Assert.True(waited < TimeSpan.FromSeconds(2), $"waited {waited}");
        Assert.DoesNotContain(addOn.Received, request => request["command"]!.ToString() == "ping");
    }

    /// <summary>A link idle while Blender restarted still reads as connected, so the next call used to write into a closed socket and report the
    /// command dropped, although it never reached Blender.</summary>
    [Fact]
    public async Task Send_AfterTheAddOnClosedTheIdleLink_ReconnectsAndIsAnswered()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serving = AnswerOncePerConnectionAsync(listener, connections: 2);
        await using var link = Connect(port);

        var first = await link.SendAsync(BridgeCommands.Ping);
        await Task.Delay(200);
        var second = await link.SendAsync(BridgeCommands.Ping);
        listener.Stop();
        await serving.ContinueWith(_ => { }, TaskScheduler.Default);

        Assert.True(first.IsOk);
        Assert.True(second.IsOk, $"{second.Error?.Type} {second.Error?.Message}");
    }

    /// <summary>Reading the token file used to happen after the link was kept and outside every catch: its failure escaped as an exception, and
    /// the next call went out on the kept link without a token, to be refused as unauthorized.</summary>
    [Fact]
    public async Task Send_WithATokenFileItCannotRead_ReportsItAndKeepsNoLink()
    {
        var file = Path.Combine(Path.GetTempPath(), $"snail-token-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(file, "secret");
        await using var addOn = FakeAddOn.Echo();
        await using var link = new BlenderConnection(new BlenderLinkOptions { Port = addOn.Port, ConnectTimeoutSeconds = 2, RequestTimeoutSeconds = 5, TokenFile = file }, NullLogger<BlenderConnection>.Instance);

        BridgeReply reply;

        await using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            reply = await link.SendAsync(BridgeCommands.Ping);
        }

        File.Delete(file);
        Assert.Equal(BridgeError.TokenUnreadableType, reply.Error?.Type);
        Assert.False(link.IsConnected);
        Assert.Empty(addOn.Received);
    }

    /// <summary>JSON has no NaN or infinity; a tool argument of 1e400 arrives as infinity and used to throw out of the call as an opaque error.</summary>
    [Fact]
    public async Task Send_WithANumberJsonCannotCarry_IsABadRequestAndNothingIsSent()
    {
        await using var addOn = FakeAddOn.Echo();
        await using var link = Connect(addOn.Port);

        var reply = await link.SendAsync(BridgeCommands.TransformObject, new JsonObject { ["location"] = new JsonArray(double.PositiveInfinity, 0.0, 0.0) });

        Assert.Equal("BadRequest", reply.Error?.Type);
        Assert.Empty(addOn.Received);
    }

    [Fact]
    public async Task Send_ReplyWhoseOkIsNotABoolean_IsMalformed()
    {
        await using var addOn = new FakeAddOn(request => Task.FromResult<string?>(new JsonObject { ["id"] = request["id"]?.DeepClone(), ["ok"] = "true" }.ToJsonString()));
        await using var link = Connect(addOn.Port);

        var reply = await link.SendAsync(BridgeCommands.Ping);

        Assert.Equal(BridgeError.MalformedType, reply.Error?.Type);
    }

    /// <summary>The caller's seconds are all the seconds there are: a Blender that listens but never accepts used to cost the connect timeout
    /// first and the request timeout after it, so a three second probe could hold the render watch for eight.</summary>
    [Fact]
    public async Task Send_ToAnAddOnThatListensAndNeverAccepts_GivesUpWithinTheCallersTimeout()
    {
        using var deaf = await DeafAsync();
        await using var link = new BlenderConnection(
            new BlenderLinkOptions { Port = deaf.Port, ConnectTimeoutSeconds = 30, RequestTimeoutSeconds = 30 },
            NullLogger<BlenderConnection>.Instance);

        var asking = Stopwatch.StartNew();
        var reply = await link.SendAsync(BridgeCommands.SceneInfo, timeout: TimeSpan.FromSeconds(1));
        asking.Stop();

        Assert.False(reply.IsOk);
        Assert.True(asking.Elapsed < TimeSpan.FromSeconds(5), $"the call took {asking.Elapsed} of the one second it was given");
    }

    /// <summary>A socket that listens with its backlog already filled: a connect to it neither completes nor is refused, which is how a Blender
    /// whose main thread never comes back looks from here.</summary>
    private static async Task<Deaf> DeafAsync()
    {
        var deaf = new Deaf();

        for (var filling = 0; filling < 8; filling++)
        {
            using var patience = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            var waiting = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                await waiting.ConnectAsync(new IPEndPoint(IPAddress.Loopback, deaf.Port), patience.Token);
                deaf.Keep(waiting);
            }
            catch (OperationCanceledException)
            {
                waiting.Dispose();

                break;
            }
        }

        return deaf;
    }

    private sealed class Deaf : IDisposable
    {
        private readonly Socket _listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly List<Socket> _waiting = [];

        public Deaf()
        {
            _listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            _listener.Listen(1);
            Port = ((IPEndPoint)_listener.LocalEndPoint!).Port;
        }

        public int Port { get; }

        public void Keep(Socket waiting) => _waiting.Add(waiting);

        public void Dispose()
        {
            _waiting.ForEach(waiting => waiting.Dispose());
            _listener.Dispose();
        }
    }

    private static async Task AnswerOncePerConnectionAsync(TcpListener listener, int connections)
    {
        for (var served = 0; served < connections; served++)
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var reader = new StreamReader(client.GetStream());
            await using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };

            if (await reader.ReadLineAsync() is { } line)
            {
                await writer.WriteLineAsync(new JsonObject { ["id"] = JsonNode.Parse(line)!["id"]?.DeepClone(), ["ok"] = true, ["result"] = "pong" }.ToJsonString());
            }
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }
}
