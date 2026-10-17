using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Application.Sessions;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Adapters;

/// <summary>The links as one: which socket a command goes down, and whose name it goes out under.</summary>
[Collection("Loopback")]
public class BlenderLinksTests
{
    /// <summary>One server over HTTP holds one link to the add-on, so the name on a command has to be the calling client's; signed from the
    /// configuration alone, two clients took each other's leases and the journal recorded one worker doing everything.</summary>
    [Fact]
    public async Task Send_WhileServingAClient_SignsTheRequestWithThatClientsName()
    {
        await using var addOn = FakeAddOn.Echo();
        var sessions = Sessions("the-server");
        await using var links = new BlenderLinks(Link(addOn.Port), NullLogger<BlenderConnection>.Instance, sessions);
        var lighting = sessions.Open(new McpServerOptions(), "lighting");

        var byTheServer = await links.SendAsync(BridgeCommands.SceneInfo);
        BridgeReply byTheClient;

        using (sessions.Serve(lighting))
        {
            byTheClient = await links.SendAsync(BridgeCommands.SceneInfo);
        }

        Assert.Equal("the-server", byTheServer.Result!["params"]!["agent"]!.ToString());
        Assert.Equal("lighting", byTheClient.Result!["params"]!["agent"]!.ToString());
    }

    /// <summary>Each channel is a socket of its own, so the add-on sees three connections rather than one.</summary>
    [Fact]
    public async Task Commands_OfDifferentChannels_GoDownDifferentSockets()
    {
        await using var addOn = new FakeAddOn(request => Task.FromResult<string?>(
            new JsonObject { ["id"] = request["id"]?.DeepClone(), ["ok"] = true, ["result"] = new JsonObject() }.ToJsonString()), isConcurrent: true);
        await using var links = new BlenderLinks(Link(addOn.Port), NullLogger<BlenderConnection>.Instance);

        await links.SendAsync(BridgeCommands.SceneInfo);
        await links.SendAsync(BridgeCommands.Ping);
        await links.SendAsync(BridgeCommands.FileList, new JsonObject { ["area"] = VolumeAreas.Files, ["path"] = "renders" });
        await links.SendAsync(BridgeCommands.SceneInfo);

        Assert.Equal(3, addOn.Connections);
    }

    /// <summary>The heartbeat and the progress probe go down the monitor socket: on the control link they would queue behind the render they are asking about.</summary>
    [Fact]
    public async Task TheMonitor_AsksForProgressAndPings_OverItsOwnSocket()
    {
        await using var addOn = new FakeAddOn(request => Task.FromResult<string?>(
            new JsonObject { ["id"] = request["id"]?.DeepClone(), ["ok"] = true, ["result"] = new JsonObject { ["command"] = request["command"]?.DeepClone(), ["params"] = request["params"]?.DeepClone() } }.ToJsonString()),
            isConcurrent: true);
        await using var links = new BlenderLinks(Link(addOn.Port), NullLogger<BlenderConnection>.Instance);
        var monitor = new BlenderMonitor(links);

        var progress = await monitor.ProbeAsync();
        var ping = await monitor.PingAsync(new JsonObject { ["since"] = 4 });

        Assert.Equal(BridgeCommands.Progress.Name, progress.Result!["command"]!.ToString());
        Assert.Equal(BridgeCommands.Ping.Name, ping.Result!["command"]!.ToString());
        Assert.Equal(4, ping.Result["params"]!["since"]!.GetValue<int>());
        Assert.Equal(1, addOn.Connections);
    }

    private static BlenderLinkOptions Link(int port) =>
        new() { Port = port, ConnectTimeoutSeconds = 2, RequestTimeoutSeconds = 5, Agent = "the-server" };

    private static ClientSessions Sessions(string? agent) =>
        new([], [], agent, new ServiceCollection().BuildServiceProvider(), TimeProvider.System);
}
