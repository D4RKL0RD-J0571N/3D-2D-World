#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    public partial class SaveManager
    {
        // Missing data may be retried; an application failure is a completed, failed outcome.
        enum TargetRestoreOutcome { Missing, Failed, Succeeded }

        /// <summary>Restores a live target and awaits active delayed helpers. False reports missing data or failed application.</summary>
        public Task<bool> RestoreSingleGameObjectAsync(GameObject target, SaveData data = null, bool suppressEvent = false)
            => QueueTargetedRestoreAsync(target, null, data, null, 1, 0, suppressEvent);

        async void ObserveTargetedRestore(Task<bool> task)
        {
            try
            {
                if (!await task) Logger.Log("Targeted restore did not complete successfully.", LogLevel.Warning);
            }
            catch (Exception ex) { Logger.Log("Targeted restore failed: " + ex.Message, LogLevel.Error); }
        }

        Task<bool> QueueTargetedRestoreAsync(GameObject target, string identifier, SaveData suppliedData,
            int? slotNumber, int maxRetries, int retryDelayMs, bool suppressEvent)
            => QueueObjectRestoreAsync((context, data) => ApplyTargetedRestoreAsync(context, target, identifier, data, suppressEvent),
                suppliedData, slotNumber, maxRetries, retryDelayMs);

        async Task<bool> QueueObjectRestoreAsync(Func<SaveOperationContext, SaveData, Task<TargetRestoreOutcome>> apply,
            SaveData suppliedData, int? slotNumber, int maxRetries, int retryDelayMs)
        {
            if (maxRetries <= 0 || retryDelayMs < 0) return false;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                TargetRestoreOutcome result;
                try
                {
                    result = await Operations.RunAsync(async context =>
                    {
                        await AwaitOperationReady(context);
                        var data = slotNumber.HasValue ? await LoadSaveDataForSlotCoreAsync(context, slotNumber.Value) : suppliedData ?? CurrentSaveData;
                        if (data == null) return TargetRestoreOutcome.Missing;
                        ValidateCandidate(data);
                        return await apply(context, data);
                    });
                }
                catch (Exception ex)
                {
                    Logger.Log("Targeted restore failed: " + ex.Message, LogLevel.Warning);
                    return false;
                }
                if (result != TargetRestoreOutcome.Missing) return result == TargetRestoreOutcome.Succeeded;
                if (attempt + 1 < maxRetries)
                {
                    // Yield outside the queue so another load can provide the missing data.
                    // Frame-driven waits work on WebGL, where Task.Delay does not run.
                    DateTime deadline = DateTime.UtcNow.AddMilliseconds(retryDelayMs);
                    do { await Task.Yield(); } while (DateTime.UtcNow < deadline);
                }
            }
            return false;
        }

        async Task<TargetRestoreOutcome> ApplyTargetedRestoreAsync(SaveOperationContext context, GameObject target,
            string identifier, SaveData data, bool suppressEvent)
        {
            if (!target && string.IsNullOrEmpty(identifier)) return TargetRestoreOutcome.Missing;
            if (!target && !string.IsNullOrEmpty(identifier))
            {
                target = FindGameObjectByUniqueID(identifier, IdentifierType.UniqueID);
                if (!target)
                    target = prefabManager?.GetSaveablePrefabs().FirstOrDefault(p => p && p.PrefabAssetID == identifier)?.gameObject;
            }
            bool instantiated = false;
            if (!target)
            {
                var prefab = data.Prefabs?.FirstOrDefault(p => p.InstanceID == identifier || p.PrefabID == identifier);
                if (prefab == null) return TargetRestoreOutcome.Missing;
                context.BeginCommit();
                await Operations.RunCoroutine(prefabManager.InstantiatePrefabsCoroutine(
                    new List<SaveablePrefabData> { prefab }, data.DestroyedGameObjects.Where(id => id == prefab.InstanceID).ToList(),
                    clearExistingPrefabs: false, handleDeferral: false), context);
                target = FindGameObjectByUniqueID(prefab.InstanceID, IdentifierType.UniqueID);
                if (!target) return TargetRestoreOutcome.Failed;
                instantiated = true;
            }
            string id = GetUniqueID(target);
            var saveable = target.GetComponent<SaveablePrefab>();
            bool hasData = data.GameObjectStates.Any(state => !string.IsNullOrEmpty(id) && state.UniqueID == id) ||
                saveable && FindTargetPrefabData(saveable, data) != null || SelectTargetComponentData(target, data).Count != 0;
            if (!hasData) return TargetRestoreOutcome.Missing;
            context.BeginCommit();
            // Instantiation already applied prefab blobs; repeating them supersedes pending tickets.
            ApplyTargetObject(target, data, applyPrefab: !instantiated);
            if (!await FinishComponentRestorationAsync(context)) return TargetRestoreOutcome.Failed;
            if (!target) return TargetRestoreOutcome.Failed;
            if (!suppressEvent) NotifyOperation(() => SaveEventDispatch.Raise(OnSingleGameObjectRestored, target));
            return TargetRestoreOutcome.Succeeded;
        }

        private static SaveablePrefabData FindTargetPrefabData(SaveablePrefab owner, SaveData data)
        {
            if (!owner) return null;
            return data.Prefabs.FirstOrDefault(p => p.InstanceID == owner.UniqueID) ??
                data.Prefabs.FirstOrDefault(p => !string.IsNullOrEmpty(owner.PrefabAssetID) && p.PrefabID == owner.PrefabAssetID);
        }

        private Dictionary<string, byte[]> SelectTargetComponentData(GameObject target, SaveData data)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var owner = target.GetComponentInParent<SaveablePrefab>(true);
            var record = FindTargetPrefabData(owner, data);
            bool isPrefabRoot = owner && owner.gameObject == target;
            Dictionary<string, byte[]> blobs = null;
            if (owner && !isPrefabRoot && record?.RuntimeModificationData != null)
            {
                var mods = serializer.Deserialize<RuntimeModificationData>(record.RuntimeModificationData);
                if (mods?.ComponentBlobs != null)
                {
                    blobs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                    foreach (var blob in mods.ComponentBlobs) blobs[blob.GuidSuffix] = blob.Payload;
                }
            }
            foreach (var helper in target.GetComponentsInChildren<ISaveable>(true))
            {
                if (helper is Component component && !component) continue;
                if (helper is SaveableComponent saveable && saveable.RuntimeSaveRegistrationSuppressed) continue;
                if (SaveablePrefab.FindNearestComponentBlobOwner(helper) != owner) continue;
                // Root prefab application already owns its blobs. Child-only requests apply
                // only helpers below that child, without changing the parent's other state.
                if (isPrefabRoot && owner.EffectiveTrackComponentBlobs) continue;
                string id = helper.UniqueIdentifier;
                if (string.IsNullOrEmpty(id)) continue;
                if (data.ComponentsData.TryGetValue(id, out var bytes)) result[id] = bytes;
                else if (owner && record != null && id.StartsWith(owner.UniqueID + "_", StringComparison.Ordinal) &&
                    data.ComponentsData.TryGetValue(record.InstanceID + id.Substring(owner.UniqueID.Length), out bytes))
                    result[id] = bytes;
                else if (blobs != null && blobs.TryGetValue(id.Substring(id.IndexOf('_') + 1), out bytes))
                    result[id] = bytes;
            }
            return result;
        }

        private async Task<bool> FinishComponentRestorationAsync(SaveOperationContext context, int firstError = 0)
        {
            while (context.PendingRestores.Any(pending => pending.Waiting)) await Task.Yield();
            if (context.RestoreErrors.Count == firstError) return true;
            Logger.Log(ComponentOperationScope.DescribeErrors(context.RestoreErrors.Skip(firstError)), LogLevel.Warning);
            return false;
        }

        private void ApplyTargetObject(GameObject target, SaveData data, bool applyPrefab, bool forceDuplicateLoads = true)
        {
			if (target == null) { Logger.Log("RestoreSingleGameObject: target is null.", LogLevel.Warning); return; }

			data ??= CurrentSaveData;
			if (data == null) { Logger.Log("RestoreSingleGameObject: no SaveData loaded.", LogLevel.Warning); return; }

                        string goUID = GetUniqueID(target);

			// 1) Active-state
			var goState = data.GameObjectStates.FirstOrDefault(s => s.UniqueID == goUID);
			if (goState != null && goState.IsActive.HasValue)
				target.SetActive(goState.IsActive.Value);

			// 2) Transform / Rigidbody / Animator
			var sp = target.GetComponent<SaveablePrefab>();
			if (sp != null && applyPrefab)
			{
				var spData = FindTargetPrefabData(sp, data);
				if (spData != null)
				{
					ApplySavedObjectTransform(target, spData);

					// Restore tracking flags BEFORE applying runtime modifications (1.6.0)
					// This is critical for procedural/runtime-modified prefabs
					sp.TrackAddedComponents = spData.TrackAddedComponents;
					sp.TrackComponentBlobs = spData.TrackComponentBlobs;
					sp.TrackMaterialOverrides = spData.TrackMaterialOverrides;
					sp.TrackChildStateOverrides = spData.TrackChildStateOverrides;
					sp.TrackChildTransformOverrides = spData.TrackChildTransformOverrides;
					sp.TrackSkinnedMeshOverrides = spData.TrackSkinnedMeshOverrides;
					sp.TrackBlendshapeOverrides = spData.TrackBlendshapeOverrides;
					sp.TrackTextureOverrides = spData.TrackTextureOverrides;
					sp.TrackParticleSnapshots = spData.TrackParticleSnapshots;
					sp.TrackColliderSettings = spData.TrackColliderSettings;

					// Apply any stored runtime modifications such as
					// name, tag or layer changes.
					if (spData.RuntimeModificationData != null &&
						spData.RuntimeModificationData.Length > 0)
					{
						sp.ApplyRuntimeModifications(spData.RuntimeModificationData);
					}

					var rb = target.GetComponent<Rigidbody>();
					if (spData.HasRigidbody && rb != null)
					{
						rb.isKinematic = spData.RigidbodyIsKinematic;
						rb.useGravity = spData.RigidbodyUseGravity;
#if UNITY_6000_0_OR_NEWER
						rb.linearDamping = spData.RigidbodyDrag;
						rb.angularDamping = spData.RigidbodyAngularDrag;
						rb.linearVelocity = spData.RigidbodyVelocity;
#else
						rb.drag            = spData.RigidbodyDrag;
						rb.angularDrag     = spData.RigidbodyAngularDrag;
						rb.velocity        = spData.RigidbodyVelocity;
#endif
						rb.angularVelocity = spData.RigidbodyAngularVelocity;
					}

					var anim = target.GetComponent<Animator>();
					if (spData.HasAnimator && anim != null)
					{
						anim.Play(spData.AnimatorStateHash, 0, spData.AnimatorNormalizedTime);
						anim.Update(0f);
					}
				}
			}

			// 3) Component data
                        var subset = SelectTargetComponentData(target, data);
                        // When restoring a single object multiple times within the same
                        // play session we still want each component to deserialize again
                        // (e.g. the TargetedRestore demo repeatedly restoring the cubes).
                        //
                        // Without forcing the reload the ComponentManager remembers that
                        // these components have already deserialized once and skips
                        // subsequent LoadData calls, so physics-driven changes such as a
                        // cube falling due to gravity are never reverted on the second
                        // restore. Allow duplicate loads so targeted restores always
                        // reapply the serialized state.
                        ComponentManager.ApplyComponentDataToObject(target, subset, forceDuplicateLoads);

        }
    }
}
#endif
