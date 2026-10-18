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
}
