# Skills

## <code>blender_<wbr>skills</code>

Lists this server's skills — groups of tools loaded on demand — with whether each is loaded and its tools.

*read-only · repeat-safe · stays inside Blender.*

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

Loads a skill: its tools join this session's tool list.

*only adds · repeat-safe · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | The skill's name, as the skills tool lists it. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "The skill\u0027s name, as the skills tool lists it.",
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

Unloads a skill: its tools leave this session's tool list until it is loaded again.

*only adds · repeat-safe · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | The skill's name, as the skills tool lists it. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "The skill\u0027s name, as the skills tool lists it.",
      "type": "string"
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

