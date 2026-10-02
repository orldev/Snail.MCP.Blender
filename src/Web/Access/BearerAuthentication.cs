using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Snail.MCP.Blender.Application.Access;

namespace Snail.MCP.Blender.Web.Access;

/// <summary>Admits a request whose Authorization header carries a client's key as a bearer, and says which client it is.</summary>
/// <remarks>A request without the header gets no result rather than a failure, so a page or a signed link can still admit it by its own
/// scheme. The name on the ticket is the key's, not a header's: it is what the client's commands are signed with, and a name the caller
/// chose for itself would let anyone holding any key take another agent's lease.</remarks>
public sealed class BearerAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ClientKeys keys)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string Prefix = "Bearer ";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (!header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (keys.Holder(header[Prefix.Length..].Trim()) is not { } client)
        {
            return Task.FromResult(AuthenticateResult.Fail("the bearer token belongs to no client of this server, or to one that was revoked"));
        }

        List<Claim> claims = [new(ClaimTypes.Name, client.Name), .. client.Scopes.Select(scope => new Claim(WebAccess.ScopeClaim, scope))];

        if (client == keys.Configured)
        {
            claims.Add(new Claim(WebAccess.ConfiguredClaim, "true"));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";

        return Task.CompletedTask;
    }
}
