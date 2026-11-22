using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Logging;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void ChangeMapAction(CCSPlayerController player, ChatMenuOption option)
        {
            Dictionary<string, string> mapList = GetMaps(_mapListFilePath);
            var mapMenu = new CenterHtmlMenu(Msg.Get("ChooseMap"), this);
            foreach (var map in mapList)
            {
                mapMenu.AddMenuOption(map.Key, (CCSPlayerController player, ChatMenuOption menuOption) =>
                {
                    if (Server.IsMapValid(map.Key))
                    {
                        Server.ExecuteCommand($"changelevel {map.Key}");
                    }
                    else if (map.Value is not null)
                    {
                        Server.ExecuteCommand($"host_workshop_map {map.Value}");
                    }
                    else
                    {
                        Server.ExecuteCommand($"ds_workshop_changelevel {map.Key}");
                    }
                });
            }
            Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("MapChangeInitiated", player.PlayerName)}");
            MenuManager.OpenCenterHtmlMenu(this, player, mapMenu);
        }

        private Dictionary<string, string> GetMaps(string mapListFilePath)
        {
            Dictionary<string, string> mapList = [];
            if (File.Exists(mapListFilePath))
            {
                foreach (var line in File.ReadLines(mapListFilePath).Where(l => !l.StartsWith(@"//")))
                {
                    try
                    {
                        var parts = line.Split(':');

                        if (parts.Length == 1)
                        {
                            var key = parts[0].Trim();
                            mapList[key] = null;
                        }
                        else if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();

                            mapList[key] = value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError($"{PluginPrefix} Error parsing line '{line}' in map list file: {ex.Message}");
                    }
                }
            }
            else
            {
                Logger?.LogError($"{PluginPrefix} Map list file not found: {mapListFilePath}");
            }
            return mapList;
        }
    }
}