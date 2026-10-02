# Configuration

Everything is optional. Environment variables use the prefix `SNAIL_MCP_BLENDER_`, with `__` between a
section and a field.

| Variable | Default | Meaning |
|---|---|---|
| `SNAIL_MCP_BLENDER_DATA_DIRECTORY` | `~/.snail-mcp-blender` | This server's own files: the add-on zip, downloads when there is no project and, when Blender runs here, the token. The add-on reads the same variable for its journal, leases, render jobs, batches and snapshots, which live on the machine Blender runs on |
| `SNAIL_MCP_BLENDER_PROJECT_DIRECTORY` | the folder the client starts the server in | Over stdio, the project on this machine: `blender_download` brings files into it, relative `blender_upload` paths are read from it, and its twin on Blender's machine is the add-on's `files/<its name>`. The home folder and the root count as no project. A server over HTTP sees none of the client's folders and ignores it; `blender_open_project` names the project there |
| `SNAIL_MCP_BLENDER_TRANSPORT` | `Stdio` | `Stdio` when the client starts the server; `Http` when it runs on its own beside Blender, in a container, and clients connect to it — [Blender in Docker](docker.md) |
| `SNAIL_MCP_BLENDER_HTTP__URL` | `http://127.0.0.1:8080` | Over HTTP: the address to listen on; the image sets `http://0.0.0.0:8080` |
| `SNAIL_MCP_BLENDER_HTTP__TOKEN_FILE` | — | Over HTTP: the file holding the token clients present — as a bearer on `/mcp` and at the pages' sign-in. Required: without it, or `HTTP__TOKEN`, the server refuses to start |
| `SNAIL_MCP_BLENDER_HTTP__TOKEN` | — | The same token given directly, for a quick local run; it wins over `HTTP__TOKEN_FILE`. Either is read once at start, so a new token takes a restart |
| `SNAIL_MCP_BLENDER_HTTP__PUBLIC_URL` | `HTTP__URL` | The address clients reach the server at behind a reverse proxy; the links `blender_upload` and `blender_download` hand out are built on it, and an `https` address marks the session cookie Secure |
| `SNAIL_MCP_BLENDER_HTTP__LINK_MINUTES` | `15` | How long those links work, 1 to 1440 |
| `SNAIL_MCP_BLENDER_BRIDGE__HOST` | `127.0.0.1` | Where the server finds the add-on: the loopback, or the Blender container's name when both run in containers. A Blender on another machine is reached through the tunnel, never directly |
| `SNAIL_MCP_BLENDER_BRIDGE__PORT` | `9876` | Must match the port in the add-on preferences |
| `SNAIL_MCP_BLENDER_BRIDGE__CONNECT_TIMEOUT_SECONDS` | `5` | Opening the socket |
| `SNAIL_MCP_BLENDER_BRIDGE__REQUEST_TIMEOUT_SECONDS` | `30` | One reply; renders, imports and Python pass their own |
| `SNAIL_MCP_BLENDER_IDLE_TIMEOUT_MINUTES` | `0` | Over stdio: exit after this long without a tool call; `0`, the default, disables it, since a client that ends closes the pipe and the server exits with it. A server over HTTP never exits on its own |
| `SNAIL_MCP_BLENDER_SKILL_IDLE_MINUTES` | `0` | Unload a skill unused for this many minutes; `0` keeps skills loaded, which suits clients that cache the tool list |
| `SNAIL_MCP_BLENDER_BRIDGE__TOKEN` | — | Shared secret for the add-on; empty reads the one the add-on generated into `<data>/state/token` |
| `SNAIL_MCP_BLENDER_BRIDGE__TOKEN_FILE` | `<data>/state/token` | Where to read that generated token when it lives outside this server's data directory |
| `SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__HOST` | — | `user@host` of a Blender on another machine: the server opens an SSH tunnel to it at start, reopens it after a drop and closes it on exit; empty keeps the link on this machine |
| `SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__KEY` | — | Private key for that login; the SSH agent and `~/.ssh/config` decide when empty |
| `SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__REMOTE_PORT` | `9876` | The add-on's port on the other machine; `BRIDGE__PORT` is the local end of the tunnel |
| `SNAIL_MCP_BLENDER_AGENT` | — | Name this server signs its requests with; the add-on's journal and leases tell agents apart |
| `SNAIL_MCP_BLENDER_BRIDGE__AGENT` | `AGENT` | The name the link signs with, when it should differ from `AGENT` |
| `SNAIL_MCP_BLENDER_PYTHON` | `allow` | Passed to Blender as well (`--python` in compose), so `off` holds even for a client that reaches the add-on another way. `fallback` runs only scripts whose work no tool covers, in `blender_run` and `blender_batch` steps as in `blender_python`; it reads the script as text, so it steers the agent towards the tools rather than guarding the door. `off` switches `blender_python` off together with the operators that run scripts or install code — the `script`, `console`, `preferences` and `extensions` modules, `text.run_script`, and anything asked to trust a file's own scripts — sequences and batches holding a `python` step, and uploads outside the add-on's `files` folder; the dedicated tools and the other operators stay |
| `SNAIL_MCP_BLENDER_OCIO_CONFIG` | — | OpenColorIO config background render jobs run under; the running Blender reads `OCIO` only at start-up |
| `SNAIL_MCP_BLENDER_CONFIG` | — | Path of an explicit settings file |

Every range lives as an attribute on the setting it constrains, and the server checks them all while the host
starts: a port outside 1 to 65535, a timeout of zero or less, negative minutes, link minutes outside 1 to 1440. The
process then exits with `OptionsValidationException` naming each setting it refused and the range it allows; a server
over HTTP without a client token exits the same way, naming `HTTP__TOKEN` and `HTTP__TOKEN_FILE`. A value that is not a
number, or a `PYTHON` or `TRANSPORT` that is none of its names, stops the process with `InvalidOperationException`
instead. Nothing is repaired in
silence, so a typo in the environment is visible immediately instead of turning into a link that never connects.

## Token

The add-on demands a token on every request. Left empty in its preferences, it generates one on first start
into `<data directory>/state/token`, readable by the user only, and the server reads that file whenever it
connects, so a fresh install needs nothing typed. Set the same value on both sides by hand when the server and
Blender use different data directories. For a Blender on another machine, copy the token it generated into a file
readable by you alone and point `SNAIL_MCP_BLENDER_BRIDGE__TOKEN_FILE` at it, so the MCP entry holds a path and no
secret; [Blender on another machine](remote.md) shows the commands. A Blender without an interface takes its token from
`blender -b -c snail_bridge --token-file <file>` instead, and `--host` sets the address it listens on, the container
network in Docker; point `SNAIL_MCP_BLENDER_BRIDGE__TOKEN_FILE` at the same file.

## Clients and their keys

A server reached over HTTP admits clients by key, one key each:

```console
$ snail-mcp-blender clients add lighting --scope read,write,farm
lighting — read, write, farm
l7m82DheQD9RO3z3RWjSgr0Gv87LgkjQfGvRwEDPhTk
This is the only time the key is shown; give it to the client and keep no copy here.
```

The client sends it as `Authorization: Bearer <key>`. `clients list` names the clients and what each may do, and
`clients revoke <name>` stops admitting one — at its next request, without restarting the server, and its browser
session with it. Only the digest of a key is kept, under `<data directory>/state/clients.json`, so a copy of that file
opens nothing and a key that is lost is reminted rather than looked up.

The name on the key is the agent name the client's commands are signed with, which the add-on's leases and journal
record; the client cannot choose it (see [Agents](agents.md#journal-and-leases)).

| Scope | Opens |
| --- | --- |
| `read` | Reading the scene, the files and the diagnostics |
| `write` | Changing the scene and writing files |
| `delete` | Deleting objects and files, clearing another agent's lease, undoing |
| `python` | `blender_python` and `blender_run_operator`, which reach past every schema |
| `farm` | Background renders and batches, which spend the machine after the call returns |

A key minted without `--scope` gets `read`, `write`, `delete` and `farm`: Python is asked for by name.

`SNAIL_MCP_BLENDER_HTTP__TOKEN` still works and opens everything, so a server set up before keys existed keeps
running and its clients move over one at a time. It is also the only key whose holder may still name itself with the
header `X-Snail-Agent`.

## State the add-on keeps

The add-on writes what must outlive a Blender session under `<data directory>/state`: `journal.jsonl`
(every mutating command with its agent), `leases.json` (objects agents hold) and `jobs.json` (the render
queues to pick up after a restart). Worker Blenders started for batches and render jobs inherit the data
directory, so their commands land in the same journal and respect the same leases. The add-on reads
`SNAIL_MCP_BLENDER_DATA_DIRECTORY` from Blender's own environment; when Blender is started from a launcher
without it, the default `~/.snail-mcp-blender` applies, which is also the server's default. Next to `state/` it keeps
`jobs/`, `batches/`, `snapshots/` and `files/`, all on the machine Blender runs on; [the working
order](workflow.md#where-things-are-stored) maps them.

## Settings file

The same keys as a JSON file, under a `Snail` section, looked up in this order, and only the first that exists is read:
`SNAIL_MCP_BLENDER_CONFIG`,
`.snail-mcp-blender.json` and `snail-mcp-blender.json` in the working directory,
`~/.config/snail-mcp-blender/config.json`, `~/.snail-mcp-blender.json`.

```json
{
  "Snail": {
    "Bridge": { "Port": 9876, "RequestTimeoutSeconds": 45 },
    "IdleTimeoutMinutes": 0
  }
}
```

Environment variables win over the file.

## Limits

| | |
|---|---|
| Timeout per call | 1 to 600 seconds, set per call by `timeoutSeconds` where a tool has it; a value outside that range is refused, not clamped |
| Render resolution | 4096 pixels per edge |
| Request size | 16 MB per JSON line to the add-on |
| Reply size | 4 MB from the add-on; larger replies come back as an error asking for less |
| Queued commands | 64 waiting for Blender's main thread; beyond that the add-on answers `Busy` |
| Sequence | 500 steps per `blender_run`; step results leave the reply once it passes half a megabyte, each marked `omitted` |
| Transfer chunk | 8 MB up, 2 MB down, the whole file checked by SHA-256 at the end |
| Upload part over HTTP | 16 MB per request of the command `blender_upload` returns, so a proxy that reads a body for a minute never cuts one off |
| Folder listing | 5,000 files per page; a download walks the pages, and past 20,000 files in all the folder is refused as `TooManyFiles` and comes down folder by folder. The HTTP archive still takes one listing |
| Folder on the pages | 2,000 entries per folder; the page says when it shows only the first 2,000 |
| Text preview | 256 KB on the pages; larger files are only downloaded |
