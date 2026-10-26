#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
#if REMEMBERME_CLOUDSAVE_PRESENT
using Unity.Services.CloudSave;
using UnityCloudSaveService = Unity.Services.CloudSave.CloudSaveService;
using PlayerDeleteOptions = Unity.Services.CloudSave.Models.Data.Player.DeleteOptions;
#endif

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Handles save operations and related helpers like compression,
    /// encryption and screenshot management.
    /// </summary>
    public partial class SaveOperationService
    {
        readonly SaveManager      manager;
        readonly ScreenshotManager screenshotManager;
        readonly SaveDataSerializer serializer;
        // Slot metadata refresh replaces SaveManager.SlotManager. Resolve the current
        // manager so dynamic quick/auto slots remain visible after a refresh.
        readonly SaveSlotManager initialSlotManager;
        SaveSlotManager slotManager => manager?.SlotManager ?? initialSlotManager;
        readonly LiveConflictResolver conflictResolver;

        /// <summary>
        /// When true, the next save operation will skip screenshot capture.
        /// Automatically reset to false after it is consumed.
        /// </summary>
        internal bool SuppressNextScreenshot { get; set; }

        public SaveOperationService(SaveManager manager,
                                    ScreenshotManager screenshotManager,
                                    SaveDataSerializer serializer,
                                    SaveSlotManager slotManager,
                                    LiveConflictResolver conflictResolver)
        {
            this.manager          = manager;
            this.screenshotManager = screenshotManager;
            this.serializer       = serializer;
            this.initialSlotManager = slotManager;
            this.conflictResolver = conflictResolver;
        }

        /* ================================================================== */
        #region Helper Methods
        /* ================================================================== */
        internal byte[] MaybeEncrypt(byte[] plain)
            => manager.EncryptionService != null ? manager.EncryptionService.MaybeEncrypt(plain) : manager.SaveSettings.enableEncryption ? throw new InvalidOperationException("Encryption service is unavailable.") : plain;

        internal byte[] MaybeDecrypt(byte[] blob)
            => manager.EncryptionService != null ? manager.EncryptionService.MaybeDecrypt(blob) : SaveCrypto.HasEnvelope(blob) ? null : blob;

        internal byte[] MaybeCompress(byte[] plain)
            => manager.CompressionService?.MaybeCompress(plain) ?? plain;

        internal byte[] MaybeDecompress(byte[] blob)
            => manager.CompressionService?.MaybeDecompress(blob) ?? blob;

        private bool UseServerSideCloudCrypto =>
            manager.SaveSettings.enableEncryption &&
            manager.SaveSettings.enableCloudSave &&
            manager.SaveSettings.cloudCryptoMode == CloudCryptoMode.ServerSide;

        private ICloudCryptoProvider CloudCryptoProvider =>
            manager.SaveSettings.cloudCryptoProvider as ICloudCryptoProvider;

        internal async Task<byte[]> EncryptForSaveAsync(byte[] plain)
        {
            if (!UseServerSideCloudCrypto)
                return MaybeEncrypt(plain);

            var provider = CloudCryptoProvider;
            if (provider == null)
            {
                Logger.Log("Server-side cloud crypto enabled but no Cloud Crypto Provider assigned.",
                           LogCategory.Cryptography, LogLevel.Error);
                return null;
            }

            return await provider.EncryptForCloudAsync(plain);
        }

        internal async Task<byte[]> DecryptFromCloudAsync(byte[] blob)
        {
            if (!UseServerSideCloudCrypto)
                return MaybeDecrypt(blob);

            var provider = CloudCryptoProvider;
            if (provider == null)
            {
                Logger.Log("Server-side cloud crypto enabled but no Cloud Crypto Provider assigned.",
                           LogCategory.Cryptography, LogLevel.Error);
                return null;
            }

            return await provider.DecryptFromCloudAsync(blob);
        }

        internal static byte[] CaptureVerificationDigest(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidDataException("Save payload is empty.");
            using var hash = System.Security.Cryptography.SHA256.Create();
            return hash.ComputeHash(payload);
        }

        static bool MatchesVerificationDigest(byte[] payload, byte[] expectedDigest)
            => payload != null && payload.Length > 0 && expectedDigest != null &&
                CaptureVerificationDigest(payload).SequenceEqual(expectedDigest);

        internal bool ValidateSavedSlot(SaveSlot slot, byte[] expectedDigest)
        {
            try
            {
                // Cloud saves use the async path, including provider-owned encryption.
                if (UseServerSideCloudCrypto) return false;
                var bytes = ReadVerificationPayload(slot);
                return MatchesVerificationDigest(bytes, expectedDigest) && ValidateCommittedData(slot, MaybeDecrypt(bytes));
            }
            catch
            {
                return false;
            }
        }

        byte[] ReadVerificationPayload(SaveSlot slot) => manager.SaveSystem is ISaveVerificationOwner owner
            ? owner.ReadVerificationPayload(slot) : manager.SaveSystem.Load(slot);

        internal async Task<bool> ValidateSavedSlotAsync(SaveSlot slot, byte[] expectedDigest)
        {
            try
            {
                var bytes = ReadVerificationPayload(slot);
                if (!MatchesVerificationDigest(bytes, expectedDigest)) return false;
                bytes = await DecryptFromCloudAsync(bytes);
                return ValidateCommittedData(slot, bytes);
            }
            catch { return false; }
        }

        bool ValidateCommittedData(SaveSlot slot, byte[] plain)
        {
            if (plain == null || plain.Length == 0) return false;
            var data = serializer.DeserializeSaveDataExact(MaybeDecompress(plain));
            if (data == null) return false;
            if (manager.SaveSystem is ISaveVerificationOwner storage)
            {
                var metadata = ConflictCandidateReader.Metadata(storage.ReadVerificationMetadata(slot));
                if (metadata == null || metadata.SlotNumber != slot.SlotNumber || metadata.SlotName != slot.SlotName ||
                    metadata.LastSaved != slot.LastSaved || metadata.LastActiveScene != slot.LastActiveScene ||
                    metadata.ScreenshotFileName != slot.ScreenshotFileName) return false;
                var expected = slot.CustomMetadata ?? new Dictionary<string, string>();
                var actual = metadata.CustomMetadata ?? new Dictionary<string, string>();
                if (actual.Count != expected.Count || expected.Any(pair => !actual.TryGetValue(pair.Key, out var value) || value != pair.Value)) return false;
            }
            return true;
        }

        internal async Task<bool> ValidateTempSlotAsync(SaveSlot slot, byte[] expectedDigest)
        {
            try
            {
                byte[] bytes = await manager.SaveSystem.LoadTempAsync(slot);
                if (!MatchesVerificationDigest(bytes, expectedDigest))
                    return false;

                bytes = await DecryptFromCloudAsync(bytes);
                bytes = MaybeDecompress(bytes);
                var data = serializer.DeserializeSaveDataExact(bytes);
                return data != null;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        /* ================================================================== */
        #region Conflict Resolution
        /* ================================================================== */

        internal async Task<SaveData> ResolveSlotDataAsync(SaveOperationContext context, SaveSlot slot)
        {
            if (!manager.SaveSettings.enableCloudSave) return null;
            if (manager.SaveSystem is not ISaveConflictSource source)
                throw new NotSupportedException($"{manager.SaveSystem.GetType().Name} must implement ISaveConflictSource to enforce Crystal Save's cloud conflict policy.");
            var candidates = await context.WaitAsync(source.ReadCandidatesAsync(SaveManager.CopySlot(slot), context.PreparationToken));
            if (candidates == null) throw new InvalidOperationException("The backend returned no conflict-read result.");
            var local = await DecodeConflictCandidateAsync(context, candidates.Local, false);
            var remote = await DecodeConflictCandidateAsync(context, candidates.Remote, true);
            if (local == null || remote == null || local.LastSaved == remote.LastSaved) return null;
            bool mergedCandidate = false;
            var selected = SelectAutomaticConflict(manager.SaveSettings, local, remote, candidates.Local.Metadata, candidates.Remote.Metadata);
            if (selected == null)
            {
                if (conflictResolver == null) throw new InvalidOperationException("Manual conflict resolution requires a LiveConflictResolver.");
                var completion = new TaskCompletionSource<SaveData>(TaskCreationOptions.RunContinuationsAsynchronously);
                var close = conflictResolver.ResolveCancellable(local, remote, data => completion.TrySetResult(data), onMerge: () => mergedCandidate = true);
                try { selected = await context.WaitAsync(completion.Task); }
                finally { close(); }
            }
            context.CheckCancellation();
            manager.ValidateCandidate(selected);
            var chosenMetadata = ReferenceEquals(selected, remote) ? candidates.Remote.Metadata : candidates.Local.Metadata;
            if (context.RequestedSlotName == null && chosenMetadata?.SlotName != null)
                slot.SlotName = chosenMetadata.SlotName;
            // Retain keys that address the destination, not stale gameplay metadata from it.
            var requested = slot.CustomMetadata;
            slot.CustomMetadata = new Dictionary<string, string>(chosenMetadata?.CustomMetadata ?? new Dictionary<string, string>());
            foreach (string pattern in new[] { manager.SaveSettings.saveFileName, manager.SaveSettings.metadataFileNamePattern, manager.SaveSettings.saveKey })
            {
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(pattern ?? "", "\\{meta:([^}]+)\\}"))
                {
                    string key = match.Groups[1].Value;
                    if (requested != null && requested.TryGetValue(key, out var value)) slot.CustomMetadata[key] = value;
                }
            }
            foreach (var entry in context.MetadataOverrides)
                slot.CustomMetadata[entry.Key] = entry.Value;
            slot.LastSaved = selected.LastSaved;
            slot.LastActiveScene = selected.LastActiveScene;
            // Remote reads do not download screenshots. A merged payload has no single source image.
            string shot = mergedCandidate || ReferenceEquals(selected, remote) ? null : chosenMetadata?.ScreenshotFileName ?? selected.ScreenshotFileName;
            string path = ScreenshotManager.IsValidScreenshotFileName(shot)
                ? System.IO.Path.Combine(manager.SaveSystem.GetSaveGamesPath(), manager.SaveSettings.screenshotFolderName, shot)
                : null;
            slot.ScreenshotFileName = path != null && System.IO.File.Exists(path) ? shot : null;
            selected.ScreenshotFileName = slot.ScreenshotFileName;
            context.PreserveLiveState = true;
            return selected;
        }

        internal async Task<SaveConflictCandidate> ResolveHistoryCandidateAsync(SaveOperationContext context, SaveSlot slot)
        {
            if (manager.SaveSystem is not ISaveConflictSource source)
                throw new NotSupportedException("Rotating cloud saves require ISaveConflictSource.");
            var candidates = await context.WaitAsync(source.ReadCandidatesAsync(SaveManager.CopySlot(slot), context.PreparationToken));
            if (candidates == null) throw new InvalidDataException("Missing history conflict candidates.");
            var local = await DecodeConflictCandidateAsync(context, candidates.Local, false);
            var remote = await DecodeConflictCandidateAsync(context, candidates.Remote, true);
            var selected = local ?? remote;
            if (selected == null) return null;
            bool mergedCandidate = false;
            if (local != null && remote != null && local.LastSaved != remote.LastSaved)
            {
                selected = SelectAutomaticConflict(manager.SaveSettings, local, remote, candidates.Local.Metadata, candidates.Remote.Metadata);
                if (selected == null)
                {
                    if (conflictResolver == null) throw new InvalidOperationException("Manual history conflict resolution requires a LiveConflictResolver.");
                    var completion = new TaskCompletionSource<SaveData>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var close = conflictResolver.ResolveCancellable(local, remote, value => completion.TrySetResult(value),
                        onMerge: () => mergedCandidate = true);
                    try { selected = await context.WaitAsync(completion.Task); }
                    finally { close(); }
                }
            }
            context.CheckCancellation();
            manager.ValidateCandidate(selected);
            var selectedSource = ReferenceEquals(selected, remote) ? candidates.Remote : ReferenceEquals(selected, local) ? candidates.Local : null;
            // Merge mutates the detached local candidate in place. Reference equality
            // identifies its metadata source, but cannot prove its original bytes still apply.
            var original = mergedCandidate ? null : selectedSource;
            var metadata = SaveManager.CopySlot(selectedSource?.Metadata ?? slot);
            metadata.LastSaved = selected.LastSaved;
            metadata.LastActiveScene = selected.LastActiveScene;
            metadata.ScreenshotFileName = original == null ? null : original.Metadata?.ScreenshotFileName ?? selected.ScreenshotFileName;
            byte[] payload = original?.Payload;
            if (payload == null)
            {
                selected.ScreenshotFileName = null;
                payload = await context.WaitAsync(EncryptForSaveAsync(MaybeCompress(serializer.Serialize(selected))));
            }
            return new SaveConflictCandidate(payload, metadata);
        }

        async Task<SaveData> DecodeConflictCandidateAsync(SaveOperationContext context, SaveConflictCandidate candidate, bool remote)
        {
            if (candidate == null) return null;
            // Cloud mirrors contain the exact backend payload, including an opaque
            // server-side envelope. Its location does not change the required decoder.
            var bytes = remote || UseServerSideCloudCrypto
                ? await context.WaitAsync(DecryptFromCloudAsync(candidate.Payload))
                : MaybeDecrypt(candidate.Payload);
            if (bytes == null) throw new System.IO.InvalidDataException("Conflict candidate authentication failed.");
            var data = serializer.DeserializeSaveDataExact(MaybeDecompress(bytes));
            manager.ValidateCandidate(data);
            return data;
        }

        internal static SaveData SelectAutomaticConflict(SaveSettings settings, SaveData local, SaveData remote, SaveSlot localMetadata, SaveSlot remoteMetadata)
        {
            if (!settings.autoResolveConflicts) return null;
            if (local == null || remote == null) throw new InvalidDataException("Invalid conflict candidate.");
            switch (settings.autoConflictPolicy)
            {
                case AutoConflictPolicy.Latest: return local.LastSaved >= remote.LastSaved ? local : remote;
                case AutoConflictPolicy.Oldest: return local.LastSaved <= remote.LastSaved ? local : remote;
                case AutoConflictPolicy.LocalWins: return local;
                case AutoConflictPolicy.CloudWins: return remote;
                case AutoConflictPolicy.Custom:
                    var empty = new Dictionary<string, string>();
                    bool localMatches = MatchesRules(localMetadata?.CustomMetadata ?? empty, settings.metadataRules, local, remote, true);
                    bool remoteMatches = MatchesRules(remoteMetadata?.CustomMetadata ?? empty, settings.metadataRules, local, remote, false);
                    return localMatches == remoteMatches ? null : localMatches ? local : remote;
                default: throw new InvalidOperationException("Unsupported conflict policy.");
            }
        }

        internal void ResolveSlotDataSync(SaveSlot slot)
        {
            if (manager.SaveSettings.enableCloudSave) throw new InvalidOperationException("Cloud conflict resolution requires an asynchronous operation.");
            // Collection merges the current local snapshots itself; there is no competing cloud generation.

        }

        static bool MatchesRules(Dictionary<string, string> meta, MetadataRule[] rules, SaveData local, SaveData cloud, bool isLocal)
        {
            if (rules == null || rules.Length == 0)
                return false;

            int count = Math.Min(rules.Length, 2);
            for (int i = 0; i < count; i++)
            {
                var r = rules[i];
                switch (r.type)
                {
                    case MetadataRuleType.Metadata:
                        if (string.IsNullOrEmpty(r.key))
                            return false;
                        if (!meta.TryGetValue(r.key, out string val))
                            return false;
                        if (!Compare(val, r.value, r.op))
                            return false;
                        break;
                    case MetadataRuleType.Latest:
                        if (local == null || cloud == null)
                            return false;
                        if (isLocal)
                        {
                            if (local.LastSaved < cloud.LastSaved)
                                return false;
                        }
                        else if (cloud.LastSaved < local.LastSaved)
                            return false;
                        break;
                    case MetadataRuleType.Oldest:
                        if (local == null || cloud == null)
                            return false;
                        if (isLocal)
                        {
                            if (local.LastSaved > cloud.LastSaved)
                                return false;
                        }
                        else if (cloud.LastSaved > local.LastSaved)
                            return false;
                        break;
                    case MetadataRuleType.LocalWins:
                        if (!isLocal)
                            return false;
                        break;
                    case MetadataRuleType.CloudWins:
                        if (isLocal)
                            return false;
                        break;
                }
            }
            return true;
        }

        static bool Compare(string lhs, string rhs, ComparisonOp op)
        {
            bool leftNum = double.TryParse(lhs, out double lv);
            bool rightNum = double.TryParse(rhs, out double rv);
            if (leftNum && rightNum)
            {
                return op switch
                {
                    ComparisonOp.Equals => lv == rv,
                    ComparisonOp.Smaller => lv < rv,
                    ComparisonOp.Larger => lv > rv,
                    _ => false
                };
            }

            int cmp = string.CompareOrdinal(lhs, rhs);
            return op switch
            {
                ComparisonOp.Equals => cmp == 0,
                ComparisonOp.Smaller => cmp < 0,
                ComparisonOp.Larger => cmp > 0,
                _ => false
            };
        }

        #endregion

        /* ================================================================== */
        #region Screenshot API
        /* ================================================================== */
        public Texture2D GetScreenshot(SaveSlot slot)
            => screenshotManager.LoadScreenshot(slot);

        public string CaptureScreenshotSync(int slotNumber)
            => screenshotManager.CaptureScreenshotSync(slotNumber);





        public void CleanupScreenshots()
            => screenshotManager.CleanupUnusedScreenshots(slotManager.GetAll());
        #endregion

        /* ================================================================== */
        #region Save API
        /* ================================================================== */
        public void Save(int slotNumber = 1, string lastActiveScene = null)
            => manager.ObserveOperation(manager.SaveAsync(slotNumber, lastActiveScene));
        public Task SaveAsync(int slotNumber = 1, string lastActiveScene = null, CancellationToken ct = default)
            => manager.SaveAsync(slotNumber, lastActiveScene, ct);
        public Task SaveAsync(int slotNumber, string lastActiveScene, string customSlotName, CancellationToken ct = default)
            => manager.SaveAsync(slotNumber, lastActiveScene, customSlotName, ct);
        public Task SaveImmediateAsync(int slotNumber = 1, string lastActiveScene = null)
            => manager.SaveImmediateAsync(slotNumber, lastActiveScene);

        internal async Task RunSaveAsync(SaveOperationContext context, int slotNumber, string scene, string name, bool immediate, bool automaticName = false, SaveSlot preparedSlot = null)
        {
            var source = preparedSlot ?? slotManager.GetByNumber(slotNumber) ?? throw new ArgumentException($"Slot {slotNumber} does not exist.");
            var slot = SaveBundleManifest.FromSlot(source, null);
            context.WorkingSlot = new SaveSlot(source.SlotNumber, source.SlotName, source.LastSaved, source.ScreenshotFileName, source.LastActiveScene);
            slot.Apply(context.WorkingSlot);
            if (automaticName && string.IsNullOrEmpty(name)) name = string.IsNullOrEmpty(scene) ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name : scene;
            context.RequestedSlotName = string.IsNullOrEmpty(name) ? null : name;
            if (!string.IsNullOrEmpty(name)) context.WorkingSlot.SlotName = name;
            var previousData = manager.CurrentSaveData;
            context.SaveReported = false; context.SaveError = null;
            try
            {
                if (immediate) await SaveImmediateCoreAsync(context, slotNumber, scene);
                else
                {
                    // The async core owns its capture coroutine and staging writes.
                    // An outer cancellable polling iterator could end before a pending
                    // staging write finishes, releasing ownership before its cleanup.
                    await SaveCandidateCoreAsync(context, context.WorkingSlot, scene, true);
                }
                if (!context.SaveReported) throw new InvalidOperationException("Save ended without an operation result.");
                if (context.SaveError != null) throw new InvalidOperationException(context.SaveError);
            }
            catch (Exception ex)
            {
                if (!context.PayloadCommitted) manager.CurrentSaveData = previousData;
                if (!context.SaveReported && !(ex is OperationCanceledException)) manager.CompleteSave(context, context.WorkingSlot, false, ex.Message);
                throw;
            }
            finally { context.WorkingSlot = null; }
        }

        internal async Task SaveImmediateCoreAsync(SaveOperationContext context, int slotNumber = 1,
                                             string lastActiveScene = null)
        {
            var slot = context?.WorkingSlot ?? SaveManager.CopySlot(slotManager.GetByNumber(slotNumber));
            await SaveCandidateCoreAsync(context, slot, lastActiveScene, false);
        }

        internal IEnumerator SaveCoroutine(SaveOperationContext context, SaveSlot slot, string lastActiveScene = null)
        {
            var task = SaveCandidateCoreAsync(context, slot, lastActiveScene, true);
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }
        #endregion
    }
}
#endif
