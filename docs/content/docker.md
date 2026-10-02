# Blender in Docker

Two images put the whole thing on a machine with a GPU: Blender without an interface, with the add-on installed, and
the MCP server in front of it, speaking HTTP. A client connects to one HTTPS address with one token — nothing to install
on the client's side, no SSH key, no tunnel. The projects, renders and snapshots live in a Docker volume beside Blender,
and the server has pages to look through them and clean them.

| Image | What it holds |
|---|---|
| `ghcr.io/orldev/s3l-blender:<blender>-<add-on>` | Ubuntu 24.04, Blender (5.2.1, checked by SHA-256), the Snail Bridge add-on; starts `blender -b -c snail_bridge` |
| `ghcr.io/orldev/s3l-blender-mcp:<version>` | the server on ASP.NET, in HTTP mode on port 8080, with its pages |

The Blender tag names both versions because the add-on is inside it: the pair in `compose.yml` is the pair
`blender_diagnose` reports as `current`.

```text
client (Claude Code, any folder)
  │  HTTPS + bearer token
  ▼
reverse proxy (Traefik) ── blender.example.com
  │  private address, port 8810
  ▼
s3l-blender-mcp ── /mcp, /raw/…, the pages, /healthz
  │  internal network, no way out, the bridge token
  ▼
s3l-blender ── Blender 5.2 -b + Snail Bridge on 9876, the GPU
  └─ volume data: files/<project>/, jobs/, batches/, snapshots/
```

## What the machine needs

- An NVIDIA GPU and its driver.
- Docker with GPU support: on Linux the NVIDIA Container Toolkit, on Windows Docker Desktop with the WSL 2 backend.
- A reverse proxy that ends TLS, and a name that points at the machine.

**OptiX and Docker Desktop.** Inside a container on Docker Desktop for Windows, OptiX cannot load: WSL hands the
container a stub `libnvoptix.so.1` that loads the real library through DXCore and does not export the entry point Cycles
looks for (`OptiX initialization failed with error code 7805`). CUDA works — set `BLENDER_BACKEND=CUDA`. On a Linux host
`NVIDIA_DRIVER_CAPABILITIES=all` brings the OptiX library in and `OPTIX` works. EEVEE renders in the container too, on
the CPU through Mesa, several times slower than Cycles on the GPU; use Cycles.

## 1. The folder

Everything the machine needs is one folder: `compose.yml` and `.env.example` from `deploy/` in the repository, and two
secrets beside them.

```text
s3l-blender/
├── compose.yml
├── .env                 # copied from .env.example
└── secrets/
    ├── bridge.token     # between the server and Blender; stays on this machine
    └── client.token     # what clients present; a copy goes to each client
```

Each token is 64 hexadecimal characters:

```bash
openssl rand -hex 32 > secrets/bridge.token
openssl rand -hex 32 > secrets/client.token
```

On Windows without OpenSSL, in PowerShell:

```powershell
foreach ($name in 'bridge', 'client') {
  $bytes = New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
  ($bytes | ForEach-Object { $_.ToString('x2') }) -join '' | Set-Content -NoNewline -Encoding ascii "secrets\$name.token"
}
```

`.env` holds what differs between machines:

| Setting | Default | Meaning |
|---|---|---|
| `BLENDER_TAG` | `latest` | The Blender image: `latest`, or a release as `5.2.1-X.Y.Z` |
| `MCP_TAG` | `latest` | The server image: `latest`, or a release as `X.Y.Z`; pin both to the same release |
| `BLENDER_BACKEND` | `OPTIX` | The Cycles compute backend Blender enables at start: `OPTIX`, `CUDA`, `NONE` for the CPU; `CUDA` on Docker Desktop. A scene renders on it once its device is GPU |
| `PUBLIC_URL` | `https://blender.example.com` | The address clients reach the server at; links to files are built on it |
| `PUBLISH_ADDRESS` | `192.0.2.10` | The address the server port is published on — one the proxy reaches, never the public one |
| `PUBLISH_PORT` | `8810` | That port |
| `PYTHON_ACCESS` | `Fallback` | `Allow`, `Fallback` (a script only where no tool covers the job) or `Off` |
| `BLENDER_MEMORY` | `24g` | What Blender may use before the kernel stops it; a scene that does not fit kills the container instead of swapping the machine to a standstill |
| `MCP_MEMORY` | `1g` | The same for the server, which holds a chunk of a transfer at a time and nothing else large |

The stack keeps four volumes: Blender's data directory (projects, render jobs, batches, snapshots), its kernel cache,
the keys that sign the pages' sessions, and the server's own data directory — which is where the keys clients are
admitted by live, so `clients add` survives an update of the stack.

## 2. Images

A tag `vX.Y.Z` in the repository publishes both images to GHCR once the whole CI has passed: the server as `X.Y.Z` and
Blender as `5.2.1-X.Y.Z`. The pair is then started and asked whether the server reaches Blender, the package goes to
NuGet, and only after that does `latest` move to this release, and only for one without a suffix. The images are
private. Log in once with a token that has `read:packages`:

```bash
docker login ghcr.io -u orldev
```

Or build them on the machine itself, from a checkout:

```bash
docker build -f deploy/blender/Dockerfile -t ghcr.io/orldev/s3l-blender:latest .
docker build -f deploy/mcp/Dockerfile --build-arg VERSION=0.1.0 -t ghcr.io/orldev/s3l-blender-mcp:latest .
```

`VERSION` is the version the server reports in `blender_diagnose` and on its status page; without it a build from a
checkout reports `0.0.0-dev`.

## 3. Start

```bash
docker compose up -d
docker compose ps
curl http://192.0.2.10:8810/healthz        # {"status":"ok","blender":"up"}
```

`s3l-blender` turns `healthy` once the bridge listens, about half a minute after the start. The server asks Blender
nothing until a client does, so `blender: down` right after the start only means Blender is still starting.

## 4. The reverse proxy

With Traefik's file provider, a route next to the other ones — the file is `deploy/traefik/s3l-blender.yml`:

```yaml
http:
  routers:
    s3l-blender:
      entryPoints:
      - https
      service: s3l-blender-host
      rule: Host(`blender.example.com`)
      tls:
        certResolver: letsEncrypt
  services:
    s3l-blender-host:
      loadBalancer:
        servers:
        - url: http://192.0.2.10:8810/
        passHostHeader: true
```

Traefik watches that directory, but not every file system tells it about a new file: on Docker Desktop for Windows a
file added to a bind-mounted Windows folder stays unnoticed until Traefik restarts (`docker restart traefik`, which
drops its other sites for a few seconds). Traefik v3 also stops reading a request body after 60 seconds by default; the
upload commands send a file in parts of 16 MB for that reason, each well inside the minute even on a slow uplink, so the
default can stay. Another proxy needs the same three things: TLS, the host name, and the published port behind it.

## 5. Connect Claude Code

The token goes into the client's MCP entry. In user scope the tools are there in every folder the client opens:

```bash
claude mcp add --transport http --scope user blender https://blender.example.com/mcp \
  --header "Authorization: Bearer $(cat ~/.snail-mcp-blender/remote/docker.token)"
```

Copy `secrets/client.token` from the machine to `~/.snail-mcp-blender/remote/docker.token` on the client, readable by you
alone (`chmod 600`). `blender_diagnose` then reports `transport.kind: http` with the public address and the link minutes,
the add-on as `current` and Blender's machine as `linux`.

## 6. A session

The server beside Blender sees none of the client's folders, so a project is a folder in the volume named after the
folder the client works in, and files travel by commands the client runs itself:

1. `blender_open_project` with the name of the folder you work in opens or creates `files/<name>/<name>.blend`, with
   `renders/` and `textures/` beside it.
2. `blender_upload` with a `remotePath` in the project — `<name>/textures` — returns a command that uploads through a
   link valid for this path only, for 15 minutes; without a `remotePath`, or with an absolute one, it refuses. The agent
   runs the command in its own shell, from the folder it works in: it packs a folder into a temporary tar, sends the file in parts of 16 MB with `dd` and
   `curl`, and the last part carries the SHA-256 of the whole, which the server checks before the file takes its name.
   160 MB over a 2 MB/s uplink took 97 seconds this way.
3. Build and render as usual; `blender_set_cycles` with `backend: OPTIX` (or `CUDA`) puts the scene on the GPU, and renders
   go into the `renders` path `blender_open_project` named.
4. `blender_download` of `<name>/renders` returns `curl | tar` that unpacks the renders into `renders/` of the client's
   folder, keeping files that are already there unless `overwrite`. The reply says how many files and bytes will arrive.
   The server checks every file against the SHA-256 the add-on computed as it sends it, and the command fails when one
   differs; a single file lands under a partial name first and takes its own only when whole.

## 7. The pages

The server's own address opens its pages — `https://blender.example.com/`. Sign in with the client token; the
session lasts twelve hours and is renewed while the pages are in use.

| Page | Shows |
|---|---|
| Status | refreshed every ten seconds: whether Blender answers, the add-on build, what Blender is busy with, the open file and unsaved changes, the GPU, render jobs, the size of each area of the volume and the free disk, the server |
| Files | the projects: folders and files with sizes; an image or a small text file opens as a preview; a file downloads, a folder comes as a zip |
| Jobs, Batches | background renders and batches with their state and progress; a queued or running job can be cancelled, and everything that ended can be deleted in one go |
| Snapshots | snapshots with their notes; each downloads as its `.blend` |

Deleting asks first and cannot be undone. The add-on refuses to delete the file open in Blender, a folder holding it, and
a job or batch still queued or running, and the page says so.

## 8. Running it

| To | Command |
|---|---|
| see the state | `docker compose ps` |
| read the logs | `docker compose logs -f blender` or `mcp` |
| restart | `docker compose restart` |
| stop | `docker compose stop` — the scene open in Blender is lost unless saved |
| update | set the tags in `.env`, then `docker compose pull && docker compose up -d` |
| remove the containers | `docker compose down` — the volumes, and with them the projects, stay |
| remove everything | `docker compose down -v` — **deletes every project, render and snapshot**, the compiled GPU kernels and the keys that keep browsers signed in |
| back up the projects | `docker run --rm -v s3l-blender_data:/data -v "$PWD":/backup ubuntu tar czf /backup/data.tgz -C /data .` |

Docker Desktop on Windows starts with the user's session: after a reboot the containers come back once someone signs in
to Windows.

**Docker over SSH on Windows.** An SSH session logged in with a key has no access to the Windows Credential Manager,
so `docker pull` and `docker build` fail with `A specified logon session does not exist`. Give that session a Docker
configuration without a credential helper — an empty `config.json` in a folder of its own named by `DOCKER_CONFIG`, and
a `PATH` holding a copy of `docker.exe` without `docker-credential-wincred` beside it.

## Security

- The client token opens `/mcp`, the files and the pages; keep it out of repositories.
- The bridge token never leaves the machine, and Blender's network is internal: nothing but the server reaches it, and
  Blender reaches nothing outside.
- A link to a file names one path, one method and an expiry, signed with a key derived from the client token; rotating
  the token revokes every link.
- Files are served sandboxed (`Content-Security-Policy: sandbox`), so an SVG or HTML file from the volume runs no script
  in the pages' origin. Forms carry antiforgery tokens, and the session cookie is `HttpOnly`, `SameSite=Strict` and
  `Secure` behind an `https` address.
- `PYTHON_ACCESS=Fallback` keeps `blender_python` for what no tool covers, and reads a `python` step of `blender_run` or
  `blender_batch` the same way; `Off` refuses Python altogether, the operators that run scripts included.
