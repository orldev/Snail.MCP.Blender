using Snail.MCP.Blender.Application.Sessions;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Adapters.Blender;

/// <summary>The TCP client of the add-on: one request in flight at a time, replies matched by id, a broken, timed-out, cancelled or garbled link dropped so the next call reconnects.</summary>
/// <remarks>The add-on runs commands one after another on Blender's main thread, so a second in-flight request would only
/// wait in its queue; serialising here keeps the reply stream trivially in order. Any exit that leaves bytes of unknown
/// shape in the socket closes it on purpose: a late reply would be read as the answer to the next request, a half-written
/// request would be glued to the next one, and a line that did not parse means the stream is no longer at a boundary.</remarks>
public sealed class BlenderConnection(BlenderLinkOptions options, ILogger<BlenderConnection> logger, ClientSessions? clients = null) : IBlenderBridge, IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>How a request is written for the add-on: as UTF-8, escaping only what JSON itself requires.</summary>
    /// <remarks>The default encoder escapes anything that could matter to a browser, and two of the sixty-four characters of base64 are among
    /// them, so every chunk of a transfer took the escaping path — which reserves six bytes per character before it knows how many it needs.
    /// One eight-megabyte chunk cost 225 MB of allocation that way and ended a server held to a gigabyte with OutOfMemoryException, in the
    /// middle of an upload the add-on never heard about. Nothing here is read by a browser: the reader on the other end is Python's json.</remarks>
    private static readonly JsonSerializerOptions Wire = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly SemaphoreSlim _turn = new(1, 1);
    private TcpClient? _client;
    private StreamReader? _reader;
    private Stream? _writer;
    private string? _token;
    private long _sequence;

    public bool IsConnected => _client?.Connected == true;

    /// <summary>Where the token came from on the last connect: configured, read from the add-on's file, or none.</summary>
    public string TokenSource { get; private set; } = "none";

    /// <remarks>The timeout is the whole of the wait, not the part of it this class finds convenient: the turn behind another call, the connect,
    /// and the exchange itself all come out of it, so a call behind a long render gives up unsent within its own time rather than waiting out
    /// the render first, and one that spends its seconds reaching a Blender that never answers does not then get them again. What is left when
    /// the request goes out travels with it as <c>deadline_s</c>, so the add-on can drop a queued command whose caller has already gone.</remarks>
    public async Task<BridgeReply> SendAsync(
        BridgeCommand command,
        JsonObject? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var allowed = timeout ?? TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        var waiting = Stopwatch.StartNew();

        if (!await _turn.WaitAsync(allowed, cancellationToken).ConfigureAwait(false))
        {
            return BridgeReply.Failed(BridgeError.Queued(command, allowed));
        }

        try
        {
            var left = allowed - waiting.Elapsed;

            return await ExchangeAsync(command, parameters, left > TimeSpan.Zero ? left : TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _turn.Release();
        }
    }

    private async Task<BridgeReply> ExchangeAsync(BridgeCommand command, JsonObject? parameters, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var spending = Stopwatch.StartNew();
        var unreachable = await ConnectAsync(timeout, cancellationToken).ConfigureAwait(false);

        if (unreachable is not null)
        {
            return BridgeReply.Failed(unreachable);
        }

        var left = timeout - spending.Elapsed;

        if (left <= TimeSpan.Zero)
        {
            return BridgeReply.Failed(BridgeError.Timeout(command, timeout));
        }

        var id = (++_sequence).ToString();
        var request = new JsonObject
        {
            ["id"] = id,
            ["command"] = command.Name,
            ["params"] = Signed(parameters ?? []),
            ["deadline_s"] = Math.Round(left.TotalSeconds, 3),
        };

        if (_token is not null)
        {
            request["token"] = _token;
        }

        if (Serialized(request) is not { } line)
        {
            return BridgeReply.Failed(BridgeError.NotFinite(command));
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(left);

        try
        {
            await _writer!.WriteAsync(line, deadline.Token).ConfigureAwait(false);
            await _writer.FlushAsync(deadline.Token).ConfigureAwait(false);

            return await ReadReplyAsync(command, id, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Drop();

            throw;
        }
        catch (OperationCanceledException)
        {
            Drop();

            return BridgeReply.Failed(BridgeError.Timeout(command, timeout));
        }
        catch (Exception failure) when (failure is SocketException or IOException or ObjectDisposedException)
        {
            Drop();

            return BridgeReply.Failed(BridgeError.Unavailable(options.Host, options.Port, failure.Message));
        }
    }

    /// <summary>Adds the agent name to the params; tools build a fresh object per call, so the object is written to rather than copied.</summary>
    /// <remarks>The name is the calling client's where a client is being served, and the configured one otherwise. One server over HTTP holds
    /// one link to the add-on, so signing with the configuration alone made every client the same agent over there: they took each other's
    /// leases without noticing and the journal recorded one worker doing everything.</remarks>
    private JsonObject Signed(JsonObject parameters)
    {
        if ((clients?.Current.Agent ?? options.Agent) is { } agent && !string.IsNullOrWhiteSpace(agent))
        {
            parameters["agent"] = agent;
        }

        return parameters;
    }

    private async Task<BridgeReply> ReadReplyAsync(BridgeCommand command, string id, CancellationToken cancellationToken)
    {
        while (await _reader!.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (TryParse(line) is not { } reply)
            {
                Drop();

                return BridgeReply.Failed(BridgeError.Malformed(line));
            }

            if (reply["id"]?.ToString() != id)
            {
                logger.LogDebug("Skipping a stale reply {ReplyId} while waiting for {Id}", reply["id"], id);

                continue;
            }

            return Interpret(reply);
        }

        Drop();

        return BridgeReply.Failed(BridgeError.Dropped(command));
    }

    /// <summary>The request as the bytes of the line that goes on the wire, newline included.</summary>
    /// <remarks>To UTF-8 directly rather than by way of a string: a chunk of a transfer is megabytes, and as a string it is built once,
    /// copied again to add the newline and encoded a third time. The buffer outlives the stream on purpose — a MemoryStream grown by
    /// this code keeps its array after it is disposed, and handing it out saves copying those megabytes once more.</remarks>
    private static ReadOnlyMemory<byte>? Serialized(JsonObject request)
    {
        try
        {
            using var written = new MemoryStream();

            using (var writer = new Utf8JsonWriter(written, new JsonWriterOptions { Encoder = Wire.Encoder }))
            {
                request.WriteTo(writer, Wire);
            }

            written.WriteByte((byte)'\n');

            return written.TryGetBuffer(out var buffer)
                ? new ReadOnlyMemory<byte>(buffer.Array, buffer.Offset, buffer.Count)
                : written.ToArray();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static JsonObject? TryParse(string line)
    {
        try
        {
            return JsonNode.Parse(line) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static BridgeReply Interpret(JsonObject reply)
    {
        if (reply["ok"] is not JsonValue ok || !ok.TryGetValue<bool>(out var isOk))
        {
            return BridgeReply.Failed(BridgeError.Malformed(reply.ToJsonString()));
        }

        if (isOk)
        {
            return BridgeReply.Ok(reply["result"]?.DeepClone());
        }

        var error = reply["error"] as JsonObject;

        return BridgeReply.Failed(new BridgeError(
            error?["type"]?.ToString() ?? "Error",
            error?["message"]?.ToString() ?? "The add-on reported a failure without a message.",
            error?["details"]?.DeepClone()));
    }

    /// <remarks>A link left idle while Blender restarted still reads as connected; one the other side has closed is readable with nothing to
    /// read, and is replaced before anything is written into it.</remarks>
    private async Task<BridgeError?> ConnectAsync(TimeSpan budget, CancellationToken cancellationToken)
    {
        if (_client is { Connected: true } && _writer is not null && !IsClosedByTheAddOn(_client.Client))
        {
            return null;
        }

        Drop();

        if (ResolveToken() is { Error: { } unreadable })
        {
            return unreadable;
        }

        var client = new TcpClient();
        var allowed = TimeSpan.FromSeconds(options.ConnectTimeoutSeconds);

        if (allowed > budget)
        {
            allowed = budget;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(allowed);

        try
        {
            await client.ConnectAsync(options.Host, options.Port, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();

            return BridgeError.Unavailable(options.Host, options.Port, $"no answer within {allowed.TotalSeconds:0.#} s");
        }
        catch (SocketException failure)
        {
            client.Dispose();

            return BridgeError.Unavailable(options.Host, options.Port, failure.Message);
        }

        var stream = client.GetStream();
        _client = client;
        _reader = new StreamReader(stream, Utf8);
        _writer = stream;

        logger.LogInformation("Connected to the Blender add-on on {Host}:{Port} with {TokenSource} token", options.Host, options.Port, TokenSource);

        return null;
    }

    /// <summary>The configured token, else the one the add-on wrote next to its state, read on every connect so a Blender started later is picked up.</summary>
    /// <remarks>Read before a link is kept: a file that cannot be read used to escape as an exception after the socket was stored, and the next
    /// call went out on it without a token.</remarks>
    private TokenRead ResolveToken()
    {
        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            TokenSource = "configured";
            _token = options.Token;

            return new TokenRead(null);
        }

        if (options.TokenFile is { } file && File.Exists(file))
        {
            string stored;

            try
            {
                stored = File.ReadAllText(file).Trim();
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                return new TokenRead(BridgeError.TokenUnreadable(file, failure.Message));
            }

            if (stored.Length > 0)
            {
                TokenSource = "file";
                _token = stored;

                return new TokenRead(null);
            }
        }

        TokenSource = "none";
        _token = null;

        return new TokenRead(null);
    }

    private static bool IsClosedByTheAddOn(Socket socket)
    {
        try
        {
            return socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0;
        }
        catch (Exception failure) when (failure is SocketException or ObjectDisposedException)
        {
            return true;
        }
    }

    private sealed record TokenRead(BridgeError? Error);

    private void Drop()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _client?.Dispose();
        _writer = null;
        _reader = null;
        _client = null;
    }

    /// <summary>Closes the link once the call in flight has let go of it, so the last request of a shutdown fails like any other.</summary>
    /// <remarks>Disposing under a running request tore the socket out from beneath it and then disposed the semaphore it was about to
    /// release: what came back was an ObjectDisposedException out of a finally block rather than the reply the caller was waiting for. A
    /// render longer than the idle timeout is enough to reach it.</remarks>
    public async ValueTask DisposeAsync()
    {
        var mine = await _turn.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Drop();

        if (mine)
        {
            _turn.Release();
        }

        _turn.Dispose();
    }
}
