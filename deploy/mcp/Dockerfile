# syntax=docker/dockerfile:1
# The MCP server over HTTP, with its pages, for a Blender in the container beside it.
# Built from the repository root: docker build -f deploy/mcp/Dockerfile .

# Pinned to the band global.json asks for: on the floating tag a newer feature band would refuse the build, and the image and the NuGet package
# of one release would come from different toolchains.
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /source

# .editorconfig sets the analyzers' severities; without it the build treats warnings the checkout never raises as errors.
COPY .editorconfig Directory.Build.props README.md LICENSE global.json ./
COPY .mcp/server.json .mcp/server.json
COPY docs/theme/css/site.css docs/theme/css/site.css
COPY addon addon
COPY src src

# The checkout holds a placeholder version; a release passes the one of its tag, which blender_diagnose then reports.
ARG VERSION=0.0.0-dev

RUN dotnet publish src/Snail.MCP.Blender.csproj -c Release -o /app -p:UseAppHost=false -p:Version="$VERSION"

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11

WORKDIR /app
COPY --from=build /app .

# Every request at Information buries what matters in a container's log; the server's own warnings and failures stay.
ENV SNAIL_MCP_BLENDER_TRANSPORT=Http \
    SNAIL_MCP_BLENDER_HTTP__URL=http://0.0.0.0:8080 \
    SNAIL_MCP_BLENDER_DATA_DIRECTORY=/home/app/.snail-mcp-blender \
    Logging__LogLevel__Default=Information \
    Logging__LogLevel__Microsoft.AspNetCore=Warning \
    Logging__LogLevel__ModelContextProtocol=Warning

# The keys that sign sessions and form tokens live in a volume, so a recreated container does not sign everyone out.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys /home/app/.snail-mcp-blender \
 && chown -R app:app /home/app/.aspnet /home/app/.snail-mcp-blender

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "Snail.MCP.Blender.dll"]
