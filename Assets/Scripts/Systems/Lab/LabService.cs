namespace Systems.Lab
{
    using System.Collections.Generic;
    using Core;
    using Domain.Lab;
    using UnityEngine;

    /// <summary>
    /// Service managing all registered LabParameterGroups.
    /// Provides get/set API for runtime parameter tweaking.
    /// Groups are loaded from Resources or manually registered.
    /// </summary>
    public sealed class LabService : IGameService
    {
        #region Fields
        private readonly List<LabParameterGroup> _groups = new();
        #endregion

        #region Properties
        /// <summary>All registered parameter groups.</summary>
        public IReadOnlyList<LabParameterGroup> Groups => _groups;
        #endregion

        #region IGameService
        public void Initialize()
        {
            LoadGroupsFromResources();
            ResetAll();
        }

        public void Shutdown()
        {
            _groups.Clear();
        }
        #endregion

        #region Public API
        /// <summary>Manually registers a group (e.g. from a mod or runtime asset).</summary>
        public void RegisterGroup(LabParameterGroup group)
        {
            if (group != null && !_groups.Contains(group))
                _groups.Add(group);
        }

        /// <summary>Sets a parameter value and publishes a change event.</summary>
        public void SetValue(string groupId, string parameterId, float value)
        {
            if (!TryGetParam(groupId, parameterId, out var group, out var param)) return;
            float old = param.CurrentValue;
            param.SetValue(value);
            GameEventManager.Publish(new LabParameterChangedEvent
            {
                GroupId     = groupId,
                ParameterId = parameterId,
                OldValue    = old,
                NewValue    = param.CurrentValue
            });
        }

        /// <summary>Gets the current float value of a parameter.</summary>
        public bool TryGetValue(string groupId, string parameterId, out float value)
        {
            value = 0f;
            if (!TryGetParam(groupId, parameterId, out _, out var param)) return false;
            value = param.CurrentValue;
            return true;
        }

        /// <summary>Resets all groups to their default values.</summary>
        public void ResetAll()
        {
            foreach (var g in _groups)
                g.ResetAll();
        }
        #endregion

        #region Helpers
        private void LoadGroupsFromResources()
        {
            var groups = Resources.LoadAll<LabParameterGroup>("Config/Lab");
            foreach (var g in groups)
                RegisterGroup(g);
        }

        private bool TryGetParam(string groupId, string parameterId,
            out LabParameterGroup group, out LabParameter param)
        {
            param = null;
            group = null;
            foreach (var g in _groups)
            {
                if (g.GroupId == groupId)
                {
                    group = g;
                    return g.TryGet(parameterId, out param);
                }
            }
            return false;
        }
        #endregion
    }
}
