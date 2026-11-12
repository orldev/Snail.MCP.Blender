namespace Snail.MCP.Blender.Domain;

/// <summary>A built add-on zip: where it is and which add-on version it carries.</summary>
internal sealed record AddOnPackage(string ZipPath, string Version);
