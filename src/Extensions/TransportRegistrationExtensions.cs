using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools;
using Snail.MCP.Blender.Tools.Base;
using Snail.MCP.Blender.Web.Access;
using Snail.MCP.Blender.Tools.Prompts;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Hosting.Discovery;
using Snail.Sdk.Mcp.Hosting.Jobs;
using Snail.Sdk.Mcp.Hosting.Sessions;
using Snail.Sdk.Mcp.Programs;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Driving adapter: tools reach the model over stdio or HTTP, guidance rides along with initialize.</summary>
public static class TransportRegistrationExtensions
{
    /// <summary>What a client calls itself over HTTP, which becomes the agent name on its commands.</summary>
    public const string AgentHeader = "X-Snail-Agent";

    /// <summary>Tasks in this server's own words, shown as examples in the description of <c>find_tool</c>'s task.</summary>
    private static readonly string[] DiscoveryExamples = ["bevel the edges of a mesh", "warm key light on the subject", "render four frames in the background"];

    /// <summary>The command schema the add-on ships, which names the parameters each command reads.</summary>
    private static readonly Lazy<CommandSemantics> Schema = new(() => new CommandSemantics());

    extension(IServiceCollection services)
    {
        /// <summary>Registers the stdio MCP server with every tool; a skill's tools are listed once the session loads it.</summary>
        public IServiceCollection AddMcpTransport(ServerConfig config)
        {
            services.AddMcpServerWithTools(config).WithStdioServerTransport();

            return services;
        }

        /// <summary>Registers the same server over Streamable HTTP.</summary>
        /// <remarks>A client that opens with initialize keeps a session, which is what carries tools/list_changed when a skill loads; a client of
        /// the sessionless protocol revision is served per request. Its listen stream is held open by Snail.Sdk.Mcp.Hosting and carries what it
        /// asked for — the updates of the job resources, and tools/list_changed when a skill loads: Claude Code reads the end of that stream as the
        /// end of the connection.
        /// The request is read for the key it presented, which names the client and the scopes the gate asks about.</remarks>
        public IServiceCollection AddMcpHttpTransport(ServerConfig config)
        {
            services.AddHttpContextAccessor();
            services.AddMcpServerWithTools(config).WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.StatefulForInitializeClients);

            return services;
        }

        /// <summary>The one server both transports carry: its name and version, the guidance, the tools, the prompts, the resources, the client
        /// each request is served for, the gate, the argument check, discovery, programs and skills.</summary>
        /// <remarks>The client is served by filters registered before the gate's, so the door, the tool list and the skill tools all see the
        /// client of the request when they ask the gate. The server names itself by its command and the informational version, as
        /// blender_diagnose does.</remarks>
        private IMcpServerBuilder AddMcpServerWithTools(ServerConfig config) =>
            services
                .AddMcpServer(server =>
                {
                    server.ServerInfo = ServerIdentity.Of(typeof(TransportRegistrationExtensions).Assembly);
                    server.ServerInstructions = ServerGuidance.Instructions;
                })
                .ExplainFailures()
                .WithBlenderTools()
                .WithPrompts<PipelinePrompts>()
                .WithResources<BlenderResources>()
                .WithJobs(ServerPaths.ShortName)
                .WithRequestFilters(filters =>
                {
                    filters.AddCallToolFilter(ServingTheCall);
                    filters.AddListToolsFilter(ServingTheList);
                })
                .WithSessionGrant(Granting)
                .WithArgumentCheck()
                .WithToolDiscovery(Discovering)
                .WithPrograms<ProgramCommands>(Programming)
                .WithSkills(skills => Loading(skills, config));

        /// <summary>How a call the client's key does not open is refused: the gate's words, the fleet's type, and this server's hint, which names
        /// the command that mints a key — one hint for the door, for <c>run_tools</c> and <c>blender_batch</c>, and for a program's command.</summary>
        internal static void Granting(GrantOptions grant) =>
            grant.HintOf = (_, _) => Messages.ScopeHint;

        /// <summary>How <c>find_tool</c> reads this server's tools: the prefix and the words of the Blender API that say nothing, the command a
        /// program sends for a tool, and the examples of a task.</summary>
        internal static void Discovering(DiscoveryOptions discovery)
        {
            discovery.Prefix = BridgeCommands.ToolPrefix;
            discovery.Noise = new HashSet<string>(["bpy", "ops"], StringComparer.OrdinalIgnoreCase);
            discovery.CommandOf = BridgeCommands.CommandOf;
            discovery.ParameterOf = ParameterOf;
            discovery.Examples = DiscoveryExamples;
        }

        /// <summary>What a tool's command calls one of the tool's parameters, or a field of one, by its dotted path in the schema's names: every
        /// segment in snake_case, the way the add-on reads it, or none when the command schema lists the command without it —
        /// <c>blender_journal</c>'s <c>agent</c> goes out as <c>author</c>, so a program written from <c>find_tool</c>'s answer is not handed a
        /// name the add-on would ignore.</summary>
        /// <remarks>A block the command takes under its own name travels whole, its fields snake_case inside it (<see cref="Block.ToWire"/>):
        /// <c>blender_set_camera_optics</c>'s <c>focus.distance</c> stays <c>focus.distance</c>. A block the command does not take is one the
        /// tool spreads over the command's own parameters, so its field answers by the top-level name it goes out under, or none:
        /// <c>blender_render_job</c>'s <c>output.fileFormat</c> is <c>file_format</c> and <c>queue.chunks</c> is <c>chunks</c>, while
        /// <c>output</c> and <c>queue</c> themselves go to no command.</remarks>
        internal static string? ParameterOf(string tool, string path)
        {
            var names = path.Split('.').Select(JsonNamingPolicy.SnakeCaseLower.ConvertName).ToArray();

            if (BridgeCommands.CommandOf(tool) is not { } command)
            {
                return string.Join('.', names);
            }

            if (names.Length == 1)
            {
                return Schema.Value.Takes(command, names[0]) is false ? null : names[0];
            }

            if (Schema.Value.Takes(command, names[0]) is not false)
            {
                return string.Join('.', names);
            }

            return Schema.Value.Takes(command, names[^1]) is false ? null : names[^1];
        }

        /// <summary>How skills load: by the client's name over HTTP, for <see cref="ServerConfig.SkillIdleMinutes"/> unused, and the farm kept
        /// while a background render it follows runs.</summary>
        /// <remarks>A client of the sessionless revision holds no MCP session, so without its name every such client would share one set of skills.
        /// A render outlives any idle time the server is configured with — hours of frames against minutes — and the model still has to poll or
        /// cancel it with the farm's tools.</remarks>
        internal static void Loading(SkillOptions skills, ServerConfig config)
        {
            skills.Prefix = ServerPaths.ShortName;
            skills.IdleTimeout = TimeSpan.FromMinutes(config.SkillIdleMinutes);
            skills.SessionOf = (provider, _) => SkillsOf(provider);
            skills.IsBusy = (skill, provider) => skill == Skills.Farm && provider.GetService<AddOnJobs>()?.IsRendering == true;
        }

        /// <summary>How <c>run_program</c> is named and bounded here: the commands hang on <c>blender</c>, a program sends at most 200 of them,
        /// and this server's sentence says what a program is for.</summary>
        internal static void Programming(ProgramOptions programs)
        {
            programs.Global = "blender";
            programs.MaxCalls = 200;
            programs.Description = ProgramTexts.Purpose;
            programs.Ran = (run, provider) => provider.GetService<BridgeTraffic>()?.RanAProgram(run.Calls.Count);
        }

        /// <summary>The kinds of work the key this request presented opens; a request admitted some other way opens all of them, which is what
        /// a signed link and the pages already stand for.</summary>
        private static IReadOnlyList<string>? Opens(HttpContext context) =>
            context.User.FindAll(WebAccess.ScopeClaim).Select(claim => claim.Value).ToList() is { Count: > 0 } scopes ? scopes : null;

        /// <summary>Who this request is: the name on the key it presented, which is the name its commands go out under.</summary>
        /// <remarks>The name used to come from the header alone, and a header is what the caller writes: every holder of the one token could
        /// call itself anything, so the agent on a lease was a claim nobody had checked. Only the token the server was configured with still
        /// lets its holder name itself, because that is what that token could always do.</remarks>
        private static string? Whom(HttpContext context) => Named(context) ?? context.User.Identity?.Name;

        /// <summary>The name a client is known by: the one on a minted key, or the one the holder of the configured token gives itself; null for
        /// the configured token unnamed, which is anybody's.</summary>
        private static string? Named(HttpContext context) =>
            context.User.FindFirst(WebAccess.ConfiguredClaim) is null
                ? context.User.Identity?.Name
                : context.Request.Headers[AgentHeader].ToString() is { Length: > 0 } named ? named : null;

        /// <summary>Whose skills a request loads and lists: the client its key names over HTTP; over stdio, and for an unnamed holder of the
        /// configured token, null, which leaves it to the MCP session.</summary>
        private static string? SkillsOf(IServiceProvider provider) =>
            provider.GetService<IHttpContextAccessor>()?.HttpContext is { } http ? Named(http) : null;

        /// <summary>Says whose call this is for as long as it runs: the gate asks about that client, and the
        /// tools built for it, and the commands they send, are that client's.</summary>
        private static McpRequestHandler<CallToolRequestParams, CallToolResult> ServingTheCall(
            McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
            async (context, cancellationToken) =>
            {
                using var serving = Serve(context);

                return await next(context, cancellationToken).ConfigureAwait(false);
            };

        /// <summary>Says whose list this is while it is made, so the gate leaves out what the client's key does not open.</summary>
        private static McpRequestHandler<ListToolsRequestParams, ListToolsResult> ServingTheList(
            McpRequestHandler<ListToolsRequestParams, ListToolsResult> next) =>
            async (context, cancellationToken) =>
            {
                using var serving = Serve(context);

                return await next(context, cancellationToken).ConfigureAwait(false);
            };

        /// <summary>Serves the client of the request: the key it presented over HTTP, the process's own client over stdio.</summary>
        private static IDisposable? Serve(MessageContext context)
        {
            var requestServices = context.Services ?? context.Server.Services;

            if (requestServices?.GetService<ClientSessions>() is not { } clients)
            {
                return null;
            }

            var clientName = context.Server.ClientInfo?.Name;

            return requestServices.GetService<IHttpContextAccessor>()?.HttpContext is { } http
                ? clients.Serve(clientName, Whom(http), Opens(http))
                : clients.Serve(clientName);
        }
    }
}
