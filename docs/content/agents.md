# Several agents

Blender runs every command on one main thread, so agents that share a Blender share a queue: their
commands run one after another, and a render inside a tool call holds everyone until it finishes.
Parallel work therefore happens in separate processes, and the server gives three ways to keep it
from colliding.

## One Blender per agent

The cleanest split is the studio one: each agent works in its own Blender on its own port
(`SNAIL_MCP_BLENDER_BRIDGE__PORT`, one server per Blender) and in its own `.blend`. A coordinator owns the
shot file and brings the results in with `blender_append_assets` (the `io` skill) and `link` true, linked rather than
copied, so the
dependency runs one way and nothing is edited twice.

## Batches

`blender_batch` runs a list of bridge commands in a headless Blender on a copy of the file and saves a
`.blend`. The calling Blender stays free; several batches run at once. `blender_batch_status` reports
how many steps finished and the last 20 with their results or errors. Batches are how sub-agents that share one server still work in
parallel: they plan, submit a batch, and the coordinator links the output.

## Journal and leases

When agents must share one file, set `SNAIL_MCP_BLENDER_AGENT` on each server. Every mutating
command is then recorded with its agent in the journal (`blender_journal`), and an agent can claim
objects or collections for a while with `blender_lease`: other agents' mutating commands on them come
back as `Leased` with the holder's name until the lease is released or expires. Taking another agent's lease, by
`release` or by `clear`, needs `force`. Read-only commands
always pass.

Over stdio the name belongs to the server, which serves one client. A server reached over HTTP serves several, and the
name of each is the name on the key it presents: that name signs its commands, so the journal and the leases tell them
apart, and it holds a tool list of its own, so a skill one client loads is not loaded for the rest. The name is not the
caller's to choose — a header would make the agent on a lease a claim nobody had checked — so an operator mints a key
per client with `clients add` (see [Configuration](configuration.md#clients-and-their-keys)). Only the token the server
was configured with still lets its holder name itself with `X-Snail-Agent`, because that is what that token could
always do. `blender_diagnose` reports the name in hand under `bridge.agent`.

Both live in files under the add-on's data directory, on the machine Blender runs on (`state/journal.jsonl`,
`state/leases.json`), so they survive
a restart of Blender, and the worker Blenders that batches start read and write the same files: a batch step on a
leased object fails with `Leased` like a direct call would, and its steps appear in the journal with the worker's `pid`.
A render job's worker runs no commands and touches neither file. A journal entry names the objects a command addressed
under `targets`; batches, snapshots, scenes, view layers, File Output nodes, render jobs and projects are named after
something else, so their `name` is not listed there.

The lease guard refuses on the objects a command will really act on, and on nothing else. Where those are named is written
down for each command in `addon/commands.json` and taken out of the handler itself, so it covers a name in the parameter
you would expect, a name inside a block (`focus.object`, `dof.focus_object`), a name in a parameter of its own
(`pivot_name`, `bevel_object`, `taper_object`, `curve`, `cameras`), a name that is the key of a block
(`object_motion_blur`) and a name inside each item of a list (`variables[].object`); for a command that works on the
selection, the objects selected at the time. A lease on a collection is a lease on everything in it.

Guessing from the parameters that usually carry an object was wrong in both directions. It missed every name written
somewhere else, so depth of field could be pointed at an object another agent held, a curve taken as a bevel profile, a
camera pivoted around one — with no refusal and no journal entry, which is worse than a refusal, because the holder had
no way to find out. And it refused too much: `name` is also the parameter that carries a material, a compositor node, a
sequencer strip, an operator and the name of something about to be created, so a lease on an object called `Steel`
stopped a material called `Steel` being edited, and a lease on `Crate` stopped a second object being made under that
name — one Blender would have called `Crate.001` and left the held one alone.

The journal is not narrowed with the guard. An entry's `targets` still names everything a command carried, the material
and the strip included, and the name a new object was created under: the record answers what an agent did, rather than
refusing anybody, and no lease could have been held on a name nothing had yet. Three commands address nothing either
reading can see: `blender_python`, `blender_run_operator` and `blender_batch`. They pass the guard
and land in the journal flagged `unguarded`, with the code, the operator name or the batch name, so a coordinator sees them.
Agents that must respect leases use the dedicated tools; a shared set-up that cannot trust them switches
Python off with `SNAIL_MCP_BLENDER_PYTHON=off`, which also refuses script operators, and sequences and batches with a
python step or a script-operator step. Blender is told the same thing — `--python off`, which compose passes from
`PYTHON_ACCESS` — so the rule holds where the power is: a client that reaches the add-on directly, with the bridge
token, is refused there too, along with the operators that install or trust code and any file written outside the
add-on's own areas. `fallback` is the server's reading of a script against the tool catalog, so to the add-on it is
`allow`; `off` is the boundary. Which parameters of a command name a path is written down once, in the command schema the add-on
reads, so the rule covers an export, a render output and an image loaded from disk, not only the transfers.

## Sequences, not scripts

`blender_run` sends a list of commands to the Blender that is already open, in one call. Each step is
dispatched as if it had been called on its own — with the bridge command's name and its params as the add-on reads
them, the tool's parameter names in snake_case, so `set_cycles` takes `adaptive_threshold`, not `adaptiveThreshold`, and a
key the command does not read fails the step with the name it was probably meant to be — so the lease guard refuses a step on another agent's object,
the journal names each step with the objects it touched, and a failed step stops the sequence and returns
every step that ran. That is the difference from `blender_python`: a script of the same length passes no
guard and tells the journal nothing, which is why it appears there as `unguarded`. A set-up where agents must
respect each other's work runs builds as sequences, and `SNAIL_MCP_BLENDER_PYTHON=fallback` refuses a script whose work
the tools already do while leaving the API no tool reaches available — a `blender_python` call and a `python` step of
`blender_run` or `blender_batch` alike, so a sequence is no way around it.

## Programs, when the steps are not known in advance

`blender_run` carries a list decided before the call. `blender_program` carries a program that decides as it goes: a
small JavaScript that calls the commands itself.

```js
const parts = blender.list_objects({ pattern: "Crate*" }).objects;
for (const part of parts) {
  blender.transform_object({ name: part.name, location: [part.location[0], 0, 0] });
}
return { moved: parts.length };
```

Every call goes over the same link as a tool call, so the lease guard, the journal and the validation hold for each
step, and a failed command throws `{type, message, details}` the program may catch. Only what the program returns
comes back, with the log it wrote and the list of commands it sent: twenty steps cost one call and one reply instead
of twenty of each, and a listing a program filters never reaches the conversation whole. It is bounded by the timeout
it was given, by its memory, and by 200 commands; past that it stops and reports what it managed. It is not
`blender_python`: it reaches the commands and nothing else — no files, no network, no `bpy`.

When the tool for a step is not in the list, `blender_find_tool` searches the whole catalog by what the step
should do. Its answer carries each tool's command and every parameter it takes, which is enough to send it with
`blender_program`; `enable` loads the skill instead, so the tools themselves join the tool list. Keeping the list
small is the point: it is sent to every client on every session, so the tools that are always there are the ones
every session needs, and the rest arrive when they are asked for.

## Gates

`blender_snapshot` before a stage and after it; `blender_render_check` and `blender_render_budget`
before a render; `blender_inspect_image` on the result. With several agents in one Blender, render
only in the background — `blender_render_job`, or `blender_render_animation`, which does it by default when called as a
tool; a `render_animation` step of `blender_run` renders in the open Blender — so a render never freezes the others.
