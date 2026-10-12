# Shape keys

Part of the `animation` skill: these tools appear once it is loaded — by `blender_enable_skill` with `animation`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>add_<wbr>shape_<wbr>key</code>

Adds a shape key (blend shape), optionally shaping it by vertex offsets; blender_mesh_geometry gives the indices.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `keyName` | `string` | — | `Key` | Name of the new key; a Basis key is created first when there is none. |
| `vertices` | `array?` | — | `null` | [[index, x, y, z], ...] vertex offsets that define the shape; positions when relative=false. |
| `relative` | `boolean` | — | `true` | Treat x, y, z as offsets from the basis (the default) or as positions. |
| `fromMix` | `boolean` | — | `false` | Start from the current mix of keys instead of the basis. |
| `value` | `number?` | — | `null` | Initial value 0 to 1. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "keyName": {
      "description": "Name of the new key; a Basis key is created first when there is none.",
      "type": "string",
      "default": "Key"
    },
    "vertices": {
      "description": "[[index, x, y, z], ...] vertex offsets that define the shape; positions when relative=false.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": [
          "array",
          "null"
        ],
        "items": {
          "type": "number"
        }
      },
      "default": null
    },
    "relative": {
      "description": "Treat x, y, z as offsets from the basis (the default) or as positions.",
      "type": "boolean",
      "default": true
    },
    "fromMix": {
      "description": "Start from the current mix of keys instead of the basis.",
      "type": "boolean",
      "default": false
    },
    "value": {
      "description": "Initial value 0 to 1.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>set_<wbr>shape_<wbr>key</code>

Sets a shape key's value and slider range, optionally keying the value on a frame.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `keyName` | `string` | yes | — | Shape key name. |
| `value` | `number?` | — | `null` | Blend value; 0 is off, 1 is the full shape. |
| `frame` | `integer?` | — | `null` | Key the value on this frame. |
| `sliderMin` | `number?` | — | `null` | Lowest allowed value. |
| `sliderMax` | `number?` | — | `null` | Highest allowed value; above 1 overshoots. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "keyName": {
      "description": "Shape key name.",
      "type": "string"
    },
    "value": {
      "description": "Blend value; 0 is off, 1 is the full shape.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "frame": {
      "description": "Key the value on this frame.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "sliderMin": {
      "description": "Lowest allowed value.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "sliderMax": {
      "description": "Highest allowed value; above 1 overshoots.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name",
    "keyName"
  ]
}
```

</details>

