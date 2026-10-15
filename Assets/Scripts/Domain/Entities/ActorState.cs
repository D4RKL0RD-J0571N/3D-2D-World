namespace Domain.Entities
{
    using System;

    [Flags]
    public enum ActorState
    {
        None = 0,
        Death = 1 << 0,
        Poison = 1 << 1,
        Sleep = 1 << 2,
        Electrified = 1 << 3,
        Enchanted = 1 << 4,
        InCombat = 1 << 5, // Useful for changing regeneration logic
    }
}
