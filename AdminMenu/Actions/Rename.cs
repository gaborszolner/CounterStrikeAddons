using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Logging;
using SharedLibrary;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void RenameAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
            {
                if (adminPlayer?.AuthorizedSteamID == null)
                {
                    adminPlayer?.PrintToChat(Msg.Get("RenameStartError"));
                    return;
                }

                var adminSteam2 = adminPlayer.AuthorizedSteamID.SteamId2;

                lock (_pendingRenameLock)
                {
                    _pendingRename ??= [];
                    _pendingRename[adminSteam2] = new PendingRenameEntry
                    {
                        TargetSteamId2 = targetPlayer.AuthorizedSteamID?.SteamId2 ?? string.Empty,
                        Expiration = Utils.GetServerTime().AddSeconds(30),
                        OldName = targetPlayer.PlayerName,
                        AdminName = adminPlayer.PlayerName,
                        AdminSteamId2 = adminSteam2
                    };
                }

                adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("RenamePrompt", targetPlayer.PlayerName)}");
                MenuManager.GetActiveMenu(adminPlayer)?.Close();
            });
        }

        private void RenamePlayer(CCSPlayerController adminPlayer, string newName, PendingRenameEntry pendingRename)
        {
            if (_pendingRename is null || pendingRename is null || string.IsNullOrWhiteSpace(newName) || newName.StartsWith('!'))
            {
                return;
            }

            try
            {
                if (pendingRename.Expiration < Utils.GetServerTime())
                {
                    lock (_pendingRenameLock)
                    {
                        _pendingRename.Remove(pendingRename.AdminSteamId2);
                    }
                    adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("RenameExpired")}");
                    return;
                }

                try
                {
                    var target = PlayerHelper.GetAllPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId2 == pendingRename.TargetSteamId2);
                    if (target != null && target.IsValid)
                    {
                        target.PlayerName = newName;
                        Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerRenamed", pendingRename.OldName, newName, pendingRename.AdminName)}");
                        Logger?.LogInformation($"Player renamed: {pendingRename.OldName} to {newName} by {pendingRename.AdminName} (SteamID2: {pendingRename.TargetSteamId2})");
                    }
                    else
                    {
                        adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("TargetDisconnected")}");
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"Error applying rename: {ex.Message}");
                    adminPlayer.PrintToChat($"{PluginPrefix} {Msg.Get("RenameApplyError", ex.Message)}");
                }

                lock (_pendingRenameLock)
                {
                    _pendingRename.Remove(pendingRename.AdminSteamId2);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error processing pending rename: {ex.Message}");
            }
        }

        private class PendingRenameEntry
        {
            public string TargetSteamId2 { get; set; } = string.Empty;
            public DateTime Expiration { get; set; }
            public string OldName { get; set; } = string.Empty;
            public string AdminName { get; set; } = string.Empty;
            public string AdminSteamId2 { get; set; } = string.Empty;
        }
    }
}
