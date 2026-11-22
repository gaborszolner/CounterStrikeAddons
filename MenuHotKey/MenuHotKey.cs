global using Msg = SharedLibrary.Msg<global::MenuHotKey.MenuHotKey>;

﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using SharedLibrary;

namespace MenuHotKey
{
    public class MenuHotKey : BasePlugin
    {
        public override string ModuleName => "MenuHotKey";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "HotKey for all menu, needs to bind";

        public readonly string PluginPrefix = $"[MenuHotkey]";


        public override void Load(bool hotReload)
        {
            var config = ConfigLoader.Load<MenuHotKeyConfig>(ModuleDirectory, "MenuHotKeyConfig.json");
            SharedLibrary.Localizer.Initialize(config.Language);

            RegisterEventHandler<EventPlayerConnect>(OnPlayerConnectFull);
            AddCommandListener("1", OnKeyPress);
            AddCommandListener("2", OnKeyPress);
            AddCommandListener("3", OnKeyPress);
            AddCommandListener("4", OnKeyPress);
            AddCommandListener("5", OnKeyPress);
            AddCommandListener("6", OnKeyPress);
            AddCommandListener("7", OnKeyPress);
            AddCommandListener("8", OnKeyPress);
            AddCommandListener("9", OnKeyPress);
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnect @event, GameEventInfo info)
        {
            var player = @event.Userid;

            if (player is null)
            {
                return HookResult.Continue;
            }

            player?.PrintToChat(Msg.Get("BindInstruction1"));
            player?.PrintToChat(Msg.Get("BindInstruction2"));

            return HookResult.Continue;
        }

        private HookResult OnKeyPress(CCSPlayerController? player, CommandInfo command)
        {
            if (player is not null)
            {
                var menu = MenuManager.GetActiveMenu(player);

                if (menu is null)
                {
                    return HookResult.Continue;
                }

                switch (command.GetCommandString)
                {
                    case "1": menu.OnKeyPress(player, 1); break;
                    case "2": menu.OnKeyPress(player, 2); break;
                    case "3": menu.OnKeyPress(player, 3); break;
                    case "4": menu.OnKeyPress(player, 4); break;
                    case "5": menu.OnKeyPress(player, 5); break;
                    case "6": menu.OnKeyPress(player, 6); break;
                    case "7": menu.OnKeyPress(player, 7); break;
                    case "8": menu.OnKeyPress(player, 8); break;
                    case "9": menu.OnKeyPress(player, 9); break;
                }
            }

            return HookResult.Continue;
        }
    }
}
