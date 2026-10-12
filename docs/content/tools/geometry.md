# Geometry

Part of the `modeling` skill: these tools appear once it is loaded — by `blender_enable_skill` with `modeling`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>join_<wbr>objects</code>

Joins several objects into one; the target keeps its name, origin and modifiers.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `names` | `array` | yes | — | Names of the objects to join; at least two. |
| `target` | `string?` | — | `null` | The object that survives and receives the others; the first name otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "names": {
      "description": "Names of the objects to join; at least two.",
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "target": {
      "description": "The object that survives and receives the others; the first name otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "names"
  ]
}
```

</details>

## <code>blender_<wbr>set_<wbr>origin</code>

Moves an object's origin to its geometry, the 3D cursor or its centre of mass, or moves the geometry to the origin.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `type` | `string` | — | `ORIGIN_GEOMETRY` | ORIGIN_GEOMETRY (origin to geometry, the default), GEOMETRY_ORIGIN (geometry to origin), ORIGIN_CURSOR, ORIGIN_CENTER_OF_MASS, ORIGIN_CENTER_OF_VOLUME. |
| `center` | `string` | — | `MEDIAN` | MEDIAN or BOUNDS: how the geometry centre is found. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "type": {
      "description": "ORIGIN_GEOMETRY (origin to geometry, the default), GEOMETRY_ORIGIN (geometry to origin), ORIGIN_CURSOR, ORIGIN_CENTER_OF_MASS, ORIGIN_CENTER_OF_VOLUME.",
      "type": "string",
      "default": "ORIGIN_GEOMETRY"
    },
    "center": {
      "description": "MEDIAN or BOUNDS: how the geometry centre is found.",
      "type": "string",
      "default": "MEDIAN"
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>apply_<wbr>transforms</code>

Bakes location, rotation and scale into the mesh data, resetting them to identity; needed before booleans, arrays and exports that expect unit scale.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `location` | `boolean` | — | `true` | Bake the location. |
| `rotation` | `boolean` | — | `true` | Bake the rotation. |
| `scale` | `boolean` | — | `true` | Bake the scale. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "location": {
      "description": "Bake the location.",
      "type": "boolean",
      "default": true
    },
    "rotation": {
      "description": "Bake the rotation.",
      "type": "boolean",
      "default": true
    },
    "scale": {
      "description": "Bake the scale.",
      "type": "boolean",
      "default": true
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

