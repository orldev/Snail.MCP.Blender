using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using ModelContextProtocol.Protocol;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>The listen stream a client of the 2026-07-28 revision opens beside its first requests, asked over raw HTTP the way Claude Code
/// asks it.</summary>
/// <remarks>The stream used to be acknowledged with nothing and closed at once. Claude Code read the close as the end of the connection and
/// dropped the tools/list still in flight: a quarter of its sessions with this server had no Blender tool at all.</remarks>
public sealed class SessionlessListenTests
{
    private const string Revision = "2026-07-28";

    private static readonly TimeSpan Held = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task ListenStream_OfASessionlessClient_StaysOpen_WhileToolsListAnswersBesideIt()
    {
        await using var app = await InProcessHttpServer.StartAsync(bridgePort: 1);
        using var http = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        using var listen = Request(app, RequestMethods.SubscriptionsListen, new JsonObject { ["notifications"] = new JsonObject { ["toolsListChanged"] = true } });
        using var tools = Request(app, RequestMethods.ToolsList, new JsonObject());

        using var listened = await http.SendAsync(listen, HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await listened.Content.ReadAsStreamAsync());
        var acknowledgement = await NextMessageAsync(reader, CancellationToken.None);
        using var listed = await http.SendAsync(tools);
        var listedTools = JsonNode.Parse(Data(await listed.Content.ReadAsStringAsync()))!["result"]!["tools"]!.AsArray();
        var ending = await EndingWithinAsync(reader, Held);

        Assert.Equal(NotificationMethods.SubscriptionsAcknowledgedNotification, acknowledgement["method"]!.ToString());
        Assert.Empty(acknowledgement["params"]!["notifications"]!.AsObject());
        Assert.Equal(1, acknowledgement["params"]!["_meta"]![ModelContextProtocol.Protocol.MetaKeys.SubscriptionId]!.GetValue<int>());
        Assert.NotEmpty(listedTools);
        Assert.Null(ending);
    }

    /// <summary>A held stream does not hold the server: stopping it ends the stream rather than waiting out the shutdown timeout.</summary>
    [Fact]
    public async Task ListenStream_WhenTheServerStops_Ends()
    {
        var app = await InProcessHttpServer.StartAsync(bridgePort: 1);
        using var http = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        using var listen = Request(app, RequestMethods.SubscriptionsListen, new JsonObject { ["notifications"] = new JsonObject { ["toolsListChanged"] = true } });
        using var listened = await http.SendAsync(listen, HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await listened.Content.ReadAsStreamAsync());
        await NextMessageAsync(reader, CancellationToken.None);

        var clock = Stopwatch.StartNew();
        await app.StopAsync();
        await app.DisposeAsync();

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"stopping took {clock.Elapsed}");
    }

    private static HttpRequestMessage Request(Microsoft.AspNetCore.Builder.WebApplication app, string method, JsonObject parameters)
    {
        parameters["_meta"] = new JsonObject
        {
            [ModelContextProtocol.Protocol.MetaKeys.ProtocolVersion] = Revision,
            [ModelContextProtocol.Protocol.MetaKeys.ClientInfo] = new JsonObject { ["name"] = "listen-test", ["version"] = "1" },
            [ModelContextProtocol.Protocol.MetaKeys.ClientCapabilities] = new JsonObject(),
        };
        var body = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = parameters };
        var request = new HttpRequestMessage(HttpMethod.Post, $"{app.Urls.First()}/mcp") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", InProcessHttpServer.Token);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", Revision);
        request.Headers.Add("Mcp-Method", method);

        return request;
    }

    private static async Task<JsonNode> NextMessageAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                return JsonNode.Parse(line["data:".Length..])!;
            }
        }

        throw new EndOfStreamException("the stream ended before a message");
    }

    /// <summary>What ended the stream within the wait, or null when it was still open at the end of it.</summary>
    private static async Task<string?> EndingWithinAsync(StreamReader reader, TimeSpan wait)
    {
        using var patience = new CancellationTokenSource(wait);

        try
        {
            var next = await reader.ReadLineAsync(patience.Token);

            return next is null ? "the stream ended" : await EndingWithinAsync(reader, wait);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static string Data(string body) =>
        body.Split('\n').FirstOrDefault(line => line.StartsWith("data:", StringComparison.Ordinal)) is { } data ? data["data:".Length..] : body;
}
