namespace Snail.MCP.Blender.Application.Sessions;

/// <summary>One client of this server: the name its commands are signed with, and the kinds of work its key opens.</summary>
/// <remarks>The name is what the add-on's leases and journal call the agent: two clients working in one Blender have to be told apart there,
/// or one takes a lease the other reads as its own and the journal records a studio of one. Which skills a client has loaded is Snail.Sdk's
/// (<c>WithSkills</c>), kept by this name over HTTP and by the MCP session over stdio.</remarks>
public sealed class ClientSession(string name, string? agent, IReadOnlyList<string>? scopes = null)
{
    /// <summary>What the client is called here, for the log and the health report.</summary>
    public string Name { get; } = name;

    /// <summary>The agent name riding on this client's commands, or none when neither the client nor the configuration gave one.</summary>
    public string? Agent { get; private set; } = agent;

    /// <summary>The kinds of work this client's key opens; a client that presented no key — over stdio, or the server itself — may do anything.</summary>
    public IReadOnlyList<string> Scopes { get; } = scopes ?? Domain.Scopes.All;

    /// <summary>Whether this client may do work of this kind at all, which is asked before the call reaches Blender.</summary>
    public bool May(string scope) => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);

    /// <summary>A client that named no agent of its own is named after itself once it has said who it is.</summary>
    /// <remarks>The name arrives with <c>initialize</c>, which is after the process started, so this is settled on the first call rather
    /// than at the start. A client that gave a name in its request keeps it: that is the one a studio chose deliberately.</remarks>
    internal void NameItself(string? clientName)
    {
        Agent ??= string.IsNullOrWhiteSpace(clientName) ? null : clientName.Trim();
    }
}

/// <summary>Which client the request in hand belongs to.</summary>
/// <remarks>A stdio server serves one client, the process's own. Over HTTP every request presents its key, and the client is read from it
/// for the length of the request — the call, and the tool list the gate narrows — so the scopes in force are the ones of the key presented
/// now: an operator who revokes a key and mints a narrower one for the same client means the narrower one from the next request on.
/// Everything downstream — the gate, the agent a command is signed with — asks for the client in hand rather than for the process's one.
/// <para>The clients of a server whose configured agent name, for a client that names none of its own, is <paramref name="agent"/>.</para></remarks>
public sealed class ClientSessions(string? agent)
{
    private readonly AsyncLocal<ClientSession?> _serving = new();

    /// <summary>The client of a server that serves no request of a named client: the stdio process, and any caller outside a request.</summary>
    public ClientSession Default { get; } = new ClientSession("this server", Called(agent));

    /// <summary>The client of the request in hand, or the process's own when there is no request.</summary>
    public ClientSession Current => _serving.Value ?? Default;

    /// <summary>Serves the client a request comes from until the returned handle is disposed: what the gate asks about and what its commands
    /// are signed with.</summary>
    /// <param name="clientName">The name the client gave in <c>initialize</c>, which names the process's own client when it named no agent.</param>
    /// <param name="name">The name on the key the request presented, or null for a request that presented none.</param>
    /// <param name="scopes">The kinds of work that key opens, or null for every kind.</param>
    public IDisposable Serve(string? clientName, string? name = null, IReadOnlyList<string>? scopes = null)
    {
        if (Called(name) is not { } named)
        {
            Default.NameItself(clientName);

            return Serve(Default);
        }

        return Serve(new ClientSession(named, named, scopes));
    }

    /// <summary>Serves this client until the returned handle is disposed.</summary>
    public IDisposable Serve(ClientSession session)
    {
        var before = _serving.Value;

        _serving.Value = session;

        return new Served(this, before);
    }

    private static string? Called(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private sealed class Served(ClientSessions sessions, ClientSession? before) : IDisposable
    {
        public void Dispose() => sessions._serving.Value = before;
    }
}
