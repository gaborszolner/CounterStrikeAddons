using AdminMenu.Entries;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using SharedLibrary;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void WeaponRestrictAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            string mapName = Server.MapName.Trim() ?? string.Empty;
            var restrictMenu = new CenterHtmlMenu(Msg.Get("WeaponRestrictMenuTitle"), this);

            restrictMenu.AddMenuOption(Msg.Get("RestrictThisMap"), (controller, _) => { ShowRestrictWeaponsMenu(adminPlayer, true, mapName); });
            restrictMenu.AddMenuOption(Msg.Get("UnrestrictThisMap"), (controller, _) => { ShowRestrictWeaponsMenu(adminPlayer, false, mapName); });
            restrictMenu.AddMenuOption(Msg.Get("RestrictAllMaps"), (controller, _) => { ShowRestrictWeaponsMenu(adminPlayer, true, "*"); });
            restrictMenu.AddMenuOption(Msg.Get("UnrestrictAllMaps"), (controller, _) => { ShowRestrictWeaponsMenu(adminPlayer, false, "*"); });
            restrictMenu.AddMenuOption(Msg.Get("ShowRestrictedWeapons"), ShowRestrictedWeapons(mapName));
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, restrictMenu);
        }

        private void ShowRestrictWeaponsMenu(CCSPlayerController adminPlayer, bool isRestrict, string mapName)
        {
            var weaponMenu = new CenterHtmlMenu(Msg.Get("ChooseWeapon"), this);

            foreach (var weaponName in WeaponHelper.AllWeapon)
            {
                if (isRestrict)
                {
                    weaponMenu.AddMenuOption(weaponName.Replace("weapon_", ""), (controller, _) => { RestrictWeapon(adminPlayer, weaponName, mapName); });
                }
                else
                {
                    weaponMenu.AddMenuOption(weaponName.Replace("weapon_", ""), (controller, _) => { UnrestrictWeapon(adminPlayer, weaponName, mapName); });
                }
            }
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, weaponMenu);
        }

        private void UnrestrictWeapon(CCSPlayerController adminPlayer, string weapon, string unrestrictMapName)
        {
            _weaponRestrictEntry ??= [];

            if (_weaponRestrictEntry.ContainsKey(weapon))
            {
                if (unrestrictMapName == "*")
                {
                    _weaponRestrictEntry.Remove(weapon);
                }
                else
                {
                    if (_weaponRestrictEntry[weapon].Maps.Contains(unrestrictMapName))
                    {
                        if (_weaponRestrictEntry[weapon].Maps.Length > 0 && _weaponRestrictEntry[weapon].Maps[0] == "*")
                        {
                            adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("UnrestrictAllFirst")}");
                            return;
                        }
                        else
                        {
                            _weaponRestrictEntry[weapon].Maps = _weaponRestrictEntry[weapon].Maps.Where(m => m != unrestrictMapName).ToArray();
                        }
                    }
                    if (_weaponRestrictEntry[weapon].Maps.Length == 0)
                    {
                        _weaponRestrictEntry.Remove(weapon);
                    }
                }
            }
            else
            {
                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("WeaponNotRestricted", weapon, unrestrictMapName)}");
                return;
            }

            Utils.WriteToFile(_weaponRestrictEntry, _weaponRestrictFilePath);

            Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("WeaponUnrestricted", weapon, unrestrictMapName, adminPlayer.PlayerName)}");
            MenuManager.GetActiveMenu(adminPlayer)?.Close();
        }

        private void RestrictWeapon(CCSPlayerController adminPlayer, string weapon, string restrictMapName)
        {
            _weaponRestrictEntry ??= [];

            if (!_weaponRestrictEntry.ContainsKey(weapon))
            {
                _weaponRestrictEntry.Add(weapon, new WeaponRestrictEntry { Maps = [restrictMapName] });
            }
            else
            {
                if (restrictMapName == "*")
                {
                    _weaponRestrictEntry[weapon].Maps = ["*"];
                }
                else
                {
                    if (!_weaponRestrictEntry[weapon].Maps.Contains(restrictMapName) && (_weaponRestrictEntry[weapon].Maps.Length == 0 || _weaponRestrictEntry[weapon].Maps[0] != "*"))
                    {
                        _weaponRestrictEntry[weapon].Maps = _weaponRestrictEntry[weapon].Maps.Append(restrictMapName).ToArray();
                    }
                }
            }

            foreach (var currentPlayer in PlayerHelper.GetAllNonSpecPlayers())
            {
                ThrowForbiddenWeapon(currentPlayer);
            }

            Utils.WriteToFile(_weaponRestrictEntry, _weaponRestrictFilePath);

            Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("WeaponRestricted", weapon.Replace("weapon_", ""), restrictMapName, adminPlayer.PlayerName)}");
            MenuManager.GetActiveMenu(adminPlayer)?.Close();
        }

        private Action<CCSPlayerController, ChatMenuOption> ShowRestrictedWeapons(string mapName)
        {
            return (controller, option) =>
            {
                string weaponList = GetRestrictedWeapons(mapName);
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("RestrictedWeaponsOnMap", mapName, weaponList.Replace("weapon_", ""))}");
            };
        }

        private static string GetRestrictedWeapons(string mapName)
        {
            string weaponList = string.Empty;
            foreach (var weapon in _weaponRestrictEntry ?? [])
            {
                if (weapon.Value.Maps.Contains("*") || weapon.Value.Maps.Contains(mapName))
                {
                    weaponList += $"{weapon.Key}, ";
                }
            }
            weaponList = string.IsNullOrWhiteSpace(weaponList) ? Msg.Get("NoRestrictedWeapon") : weaponList.TrimEnd(' ', ',');
            return weaponList;
        }

        private void ThrowForbiddenWeapon(CCSPlayerController? player)
        {
            var pawn = player?.PlayerPawn.Value;

            if (player is null || !player.IsValid || pawn is null || _weaponRestrictEntry is null || _weaponRestrictEntry.Count == 0 || _isWarmup)
            {
                return;
            }

            bool isAdminAboveRestrictionLevel = false;
            if (GetAdminLevel(player) > 2) { isAdminAboveRestrictionLevel = true; }

            var weapon = pawn.WeaponServices?.ActiveWeapon.Value;
            string weaponName = weapon?.DesignerName ?? string.Empty;
            string mapName = Server.MapName.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(mapName) || string.IsNullOrWhiteSpace(weaponName)) { return; }

            if (_weaponRestrictEntry.ContainsKey(weaponName))
            {
                var restrictedWeaponMapList = _weaponRestrictEntry[weaponName];
                if (restrictedWeaponMapList is not null &&
                    (restrictedWeaponMapList.Maps.Contains("*") || restrictedWeaponMapList.Maps.Contains(mapName)))
                {
                    if (isAdminAboveRestrictionLevel) 
                    { 
                        player.PrintToChat($"{PluginPrefix} {ChatColors.Red}{Msg.Get("WeaponForbiddenWarning", weaponName)}"); 
                    }
                    else
                    {
                        player.DropActiveWeapon();
                        weapon?.Remove();
                        player.PrintToChat($"{PluginPrefix} {Msg.Get("WeaponCannotUse", weaponName)}");
                    }
                }
            }
            else
            {
                return;
            }

            return;
        }
    }
}
