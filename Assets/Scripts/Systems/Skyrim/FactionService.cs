namespace Systems.Skyrim
{
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Core.Ids;
    using Domain.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Manages faction relationships, reputation standings, and bounties at runtime.
    /// Backed by <see cref="ContentRegistry{FactionDefinition}"/> so mods can register new factions
    /// without altering the legacy <see cref="FactionType"/> enum.
    /// </summary>
    public class FactionService : IGameService
    {
        #region Constants

        public static readonly ResourceId PlayerFactionId  = new("nordic_rpg", "faction/player");
        public static readonly ResourceId GuardsFactionId  = new("nordic_rpg", "faction/guards");
        public static readonly ResourceId BanditsFactionId = new("nordic_rpg", "faction/bandits");
        public static readonly ResourceId DraugrFactionId  = new("nordic_rpg", "faction/draugr");
        public static readonly ResourceId BeastsFactionId  = new("nordic_rpg", "faction/beasts");
        public static readonly ResourceId NeutralFactionId = new("nordic_rpg", "faction/neutral");

        private const int HostileReputationThreshold = -50;
        private const int FriendlyReputationThreshold = 50;

        #endregion

        #region Fields

        private FactionRelationshipMatrixSO _matrix;
        private readonly Dictionary<(ResourceId, ResourceId), FactionDisposition> _dispositions = new();
        private readonly Dictionary<ResourceId, int> _playerReputation = new();
        private readonly Dictionary<ResourceId, int> _playerBounties = new();

        #endregion

        #region Properties

        public IReadOnlyDictionary<ResourceId, int> PlayerReputation => _playerReputation;
        public IReadOnlyDictionary<ResourceId, int> PlayerBounties => _playerBounties;

        #endregion

        #region IGameService

        public void Initialize()
        {
            _dispositions.Clear();
            _playerReputation.Clear();
            _playerBounties.Clear();

            // Optional legacy ScriptableObject override if present in Resources
            _matrix = Resources.Load<FactionRelationshipMatrixSO>("Skyrim/FactionRelationshipMatrix");

            IContentService content = ServiceLocator.TryGet<IContentService>(out var cs) ? cs : null;
            ContentRegistry<FactionDefinition> registry = content?.GetRegistry<FactionDefinition>();

            IReadOnlyList<FactionDefinition> defaults = FactionDefinition.CreateDefaultNordicFactions();
            for (int i = 0; i < defaults.Count; i++)
            {
                FactionDefinition def = defaults[i];
                if (registry != null && !registry.Contains(def.ContentId) && !registry.IsFrozen)
                {
                    registry.Register(def, def.ContentId.Namespace, ContentRegistrationPolicy.OverrideExisting, def.LegacyFaction?.ToString());
                }

                ApplyFactionDefinition(def);
            }

            if (registry != null)
            {
                foreach (FactionDefinition customDef in registry.GetAll())
                {
                    ApplyFactionDefinition(customDef);
                }
            }
        }

        public void Shutdown()
        {
            _dispositions.Clear();
            _playerReputation.Clear();
            _playerBounties.Clear();
        }

        #endregion

        #region Public API — ResourceId (Data-Driven)

        /// <summary>Registers a new or modded <see cref="FactionDefinition"/> and seeds its default dispositions.</summary>
        public void RegisterFaction(FactionDefinition definition)
        {
            if (definition == null || !definition.ContentId.IsValid) return;

            if (ServiceLocator.TryGet<IContentService>(out var content))
            {
                var reg = content.GetRegistry<FactionDefinition>();
                if (!reg.IsFrozen)
                {
                    reg.Register(definition, definition.ContentId.Namespace, ContentRegistrationPolicy.OverrideExisting);
                }
            }

            ApplyFactionDefinition(definition);
        }

        /// <summary>Returns the effective disposition of <paramref name="from"/> toward <paramref name="to"/>.</summary>
        public FactionDisposition GetDisposition(ResourceId from, ResourceId to)
        {
            if (!from.IsValid || !to.IsValid)
            {
                return FactionDisposition.Neutral;
            }

            if (from == to)
            {
                return FactionDisposition.Friendly;
            }

            if (_dispositions.TryGetValue((from, to), out FactionDisposition disp))
            {
                return disp;
            }

            if (_matrix != null &&
                FactionDefinition.TryToLegacyEnum(from, out FactionType fEnum) &&
                FactionDefinition.TryToLegacyEnum(to, out FactionType tEnum))
            {
                return _matrix.GetDisposition(fEnum, tEnum);
            }

            return FactionDisposition.Neutral;
        }

        /// <summary>Sets a runtime override disposition and publishes <see cref="FactionDispositionChangedEvent"/>.</summary>
        public void SetDisposition(ResourceId from, ResourceId to, FactionDisposition disp)
        {
            if (!from.IsValid || !to.IsValid) return;

            _dispositions[(from, to)] = disp;
            GameEventManager.Publish(new FactionDispositionChangedEvent(from, to, disp));
        }

        /// <summary>Returns true if <paramref name="from"/> is currently hostile toward <paramref name="to"/>.</summary>
        public bool IsHostile(ResourceId from, ResourceId to) =>
            GetDisposition(from, to) == FactionDisposition.Hostile;

        /// <summary>Returns the player's current reputation score (-100 to +100) with <paramref name="factionId"/>.</summary>
        public int GetReputation(ResourceId factionId) =>
            _playerReputation.TryGetValue(factionId, out int rep) ? rep : 0;

        /// <summary>Sets the player's reputation score with <paramref name="factionId"/> and updates disposition if thresholds are crossed.</summary>
        public void SetReputation(ResourceId factionId, int reputation)
        {
            if (!factionId.IsValid) return;

            int clamped = Mathf.Clamp(reputation, -100, 100);
            _playerReputation[factionId] = clamped;

            if (clamped <= HostileReputationThreshold)
            {
                SetDisposition(factionId, PlayerFactionId, FactionDisposition.Hostile);
            }
            else if (clamped >= FriendlyReputationThreshold)
            {
                SetDisposition(factionId, PlayerFactionId, FactionDisposition.Friendly);
            }
        }

        /// <summary>Adjusts the player's reputation with <paramref name="factionId"/> by <paramref name="delta"/>.</summary>
        public void ModifyReputation(ResourceId factionId, int delta) =>
            SetReputation(factionId, GetReputation(factionId) + delta);

        /// <summary>Returns the player's current crime bounty with <paramref name="factionId"/>.</summary>
        public int GetBounty(ResourceId factionId) =>
            _playerBounties.TryGetValue(factionId, out int bounty) ? bounty : 0;

        /// <summary>Adds crime bounty to <paramref name="factionId"/>.</summary>
        public void AddBounty(ResourceId factionId, int amount)
        {
            if (!factionId.IsValid || amount <= 0) return;

            int updated = GetBounty(factionId) + amount;
            _playerBounties[factionId] = updated;
            if (updated >= 1000)
            {
                SetDisposition(factionId, PlayerFactionId, FactionDisposition.Hostile);
            }
        }

        /// <summary>Clears the player's bounty with <paramref name="factionId"/>.</summary>
        public void ClearBounty(ResourceId factionId)
        {
            if (!factionId.IsValid) return;
            _playerBounties[factionId] = 0;
        }

        #endregion

        #region Public API — Legacy FactionType Enum Overloads

        public FactionDisposition GetDisposition(FactionType from, FactionType to) =>
            GetDisposition(FactionDefinition.ToResourceId(from), FactionDefinition.ToResourceId(to));

        public void SetDisposition(FactionType from, FactionType to, FactionDisposition disp) =>
            SetDisposition(FactionDefinition.ToResourceId(from), FactionDefinition.ToResourceId(to), disp);

        public bool IsHostile(FactionType from, FactionType to) =>
            IsHostile(FactionDefinition.ToResourceId(from), FactionDefinition.ToResourceId(to));

        #endregion

        #region Helpers

        private void ApplyFactionDefinition(FactionDefinition def)
        {
            if (def == null || !def.ContentId.IsValid) return;

            if (!_playerReputation.ContainsKey(def.ContentId))
            {
                _playerReputation[def.ContentId] = 0;
            }

            if (def.DefaultDispositions == null) return;

            foreach (var kvp in def.DefaultDispositions)
            {
                if (ResourceId.TryParse(kvp.Key, out ResourceId targetId))
                {
                    if (!_dispositions.ContainsKey((def.ContentId, targetId)))
                    {
                        _dispositions[(def.ContentId, targetId)] = kvp.Value;
                    }
                }
            }
        }

        #endregion
    }
}
