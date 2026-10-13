using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Snail.Sdk.Mcp.Hosting;

namespace Snail.MCP.Blender.Configuration;

/// <summary>Builds <see cref="ServerConfig"/>: defaults → settings file → environment variables, validated before the server answers anything.</summary>
public static class ServerConfigExtensions
{
    /// <summary>Adds the configuration sources and binds them through the options pipeline; what a consumer injects is the bound <see cref="ServerConfig"/> itself.</summary>
    /// <remarks>The ranges are attributes on the settings and <see cref="ServerConfigValidation"/> is generated from them, so a setting arrives with
    /// its rule or with none at all. ValidateOnStart runs that validator while the host starts: a typo in the environment stops the process with a
    /// message naming the setting instead of being repaired in silence.</remarks>
    public static IHostApplicationBuilder AddServerConfiguration(this IHostApplicationBuilder builder) =>
        builder.AddServerConfiguration(Settings());

    /// <summary>The same, from settings read already: those the transport was chosen from, or a test's instead of this machine's.</summary>
    internal static IHostApplicationBuilder AddServerConfiguration(this IHostApplicationBuilder builder, IConfiguration settings)
    {
        builder.Services
            .AddOptions<ServerConfig>()
            .Bind(settings.GetSection(ServerConfig.SectionName))
            .Bind(settings)
            .PostConfigure(Normalize)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<ServerConfig>, ServerConfigValidation>();
        builder.Services.AddSingleton(provider => provider.GetRequiredService<IOptions<ServerConfig>>().Value);
        builder.Services.AddSingleton(provider => provider.GetRequiredService<ServerConfig>().Bridge);

        return builder;
    }

    /// <summary>What this server reads, and nothing else: its settings file, found by the chain every Snail server shares, and the
    /// SNAIL_MCP_BLENDER_ variables, which win over the file.</summary>
    /// <remarks>The host's own configuration is left out on purpose. Bound as well, it brought in every unprefixed variable, an appsettings.json
    /// in whatever folder the client started the server in, and the command line: TRANSPORT=Http in an unrelated environment turned a stdio
    /// server's transport into http while the process still spoke over stdio, because the transport is chosen from these sources alone.</remarks>
    public static IConfiguration Settings() => ServerSettings.Read(ServerPaths.ShortName);

    /// <summary>The same from a settings file named outright and the variables a caller hands over; a named file that does not exist reads none.</summary>
    public static IConfiguration Settings(string? file, IDictionary variables) =>
        new ConfigurationBuilder().AddServerSettings(ServerPaths.ShortName, file, variables).Build();

    /// <summary>The settings as a host would bind them, read before one is built: stdio and HTTP need different hosts.</summary>
    public static ServerConfig Peek(IConfiguration? configuration = null)
    {
        configuration ??= Settings();

        var config = new ServerConfig();
        configuration.GetSection(ServerConfig.SectionName).Bind(config);
        configuration.Bind(config);

        return config;
    }

    /// <summary>Expands <c>~</c>, and fills the link's agent and token file from the top-level settings.</summary>
    internal static void Normalize(ServerConfig config)
    {
        config.DataDirectory = ExpandHome(config.DataDirectory);

        if (string.IsNullOrWhiteSpace(config.Bridge.Agent))
        {
            config.Bridge.Agent = config.Agent;
        }

        if (string.IsNullOrWhiteSpace(config.Bridge.TokenFile))
        {
            config.Bridge.TokenFile = config.TokenFile;
        }

        config.ProjectDirectory = config.Transport is Transport.Http ? null : Path.GetFullPath(ExpandUser(config.ProjectDirectory) ?? Environment.CurrentDirectory);
        config.Bridge.TokenFile = ExpandUser(config.Bridge.TokenFile);
        config.Http.TokenFile = ExpandUser(config.Http.TokenFile);
        config.Bridge.Tunnel.Key = ExpandUser(config.Bridge.Tunnel.Key);

        if (string.IsNullOrWhiteSpace(config.Bridge.Host))
        {
            config.Bridge.Host = "127.0.0.1";
        }
    }

    /// <summary>Expands a leading <c>~</c> in a path that may be absent; unlike the data directory, an absent one stays absent.</summary>
    private static string? ExpandUser(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null
        : path == "~" ? ServerPaths.Home
        : path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith(@"~\", StringComparison.Ordinal) ? Path.Combine(ServerPaths.Home, path[2..])
        : path;

    private static string ExpandHome(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ServerPaths.DefaultDataDirectory;
        }

        path = path.Trim();

        if (path == "~")
        {
            return ServerPaths.Home;
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith(@"~\", StringComparison.Ordinal))
        {
            return Path.Combine(ServerPaths.Home, path[2..]);
        }

        return Path.GetFullPath(path);
    }
}
