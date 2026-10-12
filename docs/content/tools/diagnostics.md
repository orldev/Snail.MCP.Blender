# Diagnostics

## <code>blender_<wbr>diagnose</code>

Reports the server version, whether the add-on inside Blender answers (with the Blender version and whether it is busy), the machine Blender runs on — platform, home and data directory, which is where every path in a tool call lives — how clients reach this server (stdio, or HTTP with its public address and how long file links last), the SSH tunnel to Blender when the server opens one, the project folder the client works in and its twin over there — over HTTP a note instead, since the server sees none of the client's folders — whether the add-on installed in Blender is the build this server ships and which commands it lacks if it is not, the effective configuration, the data directory, and the traffic over the link since the server started: requests, failures by type, the busiest commands with their timings, and how much of that traffic went through blender_python instead of the tools. Call it first when a tool answers unexpectedly.

*read-only · every call adds · stays inside Blender.*

Takes no parameters.

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {}
}
```

</details>

## <code>blender_<wbr>install_<wbr>addon</code>

Builds the add-on zip in the data directory and returns the path with the steps to install it in Blender (Preferences → Get Extensions → Install from Disk). Needed once per Blender installation and whenever blender_diagnose reports that the installed add-on differs from the one this server ships. Over HTTP the add-on comes with the Blender image beside this server, so a mismatch is fixed with the image rather than with this zip.

*only adds · every call adds · stays inside Blender.*

Takes no parameters.

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {}
}
```

</details>

