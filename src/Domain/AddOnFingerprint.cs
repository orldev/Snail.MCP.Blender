namespace Snail.MCP.Blender.Domain;

/// <summary>What an add-on build is: the version its manifest claims and a digest over its Python.</summary>
/// <remarks>The version alone cannot tell a stale install from the current one — it stays put across bug fixes — so the digest is what
/// the two sides compare. Both compute it the same way: the top-level <c>.py</c> files in name order, each hashed as its name in UTF-8
/// followed by its bytes, SHA-256, the first twelve hex characters.</remarks>
public sealed record AddOnFingerprint(string Version, string Digest);
