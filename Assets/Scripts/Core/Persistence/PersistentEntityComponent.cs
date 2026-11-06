namespace Core.Persistence
{
    using System;
    using System.Collections.Generic;
    using Core.Ids;
    using Newtonsoft.Json;
    using UnityEngine;

    /// <summary>
    /// Assigns a stable, persistent <see cref="InstanceGuid"/> to any world entity, actor, chest, or lever
    /// so save files never rely on fragile <c>GameObject.name</c> matching.
    /// Collects all <see cref="ISaveableComponent"/>s on the same <see cref="GameObject"/>.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Core/Persistent Entity Identity")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class PersistentEntityComponent : MonoBehaviour
    {
        #region Static Active Registry

        private static readonly Dictionary<InstanceGuid, PersistentEntityComponent> ActiveByGuid = new();

        /// <summary>Read-only view of all currently enabled <see cref="PersistentEntityComponent"/>s in the scene.</summary>
        public static IReadOnlyDictionary<InstanceGuid, PersistentEntityComponent> ActiveEntities => ActiveByGuid;

        public static bool TryFindByGuid(InstanceGuid guid, out PersistentEntityComponent entity) =>
            ActiveByGuid.TryGetValue(guid, out entity);

        public static void ClearRegistry() => ActiveByGuid.Clear();

        #endregion

        #region Serialized Fields

        [SerializeField] private InstanceGuid _instanceId;
        [SerializeField] private ResourceId _definitionId;
        [SerializeField] private bool _isDynamicallySpawned;

        #endregion

        #region Properties

        public InstanceGuid InstanceId
        {
            get
            {
                EnsureRegistered();
                return _instanceId;
            }
            set
            {
                if (_instanceId == value) return;
                if (!_instanceId.IsEmpty && ActiveByGuid.TryGetValue(_instanceId, out var existing) && existing == this)
                {
                    ActiveByGuid.Remove(_instanceId);
                }

                _instanceId = value;
                if (this != null && isActiveAndEnabled && !_instanceId.IsEmpty)
                {
                    ActiveByGuid[_instanceId] = this;
                }
            }
        }

        public ResourceId DefinitionId
        {
            get => _definitionId;
            set
            {
                _definitionId = value;
                EnsureRegistered();
            }
        }

        public bool IsDynamicallySpawned
        {
            get => _isDynamicallySpawned;
            set
            {
                _isDynamicallySpawned = value;
                EnsureRegistered();
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureRegistered();
        }

        private void OnEnable()
        {
            EnsureRegistered();
        }

        private void OnDisable()
        {
            UnregisterSelf();
        }

        private void OnDestroy()
        {
            UnregisterSelf();
        }

        private void OnValidate()
        {
            if (_instanceId.IsEmpty)
            {
                _instanceId = InstanceGuid.NewGuid();
            }
        }

        public void EnsureRegistered()
        {
            if (_instanceId.IsEmpty)
            {
                _instanceId = InstanceGuid.NewGuid();
            }

            if (this != null && gameObject != null && gameObject.activeInHierarchy)
            {
                if (!ActiveByGuid.TryGetValue(_instanceId, out var existing) || existing != this)
                {
                    ActiveByGuid[_instanceId] = this;
                }
            }
        }

        private void UnregisterSelf()
        {
            if (!_instanceId.IsEmpty && ActiveByGuid.TryGetValue(_instanceId, out var current) && current == this)
            {
                ActiveByGuid.Remove(_instanceId);
            }
        }

        #endregion

        #region State Capture & Restoration

        /// <summary>
        /// Captures transform and all <see cref="ISaveableComponent"/> states on this <see cref="GameObject"/>.
        /// </summary>
        public EntitySaveRecord CaptureRecord(JsonSerializerSettings jsonSettings)
        {
            EnsureRegistered();

            var record = new EntitySaveRecord
            {
                InstanceId = _instanceId,
                DefinitionId = _definitionId,
                GameObjectName = gameObject.name,
                IsPlayer = gameObject.CompareTag("Player"),
                IsDynamicallySpawned = _isDynamicallySpawned,
                Position = transform.position,
                Rotation = transform.rotation
            };

            ISaveableComponent[] saveables = GetComponents<ISaveableComponent>();
            for (int i = 0; i < saveables.Length; i++)
            {
                ISaveableComponent comp = saveables[i];
                if (comp == null || string.IsNullOrWhiteSpace(comp.ComponentSaveKey)) continue;

                object stateObj = comp.CaptureComponentState();
                if (stateObj != null)
                {
                    record.ComponentStates[comp.ComponentSaveKey] = JsonConvert.SerializeObject(stateObj, jsonSettings);
                }
            }

            return record;
        }

        /// <summary>
        /// Restores transform and all matching <see cref="ISaveableComponent"/> states on this <see cref="GameObject"/>.
        /// </summary>
        public void RestoreRecord(EntitySaveRecord record, IWorldInstanceResolver resolver)
        {
            if (record == null) return;

            transform.position = record.Position;
            transform.rotation = record.Rotation;

            if (!record.DefinitionId.IsEmpty)
            {
                _definitionId = record.DefinitionId;
            }

            if (record.ComponentStates == null || record.ComponentStates.Count == 0)
            {
                return;
            }

            ISaveableComponent[] saveables = GetComponents<ISaveableComponent>();
            for (int i = 0; i < saveables.Length; i++)
            {
                ISaveableComponent comp = saveables[i];
                if (comp == null || string.IsNullOrWhiteSpace(comp.ComponentSaveKey)) continue;

                if (record.ComponentStates.TryGetValue(comp.ComponentSaveKey, out string stateJson) &&
                    !string.IsNullOrWhiteSpace(stateJson))
                {
                    comp.RestoreComponentState(stateJson, resolver);
                }
            }
        }

        #endregion
    }
}
