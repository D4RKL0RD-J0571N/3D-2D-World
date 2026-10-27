namespace Gameplay.Skyrim
{
    using Domain.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Drop this component on any NPC / creature / group prefab to assign it a faction.
    /// The FactionService uses this to determine hostility and AI behavior.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Skyrim/Faction Agent")]
    [DisallowMultipleComponent]
    public class FactionComponent : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Faction Identity")]
        [Tooltip("Which faction does this actor belong to?")]
        [SerializeField] private FactionType _faction = FactionType.Neutral;

        #endregion

        #region Properties

        /// <summary>This actor's faction affiliation.</summary>
        public FactionType Faction => _faction;

        #endregion

        #region Public API

        /// <summary>
        /// Runtime override of this actor's faction (e.g. after betrayal event).
        /// </summary>
        public void SetFaction(FactionType newFaction)
        {
            _faction = newFaction;
        }

        /// <summary>
        /// Queries the FactionService to check if this actor is hostile toward another.
        /// </summary>
        public bool IsHostileTo(FactionComponent other)
        {
            if (other == null) return false;

            if (Core.ServiceLocator.TryGet<Systems.Skyrim.FactionService>(out var factionSvc))
                return factionSvc.IsHostile(_faction, other.Faction);

            return false;
        }

        #endregion
    }
}
