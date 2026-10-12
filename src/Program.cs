using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.MCP.Blender.Hosting;
using Snail.Sdk.Mcp.Hosting;

if (ClientCommandLine.IsAsked(args))
{
    return ClientCommandLine.Run(args, Console.Out);
}

if (ServerConfigExtensions.Peek().Transport is Transport.Http)
{
    await WebApplication.CreateBuilder(args)
        .ConfigureHttpServer()
        .Build()
        .UseHttpServer()
        .RunAsync();

    return 0;
}

await McpHost.CreateBuilder(args)
    .ConfigureMcpServer()
    .Build()
    .RunAsync();

return 0;
