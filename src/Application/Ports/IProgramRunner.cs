namespace Snail.MCP.Blender.Application.Ports;

/// <summary>Runs a program whose functions are the commands of the catalog, so a build of many steps costs one call.</summary>
/// <remarks>The API's own programmatic tool calling runs the program in Anthropic's sandbox and cannot call the tools of an MCP server, so the
/// program runs here instead, beside the link it drives.</remarks>
public interface IProgramRunner
{
    Task<ProgramRun> RunAsync(string code, JsonObject? input, TimeSpan timeout, CancellationToken cancellationToken);
}
