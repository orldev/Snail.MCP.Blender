using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Skills;

namespace Snail.MCP.Blender.Application.Sessions;

/// <summary>One client of this server: the name its commands are signed with, and the skills it alone has loaded.</summary>
/// <remarks>A session owns its tool collection, so loading a skill changes the tool list of the client that asked and of nobody else.
/// The name is what the add-on's leases and journal call the agent: two clients working in one Blender have to be told apart there, or
/// one takes a lease the other reads as its own and the journal records a studio of one.</remarks>
public sealed class ClientSession(string name, string? agent, SkillCatalog skills, IReadOnlyList<string>? scopes = null)
{
    /// <summary>What the client is called here, for the log and the health report.</summary>
    public string Name { get; } = name;

    /// <summary>The agent name riding on this client's commands, or none when neither the client nor the configuration gave one.</summary>
    public string? Agent { get; private set; } = agent;

    public SkillCatalog Skills { get; } = skills;

    /// <summary>The kinds of work this client's key opens; a client that presented no key — over stdio, or the server itself — may do anything.</summary>
    public IReadOnlyList<string> Scopes { get; private set; } = scopes ?? Domain.Scopes.All;

    /// <summary>Whether this client may do work of this kind at all, which is asked before the call reaches Blender.</summary>
    public bool May(string scope) => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);

    /// <summary>A client that named no agent of its own is named after itself once it has said who it is.</summary>
    /// <remarks>The name arrives with <c>initialize</c>, which is after the session is opened, so this is settled on the first call rather
    /// than at the start. A client that gave a name in its request keeps it: that is the one a studio chose deliberately.</remarks>
    internal void NameItself(string? clientName)
    {
        Agent ??= string.IsNullOrWhiteSpace(clientName) ? null : clientName.Trim();
    }

    /// <summary>Takes the scopes of the key just presented, which may be narrower than the ones this session was opened with.</summary>
    /// <remarks>An operator who revokes a key and mints a narrower one for the same client means the narrower one from now on. Kept from the
    /// first connection, the old scopes would stay in force until the container was restarted — the opposite of what re-minting is for.</remarks>
    internal void Opens(IReadOnlyList<string>? presented)
    {
        if (presented is { Count: > 0 })
        {
            Scopes = presented;
        }
    }
}

/// <summary>Every client this server is serving, one session each, and which of them the call in hand belongs to.</summary>
/// <remarks>A stdio server serves one client and hands it the process's own session. Over HTTP each MCP session opens one of its own, and
/// the call being served is found through the session's options, which the SDK hands back with every request. Everything downstream — the
/// skills a tool sees, the agent a command is signed with — asks for the session in hand rather than for the process's one.</remarks>
public sealed class ClientSessions
{
    /// <summary>Clients that may be told apart by name, which is the only way to recognise one that holds no session.</summary>
    public const int MostNamedClients = 64;

    private readonly ConditionalWeakTable<McpServerPrimitiveCollection<McpServerTool>, ClientSession> _sessions = [];
    private readonly ConcurrentDictionary<string, ClientSession> _named = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncLocal<ClientSession?> _serving = new();
    private readonly IReadOnlyList<Skill> _skills;
    private readonly IServiceProvider _services;
    private readonly TimeProvider _time;

    /// <summary>The sessions of a server whose own tool collection is <paramref name="alwaysOn"/> and whose configured agent name, for a
    /// client that names none of its own, is <paramref name="agent"/>.</summary>
    public ClientSessions(IReadOnlyList<Skill> skills, McpServerPrimitiveCollection<McpServerTool> alwaysOn, string? agent, IServiceProvider services, TimeProvider time)
    {
        _skills = skills;
        _services = services;
        _time = time;
        Default = new ClientSession("this server", Called(agent), new SkillCatalog(skills, alwaysOn, services, time));
    }

    /// <summary>The session of a server that opened none: the stdio process, and any caller outside a request.</summary>
    public ClientSession Default { get; }

    /// <summary>Every session open now, the process's own among them.</summary>
    public IReadOnlyList<ClientSession> All => [Default, .. _sessions.Select(entry => entry.Value)];

    /// <summary>The sessions of clients that named themselves, which is how one without an MCP session is recognised again.</summary>
    public IReadOnlyDictionary<string, ClientSession> Named => _named;

    /// <summary>The session of the call in hand, or the process's own when there is no call.</summary>
    public ClientSession Current => _serving.Value ?? Default;

    /// <summary>Opens a session for a client of its own, with a tool collection holding the always-on tools and nothing else.</summary>
    /// <remarks>The collection is copied rather than shared: the one the process holds is what a session starts from, and what a session
    /// loads into its own is its own.
    /// <para>A client of the sessionless protocol revision is handed fresh options on every request, so skills it loaded would be gone by the
    /// next one. It is recognised by the name it gives instead and handed back the session of that name. A client that gives no name cannot be
    /// told from any other, so it is served the tool list of the server itself — which is the one every client used to share.</para>
    /// <para>Past the number of names kept, a client still gets a session of its own scopes and shares the server's tool list. It used to be
    /// handed the server's whole session, which holds every scope: a key minted to read alone became a key that could do anything, because
    /// sixty-four other names had got there first.</para></remarks>
    public ClientSession Open(McpServerOptions session, string? agent, IReadOnlyList<string>? scopes = null)
    {
        if (Called(agent) is not { } name)
        {
            session.ToolCollection = Default.Skills.Tools;

            return Default;
        }

        var opened = _named.Count >= MostNamedClients && !_named.ContainsKey(name)
            ? new ClientSession(name, name, Default.Skills, scopes)
            : _named.GetOrAdd(name, called => Fresh(called, scopes));

        opened.Opens(scopes);
        session.ToolCollection = opened.Skills.Tools;

        return opened;
    }

    private ClientSession Fresh(string name, IReadOnlyList<string>? scopes)
    {
        var alwaysOn = new McpServerPrimitiveCollection<McpServerTool>();

        foreach (var tool in Default.Skills.Tools)
        {
            alwaysOn.TryAdd(tool);
        }

        var opened = new ClientSession(name, name, new SkillCatalog(_skills, alwaysOn, _services, _time), scopes);

        _sessions.AddOrUpdate(alwaysOn, opened);

        return opened;
    }

    /// <summary>Makes the session of this server the one the call in hand belongs to, until the returned handle is disposed.</summary>
    public IDisposable Serve(McpServer? server)
    {
        var session = For(server);

        session.NameItself(server?.ClientInfo?.Name);

        return Serve(session);
    }

    /// <summary>Serves this client until the returned handle is disposed: what the tools see and what its commands are signed with.</summary>
    public IDisposable Serve(ClientSession session)
    {
        _serving.Value = session;

        return new Served(this);
    }

    /// <summary>The session a server belongs to; one this server never opened — a test, or a transport of its own — is the process's.</summary>
    /// <remarks>A session is found by the tool collection it was opened with rather than by the options that carried it: the SDK hands a
    /// request options of its own, copied from the session's, and only what they point at is the same object on both sides.</remarks>
    public ClientSession For(McpServer? server) =>
        server?.ServerOptions.ToolCollection is { } tools && _sessions.TryGetValue(tools, out var session) ? session : Default;

    private static string? Called(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private sealed class Served(ClientSessions sessions) : IDisposable
    {
        public void Dispose() => sessions._serving.Value = null;
    }
}
