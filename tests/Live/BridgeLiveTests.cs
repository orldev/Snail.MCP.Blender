using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>The link itself against a real add-on: what a malformed request, an odd reply or a client that stops reading does to everyone else.</summary>
[Collection("Loopback")]
public sealed class BridgeLiveTests : IAsyncLifetime
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private HeadlessBlender _blender = null!;
    private BlenderConnection _link = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _link = new BlenderConnection(Impatient(_blender), NullLogger<BlenderConnection>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_link is not null) await _link.DisposeAsync();

        _blender?.Dispose();
    }

    /// <summary>A file name that is not UTF-8 reaches Python as a lone surrogate; encoding the reply strictly used to raise out of the headless loop
    /// and end the Blender process with its unsaved scene.</summary>
    [BlenderFact]
    public async Task Reply_CarryingAStringPythonCannotEncode_ArrivesAndBlenderKeepsServing()
    {
        var odd = await _link.SendAsync(BridgeCommands.Python, new JsonObject { ["code"] = "result = 'caf' + chr(0xDCE9)" });
        var ping = await _link.SendAsync(BridgeCommands.Ping);

        Assert.True(odd.IsOk, $"{odd.Error?.Type} {odd.Error?.Message}");
        Assert.StartsWith("caf", odd.Result!["result"]!.ToString(), StringComparison.Ordinal);
        Assert.True(ping.IsOk, $"{ping.Error?.Type} {ping.Error?.Message}");
    }

    /// <summary>A parameter the command does not read is refused, and the refusal names the one that was meant.</summary>
    /// <remarks>These were the quiet ones. set_camera_optics({"name": "Post", "lens": 35}) answered ok and moved whichever camera the scene
    /// had, so the next render came back changed and the fault was looked for in the scene; set_material({"objects": [...]}) answered ok and
    /// assigned nothing; render_animation({"frame_start": 1}) answered ok and rendered all two hundred and fifty frames. The gate lives inside
    /// the add-on rather than in the server because a step of blender_run, a call from a program and a hand-written params never pass through
    /// a typed tool. The camera named by 'name' is the camera worked on, which is the other half of the same incident: the key was dropped,
    /// the scene's own camera was taken instead, and one camera's lens landed on another. Nothing expensive is asked for here on purpose:
    /// were the gate to go, the test would still have to be harmless.</remarks>
    [BlenderFact]
    public async Task Parameter_TheCommandDoesNotRead_IsRefused_AndTheRefusalNamesWhatWasMeant()
    {
        var mistaken = await _link.SendAsync(BridgeCommands.SetCameraOptics, new JsonObject { ["camera"] = "Camera", ["shift_x"] = 0.1 });
        var dropped = await _link.SendAsync(BridgeCommands.SetMaterial, new JsonObject { ["name"] = "Material", ["objects"] = new JsonArray("Cube") });
        var named = await _link.SendAsync(BridgeCommands.SetCameraOptics, new JsonObject { ["name"] = "Camera", ["lens"] = 35 });

        Assert.Equal("BadRequest", mistaken.Error?.Type);
        Assert.Contains("'shift'", mistaken.Error!.Message, StringComparison.Ordinal);
        Assert.Equal("BadRequest", dropped.Error?.Type);
        Assert.Contains("'objects'", dropped.Error!.Message, StringComparison.Ordinal);
        Assert.True(named.IsOk, $"{named.Error?.Type} {named.Error?.Message}");
        Assert.Equal("Camera", named.Result!["camera"]!.GetValue<string>());
        Assert.Equal(35.0, named.Result!["lens_mm"]!.GetValue<double>(), 3);
    }

    [BlenderFact]
    public async Task Request_ThatIsNotAnObject_IsAnsweredAsBadRequest()
    {
        using var client = await ConnectAsync();
        using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);

        await client.GetStream().WriteAsync("null\n"u8.ToArray());
        var heard = await NextLineAsync(reader);

        Assert.False(heard.IsSilent, "no reply to a request that is valid JSON but not an object");
        Assert.Equal("BadRequest", JsonNode.Parse(heard.Line!)!["error"]!["type"]!.ToString());
    }

    /// <summary>The main thread answers the queue; writing a reply to a client that stopped reading used to happen there too, so one slow reader
    /// held up Blender itself. A three megabyte reply to a client with a four kilobyte window cannot leave, and the next request must still be
    /// answered.</summary>
    [BlenderFact]
    public async Task Client_ThatStopsReadingALargeReply_DoesNotHoldUpTheMainThread()
    {
        using var stuck = await ConnectAsync(receiveBuffer: 4096);
        var token = await File.ReadAllTextAsync(_blender.Link.TokenFile!);
        var request = new JsonObject
        {
            ["id"] = "1",
            ["command"] = "python",
            ["params"] = new JsonObject { ["code"] = "result = 'x' * 3000000" },
            ["token"] = token.Trim(),
        };

        await stuck.GetStream().WriteAsync(Encoding.UTF8.GetBytes($"{request.ToJsonString()}\n"));
        await Task.Delay(TimeSpan.FromSeconds(2));
        var asking = Stopwatch.StartNew();
        var scene = await _link.SendAsync(BridgeCommands.SceneInfo);

        Assert.True(scene.IsOk, $"{scene.Error?.Type} {scene.Error?.Message}");
        Assert.True(asking.Elapsed < TimeSpan.FromSeconds(5), $"the main thread was busy for {asking.Elapsed}");
    }

    /// <summary>A client that never reads cannot grow without bound inside Blender: once its unsent replies pass the outbox limits the link is
    /// closed, and the clients that do read carry on.</summary>
    [BlenderFact]
    public async Task Client_WhoseRepliesPileUp_IsDropped_AndTheOthersCarryOn()
    {
        using var stuck = await ConnectAsync(receiveBuffer: 4096);

        Assert.True(await FloodUntilClosedAsync(stuck.GetStream()), "the add-on neither answered nor dropped a client that never reads");
        var scene = await _link.SendAsync(BridgeCommands.SceneInfo);

        Assert.True(scene.IsOk, $"{scene.Error?.Type} {scene.Error?.Message}");
    }

    /// <summary>A data command is carried on the data link whoever sends it: the call site holds the bridge and names <c>file_list</c>, and it is
    /// answered while another command holds Blender's main thread — on one shared socket it would have waited out that command first.</summary>
    [BlenderFact]
    public async Task DataCommand_SentOnTheBridge_IsAnsweredWhileTheMainThreadIsBusy()
    {
        await using var links = new BlenderLinks(Impatient(_blender), NullLogger<BlenderConnection>.Instance);
        var put = new JsonObject
        {
            ["area"] = VolumeAreas.Files,
            ["path"] = "channels/note.txt",
            ["data"] = Convert.ToBase64String("busy"u8.ToArray()),
            ["offset"] = 0,
            ["done"] = true,
        };

        Assert.True((await links.SendAsync(BridgeCommands.FilePut, put)).IsOk);

        var busy = links.SendAsync(BridgeCommands.Python, new JsonObject { ["code"] = "import time; time.sleep(6)" }, TimeSpan.FromSeconds(30));
        await Task.Delay(TimeSpan.FromSeconds(1));

        var asking = Stopwatch.StartNew();
        var listed = await links.SendAsync(BridgeCommands.FileList, new JsonObject { ["area"] = VolumeAreas.Files, ["path"] = "channels" }, TimeSpan.FromSeconds(3));
        asking.Stop();

        Assert.True(listed.IsOk, $"{listed.Error?.Type} {listed.Error?.Message}");
        Assert.True(asking.Elapsed < TimeSpan.FromSeconds(2), $"the listing waited {asking.Elapsed} for the main thread");
        Assert.True((await busy).IsOk);
    }

    /// <summary>A request states how long its caller will wait, and the wait for Blender's main thread is part of that: one whose deadline passed
    /// in the queue is answered rather than run, because a command nobody waits for still changes the scene the next caller reads.</summary>
    [BlenderFact]
    public async Task Request_WhoseDeadlinePassesInTheQueue_IsRefused_AndNeverRuns()
    {
        using var client = await ConnectAsync();
        using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
        var busy = _link.SendAsync(BridgeCommands.Python, new JsonObject { ["code"] = "import time; time.sleep(4)" }, TimeSpan.FromSeconds(30));
        await Task.Delay(TimeSpan.FromSeconds(1));

        await WriteAsync(client, new JsonObject
        {
            ["id"] = "late",
            ["command"] = BridgeCommands.AddPrimitive.Name,
            ["params"] = new JsonObject { ["kind"] = "cube", ["name"] = "Ghost" },
            ["deadline_s"] = 0.5,
        });
        var refused = await NextLineAsync(reader);

        Assert.True((await busy).IsOk);
        Assert.Equal("Expired", JsonNode.Parse(refused.Line!)!["error"]?["type"]?.ToString());

        var objects = await _link.SendAsync(BridgeCommands.ListObjects);

        Assert.DoesNotContain("Ghost", objects.Result!["objects"]!.AsArray().Select(item => item!["name"]!.ToString()));
    }

    /// <summary>A guess that is turned away closes the link, so trying another costs a new connection instead of one more line.</summary>
    [BlenderFact]
    public async Task Token_ThatDoesNotMatch_IsRefusedAndTheLinkClosed()
    {
        using var client = await ConnectAsync();
        using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);

        await client.GetStream().WriteAsync("""{"id": "1", "command": "ping", "token": "guess"}"""u8.ToArray());
        await client.GetStream().WriteAsync("\n"u8.ToArray());
        var refused = await NextLineAsync(reader);
        var after = await NextLineAsync(reader);

        Assert.Equal("Unauthorized", JsonNode.Parse(refused.Line!)!["error"]!["type"]!.ToString());
        Assert.True(after.IsClosed, "the link stayed open after an unauthorized request");
    }

    private static BlenderLinkOptions Impatient(HeadlessBlender blender) =>
        new() { Port = blender.Port, ConnectTimeoutSeconds = 5, RequestTimeoutSeconds = (int)Patience.TotalSeconds, TokenFile = blender.Link.TokenFile };

    /// <summary>One request on a raw socket, signed with the token the add-on wrote, so a test can say what a client of its own would say.</summary>
    private async Task WriteAsync(TcpClient client, JsonObject request)
    {
        request["token"] = (await File.ReadAllTextAsync(_blender.Link.TokenFile!)).Trim();

        await client.GetStream().WriteAsync(Encoding.UTF8.GetBytes($"{request.ToJsonString()}\n"));
    }

    private async Task<TcpClient> ConnectAsync(int? receiveBuffer = null)
    {
        var client = new TcpClient();

        if (receiveBuffer is { } bytes)
        {
            client.ReceiveBufferSize = bytes;
        }

        await client.ConnectAsync(IPAddress.Loopback, _blender.Port);

        return client;
    }

    /// <summary>Writes malformed lines, each drawing a refusal the client never reads, until the add-on closes the link and the write fails.</summary>
    private static async Task<bool> FloodUntilClosedAsync(Stream stream)
    {
        var lines = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("x\n", 1000)));
        var giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);

        while (DateTime.UtcNow < giveUp)
        {
            try
            {
                await stream.WriteAsync(lines).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception failure) when (failure is IOException or SocketException or ObjectDisposedException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<Heard> NextLineAsync(StreamReader reader)
    {
        using var deadline = new CancellationTokenSource(Patience);

        try
        {
            return await reader.ReadLineAsync(deadline.Token) is { } line ? new Heard(line, IsClosed: false) : new Heard(null, IsClosed: true);
        }
        catch (OperationCanceledException)
        {
            return new Heard(null, IsClosed: false);
        }
        catch (IOException)
        {
            return new Heard(null, IsClosed: true);
        }
    }

    private sealed record Heard(string? Line, bool IsClosed)
    {
        public bool IsSilent => Line is null && !IsClosed;
    }
}
