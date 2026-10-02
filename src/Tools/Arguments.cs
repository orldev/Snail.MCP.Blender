namespace Snail.MCP.Blender.Tools;

/// <summary>Builds the params of a bridge request, leaving out what the caller did not pass so the add-on's defaults apply.</summary>
public static class Arguments
{
    extension(JsonObject arguments)
    {
        public JsonObject With(string key, string? value) => arguments.Set(key, value is null ? null : JsonValue.Create(value));

        public JsonObject With(string key, bool? value) => arguments.Set(key, value is null ? null : JsonValue.Create(value.Value));

        public JsonObject With(string key, int? value) => arguments.Set(key, value is null ? null : JsonValue.Create(value.Value));

        public JsonObject With(string key, double? value) => arguments.Set(key, value is null ? null : JsonValue.Create(value.Value));

        public JsonObject With(string key, double[]? value) =>
            arguments.Set(key, value is null ? null : new JsonArray([.. value.Select(item => (JsonNode)item)]));

        public JsonObject With(string key, string[]? value) =>
            arguments.Set(key, value is null ? null : new JsonArray([.. value.Select(item => (JsonNode)item)]));

        public JsonObject With(string key, JsonNode? value) => arguments.Set(key, value?.DeepClone());

        public JsonObject With(string key, Block? value) => arguments.Set(key, value?.ToWire());

        /// <summary>Writes an explicit JSON null: the add-on reads "key present, value none" as "clear it".</summary>
        public JsonObject WithNull(string key)
        {
            arguments[key] = null;

            return arguments;
        }

        private JsonObject Set(string key, JsonNode? node)
        {
            if (node is not null)
            {
                arguments[key] = node;
            }

            return arguments;
        }
    }
}
