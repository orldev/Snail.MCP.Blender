using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>The HTTP contract of the real server process, the way a container runs it: nothing without the token, liveness for anyone, and the same tools as over stdio.</summary>
public class HttpContractTests
{
    private const string Token = "http-contract-token";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Server_OverHttp_AdmitsOnlyItsToken_AndServesTheToolsOfTheStdioServer()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        using var http = new HttpClient { BaseAddress = server.Address };

        var health = JsonNode.Parse(await http.GetStringAsync("/healthz"))!;
        var anonymous = await http.PostAsync("/mcp", JsonBody());
        var stranger = await http.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = JsonBody(), Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "guess") } });

        Assert.Equal("ok", health["status"]!.ToString());
        Assert.Equal("down", health["blender"]!.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, stranger.StatusCode);

        await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(server.Address, HttpServerRegistrationExtensions.McpPath),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {Token}" },
        }));

        var tools = (await client.ListToolsAsync()).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var diagnosed = await client.CallToolAsync("blender_diagnose");
        await client.CallToolAsync("blender_enable_skill", new Dictionary<string, object?> { ["name"] = "modeling" });
        var withSkill = (await client.ListToolsAsync()).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(ServerGuidance.Instructions, client.ServerInstructions);
        Assert.Equal(BaseToolNamesOfAssembly(), tools);
        Assert.NotEqual(true, diagnosed.IsError);
        Assert.Contains("blender_mesh_extrude", withSkill);
    }

    /// <summary>A client that still opens with initialize keeps a session, and the session is what tells it a skill's tools arrived.</summary>
    [Fact]
    public async Task Server_OverHttp_TellsASessionClient_ThatASkillsToolsArrived()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var client = await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(server.Address, HttpServerRegistrationExtensions.McpPath),
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {Token}" },
            }),
            new McpClientOptions { ProtocolVersion = "2025-11-25" });

        await using var subscription = client.RegisterNotificationHandler(NotificationMethods.ToolListChangedNotification, (_, _) =>
        {
            changed.TrySetResult();

            return ValueTask.CompletedTask;
        });

        await client.CallToolAsync("blender_enable_skill", new Dictionary<string, object?> { ["name"] = "rendering" });
        await changed.Task.WaitAsync(Timeout);

        Assert.False(string.IsNullOrEmpty(client.SessionId));
        Assert.Contains("blender_render_settings", (await client.ListToolsAsync()).Select(tool => tool.Name));
    }

    /// <summary>A client of the sessionless revision holds no stream for messages of its own, so the notice that a skill's tools arrived has to
    /// come back on the response to the call that loaded the skill.</summary>
    [Fact]
    public async Task Server_OverHttp_TellsASessionlessClient_ThatASkillsToolsArrived_InTheReplyToTheCall()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(server.Address, HttpServerRegistrationExtensions.McpPath),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {Token}" },
        }));

        await using var subscription = client.RegisterNotificationHandler(NotificationMethods.ToolListChangedNotification, (_, _) =>
        {
            changed.TrySetResult();

            return ValueTask.CompletedTask;
        });

        await client.CallToolAsync("blender_enable_skill", new Dictionary<string, object?> { ["name"] = "farm" });
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(string.IsNullOrEmpty(client.SessionId));
    }

    /// <summary>One server over HTTP serves several clients, and a tool list belongs to one of them: the skill one client loads is in its list
    /// and in no other, and the commands it sends are signed with its own name.</summary>
    /// <remarks>A client of the sessionless revision is handed a fresh session on every request, so it is recognised by the name it gives
    /// rather than by a session of the protocol's.</remarks>
    [Fact]
    public async Task Server_OverHttp_KeepsTheToolsOfOneClient_OutOfAnothersList()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        await using var lighting = await ClientAsync(server, "lighting");
        await using var modeling = await ClientAsync(server, "modeling");

        await lighting.CallToolAsync("blender_enable_skill", new Dictionary<string, object?> { ["name"] = "rendering" });
        var lit = (await lighting.ListToolsAsync()).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var modelled = (await modeling.ListToolsAsync()).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("blender_render_settings", lit);
        Assert.DoesNotContain("blender_render_settings", modelled);
        Assert.Equal(BaseToolNamesOfAssembly(), modelled);
    }

    /// <summary>A client is who its key says it is, and may do what the key opens: a key minted for reading names its holder on every command
    /// and turns away the call that would change the scene, whatever header the caller sends.</summary>
    [Fact]
    public async Task Server_OverHttp_NamesAClientByItsKey_AndRefusesWorkTheKeyDoesNotOpen()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        var keys = new ClientKeys(new ServerConfig { DataDirectory = server.DataDirectory }, TimeProvider.System);
        var (_, secret) = keys.Mint("lighting", [Scopes.Read]);

        await using var client = await ClientAsync(server, agent: "modeling", key: secret);

        var diagnosed = await client.CallToolAsync("blender_diagnose");
        var written = await client.CallToolAsync("blender_add_primitive", new Dictionary<string, object?> { ["kind"] = "cube" });

        Assert.NotEqual(true, diagnosed.IsError);
        Assert.Equal("lighting", JsonNode.Parse(diagnosed.Content.OfType<TextContentBlock>().First().Text)!["data"]!["bridge"]!["agent"]!.ToString());
        Assert.True(written.IsError);
        Assert.Contains("does not open it", written.Content.OfType<TextContentBlock>().First().Text, StringComparison.Ordinal);
    }

    /// <summary>Revoking a key turns its holder away at once: the list is read again when it changes, because a key is revoked from a command
    /// line while the server it admits is running.</summary>
    [Fact]
    public async Task Client_WhoseKeyWasRevoked_IsTurnedAwayWithoutRestartingTheServer()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        var keys = new ClientKeys(new ServerConfig { DataDirectory = server.DataDirectory }, TimeProvider.System);
        var (_, secret) = keys.Mint("lighting");

        await using (var admitted = await ClientAsync(server, agent: "lighting", key: secret))
        {
            Assert.NotEmpty(await admitted.ListToolsAsync());
        }

        Assert.True(keys.Revoke("lighting"));

        using var http = new HttpClient { BaseAddress = server.Address };
        var refused = await http.SendAsync(new HttpRequestMessage(HttpMethod.Post, HttpServerRegistrationExtensions.McpPath)
        {
            Content = JsonBody(),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", secret) },
        });

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    /// <summary>The scopes are not only about tool calls: a key minted to read cannot write or delete over the file endpoints either, which is
    /// where another client's renders and project files are.</summary>
    [Fact]
    public async Task Files_OverHttp_AreOpenedByTheScopesOfTheKey_NotByAdmissionAlone()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        var keys = new ClientKeys(new ServerConfig { DataDirectory = server.DataDirectory }, TimeProvider.System);
        var (_, reader) = keys.Mint("viewer", [Scopes.Read]);
        var (_, writer) = keys.Mint("editor", [Scopes.Read, Scopes.Write]);

        using var http = new HttpClient { BaseAddress = server.Address };

        Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(Signed(HttpMethod.Delete, "/raw/files/shot.png", reader))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(Signed(HttpMethod.Put, "/raw/files/shot.png", reader))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(Signed(HttpMethod.Delete, "/raw/files/shot.png", writer))).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await http.SendAsync(Signed(HttpMethod.Get, "/raw/files/shot.png", reader))).StatusCode);
    }

    /// <summary>A container started without its secret would turn every client away while looking healthy, so it refuses to start and names the setting.</summary>
    [Fact]
    public async Task Server_OverHttpWithoutAToken_ExitsBeforeListening_NamingTheSetting()
    {
        await using var server = await HttpServerProcess.StartAsync(token: null, waitForHealth: false);

        var exit = await server.WaitForExitAsync();

        Assert.NotEqual(0, exit);
        Assert.Contains("SNAIL_MCP_BLENDER_HTTP__TOKEN", server.Output, StringComparison.Ordinal);
    }

    private static StringContent JsonBody() => new("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", MediaTypeHeaderValue.Parse("application/json"));

    private static HttpRequestMessage Signed(HttpMethod method, string path, string key) =>
        new(method, path) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", key) }, Content = new ByteArrayContent([1, 2, 3]) };

    /// <summary>A client that says who it is, the way a studio tells two of them apart; with a key of its own, the key is what names it.</summary>
    private static Task<McpClient> ClientAsync(HttpServerProcess server, string agent, string? key = null) =>
        McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(server.Address, HttpServerRegistrationExtensions.McpPath),
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {key ?? Token}",
                [TransportRegistrationExtensions.AgentHeader] = agent,
            },
        }));

    private static HashSet<string> BaseToolNamesOfAssembly() =>
        SkillRegistrationExtensions.BaseToolTypes(typeof(ToolDescriptions).Assembly)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>The server started as a separate process in HTTP mode, on a free loopback port, with no Blender behind it.</summary>
    private sealed class HttpServerProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly string _dataDirectory;
        private readonly List<string> _output = [];

        private HttpServerProcess(Process process, string dataDirectory, Uri address)
        {
            _process = process;
            _dataDirectory = dataDirectory;
            Address = address;
        }

        public Uri Address { get; }

        /// <summary>Where this server keeps its state, so a test can mint a client key into it while it runs.</summary>
        public string DataDirectory => _dataDirectory;

        public string Output
        {
            get
            {
                lock (_output) return string.Join('\n', _output);
            }
        }

        public static async Task<HttpServerProcess> StartAsync(string? token, bool waitForHealth = true)
        {
            var dataDirectory = Path.Combine(Path.GetTempPath(), $"snail-blender-http-{Guid.NewGuid().ToString("N")[..8]}");
            var address = new Uri($"http://127.0.0.1:{FreePort()}");
            var start = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Snail.MCP.Blender.dll"));

            foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("SNAIL_MCP_BLENDER_", StringComparison.Ordinal)).ToList())
            {
                start.Environment.Remove(name);
            }

            start.Environment["SNAIL_MCP_BLENDER_TRANSPORT"] = "Http";
            start.Environment["SNAIL_MCP_BLENDER_HTTP__URL"] = address.ToString();
            start.Environment["SNAIL_MCP_BLENDER_DATA_DIRECTORY"] = dataDirectory;
            start.Environment["SNAIL_MCP_BLENDER_BRIDGE__PORT"] = FreePort().ToString();

            if (token is not null)
            {
                start.Environment["SNAIL_MCP_BLENDER_HTTP__TOKEN"] = token;
            }

            var server = new HttpServerProcess(Process.Start(start)!, dataDirectory, address);
            server.Drain(server._process.StandardOutput);
            server.Drain(server._process.StandardError);

            if (waitForHealth)
            {
                await server.WaitForHealthAsync();
            }

            return server;
        }

        public async Task<int> WaitForExitAsync()
        {
            using var deadline = new CancellationTokenSource(Timeout);
            await _process.WaitForExitAsync(deadline.Token);

            return _process.ExitCode;
        }

        private async Task WaitForHealthAsync()
        {
            using var http = new HttpClient { BaseAddress = Address, Timeout = TimeSpan.FromSeconds(2) };
            var deadline = DateTimeOffset.UtcNow + Timeout;

            while (DateTimeOffset.UtcNow < deadline)
            {
                if (_process.HasExited)
                {
                    throw new InvalidOperationException($"the server exited with {_process.ExitCode}:\n{Output}");
                }

                try
                {
                    using var response = await http.GetAsync("/healthz");

                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                }
                catch (TaskCanceledException)
                {
                }

                await Task.Delay(200);
            }

            throw new TimeoutException($"the server did not answer /healthz:\n{Output}");
        }

        private void Drain(StreamReader reader) =>
            _ = Task.Run(async () =>
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    lock (_output) _output.Add(line);
                }
            });

        private static int FreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }

            _process.Dispose();

            if (Directory.Exists(_dataDirectory))
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
        }
    }
}
