#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
    // Home-scene snapshots have a separate lifetime from pending full-load applications.
    // Routing supplies the current manager for world queries and the exact restoration owner.
    internal sealed class ComponentHomeSceneCache
    {
        readonly ComponentRestoreState restoration;
        readonly Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>> sceneSnapshots = new();
        readonly Dictionary<string, Dictionary<string, string>> scenePrefabRefs = new();
        internal IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, byte[]>>> Snapshots { get; }

        internal ComponentHomeSceneCache(ComponentRestoreState restoration)
        {
            this.restoration = restoration;
            Snapshots = new ReadOnlyDictionary<string, Dictionary<string, Dictionary<string, byte[]>>>(sceneSnapshots);
        }

        internal void SnapshotObjectToSceneCache(GameObject obj, SaveManager manager)
        {
            if (obj == null)
                return;
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid())
                return;
            SnapshotObjectToSceneCache(obj, active.name, manager);
        }

        /// <summary>
        /// Internal helper to snapshot a single object into a specific scene cache bucket.
        /// </summary>
        private void SnapshotObjectToSceneCache(GameObject obj, string sceneName, SaveManager manager)
        {
            if (obj == null || string.IsNullOrEmpty(sceneName))
                return;
            // Determine this object's identity — prefer UniqueID component,
            // fall back to SaveablePrefab.UniqueID for prefabs that manage
            // their own identity (e.g. SaveablePrefab + RememberComposite
            // which intentionally removes the UniqueID component).
            string objectID = null;
            var uid = obj.GetComponent<UniqueID>();
            if (uid != null && !string.IsNullOrEmpty(uid.ID))
            {
                objectID = uid.ID;
            }
            else
            {
                var sp = obj.GetComponent<SaveablePrefab>();
                if (sp != null && !string.IsNullOrEmpty(sp.UniqueID))
                    objectID = sp.UniqueID;
            }

            if (string.IsNullOrEmpty(objectID))
                return;
            var sceneDict = GetOrCreate(sceneName);
            var perObject = new Dictionary<string, byte[]>();
            // Track prefab reference for Last Snapshot Scene restoration
            string prefabRef = null;
            var saveablePrefab = obj.GetComponent<SaveablePrefab>();
            if (saveablePrefab != null && !string.IsNullOrEmpty(saveablePrefab.PrefabAssetID))
                prefabRef = saveablePrefab.PrefabAssetID;
            else if (obj.TryGetComponent<SaveableComponent>(out var scParent) && !string.IsNullOrEmpty(scParent.HomeScenePrefabID))
                prefabRef = scParent.HomeScenePrefabID;
            if (!string.IsNullOrEmpty(prefabRef))
                GetOrCreatePrefabMap(sceneName)[objectID] = prefabRef;
            int compCount = 0;
            foreach (var comp in obj.GetComponentsInChildren<ISaveable>(includeInactive: true))
            {
                // Skip entries whose underlying Unity object has been destroyed
                if (comp is MonoBehaviour mb && mb == null)
                    continue;
                if (!ComponentManager.IsValidSaveable(comp, nameof(SnapshotObjectToSceneCache)))
                    continue;
                // Skip if this component belongs to a GameObject that is marked destroyed
                try
                {
                    var goUid = ComponentRestoreEntry.ExtractGameObjectID(comp.UniqueIdentifier);
                    if (!string.IsNullOrEmpty(goUid) && manager != null && manager.IsGameObjectDestroyed(goUid))
                        continue;
                }
                catch
                { /* best-effort guard */
                }

                if (comp is SaveableComponent sc && sc.RememberHomeScene && sc.HomeSceneMode == HomeSceneMode.LastSnapshotScene)
                {
                    sc.HomeScene = sceneName;
                }

                try
                {
                    // Enable snapshot capture mode to bypass "Skip Saving When Unchanged" optimization
                    // This ensures we always capture the current state for proper restoration
                    SaveableComponent.SetSnapshotCaptureMode(true);
                    try
                    {
                        var bytes = comp.SaveData();
                        if (bytes != null && bytes.Length > 0)
                        {
                            perObject[comp.UniqueIdentifier] = bytes;
                            compCount++;
                        }
                    }
                    finally
                    {
                        SaveableComponent.SetSnapshotCaptureMode(false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"SnapshotObjectToSceneCache('{sceneName}'): Error serializing '{comp.UniqueIdentifier}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ComponentManager, LogLevel.Error);
                }
            }

            sceneDict[objectID] = perObject;
            Logger.Log($"[RememberHome] SnapshotObject: '{obj.name}' ({objectID}) -> scene '{sceneName}', Components={compCount}", LogCategory.ComponentManager, LogLevel.Info);
        }

        /// <summary>
        /// Public helper to snapshot the entire scene's registered objects.
        /// </summary>
        internal void SnapshotCurrentSceneAll(SaveManager manager)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return;
            SnapshotSceneAll(scene, manager);
        }

        /// <summary>
        /// Snapshots all <see cref = "ISaveable"/> components in the provided scene into the per-scene cache.
        /// </summary>
        internal void SnapshotSceneAll(Scene scene, SaveManager manager)
        {
            if (!scene.IsValid())
                return;
            // Start from a clean slate for this scene
            ClearSceneSnapshot(scene.name);
            // Iterate root objects to avoid crossing into other scenes
            var roots = scene.GetRootGameObjects();
            string sceneName = scene.name;
            int uidCount = 0;
            int totalComponents = 0;
            Logger.Log($"[RememberHome] SnapshotSceneAll: Begin capture for scene '{sceneName}'. Root count={roots?.Length ?? 0}", LogCategory.ComponentManager, LogLevel.Info);
            // Track which GameObjects have already been snapshotted to avoid
            // double-processing when an object has both UniqueID and SaveablePrefab
            var snapshotted = new HashSet<GameObject>();
            foreach (var root in roots)
            {
                if (root == null)
                    continue;
                // Pass 1: Snapshot objects with a UniqueID component
                var uniques = root.GetComponentsInChildren<UniqueID>(includeInactive: true);
                foreach (var uid in uniques)
                {
                    if (uid == null || string.IsNullOrEmpty(uid.ID))
                        continue;
                    SnapshotObjectToSceneCache(uid.gameObject, sceneName, manager);
                    snapshotted.Add(uid.gameObject);
                    uidCount++;
                }

                // Pass 2: Snapshot SaveablePrefab objects that have NO
                // UniqueID component (e.g. SaveablePrefab + RememberComposite
                // where UniqueID is intentionally removed). These prefabs
                // manage their own identity via SaveablePrefab.UniqueID.
                var prefabs = root.GetComponentsInChildren<SaveablePrefab>(includeInactive: true);
                foreach (var sp in prefabs)
                {
                    if (sp == null || string.IsNullOrEmpty(sp.UniqueID))
                        continue;
                    if (snapshotted.Contains(sp.gameObject))
                        continue; // already handled
                    SnapshotObjectToSceneCache(sp.gameObject, sceneName, manager);
                    snapshotted.Add(sp.gameObject);
                    uidCount++;
                }
            }

            if (sceneSnapshots.TryGetValue(sceneName, out var perScene))
            {
                foreach (var kv in perScene)
                    totalComponents += kv.Value?.Count ?? 0;
            }

            Logger.Log($"[RememberHome] SnapshotSceneAll: Done scene '{sceneName}'. Objects={uidCount}, Components={totalComponents}", LogCategory.ComponentManager, LogLevel.Info);
        }

        /// <summary>
        /// Clears cached snapshots for a specific scene name.
        /// </summary>
        internal void ClearSceneSnapshot(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return;
            if (sceneSnapshots.Remove(sceneName))
                Logger.Log($"ComponentManager: Cleared snapshot cache for scene '{sceneName}'.", LogCategory.ComponentManager, LogLevel.Info);
        }

        /// <summary>
        /// Attempts to apply a previously cached snapshot to a component when it registers
        /// and the current scene matches its home scene preference.
        /// </summary>
        internal void TryApplyRememberedSnapshot(ISaveable component)
        {
            if (component is not SaveableComponent sc)
                return;
            if (!sc.RememberHomeScene)
            {
                Logger.Log($"[RememberHome] ApplySkip: '{component.UniqueIdentifier}' not marked RememberHomeScene.", LogCategory.ComponentManager, LogLevel.Info);
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Logger.Log($"[RememberHome] ApplySkip: current scene invalid.", LogCategory.ComponentManager, LogLevel.Info);
                return;
            }

            if (string.IsNullOrEmpty(sc.HomeScene))
            {
                Logger.Log($"[RememberHome] ApplySkip: HomeScene empty for '{component.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info);
                return;
            }

            // Only apply in its designated home scene
            if (!sc.MatchesHomeScene(scene.name))
            {
                var primary = sc.HomeScene;
                Logger.Log($"[RememberHome] ApplySkip: Current='{scene.name}' not HomeScene (primary='{primary}') for '{component.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info);
                return;
            }

            // Find per-scene cache
            if (!sceneSnapshots.TryGetValue(scene.name, out var sceneDict))
            {
                Logger.Log($"[RememberHome] ApplyMiss: No scene cache for '{scene.name}'.", LogCategory.ComponentManager, LogLevel.Info);
                return;
            }

            // Find per-object cache by this GameObject UniqueID
            var goUID = sc.GameObjectUniqueID;
            if (string.IsNullOrEmpty(goUID))
            {
                Logger.Log($"[RememberHome] ApplySkip: GameObjectUniqueID empty for '{component.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info);
                return;
            }

            if (!sceneDict.TryGetValue(goUID, out var perObject))
            {
                Logger.Log($"[RememberHome] ApplyMiss: No object entry for GOUID='{goUID}' in scene '{scene.name}'.", LogCategory.ComponentManager, LogLevel.Info);
                return;
            }

            // If the component has cached data, apply it now
            if (perObject.TryGetValue(sc.UniqueIdentifier, out var data) && data != null && data.Length > 0)
            {
                if (restoration.HasRestoreData(sc, sc.UniqueIdentifier))
                {
                    Logger.Log($"[RememberHome] ApplySkip: Existing restore data for '{sc.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info);
                }
                else
                {
                    try
                    {
                        restoration.RestoreComponent(sc, data, sc.UniqueIdentifier);
                        Logger.Log($"[RememberHome] ApplyOK: Applied snapshot to '{sc.UniqueIdentifier}' in scene '{scene.name}'. Bytes={data.Length}", LogCategory.ComponentManager, LogLevel.Info);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[RememberHome] ApplyERR: '{sc.UniqueIdentifier}' failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ComponentManager, LogLevel.Error);
                    }
                }
            }
            else
            {
                Logger.Log($"[RememberHome] ApplyMiss: No component entry for '{sc.UniqueIdentifier}' under GOUID='{goUID}' in scene '{scene.name}'.", LogCategory.ComponentManager, LogLevel.Info);
            }
        }

        private Dictionary<string, Dictionary<string, byte[]>> GetOrCreate(string sceneName)
        {
            if (!sceneSnapshots.TryGetValue(sceneName, out var d))
            {
                d = new Dictionary<string, Dictionary<string, byte[]>>();
                sceneSnapshots[sceneName] = d;
            }

            return d;
        }

        private Dictionary<string, string> GetOrCreatePrefabMap(string sceneName)
        {
            if (!scenePrefabRefs.TryGetValue(sceneName, out var d))
            {
                d = new Dictionary<string, string>();
                scenePrefabRefs[sceneName] = d;
            }

            return d;
        }

        internal void ImportHomeSceneSnapshots(Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>> snapshots, Dictionary<string, Dictionary<string, string>> prefabRefs = null)
        {
            sceneSnapshots.Clear();
            scenePrefabRefs.Clear();
            if (snapshots != null && snapshots.Count > 0)
            {
                foreach (var sceneKvp in snapshots)
                {
                    var perObject = new Dictionary<string, Dictionary<string, byte[]>>();
                    foreach (var objKvp in sceneKvp.Value)
                    {
                        var perComp = new Dictionary<string, byte[]>();
                        foreach (var compKvp in objKvp.Value)
                            perComp[compKvp.Key] = compKvp.Value;
                        perObject[objKvp.Key] = perComp;
                    }

                    sceneSnapshots[sceneKvp.Key] = perObject;
                }
            }

            if (prefabRefs != null && prefabRefs.Count > 0)
            {
                foreach (var sceneKvp in prefabRefs)
                {
                    scenePrefabRefs[sceneKvp.Key] = new Dictionary<string, string>(sceneKvp.Value);
                }
            }

            Logger.Log($"[RememberHome] Import: Restored snapshot cache. Scenes={sceneSnapshots.Count}", LogCategory.ComponentManager, LogLevel.Info);
        }

        internal void InstantiateMissingHomeSceneObjectsForLoadedScenes(SaveManager manager)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                    InstantiateMissingHomeSceneObjects(scene, manager);
            }
        }

        private void InstantiateMissingHomeSceneObjects(Scene scene, SaveManager manager)
        {
            if (!scenePrefabRefs.TryGetValue(scene.name, out var prefabMap))
                return;
            foreach (var kvp in prefabMap)
            {
                string goUID = kvp.Key;
                string prefabID = kvp.Value;
                if (manager?.FindGameObjectByUniqueID(goUID, SaveManager.IdentifierType.UniqueID) != null)
                    continue;
                PrefabRegistry registry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
                var prefabAsset = registry != null ? registry.FindPrefabByID(prefabID) : null;
                if (prefabAsset == null)
                    continue;
                var sp = SaveablePrefabFactory.Instantiate(prefabAsset, Vector3.zero, Quaternion.identity, scene.name);
                if (sp != null)
                    sp.SetUniqueID(goUID);
            }
        }

        internal void Persist(SaveData saveData)
        {
            // Also persist the Remember Home per-scene snapshot cache so it survives across sessions
            try
            {
                if (saveData.HomeSceneComponentSnapshots == null)
                    saveData.HomeSceneComponentSnapshots = new Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>>();
                // Create a shallow copy to avoid external mutation of our internal dictionary
                saveData.HomeSceneComponentSnapshots.Clear();
                foreach (var sceneEntry in sceneSnapshots)
                {
                    // Copy per-object maps
                    var perObjectCopy = new Dictionary<string, Dictionary<string, byte[]>>();
                    foreach (var objEntry in sceneEntry.Value)
                    {
                        var perComponentCopy = new Dictionary<string, byte[]>();
                        foreach (var compEntry in objEntry.Value)
                        {
                            // Copy the byte[] reference; SaveData will be serialized immediately after
                            perComponentCopy[compEntry.Key] = compEntry.Value;
                        }

                        perObjectCopy[objEntry.Key] = perComponentCopy;
                    }

                    saveData.HomeSceneComponentSnapshots[sceneEntry.Key] = perObjectCopy;
                }

                Logger.Log($"[RememberHome] Persisted scene snapshot cache: Scenes={saveData.HomeSceneComponentSnapshots.Count}", LogCategory.ComponentManager, LogLevel.Info);
            }
            catch (Exception ex)
            {
                ComponentOperationScope.Record(ex);
                Logger.Log($"[RememberHome] Persist cache failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ComponentManager, LogLevel.Warning);
            }

            // Persist prefab asset references for Last Snapshot Scene
            try
            {
                if (saveData.HomeScenePrefabAssetIDs == null)
                    saveData.HomeScenePrefabAssetIDs = new Dictionary<string, Dictionary<string, string>>();
                saveData.HomeScenePrefabAssetIDs.Clear();
                foreach (var sceneEntry in scenePrefabRefs)
                {
                    saveData.HomeScenePrefabAssetIDs[sceneEntry.Key] = new Dictionary<string, string>(sceneEntry.Value);
                }

                Logger.Log($"[RememberHome] Persisted prefab refs: Scenes={saveData.HomeScenePrefabAssetIDs.Count}", LogCategory.ComponentManager, LogLevel.Info);
            }
            catch (Exception ex)
            {
                ComponentOperationScope.Record(ex);
                Logger.Log($"[RememberHome] Persist prefab refs failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ComponentManager, LogLevel.Warning);
            }
        }
    }
}
#endif
