using System.Security.Cryptography;
using System.Text;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Application.Access;

/// <summary>The secret a client of the HTTP server proves itself with: as a bearer, at the page login, and as the root of the key file links are signed with.</summary>
/// <remarks>Compared as digests in fixed time, so neither the length of a guess nor a matching prefix shows in how long the answer takes.
/// The link key is derived from the token rather than drawn at random, so a link outlives a restart of the container and dies with the token.
/// A server with no token accepts nobody and signs with a key of its own; the HTTP server refuses to start that way, the stdio server never checks.</remarks>
public sealed class ClientToken
{
    private static readonly byte[] LinkKeyPurpose = "snail file links"u8.ToArray();

    private readonly byte[]? _digest;

    public ClientToken(ServerConfig config)
    {
        var secret = Read(config.Http) is { } token ? Encoding.UTF8.GetBytes(token) : null;

        _digest = secret is null ? null : SHA256.HashData(secret);
        LinkKey = secret is null ? RandomNumberGenerator.GetBytes(32) : HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 32, info: LinkKeyPurpose);
    }

    /// <summary>The key links to files are signed with.</summary>
    public byte[] LinkKey { get; }

    public bool Accepts(string? presented) =>
        _digest is not null && !string.IsNullOrEmpty(presented) && CryptographicOperations.FixedTimeEquals(_digest, SHA256.HashData(Encoding.UTF8.GetBytes(presented)));

    /// <summary>The configured token, else the content of the token file; null when neither holds one.</summary>
    internal static string? Read(HttpOptions http)
    {
        if (!string.IsNullOrWhiteSpace(http.Token))
        {
            return http.Token.Trim();
        }

        if (string.IsNullOrWhiteSpace(http.TokenFile) || !File.Exists(http.TokenFile))
        {
            return null;
        }

        var stored = File.ReadAllText(http.TokenFile).Trim();

        return stored.Length > 0 ? stored : null;
    }
}
