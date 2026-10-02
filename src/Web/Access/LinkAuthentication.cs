using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Snail.MCP.Blender.Application.Access;

namespace Snail.MCP.Blender.Web.Access;

/// <summary>Admits a request whose query carries a link signed for its own method, path, format and replacing.</summary>
/// <remarks>The replacing flag is read here and checked against the signature, so a link issued to write a new file cannot be turned into one
/// that replaces an existing file by adding a word to the query.</remarks>
public sealed class LinkAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, FileLinks links)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Query.TryGetValue("signature", out var signature))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var isValid = long.TryParse(Request.Query["expires"], out var expires)
            && links.Verifies(
                Request.Method,
                Request.Path.Value ?? string.Empty,
                Request.Query["format"],
                string.Equals(Request.Query["overwrite"], "true", StringComparison.OrdinalIgnoreCase),
                expires,
                signature.ToString());

        if (!isValid)
        {
            return Task.FromResult(AuthenticateResult.Fail("the link is not signed for this request or has expired"));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "link")], Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;

        return Task.CompletedTask;
    }
}
