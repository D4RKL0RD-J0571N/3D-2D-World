#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Profiling;

namespace Arawn.CrystalSave.Runtime
{
    internal sealed class PrefabLoadContext
    {
        private readonly Action<SaveablePrefab, bool> removeRegistration;
        private static readonly ProfilerMarker ResolveLoadCandidateMarker = new("CrystalSave.PrefabLoad.ResolveCandidate");
        private readonly PrefabOwnerLookup exactOwners = new PrefabOwnerLookup();
        private readonly PrefabOwnerLookup activeNameOwners = new PrefabOwnerLookup();
        private readonly Dictionary<GameObject, SaveablePrefab> saveableByObject = new Dictionary<GameObject, SaveablePrefab>();
        private readonly Dictionary<SaveablePrefab, string> registrationKeyByPrefab = new Dictionary<SaveablePrefab, string>();
        private readonly Dictionary<SaveablePrefab, PrefabReuseCandidates> scenePoolByPrefab = new Dictionary<SaveablePrefab, PrefabReuseCandidates>();
        private readonly Dictionary<(string asset, string scene), List<SaveablePrefab>> homeSceneCandidates = new();
        private readonly Dictionary<(string asset, string scene), PrefabReuseCandidates> homeScenePools = new();
        private readonly Dictionary<SaveablePrefab, List<PrefabReuseCandidates>> homePoolsByPrefab = new();
        private readonly HashSet<SaveablePrefab> retiredSceneCandidates = new();
        private readonly Dictionary<GameObject, string> rigidbodyKeyByObject = new Dictionary<GameObject, string>();
        private readonly List<string> removalKeys = new List<string>();
        private readonly HashSet<GameObject> destroyedOwnerSet = new HashSet<GameObject>();
        internal readonly Dictionary<string, PrefabReuseCandidates> SceneByAssetId = new Dictionary<string, PrefabReuseCandidates>(StringComparer.Ordinal);
        internal readonly Dictionary<string, SaveablePrefab> RegistrationOwners = new Dictionary<string, SaveablePrefab>(StringComparer.Ordinal);
        internal readonly HashSet<SaveablePrefab> RegisteredInstances = new HashSet<SaveablePrefab>();
        internal readonly HashSet<string> DestroyedIds;
        internal readonly List<GameObject> DestroyedOwners = new List<GameObject>();
        internal readonly List<SaveableComponent> SaveableComponentBuffer = new List<SaveableComponent>();
        internal readonly Dictionary<string, Rigidbody> Rigidbodies = new Dictionary<string, Rigidbody>(StringComparer.Ordinal);
        internal readonly SaveablePrefab[] ScenePrefabs;
        internal PrefabLoadContext(IEnumerable<string> destroyedIds, SaveablePrefab[] scenePrefabs,
            IReadOnlyDictionary<string, GameObject> liveInstances,
            IReadOnlyList<SaveablePrefab> registeredPrefabs,
            IReadOnlyDictionary<string, TrackedGameObject> trackerSnapshot,
            Action<SaveablePrefab, bool> removeRegistration)
        {
            this.removeRegistration = removeRegistration;
            DestroyedIds = destroyedIds != null ? new HashSet<string>(destroyedIds.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
            ScenePrefabs = scenePrefabs ?? Array.Empty<SaveablePrefab>();
            foreach (var pair in liveInstances)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                    continue;
                if (DestroyedIds.Contains(pair.Key))
                    MarkDestroyedOwner(pair.Value);
                else
                    AddExact(pair.Key, pair.Value, overwrite: false);
            }

            if (trackerSnapshot != null)
            {
                foreach (var pair in trackerSnapshot)
                {
                    GameObject trackedObject = pair.Value?.GameObject;
                    if (string.IsNullOrEmpty(pair.Key) || trackedObject == null)
                        continue;
                    if (DestroyedIds.Contains(pair.Key))
                        MarkDestroyedOwner(trackedObject);
                    else
                        AddExact(pair.Key, trackedObject, overwrite: false);
                }
            }

            for (int i = 0; i < registeredPrefabs.Count; i++)
            {
                SaveablePrefab registered = registeredPrefabs[i];
                if (registered == null)
                    continue;
                saveableByObject[registered.gameObject] = registered;
                if (!string.IsNullOrEmpty(registered.UniqueID) && DestroyedIds.Contains(registered.UniqueID))
                {
                    MarkDestroyedOwner(registered.gameObject);
                    continue;
                }

                RegisteredInstances.Add(registered);
                if (!string.IsNullOrEmpty(registered.UniqueID) && !RegistrationOwners.ContainsKey(registered.UniqueID))
                {
                    RegistrationOwners.Add(registered.UniqueID, registered);
                    registrationKeyByPrefab[registered] = registered.UniqueID;
                }
            }

            // Unity's object-search order is not a lookup contract. Sort the
            // one batch snapshot before appending scene candidates so shadow
            // promotion is deterministic across loads and platforms.
            Array.Sort(ScenePrefabs, PrefabReuseCandidates.CompareStableSceneOrder);
            for (int i = 0; i < ScenePrefabs.Length; i++)
                IndexScenePrefab(ScenePrefabs[i]);
            foreach (PrefabReuseCandidates candidatePool in SceneByAssetId.Values)
                candidatePool.Prepare();
        }

        internal bool IsDestroyed(string instanceId)
        {
            PrefabLoadDiagnosticCounters.RecordDestroyedIdProbe();
            return !string.IsNullOrEmpty(instanceId) && DestroyedIds.Contains(instanceId);
        }

        internal bool TryResolve(SaveablePrefabData data, out GameObject instance, out SaveablePrefab saveable)
        {
            using (ResolveLoadCandidateMarker.Auto())
            {
                PrefabLoadDiagnosticCounters.RecordIndexedResolutionProbe();
                instance = null;
                saveable = null;
                if (data == null)
                    return false;
                string instanceId = data.InstanceID;
                if (!string.IsNullOrEmpty(instanceId))
                {
                    if (TryGetExact(instanceId, out instance, out saveable))
                        return true;
                    int suffixSeparator = instanceId.IndexOf('_');
                    if (suffixSeparator > 0 && TryGetExact(instanceId.Substring(0, suffixSeparator), out instance, out saveable))
                    {
                        return true;
                    }

                    if (instanceId.IndexOf('/') < 0 && TryGetActiveName(instanceId, out instance, out saveable))
                    {
                        return true;
                    }
                }

                // GameObjectName is a legacy identity fallback only when the
                // serialized entry has no explicit instance identity. Falling
                // back to it for a valid InstanceID makes separate runtime
                // instances of the same prefab alias one another (for example,
                // three "Cylinder(Clone)" entries), so each later entry can
                // destroy or reuse the instance restored immediately before it.
                return string.IsNullOrEmpty(instanceId) && !string.IsNullOrEmpty(data.GameObjectName) && data.GameObjectName.IndexOf('/') < 0 && TryGetActiveName(data.GameObjectName, out instance, out saveable);
            }
        }

        internal bool IsDestroyedOwner(GameObject instance)
        {
            return instance != null && destroyedOwnerSet.Contains(instance);
        }

        internal void RemoveDestroyedMappings(Dictionary<string, GameObject> mappings)
        {
            if (mappings == null || mappings.Count == 0)
                return;
            removalKeys.Clear();
            foreach (var pair in mappings)
            {
                if (DestroyedIds.Contains(pair.Key) || (pair.Value != null && destroyedOwnerSet.Contains(pair.Value)))
                {
                    removalKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < removalKeys.Count; i++)
            {
                string key = removalKeys[i];
                if (mappings.TryGetValue(key, out GameObject mappedOwner) && mappedOwner != null)
                {
                    exactOwners.Remove(key, mappedOwner);
                }

                mappings.Remove(key);
            }

            removalKeys.Clear();
        }

        internal void RemoveMappingsOwnedBy(Dictionary<string, GameObject> mappings, GameObject mappedOwner)
        {
            if (mappings == null || mappings.Count == 0 || ReferenceEquals(mappedOwner, null))
                return;
            removalKeys.Clear();
            foreach (var pair in mappings)
            {
                if (ReferenceEquals(pair.Value, mappedOwner))
                    removalKeys.Add(pair.Key);
            }

            for (int i = 0; i < removalKeys.Count; i++)
            {
                string key = removalKeys[i];
                exactOwners.Remove(key, mappedOwner);
                mappings.Remove(key);
            }

            removalKeys.Clear();
        }

        internal bool TryResolveIdentifier(string identifier, out GameObject instance)
        {
            using (ResolveLoadCandidateMarker.Auto())
            {
                PrefabLoadDiagnosticCounters.RecordIndexedResolutionProbe();
                if (TryGetExact(identifier, out instance, out _))
                    return true;
                if (!string.IsNullOrEmpty(identifier))
                {
                    int suffixSeparator = identifier.IndexOf('_');
                    if (suffixSeparator > 0 && TryGetExact(identifier.Substring(0, suffixSeparator), out instance, out _))
                    {
                        return true;
                    }
                }

                instance = null;
                return false;
            }
        }

        internal void Bind(string instanceId, GameObject instance, SaveablePrefab saveable, bool overwrite = true)
        {
            if (instance == null)
                return;
            if (saveable != null)
                saveableByObject[instance] = saveable;
            AddExact(instanceId, instance, overwrite);
            RefreshActiveNameAliases(instance);
        }

        internal void RefreshActiveNameAliases(GameObject instance)
        {
            if (instance == null)
                return;
            activeNameOwners.RemoveOwner(instance);
            if (!instance.activeInHierarchy || string.IsNullOrEmpty(instance.name))
                return;
            string exact = instance.name;
            const string cloneSuffix = "(Clone)";
            string alternate = exact.EndsWith(cloneSuffix, StringComparison.Ordinal) ? exact.Substring(0, exact.Length - cloneSuffix.Length) : exact + cloneSuffix;
            AddActiveName(exact, instance);
            AddActiveName(alternate, instance);
        }

        internal bool TryGetSaveable(GameObject instance, out SaveablePrefab saveable)
        {
            saveable = null;
            if (instance == null)
                return false;
            if (saveableByObject.TryGetValue(instance, out saveable) && saveable != null)
                return true;
            if (!instance.TryGetComponent(out saveable) || saveable == null)
                return false;
            saveableByObject[instance] = saveable;
            return true;
        }

        internal void UnbindIfOwned(string instanceId, GameObject owner)
        {
            if (string.IsNullOrEmpty(instanceId) || owner == null)
                return;
            // Remove this owner even when it is currently shadowed. Otherwise
            // it could resurface after a later promotion with a stale identity.
            exactOwners.Remove(instanceId, owner);
        }

        internal bool TryGetSceneCandidates(SaveablePrefabData data, out PrefabReuseCandidates candidates)
        {
            candidates = null;
            if (data == null || string.IsNullOrEmpty(data.PrefabID)) return false;
            if (string.IsNullOrEmpty(data.HomeScene))
                return SceneByAssetId.TryGetValue(data.PrefabID, out candidates) && candidates.Count > 0;

            var key = (data.PrefabID, data.HomeScene);
            if (!homeScenePools.TryGetValue(key, out candidates))
            {
                candidates = new PrefabReuseCandidates();
                AddHomeCandidates(candidates, data.PrefabID, data.HomeScene);
                // Persistent roots remain valid across scenes under the same rule
                // as an existing exact-ID mapping. Nonpersistent DDOL objects are
                // excluded when the scene snapshot is indexed.
                if (!string.Equals(data.HomeScene, "DontDestroyOnLoad", StringComparison.Ordinal))
                    AddHomeCandidates(candidates, data.PrefabID, "DontDestroyOnLoad");
                candidates.Prepare();
                homeScenePools.Add(key, candidates);
            }
            return candidates.Count > 0;
        }

        private void AddHomeCandidates(PrefabReuseCandidates pool, string assetId, string sceneName)
        {
            if (!homeSceneCandidates.TryGetValue((assetId, sceneName), out var entries)) return;
            foreach (var prefab in entries)
            {
                if (prefab == null || retiredSceneCandidates.Contains(prefab)) continue;
                pool.Add(prefab);
                if (!homePoolsByPrefab.TryGetValue(prefab, out var pools))
                {
                    pools = new List<PrefabReuseCandidates>();
                    homePoolsByPrefab.Add(prefab, pools);
                }
                pools.Add(pool);
            }
        }

        internal void RetireSceneCandidate(SaveablePrefab prefab)
        {
            if (prefab == null || !retiredSceneCandidates.Add(prefab)) return;
            if (scenePoolByPrefab.TryGetValue(prefab, out var global)) global.Remove(prefab);
            if (homePoolsByPrefab.TryGetValue(prefab, out var pools))
                foreach (var pool in pools) pool.Remove(prefab);
        }

        internal void RejectResolution(SaveablePrefabData data, GameObject rejected)
        {
            if (data == null || rejected == null)
                return;
            string instanceId = data.InstanceID;
            if (!string.IsNullOrEmpty(instanceId))
            {
                exactOwners.Remove(instanceId, rejected);
                int suffixSeparator = instanceId.IndexOf('_');
                if (suffixSeparator > 0)
                    exactOwners.Remove(instanceId.Substring(0, suffixSeparator), rejected);
                if (instanceId.IndexOf('/') < 0)
                    activeNameOwners.Remove(instanceId, rejected);
            }

            if (string.IsNullOrEmpty(instanceId) && !string.IsNullOrEmpty(data.GameObjectName) && data.GameObjectName.IndexOf('/') < 0)
            {
                activeNameOwners.Remove(data.GameObjectName, rejected);
            }
        }

        internal void RemoveObject(GameObject instance, bool managerCollectionsWillBeRebuilt = false)
        {
            if (instance == null)
                return;
            exactOwners.RemoveOwner(instance);
            activeNameOwners.RemoveOwner(instance);
            if (rigidbodyKeyByObject.TryGetValue(instance, out string rigidbodyKey))
            {
                rigidbodyKeyByObject.Remove(instance);
                Rigidbodies.Remove(rigidbodyKey);
            }

            if (saveableByObject.TryGetValue(instance, out SaveablePrefab saveable))
            {
                saveableByObject.Remove(instance);
                RegisteredInstances.Remove(saveable);
                removeRegistration(saveable, managerCollectionsWillBeRebuilt);
                if (registrationKeyByPrefab.TryGetValue(saveable, out string registrationKey))
                {
                    registrationKeyByPrefab.Remove(saveable);
                    if (RegistrationOwners.TryGetValue(registrationKey, out SaveablePrefab registrationOwner) && registrationOwner == saveable)
                    {
                        RegistrationOwners.Remove(registrationKey);
                    }
                }

                RetireSceneCandidate(saveable);
                scenePoolByPrefab.Remove(saveable);
            }
        }

        internal SaveablePrefab GetOrAddRegistrationOwner(string instanceId, SaveablePrefab prefab)
        {
            if (string.IsNullOrEmpty(instanceId) || prefab == null)
                return null;
            if (RegistrationOwners.TryGetValue(instanceId, out SaveablePrefab existing))
            {
                return existing;
            }

            RegistrationOwners.Add(instanceId, prefab);
            registrationKeyByPrefab[prefab] = instanceId;
            return prefab;
        }

        internal void CacheRigidbody(string instanceId, Rigidbody rigidbody)
        {
            if (string.IsNullOrEmpty(instanceId) || rigidbody == null)
                return;
            if (Rigidbodies.TryGetValue(instanceId, out Rigidbody previous) && previous != null && previous.gameObject != rigidbody.gameObject)
            {
                rigidbodyKeyByObject.Remove(previous.gameObject);
            }

            if (rigidbodyKeyByObject.TryGetValue(rigidbody.gameObject, out string previousKey) && !string.Equals(previousKey, instanceId, StringComparison.Ordinal))
            {
                Rigidbodies.Remove(previousKey);
            }

            Rigidbodies[instanceId] = rigidbody;
            rigidbodyKeyByObject[rigidbody.gameObject] = instanceId;
        }

        internal void RebindRegistrationOwner(SaveablePrefab prefab, string previousId, string newId)
        {
            if (prefab == null)
                return;
            if (!string.IsNullOrEmpty(previousId) && !string.Equals(previousId, newId, StringComparison.Ordinal) && RegistrationOwners.TryGetValue(previousId, out SaveablePrefab previousOwner) && previousOwner == prefab)
            {
                RegistrationOwners.Remove(previousId);
                registrationKeyByPrefab.Remove(prefab);
            }

            if (!string.IsNullOrEmpty(newId) && !RegistrationOwners.ContainsKey(newId))
            {
                RegistrationOwners.Add(newId, prefab);
                registrationKeyByPrefab[prefab] = newId;
            }
        }

        private void IndexScenePrefab(SaveablePrefab prefab)
        {
            if (prefab == null || prefab.gameObject == null)
                return;
            GameObject instance = prefab.gameObject;
            saveableByObject[instance] = prefab;
            instance.TryGetComponent(out UniqueID rootUniqueId);
            if (destroyedOwnerSet.Contains(instance) || (!string.IsNullOrEmpty(prefab.UniqueID) && DestroyedIds.Contains(prefab.UniqueID)) || (rootUniqueId != null && !string.IsNullOrEmpty(rootUniqueId.ID) && DestroyedIds.Contains(rootUniqueId.ID)))
            {
                MarkDestroyedOwner(instance);
                return;
            }

            AddExact(prefab.UniqueID, instance, overwrite: false);
            if (rootUniqueId != null)
                AddExact(rootUniqueId.ID, instance, overwrite: false);
            if (instance.scene.IsValid() && PrefabReusePolicy.IsParticipant(prefab) && PrefabReusePolicy.IsSceneBacked(prefab) && !string.IsNullOrEmpty(prefab.PrefabAssetID))
            {
                if (!SceneByAssetId.TryGetValue(prefab.PrefabAssetID, out PrefabReuseCandidates candidates))
                {
                    candidates = new PrefabReuseCandidates();
                    SceneByAssetId.Add(prefab.PrefabAssetID, candidates);
                }

                candidates.Add(prefab);
                scenePoolByPrefab[prefab] = candidates;
                var homeKey = (prefab.PrefabAssetID, instance.scene.name);
                if (!homeSceneCandidates.TryGetValue(homeKey, out var homeEntries))
                {
                    homeEntries = new List<SaveablePrefab>();
                    homeSceneCandidates.Add(homeKey, homeEntries);
                }
                homeEntries.Add(prefab);
            }

            RefreshActiveNameAliases(instance);
        }

        private void MarkDestroyedOwner(GameObject instance)
        {
            if (instance != null && destroyedOwnerSet.Add(instance))
                DestroyedOwners.Add(instance);
        }

        private void AddExact(string key, GameObject instance, bool overwrite)
        {
            if (string.IsNullOrEmpty(key) || instance == null)
                return;
            exactOwners.Add(key, instance, promote: overwrite);
        }

        private void AddActiveName(string key, GameObject instance)
        {
            activeNameOwners.Add(key, instance, promote: false);
        }

        private bool TryGetExact(string key, out GameObject instance, out SaveablePrefab saveable)
        {
            if (!exactOwners.TryGet(key, out instance))
            {
                instance = null;
                saveable = null;
                return false;
            }

            if (!saveableByObject.TryGetValue(instance, out saveable) || saveable == null)
            {
                instance.TryGetComponent(out saveable);
                if (saveable != null)
                    saveableByObject[instance] = saveable;
            }

            return true;
        }

        private bool TryGetActiveName(string key, out GameObject instance, out SaveablePrefab saveable)
        {
            while (activeNameOwners.TryGet(key, out instance))
            {
                if (IsCurrentActiveNameAlias(key, instance))
                    break;
                // A renamed or deactivated current owner must not erase the
                // key's shadow candidates. Remove just that stale owner and
                // continue at the promoted head.
                activeNameOwners.Remove(key, instance);
            }

            if (instance == null)
            {
                saveable = null;
                return false;
            }

            if (!saveableByObject.TryGetValue(instance, out saveable) || saveable == null)
            {
                instance.TryGetComponent(out saveable);
                if (saveable != null)
                    saveableByObject[instance] = saveable;
            }

            return true;
        }

        private static bool IsCurrentActiveNameAlias(string key, GameObject instance)
        {
            if (string.IsNullOrEmpty(key) || instance == null || !instance.activeInHierarchy || string.IsNullOrEmpty(instance.name))
            {
                return false;
            }

            string currentName = instance.name;
            if (string.Equals(currentName, key, StringComparison.Ordinal))
                return true;
            const string cloneSuffix = "(Clone)";
            if (currentName.EndsWith(cloneSuffix, StringComparison.Ordinal))
            {
                int baseLength = currentName.Length - cloneSuffix.Length;
                return key.Length == baseLength && string.CompareOrdinal(currentName, 0, key, 0, baseLength) == 0;
            }

            return key.Length == currentName.Length + cloneSuffix.Length && string.CompareOrdinal(key, 0, currentName, 0, currentName.Length) == 0 && key.EndsWith(cloneSuffix, StringComparison.Ordinal);
        }
    }
}
#endif
