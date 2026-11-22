using CounterStrikeSharp.API;
using SharedLibrary.Entries;
using System.Text.Json;

namespace SharedLibrary
{
    public class Utils
    {
        public static string LogPrefix { get; set; } = "[SharedLibrary]";

        public static DateTime GetServerTime()
        {
            TimeZoneInfo tz;

            if (OperatingSystem.IsWindows())
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            }
            else
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
            }

            DateTime time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            return time;
        }

        public static T? ReadStoredStat<T>(string filePath)
        {
            T? returnStat = default;
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                try
                {
                    var deserialized = JsonSerializer.Deserialize<T>(json);

                if (deserialized is not null)
                {
                    returnStat = deserialized;
                }
                }
                catch (Exception)
                {
                    Server.PrintToConsole($"{LogPrefix} Failed to deserialize file: {json}");
                }
            }
            else
            {
                File.WriteAllText(filePath, "{}");
            }

            return returnStat;
        }

        public static Dictionary<string, T>? LoadDataFromFile<T>(string filePath)
            => LoadDataFromFile<T>(filePath, createIfMissing: true);

        public static Dictionary<string, T>? LoadDataFromFile<T>(string filePath, bool createIfMissing)
        {
            string json;
            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (FileNotFoundException)
            {
                if (createIfMissing)
                    File.WriteAllText(filePath, "{}");
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, T>>
                    (json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                Server.PrintToConsole($"{LogPrefix} Failed to deserialize file: {json}");
                return null;
            }
        }

        public static void WriteToFile<T>(T? entry, string fileName)
        {
            if (entry is null)
            {
                return;
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(fileName, JsonSerializer.Serialize(entry, options));
            }
            catch (Exception)
            {
                Server.PrintToConsole($"{LogPrefix} Error writing to file {fileName}");
            }
        }

        /// <summary>
        /// Normalizes a Steam2 ID to always use the STEAM_0 universe prefix.
        /// CS2 servers may return STEAM_1:X:Y at runtime while stored data uses STEAM_0:X:Y.
        /// Both refer to the same account and are interchangeable.
        /// </summary>
        public static string NormalizeSteamId2(string steamId2)
        {
            return steamId2.Replace("STEAM_1:", "STEAM_0:");
        }
    }
}
