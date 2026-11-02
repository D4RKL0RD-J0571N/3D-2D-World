namespace Domain.Skyrim
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Core.Ids;
    using UnityEngine;

    /// <summary>
    /// The known factions in the world. Used for disposition and aggression logic.
    /// Retained for backward compatibility alongside data-driven <see cref="FactionDefinition"/>.
    /// </summary>
    public enum FactionType
    {
        Player,
        Guards,
        Bandits,
        Draugr,
        Beasts,
        Neutral
    }

    /// <summary>
    /// Standing of one faction toward another.
    /// </summary>
    public enum FactionDisposition
    {
        Friendly =  1,
        Neutral  =  0,
        Hostile  = -1
    }

    /// <summary>
    /// Data-driven faction definition registered in <see cref="ContentRegistry{FactionDefinition}"/>.
    /// Enables mods and content packs to introduce new factions and political standings without modifying <see cref="FactionType"/>.
    /// </summary>
    [Serializable]
    public sealed class FactionDefinition : IContentDefinition
    {
        #region Properties

        public ResourceId ContentId { get; set; }
        public string Id => ContentId.Value;
        public string SourceModId { get; set; } = ResourceId.DefaultNamespace;
        public string DisplayName { get; set; }
        public FactionType? LegacyFaction { get; set; }
        public Dictionary<string, FactionDisposition> DefaultDispositions { get; set; } = new(StringComparer.Ordinal);

        #endregion

        #region Constructors

        public FactionDefinition() { }

        public FactionDefinition(
            ResourceId contentId,
            string displayName,
            FactionType? legacyFaction = null,
            Dictionary<string, FactionDisposition> defaultDispositions = null)
        {
            ContentId = contentId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? contentId.Path : displayName;
            LegacyFaction = legacyFaction;
            if (defaultDispositions != null)
            {
                DefaultDispositions = new Dictionary<string, FactionDisposition>(defaultDispositions, StringComparer.Ordinal);
            }
        }

        #endregion

        #region Canonical Helpers

        private static readonly Dictionary<FactionType, ResourceId> EnumToId = new()
        {
            { FactionType.Player,  new ResourceId("nordic_rpg", "faction/player") },
            { FactionType.Guards,  new ResourceId("nordic_rpg", "faction/guards") },
            { FactionType.Bandits, new ResourceId("nordic_rpg", "faction/bandits") },
            { FactionType.Draugr,  new ResourceId("nordic_rpg", "faction/draugr") },
            { FactionType.Beasts,  new ResourceId("nordic_rpg", "faction/beasts") },
            { FactionType.Neutral, new ResourceId("nordic_rpg", "faction/neutral") }
        };

        private static readonly Dictionary<ResourceId, FactionType> IdToEnum = new()
        {
            { new ResourceId("nordic_rpg", "faction/player"),  FactionType.Player },
            { new ResourceId("nordic_rpg", "faction/guards"),  FactionType.Guards },
            { new ResourceId("nordic_rpg", "faction/bandits"), FactionType.Bandits },
            { new ResourceId("nordic_rpg", "faction/draugr"),  FactionType.Draugr },
            { new ResourceId("nordic_rpg", "faction/beasts"),  FactionType.Beasts },
            { new ResourceId("nordic_rpg", "faction/neutral"), FactionType.Neutral }
        };

        /// <summary>Maps a legacy <see cref="FactionType"/> enum value to its canonical <see cref="ResourceId"/>.</summary>
        public static ResourceId ToResourceId(FactionType faction) =>
            EnumToId.TryGetValue(faction, out ResourceId id)
                ? id
                : ResourceId.FromLegacyId(ResourceId.DefaultNamespace, "faction", faction.ToString());

        /// <summary>Attempts to map a <see cref="ResourceId"/> back to a legacy <see cref="FactionType"/> enum value.</summary>
        public static bool TryToLegacyEnum(ResourceId id, out FactionType faction) =>
            IdToEnum.TryGetValue(id, out faction);

        /// <summary>Creates the default Nordic RPG faction definitions and baseline dispositions.</summary>
        public static IReadOnlyList<FactionDefinition> CreateDefaultNordicFactions() => new[]
        {
            new FactionDefinition(
                new ResourceId("nordic_rpg", "faction/player"),
                "Player",
                FactionType.Player,
                new Dictionary<string, FactionDisposition>
                {
                    { "nordic_rpg:faction/player",  FactionDisposition.Friendly },
                    { "nordic_rpg:faction/guards",  FactionDisposition.Neutral },
                    { "nordic_rpg:faction/bandits", FactionDisposition.Hostile },
                    { "nordic_rpg:faction/draugr",  FactionDisposition.Hostile },
                    { "nordic_rpg:faction/beasts",  FactionDisposition.Hostile },
                    { "nordic_rpg:faction/neutral", FactionDisposition.Neutral }
                }),
            new FactionDefinition(
                new ResourceId("nordic_rpg", "faction/guards"),
                "Town Guards",
                FactionType.Guards,
                new Dictionary<string, FactionDisposition>
                {
                    { "nordic_rpg:faction/guards",  FactionDisposition.Friendly },
                    { "nordic_rpg:faction/player",  FactionDisposition.Neutral },
                    { "nordic_rpg:faction/bandits", FactionDisposition.Hostile },
                    { "nordic_rpg:faction/draugr",  FactionDisposition.Hostile },
                    { "nordic_rpg:faction/beasts",  FactionDisposition.Hostile }
                }),
            new FactionDefinition(
                new ResourceId("nordic_rpg", "faction/bandits"),
                "Bandits",
                FactionType.Bandits,
                new Dictionary<string, FactionDisposition>
                {
                    { "nordic_rpg:faction/bandits", FactionDisposition.Friendly },
                    { "nordic_rpg:faction/player",  FactionDisposition.Hostile },
                    { "nordic_rpg:faction/guards",  FactionDisposition.Hostile },
                    { "nordic_rpg:faction/draugr",  FactionDisposition.Hostile },
                    { "nordic_rpg:faction/beasts",  FactionDisposition.Hostile }
                }),
            new FactionDefinition(
                new ResourceId("nordic_rpg", "faction/draugr"),
                "Restless Draugr",
                FactionType.Draugr,
                new Dictionary<string, FactionDisposition>
                {
                    { "nordic_rpg:faction/draugr",  FactionDisposition.Friendly },
                    { "nordic_rpg:faction/player",  FactionDisposition.Hostile },
                    { "nordic_rpg:faction/guards",  FactionDisposition.Hostile },
                    { "nordic_rpg:faction/bandits", FactionDisposition.Hostile },
                    { "nordic_rpg:faction/beasts",  FactionDisposition.Hostile }
                }),
            new FactionDefinition(
                new ResourceId("nordic_rpg", "faction/beasts"),
                "Wild Beasts",
                FactionType.Beasts,
                new Dictionary<string, FactionDisposition>
                {
                    { "nordic_rpg:faction/beasts",  FactionDisposition.Friendly },
                    { "nordic_rpg:faction/player",  FactionDisposition.Hostile },
                    { "nordic_rpg:faction/guards",  FactionDisposition.Hostile },
                    { "nordic_rpg:faction/bandits", FactionDisposition.Hostile },
                    { "nordic_rpg:faction/draugr",  FactionDisposition.Hostile }
                }),
            new FactionDefinition(
                new ResourceId("nordic_rpg", "faction/neutral"),
                "Citizens & Merchants",
                FactionType.Neutral,
                new Dictionary<string, FactionDisposition>
                {
                    { "nordic_rpg:faction/neutral", FactionDisposition.Friendly },
                    { "nordic_rpg:faction/player",  FactionDisposition.Neutral },
                    { "nordic_rpg:faction/guards",  FactionDisposition.Friendly }
                })
        };

        #endregion
    }

    /// <summary>
    /// Legacy ScriptableObject: 2-D matrix describing how each faction views every other.
    /// Retained for optional Inspector overrides.
    /// </summary>
    [CreateAssetMenu(menuName = "3D2DWorld/Skyrim/Faction Relationship Matrix",
                     fileName = "FactionRelationshipMatrix")]
    public class FactionRelationshipMatrixSO : ScriptableObject
    {
        #region Serialized Data

        [Serializable]
        public struct FactionRow
        {
            public FactionType From;
            public FactionDispositionEntry[] Entries;
        }

        [Serializable]
        public struct FactionDispositionEntry
        {
            public FactionType To;
            public FactionDisposition Disposition;
        }

        [SerializeField] private FactionRow[] _rows;

        #endregion

        #region Public API

        public FactionDisposition GetDisposition(FactionType from, FactionType to)
        {
            if (_rows == null) return FactionDisposition.Neutral;

            foreach (var row in _rows)
            {
                if (row.From != from || row.Entries == null) continue;

                foreach (var entry in row.Entries)
                {
                    if (entry.To == to) return entry.Disposition;
                }
            }

            return FactionDisposition.Neutral;
        }

        public bool IsHostile(FactionType from, FactionType to) =>
            GetDisposition(from, to) == FactionDisposition.Hostile;

        #endregion
    }

    // ── Event ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when a faction's disposition toward another changes at runtime (bounty, crime, or reputation system).
    /// </summary>
    public class FactionDispositionChangedEvent : GameEvent
    {
        public FactionType From;
        public FactionType To;
        public ResourceId FromId;
        public ResourceId ToId;
        public FactionDisposition NewDisposition;

        public FactionDispositionChangedEvent(FactionType from, FactionType to, FactionDisposition disp)
        {
            From = from;
            To = to;
            FromId = FactionDefinition.ToResourceId(from);
            ToId = FactionDefinition.ToResourceId(to);
            NewDisposition = disp;
        }

        public FactionDispositionChangedEvent(ResourceId fromId, ResourceId toId, FactionDisposition disp)
        {
            FromId = fromId;
            ToId = toId;
            From = FactionDefinition.TryToLegacyEnum(fromId, out FactionType f) ? f : FactionType.Neutral;
            To = FactionDefinition.TryToLegacyEnum(toId, out FactionType t) ? t : FactionType.Neutral;
            NewDisposition = disp;
        }
    }
}
