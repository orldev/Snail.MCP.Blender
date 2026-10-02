using System.Security.Cryptography;

namespace Snail.MCP.Blender.Tests.Support;

/// <summary>The add-on's data directory kept in memory, answering the transfer and storage commands the way the add-on does, over a real socket.</summary>
public sealed class InMemoryVolume : IAsyncDisposable
{
    private readonly Dictionary<string, List<byte>> _arriving = new(StringComparer.Ordinal);

    public InMemoryVolume() => AddOn = new FakeAddOn(request => Task.FromResult<string?>(Answer(request).ToJsonString()), isConcurrent: true);

    public FakeAddOn AddOn { get; }

    /// <summary>Files by area and path: <c>files/robot/robot.blend</c>.</summary>
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    /// <summary>States of render jobs by id; a job's folder is whatever <see cref="Files"/> holds under <c>jobs/&lt;id&gt;</c>.</summary>
    public Dictionary<string, string> Jobs { get; } = new(StringComparer.Ordinal);

    /// <summary>Keys of <see cref="Files"/> whose last chunk carries a digest that does not match, as a file damaged on its way out would.</summary>
    public HashSet<string> Corrupted { get; } = new(StringComparer.Ordinal);

    /// <summary>The file Blender has open, as a key of <see cref="Files"/>; the add-on refuses to delete it or a folder holding it.</summary>
    public string? OpenFile { get; set; }

    public const string Root = "/data";

    public ValueTask DisposeAsync() => AddOn.DisposeAsync();

    private JsonObject Answer(JsonObject request)
    {
        var parameters = request["params"] as JsonObject ?? [];
        var area = parameters["area"]?.GetValue<string>() ?? "files";
        var key = $"{area}/{parameters["path"]?.GetValue<string>()}".TrimEnd('/');
        var result = request["command"]!.GetValue<string>() switch
        {
            "ping" => new JsonObject { ["blender"] = "5.2.1", ["busy"] = false, ["machine"] = new JsonObject { ["platform"] = "linux", ["background"] = true } },
            "file_list" => List(key),
            "file_get" => Get(key, parameters["offset"]!.GetValue<long>(), parameters["length"]!.GetValue<int>()),
            "file_put" => Put(key, parameters),
            "storage" => Measure(),
            "storage_list" => Folder(area, key),
            "storage_delete" => Delete(key),
            "render_job_cancel" => new JsonObject { ["id"] = parameters["id"]?.DeepClone(), ["state"] = "cancelled" },
            _ => Error("UnknownCommand", "not a command this volume answers"),
        };

        return result["error"] is not null
            ? new JsonObject { ["id"] = request["id"]?.DeepClone(), ["ok"] = false, ["error"] = result["error"]!.DeepClone() }
            : new JsonObject { ["id"] = request["id"]?.DeepClone(), ["ok"] = true, ["result"] = result };
    }

    private JsonObject List(string key)
    {
        if (Files.TryGetValue(key, out var single))
        {
            return new JsonObject { ["kind"] = "file", ["files"] = new JsonArray(new JsonObject { ["path"] = key.Split('/')[^1], ["size"] = single.Length, ["modified"] = 1_789_000_000 }), ["total_bytes"] = single.Length, ["truncated"] = false };
        }

        var under = Under(key).ToList();

        return under.Count == 0
            ? Error("NotFound", $"nothing at {key}")
            : new JsonObject
            {
                ["kind"] = "directory",
                ["files"] = new JsonArray([.. under.Select(file => (JsonNode)new JsonObject { ["path"] = file.Key[(key.Length + 1)..], ["size"] = file.Value.Length, ["modified"] = 1_789_000_000 })]),
                ["total_bytes"] = under.Sum(file => file.Value.Length),
                ["truncated"] = false,
            };
    }

    private JsonObject Get(string key, long offset, int length)
    {
        var bytes = Files[key];
        var slice = bytes.Skip((int)offset).Take(length).ToArray();
        var eof = offset + slice.Length >= bytes.Length;
        var chunk = new JsonObject { ["data"] = Convert.ToBase64String(slice), ["eof"] = eof };

        if (eof)
        {
            chunk["sha256"] = Corrupted.Contains(key) ? new string('0', 64) : Convert.ToHexStringLower(SHA256.HashData(bytes));
        }

        return chunk;
    }

    private JsonObject Put(string key, JsonObject parameters)
    {
        var offset = parameters["offset"]!.GetValue<long>();

        if (offset == 0)
        {
            _arriving[key] = [];
        }

        if (!_arriving.TryGetValue(key, out var arrived) || arrived.Count != offset)
        {
            return Error("BadRequest", $"a chunk at {offset} does not follow the bytes that arrived");
        }

        arrived.AddRange(Convert.FromBase64String(parameters["data"]!.GetValue<string>()));

        if (parameters["done"]!.GetValue<bool>() is false)
        {
            return new JsonObject { ["received"] = arrived.Count };
        }

        var bytes = arrived.ToArray();
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));

        if (parameters["sha256"]?.GetValue<string>() is { } expected && digest != expected)
        {
            _arriving.Remove(key);

            return Error("Corrupt", "digest differs");
        }

        Files[key] = bytes;

        return new JsonObject { ["path"] = key, ["size"] = bytes.Length, ["sha256"] = digest };
    }

    private JsonObject Measure() => new()
    {
        ["areas"] = new JsonArray([.. new[] { "files", "jobs", "batches", "snapshots" }.Select(area => (JsonNode)new JsonObject
        {
            ["area"] = area,
            ["directory"] = $"{Root}/{area}",
            ["bytes"] = Under(area).Sum(file => file.Value.Length),
            ["files"] = Under(area).Count(),
        })]),
        ["disk"] = new JsonObject { ["total_bytes"] = 500L * 1024 * 1024 * 1024, ["free_bytes"] = 300L * 1024 * 1024 * 1024 },
        ["open_file"] = OpenFile is null ? null : $"{Root}/{OpenFile}",
        ["modified"] = true,
        ["compute"] = new JsonObject { ["backend"] = "OPTIX", ["devices"] = new JsonArray(new JsonObject { ["name"] = "NVIDIA RTX A2000 12GB", ["use"] = true }) },
    };

    private JsonObject Folder(string area, string key)
    {
        var names = Under(key)
            .Select(file => file.Key[(key.Length + 1)..].Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (names.Count == 0 && key.Contains('/'))
        {
            return Error("NotFound", $"no folder '{key}'");
        }

        var entries = names.Select(name =>
        {
            var child = $"{key}/{name}";
            var isFile = Files.TryGetValue(child, out var bytes);
            var entry = new JsonObject
            {
                ["name"] = name,
                ["kind"] = isFile ? "file" : "directory",
                ["bytes"] = isFile ? bytes!.Length : Under(child).Sum(file => file.Value.Length),
                ["files"] = isFile ? 1 : Under(child).Count(),
                ["modified"] = 1_789_000_000,
            };

            if (!key.Contains('/') && area == "jobs" && Jobs.TryGetValue(name, out var state))
            {
                entry["job"] = new JsonObject { ["state"] = state, ["frames_done"] = 3, ["frames_total"] = 48 };
            }

            return (JsonNode)entry;
        });

        return new JsonObject { ["area"] = area, ["directory"] = $"{Root}/{key}", ["entries"] = new JsonArray([.. entries]), ["truncated"] = false, ["open_file"] = OpenFile is null ? null : $"{Root}/{OpenFile}" };
    }

    private JsonObject Delete(string key)
    {
        if (OpenFile is not null && (OpenFile == key || OpenFile.StartsWith($"{key}/", StringComparison.Ordinal)))
        {
            return Error("InUse", $"'{key}' holds the file open in Blender; open another file before deleting it");
        }

        var doomed = Files.Keys.Where(path => path == key || path.StartsWith($"{key}/", StringComparison.Ordinal)).ToList();

        if (doomed.Count == 0)
        {
            return Error("NotFound", $"nothing at '{key}'");
        }

        var bytes = doomed.Sum(path => Files[path].Length);
        doomed.ForEach(path => Files.Remove(path));

        return new JsonObject { ["deleted_files"] = doomed.Count, ["bytes"] = bytes };
    }

    private IEnumerable<KeyValuePair<string, byte[]>> Under(string key) =>
        Files.Where(file => file.Key.StartsWith($"{key}/", StringComparison.Ordinal)).OrderBy(file => file.Key, StringComparer.Ordinal);

    private static JsonObject Error(string type, string message) => new() { ["error"] = new JsonObject { ["type"] = type, ["message"] = message } };
}
