# Collections

## <code>blender_<wbr>create_<wbr>collection</code>

Creates a collection under the scene root or under another collection.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Name of the new collection. |
| `parent` | `string?` | — | `null` | Parent collection; the scene's root collection otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Name of the new collection.",
      "type": "string"
    },
    "parent": {
      "description": "Parent collection; the scene\u0027s root collection otherwise.",
      "type": [
        "string",
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

## <code>blender_<wbr>move_<wbr>to_<wbr>collection</code>

Moves objects into a collection, unlinking them from their other collections unless keepOthers=true.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `names` | `array` | yes | — | Names of the objects to move. |
| `collection` | `string` | yes | — | Target collection. |
| `keepOthers` | `boolean` | — | `false` | Keep the objects in their other collections too (link instead of move). |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "names": {
      "description": "Names of the objects to move.",
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "collection": {
      "description": "Target collection.",
      "type": "string"
    },
    "keepOthers": {
      "description": "Keep the objects in their other collections too (link instead of move).",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "names",
    "collection"
  ]
}
```

</details>

## <code>blender_<wbr>update_<wbr>collection</code>

Hides or shows a collection in viewports and renders, excludes it from the view layer, or renames it.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | Collection name. |
| `hideViewport` | `boolean?` | — | `null` | Hide or show it in every viewport. |
| `hideRender` | `boolean?` | — | `null` | Exclude from or include in renders. |
| `exclude` | `boolean?` | — | `null` | Exclude it from the view layer entirely (the checkbox in the outliner) or bring it back. |
| `newName` | `string?` | — | `null` | New name. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "Collection name.",
      "type": "string"
    },
    "hideViewport": {
      "description": "Hide or show it in every viewport.",
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
    "exclude": {
      "description": "Exclude it from the view layer entirely (the checkbox in the outliner) or bring it back.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "newName": {
      "description": "New name.",
      "type": [
        "string",
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

