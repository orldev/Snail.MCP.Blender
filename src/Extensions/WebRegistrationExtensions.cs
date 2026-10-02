using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;
using Snail.MCP.Blender.Adapters.Blender;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Application.Storage;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Web.Access;

namespace Snail.MCP.Blender.Extensions;

/// <summary>The HTTP server's own features: who may reach it, and the volume it serves files from.</summary>
public static class WebRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>The client token checked at start and on every request, signed links for the files, and the session a browser signs in to.</summary>
        public IServiceCollection AddWebAccess()
        {
            services.AddSingleton<IValidateOptions<ServerConfig>, HttpAccessValidation>();

            services
                .AddAuthentication(WebAccess.BearerScheme)
                .AddScheme<AuthenticationSchemeOptions, BearerAuthentication>(WebAccess.BearerScheme, configureOptions: null)
                .AddScheme<AuthenticationSchemeOptions, LinkAuthentication>(WebAccess.LinkScheme, configureOptions: null)
                .AddCookie(WebAccess.CookieScheme);

            services
                .AddOptions<CookieAuthenticationOptions>(WebAccess.CookieScheme)
                .Configure<ServerConfig>(Session);

            services
                .AddAuthorizationBuilder()
                .AddPolicy(WebAccess.ClientPolicy, policy => policy.AddAuthenticationSchemes(WebAccess.BearerScheme).RequireAuthenticatedUser())
                .AddPolicy(WebAccess.FilesWritePolicy, policy => policy
                    .AddAuthenticationSchemes(WebAccess.BearerScheme, WebAccess.LinkScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => Opens(context.User, Scopes.Write)))
                .AddPolicy(WebAccess.FilesDeletePolicy, policy => policy
                    .AddAuthenticationSchemes(WebAccess.BearerScheme, WebAccess.LinkScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => Opens(context.User, Scopes.Delete)))
                .AddPolicy(WebAccess.FilesReadPolicy, policy => policy
                    .AddAuthenticationSchemes(WebAccess.BearerScheme, WebAccess.LinkScheme, WebAccess.CookieScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => Opens(context.User, Scopes.Read)))
                .AddPolicy(WebAccess.BrowserPolicy, policy => policy.AddAuthenticationSchemes(WebAccess.CookieScheme).RequireAuthenticatedUser())
                .AddPolicy(WebAccess.BrowserDeletePolicy, policy => policy
                    .AddAuthenticationSchemes(WebAccess.CookieScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => Opens(context.User, Scopes.Delete)))
                .AddPolicy(WebAccess.BrowserFarmPolicy, policy => policy
                    .AddAuthenticationSchemes(WebAccess.CookieScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => Opens(context.User, Scopes.Farm)));

            services.AddAntiforgery();
            services.AddRazorComponents();
            services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

            return services;
        }

        /// <summary>The volume link and what the server does with the add-on's data directory over it.</summary>
        public IServiceCollection AddVolume()
        {
            services.AddSingleton<BlenderVolume>();
            services.AddSingleton<IBlenderVolume>(provider => provider.GetRequiredService<BlenderVolume>());
            services.AddSingleton<Volume>();

            return services;
        }
    }

    /// <summary>Whether the admitted client's key opens work of this kind; a signed link is the work itself, already asked for and granted by
    /// the tool that issued it, so it needs no scope of its own.</summary>
    /// <remarks>The scopes used to gate the MCP endpoint alone, which left the files open to any admitted client: a key minted to read deleted
    /// another client's renders over /raw, and the same key signed into the pages and deleted them through a form.</remarks>
    private static bool Opens(ClaimsPrincipal user, string scope) =>
        user.Identity?.AuthenticationType == WebAccess.LinkScheme || user.HasClaim(WebAccess.ScopeClaim, scope);

    /// <summary>The session lasts a working day and is sent only to this site; it is marked Secure when the public address is https, where a
    /// reverse proxy ends TLS and the request that reaches this server says http. A page without a session is sent to sign in, a file is
    /// answered 401: curl and a script would otherwise download the login page and take it for the file.</summary>
    /// <remarks>A session belongs to the client whose key opened it, and is checked against that key on every request: revoking a key that
    /// signed a browser in used to leave that browser signed in for the rest of the day, which is not what revoking means.</remarks>
    private static void Session(CookieAuthenticationOptions options, ServerConfig config)
    {
        options.Cookie.Name = "snail_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = (config.Http.PublicUrl ?? config.Http.Url).StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.ReturnUrlParameter = "returnUrl";
        options.Events.OnValidatePrincipal = context =>
        {
            var keys = context.HttpContext.RequestServices.GetRequiredService<ClientKeys>();
            var holder = context.Principal?.Identity?.Name;

            if (holder is null || (keys.Configured?.Name != holder && !keys.All.Any(key => !key.IsRevoked && key.Name.Equals(holder, StringComparison.OrdinalIgnoreCase))))
            {
                context.RejectPrincipal();

                return context.HttpContext.SignOutAsync(WebAccess.CookieScheme);
            }

            return Task.CompletedTask;
        };

        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments(FileLinks.RawPrefix))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                return Task.CompletedTask;
            }

            context.Response.Redirect(new Uri(context.RedirectUri, UriKind.RelativeOrAbsolute) is { IsAbsoluteUri: true } absolute ? absolute.PathAndQuery : context.RedirectUri);

            return Task.CompletedTask;
        };
    }
}
