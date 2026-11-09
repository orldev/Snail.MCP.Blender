using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.MCP.Blender.Hosting;
using Snail.Sdk.Mcp.Hosting;

try
{
    if (ClientCommandLine.IsAsked(args))
    {
        return ClientCommandLine.Run(args, Console.Out);
    }

    var settings = ServerConfigExtensions.Settings();
    var endpoint = McpEndpoint.Read(settings, new Uri(HttpOptions.DefaultUrl), HttpServerRegistrationExtensions.EndpointOptions);

    if (endpoint.Transport is McpTransport.Http)
    {
        await WebApplication.CreateBuilder(args)
            .ConfigureHttpServer(settings)
            .Build()
            .UseHttpServer()
            .RunAsync();

        return 0;
    }

    await McpHost.CreateBuilder(args, settings)
        .ConfigureMcpServer(settings)
        .Build()
        .RunAsync();

    return 0;
}
catch (StartRefusedException refused)
{
    await Console.Error.WriteLineAsync(refused.Message);

    return 1;
}
