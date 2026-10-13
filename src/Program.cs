using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Extensions;
using Snail.MCP.Blender.Hosting;
using Snail.Sdk.Mcp.Hosting;

ServerFolders.MoveFromSnailNames(ServerPaths.ShortName);

if (ClientCommandLine.IsAsked(args))
{
    return ClientCommandLine.Run(args, Console.Out);
}

var settings = ServerConfigExtensions.Settings();

if (ServerConfigExtensions.Peek(settings).Transport is Transport.Http)
{
    await WebApplication.CreateBuilder(args)
        .ConfigureHttpServer(settings)
        .Build()
        .UseHttpServer()
        .RunAsync();

    return 0;
}

await McpHost.CreateBuilder(args)
    .ConfigureMcpServer(settings)
    .Build()
    .RunAsync();

return 0;
