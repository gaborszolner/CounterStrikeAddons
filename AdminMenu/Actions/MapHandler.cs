using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Logging;
using SharedLibrary;
using SharedLibrary.Entries;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void MapHandlerAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            var mapMenu = new CenterHtmlMenu(Msg.Get("MapMenuTitle"), this);

            mapMenu.AddMenuOption(Msg.Get("AddMap"), (controller, _) => StartAddMap(controller));
            mapMenu.AddMenuOption(Msg.Get("DeleteMap"), (controller, _) => ShowDeleteMapMenu(controller));
            mapMenu.AddMenuOption(Msg.Get("SortMapsByName"), (controller, _) =>
            {
                MenuManager.GetActiveMenu(controller)?.Close();
                SortMapsByName(controller);
            });
            if (File.Exists(_mapStatFileFullPath))
            {
                var usageLabel = Msg.Get(_sortMapsByUsage ? "SortMapsByUsageOn" : "SortMapsByUsageOff");
                mapMenu.AddMenuOption($"{Msg.Get("SortMapsByUsage")} [{usageLabel}]", (controller, _) =>
                {
                    MenuManager.GetActiveMenu(controller)?.Close();
                    _sortMapsByUsage = !_sortMapsByUsage;
                    controller.PrintToChat($"{PluginPrefix} {Msg.Get(_sortMapsByUsage ? "SortMapsByUsageEnabled" : "SortMapsByUsageDisabled")}");
                    Logger?.LogInformation($"Sort maps by usage {(_sortMapsByUsage ? "enabled" : "disabled")} by {controller.PlayerName}");
                });
            }
            mapMenu.AddMenuOption(Msg.Get("CleanMapFiles"), (controller, _) => ShowCleanMapFilesConfirmMenu(controller));
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, mapMenu);
        }

        private List<string>? GetOrphanWorkshopFolders()
        {
            // Without a map list every folder would look orphaned, so refuse to clean.
            if (!File.Exists(_mapListFilePath) || !Directory.Exists(_workshopContentDirectory))
            {
                return null;
            }

            var listedIds = GetMaps(_mapListFilePath).Values
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var currentMapVpk = $"{Server.MapName?.Trim()}.vpk";

            return Directory.GetDirectories(_workshopContentDirectory)
                .Where(dir =>
                {
                    var id = Path.GetFileName(dir);
                    return id.All(char.IsDigit) &&
                           !listedIds.Contains(id) &&
                           Directory.GetFiles(dir, currentMapVpk, SearchOption.AllDirectories).Length == 0;
                })
                .ToList();
        }

        private void ShowCleanMapFilesConfirmMenu(CCSPlayerController adminPlayer)
        {
            var orphans = GetOrphanWorkshopFolders();
            if (orphans is null)
            {
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("CleanMapFilesUnavailable")}");
                return;
            }
            if (orphans.Count == 0)
            {
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("CleanMapFilesNothing")}");
                return;
            }

            var confirmMenu = new CenterHtmlMenu(Msg.Get("CleanMapFilesConfirm", orphans.Count), this);
            confirmMenu.AddMenuOption(Msg.Get("Yes"), (controller, _) =>
            {
                MenuManager.GetActiveMenu(controller)?.Close();
                CleanMapFiles(controller);
            });
            confirmMenu.AddMenuOption(Msg.Get("No"), (controller, _) => MenuManager.GetActiveMenu(controller)?.Close());
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, confirmMenu);
        }

        private void CleanMapFiles(CCSPlayerController adminPlayer)
        {
            var orphans = GetOrphanWorkshopFolders();
            if (orphans is null)
            {
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("CleanMapFilesUnavailable")}");
                return;
            }

            var deleted = 0;
            foreach (var folder in orphans)
            {
                var id = Path.GetFileName(folder);
                try
                {
                    Directory.Delete(folder, true);
                    RemoveWorkshopItemFromAcf(id);
                    deleted++;
                    Logger?.LogInformation($"Orphan workshop map folder deleted: {folder} by {adminPlayer.PlayerName}");
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"Error deleting orphan workshop folder '{folder}': {ex.Message}");
                }
            }

            adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("CleanMapFilesDone", deleted, orphans.Count - deleted)}");
        }

        // ModuleDirectory is csgo\addons\counterstrikesharp\plugins\AdminMenu
        private string _workshopDirectory => Path.GetFullPath(Path.Combine(ModuleDirectory, "..", "..", "..", "..", "workshop"));
        private string _workshopContentDirectory => Path.Combine(_workshopDirectory, "content", "730");
        private string _workshopAcfFilePath => Path.Combine(_workshopDirectory, "appworkshop_730.acf");

        private void ShowDeleteMapMenu(CCSPlayerController adminPlayer)
        {
            var maps = GetMaps(_mapListFilePath);
            if (maps.Count == 0)
            {
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("DeleteMapNoMaps")}");
                return;
            }

            var deleteMenu = new CenterHtmlMenu(Msg.Get("DeleteMapTitle"), this);
            foreach (var map in maps)
            {
                var label = map.Value is null ? map.Key : $"{map.Key} ({map.Value})";
                deleteMenu.AddMenuOption(label, (controller, _) => ShowDeleteMapConfirmMenu(controller, map.Key, map.Value));
            }
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, deleteMenu);
        }

        private void ShowDeleteMapConfirmMenu(CCSPlayerController adminPlayer, string mapName, string? workshopId)
        {
            var confirmMenu = new CenterHtmlMenu(Msg.Get("DeleteMapConfirm", mapName), this);
            confirmMenu.AddMenuOption(Msg.Get("Yes"), (controller, _) =>
            {
                MenuManager.GetActiveMenu(controller)?.Close();
                DeleteMap(controller, mapName, workshopId);
            });
            confirmMenu.AddMenuOption(Msg.Get("No"), (controller, _) => MenuManager.GetActiveMenu(controller)?.Close());
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, confirmMenu);
        }

        private void DeleteMap(CCSPlayerController adminPlayer, string mapName, string? workshopId)
        {
            if (string.Equals(Server.MapName?.Trim(), mapName, StringComparison.OrdinalIgnoreCase))
            {
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("DeleteMapIsCurrent", mapName)}");
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(workshopId) && workshopId.All(char.IsDigit))
                {
                    var mapFolder = Path.Combine(_workshopContentDirectory, workshopId);
                    if (Directory.Exists(mapFolder))
                    {
                        Directory.Delete(mapFolder, true);
                    }
                    else
                    {
                        Logger?.LogWarning($"Workshop folder not found for map {mapName}: {mapFolder}");
                    }

                    RemoveWorkshopItemFromAcf(workshopId);
                }

                RemoveMapFromList(mapName);

                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("DeleteMapSuccess", mapName)}");
                Logger?.LogInformation($"Map deleted: {mapName} ({workshopId ?? "no workshop id"}) by {adminPlayer.PlayerName}");
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error deleting map '{mapName}': {ex.Message}");
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("DeleteMapError", ex.Message)}");
            }
        }

        private void RemoveMapFromList(string mapName)
        {
            if (!File.Exists(_mapListFilePath))
            {
                return;
            }

            var lines = File.ReadAllLines(_mapListFilePath)
                .Where(line => line.StartsWith(@"//") ||
                               !line.Split(':')[0].Trim().Equals(mapName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            File.WriteAllLines(_mapListFilePath, lines);
        }

        private void RemoveWorkshopItemFromAcf(string workshopId)
        {
            if (!File.Exists(_workshopAcfFilePath))
            {
                Logger?.LogWarning($"Workshop acf file not found: {_workshopAcfFilePath}");
                return;
            }

            var content = File.ReadAllText(_workshopAcfFilePath);
            var updated = RemoveAcfBlock(content, workshopId);
            if (updated != content)
            {
                File.WriteAllText(_workshopAcfFilePath, updated);
            }
        }

        // Removes every `"key" { ... }` block (in any section) from a Valve KeyValues text.
        private static string RemoveAcfBlock(string content, string key)
        {
            var token = $"\"{key}\"";
            var searchFrom = 0;
            while (true)
            {
                var keyIndex = content.IndexOf(token, searchFrom, StringComparison.Ordinal);
                if (keyIndex < 0)
                {
                    return content;
                }

                var braceIndex = keyIndex + token.Length;
                while (braceIndex < content.Length && char.IsWhiteSpace(content[braceIndex]))
                {
                    braceIndex++;
                }

                // Skip occurrences that are values (e.g. "appid" "730") rather than block keys.
                if (braceIndex >= content.Length || content[braceIndex] != '{')
                {
                    searchFrom = keyIndex + token.Length;
                    continue;
                }

                var depth = 0;
                var endIndex = -1;
                for (var i = braceIndex; i < content.Length; i++)
                {
                    if (content[i] == '{') depth++;
                    else if (content[i] == '}' && --depth == 0)
                    {
                        endIndex = i;
                        break;
                    }
                }

                if (endIndex < 0)
                {
                    return content;
                }

                // Also remove the indentation and line break preceding the key.
                var startIndex = keyIndex;
                while (startIndex > 0 && (content[startIndex - 1] == '\t' || content[startIndex - 1] == ' '))
                {
                    startIndex--;
                }
                if (startIndex > 0 && content[startIndex - 1] == '\n') startIndex--;
                if (startIndex > 0 && content[startIndex - 1] == '\r') startIndex--;

                content = content.Remove(startIndex, endIndex + 1 - startIndex);
                searchFrom = startIndex;
            }
        }

        private void SortMapsByName(CCSPlayerController adminPlayer)
        {
            if (!File.Exists(_mapListFilePath))
            {
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("DeleteMapNoMaps")}");
                return;
            }

            try
            {
                var lines = File.ReadAllLines(_mapListFilePath);
                var comments = lines.Where(l => l.StartsWith(@"//"));
                var maps = lines
                    .Where(l => !l.StartsWith(@"//") && !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .OrderBy(l => l.Split(':')[0].Trim(), StringComparer.OrdinalIgnoreCase);

                File.WriteAllLines(_mapListFilePath, comments.Concat(maps).ToList());

                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("SortMapsByNameSuccess")}");
                Logger?.LogInformation($"Map list sorted by name by {adminPlayer.PlayerName}");
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error sorting map list by name: {ex.Message}");
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("SortMapsError", ex.Message)}");
            }
        }

        private static bool _sortMapsByUsage = false;

        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            if (_sortMapsByUsage)
            {
                SortMapsByUsage();
            }
            return HookResult.Continue;
        }

        private void SortMapsByUsage()
        {
            if (!File.Exists(_mapListFilePath) || !File.Exists(_mapStatFileFullPath))
            {
                return;
            }

            try
            {
                var stats = Utils.ReadStoredStat<Dictionary<string, MapStatEntry>>(_mapStatFileFullPath) ?? [];
                var lastPlayed = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                foreach (var stat in stats)
                {
                    lastPlayed[stat.Key] = stat.Value.LastPlayed;
                }

                var lines = File.ReadAllLines(_mapListFilePath);
                var comments = lines.Where(l => l.StartsWith(@"//"));
                var maps = lines
                    .Where(l => !l.StartsWith(@"//") && !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .Select(l => (Line: l, Name: l.Split(':')[0].Trim()))
                    .OrderBy(m => lastPlayed.TryGetValue(m.Name, out var played) ? played : DateTime.MinValue)
                    .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(m => m.Line);

                var sorted = comments.Concat(maps).ToList();
                if (!sorted.SequenceEqual(lines))
                {
                    File.WriteAllLines(_mapListFilePath, sorted);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error sorting map list by usage: {ex.Message}");
            }
        }

        private void StartAddMap(CCSPlayerController adminPlayer)
        {
            if (adminPlayer?.AuthorizedSteamID == null)
            {
                adminPlayer?.PrintToChat($"{PluginPrefix} {Msg.Get("AddMapStartError")}");
                return;
            }

            var adminSteam2 = adminPlayer.AuthorizedSteamID.SteamId2;
            lock (_pendingAddMapLock)
            {
                _pendingAddMap[adminSteam2] = new PendingAddMapEntry
                {
                    Expiration = Utils.GetServerTime().AddSeconds(AddMapTimeoutSeconds)
                };
            }

            MenuManager.GetActiveMenu(adminPlayer)?.Close();
            adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("AddMapNamePrompt", AddMapTimeoutSeconds)}");
        }

        private bool TryHandleAddMapInput(CCSPlayerController adminPlayer, string input)
        {
            var adminSteam2 = adminPlayer?.AuthorizedSteamID?.SteamId2;
            if (adminPlayer is null || adminSteam2 is null)
            {
                return false;
            }

            PendingAddMapEntry? pending;
            lock (_pendingAddMapLock)
            {
                if (!_pendingAddMap.TryGetValue(adminSteam2, out pending))
                {
                    return false;
                }

                if (pending.Expiration < Utils.GetServerTime())
                {
                    _pendingAddMap.Remove(adminSteam2);
                    adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("AddMapExpired")}");
                    return false;
                }
            }

            input = input.Trim();

            if (input.Equals("!cancel", StringComparison.OrdinalIgnoreCase))
            {
                lock (_pendingAddMapLock)
                {
                    _pendingAddMap.Remove(adminSteam2);
                }
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("AddMapCancelled")}");
                return true;
            }

            if (string.IsNullOrWhiteSpace(input) || input.StartsWith('!'))
            {
                return false;
            }

            if (pending.MapName is null)
            {
                if (input.Contains(':') || input.Any(char.IsWhiteSpace))
                {
                    adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("AddMapInvalidName")}");
                    return true;
                }

                if (GetMaps(_mapListFilePath).Keys.Any(k => k.Equals(input, StringComparison.OrdinalIgnoreCase)))
                {
                    lock (_pendingAddMapLock)
                    {
                        _pendingAddMap.Remove(adminSteam2);
                    }
                    adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("AddMapAlreadyExists", input)}");
                    return true;
                }

                lock (_pendingAddMapLock)
                {
                    pending.MapName = input;
                    pending.Expiration = Utils.GetServerTime().AddSeconds(AddMapTimeoutSeconds);
                }
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("AddMapIdPrompt", input, AddMapTimeoutSeconds)}");
                return true;
            }

            string? workshopId = null;
            if (input != "-")
            {
                if (!input.All(char.IsDigit))
                {
                    adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("AddMapInvalidId")}");
                    return true;
                }
                workshopId = input;
            }

            lock (_pendingAddMapLock)
            {
                _pendingAddMap.Remove(adminSteam2);
            }

            AddMapToList(adminPlayer, pending.MapName, workshopId);
            return true;
        }

        private void AddMapToList(CCSPlayerController adminPlayer, string mapName, string? workshopId)
        {
            try
            {
                var line = workshopId is null ? mapName : $"{mapName}:{workshopId}";

                var directory = Path.GetDirectoryName(_mapListFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var prefix = string.Empty;
                if (File.Exists(_mapListFilePath))
                {
                    var existing = File.ReadAllText(_mapListFilePath);
                    if (existing.Length > 0 && !existing.EndsWith('\n'))
                    {
                        prefix = Environment.NewLine;
                    }
                }

                File.AppendAllText(_mapListFilePath, prefix + line + Environment.NewLine);

                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("AddMapSuccess", line)}");
                Logger?.LogInformation($"Map added to map list: {line} by {adminPlayer.PlayerName}");
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error adding map '{mapName}' to map list: {ex.Message}");
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("AddMapError", ex.Message)}");
            }
        }

        private const int AddMapTimeoutSeconds = 60;
        private static readonly Dictionary<string, PendingAddMapEntry> _pendingAddMap = [];
        private static readonly object _pendingAddMapLock = new();

        private class PendingAddMapEntry
        {
            public string? MapName { get; set; }
            public DateTime Expiration { get; set; }
        }
    }
}
