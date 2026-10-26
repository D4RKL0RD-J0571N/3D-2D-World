#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace Arawn.CrystalSave.Runtime
{
    public partial class SaveManager
    {
        SaveOperationCoordinator operationCoordinator;
        internal SaveOperationCoordinator Operations => operationCoordinator ??= new SaveOperationCoordinator(this);
        internal async Task AwaitOperationReady(SaveOperationContext context)
        {
            RequireAvailableOperationHost();
            while (!IsInitialized || !AreSaveSlotsReady || SaveOperations == null)
            {
                context.CheckCancellation();
                RequireAvailableOperationHost();
                await Task.Yield();
            }
            // Supabase saves can already be waiting for a login. Let their first
            // provider call capture the authenticated account after that wait.
            bool awaitingSupabaseLogin = saveSettings.enableCloudSave && saveSettings.backend == SaveBackend.Supabase &&
                saveSettings.userFolderStrategy == UserFolderStrategy.Custom && !IsSupabaseCustomLoggedIn;
            if (!awaitingSupabaseLogin && saveSystem is ICloudRequestIdentitySource identitySource)
                await identitySource.CaptureRequestIdentityAsync();
        }
        void RequireAvailableOperationHost()
        {
            if (!this || IsShuttingDown || !isActiveAndEnabled)
                throw new InvalidOperationException("SaveManager is unavailable.");
        }
        internal static SaveSlot CopySlot(SaveSlot source)
        {
            if (source == null) throw new ArgumentException("Save slot does not exist.");
            var copy = new SaveSlot(source.SlotNumber, source.SlotName, source.LastSaved, source.ScreenshotFileName, source.LastActiveScene);
            copy.CustomMetadata = source.CustomMetadata == null ? new Dictionary<string, string>() : new Dictionary<string, string>(source.CustomMetadata);
            return copy;
        }
        void SaveSyncCore(SaveOperationContext context, int number, string scene, bool screenshot)
        {
            RequireAvailableOperationHost();
            if (!IsInitialized || !AreSaveSlotsReady || SaveOperations == null) throw new InvalidOperationException("SaveManager is not ready.");
            if (saveSettings.enableCloudSave) throw new InvalidOperationException("Cloud storage requires SaveAsync.");
            if (!string.IsNullOrEmpty(scene) && !IsSceneInBuild(scene)) throw new ArgumentException($"Scene '{scene}' is not in build settings.");
            var slot = CopySlot(GetSaveSlot(number));
            var previous = CurrentSaveData;
            try
            {
                SaveData data = SaveOperations.CaptureCandidate(scene);
                slot.LastSaved = data.LastSaved;
                slot.LastActiveScene = data.LastActiveScene;
                ApplyDefaultSlotMetadata(slot);
                (saveSystem as ISaveWritePathValidator)?.ValidateWritePaths(slot);
                if (screenshot && saveSettings.enableScreenshots) slot.ScreenshotFileName = SaveOperations.CaptureScreenshotSync(number);
                data.ScreenshotFileName = slot.ScreenshotFileName;
                var bytes = SaveOperations.MaybeEncrypt(SaveOperations.MaybeCompress(serializer.Serialize(data)));
                SaveOperations.CommitPayloadSync(context, bytes, slot);
                CompleteSave(context, slot, true);
            }
            catch { if (!context.PayloadCommitted) CurrentSaveData = previous; throw; }
        }
        internal Task QueueSaveRequest(int number, string scene, string name, bool immediate, bool automaticName, CancellationToken token)
        {
            var invoker = UnityMainThreadDispatcher.IsMainThread ? GetInvokingSelectable() : null;
            if (invoker != null) invoker.interactable = false;
            var task = Operations.RunAsync(async context =>
            {
                await AwaitOperationReady(context);
                await SaveOperations.RunSaveAsync(context, number, scene, name, immediate, automaticName);
            }, token);
            if (invoker != null) RestoreInvokerWhenComplete(task, invoker);
            return task;
        }
        static async void RestoreInvokerWhenComplete(Task task, UnityEngine.UI.Selectable invoker)
        {
            try { await task; } catch (Exception) { /* The public operation retains its outcome. */ }
            finally { if (invoker) invoker.interactable = true; }
        }
        static void ConfigureOperationDeadline(CancellationTokenSource source, TimeSpan timeout)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (timeout == Timeout.InfiniteTimeSpan) return;
            if (timeout < TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            var deadline = DateTime.UtcNow + timeout;
            UnityMainThreadDispatcher.Instance().Enqueue(CancelAtDeadline());
            System.Collections.IEnumerator CancelAtDeadline()
            {
                while (true)
                {
                    // The owner disposes the source when it completes before the deadline.
                    try
                    {
                        if (source.Token.IsCancellationRequested) yield break;
                        if (DateTime.UtcNow >= deadline) { source.Cancel(); yield break; }
                    }
                    catch (ObjectDisposedException) { yield break; }
                    yield return null;
                }
            }
#else
            source.CancelAfter(timeout);
#endif
        }
        internal void BeginOperationCommit() => Operations.Active?.BeginCommit();
        internal void CheckOperationCancellation() => Operations.Active?.CheckCancellation();
        internal Task AwaitOperationTask(Task task) => Operations.Active?.WaitAsync(task) ?? task;
        internal Task<T> AwaitOperationTask<T>(Task<T> task) => Operations.Active?.WaitAsync(task) ?? task;
        internal void PublishDeletedSlot(SaveSlot slot)
        {
            SyncSlotToInternalList(slot);
            if (operationCoordinator?.Active == null) NotifySaveSlotsUpdated();
        }
        /// <summary>Releases the current asynchronous scene activation hold, including UnityEvent calls.</summary>
        public void ActivatePendingScene() => sceneLoadManager?.ActivatePendingScene();

        internal async Task<LoadResult> QueueLoadRequest(int number, bool restoreScene, bool asyncScene,
            bool allowActivation, TimeSpan? timeout, CancellationToken token)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (timeout.HasValue) ConfigureOperationDeadline(cancellation, timeout.Value);
            try
            {
                return await Operations.RunAsync(async context =>
                {
                    return await RunLoadCoreAsync(context, number, restoreScene, asyncScene, allowActivation);
                }, cancellation.Token);
            }
            catch (OperationCanceledException) { return new LoadResult(false, "Load was cancelled before application."); }
            catch (Exception ex) { return new LoadResult(false, ex.Message); }
        }

        internal async Task<LoadResult> RunLoadCoreAsync(SaveOperationContext context, int number, bool restoreScene, bool asyncScene, bool allowActivation)
        {
                    await AwaitOperationReady(context);
                    var slot = GetSaveSlot(number);
                    if (slot == null) throw new ArgumentException($"Save slot {number} does not exist.");
                    isLoading = true;
                    TryAcquireLoadLock(number);
                    RaiseLoadStarted(slot);
                    try
                    {
                        await Operations.RunCoroutine(sceneLoadManager.RunLoadCoroutine(context, slot, restoreScene, asyncScene, allowActivation, context.Token), context);
                        if (!context.LoadReported) CompleteLoad(context, number, false, "Load ended without a result.");
                        return new LoadResult(context.LoadError == null, context.LoadError);
                    }
                    catch (Exception ex)
                    {
                        if (context.Committed && ex is OperationCanceledException)
                        {
                            CompleteLoad(context, number, false, "Load was interrupted after application started.");
                            throw new InvalidOperationException("Load was interrupted after application started.", ex);
                        }
                        if (!(ex is OperationCanceledException)) CompleteLoad(context, number, false, ex.Message);
                        throw;
                    }
                    finally
                    {
                        isLoading = false;
                        ReleaseLoadLock(number);
                        StateMachine.TransitionTo(SaveState.Idle);
                    }
        }

        internal async Task<SaveData> PrepareSlotAsync(SaveOperationContext context, SaveSlot slot, bool allowMissing = false)
        {
            if (saveSettings.enableCloudSave && !await context.WaitAsync(WaitForCloudConnectionAsync()))
                throw new InvalidOperationException("Unable to load: connection to cloud provider not established.");
            context.CheckCancellation();
            byte[] bytes = await context.WaitAsync(SaveSystem.LoadAsync(slot));
            if (bytes == null && allowMissing) return null;
            if (bytes == null || bytes.Length == 0) throw new InvalidOperationException("No data returned for the requested slot.");
            bytes = await context.WaitAsync(SaveOperations.DecryptFromCloudAsync(bytes));
            if (bytes == null) throw new InvalidOperationException("Save authentication failed.");
#if UNITY_WEBGL && !UNITY_EDITOR
            await Task.Yield(); context.CheckCancellation();
            return Serializer.DeserializeSaveDataExact(SaveOperations.MaybeDecompress(bytes));
#else
            return await context.WaitAsync(Task.Run(() =>
            {
                context.CheckCancellation();
                var decoded = SaveOperations.MaybeDecompress(bytes);
                context.CheckCancellation();
                return Serializer.DeserializeSaveDataExact(decoded);
            }));
#endif
        }

        // Detached data only: neither migration nor normalization publishes manager state.
        internal bool ValidateCandidate(SaveData data)
        {
            if (data?.Version == null) throw new InvalidOperationException("Missing save data or version.");
            var comparison = VersionManager.CompareVersion(data.Version);
            HandleVersionResult(comparison, data);
            data.ComponentsData ??= new Dictionary<string, byte[]>();
            data.ComponentMetadata ??= new Dictionary<string, ComponentDataMetadata>();
            data.Prefabs ??= new List<SaveablePrefabData>();
            data.SaveableComponents ??= new List<SaveDataEntry>();
            data.GameObjectStates ??= new List<GameObjectState>();
            data.DestroyedGameObjects ??= new List<string>();
            data.TrackedUniqueIDs ??= new List<string>();
            data.DestroyedObjectData ??= new Dictionary<string, Dictionary<string, byte[]>>();
            data.HomeSceneComponentSnapshots ??= new Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>>();
            data.HomeScenePrefabAssetIDs ??= new Dictionary<string, Dictionary<string, string>>();
            foreach (var prefab in data.Prefabs)
                if (prefab == null || string.IsNullOrEmpty(prefab.InstanceID)) throw new InvalidOperationException("Invalid prefab identity in save data.");
            if (data.SaveableComponents.Any(entry => entry == null) || data.GameObjectStates.Any(entry => entry == null))
                throw new InvalidOperationException("Null save entry.");
            if (data.HomeSceneComponentSnapshots.Values.Any(scene => scene == null || scene.Values.Any(value => value == null)) ||
                data.HomeScenePrefabAssetIDs.Values.Any(scene => scene == null) || data.DestroyedObjectData.Values.Any(value => value == null))
                throw new InvalidOperationException("Invalid nested snapshot structure.");
            return comparison == VersionComparisonResult.Older;
        }
        internal void CleanupCommittedScreenshot(string previous, string current)
        {
            if (!ScreenshotManager.IsValidScreenshotFileName(previous) || previous == current || GetSaveSlots().Any(slot => slot.ScreenshotFileName == previous)) return;
            screenshotManager.DeleteScreenshot(previous);
        }
        internal async void ObserveOperation(Task task)
        { try { await task; } catch (OperationCanceledException) { } catch (Exception ex) { Logger.Log(ex.Message, LogLevel.Error); } }
        internal void NotifyOperation(Action notification) => NotifyOperation(Operations.Active, notification);

        internal static void NotifyOperation(SaveOperationContext context, Action notification)
        {
            if (context != null) context.Notifications.Add(notification);
            else if (UnityMainThreadDispatcher.IsMainThread) notification();
            else UnityMainThreadDispatcher.Instance().Enqueue(notification);
        }
    }
}
#endif
