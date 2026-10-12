# Compositor

Part of the `post` skill: these tools appear once it is loaded — by `blender_enable_skill` with `post`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>compositor</code>

Reads or edits the compositor by hand: list nodes, reset to Render Layers → output, add a node with inputs and settings, link sockets, remove a node. Render passes appear as outputs of the Render Layers node. Grain, glare and the rest of the post stack come from blender_lens_effects. Applies to every render from then on.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `action` | `string` | — | `info` | info (list nodes and links), clear (reset to Render Layers → output), add_node, link, remove_node. |
| `type` | `string?` | — | `null` | add_node: compositor node bl_idname or suffix: Blur, Glare, Filter, ColorBalance, HueSat, BrightContrast, Denoise, Lensdist, Defocus, Kuwahara, AlphaOver, Viewer, OutputFile, RLayers; Mix, Math and Noise from the shader set; Composite is the output. |
| `name` | `string?` | — | `null` | add_node: name for the node; remove_node: node to delete. |
| `inputs` | `object?` | — | `null` | add_node: input sockets by name, e.g. {"Size": 10}; menu sockets take their label, e.g. Filter {"Type": "Box Sharpen"}. |
| `properties` | `object?` | — | `null` | add_node: settings by Python name, e.g. {"filter_type": "GAUSS"}, {"glare_type": "FOG_GLOW"}. |
| `location` | `array?` | — | `null` | add_node: [x, y] position in the editor. |
| `fromNode` | `string?` | — | `null` | link: source node name. |
| `fromSocket` | `string?` | — | `null` | link: output socket on the source, e.g. Image, Depth, Normal. |
| `toNode` | `string?` | — | `null` | link: target node name. |
| `toSocket` | `string?` | — | `null` | link: input socket on the target. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "action": {
      "description": "info (list nodes and links), clear (reset to Render Layers \u2192 output), add_node, link, remove_node.",
      "type": "string",
      "default": "info"
    },
    "type": {
      "description": "add_node: compositor node bl_idname or suffix: Blur, Glare, Filter, ColorBalance, HueSat, BrightContrast, Denoise, Lensdist, Defocus, Kuwahara, AlphaOver, Viewer, OutputFile, RLayers; Mix, Math and Noise from the shader set; Composite is the output.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "name": {
      "description": "add_node: name for the node; remove_node: node to delete.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "inputs": {
      "description": "add_node: input sockets by name, e.g. {\u0022Size\u0022: 10}; menu sockets take their label, e.g. Filter {\u0022Type\u0022: \u0022Box Sharpen\u0022}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "properties": {
      "description": "add_node: settings by Python name, e.g. {\u0022filter_type\u0022: \u0022GAUSS\u0022}, {\u0022glare_type\u0022: \u0022FOG_GLOW\u0022}.",
      "type": [
        "object",
        "null"
      ],
      "default": null
    },
    "location": {
      "description": "add_node: [x, y] position in the editor.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": "number"
      },
      "default": null
    },
    "fromNode": {
      "description": "link: source node name.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "fromSocket": {
      "description": "link: output socket on the source, e.g. Image, Depth, Normal.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "toNode": {
      "description": "link: target node name.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "toSocket": {
      "description": "link: input socket on the target.",
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

## <code>blender_<wbr>file_<wbr>output</code>

Configures a File Output node in the compositor: a folder, a format with its depth and codec, and one slot per render pass wired from the Render Layers node, each with an optional format of its own. Passes that are off get switched on. A multilayer EXR keeps every slot in one file; other formats write one file per slot.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `action` | `string` | — | `configure` | configure (create or update the node), info (list File Output nodes), remove. |
| `name` | `string?` | — | `null` | Node name; File Output otherwise. Several nodes write to several places. |
| `directory` | `string?` | — | `null` | Absolute folder the files go to. |
| `fileName` | `string?` | — | `null` | File name prefix before the slot name and frame number, e.g. shot01_. |
| `fileFormat` | `string?` | — | `null` | OPEN_EXR_MULTILAYER (every slot in one file), OPEN_EXR, PNG, JPEG, TIFF, WEBP, DPX. |
| `colorDepth` | `integer?` | — | `null` | Bits per channel: 8 or 16 for PNG and TIFF, 16 (half) or 32 (full float) for EXR, 8, 10 or 12 for video and DPX. |
| `exrCodec` | `string?` | — | `null` | EXR compression: ZIP (lossless, the default), PIZ, ZIPS, RLE, PXR24 (lossy for 32-bit), DWAA and DWAB (lossy, small), B44, B44A, HTJ2K or NONE. |
| `slots` | `array?` | — | `null` | Slots, one per pass, replacing the current ones: pass names such as Image, Depth, Normal, Mist, AO, Vector, Position, UV, Emit, Env, Shadow, IndexOB, CryptoObject00, or objects {"pass": "Depth", "name": "Z", "file_format": "OPEN_EXR", "color_depth": 32, "save_as_render": false}; save_as_render applies the view transform, off for data passes and for EXR unless asked. |
| `allPasses` | `boolean` | — | `false` | One slot for every pass the Render Layers node offers. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "action": {
      "description": "configure (create or update the node), info (list File Output nodes), remove.",
      "type": "string",
      "default": "configure"
    },
    "name": {
      "description": "Node name; File Output otherwise. Several nodes write to several places.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "directory": {
      "description": "Absolute folder the files go to.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "fileName": {
      "description": "File name prefix before the slot name and frame number, e.g. shot01_.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "fileFormat": {
      "description": "OPEN_EXR_MULTILAYER (every slot in one file), OPEN_EXR, PNG, JPEG, TIFF, WEBP, DPX.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "colorDepth": {
      "description": "Bits per channel: 8 or 16 for PNG and TIFF, 16 (half) or 32 (full float) for EXR, 8, 10 or 12 for video and DPX.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "exrCodec": {
      "description": "EXR compression: ZIP (lossless, the default), PIZ, ZIPS, RLE, PXR24 (lossy for 32-bit), DWAA and DWAB (lossy, small), B44, B44A, HTJ2K or NONE.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "slots": {
      "description": "Slots, one per pass, replacing the current ones: pass names such as Image, Depth, Normal, Mist, AO, Vector, Position, UV, Emit, Env, Shadow, IndexOB, CryptoObject00, or objects {\u0022pass\u0022: \u0022Depth\u0022, \u0022name\u0022: \u0022Z\u0022, \u0022file_format\u0022: \u0022OPEN_EXR\u0022, \u0022color_depth\u0022: 32, \u0022save_as_render\u0022: false}; save_as_render applies the view transform, off for data passes and for EXR unless asked.",
      "type": [
        "array",
        "null"
      ],
      "default": null
    },
    "allPasses": {
      "description": "One slot for every pass the Render Layers node offers.",
      "type": "boolean",
      "default": false
    }
  }
}
```

</details>

## <code>blender_<wbr>cryptomatte_<wbr>matte</code>

Adds a Cryptomatte node that mattes the named objects, materials or assets, switching the cryptomatte pass on, and optionally writes the matte through a File Output slot. Its Matte output isolates them for grading or replacement.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `names` | `array` | yes | — | Objects, materials or assets to matte, by name. |
| `layer` | `string` | — | `object` | object (the default), material or asset: which cryptomatte layer the names live in. |
| `name` | `string?` | — | `null` | Name for the Cryptomatte node; derived from the names otherwise. |
| `output` | `string?` | — | `null` | File Output node to write the matte through; created when missing. |
| `directory` | `string?` | — | `null` | Folder for that File Output node. |
| `slot` | `string?` | — | `null` | Slot name in the File Output node; Matte otherwise. |
| `scene` | `string?` | — | `null` | Scene to act on; the current scene otherwise. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "names": {
      "description": "Objects, materials or assets to matte, by name.",
      "type": "array",
      "items": {
        "type": "string"
      }
    },
    "layer": {
      "description": "object (the default), material or asset: which cryptomatte layer the names live in.",
      "type": "string",
      "default": "object"
    },
    "name": {
      "description": "Name for the Cryptomatte node; derived from the names otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "output": {
      "description": "File Output node to write the matte through; created when missing.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "directory": {
      "description": "Folder for that File Output node.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "slot": {
      "description": "Slot name in the File Output node; Matte otherwise.",
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
  },
  "required": [
    "names"
  ]
}
```

</details>

