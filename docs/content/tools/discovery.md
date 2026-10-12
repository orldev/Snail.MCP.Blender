# Finding a tool

## <code>find_<wbr>tool</code>

Finds the tools for a task. Ask in plain English words for what you are about to do ("bevel the edges", "warm key light", "render four frames in the background"); the answer names the tools with their descriptions, the skill each belongs to, the command each sends and every parameter it takes. That is enough to call them at once through run_program, which needs no skill loaded; pass enable to put the tools themselves in the tool list instead. Most of this server's tools are not in the tool list until their skill is enabled, so a tool you cannot see is not a tool that does not exist: ask here before reaching for blender_python.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `intent` | `string` | yes | — | What you are trying to do, in English words: "bevel the edges of a mesh", "warm key light on the subject", "render four frames in the background". |
| `limit` | `integer` | — | `8` | Most tools to return, 1 to 25; 8 by default. |
| `enable` | `boolean` | — | `false` | Load the skills the matches live in, so their tools join the tool list; false by default, because the answer already carries what it takes to call them through run_program. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "intent": {
      "description": "What you are trying to do, in English words: \u0022bevel the edges of a mesh\u0022, \u0022warm key light on the subject\u0022, \u0022render four frames in the background\u0022.",
      "type": "string"
    },
    "limit": {
      "description": "Most tools to return, 1 to 25; 8 by default.",
      "type": "integer",
      "default": 8
    },
    "enable": {
      "description": "Load the skills the matches live in, so their tools join the tool list; false by default, because the answer already carries what it takes to call them through run_program.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "intent"
  ]
}
```

</details>

