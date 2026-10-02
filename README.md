# Snail.MCP.Blender

[![Documentation](https://img.shields.io/badge/docs-orldev.github.io-blue)](https://orldev.github.io/Snail.MCP.Blender/)
[![NuGet](https://img.shields.io/nuget/v/Snail.MCP.Blender?logo=nuget)](https://www.nuget.org/packages/Snail.MCP.Blender)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)

An MCP server that lets your AI assistant drive a running **Blender**: 149 tools for the scene,
modeling, materials, animation, physics, rendering, video editing and file exchange. It runs on your machine, talks to your client
over stdio, and reaches Blender through a small bundled add-on listening on the loopback interface — of this machine, or of
another one through an SSH tunnel the server opens. Or it runs in Docker beside Blender on a machine with a GPU, and any
client connects to it over HTTPS.

Ask in plain words: *build a low-poly house and give it a brick material*, *animate the camera orbiting the
scene over 120 frames*, *render a 1080p preview to the desktop*, *export everything as glTF*.

- **Always on**: scene and object info, selection, primitives, text, empties, transforms, camera, lights,
  collections, files and undo, a project folder; finding a tool by what you want done, many commands in one call, files to
  and from a Blender on another machine; the journal, leases, batches and snapshots for several agents — plus any Blender operator
  with validated properties, its documentation, and Python.
- **Skills, loaded on demand**: `modeling` (mesh edits on a selection, vertices, modifiers, Geometry Nodes,
  curves), `materials` (Principled BSDF, shader node graphs, textures, UVs), `animation` (keyframes, F-Curves,
  armatures, constraints, shape keys, drivers), `rendering` (world and HDRI, stills and animations, product shots, viewport
  captures, texture bakes, colour management, EXR depth and codecs, stamps, resolution presets, stereo, Cycles and
  EEVEE in depth with the GPU backend, motion blur, Freestyle, view layers with passes, cryptomatte, AOVs and light
  groups, light linking, the physical camera, camera moves and light rigs in kelvin, per-object render flags), `post` (compositor, File Output nodes, cryptomatte mattes, a lens-effects stack with denoise, glare,
  halation, chromatic aberration and film grain), `farm` (background render jobs with a queue, chunks, retries, progress and
  cancel, a pre-flight check, a memory budget with a probe render), `io` (FBX, OBJ, STL, PLY, glTF, Collada, USD, Alembic, X3D, DXF and 3DS both ways, SVG in; assets from other .blend files), `physics` (rigid
  bodies, cloth, fluids, particles, cache baking), `video` (the sequence editor: strips and image sequences,
  transitions, fades, cuts, proxies, fractional frame rates, grading, ProRes and H.264/H.265 encode settings,
  audio mixdown).

Renders come back with a picture the model can look at and exposure statistics; long renders report progress
and can run as queued background jobs. Three MCP prompts (`photoreal_product_shot`, `farm_exr_delivery`,
`vse_grade_and_deliver`) lay out the studio order of tools.

Full tool reference with every parameter and JSON schema:
**[orldev.github.io/Snail.MCP.Blender](https://orldev.github.io/Snail.MCP.Blender/)**

## Requirements

| | |
|---|---|
| **.NET 10 SDK** | [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **An MCP client** | Claude Code, Claude Desktop, Cursor, VS Code, Codex — anything that speaks MCP over stdio, or over HTTP to the Docker set-up |
| **Blender 5.2 LTS or newer** | The add-on ships as a Blender extension |

Works on macOS, Windows and Linux.

## Install

### 1. The server

```bash
dotnet tool install -g Snail.MCP.Blender
```

Alternatives: `dnx Snail.MCP.Blender --yes` runs it without installing; or clone the repository and run
`dotnet build -c Release`, then give the client the command `dotnet` with `src/bin/Release/net10.0/Snail.MCP.Blender.dll`
as its argument.

### 2. Your client

```json
{
  "mcpServers": {
    "blender": {
      "command": "snail-mcp-blender"
    }
  }
}
```

For Claude Code: `claude mcp add blender -- snail-mcp-blender`.

For a server that runs in Docker beside Blender, register its address instead; nothing else is installed on the client,
the add-on included, and step 3 is done on that machine:

```bash
claude mcp add --transport http --scope user blender https://blender.example.com/mcp \
  --header "Authorization: Bearer $(cat ~/.snail-mcp-blender/remote/docker.token)"
```

[docs/content/docker.md](docs/content/docker.md) sets up the machine.

### 3. The add-on in Blender

Ask the agent to run `blender_install_addon`. It builds `snail_bridge-<version>.zip` in
`~/.snail-mcp-blender/addon` and lists the steps: **Edit → Preferences → Get Extensions → Install from
Disk**, pick the zip. The **Snail** tab in the 3D viewport sidebar (**N**) then says *listening on port 9876*.

### 4. Check

Ask for `blender_diagnose`: with Blender open it reports the Blender version and `reachable: true`.

## Configuration

Environment variables, all optional:

| Variable | Default | Meaning |
|---|---|---|
| `SNAIL_MCP_BLENDER_DATA_DIRECTORY` | `~/.snail-mcp-blender` | This server's files: the add-on zip, downloads without a project, the token of a local Blender |
| `SNAIL_MCP_BLENDER_PROJECT_DIRECTORY` | the folder the client starts the server in | Over stdio, the project: downloads land in it, relative uploads are read from it. A server over HTTP has none |
| `SNAIL_MCP_BLENDER_BRIDGE__PORT` | `9876` | Port the add-on listens on |
| `SNAIL_MCP_BLENDER_BRIDGE__REQUEST_TIMEOUT_SECONDS` | `30` | Timeout for one add-on reply |
| `SNAIL_MCP_BLENDER_IDLE_TIMEOUT_MINUTES` | `0` | Over stdio, exit after this long without a tool call; `0`, the default, keeps it running. A server over HTTP never exits on its own |
| `SNAIL_MCP_BLENDER_SKILL_IDLE_MINUTES` | `0` | Unload a skill unused for this many minutes; `0` keeps skills loaded, which suits clients that cache the tool list |
| `SNAIL_MCP_BLENDER_BRIDGE__TOKEN` | — | Shared secret for the add-on; empty reads the one the add-on generated into `<data>/state/token` |
| `SNAIL_MCP_BLENDER_BRIDGE__TOKEN_FILE` | `<data>/state/token` | Where to read that token, e.g. a copy of a remote add-on's |
| `SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__HOST` | — | `user@host` of a Blender on another machine; the server keeps an SSH tunnel to it while it runs |
| `SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__KEY` | — | Private key for that login |
| `SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__REMOTE_PORT` | `9876` | The add-on's port over there; `BRIDGE__PORT` is the local end |
| `SNAIL_MCP_BLENDER_AGENT` | — | Name this server signs its requests with; the add-on's journal and leases tell agents apart |
| `SNAIL_MCP_BLENDER_TRANSPORT` | `Stdio` | `Http` runs the server on its own beside Blender; the `HTTP__*` settings in [docs/content/configuration.md](docs/content/configuration.md) go with it |
| `SNAIL_MCP_BLENDER_PYTHON` | `allow` | `fallback` refuses a script whose work the tools already do, in a sequence or a batch too; `off` switches `blender_python` off together with the operators that run scripts, sequences and batches holding a script, and uploads outside the add-on's `files` folder |
| `SNAIL_MCP_BLENDER_OCIO_CONFIG` | — | OpenColorIO config background render jobs run under |

A JSON file works too: `.snail-mcp-blender.json` in the working directory or
`~/.config/snail-mcp-blender/config.json`, with the same keys under a `Snail` section. The whole list is in
[docs/content/configuration.md](docs/content/configuration.md).

## How it works

The add-on listens on `127.0.0.1:9876` — in the Docker image on the private network, started with `--host` — and accepts
one JSON line per request. Its socket threads leave the scene alone: they queue the request, and Blender's main thread
runs it — through a `bpy.app.timers` callback with the interface open, or the loop of `blender -b -c snail_bridge`
without one — and writes one JSON line back. `ping`, `progress`, the file transfers and the storage commands are
answered from the socket thread, so a busy Blender still reports what it is doing and still moves files. Every request carries a token — the add-on generates one into
`~/.snail-mcp-blender/state/token` and the server reads it from there, so a fresh install needs nothing
typed; a remote add-on's token is copied into a file `BRIDGE__TOKEN_FILE` names. A failure comes back as data with a type, a message and details, and the server adds a hint that
fits the type. A call that outlives its timeout resets the link so a late reply can never be mistaken for
the next answer.

Both sides carry a digest of the add-on's Python, so `blender_diagnose` says whether the add-on installed in
Blender is the build the server ships — and, when it is not, which commands it lacks.

Towards the client, every tool answers with `structuredContent` next to the text, flags a failed call with
`isError`, and declares through its annotations whether it is read-only, destructive or open-world. Two
resources, `snail://renders/last` and `snail://journal`, hand the last picture and the journal to clients
that read resources.

Tools live in four layers, from most to least specific: the dedicated tools, found by intent with
`blender_find_tool`, which opens the skill they live in; `blender_run`, which sends a list of them to the
open Blender in one call, so a build of hundreds of steps stays inside the guards instead of becoming a
script; `blender_run_operator` for any of Blender's thousands of operators with `blender_describe_operator`
for their properties; and `blender_python` for what none of them reaches. 42 tools are always on; the other
107 arrive with a skill. A script skips validation and leases, and the journal records only that it ran, not what it
touched, so its reply names the tools that cover what it did, and `blender_diagnose` reports how much of the traffic went that way.

Blender can also run on another machine — a workstation, or a GPU server with no display through
`blender -b -c snail_bridge` — with renders, jobs and files living there. The server opens the SSH tunnel itself for
as long as it runs, and the folder you work in is the project: `blender_upload` and `blender_download` mirror it with
its twin on the other machine. [docs/content/remote.md](docs/content/remote.md) walks through the set-up, and
[docs/content/workflow.md](docs/content/workflow.md) is the working order: which Blender is in use, what belongs to which machine, and
where projects and renders live.

The third way puts both on the GPU machine in Docker: `ghcr.io/orldev/s3l-blender` is Blender with the add-on,
`ghcr.io/orldev/s3l-blender-mcp` the server in HTTP mode, and `deploy/compose.yml` joins them on an internal network.
Clients connect to one HTTPS address with a bearer token; projects live in a volume, `blender_open_project` opens one,
and `blender_upload` and `blender_download` hand the agent `curl` commands with links that expire. The server's own pages,
behind a sign-in with the client token, show the status and let you look through the projects, render jobs, batches and
snapshots, cancel a job and delete what is done with.
[docs/content/docker.md](docs/content/docker.md) walks through it.

## Developing

One folder per concern:

| Folder | Holds |
|---|---|
| `src/` | the server: `Configuration`, `Domain` with the command catalog in `Commands`, `Application`, `Adapters`, `Hosting`, `Web`, `Extensions`, and `Tools` — `Base` for the layer that is always on, one folder per skill, and `Prompts` for the descriptions, messages and prompts the model reads |
| `addon/` | the Blender add-on, one Python module per area |
| `tests/` | a folder per layer of `src`, `Contracts` for the guards, `Tools` and `Live` split by skill like the tools, `Support` for the fakes and the headless Blender |
| `docs/` | the documentation site: `mkdocs.yml`, the pages in `content/`, the `theme/` the server's pages embed too, and the `generator/` of the tool reference |
| `deploy/` | the two images, each with its Dockerfile and its ignore file, the compose file with its `.env.example`, and the Traefik route |
| `.mcp/` | the MCP registry entry the NuGet package carries |

`global.json` pins the .NET SDK to the 10.0.3xx band that CI uses; an older SDK refuses the build with a message naming
the version.

```bash
dotnet build
dotnet test
ruff check addon                   # the add-on's Python, configured by ruff.toml
dotnet run --project docs/generator # regenerates docs/content/tools from the server itself
docker build -f deploy/blender/Dockerfile -t ghcr.io/orldev/s3l-blender:latest .
docker build -f deploy/mcp/Dockerfile --build-arg VERSION=0.1.0 -t ghcr.io/orldev/s3l-blender-mcp:latest .
pip install -r docs/requirements.txt
mkdocs serve -f docs/mkdocs.yml    # the documentation site, with its own theme in docs/theme/
```

GitHub Actions: `ci.yml` builds and tests every push, lints the add-on, builds the site, runs the live tests against
Blender 5.2.1 and builds both images when their sources change. A release is a tag, `git tag v0.2.0 && git push --tags`,
and no file holds its version: the checkout builds as `0.0.0-dev`, and `release.yml` writes the version of the tag into
the assembly, the package and its registry manifest and the image tags. It repeats the whole CI, pushes both images to
GHCR under their version, starts the pair to see that the server reaches Blender, then sends the package to NuGet, and
only then creates the GitHub Release, publishes the documentation site and moves `latest` — the last two for a tag
without a suffix. Dependabot proposes updates every two months.

The repository's `.mcp.json` holds an entry for the Debug build, commented out; uncomment it to register that build
with Claude Code. Most tests do not need Blender: the
add-on side is a scripted fake over a real TCP socket, and contract tests keep the C# command catalog equal
to the registry in the add-on's Python. The live tests do need one — they start a headless Blender 5.2 and
send it every command of the catalog but the four that need Blender's interface — and are skipped when none is installed.

## License

MIT
