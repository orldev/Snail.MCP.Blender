using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Application.Access;

/// <summary>The keys this server admits clients by: one per client, each with a name and what it may do, kept as digests on disk.</summary>
/// <remarks>One shared token made every client the same client: whoever held it could call itself anything, so the name on a command — the
/// one the add-on's leases and journal record — was a claim nobody checked. A key is minted here, shown once, and stored as a digest, so the
/// file is of no use to whoever reads it; revoking a key turns its holder away without touching anyone else.
/// <para>The digest is a plain SHA-256, not a password hash, because the secret is 32 random bytes this server drew itself: there is no
/// smaller space to search than the whole one, and stretching would only slow down every request.</para>
/// <para>The configured token stays a key of its own, with every scope, so a server that was set up with one keeps working and an operator
/// can move clients over one at a time.</para></remarks>
public sealed class ClientKeys
{
    /// <summary>Bytes of secret a minted key carries, as base64url.</summary>
    public const int SecretBytes = 32;

    private const string Note =
        "Clients this server admits, one key each. The secret is not here: only its SHA-256, so a copy of this file opens nothing. " +
        "Mint and revoke with the 'clients' command rather than by hand.";

    private static readonly JsonSerializerOptions Readable = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly string _file;
    private readonly string? _configured;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private List<ClientKey> _keys;
    private DateTime _read;

    public ClientKeys(ServerConfig config, TimeProvider time)
    {
        _time = time;
        _file = Path.Combine(config.DataDirectory, "state", "clients.json");
        _configured = ClientToken.Read(config.Http);
        _keys = Read(_file);
        _read = Written(_file);
        Configured = _configured is null
            ? null
            : new ClientKey(Named(config.Bridge.Agent) ?? "the configured client", Digest(_configured), Scopes.All, time.GetUtcNow());
    }

    /// <summary>The key of the token the server was configured with, which holds every scope; null when it was given none.</summary>
    public ClientKey? Configured { get; }

    /// <summary>Every minted key, revoked ones among them, as an operator reads the list.</summary>
    public IReadOnlyList<ClientKey> All
    {
        get
        {
            lock (_gate)
            {
                Reread();

                return [.. _keys];
            }
        }
    }

    /// <summary>The key a presented secret belongs to, or null when it belongs to none or to one that was revoked.</summary>
    /// <remarks>Every candidate is compared in fixed time and all of them are compared, so neither which key matched nor how far a guess got
    /// shows in how long the answer takes.</remarks>
    public ClientKey? Holder(string? presented)
    {
        if (string.IsNullOrWhiteSpace(presented))
        {
            return null;
        }

        var offered = SHA256.HashData(Encoding.UTF8.GetBytes(presented.Trim()));
        var found = null as ClientKey;

        foreach (var key in Candidates())
        {
            if (CryptographicOperations.FixedTimeEquals(offered, Convert.FromBase64String(key.Digest)) && !key.IsRevoked)
            {
                found = key;
            }
        }

        return found;
    }

    /// <summary>Mints a key for a client and returns the secret, which is the only time it exists outside the holder's hands.</summary>
    public (ClientKey Key, string Secret) Mint(string name, IReadOnlyList<string>? scopes = null)
    {
        var chosen = Named(name) ?? throw new ArgumentException("a client needs a name", nameof(name));
        var unknown = (scopes ?? []).Where(scope => !Scopes.IsKnown(scope)).ToList();

        if (unknown.Count > 0)
        {
            throw new ArgumentException($"no such scope: {string.Join(", ", unknown)}; the scopes are {string.Join(", ", Scopes.All)}", nameof(scopes));
        }

        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var key = new ClientKey(chosen, Digest(secret), scopes is { Count: > 0 } ? [.. scopes] : Scopes.Ordinary, _time.GetUtcNow());

        lock (_gate)
        {
            Reread();
            _keys = [.. _keys.Where(held => !held.Name.Equals(chosen, StringComparison.OrdinalIgnoreCase) || held.IsRevoked), key];
            Write();
        }

        return (key, secret);
    }

    /// <summary>Stops admitting the key of a client; false when there was none to stop.</summary>
    public bool Revoke(string name)
    {
        lock (_gate)
        {
            Reread();

            var held = _keys.FindIndex(key => key.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) && !key.IsRevoked);

            if (held < 0)
            {
                return false;
            }

            _keys[held] = _keys[held] with { Revoked = _time.GetUtcNow() };
            Write();

            return true;
        }
    }

    private IEnumerable<ClientKey> Candidates()
    {
        lock (_gate)
        {
            Reread();

            return Configured is null ? [.. _keys] : [.. _keys, Configured];
        }
    }

    /// <summary>Reads the file again when it has changed since this server last looked.</summary>
    /// <remarks>A key is revoked by the command line, in a process of its own, while the server is serving the client it belongs to. Held in
    /// memory from start-up, the list would admit that client until the container was restarted, which is the opposite of what revoking is
    /// for. The timestamp is the whole check: the file is written by one command at a time and only ever whole.</remarks>
    private void Reread()
    {
        if (Written(_file) is var written && written == _read)
        {
            return;
        }

        _keys = Read(_file);
        _read = written;
    }

    private static DateTime Written(string file) => File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;

    private static string Digest(string secret) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string? Named(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static List<ClientKey> Read(string file)
    {
        if (!File.Exists(file))
        {
            return [];
        }

        try
        {
            return
            [
                .. (JsonNode.Parse(File.ReadAllText(file))?["clients"] as JsonArray ?? [])
                    .OfType<JsonObject>()
                    .Select(client => new ClientKey(
                        client["name"]!.GetValue<string>(),
                        client["digest"]!.GetValue<string>(),
                        [.. (client["scopes"] as JsonArray ?? []).Select(scope => scope!.GetValue<string>())],
                        client["issued"]!.GetValue<DateTimeOffset>(),
                        client["revoked"]?.GetValue<DateTimeOffset>())),
            ];
        }
        catch (Exception failure) when (failure is JsonException or FormatException or InvalidOperationException or NullReferenceException)
        {
            throw new InvalidOperationException($"'{file}' is not a list of clients this server can read: {failure.Message}", failure);
        }
    }

    /// <summary>Written whole under a temporary name and moved over the old one, so a reader never finds half a list.</summary>
    private void Write()
    {
        var document = new JsonObject
        {
            ["note"] = Note,
            ["clients"] = new JsonArray([.. _keys.Select(key => new JsonObject
            {
                ["name"] = key.Name,
                ["digest"] = key.Digest,
                ["scopes"] = new JsonArray([.. key.Scopes.Select(scope => (JsonNode)JsonValue.Create(scope)!)]),
                ["issued"] = key.Issued,
                ["revoked"] = key.Revoked is { } revoked ? JsonValue.Create(revoked) : null,
            })]),
        };
        var directory = Path.GetDirectoryName(_file)!;
        var pending = Path.Combine(directory, $"clients.{Environment.ProcessId}.tmp");

        Directory.CreateDirectory(directory);
        File.WriteAllText(pending, $"{document.ToJsonString(Readable)}\n");
        File.Move(pending, _file, overwrite: true);
        _read = Written(_file);
    }
}
