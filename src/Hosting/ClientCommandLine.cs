using Snail.MCP.Blender.Application.Access;
using Snail.MCP.Blender.Configuration;

namespace Snail.MCP.Blender.Hosting;

/// <summary>The <c>clients</c> command: mints, lists and revokes the keys this server admits clients by, instead of the running server doing it.</summary>
/// <remarks>A key is worth nothing to whoever reads the file it is kept in — only its digest is stored — so it cannot be looked up later and
/// there is no editing the file by hand. This is where an operator gets one, and the only moment the secret exists outside the holder's hands
/// is the line that prints it.</remarks>
public static class ClientCommandLine
{
    public const string Verb = "clients";

    public static bool Asked(string[] args) => args.Length > 0 && args[0].Equals(Verb, StringComparison.OrdinalIgnoreCase);

    /// <summary>Runs the command and returns the exit code; anything it has to say goes to <paramref name="output"/>, not to a log, and the
    /// keys are kept where <paramref name="settings"/> says — the server's own configuration unless a caller names another place.</summary>
    public static int Run(string[] args, TextWriter output, ServerConfig? settings = null)
    {
        var keys = new ClientKeys(settings ?? ServerConfigExtensions.Peek(), TimeProvider.System);
        var action = args.Length > 1 ? args[1].ToLowerInvariant() : "list";

        switch (action)
        {
            case "list":
                List(keys, output);

                return 0;

            case "add" when args.Length > 2:
                return Add(keys, args, output);

            case "revoke" when args.Length > 2:
                return Revoke(keys, args[2], output);

            default:
                output.WriteLine("usage: clients list | clients add <name> [--scope read,write,delete,python,farm] | clients revoke <name>");

                return 1;
        }
    }

    private static void List(ClientKeys keys, TextWriter output)
    {
        if (keys.Configured is { } configured)
        {
            output.WriteLine($"{configured.Name} — the configured token, every scope");
        }

        foreach (var key in keys.All)
        {
            output.WriteLine($"{key.Name} — {string.Join(", ", key.Scopes)}, minted {key.Issued:yyyy-MM-dd}{(key.Revoked is { } revoked ? $", revoked {revoked:yyyy-MM-dd}" : string.Empty)}");
        }

        if (keys.Configured is null && keys.All.Count == 0)
        {
            output.WriteLine("no clients yet; mint one with: clients add <name>");
        }
    }

    private static int Add(ClientKeys keys, string[] args, TextWriter output)
    {
        var scopes = Scopes(args);

        try
        {
            var (key, secret) = keys.Mint(args[2], scopes);

            output.WriteLine($"{key.Name} — {string.Join(", ", key.Scopes)}");
            output.WriteLine(secret);
            output.WriteLine("This is the only time the key is shown; give it to the client and keep no copy here.");

            return 0;
        }
        catch (ArgumentException failure)
        {
            output.WriteLine(failure.Message);

            return 1;
        }
    }

    private static int Revoke(ClientKeys keys, string name, TextWriter output)
    {
        if (!keys.Revoke(name))
        {
            output.WriteLine($"no client named '{name}' holds a key here");

            return 1;
        }

        output.WriteLine($"'{name}' is no longer admitted; a session it holds ends with its next request.");

        return 0;
    }

    /// <summary>The scopes named after <c>--scope</c>, comma-separated; none means the ordinary ones.</summary>
    private static IReadOnlyList<string>? Scopes(string[] args)
    {
        var flag = Array.FindIndex(args, argument => argument.Equals("--scope", StringComparison.OrdinalIgnoreCase));

        return flag < 0 || flag + 1 >= args.Length
            ? null
            : [.. args[flag + 1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
    }
}
