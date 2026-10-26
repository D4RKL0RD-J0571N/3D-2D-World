#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace Arawn.CrystalSave.Runtime
{
    [DefaultExecutionOrder(-30)]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class PrefabManager : MonoBehaviour 
    {
        #region Fields and Properties

        [SerializeField]
        [Tooltip("Reference to the Prefab Registry asset.")]
        private PrefabRegistry prefabRegistry;

        [SerializeField]
        [Tooltip("List of registered SaveablePrefabs in the scene.")]
        private List<SaveablePrefab> saveablePrefabs = new List<SaveablePrefab>();
        private readonly HashSet<SaveablePrefab> registeredPrefabInstances = new HashSet<SaveablePrefab>();

        // Dictionary to keep track of instantiated prefabs by their unique IDs.
        private readonly Dictionary<string, GameObject> instantiatedPrefabs = new Dictionary<string, GameObject>();
        private SaveManager owner;
        private SaveManager Owner => owner ?? SaveManager.Instance;
        private System.Collections.ObjectModel.ReadOnlyCollection<SaveablePrefab> registeredPrefabView;

        internal void BindOwner(SaveManager manager)
        {
            if (ReferenceEquals(manager, null)) throw new ArgumentNullException(nameof(manager));
            if (manager.gameObject != gameObject)
                throw new InvalidOperationException("PrefabManager must share its SaveManager's host.");
            if (owner != null && !ReferenceEquals(owner, manager))
                throw new InvalidOperationException("PrefabManager already belongs to another SaveManager.");
            // A destroyed SaveManager component can be replaced on this same host.
            // Keep surviving registrations, but retire its queued/restoring snapshot.
            if (!ReferenceEquals(owner, null) && !ReferenceEquals(owner, manager))
                BeginFullLoadGeneration();
            owner = manager;
        }

        private readonly PrefabRestoreQueue restoreQueue = new();
        private readonly PrefabRestoreLifetime restoreLifetime = new();
	        private const string SaveFlowDiagPrefix = "[CrystalSave][SaveFlowDiag]";
	        private static readonly ProfilerMarker BuildLoadContextMarker =
		        new ProfilerMarker("CrystalSave.PrefabLoad.BuildContext");


	        internal sealed class PreparedPrefabBatch
	        {
		        internal PreparedPrefabBatch(List<SaveablePrefabData> immediate, List<SaveablePrefabData> deferred)
		        {
			        Immediate = immediate ?? new List<SaveablePrefabData>();
			        Deferred = deferred ?? new List<SaveablePrefabData>();
		        }

		        internal List<SaveablePrefabData> Immediate { get; }
		        internal List<SaveablePrefabData> Deferred { get; }
	        }

        internal PrefabLoadContext CreateLoadContext(IEnumerable<string> destroyedIds, SaveablePrefab[] sceneSnapshot)
        {
            registeredPrefabInstances.Clear();
            var context = new PrefabLoadContext(destroyedIds, sceneSnapshot, instantiatedPrefabs,
                saveablePrefabs, Owner?.GetTrackedGameObjects(), RemoveLoadRegistration);
            registeredPrefabInstances.UnionWith(context.RegisteredInstances);
            return context;
        }

        private void RemoveLoadRegistration(SaveablePrefab prefab, bool collectionsWillBeRebuilt)
        {
            registeredPrefabInstances.Remove(prefab);
            if (collectionsWillBeRebuilt) return;
            PrefabLoadDiagnosticCounters.RecordLegacyRegistrationCollectionScan();
            saveablePrefabs.Remove(prefab);
        }

        // Per-prefab locks to serialize access when temporarily flagging the asset as loading.
        private static readonly ConcurrentDictionary<GameObject, object> prefabLocks = new();

        private bool UsePrefabPooling =>
                Owner?.SaveSettings?.usePrefabPooling ?? false;

        private int DefaultPoolSize =>
                Owner?.SaveSettings?.defaultPrefabPoolSize ?? 0;

        private bool ShouldEmitDetailedPrefabDiagnostics
        {
                get
                {
                        if (!Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Info))
                                return false;

                        SaveManager manager = Owner;
                        bool isLoading =
                                manager != null &&
                                manager.StateMachine != null &&
                                manager.StateMachine.CurrentState == SaveState.Loading;
                        return !isLoading ||
                               manager.SaveSettings?.enableVerbosePrefabLoadDiagnostics == true;
                }
        }

        // Retained for source and behavioral compatibility. Product readers use
        // RegisteredPrefabs; mutations belong to the registration methods below.
        public List<SaveablePrefab> SaveablePrefabs => saveablePrefabs;

        /// <summary>A live read-only view of the registered serialization owners.</summary>
        public IReadOnlyList<SaveablePrefab> RegisteredPrefabs =>
            registeredPrefabView ??= saveablePrefabs.AsReadOnly();

        /// <summary>
        /// Indicates whether all prefabs have been initialized.
        /// </summary>
        public bool AllPrefabsInitialized { get; private set; } = false;

        /// <summary>
        /// Event triggered when all prefabs have been initialized.
        /// </summary>
        public event Action OnAllPrefabsInitialized;

        /// <summary>
        /// Event raised after the high-priority (immediate) prefab batch has fully restored.
        /// </summary>
        public event Action OnImmediatePrefabBatchComplete;

        /// <summary>
        /// Event raised whenever prefabs are queued for deferred instantiation.
        /// </summary>
        public event Action<IReadOnlyList<SaveablePrefabData>> OnDeferredPrefabsQueued;

                private int initializedPrefabs = 0;

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            owner = GetComponent<SaveManager>();
            LoadPrefabRegistry();

            if (prefabRegistry == null)
            {
                Logger.Log("PrefabManager: PrefabRegistry is not assigned. Please complete the Settings Wizard via Tools > Crystal Save > Settings Wizard, or import demo settings via Tools > Crystal Save > Settings > Install Demo Settings.", LogCategory.PrefabManager, LogLevel.Error);
                enabled = false;
                return;
            }

            // Ensure prefabs that start inactive are still tracked so they can be
            // saved and restored correctly.
            RegisterExistingInactivePrefabs();

        }
        
        private void OnEnable()
        {
            SaveablePrefab.OnPrefabInstantiated += OnPrefabInitialized;
            SaveablePrefab.OnPrefabInstantiated += RegisterPrefabFromLifecycle;
            SaveablePrefab.OnPrefabDestroyed += UnregisterPrefab;
        }

        private void OnDisable()
        {
            SaveablePrefab.OnPrefabInstantiated -= OnPrefabInitialized;
            SaveablePrefab.OnPrefabInstantiated -= RegisterPrefabFromLifecycle;
            SaveablePrefab.OnPrefabDestroyed -= UnregisterPrefab;
        }

        private void OnDestroy()
        {
            // Ensure we unsubscribe from static events in case OnDisable wasn't called
            SaveablePrefab.OnPrefabInstantiated -= OnPrefabInitialized;
            SaveablePrefab.OnPrefabInstantiated -= RegisterPrefabFromLifecycle;
            SaveablePrefab.OnPrefabDestroyed -= UnregisterPrefab;
            restoreLifetime.Retire();
        }

        private void LateUpdate()
        {
            if (!restoreQueue.HasPending)
                return;

            var manager = Owner;
            if (manager == null)
                return;

            string[] pendingSceneNames = restoreQueue.SnapshotPendingScenes();
            if (pendingSceneNames.Length == 0)
                return;

            List<string> destroyedIDs = manager.GetDestroyedGameObjectIDs();
            foreach (string sceneName in pendingSceneNames)
            {
                if (string.IsNullOrEmpty(sceneName))
                    continue;

                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                ProcessPendingPrefabs(sceneName, destroyedIDs);
            }
        }

        #endregion

        #region Public Accessor Methods

        /// <summary>
        /// Returns the list of SaveablePrefabs.
        /// </summary>
        public List<SaveablePrefab> GetSaveablePrefabs()
        {
            return new List<SaveablePrefab>(saveablePrefabs);
        }

        /// <summary>
        /// Returns the dictionary of instantiated prefabs.
        /// </summary>
        public Dictionary<string, GameObject> GetInstantiatedPrefabs()
        {
            return new Dictionary<string, GameObject>(instantiatedPrefabs);
        }

        /// <summary>
        /// Resolves a live, owner-valid prefab mapping without allocating a dictionary snapshot.
        /// Stale Unity-null entries and mappings whose prefab identity no longer owns the key are pruned.
        /// </summary>
        internal bool TryGetInstantiatedPrefab(string uniqueID, out GameObject instance)
        {
            instance = null;
            if (string.IsNullOrEmpty(uniqueID) ||
                !instantiatedPrefabs.TryGetValue(uniqueID, out GameObject mapped))
            {
                return false;
            }

            if (mapped == null)
            {
                instantiatedPrefabs.Remove(uniqueID);
                return false;
            }

            SaveablePrefab saveablePrefab;
            if (SaveablePrefab.TryGetCachedSaveablePrefab(
                    mapped,
                    out SaveablePrefab cached) &&
                cached != null)
            {
                saveablePrefab = cached;
            }
            else
            {
                mapped.TryGetComponent(out saveablePrefab);
            }

            if (saveablePrefab == null ||
                !string.Equals(saveablePrefab.UniqueID, uniqueID, StringComparison.Ordinal))
            {
                instantiatedPrefabs.Remove(uniqueID);
                return false;
            }

            instance = mapped;
            return true;
        }

        /// <summary>
        /// Indicates whether all prefabs have been initialized.
        /// </summary>
        public bool AreAllPrefabsInitialized => AllPrefabsInitialized;

        #endregion

        #region Prefab Registry Management

                private void LoadPrefabRegistry()
                {
                        // Attempt to load PrefabRegistry via AssetProvider.
                        prefabRegistry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");

            if (prefabRegistry == null)
            {
                Logger.Log("PrefabManager: Failed to load PrefabRegistry. Please complete the Settings Wizard via Tools > Crystal Save > Settings Wizard, or import demo settings via Tools > Crystal Save > Settings > Install Demo Settings.", LogCategory.PrefabManager, LogLevel.Error);
            }
        }

        #endregion

	        /// <summary>
	        /// Registers any <see cref="SaveablePrefab"/> instances that exist in the scene
	        /// but start inactive, ensuring they are tracked by the PrefabManager and,
	        /// when configured for automatic registration, the SaveManager.
	        /// </summary>
        private void RegisterExistingInactivePrefabs()
        {
            var prefabs = UnityObjectSearch.FindIncludingInactive<SaveablePrefab>();
            foreach (var prefab in prefabs)
            {
                if (!IsSaveSystemParticipant(prefab))
                    continue;

                if (prefab.gameObject.activeInHierarchy)
                    continue;

                // Ensure inactive prefabs that never ran Awake still get a UniqueID
                prefab.InitializeInstance();

	                RegisterPrefab(prefab);
	                if (prefab.RegisterWithSaveSystem)
	                    prefab.RegisterForSaving();
	                initializedPrefabs++;
            }

            if (initializedPrefabs >= saveablePrefabs.Count && !AllPrefabsInitialized)
            {
                AllPrefabsInitialized = true;
                OnAllPrefabsInitialized?.Invoke();
            }
        }

        #region Prefab Registration

        // Registration selection stays at the call site: public registration uses
        // first-ID ownership, while a restore batch has already resolved that owner.
        // These operations keep list membership and its batch index in step.
        private void AddRegistration(SaveablePrefab prefab)
        {
            saveablePrefabs.Add(prefab);
            registeredPrefabInstances.Add(prefab);
        }

        private void EnsureRegistration(SaveablePrefab prefab)
        {
            if (!saveablePrefabs.Contains(prefab))
                saveablePrefabs.Add(prefab);
            registeredPrefabInstances.Add(prefab);
        }

        private bool RemoveRegistration(SaveablePrefab prefab)
        {
            registeredPrefabInstances.Remove(prefab);
            return saveablePrefabs.Remove(prefab);
        }

        // Only a context that has reconciled legacy list mutations may use the
        // membership index as authority. Do not add a list scan to the batch path.
        private void AddIndexedRegistration(SaveablePrefab prefab)
        {
            if (registeredPrefabInstances.Add(prefab))
                saveablePrefabs.Add(prefab);
        }

        private void RemoveIndexedRegistration(SaveablePrefab prefab)
        {
            if (registeredPrefabInstances.Remove(prefab))
                saveablePrefabs.Remove(prefab);
        }

        private void ReplaceRegistrations(List<SaveablePrefab> preservedPrefabs,
            Dictionary<string, GameObject> preservedInstances)
        {
            saveablePrefabs.Clear();
            saveablePrefabs.AddRange(preservedPrefabs);
            registeredPrefabInstances.Clear();
            foreach (var prefab in preservedPrefabs)
                if (prefab != null) registeredPrefabInstances.Add(prefab);
            instantiatedPrefabs.Clear();
            foreach (var pair in preservedInstances)
                instantiatedPrefabs[pair.Key] = pair.Value;
        }



        private void OnPrefabInitialized(SaveablePrefab prefab)
        {
            initializedPrefabs++;
            if (ShouldEmitDetailedPrefabDiagnostics)
                Logger.Log($"PrefabManager: Prefab '{prefab.gameObject.name}' with ID '{prefab.UniqueID}' fully initialized.", LogCategory.PrefabManager, LogLevel.Info);

            if (initializedPrefabs >= saveablePrefabs.Count && !AllPrefabsInitialized)
            {
                AllPrefabsInitialized = true;
                if (ShouldEmitDetailedPrefabDiagnostics)
                    Logger.Log("PrefabManager: All prefabs have been initialized.", LogCategory.PrefabManager, LogLevel.Info);
                OnAllPrefabsInitialized?.Invoke();
            }
        }

        private void RegisterPrefabFromLifecycle(SaveablePrefab prefab)
        {
            // Prefab assets with a legacy serialized InstanceID raise their lifecycle
            // event during Object.Instantiate, before the batch can inject the saved
            // identity. Keep that event away from the public List-based registration
            // path; RegisterRestoredPrefab owns batch registration in O(1).
            SaveManager manager = Owner;
            bool loading =
                prefab != null &&
                (prefab.IsLoading ||
                 (manager?.StateMachine != null &&
                  manager.StateMachine.CurrentState == SaveState.Loading));
            if (loading)
                return;

            RegisterPrefab(prefab);
        }

                public void RegisterPrefab(SaveablePrefab prefab)
                {
                        if (prefab == null)
                        {
                                Logger.Log("PrefabManager: Attempted to register a null prefab.", LogCategory.PrefabManager, LogLevel.Warning);
                                return;
                        }

                        if (string.IsNullOrEmpty(prefab.UniqueID))
                        {
                                return;
                        }

                        if (!IsSaveSystemParticipant(prefab))
                        {
                                RemoveRegistration(prefab);
                                RemovePrefabMappingIfOwned(prefab.UniqueID, prefab.gameObject);
                                return;
                        }

	                        bool isLoading = Owner != null &&
	                                        Owner.StateMachine.CurrentState == SaveState.Loading;
	                        // Guard against any stray nulls in the tracking list to avoid NullReferenceExceptions
	                        PrefabLoadDiagnosticCounters.RecordLegacyRegistrationCollectionScan();
	                        bool alreadyRegistered = saveablePrefabs.Any(p => p != null && p.UniqueID == prefab.UniqueID);

	                        bool emitDetailedRegistration = ShouldEmitDetailedPrefabDiagnostics;
	                        if (emitDetailedRegistration)
	                        {
		                        Logger.Log($"{SaveFlowDiagPrefix} RegisterPrefab request: {DescribePrefab(prefab)}, alreadyRegistered={alreadyRegistered}, isLoading={isLoading}.",
			                        LogCategory.PrefabManager,
			                        LogLevel.Info);
	                        }

	                        if (!alreadyRegistered)
	                        {
	                                AddRegistration(prefab);
                            }
	                        else
	                        {
	                                PrefabLoadDiagnosticCounters.RecordLegacyRegistrationCollectionScan();
	                                var existingRegistered = saveablePrefabs.FirstOrDefault(p => p != null && p.UniqueID == prefab.UniqueID);
                                    if (existingRegistered == prefab)
                                        registeredPrefabInstances.Add(prefab);
	                                if (existingRegistered != null && existingRegistered != prefab)
	                                {
		                                        if (emitDetailedRegistration)
		                                        {
			                                        Logger.Log(
				                                        $"{SaveFlowDiagPrefix} RegisterPrefab duplicate UniqueID '{prefab.UniqueID}'. Existing={DescribePrefab(existingRegistered)} Incoming={DescribePrefab(prefab)}",
				                                        LogCategory.PrefabManager,
				                                        LogLevel.Warning);
		                                        }
		                                        else if (Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Warning))
		                                        {
			                                        Logger.Log(
				                                        $"PrefabManager: Duplicate UniqueID '{prefab.UniqueID}' encountered; the first registration remains the serialization owner.",
				                                        LogCategory.PrefabManager,
				                                        LogLevel.Warning);
		                                        }
		                                }

		                                if (!isLoading && Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Info))
		                                {
			                                Logger.Log($"PrefabManager: Prefab '{prefab.gameObject.name}' with ID '{prefab.UniqueID}' is already registered.",
				                                LogCategory.PrefabManager,
				                                LogLevel.Info);
		                                }
                        }

            if (!string.IsNullOrEmpty(prefab.UniqueID))
            {
                if (instantiatedPrefabs.TryGetValue(prefab.UniqueID, out var existing) && existing != null && existing != prefab.gameObject)
                {
                    if (ShouldEmitDetailedPrefabDiagnostics)
                    {
                        Logger.Log($"PrefabManager: UniqueID '{prefab.UniqueID}' was mapped to '{existing.name}'. Reassigning to '{prefab.gameObject.name}'.",
                               LogCategory.PrefabManager,
                               LogLevel.Info);
                    }
                }

                instantiatedPrefabs[prefab.UniqueID] = prefab.gameObject;
            }
                }

        public void UnregisterPrefab(SaveablePrefab prefab)
        {
            /* ────────────── basic sanity checks ────────────── */
            if (prefab == null)
            {
                Logger.Log("PrefabManager: Attempted to unregister a null prefab.", LogCategory.PrefabManager, LogLevel.Warning);
                return;
            }

            /* ──────────────────────────────────────────────────
             * Ignore helper/temp prefabs:
             *  • they carry an empty UniqueID
             *  • they were never registered in the first place
             * ────────────────────────────────────────────────── */
            if (string.IsNullOrEmpty(prefab.UniqueID))
            {
                return;
            }

            /* ────────────── normal removal path ────────────── */
            if (RemoveRegistration(prefab))
            {
                if (Logger.ShouldLog(LogCategory.PrefabManager))
                {
                    Logger.Log($"PrefabManager: Unregistered prefab '{Logger.GetUnityObjectName(prefab.gameObject)}' with ID '{prefab.UniqueID}'.",
                               LogCategory.PrefabManager,
                               LogLevel.Info);
                }
            }

            if (!string.IsNullOrEmpty(prefab.UniqueID) &&
                instantiatedPrefabs.TryGetValue(prefab.UniqueID, out var tracked) &&
                tracked == prefab.gameObject)
            {
                instantiatedPrefabs.Remove(prefab.UniqueID);
            }
        }

        public void UpdatePrefabUniqueID(SaveablePrefab prefab, string previousUniqueID, string newUniqueID)
        {
            if (prefab == null)
                return;

            if (string.IsNullOrEmpty(newUniqueID))
                return;

            if (!IsSaveSystemParticipant(prefab))
            {
                RemoveRegistration(prefab);
                RemovePrefabMappingIfOwned(previousUniqueID, prefab.gameObject);
                RemovePrefabMappingIfOwned(newUniqueID, prefab.gameObject);
                return;
            }

            if (!string.IsNullOrEmpty(previousUniqueID) &&
                !string.Equals(previousUniqueID, newUniqueID, StringComparison.Ordinal))
            {
                if (instantiatedPrefabs.TryGetValue(previousUniqueID, out var existing) &&
                    (existing == null || existing == prefab.gameObject))
                {
                    instantiatedPrefabs.Remove(previousUniqueID);
                }
            }

            PrefabLoadDiagnosticCounters.RecordLegacyRegistrationCollectionScan();
            EnsureRegistration(prefab);

            if (instantiatedPrefabs.TryGetValue(newUniqueID, out var mapped) && mapped != null && mapped != prefab.gameObject)
            {
                if (ShouldEmitDetailedPrefabDiagnostics)
                {
                    Logger.Log($"PrefabManager: UniqueID '{newUniqueID}' was mapped to '{mapped.name}'. Overriding with '{prefab.gameObject.name}'.",
                               LogCategory.PrefabManager,
                               LogLevel.Info);
                }
            }

            instantiatedPrefabs[newUniqueID] = prefab.gameObject;
        }
        #endregion

        #region Prefab Data Collection

        /// <summary>
        /// Collects data from all registered SaveablePrefabs.
        /// </summary>

			                public List<SaveablePrefabData> CollectPrefabData()
			                {
			                        RefreshManualPrefabRegistrations();
		                                bool emitDetailedDiagnostics =
			                                ShouldEmitDetailedPrefabDiagnostics;

			                        var prefabDataList = new List<SaveablePrefabData>();
	                                // A prefab capture can invoke user ISaveable callbacks. Those callbacks
	                                // may destroy or unregister another prefab, which must not invalidate
	                                // this collection pass (or silently omit the rest of the live list).
	                                // Preserve the registration order at the collection boundary; Unity's
	                                // destroyed-object null check below still skips entries destroyed before
	                                // their turn.
	                                SaveablePrefab[] prefabsToCollect = saveablePrefabs.ToArray();
	                        int totalPrefabs = prefabsToCollect.Length;
	                        Logger.Log($"[PrefabManager] CollectPrefabData: Processing {totalPrefabs} prefabs", LogCategory.PrefabManager, LogLevel.Info);
	                        LogRegisteredPrefabDiagnostics("CollectPrefabData-Begin");

	                        int processedCount = 0;
	                        foreach (var prefab in prefabsToCollect)
                        {
                                if (prefab == null)
                                {
                                        processedCount++;
                                        Logger.Log($"[PrefabManager] Skipping null prefab at index {processedCount - 1} (processed {processedCount}/{totalPrefabs})",
                                                LogCategory.PrefabManager,
                                                LogLevel.Warning);
                                        continue;
                                }

                                try
                                {
                                        var data = CreatePrefabData(prefab);
		                                        if (data != null)
		                                        {
		                                                prefabDataList.Add(data);
			                                                if (emitDetailedDiagnostics)
			                                                {
				                                                Logger.Log($"{SaveFlowDiagPrefix} CollectPrefabData serialized: {DescribePrefabData(data)} from {DescribePrefab(prefab)}",
					                                                LogCategory.PrefabManager,
					                                                LogLevel.Info);
			                                                }
		                                        }
                                }
                                catch (System.Exception ex)
                                {
                                        // Log but don't break the entire save operation
                                        ComponentOperationScope.Record(new InvalidOperationException($"Prefab {prefab.UniqueID}: {ex.Message}", ex));
                                        Logger.Log($"[PrefabManager] Failed to collect data for prefab '{prefab.name}': {ex.Message}\nStack Trace: {ex.StackTrace}",
                                                LogCategory.PrefabManager,
                                                LogLevel.Error);
                                        UnityEngine.Debug.LogException(ex);
                                }

                                processedCount++;
                                Logger.Log($"[PrefabManager] Processed {processedCount}/{totalPrefabs} prefabs",
                                        LogCategory.PrefabManager,
                                        LogLevel.Info);
                        }

	                        Logger.Log($"[PrefabManager] CollectPrefabData: Collected {prefabDataList.Count} prefab data entries", LogCategory.PrefabManager, LogLevel.Info);

                        restoreQueue.AppendCaptureData(prefabDataList, emitDetailedDiagnostics
                            ? (Action<string, string, SaveablePrefabData>)((kind, scene, data) =>
                                Logger.Log($"{SaveFlowDiagPrefix} CollectPrefabData including {kind}[{scene}]: {DescribePrefabData(data)}",
                                    LogCategory.PrefabManager, LogLevel.Info))
                            : null);

	                        return prefabDataList;
	                }

	                public SaveablePrefabData BuildPrefabData(SaveablePrefab prefab)
	                {
	                        return CreatePrefabData(prefab);
	                }

		                private void LogRegisteredPrefabDiagnostics(string context)
		                {
			                        bool emitDetailedDiagnostics =
				                        ShouldEmitDetailedPrefabDiagnostics;
			                        bool emitWarnings =
				                        Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Warning);
			                        if (!emitDetailedDiagnostics && !emitWarnings)
				                        return;

			                        if (emitDetailedDiagnostics)
			                        {
				                        Logger.Log($"{SaveFlowDiagPrefix} {context}: registered SaveablePrefabs={saveablePrefabs.Count}, instantiatedMappings={instantiatedPrefabs.Count}.",
					                        LogCategory.PrefabManager,
					                        LogLevel.Info);
			                        }

		                        var seen = new Dictionary<string, SaveablePrefab>(StringComparer.Ordinal);
	                        for (int i = 0; i < saveablePrefabs.Count; i++)
	                        {
	                                SaveablePrefab prefab = saveablePrefabs[i];
		                                if (prefab == null)
		                                {
			                                        if (emitWarnings)
			                                        {
				                                        Logger.Log($"{SaveFlowDiagPrefix} {context}: registered[{i}]=<null>",
					                                        LogCategory.PrefabManager,
					                                        LogLevel.Warning);
			                                        }
		                                        continue;
		                                }

			                                if (emitDetailedDiagnostics)
			                                {
				                                Logger.Log($"{SaveFlowDiagPrefix} {context}: registered[{i}] {DescribePrefab(prefab)}",
					                                LogCategory.PrefabManager,
					                                LogLevel.Info);
			                                }

	                                if (string.IsNullOrEmpty(prefab.UniqueID))
	                                {
	                                        continue;
	                                }

		                                if (seen.TryGetValue(prefab.UniqueID, out var existing) && existing != prefab)
		                                {
			                                        if (emitWarnings)
			                                        {
				                                        if (emitDetailedDiagnostics)
				                                        {
					                                        Logger.Log(
						                                        $"{SaveFlowDiagPrefix} {context}: duplicate registered UniqueID '{prefab.UniqueID}'. First={DescribePrefab(existing)} Duplicate={DescribePrefab(prefab)}",
						                                        LogCategory.PrefabManager,
						                                        LogLevel.Warning);
				                                        }
				                                        else
				                                        {
					                                        Logger.Log(
						                                        $"{SaveFlowDiagPrefix} {context}: duplicate registered UniqueID '{prefab.UniqueID}'.",
						                                        LogCategory.PrefabManager,
						                                        LogLevel.Warning);
				                                        }
			                                        }
		                                }
	                                else
	                                {
	                                        seen[prefab.UniqueID] = prefab;
	                                }
	                        }
	                }

	                private static string DescribePrefab(SaveablePrefab prefab)
	                {
	                        PrefabLoadDiagnosticCounters.RecordPrefabDescription();
	                        if (prefab == null)
	                                return "<null SaveablePrefab>";

	                        GameObject go = prefab.gameObject;
	                        string sceneName = go != null && go.scene.IsValid() ? go.scene.name : "<invalid>";
		                        return
		                                $"name='{(go != null ? go.name : "<null>")}', path='{GetFullTransformPath(go != null ? go.transform : null)}', scene='{sceneName}', " +
		                                $"prefabAssetID='{EmptyLabel(prefab.PrefabAssetID)}', uniqueID='{EmptyLabel(prefab.UniqueID)}', register={prefab.RegisterWithSaveSystem}, " +
		                                $"componentEnabled={prefab.enabled}, activeSelf={(go != null && go.activeSelf)}, activeInHierarchy={(go != null && go.activeInHierarchy)}, " +
		                                $"addedAtRuntime={prefab.IsAddedAtRuntime}, rememberDestroyed={(prefab.PropertySettings != null && prefab.PropertySettings.RememberDestroyed)}, " +
		                                $"reuseSceneInstanceOnLoad={prefab.ReuseSceneInstanceOnLoad}, rememberHomeScene={prefab.RememberHomeScene}, homeScene='{EmptyLabel(prefab.HomeScene)}', " +
		                                $"runtimeOriginScene='{EmptyLabel(prefab.RuntimeOriginSceneName)}'";
		                }

        private static bool IsSceneBackedReuseCandidate(SaveablePrefab prefab) => PrefabReusePolicy.IsSceneBacked(prefab);

	                private static string DescribePrefabData(SaveablePrefabData data)
	                {
	                        PrefabLoadDiagnosticCounters.RecordPrefabDataDescription();
	                        if (data == null)
	                                return "<null SaveablePrefabData>";

	                        return
	                                $"gameObjectName='{EmptyLabel(data.GameObjectName)}', prefabID='{EmptyLabel(data.PrefabID)}', instanceID='{EmptyLabel(data.InstanceID)}', " +
	                                $"parentID='{EmptyLabel(data.ParentID)}', homeScene='{EmptyLabel(data.HomeScene)}', reuseSceneInstance={data.ReuseSceneInstanceOnLoad}, " +
	                                $"handoff={data.EnableCrossSceneStateHandoff}, handoffSceneBaked={data.HandoffToSceneBakedPrefabs}, handoffTransform={data.HandoffTransform}, handoffRuntimeMods={data.HandoffRuntimeModifications}, " +
	                                $"loadPriority={data.LoadPriority}, defer={data.DeferLowPriorityUntilRequested}, trackAddedComponents={data.TrackAddedComponents}, " +
	                                $"trackComponentBlobs={data.TrackComponentBlobs}, activeSelfAtSave={(data.ActiveSelfAtSave.HasValue ? data.ActiveSelfAtSave.Value.ToString() : "<unset>")}, " +
	                                $"runtimeModBytes={(data.RuntimeModificationData != null ? data.RuntimeModificationData.Length : 0)}";
		                }

	                private static string EmptyLabel(string value)
	                {
	                        return string.IsNullOrEmpty(value) ? "<empty>" : value;
	                }

        private static bool IsSaveSystemParticipant(SaveablePrefab prefab) => PrefabReusePolicy.IsParticipant(prefab);

		                private void RefreshManualPrefabRegistrations()
		                {
		                        if (Owner == null)
		                                return;

		                        var scenePrefabs = UnityObjectSearch.FindIncludingInactive<SaveablePrefab>();
		                        foreach (var prefab in scenePrefabs)
		                        {
		                                if (prefab == null)
		                                        continue;

		                                if (string.IsNullOrEmpty(prefab.UniqueID))
		                                        continue;

		                                if (!IsSaveSystemParticipant(prefab))
		                                        continue;

		                                if (!saveablePrefabs.Contains(prefab))
		                                {
			                                        AddRegistration(prefab);
			                                        if (ShouldEmitDetailedPrefabDiagnostics)
			                                        {
				                                        Logger.Log($"{SaveFlowDiagPrefix} RefreshManualPrefabRegistrations tracked: {DescribePrefab(prefab)}",
					                                        LogCategory.PrefabManager,
					                                        LogLevel.Info);
			                                        }
		                                }
                                                else
                                                {
                                                        registeredPrefabInstances.Add(prefab);
                                                }

		                                instantiatedPrefabs[prefab.UniqueID] = prefab.gameObject;
		                        }
		                }

		                private static bool IsValidReuseTarget(
			                GameObject existing,
			                SaveablePrefab candidate,
			                SaveablePrefabData data,
			                out SaveablePrefab saveablePrefab,
			                out string reason)
	                {
	                        saveablePrefab = candidate;
	                        reason = null;

	                        if (existing == null)
	                        {
	                                reason = "candidate GameObject is null";
	                                return false;
	                        }

	                        if (data == null)
	                        {
	                                reason = "save data is null";
	                                return false;
	                        }

	                        if (saveablePrefab == null &&
	                            (!existing.TryGetComponent(out saveablePrefab) || saveablePrefab == null))
	                        {
	                                reason = "candidate has no SaveablePrefab";
	                                return false;
	                        }

		                        if (!IsSaveSystemParticipant(saveablePrefab))
		                        {
		                                reason = "candidate is not registered with the save system";
		                                return false;
		                        }

	                        if (!string.IsNullOrEmpty(data.PrefabID) &&
	                            !string.Equals(saveablePrefab.PrefabAssetID, data.PrefabID, StringComparison.Ordinal))
	                        {
	                                reason =
	                                        $"prefab asset mismatch; expected '{EmptyLabel(data.PrefabID)}' but candidate has '{EmptyLabel(saveablePrefab.PrefabAssetID)}'";
	                                return false;
	                        }

	                        var scene = existing.scene;
	                        if (!scene.IsValid())
	                        {
	                                reason = "candidate scene is invalid";
	                                return false;
	                        }

	                        if (!string.IsNullOrEmpty(data.HomeScene))
	                        {
	                                bool inHomeScene = string.Equals(scene.name, data.HomeScene, StringComparison.Ordinal);
	                                bool validPersistentScene =
	                                        string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal) &&
	                                        saveablePrefab.KeepAcrossScenes;

	                                if (!inHomeScene && !validPersistentScene)
	                                {
	                                        reason =
	                                                $"candidate is in scene '{scene.name}' but save data targets home scene '{data.HomeScene}'";
	                                        return false;
	                                }
	                        }

	                        return true;
	                }

	                private static bool IsCurrentMappedTargetForData(
		                GameObject existing,
		                SaveablePrefabData data,
		                out SaveablePrefab saveablePrefab,
		                out string reason)
	                {
	                        if (!IsValidReuseTarget(existing, null, data, out saveablePrefab, out reason))
	                                return false;
	                        if (string.IsNullOrEmpty(data.InstanceID))
	                        {
	                                reason = "save data has no InstanceID";
	                                return false;
	                        }
	                        if (!string.Equals(saveablePrefab.UniqueID, data.InstanceID, StringComparison.Ordinal))
	                        {
	                                reason = $"candidate UniqueID mismatch; expected '{EmptyLabel(data.InstanceID)}' but candidate has '{EmptyLabel(saveablePrefab.UniqueID)}'";
	                                return false;
	                        }
	                        return true;
	                }

	                private static bool ShouldProcessMappedSceneReuseTarget(
		                SaveablePrefab saveablePrefab,
		                SaveablePrefabData data)
	                {
	                        if (saveablePrefab == null || data == null)
	                                return false;

	                        return IsSceneBackedReuseCandidate(saveablePrefab) &&
	                               (data.ReuseSceneInstanceOnLoad || saveablePrefab.ReuseSceneInstanceOnLoad);
	                }

	                private static void ApplySceneReuseSavedIdentityState(GameObject target, SaveablePrefabData data, bool applySavedTransform = true)
	                {
	                    if (target == null || data == null)
	                        return;

	                    if (!string.IsNullOrEmpty(data.GameObjectName))
	                        target.name = data.GameObjectName;

	                    if (!applySavedTransform)
	                        return;

	                    bool hasOptimizationFlags = data.UsesOptimizationFlags;
	                    bool applyTransform = hasOptimizationFlags ? data.HasTransformOverride : true;
	                    if (!applyTransform)
	                        return;

                    bool savedHadParent =
                        !string.IsNullOrEmpty(data.ParentID) ||
                        !string.IsNullOrEmpty(data.ParentStableKey) ||
                        !string.IsNullOrEmpty(data.ParentPrefabAssetID);

                    if (savedHadParent)
                    {
                        if (target.transform.parent != null)
                        {
                            target.transform.localPosition = data.Position;
                            target.transform.localRotation = data.Rotation;
                        }
                    }
                    else
                    {
                        target.transform.position = data.Position;
                        target.transform.rotation = data.Rotation;
                    }

	                    target.transform.localScale = data.Scale;
	                }

	                private void RemovePrefabMappingIfOwned(string uniqueID, GameObject owner)
	                {
	                        if (string.IsNullOrEmpty(uniqueID) || owner == null)
	                                return;

	                        if (instantiatedPrefabs.TryGetValue(uniqueID, out var mapped) && mapped == owner)
	                                instantiatedPrefabs.Remove(uniqueID);
	                }

	                private static string GetFullTransformPath(Transform transform)
	                {
	                        PrefabLoadDiagnosticCounters.RecordTransformPathDescription();
	                        if (transform == null)
	                                return "<null>";

	                        string path = transform.name;
	                        Transform current = transform.parent;
	                        while (current != null)
	                        {
	                                path = current.name + "/" + path;
	                                current = current.parent;
	                        }

	                        return path;
	                }

	        #region Deferred Prefab Scheduling

        /// <summary>
        /// Splits the provided prefab data into immediate and deferred batches, queues the deferred entries,
        /// and returns the high-priority list sorted by <see cref="SaveablePrefabData.LoadPriority"/>.
        /// </summary>
        public List<SaveablePrefabData> PrepareImmediatePrefabs(List<SaveablePrefabData> prefabDataList)
        {
            return PreparePrefabBatchForLoad(prefabDataList).Immediate;
        }

        /// <summary>
        /// Prepares a load batch once so the scene load path does not normalize and sort the
        /// same entries again when starting the instantiation coroutine.
        /// </summary>
        internal PreparedPrefabBatch PreparePrefabBatchForLoad(List<SaveablePrefabData> prefabDataList)
        {
            PreparedPrefabBatch prepared = PreparePrefabBatch(prefabDataList, partitionDeferred: true);
            if (prepared.Deferred.Count > 0)
                QueueDeferredPrefabs(prepared.Deferred);
            return prepared;
        }

        /// <summary>
        /// Discards scene-pending and explicitly deferred prefab data that has not started
        /// restoring. Live instances and batches already being restored are unaffected.
        /// </summary>
        public void ClearPendingPrefabs()
        {
            restoreQueue.Clear();
            OnPendingPrefabsCleared?.Invoke();
        }

        /// <summary>
        /// Retires data and work from the previous full snapshot after preparation succeeds,
        /// before scene callbacks can resume its pending prefabs.
        /// </summary>
        internal void BeginFullLoadGeneration()
        {
            IDisposable[] retired = restoreLifetime.Invalidate();
            try
            {
                // Clear queues before notifying streamers or disposing scene overrides:
                // either action can synchronously call registration/streaming APIs.
                ClearPendingPrefabs();
            }
            finally
            {
                PrefabRestoreLifetime.DisposeRetired(retired);
            }
        }

        /// <summary>Raised after queued prefab data is discarded, so streamers can release cached snapshots.</summary>
        public event Action OnPendingPrefabsCleared;

        /// <summary>
        /// Returns <c>true</c> if any deferred prefabs remain queued.
        /// </summary>
        public bool HasDeferredPrefabs => restoreQueue.HasDeferred;

        /// <summary>Enumerates scene keys with deferred prefabs waiting to be processed.</summary>
        public IEnumerable<string> GetDeferredSceneKeys() => restoreQueue.DeferredSceneKeys();

        /// <summary>Enumerates scenes with prefabs waiting for scene loading or activation.</summary>
        public IEnumerable<string> GetPendingSceneKeys() => restoreQueue.PendingSceneKeys();

        /// <summary>Returns a snapshot without dequeuing the scene's deferred prefabs.</summary>
        public IReadOnlyList<SaveablePrefabData> PeekDeferredPrefabsForScene(string sceneName)
            => restoreQueue.PeekDeferredScene(sceneName);

        /// <summary>
        /// Processes all deferred prefabs regardless of scene using the stored queue.
        /// </summary>
        public Coroutine ProcessDeferredPrefabs(List<string> destroyedGameObjectIDs = null)
        {
            var entries = restoreQueue.TakeAllDeferred();
            if (entries.Count == 0)
                return null;

            var destroyed = destroyedGameObjectIDs ?? Owner?.GetDestroyedGameObjectIDs() ?? new List<string>();
            return StartCoroutine(InstantiatePrefabsCoroutine(entries, destroyed, clearExistingPrefabs: false, handleDeferral: false));
        }

        /// <summary>
        /// Processes deferred prefabs for a specific scene.
        /// </summary>
        public Coroutine ProcessDeferredPrefabsForScene(string sceneName, List<string> destroyedGameObjectIDs = null)
        {
            var entries = restoreQueue.TakeDeferredScene(sceneName);
            if (entries.Count == 0)
                return null;

            var destroyed = destroyedGameObjectIDs ?? Owner?.GetDestroyedGameObjectIDs() ?? new List<string>();
            return StartCoroutine(InstantiatePrefabsCoroutine(entries, destroyed, clearExistingPrefabs: false, handleDeferral: false));
        }

        /// <summary>
        /// Processes deferred prefabs for a specific prefab asset across all scenes.
        /// </summary>
        public Coroutine ProcessDeferredPrefabsForAsset(string prefabAssetID, List<string> destroyedGameObjectIDs = null)
        {
            if (string.IsNullOrEmpty(prefabAssetID))
                return null;

            var entries = restoreQueue.TakeDeferredAsset(prefabAssetID);
            if (entries.Count == 0)
                return null;

            var destroyed = destroyedGameObjectIDs ?? Owner?.GetDestroyedGameObjectIDs() ?? new List<string>();
            return StartCoroutine(InstantiatePrefabsCoroutine(entries, destroyed, clearExistingPrefabs: false, handleDeferral: false));
        }

        /// <summary>
        /// Processes deferred prefabs that match a specific instance ID across all scenes.
        /// </summary>
        public Coroutine ProcessDeferredPrefabByUniqueID(string instanceID, List<string> destroyedGameObjectIDs = null)
        {
            if (string.IsNullOrEmpty(instanceID))
                return null;

            var entries = restoreQueue.TakeDeferredInstance(instanceID);
            if (entries.Count == 0)
                return null;

            var destroyed = destroyedGameObjectIDs ?? Owner?.GetDestroyedGameObjectIDs() ?? new List<string>();
            return StartCoroutine(InstantiatePrefabsCoroutine(entries, destroyed, clearExistingPrefabs: false, handleDeferral: false));
        }

        /// <summary>
        /// Processes deferred prefabs for the supplied instance IDs across all scenes.
        /// </summary>
        /// <param name="instanceIDs">Collection of deferred prefab instance IDs to process.</param>
        /// <param name="destroyedGameObjectIDs">Optional list of destroyed object IDs to pass through to instantiation.</param>
        /// <returns>The coroutine responsible for instantiating the requested prefabs, or <c>null</c> if no entries were queued.</returns>
        public Coroutine ProcessDeferredPrefabsByInstanceIDs(IReadOnlyCollection<string> instanceIDs, List<string> destroyedGameObjectIDs = null)
        {
            if (instanceIDs == null || instanceIDs.Count == 0)
                return null;

            var entries = restoreQueue.TakeDeferredInstances(instanceIDs);
            if (entries.Count == 0)
                return null;

            var destroyed = destroyedGameObjectIDs ?? Owner?.GetDestroyedGameObjectIDs() ?? new List<string>();
            return StartCoroutine(InstantiatePrefabsCoroutine(entries, destroyed, clearExistingPrefabs: false, handleDeferral: false));
        }

        private static PreparedPrefabBatch PreparePrefabBatch(
            IEnumerable<SaveablePrefabData> source, bool partitionDeferred)
            => PrefabRestoreQueue.PrepareBatch(source, partitionDeferred);

        private void QueueDeferredPrefabs(IEnumerable<SaveablePrefabData> entries)
        {
            var queued = restoreQueue.QueueDeferred(entries);
            if (queued.Count != 0)
                OnDeferredPrefabsQueued?.Invoke(queued);
        }

        #endregion

		                private SaveablePrefabData CreatePrefabData(SaveablePrefab prefab)
		                {
			                        bool emitDetailedDiagnostics =
				                        ShouldEmitDetailedPrefabDiagnostics;
		                        if (prefab == null)
		                        {
	                                Logger.Log($"{SaveFlowDiagPrefix} CreatePrefabData skipped null prefab.",
	                                        LogCategory.PrefabManager,
	                                        LogLevel.Warning);
	                                return null;
	                        }

			                        if (!IsSaveSystemParticipant(prefab))
			                        {
				                                if (emitDetailedDiagnostics)
				                                {
					                                Logger.Log($"{SaveFlowDiagPrefix} CreatePrefabData skipped non-participating prefab: {DescribePrefab(prefab)}",
						                                LogCategory.PrefabManager,
						                                LogLevel.Info);
				                                }
			                                return null;
			                        }

	                        var data = prefab.TryBuildSaveData();
		                        if (data == null)
		                        {
			                                if (Logger.ShouldLog(
				                                    LogCategory.PrefabManager,
				                                    LogLevel.Warning))
			                                {
				                                if (emitDetailedDiagnostics)
				                                {
					                                Logger.Log($"{SaveFlowDiagPrefix} CreatePrefabData TryBuildSaveData returned null: {DescribePrefab(prefab)}",
						                                LogCategory.PrefabManager,
						                                LogLevel.Warning);
				                                }
				                                else
				                                {
					                                Logger.Log(
						                                "PrefabManager.CreatePrefabData: TryBuildSaveData returned null.",
						                                LogCategory.PrefabManager,
						                                LogLevel.Warning);
				                                }
			                                }
		                                return null;
		                        }

                        if (prefab.TryGetLastSnapshot(out var snapshot))
                        {
                                if (data.HasParentOverride || prefab.IsAddedAtRuntime)
                                {
                                        data.ParentID = snapshot.ParentID;
                                        data.IsParentSceneObject = snapshot.IsParentSceneObject;
                                }
                                else if (string.IsNullOrEmpty(data.ParentID))
                                {
                                        data.ParentID = snapshot.ParentID;
                                        data.IsParentSceneObject = snapshot.IsParentSceneObject;
                                }

                                if (data.HasTransformOverride || prefab.IsAddedAtRuntime)
                                {
                                        if (snapshot.HasParent)
                                        {
                                                data.Position = snapshot.LocalPosition;
                                                data.Rotation = snapshot.LocalRotation;
                                        }
                                        else
                                        {
                                                data.Position = snapshot.WorldPosition;
                                                data.Rotation = snapshot.WorldRotation;
                                        }

                                        data.Scale = snapshot.LocalScale;
                                }
                                else if (data.Scale == default)
                                {
                                        data.Scale = snapshot.LocalScale;
                                }

                                // Parent fingerprinting for robust reattachment
                                if (snapshot.HasParent)
                                {
                                    // If parent is a SaveablePrefab, capture its assetID and stable path
                                    var parentSp = prefab.transform.parent ? prefab.transform.parent.GetComponentInParent<SaveablePrefab>() : null;
                                    if (parentSp != null)
                                    {
                                        data.ParentPrefabAssetID = parentSp.PrefabAssetID;
                                        data.ParentStableKey = SaveablePrefab.BuildStableHierarchyKey(parentSp);
                                    }
                                }
            }

            // When remembering Home Scene, always carry the current transform in the save-data
            // so that snapshot-based scene switches restore the latest runtime position/rotation
            // instead of falling back to design-time placement.
            if (prefab.RememberHomeScene && prefab.TryGetLastSnapshot(out var hsSnapshot))
            {
                data.HasTransformOverride = true;
                if (hsSnapshot.HasParent)
                {
                    data.Position = hsSnapshot.LocalPosition;
                    data.Rotation = hsSnapshot.LocalRotation;
                }
                else
                {
                    data.Position = hsSnapshot.WorldPosition;
                    data.Rotation = hsSnapshot.WorldRotation;
                }
                data.Scale = hsSnapshot.LocalScale;

                // Also carry parent fingerprint when remembering home scene
                var parentSp = prefab.transform.parent ? prefab.transform.parent.GetComponentInParent<SaveablePrefab>() : null;
                if (parentSp != null)
                {
                    data.ParentPrefabAssetID = parentSp.PrefabAssetID;
                    data.ParentStableKey = SaveablePrefab.BuildStableHierarchyKey(parentSp);
                }
            }

            if (prefab.RememberHomeScene)
            {
                if (prefab.HomeSceneCaptureMode == SaveablePrefab.HomeSceneMode.LastSnapshotScene &&
                    prefab.gameObject.scene.name != "DontDestroyOnLoad")
                {
                    prefab.SetHomeScene(prefab.gameObject.scene.name);
                }

                data.HomeScene = prefab.HomeScene;
            }
            else
            {
                // Intentionally leave HomeScene empty unless RememberHomeScene is explicit.
                // For scene-baked reuse, an empty HomeScene means "claim the matching baked
                // instance in the currently loaded scene" instead of locking the save entry
                // to whichever scene happened to create the snapshot.
                data.HomeScene = null;
            }

                        data.DisablePooling = prefab.DisablePooling;
                        data.LoadPriority = prefab.LoadPriority;
                        data.DeferLowPriorityUntilRequested = prefab.DeferLowPriorityUntilRequested;

	                        if (data.HasVisibilityData)
	                        {
	                                data.VisibilitySettingsData ??= prefab.GetVisibilitySettings();
	                                if (data.VisibilitySettingsData == null || data.VisibilitySettingsData.Length == 0)
	                                {
	                                        data.HasVisibilityData = false;
	                                }
	                        }

			                        if (emitDetailedDiagnostics)
			                        {
				                        Logger.Log($"{SaveFlowDiagPrefix} CreatePrefabData produced: {DescribePrefabData(data)} from {DescribePrefab(prefab)}",
					                        LogCategory.PrefabManager,
					                        LogLevel.Info);
			                        }

                        if (data.RuntimeModificationData == null)
                        {
                                var mods = prefab.CaptureRuntimeModifications();
                                if (mods != null && mods.Length > 0)
                                        data.RuntimeModificationData = mods;
                        }

                        var rb = prefab.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                                data.HasRigidbody = true;
#if UNITY_6000_0_OR_NEWER
                                data.RigidbodyVelocity        = rb.linearVelocity;
                                data.RigidbodyAngularVelocity = rb.angularVelocity;
                                data.RigidbodyUseGravity      = rb.useGravity;
                                data.RigidbodyIsKinematic     = rb.isKinematic;
                                data.RigidbodyDrag            = rb.linearDamping;
                                data.RigidbodyAngularDrag     = rb.angularDamping;
#else
                                data.RigidbodyVelocity        = rb.velocity;
                                data.RigidbodyAngularVelocity = rb.angularVelocity;
                                data.RigidbodyUseGravity      = rb.useGravity;
                                data.RigidbodyIsKinematic     = rb.isKinematic;
                                data.RigidbodyDrag            = rb.drag;
                                data.RigidbodyAngularDrag     = rb.angularDrag;
#endif
                        }

                        var anim = prefab.GetComponent<Animator>();
                        if (anim != null)
                        {
                                data.HasAnimator = true;
                                AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
                                data.AnimatorStateHash     = info.shortNameHash;
                                data.AnimatorNormalizedTime = info.normalizedTime;
                        }

	                        if (prefab.TrackColliderSettings)
	                                data.Colliders = SaveColliderSettings(prefab);

			                        if (emitDetailedDiagnostics)
			                        {
				                        Logger.Log($"{SaveFlowDiagPrefix} CreatePrefabData final: {DescribePrefabData(data)} from {DescribePrefab(prefab)}",
					                        LogCategory.PrefabManager,
					                        LogLevel.Info);
			                        }

	                        return data;
	                }
        #endregion

        #region Prefab Instantiation

        /// <summary>
        /// Instantiates prefabs based on the provided SaveablePrefabData.
        /// </summary>
                public IEnumerator InstantiatePrefabsCoroutine(
                        List<SaveablePrefabData> prefabDataList,
                        List<string> destroyedGameObjectIDs,
                        bool clearExistingPrefabs = true,
                        bool handleDeferral = true
                )
                {
                        return restoreLifetime.Bind(this, InstantiatePrefabsCoroutineCore(
                                prefabDataList,
                                destroyedGameObjectIDs,
                                clearExistingPrefabs,
                                handleDeferral,
                                null));
                }

                internal IEnumerator InstantiatePreparedPrefabsCoroutine(
                        PreparedPrefabBatch preparedBatch,
                        List<string> destroyedGameObjectIDs,
                        bool clearExistingPrefabs = true)
                {
                        return restoreLifetime.Bind(this, InstantiatePrefabsCoroutineCore(
                                preparedBatch?.Immediate,
                                destroyedGameObjectIDs,
                                clearExistingPrefabs,
                                handleDeferral: false,
                                preparedBatch));
                }

                private IEnumerator InstantiatePrefabsCoroutineCore(
                        List<SaveablePrefabData> prefabDataList,
                        List<string> destroyedGameObjectIDs,
                        bool clearExistingPrefabs,
                        bool handleDeferral,
                        PreparedPrefabBatch preparedBatch)
                {
                        bool signalImmediateBatchComplete =
                                handleDeferral || preparedBatch != null;
                        if (prefabDataList == null)
                        {
                                if (signalImmediateBatchComplete)
                                        OnImmediatePrefabBatchComplete?.Invoke();
                                yield break;
                        }

	                        bool verboseLoadDiagnostics =
		                        Owner?.SaveSettings?.enableVerbosePrefabLoadDiagnostics == true &&
		                        Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Info);

	                        PreparedPrefabBatch normalizedBatch = preparedBatch ??
		                        PreparePrefabBatch(prefabDataList, partitionDeferred: handleDeferral);
	                        prefabDataList = normalizedBatch.Immediate;

	                        PrefabLoadContext loadContext;
	                        using (BuildLoadContextMarker.Auto())
	                        {
		                        PrefabLoadDiagnosticCounters.RecordContextBuild();
		                        SaveablePrefab[] sceneSnapshot =
			                        UnityObjectSearch.FindIncludingInactive<SaveablePrefab>();
		                        PrefabLoadDiagnosticCounters.RecordSceneSnapshot();
		                        loadContext = CreateLoadContext(destroyedGameObjectIDs, sceneSnapshot);
	                        }

		                        if (verboseLoadDiagnostics)
		                        {
		                        Logger.Log(
			                        $"{SaveFlowDiagPrefix} InstantiatePrefabsCoroutine begin: entries={prefabDataList.Count}, destroyedIDs={loadContext.DestroyedIds.Count}, " +
			                        $"clearExistingPrefabs={clearExistingPrefabs}, handleDeferral={handleDeferral}.",
			                        LogCategory.PrefabManager,
			                        LogLevel.Info);

		                        for (int i = 0; i < prefabDataList.Count; i++)
		                        {
			                        Logger.Log($"{SaveFlowDiagPrefix} InstantiatePrefabsCoroutine input[{i}]: {DescribePrefabData(prefabDataList[i])}",
				                        LogCategory.PrefabManager,
				                        LogLevel.Info);
			                        }
		                        }

	                        // Destroyed entries are terminal for this batch. Remove every
	                        // indexed/mapped owner before reuse discovery or clear/preserve
	                        // planning so a destroyed scene instance cannot be claimed back.
	                        loadContext.RemoveDestroyedMappings(
		                        instantiatedPrefabs);

	                        for (int i = 0; i < loadContext.DestroyedOwners.Count; i++)
	                        {
		                        GameObject destroyedOwner =
			                        loadContext.DestroyedOwners[i];
		                        if (destroyedOwner == null)
			                        continue;

		                        loadContext.TryGetSaveable(
			                        destroyedOwner,
			                        out SaveablePrefab destroyedPrefab);
		                        if (clearExistingPrefabs)
			                        Owner
				                        ?.SoftUnregisterGameObjectForPrefabBatch(
					                        destroyedOwner);
		                        else
			                        Owner
				                        ?.SoftUnregisterGameObject(
					                        destroyedOwner);
		                        loadContext.RemoveObject(
			                        destroyedOwner,
			                        managerCollectionsWillBeRebuilt:
			                        clearExistingPrefabs);

		                        bool despawned =
			                        destroyedPrefab != null &&
			                        ShouldUsePoolingFor(destroyedPrefab) &&
			                        SaveablePrefabPoolCache.TryDespawn(
				                        destroyedPrefab,
				                        GetPoolSizeForPrefab(destroyedPrefab),
				                        true);
		                        if (!despawned)
		                        {
			                        DestroyHelper.DestroyWithLogging(
				                        destroyedOwner,
				                        "PrefabManager.InstantiatePrefabsCoroutine: removing destroyed load owner");
		                        }
	                        }

	                        void ExcludeDestroyedEntries(
		                        List<SaveablePrefabData> entries)
	                        {
		                        if (entries == null || entries.Count == 0)
			                        return;

		                        int writeIndex = 0;
		                        for (int readIndex = 0;
		                             readIndex < entries.Count;
		                             readIndex++)
		                        {
			                        SaveablePrefabData entry = entries[readIndex];
			                        if (entry == null ||
			                            loadContext.IsDestroyed(entry.InstanceID))
			                        {
				                        if (entry != null && verboseLoadDiagnostics)
				                        {
					                        Logger.Log(
						                        $"PrefabManager: Skipping destroyed prefab InstanceID '{entry.InstanceID ?? "<empty>"}'.",
						                        LogCategory.PrefabManager,
						                        LogLevel.Info);
				                        }
				                        continue;
			                        }

			                        entries[writeIndex++] = entry;
		                        }

		                        if (writeIndex < entries.Count)
			                        entries.RemoveRange(
				                        writeIndex,
				                        entries.Count - writeIndex);
	                        }

	                        ExcludeDestroyedEntries(normalizedBatch.Immediate);
	                        ExcludeDestroyedEntries(normalizedBatch.Deferred);
	                        prefabDataList = normalizedBatch.Immediate;

                        // Drop entries only when the existing non-clearing mapping is still a
                        // current, valid target. Stale mappings from an unloaded/previous scene must
                        // be removed so deferred scene-baked restores can claim the loaded scene copy.
                        try
                        {
                            if (!clearExistingPrefabs && prefabDataList.Count > 0 && instantiatedPrefabs.Count > 0)
                            {
                                var remaining = new List<SaveablePrefabData>(prefabDataList.Count);

                                foreach (var data in prefabDataList)
                                {
                                    if (data == null)
                                        continue;

                                    if (string.IsNullOrEmpty(data.InstanceID) ||
                                        !instantiatedPrefabs.TryGetValue(data.InstanceID, out var mapped))
                                    {
                                        remaining.Add(data);
                                        continue;
                                    }

                                    if (IsCurrentMappedTargetForData(
                                            mapped,
                                            data,
                                            out SaveablePrefab currentSaveable,
                                            out var currentReason))
                                    {
                                        if (ShouldProcessMappedSceneReuseTarget(currentSaveable, data))
                                        {
                                            if (verboseLoadDiagnostics)
                                            {
                                                Logger.Log(
                                                    $"{SaveFlowDiagPrefix} InstantiatePrefabsCoroutine: keeping already-mapped scene reuse entry for restore {DescribePrefabData(data)}.",
                                                    LogCategory.PrefabManager,
                                                    LogLevel.Info);
                                            }
                                            remaining.Add(data);
                                            continue;
                                        }

                                        if (verboseLoadDiagnostics)
                                        {
                                            Logger.Log(
                                                $"{SaveFlowDiagPrefix} InstantiatePrefabsCoroutine: skipping already-current mapped entry {DescribePrefabData(data)}.",
                                                LogCategory.PrefabManager,
                                                LogLevel.Info);
                                        }
                                        continue;
                                    }

                                    if (mapped == null)
                                    {
                                        instantiatedPrefabs.Remove(data.InstanceID);
                                    }
                                    else
                                    {
                                        RemovePrefabMappingIfOwned(data.InstanceID, mapped);
                                        loadContext.UnbindIfOwned(data.InstanceID, mapped);
                                    }

                                    if (verboseLoadDiagnostics)
                                    {
                                        Logger.Log(
                                            $"{SaveFlowDiagPrefix} InstantiatePrefabsCoroutine: removed stale mapped entry for {DescribePrefabData(data)}; reason={currentReason ?? "<unknown>"}.",
                                            LogCategory.PrefabManager,
                                            LogLevel.Info);
                                    }

                                    remaining.Add(data);
                                }

                                prefabDataList = remaining;
                            }
                        }
                        catch { /* best-effort */ }

                        List<SaveablePrefabData> mappingList = prefabDataList;
                        List<SaveablePrefabData> deferredList = handleDeferral
                            ? normalizedBatch.Deferred
                            : new List<SaveablePrefabData>();

                        var reuseInstanceIDs =
                            new HashSet<string>(StringComparer.Ordinal);
                        var orderedReuseInstanceIDs =
                            new List<string>(mappingList.Count);

                        void AddReuseInstanceId(string instanceId)
                        {
                            if (!string.IsNullOrEmpty(instanceId) &&
                                reuseInstanceIDs.Add(instanceId))
                            {
                                orderedReuseInstanceIDs.Add(instanceId);
                            }
                        }

                        for (int i = 0; i < mappingList.Count; i++)
                        {
                            SaveablePrefabData data = mappingList[i];
                            if (data != null &&
                                data.ReuseSceneInstanceOnLoad &&
                                !string.IsNullOrEmpty(data.InstanceID))
                            {
                                AddReuseInstanceId(data.InstanceID);
                            }
                        }
	                        var assetClaimedSceneReuseIDs = new HashSet<string>(StringComparer.Ordinal);
	                        var assetClaimedSkipTransformIDs = new HashSet<string>(StringComparer.Ordinal);
	                        var assetClaimedSkipRuntimeModIDs = new HashSet<string>(StringComparer.Ordinal);

            // 1) Prefer reuse by explicit InstanceID list (from save-data flags)
            if (reuseInstanceIDs.Count > 0)
            {
                var reuseLookup = new HashSet<string>(reuseInstanceIDs, StringComparer.Ordinal);
                foreach (var tracked in saveablePrefabs)
                {
                    if (tracked == null)
                        continue;

                    if (!IsSaveSystemParticipant(tracked))
                        continue;

                    string trackedId = tracked.UniqueID;
                    if (!string.IsNullOrEmpty(trackedId))
                    {
                        reuseLookup.Remove(trackedId);
                        // Ensure instantiatedPrefabs has a mapping so later phases can find it immediately
                        if (!instantiatedPrefabs.ContainsKey(trackedId))
                            instantiatedPrefabs[trackedId] = tracked.gameObject;
                        
                    }
                }

                if (reuseLookup.Count > 0)
                {
                    foreach (var prefab in loadContext.ScenePrefabs)
                    {
                        if (prefab == null)
                            continue;

                        if (!IsSaveSystemParticipant(prefab))
                            continue;

                        string uniqueID = prefab.UniqueID;
                        if (string.IsNullOrEmpty(uniqueID) || !reuseLookup.Contains(uniqueID))
                            continue;

                        if (loadContext.RegisteredInstances.Add(prefab))
                        {
                            AddRegistration(prefab);
                        }

                        instantiatedPrefabs[uniqueID] = prefab.gameObject;
                        loadContext.Bind(uniqueID, prefab.gameObject, prefab);
                        if (verboseLoadDiagnostics)
                        {
                            Logger.Log(
                                $"PrefabManager.InstantiatePrefabsCoroutine: Captured untracked reuse candidate '{prefab.gameObject.name}' with UniqueID '{uniqueID}'.",
                                LogCategory.PrefabManager,
                                LogLevel.Info);
                        }

                        reuseLookup.Remove(uniqueID);
                        if (reuseLookup.Count == 0)
                            break;
                    }
                }
            }

            // 2) Asset-based reuse mapping for scene-placed prefabs.
            // This ensures design-time instances without UIDs are claimed and preserved
            // even when the save-data didn't request explicit reuse by InstanceID.
            // Note: During deferred processing we can optionally disable this to avoid
            // reusing scene-baked instances which could lead to under-spawning.
            try
            {
                bool allowBroadAssetReuse = true;
                try
                {
                    // If handleDeferral is false, we are processing deferred entries now.
                    // Respect the SaveSettings toggle to optionally disable asset-based reuse in that case.
                    bool isDeferredPass = !handleDeferral && preparedBatch == null;
                    if (isDeferredPass)
                    {
                        var ss = Owner?.SaveSettings;
                        bool enableForDeferred = ss?.enableAssetBasedReuseDuringDeferred ?? false;
                        allowBroadAssetReuse = enableForDeferred;
                        
                    }
                }
                catch { /* best-effort */ }

                // Group only entries which have a scene candidate. Candidate eligibility is
                // computed once when the load context snapshots the scene.
                var allGroups =
                    new Dictionary<string, List<SaveablePrefabData>>(StringComparer.Ordinal);
                for (int i = 0; i < mappingList.Count; i++)
                {
                    SaveablePrefabData data = mappingList[i];
                    if (data == null ||
                        string.IsNullOrEmpty(data.PrefabID) ||
                        !loadContext.TryGetSceneCandidates(data,
                            out PrefabReuseCandidates candidatePool) ||
                        candidatePool.Count == 0)
                    {
                        continue;
                    }

                    if (!allowBroadAssetReuse &&
                        !data.ReuseSceneInstanceOnLoad &&
                        !candidatePool.RequestsSceneReuse &&
                        !(data.EnableCrossSceneStateHandoff && data.HandoffToSceneBakedPrefabs))
                    {
                        continue;
                    }

                    if (!allGroups.TryGetValue(
                            data.PrefabID,
                            out List<SaveablePrefabData> dataList))
                    {
                        dataList = new List<SaveablePrefabData>();
                        allGroups.Add(data.PrefabID, dataList);
                    }
                    dataList.Add(data);
                }

                foreach (var kvp in allGroups)
                {
                    string prefabAssetId = kvp.Key;
                    var dataList = kvp.Value;
                    // Resolve candidate for each data entry individually with simple heuristics
                    foreach (var data in dataList)
                    {
	                        if (string.IsNullOrEmpty(data.InstanceID))
	                            continue; // can't map without a proper instance id

                        if (!loadContext.TryGetSceneCandidates(data, out PrefabReuseCandidates candidates))
                            continue;

		                        if (data.EnableCrossSceneStateHandoff && !data.HandoffToSceneBakedPrefabs)
		                        {
		                            if (verboseLoadDiagnostics)
		                            {
			                            Logger.Log(
				                            $"{SaveFlowDiagPrefix} Cross-scene state handoff did not claim scene-baked targets for {DescribePrefabData(data)} because HandoffToSceneBakedPrefabs is disabled.",
				                            LogCategory.PrefabManager,
				                            LogLevel.Info);
		                            }
		                            continue;
		                        }

                        // Claiming is O(1) and allocation-free per entry: exact-ID,
                        // empty-ID preference, and handoff compatibility are maintained by
                        // the context-owned pool as candidates are consumed.
                        bool crossSceneHandoff =
                            data.EnableCrossSceneStateHandoff &&
                            data.HandoffToSceneBakedPrefabs;
                        if (!candidates.TryClaim(
                                data,
                                crossSceneHandoff,
                                out SaveablePrefab chosen,
                                out string chosenReason,
                                out int compatibleHandoffCount))
                        {
                            if (crossSceneHandoff)
                            {
                                if (verboseLoadDiagnostics)
                                {
                                    Logger.Log(
                                        $"{SaveFlowDiagPrefix} Cross-scene state handoff skipped for {DescribePrefabData(data)}; expected exactly one scene-baked non-KeepAcrossScenes target with PrefabAssetID '{EmptyLabel(prefabAssetId)}' but found {compatibleHandoffCount}.",
                                        LogCategory.PrefabManager,
                                        LogLevel.Warning);
                                }
                                else if (Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Warning))
                                {
                                    Logger.Log(
                                        $"Prefab restore handoff skipped for InstanceID '{data.InstanceID ?? "<empty>"}': expected one compatible scene target, found {compatibleHandoffCount}.",
                                        LogCategory.PrefabManager,
                                        LogLevel.Warning);
                                }
                            }
                            continue;
                        }

                        loadContext.RetireSceneCandidate(chosen);
		                        if (crossSceneHandoff && verboseLoadDiagnostics)
		                        {
	                            Logger.Log(
	                                $"{SaveFlowDiagPrefix} Cross-scene state handoff claimed {DescribePrefab(chosen)} for {DescribePrefabData(data)}; reason={chosenReason}.",
	                                LogCategory.PrefabManager,
		                                LogLevel.Info);
	                        }

	                        if (!string.IsNullOrEmpty(data.InstanceID))
	                        {
	                            assetClaimedSceneReuseIDs.Add(data.InstanceID);
	                            if (crossSceneHandoff && !data.HandoffTransform)
	                                assetClaimedSkipTransformIDs.Add(data.InstanceID);
	                            if (crossSceneHandoff && !data.HandoffRuntimeModifications)
	                                assetClaimedSkipRuntimeModIDs.Add(data.InstanceID);
	                        }

	                        ApplySceneReuseSavedIdentityState(chosen.gameObject, data, !crossSceneHandoff || data.HandoffTransform);

                        // Ensure it's tracked by PrefabManager
	                        if (loadContext.RegisteredInstances.Add(chosen))
                            {
	                            AddRegistration(chosen);
                            }

                        // Assign the saved InstanceID to the scene instance so subsequent lookup finds it
                        if (string.IsNullOrEmpty(chosen.UniqueID) || !string.Equals(chosen.UniqueID, data.InstanceID, StringComparison.Ordinal))
                        {
                            string previousId = chosen.UniqueID;
                            chosen.SetUniqueIDForRestore(data.InstanceID, loadContext.SaveableComponentBuffer);
                            RemovePrefabMappingIfOwned(previousId, chosen.gameObject);
                            loadContext.UnbindIfOwned(previousId, chosen.gameObject);
                            loadContext.RebindRegistrationOwner(chosen, previousId, data.InstanceID);
                        }

                        // Mirror important flags from save-data
	                        chosen.SetHomeScene(data.HomeScene);
	                        chosen.LoadPriority = data.LoadPriority;
	                        chosen.DeferLowPriorityUntilRequested = data.DeferLowPriorityUntilRequested;
	                        chosen.DisablePooling = data.DisablePooling;
	                        if (data.EnableCrossSceneStateHandoff)
	                        {
	                            chosen.EnableCrossSceneStateHandoff = data.EnableCrossSceneStateHandoff;
	                            chosen.HandoffToSceneBakedPrefabs = data.HandoffToSceneBakedPrefabs;
	                            chosen.HandoffTransform = data.HandoffTransform;
	                            chosen.HandoffRuntimeModifications = data.HandoffRuntimeModifications;
	                        }
	                        if (!crossSceneHandoff)
	                            chosen.ReuseSceneInstanceOnLoad = true; // persist behavior for future saves
	                        // Ensure the data prefers reuse as well to align with the mapped scene instance
	                        data.ReuseSceneInstanceOnLoad = true;

                        // Ensure registration if needed (so future saves/loads see it tracked)
                        if (chosen.RegisterWithSaveSystem)
                        {
                            try { chosen.RegisterForSaving(); }
                            catch { /* ignore */ }
                        }

                        // Seed the lookup so InstantiatePrefabInternal hits the reuse path
                        instantiatedPrefabs[data.InstanceID] = chosen.gameObject;
                        loadContext.Bind(data.InstanceID, chosen.gameObject, chosen);

                        // Also include in the preservation list so ClearSaveablePrefabs keeps it
                        AddReuseInstanceId(data.InstanceID);

                        
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }

            List<SaveablePrefabData> immediatePrefabs;
            if (handleDeferral)
            {
                immediatePrefabs = mappingList;
                if (deferredList.Count > 0)
                    QueueDeferredPrefabs(deferredList);
            }
            else
            {
                immediatePrefabs = mappingList;
            }

            // Ensure the preserve list includes IDs for any incoming entries (in the mapping scope)
            // whose PrefabID exists in the current scene. This lets ClearSaveablePrefabs repopulate
            // scene-placed prefabs with empty UniqueID for the IMMEDIATE batch only.
            try
            {
                if (loadContext.SceneByAssetId.Count > 0)
                {
                    foreach (var entry in mappingList)
                    {
                        if (entry == null) continue;
                        if (string.IsNullOrEmpty(entry.InstanceID) || string.IsNullOrEmpty(entry.PrefabID)) continue;
                        if (!loadContext.SceneByAssetId.ContainsKey(entry.PrefabID)) continue;
                        AddReuseInstanceId(entry.InstanceID);
                    }
                }
            }
            catch { /* best-effort */ }

            if (clearExistingPrefabs)
            {
                // Queueing may happen either in this coroutine (public raw-list path)
                // or in PreparePrefabBatchForLoad (scene-load prepared path). In both
                // cases the staged queue must survive this batch's one clear.
                bool preserveDeferredQueue = restoreQueue.DeferredStagedSinceClear;

                IReadOnlyDictionary<string, string> preserveMap = null;
                if (mappingList.Count > 0)
                {
                    var tmp = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var entry in mappingList)
                    {
                        if (entry == null) continue;
                        if (string.IsNullOrEmpty(entry.InstanceID)) continue;
                        tmp[entry.InstanceID] = entry.PrefabID ?? string.Empty;
                    }

                    if (tmp.Count > 0)
                        preserveMap = tmp;
                }

                ClearSaveablePrefabsInternal(
                    preserveDeferredQueue,
                    orderedReuseInstanceIDs,
                    preserveMap,
                    loadContext);
            }
            else
            {
                // Do NOT clear the reuse mappings; we rely on them later in this coroutine.
                // Just prune any entries that point to destroyed/null instances.
                try
                {
                    var toRemove = instantiatedPrefabs.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();
                    foreach (var k in toRemove)
                        instantiatedPrefabs.Remove(k);
                }
                catch { /* best-effort */ }
            }

            if (immediatePrefabs.Count == 0)
            {
                if (signalImmediateBatchComplete)
                    OnImmediatePrefabBatchComplete?.Invoke();
                yield break;
            }

                        int prefabsProcessed = 0;
            int batchSize = Owner?.SaveSettings?.prefabInstantiationBatchSize ?? 0;
            bool syncTransformsAfterLoad = Owner?.SaveSettings?.syncTransformsAfterPrefabLoad ?? false;
            bool groupByScene = Owner?.SaveSettings?.groupInstantiationByScene ?? false;

            // Track which prefabs were actually instantiated now (vs. reused)
            var justInstantiatedIds = new HashSet<string>(StringComparer.Ordinal);
            var processedInstanceIds = new HashSet<string>(StringComparer.Ordinal);

            // First pass: Instantiate prefabs without finalizing transforms.
            (Vector3 position, Quaternion rotation) ResolveInitialSpawnPose(SaveablePrefabData data)
            {
                if (data == null)
                    return (Vector3.zero, Quaternion.identity);

                Vector3 position = data.Position;
                Quaternion rotation = data.Rotation;
                bool savedHadParent =
                    !string.IsNullOrEmpty(data.ParentID) ||
                    !string.IsNullOrEmpty(data.ParentStableKey) ||
                    !string.IsNullOrEmpty(data.ParentPrefabAssetID);

                if (!savedHadParent)
                    return (position, rotation);

                Transform parent = null;
                if (!string.IsNullOrEmpty(data.ParentID))
                {
                    if (!data.IsParentSceneObject &&
                        instantiatedPrefabs.TryGetValue(data.ParentID, out var parentInstance) &&
                        parentInstance != null)
                    {
                        parent = parentInstance.transform;
                    }

                    if (parent == null)
                    {
                        loadContext.TryResolveIdentifier(data.ParentID, out GameObject parentObject);
                        parent = parentObject != null ? parentObject.transform : null;
                    }
                }

                if (parent == null)
                    return (position, rotation);

                return (parent.TransformPoint(data.Position), parent.rotation * data.Rotation);
            }

            bool TryProcessDeferredComponents(SaveablePrefabData data, GameObject target, string context)
            {
                if (data == null)
                    return false;

                string instanceId = data.InstanceID;
                if (string.IsNullOrEmpty(instanceId))
                {
                    Logger.Log($"{context}: Prefab data is missing an InstanceID; skipping deferred component processing.", LogCategory.PrefabManager, LogLevel.Warning);
                    return false;
                }

                if (target == null)
                {
                    if (verboseLoadDiagnostics)
                        Logger.Log($"{context}: Target instance for '{instanceId}' is not available; deferred components remain queued.", LogCategory.PrefabManager, LogLevel.Info);
                    return false;
                }

                var componentManager = ComponentManager.Instance;
                if (componentManager == null)
                {
                    if (verboseLoadDiagnostics)
                        Logger.Log($"{context}: ComponentManager is not available; deferred components will be retried for '{instanceId}'.", LogCategory.PrefabManager, LogLevel.Info);
                    return false;
                }

                componentManager.ProcessDeferredComponentsForGameObject(instanceId);
                if (verboseLoadDiagnostics)
                    Logger.Log($"{context}: Requested deferred component processing for '{instanceId}'.", LogCategory.PrefabManager, LogLevel.Info);
                return true;
            }

            void RegisterRestoredPrefab(SaveablePrefab prefab)
            {
                if (prefab == null || string.IsNullOrEmpty(prefab.UniqueID))
                    return;

                PrefabLoadDiagnosticCounters.RecordRegistrationOwnerProbe();
                SaveablePrefab registrationOwner =
                    loadContext.GetOrAddRegistrationOwner(
                        prefab.UniqueID,
                        prefab);

                if (registrationOwner == prefab)
                {
                    if (loadContext.RegisteredInstances.Add(prefab))
                    {
                        AddIndexedRegistration(prefab);
                    }
                }
                else if (verboseLoadDiagnostics)
                {
                    Logger.Log(
                        $"{SaveFlowDiagPrefix} Restore registration retained first owner for ID '{prefab.UniqueID}'. Existing={DescribePrefab(registrationOwner)} Incoming={DescribePrefab(prefab)}",
                        LogCategory.PrefabManager,
                        LogLevel.Warning);
                }
                else if (registrationOwner != prefab &&
                         Logger.ShouldLog(
                             LogCategory.PrefabManager,
                             LogLevel.Warning))
                {
                    Logger.Log(
                        $"Prefab restore encountered duplicate InstanceID '{prefab.UniqueID}'; the first serialization owner was retained.",
                        LogCategory.PrefabManager,
                        LogLevel.Warning);
                }

                if (registrationOwner != prefab)
                {
                    loadContext.RegisteredInstances.Remove(prefab);
                    RemoveIndexedRegistration(prefab);
                }

                // Preserve the existing current-instance behavior independently from the
                // first-owner serialization-list behavior.
                instantiatedPrefabs[prefab.UniqueID] = prefab.gameObject;
                loadContext.Bind(prefab.UniqueID, prefab.gameObject, prefab);
            }

		            IEnumerator InstantiatePrefabInternal(SaveablePrefabData prefabData)
	            {
	                if (prefabData == null)
	                {
	                    yield break;
	                }

		                if (verboseLoadDiagnostics)
		                {
			                Logger.Log($"{SaveFlowDiagPrefix} InstantiatePrefabInternal begin: {DescribePrefabData(prefabData)}",
				                LogCategory.PrefabManager,
				                LogLevel.Info);
		                }

                if (!string.IsNullOrEmpty(prefabData.InstanceID))
                {
                    if (!processedInstanceIds.Add(prefabData.InstanceID))
                    {
                        if (verboseLoadDiagnostics)
                            Logger.Log($"[CrystalSave][Instantiate] Skipping duplicate entry for InstanceID '{prefabData.InstanceID}' (PrefabID '{prefabData.PrefabID ?? "<null>"}')", LogCategory.PrefabManager, LogLevel.Info);
                        yield break;
                    }
                }

                GameObject existing = null;
                SaveablePrefab existingSaveable = null;
                if (!string.IsNullOrEmpty(prefabData.InstanceID) &&
                    instantiatedPrefabs.TryGetValue(
                        prefabData.InstanceID,
                        out GameObject tracked) &&
                    tracked == null)
                {
                    instantiatedPrefabs.Remove(prefabData.InstanceID);
                    if (verboseLoadDiagnostics)
                        Logger.Log($"PrefabManager.InstantiatePrefabInternal: Removed stale mapping for InstanceID '{prefabData.InstanceID}'.", LogCategory.PrefabManager, LogLevel.Info);
                }

                while (loadContext.TryResolve(
                           prefabData,
                           out existing,
                           out existingSaveable) &&
                       !IsValidReuseTarget(
                           existing,
                           existingSaveable,
                           prefabData,
                           out existingSaveable,
                           out var rejectedReuseReason))
                {
                    if (verboseLoadDiagnostics)
                    {
                        Logger.Log(
                            $"{SaveFlowDiagPrefix} InstantiatePrefabInternal rejected reuse candidate for {DescribePrefabData(prefabData)} -> " +
                            $"existing name='{existing.name}', reason={rejectedReuseReason}. Falling back to instantiation.",
                            LogCategory.PrefabManager,
                            LogLevel.Warning);
                    }
                    else if (Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Warning))
                    {
                        Logger.Log(
                            $"Prefab restore rejected the indexed candidate for InstanceID '{prefabData.InstanceID ?? "<empty>"}': {rejectedReuseReason}.",
                            LogCategory.PrefabManager,
                            LogLevel.Warning);
                    }

                    RemovePrefabMappingIfOwned(prefabData.InstanceID, existing);
                    loadContext.RejectResolution(prefabData, existing);
                    existing = null;
                    existingSaveable = null;
                }

                if (existing == null && prefabData.ReuseSceneInstanceOnLoad && verboseLoadDiagnostics)
                {
                    Logger.Log(
                        $"PrefabManager.InstantiatePrefabsCoroutine: Requested reuse for UniqueID '{prefabData.InstanceID ?? "<null>"}' but no scene instance was found. " +
                        "Falling back to instantiation.",
                        LogCategory.PrefabManager,
                        LogLevel.Info);
                }

		                if (existing != null)
		                {
		                    if (verboseLoadDiagnostics)
			                    {
				                    Logger.Log(
					                    $"{SaveFlowDiagPrefix} InstantiatePrefabInternal existing instance resolved for data {DescribePrefabData(prefabData)} -> existing name='{existing.name}'.",
					                    LogCategory.PrefabManager,
					                    LogLevel.Info);
		                    }

                    // Treat an existing scene instance as reuse either when the save-data requests it
                    // or when the component on the instance is configured to reuse scene instances.
                    bool reuseRequested = prefabData.ReuseSceneInstanceOnLoad;
                    bool reuseBySceneFlag = false;
                    bool existingIsSceneBacked = false;
	                    if (existingSaveable != null)
	                    {
		                    SaveablePrefab existingSp = existingSaveable;
	                        existingIsSceneBacked = IsSceneBackedReuseCandidate(existingSp);
	                        reuseBySceneFlag = existingSp.ReuseSceneInstanceOnLoad && existingIsSceneBacked;
                    }

	                    if ((reuseRequested && existingIsSceneBacked) || reuseBySceneFlag)
	                    {
		                        if (!string.IsNullOrEmpty(prefabData.InstanceID))
		                        {
		                            assetClaimedSceneReuseIDs.Add(prefabData.InstanceID);
		                        }

			                        bool applySavedTransformForReuse =
			                            string.IsNullOrEmpty(prefabData.InstanceID) ||
			                            !assetClaimedSkipTransformIDs.Contains(prefabData.InstanceID);
			                        ApplySceneReuseSavedIdentityState(existing, prefabData, applySavedTransformForReuse);

			                        if (!string.IsNullOrEmpty(prefabData.InstanceID))
			                        {
				                        instantiatedPrefabs[prefabData.InstanceID] = existing;
				                        loadContext.Bind(prefabData.InstanceID, existing, existingSaveable);
			                        }

	                        SaveablePrefab reusedSaveable = existingSaveable;
	                        if (reusedSaveable != null)
                        {
                            // Restore tracking flags BEFORE SetUniqueID.
                            // SetUniqueID → SaveableComponent.OverrideGameObjectID → RegisterWithSaveManager
                            // → RefreshPrefabManagementFlag(), which reads TrackComponentBlobs.
                            // If TrackComponentBlobs isn't restored from save data yet, the prefab asset's
                            // default value is used instead, potentially blocking component registration
                            // and causing saved data to go to QUEUING PENDING (never resolved).
                            reusedSaveable.TrackAddedComponents = prefabData.TrackAddedComponents;
                            reusedSaveable.TrackComponentBlobs = prefabData.TrackComponentBlobs;
                            reusedSaveable.TrackMaterialOverrides = prefabData.TrackMaterialOverrides;
                            reusedSaveable.TrackChildStateOverrides = prefabData.TrackChildStateOverrides;
                            reusedSaveable.TrackChildTransformOverrides = prefabData.TrackChildTransformOverrides;
                            reusedSaveable.TrackSkinnedMeshOverrides = prefabData.TrackSkinnedMeshOverrides;
                            reusedSaveable.TrackBlendshapeOverrides = prefabData.TrackBlendshapeOverrides;
                            reusedSaveable.TrackTextureOverrides = prefabData.TrackTextureOverrides;
                            reusedSaveable.TrackParticleSnapshots = prefabData.TrackParticleSnapshots;
                            reusedSaveable.TrackColliderSettings = prefabData.TrackColliderSettings;

	                            string previousId = reusedSaveable.UniqueID;
	                            reusedSaveable.SetUniqueIDForRestore(
		                            prefabData.InstanceID,
		                            loadContext.SaveableComponentBuffer);
	                            RemovePrefabMappingIfOwned(previousId, existing);
	                            loadContext.UnbindIfOwned(previousId, existing);
	                            loadContext.RebindRegistrationOwner(
		                            reusedSaveable,
		                            previousId,
		                            prefabData.InstanceID);
	                            loadContext.Bind(prefabData.InstanceID, existing, reusedSaveable);
                                RegisterRestoredPrefab(reusedSaveable);
	                            reusedSaveable.SetHomeScene(prefabData.HomeScene);
	                            reusedSaveable.LoadPriority = prefabData.LoadPriority;
	                            reusedSaveable.DeferLowPriorityUntilRequested = prefabData.DeferLowPriorityUntilRequested;
	                            reusedSaveable.DisablePooling = prefabData.DisablePooling;
	                            if (prefabData.EnableCrossSceneStateHandoff)
	                            {
	                                reusedSaveable.EnableCrossSceneStateHandoff = prefabData.EnableCrossSceneStateHandoff;
	                                reusedSaveable.HandoffToSceneBakedPrefabs = prefabData.HandoffToSceneBakedPrefabs;
	                                reusedSaveable.HandoffTransform = prefabData.HandoffTransform;
	                                reusedSaveable.HandoffRuntimeModifications = prefabData.HandoffRuntimeModifications;
	                            }

	                            if (!string.IsNullOrEmpty(prefabData.GameObjectName))
                            {
                                existing.name = prefabData.GameObjectName;
                            }

                            // Apply runtime modifications first (which may include active state changes)
	                            bool applyRuntimeModsForReuse =
	                                string.IsNullOrEmpty(prefabData.InstanceID) ||
	                                !assetClaimedSkipRuntimeModIDs.Contains(prefabData.InstanceID);
	                            if (applyRuntimeModsForReuse &&
	                                prefabData.RuntimeModificationData != null &&
	                                prefabData.RuntimeModificationData.Length > 0)
	                            {
	                                reusedSaveable.ApplyRuntimeModifications(prefabData.RuntimeModificationData);
	                            }

                            PersistentManager.MakePersistent(existing, reusedSaveable.KeepAcrossScenes);

                            // Restore visibility settings from save data.
                            // PersistentVisibilityController is a plain MonoBehaviour (not a
                            // SaveableComponent), so its data lives exclusively in
                            // SaveablePrefabData.VisibilitySettingsData. Without this call,
                            // the controller falls back to whatever Awake() initialized
                            // (which may be empty defaults), causing the prefab to appear
                            // visible in all scenes instead of only its configured scenes.
                            bool applyVisibility = prefabData.UsesOptimizationFlags ? prefabData.HasVisibilityData : true;
                            if (applyVisibility && prefabData.VisibilitySettingsData != null && prefabData.VisibilitySettingsData.Length > 0)
                            {
                                if (existing.TryGetComponent(
                                        out PersistentVisibilityController pvc))
                                {
                                    pvc.DeserializeAndStoreSettings(
                                        prefabData.VisibilitySettingsData);
                                }
                            }
                        }

                        // Apply saved active state AFTER runtime modifications to ensure it takes precedence
	                        if (prefabData.ActiveSelfAtSave.HasValue)
	                        {
	                            if (verboseLoadDiagnostics)
		                            Logger.Log($"[CrystalSave][ActiveState] Setting '{existing.name}' active state to {prefabData.ActiveSelfAtSave.Value} (was {existing.activeSelf}) for InstanceID '{prefabData.InstanceID}'", LogCategory.PrefabManager, LogLevel.Info);
	                            existing.SetActive(prefabData.ActiveSelfAtSave.Value);
	                            if (verboseLoadDiagnostics)
		                            Logger.Log($"[CrystalSave][ActiveState] After SetActive: '{existing.name}' active state is now {existing.activeSelf}", LogCategory.PrefabManager, LogLevel.Info);
                            
                            // Also update the GameObjectTracker to prevent conflicts with ApplyGameObjectActiveStates
                            if (Owner?.GameObjectTracker != null)
                            {
                                // Try to get the UniqueID from RememberGameObject component
                                string gameObjectId = null;
                                if (existing.TryGetComponent<RememberGameObject>(out var rememberGO))
                                {
                                    gameObjectId = rememberGO.GameObjectUniqueID;
                                }
                                else if (existing.TryGetComponent<UniqueID>(out var uniqueIdComp))
                                {
                                    gameObjectId = uniqueIdComp.ID;
                                }
                                
	                                if (!string.IsNullOrEmpty(gameObjectId))
	                                {
	                                    if (verboseLoadDiagnostics)
		                                    Logger.Log($"[CrystalSave][ActiveState] Updating GameObjectTracker for GameObject ID '{gameObjectId}' to active state {prefabData.ActiveSelfAtSave.Value}", LogCategory.PrefabManager, LogLevel.Info);
	                                    Owner.GameObjectTracker.UpdateActiveState(gameObjectId, prefabData.ActiveSelfAtSave.Value);
                                }
                            }
                        }

                        // ApplySceneReuseSavedIdentityState and runtime modifications may rename
                        // or deactivate the object. Refresh only aliases still owned by this object,
                        // so old names cannot resolve to a restored instance.
                        loadContext.RefreshActiveNameAliases(existing);

                        TryProcessDeferredComponents(prefabData, existing,
                            "PrefabManager.InstantiatePrefabsCoroutine (reuse)");
	                        if (verboseLoadDiagnostics)
	                        {
		                        Logger.Log(
			                        $"PrefabManager.InstantiatePrefabsCoroutine: Reused scene instance for UniqueID '{prefabData.InstanceID ?? "<null>"}' " +
			                        $"(PrefabID '{prefabData.PrefabID ?? "<null>"}').",
			                        LogCategory.PrefabManager,
			                        LogLevel.Info);
	                        }
            yield break;
                    }

		                    if (clearExistingPrefabs)
		                    {
		                        loadContext.RemoveMappingsOwnedBy(
			                        instantiatedPrefabs,
			                        existing);
		                        loadContext.RemoveObject(existing);

		                        if (existingSaveable != null && ShouldUsePoolingFor(existingSaveable))
		                        {
		                            SaveablePrefabPoolCache.TryDespawn(
		                            existingSaveable,
		                            GetPoolSizeForPrefabID(existingSaveable.PrefabAssetID),
		                            true);
	                        }
                        else
	                        {
	                            DestroyHelper.DestroyWithLogging(existing,
		                                "PrefabManager.InstantiatePrefabsCoroutine: removing stale instance");
		                        }
		                        yield return null; // wait one frame for destruction/despawn
	                    }
	                    else
	                    {
	                        if (!string.IsNullOrEmpty(prefabData.InstanceID))
	                        {
		                        if (!instantiatedPrefabs.ContainsKey(prefabData.InstanceID))
			                        instantiatedPrefabs[prefabData.InstanceID] = existing;
		                        loadContext.Bind(prefabData.InstanceID, existing, existingSaveable);
	                        }
                        TryProcessDeferredComponents(prefabData, existing,
                            "PrefabManager.InstantiatePrefabsCoroutine (reuse)");
                        yield break;
                    }
                }

                if (string.IsNullOrEmpty(prefabData.PrefabID))
                {
                    // Synthesized placeholders (scene objects) do not correspond to prefab assets.
                    yield break;
                }

		                GameObject originalPrefab = GetPrefabByID(prefabData.PrefabID);
		                if (!originalPrefab)
		                {
		                    if (verboseLoadDiagnostics)
		                    {
			                    Logger.Log($"{SaveFlowDiagPrefix} InstantiatePrefabInternal skipped; prefab asset not found for {DescribePrefabData(prefabData)}",
				                    LogCategory.PrefabManager,
				                    LogLevel.Warning);
		                    }
		                    else if (Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Warning))
		                    {
			                    Logger.Log(
				                    $"Prefab restore skipped InstanceID '{prefabData.InstanceID ?? "<empty>"}': prefab asset '{prefabData.PrefabID}' was not found.",
				                    LogCategory.PrefabManager,
				                    LogLevel.Warning);
		                    }
		                    // Skip this entry (prefab asset not found or removed from registry)
		                    yield break;
		                }

		                if (verboseLoadDiagnostics)
		                {
			                Logger.Log(
				                $"{SaveFlowDiagPrefix} InstantiatePrefabInternal spawning asset='{originalPrefab.name}' for {DescribePrefabData(prefabData)}.",
				                LogCategory.PrefabManager,
				                LogLevel.Info);
                }

                GameObject instance = null;
                originalPrefab.TryGetComponent(out SaveablePrefab assetSaveable);
                var initialSpawnPose = ResolveInitialSpawnPose(prefabData);
                object prefabLock = prefabLocks.GetOrAdd(originalPrefab, _ => new object());
                lock (prefabLock)
                {
                    bool wasLoading = assetSaveable != null && assetSaveable.IsLoading;
                    if (assetSaveable != null)
                        assetSaveable.SetLoading(true);

                    try
                    {
                        if (assetSaveable != null && ShouldUsePoolingFor(prefabData, assetSaveable))
                        {
                            var pooled = SaveablePrefabPoolCache.Get(assetSaveable, GetPoolSizeForPrefab(assetSaveable), true);
                            var spawned = pooled?.SpawnForRestore(initialSpawnPose.position, initialSpawnPose.rotation);
                            instance = spawned != null ? spawned.gameObject : null;
                        }
                        else
                        {
                            instance = Instantiate(originalPrefab, initialSpawnPose.position, initialSpawnPose.rotation);
                        }
                    }
                    finally
                    {
                        if (assetSaveable != null)
                            assetSaveable.SetLoading(wasLoading);
                    }
                }

		                if (instance == null)
		                {
		                    if (verboseLoadDiagnostics)
		                    {
			                    Logger.Log($"{SaveFlowDiagPrefix} InstantiatePrefabInternal failed to instantiate asset for {DescribePrefabData(prefabData)}",
				                    LogCategory.PrefabManager,
				                    LogLevel.Warning);
		                    }
		                    else if (Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Warning))
		                    {
			                    Logger.Log(
				                    $"Prefab restore could not instantiate InstanceID '{prefabData.InstanceID ?? "<empty>"}'.",
				                    LogCategory.PrefabManager,
				                    LogLevel.Warning);
		                    }
		                    yield break;
		                }

		                if (verboseLoadDiagnostics)
		                {
			                Logger.Log(
				                $"{SaveFlowDiagPrefix} InstantiatePrefabInternal created instance name='{instance.name}' for {DescribePrefabData(prefabData)}.",
				                LogCategory.PrefabManager,
				                LogLevel.Info);
		                }

                // Use saved GameObject name if available, otherwise fall back to prefab name
                instance.name = !string.IsNullOrEmpty(prefabData.GameObjectName) ? prefabData.GameObjectName : originalPrefab.name;

                // Apply saved active state for scene-baked prefabs before any other setup
                if (prefabData.ActiveSelfAtSave.HasValue)
                {
                    instance.SetActive(prefabData.ActiveSelfAtSave.Value);
                }

                instance.TryGetComponent(out SaveablePrefab saveable);
                if (saveable != null)
                {
                    // This branch created a new instance from save data rather than reusing
                    // a scene-placed object, so it must not participate in scene reuse mapping.
                    saveable.MarkAsAddedAtRuntime();
                    saveable.ReuseSceneInstanceOnLoad = false;

                    // For procedural objects (TrackAddedComponents = true), do NOT set the original prefab asset.
                    // The blank prefab used for instantiation has no components, so comparing against it
                    // causes MissingComponentException when capturing mesh/material overrides.
                    // Procedural objects save everything as "added at runtime" anyway.
                    if (!prefabData.TrackAddedComponents)
                    {
                        saveable.SetOriginalPrefabAsset(originalPrefab);
                    }

                    // Restore tracking flags BEFORE SetUniqueID.
                    // SetUniqueID → SaveableComponent.OverrideGameObjectID → RegisterWithSaveManager
                    // → RefreshPrefabManagementFlag(), which reads TrackComponentBlobs.
                    // If TrackComponentBlobs isn't restored from save data yet, the prefab asset's
                    // default value is used instead, potentially blocking component registration
                    // and causing saved data to go to QUEUING PENDING (never resolved).
                    saveable.TrackAddedComponents = prefabData.TrackAddedComponents;
                    saveable.TrackComponentBlobs = prefabData.TrackComponentBlobs;
                    saveable.TrackMaterialOverrides = prefabData.TrackMaterialOverrides;
                    saveable.TrackChildStateOverrides = prefabData.TrackChildStateOverrides;
                    saveable.TrackChildTransformOverrides = prefabData.TrackChildTransformOverrides;
                    saveable.TrackSkinnedMeshOverrides = prefabData.TrackSkinnedMeshOverrides;
                    saveable.TrackBlendshapeOverrides = prefabData.TrackBlendshapeOverrides;
                    saveable.TrackTextureOverrides = prefabData.TrackTextureOverrides;
                    saveable.TrackParticleSnapshots = prefabData.TrackParticleSnapshots;
                    saveable.TrackColliderSettings = prefabData.TrackColliderSettings;

                    if (prefabData.TrackAddedComponents)
                    {
                        if (verboseLoadDiagnostics)
                            Logger.Log($"PrefabManager: Restored TrackAddedComponents=true for '{instance.name}'", LogCategory.PrefabManager, LogLevel.Info);
                    }

	                    saveable.SetHomeScene(prefabData.HomeScene);
	                    saveable.LoadPriority = prefabData.LoadPriority;
	                    saveable.DeferLowPriorityUntilRequested = prefabData.DeferLowPriorityUntilRequested;
	                    
	                    // Restore the saved DisablePooling setting to the component
		                    saveable.DisablePooling = prefabData.DisablePooling;

		                    saveable.RegisterWithSaveSystem = true;
		                    string previousId = saveable.UniqueID;
		                    saveable.SetUniqueIDForRestore(
			                    prefabData.InstanceID,
			                    loadContext.SaveableComponentBuffer);
		                    RemovePrefabMappingIfOwned(previousId, instance);
		                    loadContext.UnbindIfOwned(previousId, instance);
		                    loadContext.RebindRegistrationOwner(
			                    saveable,
			                    previousId,
			                    prefabData.InstanceID);
		                    RegisterRestoredPrefab(saveable);
		                    if (verboseLoadDiagnostics)
		                    {
			                    Logger.Log($"{SaveFlowDiagPrefix} InstantiatePrefabInternal registered restored instance: {DescribePrefab(saveable)} from {DescribePrefabData(prefabData)}",
				                    LogCategory.PrefabManager,
				                    LogLevel.Info);
		                    }
	                    PersistentManager.MakePersistent(instance, saveable.KeepAcrossScenes);

                    bool applyVisibility = prefabData.UsesOptimizationFlags ? prefabData.HasVisibilityData : true;

                    if (applyVisibility && prefabData.VisibilitySettingsData != null && prefabData.VisibilitySettingsData.Length > 0)
                    {
                        if (instance.TryGetComponent(
                                out PersistentVisibilityController pvc))
                        {
                            pvc.DeserializeAndStoreSettings(
                                prefabData.VisibilitySettingsData);
                        }
                    }

                    if (prefabData.RuntimeModificationData != null && prefabData.RuntimeModificationData.Length > 0)
                    {
                        saveable.ApplyRuntimeModifications(prefabData.RuntimeModificationData);
                    }
                }

                loadContext.RefreshActiveNameAliases(instance);

                // Mark this ID as freshly instantiated in this pass
                if (!string.IsNullOrEmpty(prefabData.InstanceID))
                    justInstantiatedIds.Add(prefabData.InstanceID);

                if (prefabData.HasRigidbody)
                {
                    PrefabLoadDiagnosticCounters.RecordRigidbodyProbe();
                    if (instance.TryGetComponent(out Rigidbody rb) && rb != null)
                    {
                        if (!string.IsNullOrEmpty(prefabData.InstanceID))
                            loadContext.CacheRigidbody(
                                prefabData.InstanceID,
                                rb);

                        if (!rb.isKinematic)
                        {
                            rb.angularVelocity = Vector3.zero;
#if UNITY_6000_0_OR_NEWER
                            rb.linearVelocity = Vector3.zero;
#else
                            rb.velocity = Vector3.zero;
#endif
                        }

                        rb.isKinematic = true;

                        if (verboseLoadDiagnostics)
                            Logger.Log($"Prefab '{instance.name}' forced Kinematic in first pass, will restore later.", LogCategory.PrefabManager, LogLevel.Info);
                    }
                }

                if (verboseLoadDiagnostics)
                    Logger.Log($"PrefabManager: Instantiated '{instance.name}' with ID '{prefabData.InstanceID}'.", LogCategory.PrefabManager, LogLevel.Info);

                TryProcessDeferredComponents(prefabData, instance,
                    "PrefabManager.InstantiatePrefabsCoroutine (instantiate)");

                prefabsProcessed++;
                if (batchSize > 0 && prefabsProcessed % batchSize == 0)
                    yield return null;
            }

            if (groupByScene)
            {
                var grouped = immediatePrefabs
                    .Where(pd => pd != null)
                    .GroupBy(pd => string.IsNullOrEmpty(pd.HomeScene) ? string.Empty : pd.HomeScene);

                foreach (var group in grouped)
                {
                    Scene previousActiveScene = SceneManager.GetActiveScene();
                    Scene targetScene = previousActiveScene;
                    string sceneName = group.Key;
                    
                    if (!string.IsNullOrEmpty(sceneName))
                    {
                        // Special handling for DontDestroyOnLoad - this isn't a real scene that can be found by SceneManager
                        if (sceneName == "DontDestroyOnLoad")
                        {
                            // Use the current active scene as target since DontDestroyOnLoad objects are created in the active scene
                            targetScene = previousActiveScene;
                        }
                        else
                        {
                            Scene homeScene = SceneManager.GetSceneByName(sceneName);
                            
                            if (!homeScene.IsValid() || !homeScene.isLoaded)
                            {
                                restoreQueue.QueuePendingRange(sceneName, group);
                                if (verboseLoadDiagnostics)
                                {
                                    try
                                    {
                                        var ids = string.Join(", ", group.Select(d => d?.InstanceID ?? "<null>").Distinct(StringComparer.Ordinal));
                                        string msg = $"PrefabManager.InstantiatePrefabsCoroutine: Deferring {group.Count()} prefab(s) for unloaded scene '{sceneName}'. InstanceIDs: {ids}";
                                        Logger.Log(msg, LogCategory.PrefabManager, LogLevel.Info);
                                    }
                                    catch { /* logging best-effort */ }
                                }
                                continue;
                            }
                            targetScene = homeScene;
                        }
                    }

                    IDisposable sceneOverride = targetScene != previousActiveScene
                        ? restoreLifetime.OverrideScene(targetScene) : null;

                    try
                    {
                        foreach (var pd in group)
                        {
                            IEnumerator instantiateRoutine = InstantiatePrefabInternal(pd);
                            while (instantiateRoutine.MoveNext())
                                yield return instantiateRoutine.Current;
                        }
                    }
                    finally
                    {
                        sceneOverride?.Dispose();
                    }
                }
            }
            else
            {
                foreach (var prefabData in immediatePrefabs)
                {
                    if (prefabData == null)
                        continue;

                    Scene previousActiveScene = SceneManager.GetActiveScene();
                    Scene targetScene = previousActiveScene;

                    if (!string.IsNullOrEmpty(prefabData.HomeScene))
                    {
                        // Special handling for DontDestroyOnLoad - this isn't a real scene that can be found by SceneManager
                        if (prefabData.HomeScene == "DontDestroyOnLoad")
                        {
                            // Use the current active scene as target since DontDestroyOnLoad objects are created in the active scene
                            targetScene = previousActiveScene;
                        }
                        else
                        {
                            Scene homeScene = SceneManager.GetSceneByName(prefabData.HomeScene);
                            
                            if (!homeScene.IsValid() || !homeScene.isLoaded)
                            {
                                restoreQueue.QueuePending(prefabData.HomeScene, prefabData);
                                if (verboseLoadDiagnostics)
                                {
                                    try
                                    {
                                        string msg = $"PrefabManager.InstantiatePrefabsCoroutine: Deferring InstanceID '{prefabData.InstanceID ?? "<null>"}' for unloaded scene '{prefabData.HomeScene}'.";
                                        Logger.Log(msg, LogCategory.PrefabManager, LogLevel.Info);
                                    }
                                    catch { /* logging best-effort */ }
                                }
                                continue;
                            }

                            targetScene = homeScene;
                        }
                    }

                    IDisposable sceneOverride = targetScene != previousActiveScene
                        ? restoreLifetime.OverrideScene(targetScene) : null;

                    try
                    {
                        IEnumerator instantiateRoutine = InstantiatePrefabInternal(prefabData);
                        while (instantiateRoutine.MoveNext())
                            yield return instantiateRoutine.Current;
                    }
                    finally
                    {
                        sceneOverride?.Dispose();
                    }
                }
            }
// ───────────────────────────────────────────────────────────────
            // Second pass: parent, transform, restore Animator & Rigidbody
            // ───────────────────────────────────────────────────────────────
            prefabsProcessed = 0;
            var parentResolver = new ParentResolver(instantiatedPrefabs);

            foreach (var prefabData in immediatePrefabs)
            {
                if (prefabData == null)
                    continue;

	                if (!string.IsNullOrEmpty(prefabData.InstanceID) &&
	                    instantiatedPrefabs.TryGetValue(prefabData.InstanceID, out var instance))
	                {
	                    if (instance == null)
	                    {
		                    instantiatedPrefabs.Remove(prefabData.InstanceID);
		                    continue;
	                    }

	                    TryProcessDeferredComponents(prefabData, instance,
	                        "PrefabManager.InstantiatePrefabsCoroutine (post-process)");

                    /* ── CharacterController safeguard ───────────────────── */
                    instance.TryGetComponent(out CharacterController cc);
                    bool ccWasEnabled = cc != null && cc.enabled;
                    if (ccWasEnabled) cc.enabled = false;

                    // 1) Parenting & transform – world-vs-local aware
	                    bool hasOptimizationFlags = prefabData.UsesOptimizationFlags;
	                    bool wasInstantiatedNow = !string.IsNullOrEmpty(prefabData.InstanceID) && justInstantiatedIds.Contains(prefabData.InstanceID);
	                    bool wasAssetClaimedSceneReuse = !string.IsNullOrEmpty(prefabData.InstanceID) && assetClaimedSceneReuseIDs.Contains(prefabData.InstanceID);
	                    bool skipAssetClaimedTransform = !string.IsNullOrEmpty(prefabData.InstanceID) && assetClaimedSkipTransformIDs.Contains(prefabData.InstanceID);
	                    // Always apply parent/transform for newly instantiated instances, regardless of optimization flags
	                    // Asset-claimed scene-baked instances may have optimization flags from the saved source
	                    // instance, not from the baked target currently in the scene. Apply the saved transform so
	                    // cross-scene scene-baked reuse does not fall back to the target scene's design-time pose.
	                    bool applyParent = !skipAssetClaimedTransform && (wasInstantiatedNow || wasAssetClaimedSceneReuse || (hasOptimizationFlags ? prefabData.HasParentOverride : true));
	                    bool applyTransform = !skipAssetClaimedTransform && (wasInstantiatedNow || wasAssetClaimedSceneReuse || (hasOptimizationFlags ? prefabData.HasTransformOverride : true));

                    Transform resolvedParent = instance.transform.parent;

                    if (applyParent)
                    {
                        resolvedParent = parentResolver.ResolveParent(prefabData);
                        if (resolvedParent != null)
                        {
                            instance.transform.SetParent(resolvedParent, false);
                        }
                        else
                        {
                            if (verboseLoadDiagnostics)
                                HandleMissingParent(instance);
                            instance.transform.SetParent(null, false);
                        }
                    }

                    if (applyTransform)
                    {
                        bool hasTargetParent = resolvedParent != null;
                        // If a parent has been resolved (either by explicit ParentID or via fingerprint),
                        // and the original save-data indicates the instance HAD a parent (by ParentID or fingerprint fields),
                        // then the stored Position/Rotation are LOCAL and must be applied as such. Otherwise, treat as world.
                        bool savedHadParent =
                            !string.IsNullOrEmpty(prefabData.ParentID) ||
                            !string.IsNullOrEmpty(prefabData.ParentStableKey) ||
                            !string.IsNullOrEmpty(prefabData.ParentPrefabAssetID);
                        bool useLocal = hasTargetParent && savedHadParent;

                        if (useLocal)
                        {
                            instance.transform.localPosition = prefabData.Position;
                            instance.transform.localRotation = prefabData.Rotation;
                            
                        }
                        else
                        {
                            instance.transform.position = prefabData.Position;
                            instance.transform.rotation = prefabData.Rotation;
                        }

                        instance.transform.localScale = prefabData.Scale;
                    }
                    // Re-apply saved active state in case downstream systems toggled it during post-processing
                    if (prefabData.ActiveSelfAtSave.HasValue && instance.activeSelf != prefabData.ActiveSelfAtSave.Value)
                    {
                        if (verboseLoadDiagnostics)
                            Logger.Log($"[CrystalSave][ActiveState-PostProcess] Setting '{instance.name}' active state to {prefabData.ActiveSelfAtSave.Value} (was {instance.activeSelf}) for InstanceID '{prefabData.InstanceID}'", LogCategory.PrefabManager, LogLevel.Info);
                        instance.SetActive(prefabData.ActiveSelfAtSave.Value);
                        
                        // Also update the GameObjectTracker to prevent conflicts with ApplyGameObjectActiveStates
                        if (Owner?.GameObjectTracker != null)
                        {
                            // Try to get the UniqueID from RememberGameObject component
                            string gameObjectId = null;
                            if (instance.TryGetComponent<RememberGameObject>(out var rememberGO))
                            {
                                gameObjectId = rememberGO.GameObjectUniqueID;
                            }
                            else if (instance.TryGetComponent<UniqueID>(out var uniqueIdComp))
                            {
                                gameObjectId = uniqueIdComp.ID;
                            }
                            
                            if (!string.IsNullOrEmpty(gameObjectId))
                            {
                                if (verboseLoadDiagnostics)
                                    Logger.Log($"[CrystalSave][ActiveState-PostProcess] Updating GameObjectTracker for GameObject ID '{gameObjectId}' to active state {prefabData.ActiveSelfAtSave.Value}", LogCategory.PrefabManager, LogLevel.Info);
                                Owner.GameObjectTracker.UpdateActiveState(gameObjectId, prefabData.ActiveSelfAtSave.Value);
                            }
                        }
                    }

                    if (ccWasEnabled) cc.enabled = true;

                    // 2) ── Animator snapshot restore ──────────────────
                    if (prefabData.HasAnimator)
                    {
                        if (instance.TryGetComponent(out Animator anim) &&
                            anim != null &&
                            anim.isActiveAndEnabled &&
                            anim.gameObject.activeInHierarchy)
                        {
                            anim.Play(prefabData.AnimatorStateHash, 0,
                                      prefabData.AnimatorNormalizedTime);
                            anim.Update(0f); // force evaluation this frame
                            if (verboseLoadDiagnostics)
                                Logger.Log($"PrefabManager: Restored Animator on '{instance.name}' " +
                                           $"state={prefabData.AnimatorStateHash:X}.", LogCategory.PrefabManager, LogLevel.Info);
                        }
                    }

                    // 3) ── AfterRestore callback ─────────────────────
                    if (loadContext.TryGetSaveable(instance, out SaveablePrefab saveable))
                    {
                        SaveablePrefab.RaiseAfterRestore(saveable);
                    }

                    // 4) Rigidbody restore — your existing block
                    if (prefabData.HasRigidbody)
                    {
                        if (!loadContext.Rigidbodies.TryGetValue(prefabData.InstanceID, out Rigidbody rb))
                        {
                            PrefabLoadDiagnosticCounters.RecordRigidbodyProbe();
                            if (instance.TryGetComponent(out rb) && rb != null)
                                loadContext.CacheRigidbody(
                                    prefabData.InstanceID,
                                    rb);
                        }

                        if (rb != null)
                        {
                            if (!syncTransformsAfterLoad)
                                Physics.SyncTransforms();

                            rb.isKinematic = prefabData.RigidbodyIsKinematic;
                            rb.useGravity  = prefabData.RigidbodyUseGravity;

                        #if UNITY_6000_0_OR_NEWER
                            rb.linearDamping    = prefabData.RigidbodyDrag;
                            rb.angularDamping   = prefabData.RigidbodyAngularDrag;
                        #else
                            rb.drag             = prefabData.RigidbodyDrag;
                            rb.angularDrag      = prefabData.RigidbodyAngularDrag;
                        #endif

                            if (!prefabData.RigidbodyIsKinematic)
                            {
                        #if UNITY_6000_0_OR_NEWER
                                rb.linearVelocity  = prefabData.RigidbodyVelocity;
                        #else
                                rb.velocity        = prefabData.RigidbodyVelocity;
                        #endif
                                rb.angularVelocity = prefabData.RigidbodyAngularVelocity;
                            }

                            if (verboseLoadDiagnostics)
                            {
                            #if UNITY_6000_0_OR_NEWER
                                Logger.Log($"PrefabManager: Restored Rigidbody on '{instance.name}' " +
                                        $"isKinematic={rb.isKinematic}, velocity={rb.linearVelocity}.",
                                        LogCategory.PrefabManager,
                                        LogLevel.Info);
                            #else
                                Logger.Log($"PrefabManager: Restored Rigidbody on '{instance.name}' " +
                                        $"isKinematic={rb.isKinematic}, velocity={rb.velocity}.",
                                        LogCategory.PrefabManager,
                                        LogLevel.Info);
                            #endif
                            }
                        }
                    }

                    // Colliders
                    if (prefabData.Colliders != null && prefabData.Colliders.Count > 0)
                    {
                        ApplyColliderSettings(instance, prefabData.Colliders);
                    }
                }

                prefabsProcessed++;
                if (batchSize > 0 && prefabsProcessed % batchSize == 0)
                    yield return null;
            }

            if (signalImmediateBatchComplete)
                OnImmediatePrefabBatchComplete?.Invoke();

            if (syncTransformsAfterLoad)
                Physics.SyncTransforms();

            if (verboseLoadDiagnostics)
                Logger.Log("PrefabManager: Prefab instantiation and setup completed.", LogCategory.PrefabManager, LogLevel.Info);

            // Finalize UniqueIDs for scene-backed prefabs that are intended to be reusable or
            // skipped when unchanged but still lack an ID (common after loading where generation
            // was deferred). This ensures subsequent saves always carry proper InstanceIDs.
            try
            {
                foreach (var sp in saveablePrefabs)
                {
                    if (sp == null) continue;
                    if (!IsSaveSystemParticipant(sp)) continue;
                    if (sp.IsAddedAtRuntime) continue;
                    if (!string.IsNullOrEmpty(sp.UniqueID)) continue;
                    bool wantsReuseOrSkip = sp.ReuseSceneInstanceOnLoad || sp.SkipSavingWhenUnchanged;
                    if (!wantsReuseOrSkip) continue;

                    string previousId = sp.UniqueID;
                    string finalizedId = Guid.NewGuid().ToString();
                    sp.SetUniqueIDForRestore(finalizedId, loadContext.SaveableComponentBuffer);
                    RemovePrefabMappingIfOwned(previousId, sp.gameObject);
                    loadContext.UnbindIfOwned(previousId, sp.gameObject);
                    loadContext.RebindRegistrationOwner(sp, previousId, finalizedId);
                    loadContext.Bind(finalizedId, sp.gameObject, sp);
                    if (sp.RegisterWithSaveSystem)
                    {
                        try { sp.RegisterForSaving(); } catch { /* ignore */ }
                    }
                }
            }
            catch { /* best-effort only */ }

            AllPrefabsInitialized = true;
            OnAllPrefabsInitialized?.Invoke();
            yield break;
        }

    public void ClearSaveablePrefabs(
        bool preserveDeferredQueue = false,
        IReadOnlyCollection<string> instanceIDsToPreserve = null,
        IReadOnlyDictionary<string, string> preserveInstanceIdToPrefabId = null)
    {
        ClearSaveablePrefabsInternal(
            preserveDeferredQueue,
            instanceIDsToPreserve,
            preserveInstanceIdToPrefabId,
            null);
    }

    private void ClearSaveablePrefabsInternal(
        bool preserveDeferredQueue,
        IReadOnlyCollection<string> instanceIDsToPreserve,
        IReadOnlyDictionary<string, string> preserveInstanceIdToPrefabId,
        PrefabLoadContext loadContext)
                {
            // Minimal tracing (Debug.Log) to avoid console flood via the plugin logger.
            bool emitClearDiagnostics =
                Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Info) &&
                (loadContext == null ||
                 Owner?.SaveSettings?.enableVerbosePrefabLoadDiagnostics == true);
            
	            var preserveLookup =
		            new HashSet<string>(StringComparer.Ordinal);
	            var preserveIdsByAsset =
		            new Dictionary<string, Queue<string>>(StringComparer.Ordinal);
	            var preserveIdsWithoutAsset = new Queue<string>();
	            if (instanceIDsToPreserve != null)
	            {
		            foreach (string id in instanceIDsToPreserve)
		            {
			            if (string.IsNullOrEmpty(id) ||
			                !preserveLookup.Add(id))
			            {
				            continue;
			            }

			            string assetId =
				            preserveInstanceIdToPrefabId != null &&
				            preserveInstanceIdToPrefabId.TryGetValue(
					            id,
					            out string mappedAssetId)
					            ? mappedAssetId ?? string.Empty
					            : string.Empty;
			            if (string.IsNullOrEmpty(assetId))
			            {
				            preserveIdsWithoutAsset.Enqueue(id);
			            }
			            else
			            {
				            if (!preserveIdsByAsset.TryGetValue(
					                assetId,
					                out Queue<string> assetQueue))
				            {
					            assetQueue = new Queue<string>();
					            preserveIdsByAsset.Add(assetId, assetQueue);
				            }
				            assetQueue.Enqueue(id);
			            }
		            }
	            }

	            var claimedPreserveIds =
		            new HashSet<string>(StringComparer.Ordinal);

            void AssignPreservedId(SaveablePrefab prefab, string uniqueId)
            {
                if (prefab == null || string.IsNullOrEmpty(uniqueId))
                    return;

                if (loadContext == null)
                {
                    prefab.SetUniqueID(uniqueId);
                    return;
                }

                string previousId = prefab.UniqueID;
                prefab.SetUniqueIDForRestore(uniqueId, loadContext.SaveableComponentBuffer);
                RemovePrefabMappingIfOwned(previousId, prefab.gameObject);
                loadContext.UnbindIfOwned(previousId, prefab.gameObject);
                loadContext.RebindRegistrationOwner(prefab, previousId, uniqueId);
                loadContext.Bind(uniqueId, prefab.gameObject, prefab);
            }

	            bool TryClaimFromQueue(
		            Queue<string> queue,
		            out string claimedId)
	            {
		            while (queue != null && queue.Count > 0)
		            {
			            string candidate = queue.Dequeue();
			            if (claimedPreserveIds.Add(candidate))
			            {
				            claimedId = candidate;
				            return true;
			            }
		            }

		            claimedId = null;
		            return false;
	            }

	            string ClaimPreserveIdForPrefab(SaveablePrefab prefab)
	            {
		            PrefabLoadDiagnosticCounters.RecordPreserveIdClaimProbe();
		            if (!IsSaveSystemParticipant(prefab))
			            return null;

		            string assetId = prefab.PrefabAssetID ?? string.Empty;
		            if (!string.IsNullOrEmpty(assetId) &&
		                preserveIdsByAsset.TryGetValue(
			                assetId,
			                out Queue<string> assetQueue) &&
		                TryClaimFromQueue(assetQueue, out string exactAssetId))
		            {
			            if (emitClearDiagnostics)
			            {
				            Logger.Log(
					            $"[CrystalSave][ClearSaveablePrefabs] Claimed InstanceID '{exactAssetId}' for scene prefab '{prefab.name}' (asset '{assetId}')",
					            LogCategory.PrefabManager,
					            LogLevel.Info);
			            }
			            return exactAssetId;
		            }

		            if (TryClaimFromQueue(
			                preserveIdsWithoutAsset,
			                out string fallbackId))
		            {
			            if (emitClearDiagnostics)
			            {
				            Logger.Log(
					            $"[CrystalSave][ClearSaveablePrefabs] Fallback-claimed InstanceID '{fallbackId}' for prefab '{prefab.name}'",
					            LogCategory.PrefabManager,
					            LogLevel.Info);
			            }
			            return fallbackId;
		            }

		            return null;
	            }

	            var preservedPrefabs = new List<SaveablePrefab>();
	            var preservedPrefabSet = new HashSet<SaveablePrefab>();
	            var preservedInstances =
		            new Dictionary<string, GameObject>(StringComparer.Ordinal);
	            SaveablePrefab[] trackedPrefabSnapshot =
		            saveablePrefabs.ToArray();
	            var trackedPrefabSet =
		            new HashSet<SaveablePrefab>(trackedPrefabSnapshot);

	            void PreservePrefab(SaveablePrefab prefab)
	            {
		            if (prefab != null && preservedPrefabSet.Add(prefab))
			            preservedPrefabs.Add(prefab);
	            }

            // Anchor any existing instanceID->GameObject mappings we already know about
            // so subsequent scans don't reassign the same ID to a different scene object
            // on a second load within play mode.
            if (instantiatedPrefabs.Count > 0)
            {
                foreach (var kv in instantiatedPrefabs)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                    if (!preserveLookup.Contains(kv.Key)) continue; // only anchor what caller asked to preserve
                    if (!preservedInstances.ContainsKey(kv.Key))
                        preservedInstances[kv.Key] = kv.Value;
                }
                foreach (var anchoredId in preservedInstances.Keys)
                    claimedPreserveIds.Add(anchoredId);
            }
                        int destroyedCount = 0;

            // If nothing is currently tracked but we have things to preserve, attempt a targeted
            // scene scan to preserve scene-placed prefabs (ReuseSceneInstanceOnLoad) and any with
            // matching UniqueIDs. This avoids losing preservation when a subsequent load phase
            // invokes a second clear before registration repopulates the list.
	            if (trackedPrefabSnapshot.Length == 0 &&
	                preserveLookup.Count > 0)
	            {
                try
                {
                    var scenePrefabs = loadContext?.ScenePrefabs ??
                                       UnityObjectSearch.FindIncludingInactive<SaveablePrefab>();
	                    foreach (var sp in scenePrefabs)
	                    {
	                        PrefabLoadDiagnosticCounters.RecordClearCandidateProbe();
	                        if (sp == null ||
	                            loadContext?.IsDestroyedOwner(sp.gameObject) == true)
	                        {
		                        continue;
	                        }
	                        if (!IsSaveSystemParticipant(sp)) continue;
                        string uid = sp.UniqueID;
                        bool sceneReuse = sp.ReuseSceneInstanceOnLoad && IsSceneBackedReuseCandidate(sp);
                        bool matchByList = !string.IsNullOrEmpty(uid) && preserveLookup.Contains(uid);
                        if (!matchByList && !sceneReuse) continue;

                        // If already has an ID and it's in the preserve list, take it and remove from available
	                        if (matchByList)
	                        {
	                            claimedPreserveIds.Add(uid);
	                            PreservePrefab(sp);
                            if (!preservedInstances.ContainsKey(uid))
                                preservedInstances[uid] = sp.gameObject;
                            
                            continue;
                        }

                        // sceneReuse with empty uid → assign one of the remaining IDs if possible
                        if (string.IsNullOrEmpty(uid))
                        {
                            var assignId = ClaimPreserveIdForPrefab(sp);
                            if (!string.IsNullOrEmpty(assignId))
                            {
                                AssignPreservedId(sp, assignId);
                                if (sp.RegisterWithSaveSystem)
                                {
                                    try { sp.RegisterForSaving(); } catch { /* ignore */ }
	                                }
	                                claimedPreserveIds.Add(assignId);
	                                PreservePrefab(sp);
                                preservedInstances[assignId] = sp.gameObject;
                                
                                continue;
                            }
                        }

                        // No ID available to preserve this candidate → treat as stray and destroy
                        if (string.IsNullOrEmpty(uid))
                        {
                            if (Owner != null)
                            {
	                            if (loadContext != null)
		                            Owner
			                            .SoftUnregisterGameObjectForPrefabBatch(
				                            sp.gameObject);
	                            else
		                            Owner
			                            .SoftUnregisterGameObject(
				                            sp.gameObject);
                            }
                            loadContext?.RemoveObject(
	                            sp.gameObject,
	                            managerCollectionsWillBeRebuilt: true);
                            if (ShouldUsePoolingFor(sp))
                                SaveablePrefabPoolCache.TryDespawn(sp, GetPoolSizeForPrefab(sp), true);
                            else
                                DestroyHelper.DestroyWithLogging(sp.gameObject, "PrefabManager.ClearSaveablePrefabs(repopulate: stray)");
                            if (emitClearDiagnostics)
                                Logger.Log($"[CrystalSave] ClearSaveablePrefabs: (repopulate) destroy stray scene-reuse candidate '{sp.name}' with empty uid (no available preserve ID)", LogCategory.PrefabManager, LogLevel.Info);
                        }
                        else if (!preserveLookup.Contains(uid))
                        {
                            var assignId = ClaimPreserveIdForPrefab(sp);
                            if (!string.IsNullOrEmpty(assignId) && !string.Equals(assignId, uid, StringComparison.Ordinal))
                            {
                                AssignPreservedId(sp, assignId);
                                if (sp.RegisterWithSaveSystem)
                                {
                                    try { sp.RegisterForSaving(); } catch { /* ignore */ }
	                                }
	                                claimedPreserveIds.Add(assignId);
	                                PreservePrefab(sp);
                                preservedInstances[assignId] = sp.gameObject;
                                
                                continue;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[CrystalSave] ClearSaveablePrefabs: scene-scan preservation fallback failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.PrefabManager, LogLevel.Warning);
                }

                // Rebuild tracked and instance maps from preserved without destroying anything else
                ReplaceRegistrations(preservedPrefabs, preservedInstances);

	                restoreQueue.Clear(preserveDeferredQueue);
                initializedPrefabs = preservedPrefabs.Count;
                AllPrefabsInitialized = false;
                
                return;
            }

	    foreach (var prefab in trackedPrefabSnapshot)
	                        {
	                                PrefabLoadDiagnosticCounters.RecordClearCandidateProbe();
	                                if (prefab == null)
                                        continue;
                                if (loadContext?.IsDestroyedOwner(
	                                    prefab.gameObject) == true)
	                            {
		                            continue;
	                            }

                                string uniqueID = prefab.UniqueID;
                                if (!IsSaveSystemParticipant(prefab))
                                {
                                        RemovePrefabMappingIfOwned(uniqueID, prefab.gameObject);
                                        continue;
                                }

                                bool shouldPreserve = !string.IsNullOrEmpty(uniqueID) && preserveLookup.Contains(uniqueID);
                                
                                // Also preserve scene prefabs that have ReuseSceneInstanceOnLoad = true
                                // These are design-time placed prefabs that should not be destroyed during clearing
                bool isScenePrefabForReuse = prefab.ReuseSceneInstanceOnLoad && IsSceneBackedReuseCandidate(prefab);
                // Preserve scene-placed prefabs that skip saving when unchanged.
                // Absence of a save entry means "keep default scene object" not "destroy it".
                bool preserveSceneUnchanged = IsSceneBackedReuseCandidate(prefab) && prefab.SkipSavingWhenUnchanged;
                                
    bool needsAlignment = (isScenePrefabForReuse || preserveSceneUnchanged) && (!shouldPreserve || string.IsNullOrEmpty(uniqueID));

    if (needsAlignment)
    {
        string claimedId = ClaimPreserveIdForPrefab(prefab);
        if (!string.IsNullOrEmpty(claimedId) && !string.Equals(uniqueID, claimedId, StringComparison.Ordinal))
        {
            if (emitClearDiagnostics)
                Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Assigning preserved ID '{claimedId}' to scene prefab '{prefab.name}' (was '{uniqueID ?? "<empty>"}')", LogCategory.PrefabManager, LogLevel.Info);
            uniqueID = claimedId;
            AssignPreservedId(prefab, claimedId);
            if (prefab.RegisterWithSaveSystem)
            {
                try { prefab.RegisterForSaving(); }
                catch { /* ignore */ }
            }
            shouldPreserve = true;
        }

        if (!string.IsNullOrEmpty(uniqueID))
            claimedPreserveIds.Add(uniqueID);
    }

    if (shouldPreserve || isScenePrefabForReuse || preserveSceneUnchanged)
                                {
                    if (emitClearDiagnostics)
                        Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Preserving tracked prefab '{prefab.name}' (ID '{uniqueID ?? "<empty>"}') reason: preserve={shouldPreserve}, reuse={isScenePrefabForReuse}, unchanged={preserveSceneUnchanged}", LogCategory.PrefabManager, LogLevel.Info);
	                                        PreservePrefab(prefab);

                    if (!string.IsNullOrEmpty(uniqueID))
                        claimedPreserveIds.Add(uniqueID);

                    if (!string.IsNullOrEmpty(uniqueID) && !preservedInstances.ContainsKey(uniqueID))
                                        {
                                                GameObject trackedInstance = instantiatedPrefabs.TryGetValue(uniqueID, out var existingInstance) && existingInstance != null
                                                        ? existingInstance
                                                        : prefab.gameObject;
                                                preservedInstances[uniqueID] = trackedInstance;
                                        }

            
                                        continue;
                                }

                                if (Owner != null)
                                {
	                                if (loadContext != null)
		                                Owner
			                                .SoftUnregisterGameObjectForPrefabBatch(
				                                prefab.gameObject);
	                                else
		                                Owner
			                                .SoftUnregisterGameObject(
				                                prefab.gameObject);
                                }

                                loadContext?.RemoveObject(
	                                prefab.gameObject,
	                                managerCollectionsWillBeRebuilt: true);
                                if (ShouldUsePoolingFor(prefab))
                                {
                                        SaveablePrefabPoolCache.TryDespawn(prefab, GetPoolSizeForPrefab(prefab), true);
                                }
                                else
                                {
                    if (emitClearDiagnostics)
                        Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Destroying tracked prefab '{prefab.name}' (ID '{uniqueID ?? "<empty>"}')", LogCategory.PrefabManager, LogLevel.Info);
                                        DestroyHelper.DestroyWithLogging(
                                                prefab.gameObject,
                                                "PrefabManager.ClearSaveablePrefabs()"
                                        );
                                }

                                if (!string.IsNullOrEmpty(uniqueID))
                                        instantiatedPrefabs.Remove(uniqueID);

                                destroyedCount++;
                
                        }

            // After processing tracked prefabs, scan the scene for any SaveablePrefabs that are
            // currently NOT tracked. If they are intended for reuse or explicitly preserved,
            // add them to the preserved set; otherwise, proactively remove them to avoid
            // lingering duplicates across subsequent loads.
            try
            {
                var scenePrefabs = loadContext?.ScenePrefabs ??
                                   UnityObjectSearch.FindIncludingInactive<SaveablePrefab>();
	                foreach (var sp in scenePrefabs)
	                {
	                    PrefabLoadDiagnosticCounters.RecordClearCandidateProbe();
	                    if (sp == null ||
	                        loadContext?.IsDestroyedOwner(sp.gameObject) == true)
	                        continue;

	                    // Already accounted for
	                    if (preservedPrefabSet.Contains(sp))
	                        continue;

	                    bool wasTracked = trackedPrefabSet.Contains(sp);
                    if (wasTracked)
                        continue; // handled in the main loop

                    string uid = sp.UniqueID;
                    if (!IsSaveSystemParticipant(sp))
                    {
                        RemovePrefabMappingIfOwned(uid, sp.gameObject);
                        continue;
                    }

                    bool preserveByList = !string.IsNullOrEmpty(uid) && preserveLookup.Contains(uid);
                    bool preserveByReuse = sp.ReuseSceneInstanceOnLoad && IsSceneBackedReuseCandidate(sp);
                    bool preserveByUnchanged = IsSceneBackedReuseCandidate(sp) && sp.SkipSavingWhenUnchanged;

                    if (preserveByList)
                    {
	                        if (emitClearDiagnostics)
	                            Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Preserving stray prefab '{sp.name}' by explicit list match (ID '{uid}')", LogCategory.PrefabManager, LogLevel.Info);
	                        claimedPreserveIds.Add(uid);
	                        PreservePrefab(sp);
	                        if (!string.IsNullOrEmpty(uid) && !preservedInstances.ContainsKey(uid))
	                            preservedInstances[uid] = sp.gameObject;
	                        
                        continue;
                    }

                    if (preserveByReuse)
                    {
                        string assignId = uid;
                        if (string.IsNullOrEmpty(assignId) || !preserveLookup.Contains(assignId))
                        {
                            assignId = ClaimPreserveIdForPrefab(sp);
                            if (!string.IsNullOrEmpty(assignId) && !string.Equals(assignId, uid, StringComparison.Ordinal))
                            {
                                AssignPreservedId(sp, assignId);
                                if (sp.RegisterWithSaveSystem)
                                {
                                    try { sp.RegisterForSaving(); } catch { /* ignore */ }
                                }
                                uid = assignId;
                            }
                        }

	                        if (!string.IsNullOrEmpty(uid))
	                        {
	                            claimedPreserveIds.Add(uid);
	                            PreservePrefab(sp);
	                            preservedInstances[uid] = sp.gameObject;
                            if (emitClearDiagnostics)
                                Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Preserving stray prefab '{sp.name}' via reuse assignment (ID '{uid}')", LogCategory.PrefabManager, LogLevel.Info);
                            
                            continue;
                        }
                    }

                    // Preserve scene-placed prefabs that skip saving when unchanged.
                    // Even with empty uid and no preserve list, they represent default scene state.
                    if (preserveByUnchanged)
                    {
                        if (string.IsNullOrEmpty(uid) || !preserveLookup.Contains(uid))
                        {
                            var assignId = ClaimPreserveIdForPrefab(sp);
                            if (!string.IsNullOrEmpty(assignId))
                            {
                                AssignPreservedId(sp, assignId);
                                if (sp.RegisterWithSaveSystem)
                                {
                                    try { sp.RegisterForSaving(); } catch { /* ignore */ }
                                }
                                uid = assignId;
                            }
                        }

	                        if (!string.IsNullOrEmpty(uid))
	                                claimedPreserveIds.Add(uid);
	                        PreservePrefab(sp);
                        if (!string.IsNullOrEmpty(uid) && !preservedInstances.ContainsKey(uid))
                            preservedInstances[uid] = sp.gameObject;
                        if (emitClearDiagnostics)
                            Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Preserving stray prefab '{sp.name}' due to SkipSavingWhenUnchanged (ID '{uid ?? "<empty>"}')", LogCategory.PrefabManager, LogLevel.Info);
                        
                        continue;
                    }

                    // Stray, untracked instance – remove to prevent duplicates later
                    if (Owner != null)
                    {
	                    if (loadContext != null)
		                    Owner
			                    .SoftUnregisterGameObjectForPrefabBatch(
				                    sp.gameObject);
	                    else
		                    Owner
			                    .SoftUnregisterGameObject(
				                    sp.gameObject);
                    }

                    loadContext?.RemoveObject(
	                    sp.gameObject,
	                    managerCollectionsWillBeRebuilt: true);
                    if (ShouldUsePoolingFor(sp))
                    {
                        SaveablePrefabPoolCache.TryDespawn(sp, GetPoolSizeForPrefab(sp), true);
                    }
                    else
                    {
                        if (emitClearDiagnostics)
                            Logger.Log($"[CrystalSave][ClearSaveablePrefabs] Destroying stray prefab '{sp.name}' (ID '{uid ?? "<empty>"}')", LogCategory.PrefabManager, LogLevel.Info);
                        DestroyHelper.DestroyWithLogging(
                            sp.gameObject,
                            "PrefabManager.ClearSaveablePrefabs(scene-scan)"
                        );
                    }

                    if (!string.IsNullOrEmpty(uid))
                        instantiatedPrefabs.Remove(uid);

                    destroyedCount++;
                    
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ClearSaveablePrefabs: scene-scan cleanup failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.PrefabManager, LogLevel.Warning);
            }

                        ReplaceRegistrations(preservedPrefabs, preservedInstances);

	                        restoreQueue.Clear(preserveDeferredQueue);
                        initializedPrefabs = preservedPrefabs.Count;
                        AllPrefabsInitialized = false;
            
                }

	                internal int ResolveKeepAcrossSceneDuplicatesForLoadedScene(string loadedSceneName)
	                {
	                        if (string.IsNullOrEmpty(loadedSceneName))
	                                return 0;

	                        var candidates = UnityObjectSearch.FindIncludingInactive<SaveablePrefab>()
	                                .Where(IsKeepAcrossSceneBakedDuplicateCandidate)
	                                .ToList();

	                        if (candidates.Count <= 1)
	                                return 0;

	                        int removed = 0;
	                        foreach (SaveablePrefab incoming in candidates
	                                         .Where(prefab => string.Equals(GetRuntimeOriginSceneName(prefab), loadedSceneName, StringComparison.Ordinal))
	                                         .ToList())
	                        {
	                                if (incoming == null)
	                                        continue;

	                                SaveablePrefab keeper = candidates.FirstOrDefault(existing =>
	                                        existing != null &&
	                                        existing != incoming &&
	                                        IsKeepAcrossSceneBakedDuplicateCandidate(existing) &&
	                                        string.Equals(existing.PrefabAssetID, incoming.PrefabAssetID, StringComparison.Ordinal) &&
	                                        IsExistingKeepAcrossCarrierForLoadedScene(existing, loadedSceneName));

	                                if (keeper == null)
	                                        continue;

	                                RemoveKeepAcrossSceneDuplicate(incoming, keeper, loadedSceneName);
	                                removed++;
	                        }

	                        if (removed > 0)
	                        {
	                                Logger.Log(
	                                        $"[CrystalSave][KeepAcrossScenes] Removed {removed} newly loaded KeepAcrossScenes duplicate prefab(s) for scene '{loadedSceneName}'.",
	                                        LogCategory.PrefabManager,
	                                        LogLevel.Info);
	                        }

	                        return removed;
	                }

	                private static bool IsKeepAcrossSceneBakedDuplicateCandidate(SaveablePrefab prefab)
	                {
	                        if (prefab == null || prefab.gameObject == null)
	                                return false;

	                        var scene = prefab.gameObject.scene;
	                        return scene.IsValid() &&
	                               prefab.KeepAcrossScenes &&
	                               !prefab.IsAddedAtRuntime &&
	                               !string.IsNullOrEmpty(prefab.PrefabAssetID);
	                }

	                private static bool IsExistingKeepAcrossCarrierForLoadedScene(SaveablePrefab prefab, string loadedSceneName)
	                {
	                        string originSceneName = GetRuntimeOriginSceneName(prefab);
	                        if (!string.IsNullOrEmpty(originSceneName))
	                                return !string.Equals(originSceneName, loadedSceneName, StringComparison.Ordinal);

	                        Scene scene = prefab.gameObject != null ? prefab.gameObject.scene : default;
	                        return scene.IsValid() &&
	                               string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal);
	                }

	                private static string GetRuntimeOriginSceneName(SaveablePrefab prefab)
	                {
	                        if (prefab == null)
	                                return string.Empty;

	                        if (!string.IsNullOrEmpty(prefab.RuntimeOriginSceneName))
	                                return prefab.RuntimeOriginSceneName;

	                        Scene scene = prefab.gameObject != null ? prefab.gameObject.scene : default;
	                        return scene.IsValid() ? scene.name : string.Empty;
	                }

	                private void RemoveKeepAcrossSceneDuplicate(SaveablePrefab duplicate, SaveablePrefab keeper, string loadedSceneName)
	                {
	                        if (duplicate == null)
	                                return;

                                if (ShouldEmitDetailedPrefabDiagnostics)
                                {
                                        Logger.Log(
                                                $"[CrystalSave][KeepAcrossScenes] Removing newly loaded scene-baked KeepAcrossScenes duplicate from scene '{loadedSceneName}'. " +
                                                $"Duplicate={DescribePrefab(duplicate)} Keeper={DescribePrefab(keeper)}",
                                                LogCategory.PrefabManager,
                                                LogLevel.Info);
                                }
                                else if (Logger.ShouldLog(LogCategory.PrefabManager, LogLevel.Info))
                                {
                                        Logger.Log(
                                                $"CrystalSave removed a duplicate KeepAcrossScenes prefab while loading scene '{loadedSceneName}'.",
                                                LogCategory.PrefabManager,
                                                LogLevel.Info);
                                }

	                        GameObject duplicateObject = duplicate.gameObject;
	                        if (Owner != null && duplicateObject != null)
	                                Owner.SoftUnregisterGameObject(duplicateObject);

	                        RemovePrefabMappingIfOwned(duplicate.UniqueID, duplicateObject);
	                        RemoveRegistration(duplicate);

	                        if (duplicateObject == null)
	                                return;

	                        if (ShouldUsePoolingFor(duplicate))
	                        {
	                                SaveablePrefabPoolCache.TryDespawn(duplicate, GetPoolSizeForPrefab(duplicate), true);
	                        }
	                        else
	                        {
	                                DestroyHelper.DestroyWithLogging(
	                                        duplicateObject,
	                                        $"PrefabManager.ResolveKeepAcrossSceneDuplicatesForLoadedScene({loadedSceneName})");
	                        }
	                }

	                /// <summary>
	                /// Destroys all instantiated <see cref="SaveablePrefab"/>s that
	                /// match the provided <paramref name="prefabAssetID"/>.
	                /// </summary>
                /// <param name="prefabAssetID">The PrefabAssetID to match.</param>
                public void DestroyPrefabsByAssetID(string prefabAssetID)
                {
                        if (string.IsNullOrEmpty(prefabAssetID))
                        {
                                Logger.Log("PrefabManager: DestroyPrefabsByAssetID called with an empty prefabAssetID.", LogCategory.PrefabManager, LogLevel.Warning);
                                return;
                        }

                        var matches = saveablePrefabs
                                .Where(sp => sp != null && sp.PrefabAssetID == prefabAssetID)
                                .ToList();

                        foreach (var prefab in matches)
                        {
                                if (Owner != null)
                                        Owner.SoftUnregisterGameObject(prefab.gameObject);

                                if (ShouldUsePoolingFor(prefab))
                                {
                                        SaveablePrefabPoolCache.TryDespawn(prefab, GetPoolSizeForPrefab(prefab), true);
                                }
                                else
                                {
                                        DestroyHelper.DestroyWithLogging(
                                                prefab.gameObject,
                                                $"PrefabManager.DestroyPrefabsByAssetID({prefabAssetID})"
                                        );
                                }

	                                RemoveRegistration(prefab);
	                                instantiatedPrefabs.Remove(prefab.UniqueID);
                        }

                        if (matches.Count > 0)
                                Logger.Log($"PrefabManager: Destroyed {matches.Count} prefab instance(s) for asset ID '{prefabAssetID}'.", LogCategory.PrefabManager, LogLevel.Info);
                }

                /// <summary>
                /// Destroys all instantiated <see cref="SaveablePrefab"/>s whose
                /// asset IDs match any of the provided <paramref name="prefabAssetIDs"/>.
                /// </summary>
                /// <param name="prefabAssetIDs">List of asset IDs to match.</param>
                public void DestroyPrefabsByAssetID(List<string> prefabAssetIDs)
                {
                        if (prefabAssetIDs == null || prefabAssetIDs.Count == 0)
                        {
                                Logger.Log("PrefabManager: DestroyPrefabsByAssetID called with an empty prefabAssetID list.", LogCategory.PrefabManager, LogLevel.Warning);
                                return;
                        }

                        foreach (var id in prefabAssetIDs)
                        {
                                DestroyPrefabsByAssetID(id);
                        }
                }

        public void ProcessPendingPrefabs(string sceneName, List<string> destroyedIDs)
        {
            var list = restoreQueue.PeekPendingScene(sceneName);
            if (list.Count == 0)
                return;

            // Check if SceneActivationPipeline hook is set and delays spawning
            if (Owner != null && Owner.SceneActivationPipeline != null)
            {
                bool allowSpawn = Owner.SceneActivationPipeline.Invoke(sceneName);
                if (!allowSpawn)
                {
                    if (restoreQueue.MarkWaitingForActivation(sceneName))
                    {
                        Logger.Log(
                            $"[SCENELOAD] PrefabManager.ProcessPendingPrefabs: SceneActivationPipeline hook returned false for '{sceneName}'. " +
                            "Delaying prefab spawn until hook returns true.",
                            LogCategory.PrefabManager,
                            LogLevel.Info
                        );
                    }
                    return;
                }
            }

            restoreQueue.ReleaseActivationWait(sceneName);

            try
            {
                var ids = string.Join(", ", list.Select(d => d?.InstanceID ?? "<null>").Distinct(StringComparer.Ordinal));
                string msg = $"PrefabManager.ProcessPendingPrefabs: Resuming {list.Count} pending prefab(s) for scene '{sceneName}'. InstanceIDs: {ids}";
                Logger.Log(msg, LogCategory.PrefabManager, LogLevel.Info);
            }
            catch { /* logging best-effort */ }

            var sceneReuseAssetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SaveablePrefab prefab in UnityObjectSearch.FindIncludingInactive<SaveablePrefab>())
            {
                if (prefab == null ||
                    !prefab.ReuseSceneInstanceOnLoad ||
                    !IsSceneBackedReuseCandidate(prefab) ||
                    string.IsNullOrEmpty(prefab.PrefabAssetID))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(sceneName) &&
                    (!prefab.gameObject.scene.IsValid() ||
                     !string.Equals(prefab.gameObject.scene.name, sceneName, StringComparison.Ordinal)))
                {
                    continue;
                }

                sceneReuseAssetIds.Add(prefab.PrefabAssetID);
            }

            var forceSceneReuseNow = new List<SaveablePrefabData>();
            var normalPending = new List<SaveablePrefabData>();
            foreach (SaveablePrefabData data in list)
            {
                if (data != null &&
                    (data.ReuseSceneInstanceOnLoad ||
                     (!string.IsNullOrEmpty(data.PrefabID) && sceneReuseAssetIds.Contains(data.PrefabID))))
                {
                    forceSceneReuseNow.Add(data);
                }
                else if (data != null)
                {
                    normalPending.Add(data);
                }
            }

            restoreQueue.RemovePendingScene(sceneName);

            StartCoroutine(restoreLifetime.Bind(this,
                ProcessPendingPrefabBatchesCoroutine(sceneName, normalPending, forceSceneReuseNow, destroyedIDs)));
        }

        private IEnumerator ProcessPendingPrefabBatchesCoroutine(
            string sceneName,
            List<SaveablePrefabData> normalPending,
            List<SaveablePrefabData> forceSceneReuseNow,
            List<string> destroyedIDs)
        {
            if (normalPending != null && normalPending.Count > 0)
            {
                IEnumerator normalRoutine = InstantiatePrefabsCoroutine(
                    normalPending,
                    destroyedIDs,
                    clearExistingPrefabs: false);

                while (normalRoutine.MoveNext())
                    yield return normalRoutine.Current;
            }

            if (forceSceneReuseNow != null && forceSceneReuseNow.Count > 0)
            {
                Logger.Log(
                    $"PrefabManager.ProcessPendingPrefabs: forcing {forceSceneReuseNow.Count} scene-reuse pending prefab(s) for scene '{sceneName}' through restore now.",
                    LogCategory.PrefabManager,
                    LogLevel.Info);

                IEnumerator forcedRoutine = InstantiatePrefabsCoroutine(
                    forceSceneReuseNow,
                    destroyedIDs,
                    clearExistingPrefabs: false,
                    handleDeferral: false);

                while (forcedRoutine.MoveNext())
                    yield return forcedRoutine.Current;
            }
        }

        private GameObject GetPrefabByID(string prefabID)
        {
            if (string.IsNullOrEmpty(prefabID))
            {
                Logger.Log("PrefabManager: GetPrefabByID called with an empty prefabID.", LogCategory.PrefabManager, LogLevel.Warning);
                return null;
            }
            var entry = prefabRegistry.FindEntryByID(prefabID);
            if (entry != null)
                return entry.prefab;

            Logger.Log($"PrefabManager: Prefab with ID '{prefabID}' not found in PrefabRegistry.", LogCategory.PrefabManager, LogLevel.Warning);
            return null;
        }

        private bool ShouldUsePoolingFor(SaveablePrefab prefab)
        {
                if (prefab == null)
                        return false;

                if (!UsePrefabPooling)
                        return false;

                // Check if pooling is disabled directly on the SaveablePrefab component first
                if (prefab.DisablePooling)
                        return false;

                // If not disabled on component, check the PrefabRegistry setting
                return ShouldUsePoolingFor(prefab.PrefabAssetID);
        }

        /// <summary>
        /// Checks if pooling should be used for a prefab during load operations.
        /// Mirrors SaveablePrefabFactory logic but checks saved data first:
        /// 1. Saved DisablePooling setting (from SaveablePrefabData) - ultimate truth for loaded instances
        /// 2. Current prefab asset DisablePooling setting (from SaveablePrefab component)
        /// 3. PrefabRegistry.IsPoolingDisabled() setting
        /// </summary>
        private bool ShouldUsePoolingFor(SaveablePrefabData prefabData, SaveablePrefab prefabAsset)
        {
                if (prefabData == null || prefabAsset == null)
                {
                        Logger.Log("PrefabManager.ShouldUsePoolingFor: prefabData or prefabAsset is null", LogCategory.PrefabManager, LogLevel.Warning);
                        return false;
                }

                if (!UsePrefabPooling)
                {
                        return false;
                }

                // 1. Check saved DisablePooling setting first (this is the state when originally instantiated)
                if (prefabData.DisablePooling)
                {
                        return false;
                }

                // 2. Check current prefab asset DisablePooling setting
                if (prefabAsset.DisablePooling)
                {
                        return false;
                }

                // 3. Check PrefabRegistry setting using the correct asset ID
                if (prefabRegistry != null && prefabRegistry.IsPoolingDisabled(prefabAsset.PrefabAssetID))
                {
                        return false;
                }

                return true;
        }

        private bool ShouldUsePoolingFor(string prefabID)
        {
            if (!UsePrefabPooling)
                return false;

            if (prefabRegistry == null || string.IsNullOrEmpty(prefabID))
                return true;

            return !prefabRegistry.IsPoolingDisabled(prefabID);
        }

        private int GetPoolSizeForPrefab(SaveablePrefab prefab)
        {
            if (prefabRegistry == null || prefab == null)
                return DefaultPoolSize;

            return prefabRegistry.ResolvePoolSize(prefab.PrefabAssetID, DefaultPoolSize);
        }

        private int GetPoolSizeForPrefabID(string prefabID)
        {
            if (prefabRegistry == null || string.IsNullOrEmpty(prefabID))
                return DefaultPoolSize;

            return prefabRegistry.ResolvePoolSize(prefabID, DefaultPoolSize);
        }

        private void HandleMissingParent(GameObject instance)
        {
            Logger.Log($"PrefabManager: No parent specified for prefab '{instance.name}'. Placing at root.", LogCategory.PrefabManager, LogLevel.Info);
        }

        /*──────────────── COLLIDER SNAPSHOTS ───────────────*/
        private static List<ColliderSnapshot> SaveColliderSettings(SaveablePrefab prefab)
        {
            if (prefab == null || !prefab.TrackColliderSettings) return null;

            var list = new List<ColliderSnapshot>();
            foreach (var col in prefab.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                if (!col) continue;

                var snap = new ColliderSnapshot
                {
                    Path          = GetTransformPath(prefab.transform, col.transform),
                    ColliderType  = col.GetType().Name,
                    Enabled       = col.enabled,
                    IsTrigger     = col.isTrigger
                };

                switch (col)
                {
                    case BoxCollider b:
                        snap.Center = b.center;
                        snap.Size   = b.size;
                        break;
                    case SphereCollider s:
                        snap.Center = s.center;
                        snap.Radius = s.radius;
                        break;
                    case CapsuleCollider c:
                        snap.Center    = c.center;
                        snap.Radius    = c.radius;
                        snap.Height    = c.height;
                        snap.Direction = c.direction;
                        break;
                }

                list.Add(snap);
            }
            return list.Count > 0 ? list : null;
        }

        private static void ApplyColliderSettings(GameObject instance, List<ColliderSnapshot> snaps)
        {
            var prefab = instance != null ? instance.GetComponent<SaveablePrefab>() : null;
            if (prefab == null || !prefab.TrackColliderSettings || snaps == null) return;

            foreach (var snap in snaps)
            {
                var tr = instance.transform.Find(snap.Path);
                if (!tr) continue;

                var col = tr.GetComponent(snap.ColliderType) as Collider;
                if (!col) continue;

                col.enabled = snap.Enabled;
                col.isTrigger = snap.IsTrigger;

                switch (col)
                {
                    case BoxCollider b:
                        b.center = snap.Center;
                        b.size = snap.Size;
                        break;
                    case SphereCollider s:
                        s.center = snap.Center;
                        s.radius = snap.Radius;
                        break;
                    case CapsuleCollider c:
                        c.center = snap.Center;
                        c.radius = snap.Radius;
                        c.height = snap.Height;
                        c.direction = snap.Direction;
                        break;
                }
            }
        }

        private static string GetTransformPath(Transform root, Transform t)
        {
            var stack = new System.Collections.Generic.Stack<string>();
            while (t != root && t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }

        #endregion
    }
}
#endif
