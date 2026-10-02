using System.Security.Cryptography;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Transfer;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Base;
using Snail.MCP.Blender.Tools.Rendering;

namespace Snail.MCP.Blender.Tests.Application;

/// <summary>Files between the server's machine and Blender's: chunks in order, a digest at each end, and nothing half-written left behind.</summary>
public sealed class FileTransferTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "snail-transfer-tests", Guid.NewGuid().ToString("N"));

    public FileTransferTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Upload_AFileLargerThanAChunk_GoesInOrder_AndOnlyTheLastChunkCarriesTheDigest()
    {
        var bytes = RandomNumberGenerator.GetBytes(FileTransfer.UploadChunkBytes + 10);
        var local = Path.Combine(_root, "hdri.exr");
        await File.WriteAllBytesAsync(local, bytes);
        var sent = new List<JsonObject>();

        var outcome = await new FileTransfer().UploadAsync(local, "hdri/studio.exr", overwrite: false, chunk =>
        {
            sent.Add(chunk);

            return Task.FromResult(BridgeReply.Ok(new JsonObject { ["path"] = "C:\\Users\\artist\\.snail-mcp-blender\\files\\hdri\\studio.exr" }));
        }, CancellationToken.None);

        Assert.Null(outcome.Error);
        Assert.Equal(2, sent.Count);
        Assert.Equal(0, sent[0]["offset"]!.GetValue<long>());
        Assert.Equal(FileTransfer.UploadChunkBytes, sent[1]["offset"]!.GetValue<long>());
        Assert.False(sent[0]["done"]!.GetValue<bool>());
        Assert.False(sent[0].ContainsKey("sha256"));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), sent[1]["sha256"]!.ToString());
        Assert.Equal(bytes.Length, outcome.Files.Single().Size);
        Assert.EndsWith("studio.exr", outcome.Files.Single().To, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Upload_AnEmptyFile_IsOneFinalChunk()
    {
        var local = Path.Combine(_root, "empty.txt");
        await File.WriteAllBytesAsync(local, []);
        var sent = new List<JsonObject>();

        await new FileTransfer().UploadAsync(local, "empty.txt", false, chunk => { sent.Add(chunk); return Task.FromResult(BridgeReply.Ok(new JsonObject())); }, CancellationToken.None);

        Assert.True(sent.Single()["done"]!.GetValue<bool>());
        Assert.Equal(string.Empty, sent.Single()["data"]!.ToString());
    }

    [Fact]
    public async Task Upload_AFolder_KeepsItsLayoutWithForwardSlashes()
    {
        Directory.CreateDirectory(Path.Combine(_root, "textures", "wood"));
        await File.WriteAllTextAsync(Path.Combine(_root, "textures", "wood", "albedo.png"), "a");
        await File.WriteAllTextAsync(Path.Combine(_root, "textures", "steel.png"), "b");
        var paths = new List<string>();

        var outcome = await new FileTransfer().UploadAsync(Path.Combine(_root, "textures"), "C:\\proj\\textures", false, chunk =>
        {
            paths.Add(chunk["path"]!.ToString());

            return Task.FromResult(BridgeReply.Ok(new JsonObject()));
        }, CancellationToken.None);

        Assert.Equal(2, outcome.Files.Count);
        Assert.Equal(["C:\\proj\\textures/steel.png", "C:\\proj\\textures/wood/albedo.png"], paths);
    }

    [Fact]
    public async Task Download_AFolder_ReassemblesEachFileUnderTheLocalRoot_AndChecksItsDigest()
    {
        var frame = RandomNumberGenerator.GetBytes(FileTransfer.DownloadChunkBytes * 2 + 7);
        var remote = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["/renders/shot/f_0001.png"] = frame };

        var outcome = await new FileTransfer().DownloadAsync("/renders/shot", Path.Combine(_root, "shot"), false,
            _ => Listing("/renders/shot", "directory", "f_0001.png"), chunk => Serve(remote, chunk), CancellationToken.None);

        Assert.Null(outcome.Error);
        Assert.Equal(frame, await File.ReadAllBytesAsync(Path.Combine(_root, "shot", "f_0001.png")));
        Assert.False(File.Exists(Path.Combine(_root, "shot", "f_0001.png.part")));
    }

    [Fact]
    public async Task Download_AFile_LandsAtTheLocalPathItself()
    {
        var remote = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["C:/renders/still.png"] = [1, 2, 3] };
        var local = Path.Combine(_root, "still.png");

        await new FileTransfer().DownloadAsync("C:/renders/still.png", local, false, _ => Listing("C:/renders", "file", "still.png"), chunk => Serve(remote, chunk), CancellationToken.None);

        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(local));
    }

    /// <summary>A file that arrives different from what was sent must not take its name: the partial copy is removed and the error says so.</summary>
    [Fact]
    public async Task Download_WithADigestThatDoesNotMatch_LeavesNothingBehind()
    {
        var local = Path.Combine(_root, "bad.png");

        var outcome = await new FileTransfer().DownloadAsync("/r/bad.png", local, false, _ => Listing("/r", "file", "bad.png"),
            _ => Task.FromResult(BridgeReply.Ok(new JsonObject { ["data"] = Convert.ToBase64String([9, 9]), ["eof"] = true, ["sha256"] = new string('0', 64) })),
            CancellationToken.None);

        Assert.Equal("Corrupt", outcome.Error!.Type);
        Assert.False(File.Exists(local));
        Assert.False(File.Exists($"{local}.part"));
    }

    /// <summary>A download the caller gave up on leaves nothing behind: the partial file was swept when a reply failed and not when the
    /// cancellation threw through the same code, so every cancelled download left a truncated file in the user's folder.</summary>
    [Fact]
    public async Task Download_Cancelled_LeavesNoPartialFileBehind()
    {
        var local = Path.Combine(_root, "huge.exr");
        using var giveUp = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FileTransfer().DownloadAsync("/r/huge.exr", local, false,
            _ => Listing("/r", "file", "huge.exr"),
            _ =>
            {
                giveUp.Cancel();
                giveUp.Token.ThrowIfCancellationRequested();

                return Task.FromResult(BridgeReply.Ok(new JsonObject()));
            },
            giveUp.Token));

        Assert.False(File.Exists($"{local}{".part"}"), "a cancelled download left its partial file behind");
        Assert.False(File.Exists(local));
    }

    /// <summary>A listing is one page of a folder; bringing only that page and answering ok would leave the rest behind without a word, so the
    /// rest is asked for page by page.</summary>
    [Fact]
    public async Task Download_AFolderLargerThanOnePage_AsksForTheRest()
    {
        var pages = new[] { "f_0001.png", "f_0002.png", "f_0003.png" };
        var asked = new List<int>();

        var outcome = await new FileTransfer().DownloadAsync("/renders/all", Path.Combine(_root, "all"), false,
            request =>
            {
                var offset = request["offset"]?.GetValue<int>() ?? 0;

                asked.Add(offset);

                return Task.FromResult(BridgeReply.Ok(new JsonObject
                {
                    ["root"] = "/renders/all",
                    ["kind"] = "directory",
                    ["offset"] = offset,
                    ["total"] = pages.Length,
                    ["files"] = new JsonArray(new JsonObject { ["path"] = pages[offset] }),
                    ["truncated"] = offset + 1 < pages.Length,
                }));
            },
            _ => Task.FromResult(BridgeReply.Ok(new JsonObject { ["data"] = Convert.ToBase64String([7]), ["eof"] = true })),
            CancellationToken.None);

        Assert.Null(outcome.Error);
        Assert.Equal([0, 1, 2], asked);
        Assert.Equal(pages, outcome.Files.Select(file => Path.GetFileName(file.To)));
    }

    /// <summary>An add-on that echoes an offset it did not honour is not paging: the page is taken once and the walk stops, because a loop that
    /// trusted "there is more" alone asked for ever — which is what it did to this suite before the check existed.</summary>
    [Fact]
    public async Task Download_FromAnAddOnThatAlwaysAnswersTheFirstPage_StopsAskingAfterOne()
    {
        var asked = 0;

        var outcome = await new FileTransfer().DownloadAsync("/renders/all", Path.Combine(_root, "all"), false,
            _ =>
            {
                asked++;

                return Task.FromResult(BridgeReply.Ok(new JsonObject
                {
                    ["root"] = "/renders/all",
                    ["kind"] = "directory",
                    ["offset"] = 0,
                    ["files"] = new JsonArray(new JsonObject { ["path"] = "f_0001.png" }),
                    ["truncated"] = true,
                }));
            },
            _ => Task.FromResult(BridgeReply.Ok(new JsonObject { ["data"] = Convert.ToBase64String([7]), ["eof"] = true })),
            CancellationToken.None);

        Assert.Null(outcome.Error);
        Assert.Equal(2, asked);
        Assert.Single(outcome.Files);
    }

    /// <summary>An add-on that knows nothing of pages answers the same page to every request; it is asked once and taken at its word.</summary>
    [Fact]
    public async Task Download_FromAnAddOnThatCannotPage_TakesTheOneListingItGives()
    {
        var asked = 0;
        var listing = new JsonObject { ["root"] = "/renders/all", ["kind"] = "directory", ["files"] = new JsonArray(new JsonObject { ["path"] = "f_0001.png" }), ["truncated"] = true };

        var outcome = await new FileTransfer().DownloadAsync("/renders/all", Path.Combine(_root, "all"), false,
            _ => { asked++; return Task.FromResult(BridgeReply.Ok(listing)); },
            _ => Task.FromResult(BridgeReply.Ok(new JsonObject { ["data"] = Convert.ToBase64String([7]), ["eof"] = true })),
            CancellationToken.None);

        Assert.Null(outcome.Error);
        Assert.Equal(1, asked);
        Assert.Single(outcome.Files);
    }

    /// <summary>Past the files one transfer carries the folder is refused whole, before a file arrives, rather than running for an hour.</summary>
    [Fact]
    public async Task Download_AFolderOfMoreFilesThanATransferCarries_IsRefusedBeforeAnyFileArrives()
    {
        var asked = 0;
        var listing = new JsonObject
        {
            ["root"] = "/renders/all",
            ["kind"] = "directory",
            ["offset"] = 0,
            ["total"] = FileTransfer.MostFiles + 1,
            ["files"] = new JsonArray(new JsonObject { ["path"] = "f_0001.png" }),
            ["truncated"] = true,
        };

        var outcome = await new FileTransfer().DownloadAsync("/renders/all", Path.Combine(_root, "all"), false, _ => Task.FromResult(BridgeReply.Ok(listing)),
            _ => { asked++; return Task.FromResult(BridgeReply.Ok(new JsonObject())); }, CancellationToken.None);

        Assert.Equal("TooManyFiles", outcome.Error!.Type);
        Assert.Equal(0, asked);
        Assert.False(Directory.Exists(Path.Combine(_root, "all")));
    }

    [Fact]
    public async Task Download_OverAnExistingLocalFile_IsRefusedUnlessAskedTo()
    {
        var local = Path.Combine(_root, "keep.png");
        await File.WriteAllTextAsync(local, "mine");
        var asked = 0;

        var outcome = await new FileTransfer().DownloadAsync("/r/keep.png", local, false, _ => Listing("/r", "file", "keep.png"),
            _ => { asked++; return Task.FromResult(BridgeReply.Ok(new JsonObject())); }, CancellationToken.None);

        Assert.Equal("Exists", outcome.Error!.Type);
        Assert.Equal(0, asked);
        Assert.Equal("mine", await File.ReadAllTextAsync(local));
    }

    [Theory]
    [InlineData("C:/Users/artist/scene.blend", true)]
    [InlineData("C:\\Users\\artist\\scene.blend", true)]
    [InlineData("/home/artist/scene.blend", true)]
    [InlineData("~/scene.blend", true)]
    [InlineData("\\\\farm\\share\\scene.blend", true)]
    [InlineData(" ~/.config/blender/5.2/scripts/startup/x.py", true)]
    [InlineData("\t/home/artist/x.py", true)]
    [InlineData("textures/wood.png", false)]
    [InlineData("scene.blend", false)]
    public void RemotePaths_AreReadAsTheOtherMachineWouldReadThem(string path, bool absolute) =>
        Assert.Equal(absolute, RemotePaths.IsAbsolute(path));

    [Fact]
    public void RemotePaths_NameAndJoin_WorkWithEitherSeparator()
    {
        Assert.Equal("shot", RemotePaths.NameOf("C:\\renders\\shot\\"));
        Assert.Equal("f_0001.png", RemotePaths.NameOf("/renders/shot/f_0001.png"));
        Assert.Equal("C:\\renders/shot/f.png", RemotePaths.Join("C:\\renders\\", "shot/f.png"));
    }

    /// <summary>With Python off, a write anywhere on Blender's machine could be code it runs later, so uploads stay inside the add-on's files folder.</summary>
    [Fact]
    public async Task UploadTool_WithPythonOff_RefusesAnAbsoluteTarget_BeforeReadingOrSending()
    {
        var bridge = new ScriptedBridge();
        var config = new ServerConfig { Python = PythonAccess.Off };
        var tools = new TransferTools(bridge, new FileTransfer(), config, new LocalProject(config), Commands(config));

        var result = await tools.UploadAsync(Path.Combine(_root, "missing.py"), "C:/Program Files/Blender Foundation/startup.py");

        Assert.True(result.Failed());
        Assert.Contains("files folder", result.Text(), StringComparison.Ordinal);
        Assert.Empty(bridge.Sent);
    }

    /// <summary>The add-on hashes the whole file when the last chunk arrives; under the link's flat 30 s a large upload timed out there while the
    /// add-on went on to commit the file, so the retry was refused as already there.</summary>
    [Fact]
    public async Task UploadTool_OfALargeFile_GivesTheLastChunkTimeForTheAddOnToHashIt()
    {
        var local = Path.Combine(_root, "huge.exr");

        await using (var stream = File.Create(local))
        {
            stream.SetLength(200L * 1024 * 1024);
        }

        var bridge = new ScriptedBridge().Answer(BridgeCommands.FilePut, parameters => new JsonObject { ["path"] = parameters!["path"]!.DeepClone() });
        var config = new ServerConfig { DataDirectory = _root, ProjectDirectory = _root };
        var tools = new TransferTools(bridge, new FileTransfer(), config, new LocalProject(config), Commands(config));

        await tools.UploadAsync(local, "huge.exr");

        Assert.True(bridge.Sent[^1].Timeout > TimeSpan.FromSeconds(33), $"the last chunk was given {bridge.Sent[^1].Timeout}");
        Assert.Equal(TimeSpan.FromSeconds(34), TransferTimeouts.For(200L * 1024 * 1024));
    }

    [Fact]
    public async Task DownloadTool_WithoutAProject_LandsInTheServersDownloadsFolder()
    {
        var bridge = new ScriptedBridge()
            .Answer(BridgeCommands.FileList, new JsonObject { ["root"] = "C:/renders", ["kind"] = "file", ["files"] = new JsonArray(new JsonObject { ["path"] = "still.png" }) })
            .Answer(BridgeCommands.FileGet, new JsonObject { ["data"] = Convert.ToBase64String([7]), ["eof"] = true, ["sha256"] = Convert.ToHexStringLower(SHA256.HashData([7])) });
        var config = new ServerConfig { DataDirectory = _root, ProjectDirectory = ServerPaths.Home };
        var tools = new TransferTools(bridge, new FileTransfer(), config, new LocalProject(config), Commands(config));

        var answer = JsonNode.Parse((await tools.DownloadAsync("C:/renders/still.png")).Text())!["data"]!;

        Assert.Equal(Path.Combine(_root, "downloads", "still.png"), answer["files"]![0]!["to"]!.ToString());
        Assert.Equal("C:/renders/still.png", bridge.Sent[1].Parameters!["path"]!.ToString());
    }

    /// <summary>The folder the client works in is where the work belongs: a render comes back there, not into a folder of the server's.</summary>
    [Fact]
    public async Task DownloadTool_WithinAProject_LandsInTheFolderTheClientWorksIn()
    {
        var project = Path.Combine(_root, "snail-test");
        var bridge = new ScriptedBridge()
            .Answer(BridgeCommands.FileList, new JsonObject { ["root"] = "C:/twin/snail-test/renders", ["kind"] = "file", ["files"] = new JsonArray(new JsonObject { ["path"] = "still.png" }) })
            .Answer(BridgeCommands.FileGet, new JsonObject { ["data"] = Convert.ToBase64String([7]), ["eof"] = true, ["sha256"] = Convert.ToHexStringLower(SHA256.HashData([7])) });
        var config = new ServerConfig { DataDirectory = _root, ProjectDirectory = project };
        var tools = new TransferTools(bridge, new FileTransfer(), config, new LocalProject(config), Commands(config));

        var answer = JsonNode.Parse((await tools.DownloadAsync("snail-test/renders/still.png")).Text())!["data"]!;

        Assert.Equal(Path.Combine(project, "renders", "still.png"), answer["files"]![0]!["to"]!.ToString());
    }

    /// <summary>Over HTTP the server sees none of the client's files: the tool hands back the command that uploads them and sends nothing itself.</summary>
    [Fact]
    public async Task UploadTool_OverHttp_ReturnsTheCommandThatUploads_AndSendsNothing()
    {
        var bridge = new ScriptedBridge();
        var config = HttpConfig();
        var tools = new TransferTools(bridge, new FileTransfer(), config, new LocalProject(config), Commands(config));

        var answer = JsonNode.Parse((await tools.UploadAsync("textures", "robot/textures")).Text())!["data"]!;
        var command = answer["command"]!.ToString();

        Assert.StartsWith("(s='textures'; ", command, StringComparison.Ordinal);
        Assert.Contains("u='https://blender.example.com/raw/files/robot/textures?expires=", command, StringComparison.Ordinal);
        Assert.Contains("&format=tar'; else f=$s; u='https://blender.example.com/raw/files/robot/textures?expires=", command, StringComparison.Ordinal);
        Assert.Contains($"bs={Commands(config).UploadPartBytes}", command, StringComparison.Ordinal);
        Assert.Contains("&offset=$o&last=$last&sha256=$sum", command, StringComparison.Ordinal);
        Assert.Empty(bridge.Sent);
    }

    [Fact]
    public async Task UploadTool_OverHttpWithoutARemotePath_AsksWhereInTheProjectItGoes()
    {
        var config = HttpConfig();
        var tools = new TransferTools(new ScriptedBridge(), new FileTransfer(), config, new LocalProject(config), Commands(config));

        var result = await tools.UploadAsync("textures");

        Assert.True(result.Failed());
        Assert.Contains("remotePath", result.Text(), StringComparison.Ordinal);
    }

    /// <summary>A project folder on Blender's side carries the name of the client's folder, so its renders come back into the client's renders folder.</summary>
    [Fact]
    public async Task DownloadTool_OverHttp_AFolder_UnpacksIntoTheMatchingFolderOfTheClient()
    {
        var bridge = new ScriptedBridge().Answer(BridgeCommands.FileList, new JsonObject
        {
            ["kind"] = "directory",
            ["files"] = new JsonArray(new JsonObject { ["path"] = "f_0001.png" }, new JsonObject { ["path"] = "f_0002.png" }),
            ["total_bytes"] = 2048,
        });
        var config = HttpConfig();
        var tools = new TransferTools(bridge, new FileTransfer(), config, new LocalProject(config), Commands(config));

        var answer = JsonNode.Parse((await tools.DownloadAsync("robot/renders")).Text())!["data"]!;

        var command = answer["command"]!.ToString();

        Assert.Contains("d='renders'", command, StringComparison.Ordinal);
        Assert.Contains("curl -fsSL 'https://blender.example.com/raw/files/robot/renders?expires=", command, StringComparison.Ordinal);
        Assert.Contains("&format=tar'", command, StringComparison.Ordinal);
        Assert.Equal(2, answer["files"]!.GetValue<int>());
        Assert.Equal("files", bridge.Sent.Single().Parameters!["area"]!.ToString());
    }

    [Theory]
    [InlineData("robot", true, ".")]
    [InlineData("robot/renders/f_0001.png", false, "renders/f_0001.png")]
    [InlineData("still.png", false, "still.png")]
    public void LocalTarget_DropsTheProjectFolder_WhichTheClientsFolderAlreadyIs(string remote, bool isFolder, string expected) =>
        Assert.Equal(expected, ClientCommands.LocalTarget(remote, isFolder));

    private static ServerConfig HttpConfig() =>
        new() { Transport = Transport.Http, Http = { Token = "t", PublicUrl = "https://blender.example.com" } };

    private static ClientCommands Commands(ServerConfig config) =>
        new(new FileLinks(new ClientToken(config), config, TimeProvider.System), config);

    [Fact]
    public async Task SetCycles_Backend_IsForwardedBesideTheDevice()
    {
        var bridge = new ScriptedBridge();

        (await new EngineTools(bridge).CyclesAsync(backend: "OPTIX")).Text();

        Assert.Equal("OPTIX", bridge.LastParameters!["backend"]!.ToString());
        Assert.False(bridge.LastParameters.ContainsKey("device"));
    }

    private static Task<BridgeReply> Listing(string root, string kind, params string[] files) =>
        Task.FromResult(BridgeReply.Ok(new JsonObject
        {
            ["root"] = root,
            ["kind"] = kind,
            ["files"] = new JsonArray([.. files.Select(file => (JsonNode)new JsonObject { ["path"] = file })]),
        }));

    private static Task<BridgeReply> Serve(Dictionary<string, byte[]> remote, JsonObject request)
    {
        var bytes = remote[request["path"]!.ToString()];
        var offset = (int)request["offset"]!.GetValue<long>();
        var length = Math.Min(request["length"]!.GetValue<int>(), bytes.Length - offset);
        var eof = offset + length >= bytes.Length;
        var reply = new JsonObject { ["data"] = Convert.ToBase64String(bytes, offset, length), ["eof"] = eof };

        if (eof)
        {
            reply["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
        }

        return Task.FromResult(BridgeReply.Ok(reply));
    }
}
