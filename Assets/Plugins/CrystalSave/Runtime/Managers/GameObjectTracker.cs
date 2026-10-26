#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Tracks GameObjects for the SaveManager and handles active-state watching.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public class GameObjectTracker : MonoBehaviour
    {
        
        readonly Dictionary<string, TrackedGameObject> trackedGameObjects = new();
        readonly object trackedLock = new();
        readonly List<TrackedGameObjectProxy> proxyBuffer = new();

        readonly HashSet<string> destroyedIDs = new();
        readonly Dictionary<string, bool> activeStates = new();
        readonly object activeLock = new();
        readonly Dictionary<string, PendingInactiveStateUpdate> pendingInactiveStateUpdates = new();
        readonly object pendingInactiveStateLock = new();
        Coroutine pendingInactiveStateRoutine;
        readonly HashSet<IEnumerator> ownedRoutines = new();
        UnityMainThreadDispatcher routineHost;
        IEnumerator watchEnumerator;

        readonly struct PendingInactiveStateUpdate
        {
            internal PendingInactiveStateUpdate(GameObject owner)
            {
                Owner = owner;
            }

            internal GameObject Owner { get; }
        }

        [SerializeField] int  activeStateEnforceDelayFrames = 1;
        [SerializeField] bool enforceActiveState = false;
        [SerializeField] float activeStateWatchDuration = 0f;
        Coroutine watchRoutine;

        internal int ActiveStateEnforceDelayFrames => activeStateEnforceDelayFrames;
        internal bool EnforceActiveState => enforceActiveState;
        internal float ActiveStateWatchDuration => activeStateWatchDuration;

        SaveManager saveManager;
        PrefabManager prefabManager;
        ComponentManager componentManager;

        internal SaveManager SaveManager => saveManager;

        internal void Initialize(SaveManager mgr, PrefabManager prefabMgr, ComponentManager compMgr)
        {
            if (!ReferenceEquals(saveManager, null) && !ReferenceEquals(saveManager, mgr))
                RetireManagerWork();
            saveManager = mgr;
            prefabManager = prefabMgr;
            componentManager = compMgr;
        }

        Coroutine StartOwnedRoutine(IEnumerator routine) => StartOwnedRoutine(routine, out _);

        Coroutine StartOwnedRoutine(IEnumerator routine, out IEnumerator owned)
        {
            // Scene shutdown can destroy the independent dispatcher before this
            // tracker's final OnDisable notifications. Those notifications must
            // not recreate a frame-loop GameObject while Unity tears the scene down.
            if (!this || !saveManager || saveManager.IsShuttingDown)
            {
                owned = null;
                (routine as System.IDisposable)?.Dispose();
                return null;
            }
            if (!routineHost) routineHost = UnityMainThreadDispatcher.InstanceIndependentOf(gameObject);
            IEnumerator pump = null;
            pump = RunOwnedRoutine(routine, () => ownedRoutines.Remove(pump));
            owned = pump;
            ownedRoutines.Add(pump);
            return routineHost.StartCoroutine(pump);
        }

        IEnumerator RunOwnedRoutine(IEnumerator routine, System.Action release)
        {
            try
            {
                while (this && routine.MoveNext()) yield return routine.Current;
            }
            finally
            {
                try { (routine as System.IDisposable)?.Dispose(); }
                finally { release(); }
            }
        }

        void StopOwnedRoutine(IEnumerator routine)
        {
            if (routine == null) return;
            if (routineHost) routineHost.StopCoroutine(routine);
            (routine as System.IDisposable)?.Dispose();
            ownedRoutines.Remove(routine);
        }

        void OnDestroy() => RetireManagerWork();

        internal void RetireManagerWork()
        {
            foreach (var routine in ownedRoutines.ToArray()) StopOwnedRoutine(routine);
            pendingInactiveStateRoutine = null;
            lock (pendingInactiveStateLock) pendingInactiveStateUpdates.Clear();
            watchRoutine = null;
            watchEnumerator = null;
        }

        #region Accessors
        public bool IsTracked(string id)
        {
            lock (trackedLock) return trackedGameObjects.ContainsKey(id);
        }

        public bool IsGameObjectDestroyed(string id) => destroyedIDs.Contains(id);

        public Dictionary<string, TrackedGameObject> GetTrackedGameObjects()
        {
            lock (trackedLock) return new Dictionary<string, TrackedGameObject>(trackedGameObjects);
        }

        public List<string> GetDestroyedGameObjectIDs() => new List<string>(destroyedIDs);

        internal List<string> GetTrackedIDSnapshot()
        {
            lock (trackedLock) return trackedGameObjects.Keys.ToList();
        }

        internal bool TryGetRecordedActiveState(string id, out bool active)
        {
            lock (activeLock) return activeStates.TryGetValue(id, out active);
        }

        // Destruction resolves aliases after an exact miss. Keep that first probe
        // allocation-free and leave stale-entry cleanup to the existing lookup path.
        internal bool TryPeekLiveTrackedGameObject(string id, out TrackedGameObject tracked)
        {
            lock (trackedLock)
                return trackedGameObjects.TryGetValue(id, out tracked) && tracked != null && tracked.GameObject != null;
        }

        internal string GetTrackedIDForObject(GameObject owner)
        {
            lock (trackedLock)
            {
                foreach (var entry in trackedGameObjects)
                    if (entry.Value?.GameObject == owner)
                        return entry.Key;
            }
            return null;
        }

        // A stale lookup must never remove a replacement that now owns this ID.
        // Lookup-cache invalidation remains with the caller and only follows success.
        internal bool PruneTrackedObjectIfOwned(string id, GameObject owner)
        {
            if (string.IsNullOrEmpty(id)) return false;
            lock (trackedLock)
            {
                if (!trackedGameObjects.TryGetValue(id, out var tracked) ||
                    tracked != null && tracked.GameObject != null && tracked.GameObject != owner)
                    return false;
                trackedGameObjects.Remove(id);
            }
            lock (activeLock) activeStates.Remove(id);
            return true;
        }

        // These mutate restored bookkeeping only. Capturing a death snapshot and
        // unregistering a live object belong to RegisterDestroyedGameObject instead.
        internal void ReplaceDestroyedIDs(IEnumerable<string> ids)
        {
            destroyedIDs.Clear();
            MergeDestroyedIDs(ids);
        }

        internal void MergeDestroyedIDs(IEnumerable<string> ids)
        {
            if (ids != null) destroyedIDs.UnionWith(ids);
        }

        internal void AddDestroyedID(string id) => destroyedIDs.Add(id);

        #endregion

        string GetUniqueID(GameObject obj)
        {
            // GameObjectTracker tracks GameObjects (active state, transform, etc.) — not components.
            // The ID must therefore be GameObject-level (no per-component suffix). Always prefer the
            // bare UniqueID component value first so tracking stays consistent regardless of whether
            // any RememberGameObject's Awake has populated its cache yet (which differs based on a
            // GameObject's design-time active state).
            if (obj.TryGetComponent<UniqueID>(out var bareUid) && !string.IsNullOrEmpty(bareUid.ID))
                return bareUid.ID;

            // Fall back to RememberGameObject's GameObject-level ID (also bare, no component suffix).
            if (RememberGameObject.TryGetCachedGameObjectUniqueID(obj, out var goId) && !string.IsNullOrEmpty(goId))
                return goId;

            // Last-resort fallback for objects that have no UniqueID component but have a
            // RememberGameObject — use its UniqueIdentifier (with component suffix). Single-component
            // GameObjects in this state cannot suffer the asymmetry described above.
            if (RememberGameObject.TryGetCachedUniqueIdentifier(obj, out var cachedUid) && !string.IsNullOrEmpty(cachedUid))
                return cachedUid;

            // Slow path: SaveablePrefab identifiers and caches
            string saveablePrefabId = null;
            if (SaveablePrefab.TryGetCachedUniqueID(obj, out var cachedSpId) && !string.IsNullOrEmpty(cachedSpId))
            {
                saveablePrefabId = cachedSpId;
            }
            else if (SaveablePrefab.TryGetCachedSaveablePrefab(obj, out var cachedSp) && cachedSp != null && !string.IsNullOrEmpty(cachedSp.UniqueID))
            {
                saveablePrefabId = cachedSp.UniqueID;
            }
            else if (obj.TryGetComponent<SaveablePrefab>(out var sp) && !string.IsNullOrEmpty(sp.UniqueID))
            {
                saveablePrefabId = sp.UniqueID;
            }

            if (!string.IsNullOrEmpty(saveablePrefabId))
            {
                if (obj.TryGetComponent<SceneObjectID>(out var sceneId) && !string.IsNullOrEmpty(sceneId.UniqueID))
                {
                    // Prefer SaveablePrefab ID over SceneObjectID; no debug log.
                }

                return saveablePrefabId;
            }

            if (obj.TryGetComponent<SceneObjectID>(out var fallbackSceneId) && !string.IsNullOrEmpty(fallbackSceneId.UniqueID))
                return fallbackSceneId.UniqueID;

            return null;
        }

        public void RegisterGameObject(GameObject obj, GameObjectPropertySettings settings)
        {
            if (obj == null || settings == null) return;
            string id = GetUniqueID(obj);
            if (string.IsNullOrEmpty(id)) return;

            lock (trackedLock)
            {
                trackedGameObjects[id] = new TrackedGameObject(obj, settings);
            }

            // Always cache the object regardless of its active state to prevent first-load issues
            // This is especially important for objects that are inactive at design time
            saveManager?.CacheGameObject(id, obj);

            if (settings.RememberActive)
            {
                lock (activeLock) 
                {
                    // Only set the initial active state if not already tracked
                    // This prevents overwriting the correct active state during re-registration
                    if (!activeStates.ContainsKey(id))
                    {
                        activeStates[id] = obj.activeSelf;
                    }
                    else
                    {
                        // Preserve existing value when the object re-registers
                    }
                }
            }

            var proxy = GetReusableProxy(obj);
            proxy.Initialize(this, id);
        }

        internal void RebindTrackedGameObjectID(GameObject obj, string previousID, string newID, GameObjectPropertySettings settings)
        {
            if (obj == null || string.IsNullOrEmpty(newID))
                return;

            bool hadPreviousActiveState = false;
            bool previousActiveState = obj.activeSelf;

            if (!string.IsNullOrEmpty(previousID) && previousID != newID)
            {
                bool releasedPreviousOwner = true;
                lock (trackedLock)
                {
                    if (trackedGameObjects.TryGetValue(previousID, out var tracked))
                    {
                        if (tracked?.GameObject != null &&
                            !IsSameInstance(tracked.GameObject, obj))
                        {
                            releasedPreviousOwner = false;
                        }
                        else
                        {
                            trackedGameObjects.Remove(previousID);
                        }
                    }
                }

                if (releasedPreviousOwner &&
                    !HasDifferentLiveOwner(previousID, obj))
                {
                    lock (activeLock)
                    {
                        if (activeStates.TryGetValue(previousID, out previousActiveState))
                        {
                            hadPreviousActiveState = true;
                            activeStates.Remove(previousID);
                        }
                    }

                    destroyedIDs.Remove(previousID);
                }

                saveManager?.UncacheGameObjectIfOwned(previousID, obj);
            }

            if (settings != null)
            {
                RegisterGameObject(obj, settings);
            }
            else
            {
                saveManager?.CacheGameObject(newID, obj);

                var proxy = GetReusableProxy(obj);
                proxy.Initialize(this, newID);
            }

            if (settings != null && settings.RememberActive && hadPreviousActiveState)
            {
                lock (activeLock)
                {
                    activeStates[newID] = previousActiveState;
                }
            }
        }

        public void UnregisterGameObject(GameObject obj)
        {
            if (obj == null) return;
            string id = GetUniqueID(obj);
            if (string.IsNullOrEmpty(id)) return;

            RemoveTrackingIfOwned(id, obj);

            // Only remove the proxy component; do NOT destroy the entire GameObject.
            DestroyProxies(obj);
        }

        public void SoftUnregisterGameObject(GameObject obj)
        {
            SoftUnregisterGameObjectInternal(
                obj,
                skipPrefabManagerCollections: false);
        }

        internal void SoftUnregisterGameObjectForPrefabBatch(GameObject obj)
        {
            SoftUnregisterGameObjectInternal(
                obj,
                skipPrefabManagerCollections: true);
        }

        private void SoftUnregisterGameObjectInternal(
            GameObject obj,
            bool skipPrefabManagerCollections)
        {
            if (!obj) return;
            string id = GetUniqueID(obj);
            if (string.IsNullOrEmpty(id)) return;

            RemoveTrackingIfOwned(id, obj);

            // Pool/load soft-unregister can be followed by re-registration in this
            // same frame. Keep one hidden proxy and reinitialize it instead of
            // scheduling a component that may be reused for deferred destruction.
            DisarmProxies(obj);

            var sp = SaveablePrefab.TryGetCachedSaveablePrefab(obj, out var cachedSp) ? cachedSp : obj.GetComponent<SaveablePrefab>();
            if (sp != null)
            {
                sp.ClearRegisteredFlag();
                if (skipPrefabManagerCollections)
                {
                    // Prevent a pool's OnDespawn callback from repeating the public
                    // soft-unregister path (and its List lookup) during this batch.
                    sp.RegisterWithSaveSystem = false;
                }
                if (!skipPrefabManagerCollections && prefabManager != null)
                {
                    PrefabLoadDiagnosticCounters
                        .RecordLegacyRegistrationCollectionScan();
                    if (prefabManager.RegisteredPrefabs.Contains(sp))
                        prefabManager.UnregisterPrefab(sp);
                }
                // Only reset for pooling (which clears UniqueID) if this was a runtime-added instance.
                // Scene-backed prefabs should retain their UniqueID to remain anchorable across loads.
                if (sp.IsAddedAtRuntime)
                    sp.ResetForPooling();
            }
        }

        private void RemoveTrackingIfOwned(string id, GameObject owner)
        {
            bool hasDifferentTrackedOwner = false;

            lock (trackedLock)
            {
                if (trackedGameObjects.TryGetValue(id, out var tracked))
                {
                    GameObject currentOwner = tracked?.GameObject;
                    if (currentOwner != null && !IsSameInstance(currentOwner, owner))
                    {
                        hasDifferentTrackedOwner = true;
                    }
                    else
                    {
                        trackedGameObjects.Remove(id);
                    }
                }
            }

            // Active-state entries do not carry an owner reference. Retain the value if
            // another live tracker/prefab/cache mapping has already claimed this ID.
            if (!hasDifferentTrackedOwner && !HasDifferentLiveOwner(id, owner))
            {
                lock (activeLock)
                {
                    activeStates.Remove(id);
                }
            }

            // The optional lookup cache can advance to the replacement before a stale
            // object's deferred lifecycle callbacks run. Remove only this object's entry.
            saveManager?.UncacheGameObjectIfOwned(id, owner);
        }

        private TrackedGameObjectProxy GetReusableProxy(GameObject owner)
        {
            proxyBuffer.Clear();
            owner.GetComponents(proxyBuffer);
            for (int i = 0; i < proxyBuffer.Count; i++)
            {
                TrackedGameObjectProxy candidate = proxyBuffer[i];
                if (candidate != null && !candidate.IsPendingRemoval)
                {
                    proxyBuffer.Clear();
                    return candidate;
                }
            }

            proxyBuffer.Clear();
            return owner.AddComponent<TrackedGameObjectProxy>();
        }

        private void DisarmProxies(GameObject owner)
        {
            proxyBuffer.Clear();
            owner.GetComponents(proxyBuffer);
            for (int i = 0; i < proxyBuffer.Count; i++)
                proxyBuffer[i]?.Disarm();
            proxyBuffer.Clear();
        }

        private void DestroyProxies(GameObject owner)
        {
            proxyBuffer.Clear();
            owner.GetComponents(proxyBuffer);
            for (int i = 0; i < proxyBuffer.Count; i++)
            {
                TrackedGameObjectProxy proxy = proxyBuffer[i];
                if (proxy == null)
                    continue;

                // Destroy(Component) is deferred in play mode. Disarm first so the
                // obsolete proxy cannot report against a replacement owner.
                proxy.Disarm(isPendingRemoval: true);
                if (Application.isPlaying)
                    Destroy(proxy);
                else
                    DestroyImmediate(proxy);
            }
            proxyBuffer.Clear();
        }

        internal bool HasDifferentLiveOwner(string id, GameObject expectedOwner)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            lock (trackedLock)
            {
                if (trackedGameObjects.TryGetValue(id, out var tracked) &&
                    tracked?.GameObject != null &&
                    !IsSameInstance(tracked.GameObject, expectedOwner))
                {
                    return true;
                }
            }

            if (prefabManager != null &&
                prefabManager.TryGetInstantiatedPrefab(id, out GameObject prefabOwner) &&
                !IsSameInstance(prefabOwner, expectedOwner))
            {
                return true;
            }

            return saveManager != null &&
                   saveManager.TryGetCachedGameObject(id, out GameObject cachedOwner) &&
                   !IsSameInstance(cachedOwner, expectedOwner);
        }

        private static bool IsSameInstance(GameObject left, GameObject right)
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

        public void RegisterDestroyedGameObject(string id)
        {
            if (string.IsNullOrEmpty(id) || destroyedIDs.Contains(id)) return;

            if (saveManager.CurrentSaveData == null)
                saveManager.CurrentSaveData = new SaveData();

            if (trackedGameObjects.TryGetValue(id, out var tgo) && tgo?.GameObject != null)
            {
                var obj = tgo.GameObject;
                if (Logger.ShouldLog(LogCategory.GameObjectTracker, LogLevel.Info))
                {
                    Logger.Log(
                        $"GameObjectTracker: Marking '{obj.name}' (ID: {id}) as destroyed in scene '{obj.scene.name}'.",
                        obj,
                        LogCategory.GameObjectTracker,
                        LogLevel.Info);
                }
                var data = componentManager.CollectComponentDataForObject(obj);
                saveManager.CurrentSaveData.DestroyedObjectData[id] = data;
            }
            else
            {
                if (Logger.ShouldLog(LogCategory.GameObjectTracker, LogLevel.Info))
                {
                    Logger.Log(
                        $"GameObjectTracker: Marking ID '{id}' as destroyed (object not currently tracked).",
                        LogCategory.GameObjectTracker,
                        LogLevel.Info);
                }
            }

            destroyedIDs.Add(id);
            lock (trackedLock) trackedGameObjects.Remove(id);
            lock (activeLock) activeStates.Remove(id);
            saveManager?.UncacheGameObject(id);
        }

        internal void RemoveDestroyedID(string id) => destroyedIDs.Remove(id);
        internal void RemoveDestroyedWhere(System.Predicate<string> match) => destroyedIDs.RemoveWhere(match);

        public void CaptureDestroyedDataIfPossible(string id)
        {
            if (saveManager.CurrentSaveData == null)
                saveManager.CurrentSaveData = new SaveData();

            if (trackedGameObjects.TryGetValue(id, out var tgo) && tgo?.GameObject != null)
            {
                var obj = tgo.GameObject;
                var data = componentManager.CollectComponentDataForObject(obj);
                saveManager.CurrentSaveData.DestroyedObjectData[id] = data;
                
                var sp = SaveablePrefab.TryGetCachedSaveablePrefab(obj, out var cachedSp) ? cachedSp : obj.GetComponent<SaveablePrefab>();
                if (sp != null && prefabManager != null)
                {
                    // Standard path: build full prefab data from SaveablePrefab
                    var pd = prefabManager.BuildPrefabData(sp);
                    if (pd != null)
                    {
                        saveManager.CurrentSaveData.Prefabs.RemoveAll(p => p.InstanceID == id);
                        saveManager.CurrentSaveData.Prefabs.Add(pd);
                        
                    }
                }
                else
                {
                    // New: Scene object without SaveablePrefab – persist its last known transform
                    // so RestoreDestroyedGameObject can place the restored prefab at the correct spot.
                    var tr = obj.transform;
                    string parentId = null;
                    bool isParentSceneObject = false;
                    if (tr != null && tr.parent != null)
                    {
                        var parentGO = tr.parent.gameObject;
                        parentId = GetUniqueID(parentGO);
                        // Consider it a scene object parent if it has a SceneObjectID
                        isParentSceneObject = parentGO.GetComponent<SceneObjectID>() != null;
                    }

                    var synthesized = new SaveablePrefabData(
                        instanceID: id,
                        prefabID: string.Empty, // unknown here, mapping is handled via SceneObjectRegistry during restore
                        gameObjectName: obj.name,
                        position: tr != null ? tr.position : Vector3.zero,
                        rotation: tr != null ? tr.rotation : Quaternion.identity,
                        scale: tr != null ? tr.localScale : Vector3.one,
                        parentID: parentId,
                        isParentSceneObject: isParentSceneObject,
                        visibilitySettingsData: null,
                        homeScene: null,
                        disablePooling: false
                    );
                    synthesized.HasTransformOverride = true;
                    synthesized.HasParentOverride = !string.IsNullOrEmpty(parentId);

                    saveManager.CurrentSaveData.Prefabs.RemoveAll(p => p.InstanceID == id);
                    saveManager.CurrentSaveData.Prefabs.Add(synthesized);
                    
                }
            }
        }

        public bool TryGetTrackedGameObject(string id, out TrackedGameObject tracked)
        {
            tracked = null;
            if (string.IsNullOrEmpty(id))
                return false;

            bool removedStaleEntry = false;
            GameObject staleOwner = null;
            lock (trackedLock)
            {
                if (!trackedGameObjects.TryGetValue(id, out var candidate))
                    return false;

                if (candidate != null && candidate.GameObject != null)
                {
                    tracked = candidate;
                    return true;
                }

                // Unity objects compare equal to null after destruction. Do not keep returning
                // a dead tracker entry (or repeatedly route lookups through it).
                staleOwner = candidate?.GameObject;
                trackedGameObjects.Remove(id);
                removedStaleEntry = true;
            }

            if (removedStaleEntry)
            {
                // Scene-unloaded objects intentionally retain their last active state so it can
                // be serialized while their additive scene is absent and reapplied when the
                // object registers again. Only the stale live-owner mappings are pruned here.
                saveManager?.UncacheGameObjectIfOwned(id, staleOwner);
            }

            return false;
        }

        public void UpdateActiveState(string id, bool isActive)
        {
            CancelPendingInactiveStateUpdate(id, null);
            ApplyActiveState(id, null, isActive);
        }

        internal void UpdateActiveState(string id, GameObject owner, bool isActive)
        {
            // OnDisable is also raised while Unity tears down a scene. Committing that
            // temporary false immediately would overwrite the last real state before the
            // owner becomes Unity-null. Confirm owner-originated deactivations one frame
            // later; a normal SetActive(false) owner is still live, while an unloaded owner
            // is not. CollectActiveStates remains same-frame correct because it resolves a
            // live inactive owner and reads activeSelf directly.
            if (owner != null && !isActive)
            {
                if (saveManager != null && saveManager.IsInSceneTransition)
                {
                    ApplyActiveState(id, owner, false);
                    return;
                }

                QueueInactiveStateUpdate(id, owner);
                return;
            }

            CancelPendingInactiveStateUpdate(id, owner);
            ApplyActiveState(id, owner, isActive);
        }

        private void ApplyActiveState(string id, GameObject owner, bool isActive)
        {
            lock (trackedLock)
            {
                if (trackedGameObjects.TryGetValue(id, out var tgo) &&
                    tgo?.GameObject != null &&
                    (owner == null || IsSameInstance(tgo.GameObject, owner)) &&
                    tgo.Settings.RememberActive)
                {
                    bool isSceneTransition = saveManager != null && saveManager.IsInSceneTransition;

                    lock (activeLock)
                    {
                        if (isSceneTransition && !isActive && activeStates.TryGetValue(id, out var previousValue) && previousValue)
                        {
                            // Ignore temporary deactivations triggered by scene unload while we're in a managed transition.
                            return;
                        }

                        activeStates[id] = isActive;
                    }
                }
            }
        }

        private void QueueInactiveStateUpdate(string id, GameObject owner)
        {
            if (string.IsNullOrEmpty(id) || owner == null)
                return;

            lock (trackedLock)
            {
                if (!trackedGameObjects.TryGetValue(id, out var tracked) ||
                    tracked?.GameObject == null ||
                    !IsSameInstance(tracked.GameObject, owner) ||
                    tracked.Settings == null ||
                    !tracked.Settings.RememberActive)
                {
                    return;
                }
            }

            lock (pendingInactiveStateLock)
            {
                pendingInactiveStateUpdates[id] =
                    new PendingInactiveStateUpdate(owner);

                if (pendingInactiveStateRoutine == null && isActiveAndEnabled)
                {
                    pendingInactiveStateRoutine =
                        StartOwnedRoutine(ConfirmPendingInactiveStateUpdates());
                }
            }
        }

        private void CancelPendingInactiveStateUpdate(string id, GameObject owner)
        {
            if (string.IsNullOrEmpty(id))
                return;

            lock (pendingInactiveStateLock)
            {
                if (!pendingInactiveStateUpdates.TryGetValue(
                        id,
                        out PendingInactiveStateUpdate pending))
                {
                    return;
                }

                if (owner == null || IsSameInstance(pending.Owner, owner))
                    pendingInactiveStateUpdates.Remove(id);
            }
        }

        private IEnumerator ConfirmPendingInactiveStateUpdates()
        {
            while (true)
            {
                yield return null;
                FlushPendingInactiveStateUpdates();

                lock (pendingInactiveStateLock)
                {
                    if (pendingInactiveStateUpdates.Count == 0)
                    {
                        pendingInactiveStateRoutine = null;
                        yield break;
                    }
                }
            }
        }

        private void FlushPendingInactiveStateUpdates()
        {
            List<KeyValuePair<string, PendingInactiveStateUpdate>> snapshot;
            lock (pendingInactiveStateLock)
            {
                if (pendingInactiveStateUpdates.Count == 0)
                    return;

                snapshot =
                    new List<KeyValuePair<string, PendingInactiveStateUpdate>>(
                        pendingInactiveStateUpdates);
                pendingInactiveStateUpdates.Clear();
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                string id = snapshot[i].Key;
                GameObject owner = snapshot[i].Value.Owner;
                if (owner == null || owner.activeSelf)
                    continue;

                ApplyActiveState(id, owner, false);
            }
        }

        public List<GameObjectState> CollectActiveStates()
        {
            var list = new List<GameObjectState>();
            List<KeyValuePair<string, bool>> snapshot;

            lock (activeLock)
            {
                snapshot = new List<KeyValuePair<string, bool>>(activeStates);
            }

            var pendingStates = new List<KeyValuePair<string, bool>>(snapshot.Count);
            for (int i = 0; i < snapshot.Count; i++)
            {
                string uniqueId = snapshot[i].Key;
                bool isActive = snapshot[i].Value;

                // Skip emitting active-state data for reusable SaveablePrefabs. Their
                // persisted prefab snapshot already records the correct state and the
                // prefab pipeline should remain authoritative to avoid reactivation when
                // scenes are reloaded.
                GameObject candidate = saveManager != null
                    ? saveManager.FindGameObjectByUniqueIDWithoutMissingLog(
                        uniqueId,
                        SaveManager.IdentifierType.UniqueID)
                    : null;

                if (candidate != null &&
                    candidate.TryGetComponent<SaveablePrefab>(out var sp) &&
                    sp.ReuseSceneInstanceOnLoad &&
                    !sp.IsAddedAtRuntime)
                {
                    Logger.Log($"[CrystalSave][GameObjectTracker] CollectActiveStates: skipping reusable SaveablePrefab '{candidate.name}' (UniqueID '{uniqueId}')", LogCategory.GameObjectTracker, LogLevel.Info);
                    continue;
                }

                // Query a live object outside activeLock. Lookup may prune stale tracker/cache
                // mappings, and proxy/lifecycle code may independently remove an obsolete
                // active-state key while this snapshot is being evaluated.
                if (candidate != null)
                    isActive = candidate.activeSelf;

                pendingStates.Add(new KeyValuePair<string, bool>(uniqueId, isActive));
            }

            // Apply staged values and emit them together under the lock. A key deliberately
            // removed after the snapshot (identity change, unregister, or destruction) must
            // not be resurrected or serialized from stale snapshot data.
            lock (activeLock)
            {
                for (int i = 0; i < pendingStates.Count; i++)
                {
                    string uniqueId = pendingStates[i].Key;
                    if (!activeStates.ContainsKey(uniqueId))
                        continue;

                    bool isActive = pendingStates[i].Value;
                    activeStates[uniqueId] = isActive;
                    list.Add(new GameObjectState
                    {
                        UniqueID = uniqueId,
                        IsActive = isActive
                    });
                }
            }

            return list;
        }

        private bool ShouldSkipActiveStateForRememberHome(GameObject go)
        {
            if (go == null) return false;
            if (go.TryGetComponent<RememberGameObject>(out var rememberGO) &&
                rememberGO != null &&
                rememberGO.RememberHomeScene)
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.IsValid() && rememberGO.MatchesHomeScene(scene.name))
                {
                    return true;
                }
            }
            return false;
        }

        public void ApplyGameObjectActiveStates(List<GameObjectState> states)
        {
            if (states == null) return;
            int batchSize = saveManager.SaveSettings?.activeStateApplyBatchSize ?? 0;
            if (batchSize <= 0)
            {
                foreach (var state in states)
                {
                    if (!state.IsActive.HasValue) continue;
                    if (!IsTracked(state.UniqueID)) continue;

                    var go = saveManager.FindGameObjectByUniqueIDWithoutMissingLog(
                        state.UniqueID,
                        SaveManager.IdentifierType.UniqueID);
                    if (go == null) continue;

                    // Check if this GameObject has a SaveablePrefab that reuses scene instances
                    // If so, the prefab system should take precedence over GameObject active states
                    bool skipForReusablePrefab = false;
                    if (go.TryGetComponent<SaveablePrefab>(out var saveablePrefab))
                    {
                        if (saveablePrefab.ReuseSceneInstanceOnLoad && !saveablePrefab.IsAddedAtRuntime)
                        {
                            Logger.Log($"[CrystalSave][GameObjectTracker] Skipping active state application for reusable SaveablePrefab '{go.name}' (UniqueID '{state.UniqueID}') - prefab system takes precedence", LogCategory.GameObjectTracker, LogLevel.Info);
                            skipForReusablePrefab = true;
                        }
                    }
                    else
                    {
                        // No SaveablePrefab component found, continue with normal processing
                    }

                    if (skipForReusablePrefab)
                    {
                        // Still update our internal tracking but don't change the actual GameObject
                        lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                        continue;
                    }

                    if (ShouldSkipActiveStateForRememberHome(go))
                    {
                        // RememberHomeScene snapshot is authoritative in its home scene.
                        lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                        continue;
                    }

                    RememberGameObject rememberComponent = null;
                    go.TryGetComponent<RememberGameObject>(out rememberComponent);

                    try
                    {
                        // Set flag to prevent OnDestroy from registering destroyed GameObjects during state application
                        if (rememberComponent != null)
                        {
                            rememberComponent.SetApplyingActiveState(true);
                        }
                        
                        try
                        {
                            go.SetActive(state.IsActive.Value);
                            lock (activeLock) activeStates[state.UniqueID] = state.IsActive.Value;
                        }
                        finally
                        {
                            // Clear flag after state application (even if SetActive throws)
                            if (rememberComponent != null)
                            {
                                rememberComponent.SetApplyingActiveState(false);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Logger.Log($"GameObjectTracker: Error applying active state for '{state.UniqueID}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.GameObjectTracker, LogLevel.Error);
                    }
                }
                StartOwnedRoutine(EnforceActiveStatesCoroutine(states, batchSize));
            }
            else
            {
                StartOwnedRoutine(ApplyActiveStatesCoroutine(states, batchSize));
            }
        }

        IEnumerator ApplyActiveStatesCoroutine(List<GameObjectState> states, int batchSize)
        {
            if (states == null) yield break;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (!state.IsActive.HasValue) continue;
                if (!IsTracked(state.UniqueID)) continue;

                var go = saveManager.FindGameObjectByUniqueIDWithoutMissingLog(
                    state.UniqueID,
                    SaveManager.IdentifierType.UniqueID);
                if (go == null) continue;

                // Check if this GameObject has a SaveablePrefab that reuses scene instances
                // If so, the prefab system should take precedence over GameObject active states
                bool skipForReusablePrefab = false;
                if (go.TryGetComponent<SaveablePrefab>(out var saveablePrefab))
                {
                    if (saveablePrefab.ReuseSceneInstanceOnLoad && !saveablePrefab.IsAddedAtRuntime)
                    {
                        skipForReusablePrefab = true;
                    }
                }

                if (skipForReusablePrefab)
                {
                    // Still update our internal tracking but don't change the actual GameObject
                    lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                    continue;
                }

                if (ShouldSkipActiveStateForRememberHome(go))
                {
                    // RememberHomeScene snapshot is authoritative in its home scene.
                    lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                    continue;
                }

                RememberGameObject rememberComponent = null;
                go.TryGetComponent<RememberGameObject>(out rememberComponent);

                try
                {
                    // Set flag to prevent OnDestroy from registering destroyed GameObjects during state application
                    if (rememberComponent != null)
                    {
                        rememberComponent.SetApplyingActiveState(true);
                    }
                    
                    try
                    {
                        go.SetActive(state.IsActive.Value);
                        lock (activeLock) activeStates[state.UniqueID] = state.IsActive.Value;
                    }
                    finally
                    {
                        // Clear flag after state application (even if SetActive throws)
                        if (rememberComponent != null)
                        {
                            rememberComponent.SetApplyingActiveState(false);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.Log($"GameObjectTracker: Error applying active state for '{state.UniqueID}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.GameObjectTracker, LogLevel.Error);
                }

                if ((i + 1) % batchSize == 0)
                    yield return null;
            }
            StartOwnedRoutine(EnforceActiveStatesCoroutine(states, batchSize));
        }

        IEnumerator EnforceActiveStatesCoroutine(List<GameObjectState> states, int batchSize)
        {
            if (states == null) yield break;
            for (int i = 0; i < activeStateEnforceDelayFrames; i++)
                yield return null;

            if (batchSize <= 0)
            {
                foreach (var state in states)
                {
                    if (!state.IsActive.HasValue) continue;
                    var go = saveManager.FindGameObjectByUniqueIDWithoutMissingLog(
                        state.UniqueID,
                        SaveManager.IdentifierType.UniqueID);
                    if (go == null) continue;
                    if (ShouldSkipActiveStateForRememberHome(go))
                    {
                        lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                        continue;
                    }
                    if (go.activeSelf != state.IsActive.Value)
                    {
                        go.SetActive(state.IsActive.Value);
                        lock (activeLock) activeStates[state.UniqueID] = state.IsActive.Value;
                    }
                }
            }
            else
            {
                for (int i = 0; i < states.Count; i++)
                {
                    var state = states[i];
                    if (!state.IsActive.HasValue) continue;
                    var go = saveManager.FindGameObjectByUniqueIDWithoutMissingLog(
                        state.UniqueID,
                        SaveManager.IdentifierType.UniqueID);
                    if (go == null) continue;
                    if (ShouldSkipActiveStateForRememberHome(go))
                    {
                        lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                        continue;
                    }
                    if (go.activeSelf != state.IsActive.Value)
                    {
                        go.SetActive(state.IsActive.Value);
                        lock (activeLock) activeStates[state.UniqueID] = state.IsActive.Value;
                    }
                    if ((i + 1) % batchSize == 0)
                        yield return null;
                }
            }
        }

        IEnumerator WatchActiveStatesCoroutine(List<GameObjectState> states)
        {
            if (states == null) yield break;
            float elapsed = 0f;
            while (elapsed < activeStateWatchDuration)
            {
                foreach (var state in states)
                {
                    if (!state.IsActive.HasValue) continue;
                    var go = saveManager.FindGameObjectByUniqueIDWithoutMissingLog(
                        state.UniqueID,
                        SaveManager.IdentifierType.UniqueID);
                    if (go == null) continue;
                    if (ShouldSkipActiveStateForRememberHome(go))
                    {
                        lock (activeLock) activeStates[state.UniqueID] = go.activeSelf;
                        continue;
                    }
                    if (go.activeSelf != state.IsActive.Value)
                    {
                        go.SetActive(state.IsActive.Value);
                        lock (activeLock) activeStates[state.UniqueID] = state.IsActive.Value;
                    }
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            watchRoutine = null;
            watchEnumerator = null;
        }

        public void StartActiveStateWatch(List<GameObjectState> states)
        {
            if (activeStateWatchDuration <= 0f) return;
            StopActiveStateWatch();
            watchRoutine = StartOwnedRoutine(WatchActiveStatesCoroutine(states), out watchEnumerator);
        }

        public void StopActiveStateWatch()
        {
            if (watchEnumerator != null)
            {
                StopOwnedRoutine(watchEnumerator);
                watchRoutine = null;
                watchEnumerator = null;
            }
        }
    }
}
#endif
