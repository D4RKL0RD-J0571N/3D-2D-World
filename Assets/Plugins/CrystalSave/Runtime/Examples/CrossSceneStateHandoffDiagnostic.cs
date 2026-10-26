#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime.Examples
{
    /// <summary>
    /// Play-mode diagnostic for SaveablePrefab cross-scene state handoff.
    /// Created by the editor diagnostic builder.
    /// </summary>
    [AddComponentMenu("Crystal Save/Diagnostics/Cross-Scene State Handoff Diagnostic")]
    public sealed class CrossSceneStateHandoffDiagnostic : MonoBehaviour
    {
        [SerializeField] private string diagnosticPrefabAssetID = "CSP_DIAG_STATE_HANDOFF_ACTOR";
        [SerializeField] private string targetSceneName = "CSP_Diag_Handoff_Target";
        [SerializeField] private string sourceInstanceID = "CSP_DIAG_HANDOFF_SOURCE_INSTANCE";
        [SerializeField] private string targetInstanceID = "CSP_DIAG_HANDOFF_TARGET_INSTANCE";
        [SerializeField] private string markerChildName = "CSP_Diag_HandoffMarker";
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool processAsDeferredPass = true;
        [SerializeField] private bool forceAssetBasedReuseDuringDeferredOff = true;
        [SerializeField] private bool carryRootTransform = true;
        [SerializeField] private bool carryRuntimeOverrides = true;
        [SerializeField] private int preTransitionWaitFrames = 6;
        [SerializeField] private int postRestoreWaitFrames = 8;
        [SerializeField] private Vector3 sourceRuntimePosition = new Vector3(0f, 1f, 0f);
        [SerializeField] private Vector3 sourceRuntimeScale = new Vector3(1.25f, 1.25f, 1.25f);
        [SerializeField] private Vector3 targetDesignPosition = new Vector3(2f, 1f, 0f);
        [SerializeField] private Vector3 targetDesignScale = Vector3.one;
        [SerializeField] private Vector3 markerDesignLocalPosition = new Vector3(0f, 0.75f, 0.45f);
        [SerializeField] private Vector3 markerDesignLocalScale = new Vector3(0.22f, 0.22f, 0.22f);
        [SerializeField] private Vector3 markerRuntimeLocalPosition = new Vector3(0.55f, 1.05f, 0f);
        [SerializeField] private Vector3 markerRuntimeLocalScale = new Vector3(0.34f, 0.18f, 0.34f);
        [SerializeField] private string runtimeChangedName = "CSP_Diag_Handoff_Actor_RuntimeChanged";

        private const string LogPrefix = "[CrystalSave][StateHandoffDiag]";
        private bool running;

        private void Start()
        {
            if (!runOnStart)
                return;

            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run());
        }

        [ContextMenu("Run Cross-Scene State Handoff Diagnostic")]
        public void RunFromContextMenu()
        {
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (running)
                yield break;

            running = true;

            yield return WaitForSaveManager();

            SaveManager manager = SaveManager.Instance;
            if (manager == null || manager.GetPrefabManager == null)
            {
                Debug.LogError($"{LogPrefix} SaveManager or PrefabManager was not available.");
                running = false;
                yield break;
            }

            if (forceAssetBasedReuseDuringDeferredOff && manager.SaveSettings != null)
            {
                manager.SaveSettings.enableAssetBasedReuseDuringDeferred = false;
                Debug.Log($"{LogPrefix} Disabled broad asset-based reuse during deferred processing for this play session. Handoff should still work because it is an explicit opt-in.");
            }

            SaveablePrefab source = FindByInstanceID(sourceInstanceID) ?? FindFirstSceneBakedMatch(SceneManager.GetActiveScene().name);
            if (source == null)
            {
                Debug.LogError($"{LogPrefix} Could not find source scene-baked SaveablePrefab with PrefabAssetID '{diagnosticPrefabAssetID}'.");
                running = false;
                yield break;
            }

            ApplyRuntimeSourceState(source);

            for (int i = 0; i < Mathf.Max(0, preTransitionWaitFrames); i++)
                yield return null;

            SaveablePrefabData data = manager.GetPrefabManager.BuildPrefabData(source);
            if (data == null)
            {
                Debug.LogError($"{LogPrefix} PrefabManager.BuildPrefabData returned null for the handoff source.");
                running = false;
                yield break;
            }

            data.HomeScene = null;
            data.ReuseSceneInstanceOnLoad = false;
            data.DeferLowPriorityUntilRequested = processAsDeferredPass;
            data.EnableCrossSceneStateHandoff = true;
            data.HandoffToSceneBakedPrefabs = true;
            data.HandoffTransform = carryRootTransform;
            data.HandoffRuntimeModifications = carryRuntimeOverrides;

            Debug.Log(
                $"{LogPrefix} Built handoff data. InstanceID='{data.InstanceID}', PrefabID='{data.PrefabID}', " +
                $"Handoff={data.EnableCrossSceneStateHandoff}, CarryTransform={data.HandoffTransform}, CarryRuntimeOverrides={data.HandoffRuntimeModifications}, " +
                $"Deferred={data.DeferLowPriorityUntilRequested}, RuntimeModBytes={(data.RuntimeModificationData != null ? data.RuntimeModificationData.Length : 0)}.");

            yield return RunManualRestoreFlow(manager, data);
            running = false;
        }

        private void ApplyRuntimeSourceState(SaveablePrefab source)
        {
            source.KeepAcrossScenes = false;
            source.ReuseSceneInstanceOnLoad = false;
            source.RememberHomeScene = false;
            source.EnableCrossSceneStateHandoff = true;
            source.HandoffToSceneBakedPrefabs = true;
            source.HandoffTransform = carryRootTransform;
            source.HandoffRuntimeModifications = carryRuntimeOverrides;
            source.TrackChildStateOverrides = true;
            source.TrackChildTransformOverrides = true;

            source.gameObject.name = runtimeChangedName;
            source.transform.position = sourceRuntimePosition;
            source.transform.rotation = Quaternion.identity;
            source.transform.localScale = sourceRuntimeScale;

            Transform marker = source.transform.Find(markerChildName);
            if (marker != null)
            {
                marker.localPosition = markerRuntimeLocalPosition;
                marker.localRotation = Quaternion.Euler(0f, 35f, 0f);
                marker.localScale = markerRuntimeLocalScale;
                marker.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} Source marker child '{markerChildName}' was not found, so runtime override verification will be weaker.");
            }

            Debug.Log(
                $"{LogPrefix} Applied source runtime state. Name='{source.name}', UniqueID='{source.UniqueID}', " +
                $"KeepAcrossScenes={source.KeepAcrossScenes}, Reuse={source.ReuseSceneInstanceOnLoad}, " +
                $"Handoff={source.EnableCrossSceneStateHandoff}, Position={source.transform.position}, Scale={source.transform.localScale}, " +
                $"Marker={DescribeMarker(source.transform.Find(markerChildName))}.");
        }

        private IEnumerator RunManualRestoreFlow(SaveManager manager, SaveablePrefabData data)
        {
            LogMatches("Before target scene load", FindAllMatchingPrefabs());

            AsyncOperation loadOperation = null;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
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
                yield return null;

            for (int i = 0; i < Mathf.Max(0, postRestoreWaitFrames); i++)
                yield return null;

            LogMatches("Before handoff restore", FindAllMatchingPrefabs());

            List<SaveablePrefabData> prefabData = new List<SaveablePrefabData> { data };
            List<string> destroyedIds = manager.GameObjectTracker != null
                ? manager.GameObjectTracker.GetDestroyedGameObjectIDs()
                : new List<string>();

            int removedSourceDestroyedIds = destroyedIds.RemoveAll(id => string.Equals(id, data.InstanceID, StringComparison.Ordinal));
            if (removedSourceDestroyedIds > 0)
                Debug.Log($"{LogPrefix} Removed source InstanceID '{data.InstanceID}' from destroyed IDs before handoff restore.");

            yield return manager.GetPrefabManager.InstantiatePrefabsCoroutine(
                prefabData,
                destroyedIds,
                clearExistingPrefabs: false,
                handleDeferral: !processAsDeferredPass);

            for (int i = 0; i < Mathf.Max(0, postRestoreWaitFrames); i++)
                yield return null;

            List<SaveablePrefab> after = FindAllMatchingPrefabs();
            LogMatches("After handoff restore", after);
            EvaluateResult(after, data);
        }

        private void EvaluateResult(List<SaveablePrefab> matches, SaveablePrefabData data)
        {
            if (matches.Count > 1)
            {
                Debug.LogWarning($"{LogPrefix} DUPLICATE DETECTED: found {matches.Count} prefabs with PrefabAssetID '{diagnosticPrefabAssetID}'. Handoff did not prevent a spawned duplicate.");
                return;
            }

            if (matches.Count == 0)
            {
                Debug.LogError($"{LogPrefix} FAIL: no matching prefab remained after handoff restore.");
                return;
            }

            SaveablePrefab restored = matches[0];
            Transform marker = restored != null ? restored.transform.Find(markerChildName) : null;

            bool sceneBacked = restored != null &&
                               !restored.IsAddedAtRuntime &&
                               restored.gameObject.scene.IsValid() &&
                               string.Equals(restored.gameObject.scene.name, targetSceneName, StringComparison.Ordinal);
            bool idMatches = restored != null && string.Equals(restored.UniqueID, data.InstanceID, StringComparison.Ordinal);
            bool targetIdWasReplaced = restored != null && !string.Equals(restored.UniqueID, targetInstanceID, StringComparison.Ordinal);
            bool nameMatches = restored != null && string.Equals(restored.name, runtimeChangedName, StringComparison.Ordinal);
            bool transformMatches = restored != null && (
                carryRootTransform
                    ? Approximately(restored.transform.position, sourceRuntimePosition) && Approximately(restored.transform.localScale, sourceRuntimeScale)
                    : Approximately(restored.transform.position, targetDesignPosition) && Approximately(restored.transform.localScale, targetDesignScale));
            bool markerMatches = marker != null && (
                carryRuntimeOverrides
                    ? Approximately(marker.localPosition, markerRuntimeLocalPosition) && Approximately(marker.localScale, markerRuntimeLocalScale)
                    : Approximately(marker.localPosition, markerDesignLocalPosition) && Approximately(marker.localScale, markerDesignLocalScale));

            if (sceneBacked && idMatches && targetIdWasReplaced && nameMatches && transformMatches && markerMatches)
            {
                Debug.Log(
                    $"{LogPrefix} PASS: target scene-baked prefab was claimed by cross-scene state handoff. " +
                    $"UniqueID='{restored.UniqueID}', Name='{restored.name}', Position={restored.transform.position}, " +
                    $"Marker={DescribeMarker(marker)}.");
                return;
            }

            Debug.LogError(
                $"{LogPrefix} FAIL: handoff result did not match expectations. " +
                $"sceneBacked={sceneBacked}, idMatches={idMatches}, targetIdWasReplaced={targetIdWasReplaced}, " +
                $"nameMatches={nameMatches}, transformMatches={transformMatches}, markerMatches={markerMatches}. " +
                $"ActualID='{(restored != null ? restored.UniqueID : "<null>")}', ExpectedID='{data.InstanceID}', " +
                $"ActualName='{(restored != null ? restored.name : "<null>")}', ExpectedName='{runtimeChangedName}', " +
                $"ActualPosition={(restored != null ? restored.transform.position.ToString() : "<null>")}, Marker={DescribeMarker(marker)}.");
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
                Debug.LogWarning($"{LogPrefix} Timed out waiting for SaveManager initialization.");
        }

        private SaveablePrefab FindFirstSceneBakedMatch(string sceneName)
        {
            foreach (SaveablePrefab prefab in FindAllMatchingPrefabs())
            {
                if (prefab != null &&
                    prefab.gameObject.scene.IsValid() &&
                    string.Equals(prefab.gameObject.scene.name, sceneName, StringComparison.Ordinal) &&
                    !prefab.IsAddedAtRuntime)
                    return prefab;
            }

            return null;
        }

        private SaveablePrefab FindByInstanceID(string instanceID)
        {
            foreach (SaveablePrefab prefab in FindAllMatchingPrefabs())
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
                if (prefab != null && string.Equals(prefab.PrefabAssetID, diagnosticPrefabAssetID, StringComparison.Ordinal))
                    results.Add(prefab);
            }

            return results;
        }

        private void LogMatches(string label, List<SaveablePrefab> prefabs)
        {
            Debug.Log($"{LogPrefix} {label}: found {prefabs.Count} matching prefabs.");
            foreach (SaveablePrefab prefab in prefabs)
            {
                Transform marker = prefab != null ? prefab.transform.Find(markerChildName) : null;
                Debug.Log(
                    $"{LogPrefix}   {prefab.name} | Scene='{SceneLabel(prefab)}' | UniqueID='{prefab.UniqueID}' | " +
                    $"AddedAtRuntime={prefab.IsAddedAtRuntime} | KeepAcrossScenes={prefab.KeepAcrossScenes} | " +
                    $"Reuse={prefab.ReuseSceneInstanceOnLoad} | Handoff={prefab.EnableCrossSceneStateHandoff} | " +
                    $"Position={prefab.transform.position} | Scale={prefab.transform.localScale} | Marker={DescribeMarker(marker)}");
            }
        }

        private static string SceneLabel(SaveablePrefab prefab)
        {
            if (prefab == null)
                return "<null>";

            Scene scene = prefab.gameObject.scene;
            return scene.IsValid() ? scene.name : "<invalid>";
        }

        private static string DescribeMarker(Transform marker)
        {
            if (marker == null)
                return "<missing>";

            return $"localPosition={marker.localPosition}, localScale={marker.localScale}, active={marker.gameObject.activeSelf}";
        }

        private static bool Approximately(Vector3 lhs, Vector3 rhs)
        {
            return Vector3.SqrMagnitude(lhs - rhs) <= 0.0001f;
        }
    }
}
#endif
