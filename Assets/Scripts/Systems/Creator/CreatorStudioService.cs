namespace Systems.Creator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Core;
    using Core.Ids;
    using Core.Persistence;
    using Domain.Creator;
    using Newtonsoft.Json;
    using UnityEngine;

    /// <summary>
    /// Service managing the state, active tool, <see cref="ICommandHistory"/> Undo/Redo stack,
    /// <see cref="EditorPlaytestCoordinator"/>, <see cref="CrossObjectLink"/> wiring, and persistence
    /// for the In-Game Creator Studio (<c>ARCH-05</c>).
    /// </summary>
    public class CreatorStudioService : IGameService, IWorldInstanceResolver
    {
        #region Fields & State

        private bool _isBuildMode = false;
        private CreatorItemDefinition _activeItem;
        private CreatorCatalogSO _catalog;

        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly Dictionary<GameObject, string> _objectToIdMap = new Dictionary<GameObject, string>();
        private readonly Dictionary<InstanceGuid, GameObject> _placedByGuid = new Dictionary<InstanceGuid, GameObject>();
        private readonly Dictionary<string, GameObject> _customTemplates = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        private readonly CommandHistory _commandHistory = new CommandHistory(128);
        private readonly EditorPlaytestCoordinator _playtestCoordinator;

        private string _saveDirectory;

        #endregion

        #region Constructors

        public CreatorStudioService()
        {
            _playtestCoordinator = new EditorPlaytestCoordinator(this);
        }

        #endregion

        #region Properties

        public bool IsBuildMode => _isBuildMode;
        public CreatorItemDefinition ActiveItem => _activeItem;
        public CreatorCatalogSO Catalog => _catalog;
        public IReadOnlyList<GameObject> SpawnedObjects => _spawnedObjects;
        public IReadOnlyDictionary<InstanceGuid, GameObject> PlacedByGuid => _placedByGuid;
        public ICommandHistory CommandHistory => _commandHistory;
        public EditorPlaytestCoordinator PlaytestCoordinator => _playtestCoordinator;

        public string SaveDirectory
        {
            get => _saveDirectory;
            set => _saveDirectory = value;
        }

        #endregion

        #region Lifecycle & Initialization

        public void Initialize()
        {
            if (string.IsNullOrWhiteSpace(_saveDirectory))
            {
                _saveDirectory = Path.Combine(Application.dataPath, "Data", "SavedWorlds");
            }

            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }

            Debug.Log($"[CreatorStudioService] Initialized. Save directory: {_saveDirectory}");
        }

        public void Shutdown()
        {
            ClearAllSpawnedObjects();
            _commandHistory.Clear();
            _customTemplates.Clear();
            _catalog = null;
            _activeItem = null;
            Debug.Log("[CreatorStudioService] Shutdown complete.");
        }

        public void SetCatalog(CreatorCatalogSO catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// Registers a runtime or test template prefab by <paramref name="itemId"/> so <see cref="SpawnFromRecord"/>
        /// can instantiate objects even when no <see cref="CreatorCatalogSO"/> is assigned.
        /// </summary>
        public void RegisterTemplatePrefab(string itemId, GameObject templatePrefab)
        {
            if (string.IsNullOrWhiteSpace(itemId) || templatePrefab == null) return;
            _customTemplates[itemId] = templatePrefab;
        }

        #endregion

        #region Mode & Item Selection

        public void SetBuildMode(bool buildMode)
        {
            if (_isBuildMode == buildMode) return;
            _isBuildMode = buildMode;

            Debug.Log($"[CreatorStudioService] Build mode toggled: {_isBuildMode}");
            GameEventManager.Publish(new CreatorModeChangedEvent(_isBuildMode));
        }

        public void ToggleBuildMode()
        {
            SetBuildMode(!_isBuildMode);
        }

        public void SelectItem(CreatorItemDefinition item)
        {
            _activeItem = item;
            Debug.Log($"[CreatorStudioService] Selected active item: {item?.displayName ?? "None"}");
            GameEventManager.Publish(new CreatorItemSelectedEvent(item));
        }

        #endregion

        #region Object Tracking & Persistent Identity (InstanceGuid)

        public InstanceGuid RegisterSpawnedObject(GameObject obj, string itemId, InstanceGuid? explicitGuid = null)
        {
            if (obj == null) return InstanceGuid.Empty;

            var tag = obj.GetComponent<EditorPlacedEntityTag>();
            if (tag == null)
            {
                tag = obj.AddComponent<EditorPlacedEntityTag>();
            }

            var pec = obj.GetComponent<PersistentEntityComponent>();
            if (pec == null)
            {
                pec = obj.AddComponent<PersistentEntityComponent>();
            }

            InstanceGuid guid = explicitGuid.HasValue && !explicitGuid.Value.IsEmpty
                ? explicitGuid.Value
                : (!tag.InstanceId.IsEmpty ? tag.InstanceId : InstanceGuid.NewGuid());

            tag.InstanceId = guid;
            tag.ItemId = itemId;
            pec.InstanceId = guid;

            var def = _catalog?.GetItemById(itemId);
            if (def != null)
            {
                tag.DefinitionId = def.ContentId;
                pec.DefinitionId = def.ContentId;
            }

            obj.name = $"{(def != null ? def.displayName : itemId)}_{guid.Value.Substring(0, Math.Min(8, guid.Value.Length))}";

            if (!_spawnedObjects.Contains(obj))
            {
                _spawnedObjects.Add(obj);
            }
            _objectToIdMap[obj] = itemId;
            _placedByGuid[guid] = obj;

            GameEventManager.Publish(new CreatorObjectPlacedEvent(itemId, obj.transform.position, obj.transform.rotation));
            return guid;
        }

        public void UnregisterSpawnedObject(GameObject obj)
        {
            if (obj == null) return;

            var tag = obj.GetComponent<EditorPlacedEntityTag>();
            if (tag != null && !tag.InstanceId.IsEmpty)
            {
                _placedByGuid.Remove(tag.InstanceId);
            }

            _spawnedObjects.Remove(obj);
            _objectToIdMap.Remove(obj);

            GameEventManager.Publish(new CreatorObjectRemovedEvent(obj));
        }

        public bool RemovePlacedObjectByGuid(InstanceGuid guid, bool destroyGameObject = true)
        {
            if (guid.IsEmpty || !_placedByGuid.TryGetValue(guid, out GameObject obj))
            {
                return false;
            }

            _placedByGuid.Remove(guid);
            if (obj != null)
            {
                _spawnedObjects.Remove(obj);
                _objectToIdMap.Remove(obj);
                GameEventManager.Publish(new CreatorObjectRemovedEvent(obj));

                if (destroyGameObject)
                {
                    DestroySafe(obj);
                }
            }
            return true;
        }

        public bool TryGetPlacedObject(InstanceGuid guid, out GameObject placedObject)
        {
            if (!guid.IsEmpty && _placedByGuid.TryGetValue(guid, out placedObject) && placedObject != null)
            {
                return true;
            }

            placedObject = null;
            return false;
        }

        public bool TryResolveGameObject(InstanceGuid instanceId, out GameObject instance) =>
            TryGetPlacedObject(instanceId, out instance);

        public bool TryResolveComponent<T>(InstanceGuid instanceId, out T component) where T : Component
        {
            if (TryGetPlacedObject(instanceId, out GameObject go) && go != null)
            {
                component = go.GetComponent<T>();
                return component != null;
            }

            component = null;
            return false;
        }

        public void RecordUnknownContentPlaceholder(UnknownContentPlaceholder placeholder)
        {
            // No-op during editor playtest snapshot restoration
        }

        public bool TryCaptureRecord(InstanceGuid guid, out PlacedObjectRecord record)
        {
            if (TryGetPlacedObject(guid, out GameObject go) && go != null)
            {
                var tag = go.GetComponent<EditorPlacedEntityTag>();
                if (tag != null)
                {
                    record = tag.ToRecord();
                    return true;
                }

                _objectToIdMap.TryGetValue(go, out string itemId);
                record = new PlacedObjectRecord
                {
                    InstanceId = guid,
                    itemId = itemId
                };
                record.SetTransform(go.transform);
                return true;
            }

            record = null;
            return false;
        }

        public GameObject SpawnFromRecord(PlacedObjectRecord record)
        {
            if (record == null) return null;

            InstanceGuid guid = record.InstanceId.IsEmpty ? InstanceGuid.NewGuid() : record.InstanceId;
            if (_placedByGuid.TryGetValue(guid, out GameObject existing) && existing != null)
            {
                RemovePlacedObjectByGuid(guid, destroyGameObject: true);
            }

            GameObject prefab = ResolvePrefabForRecord(record);
            GameObject instance;

            if (prefab != null)
            {
                instance = UnityEngine.Object.Instantiate(prefab, record.GetPosition(), record.GetRotation());
                instance.SetActive(true);
            }
            else
            {
                instance = new GameObject(string.IsNullOrWhiteSpace(record.itemId) ? "PlacedEntity" : record.itemId);
                instance.transform.SetPositionAndRotation(record.GetPosition(), record.GetRotation());
            }

            instance.transform.localScale = record.GetScale();
            RegisterSpawnedObject(instance, record.itemId, guid);

            var tag = instance.GetComponent<EditorPlacedEntityTag>();
            if (tag != null)
            {
                record.InstanceId = guid;
                tag.ApplyRecord(record);
            }

            return instance;
        }

        public bool SetTransformByGuid(InstanceGuid guid, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!TryGetPlacedObject(guid, out GameObject go) || go == null)
            {
                return false;
            }

            go.transform.position = position;
            go.transform.rotation = rotation;
            go.transform.localScale = scale;
            return true;
        }

        public void AddLink(InstanceGuid sourceGuid, CrossObjectLink link)
        {
            if (link == null || !TryGetPlacedObject(sourceGuid, out GameObject go) || go == null)
            {
                return;
            }

            var tag = go.GetComponent<EditorPlacedEntityTag>();
            if (tag != null)
            {
                tag.Links.Add(link.Clone());
            }
        }

        public void RemoveLink(InstanceGuid sourceGuid, CrossObjectLink link)
        {
            if (link == null || !TryGetPlacedObject(sourceGuid, out GameObject go) || go == null)
            {
                return;
            }

            var tag = go.GetComponent<EditorPlacedEntityTag>();
            if (tag == null) return;

            for (int i = tag.Links.Count - 1; i >= 0; i--)
            {
                CrossObjectLink current = tag.Links[i];
                if (current != null &&
                    current.TargetGuid == link.TargetGuid &&
                    string.Equals(current.OutputPortName, link.OutputPortName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(current.TargetActionName, link.TargetActionName, StringComparison.OrdinalIgnoreCase))
                {
                    tag.Links.RemoveAt(i);
                    break;
                }
            }
        }

        public void ClearAllSpawnedObjects()
        {
            for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
            {
                var obj = _spawnedObjects[i];
                if (obj != null)
                {
                    DestroySafe(obj);
                }
            }
            _spawnedObjects.Clear();
            _objectToIdMap.Clear();
            _placedByGuid.Clear();
        }

        private GameObject ResolvePrefabForRecord(PlacedObjectRecord record)
        {
            if (record == null) return null;

            if (!string.IsNullOrWhiteSpace(record.itemId))
            {
                if (_customTemplates.TryGetValue(record.itemId, out GameObject customPrefab) && customPrefab != null)
                {
                    return customPrefab;
                }

                var def = _catalog?.GetItemById(record.itemId);
                if (def != null && def.prefab != null)
                {
                    return def.prefab;
                }
            }

            return null;
        }

        private static void DestroySafe(GameObject obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        #endregion

        #region Level Persistence (Save / Load WorldAuthoringData v2)

        public CreatorLevelData CaptureLevelData(string levelName = "Custom Level")
        {
            var levelData = new CreatorLevelData
            {
                schemaVersion = 2,
                levelName = levelName,
                author = "Player",
                createdAt = DateTime.UtcNow.ToString("o"),
                modifiedAt = DateTime.UtcNow.ToString("o")
            };

            for (int i = 0; i < _spawnedObjects.Count; i++)
            {
                GameObject obj = _spawnedObjects[i];
                if (obj == null) continue;

                var tag = obj.GetComponent<EditorPlacedEntityTag>();
                if (tag != null)
                {
                    levelData.placedObjects.Add(tag.ToRecord());
                }
                else if (_objectToIdMap.TryGetValue(obj, out string itemId))
                {
                    var record = new PlacedObjectRecord { itemId = itemId };
                    record.SetTransform(obj.transform);
                    levelData.placedObjects.Add(record);
                }
            }

            return levelData;
        }

        public void RestoreLevelData(CreatorLevelData levelData, bool clearCommandHistory = false)
        {
            if (levelData == null) return;

            ClearAllSpawnedObjects();
            if (clearCommandHistory)
            {
                _commandHistory.Clear();
            }

            if (levelData.placedObjects == null) return;

            for (int i = 0; i < levelData.placedObjects.Count; i++)
            {
                PlacedObjectRecord record = levelData.placedObjects[i];
                if (record == null) continue;

                if (record.InstanceId.IsEmpty)
                {
                    record.InstanceId = InstanceGuid.NewGuid();
                }

                SpawnFromRecord(record);
            }
        }

        public bool SaveLevel(string levelName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(levelName))
                {
                    levelName = "CustomLevel_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                }

                if (string.IsNullOrWhiteSpace(_saveDirectory))
                {
                    Initialize();
                }

                CreatorLevelData levelData = CaptureLevelData(levelName);
                string filePath = Path.Combine(_saveDirectory, $"{levelName}.json");
                string json = JsonConvert.SerializeObject(levelData, FantasyEngineJsonSettings.CreateDefault());
                File.WriteAllText(filePath, json);

                Debug.Log($"[CreatorStudioService] Successfully saved level '{levelName}' with {levelData.placedObjects.Count} objects to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CreatorStudioService] Error saving level: {ex.Message}");
                return false;
            }
        }

        public bool LoadLevel(string levelName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_saveDirectory))
                {
                    Initialize();
                }

                string filePath = Path.Combine(_saveDirectory, $"{levelName}.json");
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[CreatorStudioService] Level file not found: {filePath}");
                    return false;
                }

                string json = File.ReadAllText(filePath);
                var levelData = JsonConvert.DeserializeObject<CreatorLevelData>(json, FantasyEngineJsonSettings.CreateDefault())
                                ?? JsonUtility.FromJson<CreatorLevelData>(json);
                if (levelData == null) return false;

                RestoreLevelData(levelData, clearCommandHistory: true);

                Debug.Log($"[CreatorStudioService] Successfully loaded level '{levelName}' with {levelData.placedObjects.Count} objects.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CreatorStudioService] Error loading level: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
