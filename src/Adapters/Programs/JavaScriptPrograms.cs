using System.Diagnostics;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Adapters.Programs;

/// <summary>Runs a JavaScript program whose only door to the outside is the command catalog: no files, no network, no clock but its own.</summary>
/// <remarks>The engine is synchronous, so the program runs on a thread of its own and each command it sends waits there for the link. Nothing of
/// the host is reachable from the script — the engine is created without CLR access — and the program is bounded three ways: the timeout it was
/// given, a memory ceiling, and a cap on how many commands it may send, because a program is a way to spend one call, not to hide a thousand.
/// <para>Every command a program sends is put to the calling client's key first. The tool that starts a program carries one annotation, and a
/// program is whatever the script asks for: without this, a key issued without the Python scope ran Python by writing one line of JavaScript.</para></remarks>
public sealed class JavaScriptPrograms(IBlenderBridge bridge, ServerConfig config, ScriptAdvice advice, ToolScopes scopes, ClientSessions clients) : IProgramRunner
{
    /// <summary>Commands one program may send.</summary>
    public const int MostCalls = 200;

    private const long MemoryBytes = 32L * 1024 * 1024;

    private const int MostLogLines = 200;

    private const int LongestLogLine = 1000;

    public Task<ProgramRun> RunAsync(string code, JsonObject? input, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.Run(() => Run(code, input, timeout, cancellationToken), cancellationToken);

    private ProgramRun Run(string code, JsonObject? input, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var calls = new List<ProgramCall>();
        var log = new List<string>();
        var running = Stopwatch.StartNew();
        var engine = new Engine(options => options
            .Strict()
            .LimitMemory(MemoryBytes)
            .TimeoutInterval(timeout)
            .CancellationToken(cancellationToken));

        engine.SetValue("input", Parsed(engine, input ?? []));
        engine.SetValue("log", new ClrFunction(engine, "log", (_, written) => Write(log, written)));
        engine.SetValue("blender", Catalog(engine, calls, log, running, timeout, cancellationToken));

        try
        {
            var answer = engine.Evaluate($"(() => {{\n{code}\n}})()");

            return new ProgramRun(Serialized(engine, answer), log, calls, null);
        }
        catch (ProgramStopped stopped)
        {
            return new ProgramRun(null, log, calls, stopped.Reason);
        }
        catch (JavaScriptException failure)
        {
            return new ProgramRun(null, log, calls, Thrown(failure));
        }
        catch (Exception failure) when (failure is TimeoutException or ExecutionCanceledException)
        {
            return new ProgramRun(null, log, calls, new BridgeError(BridgeError.TimeoutType, $"the program ran longer than the {timeout.TotalSeconds:0.#} s it was given; the commands it had already sent stand."));
        }
        catch (Exception failure) when (failure is MemoryLimitExceededException or StatementsCountOverflowException or RecursionDepthOverflowException)
        {
            return new ProgramRun(null, log, calls, new BridgeError("ProgramTooLarge", $"the program asked for more than a program may use ({failure.GetType().Name}); split the work into several."));
        }
    }

    /// <summary>The catalog as an object of functions: <c>blender.add_primitive({ kind: "cube" })</c> for every command the add-on answers.</summary>
    private ObjectInstance Catalog(Engine engine, List<ProgramCall> calls, List<string> log, Stopwatch running, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var catalog = new JsObject(engine);

        foreach (var command in BridgeCommands.All)
        {
            catalog.FastSetDataProperty(command.Name, new ClrFunction(engine, command.Name, (_, arguments) =>
                Send(engine, command, arguments, calls, running, timeout, cancellationToken)));
        }

        catalog.FastSetDataProperty("log", new ClrFunction(engine, "log", (_, written) => Write(log, written)));

        return catalog;
    }

    private JsValue Send(Engine engine, BridgeCommand command, JsValue[] arguments, List<ProgramCall> calls, Stopwatch running, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (calls.Count >= MostCalls)
        {
            throw new ProgramStopped(new BridgeError("TooManyCalls", $"a program sends at most {MostCalls} commands; this one asked for more."));
        }

        var parameters = Arguments(engine, arguments);
        var left = timeout - running.Elapsed;

        if (left <= TimeSpan.Zero)
        {
            throw new ProgramStopped(new BridgeError(BridgeError.TimeoutType, $"the program ran longer than the {timeout.TotalSeconds:0.#} s it was given; the commands it had already sent stand."));
        }

        if (scopes.For(command.Name) is { } scope && !clients.Current.May(scope))
        {
            throw new ProgramStopped(new BridgeError("Unscoped", ToolScopes.Refusal(command.Name, scope, clients.Current.Name)));
        }

        if (ScriptAccess.Refusal(command, parameters, config.Python, advice) is { } refused)
        {
            throw new ProgramStopped(refused);
        }

        var started = running.Elapsed;
        var reply = bridge.SendAsync(command, parameters, left, cancellationToken).GetAwaiter().GetResult();

        calls.Add(new ProgramCall(command.Name, reply.IsOk, (int)(running.Elapsed - started).TotalMilliseconds));

        return reply.IsOk
            ? Parsed(engine, reply.Result ?? new JsonObject())
            : throw new JavaScriptException(Failure(engine, reply.Error!));
    }

    /// <summary>What the program threw and did not catch; a failed command keeps the add-on's own type, so the reply says Leased or NotFound
    /// rather than only that a program stopped.</summary>
    private static BridgeError Thrown(JavaScriptException failure)
    {
        var where = $"the program stopped at line {failure.Location.Start.Line}";

        if (failure.Error is not ObjectInstance thrown || thrown.Get("type").IsUndefined())
        {
            return new BridgeError("ProgramFailed", $"{where}: {failure.Message}");
        }

        var type = thrown.Get("type").AsString();

        return new BridgeError(type, $"{where}: {type}: {thrown.Get("message")}");
    }

    /// <summary>The add-on's error as the exception a program catches: <c>failure.type</c> and <c>failure.message</c> as the reply carries them.</summary>
    private static JsValue Failure(Engine engine, BridgeError error)
    {
        var failure = new JsObject(engine);
        failure.FastSetDataProperty("type", error.Type);
        failure.FastSetDataProperty("message", error.Message);
        failure.FastSetDataProperty("details", error.Details is null ? JsValue.Null : Parsed(engine, error.Details));

        return failure;
    }

    private static JsValue Write(List<string> log, JsValue[] written)
    {
        if (log.Count < MostLogLines)
        {
            var line = string.Join(' ', written.Select(value => value.ToString()));

            log.Add(line.Length <= LongestLogLine ? line : $"{line[..LongestLogLine]}…");
        }

        return JsValue.Undefined;
    }

    private static JsonObject Arguments(Engine engine, JsValue[] arguments) =>
        arguments.Length == 0 || arguments[0].IsUndefined() || arguments[0].IsNull()
            ? []
            : Serialized(engine, arguments[0]) as JsonObject ?? [];

    /// <summary>JSON both ways rather than a converter of our own: the engine's own JSON.parse and JSON.stringify know every shape a script can make.</summary>
    private static JsValue Parsed(Engine engine, JsonNode value) =>
        engine.Evaluate("JSON.parse").Call(JsValue.Undefined, [value.ToJsonString()]);

    private static JsonNode? Serialized(Engine engine, JsValue value)
    {
        var text = engine.Evaluate("JSON.stringify").Call(JsValue.Undefined, [value]);

        return text.IsUndefined() || text.IsNull() ? null : JsonNode.Parse(text.AsString());
    }

    /// <summary>A program stopped by a rule of the server rather than by the script itself.</summary>
    private sealed class ProgramStopped(BridgeError reason) : Exception(reason.Message)
    {
        public BridgeError Reason { get; } = reason;
    }
}
