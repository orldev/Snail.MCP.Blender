using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Application.Storage;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;
using Snail.MCP.Blender.Web.Access;

namespace Snail.MCP.Blender.Web.Pages;

/// <summary>The pages of the server beside Blender: signing in with the client token, the status, and the areas of the volume to look through and clean.</summary>
/// <remarks>Every action is a form that posts and returns to the page it came from, so the pages need no script to work; the script only asks
/// before a deletion and shows times in the reader's zone. Forms carry an antiforgery token, and the session cookie is SameSite=Strict.</remarks>
public static class PageEndpoints
{
    private const string LoginPath = "/login";

    private static readonly string[] Browsable = ["files", "jobs", "batches", "snapshots"];



    public static IEndpointRouteBuilder MapPages(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(LoginPath, ShowLogin).AllowAnonymous();
        endpoints.MapPost(LoginPath, SignInAsync).AllowAnonymous();
        endpoints.MapPost("/logout", (Delegate)SignOutAsync).RequireAuthorization(WebAccess.BrowserPolicy);
        endpoints.MapGet("/assets/{name}", Asset).AllowAnonymous();

        endpoints.MapGet("/", StatusAsync).RequireAuthorization(WebAccess.BrowserPolicy);

        foreach (var area in Browsable)
        {
            endpoints.MapGet($"/{area}/{{**path}}", (Volume volume, string? path, int? deleted, long? freed, string? cancelled, string? problem, CancellationToken cancellationToken) =>
                    BrowseAsync(volume, area, path, PageViews.FlashOf(deleted, freed, cancelled, problem), cancellationToken))
                .RequireAuthorization(WebAccess.BrowserPolicy);
        }

        endpoints.MapPost("/delete", DeleteAsync).RequireAuthorization(WebAccess.BrowserDeletePolicy);
        endpoints.MapPost("/prune", PruneAsync).RequireAuthorization(WebAccess.BrowserDeletePolicy);
        endpoints.MapPost("/cancel", CancelAsync).RequireAuthorization(WebAccess.BrowserFarmPolicy);

        return endpoints;
    }

    private static IResult ShowLogin(HttpContext context, string? returnUrl) =>
        context.User.Identity?.IsAuthenticated == true
            ? Results.LocalRedirect(Local(returnUrl))
            : Page<LoginPage>(new { ReturnUrl = Local(returnUrl) });

    /// <summary>Signs a browser in with any client's key, and remembers whose it was: the session is that client's, and ends when its key does.</summary>
    private static async Task<IResult> SignInAsync(HttpContext context, ClientKeys keys, [FromForm(Name = "token")] string? presented, [FromForm] string? returnUrl)
    {
        if (keys.Holder(presented) is not { } client)
        {
            return Page<LoginPage>(new { ReturnUrl = Local(returnUrl), IsRefused = true }, StatusCodes.Status401Unauthorized);
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, client.Name), .. client.Scopes.Select(scope => new Claim(WebAccess.ScopeClaim, scope))], WebAccess.CookieScheme);
        await context.SignInAsync(WebAccess.CookieScheme, new ClaimsPrincipal(identity));

        return Results.LocalRedirect(Local(returnUrl));
    }

    private static async Task<IResult> SignOutAsync(HttpContext context)
    {
        await context.SignOutAsync(WebAccess.CookieScheme);

        return Results.LocalRedirect(LoginPath);
    }

    private static async Task<IResult> StatusAsync(ServerHealth health, Volume volume, ServerConfig config, TimeProvider time, CancellationToken cancellationToken)
    {
        var report = await health.ReportAsync(cancellationToken);
        var storage = await volume.MeasureAsync(cancellationToken);
        var jobs = await volume.ListAsync("jobs", string.Empty, cancellationToken);
        var running = time.GetUtcNow() - new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime());

        return Page<StatusPage>(new { Groups = PageViews.Status(report, storage.Result as JsonObject, jobs.Result as JsonObject, config, running) });
    }

    private static async Task<IResult> BrowseAsync(Volume volume, string area, string? path, Flash? flash, CancellationToken cancellationToken)
    {
        var relative = (path ?? string.Empty).Trim('/');
        var listing = await volume.ListAsync(area, relative, cancellationToken);

        if (listing.IsOk && listing.Result is JsonObject folder)
        {
            return Page<BrowsePage>(new { View = PageViews.Browse(area, relative, folder, flash) });
        }

        if (listing.Error?.Type == "NotFound" && relative.Length > 0 && await volume.FindAsync(area, relative, cancellationToken) is { Item: { IsFolder: false } file })
        {
            var text = PageViews.IsText(relative) && file.Bytes <= PageViews.LargestTextPreview ? await ReadTextAsync(volume, area, relative, file.Bytes, cancellationToken) : null;

            return Page<FilePage>(new { View = PageViews.File(area, relative, file.Bytes, text), Flash = flash });
        }

        return Page<BrowsePage>(new { View = PageViews.Unavailable(area, relative, listing.Error ?? new BridgeError("NotFound", $"nothing at '{relative}'"), flash) });
    }

    /// <summary>The form is read whole: a list of ticked names does not bind to a string array on this framework, which looks for a parser on string.</summary>
    private static async Task<IResult> DeleteAsync(Volume volume, IFormCollection form, CancellationToken cancellationToken)
    {
        var area = form["area"].ToString();

        if (!Browsable.Contains(area))
        {
            return Results.NotFound();
        }

        var folder = form["path"].ToString().Trim('/');
        string[] chosen = form.TryGetValue("only", out var only) ? [only.ToString()] : [.. form["names"].OfType<string>()];

        if (!AreaPaths.IsConfined(folder))
        {
            return Back(area, string.Empty, problem: Messages.FolderOutsideTheArea);
        }

        if (chosen.Length == 0)
        {
            return Back(area, folder, problem: Messages.NothingSelected);
        }

        if (chosen.Any(name => name.Trim().Length == 0 || name.Contains('/') || name.Contains('\\') || !AreaPaths.IsConfined(name)))
        {
            return Back(area, folder, problem: Messages.NotOneName);
        }

        var (count, bytes, problem) = await DeleteEachAsync(volume, area, [.. chosen.Select(name => PageViews.Join(folder, name))], cancellationToken);

        return Back(area, folder, count, bytes, problem);
    }

    private static async Task<IResult> PruneAsync(Volume volume, [FromForm] string area, CancellationToken cancellationToken)
    {
        if (area is not ("jobs" or "batches"))
        {
            return Results.NotFound();
        }

        var listing = await volume.ListAsync(area, string.Empty, cancellationToken);
        var ended = (listing.Result?["entries"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(entry => JobStates.HasEnded((entry["job"] ?? entry["batch"])?["state"]?.GetValue<string>()))
            .Select(entry => entry["name"]!.GetValue<string>())
            .ToList();

        var (count, bytes, problem) = await DeleteEachAsync(volume, area, ended, cancellationToken);

        return Back(area, string.Empty, count, bytes, problem ?? listing.Error?.Message);
    }

    private static async Task<IResult> CancelAsync(IBlenderBridge bridge, [FromForm] string id, CancellationToken cancellationToken)
    {
        var reply = await bridge.SendAsync(BridgeCommands.RenderJobCancel, new JsonObject { ["id"] = id }, cancellationToken: cancellationToken);

        return reply.IsOk ? Results.LocalRedirect($"/jobs?cancelled={Uri.EscapeDataString(id)}") : Back("jobs", string.Empty, problem: reply.Error!.Message);
    }

    private static IResult Asset(string name)
    {
        var stream = typeof(PageEndpoints).Assembly.GetManifestResourceStream($"assets/{name}");

        return stream is null
            ? Results.NotFound()
            : Results.Stream(stream, name.EndsWith(".css", StringComparison.Ordinal) ? "text/css; charset=utf-8" : "text/javascript; charset=utf-8");
    }

    /// <summary>Deletes one path after another and stops at the first refusal, so a page says exactly what did not go.</summary>
    private static async Task<(int Count, long Bytes, string? Problem)> DeleteEachAsync(Volume volume, string area, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var count = 0;
        long bytes = 0;

        foreach (var path in paths)
        {
            var reply = await volume.DeleteAsync(area, path, cancellationToken);

            if (!reply.IsOk)
            {
                return (count, bytes, reply.Error!.Message);
            }

            count++;
            bytes += reply.Result?["bytes"]?.GetValue<long>() ?? 0;
        }

        return (count, bytes, null);
    }

    private static async Task<string> ReadTextAsync(Volume volume, string area, string path, long bytes, CancellationToken cancellationToken)
    {
        await using var content = volume.OpenRead(area, path, bytes);
        using var reader = new StreamReader(content, Encoding.UTF8);

        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static RedirectHttpResult Back(string area, string folder, int? deleted = null, long? freed = null, string? problem = null)
    {
        var query = new List<string>();

        if (deleted is { } count)
        {
            query.Add($"deleted={count}&freed={freed ?? 0}");
        }

        if (problem is not null)
        {
            query.Add($"problem={Uri.EscapeDataString(problem)}");
        }

        return TypedResults.LocalRedirect($"{PageViews.Page(area, folder)}{(query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}")}");
    }

    /// <summary>A return address only when it stays on this site: a path, never another host.</summary>
    private static string Local(string? returnUrl) =>
        returnUrl is { Length: > 0 } && returnUrl[0] == '/' && !returnUrl.StartsWith("//", StringComparison.Ordinal) && !returnUrl.StartsWith("/\\", StringComparison.Ordinal) ? returnUrl : "/";

    private static IResult Page<TPage>(object parameters, int status = StatusCodes.Status200OK) where TPage : IComponent =>
        new RazorComponentResult<TPage>(parameters) { StatusCode = status };
}
