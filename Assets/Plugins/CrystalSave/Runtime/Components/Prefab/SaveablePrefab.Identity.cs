#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Profiling;
#endif

namespace Arawn.CrystalSave.Runtime
{
        public partial class SaveablePrefab
        {
                // Restore operations can reuse a caller-owned component buffer to avoid
                // allocating one List per restored prefab. The set protects a shared
                // buffer from nested/reentrant restore calls; a nested call falls back
                // to the allocation-backed public path instead of corrupting its owner.
                private static readonly HashSet<List<SaveableComponent>> s_RestoreUniqueIdBuffersInUse = new();

#if UNITY_EDITOR
                private readonly struct DeferredUniqueIdRemoval
                {
                        public readonly GameObject GameObject;
                        public readonly UniqueID UniqueID;

                        public DeferredUniqueIdRemoval(GameObject gameObject, UniqueID uniqueID)
                        {
                                GameObject = gameObject;
                                UniqueID = uniqueID;
                        }
                }

                private readonly struct DeferredPrefabRegistrySync
                {
                        public readonly SaveablePrefab Current;
                        public readonly string PrefabAssetPath;
                        public readonly string PrefabAssetGuid;

                        public DeferredPrefabRegistrySync(
                                SaveablePrefab current,
                                string prefabAssetPath,
                                string prefabAssetGuid)
                        {
                                Current = current;
                                PrefabAssetPath = prefabAssetPath;
                                PrefabAssetGuid = prefabAssetGuid;
                        }
                }

#pragma warning disable UDR0001 // Editor-only delay queues are cleared by assembly reload and play-mode lifecycle hooks below.
                private static readonly List<DeferredUniqueIdRemoval> s_PendingUniqueIdRemovals = new();
                private static readonly List<DeferredPrefabRegistrySync> s_PendingPrefabRegistrySyncs = new();
                private static readonly ProfilerMarker s_PrefabIdentityBatchMarker =
                        new ProfilerMarker("CrystalSave.PrefabIdentity.DelayedBatch");
                private static bool s_UniqueIdRemovalFlushQueued;
                private static bool s_PrefabRegistrySyncFlushQueued;
                private static bool s_ApplyingAutomaticPrefabIdentity;
                private static int s_PrefabRegistryBatchSaveCount;

                [InitializeOnLoadMethod]
                private static void InitializeEditorDelayCleanup()
                {
                        AssemblyReloadEvents.beforeAssemblyReload -= ClearEditorDelayQueues;
                        AssemblyReloadEvents.beforeAssemblyReload += ClearEditorDelayQueues;
                        EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
                        EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
                }
#pragma warning restore UDR0001

                private static void OnEditorPlayModeStateChanged(PlayModeStateChange state)
                {
                        if (state == PlayModeStateChange.ExitingEditMode ||
                            state == PlayModeStateChange.EnteredEditMode)
                        {
                                ClearEditorDelayQueues();
                        }
                }

                private static void ClearEditorDelayQueues()
                {
                        EditorApplication.delayCall -= FlushPendingUniqueIdRemovals;
                        EditorApplication.delayCall -= FlushPendingPrefabRegistrySyncs;
                        s_PendingUniqueIdRemovals.Clear();
                        s_PendingPrefabRegistrySyncs.Clear();
                        s_UniqueIdRemovalFlushQueued = false;
                        s_PrefabRegistrySyncFlushQueued = false;
                        s_ApplyingAutomaticPrefabIdentity = false;
                }

                /// <summary>Diagnostic-only view of the production delayed queues.</summary>
                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                public static int PendingPrefabRegistrySyncCountForEditorDiagnostics =>
                        s_PendingPrefabRegistrySyncs.Count;

                /// <summary>Diagnostic-only view used to refuse a run that could disturb unrelated work.</summary>
                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                public static int PendingUniqueIdRemovalCountForEditorDiagnostics =>
                        s_PendingUniqueIdRemovals.Count;

                /// <summary>
                /// Temporarily removes the scheduled callback without changing the production queue.
                /// The diagnostic can then verify the queued count before explicitly flushing it.
                /// </summary>
                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                public static void SuspendPrefabRegistrySyncFlushForEditorDiagnostics()
                {
                        EditorApplication.delayCall -= FlushPendingPrefabRegistrySyncs;
                }

                /// <summary>Executes the real production delayed-batch implementation.</summary>
                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                public static void FlushPrefabRegistrySyncsForEditorDiagnostics()
                {
                        EditorApplication.delayCall -= FlushPendingPrefabRegistrySyncs;
                        FlushPendingPrefabRegistrySyncs();
                }

                /// <summary>
                /// Removes only queued prefab work belonging to a diagnostic asset folder.
                /// Unrelated editor callbacks remain scheduled and intact.
                /// </summary>
                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
                public static void RemovePrefabRegistrySyncsForEditorDiagnostics(string assetPathPrefix)
                {
                        if (string.IsNullOrEmpty(assetPathPrefix))
                                return;

                        string normalizedPrefix = assetPathPrefix.Replace('\\', '/').TrimEnd('/') + "/";
                        s_PendingPrefabRegistrySyncs.RemoveAll(item =>
                                !string.IsNullOrEmpty(item.PrefabAssetPath) &&
                                item.PrefabAssetPath.Replace('\\', '/').StartsWith(
                                        normalizedPrefix,
                                        StringComparison.Ordinal));

                        if (s_PendingPrefabRegistrySyncs.Count == 0)
                        {
                                EditorApplication.delayCall -= FlushPendingPrefabRegistrySyncs;
                                s_PrefabRegistrySyncFlushQueued = false;
                        }
                        else if (s_PrefabRegistrySyncFlushQueued)
                        {
                                EditorApplication.delayCall -= FlushPendingPrefabRegistrySyncs;
                                EditorApplication.delayCall += FlushPendingPrefabRegistrySyncs;
                        }
                }
#endif

                public bool IsLoading => isLoading;

                // Flag to avoid emitting the instantiation event twice when a prefab
                // starts inactive and later becomes active.
                bool _instantiatedRaised = false;

                // Guard to prevent running initialization more than once when we
                // manually initialize an inactive instance via the factory.
                bool _initialized = false;

               private void Awake()
               {
                       InitializeInstance();
                       CacheSelfIfEnabled();
               }

                private void Start()
                {
                        // All helper Awake calls have completed by Start, so their composite
                        // identities are stable before central ownership reconciliation runs.
                        RequestComponentBlobOwnershipRefresh();
                }

                /// <summary>
                /// Performs the work previously done in <c>Awake</c> so that inactive
                /// prefabs instantiated at runtime can be initialized manually.
                /// </summary>
                public void InitializeInstance()
                {
                        if (_initialized) return;
                        _initialized = true;

                        // Snapshot initial GameObject properties for runtime tracking
	                        initialName        = gameObject.name;
	                        initialTag         = gameObject.tag;
	                        initialLayer       = gameObject.layer;
	                        initialActiveState = gameObject.activeSelf;
	                        CaptureRuntimeOriginSceneName();

                        visibilityController ??= GetComponent<PersistentVisibilityController>();

                        // Runtime safety-net: if the prefab is configured for cross-scene
                        // persistence with off-screen behavior but no PVC component exists
                        // (e.g. the component was never saved onto the prefab asset, or was
                        // stripped), create it dynamically so visibility toggling works.
                        // This MUST run before the rememberHomeScene block below which may
                        // clear offScreenMask, so PVC captures the original flag values.
                        if (visibilityController == null
                            && (keepAcrossScenes || rememberHomeScene)
                            && offScreenMask != OffScreenDeactivation.None
                            && VisibleInScenes != null
                            && VisibleInScenes.Count > 0)
                        {
                                visibilityController = gameObject.AddComponent<PersistentVisibilityController>();
                        }

                        EnsureUniqueID();             // generate ONLY when needed
                        EnsureUniqueIDComponent();    // keep the UniqueID component in-sync
                        HandleDontDestroy();
                        DetectRuntimeInstantiation();

                        if (skipSavingWhenUnchanged && !isAddedAtRuntime)
                        {
                                if (TryCaptureCurrentState(out var snapshot))
                                {
                                        baselineSnapshot = snapshot;
                                        lastSnapshot     = snapshot;
                                }
                        }

                        // If this is a scene-placed instance (not added at runtime), the
                        // user opted out of save-system registration, and the Instance
                        // Unique ID is still empty (e.g., during a Loading state where
                        // EnsureUniqueID() intentionally skipped generation), then assign
                        // a runtime ID so other systems relying on a stable identifier can
                        // function. This runs in both Editor Play Mode and Player builds.
                                                bool shouldAssignInstanceId = autoAssignInstanceIDWhenUnregistered &&
                                                        !RegisterWithSaveSystem &&
                                                        !isAddedAtRuntime &&
                                                        string.IsNullOrEmpty(uniqueID);

                        #if UNITY_EDITOR
                                                if (shouldAssignInstanceId && ShouldSkipEditorInstanceIDGeneration())
                                                {
                                                        shouldAssignInstanceId = false;
                                                }
                        #endif

                                                if (shouldAssignInstanceId)
                                                {
                                                        uniqueID = Guid.NewGuid().ToString();

                                // Keep UniqueID component mirrored when present (we do not
                                // auto-create it during play).
                                var uid = GetComponent<UniqueID>();
                                if (uid != null) uid.ID = uniqueID;
                        }

                        // For pooled prefabs with RememberHomeScene, don't overwrite HomeScene 
                        // if it's already been set to a real scene name and we're now in DontDestroyOnLoad
                        if (rememberHomeScene && 
                            !string.IsNullOrEmpty(homeScene) && 
                            homeScene != "DontDestroyOnLoad" &&
                            gameObject.scene.name == "DontDestroyOnLoad")
                        {
                            // Keep the existing HomeScene instead of overwriting with DontDestroyOnLoad
                        }
                        else
                        {
                            homeScene = gameObject.scene.name;
                        }

                        ValidatePersistenceSettings();

                        if (trackChildStateOverrides)
                                AttachDestroyObserversRecursively(transform);

                        if (!RegisterWithSaveSystem) return;

                        var remember = GetComponent<RememberGameObject>();
                        settings = remember ? remember.PropertySettings : propertySettings;
                        settings ??= new GameObjectPropertySettings { RememberActive = true };

                        /* During a load we defer registration until the real UniqueID has
                         * been written by PrefabManager → SetUniqueID() will take care of it */
                        var state = SaveManager.Instance?.StateMachine?.CurrentState;
                        if (!string.IsNullOrEmpty(uniqueID) && state != SaveState.Loading)
                                RegisterForSaving();

                        // If the prefab is instantiated while inactive, OnEnable will not
                        // execute and the PrefabManager would miss the registration. Raise
                        // the instantiation event here so disabled prefabs are handled
                        // immediately.
                        if (!gameObject.activeInHierarchy && !string.IsNullOrEmpty(uniqueID))
                        {
                                OnPrefabInstantiated?.Invoke(this);
                                _instantiatedRaised = true;
                        }
                }

               private void OnEnable()
               {
                       CacheSelfIfEnabled();
                       // in normal game-play OnEnable _might_ be the first moment where
                       // uniqueID exists (e.g. when prefab was added at runtime)
                       if (RegisterWithSaveSystem &&
                               !isRegisteredWithSaveManager &&
                               !string.IsNullOrEmpty(uniqueID))
                       {
                               RegisterForSaving();
                       }
                       if (!string.IsNullOrEmpty(uniqueID) && !_instantiatedRaised)
                       {
                               OnPrefabInstantiated?.Invoke(this);
                               _instantiatedRaised = true;
                       }

#if CRYSTALSAVE_TIMEMACHINE
                       // If this prefab opted into TimeMachine recording, ensure a TimeMachineRecorder exists
                       if (Application.isPlaying && enableTimeMachineRecording)
                       {
                               EnsureTimeMachineRecorder();
                       }
#endif
               }

                private void OnValidate()
                {
                #if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                                                bool markDirty = false;
                                                bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObject);

                                var hasComposite = GetComponent<RememberComposite>() != null;
                                var uid = GetComponent<UniqueID>();

	                                if (hasComposite && uid != null)
	                                {
	                                        QueueUniqueIdRemoval(gameObject, uid);
	                                }
                                else if (uid != null)
                                {
                                        uid.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
                                }

                                // Only assign Instance UniqueID if:
                                // 1. Not a prefab asset (isPrefabAsset == false)
                                // 2. Not in prefab editing mode (IsInPrefabMode() == false)
                                // 3. GameObject is placed in a valid scene (scene.IsValid() == true)
                                //
                                // This ensures only DESIGN-TIME PLACED instances get deterministic IDs.
                                // Runtime-spawned prefabs will get their IDs generated at runtime in Awake().
                                //
                                // Valid scenarios for design-time ID assignment:
                                // - Prefabs dropped into scene hierarchy during edit mode
                                // - Disabled prefabs placed in scene (enabled at runtime)
                                // - Nested prefabs in scene (children of scene GameObjects)
                                bool isPlacedInScene = gameObject.scene.IsValid();
                                bool inPrefabMode = IsInPrefabMode();
                                bool isSceneInstance = isPlacedInScene && !isPrefabAsset && !inPrefabMode;

                                if (isSceneInstance && string.IsNullOrEmpty(uniqueID))
                                {
                                        uniqueID = Guid.NewGuid().ToString();
                                        markDirty = true;
                                }
                                // Only sync UniqueID component for scene-placed instances
                                if (isSceneInstance)
                                {
                                        EnsureUniqueIDComponent();
                                }

                                if (!isPrefabAsset && !isAddedAtRuntime)
                                {
                                        if (autoEnforceReuseSceneInstanceOnLoad)
                                        {
                                                if (!reuseSceneInstanceOnLoad)
                                                {
                                                        reuseSceneInstanceOnLoad = true;
                                                        markDirty = true;
                                                }

                                                autoEnforceReuseSceneInstanceOnLoad = false;
                                                markDirty = true;
                                        }
                                }

                                GetComponent<RememberComposite>()?.ScheduleRefresh();

                                // ── Auto-assign PrefabAssetID when this becomes a prefab asset ──
	                                if (isPrefabAsset)
	                                {
	                                        if (!string.IsNullOrEmpty(uniqueID))
	                                        {
	                                                uniqueID = string.Empty;
	                                                markDirty = true;
	                                        }

	                                        // Resolve the actual prefab asset - handles Prefab Mode staging objects.
                                        // Identity correction remains immediate, while registry writes are delayed.
                                        GameObject prefabAsset = GetPrefabAssetFromInstance(gameObject);
                                        if (prefabAsset != null && !s_ApplyingAutomaticPrefabIdentity)
                                        {
                                                string previousId = prefabAssetID;
                                                string ensuredId = EnsurePrefabAssetIdentityForEditor(
                                                        this,
                                                        prefabAsset);

                                                if (!string.IsNullOrEmpty(ensuredId))
                                                {
                                                        if (!string.Equals(previousId, ensuredId, StringComparison.Ordinal))
                                                                markDirty = true;
                                                }
                                        }
                                }

                                ValidatePersistenceSettings();

                                if (markDirty)
                                        EditorUtility.SetDirty(this);
                        }
                #endif
                }

#if UNITY_EDITOR
	                /// <summary>
	                /// Applies the immediate prefab-component identity decision and queues only
	                /// registry work that is still required. Public solely for Unity's predefined
	                /// runtime/editor assembly boundary.
	                /// </summary>
	                [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
	                public static string EnsurePrefabAssetIdentityForEditor(
	                        SaveablePrefab current,
	                        GameObject prefabAsset)
	                {
#if UNITY_2022_2_OR_NEWER
                                // Undo restores the authored values, including a mismatch
                                // that prompted an explicit repair. OnValidate must not turn
                                // that restoration into another identity edit.
                                if (Undo.isProcessing)
                                        return current != null ? current.PrefabAssetID : null;
#endif
	                        if (current == null ||
	                            prefabAsset == null ||
	                            PrefabRegistryEditorDiagnosticCounters.ShouldSuppressAutomaticPrefabProcessing(prefabAsset) ||
	                            s_ApplyingAutomaticPrefabIdentity ||
	                            Application.isPlaying ||
	                            EditorApplication.isPlayingOrWillChangePlaymode)
	                        {
	                                return current != null ? current.PrefabAssetID : null;
	                        }

	                        PrefabRegistry registry = PrefabAssetResolver.LoadActivePrefabRegistry();
	                        PrefabRegistryIdentityIndex index =
	                                PrefabRegistryIdentityIndex.GetOrCreate(registry);
	                        string previousId = current.prefabAssetID;
	                        bool identitySnapshotChanged =
	                                index.HasPrefabIdentitySnapshotChanged(prefabAsset, previousId);
	                        string ensuredId;

	                        s_ApplyingAutomaticPrefabIdentity = true;
	                        try
	                        {
	                                ensuredId = PrefabAssetResolver.EnsurePrefabAssetIDForAsset(
	                                        current,
	                                        prefabAsset,
	                                        index);
	                        }
	                        finally
	                        {
	                                s_ApplyingAutomaticPrefabIdentity = false;
	                        }

	                        if (!string.IsNullOrEmpty(ensuredId))
	                        {
	                                current.prefabAssetID = ensuredId;
	                                if (registry != null &&
	                                    CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs &&
	                                    current.RegisterWithSaveSystem &&
	                                    RegistryNeedsDelayedSync(index, prefabAsset, ensuredId))
	                                {
	                                        QueuePrefabRegistrySync(current, prefabAsset, ensuredId);
	                                }
	                        }

	                        // Ensure already invalidates on a correction. This additional path is
	                        // only for a direct serialized edit that was legitimately kept.
	                        if (identitySnapshotChanged &&
	                            string.Equals(previousId, current.prefabAssetID, StringComparison.Ordinal))
	                        {
	                                PrefabRegistryIdentityIndex.NotifyPrefabIdentityChanged(prefabAsset);
	                        }

	                        return ensuredId;
	                }

	                private static bool RegistryNeedsDelayedSync(
	                        PrefabRegistryIdentityIndex index,
	                        GameObject prefabAsset,
	                        string ensuredId)
	                {
	                        if (index == null || prefabAsset == null || string.IsNullOrEmpty(ensuredId))
	                                return false;
	                        if (index.HasConflictingIdsForAsset(prefabAsset))
	                                return false;

	                        IReadOnlyList<PrefabRegistryIdentityRow> rows = index.GetRowsForAsset(prefabAsset);
	                        if (rows.Count == 0)
	                                return true;

	                        for (int i = 0; i < rows.Count; i++)
	                        {
	                                PrefabRegistryIdentityRow row = rows[i];
	                                if (row?.Entry == null)
	                                        continue;

	                                if (!row.IsCanonicalReference ||
	                                    string.IsNullOrEmpty(row.RegistryId) ||
	                                    !string.Equals(row.RegistryId, ensuredId, StringComparison.Ordinal))
	                                {
	                                        return true;
	                                }
	                        }

	                        return false;
	                }

	                private static void QueueUniqueIdRemoval(GameObject go, UniqueID uid)
	                {
	                        if (go == null || uid == null)
	                                return;

	                        s_PendingUniqueIdRemovals.RemoveAll(item => item.UniqueID == null || item.UniqueID == uid);
	                        s_PendingUniqueIdRemovals.Add(new DeferredUniqueIdRemoval(go, uid));

	                        if (s_UniqueIdRemovalFlushQueued)
	                                return;

	                        s_UniqueIdRemovalFlushQueued = true;
	                        EditorApplication.delayCall -= FlushPendingUniqueIdRemovals;
	                        EditorApplication.delayCall += FlushPendingUniqueIdRemovals;
	                }

	                private static void FlushPendingUniqueIdRemovals()
	                {
	                        s_UniqueIdRemovalFlushQueued = false;
	                        var pending = s_PendingUniqueIdRemovals.ToArray();
	                        s_PendingUniqueIdRemovals.Clear();

	                        foreach (var item in pending)
	                        {
	                                if (item.GameObject != null && item.UniqueID != null)
	                                {
	                                        Undo.DestroyObjectImmediate(item.UniqueID);
	                                        Logger.Log($"[Crystal Save] Deferred removal of UniqueID from '{item.GameObject.name}' due to SaveablePrefab + RememberComposite combo.");
	                                }
	                        }
	                }

	                private static void QueuePrefabRegistrySync(SaveablePrefab current, GameObject prefabAsset, string currentId)
	                {
	                        if (current == null || prefabAsset == null || string.IsNullOrEmpty(currentId))
	                                return;

	                        GameObject resolvedAsset = PrefabAssetResolver.ResolveNearestPrefabAsset(prefabAsset) ?? prefabAsset;
	                        string assetPath = AssetDatabase.GetAssetPath(resolvedAsset);
	                        string assetGuid = string.IsNullOrEmpty(assetPath)
	                                ? null
	                                : AssetDatabase.AssetPathToGUID(assetPath);
	                        if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(assetGuid))
	                                return;

	                        s_PendingPrefabRegistrySyncs.RemoveAll(item =>
	                                string.IsNullOrEmpty(item.PrefabAssetGuid) ||
	                                string.Equals(item.PrefabAssetGuid, assetGuid, StringComparison.Ordinal) ||
	                                string.Equals(item.PrefabAssetPath, assetPath, StringComparison.Ordinal));
	                        s_PendingPrefabRegistrySyncs.Add(new DeferredPrefabRegistrySync(
	                                current,
	                                assetPath,
	                                assetGuid));

	                        if (s_PrefabRegistrySyncFlushQueued)
	                                return;

	                        s_PrefabRegistrySyncFlushQueued = true;
	                        EditorApplication.delayCall -= FlushPendingPrefabRegistrySyncs;
	                        EditorApplication.delayCall += FlushPendingPrefabRegistrySyncs;
	                }

	                private static void FlushPendingPrefabRegistrySyncs()
	                {
	                        using (s_PrefabIdentityBatchMarker.Auto())
	                        {
	                                s_PrefabRegistrySyncFlushQueued = false;
	                                var pending = s_PendingPrefabRegistrySyncs.ToArray();
	                                s_PendingPrefabRegistrySyncs.Clear();
	                                if (Application.isPlaying ||
	                                    EditorApplication.isPlayingOrWillChangePlaymode ||
	                                    !CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs)
	                                        return;

	                                PrefabRegistry registry = PrefabAssetResolver.LoadActivePrefabRegistry();
	                                if (registry == null)
	                                        return;
	                                PrefabRegistryEditorDiagnosticCounters.RecordPrefabBatchFlush(registry);

	                                PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.Build(registry);
	                                bool registryChanged = false;
	                                bool prefabChanged = false;
	                                bool registryUndoRecorded = false;

	                                Undo.IncrementCurrentGroup();
	                                int undoGroup = Undo.GetCurrentGroup();
	                                Undo.SetCurrentGroupName("Synchronize Prefab Registry Identities");

	                                void RecordRegistryUndo()
	                                {
	                                        if (registryUndoRecorded) return;
	                                        Undo.RecordObject(registry, "Synchronize Prefab Registry Identities");
	                                        registryUndoRecorded = true;
	                                }

	                                s_ApplyingAutomaticPrefabIdentity = true;
	                                try
	                                {
	                                        foreach (DeferredPrefabRegistrySync item in pending)
	                                        {
	                                                if (!CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs)
	                                                        break;
	                                                if (string.IsNullOrEmpty(item.PrefabAssetGuid))
	                                                        continue;

	                                                string currentPath = AssetDatabase.GUIDToAssetPath(item.PrefabAssetGuid);
	                                                if (string.IsNullOrEmpty(currentPath))
	                                                        currentPath = item.PrefabAssetPath;

	                                                if (string.IsNullOrEmpty(currentPath) ||
	                                                    !string.Equals(
	                                                            AssetDatabase.AssetPathToGUID(currentPath),
	                                                            item.PrefabAssetGuid,
	                                                            StringComparison.Ordinal))
	                                                        continue;

	                                                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(currentPath);
	                                                if (prefabAsset == null ||
	                                                    !EditorUtility.IsPersistent(prefabAsset) ||
	                                                    PrefabUtility.GetPrefabAssetType(prefabAsset) == PrefabAssetType.NotAPrefab)
	                                                        continue;

	                                                SaveablePrefab assetComponent = prefabAsset.GetComponent<SaveablePrefab>();
	                                                if (assetComponent == null || !assetComponent.RegisterWithSaveSystem)
	                                                        continue;

	                                                if (item.Current != null)
	                                                {
	                                                        if (!item.Current.RegisterWithSaveSystem)
	                                                                continue;

	                                                        GameObject currentAsset =
	                                                                PrefabAssetResolver.ResolveNearestPrefabAsset(item.Current.gameObject);
	                                                        string currentAssetPath = currentAsset != null
	                                                                ? AssetDatabase.GetAssetPath(currentAsset)
	                                                                : null;
	                                                        string currentAssetGuid = string.IsNullOrEmpty(currentAssetPath)
	                                                                ? null
	                                                                : AssetDatabase.AssetPathToGUID(currentAssetPath);
	                                                        if (!string.Equals(
	                                                                currentAssetGuid,
	                                                                item.PrefabAssetGuid,
	                                                                StringComparison.Ordinal))
	                                                        {
	                                                                continue;
	                                                        }
	                                                }

	                                                if (index.HasConflictingIdsForAsset(prefabAsset))
	                                                {
	                                                        Logger.Log(
	                                                                $"[Crystal Save] Skipped delayed PrefabRegistry sync for '{prefabAsset.name}' " +
	                                                                $"(path: {currentPath}, GUID: {item.PrefabAssetGuid}) because multiple registry rows assign different IDs to that prefab. Run Clean Duplicates first.",
	                                                                LogLevel.Warning);
	                                                        continue;
	                                                }

	                                                string previousAssetId = assetComponent.PrefabAssetID;
	                                                string ensuredId = PrefabAssetResolver.EnsurePrefabAssetIDForAsset(
	                                                        assetComponent,
	                                                        prefabAsset,
	                                                        index);
	                                                if (string.IsNullOrEmpty(ensuredId))
	                                                        continue;

	                                                if (!string.Equals(previousAssetId, ensuredId, StringComparison.Ordinal))
	                                                        prefabChanged = true;

	                                                if (item.Current != null &&
	                                                    item.Current != assetComponent &&
	                                                    !string.Equals(
	                                                            item.Current.PrefabAssetID,
	                                                            ensuredId,
	                                                            StringComparison.Ordinal))
	                                                {
	                                                        Undo.RecordObject(
	                                                                item.Current,
	                                                                "Synchronize Prefab Asset ID");
	                                                        SetPrefabAssetIDForEditor(item.Current, ensuredId);
	                                                        prefabChanged = true;
	                                                }

	                                                var rows = new List<PrefabRegistryIdentityRow>(
	                                                        index.GetRowsForAsset(prefabAsset));
	                                                if (rows.Count == 0)
	                                                {
	                                                        if (index.IsIdOwnedByDifferentAsset(ensuredId, prefabAsset))
	                                                        {
	                                                                index.TryGetIdOwner(
	                                                                        ensuredId,
	                                                                        out PrefabRegistryIdentityRow owner);
	                                                                Logger.Log(
	                                                                        $"[Crystal Save] Refused delayed PrefabRegistry sync for '{prefabAsset.name}' " +
	                                                                        $"(path: {currentPath}, GUID: {item.PrefabAssetGuid}) because PrefabAssetID '{ensuredId}' " +
	                                                                        $"is still owned by {DescribeRegistryPrefab(owner?.ResolvedAsset)}.",
	                                                                        LogLevel.Warning);
	                                                                continue;
	                                                        }

	                                                        RecordRegistryUndo();
	                                                        var addedEntry = new PrefabRegistry.PrefabEntry
	                                                        {
	                                                                uniqueID = ensuredId,
	                                                                prefab = prefabAsset,
	                                                                poolSize = 0,
	                                                                disablePooling = false
	                                                        };
	                                                        registry.prefabEntries.Add(addedEntry);
	                                                        index.SynchronizeMutation(addedEntry, prefabAsset);
	                                                        registryChanged = true;
	                                                        continue;
	                                                }

	                                                for (int i = 0; i < rows.Count; i++)
	                                                {
	                                                        PrefabRegistryIdentityRow row = rows[i];
	                                                        PrefabRegistry.PrefabEntry entry = row?.Entry;
	                                                        if (entry == null)
	                                                                continue;

	                                                        bool canonicalChanged =
	                                                                row.ResolvedAsset != null &&
	                                                                entry.prefab != row.ResolvedAsset;
	                                                        bool idChanged = !string.Equals(
	                                                                entry.uniqueID,
	                                                                ensuredId,
	                                                                StringComparison.Ordinal);
	                                                        if (!canonicalChanged && !idChanged)
	                                                                continue;

	                                                        RecordRegistryUndo();
	                                                        if (canonicalChanged)
	                                                                entry.prefab = row.ResolvedAsset;
	                                                        if (idChanged)
	                                                                entry.uniqueID = ensuredId;
	                                                        index.SynchronizeMutation(entry, row.ResolvedAsset);
	                                                        registryChanged = true;
	                                                }
	                                        }
	                                }
	                                finally
	                                {
	                                        s_ApplyingAutomaticPrefabIdentity = false;
	                                        Undo.CollapseUndoOperations(undoGroup);
	                                }

	                                if (registryChanged)
	                                {
	                                        registry.RebuildLookup();
	                                        EditorUtility.SetDirty(registry);
	                                        PrefabRegistryIdentityIndex.NotifyRegistryMutated(registry);
	                                }

	                                if (registryChanged || prefabChanged)
	                                {
	                                        PrefabRegistryIdentityIndex.NotifyAutomaticSaveRequested(registry);
	                                        PrefabRegistryEditorDiagnosticCounters.RecordPrefabBatchSaveRequest(registry);
	                                        s_PrefabRegistryBatchSaveCount++;
	                                        AssetDatabase.SaveAssets();
	                                }
	                        }
	                }

	                private static string DescribeRegistryPrefab(GameObject prefab)
	                {
	                        if (prefab == null) return "<missing prefab>";

	                        GameObject asset = PrefabAssetResolver.ResolveNearestPrefabAsset(prefab) ?? prefab;
	                        string path = AssetDatabase.GetAssetPath(asset);
	                        string guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
	                        return $"'{asset.name}' (path: {(string.IsNullOrEmpty(path) ? "<none>" : path)}, GUID: {(string.IsNullOrEmpty(guid) ? "<none>" : guid)})";
	                }

	                /// <summary>
	                /// Resolves the actual prefab asset from an instance. Handles Prefab Mode staging objects,
                /// scene instances, and direct asset references.
                /// </summary>
                /// <param name="instance">The GameObject instance (may be a Prefab Mode staging object)</param>
                /// <returns>The persistent prefab asset, or null if not found</returns>
                private static GameObject GetPrefabAssetFromInstance(GameObject instance)
                {
                        return PrefabAssetResolver.ResolveNearestPrefabAsset(instance);
                }

                private static void RestorePrefabAssetIDFromRegistry(SaveablePrefab current, GameObject prefabAsset, string registryId)
                {
                        if (string.IsNullOrEmpty(registryId)) return;

                        SetPrefabAssetIDForEditor(current, registryId);

                        SaveablePrefab assetComponent = prefabAsset != null
                                ? prefabAsset.GetComponent<SaveablePrefab>()
                                : null;

                        if (assetComponent != null && assetComponent != current)
                                SetPrefabAssetIDForEditor(assetComponent, registryId);
                }

                private static void SetPrefabAssetIDForEditor(SaveablePrefab saveablePrefab, string prefabAssetId)
                {
                        if (saveablePrefab == null || string.IsNullOrEmpty(prefabAssetId))
                                return;
                        if (string.Equals(saveablePrefab.prefabAssetID, prefabAssetId, StringComparison.Ordinal))
                                return;

                        var serialized = new SerializedObject(saveablePrefab);
                        SerializedProperty idProperty = serialized.FindProperty("prefabAssetID");
                        if (idProperty == null) return;

                        idProperty.stringValue = prefabAssetId;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(saveablePrefab);
                }
#endif
		
               private void OnDisable()
               {
#if UNITY_EDITOR
                       if (!Application.isPlaying)
                       {
                               // If SaveablePrefab is being removed but RememberComposite remains
                               var composite = GetComponent<RememberComposite>();
                               var uid = GetComponent<UniqueID>();

                               // Note: UniqueID components are no longer automatically created
                               // Users must manually add UniqueID components if needed for RememberComposite
                       }
#endif
                       RemoveFromCaches();
               }

               private void OnDestroy()
               {
                       SaveablePrefabFactory.CancelPendingDestroy(this);

#if UNITY_EDITOR
                       if (!Application.isPlaying)
                       {
                                // If this SaveablePrefab is being removed but RememberComposite remains
                                var composite = GetComponent<RememberComposite>();
                                var uniqueID = GetComponent<UniqueID>();

                                // Note: UniqueID components are no longer automatically created
                                // Users must manually add UniqueID components if needed for RememberComposite
                       }
#endif

                                               if (Application.isPlaying &&
                                                       SaveManager.Instance != null &&
                                                       !SaveManager.Instance.IsLoading &&
                                                       propertySettings != null &&
                                                       propertySettings.RememberDestroyed)
                                               {
                                                       var manager = SaveManager.Instance;
                                                       if (manager.IsInSceneTransition)
                                                       {
                                                               Logger.Log(
                                                                       $"[SaveablePrefab.OnDestroy] Skip destroyed registration for '{name}' (ID: {uniqueID ?? "<null>"}) during scene transition.",
                                                                       gameObject,
                                                                       LogCategory.SaveablePrefab,
                                                                       LogLevel.Info);
                                                       }
                                                       else
                                                       {
                                                               var tracker = manager.GameObjectTracker;
                                                               bool alreadyDestroyed = tracker != null && tracker.IsGameObjectDestroyed(uniqueID);
                                                               if (!alreadyDestroyed)
                                                               {
                                                                       bool registered =
                                                                               manager.TryRegisterDestroyedGameObjectFromOwner(
                                                                                       uniqueID,
                                                                                       gameObject);
                                                                       if (registered &&
                                                                           Logger.ShouldLog(
                                                                                   LogCategory.SaveablePrefab,
                                                                                   LogLevel.Info))
                                                                       {
                                                                               Logger.Log(
                                                                                       $"[SaveablePrefab.OnDestroy] Registered '{name}' (ID: {uniqueID ?? "<null>"}) as destroyed.",
                                                                                       gameObject,
                                                                                       LogCategory.SaveablePrefab,
                                                                                       LogLevel.Info);
                                                                       }
                                                               }
                                                               else
                                                               {
                                                                       Logger.Log(
                                                                               $"[SaveablePrefab.OnDestroy] '{name}' (ID: {uniqueID ?? "<null>"}) already marked destroyed; skipping duplicate registration.",
                                                                               gameObject,
                                                                               LogCategory.SaveablePrefab,
                                                                               LogLevel.Info);
                                                               }
                                                       }
                                               }

                       /* 1 ─ Cleanly detach from the save-system without spamming PrefabManager */
                       if (isRegisteredWithSaveManager && SaveManager.Instance != null)
                       {
                                SaveManager.Instance.SoftUnregisterGameObject(gameObject);
                                isRegisteredWithSaveManager = false;    // mark clean
                       }

                       /* 2 ─ Notify listeners (unchanged) */
                       OnPrefabDestroyed?.Invoke(this);

                       // Ensure caches are cleaned up
                       RemoveFromCaches();
               }

		/* ─────────────────────────────────────────────────────────────── */

		private void EnsureUniqueID()
		{
			if (!string.IsNullOrEmpty(uniqueID)) return;

#if UNITY_EDITOR
                        if (ShouldSkipEditorInstanceIDGeneration())
                        {
                                return;
                        }
#endif

			var state = SaveManager.Instance?.StateMachine?.CurrentState;
			if (state == SaveState.Loading || isLoading) return;   // ID will be injected later

			if (TryGetComponent(out UniqueID idComp) &&
				!string.IsNullOrEmpty(idComp.ID))
			{
				uniqueID = idComp.ID;
			}
			else
			{
				uniqueID = Guid.NewGuid().ToString();
			}
		}

                /// <summary>Ensures that any existing <see cref="UniqueID"/> component 
		/// mirrors <c>uniqueID</c>. Does not auto-create UniqueID components.</summary>
                private void EnsureUniqueIDComponent()
                {
                        var uid = GetComponent<UniqueID>();

                        // Only sync existing UniqueID components, don't auto-create new ones
                        if (uid == null)
                                return;

                        if (!string.IsNullOrEmpty(uniqueID) && uid.ID != uniqueID)
                                uid.ID = uniqueID;

#if UNITY_EDITOR
                        if (!Application.isPlaying)
                                uid.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
#endif
                }

#if UNITY_EDITOR
                private bool ShouldSkipEditorInstanceIDGeneration()
                {
                        if (Application.isPlaying)
                                return false;

                        if (!gameObject.scene.IsValid())
                                return true;

                        if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                                return true;

                        if (IsInPrefabMode())
                                return true;

                        return false;
                }

                /// <summary>
                /// Check if the current GameObject is being edited in prefab mode.
                /// </summary>
                private bool IsInPrefabMode()
                {
                        try
                        {
                                var prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
                                if (prefabStage != null)
                                {
                                        return prefabStage.IsPartOfPrefabContents(gameObject);
                                }

                                prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                                if (prefabStage != null)
                                {
                                        // Check if this GameObject is part of the prefab being edited
                                        // Note: Unity doesn't allow accessing prefabContentsRoot during Awake/OnEnable
                                        // so we wrap this in try-catch to handle InvalidOperationException gracefully
                                        return prefabStage.IsPartOfPrefabContents(gameObject);
                                }
                        }
                        catch (System.InvalidOperationException)
                        {
                                // Unity throws this when accessing prefabContentsRoot from Awake/OnEnable/early OnValidate
                                // If detection fails, err on the side of assuming prefab mode to avoid writing IDs to assets.
                                return true;
                        }
                        // If the object has no valid scene (e.g., prefab asset selected in Project), treat it as prefab context.
                        if (!gameObject.scene.IsValid())
                                return true;

                        return false;
                }
#endif

			private void HandleDontDestroy()
			{
				if (keepAcrossScenes)
					PersistentManager.MakePersistent(gameObject, true);
			}

			private void CaptureRuntimeOriginSceneName()
			{
				if (!string.IsNullOrEmpty(runtimeOriginSceneName))
					return;

				var scene = gameObject.scene;
				runtimeOriginSceneName = scene.IsValid() ? scene.name : string.Empty;
			}
	
			private void DetectRuntimeInstantiation()
			{
				if (string.IsNullOrEmpty(prefabAssetID))
					isAddedAtRuntime = true;
		}

		public void MarkAsAddedAtRuntime() => isAddedAtRuntime = true;
		public void SetOriginalPrefabAsset(GameObject asset) => originalPrefabAsset = asset;

		/* =======================================================================
		 * Called by PrefabManager _after_ instantiation when the real GUID is
		 * known (coming from the save file).  Also triggers registration if we
		 * skipped it in Awake().
		 * =====================================================================*/
		public void SetUniqueID(string newUniqueID)
                {
                        SetUniqueIDCore(
                                newUniqueID,
                                componentBuffer: null,
                                updatePrefabManagerCollections: true);
                }

                /// <summary>
                /// Restore-only counterpart to <see cref="SetUniqueID"/> that reuses a
                /// caller-owned component buffer. A successfully acquired buffer is
                /// always empty on return.
                /// </summary>
                internal void SetUniqueIDForRestore(
                        string newUniqueID,
                        List<SaveableComponent> componentBuffer)
                {
                        if (componentBuffer == null)
                        {
                                SetUniqueIDCore(
                                        newUniqueID,
                                        componentBuffer: null,
                                        updatePrefabManagerCollections: false);
                                return;
                        }

                        bool ownsBuffer;
                        lock (s_RestoreUniqueIdBuffersInUse)
                        {
                                ownsBuffer = s_RestoreUniqueIdBuffersInUse.Add(componentBuffer);
                        }

                        if (!ownsBuffer)
                        {
                                // The same buffer is active higher in the call stack.
                                // Allocate for this rare nested path so the outer
                                // enumeration remains intact.
                                SetUniqueIDCore(
                                        newUniqueID,
                                        componentBuffer: null,
                                        updatePrefabManagerCollections: false);
                                return;
                        }

                        try
                        {
                                componentBuffer.Clear();
                                SetUniqueIDCore(
                                        newUniqueID,
                                        componentBuffer,
                                        updatePrefabManagerCollections: false);
                        }
                        finally
                        {
                                componentBuffer.Clear();
                                lock (s_RestoreUniqueIdBuffersInUse)
                                {
                                        s_RestoreUniqueIdBuffersInUse.Remove(componentBuffer);
                                }
                        }
                }

                private void SetUniqueIDCore(
                        string newUniqueID,
                        List<SaveableComponent> componentBuffer,
                        bool updatePrefabManagerCollections)
		{
                       if (string.IsNullOrEmpty(newUniqueID)) return;


                       // Store old ID before changing it
                       string oldUniqueID = uniqueID;
                       uniqueID = newUniqueID;
                       CacheSelfIfEnabled();

                        // keep any existing UniqueID component in sync (don't auto-create)
                        var uid = GetComponent<UniqueID>();
                        if (uid != null)
                                uid.ID = newUniqueID;

			//  ────────────────────────────────────────────────────────────

		// ──────────────────────────────────────────────────────────────
		// Update PrefabManager's instantiatedPrefabs dictionary when the ID changes
		// This ensures duplicated prefabs with new IDs can be properly restored
		// ──────────────────────────────────────────────────────────────
		var prefabManager = SaveManager.Instance?.GetPrefabManager;
		if (updatePrefabManagerCollections &&
		    prefabManager != null &&
		    !string.IsNullOrEmpty(oldUniqueID) &&
		    oldUniqueID != newUniqueID)
		{
			prefabManager.UpdatePrefabUniqueID(this, oldUniqueID, newUniqueID);
		}

		if (SaveManager.Instance?.GameObjectTracker != null &&
		    !string.IsNullOrEmpty(oldUniqueID) &&
		    oldUniqueID != newUniqueID)
		{
			if (settings == null)
			{
				var remember = GetComponent<RememberGameObject>();
				settings = remember ? remember.PropertySettings : propertySettings;
				settings ??= new GameObjectPropertySettings { RememberActive = true };
			}

			SaveManager.Instance.GameObjectTracker.RebindTrackedGameObjectID(gameObject, oldUniqueID, newUniqueID, settings);
		}
			// Tell every SaveableComponent (on this prefab & its children) that
			// the GameObject-part of the composite key has changed, so they can
			// re-register in ComponentManager with the correct identifier.
				var components = componentBuffer ?? new List<SaveableComponent>();
				GetComponentsInChildren(true, components);
				foreach (var comp in components)
				{
					comp.OverrideGameObjectID(newUniqueID);
				// make absolutely sure the component is registered under the new key
				//if (!comp.enabled)                       // alt. use a public getter on _isRegistered
					//ComponentManager.Instance?.RegisterSaveableComponent(comp);
			}
			// ───────────────────────────────────────────────────────────────────

			// late registration – only if we aren’t already registered
			if (RegisterWithSaveSystem && !isRegisteredWithSaveManager)
			{
                                if (settings == null)
                                {
                                        var remember = GetComponent<RememberGameObject>();
                                        settings = remember ? remember.PropertySettings : propertySettings;
                                        settings ??= new GameObjectPropertySettings { RememberActive = true };
                                }

				RegisterForSaving();
			}
		}

	}
}
#endif
