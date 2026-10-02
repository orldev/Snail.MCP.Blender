using ModelContextProtocol.Protocol;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>What a test reads off a tool result: the text the model sees, and whether the protocol flagged it as an error.</summary>
public static class ToolResults
{
    extension(CallToolResult result)
    {
        public string Text() => ((TextContentBlock)result.Content[0]).Text;

        public bool Failed() => result.IsError == true;
    }
}
