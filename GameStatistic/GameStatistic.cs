global using Msg = SharedLibrary.Msg<global::GameStatistic.GameStatistic>;

﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using SharedLibrary;
using SharedLibrary.Entries;

namespace GameStatistic
{
    public class GameStatistic : BasePlugin
    {
        public override string ModuleName => "GameStatistic";
        public override string ModuleVersion => "2.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "Creates statistic about players and maps";

        public readonly string PluginPrefix = $"[GameStatistic]";

        private string _playerStatFilePath => Path.Combine(ModuleDirectory, StatisticHelper.PlayerStatFileName);
        private static string _mapStatFilePath = string.Empty;
        private static Dictionary<string, PlayerStatEntry> _playerStatEntries = [];
        private static bool _isWarmup = false;
        private static bool _isRoundEnded = false;
        private static Config _config = new();

        public override void Load(bool hotReload)
        {
            Utils.LogPrefix = PluginPrefix;
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
            RegisterEventHandler<EventStartHalftime>(OnStartHalftime);
            _mapStatFilePath = Path.Combine(ModuleDirectory, StatisticHelper.MapStatFileName);
            _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
            SharedLibrary.Localizer.Initialize(_config.Language);
        }

        public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
        {
            var player = Utilities.GetPlayerFromUserid(@event.Userid);

            if (player is null || !player.IsValid)
            {
                return HookResult.Continue;
            }

            if (@event?.Text.Trim().ToLower() is "!mapstat")
            {
                StatisticHelper.PrintMapStat(_mapStatFilePath, false);
            }
            else if (@event?.Text.Trim().ToLower() is "!mystat")
            {
                PrintPlayerStat(player);
            }
            else if (@event?.Text.Trim().ToLower() is "!top" && _config.EnableTopBottomCommand)
            {
                PrintTop(player);
            }
            else if (@event?.Text.Trim().ToLower() is "!bottom" && _config.EnableTopBottomCommand)
            {
                PrintTop(player, true);
            }
            else if (@event?.Text.Trim().ToLower() is "!teamstat")
            {
                StatisticHelper.PrintTeamStat(StatisticHelper.LoadMonthsStats(ModuleDirectory, _config.DateRangeForStatisticsInMonth));
            }
            else if (@event?.Text.Trim().ToLower() is "!chance")
            {
                StatisticHelper.PrintTeamStat(StatisticHelper.LoadMonthsStats(ModuleDirectory, _config.DateRangeForStatisticsInMonth), true);
            }
            else if (@event?.Text.Trim().ToLower() is "!help")
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("HelpCommands")}");
            }
            else if (@event?.Text.Trim().ToLower() is "!reload")
            {
                string adminsFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "admins.json");
                if (PlayerHelper.GetAdminLevel(player, adminsFilePath) > 2)
                {
                    _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
                    SharedLibrary.Localizer.Initialize(_config.Language);
                    player.PrintToChat($"{PluginPrefix} {Msg.Get("ConfigsReloaded")}");
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnStartHalftime(EventStartHalftime @event, GameEventInfo info)
        {
            StatisticHelper.PrintMapStat(_mapStatFilePath, false);
            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _isRoundEnded = false;
            return HookResult.Continue;
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            _isWarmup = true;
            _config = Config.LoadConfig(Path.Combine(ModuleDirectory, "config.json"));
            SharedLibrary.Localizer.Initialize(_config.Language);
            _mapStatFilePath = Path.Combine(ModuleDirectory, StatisticHelper.MapStatFileName);
            return HookResult.Continue;
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _isWarmup = false;
            StatisticHelper.PrintMapStat(_mapStatFilePath, true);
            StatisticHelper.PrintTeamStat(StatisticHelper.LoadMonthsStats(ModuleDirectory, _config.DateRangeForStatisticsInMonth));
            return HookResult.Continue;
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            if (_isWarmup || _isRoundEnded || !(PlayerHelper.GetAllNonSpecPlayers().Count() >= _config.MinimumPlayerCountToStatistic))
            {
                return HookResult.Continue;
            }

            var attacker = @event.Attacker;
            var victim = @event.Userid;
            var assister = @event.Assister;

            if (victim is null || attacker is null || victim.AuthorizedSteamID is null || attacker.AuthorizedSteamID is null || attacker.IsBot || victim.IsBot)
            {
                return HookResult.Continue;
            }

            var attackerSteamId = Utils.NormalizeSteamId2(attacker.AuthorizedSteamID.SteamId2);
            var victimSteamId = Utils.NormalizeSteamId2(victim.AuthorizedSteamID.SteamId2);
            var assisterSteamId = assister?.AuthorizedSteamID?.SteamId2 is string aid ? Utils.NormalizeSteamId2(aid) : null;

            if (assister is not null && assister.AuthorizedSteamID is not null && assisterSteamId is not null)
            {
                if (!_playerStatEntries.ContainsKey(assisterSteamId))
                {
                    _playerStatEntries[assisterSteamId] = new PlayerStatEntry(assisterSteamId, assister.PlayerName);
                }
                _playerStatEntries[assisterSteamId].Assister++;
            }

            if (attackerSteamId is not null && victimSteamId is not null)
            {
                if (attackerSteamId != victimSteamId)
                {

                    if (!_playerStatEntries.ContainsKey(victimSteamId))
                    {
                        _playerStatEntries[victimSteamId] = new PlayerStatEntry(victimSteamId, victim.PlayerName);
                    }
                    _playerStatEntries[victimSteamId].Dead++;


                    if (!_playerStatEntries.ContainsKey(attackerSteamId))
                    {
                        _playerStatEntries[attackerSteamId] = new PlayerStatEntry(attackerSteamId, attacker.PlayerName);
                    }
                    if (attacker.Team == victim.Team)
                    {
                        _playerStatEntries[attackerSteamId].TeamKill++;
                    }
                    else
                    {
                        _playerStatEntries[attackerSteamId].Kill++;
                    }
                }
                else
                {
                    if (!_playerStatEntries.ContainsKey(victimSteamId))
                    {
                        _playerStatEntries[victimSteamId] = new PlayerStatEntry(victimSteamId, victim.PlayerName);
                    }
                    _playerStatEntries[victimSteamId].SelfKill++;
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            _isRoundEnded = true;

            if (!_isWarmup && PlayerHelper.GetAllNonSpecPlayers().Count() >= _config.MinimumPlayerCountToStatistic)
            {
                CreatePlayerStatistic();
                CreateMapStatisticRoundEnd(@event.Winner);
            }

            return HookResult.Continue;
        }

        private void PrintPlayerStat(CCSPlayerController player)
        {
            var storedStats =
                StatisticHelper.LoadMonthsStats(ModuleDirectory, _config.DateRangeForStatisticsInMonth)
                ?? [];

            var playerSteamId = player.AuthorizedSteamID?.SteamId2 is string sid ? Utils.NormalizeSteamId2(sid) : null;
            if (playerSteamId is not null && storedStats.ContainsKey(playerSteamId))
            {
                var playerEntry = storedStats[playerSteamId];
                player?.PrintToChat(Msg.Get("PlayerStatHeader", _config.DateRangeForStatisticsInMonth));
                player?.PrintToChat(Msg.Get("PlayerStatDetails", playerEntry?.Kill, playerEntry?.Dead, playerEntry?.Assister, playerEntry?.SelfKill, playerEntry?.TeamKill));
            }
        }

        private void PrintTop(CCSPlayerController player, bool isReverse = false)
        {
            List<KeyValuePair<string, PlayerStatEntry>> storedStats =
                StatisticHelper.LoadMonthsStats(ModuleDirectory, _config.DateRangeForStatisticsInMonth)
                .Where(p => p.Value.Kill > _config.MinimumKillCountToShowInTop)
                .OrderByDescending(p => p.Value.Score).ToList()
                ?? [];

            if (isReverse) {
                storedStats = storedStats.OrderBy(p => p.Value.Score).ToList();
            }

            if (storedStats != null && storedStats.Count != 0)
            {
                string topList = string.Empty;

                if (storedStats.Count < 10)
                {
                    for (int i = 0; i < storedStats.Count; i++)
                    {
                        topList += $"{i + 1}. {storedStats[i].Value.Name}({storedStats[i].Value.Score:F2}), ";
                    }
                }
                else
                {
                    for (int i = 0; i < 10; i++)
                    {
                        topList += $"{i + 1}. {storedStats[i].Value.Name}({storedStats[i].Value.Score:F2}), ";
                    }
                }

                Server.PrintToChatAll($"{PluginPrefix} {topList}");

                for (int i = 0; i < storedStats.Count - 1; i++)
                {
                    if (storedStats[i].Key == Utils.NormalizeSteamId2(player.AuthorizedSteamID?.SteamId2 ?? string.Empty))
                    {
                        player.PrintToChat(Msg.Get("CurrentPosition", i + 1));
                        break;
                    }
                }
            }
        }

        private static void CreateMapStatisticRoundEnd(int winnerTeam)
        {
            var storedStats =
                Utils.ReadStoredStat<Dictionary<string, MapStatEntry>>(_mapStatFilePath)
                ?? [];
            string mapName = Server.MapName.Trim() ?? string.Empty;

            if (storedStats.ContainsKey(mapName))
            {
                storedStats[mapName].LastPlayed = Utils.GetServerTime();
            }
            else
            {
                storedStats[mapName] = new MapStatEntry(mapName);
            }

            if (winnerTeam == 2)
            {
                ++storedStats[mapName].TWin;
            }
            else if (winnerTeam == 3)
            {
                ++storedStats[mapName].CTWin;
            }

            Utils.WriteToFile<Dictionary<string, MapStatEntry>>(storedStats, _mapStatFilePath);
        }

        private void CreatePlayerStatistic()
        {
            var storedStats =
                Utils.ReadStoredStat<Dictionary<string, PlayerStatEntry>>(_playerStatFilePath)
                ?? [];

            foreach (var kvp in _playerStatEntries)
            {
                if (storedStats.ContainsKey(kvp.Key))
                {
                    var existing = storedStats[kvp.Key];
                    existing.Identity = kvp.Key;
                    existing.Kill += kvp.Value.Kill;
                    existing.Dead += kvp.Value.Dead;
                    existing.Assister += kvp.Value.Assister;
                    existing.SelfKill += kvp.Value.SelfKill;
                    existing.TeamKill += kvp.Value.TeamKill;
                }
                else
                {
                    storedStats[kvp.Key] = kvp.Value;
                }
            }

            Utils.WriteToFile(storedStats, _playerStatFilePath);

            _playerStatEntries.Clear();
        }
    }
}
