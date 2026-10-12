# Files between machines

## <code>blender_<wbr>upload</code>

Copies a file, or every file under a folder, from this machine to the machine Blender runs on: textures, HDRIs, models and .blend files a remote Blender cannot otherwise see. A relative localPath is read from the project — the folder the client works in — and without a remotePath the file lands in the project's twin on Blender's machine at the same relative path, so the two folders mirror each other; blender_diagnose names both under project. Sent in chunks and checked by SHA-256 before the file takes its name; an existing file is kept unless overwrite. With Blender on this same machine none of this is needed. Over HTTP the call copies nothing itself: remotePath is required, relative to the files area and starting with the project's folder (robot/textures), and the reply holds a command to run in a shell on the client's machine, from the folder you work in, before expires; it sends the file in parts of 16 MB, a folder as one tar, checked by SHA-256 at the end.

*may overwrite or delete · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `localPath` | `string` | yes | — | File or folder on this machine: absolute, or relative to the project, the folder the client works in. |
| `remotePath` | `string?` | — | `null` | Where it lands on Blender's machine: absolute, or relative to the add-on's files folder; the same relative path in the project's twin otherwise. |
| `overwrite` | `boolean` | — | `false` | Replace files that already exist there. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "localPath": {
      "description": "File or folder on this machine: absolute, or relative to the project, the folder the client works in.",
      "type": "string"
    },
    "remotePath": {
      "description": "Where it lands on Blender\u0027s machine: absolute, or relative to the add-on\u0027s files folder; the same relative path in the project\u0027s twin otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "overwrite": {
      "description": "Replace files that already exist there.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "localPath"
  ]
}
```

</details>

## <code>blender_<wbr>download</code>

Copies a file, or every file under a folder, from the machine Blender runs on to this one: finished frames, render job output, saved .blend files. Without a localPath it lands in the project — the folder the client works in — at the relative path it had in the project's twin over there, or under its own name when it came from elsewhere; with no project, in the downloads folder of this server's data directory. Checked by SHA-256 before the file takes its name, and an existing local file is kept unless overwrite. Over HTTP the call copies nothing itself: remotePath must be relative to the files area and start with the project's folder, and the reply holds a command to run in a shell on the client's machine, from the folder you work in, before expires; it writes to localPath or to the same relative place without the project's folder and keeps files that exist unless overwrite, while the server checks every file against the SHA-256 the add-on computed and breaks the command off when one differs.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `remotePath` | `string` | yes | — | File or folder on Blender's machine: absolute, or relative to the add-on's files folder. |
| `localPath` | `string?` | — | `null` | Where it lands on this machine: absolute, or relative to the project; the matching place in the project otherwise. |
| `overwrite` | `boolean` | — | `false` | Replace local files that already exist. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "remotePath": {
      "description": "File or folder on Blender\u0027s machine: absolute, or relative to the add-on\u0027s files folder.",
      "type": "string"
    },
    "localPath": {
      "description": "Where it lands on this machine: absolute, or relative to the project; the matching place in the project otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "overwrite": {
      "description": "Replace local files that already exist.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "remotePath"
  ]
}
```

</details>

