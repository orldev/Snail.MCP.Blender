# Finding a tool

## <code>find_<wbr>tool</code>

Finds tools of this server for a task in words, best first, with the parameters of each. Ask it before deciding that no tool exists. It searches unloaded skills too: enable: true loads them, and run_program or this server's own run_tools (see its description) runs them without loading.

*only adds · repeat-safe · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `task` | `string?` | — | `null` | What you are trying to do, in words: 'bevel the edges of a mesh', 'warm key light on the subject', 'render four frames in the background'. |
| `limit` | `integer` | — | `5` | How many tools to name, 1 to 20. |
| `enable` | `boolean` | — | `false` | Load the skills of the tools found, so they join the tool list. |
| `intent` | `string?` | — | `null` | Same as task. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "task": {
      "description": "What you are trying to do, in words: \u0027bevel the edges of a mesh\u0027, \u0027warm key light on the subject\u0027, \u0027render four frames in the background\u0027.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "limit": {
      "description": "How many tools to name, 1 to 20.",
      "type": "integer",
      "default": 5
    },
    "enable": {
      "description": "Load the skills of the tools found, so they join the tool list.",
      "type": "boolean",
      "default": false
    },
    "intent": {
      "description": "Same as task.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  }
}
```

</details>

