#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>One-stop shop for instantiating SaveablePrefab assets.</summary>
	public static class SaveablePrefabFactory
	{
                private sealed class PendingDestroyRequest
                {
                        internal readonly int InstanceId;
                        internal readonly float DueTime;
                        internal SaveablePrefab Instance;
                        internal UnityMainThreadDispatcher Runner;
                        internal Coroutine Coroutine;

                        internal PendingDestroyRequest(
                                int instanceId,
                                float dueTime,
                                SaveablePrefab instance,
                                UnityMainThreadDispatcher runner)
                        {
                                InstanceId = instanceId;
                                DueTime = dueTime;
                                Instance = instance;
                                Runner = runner;
                        }

                        internal void ReleaseReferences()
                        {
                                Instance = null;
                                Runner = null;
                                Coroutine = null;
                        }
                }

                private static readonly Dictionary<int, PendingDestroyRequest> pendingDestroyRequests = new();

                [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
                private static void ResetDelayedDestroyState()
                {
                        foreach (PendingDestroyRequest request in pendingDestroyRequests.Values)
                                request.ReleaseReferences();

                        pendingDestroyRequests.Clear();
                }

                /// <remarks>
                /// • Pass the *prefab asset* that already carries a SaveablePrefab component.
                /// • If the asset has no SaveablePrefab the helper adds one automatically.
                /// • Respects <see cref="SaveSettings.usePrefabPooling"/> – when enabled instances are
                ///   spawned from <see cref="SaveablePrefabPoolCache"/> and returned via
                ///   <see cref="Destroy(SaveablePrefab)"/> which calls <c>TryDespawn</c>; otherwise classic
                ///   <c>Instantiate</c>/<c>Destroy</c> is used.
                /// • Individual prefab "Disable Pooling" settings always take precedence over global 
                ///   pooling settings and overrides, checked in this order:
                ///   1. <see cref="SaveablePrefab.DisablePooling"/> property on the component
                ///   2. "Disable Pooling" setting in <see cref="PrefabRegistry"/> entry
                /// </remarks>
                public static SaveablePrefab Instantiate(
                        GameObject prefabAsset,
                        Vector3 position,
                        Quaternion rotation,
                        string sceneName,
                        Transform parent = null,
                        bool registerWithSaveSystem = true,
                        bool? overrideUsePooling = null)
                {
                        if (!string.IsNullOrEmpty(sceneName))
                        {
                                var scene = SceneManager.GetSceneByName(sceneName);
                                if (scene.IsValid() && scene.isLoaded)
                                {
                                        using (SaveablePrefabPoolCache.SuppressDisposal())
                                                SceneManager.SetActiveScene(scene);
                                }
                        }
                        return Instantiate(prefabAsset, position, rotation, parent, registerWithSaveSystem, overrideUsePooling);
                }

                public static SaveablePrefab Instantiate(
                        GameObject prefabAsset,
                        Vector3 position,
                        Quaternion rotation,
                        Transform parent = null,
                        bool registerWithSaveSystem = true,
                        bool? overrideUsePooling = null)
                {
                        if (!prefabAsset)
                        {
                                Debug.LogWarning("SaveablePrefabFactory.Instantiate: prefabAsset is null");
                                return null;
                        }

                        var settings = SaveManager.Instance?.SaveSettings;
                        bool usePooling = settings?.usePrefabPooling ?? false;

                        if (overrideUsePooling.HasValue)
                                usePooling = overrideUsePooling.Value;

                        var prefabComp = prefabAsset.GetComponent<SaveablePrefab>() ??
                                         prefabAsset.AddComponent<SaveablePrefab>();

                        PrefabRegistry prefabRegistry = null;

                        if (string.IsNullOrEmpty(prefabComp.PrefabAssetID))
                        {
                                prefabComp.GenerateAndRegisterPrefabAssetIDAtRuntime();
                        }
                        else
                        {
                                prefabRegistry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
                                prefabRegistry?.TryAddPrefab(prefabComp.PrefabAssetID, prefabAsset, out _);
                        }

                        prefabRegistry ??= AssetProvider.Load<PrefabRegistry>("PrefabRegistry");

                        // Check if pooling is disabled directly on the SaveablePrefab component - this always takes precedence
                        if (usePooling && prefabComp.DisablePooling)
                        {
                                usePooling = false;
                        }
                        // If not disabled on component, check the PrefabRegistry setting
                        else if (usePooling && prefabRegistry != null &&
                            prefabRegistry.IsPoolingDisabled(prefabComp.PrefabAssetID))
                        {
                                usePooling = false;
                        }

                        if (usePooling)
                        {
                                int poolSize = prefabRegistry?.ResolvePoolSize(
                                                       prefabComp.PrefabAssetID,
                                                       settings?.defaultPrefabPoolSize ?? 0)
                                                ?? (settings?.defaultPrefabPoolSize ?? 0);

                                var pool = SaveablePrefabPoolCache.Get(
                                        prefabComp,
                                        poolSize,
                                        registerWithSaveSystem);

                                SaveablePrefab sp = pool?.Spawn(position, rotation);
                                if (sp == null) return null;
                                if (parent) sp.transform.SetParent(parent, true);

                                sp.SetOriginalPrefabAsset(prefabAsset);
                                sp.MarkAsAddedAtRuntime();

                                if (!registerWithSaveSystem && sp.RegisterWithSaveSystem)
                                {
                                        sp.UnregisterFromSaving();
                                        sp.RegisterWithSaveSystem = false;
                                }

                                return sp;
                        }

                        // 1 ▸ instantiate + optional parenting
                        GameObject go = UnityEngine.Object.Instantiate(prefabAsset, position, rotation);
                        if (parent) go.transform.SetParent(parent, true);

                        // 2 ▸ ensure SaveablePrefab component
                        SaveablePrefab spFallback = go.GetComponent<SaveablePrefab>() ??
                                                                  go.AddComponent<SaveablePrefab>();

                        // 3 ▸ honor save-system flag and initialize so inactive instances
                        //     get a UniqueID and register correctly
                        spFallback.RegisterWithSaveSystem = registerWithSaveSystem;
                        spFallback.InitializeInstance();

                        // 4 ▸ store reference to original asset (needed for diff logic)
                        spFallback.SetOriginalPrefabAsset(prefabAsset);

                        // 5 ▸ guarantee PrefabAssetID + registry entry
                        if (string.IsNullOrEmpty(spFallback.PrefabAssetID))
                                spFallback.GenerateAndRegisterPrefabAssetIDAtRuntime();
                        else
                                prefabRegistry?.TryAddPrefab(spFallback.PrefabAssetID, prefabAsset, out _);

                        // 6 ▸ runtime flag + optional save-system registration
                        spFallback.MarkAsAddedAtRuntime();
                        if (registerWithSaveSystem) spFallback.RegisterForSaving();

                        return spFallback;
                }

                /// <summary>
                /// Destroys or returns a runtime prefab instance to its Crystal Save pool immediately.
                /// </summary>
                /// <remarks>
                /// Any delayed destruction already scheduled for this instance is cancelled first.
                /// </remarks>
                public static void Destroy(SaveablePrefab inst)
                {
                        if (!inst) return;

                        CancelPendingDestroy(inst);
                        DestroyImmediate(inst);
                }

                /// <summary>
                /// Destroys or returns a runtime prefab instance to its Crystal Save pool after a delay.
                /// </summary>
                /// <param name="inst">The runtime prefab instance to destroy or despawn.</param>
                /// <param name="delay">
                /// Delay in scaled seconds. A value less than or equal to zero uses the immediate path.
                /// </param>
                /// <remarks>
                /// The complete Crystal Save destruction operation is delayed, preserving destroyed-state
                /// snapshots and pooling behavior. The timer is hosted by Crystal Save rather than by
                /// <paramref name="inst"/>, so disabling the instance or the calling component does not cancel it.
                /// When several delayed requests target the same live instance, the earliest deadline wins.
                /// </remarks>
                public static void Destroy(SaveablePrefab inst, float delay)
                {
                        if (!inst) return;

                        // This also treats NaN as immediate instead of leaving an unfinishable request.
                        if (!(delay > 0f))
                        {
                                Destroy(inst);
                                return;
                        }

                        ScheduleDelayedDestroy(inst, delay);
                }

                internal static void CancelPendingDestroy(SaveablePrefab inst)
                {
                        if (ReferenceEquals(inst, null))
                                return;

                        int instanceId = UnityObjectHelper.GetUniqueId(inst);
                        if (instanceId == 0 ||
                            !pendingDestroyRequests.TryGetValue(instanceId, out PendingDestroyRequest request) ||
                            !ReferenceEquals(request.Instance, inst))
                        {
                                return;
                        }

                        CancelRequest(request);
                }

                private static void ScheduleDelayedDestroy(SaveablePrefab inst, float delay)
                {
                        int instanceId = UnityObjectHelper.GetUniqueId(inst);
                        if (instanceId == 0)
                        {
                                Destroy(inst);
                                return;
                        }

                        float dueTime = Time.time + delay;
                        if (pendingDestroyRequests.TryGetValue(instanceId, out PendingDestroyRequest existing))
                        {
                                if (ReferenceEquals(existing.Instance, inst) && existing.DueTime <= dueTime)
                                        return;

                                CancelRequest(existing);
                        }

                        UnityMainThreadDispatcher runner = UnityMainThreadDispatcher.Instance();
                        if (!runner || !runner.isActiveAndEnabled)
                        {
                                Debug.LogWarning(
                                        "SaveablePrefabFactory.Destroy could not start its delayed operation because " +
                                        "the Crystal Save main-thread dispatcher is inactive. Destroying immediately instead.",
                                        inst);
                                Destroy(inst);
                                return;
                        }

                        var request = new PendingDestroyRequest(instanceId, dueTime, inst, runner);
                        pendingDestroyRequests[instanceId] = request;

                        try
                        {
                                request.Coroutine = runner.StartCoroutine(DestroyAfterDelay(request, delay));
                        }
                        catch (Exception exception)
                        {
                                CancelRequest(request);
                                Debug.LogException(exception, inst);
                                Destroy(inst);
                                return;
                        }

                        if (request.Coroutine == null)
                        {
                                CancelRequest(request);
                                Debug.LogWarning(
                                        "SaveablePrefabFactory.Destroy could not start its delayed operation. " +
                                        "Destroying immediately instead.",
                                        inst);
                                Destroy(inst);
                        }
                }

                private static IEnumerator DestroyAfterDelay(PendingDestroyRequest request, float delay)
                {
                        yield return new WaitForSeconds(delay);

                        if (!pendingDestroyRequests.TryGetValue(request.InstanceId, out PendingDestroyRequest current) ||
                            !ReferenceEquals(current, request))
                        {
                                request.ReleaseReferences();
                                yield break;
                        }

                        pendingDestroyRequests.Remove(request.InstanceId);
                        SaveablePrefab inst = request.Instance;
                        request.ReleaseReferences();

                        if (inst)
                                DestroyImmediate(inst);
                }

                private static void CancelRequest(PendingDestroyRequest request)
                {
                        if (request == null)
                                return;

                        if (pendingDestroyRequests.TryGetValue(request.InstanceId, out PendingDestroyRequest current) &&
                            ReferenceEquals(current, request))
                        {
                                pendingDestroyRequests.Remove(request.InstanceId);
                        }

                        if (request.Runner && request.Coroutine != null)
                                request.Runner.StopCoroutine(request.Coroutine);

                        request.ReleaseReferences();
                }

                private static void DestroyImmediate(SaveablePrefab inst)
                {
                        if (!inst) return;

                        var settings = SaveManager.Instance?.SaveSettings;
                        bool usePooling = settings?.usePrefabPooling ?? false;
                        PrefabRegistry prefabRegistry = null;

                        if (usePooling)
                        {
                                // Check if pooling is disabled directly on the SaveablePrefab component first
                                if (inst.DisablePooling)
                                {
                                        usePooling = false;
                                }
                                // If not disabled on component, check the PrefabRegistry setting
                                else
                                {
                                        prefabRegistry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
                                        if (prefabRegistry != null &&
                                            prefabRegistry.IsPoolingDisabled(inst.PrefabAssetID))
                                        {
                                                usePooling = false;
                                        }
                                }
                        }

                        if (usePooling)
                        {
                                int poolSize = prefabRegistry?.ResolvePoolSize(
                                                       inst.PrefabAssetID,
                                                       settings?.defaultPrefabPoolSize ?? 0)
                                                ?? (settings?.defaultPrefabPoolSize ?? 0);

                                SaveablePrefabPoolCache.TryDespawn(
                                        inst,
                                        poolSize,
                                        inst.RegisterWithSaveSystem);
                        }
                        else
                        {
                                if (SaveManager.Instance != null)
                                        SaveManager.Instance.DestroyWithSnapshot(inst.gameObject);
                                else
                                        UnityEngine.Object.Destroy(inst.gameObject);
                        }
                }

                /// <summary>
                /// Instantiates a SaveablePrefab and forces generation of a new
                /// instance ID even if the prefab asset already has one.
                /// </summary>
                public static SaveablePrefab InstantiateWithFreshID(
                        GameObject prefabAsset,
                        Vector3 position,
                        Quaternion rotation,
                        string sceneName,
                        Transform parent = null,
                        bool registerWithSaveSystem = true,
                        bool? overrideUsePooling = null)
                {
                        if (!string.IsNullOrEmpty(sceneName))
                        {
                                var scene = SceneManager.GetSceneByName(sceneName);
                                if (scene.IsValid() && scene.isLoaded)
                                {
                                        using (SaveablePrefabPoolCache.SuppressDisposal())
                                                SceneManager.SetActiveScene(scene);
                                }
                        }
                        return InstantiateWithFreshID(prefabAsset, position, rotation, parent, registerWithSaveSystem, overrideUsePooling);
                }

                public static SaveablePrefab InstantiateWithFreshID(
                        GameObject prefabAsset,
                        Vector3 position,
                        Quaternion rotation,
                        Transform parent = null,
                        bool registerWithSaveSystem = true,
                        bool? overrideUsePooling = null)
                {
                        var sp = Instantiate(
                                prefabAsset,
                                position,
                                rotation,
                                parent,
                                registerWithSaveSystem,
                                overrideUsePooling);

                        if (sp == null) return null;

                        sp.UnregisterFromSaving();
                        sp.ClearRegisteredFlag();
                        sp.SetUniqueID(Guid.NewGuid().ToString());

                        return sp;
                }
        }
}
#endif
