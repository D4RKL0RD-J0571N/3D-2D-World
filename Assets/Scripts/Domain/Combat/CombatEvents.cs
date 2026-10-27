namespace Domain.Combat
{
    using Core;

    /// <summary>
    /// Published when an entity attempts a combat action without sufficient stamina.
    /// Used by UI to trigger exhaustion feedback.
    /// </summary>
    public sealed class PlayerStaminaExhaustedEvent : GameEvent
    {
    }

    /// <summary>
    /// Published when an attack lands while the attacker is undetected in stealth.
    /// Used for Skyrim-style critical damage multiplication and UI notifications.
    /// </summary>
    public sealed class SneakAttackTriggeredEvent : GameEvent
    {
        public UnityEngine.GameObject Attacker;
        public Gameplay.Entities.BaseActor Target;
        public float Multiplier;
        public int FinalDamage;

        public SneakAttackTriggeredEvent(UnityEngine.GameObject attacker, Gameplay.Entities.BaseActor target, float multiplier, int finalDamage)
        {
            Attacker = attacker;
            Target = target;
            Multiplier = multiplier;
            FinalDamage = finalDamage;
        }
    }
}
