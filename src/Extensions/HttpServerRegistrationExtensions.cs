using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Web.Access;
using Snail.MCP.Blender.Web.Files;
using Snail.MCP.Blender.Web.Pages;
using Snail.Sdk.Mcp.Hosting;
using Snail.Sdk.Mcp.Http;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Composition root of the server a container runs: the bridge and tools of the stdio server, reached over HTTP behind the client token.</summary>
/// <remarks>No idle watchdog: over stdio it ends a server its client forgot, but a container is meant to outlive every client.</remarks>
public static class HttpServerRegistrationExtensions
{
    public const string McpPath = "/mcp";

    /// <summary>Where the liveness answer is, open to anyone.</summary>
    public const string HealthPath = McpHttpHost.DefaultHealthPath;

    /// <summary>How the endpoint is read from the settings: under the server's section as well as at the root, and anywhere the settings name.</summary>
    /// <remarks>Unrestricted, because every request to <c>/mcp</c> presents a key and a container beside Blender may be reached on a LAN address;
    /// a host name, which the address alone cannot listen on, is refused at the start. A refusal names the variable by the server's name.</remarks>
    public static McpEndpointOptions EndpointOptions { get; } = new()
    {
        FallbackSection = ServerConfig.SectionName,
        Reach = McpReach.Unrestricted,
        Server = ServerPaths.ShortName,
    };

    extension(WebApplicationBuilder builder)
    {
        /// <summary>Binds the server to <paramref name="settings"/>, or to this machine's when none are given, and hands the same settings to the host's
        /// configuration, where the stdio host's builder puts them itself; the full builder's other sources stay out of the server's settings.</summary>
        /// <remarks>The host is the full ASP.NET Core one, for the pages, the sign-in and the antiforgery the SDK's empty builders leave out;
        /// <c>UseMcpEndpoint</c> gives it the endpoint's address, the log levels and the idle trim those builders have.
        /// The address it listens on is written back into <see cref="HttpOptions.Url"/>: <c>SNAIL_MCP_BLENDER_URL</c> or a <c>Port</c> moves the
        /// listener away from <c>Http:Url</c>, and the links, the status page and the diagnosis built on that setting led to the old address.
        /// The transport it serves is written into <see cref="ServerConfig.Transport"/> the same way, before the settings are normalised and
        /// validated: a <c>Port</c> alone asks for HTTP while <c>Transport</c> stayed Stdio, and the server listened with no token asked for,
        /// the client's working folder as its project, and transfers and the diagnosis answering as over stdio.</remarks>
        public WebApplicationBuilder ConfigureHttpServer(IConfiguration? settings = null)
        {
            settings ??= ServerConfigExtensions.Settings();

            var endpoint = McpEndpoint.ReadHttp(settings, new Uri(HttpOptions.DefaultUrl), EndpointOptions);
            var listening = endpoint.Url.AbsoluteUri.TrimEnd('/');

            builder.Configuration.AddConfiguration(settings);
            builder.AddServerConfiguration(settings);
            builder.Services.Configure<ServerConfig>(config => config.Transport = endpoint.Transport);
            builder.UseMcpEndpoint(endpoint);
            builder.Services.PostConfigure<ServerConfig>(config => config.Http.Url = listening);

            builder.Services
                .AddBlenderBridge()
                .AddAccess()
                .AddWebAccess()
                .AddVolume()
                .AddMcpHttpTransport(ServerConfigExtensions.Peek(settings));

            return builder;
        }
    }

    extension(WebApplication app)
    {
        /// <summary>The pipeline of the server a container runs: a browser refused at <c>/mcp</c>, then the key asked of everyone else, then the
        /// pages, the files, the liveness answer and the MCP endpoint.</summary>
        /// <remarks>The refusal comes before the authentication, so a page is answered 403 rather than challenged with 401 like a client that forgot
        /// its key; the pages themselves post their forms with an <c>Origin</c> and stay outside it.</remarks>
        public WebApplication UseHttpServer()
        {
            app.UseBrowserRefusal(McpPath);
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();

            app.MapHealth(HealthPath, LivenessAsync);
            app.MapRawFiles();
            app.MapPages();
            app.MapMcp(McpPath).RequireAuthorization(WebAccess.ClientPolicy);

            return app;
        }
    }

    /// <summary>The liveness answer: that the server runs, always 200, and whether Blender answers it — <c>{"status":"ok","blender":"up|down|stalled"}</c>.</summary>
    /// <remarks>A Blender that is down does not make the server unhealthy: the container still serves the pages and the files, and restarting it
    /// would not bring Blender back.</remarks>
    private static async Task<HealthAnswer> LivenessAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        new(true, await services.GetRequiredService<ServerHealth>().LivenessAsync(cancellationToken).ConfigureAwait(false));
}
