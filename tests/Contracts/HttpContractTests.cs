using System.Net;
using System.Net.Http.Headers;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
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

    /// <summary>The variable the server reads its HTTP address from, as a container sets it.</summary>
    private const string AddressVariable = "SNAIL_MCP_BLENDER_HTTP__URL";

    /// <summary>The variable that alone asks for HTTP on a port, as an operator starts the server by hand.</summary>
    private const string PortVariable = "SNAIL_MCP_BLENDER_PORT";

    /// <summary>The variable that names the transport; a test that leaves it out sets it to null, so the start does not name it either.</summary>
    private const string TransportVariable = "SNAIL_MCP_BLENDER_TRANSPORT";

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

    /// <summary>A page in a browser is refused at the MCP endpoint with 403 before it is asked for the key, and an agent's client is still asked
    /// for it and let in with it.</summary>
    /// <remarks>The token alone kept pages out, but a page was challenged with 401 like a client that forgot its key; the refusal Snail.Sdk's
    /// <c>UseBrowserRefusal</c> holds comes before the authentication, as on every server of the fleet.</remarks>
    [Fact]
    public async Task Server_OverHttp_RefusesABrowser_BeforeAskingForTheKey()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        var endpoint = new Uri(server.Address, HttpServerRegistrationExtensions.McpPath);

        Assert.Empty(await HttpGuards.ProblemsAsync(endpoint));
        Assert.Empty(await HttpGuards.AuthorizedProblemsAsync(endpoint, new AuthenticationHeaderValue("Bearer", Token)));
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

    /// <summary>One server over HTTP serves several clients, and a tool list belongs to one session: the skill one client loads is in its list
    /// and in no other.</summary>
    /// <remarks>Each client has its own skills (Snail.Sdk's <c>WithSkills</c>), known by the name on its key or the one the holder of the
    /// configured token gives itself.</remarks>
    [Fact]
    public async Task Server_OverHttp_KeepsTheToolsOfOneClient_OutOfAnothersList()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        await using var lighting = await ClientAsync(server, "lighting", protocolVersion: "2025-11-25");
        await using var modeling = await ClientAsync(server, "modeling", protocolVersion: "2025-11-25");

        await lighting.CallToolAsync("blender_enable_skill", new Dictionary<string, object?> { ["name"] = "rendering" });
        var lit = (await lighting.ListToolsAsync()).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var modelled = (await modeling.ListToolsAsync()).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("blender_render_settings", lit);
        Assert.DoesNotContain("blender_render_settings", modelled);
        Assert.Equal(BaseToolNamesOfAssembly(), modelled);
    }

    /// <summary>A client of the sessionless revision holds no MCP session, so its skills are known by its name: a skill one client loads is in
    /// its list on its next connection too, and in no other client's.</summary>
    [Fact]
    public async Task Server_OverHttp_KeepsTheSkillsOfASessionlessClient_ByItsName()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        await using var lighting = await ClientAsync(server, "lighting");
        await using var modeling = await ClientAsync(server, "modeling");

        await lighting.CallToolAsync("blender_enable_skill", new Dictionary<string, object?> { ["name"] = "rendering" });
        await using var lightingAgain = await ClientAsync(server, "lighting");
        var lit = (await lightingAgain.ListToolsAsync()).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var modelled = (await modeling.ListToolsAsync()).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.True(string.IsNullOrEmpty(lighting.SessionId));
        Assert.Contains("blender_render_settings", lit);
        Assert.DoesNotContain("blender_render_settings", modelled);
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

    /// <summary>A call that spells a parameter wrong is told which one, instead of being told that something went wrong.</summary>
    /// <remarks>The names here are ones models really sent — program for code, query and intent for task — and each cost a round trip, because
    /// binding the call threw inside the SDK and the caller read only "An error occurred invoking …". <c>find_tool</c> now takes query and intent
    /// for its task.</remarks>
    [Fact]
    public async Task ACallWithTheWrongParameterName_IsToldWhichNameTheToolTakes()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        await using var client = await ClientAsync(server, agent: "lighting");

        var guessed = await client.CallToolAsync("run_program", new Dictionary<string, object?> { ["program"] = "return 1;" });
        var queried = await client.CallToolAsync("find_tool", new Dictionary<string, object?> { ["query"] = "render four frames in the background" });
        var intended = await client.CallToolAsync("find_tool", new Dictionary<string, object?> { ["intent"] = "render four frames in the background" });
        var answer = guessed.Content.OfType<TextContentBlock>().First().Text;

        Assert.True(guessed.IsError);
        Assert.Contains("code", answer, StringComparison.Ordinal);
        Assert.Contains("'program'", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("An error occurred invoking", answer, StringComparison.Ordinal);
        Assert.NotEqual(true, queried.IsError);
        Assert.NotEqual(true, intended.IsError);
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

        Assert.True(keys.TryRevoke("lighting"));

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

    /// <summary>A key that reads, writes and queues renders may upload a file and run a sequence or a program, and is still refused what is
    /// delete work inside them: a destroying command carried in a program, or an upload that replaces a file.</summary>
    /// <remarks>The tools that carry commands are annotated destructive because they can carry anything, and that annotation used to be read
    /// as the scope: such a key could not upload a scene or run a program that only read one, and its holder was asked for delete — the scope
    /// that lets a key remove other people's objects and files — to do work that removes nothing. Each carried command is asked about on its own,
    /// so the carrier itself needs no more than reading.</remarks>
    [Fact]
    public async Task Key_ThatReadsWritesAndRenders_Uploads_AndRunsCarriers_ButIsRefusedWhatDestroys()
    {
        await using var server = await HttpServerProcess.StartAsync(Token);
        var keys = new ClientKeys(new ServerConfig { DataDirectory = server.DataDirectory }, TimeProvider.System);
        var (_, secret) = keys.Mint("a", [Scopes.Read, Scopes.Write, Scopes.Farm]);
        await using var client = await ClientAsync(server, agent: "a", key: secret);

        static string Text(CallToolResult result) => result.Content.OfType<TextContentBlock>().First().Text;

        var upload = await client.CallToolAsync("blender_upload", new Dictionary<string, object?> { ["localPath"] = "scene.blend", ["remotePath"] = "office/scene.blend" });
        var replace = await client.CallToolAsync("blender_upload", new Dictionary<string, object?> { ["localPath"] = "scene.blend", ["remotePath"] = "office/scene.blend", ["overwrite"] = true });
        var reading = await client.CallToolAsync("run_program", new Dictionary<string, object?> { ["code"] = "return blender.scene_info({});" });
        var destroying = await client.CallToolAsync("run_program", new Dictionary<string, object?> { ["code"] = "return blender.delete_objects({names: ['Cube']});" });
        var sequence = await client.CallToolAsync("run_tools", new Dictionary<string, object?>
        {
            ["commands"] = new JsonArray(new JsonObject { ["command"] = "scene_info", ["params"] = new JsonObject() }),
        });

        Assert.NotEqual(true, upload.IsError);
        Assert.Contains("curl", Text(upload), StringComparison.Ordinal);
        Assert.True(replace.IsError);
        Assert.Contains("delete work", Text(replace), StringComparison.Ordinal);
        Assert.DoesNotContain("does not open it", Text(reading), StringComparison.Ordinal);
        Assert.DoesNotContain("does not open it", Text(sequence), StringComparison.Ordinal);
        Assert.Contains("delete", Text(destroying), StringComparison.Ordinal);
        Assert.Contains("does not open", Text(destroying), StringComparison.Ordinal);

        using var http = new HttpClient { BaseAddress = server.Address };
        var overwriting = Signed(HttpMethod.Put, "/raw/files/office/scene.blend?overwrite=true", secret);

        Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(overwriting)).StatusCode);
    }

    /// <summary>A container started without its secret would turn every client away while looking healthy, so it refuses to start and names the setting.</summary>
    [Fact]
    public async Task Server_OverHttpWithoutAToken_ExitsBeforeListening_NamingTheSetting()
    {
        var dataDirectory = TestFolder.Create();

        var refused = await Assert.ThrowsAsync<ServerExitedException>(() =>
            HttpServerProgram.StartAsync("Snail.MCP.Blender", "SNAIL_BLENDER_SERVER", HttpServerProcess.Environment(dataDirectory, token: null), AddressVariable));

        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("SNAIL_MCP_BLENDER_HTTP__TOKEN", Assert.Single(Lines(refused.Stderr)), StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", refused.Stderr, StringComparison.Ordinal);
        Directory.Delete(dataDirectory, recursive: true);
    }

    /// <summary><c>SNAIL_MCP_BLENDER_PORT</c> alone asks for HTTP, so a server started with it and no token refuses to start like one that names
    /// its transport, instead of listening with every check thinking it spoke stdio.</summary>
    [Fact]
    public async Task Server_OverHttpByItsPortAlone_WithoutAToken_ExitsBeforeListening_NamingTheSetting()
    {
        var dataDirectory = TestFolder.Create();
        var environment = HttpServerProcess.Environment(dataDirectory, token: null);
        environment[TransportVariable] = null;

        var refused = await Assert.ThrowsAsync<ServerExitedException>(() =>
            HttpServerProgram.StartAsync("Snail.MCP.Blender", "SNAIL_BLENDER_SERVER", environment, PortVariable));

        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("SNAIL_MCP_BLENDER_HTTP__TOKEN", Assert.Single(Lines(refused.Stderr)), StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", refused.Stderr, StringComparison.Ordinal);
        Directory.Delete(dataDirectory, recursive: true);
    }

    /// <summary>A server reached over HTTP by its port alone tells the client it is reached over HTTP, and has no project folder of the client's.</summary>
    [Fact]
    public async Task Server_OverHttpByItsPortAlone_DiagnosesItselfAsHttp()
    {
        var dataDirectory = TestFolder.Create();
        var environment = HttpServerProcess.Environment(dataDirectory, Token);
        environment[TransportVariable] = null;
        await using var program = await HttpServerProgram.StartAsync("Snail.MCP.Blender", "SNAIL_BLENDER_SERVER", environment, PortVariable);
        await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(program.Address, HttpServerRegistrationExtensions.McpPath),
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {Token}" },
        }));

        var diagnosed = await client.CallToolAsync("blender_diagnose");

        Assert.Equal("http", diagnosed.Data()!["transport"]!["kind"]!.ToString());
        Assert.Equal(Messages.HttpProjectNote, diagnosed.Data()!["project"]!["note"]!.ToString());
        Directory.Delete(dataDirectory, recursive: true);
    }

    /// <summary>An address the server cannot listen on stops the start in one line that names the variable, with exit code 1 and no trace.</summary>
    [Fact]
    public async Task Server_AtAHostName_IsRefusedInOneLine_NamingTheVariable()
    {
        var dataDirectory = TestFolder.Create();
        var environment = HttpServerProcess.Environment(dataDirectory, Token);
        environment["SNAIL_MCP_BLENDER_URL"] = "http://blender.example.com:8080";

        var refused = await Assert.ThrowsAsync<ServerExitedException>(() =>
            HttpServerProgram.StartAsync("Snail.MCP.Blender", "SNAIL_BLENDER_SERVER", environment, AddressVariable));

        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("SNAIL_MCP_BLENDER_URL", Assert.Single(Lines(refused.Stderr)), StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", refused.Stderr, StringComparison.Ordinal);
        Directory.Delete(dataDirectory, recursive: true);
    }

    private static string[] Lines(string stderr) => stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static StringContent JsonBody() => new("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", MediaTypeHeaderValue.Parse("application/json"));

    private static HttpRequestMessage Signed(HttpMethod method, string path, string key) =>
        new(method, path) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", key) }, Content = new ByteArrayContent([1, 2, 3]) };

    /// <summary>A client that says who it is, the way a studio tells two of them apart; with a key of its own, the key is what names it.</summary>
    private static Task<McpClient> ClientAsync(HttpServerProcess server, string agent, string? key = null, string? protocolVersion = null) =>
        McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(server.Address, HttpServerRegistrationExtensions.McpPath),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {key ?? Token}",
                    [TransportRegistrationExtensions.AgentHeader] = agent,
                },
            }),
            new McpClientOptions { ProtocolVersion = protocolVersion });

    private static HashSet<string> BaseToolNamesOfAssembly() => ServerTools.BaseNames();

    /// <summary>The server started as a separate process in HTTP mode, on a free loopback port, with no Blender behind it.</summary>
    /// <remarks>Started by Snail.Sdk's <see cref="HttpServerProgram"/>, which waits for <c>/healthz</c>; the address goes into
    /// <c>SNAIL_MCP_BLENDER_HTTP__URL</c>, the variable a container sets, and the data directory is the test's own so a test can mint a
    /// client key into it while the server runs.</remarks>
    private sealed class HttpServerProcess : IAsyncDisposable
    {
        private readonly HttpServerProgram _program;

        private HttpServerProcess(HttpServerProgram program, string dataDirectory)
        {
            _program = program;
            DataDirectory = dataDirectory;
        }

        public Uri Address => _program.Address;

        /// <summary>Where this server keeps its state, so a test can mint a client key into it while it runs.</summary>
        public string DataDirectory { get; }

        public static async Task<HttpServerProcess> StartAsync(string? token)
        {
            var dataDirectory = TestFolder.Create();
            var program = await HttpServerProgram.StartAsync("Snail.MCP.Blender", "SNAIL_BLENDER_SERVER", Environment(dataDirectory, token), AddressVariable);

            return new HttpServerProcess(program, dataDirectory);
        }

        /// <summary>The variables of a server with its own data directory, an add-on port nothing listens on, and the token when one is given.</summary>
        public static Dictionary<string, string?> Environment(string dataDirectory, string? token) => new()
        {
            ["SNAIL_MCP_BLENDER_DATA_DIRECTORY"] = dataDirectory,
            ["SNAIL_MCP_BLENDER_BRIDGE__PORT"] = Ports.Free().ToString(),
            ["SNAIL_MCP_BLENDER_HTTP__TOKEN"] = token,
        };

        public async ValueTask DisposeAsync()
        {
            await _program.DisposeAsync();

            if (Directory.Exists(DataDirectory))
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
        }
    }
}
