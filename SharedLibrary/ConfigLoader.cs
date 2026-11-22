using System.Text.Json;
using System.Text.Json.Nodes;

namespace SharedLibrary
{
    public static class ConfigLoader
    {
        public static T Load<T>(string directory, string fileName, bool migrateSharedConfig = true) where T : class, new()
        {
            string path = Path.Combine(directory, fileName);
            if (File.Exists(path))
                return Read<T>(path);

            const string suffix = "Config.json";
            string previousName = fileName.EndsWith(suffix, StringComparison.Ordinal)
                ? fileName[..^suffix.Length] + "config.json"
                : fileName;
            string previousPath = Path.Combine(directory, previousName);
            string legacyPath = Path.Combine(directory, "config.json");
            T config = File.Exists(previousPath)
                ? Read<T>(previousPath)
                : migrateSharedConfig && File.Exists(legacyPath) ? ReadLegacy<T>(legacyPath) : new T();
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            using var writer = new StreamWriter(stream);
            writer.Write(json);
            return config;
        }

        private static T Read<T>(string path) where T : class
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path))
                ?? throw new JsonException($"Configuration file '{path}' contains null.");
        }

        private static T ReadLegacy<T>(string path) where T : class
        {
            var json = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new JsonException($"Configuration file '{path}' must contain an object.");
            if (!json.ContainsKey("AutoTeamShuffleOnRoundStart"))
                json["AutoTeamShuffleOnRoundStart"] = false;
            if (!json.ContainsKey("AllowSameName"))
                json["AllowSameName"] = true;
            if (!json.ContainsKey("EnableTopBottomCommand"))
                json["EnableTopBottomCommand"] = true;
            if (!json.ContainsKey("CanReviveTeammate"))
                json["CanReviveTeammate"] = true;

            return json.Deserialize<T>()
                ?? throw new JsonException($"Configuration file '{path}' contains null.");
        }
    }
}
