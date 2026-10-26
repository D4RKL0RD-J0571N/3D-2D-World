#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Filtered-save pipeline. Collects the full SaveData in memory (so the
    /// in-session CurrentSaveData stays complete and accurate) and writes a
    /// sliced copy to the target slot. Used by the public
    /// <c>SaveSlotForTagAsync</c>, <c>SaveSlotForGameObjectAsync</c>,
    /// <c>SaveSlotWithFilterAsync</c> API on <see cref="SaveManager"/>.
    /// </summary>
    public partial class SaveOperationService
    {
        /// <summary>
        /// Mirrors <see cref="SaveImmediateAsync"/> but writes only the subset
        /// of the collected <see cref="SaveData"/> whose owning GameObject
        /// UniqueID passes the <paramref name="includeByGoUid"/> predicate.
        /// </summary>
        /// <param name="slotNumber">Target save slot.</param>
        /// <param name="includeByGoUid">Returns true for GameObject UIDs that should be persisted.</param>
        /// <param name="includeGlobalSceneState">
        /// When true, global lists that are not keyed per-GameObject
        /// (DestroyedGameObjects, HomeScene snapshots of other objects) are
        /// carried over; when false they are cleared/filtered out.
        /// </param>
        /// <param name="lastActiveScene">Optional override for the active scene name.</param>
        /// <param name="customSlotName">Optional custom slot display name.</param>
        /// <param name="ct">Cancellation token.</param>
        internal async Task<bool> SaveFilteredImmediateAsync(
            SaveOperationContext context,
            int slotNumber,
            Func<string, bool> includeByGoUid,
            bool includeGlobalSceneState,
            string lastActiveScene,
            string customSlotName,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (includeByGoUid == null) throw new ArgumentNullException(nameof(includeByGoUid));
            var slot = SaveManager.CopySlot(slotManager.GetByNumber(slotNumber));
            context.RequestedSlotName = string.IsNullOrEmpty(customSlotName) ? null : customSlotName;
            if (!string.IsNullOrEmpty(customSlotName)) slot.SlotName = customSlotName;
            var previous = manager.CurrentSaveData;
            try
            {
                await SaveCandidateCoreAsync(context, slot, lastActiveScene, false, includeByGoUid, includeGlobalSceneState);
                return true;
            }
            catch (Exception ex)
            {
                if (context?.PayloadCommitted != true) manager.CurrentSaveData = previous;
                if (ex is OperationCanceledException) throw;
                manager.CompleteSave(context, slot, false, ex.Message);
                return false;
            }
        }

        // -------- Filtering helpers --------

        /// <summary>
        /// Builds a sliced copy of <paramref name="full"/> that contains only
        /// entries whose owning GameObject UID satisfies <paramref name="include"/>.
        /// </summary>
        private static SaveData BuildFilteredCopy(
            SaveData full,
            Func<string, bool> include,
            bool includeGlobalSceneState)
        {
            var sliced = new SaveData
            {
                Version           = full.Version,
                LastSaved         = full.LastSaved,
                ScreenshotFileName = full.ScreenshotFileName,
                LastActiveScene   = full.LastActiveScene,
                ComponentsData    = new Dictionary<string, byte[]>(),
                ComponentMetadata = new Dictionary<string, ComponentDataMetadata>(),
                Prefabs           = new List<SaveablePrefabData>(),
                SaveableComponents = new List<SaveDataEntry>(),
                GameObjectStates  = new List<GameObjectState>(),
                DestroyedGameObjects = new List<string>(),
                TrackedUniqueIDs  = new List<string>(),
                DestroyedObjectData = new Dictionary<string, Dictionary<string, byte[]>>(),
                HomeSceneComponentSnapshots = new Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>>(),
                HomeScenePrefabAssetIDs     = new Dictionary<string, Dictionary<string, string>>(),
            };

            // ComponentsData: keys are "{goUid}_{componentId}".
            if (full.ComponentsData != null)
            {
                foreach (var kv in full.ComponentsData)
                {
                    if (include(ExtractGoUid(kv.Key)))
                        sliced.ComponentsData[kv.Key] = kv.Value;
                }
            }

            // ComponentMetadata: same keying rule.
            if (full.ComponentMetadata != null)
            {
                foreach (var kv in full.ComponentMetadata)
                {
                    if (include(ExtractGoUid(kv.Key)))
                        sliced.ComponentMetadata[kv.Key] = kv.Value;
                }
            }

            if (full.Prefabs != null)
            {
                foreach (var p in full.Prefabs)
                {
                    if (p == null) continue;
                    if (include(p.InstanceID))
                        sliced.Prefabs.Add(p);
                }
            }

            if (full.SaveableComponents != null)
            {
                foreach (var entry in full.SaveableComponents)
                {
                    if (entry == null) continue;
                    // SaveDataEntry keys track the component identifier; derive the owning GO.
                    // We fall back to the whole key if no underscore is present.
                    string owner = ExtractGoUid(GetEntryIdentifier(entry));
                    if (include(owner))
                        sliced.SaveableComponents.Add(entry);
                }
            }

            if (full.GameObjectStates != null)
            {
                foreach (var s in full.GameObjectStates)
                {
                    if (s == null) continue;
                    if (include(s.UniqueID))
                        sliced.GameObjectStates.Add(s);
                }
            }

            if (full.TrackedUniqueIDs != null)
            {
                foreach (var id in full.TrackedUniqueIDs)
                {
                    if (include(id))
                        sliced.TrackedUniqueIDs.Add(id);
                }
            }

            // DestroyedObjectData: outer key is the destroyed GO UID.
            if (full.DestroyedObjectData != null)
            {
                foreach (var kv in full.DestroyedObjectData)
                {
                    if (include(kv.Key))
                        sliced.DestroyedObjectData[kv.Key] = kv.Value;
                }
            }

            // HomeSceneComponentSnapshots: Scene -> GoUid -> CompId -> bytes.
            if (full.HomeSceneComponentSnapshots != null)
            {
                foreach (var sceneKv in full.HomeSceneComponentSnapshots)
                {
                    var perObject = new Dictionary<string, Dictionary<string, byte[]>>();
                    foreach (var objKv in sceneKv.Value)
                    {
                        if (include(objKv.Key))
                            perObject[objKv.Key] = objKv.Value;
                    }
                    if (perObject.Count > 0)
                        sliced.HomeSceneComponentSnapshots[sceneKv.Key] = perObject;
                }
            }

            // HomeScenePrefabAssetIDs: Scene -> GoUid -> PrefabAssetID.
            if (full.HomeScenePrefabAssetIDs != null)
            {
                foreach (var sceneKv in full.HomeScenePrefabAssetIDs)
                {
                    var perObject = new Dictionary<string, string>();
                    foreach (var objKv in sceneKv.Value)
                    {
                        if (include(objKv.Key))
                            perObject[objKv.Key] = objKv.Value;
                    }
                    if (perObject.Count > 0)
                        sliced.HomeScenePrefabAssetIDs[sceneKv.Key] = perObject;
                }
            }

            // DestroyedGameObjects is a global list of deletion UIDs, not tied
            // to any single GameObject's state payload. On the "world" slot we
            // preserve it; on a targeted slot we drop it.
            if (includeGlobalSceneState && full.DestroyedGameObjects != null)
            {
                foreach (var id in full.DestroyedGameObjects)
                {
                    if (!string.IsNullOrEmpty(id))
                        sliced.DestroyedGameObjects.Add(id);
                }
            }

            return sliced;
        }

        private static string ExtractGoUid(string compositeKey)
        {
            if (string.IsNullOrEmpty(compositeKey)) return string.Empty;
            int underscore = compositeKey.IndexOf('_');
            return underscore < 0 ? compositeKey : compositeKey.Substring(0, underscore);
        }

        /// <summary>
        /// Best-effort retrieval of an identifier string from a
        /// <see cref="SaveDataEntry"/> without taking a hard dependency on its
        /// exact field layout (which varies across plugin versions).
        /// </summary>
        private static string GetEntryIdentifier(SaveDataEntry entry)
        {
            if (entry == null) return string.Empty;
            var t = entry.GetType();

            var idProp = t.GetProperty("UniqueIdentifier") ?? t.GetProperty("UniqueID")
                         ?? t.GetProperty("Identifier") ?? t.GetProperty("Id");
            if (idProp != null)
            {
                var v = idProp.GetValue(entry) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }

            var idField = t.GetField("UniqueIdentifier") ?? t.GetField("UniqueID")
                          ?? t.GetField("Identifier") ?? t.GetField("Id");
            if (idField != null)
            {
                var v = idField.GetValue(entry) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }

            return string.Empty;
        }
    }
}
#endif
