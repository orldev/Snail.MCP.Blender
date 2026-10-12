# Rendering

Part of the `rendering` skill: these tools appear once it is loaded — by `blender_enable_skill` with `rendering`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>render_<wbr>settings</code>

Sets the render engine, resolution, samples, denoising, background transparency, pixel filter width (sharpness) and camera; every value optional. Returns the effective settings. Output path, format, depth, codecs and stamps live in blender_set_output; engine details in blender_set_cycles and blender_set_eevee; passes in blender_view_layer.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `engine` | `string?` | — | `null` | EEVEE, CYCLES or WORKBENCH. |
| `resolutionX` | `integer?` | — | `null` | Pixels along this edge, 1 to 4096. |
| `resolutionY` | `integer?` | — | `null` | Pixels along this edge, 1 to 4096. |
| `percentage` | `integer?` | — | `null` | Resolution percentage 1 to 100; 50 renders a quick preview at half size. |
| `samples` | `integer?` | — | `null` | Samples per pixel; Cycles 64 to 1024, EEVEE 16 to 256. |
| `denoise` | `boolean?` | — | `null` | Cycles denoising. |
| `device` | `string?` | — | `null` | Cycles device: CPU or GPU. |
| `filmTransparent` | `boolean?` | — | `null` | Transparent background instead of the world. |
| `filterSize` | `number?` | — | `null` | Pixel filter width in pixels: 1.5 is Blender's default, 1.0 or lower renders sharper and crisper, 2 or more softer. |
| `camera` | `string?` | — | `null` | Camera object to render from. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "engine": {
      "description": "EEVEE, CYCLES or WORKBENCH.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "resolutionX": {
      "description": "Pixels along this edge, 1 to 4096.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "resolutionY": {
      "description": "Pixels along this edge, 1 to 4096.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "percentage": {
      "description": "Resolution percentage 1 to 100; 50 renders a quick preview at half size.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "samples": {
      "description": "Samples per pixel; Cycles 64 to 1024, EEVEE 16 to 256.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "denoise": {
      "description": "Cycles denoising.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "device": {
      "description": "Cycles device: CPU or GPU.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "filmTransparent": {
      "description": "Transparent background instead of the world.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "filterSize": {
      "description": "Pixel filter width in pixels: 1.5 is Blender\u0027s default, 1.0 or lower renders sharper and crisper, 2 or more softer.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "camera": {
      "description": "Camera object to render from.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "scene": {
      "description": "Scene to act on; the current scene otherwise.",
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

## <code>blender_<wbr>render_<wbr>image</code>

Renders the current or a given frame of a scene and view layer to an image file and returns its path, size, duration, peak memory, a preview to look at and exposure statistics. Blender is busy until it finishes and the render cannot be interrupted once it starts — cancelling the call stops the waiting, not the render. Lower the resolution percentage or samples for previews, and use blender_render_job for anything long, which renders in a second Blender and can be cancelled.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the image to write; the extension follows the output format. |
| `frame` | `integer?` | — | `null` | Frame to render; the current frame otherwise. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |
| `viewLayer` | `string?` | — | `null` | View layer to render alone; every enabled layer otherwise. |
| `preview` | `boolean` | — | `true` | Return a small JPEG of the result as image content, with exposure statistics; false for the path only. |
| `timeoutSeconds` | `integer` | — | `300` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the image to write; the extension follows the output format.",
      "type": "string"
    },
    "frame": {
      "description": "Frame to render; the current frame otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "scene": {
      "description": "Scene to act on; the current scene otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "viewLayer": {
      "description": "View layer to render alone; every enabled layer otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "preview": {
      "description": "Return a small JPEG of the result as image content, with exposure statistics; false for the path only.",
      "type": "boolean",
      "default": true
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 300
    }
  },
  "required": [
    "path"
  ]
}
```

</details>

## <code>blender_<wbr>render_<wbr>animation</code>

Renders the frame range as an image sequence or a video. In the background by default: a second Blender renders the same range to the same path, this one stays free, and the reply is a job id that blender_render_job_status follows and blender_render_job_cancel stops. Pass background=false to render here instead, which holds Blender until the last frame and cannot be interrupted — a cancelled tool call stops the waiting, not the render.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `outputPath` | `string` | yes | — | Output path: a folder with a prefix for image sequences (/renders/shot_ or /renders/shot_####), a file for FFMPEG. Blender adds the frame number and the extension; a finished file name such as shot_0001.png is refused, pass shot_####.png. |
| `start` | `integer?` | — | `null` | First frame; the scene start otherwise. |
| `end` | `integer?` | — | `null` | Last frame; the scene end otherwise. |
| `step` | `integer?` | — | `null` | Render every nth frame. |
| `fileFormat` | `string?` | — | `null` | PNG, JPEG, OPEN_EXR or FFMPEG for a video. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |
| `preview` | `boolean` | — | `true` | Return a small JPEG of the result as image content, with exposure statistics; false for the path only. |
| `background` | `boolean` | — | `true` | Render in a second Blender, the default: this one stays free, the reply is a job id and blender_render_job_cancel stops it. False renders here, which blocks Blender until every frame is done and cannot be stopped. |
| `timeoutSeconds` | `integer` | — | `600` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "outputPath": {
      "description": "Output path: a folder with a prefix for image sequences (/renders/shot_ or /renders/shot_####), a file for FFMPEG. Blender adds the frame number and the extension; a finished file name such as shot_0001.png is refused, pass shot_####.png.",
      "type": "string"
    },
    "start": {
      "description": "First frame; the scene start otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "end": {
      "description": "Last frame; the scene end otherwise.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "step": {
      "description": "Render every nth frame.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "fileFormat": {
      "description": "PNG, JPEG, OPEN_EXR or FFMPEG for a video.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "scene": {
      "description": "Scene to act on; the current scene otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "preview": {
      "description": "Return a small JPEG of the result as image content, with exposure statistics; false for the path only.",
      "type": "boolean",
      "default": true
    },
    "background": {
      "description": "Render in a second Blender, the default: this one stays free, the reply is a job id and blender_render_job_cancel stops it. False renders here, which blocks Blender until every frame is done and cannot be stopped.",
      "type": "boolean",
      "default": true
    },
    "timeoutSeconds": {
      "description": "Seconds to wait for Blender, 1 to 600.",
      "type": "integer",
      "default": 600
    }
  },
  "required": [
    "outputPath"
  ]
}
```

</details>

## <code>blender_<wbr>set_<wbr>world</code>

Sets the world: background colour and strength, an HDRI environment with rotation, and mist. The first thing to fix when renders look dark or flat.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `color` | `array?` | — | `null` | [r, g, b] or [r, g, b, a] with components 0 to 1. |
| `strength` | `number?` | — | `null` | Background light strength; 1 is the default. |
| `hdriPath` | `string?` | — | `null` | Absolute path of an HDRI (.hdr or .exr) to light the scene with; empty string removes it. |
| `rotation` | `number?` | — | `null` | Rotate the HDRI around the vertical axis, in degrees. |
| `mist` | `boolean?` | — | `null` | Enable or disable mist (fog by distance). |
| `mistStart` | `number?` | — | `null` | Distance where mist starts, in metres. |
| `mistDepth` | `number?` | — | `null` | Distance over which mist reaches full density, in metres. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "color": {
      "description": "[r, g, b] or [r, g, b, a] with components 0 to 1.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "strength": {
      "description": "Background light strength; 1 is the default.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "hdriPath": {
      "description": "Absolute path of an HDRI (.hdr or .exr) to light the scene with; empty string removes it.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "rotation": {
      "description": "Rotate the HDRI around the vertical axis, in degrees.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "mist": {
      "description": "Enable or disable mist (fog by distance).",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "mistStart": {
      "description": "Distance where mist starts, in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    },
    "mistDepth": {
      "description": "Distance over which mist reaches full density, in metres.",
      "type": [
        "number",
        "null"
      ],
      "default": null
    }
  }
}
```

</details>

