#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime.Examples
{
    [AddComponentMenu("Crystal Save/Diagnostics/Scene-Baked Prefab Save Slot Diagnostic")]
    public sealed class SceneBakedPrefabSaveLoadSlotDiagnostic : MonoBehaviour
    {
        [SerializeField] private string diagnosticPrefabAssetID = "CSP_DIAG_SCENE_BAKED_ACTOR";
        [SerializeField] private string targetSceneName = "CSP_Diag_Target";
        [SerializeField] private string savedInstanceID = "CSP_DIAG_SOURCE_INSTANCE";
        [SerializeField] private string freshTargetInstanceID = "CSP_DIAG_TARGET_INSTANCE";
        [SerializeField] private int diagnosticSaveSlotNumber = 1;
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool saveWhenClaimed = true;
        [SerializeField] private bool loadWhenFreshTarget = true;
        [SerializeField] private bool restoreLastActiveSceneOnLoad = false;
        [SerializeField] private bool requestDeferredAfterSlotLoad = true;
        [SerializeField] private int startupDelayFrames = 30;
        [SerializeField] private int postLoadWaitFrames = 10;
        [SerializeField] private int postDeferredRequestWaitFrames = 10;
        [SerializeField] private float loadTimeoutSeconds = 20f;
        [SerializeField] private Vector3 fallbackExpectedSavedPosition = new Vector3(0f, 1f, 0f);

        private const string LogPrefix = "[CrystalSave][SceneBakedSaveLoadDiag]";
        private bool running;

        private void Start()
        {
            if (runOnStart)
            {
                StartCoroutine(Run());
            }
        }

        [ContextMenu("Run Scene-Baked Save Slot Diagnostic")]
        public void RunFromContextMenu()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (running)
                yield break;

            running = true;

            yield return WaitForSaveManager();

            SaveManager manager = SaveManager.Instance;
            if (manager == null)
            {
                Debug.LogError($"{LogPrefix} SaveManager.Instance was not available.");
                running = false;
                yield break;
            }

            if (!string.Equals(SceneManager.GetActiveScene().name, targetSceneName, StringComparison.Ordinal))
            {
                Debug.Log($"{LogPrefix} Skipping because active scene is '{SceneManager.GetActiveScene().name}', not target scene '{targetSceneName}'.");
                running = false;
                yield break;
            }

            Task slotsTask = manager.WaitForSaveSlotsAsync();
            yield return WaitForTask(slotsTask, "save slot initialization");
            if (!TaskSucceeded(slotsTask, "save slot initialization"))
            {
                running = false;
                yield break;
            }

            for (int i = 0; i < startupDelayFrames; i++)
            {
                yield return null;
            }

            List<SaveablePrefab> matches = FindAllMatchingPrefabs();
            LogMatches("Startup state", matches);

            SaveablePrefab claimed = FindByInstanceID(matches, savedInstanceID);
            if (claimed != null)
            {
                if (saveWhenClaimed)
                    yield return SaveClaimedState(manager, claimed);
                else
                    Debug.Log($"{LogPrefix} Claimed actor found, but saveWhenClaimed is disabled.");

                running = false;
                yield break;
            }

            SaveablePrefab freshTarget = FindByInstanceID(matches, freshTargetInstanceID);
            if (freshTarget != null)
            {
                if (loadWhenFreshTarget)
                    yield return LoadAndVerifySavedState(manager);
                else
                    Debug.Log($"{LogPrefix} Fresh target actor found, but loadWhenFreshTarget is disabled.");

                running = false;
                yield break;
            }

            Debug.LogWarning($"{LogPrefix} Could not determine phase. Expected claimed ID '{savedInstanceID}' or fresh target ID '{freshTargetInstanceID}'.");
            running = false;
        }

        private IEnumerator SaveClaimedState(SaveManager manager, SaveablePrefab claimed)
        {
            Debug.Log($"{LogPrefix} SAVE PHASE: claimed scene-baked actor is ready. Name='{claimed.name}', UniqueID='{claimed.UniqueID}', Position={claimed.transform.position}. Saving slot {diagnosticSaveSlotNumber}.");

            Task saveTask = manager.SaveImmediateAsync(diagnosticSaveSlotNumber, targetSceneName);
            yield return WaitForTask(saveTask, "diagnostic save");
            if (!TaskSucceeded(saveTask, "diagnostic save"))
                yield break;

            Task<SaveData> dataTask = manager.LoadSaveDataForSlotAsync(diagnosticSaveSlotNumber);
            yield return WaitForTask(dataTask, "diagnostic saved data readback");

            SaveablePrefabData savedData = dataTask.Status == TaskStatus.RanToCompletion
                ? FindSavedPrefabData(dataTask.Result)
                : null;

            if (savedData == null)
            {
                Debug.LogWarning($"{LogPrefix} SAVE PHASE finished, but readback did not find PrefabID '{diagnosticPrefabAssetID}' in slot {diagnosticSaveSlotNumber}.");
                yield break;
            }

            Debug.Log($"{LogPrefix} SAVE PHASE PASS: slot {diagnosticSaveSlotNumber} contains InstanceID='{savedData.InstanceID}', PrefabID='{savedData.PrefabID}', Position={savedData.Position}, Scale={savedData.Scale}, HomeScene='{savedData.HomeScene}', Reuse={savedData.ReuseSceneInstanceOnLoad}, Deferred={savedData.DeferLowPriorityUntilRequested}, HasTransformOverride={savedData.HasTransformOverride}, HasParentOverride={savedData.HasParentOverride}, UsesOptimizationFlags={savedData.UsesOptimizationFlags}.");
        }

        private IEnumerator LoadAndVerifySavedState(SaveManager manager)
        {
            Debug.Log($"{LogPrefix} LOAD PHASE: fresh baked target found. Loading slot {diagnosticSaveSlotNumber}. restoreLastActiveScene={restoreLastActiveSceneOnLoad}.");

            Task<SaveData> beforeLoadDataTask = manager.LoadSaveDataForSlotAsync(diagnosticSaveSlotNumber);
            yield return WaitForTask(beforeLoadDataTask, "diagnostic saved data pre-load read");

            SaveablePrefabData expectedData = beforeLoadDataTask.Status == TaskStatus.RanToCompletion
                ? FindSavedPrefabData(beforeLoadDataTask.Result)
                : null;

            if (expectedData == null)
            {
                Debug.LogError($"{LogPrefix} LOAD PHASE FAIL: slot {diagnosticSaveSlotNumber} did not contain a saved prefab for PrefabID '{diagnosticPrefabAssetID}'. Run the source scene transition/save phase first.");
                yield break;
            }

            Debug.Log($"{LogPrefix} LOAD PHASE expected saved data: InstanceID='{expectedData.InstanceID}', PrefabID='{expectedData.PrefabID}', Position={expectedData.Position}, Scale={expectedData.Scale}, HomeScene='{expectedData.HomeScene}', Reuse={expectedData.ReuseSceneInstanceOnLoad}, Deferred={expectedData.DeferLowPriorityUntilRequested}, HasTransformOverride={expectedData.HasTransformOverride}, HasParentOverride={expectedData.HasParentOverride}, UsesOptimizationFlags={expectedData.UsesOptimizationFlags}.");

            Task<LoadResult> loadTask = manager.LoadSaveSlotAsync(
                diagnosticSaveSlotNumber,
                restoreLastActiveSceneOnLoad,
                loadAsync: false,
                allowSceneActivation: true,
                timeout: TimeSpan.FromSeconds(Mathf.Max(1f, loadTimeoutSeconds)));

            yield return WaitForTask(loadTask, "diagnostic load");

            if (loadTask.Status != TaskStatus.RanToCompletion || !loadTask.Result.Success)
            {
                string error = loadTask.Status == TaskStatus.RanToCompletion ? loadTask.Result.ErrorMessage : TaskError(loadTask);
                Debug.LogError($"{LogPrefix} LOAD PHASE FAIL: LoadSaveSlotAsync failed. {error}");
                yield break;
            }

            if (expectedData.DeferLowPriorityUntilRequested)
            {
                if (requestDeferredAfterSlotLoad)
                {
                    yield return RequestDeferredSavedPrefab(manager, expectedData);
                }
                else
                {
                    Debug.LogWarning($"{LogPrefix} LOAD PHASE: saved prefab is deferred and requestDeferredAfterSlotLoad is disabled. The baked target is expected to stay unchanged until gameplay requests deferred prefabs.");
                }
            }

            for (int i = 0; i < postLoadWaitFrames; i++)
            {
                yield return null;
            }

            List<SaveablePrefab> matches = FindAllMatchingPrefabs();
            LogMatches("After slot load", matches);
            EvaluateLoadedState(matches, expectedData);
        }

        private IEnumerator RequestDeferredSavedPrefab(SaveManager manager, SaveablePrefabData expectedData)
        {
            if (manager == null || manager.GetPrefabManager == null || expectedData == null)
                yield break;

            Debug.Log($"{LogPrefix} LOAD PHASE: saved prefab is deferred; requesting deferred restore for InstanceID='{expectedData.InstanceID}', PrefabID='{expectedData.PrefabID}'.");

            Coroutine deferredRoutine = null;
            if (!string.IsNullOrEmpty(expectedData.InstanceID))
            {
                deferredRoutine = manager.GetPrefabManager.ProcessDeferredPrefabByUniqueID(
                    expectedData.InstanceID,
                    manager.GetDestroyedGameObjectIDs());
            }

            if (deferredRoutine == null && !string.IsNullOrEmpty(expectedData.PrefabID))
            {
                deferredRoutine = manager.GetPrefabManager.ProcessDeferredPrefabsForAsset(
                    expectedData.PrefabID,
                    manager.GetDestroyedGameObjectIDs());
            }

            if (deferredRoutine == null)
            {
                Debug.Log($"{LogPrefix} LOAD PHASE: no queued deferred prefab was found for InstanceID='{expectedData.InstanceID}', PrefabID='{expectedData.PrefabID}'. It may already have been claimed during the slot load.");
                yield break;
            }

            yield return deferredRoutine;

            for (int i = 0; i < postDeferredRequestWaitFrames; i++)
            {
                yield return null;
            }
        }

        private void EvaluateLoadedState(List<SaveablePrefab> matches, SaveablePrefabData expectedData)
        {
            if (matches.Count > 1)
            {
                Debug.LogWarning($"{LogPrefix} LOAD PHASE FAIL: duplicate actors found after load. Count={matches.Count}.");
                return;
            }

            if (matches.Count == 0)
            {
                Debug.LogError($"{LogPrefix} LOAD PHASE FAIL: no actor with PrefabAssetID '{diagnosticPrefabAssetID}' remained after load.");
                return;
            }

            SaveablePrefab restored = matches[0];
            bool sceneBacked = restored != null &&
                               !restored.IsAddedAtRuntime &&
                               restored.gameObject.scene.IsValid() &&
                               string.Equals(restored.gameObject.scene.name, targetSceneName, StringComparison.Ordinal);
            bool idMatches = restored != null &&
                             string.Equals(restored.UniqueID, expectedData.InstanceID, StringComparison.Ordinal);
            bool stillFreshTarget = restored != null &&
                                    string.Equals(restored.UniqueID, freshTargetInstanceID, StringComparison.Ordinal);
            Vector3 expectedPosition = expectedData.HasTransformOverride ? expectedData.Position : fallbackExpectedSavedPosition;
            bool positionMatches = restored != null && Approximately(restored.transform.position, expectedPosition);
            bool scaleMatches = restored != null && Approximately(restored.transform.localScale, expectedData.Scale);

            if (stillFreshTarget)
            {
                Debug.LogError($"{LogPrefix} LOAD PHASE FAIL: actor stayed on fresh target ID '{freshTargetInstanceID}', so saved slot data did not reclaim the scene-baked prefab.");
                return;
            }

            if (sceneBacked && idMatches && positionMatches && scaleMatches)
            {
                Debug.Log($"{LogPrefix} LOAD PHASE PASS: saved slot reclaimed scene-baked target. UniqueID='{restored.UniqueID}', Position={restored.transform.position}, Scale={restored.transform.localScale}.");
                return;
            }

            Debug.LogError(
                $"{LogPrefix} LOAD PHASE FAIL: saved slot did not restore expected scene-baked state. " +
                $"sceneBacked={sceneBacked}, idMatches={idMatches}, positionMatches={positionMatches}, scaleMatches={scaleMatches}. " +
                $"ExpectedID='{expectedData.InstanceID}', ActualID='{(restored != null ? restored.UniqueID : "<null>")}', " +
                $"ExpectedPosition={expectedPosition}, ActualPosition={(restored != null ? restored.transform.position.ToString() : "<null>")}.");
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

        private bool TaskSucceeded(Task task, string label)
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

        private SaveablePrefabData FindSavedPrefabData(SaveData data)
        {
            if (data?.Prefabs == null)
                return null;

            SaveablePrefabData bySavedInstance = null;
            foreach (SaveablePrefabData prefabData in data.Prefabs)
            {
                if (prefabData == null)
                    continue;

                if (string.Equals(prefabData.InstanceID, savedInstanceID, StringComparison.Ordinal))
                    return prefabData;

                if (bySavedInstance == null &&
                    string.Equals(prefabData.PrefabID, diagnosticPrefabAssetID, StringComparison.Ordinal))
                {
                    bySavedInstance = prefabData;
                }
            }

            return bySavedInstance;
        }

        private SaveablePrefab FindByInstanceID(List<SaveablePrefab> prefabs, string instanceID)
        {
            foreach (SaveablePrefab prefab in prefabs)
            {
                if (prefab != null && string.Equals(prefab.UniqueID, instanceID, StringComparison.Ordinal))
                    return prefab;
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

        private static bool Approximately(Vector3 lhs, Vector3 rhs)
        {
            return Vector3.SqrMagnitude(lhs - rhs) <= 0.0001f;
        }
    }
}
#endif
