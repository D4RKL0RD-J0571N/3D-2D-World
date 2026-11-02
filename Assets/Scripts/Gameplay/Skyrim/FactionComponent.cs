namespace Gameplay.Skyrim
{
    using Core.Ids;
    using Domain.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Drop this component on any NPC / creature / group prefab to assign it a faction.
    /// Supports both legacy <see cref="FactionType"/> enum values and custom mod <see cref="ResourceId"/> factions.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Skyrim/Faction Agent")]
    [DisallowMultipleComponent]
    public class FactionComponent : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Faction Identity")]
        [Tooltip("Which built-in faction does this actor belong to?")]
        [SerializeField] private FactionType _faction = FactionType.Neutral;

        [Tooltip("Optional canonical ResourceId override (e.g. 'mod_pack:faction/crimson_cult').")]
        [SerializeField] private string _customFactionId;

        #endregion

        #region Properties

        /// <summary>This actor's legacy faction enum affiliation.</summary>
        public FactionType Faction => _faction;

        /// <summary>This actor's canonical faction <see cref="ResourceId"/>.</summary>
        public ResourceId FactionId =>
            !string.IsNullOrWhiteSpace(_customFactionId) && ResourceId.TryParse(_customFactionId, out ResourceId parsed)
                ? parsed
                : FactionDefinition.ToResourceId(_faction);

        #endregion

        #region Public API

        /// <summary>
        /// Runtime override of this actor's faction using the legacy enum.
        /// </summary>
        public void SetFaction(FactionType newFaction)
        {
            _faction = newFaction;
            _customFactionId = FactionDefinition.ToResourceId(newFaction).ToString();
        }

        /// <summary>
        /// Runtime override of this actor's faction using a data-driven <see cref="ResourceId"/>.
        /// </summary>
        public void SetFaction(ResourceId factionId)
        {
            _customFactionId = factionId.IsValid ? factionId.ToString() : string.Empty;
            if (FactionDefinition.TryToLegacyEnum(factionId, out FactionType mapped))
            {
                _faction = mapped;
            }
        }

        /// <summary>
        /// Queries the <see cref="Systems.Skyrim.FactionService"/> to check if this actor is hostile toward another.
        /// </summary>
        public bool IsHostileTo(FactionComponent other)
        {
            if (other == null) return false;

            if (Core.ServiceLocator.TryGet<Systems.Skyrim.FactionService>(out var factionSvc))
            {
                return factionSvc.IsHostile(FactionId, other.FactionId);
            }

            return false;
        }

        #endregion
    }
}
