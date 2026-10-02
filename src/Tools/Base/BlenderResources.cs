using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Rendering;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>What a client can read without calling a tool: the last picture a render returned and the add-on's journal.</summary>
/// <remarks>A client with resource support shows the picture in its own viewer and follows the journal as it grows, at no cost
/// in context; the tools stay the way the model gets the same things. Like the image block, the blob carries the base64 text
/// as UTF-8 bytes, not the decoded picture.</remarks>
[McpServerResourceType]
public sealed class BlenderResources(IBlenderBridge bridge, RenderGallery gallery)
{
    public const string LastRenderUri = "snail://renders/last";

    public const string JournalUri = "snail://journal";

    [McpServerResource(UriTemplate = LastRenderUri, Name = "last_render", Title = "Last render", MimeType = "image/jpeg")]
    [Description(ToolDescriptions.Resources.LastRender)]
    public ReadResourceResult LastRender() =>
        gallery.Last is { } picture
            ? new ReadResourceResult { Contents = [new BlobResourceContents { Uri = LastRenderUri, MimeType = picture.MimeType, Blob = Encoding.UTF8.GetBytes(picture.Base64) }] }
            : new ReadResourceResult { Contents = [new TextResourceContents { Uri = LastRenderUri, MimeType = "application/json", Text = ToolResponse.Error(Messages.NoRenderYet, Messages.NoRenderYetHint) }] };

    [McpServerResource(UriTemplate = JournalUri, Name = "journal", Title = "Journal", MimeType = "application/json")]
    [Description(ToolDescriptions.Resources.Journal)]
    public async Task<ReadResourceResult> JournalAsync(CancellationToken cancellationToken = default)
    {
        var reply = await bridge.SendAsync(BridgeCommands.Journal, new JsonObject { ["limit"] = 200 }, null, cancellationToken).ConfigureAwait(false);
        var text = ((TextContentBlock)ToolResponse.From(reply).Content[0]).Text;

        return new ReadResourceResult { Contents = [new TextResourceContents { Uri = JournalUri, MimeType = "application/json", Text = text }] };
    }
}
