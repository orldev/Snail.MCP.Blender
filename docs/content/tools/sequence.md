# Sequences

## <code>run_<wbr>tools</code>

Runs a list of bridge commands in this Blender, in order, in one call: what to use for a build that takes many steps. A step is a command name and its params — the tool name without blender_, so add_primitive, transform_object, create_material — with the params named as the add-on reads them, the tool's parameter names in snake_case: adaptive_threshold, not adaptiveThreshold. A key the command does not read fails the step as BadRequest, naming the key it was probably meant to be, rather than vanishing from it. Each step passes the add-on's own checks, the same lease guard and the same journal as a direct tool call. Generate the list with a loop instead of writing a script: this, not blender_python, is what makes repetitive building cheap. Stops at the first failed step unless told to continue, and returns every step that ran with its result or its error; up to 500 steps, whose results are dropped from the reply once it grows past half a megabyte, each dropped one marked omitted.

*may overwrite or delete · every call adds · may reach beyond Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `commands` | `array` | yes | — | Steps in order: [{"command": "add_primitive", "params": {"kind": "cube", "name": "Crate"}}, ...]; command names are the bridge names, the tool name without blender_. Up to 500 steps. |
| `continueOnError` | `boolean` | — | `false` | Keep going after a failed step; either way the reply lists every step that ran with its result or its error. |
| `timeoutSeconds` | `integer` | — | `120` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "commands": {
      "description": "Steps in order: [{\u0022command\u0022: \u0022add_primitive\u0022, \u0022params\u0022: {\u0022kind\u0022: \u0022cube\u0022, \u0022name\u0022: \u0022Crate\u0022}}, ...]; command names are the bridge names, the tool name without blender_. Up to 500 steps.",
      "type": "array"
    },
    "continueOnError": {
      "description": "Keep going after a failed step; either way the reply lists every step that ran with its result or its error.",
      "type": "boolean",
      "default": false
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 120
    }
  },
  "required": [
    "commands"
  ]
}
```

</details>

