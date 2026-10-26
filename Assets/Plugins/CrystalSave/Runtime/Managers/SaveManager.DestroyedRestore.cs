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
        private Task<bool> QueueDestroyedRestoreAsync(string identifier, SaveData suppliedData, int? slotNumber,
            int maxRetries, int retryDelayMs, bool prefabsOnly = false, bool byAsset = false, bool all = false)
            => QueueObjectRestoreAsync(async (context, data) =>
            {
                if (!all)
                {
                    if (string.IsNullOrEmpty(identifier)) return TargetRestoreOutcome.Missing;
                    string instanceID = identifier;
                    if (byAsset)
                    {
                        var record = data.Prefabs.FirstOrDefault(p => p.PrefabID == identifier && IsDestroyedRestoreCandidate(p.InstanceID, data, true));
                        if (record == null) return TargetRestoreOutcome.Missing;
                        instanceID = record.InstanceID;
                    }
                    return await RestoreDestroyedObjectCoreAsync(context, instanceID, data, prefabsOnly);
                }

                // Freeze the request set: callbacks and successful cleanup can change these collections.
                var ids = gameObjectTracker.GetDestroyedGameObjectIDs()
                    .Concat(data.DestroyedGameObjects).Concat(data.DestroyedObjectData.Keys)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(id => ResolveDestroyedIdentifierInfo(id).CanonicalId)
                    .Distinct(StringComparer.Ordinal).ToArray();
                bool succeeded = true;
                foreach (string id in ids)
                {
                    if (prefabsOnly && string.IsNullOrEmpty(FindPrefabDataInList(data.Prefabs, ResolveDestroyedIdentifierInfo(id).Variants)?.PrefabID))
                        continue;
                    try
                    {
                        var result = await RestoreDestroyedObjectCoreAsync(context, id, data, prefabsOnly);
                        succeeded &= result == TargetRestoreOutcome.Succeeded;
                    }
                    catch (Exception ex)
                    {
                        succeeded = false;
                        Logger.Log("Destroyed object restore failed for '" + id + "': " + ex.Message, LogLevel.Warning);
                    }
                }
                if (succeeded && !prefabsOnly) NotifyOperation(() => SaveEventDispatch.Raise(OnAllGameObjectsRestored));
                return succeeded ? TargetRestoreOutcome.Succeeded : TargetRestoreOutcome.Failed;
            }, suppliedData, slotNumber, maxRetries, retryDelayMs);

        private bool IsDestroyedRestoreCandidate(string id, SaveData data, bool allowMissingPrefab)
        {
            var variants = ResolveDestroyedIdentifierInfo(id).Variants;
            return variants.Any(key => data.DestroyedObjectData.ContainsKey(key)) ||
                data.DestroyedGameObjects.Any(key => VariantIdsContainMatch(variants, key)) ||
                gameObjectTracker.GetDestroyedGameObjectIDs().Any(key => VariantIdsContainMatch(variants, key)) ||
                allowMissingPrefab && !variants.Any(key => FindGameObjectByUniqueID(key, IdentifierType.UniqueID));
        }

        private async Task<TargetRestoreOutcome> RestoreDestroyedObjectCoreAsync(SaveOperationContext context,
            string identifier, SaveData data, bool prefabOnly)
        {
            var identity = ResolveDestroyedIdentifierInfo(identifier);
            var variants = identity.Variants;
            var prefabData = FindPrefabDataInList(data.Prefabs, variants);
            if (prefabOnly && (prefabData == null || string.IsNullOrEmpty(prefabData.PrefabID)))
                return TargetRestoreOutcome.Missing;
            if (!IsDestroyedRestoreCandidate(identifier, data, prefabOnly)) return TargetRestoreOutcome.Missing;

            var target = variants.Select(id => FindGameObjectByUniqueID(id, IdentifierType.UniqueID)).FirstOrDefault(go => go);
            GameObject sceneTemplate = null;
            bool instantiatePrefab = !target && prefabData != null && !string.IsNullOrEmpty(prefabData.PrefabID);
            if (!target && !instantiatePrefab)
            {
                var mappings = sceneObjectRegistry?.GetPrefabMappings();
                if (mappings != null)
                    foreach (string id in variants)
                        if (mappings.TryGetValue(id, out sceneTemplate) && sceneTemplate) break;
                if (!sceneTemplate) return TargetRestoreOutcome.Missing;
            }

            // A small application view gives the destroyed snapshot precedence without
            // mutating either the supplied data or CurrentSaveData before success.
            var application = new SaveData(data.Version)
            {
                ComponentsData = new Dictionary<string, byte[]>(data.ComponentsData),
                Prefabs = prefabData == null ? new List<SaveablePrefabData>() : new List<SaveablePrefabData> { prefabData }
            };
            if (TryGetComponentDataFrom(data, variants, out _, out var captured))
                foreach (var pair in captured) application.ComponentsData[pair.Key] = pair.Value;

            context.BeginCommit();
            int firstError = context.RestoreErrors.Count;
            var marked = gameObjectTracker.GetDestroyedGameObjectIDs().Where(id => VariantIdsContainMatch(variants, id)).ToArray();
            // Registration must be permitted while reconstructing the object. Keep all
            // snapshot records until completion and reinstate these markers on failure.
            gameObjectTracker.RemoveDestroyedWhere(id => VariantIdsContainMatch(variants, id));
            bool succeeded = false;
            try
            {
                if (instantiatePrefab)
                {
                    await Operations.RunCoroutine(prefabManager.InstantiatePrefabsCoroutine(
                        new List<SaveablePrefabData> { prefabData }, new List<string>(),
                        clearExistingPrefabs: false, handleDeferral: false), context);
                    target = FindGameObjectByUniqueID(prefabData.InstanceID, IdentifierType.UniqueID);
                    if (!target) return TargetRestoreOutcome.Failed;
                }
                else if (!target)
                {
                    var assetOwner = sceneTemplate.GetComponent<SaveablePrefab>();
                    bool loading = assetOwner && assetOwner.IsLoading;
                    try
                    {
                        assetOwner?.SetLoading(true);
                        target = Instantiate(sceneTemplate);
                    }
                    finally { assetOwner?.SetLoading(loading); }
                    string id = prefabData?.InstanceID ?? identity.CanonicalId;
                    var owner = target.GetComponent<SaveablePrefab>();
                    if (owner) owner.SetUniqueID(id);
                    else
                    {
                        var uid = target.GetComponent<UniqueID>() ?? target.AddComponent<UniqueID>();
                        uid.ID = id;
                        foreach (var helper in target.GetComponents<SaveableComponent>()) helper.OverrideGameObjectID(id);
                    }
                    target.name = sceneTemplate.name + "_Restored";
                }

                target.SetActive(true);
                // Synthetic scene-object records store the last known parent/transform too.
                if (prefabData != null && !target.GetComponent<SaveablePrefab>()) ApplySavedObjectTransform(target, prefabData);
                ApplyTargetObject(target, application, applyPrefab: !instantiatePrefab, forceDuplicateLoads: false);
                if (!await FinishComponentRestorationAsync(context, firstError) || !target) return TargetRestoreOutcome.Failed;

                var restoredPrefab = target.GetComponent<SaveablePrefab>();
                if (restoredPrefab && restoredPrefab.RegisterWithSaveSystem) restoredPrefab.RegisterForSaving();
                var remember = target.GetComponent<RememberGameObject>();
                if (remember) RegisterGameObject(target, remember.PropertySettings);
                CacheGameObject(GetUniqueID(target), target);

                CompleteDestroyedRestore(data, variants, removePrefabRecord: !prefabOnly);
                if (data != CurrentSaveData) CompleteDestroyedRestore(CurrentSaveData, variants, removePrefabRecord: !prefabOnly);
                RemoveDestroyedAliasMappings(variants);
                succeeded = true;
                if (prefabOnly) NotifyOperation(() => SaveEventDispatch.Raise(OnSingleGameObjectRestored, target));
                else NotifyOperation(() => SaveEventDispatch.Raise(OnGameObjectRestored, identifier));
                return TargetRestoreOutcome.Succeeded;
            }
            finally
            {
                if (!succeeded)
                    gameObjectTracker.MergeDestroyedIDs(marked);
            }
        }

        private void CompleteDestroyedRestore(SaveData data, IReadOnlyList<string> variants, bool removePrefabRecord)
        {
            if (data == null) return;
            RemoveDestroyedListEntries(data, variants);
            RemoveDestroyedObjectDataEntries(data, variants);
            if (removePrefabRecord) RemovePrefabEntries(data, variants);
        }

        private void ApplySavedObjectTransform(GameObject target, SaveablePrefabData data)
        {
            var controller = target.GetComponent<CharacterController>();
            bool wasEnabled = controller && controller.enabled;
            try
            {
                if (wasEnabled) controller.enabled = false;
                var parent = string.IsNullOrEmpty(data.ParentID) ? null : FindGameObjectByUniqueID(data.ParentID, IdentifierType.Auto);
                target.transform.SetParent(parent ? parent.transform : null, false);
                if (parent)
                {
                    target.transform.localPosition = data.Position;
                    target.transform.localRotation = data.Rotation;
                }
                else
                {
                    target.transform.position = data.Position;
                    target.transform.rotation = data.Rotation;
                }
                target.transform.localScale = data.Scale;
            }
            finally { if (controller) controller.enabled = wasEnabled; }
        }
    }
}
#endif
