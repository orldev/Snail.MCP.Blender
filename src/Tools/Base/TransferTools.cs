using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Transfer;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Files between this machine and the one Blender runs on: what a remote Blender needs to see, and what it rendered.</summary>
[McpServerToolType]
public sealed class TransferTools(IBlenderBridge bridge, FileTransfer transfer, ServerConfig config, LocalProject project, ClientCommands commands) : BridgeToolBase(bridge)
{
    private TimeSpan Basis => TimeSpan.FromSeconds(config.Bridge.RequestTimeoutSeconds);

    [McpServerTool(Name = "blender_upload", Destructive = true, OpenWorld = false)]
    [Description(ToolDescriptions.Transfer.Upload)]
    public async Task<CallToolResult> UploadAsync(
        [Description("File or folder on this machine: absolute, or relative to the project, the folder the client works in.")] string localPath,
        [Description("Where it lands on Blender's machine: absolute, or relative to the add-on's files folder; the same relative path in the project's twin otherwise.")] string? remotePath = null,
        [Description("Replace files that already exist there.")] bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        if (config.Transport is Transport.Http)
        {
            return UploadCommand(localPath, remotePath, overwrite);
        }

        var local = project.Local(localPath);
        var target = remotePath ?? project.UploadTarget(local);

        return config.Python == PythonAccess.Off && RemotePaths.IsAbsolute(target)
            ? ToolResponse.Failure(Messages.UploadOutsideFilesRefused, Messages.UploadOutsideFilesHint)
            : Report(await transfer.UploadAsync(local, target, overwrite, chunk => ExchangeAsync(BridgeCommands.FilePut, chunk, TransferTimeouts.ForChunk(chunk, Basis), cancellationToken), cancellationToken)
                .ConfigureAwait(false));
    }

    [McpServerTool(Name = "blender_download", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Transfer.Download)]
    public async Task<CallToolResult> DownloadAsync(
        [Description("File or folder on Blender's machine: absolute, or relative to the add-on's files folder.")] string remotePath,
        [Description("Where it lands on this machine: absolute, or relative to the project; the matching place in the project otherwise.")] string? localPath = null,
        [Description("Replace local files that already exist.")] bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        config.Transport is Transport.Http
            ? await DownloadCommandAsync(remotePath, localPath, overwrite, cancellationToken).ConfigureAwait(false)
            : Report(await transfer.DownloadAsync(
                    remotePath,
                    localPath is null ? project.DownloadTarget(remotePath) : project.Local(localPath),
                    overwrite,
                    listing => ExchangeAsync(BridgeCommands.FileList, listing, null, cancellationToken),
                    chunk => ExchangeAsync(BridgeCommands.FileGet, chunk, TransferTimeouts.ForChunk(chunk, Basis), cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false));

    /// <summary>Over HTTP nothing is copied here: the client gets the command that uploads through a signed link, into a path of the files area.</summary>
    private CallToolResult UploadCommand(string localPath, string? remotePath, bool overwrite) =>
        remotePath is null || RemotePaths.IsAbsolute(remotePath)
            ? ToolResponse.Failure(Messages.HttpUploadNeedsAPlace, Messages.HttpUploadHint)
            : Command(commands.Upload(localPath, remotePath.Trim('/'), overwrite), remotePath.Trim('/'), null);

    /// <summary>Over HTTP the file or folder is looked up first, so the command fits what it is and says how much will arrive.</summary>
    private async Task<CallToolResult> DownloadCommandAsync(string remotePath, string? localPath, bool overwrite, CancellationToken cancellationToken)
    {
        if (RemotePaths.IsAbsolute(remotePath))
        {
            return ToolResponse.Failure(Messages.HttpDownloadOutsideFiles, Messages.HttpUploadHint);
        }

        var relative = remotePath.Trim('/');
        var listing = await ExchangeAsync(BridgeCommands.FileList, new JsonObject { ["area"] = VolumeAreas.Files, ["path"] = relative }, null, cancellationToken).ConfigureAwait(false);

        if (!listing.IsOk)
        {
            return ToolResponse.From(listing);
        }

        var isFolder = listing.Result?["kind"]?.GetValue<string>() == "directory";
        var command = commands.Download(relative, localPath ?? ClientCommands.LocalTarget(relative, isFolder), isFolder, overwrite);

        return Command(command, relative, new JsonObject
        {
            ["files"] = listing.Result?["files"]?.AsArray().Count,
            ["bytes"] = listing.Result?["total_bytes"]?.DeepClone(),
        });
    }

    private static CallToolResult Command(ClientCommand command, string remotePath, JsonObject? size)
    {
        var data = new JsonObject
        {
            ["command"] = command.Command,
            ["expires"] = command.Expires.ToString("O"),
            ["remotePath"] = remotePath,
            ["note"] = Messages.HttpCommandNote,
        };

        foreach (var (key, value) in size ?? [])
        {
            data[key] = value?.DeepClone();
        }

        return ToolResponse.Success(data);
    }

    /// <summary>Every file that made it, and the error that stopped the rest; a failure still says what arrived before it.</summary>
    private static CallToolResult Report(TransferOutcome outcome)
    {
        var files = new JsonArray([.. outcome.Files.Select(file => (JsonNode)new JsonObject
        {
            ["from"] = file.From,
            ["to"] = file.To,
            ["size"] = file.Size,
            ["sha256"] = file.Sha256,
        })]);
        var summary = new JsonObject
        {
            ["files"] = files,
            ["count"] = outcome.Files.Count,
            ["bytes"] = outcome.Files.Sum(file => file.Size),
        };

        if (outcome.Error is null)
        {
            return ToolResponse.Success(summary);
        }

        summary["type"] = outcome.Error.Type;
        summary["details"] = outcome.Error.Details?.DeepClone();

        return ToolResponse.Failure(outcome.Error.Message, Messages.HintFor(outcome.Error), summary);
    }
}
