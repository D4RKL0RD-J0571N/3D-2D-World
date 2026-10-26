#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    internal sealed class PreparedSaveWrite
    {
        internal readonly byte[] Payload;
        internal readonly SaveSlot Slot;
        internal readonly Action Publish;
        internal PreparedSaveWrite(byte[] payload, SaveSlot slot, Action publish = null)
        { Payload = payload; Slot = slot; Publish = publish; }
    }

    public partial class SaveOperationService
    {
        internal SaveData CaptureCandidate(string scene)
        {
            SaveableComponent.BeginSaveCallDiagnostics("Save capture");
            try
            {
                var data = manager.CollectSaveData(scene);
                var diagnostics = SaveableComponent.CompleteSaveCallDiagnostics();
                if (diagnostics.HasDuplicates) SaveableComponent.ReportSaveCallDiagnostics("Save capture", diagnostics);
                data.TrackedUniqueIDs = manager.GameObjectTracker.GetTrackedIDSnapshot();
                manager.ValidateCandidate(data);
                return data;
            }
            catch (Exception ex)
            {
                SaveableComponent.AbortSaveCallDiagnosticsWithReason(ex.Message);
                throw;
            }
        }

        async Task CaptureScreenshotAsync(SaveOperationContext context, SaveSlot slot)
        {
            manager.RaiseScreenshotCaptureStarted();
            var capture = manager.Operations.RunCoroutine(screenshotManager.CaptureScreenshot(slot.SlotNumber,
                name => slot.ScreenshotFileName = name), context, out var routine);
            try
            {
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!capture.IsCompleted && Time.realtimeSinceStartup < deadline) await Task.Yield();
                if (capture.IsCompleted) await capture;
                else Logger.Log("Screenshot capture timed out; retaining the previous screenshot.", LogLevel.Warning);
            }
            finally
            {
                manager.Operations.StopRoutine(routine);
                manager.RaiseScreenshotCaptureFinished();
            }
        }

        async Task SaveCandidateCoreAsync(SaveOperationContext context, SaveSlot slot, string scene, bool captureScreenshot,
            Func<string, bool> filter = null, bool includeGlobalState = true)
        {
            if (slot == null) throw new ArgumentException("Save slot does not exist.");
            if (!string.IsNullOrEmpty(scene) && !manager.IsSceneInBuild(scene))
                throw new ArgumentException($"Scene '{scene}' is not in build settings.");
            if (manager.SaveSettings.enableCloudSave && !await context.WaitAsync(manager.WaitForCloudConnectionAsync()))
                throw new InvalidOperationException("Unable to save: connection to cloud provider not established.");
            var data = await context.WaitAsync(ResolveSlotDataAsync(context, slot));
            bool selectedConflict = data != null;
            bool suppressed = SuppressNextScreenshot;
            SuppressNextScreenshot = false;
            if (!selectedConflict)
            {
                manager.EnsureCacheConsistency();
                context.CheckCancellation();
                data = CaptureCandidate(scene);
                if (captureScreenshot && manager.SaveSettings.enableScreenshots && !suppressed)
                    await CaptureScreenshotAsync(context, slot);
                slot.LastSaved = data.LastSaved;
                slot.LastActiveScene = data.LastActiveScene;
            }
            manager.ApplyDefaultSlotMetadata(slot, overwrite: !selectedConflict);
            foreach (var entry in context.MetadataOverrides)
                slot.CustomMetadata[entry.Key] = entry.Value;
            data.ScreenshotFileName = slot.ScreenshotFileName;
            if (filter != null) data = BuildFilteredCopy(data, filter, includeGlobalState);
            manager.ValidateCandidate(data);
            byte[] plain;
#if UNITY_WEBGL && !UNITY_EDITOR
            context.CheckCancellation();
            plain = serializer.Serialize(data);
#else
            plain = await context.WaitAsync(Task.Run(() => serializer.Serialize(data)));
#endif
            if (plain == null || plain.Length == 0) throw new InvalidDataException("Save serialization failed.");
            var payload = await context.WaitAsync(EncryptForSaveAsync(MaybeCompress(plain)));
            if (payload == null || payload.Length == 0) throw new InvalidDataException("Save encryption failed.");
            try
            {
                if (context.PrepareHistory != null)
                {
                    var writes = await context.PrepareHistory();
                    writes.Add(new PreparedSaveWrite(payload, slot));
                    await CommitBatchAsync(context, writes);
                }
                else await CommitPayloadAsync(context, payload, slot);
            }
            catch (Exception commitError)
            {
                if (context.PayloadCommitted)
                {
                    try { manager.PublishCommittedSlot(slot); }
                    catch (Exception cleanupError)
                    {
                        throw new AggregateException("Save committed, but local cleanup failed. Remote writes were not rolled back.", commitError, cleanupError);
                    }
                }
                throw;
            }
            manager.PublishCommittedSlot(slot);
            await FlushCommittedCleanupAsync();
            manager.CompleteSave(context, slot, true);

            if (!selectedConflict && manager.SyncManager != null && manager.SaveSettings.enableSync)
            {
                try
                {
                    if (manager.SaveSettings.syncSettings?.enableDiffs == true)
                        await manager.SyncManager.SendDiffFromSnapshotAsync(plain, slot.SlotNumber.ToString());
                    else await manager.SyncManager.SendSnapshotAsync(plain, slot.SlotNumber.ToString());
                }
                catch (Exception ex) { Logger.Log($"SyncManager send failed after save commit: {ex.Message}", LogLevel.Warning); }
            }
        }

        // Providers may intentionally ignore a requested mirror (REST + PlayerPrefs,
        // or the Unity backend with server-side crypto). Verify the storage they
        // actually write; absent mirrors use temporary validation before upload.
        bool VerifyLocal => manager.SaveSystem is ISaveVerificationOwner storage
            ? storage.WritesLocalSaves
            : !manager.SaveSettings.enableCloudSave || manager.SaveSettings.keepLocalMirror;

        internal async Task CommitPayloadAsync(SaveOperationContext context, byte[] payload, SaveSlot slot, Action beforeWrite = null)
        {
            (manager.SaveSystem as ISaveWritePathValidator)?.ValidateWritePaths(slot);
            bool verify = manager.SaveSettings.enableSaveFileVerification;
            // Capture before a backend receives the mutable buffer. A valid old save
            // (including one with identical metadata) is not the requested generation.
            byte[] expectedDigest = verify ? CaptureVerificationDigest(payload) : null;
            bool local = VerifyLocal;
            bool temporary = verify && !local;
            bool backedUp = false;
            bool payloadCommitted = false;
            Exception operationError = null;
            try
            {
                if (temporary)
                {
                    context.CheckCancellation();
                    // A staging write is a mutation: cancellation must wait for it to
                    // finish before cleanup, otherwise a late writer can recreate it.
                    await manager.SaveSystem.WriteTempAsync(payload, slot);
                    context.CheckCancellation();
                    if (!await context.WaitAsync(ValidateTempSlotAsync(slot, expectedDigest)))
                        throw new InvalidDataException("Temporary save verification failed.");
                }
                if (verify && local)
                {
                    PrepareBackup(slot);
                    backedUp = true;
                }
                context.BeginCommit();
                await PublishCommitSideEffectsAsync(context);
                beforeWrite?.Invoke();
                await manager.SaveSystem.SaveAsync(payload, slot);
                if (verify && local)
                {
                    if (!await ValidateSavedSlotAsync(slot, expectedDigest)) ReportVerificationFailure(slot);
                    if (manager.SaveSystem is ISaveVerificationOwner owner) await owner.Verification.CompleteAsync(slot.SlotNumber);
                    else manager.SaveSystem.DeleteBackup(slot);
                }
                backedUp = false;
                payloadCommitted = true;
                MarkPayloadCommitted(context);
            }
            catch (Exception commitError)
            {
                operationError = commitError;
                if (backedUp)
                {
                    try
                    {
                        if (manager.SaveSystem is ISaveVerificationOwner owner) await owner.Verification.RestoreAsync(slot.SlotNumber);
                        else manager.SaveSystem.RestoreBackup(slot);
                    }
                    catch (Exception rollbackError)
                    {
                        operationError = new AggregateException("Save failed and local rollback could not be completed.", commitError, rollbackError);
                        throw operationError;
                    }
                }
                throw;
            }
            finally
            {
                if (temporary)
                    await CleanupTemporarySlotsAsync(new[] { slot }, operationError,
                        payloadCommitted);
            }
        }

        static void MarkPayloadCommitted(SaveOperationContext context)
        {
            if (context == null) return;
            context.PayloadCommitted = true;
            context.Rollback.Clear();
            context.AsyncRollback.Clear();
        }

        internal async Task FlushCommittedCleanupAsync()
        {
            try { await CloudMirrorFiles.FlushAsync(); }
            catch (Exception ex)
            {
                throw new IOException("Save committed, but local cleanup persistence failed. Remote writes were not rolled back.", ex);
            }
        }

        async Task CleanupTemporarySlotsAsync(IEnumerable<SaveSlot> slots, Exception operationError,
            bool payloadCommitted)
        {
            var errors = new List<Exception>();
            var previousScope = SaveIoScope.Current.Value;
            SaveIoScope.Current.Value = null;
            try
            {
                // Cleanup belongs to this request even after its token has expired.
                using var cancellation = SaveIoScope.WithReadCancellation(System.Threading.CancellationToken.None);
                foreach (var slot in slots)
                {
                    try { manager.SaveSystem.DeleteTemp(slot); }
                    catch (Exception ex) { errors.Add(ex); }
                }
                try { await CloudMirrorFiles.FlushAsync(); }
                catch (Exception ex) { errors.Add(ex); }
            }
            finally { SaveIoScope.Current.Value = previousScope; }
            if (errors.Count == 0) return;
            if (operationError != null) errors.Insert(0, operationError);
            throw new AggregateException(payloadCommitted
                ? "Save committed, but temporary verification cleanup failed. Remote writes were not rolled back."
                : "Save failed and temporary verification cleanup failed; remote requests may have partially completed.", errors);
        }

        static async Task PublishCommitSideEffectsAsync(SaveOperationContext context)
        {
            var effects = context?.CommitSideEffects;
            if (effects == null) return;
            foreach (var publish in effects) await publish();
            effects.Clear();
        }

        internal async Task CommitBatchAsync(SaveOperationContext context, List<PreparedSaveWrite> writes)
        {
            // Validate every destination before any entry can stage files or publish
            // a screenshot; a later invalid route must not partially commit history.
            foreach (var write in writes)
                (manager.SaveSystem as ISaveWritePathValidator)?.ValidateWritePaths(write.Slot);
            if (writes.Count == 1)
            {
                await CommitPayloadAsync(context, writes[0].Payload, writes[0].Slot);
                writes[0].Publish?.Invoke();
                return;
            }
            bool verify = manager.SaveSettings.enableSaveFileVerification;
            var expectedDigests = verify ? writes.Select(write => CaptureVerificationDigest(write.Payload)).ToArray() : null;
            bool local = VerifyLocal;
            var backedUp = new List<SaveSlot>();
            var temporary = new List<SaveSlot>();
            bool payloadCommitted = false;
            Exception operationError = null;
            try
            {
                // Read/encode the entire incoming generation before any history writes.
                for (int index = 0; index < writes.Count; index++)
                {
                    var write = writes[index];
                    context.CheckCancellation();
                    if (local)
                    {
                        PrepareBackup(write.Slot, requiredForRollback: true);
                        backedUp.Add(write.Slot);
                    }
                    else if (verify)
                    {
                        temporary.Add(write.Slot);
                        await manager.SaveSystem.WriteTempAsync(write.Payload, write.Slot);
                        context.CheckCancellation();
                        if (!await context.WaitAsync(ValidateTempSlotAsync(write.Slot, expectedDigests[index])))
                            throw new InvalidDataException("Rotating save temporary verification failed.");
                    }
                }
                context.BeginCommit();
                await PublishCommitSideEffectsAsync(context);
                for (int index = 0; index < writes.Count; index++)
                {
                    var write = writes[index];
                    await manager.SaveSystem.SaveAsync(write.Payload, write.Slot);
                    if (verify && local && !await ValidateSavedSlotAsync(write.Slot, expectedDigests[index])) ReportVerificationFailure(write.Slot);
                }
                if (local)
                {
                    if (manager.SaveSystem is ISaveVerificationOwner owner)
                        await owner.Verification.CompleteBatchAsync(backedUp.Select(slot => slot.SlotNumber));
                    else foreach (var slot in backedUp) manager.SaveSystem.DeleteBackup(slot);
                }
                backedUp.Clear();
                payloadCommitted = true;
                MarkPayloadCommitted(context);
                var publicationErrors = new List<Exception>();
                foreach (var write in writes)
                {
                    try { write.Publish?.Invoke(); }
                    catch (Exception ex) { publicationErrors.Add(ex); }
                }
                if (publicationErrors.Count != 0)
                    throw new AggregateException("Save committed, but history cleanup failed. Remote writes were not rolled back.", publicationErrors);
            }
            catch (Exception commitError)
            {
                operationError = commitError;
                var errors = new List<Exception> { commitError };
                foreach (var slot in backedUp.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (manager.SaveSystem is ISaveVerificationOwner owner) await owner.Verification.RestoreAsync(slot.SlotNumber);
                        else manager.SaveSystem.RestoreBackup(slot);
                    }
                    catch (Exception rollbackError) { errors.Add(rollbackError); }
                }
                if (errors.Count > 1)
                {
                    operationError = new AggregateException("Rotating save failed and local rollback was incomplete.", errors);
                    throw operationError;
                }
                if (manager.SaveSettings.enableCloudSave && context.Committed)
                {
                    operationError = new IOException("Rotating cloud save failed; remote requests may have partially committed. Local rollback does not undo remote writes.", commitError);
                    throw operationError;
                }
                throw;
            }
            finally
            {
                if (temporary.Count != 0)
                    await CleanupTemporarySlotsAsync(temporary, operationError,
                        payloadCommitted);
            }
        }

        internal void CommitPayloadSync(SaveOperationContext context, byte[] payload, SaveSlot slot)
        {
            (manager.SaveSystem as ISaveWritePathValidator)?.ValidateWritePaths(slot);
            bool verify = manager.SaveSettings.enableSaveFileVerification;
            byte[] expectedDigest = verify ? CaptureVerificationDigest(payload) : null;
            bool backedUp = false;
            try
            {
                if (verify) { PrepareBackup(slot); backedUp = true; }
                context.BeginCommit();
                manager.SaveSystem.Save(payload, slot);
                CompleteVerification(slot, verify, expectedDigest);
                backedUp = false;
                MarkPayloadCommitted(context);
            }
            catch (Exception commitError)
            {
                if (backedUp)
                {
                    try { manager.SaveSystem.RestoreBackup(slot); }
                    catch (Exception rollbackError)
                    {
                        throw new AggregateException("Save failed and local rollback could not be completed.", commitError, rollbackError);
                    }
                }
                throw;
            }
        }

        void PrepareBackup(SaveSlot slot, bool requiredForRollback = false)
        {
            try
            {
                if (requiredForRollback && manager.SaveSystem is ISaveVerificationOwner owner) owner.PrepareRollback(slot);
                else manager.SaveSystem.BackupSlot(slot);
            }
            catch (Exception ex)
            {
                manager.InvokeBackupFailed(slot, "Backup", ex.Message);
                throw new IOException("Save backup failed: " + ex.Message, ex);
            }
        }

        void CompleteVerification(SaveSlot slot, bool verify, byte[] expectedDigest)
        {
            if (!verify) return;
            RequireValidSavedSlot(slot, expectedDigest);
            manager.SaveSystem.DeleteBackup(slot);
        }

        void RequireValidSavedSlot(SaveSlot slot, byte[] expectedDigest)
        {
            if (!ValidateSavedSlot(slot, expectedDigest)) ReportVerificationFailure(slot);
        }

        void ReportVerificationFailure(SaveSlot slot)
        {
            manager.InvokeVerificationFailed(slot, "Verification", "Saved payload or metadata failed verification; rolling back.");
            throw new InvalidDataException("Save verification failed.");
        }
    }
}
#endif
