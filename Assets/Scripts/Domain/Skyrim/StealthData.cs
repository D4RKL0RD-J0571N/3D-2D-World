namespace Domain.Skyrim
{
    /// <summary>
    /// Represents the player's current stealth / awareness state in the world.
    /// Matches Skyrim's three-tier sneak system: Hidden, Searching, Detected.
    /// </summary>
    public enum StealthState
    {
        Hidden = 0,
        Searching = 1,
        Detected = 2
    }

    /// <summary>
    /// Runtime data snapshot representing stealth awareness metrics.
    /// </summary>
    public struct StealthSnapshot
    {
        public StealthState State;
        public float DetectionRatio; // 0.0f (fully hidden) to 1.0f (detected)
        public int AwareEnemiesCount;

        public StealthSnapshot(StealthState state, float ratio, int enemies)
        {
            State = state;
            DetectionRatio = ratio;
            AwareEnemiesCount = enemies;
        }
    }
}
