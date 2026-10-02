using Microsoft.Extensions.Options;
using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Web.Access;

/// <summary>Stops an HTTP server that has no token before it listens, instead of letting it turn every client away.</summary>
public sealed class HttpAccessValidation : IValidateOptions<ServerConfig>
{
    public ValidateOptionsResult Validate(string? name, ServerConfig options) =>
        options.Transport is Transport.Http && ClientToken.Read(options.Http) is null
            ? ValidateOptionsResult.Fail(Messages.HttpTokenMissing)
            : ValidateOptionsResult.Success;
}
