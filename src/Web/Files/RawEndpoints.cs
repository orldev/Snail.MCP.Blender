using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Storage;
using Snail.MCP.Blender.Web.Access;

namespace Snail.MCP.Blender.Web.Files;

/// <summary>The volume's files over HTTP: read a file or a folder as an archive, write a file or unpack a tar into a folder, delete.</summary>
/// <remarks>Only the files area takes writes: jobs, batches and snapshots are made by Blender, and a page or a link only reads or removes them.
/// A path is checked here before it reaches the add-on, which checks it again against the area it names. What is read comes sandboxed and
/// unsniffed: an SVG or an HTML file from the volume must not run script in the pages' origin.</remarks>
public static class RawEndpoints
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static IEndpointRouteBuilder MapRawFiles(this IEndpointRouteBuilder endpoints)
    {
        var raw = endpoints.MapGroup(FileLinks.RawPrefix);

        raw.MapGet("/{area}/{**path}", ReadAsync).RequireAuthorization(WebAccess.FilesReadPolicy);
        raw.MapPut("/{area}/{**path}", WriteAsync).RequireAuthorization(WebAccess.FilesWritePolicy);
        raw.MapDelete("/{area}/{**path}", DeleteAsync).RequireAuthorization(WebAccess.FilesDeletePolicy);

        return endpoints;
    }

    private static async Task<IResult> ReadAsync(HttpContext context, Volume volume, string area, string? path, string? format, bool? download, CancellationToken cancellationToken)
    {
        if (Refused(area, path) is { } refusal)
        {
            return refusal;
        }

        context.Response.Headers.ContentSecurityPolicy = "sandbox";
        context.Response.Headers.XContentTypeOptions = "nosniff";

        var relative = path ?? string.Empty;
        var lookup = await volume.FindAsync(area, relative, cancellationToken);

        if (lookup.Item is not { } item)
        {
            return Failure(lookup.Error!);
        }

        var name = Name(area, relative);

        if (!item.IsFolder)
        {
            context.Response.ContentLength = item.Bytes;

            return Results.Stream(
                body => CopyAsync(volume.OpenRead(area, relative, item.Bytes), body, cancellationToken),
                ContentTypes.TryGetContentType(name, out var contentType) ? contentType : "application/octet-stream",
                download == true ? name : null);
        }

        if (item.Truncated)
        {
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, detail: $"'{relative}' holds more files than one archive carries; download its folders one by one");
        }

        if (format != FileLinks.ZipFormat)
        {
            return Results.Stream(body => volume.WriteTarAsync(area, relative, item, body, cancellationToken), "application/x-tar", $"{name}.tar");
        }

        AllowSynchronousWrites(context);

        return Results.Stream(body => volume.WriteZipAsync(area, relative, item, body, cancellationToken), "application/zip", $"{name}.zip");
    }

    /// <summary>A body is a whole file, or with offset and last one part of a file sent in several; a tar sent in parts gathers in a staging
    /// file and is unpacked when its last part arrives.</summary>
    private static async Task<IResult> WriteAsync(
        HttpContext context, Volume volume, string area, string? path, string? format, bool? overwrite, long? offset, bool? last, string? sha256, CancellationToken cancellationToken)
    {
        if (Refused(area, path) is { } refusal)
        {
            return refusal;
        }

        if (offset is < 0 || (sha256 is not null && !IsDigest(sha256)))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "offset is a byte position of 0 or more, and sha256 is 64 hexadecimal characters");
        }

        var part = offset is null && last is null ? null : new UploadPart(offset ?? 0, last ?? true, sha256?.ToLowerInvariant());

        if (area != VolumeAreas.Files)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: $"only the {VolumeAreas.Files} area takes uploads");
        }

        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = null;
        }

        var relative = path ?? string.Empty;

        if (format == FileLinks.TarFormat && part is null)
        {
            return Unpacked(await volume.UnpackTarAsync(area, relative, context.Request.Body, overwrite == true, cancellationToken));
        }

        if (format == FileLinks.TarFormat)
        {
            var staged = Volume.StagingPath(relative, context.User.Identity?.Name);
            var gathered = await volume.WritePartAsync(area, staged, context.Request.Body, part!, overwrite: true, cancellationToken);

            if (!gathered.IsOk)
            {
                return Failure(gathered.Error!);
            }

            return part!.IsLast
                ? Unpacked(await volume.UnpackStagedTarAsync(area, relative, staged, overwrite == true, cancellationToken))
                : Results.Ok(gathered.Result);
        }

        if (relative.Length == 0)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "a file needs a path inside the area; a tar with format=tar unpacks into a folder");
        }

        var written = part is null
            ? await volume.WriteAsync(area, relative, context.Request.Body, overwrite == true, cancellationToken)
            : await volume.WritePartAsync(area, relative, context.Request.Body, part, overwrite == true, cancellationToken);

        return written.IsOk ? Results.Ok(written.Result) : Failure(written.Error!);
    }

    private static async Task<IResult> DeleteAsync(Volume volume, string area, string? path, CancellationToken cancellationToken)
    {
        if (Refused(area, path) is { } refusal)
        {
            return refusal;
        }

        var deleted = await volume.DeleteAsync(area, path ?? string.Empty, cancellationToken);

        return deleted.IsOk ? Results.Ok(deleted.Result) : Failure(deleted.Error!);
    }

    /// <summary>A zip entry flushes its compressor synchronously when it closes, even when closed asynchronously, and Kestrel refuses synchronous
    /// writes unless the request allows them; a browser's zip download is the one response that needs it.</summary>
    private static void AllowSynchronousWrites(HttpContext context)
    {
        if (context.Features.Get<IHttpBodyControlFeature>() is { } body)
        {
            body.AllowSynchronousIO = true;
        }
    }

    private static IResult Unpacked(VolumeWrite unpacked) =>
        unpacked.Error is { } error
            ? Failure(error)
            : Results.Ok(new { files = unpacked.Files.Select(file => new { path = file.To, size = file.Size, sha256 = file.Sha256 }), bytes = unpacked.Files.Sum(file => file.Size) });

    private static bool IsDigest(string value) => value.Length == 64 && value.All(char.IsAsciiHexDigit);

    private static async Task CopyAsync(Stream source, Stream body, CancellationToken cancellationToken)
    {
        await using (source)
        {
            await source.CopyToAsync(body, cancellationToken);
        }
    }

    /// <summary>An unknown area, or a path that is absolute, climbs out or names a drive, is refused before it reaches Blender.</summary>
    private static IResult? Refused(string area, string? path)
    {
        if (!VolumeAreas.All.Contains(area))
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: $"no area '{area}'; the areas are {string.Join(", ", VolumeAreas.All)}");
        }

        var relative = path ?? string.Empty;

        return !AreaPaths.IsConfined(relative) ? Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: $"'{relative}' must stay inside the {area} area") : null;
    }

    private static string Name(string area, string relative) => relative.Length == 0 ? area : relative.TrimEnd('/').Split('/')[^1];

    private static IResult Failure(BridgeError error) => Results.Problem(
        statusCode: error.Type switch
        {
            "NotFound" => StatusCodes.Status404NotFound,
            "BadRequest" => StatusCodes.Status400BadRequest,
            "InUse" or "Exists" => StatusCodes.Status409Conflict,
            "Corrupt" => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status502BadGateway,
        },
        title: error.Type,
        detail: error.Message);
}
