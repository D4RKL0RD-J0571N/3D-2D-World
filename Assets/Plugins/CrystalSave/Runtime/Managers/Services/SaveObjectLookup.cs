#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using IdentifierType = Arawn.CrystalSave.Runtime.SaveManager.IdentifierType;

namespace Arawn.CrystalSave.Runtime
{
    // Owns the optional lookup cache and the ordered public lookup policy. Live registries
    // are supplied by the caller, so this service cannot resolve a different singleton.
    internal sealed class SaveObjectLookup
    {
        readonly Dictionary<string, GameObject> lookupCache = new();
        bool enableLookupCache;
        internal bool Enabled => enableLookupCache;
        internal IReadOnlyDictionary<string, GameObject> CachedObjects { get; }

        internal SaveObjectLookup()
        {
            CachedObjects = new ReadOnlyDictionary<string, GameObject>(lookupCache);
        }

        private static T[] FindObjectsIncludingInactive<T>()
            where T : UnityEngine.Object
        {
            return UnityObjectSearch.FindIncludingInactive<T>();
        }

        private static UniqueID FindUniqueIDComponent(string id, string fallbackId = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var components = FindObjectsIncludingInactive<UniqueID>();
            UniqueID fallback = null;
            bool hasFallback = !string.IsNullOrEmpty(fallbackId);
            for (int i = 0; i < components.Length; i++)
            {
                UniqueID component = components[i];
                if (component != null && string.Equals(component.ID, id, StringComparison.Ordinal))
                {
                    return component;
                }

                if (fallback == null && component != null && hasFallback && string.Equals(component.ID, fallbackId, StringComparison.Ordinal))
                {
                    fallback = component;
                }
            }

            return fallback;
        }

        private static SaveablePrefab FindSaveablePrefabByUniqueID(string id, string fallbackId = null, SaveablePrefab[] snapshot = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var prefabs = snapshot ?? FindObjectsIncludingInactive<SaveablePrefab>();
            SaveablePrefab fallback = null;
            bool hasFallback = !string.IsNullOrEmpty(fallbackId);
            for (int i = 0; i < prefabs.Length; i++)
            {
                SaveablePrefab prefab = prefabs[i];
                if (prefab != null && string.Equals(prefab.UniqueID, id, StringComparison.Ordinal))
                {
                    return prefab;
                }

                if (fallback == null && prefab != null && hasFallback && string.Equals(prefab.UniqueID, fallbackId, StringComparison.Ordinal))
                {
                    fallback = prefab;
                }
            }

            return fallback;
        }

        private static SceneObjectID FindSceneObjectID(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var sceneObjects = FindObjectsIncludingInactive<SceneObjectID>();
            for (int i = 0; i < sceneObjects.Length; i++)
            {
                SceneObjectID sceneObject = sceneObjects[i];
                if (sceneObject != null && string.Equals(sceneObject.UniqueID, id, StringComparison.Ordinal))
                {
                    return sceneObject;
                }
            }

            return null;
        }

        private static SaveablePrefab FindSaveablePrefabByAssetID(string id, SaveablePrefab[] snapshot = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var prefabs = snapshot ?? FindObjectsIncludingInactive<SaveablePrefab>();
            for (int i = 0; i < prefabs.Length; i++)
            {
                SaveablePrefab prefab = prefabs[i];
                if (prefab != null && string.Equals(prefab.PrefabAssetID, id, StringComparison.Ordinal))
                {
                    return prefab;
                }
            }

            return null;
        }

        private bool TryGetFastTrackedObject(GameObjectTracker gameObjectTracker, string identifier, IdentifierType identifierType, out GameObject trackedObject)
        {
            trackedObject = null;
            if (gameObjectTracker == null || !gameObjectTracker.TryGetTrackedGameObject(identifier, out var tracked) || tracked?.GameObject == null)
            {
                return false;
            }

            GameObject candidate = tracked.GameObject;
            if (!IsFastLookupMatch(candidate, identifier, identifierType))
            {
                // An exact tracker key is not sufficient after an object's identity
                // changes. Preserve valid cross-mode mappings, but prune keys whose
                // live owner no longer matches any supported public identifier.
                if (!MatchesAnyPublicIdentifier(candidate, identifier))
                    PruneTrackedObjectIfOwned(gameObjectTracker, identifier, candidate);
                return false;
            }

            trackedObject = candidate;
            return true;
        }

        private static bool HasSceneObjectIdentifier(GameObject candidate, string identifier)
        {
            return candidate != null && candidate.TryGetComponent<SceneObjectID>(out var sceneObjectID) && sceneObjectID != null && string.Equals(sceneObjectID.UniqueID, identifier, StringComparison.Ordinal);
        }

        private static bool HasPrefabAssetIdentifier(GameObject candidate, string identifier)
        {
            if (candidate == null)
                return false;
            SaveablePrefab saveablePrefab;
            if (SaveablePrefab.TryGetCachedSaveablePrefab(candidate, out SaveablePrefab cachedPrefab) && cachedPrefab != null)
            {
                saveablePrefab = cachedPrefab;
            }
            else
            {
                candidate.TryGetComponent(out saveablePrefab);
            }

            return saveablePrefab != null && string.Equals(saveablePrefab.PrefabAssetID, identifier, StringComparison.Ordinal);
        }

        private static bool HasUniqueIdentifier(GameObject candidate, string identifier)
        {
            if (candidate == null || string.IsNullOrEmpty(identifier))
                return false;
            if (candidate.TryGetComponent<UniqueID>(out var uniqueId) && uniqueId != null && IdentifierOrBaseEquals(uniqueId.ID, identifier))
            {
                return true;
            }

            if (candidate.TryGetComponent<RememberGameObject>(out var remember) && remember != null && (IdentifierOrBaseEquals(remember.GameObjectUniqueID, identifier) || string.Equals(remember.UniqueIdentifier, identifier, StringComparison.Ordinal)))
            {
                return true;
            }

            SaveablePrefab saveablePrefab;
            if (SaveablePrefab.TryGetCachedSaveablePrefab(candidate, out SaveablePrefab cachedPrefab) && cachedPrefab != null)
            {
                saveablePrefab = cachedPrefab;
            }
            else
            {
                candidate.TryGetComponent(out saveablePrefab);
            }

            return saveablePrefab != null && IdentifierOrBaseEquals(saveablePrefab.UniqueID, identifier);
        }

        private static bool IdentifierOrBaseEquals(string candidateIdentifier, string requestedIdentifier)
        {
            if (string.IsNullOrEmpty(candidateIdentifier) || string.IsNullOrEmpty(requestedIdentifier))
            {
                return false;
            }

            if (string.Equals(candidateIdentifier, requestedIdentifier, StringComparison.Ordinal))
            {
                return true;
            }

            int separatorIndex = requestedIdentifier.IndexOf('_');
            return separatorIndex > 0 && candidateIdentifier.Length == separatorIndex && string.CompareOrdinal(candidateIdentifier, 0, requestedIdentifier, 0, separatorIndex) == 0;
        }

        private static bool HasLegacyNameIdentifier(GameObject candidate, string identifier)
        {
            if (candidate == null || string.IsNullOrEmpty(identifier) || identifier.IndexOf('/') >= 0)
            {
                return false;
            }

            string candidateName = candidate.name;
            if (string.Equals(candidateName, identifier, StringComparison.Ordinal))
                return true;
            const string cloneSuffix = "(Clone)";
            return candidateName != null && candidateName.Length == identifier.Length + cloneSuffix.Length && candidateName.StartsWith(identifier, StringComparison.Ordinal) && candidateName.EndsWith(cloneSuffix, StringComparison.Ordinal);
        }

        private static bool MatchesAnyPublicIdentifier(GameObject candidate, string identifier)
        {
            return HasUniqueIdentifier(candidate, identifier) || HasSceneObjectIdentifier(candidate, identifier) || HasPrefabAssetIdentifier(candidate, identifier) || HasLegacyNameIdentifier(candidate, identifier);
        }

        private static bool IsFastLookupMatch(GameObject candidate, string identifier, IdentifierType identifierType)
        {
            bool typedMatch = identifierType switch
            {
                IdentifierType.UniqueID => HasUniqueIdentifier(candidate, identifier),
                IdentifierType.SceneObjectID => HasSceneObjectIdentifier(candidate, identifier),
                IdentifierType.PrefabAssetID => HasPrefabAssetIdentifier(candidate, identifier),
                _ => HasUniqueIdentifier(candidate, identifier) || HasSceneObjectIdentifier(candidate, identifier) || HasPrefabAssetIdentifier(candidate, identifier)};
            // Public lookup has always applied the exact-name and "(Clone)"
            // fallbacks after every identifier mode, so a cached name result remains
            // valid even when the caller supplied an explicit typed mode.
            return typedMatch || HasLegacyNameIdentifier(candidate, identifier);
        }

        private void PruneTrackedObjectIfOwned(GameObjectTracker gameObjectTracker, string identifier, GameObject owner)
        {
            if (gameObjectTracker != null && gameObjectTracker.PruneTrackedObjectIfOwned(identifier, owner))
                UncacheGameObject(identifier);
        }

        internal GameObject Find(string uniqueID, IdentifierType identifierType, bool logTerminalMiss, GameObjectTracker gameObjectTracker, PrefabManager prefabManager)
        {
            if (string.IsNullOrEmpty(uniqueID))
                return null;
            // The optional global cache remains the first lookup layer when enabled.
            if (TryGetCachedGameObject(uniqueID, out var cached))
            {
                if (IsFastLookupMatch(cached, uniqueID, identifierType))
                    return cached;
                // A cache key may legitimately be reused by a different explicit
                // identifier mode. Only prune it when the live object no longer owns
                // this identifier through any supported public lookup route.
                if (!MatchesAnyPublicIdentifier(cached, uniqueID))
                    UncacheGameObject(uniqueID);
            }

            // Tracker and PrefabManager dictionaries are authoritative live mappings and do
            // not depend on the optional global lookup-cache setting.
            if (TryGetFastTrackedObject(gameObjectTracker, uniqueID, identifierType, out var trackedFastPath))
            {
                CacheGameObject(uniqueID, trackedFastPath);
                return trackedFastPath;
            }

            if ((identifierType == IdentifierType.Auto || identifierType == IdentifierType.UniqueID) && prefabManager != null && prefabManager.TryGetInstantiatedPrefab(uniqueID, out var instantiatedPrefab))
            {
                CacheGameObject(uniqueID, instantiatedPrefab);
                return instantiatedPrefab;
            }

            SaveablePrefab[] saveablePrefabSnapshot = null;
            SaveablePrefab[] GetSaveablePrefabSnapshot()
            {
                return saveablePrefabSnapshot ??= FindObjectsIncludingInactive<SaveablePrefab>();
            }

            GameObject ByUniqueID()
            {
                string baseID = null;
                int separatorIndex = uniqueID.IndexOf('_');
                if (separatorIndex > 0)
                {
                    baseID = uniqueID.Substring(0, separatorIndex);
                }

                UniqueID uidComponent = FindUniqueIDComponent(uniqueID, baseID);
                if (uidComponent != null)
                {
                    if (Logger.ShouldLog(LogCategory.SaveManager))
                    {
                        Logger.Log($"SaveManager: Found GameObject '{Logger.GetUnityObjectName(uidComponent.gameObject)}' via UniqueID component.", LogCategory.SaveManager);
                    }

                    return uidComponent.gameObject;
                }

                SaveablePrefab spUnique = FindSaveablePrefabByUniqueID(uniqueID, baseID, GetSaveablePrefabSnapshot());
                if (spUnique != null)
                {
                    if (Logger.ShouldLog(LogCategory.SaveManager))
                    {
                        Logger.Log($"SaveManager: Found GameObject '{Logger.GetUnityObjectName(spUnique.gameObject)}' via SaveablePrefab component.", LogCategory.SaveManager);
                    }

                    return spUnique.gameObject;
                }

                return null;
            }

            GameObject BySceneObjectID()
            {
                SceneObjectID sceneObject = FindSceneObjectID(uniqueID);
                if (sceneObject != null)
                {
                    if (Logger.ShouldLog(LogCategory.SaveManager))
                    {
                        Logger.Log($"SaveManager: Found GameObject '{Logger.GetUnityObjectName(sceneObject.gameObject)}' via SceneObjectID component.", LogCategory.SaveManager);
                    }

                    return sceneObject.gameObject;
                }

                return null;
            }

            GameObject ByPrefabAssetID()
            {
                SaveablePrefab saveablePrefab = FindSaveablePrefabByAssetID(uniqueID, GetSaveablePrefabSnapshot());
                if (saveablePrefab != null)
                {
                    if (Logger.ShouldLog(LogCategory.SaveManager))
                    {
                        Logger.Log($"SaveManager: Found GameObject '{Logger.GetUnityObjectName(saveablePrefab.gameObject)}' via SaveablePrefab component.", LogCategory.SaveManager);
                    }

                    return saveablePrefab.gameObject;
                }

                return null;
            }

            GameObject result = identifierType switch
            {
                IdentifierType.UniqueID => ByUniqueID(),
                IdentifierType.SceneObjectID => BySceneObjectID(),
                IdentifierType.PrefabAssetID => ByPrefabAssetID(),
                _ => ByUniqueID() ?? BySceneObjectID() ?? ByPrefabAssetID()};
            if (result != null)
            {
                CacheGameObject(uniqueID, result);
                return result;
            }

            // Fallback: Try finding by name if UniqueID/SceneObjectID component is absent
            GameObject go = GameObject.Find(uniqueID);
            if (go != null)
            {
                if (Logger.ShouldLog(LogCategory.SaveManager))
                {
                    Logger.Log($"SaveManager: Found GameObject '{Logger.GetUnityObjectName(go)}' by name matching UniqueID.", LogCategory.SaveManager, LogLevel.Info);
                }

                CacheGameObject(uniqueID, go);
                return go;
            }

            // Fallback: Try finding by name with "(Clone)" appended
            string cloneName = $"{uniqueID}(Clone)";
            go = GameObject.Find(cloneName);
            if (go != null)
            {
                if (Logger.ShouldLog(LogCategory.SaveManager))
                {
                    Logger.Log($"FindGameObjectByUniqueID: Found GameObject by name with '(Clone)' appended '{cloneName}'.", LogCategory.SaveManager, LogLevel.Info);
                }

                CacheGameObject(uniqueID, go);
                return go;
            }

            // GameObject.Find only considers active objects. Preserve the public lookup
            // contract for inactive scene objects with simple exact-name and legacy
            // "(Clone)" aliases without treating arbitrary hierarchy paths as names.
            if (uniqueID.IndexOf('/') < 0)
            {
                Transform cloneNameMatch = null;
                Transform[] transforms = FindObjectsIncludingInactive<Transform>();
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform transform = transforms[i];
                    if (transform == null)
                        continue;
                    string candidateName = transform.gameObject.name;
                    if (string.Equals(candidateName, uniqueID, StringComparison.Ordinal))
                    {
                        go = transform.gameObject;
                        break;
                    }

                    if (cloneNameMatch == null && string.Equals(candidateName, cloneName, StringComparison.Ordinal))
                    {
                        cloneNameMatch = transform;
                    }
                }

                if (go == null && cloneNameMatch != null)
                    go = cloneNameMatch.gameObject;
                if (go != null)
                {
                    if (Logger.ShouldLog(LogCategory.SaveManager))
                    {
                        Logger.Log($"FindGameObjectByUniqueID: Found inactive GameObject '{Logger.GetUnityObjectName(go)}' by exact or '(Clone)' name.", LogCategory.SaveManager, LogLevel.Info);
                    }

                    CacheGameObject(uniqueID, go);
                    return go;
                }
            }

            if (gameObjectTracker != null && gameObjectTracker.TryGetTrackedGameObject(uniqueID, out var trackedGO) && trackedGO?.GameObject != null && IsFastLookupMatch(trackedGO.GameObject, uniqueID, identifierType))
            {
                CacheGameObject(uniqueID, trackedGO.GameObject);
                return trackedGO.GameObject;
            }

            if (gameObjectTracker != null && gameObjectTracker.IsGameObjectDestroyed(uniqueID))
            {
                if (logTerminalMiss && Logger.ShouldLog(LogCategory.SaveManager, LogLevel.Info))
                {
                    Logger.Log($"FindGameObjectByUniqueID: ID '{uniqueID}' is marked as destroyed; skipping warning.", LogCategory.SaveManager, LogLevel.Info);
                }

                return null;
            }

            if (logTerminalMiss && Logger.ShouldLog(LogCategory.SaveManager, LogLevel.Info))
            {
                Logger.Log($"GameObject with UniqueID '{uniqueID}' not found. \r\nIt may have been destroyed because you destroyed it before your last save and its component \"RememberGameObject\" and its Property SaveDestroy is set to true, or you try to restore a single prefab, if this is the case: Either ignore the warning or disable it by setting the log level to \"Error\" (recommended) or \"Off\" (not recommended), in the RememberMeSaveSettings ScriptableObject.", LogCategory.SaveManager, LogLevel.Info);
            }

            return null;
        }

        internal void SetLookupCacheEnabled(bool enabled)
        {
            enableLookupCache = enabled;
            if (!enableLookupCache)
                lookupCache.Clear();
        }

        internal void ClearLookupCache() => lookupCache.Clear();
        /// <summary>
        /// Ensures the lookup cache is properly populated with all tracked objects.
        /// This is particularly important when enableLookupCache is true to prevent first-load issues.
        /// Call this manually if you experience issues with RememberGameObject restoration on the first load.
        /// </summary>
        internal void EnsureCacheConsistency(GameObjectTracker gameObjectTracker)
        {
            if (!enableLookupCache || gameObjectTracker == null)
                return;
            var trackedObjects = gameObjectTracker.GetTrackedGameObjects();
            foreach (var kvp in trackedObjects)
            {
                if (kvp.Value?.GameObject != null && !lookupCache.ContainsKey(kvp.Key))
                {
                    lookupCache[kvp.Key] = kvp.Value.GameObject;
                    Logger.Log($"SaveManager: Added missing object '{kvp.Value.GameObject.name}' to lookup cache.", LogCategory.SaveManager, LogLevel.Info);
                }
            }
        }

        internal void CacheGameObject(string id, GameObject obj)
        {
            if (!enableLookupCache || string.IsNullOrEmpty(id) || obj == null)
                return;
            lookupCache[id] = obj;
        }

        internal void UncacheGameObject(string id)
        {
            if (!enableLookupCache || string.IsNullOrEmpty(id))
                return;
            lookupCache.Remove(id);
        }

        internal void UncacheGameObjectIfOwned(string id, GameObject owner)
        {
            if (!enableLookupCache || string.IsNullOrEmpty(id) || !lookupCache.TryGetValue(id, out GameObject cached))
            {
                return;
            }

            if (cached == null || IsSameGameObjectInstance(cached, owner))
                lookupCache.Remove(id);
        }

        internal static bool IsSameGameObjectInstance(GameObject left, GameObject right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;
            try
            {
#if UNITY_6000_5_OR_NEWER
                return left.GetEntityId() == right.GetEntityId();
#else
                return left.GetInstanceID() == right.GetInstanceID();
#endif
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        internal bool TryGetCachedGameObject(string id, out GameObject obj)
        {
            if (enableLookupCache && !string.IsNullOrEmpty(id) && lookupCache.TryGetValue(id, out obj))
            {
                if (obj != null)
                    return true;
                // Unity's destroyed-object null semantics leave dictionary entries behind
                // unless explicitly pruned.
                lookupCache.Remove(id);
            }

            obj = null;
            return false;
        }
    }
}
#endif
