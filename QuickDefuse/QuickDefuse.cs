global using Msg = SharedLibrary.Msg<global::QuickDefuse.QuickDefuse>;

﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using SharedLibrary;

namespace QuickDefuse
{
    public class QuickDefuse : BasePlugin
    {
        public override string ModuleName => "QuickDefuse";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "Allows you to defuse the bomb by cutting the correct wire. Use with MenuHotKey plugin, to choose menu options quikly";

        public readonly string PluginPrefix = $"[QuickDefuse]";
        private static Wire _rightWire = Wire.NotDefined;
        private static Wire _triedWire = Wire.NotDefined;
        private static CPlantedC4? _plantedBomb;
        private static CCSPlayerController? _planterPlayer = null;
        private static CCSPlayerController? _defuserPlayer = null;
        private static bool _isRoundEnded = false;

        enum Wire
        {
            NotDefined = 0,
            Green = 1,
            Yellow = 2,
            Red = 3,
            Blue = 4,
            Random = 5
        }

        public override void Load(bool hotReload)
        {
            var config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
            SharedLibrary.Localizer.Initialize(config.Language);

            RegisterEventHandler<EventBombBegindefuse>(OnBombBeginDefuse);
            RegisterEventHandler<EventBombAbortdefuse>(OnBombAbortDefuse);
            RegisterEventHandler<EventBombPlanted>(OnBombPlantedCommand);
            RegisterEventHandler<EventBombBeginplant>(OnBombBeginplant);
            RegisterEventHandler<EventBombAbortplant>(OnBombAbortPlant);
            RegisterEventHandler<EventBombExploded>(OnBombExploded);
            RegisterEventHandler<EventBombDefused>(OnBombDefused);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            _isRoundEnded = true;
            return HookResult.Continue;
        }

        private HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
        {
            if (_planterPlayer is not null)
            {
                MenuManager.GetActiveMenu(_planterPlayer)?.Close();
            }
            if (_defuserPlayer is not null)
            {
                MenuManager.GetActiveMenu(_defuserPlayer)?.Close();
            }
            _isRoundEnded = true;
            return HookResult.Continue;
        }

        private HookResult OnBombExploded(EventBombExploded @event, GameEventInfo info)
        {
            if (_planterPlayer is not null)
            {
                MenuManager.GetActiveMenu(_planterPlayer)?.Close();
            }
            if (_defuserPlayer is not null)
            {
                MenuManager.GetActiveMenu(_defuserPlayer)?.Close();
            }
            _isRoundEnded = true;
            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _rightWire = (Wire)new Random().Next(1, 5);
            _plantedBomb = null;
            _planterPlayer = null;
            _defuserPlayer = null;
            _isRoundEnded = false;

            return HookResult.Continue;
        }

        private void ShowSelectionMenu(CCSPlayerController player, bool isPlant)
        {
            if (_isRoundEnded && isPlant)
            {
                return;
            }

            int menuTimeoutSec = 10;
            var menu = new CenterHtmlMenu(Msg.Get("WireMenuTitle", menuTimeoutSec), this);

            menu.AddMenuOption(Msg.Get("WireGreen"), isPlant ? GreenPlantAction : GreenDefuseAction);
            menu.AddMenuOption(Msg.Get("WireYellow"), isPlant ? YellowPlantAction : YellowDefuseAction);
            menu.AddMenuOption(Msg.Get("WireRed"), isPlant ? RedPlantAction : RedDefuseAction);
            menu.AddMenuOption(Msg.Get("WireBlue"), isPlant ? BluePlantAction : BlueDefuseAction);
            menu.AddMenuOption(Msg.Get("WireRandom"), isPlant ? RandomPlantAction : RandomDefuseAction);
            MenuManager.OpenCenterHtmlMenu(this, player, menu);

            Task.Run(() =>
            {
                for (int i = menuTimeoutSec; i > 0; --i)
                {
                    menu.Title = Msg.Get("WireMenuTitle", i);
                    Task.Delay(1000).Wait();
                }
                MenuManager.CloseActiveMenu(player);
                if (isPlant)
                {
                    PrintYouChose(player, _rightWire);
                }
            });
        }

        private static char GetChatColor(Wire rightWire)
        {
            return rightWire switch
            {
                Wire.Green => ChatColors.Green,
                Wire.Yellow => ChatColors.Yellow,
                Wire.Red => ChatColors.Red,
                Wire.Blue => ChatColors.Blue,
                _ => ChatColors.Default,
            };
        }

        private static string GetWireName(Wire wire)
        {
            return wire switch
            {
                Wire.Green => Msg.Get("WireGreen"),
                Wire.Yellow => Msg.Get("WireYellow"),
                Wire.Red => Msg.Get("WireRed"),
                Wire.Blue => Msg.Get("WireBlue"),
                Wire.Random => Msg.Get("WireRandom"),
                _ => wire.ToString()
            };
        }

        #region Plant

        private HookResult OnBombBeginplant(EventBombBeginplant @event, GameEventInfo info)
        {
            _rightWire = (Wire)new Random().Next(1, 5);
            var player = @event.Userid;
            if (player == null || !player.IsValid)
                return HookResult.Continue;

            _planterPlayer = player;

            ShowSelectionMenu(player, true);

            return HookResult.Continue;
        }

        private HookResult OnBombPlantedCommand(EventBombPlanted @event, GameEventInfo info)
        {
            Server.PrintToChatAll(Msg.Get("BombDefusable"));
            return HookResult.Continue;
        }

        private static void GreenPlantAction(CCSPlayerController player, ChatMenuOption option)
        {
            _rightWire = Wire.Green;
            MenuManager.CloseActiveMenu(player);
            PrintYouChose(player, _rightWire);
        }

        private static void YellowPlantAction(CCSPlayerController player, ChatMenuOption option)
        {
            _rightWire = Wire.Yellow;
            MenuManager.CloseActiveMenu(player);
            PrintYouChose(player, _rightWire);
        }

        private static void RedPlantAction(CCSPlayerController player, ChatMenuOption option)
        {
            _rightWire = Wire.Red;
            MenuManager.CloseActiveMenu(player);
            PrintYouChose(player, _rightWire);
        }

        private static void BluePlantAction(CCSPlayerController player, ChatMenuOption option)
        {
            _rightWire = Wire.Blue;
            MenuManager.CloseActiveMenu(player);
            PrintYouChose(player, _rightWire);
        }

        private static void RandomPlantAction(CCSPlayerController player, ChatMenuOption option)
        {
            _rightWire = (Wire)new Random().Next(1, 5);
            MenuManager.CloseActiveMenu(player);
            PrintYouChose(player, _rightWire);
        }

        private static void PrintYouChose(CCSPlayerController player, Wire rightWire)
        {
            char color = GetChatColor(rightWire);
            player.PrintToChat(Msg.Get("WireChosen", $"{color}{GetWireName(rightWire)}{ChatColors.Default}"));
        }

        private HookResult OnBombAbortPlant(EventBombAbortplant @event, GameEventInfo info)
        {
            _planterPlayer = null;

            var player = @event.Userid;
            if (player == null || !player.IsValid)
            {
                return HookResult.Continue;
            }

            MenuManager.GetActiveMenu(player)?.Close();

            return HookResult.Continue;
        }

        #endregion

        #region Defuse
        private HookResult OnBombAbortDefuse(EventBombAbortdefuse @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid)
                return HookResult.Continue;

            _triedWire = Wire.NotDefined;
            _defuserPlayer = null;
            MenuManager.GetActiveMenu(player)?.Close();

            return HookResult.Continue;
        }

        private HookResult OnBombBeginDefuse(EventBombBegindefuse @event, GameEventInfo info)
        {
            if (@event.Userid == null || !@event.Userid.IsValid)
            {
                return HookResult.Continue;
            }

            _rightWire = _rightWire == Wire.NotDefined ? (Wire)new Random().Next(1, 5) : _rightWire;

            var player = @event.Userid;
            _defuserPlayer = player;
            _triedWire = Wire.NotDefined;
            _plantedBomb = FindPlantedBomb();
            if (_plantedBomb is null)
            {
                return HookResult.Continue;
            }

            ShowSelectionMenu(player, false);

            return HookResult.Continue;
        }

        private static void GreenDefuseAction(CCSPlayerController player, ChatMenuOption option)
        {
            if (_triedWire is Wire.NotDefined)
            {
                CutBombWire(Wire.Green, player);
            }
        }

        private static void YellowDefuseAction(CCSPlayerController player, ChatMenuOption option)
        {
            if (_triedWire is Wire.NotDefined)
            {
                CutBombWire(Wire.Yellow, player);
            }
        }

        private static void RedDefuseAction(CCSPlayerController player, ChatMenuOption option)
        {
            if (_triedWire is Wire.NotDefined)
            {
                CutBombWire(Wire.Red, player);
            }
        }

        private static void BlueDefuseAction(CCSPlayerController player, ChatMenuOption option)
        {
            if (_triedWire is Wire.NotDefined)
            {
                CutBombWire(Wire.Blue, player);
            }
        }

        private static void RandomDefuseAction(CCSPlayerController player, ChatMenuOption option)
        {
            if (_triedWire is Wire.NotDefined)
            {
                CutBombWire((Wire)new Random().Next(1, 5), player);
            }
        }

        private static void CutBombWire(Wire triedWire, CCSPlayerController player)
        {
            if (_plantedBomb is null)
            {
                return;
            }

            _triedWire = triedWire;
            if (_rightWire == _triedWire)
            {
                Server.NextFrame(() =>
                {
                    Server.PrintToChatAll($"{player.PlayerName}: " + 
                        Msg.Get("BombDefusedSuccess", $"{GetChatColor(_rightWire)}{GetWireName(_rightWire)}{ChatColors.Default}"));
                    _plantedBomb.DefuseCountDown = 0;
                    _plantedBomb.BombDefused = true;
                });
            }
            else
            {
                Server.PrintToChatAll($"{player.PlayerName}: " + 
                    Msg.Get("BombDefusedFailed",
                    $"{GetChatColor(_triedWire)}{GetWireName(_triedWire)}{ChatColors.Default}",
                    $"{GetChatColor(_rightWire)}{GetWireName(_rightWire)}{ChatColors.Default}"));
                _plantedBomb.CannotBeDefused = true;
                _plantedBomb.C4Blow = 1;
            }
        }

        private static CPlantedC4? FindPlantedBomb()
        {
            var plantedBombList = Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4");

            return !plantedBombList.Any() ? null : plantedBombList.FirstOrDefault();
        }
        #endregion

    }
}