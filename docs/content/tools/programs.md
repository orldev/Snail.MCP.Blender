# Programs

## <code>run_<wbr>program</code>

Runs a small JavaScript program that calls the commands itself: blender.add_primitive({kind: "cube", name: "Crate"}) and so on for every command of the catalog, with loops, conditions and the results in hand. Use it when a build takes many steps, when each step depends on what the last one answered, or when only a summary of a long listing matters: the steps run here, and only what the program returns comes back, instead of one call and one reply per step. Return a value from the program; log(...) adds a line to the reply. A failed command throws {type, message, details}, which the program may catch and carry on. This is not Blender's Python: the program reaches nothing but the commands, and blender_python stays the last resort for what no command does. The reply lists every command it sent. Limits: 200 commands, the timeout it was given, and one program's memory.

*may overwrite or delete · every call adds · may reach beyond Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `code` | `string` | yes | — | JavaScript. Call commands as blender.&lt;command&gt;({...}) — the names find_tool reports, the tool name without blender_ — return a value, log(...) to add a line. Up to 200 commands, no files, no network, no bpy. |
| `input` | `object?` | — | `null` | Values the program reads as input, e.g. {"count": 8}. |
| `timeoutSeconds` | `integer` | — | `120` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "code": {
      "description": "JavaScript. Call commands as blender.\u003Ccommand\u003E({...}) \u2014 the names find_tool reports, the tool name without blender_ \u2014 return a value, log(...) to add a line. Up to 200 commands, no files, no network, no bpy.",
      "type": "string"
    },
    "input": {
      "description": "Values the program reads as input, e.g. {\u0022count\u0022: 8}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 120
    }
  },
  "required": [
    "code"
  ]
}
```

</details>

