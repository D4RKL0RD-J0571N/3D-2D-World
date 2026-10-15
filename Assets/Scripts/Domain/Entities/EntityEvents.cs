namespace Domain.Entities
{
    using Core;
    using Gameplay.Entities;

    public class EntityDamagedEvent : GameEvent
    {
        public BaseActor Target;
        public int Amount;
    }

    public class EntityHealedEvent : GameEvent
    {
        public BaseActor Target;
        public int Amount;
    }

    public class EntityStateChangedEvent : GameEvent
    {
        public BaseActor Target;
        public ActorState NewStates;
    }

    public class EntityLevelUpEvent : GameEvent
    {
        public BaseActor Target;
        public int NewLevel;
    }
}
