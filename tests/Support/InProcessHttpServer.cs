using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snail.MCP.Blender.Extensions;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>The HTTP server composed exactly as a container runs it, on a free loopback port, in front of an add-on of the test's choosing.</summary>
internal static class InProcessHttpServer
{
    public const string Token = "in-process-token";

    public static async Task<WebApplication> StartAsync(int bridgePort, TimeProvider? time = null, string? publicUrl = null, string? bridgeTokenFile = null)
    {
        var builder = WebApplication.CreateBuilder();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Transport"] = "Http",
            ["Http:Url"] = $"http://127.0.0.1:{Ports.Free()}",
            ["Http:Token"] = Token,
            ["Http:PublicUrl"] = publicUrl,
            ["Bridge:Port"] = bridgePort.ToString(),
            ["Bridge:Token"] = bridgeTokenFile is null ? "add-on" : null,
            ["Bridge:TokenFile"] = bridgeTokenFile,
            ["DataDirectory"] = TestFolder.Create(),
        }).Build();
        builder.ConfigureHttpServer(settings);
        builder.Logging.ClearProviders();

        if (time is not null)
        {
            builder.Services.AddSingleton(time);
        }

        var app = builder.Build().UseHttpServer();
        await app.StartAsync();

        return app;
    }
}
