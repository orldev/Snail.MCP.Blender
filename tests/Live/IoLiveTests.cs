using Snail.MCP.Blender.Adapters.Blender;

namespace Snail.MCP.Blender.Tests.Live;

/// <summary>Assets against a real Blender: listed in another file and appended from it.</summary>
public sealed class IoLiveTests : IAsyncLifetime
{
    private HeadlessBlender _blender = null!;
    private BlenderConnection _link = null!;
    private string _workDirectory = null!;

    public async Task InitializeAsync()
    {
        if (BlenderInstallation.Find() is null)
        {
            return;
        }

        _blender = await HeadlessBlender.StartAsync();
        _link = new BlenderConnection(_blender.Link, NullLogger<BlenderConnection>.Instance);
        _workDirectory = Path.Combine(Path.GetTempPath(), "snail-blender-live", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
    }

    public async Task DisposeAsync()
    {
        if (_link is not null) await _link.DisposeAsync();

        _blender?.Dispose();

        if (_workDirectory is not null && Directory.Exists(_workDirectory)) Directory.Delete(_workDirectory, recursive: true);
    }

    [BlenderFact]
    public async Task Assets_ListedAndAppended_FromAnotherFile()
    {
        var library = Path.Combine(_workDirectory, "library.blend");
        await Send(BridgeCommands.SaveFile, new JsonObject { ["path"] = library, ["copy"] = true });

        var listed = await Send(BridgeCommands.ListAssets, new JsonObject { ["path"] = library, ["types"] = new JsonArray("objects", "materials") });
        Assert.Contains("Cube", listed["contents"]!["objects"]!.AsArray().Select(node => node!.ToString()));

        await Send(BridgeCommands.NewFile, new JsonObject { ["empty"] = true });
        var appended = await Send(BridgeCommands.AppendAssets, new JsonObject { ["path"] = library, ["type"] = "objects", ["names"] = new JsonArray("Cube"), ["collection"] = "Imported" });

        Assert.Equal(["Cube"], appended["placed"]!.AsArray().Select(node => node!.ToString()));
        Assert.Equal("Imported", appended["collection"]!.ToString());
    }

    private async Task<JsonNode> Send(BridgeCommand command, JsonObject? parameters = null, TimeSpan? timeout = null)
    {
        var reply = await _link.SendAsync(command, parameters, timeout);

        Assert.True(reply.IsOk, $"{command.Name}: {reply.Error?.Type} {reply.Error?.Message} {reply.Error?.Details}");

        return reply.Result!;
    }
}
