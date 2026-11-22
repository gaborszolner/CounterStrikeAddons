namespace RockTheVote
{
    public class RtvConfig
    {
        public bool EnableRtv { get; set; } = true;
        public bool EnableRtvInWarmup { get; set; } = false;
        public int MinimumRtvPercentage { get; set; } = 60;
        public bool ChangeMapImmediatelyAfterRtv { get; set; } = true;
        public bool EnableNominate { get; set; } = true;
        public bool EnableNominateInWarmup { get; set; } = true;
        public bool EnableEndOfMapVote { get; set; } = true;
        public int EndOfMapVoteRoundsBeforeEnd { get; set; } = 1;
        public int VotingSecondLimit { get; set; } = 30;
        public bool CloseVotingMenuAfterVote { get; set; } = true;
        public int MapsToShow { get; set; } = 8;
        public int MapsInCoolDown { get; set; } = 3;
        public string MapSortOrder { get; set; } = "None";
        public string Language { get; set; } = "en";

        public static RtvConfig LoadConfig(string configFile)
        {
            RtvConfig? config = null;
            try
            {
                if (!File.Exists(configFile))
                {
                    config = new RtvConfig();
                    var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(configFile, json);
                    return config;
                }
                string fileContent = File.ReadAllText(configFile);
                config = System.Text.Json.JsonSerializer.Deserialize<RtvConfig>(fileContent);
            }
            catch { }

            return config ?? new RtvConfig();
        }
    }
}
