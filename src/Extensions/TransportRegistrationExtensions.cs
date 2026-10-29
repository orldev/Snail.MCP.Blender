using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Discovery;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Hosting;
using Snail.MCP.Blender.Tools;
using Snail.MCP.Blender.Tools.Base;
using Snail.MCP.Blender.Web.Access;
using Snail.MCP.Blender.Tools.Prompts;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Hosting.Jobs;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Driving adapter: tools reach the model over stdio or HTTP, guidance rides along with initialize.</summary>
public static class TransportRegistrationExtensions
{
    /// <summary>What a client calls itself over HTTP, which becomes the agent name on its commands.</summary>
    public const string AgentHeader = "X-Snail-Agent";

    extension(IServiceCollection services)
    {
        /// <summary>Registers the stdio MCP server with the base tools; skills join the collection later. Each call marks the server and the tool's skill as active.</summary>
        public IServiceCollection AddMcpTransport()
        {
            services.AddMcpServerWithTools().WithStdioServerTransport();

            return services;
        }

        /// <summary>Registers the same server over Streamable HTTP.</summary>
        /// <remarks>A client that opens with initialize keeps a session, which is what carries tools/list_changed when a skill loads; a client of the
        /// sessionless protocol revision is served per request and has no session to be notified on, so the tools that load or unload a skill
        /// tell it in the reply to the call. Its listen stream is held open by the jobs' registration (Snail.Sdk.Mcp.Hosting), which carries the
        /// updates of the job resources it asked for and nothing else: Claude Code reads the end of that stream as the end of the connection.</remarks>
        public IServiceCollection AddMcpHttpTransport()
        {
            services.AddMcpServerWithTools().WithHttpTransport(options =>
            {
                options.SessionMode = HttpServerSessionMode.StatefulForInitializeClients;
                options.ConfigureSessionOptions = OpenASessionAsync;
            });
            return services;
        }

        /// <summary>The one server both transports carry: its name and version, the guidance, the base tools, the prompts, the resources and the
        /// activity filter.</summary>
        /// <remarks>The server names itself by its command and the informational version, as blender_diagnose does: left to the SDK, it reports
        /// the assembly's name and version, which reads 0.2.0.0 for the release 0.2.0 and drops the suffix of a prerelease.</remarks>
        private IMcpServerBuilder AddMcpServerWithTools() =>
            services
                .AddMcpServer(server =>
                {
                    server.ServerInfo = ServerIdentity.Of(typeof(ThisAssembly).Assembly);
                    server.ServerInstructions = ServerGuidance.Instructions;
                })
                .WithBaseTools()
                .WithPrompts<PipelinePrompts>()
                .WithResources<BlenderResources>()
                .WithJobs("blender")
                .WithRequestFilters(filters => filters.AddCallToolFilter(TrackActivity));

        /// <summary>Every session and every sessionless request gets options of its own, and with them a client of its own: a tool collection
        /// holding the always-on tools, and the name its commands are signed with.</summary>
        /// <remarks>The collection used to be the one the process holds, handed to every session, so a skill one client loaded appeared in the
        /// tool list of all of them and expiry took it away from all of them. A client says who it is with <c>X-Snail-Agent</c>; one that says
        /// nothing is named after the client name it sends with initialize, which is what the add-on's leases and journal then record.</remarks>
        private static Task OpenASessionAsync(HttpContext context, McpServerOptions session, CancellationToken cancellationToken)
        {
            context.RequestServices.GetRequiredService<ClientSessions>().Open(session, Whom(context), Opens(context));

            return Task.CompletedTask;
        }

        /// <summary>Who this request is: the name on the key it presented, which is the name its commands go out under.</summary>
        /// <remarks>The name used to come from the header alone, and a header is what the caller writes: every holder of the one token could
        /// call itself anything, so the agent on a lease was a claim nobody had checked. Only the token the server was configured with still
        /// lets its holder name itself, because that is what that token could always do.</remarks>
        /// <summary>The kinds of work the key this request presented opens; a request admitted some other way opens all of them, which is what
        /// a signed link and the pages already stand for.</summary>
        private static IReadOnlyList<string>? Opens(HttpContext context) =>
            context.User.FindAll(WebAccess.ScopeClaim).Select(claim => claim.Value).ToList() is { Count: > 0 } scopes ? scopes : null;

        private static string? Whom(HttpContext context) =>
            context.User.FindFirst(WebAccess.ConfiguredClaim) is null
                ? context.User.Identity?.Name
                : context.Request.Headers[AgentHeader].ToString() is { Length: > 0 } named ? named : context.User.Identity?.Name;

        /// <summary>Marks the server and the calling client's skill as active, says whose call this is for as long as it runs — the tools built
        /// for it, and the commands they send, are that client's — turns the call away when the client's key does not open work of its kind,
        /// and names the parameter a call got wrong before the SDK hides it.</summary>
        /// <remarks>Both refusals are here rather than in each tool: the first is about the caller and the second about the shape of the call,
        /// and a tool that had to ask would be a tool that could forget to. The key is asked about first, because what a client may not do at
        /// all it should not learn to spell.</remarks>
        private static McpRequestHandler<CallToolRequestParams, CallToolResult> TrackActivity(
            McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
            async (context, cancellationToken) =>
            {
                context.Services?.GetService<IActivityTracker>()?.Touch();

                var clients = context.Services?.GetService<ClientSessions>();
                using var serving = clients?.Serve(context.Server);

                if (clients is not null
                    && context.Services?.GetService<ToolScopes>() is { } scopes
                    && context.MatchedPrimitive is McpServerTool tool
                    && scopes.For(tool) is { } scope
                    && !clients.Current.May(scope))
                {
                    return ToolResponse.Failure(ToolScopes.Refusal(tool.ProtocolTool.Name, scope, clients.Current.Name), Messages.ScopeHint);
                }

                if (context.MatchedPrimitive is McpServerTool called
                    && CallArguments.Refusal(called.ProtocolTool.Name, called.ProtocolTool.InputSchema, context.Params?.Arguments?.Keys) is { } wrong)
                {
                    return ToolResponse.Failure(wrong, Messages.ArgumentsHint);
                }

                clients?.Current.Skills.Touch(context.Params?.Name);

                return await next(context, cancellationToken);
            };
    }
}
