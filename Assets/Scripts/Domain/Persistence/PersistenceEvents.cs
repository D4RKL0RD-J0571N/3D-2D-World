namespace Domain.Persistence
{
    using Core;

    public class GameSaveRequestedEvent : GameEvent { }
    public class GameLoadRequestedEvent : GameEvent { }
    public class GameDataLoadedEvent : GameEvent
    {
        public LevelSaveData SaveData;
    }

    public class EntityStateLoadedEvent : GameEvent
    {
        public EntitySaveData State;
    }
}
