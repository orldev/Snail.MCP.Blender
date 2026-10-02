# Blender on another machine

The server runs next to the MCP client; Blender can run somewhere else — a workstation, or a GPU server with no
display — and do all the modelling and all the rendering there. The link does not change: the add-on listens on
`127.0.0.1` on its machine, the server connects to `127.0.0.1` on yours, and an SSH tunnel joins the two.

To run the server on that machine too, in Docker next to Blender, and reach it over HTTPS instead of a tunnel, see
[Blender in Docker](docker.md).

Never open the bridge port to the network instead. The bridge runs Python inside Blender and its token travels as
plain text; the add-on listens on the loopback, and only its headless command takes `--host`, for the private network of
[Blender in Docker](docker.md). SSH is what adds encryption and a login.

## 1. Prepare the remote machine

Blender 5.2 and a current GPU driver. Then SSH, which Windows ships as an optional feature — in an elevated PowerShell:

```powershell
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
Start-Service sshd
Set-Service sshd -StartupType Automatic
```

Install the add-on without opening Blender. `blender_install_addon` builds the zip on your machine and names it; copy
it over and let Blender install it from the command line:

```bash
scp ~/.snail-mcp-blender/addon/snail_bridge-0.1.0.zip artist@render-box:C:/Users/artist/Downloads/
ssh artist@render-box "\"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe\" --command extension install-file -r user_default -e C:\Users\artist\Downloads\snail_bridge-0.1.0.zip"
```

On Linux the second line is `ssh artist@render-box blender --command extension install-file -r user_default -e ~/snail_bridge-0.1.0.zip`.

## 2. Start Blender there

**With a display** — a workstation, or a session over VNC or Remote Desktop: start Blender as usual. The add-on starts
listening on its own.

**Without a display**: the add-on brings its own command.

```bash
blender -b -c snail_bridge --port 9876 --file D:/shots/lobby.blend
```

A background Blender runs no timers, and the interface runs the bridge's queue and the render job queue on timers;
this command runs both itself and keeps Blender answering until the process is stopped. All flags are optional:
`--port` (the preferences' port otherwise), `--file` to open a `.blend` first, `--token-file` for a file holding the token
to demand instead of the generated one, and `--backend` — `OPTIX`, `CUDA`, `HIP`, `ONEAPI`, `METAL` or `NONE` — to enable
a Cycles compute backend before listening; `--host` is for the Docker image alone. The commands that need Blender's
window — `viewport_capture`, `set_viewport_shading`, `sequencer_meta`, `sequencer_proxy`, `scenes` when it activates a
scene or makes a full copy, and `blender_run_operator` with an `area` — answer `NoArea` there; everything else, rendering
included, works.

To start it with the machine on Windows, register a scheduled task that runs as the artist without storing a password —
the S4U logon — so the add-on finds its preferences and its data directory in that profile. In an elevated PowerShell:

```powershell
$action = New-ScheduledTaskAction -Execute 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -Argument '-b -c snail_bridge --port 9876' -WorkingDirectory $env:USERPROFILE
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "$env:COMPUTERNAME\$env:USERNAME" -LogonType S4U -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -StartWhenAvailable -MultipleInstances IgnoreNew -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName 'Snail Bridge' -Action $action -Trigger $trigger -Principal $principal -Settings $settings
Start-ScheduledTask -TaskName 'Snail Bridge'
```

An execution time limit of zero matters: the default of 72 hours would end Blender on the third day. An S4U task runs in
session 0, with no window; `Get-ScheduledTaskInfo` reporting 267009 means "running", not an error.

### Managing the service

From a shell on the Windows machine, or from yours through ssh — Windows OpenSSH answers in `cmd`, so each command runs
as it is, for example `ssh -i ~/.ssh/id_ed25519 artist@render-box schtasks /end /tn "Snail Bridge"`:

| To | Command |
|---|---|
| see whether it runs | `schtasks /query /tn "Snail Bridge"` — the status column is in the system's language |
| see whether it listens | `powershell Get-NetTCPConnection -LocalPort 9876 -State Listen` |
| stop it now | `schtasks /end /tn "Snail Bridge"` — ends that Blender, and whatever its scene held unsaved |
| start it again | `schtasks /run /tn "Snail Bridge"` |
| keep it from starting with the machine | `schtasks /change /tn "Snail Bridge" /disable`; `/enable` undoes it |
| remove it | `schtasks /delete /tn "Snail Bridge" /f` |

A stopped service closes the port, so the server's commands answer `Unavailable` until it runs again. The task keeps no
log — Task Scheduler drops Blender's output — so its state shows in `blender_diagnose`. While it runs, a Blender opened
by hand on that machine works as usual but cannot listen on 9876, which the service holds: stop the service first to
drive the Blender with the window.

On Linux, a systemd unit:

```ini
[Unit]
Description=Snail Bridge
After=network.target

[Service]
User=artist
ExecStart=/opt/blender/blender -b -c snail_bridge
Restart=on-failure

[Install]
WantedBy=multi-user.target
```

## 3. The tunnel and the token, on your machine

**Let the server open it** — the tunnel is then up exactly while you work with Blender. The client starts the server when
a session uses it and the server exits with the session; a server with a tunnel host starts `ssh -N -L` itself, reopens
it after a drop and closes it on exit, so nothing stays open in between. Everything goes into the environment of the
MCP entry, and none of it is a secret — the token is read from a file:

```bash
ssh -i ~/.ssh/id_ed25519 artist@203.0.113.42 "type %USERPROFILE%\.snail-mcp-blender\state\token" > ~/.snail-mcp-blender/remote/render-box.token
chmod 600 ~/.snail-mcp-blender/remote/render-box.token

claude mcp add blender --scope local \
  -e SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__HOST=artist@203.0.113.42 \
  -e 'SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__KEY=~/.ssh/id_ed25519' \
  -e SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__REMOTE_PORT=9876 \
  -e SNAIL_MCP_BLENDER_BRIDGE__PORT=9877 \
  -e 'SNAIL_MCP_BLENDER_BRIDGE__TOKEN_FILE=~/.snail-mcp-blender/remote/render-box.token' \
  -- snail-mcp-blender
```

The local scope keeps this out of a shared `.mcp.json` and ties it to the folder you ran it in; with `--scope user`
it follows you into every folder, and each folder you start the client in becomes the project. `BRIDGE__PORT` is the local end — 9877 here, so a Blender on your
own machine can keep 9876 — and `TUNNEL__REMOTE_PORT` is the add-on's port over there. ssh runs with `BatchMode`, so it
never waits for a prompt nobody sees: connect once by hand first to accept the host key. `blender_diagnose` shows the
tunnel under `tunnel` — up or not, since when, how many times it was opened, and the last thing ssh said.

**Or keep it open yourself** with an entry in `~/.ssh/config`, so one command opens the tunnel and keeps it alive:

```
Host render-box
    HostName 192.168.1.50
    User artist
    LocalForward 9876 127.0.0.1:9876
    ServerAliveInterval 30
    ServerAliveCountMax 3
    ExitOnForwardFailure yes
```

`ssh -N render-box` holds it open; `autossh -M 0 -N render-box` also reopens it after a drop. If a Blender on your own
machine already listens on 9876, forward another local port — `LocalForward 9877 127.0.0.1:9876` — and set
`SNAIL_MCP_BLENDER_BRIDGE__PORT=9877`.

For this way too the token has to come across: the add-on writes it into the data directory **of its machine**, where the
server cannot read it, so pass it explicitly:

```bash
ssh render-box type %USERPROFILE%\.snail-mcp-blender\state\token     # Windows
ssh render-box cat ~/.snail-mcp-blender/state/token                  # Linux
```

```json
{
  "mcpServers": {
    "blender": {
      "command": "snail-mcp-blender",
      "env": { "SNAIL_MCP_BLENDER_BRIDGE__TOKEN": "the value printed above" }
    }
  }
}
```

## 4. Check

`blender_diagnose` should say `reachable`, the add-on `current`, the tunnel `up`, under `blender.machine` the remote platform
(`win32`, `linux`), its home and its data directory, and under `project` the folder you work in with its twin over
there. Every path a tool takes is a path on that machine.

## 5. Working remotely

- **Files.** The folder you start the client in is the project, and its twin over there is `files\<its name>`;
  `blender_open_project` with the folder's name opens `<name>.blend` in the twin, or creates it with `renders` and
  `textures` beside it and the scene's output set to `//renders/`. `blender_upload` of a path in the project lands in the twin at the same place, and `blender_download` from the twin
  brings frames and files back to the same place in the project — see [the working order](workflow.md). Both move a whole folder when given one, in chunks, and a file
  takes its name only once its SHA-256 matches on arrival.
- **GPU.** `blender_set_cycles` with `backend` — `OPTIX` or `CUDA` on NVIDIA, `HIP` on AMD, `ONEAPI` on Intel —
  enables that backend's devices, puts the scene on the GPU and lists them — or start the headless command with
  `--backend`. A fresh machine starts at `NONE`, which renders on the CPU; render
  jobs carry the choice to their worker Blender, which would otherwise read only its own saved preferences.
- **Renders.** `blender_render_animation` renders in a second Blender on the remote machine by default and returns a
  job id; `blender_render_job_status` follows it, `blender_render_job_cancel` stops it, and `blender_download` fetches
  the frames. Jobs, batches and snapshots live in the add-on's data directory there.
- **Many steps.** Every call is a round trip over the tunnel, so builds belong in `blender_run`: one call for hundreds
  of steps.

On a machine other agents or people share, set `SNAIL_MCP_BLENDER_PYTHON=off`: Python is refused, and uploads may land
only in the `files` folder, since a file written anywhere else can be code Blender runs later.
