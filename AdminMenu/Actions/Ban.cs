using AdminMenu.Entries;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Microsoft.Extensions.Logging;
using SharedLibrary;
using System.Text.Json;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void BanAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            ShowPlayerListMenu(adminPlayer, false, false, (CCSPlayerController targetPlayer) =>
            {
                ChooseBanTimePlayer(adminPlayer, targetPlayer);
            });
        }

        private void ChooseBanTimePlayer(CCSPlayerController adminPlayer, CCSPlayerController player)
        {
            if (player == null || player.AuthorizedSteamID == null)
            {
                Logger?.LogError($"{PluginPrefix} Player or SteamID is invalid.");
                return;
            }

            var banTimeMenu = new CenterHtmlMenu(Msg.Get("BanMenuTitle"), this);
            banTimeMenu.AddMenuOption(Msg.Get("Ban1Min"), (CCSPlayerController controller, ChatMenuOption option) =>
            {
                BanPlayer(adminPlayer, player, Utils.GetServerTime().AddMinutes(1));
            });
            banTimeMenu.AddMenuOption(Msg.Get("Ban10Min"), (CCSPlayerController controller, ChatMenuOption option) =>
            {
                BanPlayer(adminPlayer, player, Utils.GetServerTime().AddMinutes(10));
            });
            banTimeMenu.AddMenuOption(Msg.Get("Ban1Day"), (CCSPlayerController controller, ChatMenuOption option) =>
            {
                BanPlayer(adminPlayer, player, Utils.GetServerTime().AddDays(1));
            });
            banTimeMenu.AddMenuOption(Msg.Get("Ban1Week"), (CCSPlayerController controller, ChatMenuOption option) =>
            {
                BanPlayer(adminPlayer, player, Utils.GetServerTime().AddDays(7));
            });
            banTimeMenu.AddMenuOption(Msg.Get("BanPermanent"), (CCSPlayerController controller, ChatMenuOption option) =>
            {
                BanPlayer(adminPlayer, player, DateTime.MaxValue);
            });

            banTimeMenu.PostSelectAction = PostSelectAction.Close;
            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, banTimeMenu);
        }

        private void BanPlayer(CCSPlayerController adminPlayer, CCSPlayerController player, DateTime banTime)
        {
            try
            {
                string steamId = player.AuthorizedSteamID.SteamId2;
                var bannedList = new Dictionary<string, BannedEntry>();

                if (File.Exists(_bannedFilePath))
                {
                    string json = File.ReadAllText(_bannedFilePath);
                    bannedList = JsonSerializer.Deserialize<Dictionary<string, BannedEntry>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new Dictionary<string, BannedEntry>();
                }

                var newEntry = new BannedEntry
                {
                    Identity = steamId,
                    Name = player.PlayerName,
                    BannedBy = adminPlayer.PlayerName,
                    Expiration = banTime
                };

                _bannedEntry ??= [];
                _bannedEntry[steamId] = newEntry;
                bannedList[steamId] = newEntry;
                Utils.WriteToFile(bannedList, _bannedFilePath);

                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKBANADDED);
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerBanned", player.PlayerName, adminPlayer.PlayerName, banTime)}");
                Logger?.LogInformation($"{PluginPrefix} {player.PlayerName} has been banned by {adminPlayer.PlayerName} until {banTime}.");
            }
            catch (Exception ex)
            {
                Logger?.LogError($"{PluginPrefix} Error banning player: {ex.Message}");
            }
        }
    }
}