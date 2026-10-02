# Working with a local and a remote Blender

The same server drives either a Blender on this machine or one on another machine. This page is the working order for
both: what runs where, how one is chosen, what belongs to which machine, and where projects and renders live. A third
set-up puts the server beside Blender in Docker and the client reaches it over HTTPS; its own order is in
[Blender in Docker](docker.md#6-a-session).

## What runs where

| Piece | Local Blender | Remote Blender | Blender in Docker |
|---|---|---|---|
| The client (Claude Code) | this machine | this machine | this machine, any folder |
| The MCP server | this machine | this machine | the GPU machine, container `s3l-blender-mcp`, behind a reverse proxy |
| Blender with the add-on | this machine, with its window | the other machine, as a service without a window, or with a window over Remote Desktop | the GPU machine, container `s3l-blender`, without a window |
| The link | `127.0.0.1:9876` | `127.0.0.1:9877` here, carried by the SSH tunnel the server opens, to `127.0.0.1:9876` there | `https://<address>/mcp` from the client; `blender:9876` on Docker's internal network from the server |
| The token | the add-on writes it to `~/.snail-mcp-blender/state/token`, the server reads it | a copy in `~/.snail-mcp-blender/remote/<machine>.token`, named by `BRIDGE__TOKEN_FILE` | the client token as a bearer in the client's entry; the bridge token in `secrets/bridge.token`, which never leaves the machine |

Over stdio the server never moves: it runs next to the client, which starts it for a session and stops it at the end.
Only Blender moves, and with it everything Blender touches. In Docker the server moves with Blender: it runs in its own
container beside it, outlives every client, and the client reaches it over HTTPS.

## Choosing the Blender

Which Blender a project works with is decided by which MCP entry named `blender` is in force:

- **Local** — an entry named `blender` without a tunnel, such as `claude mcp add blender -- snail-mcp-blender`. The
  repository's `.mcp.json` keeps such an entry for the Debug build, commented out; uncomment it to use it.
- **Remote** — a local-scope entry of the same name, added with `claude mcp add blender --scope local` and the tunnel
  settings from [Blender on another machine](remote.md). A local-scope entry takes precedence over the project's, and it
  lives in `~/.claude.json`, private to you and out of git. With `--scope user` the same entry follows you into every
  folder, which is what working in each project's own folder needs; a local-scope entry still wins inside its folder.
- **Docker** — an HTTP entry of the same name, `claude mcp add --transport http --scope user blender https://<address>/mcp
  --header "Authorization: Bearer …"`, from [Blender in Docker](docker.md#5-connect-claude-code). `blender_diagnose` then
  reports `transport.kind: http` and no tunnel.

To switch to the remote Blender, add the local-scope entry; to switch back, `claude mcp remove blender -s local`. Either
way, reconnect the server with `/mcp` so the new entry takes effect. To see which one you are talking to,
`blender_diagnose`: `blender.machine.platform` names the machine (`darwin`, `win32`, `linux`), and a `tunnel` block appears
only when the server opened one.

## What belongs to which machine

Everything that happens inside Blender happens on Blender's machine, and so does everything Blender remembers:

- **Paths.** Every path a tool takes or returns is a path on Blender's machine — the `.blend` to open, the texture to
  load, the file to render to. A path on this machine means nothing to a remote Blender; send the file with
  `blender_upload` first and use the path it returns.
- **State.** The journal, leases, render jobs, batches and snapshots are kept by the add-on in its data directory, on its
  machine. The local and the remote Blender each have their own and never see the other's.
- **The scene.** What is open in Blender lives in that Blender's memory until it is saved. A remote service starts with an
  empty scene after every restart of the machine or of the service, so nothing survives that was not saved.

What stays on this machine is the server's own: the add-on zip it builds, the files it downloads, and the token copy for a
remote Blender.

## Where things are stored

**On this machine**, in `~/.snail-mcp-blender`:

| Folder | What |
|---|---|
| `addon/` | the add-on zip `blender_install_addon` builds, to install into a Blender |
| `downloads/` | files `blender_download` fetched without a `localPath` while there was no project — the client started in the home folder or at the root |
| `remote/` | token copies of remote Blenders, readable by you alone |
| `files/`, `state/`, `jobs/`, `batches/`, `snapshots/` | the local Blender's add-on data, when the local Blender is the one in use: its projects and uploads, token, journal, render jobs, batches and snapshots |

**On Blender's machine**, in the add-on's data directory — `C:\Users\<profile>\.snail-mcp-blender` on Windows,
`~/.snail-mcp-blender` elsewhere; `blender_diagnose` shows it as `blender.machine.data_directory`:

| Folder | What |
|---|---|
| `files/` | what `blender_upload` sent with a relative path, and the projects `blender_open_project` opens; the natural home of remote projects |
| `jobs/<job-id>/` | one render job: `scene.blend` (the copy it renders), `spec.json`, `status.json`, `log.txt` |
| `batches/<batch-id>/` | one batch: `input.blend`, `output.blend`, its spec, status and log |
| `snapshots/` | `<name>.blend` with `<name>.json` saying where it came from and why |
| `state/` | `token`, `journal.jsonl`, `leases.json`, `jobs.json` |

**In Docker**, the add-on's data directory is `/home/blender/.snail-mcp-blender` in the `s3l-blender` container, kept in
the volume `s3l-blender_data`, with the same folders.

Nothing in `jobs/` and `batches/` is removed on its own; clear old ones by hand when the disk asks for it, or, with
Blender in Docker, from the server's Jobs and Batches pages.

**Projects.** The folder you start the client in is the project. The client starts the server there, so the server
takes it as the project without being told, and `blender_diagnose` shows it under `project`. Its twin on Blender's
machine is the add-on's `files\<the folder's name>`, and the two mirror each other:

```
this machine: ~/Projects/lobby/                  Blender's machine: C:\Users\Artist\.snail-mcp-blender\files\lobby\
    textures/wood.png      -- blender_upload -->      textures\wood.png
    renders/turntable/     <-- blender_download --    renders\turntable\f_0001.png …
    lobby.blend            <-- blender_download --    lobby.blend
```

A relative path given to `blender_upload` is read from the project, and without a `remotePath` the file lands in the
twin at the same relative path. `blender_download` without a `localPath` brings a file from the twin back to the same
relative place in the project, and a file from anywhere else into the project under its own name. So the source of a
project stays where you work — versioned and backed up — and the other machine holds a working copy of it.

Start the client in the project's folder for this to hold. Started in the home folder or at the root there is no
project, and downloads go to `~/.snail-mcp-blender/downloads` as before; `SNAIL_MCP_BLENDER_PROJECT_DIRECTORY` names a
project folder explicitly. Started inside a source repository, renders land in the repository, next to its code.

With Blender in Docker there is no project on the server's side: a server reached over HTTP sees none of the client's
folders, `blender_diagnose` says so under `project.note`, `blender_open_project` names the project in the volume, and
`blender_upload` and `blender_download` return commands the client runs from its folder — see
[Blender in Docker](docker.md#6-a-session).

**Renders** land wherever the render call says — a render tool always takes the output path — so point them into the
twin's `renders\` folder on Blender's machine (`blender_diagnose` gives its full path as `project.twin`), and a
`blender_download` of the twin's `renders` brings them into the project's `renders/` here. A render job additionally
keeps its scene copy and log in `jobs/<job-id>/`.

## A session, in order

1. **`blender_diagnose`** — the right machine (`blender.machine.platform`), the tunnel `up`, the add-on `current`. A `tunnel.lastError`
   or `reachable: false` is the first thing to fix; [troubleshooting](troubleshooting.md) covers each case.
2. **Open the project** — `blender_open_project` with the name of the project's folder opens `<name>.blend` in the twin,
   or creates it with `renders/` and `textures/` beside it and the scene's output set to `//renders/`; it refuses while
   the open file has unsaved changes unless told to discard them. `blender_open_file` on another `.blend`, or
   `blender_new_file` and `blender_save_file` into the twin (`project.twin` in `blender_diagnose`), still work.
3. **Bring what it needs** — `blender_upload` with a path relative to the project (`textures`); it lands in the twin at
   the same place. Local work skips this step: Blender reads this machine's paths.
4. **Build** — `blender_find_tool` for what each step needs, `blender_run` for many steps in one call.
   `blender_snapshot` before anything risky.
5. **Look** — `blender_render_image` for short previews (it blocks Blender until it finishes and cannot be stopped);
   `blender_render_animation`, which renders as a background job by default, or `blender_render_job` for finals, with
   `blender_render_job_status` to follow and `blender_render_job_cancel` to stop. On a remote NVIDIA machine
   `blender_set_cycles` with `backend: OPTIX` puts the GPU to work.
6. **Save** — `blender_save_file`. On a remote service this is what survives a restart.
7. **Bring the results home** — `blender_download` of the twin's `renders` and `.blend`, or of the whole twin; without a
   `localPath` they land back in the project at the same places. Until then they exist only on the other machine.

When the session ends the client stops the server, and the server closes the tunnel. The remote service keeps running
with the scene as it was, until it is stopped or the machine restarts.
