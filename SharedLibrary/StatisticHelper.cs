using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using SharedLibrary.Entries;
using System.Globalization;

namespace SharedLibrary
{
    public static class StatisticHelper
    {
        public static double GetPercentageDifference(double ctSumScore, double tSumScore)
        {
            double higher = Math.Max(ctSumScore, tSumScore);
            double lower = Math.Min(ctSumScore, tSumScore);
            double percentDiff = ((higher - lower) / lower) * 100.0;
            return percentDiff;
        }

        public static double GetSumScores(Dictionary<string, PlayerStatEntry>? storedStats, IEnumerable<CCSPlayerController> players)
        {
            if (storedStats is null)
            {
                return 0.0;
            }

            return players
               .Select(p => p.AuthorizedSteamID?.SteamId2)
               .Where(id => id is not null)
               .Select(id => Utils.NormalizeSteamId2(id!))
               .Where(id => storedStats.ContainsKey(id))
               .Sum(id => storedStats[id].Score);
        }

        public static Dictionary<string, PlayerStatEntry> LoadMonthsStats(string playerStatFilesBasePath, int month)
        {
            var result = new Dictionary<string, PlayerStatEntry>();

            for (int i = 0; i < month; i++)
            {
                var date = DateTime.Now.AddMonths(-i);
                var fileName = GetPlayerStatFileName(date);
                var fullPath = Path.Combine(playerStatFilesBasePath, fileName);

                var monthlyStats = Utils.LoadDataFromFile<PlayerStatEntry>(fullPath, createIfMissing: false);
                if (monthlyStats == null)
                    continue;

                foreach (var (identity, entry) in monthlyStats)
                {
                    if (!result.TryGetValue(identity, out var existing))
                    {
                        result[identity] = new PlayerStatEntry(
                            identity,
                            entry.Name,
                            entry.Kill,
                            entry.Dead,
                            entry.SelfKill,
                            entry.TeamKill,
                            entry.Assister);
                    }
                    else
                    {
                        existing.Kill += entry.Kill;
                        existing.Dead += entry.Dead;
                        existing.SelfKill += entry.SelfKill;
                        existing.TeamKill += entry.TeamKill;
                        existing.Assister += entry.Assister;
                    }
                }
            }

            return result;
        }

        public static void DeleteOldFiles(string directory, int month)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(month);

            var now = DateTime.Now;
            var oldestRetainedMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1 - month);
            const string prefix = "playerStatistic";
            foreach (var file in Directory.EnumerateFiles(directory, $"{prefix}*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.StartsWith(prefix, StringComparison.Ordinal)
                    && DateTime.TryParseExact(name[prefix.Length..], "yyyyMM", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var fileDate)
                    && fileDate < oldestRetainedMonth)
                {
                    File.Delete(file);
                }
            }
        }

        public static string GetPlayerStatFileName(DateTime dateTime)
        {
            return $"playerStatistic{dateTime:yyyyMM}.json";
        }

        public static void PrintMapStat(string statFilePath, bool refreshCache = false)
        {
            string mapName = Server.MapName.Trim() ?? string.Empty;
            var storedStats =
                Utils.ReadStoredStat<Dictionary<string, MapStatEntry>>(statFilePath)
                ?? [];

            if (refreshCache)
            {
                CacheLastPlayedActualMap = storedStats.ContainsKey(mapName)
                ? storedStats[mapName].LastPlayed
                : DateTime.MinValue;
            }

            if (storedStats.ContainsKey(mapName))
            {
                int ctWin = storedStats[mapName].CTWin;
                int tWin = storedStats[mapName].TWin;
                int fullRoundCount = ctWin + tWin;

                double tWinPercentage = (double)tWin / fullRoundCount * 100;
                double ctWinPercentage = (double)ctWin / fullRoundCount * 100;
                if (CacheLastPlayedActualMap.Year == DateTime.MinValue.Year)
                {
                    Server.PrintToChatAll(Msg.Get("MapStatsNeverPlayed",
                        (object)ChatColors.Red, $"{tWinPercentage:F2}",
                        (object)ChatColors.Blue, $"{ctWinPercentage:F2}",
                        (object)ChatColors.Green));
                }
                else
                {
                    TimeSpan difference = Utils.GetServerTime() - CacheLastPlayedActualMap;
                    var daysPassed = difference.TotalDays;
                    Server.PrintToChatAll(Msg.Get("MapStatsLastPlayed",
                        (object)ChatColors.Red, $"{tWinPercentage:F2}",
                        (object)ChatColors.Blue, $"{ctWinPercentage:F2}",
                        (object)ChatColors.Green, $"{daysPassed:F1}"));
                }
            }
        }

        public static void PrintTeamStat(Dictionary<string, PlayerStatEntry> storedStatistic, bool aliveOnly = false)
        {
            double ctSumScore = Math.Round(StatisticHelper.GetSumScores(storedStatistic, aliveOnly ? PlayerHelper.GetAllCounterTerroristAlive() : PlayerHelper.GetAllCounterTerrorist()), 1);
            double tSumScore = Math.Round(StatisticHelper.GetSumScores(storedStatistic, aliveOnly ? PlayerHelper.GetAllTerroristAlive() : PlayerHelper.GetAllTerrorist()), 1);

            if (ctSumScore == 0 || tSumScore == 0)
            {
                return;
            }

            double percentDiff = StatisticHelper.GetPercentageDifference(ctSumScore, tSumScore);

            string comparisonText;
            if (ctSumScore == tSumScore)
            {
                comparisonText = $"{ChatColors.Green}{Msg.Get("TeamsEqual")}";
            }
            else
            {
                comparisonText = ctSumScore > tSumScore
                        ? $"{ChatColors.Blue} {Msg.Get("CTStronger", $"{percentDiff:F1}")}"
                        : $"{ChatColors.Red} {Msg.Get("TStronger", $"{percentDiff:F1}")}";
            }

            Server.PrintToChatAll($"{ChatColors.Green} {Msg.Get("TeamScores", comparisonText)}");
        }

        public static DateTime CacheLastPlayedActualMap { get; set; } = DateTime.MinValue;

        public static string PlayerStatFileName => GetPlayerStatFileName(DateTime.Now);

        public static string MapStatFileName { get { return "mapStatistic.json"; } }
    }
}
