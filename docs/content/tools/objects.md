# Objects

## <code>blender_<wbr>add_<wbr>primitive</code>

Adds a mesh primitive at a location with rotation, scale and size, names it and makes it active. Returns the new object's summary.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `kind` | `string` | yes | — | Which primitive: cube, plane, grid, monkey, circle, uv_sphere, ico_sphere, cylinder, cone or torus. |
| `name` | `string?` | — | `null` | Name for the new object; Blender's default otherwise. |
| `location` | `array?` | — | `null` | [x, y, z] in metres. |
| `rotation` | `array?` | — | `null` | [x, y, z] Euler rotation in degrees. |
| `scale` | `array?` | — | `null` | [x, y, z] scale factors; 1 keeps the size. |
| `size` | `number?` | — | `null` | Overall size in metres: edge length for cube, plane, grid and monkey; radius for the round ones; major radius for torus. |
| `depth` | `number?` | — | `null` | Height of a cylinder or cone. |
| `minorRadius` | `number?` | — | `null` | Minor radius of a torus. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "kind": {
      "description": "Which primitive: cube, plane, grid, monkey, circle, uv_sphere, ico_sphere, cylinder, cone or torus.",
      "type": "string"
    },
    "name": {
      "description": "Name for the new object; Blender\u0027s default otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "location": {
      "description": "[x, y, z] in metres.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "rotation": {
      "description": "[x, y, z] Euler rotation in degrees.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "scale": {
      "description": "[x, y, z] scale factors; 1 keeps the size.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "size": {
      "description": "Overall size in metres: edge length for cube, plane, grid and monkey; radius for the round ones; major radius for torus.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "depth": {
      "description": "Height of a cylinder or cone.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "minorRadius": {
      "description": "Minor radius of a torus.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "kind"
  ]
}
```

</details>

## <code>blender_<wbr>delete_<wbr>objects</code>

Deletes objects by name and/or the current selection. Returns what was deleted.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `names` | `array?` | — | `null` | Names of the objects to delete. |
| `selected` | `boolean` | — | `false` | Also delete everything currently selected. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "names": {
      "description": "Names of the objects to delete.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": [
          "string",
          "null"
        ]
      },
      "default": null
    },
    "selected": {
      "description": "Also delete everything currently selected.",
      "type": "boolean",
      "default": false
    }
  }
}
```

</details>

## <code>blender_<wbr>duplicate_<wbr>object</code>

Copies an object, its mesh included unless linked=true, into the same collections at an optional offset.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `newName` | `string?` | — | `null` | Name for the copy; Blender appends .001 otherwise. |
| `linked` | `boolean` | — | `false` | Share the mesh data with the original instead of copying it. |
| `offset` | `array?` | — | `null` | [x, y, z] offset of the copy from the original, in metres. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "newName": {
      "description": "Name for the copy; Blender appends .001 otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "linked": {
      "description": "Share the mesh data with the original instead of copying it.",
      "type": "boolean",
      "default": false
    },
    "offset": {
      "description": "[x, y, z] offset of the copy from the original, in metres.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    }
  },
  "required": [
    "name"
  ]
}
```

</details>

## <code>blender_<wbr>transform_<wbr>object</code>

Sets or, with relative=true, adjusts an object's location, rotation (degrees) and scale.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `location` | `array?` | — | `null` | [x, y, z] in metres. |
| `rotation` | `array?` | — | `null` | [x, y, z] Euler rotation in degrees. |
| `scale` | `array?` | — | `null` | [x, y, z] scale factors; 1 keeps the size. |
| `relative` | `boolean` | — | `false` | Apply the values on top of the current ones (location and rotation add, scale multiplies) instead of replacing them. |

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
      "description": "[x, y, z] in metres.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "rotation": {
      "description": "[x, y, z] Euler rotation in degrees.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "scale": {
      "description": "[x, y, z] scale factors; 1 keeps the size.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "relative": {
      "description": "Apply the values on top of the current ones (location and rotation add, scale multiplies) instead of replacing them.",
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

## <code>blender_<wbr>update_<wbr>object</code>

Renames an object, hides or shows it in the viewport or in renders, and sets or clears its parent.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the object as shown in the outliner. |
| `newName` | `string?` | — | `null` | New name. |
| `hideViewport` | `boolean?` | — | `null` | Hide or show in the viewport. |
| `hideRender` | `boolean?` | — | `null` | Exclude from or include in renders. |
| `parent` | `string?` | — | `null` | Name of the new parent object. |
| `clearParent` | `boolean` | — | `false` | Detach from the current parent. |
| `keepTransform` | `boolean` | — | `true` | Keep the object where it is in the world when the parent changes. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the object as shown in the outliner.",
      "type": "string"
    },
    "newName": {
      "description": "New name.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "hideViewport": {
      "description": "Hide or show in the viewport.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "hideRender": {
      "description": "Exclude from or include in renders.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "parent": {
      "description": "Name of the new parent object.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "clearParent": {
      "description": "Detach from the current parent.",
      "type": "boolean",
      "default": false
    },
    "keepTransform": {
      "description": "Keep the object where it is in the world when the parent changes.",
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

## <code>blender_<wbr>add_<wbr>text</code>

Adds a 3D text object with a body, size, extrusion, alignment and an optional font file; standing upright by default.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `body` | `string` | yes | — | The text to show. |
| `name` | `string?` | — | `null` | Name for the text object. |
| `size` | `number?` | — | `null` | Letter height in metres. |
| `extrude` | `number?` | — | `null` | Depth of the extrusion in metres; 0 keeps it flat. |
| `bevelDepth` | `number?` | — | `null` | Rounded edge depth in metres. |
| `align` | `string?` | — | `null` | LEFT, CENTER, RIGHT, JUSTIFY or FLUSH. |
| `fontPath` | `string?` | — | `null` | Absolute path of a .ttf or .otf font file. |
| `location` | `array?` | — | `null` | [x, y, z] in metres. |
| `rotation` | `array?` | — | `null` | [x, y, z] Euler rotation in degrees. |
| `upright` | `boolean` | — | `true` | Stand the text up facing -Y (rotated 90° about X) instead of lying flat on the ground. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "body": {
      "description": "The text to show.",
      "type": "string"
    },
    "name": {
      "description": "Name for the text object.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "size": {
      "description": "Letter height in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "extrude": {
      "description": "Depth of the extrusion in metres; 0 keeps it flat.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "bevelDepth": {
      "description": "Rounded edge depth in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "align": {
      "description": "LEFT, CENTER, RIGHT, JUSTIFY or FLUSH.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "fontPath": {
      "description": "Absolute path of a .ttf or .otf font file.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "location": {
      "description": "[x, y, z] in metres.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "rotation": {
      "description": "[x, y, z] Euler rotation in degrees.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "upright": {
      "description": "Stand the text up facing -Y (rotated 90\u00B0 about X) instead of lying flat on the ground.",
      "type": "boolean",
      "default": true
    }
  },
  "required": [
    "body"
  ]
}
```

</details>

## <code>blender_<wbr>add_<wbr>empty</code>

Adds an empty: a point with a display shape, used as a parent, a target for constraints or a reference image.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `type` | `string` | — | `PLAIN_AXES` | Display shape: PLAIN_AXES, ARROWS, SINGLE_ARROW, CIRCLE, CUBE, SPHERE, CONE or IMAGE. |
| `name` | `string?` | — | `null` | Name for the empty. |
| `size` | `number?` | — | `null` | Display size in metres. |
| `location` | `array?` | — | `null` | [x, y, z] in metres. |
| `rotation` | `array?` | — | `null` | [x, y, z] Euler rotation in degrees. |
| `imagePath` | `string?` | — | `null` | IMAGE: absolute path of the picture to show. |
| `parent` | `string?` | — | `null` | Parent object. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "type": {
      "description": "Display shape: PLAIN_AXES, ARROWS, SINGLE_ARROW, CIRCLE, CUBE, SPHERE, CONE or IMAGE.",
      "type": "string",
      "default": "PLAIN_AXES"
    },
    "name": {
      "description": "Name for the empty.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "size": {
      "description": "Display size in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "location": {
      "description": "[x, y, z] in metres.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "rotation": {
      "description": "[x, y, z] Euler rotation in degrees.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "imagePath": {
      "description": "IMAGE: absolute path of the picture to show.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "parent": {
      "description": "Parent object.",
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

