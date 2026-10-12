# Files, projects, snapshots and undo

## <code>blender_<wbr>undo_<wbr>redo</code>

Steps back or forward through Blender's undo history.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `action` | `string` | — | `undo` | undo or redo. |
| `steps` | `integer` | — | `1` | How many steps. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "action": {
      "description": "undo or redo.",
      "type": "string",
      "default": "undo"
    },
    "steps": {
      "description": "How many steps.",
      "type": "integer",
      "default": 1
    }
  }
}
```

</details>

## <code>blender_<wbr>save_<wbr>file</code>

Saves the .blend file to its path or to a new one; copy=true writes a copy without switching to it.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string?` | — | `null` | Absolute path of the .blend file; the current file's path otherwise. |
| `copy` | `boolean` | — | `false` | Write a copy and keep working in the current file. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the .blend file; the current file\u0027s path otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "copy": {
      "description": "Write a copy and keep working in the current file.",
      "type": "boolean",
      "default": false
    }
  }
}
```

</details>

## <code>blender_<wbr>open_<wbr>file</code>

Opens a .blend file, replacing the current scene without asking; save first if the current work matters.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the .blend file. |
| `loadUi` | `boolean` | — | `false` | Also load the window layout saved in the file. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the .blend file.",
      "type": "string"
    },
    "loadUi": {
      "description": "Also load the window layout saved in the file.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "path"
  ]
}
```

</details>

## <code>blender_<wbr>new_<wbr>file</code>

Starts a new file from the default template, or empty, replacing the current scene without asking.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `empty` | `boolean` | — | `false` | Start empty instead of with the default cube, light and camera. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "empty": {
      "description": "Start empty instead of with the default cube, light and camera.",
      "type": "boolean",
      "default": false
    }
  }
}
```

</details>

## <code>blender_<wbr>open_<wbr>project</code>

Opens the project named after the folder the client works in: files/&lt;name&gt;/&lt;name&gt;.blend on Blender's machine, with renders and textures folders beside it, all created when the project is new. from=current makes the open scene the project's, and is refused when the project already has its .blend. Render into the renders path the reply names and upload textures into textures: the project stays one folder, which blender_download brings back whole and, over HTTP, the server's pages list and clean. Unless from=current, refuses while the open file has unsaved changes, unless discard.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `name` | `string` | yes | — | The project: one folder name, the name of the folder the client works in. |
| `from` | `string` | — | `empty` | empty (the default) starts a new project from an empty scene; current makes the scene open now the project's. |
| `discard` | `boolean` | — | `false` | Drop the unsaved changes of the open file instead of refusing. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "name": {
      "description": "The project: one folder name, the name of the folder the client works in.",
      "type": "string"
    },
    "from": {
      "description": "empty (the default) starts a new project from an empty scene; current makes the scene open now the project\u0027s.",
      "type": "string",
      "default": "empty"
    },
    "discard": {
      "description": "Drop the unsaved changes of the open file instead of refusing.",
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

## <code>blender_<wbr>snapshot</code>

Saves a copy of the whole file to fall back to, lists the copies, restores one or removes it. A restore replaces the current file and saves it back to its own path; a file that was never saved stays at the snapshot path until blender_save_file moves it. Take one before a series of edits; cheaper than undo by steps.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `action` | `string` | — | `save` | save (the default), restore, list, remove. |
| `name` | `string?` | — | `null` | Snapshot name; a timestamp otherwise. |
| `note` | `string?` | — | `null` | save: a note to remember why. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "action": {
      "description": "save (the default), restore, list, remove.",
      "type": "string",
      "default": "save"
    },
    "name": {
      "description": "Snapshot name; a timestamp otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "note": {
      "description": "save: a note to remember why.",
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

