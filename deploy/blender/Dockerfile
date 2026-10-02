# syntax=docker/dockerfile:1
# Blender without an interface, with the Snail Bridge add-on installed, serving the MCP server that runs beside it.
# Built from the repository root: docker build -f deploy/blender/Dockerfile .

FROM ubuntu:25.10

ARG BLENDER_VERSION=5.2.1
ARG BLENDER_SHA256=a31f524fa99a527d3d52b7f5aaa68c34e1a19d5a1c9473f79c5cc610fd5b10e9
# download.blender.org answers scripted downloads with 403; the mirrors carry the same files and the checksum proves it.
ARG BLENDER_MIRROR=https://mirrors.ocf.berkeley.edu/blender/release

ENV DEBIAN_FRONTEND=noninteractive

# The libraries Blender links against live in packages.txt, which the live tests in CI install from too.
COPY deploy/blender/packages.txt /tmp/packages.txt

RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl xz-utils $(cat /tmp/packages.txt) \
 && rm -rf /var/lib/apt/lists/* /tmp/packages.txt

# The checksum is checked before anything is unpacked, and a library Blender links against but the image lacks fails the build here, not at start.
RUN series="${BLENDER_VERSION%.*}" \
 && curl -fsSL "${BLENDER_MIRROR}/Blender${series}/blender-${BLENDER_VERSION}-linux-x64.tar.xz" -o /tmp/blender.tar.xz \
 && echo "${BLENDER_SHA256}  /tmp/blender.tar.xz" | sha256sum -c - \
 && mkdir /opt/blender \
 && tar -xJf /tmp/blender.tar.xz -C /opt/blender --strip-components=1 \
 && rm /tmp/blender.tar.xz \
 && missing="$(ldd /opt/blender/blender | grep 'not found' || true)" \
 && if [ -n "$missing" ]; then echo "$missing"; exit 1; fi

RUN userdel --remove ubuntu \
 && useradd --create-home --uid 1000 blender

USER blender
WORKDIR /home/blender

COPY --chown=blender:blender addon /tmp/snail_bridge

RUN mkdir -p /tmp/extension \
 && /opt/blender/blender -b --factory-startup --command extension build --source-dir /tmp/snail_bridge --output-dir /tmp/extension \
 && /opt/blender/blender -b --command extension install-file -r user_default -e /tmp/extension/snail_bridge-*.zip \
 && rm -rf /tmp/snail_bridge /tmp/extension \
 && mkdir -p /home/blender/.snail-mcp-blender /home/blender/.cache

VOLUME ["/home/blender/.snail-mcp-blender", "/home/blender/.cache"]

EXPOSE 9876

HEALTHCHECK --interval=30s --timeout=5s --start-period=90s CMD bash -c '</dev/tcp/127.0.0.1/9876' || exit 1

ENTRYPOINT ["/opt/blender/blender", "-b", "-c", "snail_bridge"]
CMD ["--host", "0.0.0.0", "--port", "9876"]
