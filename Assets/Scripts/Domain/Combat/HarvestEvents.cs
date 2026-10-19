namespace Domain.Combat
{
    using UnityEngine;
    using Core;
    using Data.Config;

    /// <summary>
    /// Event dispatched when a harvestable world node is damaged or depleted.
    /// Useful for sound, quest tracking, or particle feedback.
    /// </summary>
    public sealed class HarvestNodeDamagedEvent : GameEvent
    {
        public GameObject NodeObject;
        public int DamageAmount;
        public int RemainingIntegrity;
        public bool IsDepleted;
    }
}
