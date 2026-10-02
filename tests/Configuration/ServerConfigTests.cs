using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Tests.Configuration;

public class ServerConfigTests
{
    [Fact]
    public void Defaults_MatchTheAddOnListener()
    {
        var config = new ServerConfig();

        Assert.Equal("127.0.0.1", config.Bridge.Host);
        Assert.Equal(9876, config.Bridge.Port);
        Assert.Equal(30, config.Bridge.RequestTimeoutSeconds);
    }

    [Fact]
    public void Defaults_UseHomeDataDirectory()
    {
        var config = new ServerConfig();

        Assert.EndsWith(ServerPaths.DataDirectoryName, config.DataDirectory);
    }

    [Fact]
    public void Normalize_BlankHost_FallsBackToLoopback()
    {
        var config = new ServerConfig { Bridge = { Host = " " } };

        ServerConfigExtensions.Normalize(config);

        Assert.Equal("127.0.0.1", config.Bridge.Host);
    }

    /// <summary>A wrong port used to be repaired to 9876 in silence, so a typo in the environment connected to the wrong Blender without a word.</summary>
    [Fact]
    public void Validation_InvalidValues_NamesEveryFault()
    {
        var config = new ServerConfig { Bridge = { Port = 70000, ConnectTimeoutSeconds = 0, RequestTimeoutSeconds = -1 } };

        var result = new ServerConfigValidation().Validate(Options.DefaultName, config);

        Assert.True(result.Failed);
        Assert.Contains("Bridge.Port", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("ConnectTimeoutSeconds", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("RequestTimeoutSeconds", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validation_NegativeMinutes_AreRefused()
    {
        var result = new ServerConfigValidation().Validate(Options.DefaultName, new ServerConfig { SkillIdleMinutes = -1 });

        Assert.True(result.Failed);
        Assert.Contains("SkillIdleMinutes", result.FailureMessage, StringComparison.Ordinal);
    }

    /// <summary>The whole path as the server takes it: sources, binding, normalisation and the generated validator behind one injected config.</summary>
    [Fact]
    public void Configuration_PortOutOfRange_RefusesToHandOutTheConfig()
    {
        using var provider = WithVariables(new Dictionary<string, string?> { ["SNAIL_MCP_BLENDER_BRIDGE__PORT"] = "70000" });

        var failure = Assert.Throws<OptionsValidationException>(provider.GetRequiredService<ServerConfig>);

        Assert.Contains("Bridge.Port", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The setting that decides whether a script is answered, advised or refused; a typo in it must not read as "allow".</summary>
    [Fact]
    public void Configuration_PythonAccessFromTheEnvironment_Binds_AndANonsenseValueIsRefused()
    {
        using var provider = WithVariables(new Dictionary<string, string?> { ["SNAIL_MCP_BLENDER_PYTHON"] = "fallback" });

        Assert.Equal(PythonAccess.Fallback, provider.GetRequiredService<ServerConfig>().Python);
        Assert.Equal(PythonAccess.Allow, new ServerConfig().Python);

        using var broken = WithVariables(new Dictionary<string, string?> { ["SNAIL_MCP_BLENDER_PYTHON"] = "sometimes" });

        Assert.Throws<InvalidOperationException>(broken.GetRequiredService<ServerConfig>);
    }

    /// <summary>Everything a remote Blender needs comes from the environment of the client's MCP entry, and paths there may start with a tilde nobody expands.</summary>
    [Fact]
    public void Configuration_TunnelFromTheEnvironment_Binds_AndItsPathsLoseTheirTilde()
    {
        using var provider = WithVariables(new Dictionary<string, string?>
        {
            ["SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__HOST"] = "artist@203.0.113.42",
            ["SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__KEY"] = "~/.ssh/id_ed25519",
            ["SNAIL_MCP_BLENDER_BRIDGE__TUNNEL__REMOTE_PORT"] = "9876",
            ["SNAIL_MCP_BLENDER_BRIDGE__TOKEN_FILE"] = "~/.snail-mcp-blender/remote/win-main.token",
            ["SNAIL_MCP_BLENDER_BRIDGE__PORT"] = "9877",
        });

        var link = provider.GetRequiredService<BlenderLinkOptions>();

        Assert.True(link.Tunnel.IsEnabled);
        Assert.Equal(Path.Combine(ServerPaths.Home, ".ssh", "id_ed25519"), link.Tunnel.Key);
        Assert.Equal(Path.Combine(ServerPaths.Home, ".snail-mcp-blender", "remote", "win-main.token"), link.TokenFile);
        Assert.Equal(9876, link.Tunnel.RemotePort);
        Assert.Equal(9877, link.Port);
    }

    /// <summary>The client starts the server in the folder it works in; that folder is the project unless the environment names another.</summary>
    [Fact]
    public void Normalize_ProjectDirectory_IsTheWorkingDirectory_UnlessNamed_AndLosesItsTilde()
    {
        var unnamed = new ServerConfig();
        var named = new ServerConfig { ProjectDirectory = "~/Projects/lobby" };

        ServerConfigExtensions.Normalize(unnamed);
        ServerConfigExtensions.Normalize(named);

        Assert.Equal(Path.GetFullPath(Environment.CurrentDirectory), unnamed.ProjectDirectory);
        Assert.Equal(Path.Combine(ServerPaths.Home, "Projects", "lobby"), named.ProjectDirectory);
    }

    /// <summary>A container is configured through its environment only; the secret arrives as a file with a path that may carry a tilde, and a server
    /// reached over HTTP sees none of the client's folders, so it has no project however the working directory looks.</summary>
    [Fact]
    public void Configuration_HttpFromTheEnvironment_Binds_AndTheServerHasNoProject()
    {
        using var provider = WithVariables(new Dictionary<string, string?>
        {
            ["SNAIL_MCP_BLENDER_TRANSPORT"] = "Http",
            ["SNAIL_MCP_BLENDER_HTTP__URL"] = "http://0.0.0.0:8080",
            ["SNAIL_MCP_BLENDER_HTTP__TOKEN_FILE"] = "~/secrets/client.token",
            ["SNAIL_MCP_BLENDER_HTTP__PUBLIC_URL"] = "https://blender.example.com",
            ["SNAIL_MCP_BLENDER_HTTP__LINK_MINUTES"] = "30",
        });

        var config = provider.GetRequiredService<IOptions<ServerConfig>>();
        var http = new ServerConfigValidation().Validate(Options.DefaultName, config.Value);

        Assert.False(http.Failed);
        Assert.Equal(Transport.Http, config.Value.Transport);
        Assert.Equal("http://0.0.0.0:8080", config.Value.Http.Url);
        Assert.Equal(Path.Combine(ServerPaths.Home, "secrets", "client.token"), config.Value.Http.TokenFile);
        Assert.Equal("https://blender.example.com", config.Value.Http.PublicUrl);
        Assert.Equal(30, config.Value.Http.LinkMinutes);
        Assert.Null(config.Value.ProjectDirectory);
    }

    /// <summary>The host is chosen before one exists, so the transport is read from the same sources the host binds later.</summary>
    [Fact]
    public void Peek_ReadsTheTransport_BeforeAHostIsBuilt()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Snail:Transport"] = "http", ["Http:Url"] = "http://0.0.0.0:9000" }).Build();

        var peeked = ServerConfigExtensions.Peek(configuration);

        Assert.Equal(Transport.Http, peeked.Transport);
        Assert.Equal("http://0.0.0.0:9000", peeked.Http.Url);
    }

    [Fact]
    public void Validation_LinkMinutesOutOfRange_IsRefused()
    {
        var result = new ServerConfigValidation().Validate(Options.DefaultName, new ServerConfig { Http = { LinkMinutes = 0 } });

        Assert.True(result.Failed);
        Assert.Contains("LinkMinutes", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validation_TunnelRemotePortOutOfRange_IsRefused()
    {
        var result = new ServerConfigValidation().Validate(Options.DefaultName, new ServerConfig { Bridge = { Tunnel = { RemotePort = 0 } } });

        Assert.True(result.Failed);
        Assert.Contains("RemotePort", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Configuration_DataDirectoryFromTheEnvironment_ReachesTheLinkTokenFile()
    {
        using var provider = WithVariables(new Dictionary<string, string?> { ["SNAIL_MCP_BLENDER_DATA_DIRECTORY"] = "~/blender-from-the-environment" });

        var link = provider.GetRequiredService<BlenderLinkOptions>();

        Assert.Equal(Path.Combine(ServerPaths.Home, "blender-from-the-environment", "state", "token"), link.TokenFile);
        Assert.Same(provider.GetRequiredService<ServerConfig>().Bridge, link);
    }

    /// <summary>A host configured from these variables alone, without touching the environment of the test process, which the tests running
    /// beside this one share.</summary>
    private static ServiceProvider WithVariables(Dictionary<string, string?> variables)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.AddServerConfiguration(ServerConfigExtensions.Settings(Named(variables)));

        return builder.Services.BuildServiceProvider();
    }

    private static Hashtable Named(Dictionary<string, string?> variables)
    {
        var named = new Hashtable { [EnvironmentSettings.ConfigPathVariable] = Path.Combine(Path.GetTempPath(), $"snail-no-config-{Guid.NewGuid():N}.json") };

        foreach (var (key, value) in variables)
        {
            named[key] = value;
        }

        return named;
    }

    [Fact]
    public void Normalize_TokenFile_LandsUnderTheDataDirectoryState()
    {
        var config = new ServerConfig { DataDirectory = "~/blender-data" };

        ServerConfigExtensions.Normalize(config);

        Assert.Equal(Path.Combine(ServerPaths.Home, "blender-data", "state", "token"), config.Bridge.TokenFile);
    }

    [Fact]
    public void Normalize_TildeInDataDirectory_IsExpanded()
    {
        var config = new ServerConfig { DataDirectory = "~/custom-blender-data" };

        ServerConfigExtensions.Normalize(config);

        Assert.Equal(Path.Combine(ServerPaths.Home, "custom-blender-data"), config.DataDirectory);
    }

    [Theory]
    [InlineData("DATA_DIRECTORY", "DataDirectory")]
    [InlineData("IDLE_TIMEOUT_MINUTES", "IdleTimeoutMinutes")]
    [InlineData("BRIDGE__PORT", "Bridge:Port")]
    [InlineData("BRIDGE__REQUEST_TIMEOUT_SECONDS", "Bridge:RequestTimeoutSeconds")]
    [InlineData("Bridge__ConnectTimeoutSeconds", "Bridge:ConnectTimeoutSeconds")]
    [InlineData("BRIDGE__TOKEN", "Bridge:Token")]
    public void EnvironmentName_BecomesConfigurationKey(string variable, string expected) =>
        Assert.Equal(expected, EnvironmentSettings.ToConfigurationKey(variable), ignoreCase: true);

    [Fact]
    public void EnvironmentValues_BindIntoConfig_AndBlankOnesAreIgnored()
    {
        var variables = new System.Collections.Hashtable
        {
            ["SNAIL_MCP_BLENDER_BRIDGE__PORT"] = " 9900 ",
            ["SNAIL_MCP_BLENDER_BRIDGE__CONNECT_TIMEOUT_SECONDS"] = "   ",
            ["PATH"] = "/usr/bin",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(EnvironmentSettings.Read(variables))
            .Build();
        var config = new ServerConfig();
        configuration.Bind(config);
        ServerConfigExtensions.Normalize(config);

        Assert.Equal(9900, config.Bridge.Port);
        Assert.Equal(5, config.Bridge.ConnectTimeoutSeconds);
        Assert.Equal(0, config.SkillIdleMinutes);
        Assert.Null(configuration["PATH"]);
    }

    /// <summary>A named config file is the whole chain: naming one that does not exist reads no file, rather than falling back to whatever
    /// happens to sit in the home folder of whoever runs the server.</summary>
    [Fact]
    public void ConfigCandidates_ANamedFile_IsTheOnlyOne_AndTheDefaultChainIsTheUsualPlaces()
    {
        var named = ServerPaths.ConfigCandidates("/tmp/snail-mcp-blender-explicit.json").ToList();
        var usual = ServerPaths.ConfigCandidates().ToList();

        Assert.Equal(["/tmp/snail-mcp-blender-explicit.json"], named);
        Assert.Contains(usual, candidate => candidate.EndsWith(".snail-mcp-blender.json", StringComparison.Ordinal));
    }

    /// <summary>The server reads its own variables and its own file, and nothing else: bound to the host's configuration as well, an unprefixed
    /// variable of the machine — TRANSPORT — turned a stdio server's transport into http while it still spoke over stdio.</summary>
    [Fact]
    public void Configuration_AnUnprefixedVariable_IsNotRead()
    {
        var read = RunWithEnvironment(
            new Dictionary<string, string?> { ["TRANSPORT"] = "Http", ["PYTHON"] = "Off" },
            () =>
            {
                var builder = Host.CreateEmptyApplicationBuilder(null);
                builder.AddServerConfiguration();

                return builder.Services.BuildServiceProvider().GetRequiredService<ServerConfig>();
            });

        Assert.Equal(Transport.Stdio, read.Transport);
        Assert.Equal(PythonAccess.Allow, read.Python);
    }

    private static T RunWithEnvironment<T>(Dictionary<string, string?> values, Func<T> action)
    {
        var previous = values.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);

        try
        {
            foreach (var (key, value) in values)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            return action();
        }
        finally
        {
            foreach (var (key, value) in previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    [Fact]
    public void Normalize_TopLevelAgent_ReachesTheLink()
    {
        var config = new ServerConfig { Agent = "lighting" };

        ServerConfigExtensions.Normalize(config);

        Assert.Equal("lighting", config.Bridge.Agent);
    }
}
