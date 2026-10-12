using Microsoft.Extensions.Configuration;

namespace Snail.MCP.Blender.Configuration;

/// <summary>Maps <c>SNAIL_MCP_BLENDER_SECTION__FIELD_NAME</c> variables onto configuration keys <c>Section:FieldName</c>.</summary>
/// <remarks>The reading is Snail.Sdk.Mcp's, the same in every Snail server; the prefix and the config file variable are this server's.</remarks>
public static class EnvironmentSettings
{
    public const string Prefix = "SNAIL_MCP_BLENDER_";

    public const string ConfigPathVariable = $"{Prefix}CONFIG";

    /// <summary>The configuration key the config path arrives under, once the prefix is off.</summary>
    public const string ConfigKey = "CONFIG";

    public static IConfigurationBuilder AddEnvironment(this IConfigurationBuilder builder) =>
        builder.AddInMemoryCollection(Read(Environment.GetEnvironmentVariables()));

    public static IEnumerable<KeyValuePair<string, string?>> Read(System.Collections.IDictionary variables) =>
        Snail.Sdk.Mcp.Hosting.EnvironmentSettings.Read(Prefix, variables);

    internal static string ToConfigurationKey(string variable) =>
        Snail.Sdk.Mcp.Hosting.EnvironmentSettings.ToConfigurationKey(variable);
}
