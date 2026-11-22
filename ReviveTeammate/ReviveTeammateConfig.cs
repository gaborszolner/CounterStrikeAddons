namespace ReviveTeammate
{
    public class ReviveTeammateConfig
    {
        public bool CanReviveTeammate { get; set; } = false;
        public float ReviveHoldDurationSeconds { get; set; } = 10.0f;
        public float ReviveDeathWindowSeconds { get; set; } = 30.0f;
        public int ReviveHP { get; set; } = 10;
        public string Language { get; set; } = "en";
    }
}
