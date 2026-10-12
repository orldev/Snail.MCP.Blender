# Geometry Nodes

Part of the `modeling` skill: these tools appear once it is loaded — by `blender_enable_skill` with `modeling`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>build_<wbr>geometry_<wbr>nodes</code>

Builds a Geometry Nodes tree on an object from a specification of nodes and links; Group Input and Group Output exist already, so link the geometry through them. Any of Blender's geometry, function and math nodes by name.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `nodes` | `array?` | — | `null` | Nodes as [{"id": "sub", "type": "SubdivideMesh", "inputs": {"Level": 2}, "properties": {}, "location": [0, 0]}, ...]. Group Input and Group Output exist already. |
| `links` | `array?` | — | `null` | Links as [["Group Input", "Geometry", "sub", "Mesh"], ["sub", "Mesh", "Group Output", "Geometry"]]: from id, output socket, to id, input socket. |
| `interfaceInputs` | `array?` | — | `null` | Extra tree inputs exposed on the modifier: [{"name": "Count", "socket_type": "NodeSocketInt"}]. |
| `inputs` | `object?` | — | `null` | Values for the tree's inputs by name, e.g. {"Count": 5}. |
| `modifier` | `string?` | — | `null` | Name of the Geometry Nodes modifier to build on; the first one, or a new one, otherwise. |
| `clear` | `boolean` | — | `false` | Remove every node except Group Input and Group Output first. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "nodes": {
      "description": "Nodes as [{\u0022id\u0022: \u0022sub\u0022, \u0022type\u0022: \u0022SubdivideMesh\u0022, \u0022inputs\u0022: {\u0022Level\u0022: 2}, \u0022properties\u0022: {}, \u0022location\u0022: [0, 0]}, ...]. Group Input and Group Output exist already.",
      "type": [
        "array",
        "null"
      ],
      "default": null
    },
    "links": {
      "description": "Links as [[\u0022Group Input\u0022, \u0022Geometry\u0022, \u0022sub\u0022, \u0022Mesh\u0022], [\u0022sub\u0022, \u0022Mesh\u0022, \u0022Group Output\u0022, \u0022Geometry\u0022]]: from id, output socket, to id, input socket.",
      "type": [
        "array",
        "null"
      ],
      "default": null
    },
    "interfaceInputs": {
      "description": "Extra tree inputs exposed on the modifier: [{\u0022name\u0022: \u0022Count\u0022, \u0022socket_type\u0022: \u0022NodeSocketInt\u0022}].",
      "type": [
        "array",
        "null"
      ],
      "default": null
    },
    "inputs": {
      "description": "Values for the tree\u0027s inputs by name, e.g. {\u0022Count\u0022: 5}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "modifier": {
      "description": "Name of the Geometry Nodes modifier to build on; the first one, or a new one, otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "clear": {
      "description": "Remove every node except Group Input and Group Output first.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>set_<wbr>geometry_<wbr>inputs</code>

Sets the exposed inputs of a Geometry Nodes modifier by name.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `inputs` | `object` | yes | — | Values by input name; objects are named by their name. |
| `modifier` | `string?` | — | `null` | Name of the Geometry Nodes modifier; the first one otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "inputs": {
      "description": "Values by input name; objects are named by their name.",
      "type": "object"
    },
    "modifier": {
      "description": "Name of the Geometry Nodes modifier; the first one otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "name",
    "inputs"
  ]
}
```

</details>

