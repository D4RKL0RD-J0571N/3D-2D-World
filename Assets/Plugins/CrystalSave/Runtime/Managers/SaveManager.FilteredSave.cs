#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Partial save-filtering API that lets a game partition the world state
    /// into multiple slots — for example, saving the map to slot 1 while
    /// persisting the Player to a dedicated slot 100.
    ///
    /// All three variants write to a single slot; loading is unchanged —
    /// load the world with <see cref="Load(int)"/> as usual and then overlay
    /// the player with <c>RestoreSingleGameObject(player, slot: 100)</c>.
    /// </summary>
    public partial class SaveManager
    {
        #region Filtered Save API

        /// <summary>
        /// Saves only components whose owning GameObject carries the given
        /// Unity <paramref name="tag"/> to <paramref name="slotNumber"/>.
        /// Everything else is excluded from the file.
        /// </summary>
        /// <param name="slotNumber">Destination slot.</param>
        /// <param name="tag">Unity tag to include (e.g. <c>"Player"</c>).</param>
        /// <param name="includeChildren">Include descendants of tagged roots. Defaults to true.</param>
        /// <param name="lastActiveScene">Optional override for the active scene name.</param>
        /// <param name="slotName">Optional custom slot display name.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<bool> SaveSlotForTagAsync(
            int slotNumber,
            string tag,
            bool includeChildren = true,
            string lastActiveScene = null,
            string slotName = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tag))
            {
                Logger.Log("SaveSlotForTagAsync: tag is null or empty.", LogLevel.Error);
                return false;
            }

            return await EnsureInitializedForFilteredSaveAsync(context =>
            {
                var allowed = CollectUidsForTag(tag, includeChildren);
                return SaveOperations.SaveFilteredImmediateAsync(
                    context,
                    slotNumber,
                    uid => !string.IsNullOrEmpty(uid) && allowed.Contains(uid),
                    includeGlobalSceneState: false,
                    lastActiveScene,
                    slotName,
                    ct);
            }, ct);
        }

        /// <summary>
        /// Saves the full world <em>except</em> for GameObjects that carry
        /// the specified <paramref name="tag"/>. Use this for the main
        /// "world/map" slot when the Player is persisted separately.
        /// </summary>
        public async Task<bool> SaveSlotExcludingTagAsync(
            int slotNumber,
            string tag,
            bool includeChildren = true,
            string lastActiveScene = null,
            string slotName = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tag))
            {
                Logger.Log("SaveSlotExcludingTagAsync: tag is null or empty.", LogLevel.Error);
                return false;
            }

            return await EnsureInitializedForFilteredSaveAsync(context =>
            {
                var excluded = CollectUidsForTag(tag, includeChildren);
                return SaveOperations.SaveFilteredImmediateAsync(
                    context,
                    slotNumber,
                    uid => string.IsNullOrEmpty(uid) || !excluded.Contains(uid),
                    includeGlobalSceneState: true,
                    lastActiveScene,
                    slotName,
                    ct);
            }, ct);
        }

        /// <summary>
        /// Saves only the components owned by <paramref name="root"/>
        /// (and optionally its children) to <paramref name="slotNumber"/>.
        /// </summary>
        public async Task<bool> SaveSlotForGameObjectAsync(
            GameObject root,
            int slotNumber,
            bool includeChildren = true,
            string lastActiveScene = null,
            string slotName = null,
            CancellationToken ct = default)
        {
            if (root == null)
            {
                Logger.Log("SaveSlotForGameObjectAsync: root is null.", LogLevel.Error);
                return false;
            }

            return await EnsureInitializedForFilteredSaveAsync(context =>
            {
                var allowed = CollectUidsForRoot(root, includeChildren);
                return SaveOperations.SaveFilteredImmediateAsync(
                    context,
                    slotNumber,
                    uid => !string.IsNullOrEmpty(uid) && allowed.Contains(uid),
                    includeGlobalSceneState: false,
                    lastActiveScene,
                    slotName,
                    ct);
            }, ct);
        }

        /// <summary>
        /// Saves the full world <em>except</em> for <paramref name="excluded"/>
        /// (and optionally its children).
        /// </summary>
        public async Task<bool> SaveSlotExcludingGameObjectAsync(
            GameObject excluded,
            int slotNumber,
            bool includeChildren = true,
            string lastActiveScene = null,
            string slotName = null,
            CancellationToken ct = default)
        {
            if (excluded == null)
            {
                Logger.Log("SaveSlotExcludingGameObjectAsync: excluded is null.", LogLevel.Error);
                return false;
            }

            return await EnsureInitializedForFilteredSaveAsync(context =>
            {
                var skip = CollectUidsForRoot(excluded, includeChildren);
                return SaveOperations.SaveFilteredImmediateAsync(
                    context,
                    slotNumber,
                    uid => string.IsNullOrEmpty(uid) || !skip.Contains(uid),
                    includeGlobalSceneState: true,
                    lastActiveScene,
                    slotName,
                    ct);
            }, ct);
        }

        /// <summary>
        /// Saves only the components whose owning GameObject passes the
        /// supplied <paramref name="includeGameObject"/> predicate. The
        /// predicate is invoked once per currently-tracked GameObject;
        /// anything unreachable at save time (e.g. already destroyed) is
        /// excluded from the slot.
        /// </summary>
        /// <param name="slotNumber">Destination slot.</param>
        /// <param name="includeGameObject">Per-GameObject filter predicate.</param>
        /// <param name="includeGlobalSceneState">
        /// When true, global lists such as <c>DestroyedGameObjects</c>
        /// are carried over. Default is false (targeted/subset slot).
        /// </param>
        /// <param name="lastActiveScene">Optional override for the active scene name.</param>
        /// <param name="slotName">Optional custom slot display name.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<bool> SaveSlotWithFilterAsync(
            int slotNumber,
            Func<GameObject, bool> includeGameObject,
            bool includeGlobalSceneState = false,
            string lastActiveScene = null,
            string slotName = null,
            CancellationToken ct = default)
        {
            if (includeGameObject == null)
            {
                Logger.Log("SaveSlotWithFilterAsync: includeGameObject is null.", LogLevel.Error);
                return false;
            }

            return await EnsureInitializedForFilteredSaveAsync(context =>
            {
                var allowed = CollectUidsMatching(includeGameObject);
                return SaveOperations.SaveFilteredImmediateAsync(
                    context,
                    slotNumber,
                    uid => !string.IsNullOrEmpty(uid) && allowed.Contains(uid),
                    includeGlobalSceneState,
                    lastActiveScene,
                    slotName,
                    ct);
            }, ct);
        }

        #endregion

        #region Filtered Save Helpers

        private async Task<bool> EnsureInitializedForFilteredSaveAsync(Func<SaveOperationContext, Task<bool>> run, CancellationToken token)
        {
            try
            {
                return await Operations.RunAsync(async context =>
                {
                    await AwaitOperationReady(context);
                    var previous = CurrentSaveData;
                    try { bool result = await run(context); if (!result && !context.PayloadCommitted) CurrentSaveData = previous; return result; }
                    catch { if (!context.PayloadCommitted) CurrentSaveData = previous; throw; }
                }, token);
            }
            catch (OperationCanceledException) { return false; }
        }

        /// <summary>
        /// Returns the set of UniqueIDs whose GameObject carries the given tag.
        /// </summary>
        private HashSet<string> CollectUidsForTag(string tag, bool includeChildren)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            GameObject[] roots;
            try
            {
                roots = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                Logger.Log($"Filtered save: tag '{tag}' is not defined in the Tag Manager.", LogLevel.Error);
                return set;
            }

            if (roots == null) return set;
            foreach (var go in roots)
            {
                if (go == null) continue;
                AddRootUids(go, includeChildren, set);
            }
            return set;
        }

        /// <summary>
        /// Returns the set of UniqueIDs for <paramref name="root"/> (and
        /// children if <paramref name="includeChildren"/> is true).
        /// </summary>
        private HashSet<string> CollectUidsForRoot(GameObject root, bool includeChildren)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            AddRootUids(root, includeChildren, set);
            return set;
        }

        /// <summary>
        /// Walks currently-tracked GameObjects and returns UIDs for those
        /// matching <paramref name="filter"/>.
        /// </summary>
        private HashSet<string> CollectUidsMatching(Func<GameObject, bool> filter)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (gameObjectTracker == null) return set;
            // User filters can change registrations. Enumerate a stable membership
            // snapshot without exposing or locking the tracker's live containers.
            foreach (var kv in gameObjectTracker.GetTrackedGameObjects())
            {
                var go = kv.Value?.GameObject;
                if (go == null) continue;
                try
                {
                    if (filter(go) && !string.IsNullOrEmpty(kv.Key))
                        set.Add(kv.Key);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Filtered save: filter threw for '{kv.Key}': {ex.Message}", LogLevel.Warning);
                }
            }
            return set;
        }

        private void AddRootUids(GameObject root, bool includeChildren, HashSet<string> set)
        {
            if (root == null) return;

            string rootUid = GameObjectUtilities.GetUniqueID(root);
            if (!string.IsNullOrEmpty(rootUid))
                set.Add(rootUid);

            if (!includeChildren) return;

            // Include inactive children as well — saveables on disabled objects
            // still need to be partitioned correctly.
            var childTransforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            if (childTransforms == null) return;
            foreach (var t in childTransforms)
            {
                if (t == null || t.gameObject == root) continue;
                string uid = GameObjectUtilities.GetUniqueID(t.gameObject);
                if (!string.IsNullOrEmpty(uid))
                    set.Add(uid);
            }
        }

        #endregion
    }
}
#endif
