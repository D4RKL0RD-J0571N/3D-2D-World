namespace Gameplay.World
{
    using Core;
    using Domain.LevelGeneration;

    public sealed class LevelGenerationRequestedEvent : GameEvent
    {
        public LevelGenerationParameters Parameters;
    }

    public sealed class LevelGenerationCompletedEvent : GameEvent
    {
        public LevelLayout Layout;
    }

    public sealed class LevelGenerationFailedEvent : GameEvent
    {
        public string Reason;
    }

    public sealed class LevelCompletedEvent : GameEvent { }

    public sealed class PlayerSpawnRequestedEvent : GameEvent
    {
        public UnityEngine.Vector3 SpawnPosition;
    }
}
