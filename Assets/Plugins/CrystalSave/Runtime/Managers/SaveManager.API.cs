#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Arawn.CrystalSave.Runtime
{
    public partial class SaveManager
    {
                #region Save and Load API

                // Capture the UI element that triggered the save so we can
                // disable it while the operation is running and prevent
                // repeated clicks.
                private static Selectable GetInvokingSelectable()
                {
                        var go = EventSystem.current?.currentSelectedGameObject;
                        return go != null ? go.GetComponent<Selectable>() : null;
                }

		// SaveManager.cs  –  add inside the public class, e.g. near the bottom.

		/// <summary>
		/// Legacy: always fire the event.
		/// </summary>
		public void RestoreSingleGameObject(GameObject target, SaveData data = null)
			=> RestoreSingleGameObject(target, data, suppressEvent: false);

		/// <summary>
		/// Restores only the specified GameObject from the current (or supplied) SaveData.
		/// Nothing else in the scene is touched.
		/// </summary>
		public void RestoreSingleGameObject(GameObject target, SaveData data = null, bool suppressEvent = false)
		{ ObserveTargetedRestore(RestoreSingleGameObjectAsync(target, data, suppressEvent)); }

		/// <summary>
		/// Tries up to <paramref name="maxRetries"/> times to LoadSaveDataForSlotAsync(slotNumber),
		/// waiting <paramref name="retryDelayMs"/>ms between attempts. If successful, applies the restore.
		/// </summary>
		public async Task<bool> RestoreSingleGameObjectWithRetryAsync(
			GameObject target,
			int        slotNumber,
			int        maxRetries   = 3,
			int        retryDelayMs = 500
			)
			{
            return await QueueTargetedRestoreAsync(target, null, null, slotNumber, maxRetries, retryDelayMs, false);
        }

			/// <summary>
			/// Uses the retrying API under the hood so you get retry logging.
			/// </summary>
	                public async void RestoreSingleGameObject(GameObject target, int slotNumber)
	                {
	                        await RestoreSingleGameObjectWithRetryAsync(target, slotNumber);
	                }

                /// <summary>
                /// Attempts to restore <paramref name="target"/> from <see cref="CurrentSaveData"/>,
                /// retrying while data is unavailable or missing required entries.
                /// </summary>
                public async Task<bool> RestoreSingleGameObjectFromCurrentDataAsync(
                        GameObject target,
                        int maxRetries = 3,
                        int retryDelayMs = 1000
                )
                {
            return await QueueTargetedRestoreAsync(target, null, null, null, maxRetries, retryDelayMs, false);
        }

                /// <summary>
                /// Fire-and-forget wrapper for <see cref="RestoreSingleGameObjectFromCurrentDataAsync"/>.
                /// Logs an error if the restore ultimately fails.
                /// </summary>
                public async void RestoreSingleGameObjectFromCurrentData(GameObject target)
                {
                        bool ok = await RestoreSingleGameObjectFromCurrentDataAsync(target);
                        if (!ok)
                                Logger.Log(
                                        $"RestoreSingleGameObjectFromCurrentData: failed to restore '{target?.name}'.",
                                        LogLevel.Error
                                );
                }

                /// <summary>
                /// Attempts to restore a GameObject or SaveablePrefab identified by
                /// <paramref name="uniqueID"/> (which may be either a prefab instance ID
                /// or a PrefabAssetID) from <see cref="CurrentSaveData"/>, retrying while
                /// data is unavailable or missing required entries.
                /// </summary>
                /// <param name="uniqueID">UniqueID or PrefabAssetID to restore.</param>
                /// <param name="maxRetries">Maximum number of retries while data is unavailable.</param>
                /// <param name="retryDelayMs">Delay in milliseconds between retries.</param>
                public async Task<bool> RestoreSingleGameObjectFromCurrentDataAsync(
                        string uniqueID,
                        int maxRetries = 3,
                        int retryDelayMs = 1000
                )
                {
            return await QueueTargetedRestoreAsync(null, uniqueID, null, null, maxRetries, retryDelayMs, false);
        }

                /// <summary>
                /// Fire-and-forget wrapper for <see cref="RestoreSingleGameObjectFromCurrentDataAsync(string,int,int)"/>.
                /// Logs an error if the restore ultimately fails.
                /// </summary>
                public async void RestoreSingleGameObjectFromCurrentData(string uniqueID)
                {
                        bool ok = await RestoreSingleGameObjectFromCurrentDataAsync(uniqueID);
                        if (!ok)
                                Logger.Log(
                                        $"RestoreSingleGameObjectFromCurrentData: failed to restore '{uniqueID}'.",
                                        LogLevel.Error
                                );
                }

                /// <summary>
                /// Returns the current UniqueID of an instantiated <see cref="SaveablePrefab"/>
                /// that matches the provided <paramref name="prefabAssetID"/>. If no instance
                /// exists, <c>null</c> is returned.
                /// </summary>
                /// <param name="prefabAssetID">The PrefabAssetID to search for.</param>
                public string GetCurrentUniqueIDFromPrefabAssetID(string prefabAssetID)
                {
                        if (string.IsNullOrEmpty(prefabAssetID))
                        {
                                Logger.Log("GetCurrentUniqueIDFromPrefabAssetID: prefabAssetID is null or empty.", LogLevel.Warning);
                                return null;
                        }

                        var prefab = prefabManager?
                                .GetSaveablePrefabs()
                                .FirstOrDefault(p => p.PrefabAssetID == prefabAssetID);

                        if (prefab != null)
                                return prefab.UniqueID;

	                        SaveablePrefab fallback = null;
	                        var prefabs = UnityObjectSearch.FindIncludingInactive<SaveablePrefab>();
	                        for (int i = 0; i < prefabs.Length; i++)
	                        {
	                                SaveablePrefab candidate = prefabs[i];
	                                if (candidate != null && candidate.PrefabAssetID == prefabAssetID)
	                                {
	                                        fallback = candidate;
	                                        break;
	                                }
	                        }

                        if (fallback != null)
                                return fallback.UniqueID;

                        Logger.Log($"GetCurrentUniqueIDFromPrefabAssetID: no instance found for asset ID '{prefabAssetID}'.", LogLevel.Info);
                        return null;
                }

                /// <summary>
                /// Returns the PrefabAssetID associated with a live <see cref="SaveablePrefab"/>
                /// instance that has the provided <paramref name="uniqueID"/>. If no instance
                /// is found, <c>null</c> is returned.
                /// </summary>
                /// <param name="uniqueID">The SaveablePrefab instance UniqueID to search for.</param>
                public string GetPrefabAssetIDFromCurrentUniqueID(string uniqueID)
                {
                        if (string.IsNullOrEmpty(uniqueID))
                        {
                                Logger.Log("GetPrefabAssetIDFromCurrentUniqueID: uniqueID is null or empty.", LogLevel.Warning);
                                return null;
                        }

                        var prefab = prefabManager?
                                .GetSaveablePrefabs()
                                .FirstOrDefault(p => p != null && p.UniqueID == uniqueID && !string.IsNullOrEmpty(p.PrefabAssetID));

                        if (prefab != null)
                                return prefab.PrefabAssetID;

                        GameObject candidate = FindGameObjectByUniqueID(uniqueID, IdentifierType.UniqueID);
                        if (candidate != null)
                        {
                                var cached = SaveablePrefab.TryGetCachedSaveablePrefab(candidate, out var cachedPrefab)
                                        ? cachedPrefab
                                        : candidate.GetComponent<SaveablePrefab>();

                                if (cached != null && !string.IsNullOrEmpty(cached.PrefabAssetID))
                                        return cached.PrefabAssetID;
                        }

	                        SaveablePrefab fallback = null;
	                        var prefabs = UnityObjectSearch.FindIncludingInactive<SaveablePrefab>();
	                        for (int i = 0; i < prefabs.Length; i++)
	                        {
	                                SaveablePrefab candidatePrefab = prefabs[i];
	                                if (candidatePrefab != null &&
	                                    candidatePrefab.UniqueID == uniqueID &&
	                                    !string.IsNullOrEmpty(candidatePrefab.PrefabAssetID))
	                                {
	                                        fallback = candidatePrefab;
	                                        break;
	                                }
	                        }

                        if (fallback != null)
                                return fallback.PrefabAssetID;

                        Logger.Log($"GetPrefabAssetIDFromCurrentUniqueID: no prefab asset found for instance ID '{uniqueID}'.", LogLevel.Info);
                        return null;
                }

                /// <summary>
                /// Attempts to restore a destroyed GameObject from <see cref="CurrentSaveData"/>,
                /// retrying while data is unavailable or missing required entries.
                /// </summary>
                public Task<bool> RestoreDestroyedGameObjectFromCurrentDataAsync(
                        string uniqueID, int maxRetries = 3, int retryDelayMs = 1000)
                    => QueueDestroyedRestoreAsync(uniqueID, null, null, maxRetries, retryDelayMs);

                /// <summary>
                /// Fire-and-forget wrapper for <see cref="RestoreDestroyedGameObjectFromCurrentDataAsync"/>.
                /// Logs an error if the restore ultimately fails.
                /// </summary>
                public async void RestoreDestroyedGameObjectFromCurrentData(string uniqueID)
                {
                        bool ok = await RestoreDestroyedGameObjectFromCurrentDataAsync(uniqueID);
                        if (!ok)
                                Logger.Log(
                                        $"RestoreDestroyedGameObjectFromCurrentData: failed to restore '{uniqueID}'.",
                                        LogLevel.Error
                                );
                }

		/// <summary>
	/// Finds the live GameObject by UniqueID then applies the supplied data.
	/// Falls back to instantiating & restoring a single prefab if needed.
	/// The <paramref name="uniqueID"/> may be either the instance UniqueID
	/// or the prefab's asset ID.
	/// </summary>
	public void RestoreSingleGameObject(string uniqueID, SaveData data = null)
	{
            ObserveTargetedRestore(QueueTargetedRestoreAsync(null, uniqueID, data, null, 1, 0, false));
        }

	/// <summary>
	/// Restores by UniqueID from the given slot (with retry),
	/// falling back to instantiating a prefab if needed. The
	/// <paramref name="uniqueID"/> parameter can be either the
	/// prefab instance ID or the prefab asset ID.
	/// </summary>
        public async void RestoreSingleGameObject(string uniqueID, int slotNumber)
        {
            try { await RestoreSingleGameObjectAsync(uniqueID, slotNumber); }
            catch (Exception ex) { Logger.Log(ex.Message, LogLevel.Error); }
        }


        /// <summary>
        /// Async counterpart to <see cref="RestoreSingleGameObject(string,int)"/>,
        /// allowing callers to <c>await</c> the restore operation.
        /// </summary>
        /// <param name="uniqueID">UniqueID or prefab asset ID.</param>
        /// <param name="slotNumber">Save slot to load from.</param>
        public async Task RestoreSingleGameObjectAsync(string uniqueID, int slotNumber)
        {
            if (!await QueueTargetedRestoreAsync(null, uniqueID, null, slotNumber, 3, 500, false))
                throw new InvalidOperationException("Targeted restore failed for '" + uniqueID + "'.");
        }


        /// <summary>
        /// The general save method for most cases. Initiates a save operation for the specified slot.
                /// </summary>
                /// <param name="slotNumber">The save slot number to save to.</param>
                /// <param name="lastActiveScene">Optional: The name of the last active scene.</param>
                public void Save(int slotNumber = 1, string lastActiveScene = null)
                {
            ObserveOperation(QueueSaveRequest(slotNumber, lastActiveScene, null, false, true, default));
        }

                /// <summary>
                /// Overload allowing the slot name to be set explicitly.
                /// </summary>
                /// <param name="slotNumber">The save slot number to save to.</param>
                /// <param name="lastActiveScene">Optional: The name of the last active scene.</param>
                /// <param name="slotName">Custom slot name. If null or empty the current scene name is used.</param>
                public void Save(int slotNumber, string lastActiveScene, string slotName)
                {
            ObserveOperation(QueueSaveRequest(slotNumber, lastActiveScene, slotName, false, true, default));
        }

		/// <summary>
		/// Saves immediately on the calling thread – no coroutines, no yields.
		/// Intended for OnApplicationQuit / crash handlers.
		/// Screenshots are optional (default: off because they need a frame).
		/// </summary>
                public void SaveSync(int slotNumber = 1,
                                                        string lastActiveScene = null,
                                                        bool captureScreenshot = false)
                {
            try { Operations.RunSync(context => SaveSyncCore(context, slotNumber, lastActiveScene, captureScreenshot)); }
            catch (Exception ex)
            {
                SaveEventDispatch.Raise(OnSaveFailed, this, new OperationFailedEventArgs(GetSaveSlot(slotNumber), "Save", ex.Message));
                Logger.Log($"SaveSync failed: {ex.Message}", LogLevel.Error);
            }
        }

		/// <summary>
		/// Starts the usual coroutine-based <see cref="Save"/> and returns a
		/// <see cref="Task"/> that completes when the save finishes (or fails).
		///
		/// No blocking, no <c>ConfigureAwait</c> needed – you stay on the main
		/// thread while Unity does its <c>yield return</c> dance.
		/// </summary>
		/// <remarks>
		/// Ideal for autosave loops:
		/// <code>
		///     async void Start () {
		///         while (true) {
		///             await SaveManager.Instance.SaveAsync(AUTO_SLOT);
		///             await Task.Delay(TimeSpan.FromMinutes(3));
		///         }
		///     }
		/// </code>
		/// </remarks>
                public Task SaveAsync(int slotNumber = 1,
                                                        string lastActiveScene = null,
                                                        CancellationToken ct = default)
                {
                        return SaveAsync(slotNumber, lastActiveScene, null, ct);
                }

                /// <summary>
                /// Saves the current state to the specified slot with a custom slot name.
                /// </summary>
                /// <param name="slotNumber">The slot number to save to</param>
                /// <param name="lastActiveScene">The last active scene name</param>
                /// <param name="customSlotName">Custom name for the slot (if null, scene name will be used)</param>
                /// <param name="ct">Cancellation token</param>
                /// <returns>A task representing the save operation</returns>
                public Task SaveAsync(int slotNumber,
                                                        string lastActiveScene,
                                                        string customSlotName,
                                                        CancellationToken ct = default)
                {
            return QueueSaveRequest(slotNumber, lastActiveScene, customSlotName, false, false, ct);
        }

                /// <summary>
                /// Debug helper that wraps <see cref="SaveAsync(int,string,string,System.Threading.CancellationToken)"/> in a
                /// try/catch block to surface exceptions that might otherwise freeze the editor
                /// when using certain backends (e.g. Supabase).
                /// </summary>
                /// <remarks>
                /// Intended for diagnostics only.
                /// </remarks>
                public async Task SaveAsyncDebug(int slotNumber = 1,
                                                 string lastActiveScene = null,
                                                 string customSlotName = null,
                                                 CancellationToken ct = default)
                {
                        try
                        {
                                await SaveAsync(slotNumber, lastActiveScene, customSlotName, ct).ConfigureAwait(true);
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"SaveAsyncDebug caught exception: {ex}", LogLevel.Error);
                        }
                }

		/// <summary>
		/// Alternative to SaveSync that saves immediately on the calling thread.
		/// Immediately saves the current game state on the caller’s thread.
		/// <para>
		/// *No coroutines, no <c>yield</c>. The core save-file is flushed before the
		/// method returns; the returned <see cref="Task"/> completes when slot
		/// metadata has also been written (if <see cref="UseLocalMirror"/> is <c>true</c>).
		/// </para>
		/// </summary>
		/// <param name="slotNumber">1-based index of the save-slot.</param>
		/// <param name="lastActiveScene">
		/// Scene name to store in the slot; <c>null</c> ➜ use the current scene.
		/// </param>
		/// <remarks>
		/// Ideal for <c>OnApplicationQuit</c> / <c>OnApplicationPause(true)</c>.
		/// Await this method; do not block Unity's main thread with Wait/Result/GetResult,
        /// because serialization, events and backend work may require the runtime dispatcher.
		/// </remarks>
                public Task SaveImmediateAsync(int slotNumber = 1,
                                                                               string lastActiveScene = null)
                {
            return SaveImmediateAsync(slotNumber, lastActiveScene, CancellationToken.None);
        }
        public Task SaveImmediateAsync(int slotNumber, string lastActiveScene, CancellationToken cancellationToken)
            => QueueSaveRequest(slotNumber, lastActiveScene, null, true, false, cancellationToken);

		/// <summary>
		/// Reads the chosen save slot **without** instantiating anything in the scene
		/// and returns the deserialised <see cref="SaveData"/> blob.
		/// </summary>
		/// <remarks>
		/// • Uses the synchronous path when Cloud-Save is OFF (fast).
		/// • Uses the async API when Cloud-Save is ON; you can `await` it from a coroutine.
		/// </remarks>
		public Task<SaveData> LoadSaveDataForSlotAsync(int slotNumber) => LoadSaveDataForSlotAsync(slotNumber, CancellationToken.None);
        public Task<SaveData> LoadSaveDataForSlotAsync(int slotNumber, CancellationToken token)
            => Operations.RunAsync(async context => { await AwaitOperationReady(context); return await context.WaitAsync(LoadSaveDataForSlotCoreAsync(context, slotNumber)); }, token);

        private async Task<SaveData> LoadSaveDataForSlotCoreAsync(SaveOperationContext context, int slotNumber)
		{
			if (!AreSaveSlotsReady || SlotManager == null)
				await WaitForSaveSlotsAsync();

			SaveSlot slot = GetSaveSlot(slotNumber);
			if (slot == null)
			{
				await WaitForSaveSlotsAsync();
				slot = GetSaveSlot(slotNumber);
			}

			if (slot == null)
				slot = EnsurePrimarySlotExists(slotNumber);

			if (slot == null)
			{
				Logger.Log($"Slot {slotNumber} does not exist.", LogLevel.Warning);
				return null;
			}

                       var data = await PrepareSlotAsync(context, slot, allowMissing: true);
                       if (data != null) ValidateCandidate(data);
                       return data;
               }

		/// <summary>
		/// Initiates a load operation with cancellation support.
		/// </summary>
		/// <param name="slotNumber">The save slot number to load.</param>
		/// <param name="restoreLastActiveScene">Whether to restore the last active scene.</param>
		/// <param name="loadAsync">Whether to load the scene asynchronously.</param>
		/// <param name="allowSceneActivation">Whether to allow scene activation when loaded asynchronously.</param>
		/// <param name="cancellationToken">CancellationToken to observe.</param>
                public void Load(int slotNumber = 1, bool restoreLastActiveScene = false, bool loadAsync = false, bool allowSceneActivation = true, CancellationToken cancellationToken = default)
                {
            ObserveOperation(QueueLoadRequest(slotNumber, restoreLastActiveScene, loadAsync, allowSceneActivation, null, cancellationToken));
        }

		/// <summary>
		/// Ensures the current load has a completion source without replacing
		/// one registered by LoadSaveSlotAsync.
		/// </summary>
		private void EnsureLoadCompletionSource(int slotNumber)
		{
			lock (loadCompletionLock)
			{
				if (!loadCompletionSources.ContainsKey(slotNumber))
				{
					loadCompletionSources[slotNumber] = new TaskCompletionSource<LoadResult>();
				}
			}
		}

		/// <summary>
		/// Attempts to acquire a load lock for the specified slot.
		/// </summary>
		/// <param name="slotNumber">The save slot number.</param>
		/// <returns>True if the lock was acquired; otherwise, false.</returns>
                internal bool TryAcquireLoadLock(int slotNumber)
		{
			lock (loadLocksLock)
			{
				if (loadLocks.TryGetValue(slotNumber, out bool isSlotLoading) && isSlotLoading)
				{
					return false; // Lock already held
				}
				else
				{
					loadLocks[slotNumber] = true; // Acquire lock
					return true;
				}
			}
		}

		/// <summary>
		/// Releases the load lock for the specified slot.
		/// </summary>
		/// <param name="slotNumber">The save slot number.</param>
                internal void ReleaseLoadLock(int slotNumber)
		{
			lock (loadLocksLock)
			{
				if (loadLocks.ContainsKey(slotNumber))
				{
					loadLocks[slotNumber] = false;
				}
				else
				{
					Logger.Log($"ReleaseLoadLock: Attempted to release load lock for non-existent slot {slotNumber}.", LogLevel.Warning);
				}
			}
		}

		/// <summary>
		/// Asynchronously loads a save slot and awaits its completion.
		/// Supports cancellation and timeout.
		/// </summary>
		/// <param name="slotNumber">The save slot number to load.</param>
		/// <param name="restoreLastActiveScene">Whether to restore the last active scene.</param>
		/// <param name="loadAsync">Whether to load the scene asynchronously.</param>
		/// <param name="allowSceneActivation">Whether to allow scene activation when loaded asynchronously.</param>
		/// <param name="timeout">Optional: Maximum duration to wait for the load operation.</param>
		/// <param name="cancellationToken">Optional: Token to observe while waiting for the load operation to complete.</param>
		/// <returns>A Task that resolves to a LoadResult indicating success or failure with an error message.</returns>
                public async Task<LoadResult> LoadSaveSlotAsync(
                        int slotNumber = 1,
                        bool restoreLastActiveScene = false,
                        bool loadAsync = false,
                        bool allowSceneActivation = true,
                        TimeSpan? timeout = null,
                        CancellationToken cancellationToken = default)
                {
            return await QueueLoadRequest(slotNumber, restoreLastActiveScene, loadAsync, allowSceneActivation, timeout, cancellationToken);
        }

		/// <summary>
		/// Deletes the specified save slot.
		/// </summary>
		/// <param name="slotNumber">The save slot number to delete.</param>
                public async void Delete(int slotNumber)
                {
                    try { await DeleteAsync(slotNumber); }
                    catch { /* DeleteAsync reports failure through the existing event/log contract. */ }
                }

                /// <summary>Deletes a slot and reports completion through both the returned Task and existing events.</summary>
                public Task DeleteAsync(int slotNumber) => DeleteAsync(slotNumber, CancellationToken.None);
                public async Task DeleteAsync(int slotNumber, CancellationToken cancellationToken)
                {
            await Operations.RunAsync(async context =>
            {
                await AwaitOperationReady(context);
                var slot = SlotManager.GetByNumber(slotNumber);
                try
                {
                    if (slot == null) throw new ArgumentException($"Save slot {slotNumber} does not exist.");
                    context.BeginCommit();
                    await SlotManager.DeleteAsync(slotNumber);
                    NotifyOperation(() => { SaveEventDispatch.Raise(OnDeleteCompleted, this, new SaveManagerEventArgs(slot)); SaveEventDispatch.Raise(OnSaveSlotsUpdated); });
                }
                catch (Exception ex)
                {
                    NotifyOperation(() => InvokeDeleteFailed(slot, "Delete", ex.Message));
                    throw;
                }
            }, cancellationToken);
        }

		/// <summary>
		/// Registers a TaskCompletionSource for a specific save slot.
		/// Prevents multiple concurrent load operations for the same slot.
		/// </summary>
		public bool RegisterLoadCompletion(int slotNumber, TaskCompletionSource<LoadResult> tcs)
		{
			lock (loadCompletionLock)
			{
				if (loadCompletionSources.ContainsKey(slotNumber))
				{
					Logger.Log($"LoadSaveSlotAsync: A load operation is already in progress for slot {slotNumber}.", LogLevel.Warning);
					return false;
				}
				loadCompletionSources.Add(slotNumber, tcs);
				return true;
			}
		}

		/// <summary>
		/// Unregisters a TaskCompletionSource for a specific save slot.
		/// </summary>
		public void UnregisterLoadCompletion(int slotNumber)
		{
			lock (loadCompletionLock)
			{
				loadCompletionSources.Remove(slotNumber);
			}
		}

		/// <summary>
		/// Attempts to set the load completion TaskCompletionSource.
		/// </summary>
		public bool TrySetLoadCompletion(int slotNumber, bool success, string errorMessage = null)
		{
			TaskCompletionSource<LoadResult> tcs = null;
			lock (loadCompletionLock)
			{
				if (loadCompletionSources.TryGetValue(slotNumber, out tcs))
				{
					loadCompletionSources.Remove(slotNumber);
				}
			}

			if (tcs != null)
			{
				tcs.SetResult(new LoadResult(success, errorMessage));
				return true;
			}
			return false;
		}

		/// <summary>
		/// Attempts to set the load failure TaskCompletionSource.
		/// </summary>
		public bool TrySetLoadFailure(int slotNumber, string errorMessage)
		{
			TaskCompletionSource<LoadResult> tcs = null;
			lock (loadCompletionLock)
			{
				if (loadCompletionSources.TryGetValue(slotNumber, out tcs))
				{
					loadCompletionSources.Remove(slotNumber);
				}
			}

			if (tcs != null)
			{
				tcs.SetResult(new LoadResult(false, errorMessage));
				return true;
			}
			return false;
		}

		/// <summary>
		/// Handles the completion of a load operation, signaling success or failure.
		/// </summary>
		/// <param name="slotNumber">The save slot number associated with the load operation.</param>
		/// <param name="success">Indicates whether the load was successful.</param>
		/// <param name="errorMessage">Optional error message in case of failure.</param>
        internal void HandleLoadCompletion(int slotNumber, bool success, string errorMessage = null)
            => CompleteLoad(Operations.Active, slotNumber, success, errorMessage);

        internal void CompleteLoad(SaveOperationContext context, int slotNumber, bool success, string errorMessage = null)
        {
            if (context != null)
            {
                if (context.LoadReported) return;
                context.LoadReported = true; context.LoadError = success ? null : errorMessage ?? "Load failed.";
            }
            if (success) currentSaveSlot = GetSaveSlot(slotNumber);
            NotifyOperation(context, () =>
            {
                TrySetLoadCompletion(slotNumber, success, errorMessage); // legacy observers only
                if (success) SaveEventDispatch.Raise(OnLoadCompleted, this, new SaveLoadEventArgs(currentSaveSlot, true, $"Load completed for slot {slotNumber}."));
                else
                {
                    SaveEventDispatch.Raise(OnLoadFailed, this, new OperationFailedEventArgs(GetSaveSlot(slotNumber), "Load", errorMessage));
                    Logger.Log($"HandleLoadCompletion: Load operation for slot {slotNumber} failed with error: {errorMessage}", LogLevel.Error);
                }
            });
        }

		/// <summary>
		/// Handles the completion of a save operation, signaling success or failure.
		/// Ensures that event invocations occur on the main thread.
		/// </summary>
		/// <param name="slot">The save slot associated with the save operation.</param>
		/// <param name="success">Indicates whether the save was successful.</param>
		/// <param name="errorMessage">Optional error message in case of failure.</param>
        public void HandleSaveCompletion(SaveSlot slot, bool success, string errorMessage = null)
            => CompleteSave(Operations.Active, slot, success, errorMessage);

        internal void CompleteSave(SaveOperationContext context, SaveSlot slot, bool success, string errorMessage = null)
        {
            if (success)
            {
                PublishCommittedSlot(slot);
                try { if (context?.PreserveLiveState != true) componentManager?.SnapshotCurrentSceneAll(); }
                catch (Exception ex) { Logger.Log($"Snapshot after save failed: {ex.Message}", LogLevel.Warning); }
            }
            if (context != null) { context.SaveReported = true; context.SaveError = success ? null : errorMessage ?? "Save failed."; }
            NotifyOperation(context, () =>
            {
                if (success)
                {
                    SaveEventDispatch.Raise(OnSaveCompleted, this, new SaveLoadEventArgs(slot, true, $"Save completed for slot {slot.SlotNumber}."));
                    SaveEventDispatch.Raise(OnSaveSlotsUpdated);
                }
                else
                {
                    SaveEventDispatch.Raise(OnSaveFailed, this, new OperationFailedEventArgs(slot, "Save", errorMessage));
                    Logger.Log($"HandleSaveCompletion: Save operation for slot {slot?.SlotNumber} failed with error: {errorMessage}", LogLevel.Error);
                }
            });
        }


        internal void PublishCommittedSlot(SaveSlot slot)
        {
            string previousScreenshot = GetSaveSlot(slot.SlotNumber)?.ScreenshotFileName;
            SlotManager?.InvalidateCloudCache(slot.SlotNumber);
            SyncSlotToInternalList(slot);
            currentSaveSlot = slot;
            CleanupCommittedScreenshot(previousScreenshot, slot.ScreenshotFileName);
        }

		/// <summary>
		/// Asynchronously renames a save slot.
		/// Ensures that Unity API calls occur on the main thread.
		/// </summary>
		/// <param name="slotNumber">The save slot number to rename.</param>
		/// <param name="newName">The new name for the save slot.</param>
		/// <returns>A Task that resolves to true if the renaming was successful; otherwise, false.</returns>
		public async Task<bool> RenameSaveSlotAsync(int slotNumber, string newName)
		{
            try
            {
                return await Operations.RunAsync(async context =>
                {
                    await AwaitOperationReady(context);
                    if (string.IsNullOrEmpty(newName)) throw new ArgumentException("New name cannot be empty.");
                    var slot = CopySlot(SlotManager.GetByNumber(slotNumber) ?? EnsurePrimarySlotExists(slotNumber));
                    string oldName = slot.SlotName;
                    slot.SlotName = newName;
                    context.MetadataUpdate = new SaveSlotMetadataUpdate(newName);
                    context.BeginCommit();
                    await saveSystem.SaveSlotMetadataAsync(slot);
                    SyncSlotToInternalList(slot);
                    NotifyOperation(() => { SaveEventDispatch.Raise(OnSaveSlotsUpdated); SaveEventDispatch.Raise(OnRenameSlotCompleted, this, new RenameSlotEventArgs(slot, oldName, newName)); });
                    return true;
                });
            }
            catch (Exception ex) { InvokeRenameSlotFailed(GetSaveSlot(slotNumber), "Rename", ex.Message); return false; }
        }


		/// <summary>
		/// Renames the specified save slot.
		/// Should be called only from the main thread.
		/// </summary>
		/// <param name="slotNumber">The save slot number to rename.</param>
		/// <param name="newName">The new name for the save slot.</param>
                public void RenameSlot(int slotNumber, string newName)
                {
            try
            {
                Operations.RunSync(context =>
                {
                    if (!IsInitialized || saveSettings.enableCloudSave || !(saveSystem is SaveSystem local)) throw new InvalidOperationException("RenameSlot requires initialized local storage; use RenameSaveSlotAsync.");
                    if (string.IsNullOrEmpty(newName)) throw new ArgumentException("New name cannot be empty.");
                    var slot = CopySlot(GetSaveSlot(slotNumber)); string oldName = slot.SlotName;
                    slot.SlotName = newName;
                    context.BeginCommit(); local.SaveSlotMetadata(slot); SyncSlotToInternalList(slot);
                    NotifyOperation(() => { SaveEventDispatch.Raise(OnSaveSlotsUpdated); SaveEventDispatch.Raise(OnRenameSlotCompleted, this, new RenameSlotEventArgs(slot, oldName, newName)); });
                });
            }
            catch (Exception ex) { InvokeRenameSlotFailed(GetSaveSlot(slotNumber), "Rename", ex.Message); }
        }

		/// <summary>
		/// Retrieves a screenshot associated with the specified save slot.
		/// </summary>
		/// <param name="slot">The save slot to retrieve the screenshot from.</param>
		/// <returns>The screenshot as a Texture2D, or null if not found.</returns>
                public Texture2D GetScreenshot(SaveSlot slot)
                {
                        return SaveOperations?.GetScreenshot(slot);
                }

		/// <summary>
		/// Captures a screenshot synchronously for the given slot number and
		/// returns the generated file name.  Uses the same logic as the
		/// internal save routine.
		/// </summary>
                public string CaptureScreenshotSync(int slotNumber)
                {
                        return SaveOperations != null ?
                                SaveOperations.CaptureScreenshotSync(slotNumber) :
                                null;
                }

		/// <summary>
		/// Cleans up unused screenshots based on the current save slots.
		/// </summary>
                public void CleanupScreenshots()
                {
                        SaveOperations?.CleanupScreenshots();
                }

                public Task QuickSaveAsync() => Operations.RunAsync(async context => { await AwaitOperationReady(context); await QuickSaveCoreAsync(context); });

                private async Task QuickSaveCoreAsync(SaveOperationContext context)
                {
                        float startTime = Time.realtimeSinceStartup;

                        await EnsureRotatingSlotsReadyAsync(false);

                        var settings = SaveSettings;
                        if (settings == null)
                        {
                                Logger.Log("[QuickSave] Aborting: SaveSettings is null.", LogLevel.Warning);
                                return;
                        }

                        int limit = settings.numberOfQuickSaveSlots;
                        int offset = settings.quickSaveSlotOffset;
                        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                        Logger.Log($"[QuickSave] Starting QuickSaveAsync (limit={limit}, offset={offset}, scene='{sceneName}', time={DateTime.UtcNow:O}).", LogLevel.Info);

                        if (limit <= 0)
                        {
                                Logger.Log($"[QuickSave] Aborting: numberOfQuickSaveSlots={limit} (must be > 0).", LogLevel.Warning);
                                return;
                        }

                        if (SlotManager == null)
                        {
                                Logger.Log("[QuickSave] Aborting: SlotManager is null.", LogLevel.Error);
                                return;
                        }

                        if (QuickSlotManager == null)
                        {
                                Logger.Log("[QuickSave] QuickSlotManager is null after WaitForQuickSlotsAsync; reinitializing quick slots.", LogLevel.Warning);
                                await InitializeRotatingSlotsCoreAsync(limit, false);
                                if (QuickSlotManager == null)
                                {
                                        Logger.Log("[QuickSave] Aborting: QuickSlotManager is still null after reinitialization.", LogLevel.Error);
                                        return;
                                }
                        }

                        try
                        {
                                context.PrepareHistory = () => PrepareHistoryAsync(context, QuickSlotManager, offset, limit);

                                int slotNumber = offset + 1;
                                var primarySlot = EnsurePrimarySlotExists(slotNumber);
                                Logger.Log($"[QuickSave] Target primary slot {slotNumber} ready? {primarySlot != null}.", LogLevel.Info);
                                Logger.Log($"[QuickSave] Saving slot {slotNumber}.", LogLevel.Info);
                                if (settings.disableQuickSaveScreenshots)
                                        SaveOperations.SuppressNextScreenshot = true;
                                await SaveOperations.RunSaveAsync(context, slotNumber, sceneName, "Quick Save", false);

                                var savedSlot = SlotManager?.GetByNumber(slotNumber) ?? EnsurePrimarySlotExists(slotNumber);
                                var quickSlot = QuickSlotManager?.GetByNumber(slotNumber);
                                Logger.Log($"[QuickSave] Post-save lookup: savedSlot null? {savedSlot == null}, quickSlot null? {quickSlot == null}.", LogLevel.Info);

                                if (savedSlot != null)
                                {
                                        Logger.Log($"[QuickSave] Saved slot {savedSlot.SlotNumber}: LastSaved={savedSlot.LastSaved:O}, SlotName='{savedSlot.SlotName}', Screenshot='{savedSlot.ScreenshotFileName ?? "null"}'.", LogLevel.Info);
                                }

                                if (savedSlot != null && quickSlot != null)
                                {
                                        quickSlot.LastSaved = savedSlot.LastSaved;
                                        quickSlot.LastActiveScene = savedSlot.LastActiveScene;
                                        quickSlot.SlotName = savedSlot.SlotName;
                                        quickSlot.ScreenshotFileName = savedSlot.ScreenshotFileName;
                                        quickSlot.CustomMetadata = savedSlot.CustomMetadata != null
                                                ? new Dictionary<string, string>(savedSlot.CustomMetadata)
                                                : null;

                                        Logger.Log($"[QuickSave] Quick slot {quickSlot.SlotNumber} metadata synchronized.", LogLevel.Info);
                                }
                                else if (quickSlot == null)
                                {
                                        Logger.Log("[QuickSave] QuickSlotManager returned null for target slot; metadata sync skipped.", LogLevel.Warning);
                                }

                                NotifyOperation(() => SaveEventDispatch.Raise(OnQuickSlotsUpdated));
                                Logger.Log($"[QuickSave] Completed in {(Time.realtimeSinceStartup - startTime) * 1000f:F1} ms.", LogLevel.Info);
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"[QuickSave] Exception encountered: {ex}", LogLevel.Error);
                                throw;
                        }
                }

                public void QuickSave()
                {
            ObserveOperation(QuickSaveAsync());
        }

                private SaveSlot EnsurePrimarySlotExists(int slotNumber)
                {
                        if (SlotManager == null)
                                return null;

                        var slot = SlotManager.GetByNumber(slotNumber);
                        if (slot != null)
                                return slot;

                        slot = new SaveSlot(slotNumber, $"Slot {slotNumber}", DateTime.MinValue, string.Empty, string.Empty);
                        SlotManager.AddSlotInNumberOrder(slot);

                        if (!saveSlots.Any(s => s.SlotNumber == slotNumber))
                        {
                                saveSlots.Add(slot);
                                saveSlots.Sort((a, b) => a.SlotNumber.CompareTo(b.SlotNumber));
                        }

                        Logger.Log($"[QuickSave] Created placeholder slot {slotNumber} in primary SlotManager.", LogLevel.Info);
                        return slot;
                }

                public void QuickLoad()
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("QuickLoad: Waiting for SaveManager initialisation...", LogLevel.Info);
                                QueueOperation(() => QuickLoad());
                                return;
                        }

                        int slot = SaveSettings.quickSaveSlotOffset + 1;
                        Load(slot, true);
                }

                public async Task QuickLoadAsync(
                        bool loadAsync = false,
                        bool allowSceneActivation = true,
                        TimeSpan? timeout = null,
                        CancellationToken cancellationToken = default)
                {
                        int slot = SaveSettings.quickSaveSlotOffset + 1;
                        var result = await LoadSaveSlotAsync(
                                slot,
                                true,
                                loadAsync,
                                allowSceneActivation,
                                timeout,
                                cancellationToken);
                if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
                }

                public Task AutoSaveAsync() => Operations.RunAsync(async context => { await AwaitOperationReady(context); await AutoSaveCoreAsync(context); });

                private async Task AutoSaveCoreAsync(SaveOperationContext context)
                {
                        var settings = SaveSettings;
                        int limit = settings.numberOfAutoSaveSlots;

                        // Multi-slot FIFO auto save (mirrors QuickSaveAsync)
                        if (limit > 0)
                        {
                                await EnsureRotatingSlotsReadyAsync(true);

                                if (AutoSlotManager == null)
                                {
                                        Logger.Log("[AutoSave] Aborting: AutoSlotManager is null. Ensure InitializeAutoSaveSlotsAsync was called before using multi-slot auto saves.", LogLevel.Warning);
                                        return;
                                }

                                int offset = settings.autoSaveSlotOffset;
                                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                                Logger.Log($"[AutoSave] Starting multi-slot AutoSaveAsync (limit={limit}, offset={offset}).", LogLevel.Info);

                                if (SlotManager == null)
                                {
                                        Logger.Log("[AutoSave] Aborting: SlotManager is null.", LogLevel.Error);
                                        return;
                                }

                                context.PrepareHistory = () => PrepareHistoryAsync(context, AutoSlotManager, offset, limit);

                                int slotNumber = offset + 1;
                                EnsurePrimarySlotExists(slotNumber);
                                if (settings.disableAutoSaveScreenshots)
                                        SaveOperations.SuppressNextScreenshot = true;
                                await SaveOperations.RunSaveAsync(context, slotNumber, sceneName, "Auto Save", false);

                                var savedSlot = SlotManager?.GetByNumber(slotNumber) ?? EnsurePrimarySlotExists(slotNumber);
                                var autoSlot = AutoSlotManager?.GetByNumber(slotNumber);
                                if (savedSlot != null && autoSlot != null)
                                {
                                        autoSlot.LastSaved = savedSlot.LastSaved;
                                        autoSlot.LastActiveScene = savedSlot.LastActiveScene;
                                        autoSlot.SlotName = savedSlot.SlotName;
                                        autoSlot.ScreenshotFileName = savedSlot.ScreenshotFileName;
                                        autoSlot.CustomMetadata = savedSlot.CustomMetadata != null
                                                ? new Dictionary<string, string>(savedSlot.CustomMetadata)
                                                : null;
                                }

                                NotifyOperation(() => SaveEventDispatch.Raise(OnAutoSlotsUpdated));
                                return;
                        }

                        // Legacy single-slot auto save
                        int slot = settings.autoSaveSlotNumber;
                        if (slot <= 0) return;

                        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                        if (settings.disableAutoSaveScreenshots)
                                SaveOperations.SuppressNextScreenshot = true;
                        await SaveOperations.RunSaveAsync(context, slot, scene, "Auto Save", false);
                }

                public void AutoSave()
                {
            ObserveOperation(AutoSaveAsync());
        }

                /// <summary>
                /// Loads the autosave asynchronously with full control over scene loading.
                /// </summary>
                public async Task LoadAutoSaveAsync(
                        bool restoreScene = true,
                        bool loadAsync = false,
                        bool allowSceneActivation = true,
                        TimeSpan? timeout = null,
                        CancellationToken cancellationToken = default)
                {
                        int slot = SaveSettings.numberOfAutoSaveSlots > 0
                                ? SaveSettings.autoSaveSlotOffset + 1
                                : SaveSettings.autoSaveSlotNumber;
                        if (slot <= 0)
                        {
                                throw new InvalidOperationException("Auto save is not configured.");
                        }
                        
                        var result = await LoadSaveSlotAsync(
                                slot,
                                restoreScene,
                                loadAsync,
                                allowSceneActivation,
                                timeout,
                                cancellationToken);
                if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
                }

                /// <summary>
                /// Loads the autosave synchronously. Alias for LoadAutoSave().
                /// </summary>
                public void AutoLoad(bool restoreScene = true)
                {
                        LoadAutoSave(restoreScene);
                }

                /// <summary>
                /// Loads the autosave synchronously.
                /// </summary>
                public void LoadAutoSave(bool restoreScene = true)
                {
                        if (!IsInitialized || SaveOperations == null)
                        {
                                Logger.Log("LoadAutoSave: Waiting for SaveManager initialisation...", LogLevel.Info);
                                QueueOperation(() => LoadAutoSave(restoreScene));
                                return;
                        }

                        int slot = SaveSettings.numberOfAutoSaveSlots > 0
                                ? SaveSettings.autoSaveSlotOffset + 1
                                : SaveSettings.autoSaveSlotNumber;
                        if (slot <= 0)
                        {
                                Logger.Log("LoadAutoSave: Auto save is not configured (legacy Auto Save Slot Number <= 0 and Number Of Auto Save Slots <= 0).", LogLevel.Warning);
                                return;
                        }
                        
                        Load(slot, restoreScene);
                }

		/// <summary>
		/// Fetches metadata for the given slot, falling back to remote backends if no local mirror exists.
		/// </summary>
		public async Task<SaveSlot> GetSlotMetadataAsync(int slotNumber)
		{
			// 1 Unity Cloud Save fallback 
#if REMEMBERME_CLOUDSAVE_PRESENT
			if (SaveSettings.backend == SaveBackend.UnityCloudSave
			    && SaveSystem is SaveSystem unityCloudSave)
			{
				var remote = await unityCloudSave.LoadSlotMetadataAsync(slotNumber);
				if (remote != null) return remote;
			}
#endif
			
			// 2 Supabase fallback
			if (SaveSettings.backend == SaveBackend.Supabase
                                && SaveSystem is SupabaseSaveSystem supabase)
                        {
                                var remote = await supabase.LoadSlotMetadataAsync(slotNumber);
                                if (remote != null) return remote;
                        }
                        else if (SaveSettings.backend == SaveBackend.Firebase
                                && SaveSystem is FirebaseSaveSystem firebase)
                        {
                                var remote = await firebase.LoadSlotMetadataAsync(slotNumber);
                                if (remote != null) return remote;
                        }

			// 3 Local in-memory load from startup (fallback)
			return GetSaveSlotByNumber(slotNumber);
		}

		/// <summary>
        /// Returns the cached slot metadata without network I/O. Use GetSlotMetadataAsync
        /// to await a backend read, or ForceRefreshSlotsAsync to refresh the published list.
        /// </summary>
        public SaveSlot GetSlotMetadata(int slotNumber) => SlotManager?.GetByNumber(slotNumber);

		/// <summary>
		/// Retrieves a SaveSlot by its slot number.
		/// </summary>
		/// <param name="slotNumber">The slot number to retrieve.</param>
		/// <returns>The corresponding SaveSlot, or null if not found.</returns>
                private SaveSlot GetSaveSlot(int slotNumber)
                {
                        return SlotManager.GetByNumber(slotNumber);
                }

		/// <summary>
		/// Retrieves a SaveSlot by its slot name.
		/// </summary>
		/// <param name="slotName">The slot name to retrieve.</param>
		/// <returns>The corresponding SaveSlot, or null if not found.</returns>
		public SaveSlot GetSaveSlotByName(string slotName)
		{
                        return SlotManager.GetByName(slotName);
		}

		/// <summary>
		/// Retrieves a SaveSlot by its SlotNumber.
		/// </summary>
		public SaveSlot GetSaveSlotByNumber(int slotNumber)
		{
                        return SlotManager.GetByNumber(slotNumber);
		}

	/// <summary>
	/// Retrieves all save slots.
	/// </summary>
	/// <returns>A list of SaveSlot objects.</returns>
	public List<SaveSlot> GetSaveSlots()
	{
                        // Use the internal saveSlots list which is synchronized with SlotManager
                        // This ensures we return the correct runtime slot count even after SetSaveSlotCountAsync
                        var slots = saveSlots != null && saveSlots.Count > 0 
                            ? new List<SaveSlot>(saveSlots) 
                            : (SlotManager?.GetAll() ?? new List<SaveSlot>());
                        
						Logger.Log($"[SaveManager] GetSaveSlots returning {slots.Count} slots", LogCategory.SaveManager);
                        foreach (var slot in slots)
                        {
                            if (slot.LastSaved > DateTime.MinValue)
                            {
								Logger.Log($"[SaveManager]   Slot {slot.SlotNumber}: Name='{slot.SlotName}', LastSaved={slot.LastSaved}, Screenshot='{slot.ScreenshotFileName ?? "null"}'", LogCategory.SaveManager);
                            }
	                        }
	                        return slots;
		}

		/// <summary>
		/// Retrieves all save slots whose <see cref="SaveSlot.CustomMetadata"/> contains an exact key/value match.
		/// </summary>
		/// <param name="metadataKey">Metadata key to match.</param>
		/// <param name="metadataValue">Metadata value to match.</param>
		/// <param name="comparison">String comparison mode for value matching.</param>
		/// <returns>Matching save slots sorted by slot number.</returns>
		public List<SaveSlot> GetSaveSlotsByMetadata(
			string metadataKey,
			string metadataValue,
			StringComparison comparison = StringComparison.Ordinal)
		{
			if (string.IsNullOrEmpty(metadataKey))
				return new List<SaveSlot>();

			string expected = metadataValue ?? string.Empty;
			return GetSaveSlots()
				.Where(s =>
					s != null &&
					s.CustomMetadata != null &&
					s.CustomMetadata.TryGetValue(metadataKey, out var current) &&
					string.Equals(current ?? string.Empty, expected, comparison))
				.OrderBy(s => s.SlotNumber)
				.ToList();
		}

		/// <summary>
		/// Retrieves the latest slot matching a metadata key/value pair.
		/// </summary>
		/// <param name="metadataKey">Metadata key to match.</param>
		/// <param name="metadataValue">Metadata value to match.</param>
		/// <param name="comparison">String comparison mode for value matching.</param>
		/// <returns>The latest matching slot, or null when none exists.</returns>
		public SaveSlot GetLatestSaveSlotByMetadata(
			string metadataKey,
			string metadataValue,
			StringComparison comparison = StringComparison.Ordinal)
		{
			return GetSaveSlotsByMetadata(metadataKey, metadataValue, comparison)
				.OrderByDescending(s => s.LastSaved)
				.ThenByDescending(s => s.SlotNumber)
				.FirstOrDefault();
		}

		/// <summary>
		/// Ensures the runtime slot count includes all slot folders/metadata files found on disk.
		/// Useful for world-style save workflows where the number of worlds grows over time.
		/// </summary>
		/// <param name="includeReservedSystemSlots">
		/// When false, quick-save/auto-save reserved ranges are excluded from expansion.
		/// </param>
		/// <returns>The final primary slot count.</returns>
		public async Task<int> EnsureSlotCountCoversDiscoveredLocalDataAsync(bool includeReservedSystemSlots = false)
		{
            // Initialization invokes discovery before IsInitialized/SaveOperations can become ready.
            if (!initializationCompleted) return await EnsureSlotCountCoversDiscoveredLocalDataAsync(includeReservedSystemSlots, CancellationToken.None);
			return await Operations.RunAsync(async context => { await AwaitOperationReady(context); return await EnsureSlotCountCoversDiscoveredLocalDataAsync(includeReservedSystemSlots, context.Token); });
		}

		private async Task<int> EnsureSlotCountCoversDiscoveredLocalDataAsync(
			bool includeReservedSystemSlots,
			CancellationToken cancellationToken)
		{
			if (saveSettings == null)
				return CurrentSaveSlotCount;

			cancellationToken.ThrowIfCancellationRequested();
			await AwaitWithCancellation(
				WaitForSaveSlotsAsync(),
				cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();

			int currentCount = Math.Max(CurrentSaveSlotCount, 0);
			int discoveredHighest = DiscoverHighestPrimarySlotFromDisk(
				includeReservedSystemSlots,
				cancellationToken);
			if (discoveredHighest > currentCount)
			{
				Logger.Log($"EnsureSlotCountCoversDiscoveredLocalDataAsync: Expanding slot count from {currentCount} to {discoveredHighest}.",
					LogCategory.SaveManager,
					LogLevel.Info);
				await AwaitWithCancellation(
					SetSaveSlotCountCoreAsync(discoveredHighest),
					cancellationToken);
			}

			cancellationToken.ThrowIfCancellationRequested();
			return CurrentSaveSlotCount;
		}

		private static CancellationTokenSource CreateRuntimeLoadOperationCancellationSource(
			TimeSpan? timeout,
			CancellationToken cancellationToken)
		{
			CancellationTokenSource operationCancellation =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			if (timeout.HasValue)
				ConfigureOperationDeadline(operationCancellation, timeout.Value);
			return operationCancellation;
		}

		private static async Task AwaitWithCancellation(
			Task operation,
			CancellationToken cancellationToken)
		{
			if (operation == null)
				throw new ArgumentNullException(nameof(operation));

			if (!cancellationToken.CanBeCanceled)
			{
				await operation;
				return;
			}

			cancellationToken.ThrowIfCancellationRequested();
#if UNITY_WEBGL && !UNITY_EDITOR
            while (!operation.IsCompleted)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    SaveOperationContext.ObserveLateFailure(operation);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }
#else
			var cancellationSignal =
				new TaskCompletionSource<bool>(
					TaskCreationOptions.RunContinuationsAsynchronously);
			using (cancellationToken.Register(
				       state =>
					       ((TaskCompletionSource<bool>)state).TrySetResult(true),
				       cancellationSignal))
			{
				Task completed = await Task.WhenAny(
					operation,
					cancellationSignal.Task);
				if (completed != operation)
				{
					// The underlying slot-initialization APIs predate cancellation.
					// Observe a later fault while allowing the metadata operation to
					// stop immediately when its owner cancels or times out.
					_ = operation.ContinueWith(
						task => { _ = task.Exception; },
						CancellationToken.None,
						TaskContinuationOptions.OnlyOnFaulted |
						TaskContinuationOptions.ExecuteSynchronously,
						TaskScheduler.Default);
					cancellationToken.ThrowIfCancellationRequested();
				}
			}

#endif
			await operation;
		}

		/// <summary>
		/// Finds the latest slot matching metadata; creates one when missing.
		/// </summary>
		/// <param name="metadataKey">Metadata key used as a logical world/profile identifier.</param>
		/// <param name="metadataValue">Metadata value to match or assign.</param>
		/// <param name="slotName">Optional slot display name when creating/updating.</param>
		/// <param name="includeReservedSystemSlots">
		/// When false, quick-save/auto-save reserved ranges are excluded from new slot allocation.
		/// </param>
		/// <returns>The matching or newly created slot, or null on failure.</returns>
		public Task<SaveSlot> GetOrCreateSaveSlotByMetadataAsync(string metadataKey, string metadataValue, string slotName = null, bool includeReservedSystemSlots = false)
        => Operations.RunAsync(async context => { await AwaitOperationReady(context); return await GetOrCreateSaveSlotByMetadataCoreAsync(metadataKey, metadataValue, slotName, includeReservedSystemSlots); });

        private async Task<SaveSlot> GetOrCreateSaveSlotByMetadataCoreAsync(
			string metadataKey,
			string metadataValue,
			string slotName = null,
			bool includeReservedSystemSlots = false, bool persist = true)
		{
			if (string.IsNullOrEmpty(metadataKey))
			{
				Logger.Log("GetOrCreateSaveSlotByMetadataAsync: metadataKey cannot be null or empty.",
					LogCategory.SaveManager,
					LogLevel.Warning);
				return null;
			}

			await EnsureSlotCountCoversDiscoveredLocalDataAsync(includeReservedSystemSlots, SaveIoScope.Token);

			SaveSlot slot = GetLatestSaveSlotByMetadata(metadataKey, metadataValue, StringComparison.Ordinal);
			if (slot == null)
			{
				int candidateNumber = 1;
				var used = new HashSet<int>(GetSaveSlots().Select(s => s.SlotNumber));
				while (used.Contains(candidateNumber) ||
				       (!includeReservedSystemSlots && IsReservedSystemSlot(candidateNumber)))
				{
					candidateNumber++;
				}

				if (candidateNumber > CurrentSaveSlotCount)
					await SetSaveSlotCountCoreAsync(candidateNumber);

				slot = GetSaveSlotByNumber(candidateNumber) ?? EnsurePrimarySlotExists(candidateNumber);
				if (slot == null)
				{
					Logger.Log($"GetOrCreateSaveSlotByMetadataAsync: Failed to create slot {candidateNumber}.",
						LogCategory.SaveManager,
						LogLevel.Error);
					return null;
				}
			}

			slot = CopySlot(slot);
            slot.CustomMetadata ??= new Dictionary<string, string>();
			slot.CustomMetadata[metadataKey] = metadataValue ?? string.Empty;
			if (!string.IsNullOrEmpty(slotName))
				slot.SlotName = slotName;

            if (!persist) return slot;
            Operations.Active.MetadataUpdate = new SaveSlotMetadataUpdate(
                string.IsNullOrEmpty(slotName) ? null : slotName,
                new Dictionary<string, string> { [metadataKey] = metadataValue ?? string.Empty });
            BeginOperationCommit();
            await saveSystem.SaveSlotMetadataAsync(slot);

			SyncSlotToInternalList(slot);
			NotifyOperation(() => NotifySaveSlotsUpdated());
			return slot;
		}

		/// <summary>
		/// Saves to a slot identified by metadata. Creates a slot when no match exists.
		/// </summary>
		/// <param name="metadataKey">Metadata key used as a logical world/profile identifier.</param>
		/// <param name="metadataValue">Metadata value to match or assign.</param>
		/// <param name="lastActiveScene">Optional scene name override.</param>
		/// <param name="customSlotName">Optional slot display name.</param>
		/// <param name="ct">Cancellation token for the save operation.</param>
		/// <param name="includeReservedSystemSlots">
		/// When false, quick-save/auto-save reserved ranges are excluded from new slot allocation.
		/// </param>
		/// <returns>The slot number that was saved, or -1 on failure to allocate a slot.</returns>
		public async Task<int> SaveByMetadataAsync(
			string metadataKey,
			string metadataValue,
			string lastActiveScene = null,
			string customSlotName = null,
			CancellationToken ct = default,
			bool includeReservedSystemSlots = false)
		{
            return await Operations.RunAsync(async context =>
            {
                await AwaitOperationReady(context);
                var slot = await GetOrCreateSaveSlotByMetadataCoreAsync(metadataKey, metadataValue, customSlotName, includeReservedSystemSlots, false);
                if (slot == null) return -1;
                context.MetadataOverrides[metadataKey] = metadataValue ?? string.Empty;
                await SaveOperations.RunSaveAsync(context, slot.SlotNumber, lastActiveScene, customSlotName, false, preparedSlot: slot);
                return slot.SlotNumber;
            }, ct);
        }

		/// <summary>
		/// Loads the most recently saved slot matching a metadata key/value pair.
		/// </summary>
		/// <param name="metadataKey">Metadata key used as a logical world/profile identifier.</param>
		/// <param name="metadataValue">Metadata value to match.</param>
		/// <param name="restoreLastActiveScene">Whether to restore the saved active scene.</param>
		/// <param name="loadAsync">Whether to load scenes asynchronously.</param>
		/// <param name="allowSceneActivation">Whether async scene load may activate immediately.</param>
		/// <param name="timeout">Optional load timeout.</param>
		/// <param name="cancellationToken">Optional cancellation token.</param>
		/// <param name="includeReservedSystemSlots">
		/// When false, quick-save/auto-save reserved ranges are excluded from disk-based slot expansion.
		/// </param>
		/// <returns><see cref="LoadResult"/> describing success/failure.</returns>
		public async Task<LoadResult> LoadByMetadataAsync(
			string metadataKey,
			string metadataValue,
			bool restoreLastActiveScene = true,
			bool loadAsync = false,
			bool allowSceneActivation = true,
			TimeSpan? timeout = null,
			CancellationToken cancellationToken = default,
			bool includeReservedSystemSlots = false)
		{
            using var cancellation = CreateRuntimeLoadOperationCancellationSource(timeout, cancellationToken);
            try
            {
                return await Operations.RunAsync(async context =>
                {
                    await AwaitOperationReady(context);
                    if (string.IsNullOrEmpty(metadataKey)) return new LoadResult(false, "Metadata key cannot be empty.");
                    await EnsureSlotCountCoversDiscoveredLocalDataAsync(includeReservedSystemSlots, context.Token);
                    var slot = GetLatestSaveSlotByMetadata(metadataKey, metadataValue, StringComparison.Ordinal);
                    if (slot == null) return new LoadResult(false, "No slot matched the requested metadata.");
                    return await RunLoadCoreAsync(context, slot.SlotNumber, restoreLastActiveScene, loadAsync, allowSceneActivation);
                }, cancellation.Token);
            }
            catch (OperationCanceledException) { return new LoadResult(false, "Load was cancelled before application."); }
            catch (Exception ex) { return new LoadResult(false, ex.Message); }
        }

		private int DiscoverHighestPrimarySlotFromDisk(
			bool includeReservedSystemSlots,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			int highest = Math.Max(CurrentSaveSlotCount, 0);
			if (string.IsNullOrEmpty(_rootPath) || !Directory.Exists(_rootPath))
				return highest;

			try
			{
				string folderPattern = saveSettings != null ? saveSettings.slotFolderNamePattern : null;
				foreach (string directoryPath in Directory.GetDirectories(_rootPath))
				{
					cancellationToken.ThrowIfCancellationRequested();
					string folderName = Path.GetFileName(directoryPath);
					if (!TryParseSlotNumberFromPattern(folderName, folderPattern, "slot{n}", out int slotNumber))
						continue;
					if (!includeReservedSystemSlots && IsReservedSystemSlot(slotNumber))
						continue;
					if (slotNumber > highest)
						highest = slotNumber;
				}

				string metadataPattern = saveSettings != null ? saveSettings.metadataFileNamePattern : null;
				foreach (string filePath in Directory.GetFiles(_rootPath))
				{
					cancellationToken.ThrowIfCancellationRequested();
					string fileName = Path.GetFileName(filePath);
					if (!TryParseSlotNumberFromPattern(fileName, metadataPattern, "Slot{n}_Meta.bin", out int slotNumber))
						continue;
					if (!includeReservedSystemSlots && IsReservedSystemSlot(slotNumber))
						continue;
					if (slotNumber > highest)
						highest = slotNumber;
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				Logger.Log($"DiscoverHighestPrimarySlotFromDisk: Failed to enumerate local slots: {ex.Message}\nStack Trace: {ex.StackTrace}",
					LogCategory.SaveManager,
					LogLevel.Warning);
			}

			return highest;
		}

		private static bool TryParseSlotNumberFromPattern(
			string candidateName,
			string configuredPattern,
			string fallbackPattern,
			out int slotNumber)
		{
			slotNumber = 0;
			if (string.IsNullOrEmpty(candidateName))
				return false;

			string pattern = configuredPattern;
			if (string.IsNullOrWhiteSpace(pattern) || pattern.IndexOf("{n}", StringComparison.Ordinal) < 0)
				pattern = fallbackPattern;

			int markerIndex = pattern.IndexOf("{n}", StringComparison.Ordinal);
			if (markerIndex < 0)
				return false;

			string prefix = pattern.Substring(0, markerIndex);
			string suffix = pattern.Substring(markerIndex + 3);

			if (!candidateName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				return false;
			if (!candidateName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
				return false;

			int numberLength = candidateName.Length - prefix.Length - suffix.Length;
			if (numberLength <= 0)
				return false;

			string numberText = candidateName.Substring(prefix.Length, numberLength);
			if (!int.TryParse(numberText, out int parsed) || parsed <= 0)
				return false;

			slotNumber = parsed;
			return true;
		}

		private bool IsReservedSystemSlot(int slotNumber)
		{
			if (saveSettings == null || slotNumber <= 0)
				return false;

			if (saveSettings.numberOfQuickSaveSlots > 0)
			{
				int start = saveSettings.quickSaveSlotOffset + 1;
				int end = start + saveSettings.numberOfQuickSaveSlots - 1;
				if (slotNumber >= start && slotNumber <= end)
					return true;
			}

			if (saveSettings.numberOfAutoSaveSlots > 0)
			{
				int start = saveSettings.autoSaveSlotOffset + 1;
				int end = start + saveSettings.numberOfAutoSaveSlots - 1;
				if (slotNumber >= start && slotNumber <= end)
					return true;
			}
			else if (saveSettings.autoSaveSlotNumber > 0 &&
			         slotNumber == saveSettings.autoSaveSlotNumber)
			{
				return true;
			}

			return false;
		}

		public SaveSlot GetLatestSaveSlot()
		{
	                        var slots = SlotManager.ManagedSlots;
	                        if (slots == null || slots.Count == 0)
	                                return null;
                        SaveSlot latest = null;
                        foreach (var slot in slots)
			{
				if (latest == null || slot.LastSaved > latest.LastSaved)
				{
					latest = slot;
				}
			}
			return latest;
		}

		public SaveSettings GetSaveSettings()
		{
			return saveSettings;
		}

		/// <summary>
                /// Checks if there is at least one saved game.
                /// </summary>
                /// <returns>True if any save slot has been saved; otherwise, false.</returns>
                public async Task<bool> HasSaveAsync(bool hasMetaData = true)
                {
                        return await RunOnMainThreadAsync(async () =>
                        {
                                await EnsureCloudFreshnessAsync().ConfigureAwait(true);
                                return await SlotManager.HasSaveAsync(hasMetaData).ConfigureAwait(true);
                        }).ConfigureAwait(true);
                }

                public bool HasSave(bool hasMetaData = true)
                {
                        if (saveSettings != null && saveSettings.enableCloudSave)
                        {
                                Logger.Log(
                                        "HasSave is blocked: Cloud Save is enabled. Use HasSaveAsync to avoid freezing.",
                                        LogLevel.Warning);
                                return false;
                        }
                        if (Operations.IsBusy || !UnityMainThreadDispatcher.IsMainThread || SlotManager == null) return false;
                        return SlotManager.HasSave(hasMetaData);
                }

                /// <summary>
                /// Checks if a specific save slot contains saved metadata and
                /// that the corresponding data is accessible (local or cloud).
                /// </summary>
                /// <param name="slotNumber">The save slot number to check.</param>
                /// <returns>True if the specified save slot has been saved; otherwise, false.</returns>
                public async Task<bool> HasSaveAtAsync(int slotNumber, bool hasMetaData = true)
                {
                        return await RunOnMainThreadAsync(async () =>
                        {
                                await EnsureCloudFreshnessAsync().ConfigureAwait(true);
                                SaveSlot slot = SlotManager.GetByNumber(slotNumber);
                                if (slot == null)
                                {
                                        Logger.Log($"HasSaveAt: Save slot {slotNumber} does not exist.", LogLevel.Warning);
                                        return false;
                                }

                                return await SlotManager.HasSaveAtAsync(slotNumber, hasMetaData).ConfigureAwait(true);
                        }).ConfigureAwait(true);
                }

                public bool HasSaveAt(int slotNumber, bool hasMetaData = true)
                {
                        if (saveSettings != null && saveSettings.enableCloudSave)
                        {
                                Logger.Log(
                                        "HasSaveAt is blocked: Cloud Save is enabled. Use HasSaveAtAsync to avoid freezing.",
                                        LogLevel.Warning);
                                return false;
                        }
                        if (Operations.IsBusy || !UnityMainThreadDispatcher.IsMainThread || SlotManager == null) return false;
                        return SlotManager.HasSaveAt(slotNumber, hasMetaData);
                }

		/// <summary>
                /// Checks if there is at least one saved game after the specified date.
                /// </summary>
                /// <param name="date">The date to compare against.</param>
                /// <returns>True if any save slot has LastSaved > date; otherwise, false.</returns>
                public async Task<bool> HasSaveAfterDateAsync(DateTime date, bool hasMetaData = true)
                {
                        return await RunOnMainThreadAsync(async () =>
                        {
                                await EnsureCloudFreshnessAsync().ConfigureAwait(true);
                                return await SlotManager.HasSaveAfterAsync(date, hasMetaData).ConfigureAwait(true);
                        }).ConfigureAwait(true);
                }

		/// <summary>
		/// Checks if any save slot has data by examining the LastSaved timestamp.
		/// This is a more reliable alternative to HasSave() when cloud save is enabled.
		/// </summary>
		/// <returns>True if any save slot has a LastSaved timestamp greater than DateTime.MinValue; otherwise, false.</returns>
		public bool HasAnySaveInSlots()
		{
			var latestSlot = GetLatestSaveSlot();
			return latestSlot != null && latestSlot.LastSaved > DateTime.MinValue;
		}

		/// <summary>
		/// Asynchronously checks if any save slot has data by examining the LastSaved timestamp.
		/// This is a more reliable alternative to HasSaveAsync() when cloud save is enabled.
		/// </summary>
		/// <returns>True if any save slot has a LastSaved timestamp greater than DateTime.MinValue; otherwise, false.</returns>
		public Task<bool> HasAnySaveInSlotsAsync()
		{
			return RunOnMainThreadAsync(() =>
			{
				var latestSlot = GetLatestSaveSlot();
				bool result = latestSlot != null && latestSlot.LastSaved > DateTime.MinValue;
				return Task.FromResult(result);
			});
		}

		// Ensure we have the most recent cloud state before evaluating presence checks
                private async Task EnsureCloudFreshnessAsync()
                {
                        try
                        {
                                if (saveSettings != null &&
                                    saveSettings.enableCloudSave &&
                                    !saveSettings.keepLocalMirror &&
                                    CloudSaveService != null)
                                {
                                        // Full force-refresh: reinitialize local slots and then merge remote
                                        await ForceRefreshSlotsAsync();
                                }
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"EnsureCloudFreshnessAsync: refresh failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogLevel.Warning);
                        }
                }

                private static Task<T> RunOnMainThreadAsync<T>(Func<Task<T>> func)
                {
                        if (UnityMainThreadDispatcher.IsMainThread)
                                return func();

                        return UnityMainThreadDispatcher.Instance().EnqueueAsync(func);
                }

		/// <summary>
		/// Reinitializes slot metadata from disk and then fetches and merges the latest
		/// remote state from the configured cloud backend. Notifies listeners afterwards.
		/// Always executed on the Unity main thread to safely interact with Unity APIs.
		/// </summary>
        public Task ForceRefreshSlotsAsync() => QueueSlotRefreshAsync(true, CancellationToken.None, null);

        public Task ForceRefreshSlotsAsync(CancellationToken cancellationToken, TimeSpan? timeout = null)
            => QueueSlotRefreshAsync(true, cancellationToken, timeout);

        async Task QueueSlotRefreshAsync(bool readMetadata, CancellationToken token, TimeSpan? timeout)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (timeout.HasValue) ConfigureOperationDeadline(cancellation, timeout.Value);
            await Operations.RunAsync(async context =>
            {
                await AwaitOperationReady(context);
                var current = SlotManager;
                var candidate = current.GetAll().Select(CopySlot).ToList();
                if (readMetadata)
                {
                    for (int i = 0; i < candidate.Count; i++)
                    {
                        var slot = candidate[i];
                        var loaded = await context.WaitAsync(saveSystem.LoadSlotMetadataAsync(slot.SlotNumber));
                        if (loaded != null && loaded.SlotNumber != slot.SlotNumber)
                            throw new InvalidDataException("Slot refresh returned metadata for a different slot.");
                        if (loaded != null && loaded.LastSaved < slot.LastSaved) continue;
                        candidate[i] = loaded == null
                            ? new SaveSlot(slot.SlotNumber, $"Slot {slot.SlotNumber}", DateTime.MinValue, string.Empty, string.Empty)
                            : CopySlot(loaded);
                    }
                }
                var obsolete = new List<SaveSlot>();
                if (saveSettings.enableCloudSave)
                {
                    if (cloudSaveService == null)
                        throw new InvalidOperationException("Cloud slot discovery is not initialized. Configure cloud saving before SaveManager starts.");
                    candidate = await context.WaitAsync(cloudSaveService.PrepareRefreshAsync(candidate, obsolete));
                }
                context.CheckCancellation();
                if (current != SlotManager) throw new InvalidOperationException("Slot manager changed during refresh.");
                context.BeginCommit();
                if (obsolete.Count != 0) await cloudSaveService.DeleteObsoleteLocalMetadataAsync(obsolete);
                // There are no awaits or callbacks while replacing the published collections.
                current.ReplaceSlots(candidate);
                saveSlots.Clear(); saveSlots.AddRange(candidate);
                foreach (var slot in candidate) SyncSlotToInternalList(slot);
                NotifyOperation(() => NotifySaveSlotsUpdated());
            }, cancellation.Token);
        }

                public bool HasSaveAfterDate(DateTime date, bool hasMetaData = true)
                {
                        if (saveSettings != null && saveSettings.enableCloudSave)
                        {
                                Logger.Log(
                                        "HasSaveAfterDate is blocked: Cloud Save is enabled. Use HasSaveAfterDateAsync to avoid freezing.",
                                        LogLevel.Warning);
                                return false;
                        }
                        if (Operations.IsBusy || !UnityMainThreadDispatcher.IsMainThread || SlotManager == null) return false;
                        return SlotManager.HasSaveAfter(date, hasMetaData);
                }

		/// <summary>
		/// Checks if there is at least one saved game in the specified scene.
		/// </summary>
                /// <param name="sceneName">The name of the scene to check.</param>
                /// <returns>True if any save slot has LastActiveScene equal to sceneName; otherwise, false.</returns>
                public async Task<bool> HasSaveInSceneAsync(string sceneName, bool hasMetaData = true)
                {
                        if (string.IsNullOrEmpty(sceneName))
                        {
                                Logger.Log("HasSaveInScene: Provided sceneName is null or empty.", LogLevel.Warning);
                                return false;
                        }

                        return await RunOnMainThreadAsync(() =>
                                SlotManager.HasSaveInSceneAsync(sceneName, hasMetaData)
                        ).ConfigureAwait(true);
                }

                public bool HasSaveInScene(string sceneName, bool hasMetaData = true)
                {
                        if (saveSettings != null && saveSettings.enableCloudSave)
                        {
                                Logger.Log(
                                        "HasSaveInScene is blocked: Cloud Save is enabled. Use HasSaveInSceneAsync to avoid freezing.",
                                        LogLevel.Warning);
                                return false;
                        }
                        if (Operations.IsBusy || !UnityMainThreadDispatcher.IsMainThread || SlotManager == null) return false;
                        return SlotManager.HasSaveInScene(sceneName, hasMetaData);
                }

                /// <summary>
                /// Captures the current game state into <see cref="CurrentSaveData"/> without writing to disk.
                /// Useful before calling restore helpers that rely on in-memory data.
                /// </summary>
                /// <param name="lastActiveScene">Optional name of the active scene to store in the snapshot. If null or empty, the current scene is used.</param>
                /// <returns>True if the snapshot was captured successfully; otherwise, false.</returns>
                public bool SnapshotCurrentData(string lastActiveScene = null)
                {
                    try
                    {
                        bool result = false;
                        Operations.RunSync(context => result = SnapshotCurrentDataCore(lastActiveScene));
                        return result;
                    }
                    catch (Exception ex) { Logger.Log("Snapshot rejected: " + ex.Message, LogLevel.Warning); return false; }
                }

                bool SnapshotCurrentDataCore(string lastActiveScene)
                {
                        try
                        {
                                var data = CollectSaveData(lastActiveScene);
                                if (data == null)
                                {
                                        Logger.Log("SnapshotCurrentData: CollectSaveData returned null.", LogLevel.Warning);
                                        return false;
                                }

                                CurrentSaveData = data;

                                // Refresh destroyed-object tracker to match the new snapshot
                                GameObjectTracker.ReplaceDestroyedIDs(data.DestroyedGameObjects);

                                Logger.Log("SnapshotCurrentData: captured current state in memory.", LogCategory.SaveManager, LogLevel.Info);
                                return true;
                        }
                        catch (Exception ex)
                        {
                                Logger.Log($"SnapshotCurrentData: failed to collect save data: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Error);
                                return false;
                        }
                }

                /// <summary>
                /// Captures the current game state and updates prefab/component managers so pending
                /// prefabs will spawn when their home scenes load.
                /// </summary>
                /// <param name="lastActiveScene">Optional name of the active scene to record in the snapshot.
                /// If null or empty, the current scene is used.</param>
                /// <returns>True if the snapshot was captured successfully; otherwise, false.</returns>
                public async Task<bool> SnapshotAndPopulateAsync(string lastActiveScene = null)
                {
                    try
                    {
                        return await Operations.RunAsync(async context =>
                        {
                            await AwaitOperationReady(context);
                            await PopulatePrefabsCoreAsync(context, lastActiveScene: lastActiveScene);
                            return true;
                        });
                    }
                    catch (Exception ex) { Logger.Log("Snapshot failed: " + ex.Message, LogLevel.Warning); return false; }
                }
                /// <summary>
                /// Initiates the load process by creating and running the LoadStateMachine.
                /// </summary>
                public IEnumerator StartLoadProcess(SaveSlot slot, bool restoreScene, bool asyncLoad, bool allowActivation, CancellationToken token)
                {
                        yield return sceneLoadManager.StartLoadProcess(slot, restoreScene, asyncLoad, allowActivation, token);
                }

                /// <summary>
                /// Retrieves the base UniqueID of a GameObject without any component suffix.
                /// </summary>
                private string GetGameObjectBaseID(GameObject obj)
                {
                        if (obj == null) return null;

                        var rememberGO = obj.GetComponent<RememberGameObject>();
                        if (rememberGO != null && !string.IsNullOrEmpty(rememberGO.GameObjectUniqueID))
                                return rememberGO.GameObjectUniqueID;

                        // Prefer the dedicated UniqueID component. If absent, fall back to a
                        // SceneObjectID and finally to SaveablePrefab identifiers.
                        var uid = obj.GetComponent<UniqueID>()?.ID;
                        if (!string.IsNullOrEmpty(uid)) return uid;

                        var sceneId = obj.GetComponent<SceneObjectID>()?.UniqueID;
                        if (!string.IsNullOrEmpty(sceneId)) return sceneId;

                        var sp = obj.GetComponent<SaveablePrefab>();
                        if (sp != null)
                        {
                                if (!string.IsNullOrEmpty(sp.UniqueID)) return sp.UniqueID;
                                if (!string.IsNullOrEmpty(sp.PrefabAssetID)) return sp.PrefabAssetID;
                        }

                        return null;
                }

                #region Utility Methods for Scene Loading and Prefab Queue Inspection

                /// <summary>
                /// Gets the total number of prefabs currently queued for spawning across all scenes.
                /// Useful for debugging scene loading issues and verifying that prefabs are queued correctly.
                /// </summary>
                /// <returns>Total count of queued prefabs</returns>
                public int GetPendingPrefabCount()
                {
                        if (prefabManager == null)
                                return 0;

                        return prefabManager.GetPendingSceneKeys()
                                .Concat(prefabManager.GetDeferredSceneKeys())
                                .Where(sceneName => sceneName != null)
                                .Distinct(StringComparer.Ordinal)
                                .Count();
                }

                /// <summary>
                /// Gets the names of scenes that have prefabs queued for spawning.
                /// Useful for debugging to see which scenes have pending prefabs waiting to be instantiated.
                /// </summary>
                /// <returns>List of scene names with queued prefabs</returns>
                public List<string> GetScenesWithPendingPrefabs()
                {
                        if (prefabManager == null)
                                return new List<string>();

                        return prefabManager.GetPendingSceneKeys()
                                .Concat(prefabManager.GetDeferredSceneKeys())
                                .Where(sceneName => sceneName != null)
                                .Distinct(StringComparer.Ordinal)
                                .ToList();
                }

                /// <summary>
                /// Validates that the current scene loading workflow follows best practices.
                /// Logs warnings if potential timing issues are detected.
                /// </summary>
                /// <param name="operationName">Name of the operation being validated (for logging)</param>
                /// <param name="targetSceneName">Name of the target scene being loaded</param>
                public void ValidateSceneLoadingTiming(string operationName, string targetSceneName)
                {
                        if (string.IsNullOrEmpty(targetSceneName))
                        {
                                Logger.Log($"[{operationName}] Target scene name is null or empty.", LogLevel.Warning);
                                return;
                        }

                        // Check if target scene is already loaded
                        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
                        bool sceneIsLoaded = targetScene.isLoaded;
                        bool sceneIsActive = SceneManager.GetActiveScene().name == targetSceneName;

                        if (sceneIsActive && !sceneIsLoaded)
                        {
                                Logger.Log(
                                        $"[{operationName}] WARNING: Scene '{targetSceneName}' is set as active but not fully loaded. " +
                                        "This may cause prefabs to spawn in the wrong scene. " +
                                        "Ensure you wait for SceneManager.LoadSceneAsync().isDone before setting the active scene.",
                                        LogLevel.Warning
                                );
                        }

                        // Check if there are pending prefabs for a scene that's about to become active
                        var pendingScenes = GetScenesWithPendingPrefabs();
                        if (pendingScenes.Contains(targetSceneName) && sceneIsActive)
                        {
                                Logger.Log(
                                        $"[{operationName}] INFO: Scene '{targetSceneName}' is active and has {pendingScenes.Count} queued prefabs. " +
                                        "Prefabs will spawn in the next frame.",
                                        LogLevel.Info
                                );
                        }
                }

                /// <summary>
                /// Clears all queued prefabs without spawning them.
                /// WARNING: Use with extreme caution! This will permanently discard queued prefabs.
                /// Only use this when you're certain you want to abandon the queued state.
                /// </summary>
                public void ClearPendingPrefabs()
                {
                        prefabManager?.ClearPendingPrefabs();
                }

                /// <summary>
                /// Gets diagnostic information about the current save/load state.
                /// Useful for debugging scene loading and prefab spawning issues.
                /// </summary>
                /// <returns>Formatted diagnostic string</returns>
                public string GetLoadStateDiagnostics()
                {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine("=== Crystal Save Load State Diagnostics ===");
                        sb.AppendLine($"Current Save Data Loaded: {(CurrentSaveData != null ? "Yes" : "No")}");
                        
                        if (CurrentSaveData != null)
                        {
                                sb.AppendLine($"  - Saved Prefabs: {CurrentSaveData.Prefabs?.Count ?? 0}");
                                sb.AppendLine($"  - Last Active Scene: {CurrentSaveData.LastActiveScene ?? "None"}");
                        }

                        sb.AppendLine($"Active Scene: {SceneManager.GetActiveScene().name}");
                        sb.AppendLine($"Loaded Scenes: {SceneManager.sceneCount}");
                        
                        for (int i = 0; i < SceneManager.sceneCount; i++)
                        {
                                Scene scene = SceneManager.GetSceneAt(i);
                                sb.AppendLine($"  [{i}] {scene.name} (Active: {scene == SceneManager.GetActiveScene()})");
                        }

                        var pendingScenes = GetScenesWithPendingPrefabs();
                        sb.AppendLine($"Scenes with Pending Prefabs: {pendingScenes.Count}");
                        foreach (var sceneName in pendingScenes)
                        {
                                sb.AppendLine($"  - {sceneName}");
                        }

                        return sb.ToString();
                }

                #endregion

                #region Scene Load Orchestrator Registration

                /// <summary>
                /// Registers a custom scene load orchestrator to receive callbacks during the scene loading lifecycle.
                /// Use this to integrate custom loaders (Addressables, asset bundles, etc.) with Crystal Save.
                /// </summary>
                /// <param name="orchestrator">The orchestrator implementation to register</param>
                /// <remarks>
                /// Orchestrators receive callbacks at critical points:
                /// - OnScenePreLoad: Before scene load starts (prime prefab data here)
                /// - OnSceneWillActivate: After load completes, before activation
                /// - OnSceneActivated: After scene becomes active
                /// - OnSceneUnloaded: After scene unloads
                /// 
                /// Example:
                /// <code>
                /// public class MyLoader : SceneLoadOrchestratorBase
                /// {
                ///     public override async Task OnScenePreLoad(string sceneName)
                ///     {
                ///         await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(0);
                ///     }
                /// }
                /// 
                /// // Register
                /// SaveManager.Instance.RegisterSceneLoadOrchestrator(new MyLoader());
                /// </code>
                /// </remarks>
                public void RegisterSceneLoadOrchestrator(ISceneLoadOrchestrator orchestrator)
                {
                        if (orchestrator == null)
                        {
                                Logger.Log("[SCENELOAD] RegisterSceneLoadOrchestrator: orchestrator is null.", LogLevel.Warning);
                                return;
                        }

                        if (!sceneLoadOrchestrators.Contains(orchestrator))
                        {
                                sceneLoadOrchestrators.Add(orchestrator);
                                Logger.Log($"[SCENELOAD] Registered scene load orchestrator: {orchestrator.GetType().Name}", LogLevel.Info);
                        }
                        else
                        {
                                Logger.Log($"[SCENELOAD] Scene load orchestrator already registered: {orchestrator.GetType().Name}", LogLevel.Warning);
                        }
                }

                /// <summary>
                /// Unregisters a previously registered scene load orchestrator.
                /// </summary>
                /// <param name="orchestrator">The orchestrator to unregister</param>
                public void UnregisterSceneLoadOrchestrator(ISceneLoadOrchestrator orchestrator)
                {
                        if (orchestrator == null)
                        {
                                Logger.Log("[SCENELOAD] UnregisterSceneLoadOrchestrator: orchestrator is null.", LogLevel.Warning);
                                return;
                        }

                        if (sceneLoadOrchestrators.Remove(orchestrator))
                        {
                                Logger.Log($"[SCENELOAD] Unregistered scene load orchestrator: {orchestrator.GetType().Name}", LogLevel.Info);
                        }
                }

                /// <summary>
                /// Internal method to invoke OnScenePreLoad on all registered orchestrators.
                /// </summary>
                internal async Task InvokeOrchestratorPreLoad(string sceneName)
                {
                        foreach (var orchestrator in sceneLoadOrchestrators)
                        {
                                try
                                {
                                        await orchestrator.OnScenePreLoad(sceneName);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"[SCENELOAD] Orchestrator {orchestrator.GetType().Name}.OnScenePreLoad failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogLevel.Error);
                                }
                        }
                }

                /// <summary>
                /// Internal method to invoke OnSceneWillActivate on all registered orchestrators.
                /// </summary>
                internal async Task InvokeOrchestratorWillActivate(Scene scene)
                {
                        foreach (var orchestrator in sceneLoadOrchestrators)
                        {
                                try
                                {
                                        await orchestrator.OnSceneWillActivate(scene);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"[SCENELOAD] Orchestrator {orchestrator.GetType().Name}.OnSceneWillActivate failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogLevel.Error);
                                }
                        }
                }

                /// <summary>
                /// Internal method to invoke OnSceneActivated on all registered orchestrators.
                /// </summary>
                internal void InvokeOrchestratorActivated(Scene scene)
                {
                        foreach (var orchestrator in sceneLoadOrchestrators)
                        {
                                try
                                {
                                        orchestrator.OnSceneActivated(scene);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"[SCENELOAD] Orchestrator {orchestrator.GetType().Name}.OnSceneActivated failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogLevel.Error);
                                }
                        }
                }

                /// <summary>
                /// Internal method to invoke OnSceneUnloaded on all registered orchestrators.
                /// </summary>
                internal void InvokeOrchestratorUnloaded(Scene scene)
                {
                        foreach (var orchestrator in sceneLoadOrchestrators)
                        {
                                try
                                {
                                        orchestrator.OnSceneUnloaded(scene);
                                }
                                catch (Exception ex)
                                {
                                        Logger.Log($"[SCENELOAD] Orchestrator {orchestrator.GetType().Name}.OnSceneUnloaded failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogLevel.Error);
                                }
                        }
                }

                #endregion

                #endregion
    }
}
#endif
