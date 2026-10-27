namespace Systems.Skyrim
{
    using System.Collections.Generic;
    using Core;
    using Domain.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Manages faction relationships at runtime: reads base dispositions from a ScriptableObject
    /// and supports per-session overrides for bounty / crime systems.
    /// </summary>
    public class FactionService : IGameService
    {
        #region Fields

        private FactionRelationshipMatrixSO _matrix;

        /// <summary>Runtime overrides that supersede the SO's base values.</summary>
        private readonly Dictionary<(FactionType, FactionType), FactionDisposition> _overrides = new();

        #endregion

        #region IGameService

        public void Initialize()
        {
            _matrix = Resources.Load<FactionRelationshipMatrixSO>("Skyrim/FactionRelationshipMatrix");

#if UNITY_EDITOR
            if (_matrix == null)
            {
                _matrix = UnityEditor.AssetDatabase.LoadAssetAtPath<FactionRelationshipMatrixSO>(
                    "Assets/Data/Skyrim/FactionRelationshipMatrix.asset");
            }
#endif
            if (_matrix == null)
            {
                Debug.LogWarning("[FactionService] No FactionRelationshipMatrixSO found. " +
                                 "Create one at Assets/Data/Skyrim/ via the Create menu.");
            }
        }

        public void Shutdown()
        {
            _overrides.Clear();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Returns the effective disposition of <paramref name="from"/> toward <paramref name="to"/>,
        /// checking runtime overrides first then the base ScriptableObject.
        /// </summary>
        public FactionDisposition GetDisposition(FactionType from, FactionType to)
        {
            if (_overrides.TryGetValue((from, to), out var disp))
                return disp;

            return _matrix != null
                ? _matrix.GetDisposition(from, to)
                : FactionDisposition.Neutral;
        }

        /// <summary>
        /// Sets a runtime override disposition and fires <see cref="FactionDispositionChangedEvent"/>.
        /// </summary>
        public void SetDisposition(FactionType from, FactionType to, FactionDisposition disp)
        {
            _overrides[(from, to)] = disp;
            GameEventManager.Publish(new FactionDispositionChangedEvent(from, to, disp));
        }

        /// <summary>Returns true if <paramref name="from"/> is currently hostile toward <paramref name="to"/>.</summary>
        public bool IsHostile(FactionType from, FactionType to) =>
            GetDisposition(from, to) == FactionDisposition.Hostile;

        #endregion
    }
}
