using System.Text;
using ModelContextProtocol.Protocol;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Tools.Base;

namespace Snail.MCP.Blender.Tests.Tools;

/// <summary>What a client reads without calling a tool: the last picture and the add-on's journal.</summary>
public class ResourcesTests
{
    private readonly ScriptedBridge _bridge = new();

    private readonly RenderGallery _gallery = new();

    [Fact]
    public async Task Journal_AsksTheAddOn_AndComesBackAsJsonUnderItsOwnUri()
    {
        _bridge.Answer(BridgeCommands.Journal, new JsonObject { ["entries"] = new JsonArray(new JsonObject { ["command"] = "add_primitive" }) });
        var resources = new BlenderResources(_bridge, _gallery);

        var contents = (TextResourceContents)(await resources.JournalAsync()).Contents[0];

        Assert.Equal(BridgeCommands.Journal, _bridge.Sent.Single().Command);
        Assert.Equal(200, _bridge.LastParameters!["limit"]!.GetValue<int>());
        Assert.Equal(BlenderResources.JournalUri, contents.Uri);
        Assert.Contains("add_primitive", contents.Text, StringComparison.Ordinal);
    }

    /// <summary>A journal the add-on refuses is still an answer the client can read, not an empty resource.</summary>
    [Fact]
    public async Task Journal_WhenBlenderDoesNotAnswer_CarriesTheFailureAndItsHint()
    {
        _bridge.Fail(BridgeCommands.Journal, BridgeError.Unavailable("127.0.0.1", 8765, "connection refused"));
        var resources = new BlenderResources(_bridge, _gallery);

        var contents = (TextResourceContents)(await resources.JournalAsync()).Contents[0];

        Assert.Contains("not answering", contents.Text, StringComparison.Ordinal);
        Assert.Contains("blender_install_addon", contents.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void LastRender_BeforeAnythingWasRendered_SaysSo_AndNamesTheToolsThatMakeOne()
    {
        var resources = new BlenderResources(_bridge, _gallery);

        var contents = (TextResourceContents)resources.LastRender().Contents[0];

        Assert.Contains("No render", contents.Text, StringComparison.Ordinal);
        Assert.Contains("blender_render_image", contents.Text, StringComparison.Ordinal);
    }

    /// <summary>The blob carries the base64 text as UTF-8 bytes, not the decoded picture: a client that decodes the blob gets the text back.</summary>
    [Fact]
    public void LastRender_AfterARenderReturnedAPicture_CarriesItAsABlobOfItsOwnMediaType()
    {
        var base64 = Convert.ToBase64String("a picture"u8);
        _gallery.Remember(BridgeReply.Ok(new JsonObject
        {
            ["path"] = "/renders/shot.png",
            ["preview"] = new JsonObject { ["base64"] = base64, ["mime"] = "image/png" },
        }));
        var resources = new BlenderResources(_bridge, _gallery);

        var contents = (BlobResourceContents)resources.LastRender().Contents[0];

        Assert.Equal("image/png", contents.MimeType);
        Assert.Equal(BlenderResources.LastRenderUri, contents.Uri);
        Assert.Equal(base64, Encoding.UTF8.GetString(contents.Blob.Span));
    }
}
