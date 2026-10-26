#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime.Examples
{
    /// <summary>
    /// Play-mode repro driver for scene-baked SaveablePrefab reuse.
    /// Created by the editor diagnostic builder.
    /// </summary>
    [AddComponentMenu("Crystal Save/Diagnostics/Scene-Baked Prefab Reuse Diagnostic")]
    public sealed class SceneBakedPrefabReuseDiagnostic : MonoBehaviour
    {
        public enum DriverMode
        {
            ManualPrefabManager,
            SnapshotOrchestrator
        }

        [SerializeField] private string diagnosticPrefabAssetID = "CSP_DIAG_SCENE_BAKED_ACTOR";
        [SerializeField] private string targetSceneName = "CSP_Diag_Target";
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private DriverMode driverMode = DriverMode.ManualPrefabManager;
        [SerializeField] private bool processAsDeferredPass = true;
        [SerializeField] private bool forceAssetBasedReuseDuringDeferred;
        [SerializeField] private bool reuseSceneInstanceFlagInData = true;
        [SerializeField] private bool setHomeSceneInManualSyntheticData;
        [SerializeField] private string unchangedTargetInstanceID = "CSP_DIAG_TARGET_INSTANCE";
        [SerializeField] private int waitFramesAfterSceneLoad = 2;
        [SerializeField] private bool orchestratorGatePendingPrefabsUntilActivated = true;
        [SerializeField] private int orchestratorActivationDelayFrames;
        [SerializeField] private Vector3 savedRuntimePosition = new Vector3(0f, 1f, 0f);
        [SerializeField] private Vector3 savedRuntimeScale = new Vector3(1.25f, 1.25f, 1.25f);

        private const string LogPrefix = "[CrystalSave][SceneBakedPrefabDiag]";

        private void Start()
        {
            if (runOnStart)
            {
                DontDestroyOnLoad(gameObject);
                StartCoroutine(Run());
            }
        }

        [ContextMenu("Run Scene-Baked Prefab Diagnostic")]
        public void RunFromContextMenu()
        {
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return WaitForSaveManager();

            SaveManager manager = SaveManager.Instance;
            if (manager == null || manager.GetPrefabManager == null)
            {
                Debug.LogError($"{LogPrefix} SaveManager or PrefabManager was not available.");
                yield break;
            }

            if (forceAssetBasedReuseDuringDeferred && manager.SaveSettings != null)
            {
                manager.SaveSettings.enableAssetBasedReuseDuringDeferred = true;
                Debug.Log($"{LogPrefix} Enabled asset-based reuse during deferred processing for this play session.");
            }

            SaveablePrefab source = FindFirstSceneBakedMatch(SceneManager.GetActiveScene().name);
            if (source == null)
            {
                Debug.LogError($"{LogPrefix} Could not find a source scene-baked SaveablePrefab with PrefabAssetID '{diagnosticPrefabAssetID}'.");
                yield break;
            }

            source.transform.position = savedRuntimePosition;
            source.transform.localScale = savedRuntimeScale;
            source.gameObject.name = "CSP_Diag_Actor_SavedState";
            source.ReuseSceneInstanceOnLoad = reuseSceneInstanceFlagInData;
            source.DeferLowPriorityUntilRequested = processAsDeferredPass;

            if (driverMode == DriverMode.SnapshotOrchestrator)
            {
                yield return RunSnapshotOrchestratorFlow(manager, source);
                yield break;
            }

            SaveablePrefabData data = manager.GetPrefabManager.BuildPrefabData(source);
            if (data == null)
            {
                Debug.LogError($"{LogPrefix} PrefabManager.BuildPrefabData returned null for the diagnostic prefab.");
                yield break;
            }

            data.HomeScene = setHomeSceneInManualSyntheticData ? targetSceneName : null;
            data.DeferLowPriorityUntilRequested = processAsDeferredPass;
            data.ReuseSceneInstanceOnLoad = reuseSceneInstanceFlagInData;

            Debug.Log($"{LogPrefix} Built saved prefab data from source instance. Driver={driverMode}, InstanceID='{data.InstanceID}', PrefabID='{data.PrefabID}', HomeScene='{(string.IsNullOrEmpty(data.HomeScene) ? "<empty>" : data.HomeScene)}', Deferred={data.DeferLowPriorityUntilRequested}, ReuseFlag={data.ReuseSceneInstanceOnLoad}, HasTransformOverride={data.HasTransformOverride}.");

            yield return RunManualPrefabManagerFlow(manager, data);
        }

        private IEnumerator RunManualPrefabManagerFlow(SaveManager manager, SaveablePrefabData data)
        {
            AsyncOperation loadOperation = null;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"{LogPrefix} Could not load target scene '{targetSceneName}'. Make sure it is in Build Settings. {exception.Message}");
                yield break;
            }

            if (loadOperation == null)
            {
                Debug.LogError($"{LogPrefix} SceneManager.LoadSceneAsync returned null for '{targetSceneName}'.");
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            for (int i = 0; i < waitFramesAfterSceneLoad; i++)
            {
                yield return null;
            }

            List<SaveablePrefab> before = FindAllMatchingPrefabs();
            LogMatches("Before restore", before);

            List<SaveablePrefabData> prefabData = new List<SaveablePrefabData> { data };
            List<string> destroyedIds = manager.GameObjectTracker != null
                ? manager.GameObjectTracker.GetDestroyedGameObjectIDs()
                : new List<string>();

            int removedSyntheticDestroyedIds = destroyedIds.RemoveAll(id => string.Equals(id, data.InstanceID, StringComparison.Ordinal));
            if (removedSyntheticDestroyedIds > 0)
            {
                Debug.Log($"{LogPrefix} Removed synthetic source InstanceID '{data.InstanceID}' from destroyed IDs before restore.");
            }

            yield return manager.GetPrefabManager.InstantiatePrefabsCoroutine(
                prefabData,
                destroyedIds,
                clearExistingPrefabs: false,
                handleDeferral: !processAsDeferredPass);

            for (int i = 0; i < waitFramesAfterSceneLoad; i++)
            {
                yield return null;
            }

            List<SaveablePrefab> after = FindAllMatchingPrefabs();
            LogMatches("After restore", after);

            EvaluateRestoreResult(after, data);
        }

        private IEnumerator RunSnapshotOrchestratorFlow(SaveManager manager, SaveablePrefab source)
        {
            string expectedInstanceID = source.UniqueID;
            source.RememberHomeScene = true;
            source.SetHomeScene(targetSceneName);
            source.ReuseSceneInstanceOnLoad = reuseSceneInstanceFlagInData;
            source.DeferLowPriorityUntilRequested = processAsDeferredPass;

            Debug.Log($"{LogPrefix} Starting snapshot orchestrator flow. Source InstanceID='{expectedInstanceID}', PrefabID='{source.PrefabAssetID}', TargetHomeScene='{targetSceneName}', Deferred={processAsDeferredPass}, ReuseFlag={reuseSceneInstanceFlagInData}.");

            var loader = new DiagnosticSnapshotOrchestrator(
                manager,
                targetSceneName,
                orchestratorGatePendingPrefabsUntilActivated,
                orchestratorActivationDelayFrames);
            manager.RegisterSceneLoadOrchestrator(loader);

            Task loadTask = loader.LoadSceneAsync(targetSceneName);
            yield return WaitForTask(loadTask, "snapshot orchestrator scene load");

            manager.UnregisterSceneLoadOrchestrator(loader);
            loader.RestoreSceneActivationPipeline();

            if (!TaskSucceeded(loadTask, "snapshot orchestrator scene load"))
                yield break;

            for (int i = 0; i < waitFramesAfterSceneLoad; i++)
            {
                yield return null;
            }

            SaveablePrefabData savedData = FindSavedPrefabData(manager.CurrentSaveData, expectedInstanceID);
            if (savedData == null)
            {
                Debug.LogError($"{LogPrefix} FAIL: snapshot orchestrator flow did not capture prefab data for InstanceID '{expectedInstanceID}'.");
                yield break;
            }

            List<SaveablePrefab> after = FindAllMatchingPrefabs();
            LogMatches("After orchestrated pending restore", after);
            EvaluateRestoreResult(after, savedData);
        }

        private void EvaluateRestoreResult(List<SaveablePrefab> prefabs, SaveablePrefabData data)
        {
            if (prefabs.Count > 1)
            {
                Debug.LogWarning($"{LogPrefix} DUPLICATE DETECTED: found {prefabs.Count} prefabs with PrefabAssetID '{diagnosticPrefabAssetID}' after restore.");
                return;
            }

            if (prefabs.Count == 0)
            {
                Debug.LogError($"{LogPrefix} FAIL: no matching prefab remained after restore.");
                return;
            }

            SaveablePrefab restored = prefabs[0];
            bool sceneBacked = restored != null &&
                               !restored.IsAddedAtRuntime &&
                               restored.gameObject.scene.IsValid() &&
                               restored.gameObject.scene.name == targetSceneName;
            bool idMatches = restored != null && string.Equals(restored.UniqueID, data.InstanceID, StringComparison.Ordinal);
            bool stillOriginalTarget = restored != null && string.Equals(restored.UniqueID, unchangedTargetInstanceID, StringComparison.Ordinal);
            bool nameMatches = restored != null &&
                               (string.IsNullOrEmpty(data.GameObjectName) ||
                                string.Equals(restored.name, data.GameObjectName, StringComparison.Ordinal));
            bool positionMatches = restored != null && Approximately(restored.transform.position, data.Position);
            bool scaleMatches = restored != null && Approximately(restored.transform.localScale, data.Scale);
            bool rotationMatches = restored != null && Quaternion.Angle(restored.transform.rotation, data.Rotation) <= 0.25f;

            if (stillOriginalTarget)
            {
                Debug.LogError($"{LogPrefix} FAIL: target scene prefab stayed on unchanged target InstanceID '{unchangedTargetInstanceID}', so saved data was not claimed.");
                return;
            }

            if (sceneBacked && idMatches && nameMatches && positionMatches && scaleMatches && rotationMatches)
            {
                Debug.Log($"{LogPrefix} PASS: scene-baked target was claimed and restored. UniqueID='{restored.UniqueID}', Name='{restored.name}', Position={restored.transform.position}.");
                return;
            }

            Debug.LogError(
                $"{LogPrefix} FAIL: restore did not match saved scene-baked state. " +
                $"sceneBacked={sceneBacked}, idMatches={idMatches}, nameMatches={nameMatches}, " +
                $"positionMatches={positionMatches}, scaleMatches={scaleMatches}, rotationMatches={rotationMatches}. " +
                $"ExpectedID='{data.InstanceID}', ActualID='{(restored != null ? restored.UniqueID : "<null>")}', " +
                $"ExpectedName='{data.GameObjectName}', ActualName='{(restored != null ? restored.name : "<null>")}'.");
        }

        private static bool Approximately(Vector3 lhs, Vector3 rhs)
        {
            return Vector3.SqrMagnitude(lhs - rhs) <= 0.0001f;
        }

        private IEnumerator WaitForSaveManager()
        {
            const int maxFrames = 600;
            int frame = 0;

            while ((SaveManager.Instance == null || !SaveManager.IsInitialized || SaveManager.Instance.GetPrefabManager == null) && frame < maxFrames)
            {
                frame++;
                yield return null;
            }

            if (frame >= maxFrames)
            {
                Debug.LogWarning($"{LogPrefix} Timed out waiting for SaveManager initialization.");
            }
        }

        private static IEnumerator WaitForTask(Task task, string label)
        {
            while (task != null && !task.IsCompleted)
            {
                yield return null;
            }
        }

        private static bool TaskSucceeded(Task task, string label)
        {
            if (task == null)
            {
                Debug.LogError($"{LogPrefix} {label} failed: task was null.");
                return false;
            }

            if (task.IsCanceled)
            {
                Debug.LogError($"{LogPrefix} {label} was canceled.");
                return false;
            }

            if (task.IsFaulted)
            {
                Debug.LogError($"{LogPrefix} {label} faulted. {TaskError(task)}");
                return false;
            }

            return true;
        }

        private static string TaskError(Task task)
        {
            return task?.Exception != null ? task.Exception.GetBaseException().Message : "<no error>";
        }

        private SaveablePrefabData FindSavedPrefabData(SaveData data, string instanceID)
        {
            if (data?.Prefabs == null)
                return null;

            SaveablePrefabData byAsset = null;
            foreach (SaveablePrefabData prefabData in data.Prefabs)
            {
                if (prefabData == null)
                    continue;

                if (!string.IsNullOrEmpty(instanceID) &&
                    string.Equals(prefabData.InstanceID, instanceID, StringComparison.Ordinal))
                {
                    return prefabData;
                }

                if (byAsset == null &&
                    string.Equals(prefabData.PrefabID, diagnosticPrefabAssetID, StringComparison.Ordinal))
                {
                    byAsset = prefabData;
                }
            }

            return byAsset;
        }

        private SaveablePrefab FindFirstSceneBakedMatch(string sceneName)
        {
            foreach (SaveablePrefab prefab in FindAllMatchingPrefabs())
            {
                if (prefab != null &&
                    prefab.gameObject.scene.IsValid() &&
                    prefab.gameObject.scene.name == sceneName &&
                    !prefab.IsAddedAtRuntime)
                {
                    return prefab;
                }
            }

            return null;
        }

        private List<SaveablePrefab> FindAllMatchingPrefabs()
        {
            List<SaveablePrefab> results = new List<SaveablePrefab>();
            foreach (SaveablePrefab prefab in UnityObjectSearch.FindIncludingInactive<SaveablePrefab>())
            {
                if (prefab != null && prefab.PrefabAssetID == diagnosticPrefabAssetID)
                {
                    results.Add(prefab);
                }
            }

            return results;
        }

        private void LogMatches(string label, List<SaveablePrefab> prefabs)
        {
            Debug.Log($"{LogPrefix} {label}: found {prefabs.Count} matching prefabs.");
            foreach (SaveablePrefab prefab in prefabs)
            {
                Debug.Log($"{LogPrefix}   {prefab.name} | Scene='{prefab.gameObject.scene.name}' | UniqueID='{prefab.UniqueID}' | AddedAtRuntime={prefab.IsAddedAtRuntime} | Reuse={prefab.ReuseSceneInstanceOnLoad} | Position={prefab.transform.position}");
            }
        }

        private sealed class DiagnosticSnapshotOrchestrator : SceneLoadOrchestratorBase
        {
            private readonly SaveManager manager;
            private readonly string expectedSceneName;
            private readonly bool gatePendingPrefabsUntilActivated;
            private readonly int activationDelayFrames;
            private Func<string, bool> previousSceneActivationPipeline;
            private bool allowPrefabSpawn;
            private bool sceneActivationGateInstalled;

            public DiagnosticSnapshotOrchestrator(
                SaveManager manager,
                string expectedSceneName,
                bool gatePendingPrefabsUntilActivated,
                int activationDelayFrames)
            {
                this.manager = manager;
                this.expectedSceneName = expectedSceneName;
                this.gatePendingPrefabsUntilActivated = gatePendingPrefabsUntilActivated;
                this.activationDelayFrames = Mathf.Max(0, activationDelayFrames);
            }

            public async Task LoadSceneAsync(string sceneName)
            {
                await OnScenePreLoad(sceneName);

                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (loadOperation == null)
                {
                    throw new InvalidOperationException($"SceneManager.LoadSceneAsync returned null for '{sceneName}'.");
                }

                while (!loadOperation.isDone)
                {
                    await Task.Yield();
                }

                Scene loadedScene = SceneManager.GetSceneByName(sceneName);
                await OnSceneWillActivate(loadedScene);

                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    SceneManager.SetActiveScene(loadedScene);
                }

                OnSceneActivated(loadedScene);
                await Task.Yield();
                await Task.Yield();
            }

            public override async Task OnScenePreLoad(string sceneName)
            {
                Debug.Log($"{LogPrefix} [Orchestrator] OnScenePreLoad('{sceneName}') populating pending prefabs from snapshot.");

                if (gatePendingPrefabsUntilActivated)
                {
                    allowPrefabSpawn = false;
                    previousSceneActivationPipeline = manager.SceneActivationPipeline;
                    manager.SceneActivationPipeline = SceneActivationGate;
                    sceneActivationGateInstalled = true;
                    Debug.Log($"{LogPrefix} [Orchestrator] SceneActivationPipeline gate enabled until OnSceneActivated.");
                }

                await SaveManagerExtensions.PopulatePendingPrefabsFromSnapshotAsync(manager);

                var pendingScenes = manager.GetScenesWithPendingPrefabs();
                Debug.Log($"{LogPrefix} [Orchestrator] Pending scenes after preload: {string.Join(", ", pendingScenes)}.");
            }

            public override async Task OnSceneWillActivate(Scene scene)
            {
                Debug.Log($"{LogPrefix} [Orchestrator] OnSceneWillActivate('{scene.name}') expected='{expectedSceneName}', delayFrames={activationDelayFrames}.");
                for (int i = 0; i < activationDelayFrames; i++)
                {
                    await Task.Yield();
                }
            }

            public override void OnSceneActivated(Scene scene)
            {
                Debug.Log($"{LogPrefix} [Orchestrator] OnSceneActivated('{scene.name}').");
                allowPrefabSpawn = true;

                RestoreSceneActivationPipeline();

                manager.ValidateSceneLoadingTiming("SceneBakedPrefabReuseDiagnostic.Orchestrator", expectedSceneName);

                Coroutine deferredCoroutine = manager.GetPrefabManager != null
                    ? manager.GetPrefabManager.ProcessDeferredPrefabsForScene(scene.name, manager.GetDestroyedGameObjectIDs())
                    : null;

                if (deferredCoroutine != null)
                {
                    Debug.Log($"{LogPrefix} [Orchestrator] Requested deferred prefab processing for scene '{scene.name}'.");
                }
            }

            private bool SceneActivationGate(string sceneName)
            {
                bool previousAllows = previousSceneActivationPipeline?.Invoke(sceneName) ?? true;
                if (!previousAllows)
                    return false;

                return !string.Equals(sceneName, expectedSceneName, StringComparison.Ordinal) || allowPrefabSpawn;
            }

            public void RestoreSceneActivationPipeline()
            {
                if (!gatePendingPrefabsUntilActivated)
                    return;

                if (sceneActivationGateInstalled)
                {
                    manager.SceneActivationPipeline = previousSceneActivationPipeline;
                    sceneActivationGateInstalled = false;
                    Debug.Log($"{LogPrefix} [Orchestrator] SceneActivationPipeline gate released.");
                }
            }
        }
    }
}
#endif
