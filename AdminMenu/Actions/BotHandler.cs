using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void BotHandlerAction(CCSPlayerController adminPlayer, ChatMenuOption option)
        {
            var botMenu = new CenterHtmlMenu(Msg.Get("BotMenuTitle"), this);

            botMenu.AddMenuOption(Msg.Get("BotKickAll"), (controller, _) =>
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("AllBotsKicked", adminPlayer.PlayerName)}");
                Server.ExecuteCommand("bot_kick all");
            });
            botMenu.AddMenuOption(Msg.Get("BotAddT"), (controller, _) =>
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("TerroristBotAdded", adminPlayer.PlayerName)}");
                Server.ExecuteCommand("bot_add_t");
            });
            botMenu.AddMenuOption(Msg.Get("BotAddCT"), (controller, _) =>
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("CTBotAdded", adminPlayer.PlayerName)}");
                Server.ExecuteCommand("bot_add_ct");
            });

            MenuManager.OpenCenterHtmlMenu(this, adminPlayer, botMenu);
        }
    }
}
