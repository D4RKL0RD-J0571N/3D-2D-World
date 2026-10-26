namespace Systems.Creator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Core;
    using Domain.Creator;
    using UnityEngine;

    /// <summary>
    /// Service managing the state, active tool, and persistence for the In-Game Creator Studio.
    /// Registered with ServiceLocator at startup.
    /// </summary>
    public class CreatorStudioService : IGameService
    {
        #region Fields & State

        private bool _isBuildMode = false;
        private CreatorItemDefinition _activeItem;
        private CreatorCatalogSO _catalog;
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly Dictionary<GameObject, string> _objectToIdMap = new Dictionary<GameObject, string>();

        private string _saveDirectory;

        #endregion

        #region Properties

        public bool IsBuildMode => _isBuildMode;
        public CreatorItemDefinition ActiveItem => _activeItem;
        public CreatorCatalogSO Catalog => _catalog;
        public IReadOnlyList<GameObject> SpawnedObjects => _spawnedObjects;

        #endregion

        #region Lifecycle & Initialization

        public void Initialize()
        {
            _saveDirectory = Path.Combine(Application.dataPath, "Data", "SavedWorlds");
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }

            Debug.Log($"[CreatorStudioService] Initialized. Save directory: {_saveDirectory}");
        }

        public void Shutdown()
        {
            ClearAllSpawnedObjects();
            _catalog = null;
            _activeItem = null;
            Debug.Log("[CreatorStudioService] Shutdown complete.");
        }

        public void SetCatalog(CreatorCatalogSO catalog)
        {
            _catalog = catalog;
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

        #region Object Tracking

        public void RegisterSpawnedObject(GameObject obj, string itemId)
        {
            if (obj == null) return;
            _spawnedObjects.Add(obj);
            _objectToIdMap[obj] = itemId;

            GameEventManager.Publish(new CreatorObjectPlacedEvent(itemId, obj.transform.position, obj.transform.rotation));
        }

        public void UnregisterSpawnedObject(GameObject obj)
        {
            if (obj == null) return;
            _spawnedObjects.Remove(obj);
            _objectToIdMap.Remove(obj);

            GameEventManager.Publish(new CreatorObjectRemovedEvent(obj));
        }

        public void ClearAllSpawnedObjects()
        {
            foreach (var obj in _spawnedObjects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }
            _spawnedObjects.Clear();
            _objectToIdMap.Clear();
        }

        #endregion

        #region Level Persistence (Save / Load)

        public bool SaveLevel(string levelName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(levelName))
                {
                    levelName = "CustomLevel_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                }

                var levelData = new CreatorLevelData
                {
                    levelName = levelName,
                    author = "Player",
                    createdAt = DateTime.Now.ToString("o"),
                    modifiedAt = DateTime.Now.ToString("o")
                };

                foreach (var obj in _spawnedObjects)
                {
                    if (obj == null) continue;
                    if (_objectToIdMap.TryGetValue(obj, out string itemId))
                    {
                        var record = new PlacedObjectRecord();
                        record.itemId = itemId;
                        record.SetTransform(obj.transform);
                        levelData.placedObjects.Add(record);
                    }
                }

                string filePath = Path.Combine(_saveDirectory, $"{levelName}.json");
                string json = JsonUtility.ToJson(levelData, true);
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
                string filePath = Path.Combine(_saveDirectory, $"{levelName}.json");
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[CreatorStudioService] Level file not found: {filePath}");
                    return false;
                }

                string json = File.ReadAllText(filePath);
                var levelData = JsonUtility.FromJson<CreatorLevelData>(json);
                if (levelData == null) return false;

                ClearAllSpawnedObjects();

                foreach (var record in levelData.placedObjects)
                {
                    var def = _catalog?.GetItemById(record.itemId);
                    if (def != null && def.prefab != null)
                    {
                        var instance = UnityEngine.Object.Instantiate(def.prefab, record.GetPosition(), record.GetRotation());
                        instance.transform.localScale = record.GetScale();
                        RegisterSpawnedObject(instance, record.itemId);
                    }
                }

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
