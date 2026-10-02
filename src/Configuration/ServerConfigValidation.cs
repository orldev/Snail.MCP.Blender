using Microsoft.Extensions.Options;

namespace Snail.MCP.Blender.Configuration;

/// <summary>Checks the settings against the ranges declared on them; the implementation is generated from those attributes at compile time.</summary>
/// <remarks>The rule lives next to the setting it constrains, so a new setting cannot be added without one, and nothing is validated by
/// reflection at run time. The generated attributes format their own message — <c>ErrorMessage</c> on a range is ignored — and it already
/// names the setting and the range it broke.</remarks>
[OptionsValidator]
public sealed partial class ServerConfigValidation : IValidateOptions<ServerConfig>
{
}
