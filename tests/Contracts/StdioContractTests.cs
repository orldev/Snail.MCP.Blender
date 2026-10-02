using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>The stdio contract of the real server process: stdout carries JSON-RPC and nothing else, the composition root boots, guidance and the version blender_diagnose reports reach the client, and every tool of the assembly reaches the model.</summary>
public class StdioContractTests
{
    private const string ProtocolVersion = "2025-06-18";

    /// <summary>A budget for the tool list every client reads before it can do anything: 38.3 KB measured for the 42 tools that are always
    /// there, and three to grow in.</summary>
    /// <remarks>Not a measurement to keep updating: this list goes to every client on every session and sits in front of whatever it caches, so
    /// a new tool that would live here earns its place or moves behind a skill, where blender_find_tool carries it on demand.</remarks>
    private const double MostKilobytesOfTools = 41.5;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The session points at a port nothing listens on, so the run never reaches a Blender that happens to be open on the default port.</summary>
    [Fact]
    public async Task Server_AfterHandshake_KeepsStdoutCleanOfEverythingButJsonRpc()
    {
        using var session = await ServerSession.StartAsync(ClosedPort());

        var initialize = await session.CallAsync(1, "initialize", new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new { },
            clientInfo = new { name = "stdio-contract", version = "1.0" },
        });

        var result = initialize.GetProperty("result");
        var serverInfo = result.GetProperty("serverInfo");

        Assert.Equal("Snail.MCP.Blender", serverInfo.GetProperty("name").GetString());
        Assert.Equal(ThisAssembly.InformationalVersion, serverInfo.GetProperty("version").GetString());
        Assert.Equal(ServerGuidance.Instructions, result.GetProperty("instructions").GetString());

        await session.NotifyAsync("notifications/initialized");

        var listed = await session.CallAsync(2, "tools/list", new { });
        var names = listed.GetProperty("result").GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(BaseToolNamesOfAssembly(), names);
        Assert.True(Kilobytes(listed) <= MostKilobytesOfTools, $"the always-on tool list is {Kilobytes(listed):0.0} KB, above the {MostKilobytesOfTools} KB budget");

        var called = await session.CallAsync(3, "tools/call", new
        {
            name = "blender_diagnose",
            arguments = new { },
        });

        var payload = called.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
        var report = JsonDocument.Parse(payload).RootElement;

        Assert.True(report.GetProperty("ok").GetBoolean());
        Assert.False(report.GetProperty("data").GetProperty("blender").GetProperty("reachable").GetBoolean());

        await session.CallAsync(4, "tools/call", new { name = "blender_enable_skill", arguments = new { name = "modeling" } });

        Assert.Contains(session.StdoutLines, line => line.Contains("notifications/tools/list_changed", StringComparison.Ordinal));

        var relisted = await session.CallAsync(5, "tools/list", new { });
        var withSkill = relisted.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!).ToHashSet();

        Assert.Contains("blender_mesh_extrude", withSkill);
        Assert.True(withSkill.IsProperSupersetOf(names));

        var skillCall = await session.CallAsync(6, "tools/call", new { name = "blender_mesh_extrude", arguments = new { name = "Cube", distance = 0.5 } });
        var skillAnswer = JsonDocument.Parse(skillCall.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!).RootElement;

        Assert.False(skillAnswer.GetProperty("ok").GetBoolean());
        Assert.Equal("Unavailable", skillAnswer.GetProperty("data").GetProperty("type").GetString());
        Assert.True(skillCall.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Equal("Unavailable", skillCall.GetProperty("result").GetProperty("structuredContent").GetProperty("data").GetProperty("type").GetString());

        var prompts = await session.CallAsync(7, "prompts/list", new { });
        var promptNames = prompts.GetProperty("result").GetProperty("prompts").EnumerateArray().Select(prompt => prompt.GetProperty("name").GetString()!).ToHashSet();

        Assert.Equal([PromptTexts.FarmDeliveryName, PromptTexts.ProductShotName, PromptTexts.VideoDeliveryName], promptNames.Order());

        var recipe = await session.CallAsync(8, "prompts/get", new { name = PromptTexts.FarmDeliveryName, arguments = new { frames = "1-24", directory = "/renders/shot" } });
        var recipeText = recipe.GetProperty("result").GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;

        Assert.Contains("blender_render_check", recipeText, StringComparison.Ordinal);
        Assert.Contains("/renders/shot", recipeText, StringComparison.Ordinal);

        var resources = await session.CallAsync(9, "resources/list", new { });
        var resourceUris = resources.GetProperty("result").GetProperty("resources").EnumerateArray().Select(resource => resource.GetProperty("uri").GetString()!).ToHashSet();

        Assert.Equal(["snail://journal", "snail://renders/last"], resourceUris.Order());

        var lastRender = await session.CallAsync(10, "resources/read", new { uri = "snail://renders/last" });
        var noRender = JsonDocument.Parse(lastRender.GetProperty("result").GetProperty("contents")[0].GetProperty("text").GetString()!).RootElement;

        Assert.False(noRender.GetProperty("ok").GetBoolean());

        var refused = await session.CallAsync(11, "tools/call", new { name = "blender_python", arguments = new { code = "1", timeoutSeconds = 0 } });

        Assert.True(refused.GetProperty("result").GetProperty("isError").GetBoolean());

        var exit = await session.ShutdownAsync();

        Assert.Equal(0, exit);
        Assert.All(session.StdoutLines, line => Assert.Equal("2.0", session.ParseOrFail(line).GetProperty("jsonrpc").GetString()));
    }

    /// <summary>The picture of a capture reaches the client as an image block whose data is the base64 text: a unit test over the SDK types once passed while a real client rejected the bytes, so the check runs over the wire.</summary>
    [Fact]
    public async Task ViewportCapture_OverStdio_ReturnsThePictureAsValidBase64ImageContent()
    {
        var picture = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
        await using var addOn = new FakeAddOn(request => Task.FromResult<string?>(new JsonObject
        {
            ["id"] = request["id"]?.DeepClone(),
            ["ok"] = true,
            ["result"] = new JsonObject
            {
                ["path"] = "/renders/capture.png",
                ["preview"] = new JsonObject { ["mime"] = "image/png", ["base64"] = Convert.ToBase64String(picture), ["width"] = 1, ["height"] = 1 },
                ["stats"] = new JsonObject { ["mean_luminance"] = 0.5 },
            },
        }.ToJsonString()));
        using var session = await ServerSession.StartAsync(addOn.Port);

        await session.CallAsync(1, "initialize", new { protocolVersion = ProtocolVersion, capabilities = new { }, clientInfo = new { name = "stdio-contract", version = "1.0" } });
        await session.NotifyAsync("notifications/initialized");
        await session.CallAsync(2, "tools/call", new { name = "blender_enable_skill", arguments = new { name = "rendering" } });
        var called = await session.CallAsync(3, "tools/call", new { name = "blender_viewport_capture", arguments = new { path = "/renders/capture.png" } });

        Assert.True(called.TryGetProperty("result", out var result), called.ToString());
        var content = result.GetProperty("content");
        var text = JsonDocument.Parse(content[0].GetProperty("text").GetString()!).RootElement;

        Assert.Equal("image", content[1].GetProperty("type").GetString());
        Assert.Equal("image/png", content[1].GetProperty("mimeType").GetString());
        Assert.Equal(picture, Convert.FromBase64String(content[1].GetProperty("data").GetString()!));
        Assert.Equal(0.5, text.GetProperty("data").GetProperty("stats").GetProperty("mean_luminance").GetDouble());
        Assert.False(text.GetProperty("data").GetProperty("preview").TryGetProperty("base64", out _));
    }

    private static int ClosedPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    /// <summary>Tools are discovered by the SDK through attributes, so a misconfigured attribute is invisible to reflection-only guards; skill tools stay out until a skill is enabled.</summary>
    private static double Kilobytes(JsonElement listed) => listed.GetProperty("result").GetProperty("tools").GetRawText().Length / 1024.0;

    private static HashSet<string> BaseToolNamesOfAssembly() =>
        SkillRegistrationExtensions.BaseToolTypes(typeof(ToolDescriptions).Assembly)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>The server started as a separate process and spoken to over stdin/stdout, exactly as an MCP client does.</summary>
    private sealed class ServerSession : IDisposable
    {
        private readonly Process _process;
        private readonly string _dataDirectory;
        private readonly List<string> _stderr = [];

        private ServerSession(Process process, string dataDirectory)
        {
            _process = process;
            _dataDirectory = dataDirectory;
        }

        public List<string> StdoutLines { get; } = [];

        public static Task<ServerSession> StartAsync(int? bridgePort = null)
        {
            var dataDirectory = Path.Combine(Path.GetTempPath(), $"snail-blender-stdio-{Guid.NewGuid().ToString("N")[..8]}");
            var start = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Snail.MCP.Blender.dll"));
            start.Environment["SNAIL_MCP_BLENDER_DATA_DIRECTORY"] = dataDirectory;
            start.Environment["SNAIL_MCP_BLENDER_IDLE_TIMEOUT_MINUTES"] = "0";
            start.Environment.Remove("SNAIL_MCP_BLENDER_BRIDGE__PORT");
            start.Environment.Remove("SNAIL_MCP_BLENDER_CONFIG");

            if (bridgePort is { } port)
            {
                start.Environment["SNAIL_MCP_BLENDER_BRIDGE__PORT"] = port.ToString();
            }

            var session = new ServerSession(Process.Start(start)!, dataDirectory);
            session.DrainStderr();

            return Task.FromResult(session);
        }

        public async Task<JsonElement> CallAsync(int id, string method, object parameters)
        {
            await NotifyAsync(method, parameters, id);

            return await ReadAsync();
        }

        public async Task NotifyAsync(string method, object? parameters = null, int? id = null)
        {
            var envelope = new Dictionary<string, object> { ["jsonrpc"] = "2.0", ["method"] = method };

            if (id is { } value) envelope["id"] = value;
            if (parameters is not null) envelope["params"] = parameters;

            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(envelope));
            await _process.StandardInput.FlushAsync();
        }

        public async Task<int> ShutdownAsync()
        {
            _process.StandardInput.Close();

            using var deadline = new CancellationTokenSource(Timeout);

            while (await _process.StandardOutput.ReadLineAsync(deadline.Token) is { } line)
            {
                if (line.Length > 0) StdoutLines.Add(line);
            }

            await _process.WaitForExitAsync(deadline.Token);

            return _process.ExitCode;
        }

        /// <summary>stdio carries JSON-RPC only: a single stray Console.Write breaks every MCP client, so the line itself is reported.</summary>
        public JsonElement ParseOrFail(string line)
        {
            try
            {
                return JsonDocument.Parse(line).RootElement;
            }
            catch (JsonException)
            {
                Assert.Fail($"stdout carries a line that is not JSON-RPC: {line}\nstderr:\n{Stderr()}");

                throw;
            }
        }

        private string Stderr()
        {
            lock (_stderr) return string.Join('\n', _stderr);
        }

        private async Task<JsonElement> ReadAsync()
        {
            using var deadline = new CancellationTokenSource(Timeout);

            while (await _process.StandardOutput.ReadLineAsync(deadline.Token) is { } line)
            {
                if (line.Length == 0) continue;

                StdoutLines.Add(line);

                var message = ParseOrFail(line);

                if (message.TryGetProperty("result", out _) || message.TryGetProperty("error", out _))
                {
                    return message;
                }
            }

            throw new InvalidOperationException($"the server closed stdout without answering. stderr:\n{Stderr()}");
        }

        private void DrainStderr() => _ = Task.Run(async () =>
        {
            while (await _process.StandardError.ReadLineAsync() is { } line)
            {
                lock (_stderr) _stderr.Add(line);
            }
        });

        public void Dispose()
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);

            _process.Dispose();

            if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
        }
    }
}
