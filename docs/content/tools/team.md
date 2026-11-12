# Journal, leases and batches

## <code>blender_<wbr>journal</code>

What changed in this Blender and by whom: every mutating command with its agent, time, targets and outcome, newest last. Read it before touching objects another agent may be working on, and after a batch lands.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `since` | `integer` | — | `0` | Only entries after this index; the latest index comes back with every reply. |
| `agent` | `string?` | — | `null` | Only this agent's entries. |
| `target` | `string?` | — | `null` | Only entries that touched this object or collection. |
| `limit` | `integer` | — | `100` | Most entries to return, up to 500. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "since": {
      "description": "Only entries after this index; the latest index comes back with every reply.",
      "type": "integer",
      "default": 0
    },
    "agent": {
      "description": "Only this agent\u0027s entries.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "target": {
      "description": "Only entries that touched this object or collection.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "limit": {
      "description": "Most entries to return, up to 500.",
      "type": "integer",
      "default": 100
    }
  }
}
```

</details>

## <code>blender_<wbr>lease</code>

Claims objects or collections for this agent for a while: mutating commands from other agents on them are refused with leased until the lease is released or expires. list shows holders; clear drops every lease.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `action` | `string` | — | `list` | claim, release, list (the default) or clear. |
| `names` | `array?` | — | `null` | Objects or collections (every object inside) to claim or release. |
| `ttlSeconds` | `integer?` | — | `null` | claim: seconds the lease lasts; 900 by default. |
| `note` | `string?` | — | `null` | claim: what the agent is doing with them. |
| `force` | `boolean` | — | `false` | release: take leases held by other agents too. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "action": {
      "description": "claim, release, list (the default) or clear.",
      "type": "string",
      "default": "list"
    },
    "names": {
      "description": "Objects or collections (every object inside) to claim or release.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": [
          "string",
          "null"
        ]
      },
      "default": null
    },
    "ttlSeconds": {
      "description": "claim: seconds the lease lasts; 900 by default.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "note": {
      "description": "claim: what the agent is doing with them.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "force": {
      "description": "release: take leases held by other agents too.",
      "type": "boolean",
      "default": false
    }
  }
}
```

</details>

## <code>blender_<wbr>batch</code>

Runs a list of bridge commands in a separate headless Blender on a copy of the file (or a given .blend) and saves the result to a .blend: parallel work that does not touch this Blender. Link the output back with blender_append_assets. Answers when the batch ends within `wait` seconds, with its steps, output and log; a longer batch answers with the job running — read its resource (job://blender/batch/&lt;id&gt;) for where it stands, or wait on it.

*may overwrite or delete · every call adds · may reach beyond Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `commands` | `array` | yes | — | Steps in order: [{"command": "add_primitive", "params": {"kind": "cube", "name": "Crate"}}, ...]; command names are the bridge names, the tool name without blender_. |
| `input` | `string?` | — | `null` | Absolute .blend to start from; a copy of the current file otherwise. |
| `output` | `string?` | — | `null` | Absolute .blend to save the result to; output.blend in the batch folder otherwise. |
| `continueOnError` | `boolean` | — | `false` | Keep going after a failed step. |
| `name` | `string?` | — | `null` | Name for the batch. |
| `wait` | `integer` | — | `60` | Seconds to wait for the job to end before answering with it still running; 60 by default, 0 answers at once with the job to read or wait on. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "commands": {
      "description": "Steps in order: [{\u0022command\u0022: \u0022add_primitive\u0022, \u0022params\u0022: {\u0022kind\u0022: \u0022cube\u0022, \u0022name\u0022: \u0022Crate\u0022}}, ...]; command names are the bridge names, the tool name without blender_.",
      "type": "array"
    },
    "input": {
      "description": "Absolute .blend to start from; a copy of the current file otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "output": {
      "description": "Absolute .blend to save the result to; output.blend in the batch folder otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "continueOnError": {
      "description": "Keep going after a failed step.",
      "type": "boolean",
      "default": false
    },
    "name": {
      "description": "Name for the batch.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "wait": {
      "description": "Seconds to wait for the job to end before answering with it still running; 60 by default, 0 answers at once with the job to read or wait on.",
      "type": "integer",
      "default": 60
    }
  },
  "required": [
    "commands"
  ]
}
```

</details>

## <code>blender_<wbr>batches</code>

The recent batches the add-on holds, newest first: each one's state, steps finished of total, output file and log tail, and which are still running. One batch's progress is its resource, job://blender/batch/&lt;id&gt;.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `logLines` | `integer` | — | `0` | Lines from the end of each batch log. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "logLines": {
      "description": "Lines from the end of each batch log.",
      "type": "integer",
      "default": 0
    }
  }
}
```

</details>

