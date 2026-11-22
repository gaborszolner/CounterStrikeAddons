namespace GameStatistic
{
    public class GameStatisticConfig
    {
        public int DateRangeForStatisticsInMonth { get; set; } = 3;
        public int MinimumKillCountToShowInTop { get; set; } = 300;
        public int MinimumPlayerCountToStatistic { get; set; } = 4;
        public bool EnableTopBottomCommand { get; set; } = false;
        public string Language { get; set; } = "en";
    }
}
