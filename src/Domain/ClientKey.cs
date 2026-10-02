namespace Snail.MCP.Blender.Domain;

/// <summary>What a client of this server is allowed to do, named on the key it presents.</summary>
/// <remarks>Coarse on purpose: a scope answers "may this client do this kind of thing at all", which an operator can decide once for a key,
/// and the lease guard and the Python setting still decide the rest per call. Finer scopes would be a second policy to keep in step with the
/// command schema, and the schema is already the one place that says what a command does.</remarks>
public static class Scopes
{
    /// <summary>Reading the scene, the files and the diagnostics.</summary>
    public const string Read = "read";

    /// <summary>Changing the scene and writing files.</summary>
    public const string Write = "write";

    /// <summary>Losing work: deleting objects and files, clearing another agent's lease, undoing.</summary>
    public const string Delete = "delete";

    /// <summary>Running Python and Blender's operators by name, which reach past every schema.</summary>
    public const string Python = "python";

    /// <summary>Queueing background renders and batches, which spend the machine long after the call returns.</summary>
    public const string Farm = "farm";

    public static IReadOnlyList<string> All { get; } = [Read, Write, Delete, Python, Farm];

    /// <summary>What a key gets when nobody said otherwise: everything but Python, which is asked for by name.</summary>
    public static IReadOnlyList<string> Ordinary { get; } = [Read, Write, Delete, Farm];

    public static bool IsKnown(string scope) => All.Contains(scope, StringComparer.OrdinalIgnoreCase);
}

/// <summary>A client this server admits: the name its work is recorded under, what it may do, and the digest of the secret it proves itself with.</summary>
/// <param name="Name">The agent name its commands are signed with, which the add-on's leases and journal record.</param>
/// <param name="Digest">SHA-256 of the secret, base64. The secret itself is never stored: it is shown once, when the key is minted.</param>
/// <param name="Scopes">The kinds of work this key opens; see <see cref="Snail.MCP.Blender.Domain.Scopes"/>.</param>
/// <param name="Issued">When the key was minted, for an operator reading the list.</param>
/// <param name="Revoked">When it stopped being accepted, or null while it still is.</param>
public sealed record ClientKey(string Name, string Digest, IReadOnlyList<string> Scopes, DateTimeOffset Issued, DateTimeOffset? Revoked = null)
{
    public bool IsRevoked => Revoked is not null;

    public bool May(string scope) => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
}
