#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime.Examples
{
    /// <summary>
    /// Play-mode repro driver for scene-baked SaveablePrefabs that use Keep Across Scenes.
    /// Created by the editor diagnostic builder.
    /// </summary>
    [AddComponentMenu("Crystal Save/Diagnostics/Keep Across Scenes Prefab Diagnostic")]
    public sealed class KeepAcrossScenesPrefabDiagnostic : MonoBehaviour
    {
        [SerializeField] private string diagnosticPrefabAssetID = "CSP_DIAG_KEEP_ACROSS_ACTOR";
        [SerializeField] private string targetSceneName = "CSP_Diag_KAS_Target";
        [SerializeField] private string sourceInstanceID = "CSP_DIAG_KAS_SOURCE_INSTANCE";
        [SerializeField] private string targetInstanceID = "CSP_DIAG_KAS_TARGET_INSTANCE";
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool forceSourceKeepAcrossScenes = true;
        [SerializeField] private bool forceSourceReuseSceneInstanceOnLoad = true;
        [SerializeField] private int preTransitionWaitFrames = 6;
        [SerializeField] private int postTransitionWaitFrames = 12;
        [SerializeField] private Vector3 sourceRuntimePosition = new Vector3(0f, 1f, 0f);
        [SerializeField] private Vector3 sourceRuntimeScale = new Vector3(1.25f, 1.25f, 1.25f);
        [SerializeField] private Vector3 targetDesignPosition = new Vector3(2f, 1f, 0f);
        [SerializeField] private string runtimeChangedName = "CSP_Diag_KAS_Actor_RuntimeChanged";
        [SerializeField] private string targetDesignName = "CSP_Diag_KAS_Actor_TargetBaked";

        private const string LogPrefix = "[CrystalSave][KeepAcrossPrefabDiag]";
        private bool running;

        private void Start()
        {
            if (runOnStart)
            {
                DontDestroyOnLoad(gameObject);
                StartCoroutine(Run());
            }
        }

        [ContextMenu("Run Keep Across Scenes Prefab Diagnostic")]
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

            yield return WaitForOptionalSaveManager();

            List<SaveablePrefab> initialMatches = FindAllMatchingPrefabs();
            LogMatches("Initial state", initialMatches);

            SaveablePrefab source = FindByInstanceID(initialMatches, sourceInstanceID) ?? FindFirstMatchingPrefab(initialMatches);
            if (source == null)
            {
                Debug.LogError($"{LogPrefix} Could not find a source SaveablePrefab with PrefabAssetID '{diagnosticPrefabAssetID}'.");
                running = false;
                yield break;
            }

            ApplyRuntimeSourceState(source);

            for (int i = 0; i < Mathf.Max(0, preTransitionWaitFrames); i++)
                yield return null;

            List<SaveablePrefab> beforeTransition = FindAllMatchingPrefabs();
            LogMatches("Before target scene load", beforeTransition);

            AsyncOperation loadOperation = null;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogError($"{LogPrefix} Could not load target scene '{targetSceneName}'. Make sure it is in Build Settings. {exception.Message}");
                running = false;
                yield break;
            }

            if (loadOperation == null)
            {
                Debug.LogError($"{LogPrefix} SceneManager.LoadSceneAsync returned null for '{targetSceneName}'.");
                running = false;
                yield break;
            }

            while (!loadOperation.isDone)
                yield return null;

            for (int i = 0; i < Mathf.Max(0, postTransitionWaitFrames); i++)
                yield return null;

            List<SaveablePrefab> afterTransition = FindAllMatchingPrefabs();
            LogMatches("After target scene load", afterTransition);
            EvaluateResult(afterTransition);

            running = false;
        }

        private void ApplyRuntimeSourceState(SaveablePrefab source)
        {
            source.gameObject.name = runtimeChangedName;
            source.transform.position = sourceRuntimePosition;
            source.transform.rotation = Quaternion.identity;
            source.transform.localScale = sourceRuntimeScale;

            if (forceSourceKeepAcrossScenes)
                source.KeepAcrossScenes = true;

            if (forceSourceReuseSceneInstanceOnLoad)
                source.ReuseSceneInstanceOnLoad = true;

            source.RememberHomeScene = false;

            PersistentManager.MakePersistent(source.gameObject, source.KeepAcrossScenes);

            Debug.Log(
                $"{LogPrefix} Applied runtime source state. " +
                $"Name='{source.name}', UniqueID='{source.UniqueID}', Scene='{SceneLabel(source)}', " +
                $"KeepAcrossScenes={source.KeepAcrossScenes}, Reuse={source.ReuseSceneInstanceOnLoad}, " +
                $"Position={source.transform.position}, Scale={source.transform.localScale}.");
        }

        private void EvaluateResult(List<SaveablePrefab> matches)
        {
            SaveablePrefab source = FindByInstanceID(matches, sourceInstanceID);
            SaveablePrefab target = FindByInstanceID(matches, targetInstanceID);

            bool sourcePersisted = source != null && IsDontDestroyOnLoadScene(source);
            bool sourceHasRuntimeState = source != null &&
                                         string.Equals(source.name, runtimeChangedName, StringComparison.Ordinal) &&
                                         Approximately(source.transform.position, sourceRuntimePosition) &&
                                         Approximately(source.transform.localScale, sourceRuntimeScale);

            bool targetExists = target != null;
            bool targetStillDesignState = target != null &&
                                          string.Equals(target.name, targetDesignName, StringComparison.Ordinal) &&
                                          Approximately(target.transform.position, targetDesignPosition);

            bool duplicateByKnownIds = source != null && target != null && source != target;
            bool duplicateByAsset = matches.Count > 1;

            if (duplicateByKnownIds)
            {
                Debug.LogWarning(
                    $"{LogPrefix} REPRODUCED: source KeepAcrossScenes prefab survived and the target scene baked copy also exists. " +
                    $"Count={matches.Count}, SourceScene='{SceneLabel(source)}', TargetScene='{SceneLabel(target)}', " +
                    $"sourcePersisted={sourcePersisted}, sourceHasRuntimeState={sourceHasRuntimeState}, targetStillDesignState={targetStillDesignState}. " +
                    "This matches the duplicate workflow Kuzai described.");
                return;
            }

            if (duplicateByAsset)
            {
                Debug.LogWarning(
                    $"{LogPrefix} DUPLICATE DETECTED: found {matches.Count} prefabs with PrefabAssetID '{diagnosticPrefabAssetID}', " +
                    "but they did not match the expected source/target diagnostic InstanceIDs.");
                return;
            }

            if (matches.Count == 1)
            {
                SaveablePrefab only = matches[0];
                bool hasRuntimeState = only != null &&
                                       string.Equals(only.name, runtimeChangedName, StringComparison.Ordinal) &&
                                       Approximately(only.transform.position, sourceRuntimePosition) &&
                                       Approximately(only.transform.localScale, sourceRuntimeScale);

                if (hasRuntimeState)
                {
                    Debug.Log(
                        $"{LogPrefix} NO-DUPLICATE PASS: only one matching prefab remains and it carries the runtime source state. " +
                        $"UniqueID='{only.UniqueID}', Scene='{SceneLabel(only)}'.");
                    return;
                }

                Debug.LogError(
                    $"{LogPrefix} INCONCLUSIVE: only one matching prefab remains, but it does not carry the runtime source state. " +
                    $"UniqueID='{only?.UniqueID ?? "<null>"}', Scene='{SceneLabel(only)}', Position={(only != null ? only.transform.position.ToString() : "<null>")}.");
                return;
            }

            if (!targetExists)
            {
                Debug.LogError(
                    $"{LogPrefix} FAIL: target scene baked copy with InstanceID '{targetInstanceID}' was not found after loading '{targetSceneName}'.");
                return;
            }

            Debug.LogError($"{LogPrefix} FAIL: no matching prefabs remained after scene transition.");
        }

        private IEnumerator WaitForOptionalSaveManager()
        {
            const int maxFrames = 600;
            int frame = 0;

            while ((SaveManager.Instance == null || !SaveManager.IsInitialized) && frame < maxFrames)
            {
                frame++;
                yield return null;
            }

            if (SaveManager.Instance == null || !SaveManager.IsInitialized)
            {
                Debug.LogWarning($"{LogPrefix} SaveManager did not initialize before timeout. Continuing because this diagnostic primarily tests Unity scene persistence.");
            }
        }

        private SaveablePrefab FindFirstMatchingPrefab(List<SaveablePrefab> prefabs)
        {
            foreach (SaveablePrefab prefab in prefabs)
            {
                if (prefab != null && string.Equals(prefab.PrefabAssetID, diagnosticPrefabAssetID, StringComparison.Ordinal))
                    return prefab;
            }

            return null;
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
                Debug.Log(
                    $"{LogPrefix}   {prefab.name} | Scene='{SceneLabel(prefab)}' | UniqueID='{prefab.UniqueID}' | " +
                    $"AddedAtRuntime={prefab.IsAddedAtRuntime} | KeepAcrossScenes={prefab.KeepAcrossScenes} | " +
                    $"Reuse={prefab.ReuseSceneInstanceOnLoad} | Position={prefab.transform.position} | Scale={prefab.transform.localScale}");
            }
        }

        private static bool IsDontDestroyOnLoadScene(SaveablePrefab prefab)
        {
            return prefab != null &&
                   prefab.gameObject.scene.IsValid() &&
                   string.Equals(prefab.gameObject.scene.name, "DontDestroyOnLoad", StringComparison.Ordinal);
        }

        private static string SceneLabel(SaveablePrefab prefab)
        {
            if (prefab == null)
                return "<null>";

            Scene scene = prefab.gameObject.scene;
            return scene.IsValid() ? scene.name : "<invalid>";
        }

        private static bool Approximately(Vector3 lhs, Vector3 rhs)
        {
            return Vector3.SqrMagnitude(lhs - rhs) <= 0.0001f;
        }
    }
}
#endif
