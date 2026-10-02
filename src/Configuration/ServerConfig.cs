using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Snail.MCP.Blender.Configuration;

/// <summary>Root server configuration, bound from a JSON file and environment variables.</summary>
public sealed class ServerConfig
{
    public const string SectionName = "Snail";

    /// <summary>This server's own data root: the add-on zip, downloads from a remote Blender and, when Blender runs here, the token file; defaults to <c>~/.snail-mcp-blender</c>.
    /// Render jobs, batches and snapshots live in the add-on's data directory instead, on the machine Blender runs on.</summary>
    public string DataDirectory { get; set; } = ServerPaths.DefaultDataDirectory;

    [ValidateObjectMembers]
    public BlenderLinkOptions Bridge { get; set; } = new();

    /// <summary>stdio when the client starts this server; http when it runs on its own beside Blender and clients connect to it.</summary>
    public Transport Transport { get; set; } = Transport.Stdio;

    [ValidateObjectMembers]
    public HttpOptions Http { get; set; } = new();

    /// <summary>The folder on this machine the work belongs to: downloads land in it and relative upload paths are read from it, and its twin on Blender's
    /// machine is the add-on's files/&lt;its name&gt;. Defaults to the directory the client starts the server in, which is the folder the client works in;
    /// the home folder and the root count as no project, and so does every folder of a server reached over HTTP, which sees none of the client's.</summary>
    public string? ProjectDirectory { get; set; }

    /// <summary>Minutes without a tool call after which the process exits; 0, the default, disables the watchdog.</summary>
    /// <remarks>Off by default because it was solving a problem the transport already solves: a client that ends, crashes or is killed closes the
    /// pipe, and the stdio server exits within a second. What the watchdog did instead was end sessions that were merely idle, mid-render or
    /// between two tasks, and the client saw the server disappear.</remarks>
    [Range(0, int.MaxValue)]
    public int IdleTimeoutMinutes { get; set; }

    /// <summary>Minutes a loaded skill may go unused before its tools leave the catalog; 0, the default, keeps skills loaded, because a client that caches the tool list loses tools it still expects when they unload.</summary>
    [Range(0, int.MaxValue)]
    public int SkillIdleMinutes { get; set; }

    /// <summary>The token file the add-on writes next to its state when its preferences leave the token empty.</summary>
    public string TokenFile => Path.Combine(DataDirectory, "state", "token");

    /// <summary>OpenColorIO config background render jobs run under; empty keeps Blender's own. The running Blender reads OCIO only at start-up.</summary>
    public string? OcioConfig { get; set; }

    /// <summary>How far blender_python may go: allow, fallback (only what no tool covers) or off, for shared or untrusted set-ups.</summary>
    public PythonAccess Python { get; set; } = PythonAccess.Allow;

    /// <summary>Name this server signs its requests with, so the add-on's journal and leases tell agents apart; empty means anonymous.</summary>
    public string? Agent { get; set; }
}

/// <summary>Settings of the TCP link to the Blender add-on.</summary>
public sealed class BlenderLinkOptions
{
    /// <summary>Where the add-on listens: the loopback, or the Blender container's name when the server runs beside it in Docker; a Blender on another
    /// machine is reached through <see cref="Tunnel"/>, never directly.</summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>Port the add-on listens on; matches the add-on preferences and cannot change without them.</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 9876;

    /// <summary>Timeout for opening the socket; Blender not running answers fast, a hung Blender answers never.</summary>
    [Range(1, int.MaxValue)]
    public int ConnectTimeoutSeconds { get; set; } = 5;

    /// <summary>Timeout for one add-on reply; renders and file imports pass their own longer one.</summary>
    [Range(1, int.MaxValue)]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>The agent name the link signs requests with; filled from the top-level setting when the section leaves it empty.</summary>
    public string? Agent { get; set; }

    /// <summary>Shared secret the add-on expects on every request when one is set in its preferences; empty reads the add-on's token file.</summary>
    public string? Token { get; set; }

    /// <summary>Where the add-on keeps the token it generated; filled from the data directory when the section leaves it empty.</summary>
    public string? TokenFile { get; set; }

    /// <summary>The SSH tunnel to a Blender on another machine; without a host the link stays on this one.</summary>
    [ValidateObjectMembers]
    public TunnelOptions Tunnel { get; set; } = new();
}

/// <summary>An SSH tunnel this server opens to a Blender on another machine: up while the server runs, reopened after a drop, closed when it exits.</summary>
/// <remarks>The server's own <see cref="BlenderLinkOptions.Port"/> is the local end; the add-on listens on <see cref="RemotePort"/> over there. The tunnel
/// lives exactly as long as the server, which lives as long as the client works with Blender, so nothing stays open between sessions.</remarks>
public sealed class TunnelOptions
{
    /// <summary>user@host, or a host from ~/.ssh/config; empty leaves the link on this machine.</summary>
    public string? Host { get; set; }

    /// <summary>Private key to log in with; the SSH agent and ~/.ssh/config decide when empty.</summary>
    public string? Key { get; set; }

    /// <summary>Port the add-on listens on over there.</summary>
    [Range(1, 65535)]
    public int RemotePort { get; set; } = 9876;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Host);
}

/// <summary>Settings of the HTTP server a container runs: where it listens, the token clients present and the address the outside reaches it at.</summary>
public sealed class HttpOptions
{
    /// <summary>The address to listen on; a container listens on every interface, a server started by hand on the loopback only.</summary>
    public string Url { get; set; } = "http://127.0.0.1:8080";

    /// <summary>The token a client presents as a bearer and on the page login; empty reads <see cref="TokenFile"/>.</summary>
    public string? Token { get; set; }

    /// <summary>A file holding the token, the way a container receives a secret.</summary>
    public string? TokenFile { get; set; }

    /// <summary>The address clients reach this server at behind a reverse proxy, which links to files are built on; empty uses <see cref="Url"/>.</summary>
    public string? PublicUrl { get; set; }

    /// <summary>Minutes a signed link to a file stays valid.</summary>
    [Range(1, 1440)]
    public int LinkMinutes { get; set; } = 15;

    /// <summary>Megabytes an upload sends in one request, which a reverse proxy must read within its own body timeout.</summary>
    /// <remarks>Traefik stops reading a body after a minute by default, and a part that does not arrive in that time breaks the upload at
    /// exactly the same point every retry. Sixteen megabytes arrive in 32 seconds at half a megabyte per second; lower it for clients on a
    /// slower uplink, raise it where the proxy is patient and the round trips cost more than the bytes.</remarks>
    [Range(1, 64)]
    public int UploadPartMb { get; set; } = 16;
}
