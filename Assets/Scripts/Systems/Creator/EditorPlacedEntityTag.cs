namespace Systems.Creator
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Core.Ids;
    using Core.Persistence;
    using Domain.Creator;
    using UnityEngine;

    /// <summary>
    /// Runtime tag attached to every object placed in the In-Game Creator Studio (<c>ARCH-05</c>).
    /// Holds its persistent <see cref="InstanceGuid"/>, <see cref="ResourceId"/>, property overrides,
    /// and outgoing <see cref="CrossObjectLink"/> wiring.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Creator/Editor Placed Entity Tag")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class EditorPlacedEntityTag : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private InstanceGuid _instanceId;
        [SerializeField] private ResourceId _definitionId;
        [SerializeField] private string _itemId;
        [SerializeField] private string _customLabel;
        [SerializeField] private List<CrossObjectLink> _links = new List<CrossObjectLink>();

        private readonly Dictionary<string, string> _propertyOverrides = new Dictionary<string, string>(StringComparer.Ordinal);

        #endregion

        #region Properties

        public InstanceGuid InstanceId
        {
            get
            {
                if (_instanceId.IsEmpty)
                {
                    var pec = GetComponent<PersistentEntityComponent>();
                    _instanceId = pec != null && !pec.InstanceId.IsEmpty ? pec.InstanceId : InstanceGuid.NewGuid();
                }
                return _instanceId;
            }
            set
            {
                _instanceId = value;
                var pec = GetComponent<PersistentEntityComponent>();
                if (pec != null && !value.IsEmpty)
                {
                    pec.InstanceId = value;
                }
            }
        }

        public ResourceId DefinitionId
        {
            get => _definitionId;
            set
            {
                _definitionId = value;
                var pec = GetComponent<PersistentEntityComponent>();
                if (pec != null && !value.IsEmpty)
                {
                    pec.DefinitionId = value;
                }
            }
        }

        public string ItemId
        {
            get => _itemId;
            set => _itemId = value;
        }

        public string CustomLabel
        {
            get => _customLabel;
            set => _customLabel = value;
        }

        public IDictionary<string, string> PropertyOverrides => _propertyOverrides;

        public IList<CrossObjectLink> Links => _links;

        #endregion

        #region Initialization & Snapshot Sync

        /// <summary>
        /// Applies a <see cref="PlacedObjectRecord"/>'s identity, overrides, and links onto this tag.
        /// </summary>
        public void ApplyRecord(PlacedObjectRecord record)
        {
            if (record == null) return;

            InstanceId = record.InstanceId.IsEmpty ? InstanceGuid.NewGuid() : record.InstanceId;
            DefinitionId = record.DefinitionId;
            _itemId = record.itemId;
            _customLabel = record.CustomLabel;

            _propertyOverrides.Clear();
            if (record.PropertyOverrides != null)
            {
                foreach (var kvp in record.PropertyOverrides)
                {
                    _propertyOverrides[kvp.Key] = kvp.Value;
                }
            }

            _links.Clear();
            if (record.Links != null)
            {
                for (int i = 0; i < record.Links.Count; i++)
                {
                    if (record.Links[i] != null)
                    {
                        _links.Add(record.Links[i].Clone());
                    }
                }
            }
        }

        /// <summary>
        /// Captures this placed entity's current transform, identity, property overrides, and links.
        /// </summary>
        public PlacedObjectRecord ToRecord()
        {
            var record = new PlacedObjectRecord
            {
                InstanceId = InstanceId,
                DefinitionId = _definitionId,
                CustomLabel = _customLabel,
                itemId = _itemId
            };
            record.SetTransform(transform);

            foreach (var kvp in _propertyOverrides)
            {
                record.PropertyOverrides[kvp.Key] = kvp.Value;
            }

            for (int i = 0; i < _links.Count; i++)
            {
                if (_links[i] != null)
                {
                    record.Links.Add(_links[i].Clone());
                }
            }

            return record;
        }

        #endregion

        #region Link Execution

        /// <summary>
        /// Fires all outgoing <see cref="CrossObjectLink"/>s matching <paramref name="outputPortName"/>
        /// (or <c>"OnActivated"</c> / <c>"*"</c>), resolving targets by <see cref="InstanceGuid"/>.
        /// </summary>
        public int TriggerOutputPort(string outputPortName)
        {
            if (_links == null || _links.Count == 0)
            {
                return 0;
            }

            ServiceLocator.TryGet<CreatorStudioService>(out var studio);
            int executedCount = 0;

            for (int i = 0; i < _links.Count; i++)
            {
                CrossObjectLink link = _links[i];
                if (link == null || link.TargetGuid.IsEmpty) continue;

                bool portMatches = string.IsNullOrWhiteSpace(outputPortName) ||
                                   string.Equals(link.OutputPortName, outputPortName, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(link.OutputPortName, "OnActivated", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(link.OutputPortName, "*", StringComparison.Ordinal);

                if (!portMatches) continue;

                GameObject targetGo = null;
                if (studio != null && studio.TryGetPlacedObject(link.TargetGuid, out var foundGo))
                {
                    targetGo = foundGo;
                }
                else if (PersistentEntityComponent.TryFindByGuid(link.TargetGuid, out var pec) && pec != null)
                {
                    targetGo = pec.gameObject;
                }

                if (targetGo == null) continue;

                string action = string.IsNullOrWhiteSpace(link.TargetActionName) ? "Activate" : link.TargetActionName;
                ILinkReceiver[] receivers = targetGo.GetComponents<ILinkReceiver>();
                if (receivers != null && receivers.Length > 0)
                {
                    for (int r = 0; r < receivers.Length; r++)
                    {
                        receivers[r]?.ExecuteLinkAction(action, gameObject);
                    }
                    executedCount++;
                }
                else
                {
                    // Default fallback for plain structural GameObjects (e.g. doors/gates without custom script)
                    if (string.Equals(action, "Toggle", StringComparison.OrdinalIgnoreCase))
                    {
                        targetGo.SetActive(!targetGo.activeSelf);
                    }
                    else if (string.Equals(action, "Deactivate", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(action, "Close", StringComparison.OrdinalIgnoreCase))
                    {
                        targetGo.SetActive(false);
                    }
                    else
                    {
                        targetGo.SetActive(true);
                    }
                    executedCount++;
                }
            }

            return executedCount;
        }

        #endregion
    }
}
