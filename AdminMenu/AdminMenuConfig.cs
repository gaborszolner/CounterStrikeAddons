namespace AdminMenu
{
    public class AdminMenuConfig
    {
        public bool AutoTeamShuffleOnRoundStart { get; set; } = true;
        public int AutoTeamShuffleMinDifferentPercentage { get; set; } = 25;
        public string WelcomeMessage { get; set; } = "Welcome to the server {0}!";
        public int DateRangeForStatisticsInMonth { get; set; } = 3;
        public int MinimumPlayerCountToStatistic { get; set; } = 4;
        public bool AllowSameName { get; set; } = false;
        public int MuteAfterDeathInSecounds { get; set; } = 0;
        public string Language { get; set; } = "en";
    }
}
