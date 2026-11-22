using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using SharedLibrary;
using SharedLibrary.Entries;

namespace AdminMenu
{
    public partial class AdminMenu : BasePlugin
    {
        private void TeamShuffleAction(CCSPlayerController? adminPlayer, ChatMenuOption? option)
        {
            var statEntry = StatisticHelper.LoadMonthsStats(_statDirectory, _config.DateRangeForStatisticsInMonth);

            if (statEntry is null || statEntry.Count == 0)
            {
                return;
            }

            var sortedPlayers = PlayerHelper.GetAllNonSpecPlayers().Select(p =>
            {
                statEntry.TryGetValue(p.AuthorizedSteamID.SteamId2, out var s);
                s ??= new PlayerStatEntry(p.AuthorizedSteamID.SteamId2, p.PlayerName);
                return new Shuffle.PlayerShuffleData(p.AuthorizedSteamID.SteamId2, s);
            }).OrderByDescending(p => p.Stats.Score).ToList();

            var method1Result = Shuffle.GetShuffleResult(sortedPlayers, 1);
            var method2Result = Shuffle.GetShuffleResult(sortedPlayers, 2);
            var method3Result = Shuffle.GetShuffleResult(sortedPlayers, 3);
            var method4Result = Shuffle.GetShuffleResult(sortedPlayers, 4);

            var bestMethod = method1Result.Difference <= method2Result.Difference &&
                             method1Result.Difference <= method3Result.Difference &&
                             method1Result.Difference <= method4Result.Difference ? method1Result :
                             method2Result.Difference <= method3Result.Difference &&
                             method2Result.Difference <= method4Result.Difference ? method2Result :
                             method3Result.Difference <= method4Result.Difference ? method3Result : method4Result;

            Logger?.LogInformation($"{PluginPrefix} Used shuffle method {bestMethod.MethodNumber} with difference {bestMethod.Difference:F2}% (Method1: {method1Result.Difference:F2}%, Method2: {method2Result.Difference:F2}%, Method3: {method3Result.Difference:F2}%, Method4: {method4Result.Difference:F2}%)");

            Shuffle.ReOrganizeTeams(bestMethod.TeamTSteamId2List, bestMethod.TeamCTSteamId2List);

            if (adminPlayer != null)
            {
                MenuManager.GetActiveMenu(adminPlayer)?.Close();
            }
        }

        internal static class Shuffle
        {
            public static void ReOrganizeTeams(List<string> teamTSteamId2List, List<string> teamCTSteamId2List)
            {
                foreach (var tPlayerSteamId2 in teamTSteamId2List)
                {
                    try
                    {
                        var t = PlayerHelper.GetAllPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId2 == tPlayerSteamId2);
                        if (t?.IsValid == true)
                        {
                            t.SwitchTeam(CsTeam.Terrorist);
                            t.Respawn();
                        }
                    }
                    catch { }
                }

                foreach (var ctPlayerSteamId2 in teamCTSteamId2List)
                {
                    try
                    {
                        var ct = PlayerHelper.GetAllPlayers().FirstOrDefault(p => p.AuthorizedSteamID?.SteamId2 == ctPlayerSteamId2);
                        if (ct?.IsValid == true)
                        {
                            ct.SwitchTeam(CsTeam.CounterTerrorist);
                            ct.Respawn();
                        }
                    }
                    catch { }
                }
            }

            public static ShuffleResult GetShuffleResult(List<PlayerShuffleData> sortedPlayers, int methodNumber)
            {
                var teamTSteamId2List = new List<string>();
                var teamCTSteamId2List = new List<string>();

                int totalPlayers = sortedPlayers.Count;
                int maxTeamSizeT = totalPlayers / 2 + (totalPlayers % 2);
                int maxTeamSizeCT = totalPlayers / 2;

                double difference = methodNumber switch
                {
                    1 => ShuffleMethod1(sortedPlayers, maxTeamSizeT, maxTeamSizeCT, teamTSteamId2List, teamCTSteamId2List),
                    2 => ShuffleMethod2(sortedPlayers, maxTeamSizeT, maxTeamSizeCT, teamTSteamId2List, teamCTSteamId2List),
                    3 => ShuffleMethod3(sortedPlayers, maxTeamSizeT, maxTeamSizeCT, teamTSteamId2List, teamCTSteamId2List),
                    4 => ShuffleMethod4(sortedPlayers, maxTeamSizeT, maxTeamSizeCT, teamTSteamId2List, teamCTSteamId2List),
                    _ => double.MaxValue
                };

                return new ShuffleResult(methodNumber, teamTSteamId2List, teamCTSteamId2List, difference);
            }

            public static double ShuffleMethod1(List<PlayerShuffleData> sortedPlayers, int maxTeamSizeT, int maxTeamSizeCT, List<string> teamTSteamId2List, List<string> teamCTSteamId2List)
            {
                double sumScoreT = 0;
                double sumScoreCT = 0;

                foreach (var player in sortedPlayers)
                {
                    bool teamTHasSpace = teamTSteamId2List.Count < maxTeamSizeT;
                    bool teamCTHasSpace = teamCTSteamId2List.Count < maxTeamSizeCT;

                    if (!teamTHasSpace)
                    {
                        teamCTSteamId2List.Add(player.SteamId2);
                        sumScoreCT += player.Stats.Score;
                        continue;
                    }

                    if (!teamCTHasSpace)
                    {
                        teamTSteamId2List.Add(player.SteamId2);
                        sumScoreT += player.Stats.Score;
                        continue;
                    }

                    if (sumScoreT <= sumScoreCT)
                    {
                        teamTSteamId2List.Add(player.SteamId2);
                        sumScoreT += player.Stats.Score;
                    }
                    else
                    {
                        teamCTSteamId2List.Add(player.SteamId2);
                        sumScoreCT += player.Stats.Score;
                    }
                }

                return StatisticHelper.GetPercentageDifference(sumScoreCT, sumScoreT);
            }

            public static double ShuffleMethod2(List<PlayerShuffleData> sortedPlayers, int maxTeamSizeT, int maxTeamSizeCT, List<string> teamTSteamId2List, List<string> teamCTSteamId2List)
            {
                double sumScoreT = 0;
                double sumScoreCT = 0;

                foreach (var player in sortedPlayers)
                {
                    bool teamTHasSpace = teamTSteamId2List.Count < maxTeamSizeT;
                    bool teamCTHasSpace = teamCTSteamId2List.Count < maxTeamSizeCT;

                    if (!teamTHasSpace)
                    {
                        teamCTSteamId2List.Add(player.SteamId2);
                        sumScoreCT += player.Stats.Score;
                        continue;
                    }

                    if (!teamCTHasSpace)
                    {
                        teamTSteamId2List.Add(player.SteamId2);
                        sumScoreT += player.Stats.Score;
                        continue;
                    }

                    double teamTAverage = teamTSteamId2List.Count > 0 ? sumScoreT / teamTSteamId2List.Count : 0;
                    double teamCTAverage = teamCTSteamId2List.Count > 0 ? sumScoreCT / teamCTSteamId2List.Count : 0;

                    if (teamTAverage <= teamCTAverage)
                    {
                        teamTSteamId2List.Add(player.SteamId2);
                        sumScoreT += player.Stats.Score;
                    }
                    else
                    {
                        teamCTSteamId2List.Add(player.SteamId2);
                        sumScoreCT += player.Stats.Score;
                    }
                }

                return StatisticHelper.GetPercentageDifference(sumScoreCT, sumScoreT);
            }

            public static double ShuffleMethod3(List<PlayerShuffleData> sortedPlayers, int maxTeamSizeT, int maxTeamSizeCT, List<string> teamTSteamId2List, List<string> teamCTSteamId2List)
            {
                double sumScoreT = 0;
                double sumScoreCT = 0;
                bool assignToT = true; // Start with T team (which gets the extra player if odd total)

                foreach (var player in sortedPlayers)
                {
                    bool teamTFull = teamTSteamId2List.Count >= maxTeamSizeT;
                    bool teamCTFull = teamCTSteamId2List.Count >= maxTeamSizeCT;

                    // If one team is full, add to the other
                    if (teamTFull && !teamCTFull)
                    {
                        teamCTSteamId2List.Add(player.SteamId2);
                        sumScoreCT += player.Stats.Score;
                        assignToT = !assignToT;
                        continue;
                    }

                    if (teamCTFull && !teamTFull)
                    {
                        teamTSteamId2List.Add(player.SteamId2);
                        sumScoreT += player.Stats.Score;
                        assignToT = !assignToT;
                        continue;
                    }

                    // Both teams have space: use alternating strategy biased by score difference
                    // If score difference is large, prioritize adding to lower-scoring team
                    double scoreDifference = Math.Abs(sumScoreT - sumScoreCT);
                    bool shouldAddToLowerScoreTeam = scoreDifference > player.Stats.Score * 0.5;

                    if (shouldAddToLowerScoreTeam)
                    {
                        if (sumScoreT < sumScoreCT)
                        {
                            teamTSteamId2List.Add(player.SteamId2);
                            sumScoreT += player.Stats.Score;
                        }
                        else
                        {
                            teamCTSteamId2List.Add(player.SteamId2);
                            sumScoreCT += player.Stats.Score;
                        }
                    }
                    else
                    {
                        // Default: alternate between teams (with T team preference for even distribution)
                        if (assignToT)
                        {
                            teamTSteamId2List.Add(player.SteamId2);
                            sumScoreT += player.Stats.Score;
                        }
                        else
                        {
                            teamCTSteamId2List.Add(player.SteamId2);
                            sumScoreCT += player.Stats.Score;
                        }
                    }

                    assignToT = !assignToT;
                }

                return StatisticHelper.GetPercentageDifference(sumScoreCT, sumScoreT);
            }

            public static double ShuffleMethod4(List<PlayerShuffleData> sortedPlayers, int maxTeamSizeT, int maxTeamSizeCT, List<string> teamTSteamId2List, List<string> teamCTSteamId2List)
            {
                double sumScoreT = 0;
                double sumScoreCT = 0;

                int currentIndex = 0;
                bool assignToT = true;

                while (currentIndex < sortedPlayers.Count)
                {
                    if (assignToT && teamTSteamId2List.Count < maxTeamSizeT)
                    {
                        teamTSteamId2List.Add(sortedPlayers[currentIndex].SteamId2);
                        sumScoreT += sortedPlayers[currentIndex].Stats.Score;
                        currentIndex++;
                    }
                    else if (!assignToT && teamCTSteamId2List.Count < maxTeamSizeCT)
                    {
                        teamCTSteamId2List.Add(sortedPlayers[currentIndex].SteamId2);
                        sumScoreCT += sortedPlayers[currentIndex].Stats.Score;
                        currentIndex++;
                    }
                    else if (teamTSteamId2List.Count >= maxTeamSizeT && teamCTSteamId2List.Count < maxTeamSizeCT)
                    {
                        teamCTSteamId2List.Add(sortedPlayers[currentIndex].SteamId2);
                        sumScoreCT += sortedPlayers[currentIndex].Stats.Score;
                        currentIndex++;
                    }
                    else if (teamCTSteamId2List.Count >= maxTeamSizeCT && teamTSteamId2List.Count < maxTeamSizeT)
                    {
                        teamTSteamId2List.Add(sortedPlayers[currentIndex].SteamId2);
                        sumScoreT += sortedPlayers[currentIndex].Stats.Score;
                        currentIndex++;
                    }

                    assignToT = !assignToT;
                }

                return StatisticHelper.GetPercentageDifference(sumScoreCT, sumScoreT);
            }

            public record PlayerShuffleData(string SteamId2, PlayerStatEntry Stats);

            public record ShuffleResult(int MethodNumber, List<string> TeamTSteamId2List, List<string> TeamCTSteamId2List, double Difference);

        }
    }
}
