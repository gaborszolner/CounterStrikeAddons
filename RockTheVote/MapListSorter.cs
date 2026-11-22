using System.Text.Json;
using SharedLibrary.Entries;

namespace RockTheVote
{
    public static class MapListSorter
    {
        public static void Sort(string mapFile, string statisticsFile, string? order)
        {
            if (string.Equals(order, "None", StringComparison.OrdinalIgnoreCase))
                return;

            bool byUsage = string.Equals(order, "Usage", StringComparison.OrdinalIgnoreCase);
            if (!byUsage && !string.Equals(order, "Name", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Invalid MapSortOrder '{order}'. Expected None, Name or Usage.", nameof(order));

            var lastPlayed = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            if (byUsage)
            {
                var stats = JsonSerializer.Deserialize<Dictionary<string, MapStatEntry?>>(File.ReadAllText(statisticsFile))
                    ?? throw new JsonException("Map statistics must be a JSON object.");
                foreach (var stat in stats)
                    lastPlayed[stat.Key] = (stat.Value ?? throw new JsonException($"Map statistics entry '{stat.Key}' is null.")).LastPlayed;
            }

            var lines = File.ReadAllLines(mapFile);
            var comments = lines.Where(line => line.StartsWith("//"));
            var maps = lines
                .Where(line => !line.StartsWith("//") && !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .Select(line => (Line: line, Name: line.Split(':')[0].Trim()));

            var sortedMaps = byUsage
                ? maps.OrderBy(map => lastPlayed.TryGetValue(map.Name, out var played) ? played : DateTime.MinValue)
                    .ThenBy(map => map.Name, StringComparer.OrdinalIgnoreCase)
                : maps.OrderBy(map => map.Name, StringComparer.OrdinalIgnoreCase);
            var sorted = comments.Concat(sortedMaps.Select(map => map.Line)).ToArray();
            if (!sorted.SequenceEqual(lines))
                File.WriteAllLines(mapFile, sorted);
        }
    }
}
