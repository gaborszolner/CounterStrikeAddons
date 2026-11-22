using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace StartMap
{
    public class StartMap : BasePlugin
    {
        public override string ModuleName => "StartMap";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "Automatically switch to a specified map after the server starts";

        public readonly string PluginPrefix = $"[StartMap]";

        private string _startMapFilePath = string.Empty;
        public override void Load(bool hotReload)
        {
            _startMapFilePath = Path.Combine(ModuleDirectory, "startMap.txt");

            AddTimer(0.5f, () =>
            {
                MapChange(GetMap(_startMapFilePath));
            });
        }

        private void MapChange(KeyValuePair<string, string>? map)
        {
            if (map is null || !map.HasValue || string.IsNullOrWhiteSpace(map.Value.Value) || string.IsNullOrWhiteSpace(map.Value.Key))
            {
                return;
            }

            if (Server.IsMapValid(map.Value.Key))
            {
                Server.ExecuteCommand($"map {map.Value.Key}");
            }
            else if (map.Value.Value is not null)
            {
                Server.ExecuteCommand($"host_workshop_map {map.Value.Value}");
            }
            else
            {
                Server.ExecuteCommand($"ds_workshop_changelevel {map.Value.Key}");
            }
        }

        private KeyValuePair<string, string>? GetMap(string mapFilePath)
        {
            if (!File.Exists(mapFilePath))
            {
                Logger?.LogError($"{PluginPrefix} Map list file not found: {mapFilePath}");
                return null;
            }

            try
            {
                var line = File.ReadLines(mapFilePath).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(line))
                    return null;

                var parts = line.Split(':', 2);

                if (parts.Length != 2)
                    return null;

                return new KeyValuePair<string, string>
                (
                    parts[0].Trim(),
                    parts[1].Trim()
                );
            }
            catch (Exception ex)
            {
                Logger?.LogError($"{PluginPrefix} Error parsing map list file '{mapFilePath}': {ex.Message}");
                return null;
            }
        }
    }
}