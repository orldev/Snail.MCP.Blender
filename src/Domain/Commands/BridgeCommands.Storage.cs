namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/storage.py</c>: what each area of the data directory holds and deleting what is done with, answered from the socket thread.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand Storage = new("storage", AddOnModules.Storage, BridgeChannel.Data);

    public static readonly BridgeCommand StorageList = new("storage_list", AddOnModules.Storage, BridgeChannel.Data);

    public static readonly BridgeCommand StorageDelete = new("storage_delete", AddOnModules.Storage, BridgeChannel.Data);
}
