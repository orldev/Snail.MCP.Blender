# Operators and Python

## <code>blender_<wbr>run_<wbr>operator</code>

Runs any bpy operator with its properties: everything Blender's menus do, thousands of operators. Properties are validated against the operator before it runs; blender_describe_operator lists them. Mesh editing operators need Edit Mode and usually area=VIEW_3D. Prefer the dedicated tools when one exists.

*may overwrite or delete · every call adds · may reach beyond Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Operator as bpy.ops names it, without the prefix: mesh.primitive_cube_add, object.shade_smooth, transform.resize. |
| `parameters` | `object?` | — | `null` | Operator properties as a JSON object, e.g. {"size": 2, "location": [0, 0, 1]}. Unknown names are rejected with the list of known ones. |
| `area` | `string?` | — | `null` | Editor type to run inside, e.g. VIEW_3D for mesh editing operators that need a 3D viewport; empty for operators that work anywhere. |
| `timeoutSeconds` | `integer` | — | `30` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Operator as bpy.ops names it, without the prefix: mesh.primitive_cube_add, object.shade_smooth, transform.resize.",
      "type": "string"
    },
    "parameters": {
      "description": "Operator properties as a JSON object, e.g. {\u0022size\u0022: 2, \u0022location\u0022: [0, 0, 1]}. Unknown names are rejected with the list of known ones.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "area": {
      "description": "Editor type to run inside, e.g. VIEW_3D for mesh editing operators that need a 3D viewport; empty for operators that work anywhere.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 30
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>describe_<wbr>operator</code>

Documents an operator: label, description, and every property with type, default, limits and enum values.

*read-only · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Operator as bpy.ops names it, without the prefix: mesh.primitive_cube_add, object.shade_smooth, transform.resize. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Operator as bpy.ops names it, without the prefix: mesh.primitive_cube_add, object.shade_smooth, transform.resize.",
      "type": "string"
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>python</code>

Runs Python inside Blender with bpy available and returns stdout plus the value assigned to result. The last resort, and there is a procedure before it: find_tool for what you are about to do, run_tools for a build of many steps, blender_run_operator for a single operator. What a script does passes no validation, ignores the leases other agents hold and lands in the journal as unguarded, so nobody can see which objects it touched; the reply names the tools that already cover what it did. Blender's Python API is fully reachable, which is what this is for: what no tool of this server does.

*may overwrite or delete · every call adds · may reach beyond Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `code` | `string` | yes | — | Python source. bpy, bmesh, mathutils and math are imported; assign to a variable named result to return a value. |
| `timeoutSeconds` | `integer` | — | `30` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "code": {
      "description": "Python source. bpy, bmesh, mathutils and math are imported; assign to a variable named result to return a value.",
      "type": "string"
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 30
    }
  },
  "required": [
    "code"
  ]
}
```

</details>

