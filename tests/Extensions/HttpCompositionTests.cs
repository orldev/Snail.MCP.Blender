using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Blender.Tests.Extensions;

/// <summary>The HTTP server composed as a container runs it, read without starting it: what is built on its address names the address it listens on.</summary>
public class HttpCompositionTests
{
    /// <summary><c>SNAIL_MCP_BLENDER_URL</c> (<c>Url</c> at the root) or a <c>Port</c> moves the listener away from <c>Http:Url</c>; a link signed with
    /// <c>Http:Url</c> led to an address nothing listened on.</summary>
    [Theory]
    [InlineData("Url")]
    [InlineData("Port")]
    public void Links_WhenASettingMovesTheListener_LeadToTheAddressItListensOn(string moving)
    {
        var port = Ports.Free();
        var listening = $"http://127.0.0.1:{port}";
        var settings = Settings(new()
        {
            [moving] = moving == "Port" ? port.ToString() : listening,
            ["Http:Url"] = "http://127.0.0.1:8080",
        });
        var builder = WebApplication.CreateBuilder();
        builder.ConfigureHttpServer(settings);
        using var app = builder.Build();

        var link = app.Services.GetRequiredService<FileLinks>().For("GET", "files", "robot/shot.png");

        Assert.StartsWith($"{listening}/raw/files/robot/shot.png?", link.Url, StringComparison.Ordinal);
        Assert.Equal(listening, app.Services.GetRequiredService<ServerConfig>().Http.Url);
    }

    /// <summary>The public address of a reverse proxy still wins over the one the server listens on.</summary>
    [Fact]
    public void Links_BehindAPublicUrl_LeadToThePublicUrl()
    {
        var settings = Settings(new()
        {
            ["Url"] = $"http://127.0.0.1:{Ports.Free()}",
            ["Http:PublicUrl"] = "https://blender.example.com",
        });
        var builder = WebApplication.CreateBuilder();
        builder.ConfigureHttpServer(settings);
        using var app = builder.Build();

        var link = app.Services.GetRequiredService<FileLinks>().For("GET", "files", "robot/shot.png");

        Assert.StartsWith("https://blender.example.com/raw/files/robot/shot.png?", link.Url, StringComparison.Ordinal);
    }

    /// <summary>A <c>Port</c> alone makes the server answer over HTTP, so every check that asks the transport is told HTTP: the token is required,
    /// there is no project folder, and transfers go through signed links.</summary>
    /// <remarks>The transport was a setting of its own, Stdio unless named, while the listener was chosen from the endpoint: a server started with
    /// <c>SNAIL_MCP_BLENDER_PORT</c> alone served HTTP and checked everything as stdio, with no token asked for.</remarks>
    [Fact]
    public void Transport_WhenAPortAloneAsksForHttp_IsHttpForEveryCheck()
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Port"] = Ports.Free().ToString(),
            ["Http:Token"] = "t",
            ["DataDirectory"] = TestFolder.Create(),
        }).Build();
        var builder = WebApplication.CreateBuilder();
        builder.ConfigureHttpServer(settings);
        using var app = builder.Build();

        var config = app.Services.GetRequiredService<ServerConfig>();

        Assert.Equal(McpTransport.Http, config.Transport);
        Assert.Null(config.ProjectDirectory);
    }

    private static IConfiguration Settings(Dictionary<string, string?> values)
    {
        values["Transport"] = "Http";
        values["Http:Token"] = "t";
        values["DataDirectory"] = TestFolder.Create();

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
