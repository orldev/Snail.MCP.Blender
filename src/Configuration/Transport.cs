namespace Snail.MCP.Blender.Configuration;

/// <summary>How a client reaches this server.</summary>
public enum Transport
{
    /// <summary>The client starts the server and speaks JSON-RPC over its stdin and stdout; the server lives as long as the client.</summary>
    Stdio,

    /// <summary>The server runs on its own, in a container beside Blender, and clients connect over Streamable HTTP with a bearer token.</summary>
    Http,
}
