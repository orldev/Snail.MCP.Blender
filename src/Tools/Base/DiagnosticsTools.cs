using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Blender.Application.Diagnostics;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Tools.Base;

/// <summary>Health of the server and of the link into Blender, plus the add-on installer.</summary>
[McpServerToolType]
public sealed class DiagnosticsTools(ServerHealth health, IAddOnPackager packager)
{
    [McpServerTool(Name = "blender_diagnose", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.Diagnostics.Diagnose)]
    public async Task<CallToolResult> DiagnoseAsync(CancellationToken cancellationToken = default) =>
        ToolResponse.Success(await health.ReportAsync(cancellationToken).ConfigureAwait(false));

    [McpServerTool(Name = "blender_install_addon", Destructive = false, OpenWorld = false)]
    [Description(ToolDescriptions.Diagnostics.InstallAddOn)]
    public CallToolResult InstallAddOn() =>
        ToolResponse.Success(Describe(packager.Pack()));

    private static JsonObject Describe(AddOnPackage package) => new()
    {
        ["zip"] = package.ZipPath,
        ["version"] = package.Version,
        ["steps"] = new JsonArray([.. Messages.InstallSteps.Select(step => (JsonNode)step)]),
    };
}
