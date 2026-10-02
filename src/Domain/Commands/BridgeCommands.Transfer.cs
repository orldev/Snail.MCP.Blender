namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/transfer.py</c>: files moved in chunks between the server's machine and Blender's, answered from the socket thread.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand FilePut = new("file_put", AddOnModules.Transfer, BridgeChannel.Data);

    public static readonly BridgeCommand FileGet = new("file_get", AddOnModules.Transfer, BridgeChannel.Data);

    public static readonly BridgeCommand FileList = new("file_list", AddOnModules.Transfer, BridgeChannel.Data);
}
