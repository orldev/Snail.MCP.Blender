# Troubleshooting

Start with `blender_diagnose`: it reports whether the add-on answers and whether it is the build this server
ships, the machine Blender runs on, the tunnel to it and the project folder when there are any, the port both sides
use, and the traffic so far.

## "The Blender add-on is not answering"

- Blender is not running, or the extension is not installed or disabled. Install it with the zip from
  `blender_install_addon`; the **Snail** tab in the 3D viewport sidebar (**N**) shows the state.
- The ports differ. The add-on preferences and `SNAIL_MCP_BLENDER_BRIDGE__PORT` must agree; 9876 by default.
- Another program holds the port. Change it on both sides.

## "Blender did not answer 'command' within N s; the link was reset" (`Timeout`)

Blender was busy: a render, a heavy import, a modal operator waiting for input, or a dialog. Finish it in
Blender; the next call reconnects. Pass a longer `timeoutSeconds` for imports. A render that timed out is still
going — see [I stopped the agent and Blender keeps rendering](#i-stopped-the-agent-and-blender-keeps-rendering) — and
the next one that long belongs in a background job.

## "the add-on expects the token set in its preferences" (`Unauthorized`)

The add-on has a token in its preferences (Edit → Preferences → Get Extensions → Snail Bridge → Token) and
the server sent none or another one. Set `SNAIL_MCP_BLENDER_BRIDGE__TOKEN` on the server to the same value, or clear the
field in Blender and press Stop, then Start, in the **Snail** tab: the add-on reads its token when it starts listening,
and then uses the one it generated, which the server reads when both use the same data directory.

## "requests are already waiting for Blender's main thread" (`Busy`)

The add-on holds at most 64 queued commands. A render inside a tool call keeps the main thread for its whole
length, so calls behind it pile up. Wait for it, or move renders longer than a preview to `blender_render_job`.

## "the link to Blender stayed busy with another request" (`Busy`)

The server sends one command at a time, and this one's own timeout ran out while another was still in flight —
a blocking render, say. Nothing of it was sent, so nothing of it ran: repeat it once the long call is done, or give it
a longer `timeoutSeconds` than the call it waits behind.

## "the add-on's token file cannot be read" (`TokenUnreadable`)

The server found the file but was not allowed to read it, or something else holds it. In Docker both containers read
the same secret, so its mode has to let both of their users read it; on one machine, the file under the add-on's data
directory belongs to whoever runs Blender.

## "timeoutSeconds is outside the 1 to 600 range this server accepts"

The per-call timeout is refused rather than quietly corrected, so a call meant to wait an hour does not come
back after ten minutes as if the render had failed. Renders longer than 600 seconds belong in
`blender_render_job`, which returns at once and is polled.

## "cannot run in the current context" (`PollFailed`)

The operator's poll rejected the state Blender is in: wrong mode, no active object, nothing selected.
Check `blender_scene_info`, select with `blender_select_objects`, and pass `area=VIEW_3D` to
`blender_run_operator` for editors that need a 3D viewport. Mesh edits are easier through the `modeling`
skill, which works on the mesh data directly and needs no viewport.

## "step N (command) failed" (`StepFailed`)

One step of a `blender_run` sequence was refused; the error carries `steps` with every step that ran, its
result or its error, and `failed` with the indexes. The sequence stops there unless it was sent with
`continueOnError`, in which case it runs to the end and fails as `N of M steps failed` with the same `steps` and
`failed`; either way the scene holds the steps that succeeded. Fix the step the error names and send the rest.
A step refused as `Leased` means another agent holds that object: the guard applies inside a sequence exactly
as it does to a direct call.

## "'command' takes no 'parameter'" (`BadRequest`)

The command does not read that parameter, so it would have been dropped and the work left undone. The error names the
parameter, suggests the one it was probably meant to be, and carries `accepted` with every parameter the command does
take. This reaches you through every path — a tool, a step of `blender_run`, a call inside `blender_program`, a job in
`blender_batch` — because the check lives in the add-on, where all of them arrive.

It used to be silence: `blender_set_material` sent an `objects` list, answered ok and assigned nothing;
`blender_render_animation` sent `frame_start` and `frame_end`, answered ok and rendered the whole scene range; optics
sent for a camera named by `name` moved whichever camera the scene had. The reply said the work was done, so the fault
was looked for in the scene. Where a parameter belongs to no command at all, the reference page for the tool lists the
ones that do; `blender_find_tool` finds the tool for what you are trying to do.

## A call answered ok and the scene did not change

Read the reply against what you asked for. Every command answers with what it did, not with the word ok:
`blender_assign_material` returns the slot it filled, the slot list and how many faces now point at it;
`blender_set_camera_optics` returns the camera it worked on and its lens; a render returns the file it wrote. A number
that is not the one you asked for is the answer to what happened.

Two defaults used to be worth this note by themselves. `blender_assign_material` added a slot at the end and left the
faces on slot 0, which changed nothing anyone could see; it now fills slot 0 and moves the faces there, and `append`
asks for the old behaviour, which is what a mesh wearing several materials needs. `blender_lens_effects` with no effect
named read as "clear" and took the compositor's chain down; it is now refused, and the chain in place is read with
`blender_compositor` and `action: info`.

## "this script does N things this server has tools for", or "Step N is a script that does…"

The server is configured with `SNAIL_MCP_BLENDER_PYTHON=fallback`, which keeps `blender_python` for API no
tool reaches, and reads a `python` step of `blender_run` or `blender_batch` the same way; a refused sequence or batch
sends no step at all. The error's data names the tools that cover what the script did; call them, or send them as one
`blender_run` sequence. `blender_find_tool` loads the skill they live in. An administrator relaxes this with
`SNAIL_MCP_BLENDER_PYTHON=allow`, where the same list comes back as `advice` beside a successful result.

## "blender_python is switched off by configuration on this server"

The server runs with `SNAIL_MCP_BLENDER_PYTHON=off`, which also refuses the operators that run scripts, sequences and
batches with a `python` step, and uploads outside the add-on's `files` folder. Use the dedicated tools or
`blender_run_operator`; an administrator enables Python with `SNAIL_MCP_BLENDER_PYTHON=allow`.

## `Unauthorized` through an SSH tunnel

The add-on generated its token into the data directory of its own machine, and the server looked for it in its own.
Read it there (`type %USERPROFILE%\.snail-mcp-blender\state\token` on Windows, `cat ~/.snail-mcp-blender/state/token`
elsewhere), save it into a file readable by you alone and point `SNAIL_MCP_BLENDER_BRIDGE__TOKEN_FILE` at it — or put
the value itself in `SNAIL_MCP_BLENDER_BRIDGE__TOKEN`; [Blender on another machine](remote.md) has the whole set-up.

## `blender_diagnose` says the tunnel is not up

The server opens the SSH tunnel itself and keeps reopening it; `tunnel.lastError` is the last thing ssh said. `Permission
denied (publickey)`: the key in `BRIDGE__TUNNEL__KEY` is not authorised on that machine — on Windows an administrator's
key belongs in `C:\ProgramData\ssh\administrators_authorized_keys`, not in the profile. `Host key verification failed`:
ssh runs without prompts, so connect once by hand to accept the key. `Address already in use`: something else holds the
local port — often a tunnel an earlier server left behind when it was killed; `lsof -i :<BRIDGE__PORT>` names it
(9877 in [Blender on another machine](remote.md)), `netstat -ano | findstr :<port>` on Windows.

## A path that exists here is "not found" in Blender

Blender runs on another machine and every path is read there. `blender_diagnose` shows that machine under `blender.machine`;
send the file with `blender_upload` and pass the path it returns.

## Downloads land in `~/.snail-mcp-blender/downloads`, not in my folder

The client was started in the home folder or at the root, and neither counts as a project, so downloads went to the
server's own folder instead of scattering across your home. Start the client in the project's folder, or set
`SNAIL_MCP_BLENDER_PROJECT_DIRECTORY`; `blender_diagnose` shows under `project` where downloads go.

## `Exists` or `Corrupt` from a transfer

`Exists`: the target is already there and the transfer keeps it; pass `overwrite`. `Corrupt`: the file that arrived
does not match the SHA-256 of the one sent, so it was not kept under its name — send it again; the files before it in
the same folder did arrive and are listed. `TooManyFiles`: the folder holds more than 20,000 files, more than one transfer
carries, so nothing was downloaded; download its folders one by one. A folder below that comes down page by page.

## `401` from `/mcp`, or the pages send me back to sign in

The server beside Blender admits a client holding the token in `secrets/client.token` or a key minted with
`snail-mcp-blender clients add`: Claude Code sends it as `Authorization: Bearer <key>` in the MCP entry, and the pages
take the same key at sign-in. A key copied with a trailing newline still works; one from an older copy of the file, or
one that was revoked, does not — a revoked key ends the browser session it opened at the next request. A session on the pages lasts twelve hours, is
renewed while the pages are in use, and survives a restart of the container, because its keys live in the `keys` volume.

## `curl` from `blender_download` or `blender_upload` answers `401`

The link inside the command works for its path and its method, and only until the `expires` the reply names — 15 minutes
by default. Call the tool again for a fresh command. A link also stops working when the client token changes.

## `curl` from the command answers `409`, `403`, `400` or `413`

`409`: the file is already there — call `blender_upload` again with `overwrite` — or, on a deletion, it is the file open in
Blender or a job or batch still queued or running (`InUse`); open another file, or cancel or wait, first. `403`: only the
`files` area takes uploads. `400`: the path climbs out of its area, or a part does not follow the last one that arrived —
run the whole command again. `413`: the folder holds more than 5,000 files; download its folders one by one.

## An upload answers `500`, or `502` on the part after it

The server ran out of memory while sending a chunk of the file to Blender, and the container it runs in is held to a
gigabyte by `deploy/compose.yml`. Both codes are the same fault: `500` is the exception reaching the request, and `502`
is what the next part gets once the link to Blender has been dropped. Blender's own log says nothing, because the chunk
never arrived.

It was fixed by sending the chunk as the bytes it is: the JSON encoder used to escape `+` and `/`, which are two of the
sixty-four characters of base64, and its escaping path reserves six bytes per character before counting how many it
needs — one eight-megabyte chunk cost 225 MB. A server built before that fix shows it on files of tens of megabytes
while a small one goes through; `blender_diagnose` names the build it is running. Raising `MCP_MEMORY` works around it
without fixing it.

## The download command stops with "already exists" or "the download stopped"

"already exists": the command keeps a file that is there; call `blender_download` again with `overwrite` to replace it.
A folder keeps the files that exist and brings the rest without complaint. "the download stopped: curl exit N": the
transfer broke off — the link expired, the server went away, or a file differed from the SHA-256 the add-on computed and
the server cut it off; call the tool again for a fresh command.

## `curl` from the command cannot connect to `0.0.0.0:8080`

The links are built on `SNAIL_MCP_BLENDER_HTTP__PUBLIC_URL`, and without it on the address the server listens on inside
its container. Set `PUBLIC_URL` in `.env` to the address clients reach through the reverse proxy and recreate the
container.

## An upload through the command stops with `422 Corrupt`

The last part carries the SHA-256 of the whole file, and what arrived differs from it — usually because the file changed
while it was being sent. Run the command again; the first part starts the file afresh.

## The address answers `404` or shows Traefik's default certificate

The reverse proxy has not loaded the route. Traefik's file provider does not notice a file added to a bind-mounted
Windows folder on Docker Desktop; restart Traefik. `curl http://<publish address>:8810/healthz` on the machine itself
tells whether the server is up behind it.

## `OptiX initialization failed with error code 7805` in the Blender container

Docker Desktop on Windows cannot give a container OptiX; CUDA works. Set `BLENDER_BACKEND=CUDA` in `.env` and
`docker compose up -d`. [Blender in Docker](docker.md#what-the-machine-needs) explains why.

## The tool I want is missing

It lives in a skill. `blender_find_tool` searches the whole catalog by what you are trying to do and loads
the skill the match lives in; `blender_skills` lists them, `blender_enable_skill` loads one and the tools
appear at once, and it stays loaded until `blender_disable_skill`. If tools do vanish on their own, the server was
started with `SNAIL_MCP_BLENDER_SKILL_IDLE_MINUTES` set; `0`, the default, keeps skills loaded.

## I stopped the agent and Blender keeps rendering

Cancelling a tool call stops the waiting, not the work. The command is already on Blender's main thread, and
Blender offers no way to interrupt one from outside: the escape key belongs to the interface, and the add-on's
socket thread can answer questions but not break a render. So the server drops the link, the model gets nothing,
and the frame finishes.

What to do about it: `blender_diagnose` still answers while Blender is busy and says what it is running; the
render ends on its own and the next command runs after it. To avoid the situation, render through
`blender_render_job` — a second Blender, cancellable with `blender_render_job_cancel` — which is what
`blender_render_animation` does by default. A still through `blender_render_image` blocks by design: keep it
short with the resolution percentage and samples.

Cancel of a job is cooperative: it leaves a marker the worker reads between cameras and between the frames of a frame
list, and sends the break signal, so the job stops the frame it is in, writes its own last status and keeps the frames
already on disk.

## A render takes longer than a tool call may

`blender_render_image` blocks Blender and the link until it finishes, and so does `blender_render_animation`
with `background: false`; the client gives up after `timeoutSeconds`. For anything beyond a preview render in the
background — `blender_render_job`, or `blender_render_animation`, which does it by default: the file is saved as a
copy and a second Blender renders it, while this one keeps answering. Poll
`blender_render_job_status` for frames done, the estimated time left and the files written; stop it with
`blender_render_job_cancel`. Run `blender_render_check` first: it names the missing camera, texture or
codec before an hour of rendering does.

## "building them needs the Video Sequencer editor open" (`NoArea`)

Proxy settings are stored on the strips, but Blender builds proxies only from the sequencer editor. Open
a Video Sequencer area in Blender and call `blender_sequencer_proxy` again, or build them in Blender with
Strip → Rebuild Proxy and Timecode Indices.

## The server exits at start-up with `OptionsValidationException`

A setting is out of range — a port, a timeout, negative minutes, link minutes outside 1 to 1440 — and the message names
each one with the range it allows: `Port: The field ServerConfig.Bridge.Port must be between 1 and 65535.` A server over
HTTP without a client token stops the same way with `The HTTP server needs the token clients present: set
SNAIL_MCP_BLENDER_HTTP__TOKEN, or SNAIL_MCP_BLENDER_HTTP__TOKEN_FILE to a file that holds it.`; a missing or empty token
file counts as none. A value that is not a number, or not one of a setting's names, stops the process with
`InvalidOperationException` instead. Fix the environment variable or the settings file; see
[Configuration](configuration.md).

## "is not a bridge command" (`UnknownCommand`), or `blender_diagnose` says the add-on `differs`

The add-on inside Blender is not the build this server ships. Run `blender_install_addon`, reinstall the zip
it names (**Edit → Preferences → Get Extensions → Install from Disk**), and restart Blender. The `addOn` block
of `blender_diagnose` names both digests and lists the commands the installed add-on lacks — those are the
tools that fail until it is reinstalled. For a Blender on another machine, install the zip there from the
command line, as [Blender on another machine](remote.md) shows.

## After updating the server

Run `blender_install_addon` and reinstall the zip in Blender; `blender_diagnose` confirms it with
`addOn.status: current`.
