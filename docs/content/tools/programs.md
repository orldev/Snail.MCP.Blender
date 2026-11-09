# Programs

## <code>run_<wbr>program</code>

Runs a small JavaScript program that calls the commands itself: blender.add_primitive({kind: "cube", name: "Crate"}) and so on for every command of the catalog — the names find_tool reports, the tool name without blender_ — with loops, conditions and the results in hand. Use it when a build takes many steps, when each step depends on what the last one answered, or when only a summary of a long listing matters: the steps run here, and only what the program returns comes back, instead of one call and one reply per step. This is not Blender's Python: blender_python stays the last resort for what no command does.
What the program returns comes back as result, log(…) writes lines that come back with it, and input is the object
passed alongside. A failed or refused command throws { error, message, hint, type, details }, which the program may
catch and act on; a command the server does not have stops the program with the names nearest to it. The program
reaches nothing but the commands: no files, no network, no clock but its own. The reply lists every command it sent.
Limits: 200 commands, 1000000 statements, calls nested 256 deep, 32 MB of memory, and 120 seconds unless timeoutSeconds gives up to 600.

*may overwrite or delete · every call adds · may reach beyond Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `code` | `string` | yes | — | The body of the program: JavaScript in strict mode, with return for whatever should come back as result. |
| `input` | `object?` | — | `null` | Values the program reads as input, so that the code itself can stay the same between calls. |
| `timeoutSeconds` | `integer?` | — | `null` | Seconds the program may run; the description of run_program states the default and the most it may be given. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "code": {
      "description": "The body of the program: JavaScript in strict mode, with return for whatever should come back as result.",
      "type": "string"
    },
    "input": {
      "description": "Values the program reads as input, so that the code itself can stay the same between calls.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "timeoutSeconds": {
      "description": "Seconds the program may run; the description of run_program states the default and the most it may be given.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "code"
  ]
}
```

</details>

