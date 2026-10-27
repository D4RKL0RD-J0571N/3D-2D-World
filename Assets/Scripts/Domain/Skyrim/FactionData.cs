namespace Domain.Skyrim
{
    using System;
    using Core;
    using UnityEngine;

    /// <summary>
    /// The known factions in the world. Used for disposition and aggression logic.
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
    /// ScriptableObject: 2-D matrix describing how each faction views every other.
    /// Editable in the Inspector — no code changes needed to tune faction politics.
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

        /// <summary>
        /// Returns the base disposition of <paramref name="from"/> toward <paramref name="to"/>.
        /// Falls back to Neutral if no row is defined.
        /// </summary>
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

        /// <summary>Returns true if <paramref name="from"/> is hostile toward <paramref name="to"/>.</summary>
        public bool IsHostile(FactionType from, FactionType to) =>
            GetDisposition(from, to) == FactionDisposition.Hostile;

        #endregion
    }

    // ── Event ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when a faction's disposition toward another changes at runtime (bounty, crime system).
    /// </summary>
    public class FactionDispositionChangedEvent : GameEvent
    {
        public FactionType From;
        public FactionType To;
        public FactionDisposition NewDisposition;

        public FactionDispositionChangedEvent(FactionType from, FactionType to, FactionDisposition disp)
        {
            From           = from;
            To             = to;
            NewDisposition = disp;
        }
    }
}
