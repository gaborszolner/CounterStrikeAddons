global using Msg = SharedLibrary.Msg<global::RockTheVote.RockTheVote>;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using SharedLibrary;
using System.Text.Json;

namespace RockTheVote
{
    public class RockTheVote : BasePlugin
    {
        public override string ModuleName => "RockTheVote";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Sinistral";
        public override string ModuleDescription => "Rock the vote, map nomination and end of map vote";

        public readonly string PluginPrefix = "[RockTheVote]";

        private const string ConfigFileName = "rtvConfig.json";
        private const string MapListFileName = "maplist.txt";
        private const float MapChangeDelaySeconds = 5.0f;

        private static RtvConfig _config = new();
        private static string _adminsFilePath = string.Empty;
        private static string _mapListFilePath = string.Empty;
        private static bool _isWarmup = false;

        private static string? _nextMap = null;
        private static readonly Dictionary<ulong, string> _nominatedMaps = [];
        private static readonly HashSet<ulong> _rtvPlayers = [];
        private static readonly List<string> _recentMaps = [];

        private static bool _voteInProgress = false;
        private static bool _rtvTriggered = false;
        private static bool _endOfMapVoteStarted = false;
        private static bool _isRtvVote = false;
        private static bool _changeMapAfterVote = false;
        private static bool _mapChangeScheduled = false;
        private static List<string> _voteOptions = [];
        private static readonly Dictionary<ulong, (string Map, long Order)> _mapVotes = [];
        private static long _voteSequence = 0;
        private static CounterStrikeSharp.API.Modules.Timers.Timer? _voteTimer = null;

        public override void Load(bool hotReload)
        {
            _config = ConfigLoader.Load<RtvConfig>(ModuleDirectory, ConfigFileName, migrateSharedConfig: false);
            _adminsFilePath = Path.Combine(ModuleDirectory, "..", "..", "configs", "admins.json");
            _mapListFilePath = Path.Combine(ModuleDirectory, MapListFileName);
            SharedLibrary.Localizer.Initialize(_config.Language);
            SortMapList();

            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundAnnounceWarmup>(OnRoundAnnounceWarmup);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            RegisterEventHandler<EventCsWinPanelMatch>(OnMatchEnd);

            if (hotReload)
            {
                OnMapStart(Server.MapName);
            }
        }

        private void OnMapStart(string mapName)
        {
            _nextMap = null;
            _nominatedMaps.Clear();
            _rtvPlayers.Clear();
            _mapVotes.Clear();
            _voteOptions = [];
            _voteInProgress = false;
            _voteTimer = null;
            _rtvTriggered = false;
            _endOfMapVoteStarted = false;
            _isRtvVote = false;
            _changeMapAfterVote = false;
            _mapChangeScheduled = false;

            if (string.IsNullOrWhiteSpace(mapName))
                return;

            _recentMaps.RemoveAll(m => m.Equals(mapName, StringComparison.OrdinalIgnoreCase));
            _recentMaps.Insert(0, mapName);

            // The current map is always excluded, so keep it plus the configured number of previous maps
            int keep = Math.Max(0, _config.MapsInCoolDown) + 1;
            if (_recentMaps.Count > keep)
                _recentMaps.RemoveRange(keep, _recentMaps.Count - keep);
        }

        private HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
        {
            var player = Utilities.GetPlayerFromUserid(@event.Userid);
            if (player is null || !player.IsValid || player.IsBot)
                return HookResult.Continue;

            var parts = (@event.Text ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return HookResult.Continue;

            string command = parts[0].ToLowerInvariant();
            string argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            switch (command)
            {
                case "!rtv":
                case "rtv":
                    CallRtv(player);
                    break;
                case "!nominate":
                case "nominate":
                    Nominate(player, argument);
                    break;
                case "!nextmap":
                case "nextmap":
                    ShowNextMap();
                    break;
                case "!setnextmap":
                case "setnextmap":
                    SetNextMap(player, argument);
                    break;
                case "!revote":
                case "revote":
                    Revote(player);
                    break;
                case "!reload":
                    ReloadConfig(player);
                    break;
            }

            return HookResult.Continue;
        }

        private void ReloadConfig(CCSPlayerController player)
        {
            if (PlayerHelper.GetAdminLevel(player, _adminsFilePath) <= 2)
                return;

            _config = ConfigLoader.Load<RtvConfig>(ModuleDirectory, ConfigFileName, migrateSharedConfig: false);
            SharedLibrary.Localizer.Initialize(_config.Language);
            SortMapList();
            player.PrintToChat($"{PluginPrefix} {Msg.Get("ConfigsReloaded")}");
        }

        private void SortMapList()
        {
            try
            {
                var statisticsFile = Path.Combine(ModuleDirectory, "..", "GameStatistic", StatisticHelper.MapStatFileName);
                MapListSorter.Sort(_mapListFilePath, statisticsFile, _config.MapSortOrder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                Logger.LogError(ex, "{PluginPrefix} Failed to sort map list using MapSortOrder {MapSortOrder}", PluginPrefix, _config.MapSortOrder);
            }
        }

        #region RTV

        private void CallRtv(CCSPlayerController player)
        {
            if (!_config.EnableRtv)
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("RtvDisabled")}");
                return;
            }
            if (_isWarmup && !_config.EnableRtvInWarmup)
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("RtvWarmupDisabled")}");
                return;
            }
            if (_rtvTriggered || _mapChangeScheduled)
            {
                int requiredVotes = GetRequiredRtvCount(PlayerHelper.GetAllPlayers().Count());
                player.PrintToChat($"{PluginPrefix} {Msg.Get("RtvAlreadyTriggered", _rtvPlayers.Count, requiredVotes)}");
                return;
            }
            if (_voteInProgress)
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteInProgress")}");
                return;
            }

            int required = GetRequiredRtvCount(PlayerHelper.GetAllPlayers().Count());

            if (!_rtvPlayers.Add(player.SteamID))
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("RtvAlreadyVoted", _rtvPlayers.Count, required)}");
                return;
            }

            Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("RtvPlayerVoted", player.PlayerName, _rtvPlayers.Count, required)}");
            CheckRtvThreshold();
        }

        private void CheckRtvThreshold(ulong? leavingSteamId = null)
        {
            if (_rtvTriggered || _voteInProgress || _mapChangeScheduled || _rtvPlayers.Count == 0)
                return;

            int playerCount = PlayerHelper.GetAllPlayers().Count(p => p.SteamID != leavingSteamId);
            if (playerCount == 0 || _rtvPlayers.Count < GetRequiredRtvCount(playerCount))
                return;

            _rtvTriggered = true;

            // The next map is already decided (e.g. by !setnextmap), so there is nothing to vote on
            if (_nextMap is not null)
            {
                Server.PrintToChatAll($"{PluginPrefix} {ChatColors.Green}{Msg.Get("RtvSuccessNextMapSet", _nextMap)}{ChatColors.Default}");
                ScheduleMapChange(_nextMap, MapChangeDelaySeconds);
                return;
            }

            Server.PrintToChatAll($"{PluginPrefix} {ChatColors.Green}{Msg.Get("RtvSuccess")}{ChatColors.Default}");
            StartMapVote(isRtvVote: true, _config.ChangeMapImmediatelyAfterRtv);
        }

        private static int GetRequiredRtvCount(int playerCount)
        {
            int percentage = Math.Clamp(_config.MinimumRtvPercentage, 0, 100);
            return Math.Max(1, (int)Math.Ceiling(playerCount * percentage / 100.0));
        }

        #endregion

        #region Nominate

        private void Nominate(CCSPlayerController player, string mapFilter)
        {
            if (!_config.EnableNominate)
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("NominateDisabled")}");
                return;
            }
            if (_isWarmup && !_config.EnableNominateInWarmup)
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("NominateWarmupDisabled")}");
                return;
            }
            if (_voteInProgress)
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteInProgress")}");
                return;
            }
            if (_nextMap is not null)
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("NextMapAlreadyDecided", _nextMap)}");
                return;
            }

            var maps = FilterMaps(GetAvailableMaps(), mapFilter);
            if (maps.Count == 0)
            {
                player.PrintToChat($"{PluginPrefix} {(string.IsNullOrEmpty(mapFilter) ? Msg.Get("NoMapsAvailable") : Msg.Get("MapNotFound", mapFilter))}");
                return;
            }

            if (maps.Count == 1 && !string.IsNullOrEmpty(mapFilter))
            {
                AddNomination(player, maps[0]);
                return;
            }

            OpenMapMenu(player, Msg.Get("NominateMenuTitle"), maps, AddNomination);
        }

        private void AddNomination(CCSPlayerController player, string map)
        {
            if (!player.IsValid || _voteInProgress || _nextMap is not null)
                return;

            if (!GetAvailableMaps().Contains(map, StringComparer.OrdinalIgnoreCase))
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("MapNotFound", map)}");
                return;
            }

            int maxNominations = GetMapsToShow();
            if (!CanAddNomination(_nominatedMaps, player.SteamID, map, maxNominations))
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("NominationLimitReached", maxNominations)}");
                return;
            }

            _nominatedMaps[player.SteamID] = map;
            Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("PlayerNominated", player.PlayerName, map)}");
        }

        public static bool CanAddNomination(IReadOnlyDictionary<ulong, string> nominations, ulong steamId, string map, int maxNominations)
        {
            var otherNominatedMaps = nominations
                .Where(n => n.Key != steamId)
                .Select(n => n.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return otherNominatedMaps.Contains(map) || otherNominatedMaps.Count < maxNominations;
        }

        private static int GetMapsToShow() => Math.Max(2, _config.MapsToShow);

        #endregion

        #region Next map

        private void ShowNextMap()
        {
            if (_nextMap is not null)
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("NextMapIs", _nextMap)}");
            else if (_voteInProgress)
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("VoteInProgress")}");
            else
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("NextMapNotDecided")}");
        }

        private void SetNextMap(CCSPlayerController player, string mapFilter)
        {
            if (PlayerHelper.GetAdminLevel(player, _adminsFilePath) < 3)
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("NoPermission")}");
                return;
            }
            if (_voteInProgress)
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteInProgress")}");
                return;
            }

            var maps = FilterMaps(ReadMapsFromFile().Keys.ToList(), mapFilter);
            if (maps.Count == 0)
            {
                player.PrintToChat($"{PluginPrefix} {(string.IsNullOrEmpty(mapFilter) ? Msg.Get("NoMapsAvailable") : Msg.Get("MapNotFound", mapFilter))}");
                return;
            }

            if (maps.Count == 1 && !string.IsNullOrEmpty(mapFilter))
            {
                ApplyAdminNextMap(player, maps[0]);
                return;
            }

            OpenMapMenu(player, Msg.Get("SetNextMapMenuTitle"), maps, ApplyAdminNextMap);
        }

        private void ApplyAdminNextMap(CCSPlayerController player, string map)
        {
            if (!player.IsValid || _voteInProgress)
                return;

            _nextMap = map;
            Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("NextMapSetByAdmin", player.PlayerName, map)}");
        }

        #endregion

        #region Map vote

        private void StartMapVote(bool isRtvVote, bool changeMapAfterVote)
        {
            if (_voteInProgress)
                return;

            var options = BuildVoteOptions();
            if (options.Count == 0)
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("NoMapsAvailable")}");
                return;
            }

            _isRtvVote = isRtvVote;
            _changeMapAfterVote = changeMapAfterVote;

            if (options.Count == 1)
            {
                SetVoteWinner(options[0]);
                return;
            }

            _voteInProgress = true;
            _voteOptions = options;
            _mapVotes.Clear();
            _voteSequence = 0;

            // Nomination votes are cast at vote start, so they count as the earliest votes
            foreach (var (steamId, map) in GetNominationVotes(_nominatedMaps, options))
            {
                _mapVotes[steamId] = (map, 0);
            }

            int seconds = Math.Max(5, _config.VotingSecondLimit);
            Server.PrintToChatAll($"{PluginPrefix} {ChatColors.Green}{Msg.Get("VoteStarted", seconds)}{ChatColors.Default}");

            foreach (var player in PlayerHelper.GetAllPlayers())
            {
                if (_mapVotes.TryGetValue(player.SteamID, out var vote))
                {
                    player.PrintToChat($"{PluginPrefix} {Msg.Get("AutoVotedForNomination", vote.Map)}");
                    continue;
                }

                ShowVoteMenu(player);
            }

            _voteTimer = AddTimer(seconds, EndMapVote, TimerFlags.STOP_ON_MAPCHANGE);
            EndMapVoteIfAllVoted();
        }

        public static bool HaveAllPlayersVoted(IEnumerable<ulong> playerIds, IEnumerable<ulong> voterIds)
        {
            var voters = voterIds.ToHashSet();
            var players = playerIds.ToList();
            return players.Count > 0 && players.All(voters.Contains);
        }

        // The winner is certain when no other map can overtake it even if every remaining player votes for it;
        // reaching the same count later still loses the tie-break
        public static bool IsVoteDecided(IReadOnlyCollection<(string Map, long Order)> votes, int remainingVoters)
        {
            var winners = GetWinningMaps(votes);
            if (winners.Count != 1)
                return false;

            int leaderCount = votes.Count(v => v.Map.Equals(winners[0], StringComparison.OrdinalIgnoreCase));
            int runnerUpCount = votes
                .Where(v => !v.Map.Equals(winners[0], StringComparison.OrdinalIgnoreCase))
                .GroupBy(v => v.Map, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Count())
                .DefaultIfEmpty(0)
                .Max();

            return leaderCount >= runnerUpCount + remainingVoters;
        }

        private void EndMapVoteIfAllVoted(ulong? leavingSteamId = null)
        {
            if (!_voteInProgress)
                return;

            var playerIds = PlayerHelper.GetAllPlayers().Select(p => p.SteamID).Where(id => id != leavingSteamId).ToList();
            if (HaveAllPlayersVoted(playerIds, _mapVotes.Keys))
            {
                EndMapVote();
                return;
            }

            if (_isRtvVote)
            {
                var votes = _mapVotes.Values.Where(v => _voteOptions.Contains(v.Map, StringComparer.OrdinalIgnoreCase)).ToList();
                int remainingVoters = playerIds.Count(id => !_mapVotes.ContainsKey(id));
                if (IsVoteDecided(votes, remainingVoters))
                    EndMapVote();
            }
        }

        public static Dictionary<ulong, string> GetNominationVotes(IReadOnlyDictionary<ulong, string> nominations, IReadOnlyList<string> voteOptions)
        {
            var votes = new Dictionary<ulong, string>();
            foreach (var (steamId, nominatedMap) in nominations)
            {
                var option = voteOptions.FirstOrDefault(o => o.Equals(nominatedMap, StringComparison.OrdinalIgnoreCase));
                if (option is not null)
                    votes[steamId] = option;
            }
            return votes;
        }

        private List<string> BuildVoteOptions()
        {
            var available = GetAvailableMaps();
            int mapsToShow = GetMapsToShow();

            var options = _nominatedMaps.Values
                .Where(m => available.Contains(m, StringComparer.OrdinalIgnoreCase))
                .GroupBy(m => m, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(mapsToShow)
                .ToList();

            var randomMaps = available
                .Where(m => !options.Contains(m, StringComparer.OrdinalIgnoreCase))
                .OrderBy(_ => Random.Shared.Next())
                .Take(mapsToShow - options.Count);

            options.AddRange(randomMaps);
            return options;
        }

        private void ShowVoteMenu(CCSPlayerController player)
        {
            try
            {
                var menu = new CenterHtmlMenu(Msg.Get("VoteMenuTitle"), this)
                {
                    PostSelectAction = _config.CloseVotingMenuAfterVote ? PostSelectAction.Close : PostSelectAction.Nothing
                };

                foreach (var map in _voteOptions)
                {
                    menu.AddMenuOption(map, (controller, _) =>
                    {
                        if (!_voteInProgress || !controller.IsValid)
                            return;

                        // Re-selecting the same map keeps the original vote time
                        if (!_mapVotes.TryGetValue(controller.SteamID, out var existing) || !existing.Map.Equals(map, StringComparison.OrdinalIgnoreCase))
                            _mapVotes[controller.SteamID] = (map, ++_voteSequence);
                        controller.PrintToChat($"{PluginPrefix} {Msg.Get("YouVotedFor", map)}");
                        EndMapVoteIfAllVoted();
                    });
                }

                MenuManager.OpenCenterHtmlMenu(this, player, menu);
            }
            catch (Exception ex)
            {
                Logger?.LogError($"{PluginPrefix} Map vote menu error: {ex.Message}");
            }
        }

        private void Revote(CCSPlayerController player)
        {
            if (!_voteInProgress)
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("NoActiveVote")}");
                return;
            }

            if (_mapVotes.Remove(player.SteamID))
            {
                player.PrintToChat($"{PluginPrefix} {Msg.Get("VoteRemoved")}");
            }

            ShowVoteMenu(player);
        }

        private void EndMapVote()
        {
            if (!_voteInProgress)
                return;

            _voteInProgress = false;
            _voteTimer?.Kill();
            _voteTimer = null;

            foreach (var player in PlayerHelper.GetAllPlayers())
            {
                MenuManager.GetActiveMenu(player)?.Close();
            }

            var results = _voteOptions.ToDictionary(m => m, _ => 0, StringComparer.OrdinalIgnoreCase);
            foreach (var vote in _mapVotes.Values)
            {
                if (results.ContainsKey(vote.Map))
                    results[vote.Map]++;
            }

            string winner;
            int maxVotes = results.Values.DefaultIfEmpty(0).Max();

            string resultsHeader = Msg.Get(_isRtvVote ? "RtvVoteResults" : "EndOfMapVoteResults");
            Server.PrintToChatAll($"{PluginPrefix} {ChatColors.Green}=== {resultsHeader} ==={ChatColors.Default}");

            if (maxVotes == 0)
            {
                Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("NoVotes")}");
                winner = _voteOptions[Random.Shared.Next(_voteOptions.Count)];
            }
            else
            {
                foreach (var result in results.Where(r => r.Value > 0).OrderByDescending(r => r.Value))
                {
                    Server.PrintToChatAll(Msg.Get("VoteResult", result.Key, result.Value));
                }

                var winners = GetWinningMaps(_mapVotes.Values.Where(v => results.ContainsKey(v.Map)));
                winner = winners[Random.Shared.Next(winners.Count)];
            }

            _mapVotes.Clear();
            _voteOptions = [];
            SetVoteWinner(winner);
        }

        // Most votes wins; on a tie the map whose last counted vote came first (reached the count first) wins
        public static List<string> GetWinningMaps(IEnumerable<(string Map, long Order)> votes)
        {
            var tally = votes
                .GroupBy(v => v.Map, StringComparer.OrdinalIgnoreCase)
                .Select(g => (Map: g.Key, Count: g.Count(), ReachedAt: g.Max(v => v.Order)))
                .ToList();

            if (tally.Count == 0)
                return [];

            int maxCount = tally.Max(t => t.Count);
            var leaders = tally.Where(t => t.Count == maxCount).ToList();
            long earliest = leaders.Min(t => t.ReachedAt);

            return leaders.Where(t => t.ReachedAt == earliest).Select(t => t.Map).ToList();
        }

        private void SetVoteWinner(string map)
        {
            _nextMap = map;
            Server.PrintToChatAll($"{PluginPrefix} {ChatColors.Green}{Msg.Get("NextMapChosen", map)}{ChatColors.Default}");

            if (_changeMapAfterVote)
            {
                ScheduleMapChange(map, MapChangeDelaySeconds);
            }
        }

        #endregion

        #region Map change

        private HookResult OnMatchEnd(EventCsWinPanelMatch @event, GameEventInfo info)
        {
            if (_nextMap is null || _mapChangeScheduled)
                return HookResult.Continue;

            float restartDelay = GetConVarValue("mp_match_restart_delay", 10);
            ScheduleMapChange(_nextMap, Math.Clamp(restartDelay - 1.0f, 1.0f, 30.0f));
            return HookResult.Continue;
        }

        private void ScheduleMapChange(string map, float delaySeconds)
        {
            _mapChangeScheduled = true;
            Server.PrintToChatAll($"{PluginPrefix} {Msg.Get("ChangingMap", map, (int)Math.Ceiling(delaySeconds))}");
            AddTimer(delaySeconds, () => ChangeMap(map), TimerFlags.STOP_ON_MAPCHANGE);
        }

        private void ChangeMap(string map)
        {
            ReadMapsFromFile().TryGetValue(map, out var workshopId);

            if (Server.IsMapValid(map))
            {
                Server.ExecuteCommand($"changelevel {map}");
            }
            else if (!string.IsNullOrWhiteSpace(workshopId))
            {
                Server.ExecuteCommand($"host_workshop_map {workshopId}");
            }
            else
            {
                Server.ExecuteCommand($"ds_workshop_changelevel {map}");
            }
        }

        #endregion

        #region Round / warmup events

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            SortMapList();
            if (_isWarmup || !_config.EnableEndOfMapVote || _endOfMapVoteStarted || _voteInProgress || _nextMap is not null)
                return HookResult.Continue;

            if (IsMatchEndingWithinConfiguredRounds())
            {
                _endOfMapVoteStarted = true;
                StartMapVote(isRtvVote: false, changeMapAfterVote: false);
            }

            return HookResult.Continue;
        }

        private static bool IsMatchEndingWithinConfiguredRounds()
        {
            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            if (gameRules is null || gameRules.WarmupPeriod)
                return false;

            int maxRounds = GetConVarValue("mp_maxrounds", 0);
            bool canClinch = GetConVarValue("mp_match_can_clinch", true);

            int highestScore = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager")
                .Where(t => t.TeamNum == (byte)CsTeam.Terrorist || t.TeamNum == (byte)CsTeam.CounterTerrorist)
                .Select(t => t.Score)
                .DefaultIfEmpty(0)
                .Max();

            return ShouldStartEndOfMapVote(maxRounds, gameRules.TotalRoundsPlayed, canClinch, highestScore, _config.EndOfMapVoteRoundsBeforeEnd);
        }

        public static bool ShouldStartEndOfMapVote(int maxRounds, int roundsPlayed, bool canClinch, int highestScore, int roundsBeforeEnd)
        {
            if (maxRounds <= 0)
                return false;

            int rounds = Math.Max(1, roundsBeforeEnd);

            if (maxRounds - roundsPlayed <= rounds)
                return true;

            if (!canClinch)
                return false;

            int winsNeededToClinch = maxRounds / 2 + 1 - highestScore;
            return winsNeededToClinch <= rounds;
        }

        private HookResult OnRoundAnnounceWarmup(EventRoundAnnounceWarmup @event, GameEventInfo info)
        {
            _isWarmup = true;
            return HookResult.Continue;
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _isWarmup = false;
            return HookResult.Continue;
        }

        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player is null || player.IsBot)
                return HookResult.Continue;

            ulong steamId = player.SteamID;
            _rtvPlayers.Remove(steamId);
            _nominatedMaps.Remove(steamId);
            _mapVotes.Remove(steamId);

            CheckRtvThreshold(steamId);
            EndMapVoteIfAllVoted(steamId);
            return HookResult.Continue;
        }

        #endregion

        #region Helpers

        private void OpenMapMenu(CCSPlayerController player, string title, List<string> maps, Action<CCSPlayerController, string> onSelect)
        {
            var menu = new CenterHtmlMenu(title, this)
            {
                PostSelectAction = PostSelectAction.Close
            };

            foreach (var map in maps)
            {
                menu.AddMenuOption(map, (controller, _) => onSelect(controller, map));
            }

            MenuManager.OpenCenterHtmlMenu(this, player, menu);
        }

        private static List<string> FilterMaps(List<string> maps, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return maps;

            var exact = maps.FirstOrDefault(m => m.Equals(filter, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
                return [exact];

            return maps.Where(m => m.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private List<string> GetAvailableMaps()
        {
            return ReadMapsFromFile().Keys
                .Where(m => !_recentMaps.Contains(m, StringComparer.OrdinalIgnoreCase))
                .Where(m => !m.Equals(Server.MapName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private Dictionary<string, string?> ReadMapsFromFile()
        {
            Dictionary<string, string?> mapList = new(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_mapListFilePath))
            {
                Logger?.LogError($"{PluginPrefix} Map list file not found: {_mapListFilePath}");
                return mapList;
            }

            try
            {
                foreach (var line in File.ReadLines(_mapListFilePath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//"))
                        continue;

                    var parts = trimmed.Split(':', 2);
                    var name = parts[0].Trim();
                    if (string.IsNullOrEmpty(name))
                        continue;

                    var workshopId = parts.Length == 2 ? parts[1].Trim() : null;
                    mapList[name] = string.IsNullOrEmpty(workshopId) ? null : workshopId;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"{PluginPrefix} Error reading map list file '{_mapListFilePath}': {ex.Message}");
            }

            return mapList;
        }

        private static T GetConVarValue<T>(string name, T defaultValue)
        {
            try
            {
                var conVar = ConVar<T>.Find(name);
                return conVar is null ? defaultValue : conVar.Value;
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion
    }
}
