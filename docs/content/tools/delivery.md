# Encode and mixdown

Part of the `video` skill: these tools appear once it is loaded — by `blender_enable_skill` with `video`, or by `find_tool` when a search returns one of them.

## <code>blender_<wbr>sequencer_<wbr>encode</code>

Sets the encode without rendering: container, codec (H264, H265, AV1, ProRes with profile, DNxHD, FFV1, WebM), quality by constant rate factor or a target bitrate with limits, GOP, B-frames, encoder preset, colour depth and audio codec, bitrate, sample rate and channels. blender_sequencer_render then uses these settings.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `container` | `string?` | — | `null` | mp4, mkv, webm, mov, avi or ogv; the extension of the render path can also decide. |
| `codec` | `string?` | — | `null` | H264, H265, AV1, WEBM (VP9), PRORES, DNXHD, FFV1 (lossless), HUFFYUV, MPEG4, THEORA, PNG, QTRLE. |
| `proresProfile` | `string?` | — | `null` | ProRes profile, which also selects the PRORES codec: 422_PROXY, 422_LT, 422_STD, 422_HQ, 4444 or 4444_XQ. |
| `quality` | `string?` | — | `null` | LOSSLESS, PERC_LOSSLESS, HIGH, MEDIUM, LOW, VERYLOW, LOWEST; constant quality instead of a bitrate. |
| `crf` | `integer?` | — | `null` | Exact constant rate factor 0 to 51 for H.264/H.265/AV1; 18 is visually lossless, 23 the default. |
| `bitrate` | `integer?` | — | `null` | Target bitrate in kbit/s; switches rate control from quality to bitrate. |
| `maxBitrate` | `integer?` | — | `null` | Maximum bitrate in kbit/s. |
| `minBitrate` | `integer?` | — | `null` | Minimum bitrate in kbit/s. |
| `bufferSize` | `integer?` | — | `null` | Rate control buffer in kbit. |
| `gop` | `integer?` | — | `null` | Keyframe interval in frames; smaller seeks better, larger compresses better. |
| `maxBFrames` | `integer?` | — | `null` | B-frames between keyframes; 0 disables them. |
| `preset` | `string?` | — | `null` | Encoder speed preset: BEST (slowest, smallest), GOOD or REALTIME. |
| `lossless` | `boolean?` | — | `null` | Lossless output for codecs that support it. |
| `autosplit` | `boolean?` | — | `null` | Split the file every 2 GB. |
| `colorDepth` | `integer?` | — | `null` | 8, 10 or 12 bits per channel for codecs that support it. |
| `audio` | `object?` | — | `null` | Audio codec, bitrate, sample rate and channels. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "container": {
      "description": "mp4, mkv, webm, mov, avi or ogv; the extension of the render path can also decide.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "codec": {
      "description": "H264, H265, AV1, WEBM (VP9), PRORES, DNXHD, FFV1 (lossless), HUFFYUV, MPEG4, THEORA, PNG, QTRLE.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "proresProfile": {
      "description": "ProRes profile, which also selects the PRORES codec: 422_PROXY, 422_LT, 422_STD, 422_HQ, 4444 or 4444_XQ.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "quality": {
      "description": "LOSSLESS, PERC_LOSSLESS, HIGH, MEDIUM, LOW, VERYLOW, LOWEST; constant quality instead of a bitrate.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "crf": {
      "description": "Exact constant rate factor 0 to 51 for H.264/H.265/AV1; 18 is visually lossless, 23 the default.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "bitrate": {
      "description": "Target bitrate in kbit/s; switches rate control from quality to bitrate.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "maxBitrate": {
      "description": "Maximum bitrate in kbit/s.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "minBitrate": {
      "description": "Minimum bitrate in kbit/s.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "bufferSize": {
      "description": "Rate control buffer in kbit.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "gop": {
      "description": "Keyframe interval in frames; smaller seeks better, larger compresses better.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "maxBFrames": {
      "description": "B-frames between keyframes; 0 disables them.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "preset": {
      "description": "Encoder speed preset: BEST (slowest, smallest), GOOD or REALTIME.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "lossless": {
      "description": "Lossless output for codecs that support it.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "autosplit": {
      "description": "Split the file every 2 GB.",
      "type": [
        "boolean",
        "null"
      ],
      "default": null
    },
    "colorDepth": {
      "description": "8, 10 or 12 bits per channel for codecs that support it.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "audio": {
      "description": "Audio codec, bitrate, sample rate and channels.",
      "type": [
        "object",
        "null"
      ],
      "properties": {
        "codec": {
          "description": "AAC, MP3, VORBIS, OPUS, FLAC, PCM, AC3 or NONE.",
          "type": [
            "string",
            "null"
          ]
        },
        "bitrate": {
          "description": "kbit/s for lossy codecs.",
          "type": [
            "integer",
            "null"
          ]
        },
        "sampleRate": {
          "description": "Sample rate in Hz; 48000 for video.",
          "type": [
            "integer",
            "null"
          ]
        },
        "channels": {
          "description": "MONO, STEREO, STEREO_LFE, SURROUND4, SURROUND5, SURROUND51, SURROUND61 or SURROUND71.",
          "type": [
            "string",
            "null"
          ]
        },
        "volume": {
          "description": "Volume; 1 keeps the mix level.",
          "type": [
            "number",
            "null"
          ]
        }
      },
      "default": null
    }
  }
}
```

</details>

## <code>blender_<wbr>sequencer_<wbr>mixdown</code>

Mixes every sound strip of the timeline down to one audio file: WAV, FLAC, MP3, OGG, AAC or AC3, with sample format, bitrate and channel layout.

*only adds · every call adds · stays inside Blender.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path of the audio file; the extension picks the container: .wav, .flac, .mp3, .ogg, .aac, .ac3. |
| `codec` | `string?` | — | `null` | PCM, FLAC, MP3, VORBIS, AAC, AC3 or MP2; the container's natural codec otherwise. |
| `format` | `string?` | — | `null` | Sample format: U8, S16, S24, S32, F32 or F64; F32 for WAV, S16 otherwise. |
| `bitrate` | `integer?` | — | `null` | Bitrate in kbit/s for lossy codecs; 192 is the default. |
| `channels` | `string?` | — | `null` | MONO, STEREO, STEREO_LFE, SURROUND4, SURROUND5, SURROUND51, SURROUND61 or SURROUND71. |
| `splitChannels` | `boolean` | — | `false` | Write each channel to its own file. |
| `timeoutSeconds` | `integer` | — | `300` | Seconds to wait for Blender, 1 to 600. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path of the audio file; the extension picks the container: .wav, .flac, .mp3, .ogg, .aac, .ac3.",
      "type": "string"
    },
    "codec": {
      "description": "PCM, FLAC, MP3, VORBIS, AAC, AC3 or MP2; the container\u0027s natural codec otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "format": {
      "description": "Sample format: U8, S16, S24, S32, F32 or F64; F32 for WAV, S16 otherwise.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "bitrate": {
      "description": "Bitrate in kbit/s for lossy codecs; 192 is the default.",
      "type": [
        "integer",
        "null"
      ],
      "default": null
    },
    "channels": {
      "description": "MONO, STEREO, STEREO_LFE, SURROUND4, SURROUND5, SURROUND51, SURROUND61 or SURROUND71.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "splitChannels": {
      "description": "Write each channel to its own file.",
      "type": "boolean",
      "default": false
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

