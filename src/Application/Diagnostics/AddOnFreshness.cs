using Snail.MCP.Blender.Configuration;
using Snail.MCP.Blender.Tools.Prompts;

namespace Snail.MCP.Blender.Application.Diagnostics;

/// <summary>Whether the add-on Blender has installed is the one this server ships, and which commands are missing when it is not.</summary>
/// <remarks>A server updated without its add-on answers <c>UnknownCommand</c> in the middle of a task, which reads as a broken tool rather than
/// a stale install. Versions cannot catch that — the add-on's version stays put across bug fixes — so both sides hash their own Python and the
/// digests are compared. The direction follows from the catalog: commands this server knows and the add-on does not mean the add-on is behind.</remarks>
public sealed class AddOnFreshness(IAddOnPackager packager, ILogger<AddOnFreshness> logger)
{
    private string? _reported;

    public JsonObject Describe(BridgeReply ping)
    {
        var bundled = packager.Describe();
        var report = new JsonObject
        {
            ["bundled"] = new JsonObject { ["version"] = bundled.Version, ["digest"] = bundled.Digest },
            ["path"] = ServerPaths.BundledAddOnDirectory,
        };

        if ((ping.Result as JsonObject)?["addon"] is not JsonObject installed)
        {
            report["status"] = "unknown";

            return report;
        }

        var digest = installed["digest"]?.GetValue<string>();
        report["installed"] = new JsonObject
        {
            ["version"] = installed["version"]?.GetValue<string>(),
            ["digest"] = digest,
            ["protocol"] = installed["protocol"]?.GetValue<int>(),
            ["commands"] = installed["commands"]?.GetValue<int>(),
        };
        report["protocol"] = Spoken(installed);

        if (digest is null)
        {
            report["status"] = "unknown";

            return report;
        }

        if (string.Equals(digest, bundled.Digest, StringComparison.Ordinal))
        {
            report["status"] = "current";

            return report;
        }

        if (report["protocol"]!["behind"]!.GetValue<bool>())
        {
            report["status"] = "older";
            report["hint"] = Messages.StaleAddOnHint;
            Missing(installed, report);
            Announce(digest, report);

            return report;
        }

        report["status"] = "differs";
        report["hint"] = Messages.StaleAddOnHint;
        Missing(installed, report);
        Announce(digest, report);

        return report;
    }

    /// <summary>What the add-on speaks against what this server needs: the two version numbers, and the abilities it is missing.</summary>
    /// <remarks>An add-on ahead of this server is not behind: it answers everything this server knows, and a server that refused it would
    /// make every upgrade a lockstep. One that reports no protocol at all is from before the two sides counted, which is behind by
    /// definition.</remarks>
    private static JsonObject Spoken(JsonObject installed)
    {
        var spoken = installed["protocol"]?.GetValue<int>() ?? 0;
        var able = (installed["capabilities"] as JsonArray)?.Select(name => name!.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ?? [];
        var lacking = AddOnProtocol.Required.Where(required => !able.Contains(required)).ToList();

        return new JsonObject
        {
            ["server"] = AddOnProtocol.Version,
            ["addOn"] = spoken,
            ["behind"] = spoken < AddOnProtocol.Version || lacking.Count > 0,
            ["lacking"] = new JsonArray([.. lacking.Select(name => JsonValue.Create(name))]),
        };
    }

    /// <summary>The commands each side has and the other does not; the first list names the tools that would fail right now.</summary>
    private static void Missing(JsonObject installed, JsonObject report)
    {
        if (installed["command_names"] is not JsonArray names)
        {
            return;
        }

        var registered = names.Select(name => name!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var catalog = BridgeCommands.All.Select(command => command.Name).ToHashSet(StringComparer.Ordinal);

        report["missing"] = new JsonArray([.. catalog.Except(registered).Order().Select(name => JsonValue.Create(name))]);
        report["notInTheCatalog"] = new JsonArray([.. registered.Except(catalog).Order().Select(name => JsonValue.Create(name))]);
    }

    /// <summary>Said once per installed build, so a long session does not repeat it on every report.</summary>
    private void Announce(string digest, JsonObject report)
    {
        if (string.Equals(_reported, digest, StringComparison.Ordinal))
        {
            return;
        }

        _reported = digest;
        logger.LogWarning(
            "The add-on installed in Blender ({Installed}) is not the one this server ships ({Bundled}); run blender_install_addon and reinstall the zip. Commands it does not have: {Missing}",
            digest,
            packager.Describe().Digest,
            report["missing"] is JsonArray missing && missing.Count > 0 ? string.Join(", ", missing.Select(name => name!.GetValue<string>())) : "none");
    }
}
