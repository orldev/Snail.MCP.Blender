# Architecture

## Two processes, one line of JSON

The server is a .NET process: the MCP client starts it over stdio, or it runs on its own beside Blender and serves
clients over HTTP (see [The server beside Blender](#the-server-beside-blender)). Blender runs the **Snail Bridge**
extension, which listens on `127.0.0.1:9876`, or on the address `--host` names when a headless Blender serves the
container beside it. The server connects on the first tool call and keeps the socket open; every request is one JSON
line and every reply one JSON line, matched by id:

```json
{"id": "7", "command": "add_primitive", "params": {"kind": "cube", "location": [0, 0, 1]}, "deadline_s": 60}
{"id": "7", "ok": true, "result": {"name": "Cube", "type": "MESH", "location": [0, 0, 1]}}
```

`deadline_s` is how long the caller will still be waiting, counted from the moment the request arrives — a duration, so
the two clocks need not agree. A request that reaches the front of the main thread's queue after it has passed is
answered `Expired` instead of run: nobody is waiting for the result, and a command run for nobody still changes the
scene the next caller reads.

A failure comes back as data, never as a dropped connection:

```json
{"id": "8", "ok": false, "error": {"type": "PollFailed", "message": "bpy.ops.mesh.extrude_region cannot run in the current context", "details": {"mode": "OBJECT"}}}
```

The server turns it into a tool response with `ok: false`, the message, a hint that fits the error type,
and the details under `data`.

## The main thread

Blender's Python API is not thread-safe. The add-on's socket threads only parse requests and put them in
a queue; a `bpy.app.timers` callback drains the queue on the main thread every 50 ms and writes the
replies. A few commands are answered from the socket thread instead and never touch Blender's data: `ping`, so the
server can tell a busy Blender from a dead one; `progress`, which reports the render in flight while the main thread
is inside it; the three file-transfer commands, so moving a file neither waits for a render nor holds one up; and the
three storage commands, so a page that measures, lists or cleans the data directory never waits for one either.

A command that outlives the server's timeout resets the link: the socket is closed on purpose, because a
late reply would otherwise be read as the answer to the next request. The next call reconnects.

Anything on the machine can open a loopback socket, and anything on a container's network can open that one, so
every request carries a token: the one from the add-on's preferences, the one in the file `--token-file` names, or one
the add-on generated into `<data>/state/token` that the server reads on connect. A request
without the right token comes back as `Unauthorized`. The add-on keeps at most 64 requests waiting; beyond that
it answers `Busy`; a request whose client has gone by its turn, or whose own deadline passed while it waited, is answered rather than run for nobody.

## What the client sees

Every tool answers with JSON: the result under `structuredContent` for a client that reads it, the same
payload as text for one that does not. A failed call carries `isError`, so an orchestrator sees the failure
without parsing prose, and the payload holds the error type, the message and a hint that fits the type.

Every tool states through its annotations whether it is read-only, destructive or open-world, so a client can gate the
destructive ones; a test holds the read-only hint to the list the add-on's lease guard uses and the other two to a
policy that names the commands that can lose work. Renders, captures, bakes and `blender_inspect_image` add an image
block next to the JSON, described below.

Two resources hand data to clients that read resources: `snail://renders/last` is the last picture a tool
returned, `snail://journal` the tail of the add-on's journal. Three prompts — `photoreal_product_shot`,
`farm_exr_delivery`, `vse_grade_and_deliver` — lay out the studio order of tools; see
[Prompts](prompts.md).

## One build on both sides

The server and the add-on share one command catalog, so a server updated without its add-on answers
`UnknownCommand` in the middle of a task — which reads as a broken tool rather than a stale install. Versions
cannot catch that: the add-on's version stays put across bug fixes. So each side hashes its own Python — the
top-level `.py` files in name order, each as its name followed by its bytes — and `ping` carries the add-on's
digest, its version and the number of commands it registers.

A digest says the two sides differ; it cannot say what the difference costs. That is what the protocol number and the
list of abilities beside it are for: `ping` carries both, and the add-on names what it can do — answer the file and
heartbeat commands off the main thread, drop a request whose caller has gone, enforce the Python setting and the
allowed paths itself, keep render jobs as one closed set of states. An add-on behind on either count is behind; one
*ahead* of this server is not, because it answers everything this server knows, and refusing it would make every
upgrade a lockstep.

`blender_diagnose` compares them and answers `current`, `differs`, `older` or `unknown` under `addOn`, with the two
protocol numbers and the abilities the add-on lacks under `protocol`; when they differ it lists the commands the
catalog has and the add-on does not: those are the tools that would fail right now.

## What an operator can see

`ping` also carries the pulse of the dispatcher: how long ago Blender's main thread last drained the queue. It is the
one thing a client outside Blender cannot work out for itself, because ping is answered from the socket thread — a
Blender whose timer has died answers it exactly like a healthy one and merely looks quiet. `/healthz` answers `up`,
`down`, or `stalled` when nothing has run for a minute while nothing is running; a long render is not stalled, it is
holding the main thread on purpose, which is why the two are read together.

`blender_diagnose` reports the traffic since the server started — the busiest commands, the failures by type, the last
one, and, since a server over HTTP serves several clients over one link, a count per client. The same exchanges are
published as metrics under the meter `Snail.MCP.Blender` (`snail.blender.commands` and
`snail.blender.command.duration`, tagged with the command, the outcome and the client) for a host that collects them:
what a model reads in one call is not what an operator watches over a month. The same conclusion arrives on its own too — a reply of type `UnknownCommand` carries the hint to
reinstall and is logged as a warning, and a digest that differs is logged once per installed build.

## Inside the add-on

`core.py` is the registry and the dispatcher: the other 35 modules register their commands with a decorator,
one module per area (scene, objects, cameras, lights, collections, files, operators, modeling, materials,
rendering, images, compositor, world, interchange, sequence, transfer, storage and so on), and hooks run before and after every command
on the main thread.
`@command` runs on the main thread, `@immediate_command` on the socket thread. Imports run one
way, module level only. The two worker scripts, `job_worker.py` and `batch_worker.py`, are files of the package that
a headless Blender runs by path. `team.py` registers the lease guard and the journal writer as hooks,
so the dispatcher knows nothing about agents and no module imports another lazily. `state.py` keeps what
outlives a session under `<data directory>/state`: the journal as one JSON line per command that is not a read, the
leases, the render-job roots to resume, and the token it generated. Files are replaced atomically and a lock file guards every
read-modify-write, because worker Blenders started for batches and jobs write the same files.

## Layers in the server

- **Configuration** — defaults, then the settings file, then the environment, bound through the options pipeline; the ranges are attributes on the settings, checked by a validator generated from them, and `ValidateOnStart` stops the process on a bad value rather than repairing it in silence.
- **Domain** — the command catalog in `Commands`, one partial per add-on module, and the names of those modules; the closed vocabularies the add-on keeps a copy of, the areas of its data directory among them; replies, errors, the add-on's fingerprint and package, and the skills, as records.
- **Application** — the ports of the three links to the add-on, the tunnel and the packager; the traffic trace around the control link (timings and failure counts that `blender_diagnose` reports), the health report, the skill catalog, the tool index and script advice behind discovery; the render watch that turns monitor probes into progress, the last picture behind `snail://renders/last`, and the jobs in flight that pin the farm skill; file transfers and the project folder, the client token and signed file links, and the volume: the add-on's data directory read and written over a link of its own.
- **Adapters** — the TCP clients of the add-on, the SSH tunnel to a Blender elsewhere, the add-on packager, and the program runner: a JavaScript engine without CLR access whose globals are the command catalog, `input` and `log`, driving the same link the tools do, so a program's steps meet the same guards.
- **Hosting** — only over stdio: the activity tracker and the idle watchdog, which is off unless a timeout is configured.
- **Web** — only over HTTP: bearer, link and session authentication, the `/raw` file endpoints, `/healthz`, and the pages, rendered on the server from Razor components with the documentation's own stylesheet embedded.
- **Tools** — one expression each: build the params, send the command, map the reply. `Base` holds the tools that are always on and every skill has a folder of its own, so a tool's folder and its `[Skill]` say the same thing; `Prompts` holds the text the model reads.
- **Extensions** — the composition root: `ConfigureMcpServer()` lists the features of the stdio server, `ConfigureHttpServer()` those of the server beside Blender, and each feature wires itself. `Program.cs` reads the transport before a host exists, because the two need different hosts.

## Sequences instead of scripts

A tool call is one command and one round trip. A build that places two hundred panels is therefore two
hundred calls, while the same work as a `blender_python` script is one — which is why an agent that measures
its own cost writes the script, and why two thirds of the mutating commands in a real session were scripts
even though every one of the things they did had a tool.

`blender_run` closes that gap: a list of commands goes to the Blender that is already open, in one call, and
the add-on dispatches each step through the same `core.dispatch` a direct call goes through. The lease guard
refuses a step on another agent's object, the journal records each step under its own command name with the
objects it touched, and a failed step stops the sequence and comes back as `StepFailed` with every step that
ran. A script gets none of that, so the choice is no longer between correctness and cost.

The other half of the gap is discovery: the tool list a client holds shows only the loaded skills, so a model
that has not opened `modeling` cannot see that bevelling has a tool. `blender_find_tool` searches the whole
catalog — read from the same attributes the tools are registered from, so it cannot promise one that does not
exist — and loads the skill the match lives in. What remains for Python is answered in-band: the server reads
the script for the API it uses and names the tools that cover it, `SNAIL_MCP_BLENDER_PYTHON=fallback` refuses
a script whose work the tools already do — as a `blender_python` call or as a step of a sequence or a batch — and `blender_diagnose` reports the share of traffic that went
through Python rather than a tool.

## Skills

Tool descriptions are what the model reads before choosing, and a long list costs context and attention on
every turn. Of the 149 tools, 42 are always on; the other 107 live in nine skills. `blender_enable_skill`
adds a skill's tools to the tool collection of the client that asked, the SDK sends `notifications/tools/list_changed`
on its session, and the tool sends the same notice into its own reply for a client that holds no session (see below), so
the client refreshes its list at once. A tool list belongs to one client: a skill one loads is in its list and in no
other, and idle expiry takes it away from that client alone.

A skill stays loaded until `blender_disable_skill`: a client that caches the tool list would otherwise lose
tools it still expects, so unloading idle skills is off by default and
`SNAIL_MCP_BLENDER_SKILL_IDLE_MINUTES` turns it on. With it on, a skill that started something still
running — a queued or rendering background job — is pinned and survives expiry until that work ends.

## Links, and pictures in replies

The add-on accepts several connections. The server keeps three: the control link, on which commands run one after
another on Blender's main thread; the monitor link, answered from the add-on's socket thread out of a
dictionary that render handlers fill, where the health report pings too, so a busy Blender does not look dead; and the
data link for files and the data directory, described below.

Which link carries a command is the command's own property, not the caller's choice. The schema both sides read names
the channel of every command, the add-on answers exactly those commands off its main thread, a test compares the three
readings, and one place in the server picks the socket. A file listing asked for through the plain bridge still goes
down the data link, and so cannot end up waiting out a render — which is what the split exists for, and what a
convention alone could not promise. While a render, a bake, a product shot or a
sequencer render blocks the control link, the server probes the monitor link every two seconds and turns the answer
into MCP progress notifications when the client passed a progress token. Renders, captures, bakes and `blender_inspect_image` return a small JPEG as image
content next to the JSON, with exposure statistics (mean luminance, clipped highlights and shadows, a
histogram), so the model judges the picture rather than the file size.

## Blender on another machine

The link does not change when Blender moves: the add-on still listens on the loopback of its machine and an SSH
tunnel carries the connection, because the bridge runs Python and its token is plain text. What changes is who owns
which paths. The add-on owns its working directories — render jobs, batches and snapshots live in its data directory,
on its machine — and the server sends none of its own, which on another machine would name folders that do not
exist. `ping` reports the machine: platform, home, data directory and the `files` folder uploads land in.

Files move through three commands answered from the socket thread, like `ping`, so a transfer neither waits for a
render nor holds one up. Chunks are sized by the line limits — 16 MB a request and 4 MB a reply, both base64 — so a
chunk carries 8 MB up and 2 MB down, and a file lands under a partial name and takes its own only when the SHA-256 computed at the far end matches, so an
interrupted transfer leaves nothing that looks whole. A relative path resolves inside `files` and cannot climb out.

The folder the client starts the server in is the project, and its twin on Blender's machine is the add-on's
`files/<its name>`: an upload from the project lands in the twin at the same relative path, and a download from the
twin comes back to the same place, so the source of a project stays where the user works. The home folder and the
root are no project, so a client started there does not scatter renders across them.

The tunnel itself can belong to the server: with a tunnel host configured, a hosted service starts `ssh -N -L` from
the link port to the add-on's port over there, with BatchMode so it never waits for a prompt and ExitOnForwardFailure so
a port it cannot forward ends it. It reopens a dropped tunnel after a pause that doubles up to half a minute, kills ssh
when the server stops, and reports its state in `blender_diagnose`. ssh gets none of the server's standard streams:
they carry JSON-RPC. Since the client starts the server for a session and the server exits with it, the tunnel is up
exactly while someone works with Blender; see [Blender on another machine](remote.md) for the set-up.

A render job's worker is a second Blender that reads only its own saved preferences, which on a fresh machine say
the compute backend is `NONE`; the job spec therefore carries the backend and the devices of the Blender that queued
it. And a Blender without a display runs no timers, which the interface uses for the bridge's queue and the job
queue: `blender -b -c snail_bridge` is a loop that runs both itself. The live tests start Blender through that same
loop, so every live scenario exercises the headless path.

## The server beside Blender

With `SNAIL_MCP_BLENDER_TRANSPORT=Http` the same tools, prompts and resources are served over Streamable HTTP on
`/mcp` instead of stdio, from a server that runs on its own — in a container next to Blender's. `Program.cs` reads the
transport from the settings before any host exists, because stdio wants a plain host and HTTP a web host with
Kestrel, and each has its own composition root. The HTTP one leaves out the idle watchdog, which over stdio can end a
server its client forgot; a container is meant to outlive every client.

What each command means is written once, in `addon/commands.json`: which parameters it takes, where among them it names an object
or a collection, which of them name a file or a folder, whether it only reads, whether it destroys and whether it reaches beyond
Blender. The add-on reads it for the lease guard, the journal and the Python setting; the server's guard tests read it for the tool annotations, so a tool cannot call itself
read-only while the command it sends writes. It is part of the add-on's digest, because a server reading one set of meanings while
Blender acts on another is what that check exists to catch.

The parameter list is what lets the add-on refuse a parameter it does not read, instead of dropping it. `addon/parameters.py`
registers that refusal as a gate, and a gate is the one check both kinds of command pass, so it covers a tool, a step of
`blender_run`, a call inside `blender_program` and a job of `blender_batch` alike — which matters, because only the first of those
goes through a typed tool where a wrong name could not be written in the first place. The list is a second copy of something the
handlers already say, and a second copy drifts, so `tools/parameters_from_source.py` reads the keys back out of the Python and CI
fails when the two disagree; the same tool writes the lists with `--write`.

The same tool answers the lease guard's question the same way, by following the value rather than the name of the key: every object
reaches Blender through `_object` or `bpy.data.objects.get`, so the parameter it came from is what the guard needs, whether the name
sat in the parameter itself, inside a block, in each item of a list or as the key of a block. Two commands name objects without a
lookup to follow, and are read by the shape of what they do instead: `object_render_flags` hands a whole list to a helper that
resolves each item, and `export_file` resolves nothing at all — it selects objects by testing their names for membership.

What the tool finds is what the guard refuses on, and the guess it replaced there is kept for the journal, whose question is a
different one. The guard must be exact: `name` carries a material, a compositor node, a sequencer strip, an operator and the name of
a thing about to be created, and only the handler knows which of those it looks up. The journal may be generous: it records what a
command was about, including the name a new object was created under, and no lease could have been held on a name nothing had yet.
`team.py` keeps them apart as `_addressed` and `_targets`.

Inside the add-on a connection is two threads and a queue: one reads its lines, one writes its replies, and nothing else ever waits
on its socket. The main thread takes requests from one queue, runs them and leaves each reply in the client's outbox, so a client that
stopped reading holds up neither Blender nor another client; when its unsent replies pass the outbox limits the link is closed rather
than grown inside Blender. A refusal is the exception that still reaches its client: an unauthorized request ends the link after the
reply has left.

Blender in the next container runs headless as `blender -b -c snail_bridge --host 0.0.0.0 --token-file <secret> --backend
<backend>`: it listens on a private network that only the server shares, reads the bridge token from a file both
containers mount, and enables the compute backend before it listens, because a fresh container's preferences know no GPU.
The server finds it through `SNAIL_MCP_BLENDER_BRIDGE__HOST` and `SNAIL_MCP_BLENDER_BRIDGE__TOKEN_FILE`. It sees none of the
client's folders, so it has no project folder; a piece of work is a folder under the add-on's `files` instead, which
`blender_open_project` opens or creates with `<name>.blend`, `renders/` and `textures/`. [Blender in Docker](docker.md)
walks through the set-up.

A client that opens with `initialize` keeps a session, and the session carries `tools/list_changed` when a skill loads.
A client of the sessionless protocol revision is served per request and holds no stream of its own, so the tools that
load or unload a skill — `blender_enable_skill`, `blender_disable_skill`, `blender_find_tool` — also send the notice
through the server bound to the request, into the reply the client is already reading. Either way every session gets options of its own from the SDK, and with them a client of its own here: a tool
collection holding the always-on tools and nothing else, and a name.

A client is named by the key it presents, not by anything it writes for itself: that name is what the add-on's leases
and journal record, so two people working in one Blender are told apart there, and `blender_diagnose` reports it back
under `bridge.agent`. Only the token the server was configured with may still name itself with the header
`X-Snail-Agent`, because that is what that token could always do; a client presenting it and no header signs with the
configured `SNAIL_MCP_BLENDER_BRIDGE__AGENT`.

The name matters twice over for a client of the sessionless revision, which is handed a fresh session on every request:
it is how such a client is recognised again, and so how the skills it loaded are still there on the next call. At most
64 named clients are kept.

Three schemes let requests in. A bearer key opens `/mcp` and the files: one key per client, each with a name and the
kinds of work it opens, kept as digests under `state/clients.json` so a copy of that file opens nothing. The key names
its holder, which is what the add-on's leases and journal record, and a call of a kind the key does not open is turned
away before Blender hears of it — a tool call by its command, a file by the method, and a form of the pages by what it
does. A tool that carries commands is asked about every one of them, because a key that opens none of this work could
otherwise send it as a step. Revoking a key turns its holder away at the next request, browser session included,
because the list is read again whenever it changes on disk. The token the server was configured with stays a key of its
own with every scope, so a server set up before keys existed keeps working. A link signed with HMAC opens one path of the
volume for one method until it expires — that is what `blender_upload` and `blender_download` return, as a `curl`
command the agent runs in its own shell, because the server sees none of the client's folders. The upload command sends
parts of 16 MB, each continuing the partial file at its offset, and the last carries the SHA-256 the client computed:
Traefik stops reading a body after a minute, and a 160 MB file over a slow uplink broke off at exactly that. A folder
goes as a tar that gathers in a staging file until its last part and is unpacked from there. A listing is one page of a
folder — the add-on answers at most five thousand names at a time, says how many there are in all and whether more
follow — so a folder of a hundred thousand frames downloads page by page instead of being cut off; past twenty
thousand files a transfer refuses the folder whole rather than running for an hour. And a session cookie,
set by signing in with the same token on the pages, opens the pages — whose own forms delete files, clear ended jobs and
batches, and cancel a job — and lets a browser read files under `/raw`, never write or delete them there.
Forms carry antiforgery tokens; files come with `Content-Security-Policy: sandbox`.

The server reaches the add-on's data directory over a third link, the volume link, answered from the add-on's socket
thread: `file_list`, `file_get` and `file_put` with an area named, and `storage`, `storage_list` and `storage_delete`.
A named area confines a path to that area and refuses absolute ones. A folder is streamed as a tar written by hand —
.NET's own writer refuses a data stream that cannot seek into an archive that cannot seek, and both ends are exactly
that — or as a zip for a browser. The add-on refuses to delete the file open in Blender and a job or batch still
running; it learns the open file on the main thread, through load and save handlers and an after-hook, because the
socket thread never reads `bpy`, and it counts every successful command that is neither a read nor a render as an unsaved
change until a file is saved, opened or created, since a Blender without an interface never raises `bpy.data.is_dirty`
for edits made through the API.

## Background render jobs

A render inside a tool call holds Blender's main thread, so nothing else answers until it ends — and it cannot be
stopped: Blender has no way to interrupt a running operator from outside, so cancelling the call ends the waiting
while the render goes on. That is why `blender_render_animation` renders in the background by default, sending the
same range and the same output path to a job, and why the hint after a render times out says so instead of
suggesting a longer timeout. The server counts abandoned calls and `blender_diagnose` reports what Blender is busy
with, so the state is at least visible.

The `farm` skill therefore offers `blender_render_job`: the add-on saves the file as a copy next to a job
spec, starts a second Blender in background mode on that copy, and returns at once. The job script renders
the frames, cameras and view layers of the spec and writes a status file after every frame (frames done,
current frame, peak memory, files written) that `blender_render_job_status` reads, together with the tail
of the job's log. Jobs live in the add-on's data directory, on Blender's machine, in `jobs/<id>/`, and survive a restart of the server;
`blender_render_job_cancel` stops it cooperatively: it writes a marker the worker reads between cameras and between
the frames of a frame list, and sends SIGINT, which a background Blender treats as the break Ctrl+C is, so the worker ends the render it is inside and
writes its own last status; SIGTERM and then SIGKILL follow only for a worker that ignores it. The written frames
stay on disk either way. Jobs queue: at most
`maxParallel` run at once, higher priority starts first, a crashed job restarts `retries` times, and
`chunks` splits a frame range into interleaved sub-jobs that render in parallel. While a job is queued or
running the farm skill is pinned, so idle expiry cannot take `blender_render_job_status` away before the frames land.

The queue lives in the add-on, on Blender's machine, and stays there. It was once meant to move into the server, where
a durable store and a scheduler would be easier to write — but the server is not where the work happens. Over stdio it
shares a machine with Blender; over HTTP it is a container of its own beside another, and behind a tunnel it may be on
another continent. Nothing there can start a Blender on Blender's machine, and a queue whose owner cannot start its
workers is a second opinion about what should be running rather than a queue. What the move was wanted for — a job
that survives a restart, one worker per job, retries that fire, files that are never read half-written — belongs to
whoever starts the processes, and is written down here.

A job has six states and no seventh: `queued`, `running`, `finished`, `failed`, `cancelled`, `interrupted`. The set
is written once in the add-on and once in the server's vocabulary, and a test compares the two, because a state the
server does not know is a job the pages show as neither active nor ended. Three of them are active and three have
ended, and everything that asks "is this job still going" — the queue, the skill pin, the pages, the retention —
asks that one question rather than listing states of its own.

Every file a job writes is written whole: the spec, the status after each frame and the status of a chunk go to a
temporary name in the same folder and are renamed over the old one, so a reader between two frames sees the previous
status rather than half of the next. The worker is identified by its process and the run that started it, not by a
pid alone, because a pid is reused and a dead worker would otherwise look alive. One timer serves the whole queue
instead of one per job. When a job ends, its copy of the scene is deleted while the status and the log stay: the copy
is the heavy part of a job's folder and nothing reads it after the frames land — except a parent whose chunks are
still rendering, which keeps it until the last of them ends. The frames themselves are logged as they are written,
every path to `files.txt`, while the status keeps the last twenty, so a job of a thousand frames names all of them
somewhere without carrying all of them in every reply.

## Contracts kept by tests

The guards live in `tests/Contracts`; the tests of the tools and the live scenarios are split by skill, like the tools.

- The C# command catalog equals the registry in the add-on's Python modules, file by file: each partial of the catalog names an add-on module and lists exactly its commands.
- Every module that registers commands is imported by the package, and the add-on's socket threads touch `bpy` nowhere but in the handlers of immediate commands.
- Closed vocabularies (primitives, light types, file formats, the areas of the data directory) equal their tables in the add-on.
- The guard lists in `team.py` — read-only commands, list actions, unguarded commands, commands named after something else — name only commands that are registered.
- Every tool's annotations agree with the add-on: a tool is `readOnly` exactly when all its commands are in the add-on's read-only list, and `destructive` and `openWorld` exactly when the policy says so.
- The composition root puts the base tools into the server's tool collection, and the skill catalog loads skills into that same collection.
- Every tool takes its description from the catalog, every parameter is described, every limit stated in
  prose equals the number in code, and every tool named in a description or a prompt exists.
- The server process, started over stdio, answers the handshake with its instructions, lists exactly the base tools,
  loads a skill and announces the change, lists and gets its prompts, lists and reads its resources, flags a failed
  call with `isError`, never writes anything but JSON-RPC to stdout, and sends a picture as valid base64 image content.
- The server process, started over HTTP, answers `/healthz` to anyone and turns away a request without its token or
  with the wrong one, serves the same instructions and base tools as over stdio, tells a session client on its own
  stream and a sessionless client in the reply to the call that a skill's tools arrived, and refuses to start without
  a client token, naming the setting.
- The commands `blender_upload` and `blender_download` return over HTTP are run by a real shell against the server: a file
  larger than a part arrives whole, a folder arrives with its layout, files that exist are kept unless `overwrite`, and a
  file that arrives different from the one on the volume fails the command and never takes its name.
- The registry manifest `.mcp/server.json` names every setting the server binds, with the defaults the code starts from.
- Over HTTP a signed link opens only its own path and method until it expires; parts whose digest does not match are
  refused; a tar sent in parts is unpacked on its last part and leaves no staging file; a path outside its area is
  refused before it reaches Blender and only `files` takes writes; a page's deletion needs the form's antiforgery token,
  and a session reads files but cannot delete them.
- Every command of the catalog is sent at least once against a real Blender by the live scenarios, except the
  four that need Blender's interface (`viewport_capture`, `set_viewport_shading`, `sequencer_meta`,
  `sequencer_proxy`); the exemption list is checked too, so a command that becomes testable cannot keep its excuse,
  and every skill has a live scenario.
- A test of the discovery tools checks that every tool the script advice promises exists in the catalog, so a renamed
  tool cannot leave a promise behind.
- A live scenario runs a sequence in a real Blender and checks that a step on a leased object is refused
  exactly as a direct call would be, and that each step reaches the journal under its own command name.
- A live scenario uploads a file larger than a chunk, lists and downloads it back byte for byte, refuses a relative
  path that climbs out of `files`, and checks that a render job's spec carries the compute backend.
- A live scenario starts Blender through `--host`, `--token-file` and `--backend` and checks that only that token is
  served, and that the add-on refuses to delete the open file or a running job.
- The add-on's Python passes `ruff` on every push, which is also what keeps an undefined name — an import
  dropped from a module that still uses it — from reaching Blender.
- A version is written only in the release tag. The csproj and the registry manifest both hold the placeholder
  `0.0.0-dev`, which a test keeps equal, and the release writes the version of the tag into the assembly, the package,
  the manifest it carries and the image tags at once, so NuGet, GHCR and the registry cannot disagree; the assembly
  reports the same version in `blender_diagnose`.
- CI builds the documentation site with `mkdocs --strict`, builds both images when their sources change, and fails the
  live job when a live test did not run, so a runner without Blender cannot pass as green.
- A live scenario pings the real add-on and compares the digest it reports with the one the server computes
  over the same files, so the freshness check cannot start lying if one side changes how it hashes.
- The refusals the unit tests work against were recorded from a real Blender into `tests/Contracts/addon-answers.json`:
  the type, the message and the details of fifteen answers, most of them failures. A test hands one of those to a fake
  rather than inventing a `NotFound` of its own, a guard refuses any add-on error type written by hand in a test, and a
  live scenario puts every recorded question again and fails when the add-on answers differently — which is the moment
  the fakes stopped standing for Blender, and until then the moment nothing would have shown.
