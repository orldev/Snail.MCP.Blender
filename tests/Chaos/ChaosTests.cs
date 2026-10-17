using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Base;

namespace Snail.MCP.Blender.Tests.Chaos;

/// <summary>Crash tests of the audit: each one breaks the server the way the thing it names breaks in the field.</summary>
[Collection("Loopback")]
public class ChaosTests : IDisposable
{
    private readonly string _data = TestFolder.Create();

    public void Dispose()
    {
        if (Directory.Exists(_data))
        {
            Directory.Delete(_data, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>A digest in clients.json that is not base64 — a hand edit, a file restored from elsewhere — must turn its client away, not take
    /// the HTTP server down.</summary>
    /// <remarks>Holder decodes every candidate digest on every request, so the FormatException leaves the authentication handler and the server
    /// answers 500 to everybody, including the clients whose keys are fine.</remarks>
    [Fact]
    public void ClientKeys_WhenAStoredDigestIsNotBase64_TurnsTheClientAway_InsteadOfFailingEveryRequest()
    {
        var file = Path.Combine(_data, "state", "clients.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """
            {
              "clients": [
                { "name": "lighting", "digest": "not a digest", "scopes": ["read"], "issued": "2026-10-16T00:00:00+00:00" }
              ]
            }
            """);
        var keys = new ClientKeys(new ServerConfig { DataDirectory = _data }, TimeProvider.System);

        var admitted = Record.Exception(() => keys.Holder("whatever the client presents"));

        Assert.Null(admitted);
    }

    /// <summary>The server stopping while a render is still running must let that call end, not tear the turnstile out from under it.</summary>
    /// <remarks>DisposeAsync waits five seconds for the call in flight and then disposes the semaphore anyway; the call that is still waiting for
    /// Blender releases a disposed semaphore in its finally, so what reaches the client is an ObjectDisposedException instead of the timeout the
    /// call was given. The idle watchdog stops the host under exactly this condition — a render longer than the idle timeout.</remarks>
    [Fact]
    public async Task BlenderConnection_DisposedWhileACallIsInFlight_LetsThatCallEnd_WithAFailureRatherThanAThrow()
    {
        var silence = new TaskCompletionSource<string?>();
        await using var addOn = new FakeAddOn(_ => silence.Task);
        var link = new BlenderConnection(
            new BlenderLinkOptions { Port = addOn.Port, ConnectTimeoutSeconds = 2, RequestTimeoutSeconds = 30 },
            NullLogger<BlenderConnection>.Instance);
        var rendering = link.SendAsync(BridgeCommands.RenderImage, new JsonObject { ["output_path"] = "/renders/shot.png" }, TimeSpan.FromSeconds(20));

        for (var attempt = 0; attempt < 200 && addOn.Received.Count == 0 && !rendering.IsCompleted; attempt++)
        {
            await Task.Delay(10);
        }

        await link.DisposeAsync();

        BridgeReply? answered = null;
        var thrown = await Record.ExceptionAsync(async () => answered = await rendering);

        silence.TrySetResult(null);

        Assert.Null(thrown);
        Assert.False(answered!.IsOk);
    }

    /// <summary>A command that loses work is delete work however it arrives; without the schema a step of run_tools is only write work, and a key
    /// minted to build but not to destroy deletes the scene through it.</summary>
    /// <remarks>ToolScopes reads a tool's own annotation when the schema is unreadable, which keeps blender_delete_objects delete work. A carrier's
    /// step has no annotation to fall back on and lands on the widest ordinary answer instead, so the same command costs less inside run_tools
    /// than outside it the moment addon/commands.json is missing from the publication.</remarks>
    [Fact]
    public async Task RunTools_WithoutTheCommandSchema_RefusesADestructiveStep_FromAKeyThatOnlyBuilds()
    {
        var bridge = new ScriptedBridge().AnswerAnything(new JsonObject());
        var scopes = new ToolScopes(new CommandSemantics(Path.Combine(_data, "no-add-on-here")));
        var (clients, serving) = Access.Serving("lighting", Scopes.Read, Scopes.Write);
        using var _ = serving;
        var tools = new SequenceTools(bridge, new ServerConfig(), new ScriptAdvice(new ToolIndex(typeof(SequenceTools).Assembly)), scopes, clients);
        var steps = new JsonArray(new JsonObject { ["command"] = "delete_objects", ["params"] = new JsonObject { ["names"] = new JsonArray("Cube") } });

        var answer = await tools.RunAsync(steps);

        Assert.True(answer.Failed(), "a key without the delete scope deleted an object through run_tools");
        Assert.Empty(bridge.Sent);
    }

    /// <summary>Several tools called at once share one socket; each caller must get its own reply and no other's.</summary>
    [Fact]
    public async Task BlenderConnection_UnderParallelCalls_GivesEachCallerTheReplyToItsOwnRequest()
    {
        await using var addOn = new FakeAddOn(
            async request =>
            {
                await Task.Delay(Random.Shared.Next(1, 15));

                return new JsonObject
                {
                    ["id"] = request["id"]?.DeepClone(),
                    ["ok"] = true,
                    ["result"] = new JsonObject { ["echo"] = request["params"]?["name"]?.DeepClone() },
                }.ToJsonString();
            },
            isConcurrent: true);
        await using var link = new BlenderConnection(
            new BlenderLinkOptions { Port = addOn.Port, ConnectTimeoutSeconds = 2, RequestTimeoutSeconds = 10 },
            NullLogger<BlenderConnection>.Instance);

        var asked = Enumerable.Range(0, 32).ToList();
        var answers = await Task.WhenAll(asked.Select(index =>
            link.SendAsync(BridgeCommands.ObjectInfo, new JsonObject { ["name"] = $"Cube{index}" })));

        Assert.All(answers, reply => Assert.True(reply.IsOk, reply.Error?.Message));
        Assert.Equal(asked.Select(index => $"Cube{index}"), answers.Select(reply => reply.Result!["echo"]!.ToString()));
    }
}
