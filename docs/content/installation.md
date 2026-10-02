# Installation

By default everything runs locally: a .NET tool started by your MCP client, and an extension inside Blender that
listens on the loopback interface only, so nothing leaves the machine. A Blender on another machine is reached through
an SSH tunnel instead; [Blender on another machine](remote.md) covers installing the add-on there. A server can also run
beside Blender in Docker and be reached over HTTPS; [Blender in Docker](docker.md) sets that up.

## What you need

| | |
|---|---|
| **.NET 10 SDK** | [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **An MCP client** | Claude Code, Claude Desktop, Cursor, VS Code, Codex — anything that starts an MCP server over stdio, or connects to one over Streamable HTTP for a server in Docker |
| **Blender 5.2 LTS or newer** | The add-on ships as a Blender extension, and 5.2 is what the live tests run against |

macOS, Windows and Linux are all supported.

## Step 1 — install the server

### As a global tool (recommended)

```bash
dotnet tool install -g Snail.MCP.Blender
```

This gives you a `snail-mcp-blender` command. If the shell answers `command not found`, add the tools
folder to `PATH`: `~/.dotnet/tools` on macOS and Linux, `%USERPROFILE%\.dotnet\tools` on Windows. GUI
clients do not always inherit the shell `PATH`; when one cannot start the server, put the absolute path in
`command`.

### Without installing

```bash
dnx Snail.MCP.Blender --yes
```

In a client configuration that becomes `"command": "dnx"`, `"args": ["Snail.MCP.Blender", "--yes"]`.

### From source

```bash
git clone https://github.com/orldev/Snail.MCP.Blender.git
cd Snail.MCP.Blender
dotnet build -c Release
```

The command is then `dotnet` with `"args": ["<repo>/src/bin/Release/net10.0/Snail.MCP.Blender.dll"]`.
The repository's `.mcp.json` holds an entry for a Debug build, commented out; uncomment it to register that build with
Claude Code.

!!! note "Do not start the server by hand"
    Over stdio, the default, it speaks JSON-RPC and has no interactive interface. The client starts it and stops it by
    closing the pipe, which the server exits with. Only a server with `SNAIL_MCP_BLENDER_TRANSPORT=Http` runs on its
    own, as the Docker image does.

## Step 2 — register the server with your client

### Claude Code

```bash
claude mcp add blender -- snail-mcp-blender
```

### Claude Desktop

In `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "blender": { "command": "snail-mcp-blender" }
  }
}
```

### A server in Docker

A server that runs beside Blender in Docker is reached over HTTP instead of started by the client. For Claude Code:

```bash
claude mcp add --transport http --scope user blender https://blender.example.com/mcp \
  --header "Authorization: Bearer $(cat ~/.snail-mcp-blender/remote/docker.token)"
```

Nothing else is installed on the client, the add-on included; [Blender in Docker](docker.md) sets up the machine.

### Cursor, VS Code, Codex

The same `command` in the client's MCP settings; the key is `mcpServers` or `servers` depending on the client.

## Step 3 — install the add-on in Blender

Ask the agent to call `blender_install_addon`, or run it yourself from the client's tool list. It writes
`snail_bridge-<version>.zip` into the data directory (`~/.snail-mcp-blender/addon`) and lists the steps:

1. In Blender open **Edit → Preferences → Get Extensions**.
2. Open the drop-down arrow in the top-right corner and choose **Install from Disk**.
3. Pick the zip. The extension is enabled and starts listening at once.
4. In the 3D viewport press **N**, open the **Snail** tab and check that it says *listening on port 9876*.

The add-on starts with Blender from then on. Its preferences (**Edit → Preferences → Get Extensions →
Snail Bridge**) hold the port, the autostart switch and the token. Leave the token empty: the add-on
generates one into `~/.snail-mcp-blender/state/token`, readable by you only, and the server reads it from
there, so a single-user machine needs nothing typed on either side.

## Step 4 — check

Ask the agent to run `blender_diagnose`. With Blender open it reports `blender.reachable: true` and the
Blender version. Then try *add a cube at 0, 0, 1 and make it red*.

## Updating

```bash
dotnet tool update -g Snail.MCP.Blender
```

After a server update run `blender_install_addon` again and reinstall the zip: the add-on and the server
share one command catalog and must match. They check each other — `blender_diagnose` reports
`addOn.status: current` when the installed add-on is the build the server ships, `older` with the list of missing
commands when it is behind, `differs` when it is another build that is not behind, and `unknown` when Blender does not
answer.

## Uninstalling

```bash
dotnet tool uninstall -g Snail.MCP.Blender
```

Remove the extension in Blender's preferences and delete `~/.snail-mcp-blender`.
