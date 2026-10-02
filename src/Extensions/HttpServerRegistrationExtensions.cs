using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Web.Access;
using Snail.MCP.Blender.Web.Files;
using Snail.MCP.Blender.Web.Health;
using Snail.MCP.Blender.Web.Pages;

namespace Snail.MCP.Blender.Extensions;

/// <summary>Composition root of the server a container runs: the bridge and tools of the stdio server, reached over HTTP behind the client token.</summary>
/// <remarks>No idle watchdog: over stdio it ends a server its client forgot, but a container is meant to outlive every client.</remarks>
public static class HttpServerRegistrationExtensions
{
    public const string McpPath = "/mcp";

    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder ConfigureHttpServer(IConfiguration? settings = null)
        {
            builder.AddServerConfiguration(settings ?? ServerConfigExtensions.Settings());

            builder.Services
                .AddBlenderBridge()
                .AddSkills()
                .AddWebAccess()
                .AddVolume()
                .AddMcpHttpTransport();

            return builder;
        }
    }

    extension(WebApplication app)
    {
        public WebApplication UseHttpServer()
        {
            app.Urls.Clear();
            app.Urls.Add(app.Services.GetRequiredService<ServerConfig>().Http.Url);

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();

            app.MapHealth();
            app.MapRawFiles();
            app.MapPages();
            app.MapMcp(McpPath).RequireAuthorization(WebAccess.ClientPolicy);

            return app;
        }
    }
}
