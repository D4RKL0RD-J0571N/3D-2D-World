namespace Domain.Entities
{
    using Core;
    using Gameplay.Entities;

    public class EntityDamagedEvent : GameEvent
    {
        public BaseActor Target;
        public int Amount;

        /// <summary>The GameObject that dealt the damage (can be null for environmental damage).</summary>
        public UnityEngine.GameObject Instigator;

        /// <summary>Element/type of damage dealt (Physical, Magic, Fire, etc.).</summary>
        public Domain.Combat.DamageType DamageType;
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

    /// <summary>
    /// Published when an entity's Health attribute is depleted to zero.
    /// </summary>
    public class EntityDiedEvent : GameEvent
    {
        /// <summary>The actor that perished.</summary>
        public BaseActor Victim;

        /// <summary>The GameObject that dealt the fatal blow (can be null).</summary>
        public UnityEngine.GameObject Killer;
    }
}
