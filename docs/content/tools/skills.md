# Skills

## <code>blender_<wbr>skills</code>

Lists the skills: named groups of tools that are loaded on demand, with what each covers, whether it is loaded and the tools it brings.

*read-only · every call adds · stays inside Blender.*

Takes no parameters.

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {}
}
```

</details>

## <code>blender_<wbr>enable_<wbr>skill</code>

Loads a skill: its tools appear in the tool list right away (the client is notified). A skill stays loaded until blender_disable_skill, unless this server is configured to unload idle ones.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Skill name as blender_skills lists it. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Skill name as blender_skills lists it.",
      "type": "string"
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>disable_<wbr>skill</code>

Unloads a skill, removing its tools from the tool list.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Skill name as blender_skills lists it. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Skill name as blender_skills lists it.",
      "type": "string"
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

