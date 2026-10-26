#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Handles the scene loading process including the LoadStateMachine.
    /// </summary>
    public class SceneLoadManager
    {
        readonly SaveManager manager;
        AsyncOperation pendingSceneOperation;
        public void ActivatePendingScene()
        { if (pendingSceneOperation != null) pendingSceneOperation.allowSceneActivation = true; }

        public SceneLoadManager(SaveManager manager)
        {
            this.manager = manager;
        }

        /// <summary>
        /// Initiates the load process by creating and running the LoadStateMachine.
        /// </summary>
        public IEnumerator StartLoadProcess(SaveSlot slot, bool restoreScene, bool asyncLoad, bool allowActivation, CancellationToken token)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            var request = manager.QueueLoadRequest(slot.SlotNumber, restoreScene, asyncLoad, allowActivation, null, token);
            while (!request.IsCompleted) yield return null;
            var result = request.GetAwaiter().GetResult();
            if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
        }

        /// <summary>
        /// Asynchronously loads a save slot and awaits its completion.
        /// </summary>
        public async Task<LoadResult> LoadSaveSlotAsync(int slotNumber = 1,
            bool restoreLastActiveScene = false,
            bool loadAsync = false,
            bool allowSceneActivation = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return await manager.QueueLoadRequest(slotNumber, restoreLastActiveScene, loadAsync, allowSceneActivation, timeout, cancellationToken);
        }

        void DestroyAllKASPrefabs()
        {
            SaveablePrefab[] allSaveables = UnityObjectSearch.FindIncludingInactive<SaveablePrefab>();
            foreach (var sp in allSaveables)
            {
                if (sp.KeepAcrossScenes)
                {
                    Logger.Log($"DestroyAllKASPrefabs: Destroying leftover KAS prefab '{sp.name}' (ID: {sp.UniqueID}).");
                    DestroyHelper.DestroyWithLogging(sp.gameObject, "DestroyAllKASPrefabs");
                }
            }
        }

        class LoadStateMachine
        {
            readonly SceneLoadManager owner;
            enum LoadState
            {
                LoadScene,
                InstantiatePrefabs,
                ApplyComponents,
                ApplyGameObjectStates,
                HandleDestroyedObjects,
                Finalize,
                Completed,
                Failed
            }

            readonly SaveManager saveManager;
            readonly SaveSlot slot;
            readonly bool restoreLastActiveScene;
            readonly bool loadAsync;
            readonly bool allowSceneActivationFlag;
            readonly CancellationToken cancellationToken;
            readonly SaveOperationContext context;

            LoadState currentState = LoadState.LoadScene;
            SaveData loadedData;
            bool migrationPerformed = false;

            public LoadStateMachine(SaveOperationContext context, SceneLoadManager owner, SaveManager manager, SaveSlot saveSlot, bool restoreScene, bool asyncLoad, bool allowActivation, CancellationToken token, SaveData candidate, bool migrated)
            {
                loadedData = candidate; migrationPerformed = migrated;
                this.owner = owner;
                this.context = context;
                saveManager = manager;
                slot = saveSlot;
                restoreLastActiveScene = restoreScene;
                loadAsync = asyncLoad;
                allowSceneActivationFlag = allowActivation;
                cancellationToken = token;
            }

            IEnumerator ExecuteStep(LoadState state, LoadState nextState, Func<IEnumerator> action)
            {
                Logger.Log($"LoadStateMachine: Entering {state} state.", LogCategory.SceneManagement);
                yield return action();
                if (currentState == state)
                {
                    Logger.Log($"LoadStateMachine: Exiting {state} state.", LogCategory.SceneManagement);
                    currentState = nextState;
                }
            }

            public IEnumerator Run()
            {
                Logger.Log($"LoadStateMachine: Starting load process for slot {slot.SlotNumber}.", LogCategory.SceneManagement, LogLevel.Info);
                while (currentState != LoadState.Completed && currentState != LoadState.Failed)
                {
                    switch (currentState)
                    {
                        case LoadState.LoadScene:
                            yield return ExecuteStep(LoadState.LoadScene, LoadState.InstantiatePrefabs, LoadSceneCoroutine);
                            break;
                        case LoadState.InstantiatePrefabs:
                            yield return ExecuteStep(LoadState.InstantiatePrefabs, LoadState.ApplyComponents, InstantiatePrefabsCoroutine);
                            break;
                        case LoadState.ApplyComponents:
                            yield return ExecuteStep(LoadState.ApplyComponents, LoadState.ApplyGameObjectStates, ApplyComponentsCoroutine);
                            break;
                        case LoadState.ApplyGameObjectStates:
                            yield return ExecuteStep(LoadState.ApplyGameObjectStates, LoadState.HandleDestroyedObjects, ApplyGameObjectStatesCoroutine);
                            break;
                        case LoadState.HandleDestroyedObjects:
                            yield return ExecuteStep(LoadState.HandleDestroyedObjects, LoadState.Finalize, HandleDestroyedObjectsCoroutine);
                            break;
                        case LoadState.Finalize:
                            Logger.Log("LoadStateMachine: Entering Finalize state.", LogCategory.SceneManagement);
                            while (context.PendingRestores.Any(pending => pending.Waiting)) yield return null;
                            if (context.RestoreErrors.Count != 0)
                            {
                                saveManager.CompleteLoad(context, slot.SlotNumber, false,
                                    ComponentOperationScope.DescribeErrors(context.RestoreErrors));
                                currentState = LoadState.Failed;
                                break;
                            }
                            FinalizeLoad();
                            Logger.Log("LoadStateMachine: Exiting Finalize state.", LogCategory.SceneManagement);
                            if (saveManager.SaveSettings.autoSaveMigratedData && migrationPerformed)
                                yield return saveManager.SaveOperations.SaveCoroutine(context, slot, loadedData.LastActiveScene);
                            currentState = LoadState.Completed;
                            break;
                    }
                    yield return null;
                }

                if (currentState == LoadState.Completed)
                {
                    saveManager.StateMachine.TransitionTo(SaveState.Idle);
                    Logger.Log($"LoadStateMachine: Load process for slot {slot.SlotNumber} completed successfully.", LogCategory.SceneManagement, LogLevel.Info);
                }
                else if (currentState == LoadState.Failed)
                {
                    Logger.Log($"LoadStateMachine: Load process for slot {slot.SlotNumber} failed.", LogCategory.SceneManagement, LogLevel.Error);
                }
            }

            IEnumerator LoadSceneCoroutine()
            {
                string sceneName = !string.IsNullOrEmpty(loadedData.LastActiveScene) ? loadedData.LastActiveScene : slot.LastActiveScene;
                if (!restoreLastActiveScene || string.IsNullOrEmpty(sceneName)) yield break;
                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    Logger.Log($"Scene '{sceneName}' is unavailable; restoring into the current scene.", LogCategory.SceneManagement, LogLevel.Warning);
                    yield break;
                }
                saveManager.RaiseSceneLoadStarted(sceneName);
                // Even the legacy synchronous entry waits a frame for activation before restoring.
                if (!loadAsync)
                {
                    SceneManager.LoadScene(sceneName);
                    yield return null;
                }
                else
                {
                    var operation = SceneManager.LoadSceneAsync(sceneName);
                    if (operation == null) throw new InvalidOperationException($"Could not load scene '{sceneName}'.");
                    owner.pendingSceneOperation = operation;
                    operation.allowSceneActivation = allowSceneActivationFlag;
                    bool requested = false;
                    while (!operation.isDone)
                    {
                        // Unity cannot cancel a started scene transition. Release a hold and finish.
                        if (cancellationToken.IsCancellationRequested) operation.allowSceneActivation = true;
                        saveManager.RaiseSceneLoadProgress(Mathf.Clamp01(operation.progress / 0.9f));
                        if (!operation.allowSceneActivation && operation.progress >= 0.9f && !requested)
                        {
                            requested = true;
                            saveManager.RaiseSceneActivationRequested(sceneName);
                        }
                        yield return null;
                    }
                    owner.pendingSceneOperation = null;
                }
                saveManager.RaiseSceneLoadCompleted(sceneName);
            }

            IEnumerator InstantiatePrefabsCoroutine()
            {
                saveManager.StateMachine.TransitionTo(SaveState.Loading);
                IEnumerator instantiationCoroutine = null;
                try
                {
                    var prefabManager = saveManager.GetPrefabManager;
                    var destroyedIds = saveManager.GameObjectTracker.GetDestroyedGameObjectIDs();
                    PrefabManager.PreparedPrefabBatch preparedPrefabs =
                        prefabManager.PreparePrefabBatchForLoad(loadedData.Prefabs);

                    instantiationCoroutine = prefabManager.InstantiatePreparedPrefabsCoroutine(
                        preparedPrefabs,
                        destroyedIds,
                        clearExistingPrefabs: true);
                }
                catch (Exception ex)
                {
                    Logger.Log($"LoadStateMachine: Failed to start prefab instantiation for slot {slot.SlotNumber}: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SceneManagement, LogLevel.Error);
                    saveManager.CompleteLoad(context, slot.SlotNumber, false, $"Starting prefab instantiation failed: {ex.Message}");
                    saveManager.StateMachine.TransitionTo(SaveState.Error);
                    currentState = LoadState.Failed;
                    yield break;
                }

                if (instantiationCoroutine != null)
                {
                    yield return instantiationCoroutine;
                }
                else
                {
                    yield break;
                }
            }

            IEnumerator ApplyComponentsCoroutine()
            {
                SaveableComponent.ResetLoadCallCounts();
                saveManager.ComponentManager.ResetDeserializedComponents();
                saveManager.ComponentManager.InstantiateMissingHomeSceneObjectsForLoadedScenes();
                int batchSize = saveManager.SaveSettings?.componentApplyBatchSize ?? 0;
                if (batchSize > 0)
                {
                    var compCoroutine = saveManager.ComponentManager.ApplyComponentDataCoroutine(loadedData, batchSize);
                    yield return compCoroutine;
                }
                else
                {
                    saveManager.ComponentManager.ApplyComponentData(loadedData);
                }
                var dupLoads = SaveableComponent.LoadCallCounts.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList();
                if (dupLoads.Count > 0)
                    Debug.LogWarning($"ApplyComponentsCoroutine: Duplicate LoadData calls for {string.Join(", ", dupLoads)}");
                else
                yield return null;
            }

            IEnumerator ApplyGameObjectStatesCoroutine()
            {
                try
                {
                    if (loadedData == null || loadedData.GameObjectStates == null)
                    {
                        Logger.Log("LoadStateMachine: No GameObjectStates to apply (loadedData or GameObjectStates is null).", LogCategory.SceneManagement, LogLevel.Warning);
                    }
                    else
                    {
                        Logger.Log($"LoadStateMachine: Applying active states for {loadedData.GameObjectStates.Count} GameObjects.", LogCategory.SceneManagement, LogLevel.Info);
                        saveManager.ApplyGameObjectActiveStates(loadedData.GameObjectStates);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"ApplyGameObjectStatesCoroutine error: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SceneManagement, LogLevel.Error);
                    throw;
                }
                yield return null;
            }

            IEnumerator HandleDestroyedObjectsCoroutine()
            {
                try
                {
                    if (loadedData == null || loadedData.DestroyedGameObjects == null)
                    {
                        Logger.Log("HandleDestroyedObjectsCoroutine: No destroyed-object list to process.", LogCategory.SceneManagement, LogLevel.Info);
                    }
                    else
                    {
                        Logger.Log($"HandleDestroyedObjectsCoroutine: Processing {loadedData.DestroyedGameObjects.Count} destroyed GameObjects.", LogCategory.SceneManagement, LogLevel.Info);
                        foreach (string destroyedID in loadedData.DestroyedGameObjects)
                        {
                            if (string.IsNullOrEmpty(destroyedID))
                            {
                                Logger.Log("HandleDestroyedObjectsCoroutine: Encountered null or empty destroyedID; skipping.", LogCategory.SceneManagement, LogLevel.Info);
                                continue;
                            }

                            GameObject go = saveManager.FindGameObjectByUniqueID(destroyedID, SaveManager.IdentifierType.UniqueID);
                            if (go != null)
                            {
                                DestroyHelper.DestroyWithLogging(go, "LoadStateMachine.HandleDestroyedObjects: Removing destroyed GameObject");
                                Logger.Log($"LoadStateMachine: Destroyed GameObject with ID '{destroyedID}' according to saved data.", LogCategory.SceneManagement, LogLevel.Info);
                            }
                            else
                            {
                                Logger.Log($"HandleDestroyedObjectsCoroutine: GameObject with ID '{destroyedID}' not found.", LogCategory.SceneManagement, LogLevel.Info);
                            }
                        }
                    }

                    string currentSceneName = SceneManager.GetActiveScene().name;

                    foreach (var component in saveManager.SaveableComponents)
                    {
                        if (component == null)
                            continue;

                        if (component is PersistentVisibilityController pvc)
                            pvc.ApplyVisibilityBasedOnScene(currentSceneName);
                    }

                    var prefabManager = saveManager.GetPrefabManager;
                    if (prefabManager != null)
                    {
                        foreach (var prefab in prefabManager.RegisteredPrefabs.ToArray())
                        {
                            if (prefab == null)
                                continue;

                            if (prefab.TryGetComponent(out PersistentVisibilityController prefabPvc))
                                prefabPvc.ApplyVisibilityBasedOnScene(currentSceneName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"HandleDestroyedObjectsCoroutine error: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SceneManagement, LogLevel.Error);
                    throw;
                }
                yield return null;
            }

            void FinalizeLoad()
            {
                Logger.Log("LoadStateMachine: Load successful.", LogCategory.SceneManagement, LogLevel.Info);
                if (saveManager.EnforceActiveState && loadedData != null && loadedData.GameObjectStates != null)
                    saveManager.ApplyGameObjectActiveStates(loadedData.GameObjectStates);

                if (loadedData != null && loadedData.GameObjectStates != null)
                    saveManager.StartActiveStateWatch(loadedData.GameObjectStates);

                // Do not advance physics during restore. Dynamic objects may have
                // just been recreated from saved poses and velocities; forcing a
                // solver step here can inject energy before gameplay resumes.
                Physics.SyncTransforms();

                // After restored transforms are synced, ensure visibility is correct for the active scene once more
                try
                {
                    string sceneName = SceneManager.GetActiveScene().name;
                    foreach (var component in saveManager.SaveableComponents)
                    {
                        if (component == null)
                            continue;

                        if (component is PersistentVisibilityController pvc)
                            pvc.ApplyVisibilityBasedOnScene(sceneName);
                    }

                    var prefabManager = saveManager.GetPrefabManager;
                    if (prefabManager != null)
                    {
                        foreach (var prefab in prefabManager.RegisteredPrefabs.ToArray())
                        {
                            if (prefab == null)
                                continue;

                            if (prefab.TryGetComponent(out PersistentVisibilityController prefabPvc))
                                prefabPvc.ApplyVisibilityBasedOnScene(sceneName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"FinalizeLoad visibility pass error: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SceneManagement, LogLevel.Warning);
                    throw;
                }
            }
        }

        internal IEnumerator RunLoadCoroutine(SaveOperationContext context, SaveSlot slot, bool restoreLastActiveScene, bool loadAsync, bool allowSceneActivation, CancellationToken cancellationToken)
        {
            var prepare = manager.PrepareSlotAsync(context, slot);
            while (!prepare.IsCompleted) yield return null;
            var candidate = prepare.GetAwaiter().GetResult();
            bool migrated = manager.ValidateCandidate(candidate);
            string destination = !string.IsNullOrEmpty(candidate.LastActiveScene) ? candidate.LastActiveScene : slot.LastActiveScene;
            if (restoreLastActiveScene && !string.IsNullOrEmpty(destination) && !Application.CanStreamedLevelBeLoaded(destination))
                throw new InvalidOperationException($"Scene '{destination}' is unavailable.");
            context.CheckCancellation();
            context.BeginCommit();
#if UNITY_2022_1_OR_NEWER
            var prev3D = Physics.simulationMode;
            var prev2D = Physics2D.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            Physics2D.simulationMode = SimulationMode2D.Script;
#else
#pragma warning disable CS0618
            bool prev3D = Physics.autoSimulation, prev2D = Physics2D.autoSimulation;
            Physics.autoSimulation = false; Physics2D.autoSimulation = false;
#pragma warning restore CS0618
#endif
            try
            {
                manager.ComponentManager?.BeginFullLoadGeneration();
                manager.GetPrefabManager?.BeginFullLoadGeneration();
                manager.StopActiveStateWatch();
                DestroyAllKASPrefabs();
                manager.CurrentSaveData = candidate;
                manager.ComponentManager?.ImportHomeSceneSnapshots(candidate.HomeSceneComponentSnapshots, candidate.HomeScenePrefabAssetIDs);
                manager.GameObjectTracker.ReplaceDestroyedIDs(candidate.DestroyedGameObjects);
                manager.StateMachine.TransitionTo(SaveState.Loading);
                var machine = new LoadStateMachine(context, this, manager, slot, restoreLastActiveScene, loadAsync, allowSceneActivation, cancellationToken, candidate, migrated);
                yield return machine.Run();
                if (context.LoadError == null)
                {
                    manager.ReMapTrackedUniqueIDs(slot);
                    // Prepared cloud mirrors cannot write until the candidate has restored successfully.
                    foreach (var write in context.PreparedCacheWrites)
                    {
                        var cache = write();
                        while (!cache.IsCompleted) yield return null;
                        cache.GetAwaiter().GetResult();
                    }
                    manager.CompleteLoad(context, slot.SlotNumber, true);
                }
            }
            finally
            {
#if UNITY_2022_1_OR_NEWER
                Physics.simulationMode = prev3D;
                Physics2D.simulationMode = prev2D;
#else
#pragma warning disable CS0618
                Physics.autoSimulation = prev3D; Physics2D.autoSimulation = prev2D;
#pragma warning restore CS0618
#endif
                if (pendingSceneOperation != null) pendingSceneOperation.allowSceneActivation = true;
                pendingSceneOperation = null;
            }
        }
    }
}
#endif
