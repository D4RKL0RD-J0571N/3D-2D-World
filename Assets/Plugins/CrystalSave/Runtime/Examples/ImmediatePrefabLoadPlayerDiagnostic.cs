#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Arawn.CrystalSave.Runtime.Examples
{
    /// <summary>
    /// Repeatable Development Player diagnostic for the immediate-prefab restore path.
    /// A generated diagnostic scene supplies a PrefabRegistry and invokes this component
    /// with Deep Profiling disabled.
    /// </summary>
    [AddComponentMenu("Crystal Save/Diagnostics/Immediate Prefab Load Player Diagnostic")]
    public sealed class ImmediatePrefabLoadPlayerDiagnostic : MonoBehaviour
    {
        private const string LogPrefix = "[CrystalSave][ImmediatePrefabLoadProfile]";

        [SerializeField] private string prefabAssetID = "CSP_IMMEDIATE_LOAD_PROFILE_PREFAB";
        [SerializeField, Min(1)] private int entryCount = 228;
        [SerializeField, Min(0)] private int destroyedEntryStride = 57;
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool quitPlayerWhenComplete = true;

        private bool ownsLoadingState;
        private SaveState previousSaveState;
        private bool QuitPlayerWhenComplete => quitPlayerWhenComplete;

        private IEnumerator Start()
        {
            if (runOnStart)
                yield return Run();
        }

        [ContextMenu("Run Immediate Prefab Load Diagnostic")]
        public void RunFromContextMenu()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;

            PrefabManager prefabManager = GetComponent<PrefabManager>();
            if (prefabManager == null)
            {
#if UNITY_6000_5_OR_NEWER
                prefabManager = FindAnyObjectByType<PrefabManager>();
#else
                prefabManager = FindFirstObjectByType<PrefabManager>();
#endif
            }

            if (prefabManager == null || string.IsNullOrEmpty(prefabAssetID))
            {
                Finish(false, "PrefabManager or diagnostic PrefabAssetID is missing.");
                yield break;
            }

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null || saveManager.StateMachine == null)
            {
                Finish(false, "SaveManager or its state machine is missing.");
                yield break;
            }

            previousSaveState = saveManager.StateMachine.CurrentState;
            if (previousSaveState != SaveState.Loading)
            {
                saveManager.StateMachine.TransitionTo(SaveState.Loading);
                ownsLoadingState = true;
            }

            List<SaveablePrefabData> firstBatch = CreateBatch(positionOffset: Vector3.zero);
            List<string> destroyedIDs = CreateDestroyedIDs();
            int expectedLiveCount = entryCount - destroyedIDs.Count;

            PrefabLoadDiagnosticCounters.Reset();
            var firstStopwatch = Stopwatch.StartNew();
            yield return prefabManager.InstantiatePrefabsCoroutine(
                firstBatch,
                destroyedIDs,
                clearExistingPrefabs: true,
                handleDeferral: false);
            firstStopwatch.Stop();

            PrefabLoadDiagnosticSnapshot firstCounters =
                PrefabLoadDiagnosticCounters.Capture();
            Dictionary<string, GameObject> firstMappings =
                prefabManager.GetInstantiatedPrefabs();
            var firstInstanceIds = new Dictionary<string, int>(
                expectedLiveCount,
                StringComparer.Ordinal);

            if (!ValidateBatch(
                    firstBatch,
                    destroyedIDs,
                    firstMappings,
                    prefabManager.RegisteredPrefabs,
                    expectedLiveCount,
                    firstInstanceIds,
                    out string firstFailure))
            {
                Finish(false, $"First load failed: {firstFailure}");
                yield break;
            }

            if (!ValidateCounters(
                    firstCounters,
                    expectedLiveCount,
                    entryCount,
                    0,
                    out string firstCounterFailure))
            {
                Finish(false, $"First load counters failed: {firstCounterFailure}");
                yield break;
            }

            Vector3 secondOffset = new Vector3(1000f, 5f, -1000f);
            List<SaveablePrefabData> secondBatch = CreateBatch(secondOffset);

            PrefabLoadDiagnosticCounters.Reset();
            var secondStopwatch = Stopwatch.StartNew();
            yield return prefabManager.InstantiatePrefabsCoroutine(
                secondBatch,
                destroyedIDs,
                clearExistingPrefabs: true,
                handleDeferral: false);
            secondStopwatch.Stop();

            PrefabLoadDiagnosticSnapshot secondCounters =
                PrefabLoadDiagnosticCounters.Capture();

            // UnityEngine.Object.Destroy is deferred until the end of the frame.
            // Let the first load's objects finish their lifecycle callbacks before
            // validating the replacement mappings and starting the reuse pass.
            yield return null;

            Dictionary<string, GameObject> secondMappings =
                prefabManager.GetInstantiatedPrefabs();
            var secondInstanceIds = new Dictionary<string, int>(
                expectedLiveCount,
                StringComparer.Ordinal);
            if (!ValidateBatch(
                    secondBatch,
                    destroyedIDs,
                    secondMappings,
                    prefabManager.RegisteredPrefabs,
                    expectedLiveCount,
                    secondInstanceIds,
                    out string secondFailure))
            {
                Finish(false, $"Second load failed: {secondFailure}");
                yield break;
            }

            foreach (var pair in firstInstanceIds)
            {
                if (!secondMappings.TryGetValue(pair.Key, out GameObject current) ||
                    current == null ||
                    UnityObjectHelper.GetUniqueId(current) == pair.Value)
                {
                    Finish(
                        false,
                        $"Second clearing load did not replace the original object for InstanceID '{pair.Key}'.");
                    yield break;
                }
            }

            if (!ValidateCounters(
                    secondCounters,
                    expectedLiveCount,
                    entryCount,
                    expectedLiveCount * 2,
                    out string secondCounterFailure))
            {
                Finish(false, $"Second load counters failed: {secondCounterFailure}");
                yield break;
            }

            // A non-clearing restore intentionally retains already-current
            // mappings without reapplying state. A third pass over the second
            // batch validates that O(1) current-instance reuse path.
            PrefabLoadDiagnosticCounters.Reset();
            var reuseStopwatch = Stopwatch.StartNew();
            yield return prefabManager.InstantiatePrefabsCoroutine(
                secondBatch,
                destroyedIDs,
                clearExistingPrefabs: false,
                handleDeferral: false);
            reuseStopwatch.Stop();

            PrefabLoadDiagnosticSnapshot reuseCounters =
                PrefabLoadDiagnosticCounters.Capture();
            Dictionary<string, GameObject> reuseMappings =
                prefabManager.GetInstantiatedPrefabs();
            if (!ValidateBatch(
                    secondBatch,
                    destroyedIDs,
                    reuseMappings,
                    prefabManager.RegisteredPrefabs,
                    expectedLiveCount,
                    null,
                    out string reuseFailure))
            {
                Finish(false, $"Reuse pass failed: {reuseFailure}");
                yield break;
            }

            foreach (var pair in secondInstanceIds)
            {
                if (!reuseMappings.TryGetValue(pair.Key, out GameObject current) ||
                    current == null ||
                    UnityObjectHelper.GetUniqueId(current) != pair.Value)
                {
                    Finish(
                        false,
                        $"Non-clearing pass did not reuse the current object for InstanceID '{pair.Key}'.");
                    yield break;
                }
            }

            if (!ValidateCounters(
                    reuseCounters,
                    0,
                    entryCount,
                    0,
                    out string reuseCounterFailure))
            {
                Finish(false, $"Reuse pass counters failed: {reuseCounterFailure}");
                yield break;
            }

            Finish(
                true,
                $"entries={entryCount}, destroyed={destroyedIDs.Count}, live={expectedLiveCount}, " +
                $"firstMs={firstStopwatch.Elapsed.TotalMilliseconds:F3}, " +
                $"secondMs={secondStopwatch.Elapsed.TotalMilliseconds:F3}, " +
                $"reuseMs={reuseStopwatch.Elapsed.TotalMilliseconds:F3}, " +
                $"indexedProbes={firstCounters.IndexedResolutionProbes}/{secondCounters.IndexedResolutionProbes}/{reuseCounters.IndexedResolutionProbes}, " +
                $"destroyedIdProbes={firstCounters.DestroyedIdProbes}/{secondCounters.DestroyedIdProbes}/{reuseCounters.DestroyedIdProbes}, " +
                $"registrationOwnerProbes={firstCounters.RegistrationOwnerProbes}/{secondCounters.RegistrationOwnerProbes}/{reuseCounters.RegistrationOwnerProbes}, " +
                $"legacyRegistrationScans={firstCounters.LegacyRegistrationCollectionScans}/{secondCounters.LegacyRegistrationCollectionScans}/{reuseCounters.LegacyRegistrationCollectionScans}, " +
                $"sceneSnapshots={firstCounters.SceneSnapshots}/{secondCounters.SceneSnapshots}/{reuseCounters.SceneSnapshots}, " +
                $"clearCandidateProbes={firstCounters.ClearCandidateProbes}/{secondCounters.ClearCandidateProbes}/{reuseCounters.ClearCandidateProbes}, " +
                $"preserveIdClaimProbes={firstCounters.PreserveIdClaimProbes}/{secondCounters.PreserveIdClaimProbes}/{reuseCounters.PreserveIdClaimProbes}, " +
                $"publicLookups={firstCounters.PublicLookupCalls}/{secondCounters.PublicLookupCalls}/{reuseCounters.PublicLookupCalls}, " +
                $"descriptions={firstCounters.PrefabDescriptions + firstCounters.PrefabDataDescriptions + firstCounters.TransformPathDescriptions}/" +
                $"{secondCounters.PrefabDescriptions + secondCounters.PrefabDataDescriptions + secondCounters.TransformPathDescriptions}/" +
                $"{reuseCounters.PrefabDescriptions + reuseCounters.PrefabDataDescriptions + reuseCounters.TransformPathDescriptions}");
        }

        private List<SaveablePrefabData> CreateBatch(Vector3 positionOffset)
        {
            var batch = new List<SaveablePrefabData>(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                string instanceID = GetInstanceID(i);
                Vector3 position =
                    positionOffset +
                    new Vector3(i % 19, (i / 19) % 3, i / 57);
                var data = new SaveablePrefabData(
                    instanceID,
                    prefabAssetID,
                    instanceID,
                    position,
                    Quaternion.Euler(0f, i % 360, 0f),
                    Vector3.one,
                    parentID: null,
                    isParentSceneObject: false,
                    visibilitySettingsData: null,
                    homeScene: null,
                    disablePooling: true)
                {
                    HasRigidbody = false,
                    ReuseSceneInstanceOnLoad = false,
                    DeferLowPriorityUntilRequested = false,
                    ActiveSelfAtSave = true
                };
                batch.Add(data);
            }

            return batch;
        }

        private List<string> CreateDestroyedIDs()
        {
            var destroyed = new List<string>();
            if (destroyedEntryStride <= 0)
                return destroyed;

            for (int i = destroyedEntryStride - 1; i < entryCount; i += destroyedEntryStride)
                destroyed.Add(GetInstanceID(i));
            return destroyed;
        }

        private bool ValidateBatch(
            List<SaveablePrefabData> batch,
            List<string> destroyedIDs,
            Dictionary<string, GameObject> mappings,
            IReadOnlyCollection<SaveablePrefab> registeredPrefabs,
            int expectedLiveCount,
            Dictionary<string, int> captureInstanceIds,
            out string failure)
        {
            failure = null;
            if (mappings.Count != expectedLiveCount)
            {
                failure =
                    $"Expected {expectedLiveCount} mappings but found {mappings.Count}.";
                return false;
            }

            if (registeredPrefabs == null ||
                registeredPrefabs.Count != expectedLiveCount)
            {
                failure =
                    $"Expected {expectedLiveCount} registered prefabs but found " +
                    $"{registeredPrefabs?.Count ?? 0}.";
                return false;
            }

            var destroyedSet = new HashSet<string>(
                destroyedIDs,
                StringComparer.Ordinal);
            for (int i = 0; i < batch.Count; i++)
            {
                SaveablePrefabData data = batch[i];
                if (destroyedSet.Contains(data.InstanceID))
                {
                    if (mappings.TryGetValue(data.InstanceID, out GameObject destroyed) &&
                        destroyed != null)
                    {
                        failure =
                            $"Destroyed InstanceID '{data.InstanceID}' was instantiated.";
                        return false;
                    }

                    continue;
                }

                if (!mappings.TryGetValue(data.InstanceID, out GameObject instance) ||
                    instance == null)
                {
                    failure = $"Missing mapping for InstanceID '{data.InstanceID}'.";
                    return false;
                }

                if (!instance.TryGetComponent(out SaveablePrefab saveable) ||
                    saveable == null ||
                    !string.Equals(
                        saveable.UniqueID,
                        data.InstanceID,
                        StringComparison.Ordinal))
                {
                    failure =
                        $"Restored identity is incorrect for InstanceID '{data.InstanceID}'.";
                    return false;
                }

                if (SaveManager.Instance != null &&
                    SaveManager.Instance.IsGameObjectDestroyed(data.InstanceID))
                {
                    failure =
                        $"Live replacement InstanceID '{data.InstanceID}' was incorrectly marked destroyed.";
                    return false;
                }

                if ((instance.transform.position - data.Position).sqrMagnitude > 0.0001f ||
                    Quaternion.Angle(instance.transform.rotation, data.Rotation) > 0.01f ||
                    (instance.transform.localScale - data.Scale).sqrMagnitude > 0.0001f)
                {
                    failure =
                        $"Restored transform is incorrect for InstanceID '{data.InstanceID}'.";
                    return false;
                }

                captureInstanceIds?.Add(
                    data.InstanceID,
                    UnityObjectHelper.GetUniqueId(instance));
            }

            return true;
        }

        private static bool ValidateCounters(
            PrefabLoadDiagnosticSnapshot counters,
            int expectedEntryCount,
            int expectedDestroyedIdProbes,
            int expectedClearCandidateProbes,
            out string failure)
        {
            failure = null;
            if (counters.BatchPreparations != 1 ||
                counters.ContextBuilds != 1 ||
                counters.SceneSnapshots != 1)
            {
                failure =
                    $"Expected one preparation/context/snapshot but observed " +
                    $"{counters.BatchPreparations}/{counters.ContextBuilds}/{counters.SceneSnapshots}.";
                return false;
            }

            if (counters.IndexedResolutionProbes != expectedEntryCount)
            {
                failure =
                    $"Expected {expectedEntryCount} indexed probes but observed " +
                    $"{counters.IndexedResolutionProbes}.";
                return false;
            }

            if (counters.DestroyedIdProbes != expectedDestroyedIdProbes)
            {
                failure =
                    $"Expected {expectedDestroyedIdProbes} destroyed-ID probes but observed " +
                    $"{counters.DestroyedIdProbes}.";
                return false;
            }

            if (counters.PublicLookupCalls != 0)
            {
                failure =
                    $"Observed {counters.PublicLookupCalls} public lookup calls.";
                return false;
            }

            if (counters.LegacyRegistrationCollectionScans != 0)
            {
                failure =
                    $"Observed {counters.LegacyRegistrationCollectionScans} legacy registration collection scans.";
                return false;
            }

            if (counters.ClearCandidateProbes != expectedClearCandidateProbes)
            {
                failure =
                    $"Expected {expectedClearCandidateProbes} linear clear-candidate probes but observed " +
                    $"{counters.ClearCandidateProbes}.";
                return false;
            }

            if (counters.PreserveIdClaimProbes != 0)
            {
                failure =
                    $"Expected zero preserve-ID claims but observed {counters.PreserveIdClaimProbes}.";
                return false;
            }

            if (counters.PrefabDescriptions != 0 ||
                counters.PrefabDataDescriptions != 0 ||
                counters.TransformPathDescriptions != 0)
            {
                failure = "Verbose prefab descriptions were constructed.";
                return false;
            }

            if (counters.RigidbodyProbes != 0)
            {
                failure =
                    $"Expected zero Rigidbody probes but observed {counters.RigidbodyProbes}.";
                return false;
            }

            if (counters.RegistrationOwnerProbes != expectedEntryCount)
            {
                failure =
                    $"Expected {expectedEntryCount} O(1) registration-owner probes but observed " +
                    $"{counters.RegistrationOwnerProbes}.";
                return false;
            }

            return true;
        }

        private static string GetInstanceID(int index)
        {
            return $"CSP_IMMEDIATE_LOAD_PROFILE_{index:D4}";
        }

        private void Finish(bool success, string message)
        {
            if (ownsLoadingState)
            {
                SaveManager manager = SaveManager.Instance;
                if (manager != null && manager.StateMachine != null)
                    manager.StateMachine.TransitionTo(previousSaveState);
                ownsLoadingState = false;
            }

            if (success)
                Debug.Log($"{LogPrefix} PASS {message}");
            else
                Debug.LogError($"{LogPrefix} FAIL {message}");

#if !UNITY_EDITOR
            if (QuitPlayerWhenComplete)
                Application.Quit(success ? 0 : 1);
#endif
        }
    }
}
#endif
