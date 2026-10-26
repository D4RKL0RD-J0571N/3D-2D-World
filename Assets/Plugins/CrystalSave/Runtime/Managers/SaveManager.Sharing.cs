#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace Arawn.CrystalSave.Runtime
{
    public partial class SaveManager
    {
        /// <summary>Exports portable, unencrypted MemoryPack save data.</summary>
        public Task<byte[]> ExportSaveAsync(int slotNumber) => ExportSaveAsync(slotNumber, null);
        public Task<byte[]> ExportSaveAsync(int slotNumber, SaveBundleOptions options, CancellationToken token = default)
        {
            var limits = SaveBundleOptions.Resolve(options);
            return Operations.RunAsync(async context =>
            {
                await AwaitOperationReady(context);
                var data = await PrepareSlotAsync(context, GetSaveSlot(slotNumber));
                ValidateCandidate(data);
                var bytes = Serializer.Serialize(data);
                SaveBundleCodec.CheckLength(bytes.Length, limits.MaxEntryBytes);
                return bytes;
            }, token);
        }
        public Task<bool> ImportSaveAsync(byte[] portableBytes, int slotNumber) => ImportSaveAsync(portableBytes, slotNumber, null);
        public Task<bool> ImportSaveAsync(byte[] portableBytes, int slotNumber, SaveBundleOptions options, CancellationToken token = default)
            => QueueImport(portableBytes, slotNumber, options, false, token);
        /// <summary>Returns a v2 bundle for browser downloads or application-defined transport.</summary>
        public Task<byte[]> ExportSaveBundleBytesAsync(int slotNumber, SaveBundleOptions options = null, CancellationToken token = default)
        {
            var limits = SaveBundleOptions.Resolve(options);
            return Operations.RunAsync(async context =>
            {
                await AwaitOperationReady(context);
                var (slot, data) = await PrepareBundleCandidateAsync(context, CopySlot(GetSaveSlot(slotNumber)), context.Token);
                ValidateCandidate(data);
                // Migration may change these values; the exported pair must describe its result.
                slot.LastSaved = data.LastSaved;
                slot.LastActiveScene = data.LastActiveScene;
                // A current header may explicitly clear its preview without rewriting gameplay.
                data.ScreenshotFileName = slot.ScreenshotFileName;
                var bytes = Serializer.Serialize(data);
                SaveBundleCodec.CheckLength(bytes.Length, limits.MaxEntryBytes);
                byte[] screenshot = null; string extension = null;
                if (ScreenshotManager.IsValidScreenshotFileName(slot.ScreenshotFileName))
                {
                    string path = Path.Combine(_rootPath, saveSettings.screenshotFolderName, slot.ScreenshotFileName);
                    if (File.Exists(path))
                    {
                        screenshot = ReadBoundedFile(path, limits.MaxEntryBytes);
                        if (SaveCrypto.HasEnvelope(screenshot)) screenshot = SaveOperations.MaybeDecrypt(screenshot) ?? throw new InvalidDataException("Screenshot authentication failed.");
                        extension = SaveBundleCodec.NormalizeExtension(Path.GetExtension(path));
                    }
                }
                context.CheckCancellation();
                return SaveBundleCodec.Encode(SaveBundleManifest.FromSlot(slot, extension), bytes, screenshot, extension, limits);
            }, token);
        }

        async Task<(SaveSlot slot, SaveData data)> PrepareBundleCandidateAsync(SaveOperationContext context, SaveSlot published, CancellationToken token)
        {
            SaveSlot metadata;
            SaveData data;
            if (SaveSystem is ISaveExportSource source)
            {
                if (saveSettings.enableCloudSave && !await context.WaitAsync(WaitForCloudConnectionAsync()))
                    throw new InvalidOperationException("Unable to export: connection to cloud provider not established.");
                var candidates = await context.WaitAsync(source.ReadExportCandidatesAsync(published, token));
                if (candidates == null) throw new InvalidDataException("Export source returned no candidates.");
                var chosen = candidates.Remote ?? candidates.Local;
                if (candidates.Local != null && candidates.Remote != null && saveSettings.autoResolveConflicts)
                {
                    var local = await DecodeExportCandidateAsync(context, candidates.Local);
                    var remote = await DecodeExportCandidateAsync(context, candidates.Remote);
                    data = SaveOperationService.SelectAutomaticConflict(saveSettings, local, remote,
                        candidates.Local.Metadata, candidates.Remote.Metadata) ?? (local.LastSaved >= remote.LastSaved ? local : remote);
                    chosen = ReferenceEquals(data, local) ? candidates.Local : candidates.Remote;
                }
                else data = await DecodeExportCandidateAsync(context, chosen);
                metadata = chosen.Metadata;
            }
            else
            {
                // Custom backends keep their ordinary read behavior. Fetch their header anew;
                // only use a cached legacy fallback if it demonstrably describes this generation.
                data = await PrepareSlotAsync(context, published);
                metadata = await context.WaitAsync(SaveSystem.LoadSlotMetadataAsync(published.SlotNumber));
                if (metadata == null && BundleHeaderMatches(published, data)) metadata = published;
            }
            if (metadata != null && (metadata.SlotNumber != published.SlotNumber || !BundleHeaderMatches(metadata, data)))
                throw new InvalidDataException("Save data and metadata changed generations during export. Refresh the slot and retry.");
            var slot = metadata == null
                ? new SaveSlot(published.SlotNumber, $"Slot {published.SlotNumber}", data.LastSaved, data.ScreenshotFileName, data.LastActiveScene)
                : CopySlot(metadata);
            token.ThrowIfCancellationRequested();
            return (slot, data);
        }

        static bool BundleHeaderMatches(SaveSlot slot, SaveData data) => data != null &&
            (slot.LastSaved == default || slot.LastSaved == data.LastSaved) &&
            (string.IsNullOrEmpty(slot.LastActiveScene) || slot.LastActiveScene == data.LastActiveScene) &&
            (string.IsNullOrEmpty(slot.ScreenshotFileName) || string.IsNullOrEmpty(data.ScreenshotFileName) ||
             slot.ScreenshotFileName == data.ScreenshotFileName);

        async Task<SaveData> DecodeExportCandidateAsync(SaveOperationContext context, SaveConflictCandidate candidate)
        {
            if (candidate == null || candidate.Payload.Length == 0) throw new InvalidDataException("No data returned for the requested slot.");
            var bytes = await context.WaitAsync(SaveOperations.DecryptFromCloudAsync(candidate.Payload));
            if (bytes == null) throw new InvalidDataException("Save authentication failed.");
#if UNITY_WEBGL && !UNITY_EDITOR
            await Task.Yield();
            context.CheckCancellation();
            return Serializer.DeserializeSaveDataExact(SaveOperations.MaybeDecompress(bytes));
#else
            return await context.WaitAsync(Task.Run(() =>
            {
                context.CheckCancellation();
                var plain = SaveOperations.MaybeDecompress(bytes);
                context.CheckCancellation();
                return Serializer.DeserializeSaveDataExact(plain);
            }));
#endif
        }
        public async Task<string> ExportSaveBundleAsync(int slotNumber)
        {
            string path = Path.Combine(UnityEngine.Application.persistentDataPath, "CrystalSave Exports", $"Slot_{slotNumber}_{DateTime.Now:yyyyMMdd_HHmmssfff}.crystalsave");
            return await ExportSaveBundleAsync(slotNumber, path) ? path : null;
        }
        public Task<bool> ExportSaveBundleAsync(int slotNumber, string destinationPath) => ExportSaveBundleAsync(slotNumber, destinationPath, null);
        public async Task<bool> ExportSaveBundleAsync(int slotNumber, string destinationPath, SaveBundleOptions options, CancellationToken token = default)
        {
            try { var bytes = await ExportSaveBundleBytesAsync(slotNumber, options, token); token.ThrowIfCancellationRequested(); AtomicSaveFile.WriteAllBytes(destinationPath, bytes); return true; }
            catch (Exception ex) { Logger.Log($"Bundle export failed: {ex.Message}", LogLevel.Warning); return false; }
        }
        public Task<bool> ImportSaveBundleAsync(string bundlePath, int slotNumber) => ImportSaveBundleAsync(bundlePath, slotNumber, null);
        public async Task<bool> ImportSaveBundleAsync(string bundlePath, int slotNumber, SaveBundleOptions options, CancellationToken token = default)
        {
            try
            {
                var limits = SaveBundleOptions.Resolve(options);
                token.ThrowIfCancellationRequested();
                var bytes = ReadBoundedFile(bundlePath, limits.MaxCompressedBytes);
                return await QueueImport(bytes, slotNumber, limits, true, token);
            }
            catch (Exception ex) { Logger.Log($"Bundle import rejected: {ex.Message}", LogLevel.Warning); return false; }
        }
        public Task<bool> ImportSaveBundleAsync(byte[] bundleBytes, int slotNumber, SaveBundleOptions options = null, CancellationToken token = default)
            => QueueImport(bundleBytes, slotNumber, options, true, token);
        async Task<bool> QueueImport(byte[] input, int slotNumber, SaveBundleOptions options, bool bundle, CancellationToken token)
        {
            try
            {
                var limits = SaveBundleOptions.Resolve(options);
                if (input == null || input.Length == 0) return false;
                SaveBundleCodec.CheckLength(input.Length, bundle ? limits.MaxCompressedBytes : limits.MaxEntryBytes);
                token.ThrowIfCancellationRequested();
                var request = (byte[])input.Clone();
                return await Operations.RunAsync(async context =>
                {
                    await AwaitOperationReady(context);
                    var decoded = bundle ? SaveBundleCodec.Decode(request, limits) : new DecodedSaveBundle { SaveBytes = request, Manifest = new SaveBundleManifest() };
                    var data = Serializer.DeserializeSaveDataExact(decoded.SaveBytes);
                    ValidateBundleGeneration(decoded.Manifest, data);
                    ValidateCandidate(data);
                    var slot = CopySlot(GetSaveSlot(slotNumber));
                    string previousScreenshot = slot.ScreenshotFileName;
                    slot.LastSaved = data.LastSaved; slot.LastActiveScene = data.LastActiveScene;
                    decoded.Manifest.Apply(slot);
                    slot.LastSaved = data.LastSaved;
                    slot.LastActiveScene = data.LastActiveScene;
                    // A foreign path is never used for screenshot storage.
                    slot.ScreenshotFileName = null;
                    byte[] screenshot = decoded.Screenshot;
                    string screenshotPath = null;
                    if (screenshot != null && screenshot.Length > 0)
                    {
                        slot.ScreenshotFileName = $"Slot_{slotNumber}_{Guid.NewGuid():N}{decoded.ScreenshotExtension}";
                        screenshotPath = Path.Combine(_rootPath, saveSettings.screenshotFolderName, slot.ScreenshotFileName);
                        if (saveSettings.enableEncryption && saveSettings.encryptScreenshots &&
                            !(saveSettings.enableCloudSave && saveSettings.cloudCryptoMode == CloudCryptoMode.ServerSide))
                            screenshot = SaveOperations.MaybeEncrypt(screenshot);
                    }
                    data.ScreenshotFileName = slot.ScreenshotFileName;
                    byte[] payload = await context.WaitAsync(SaveOperations.EncryptForSaveAsync(SaveOperations.MaybeCompress(Serializer.Serialize(data))));
                    if (payload == null || payload.Length == 0) throw new InvalidDataException("Save encryption failed.");
                    try
                    {
                        await SaveOperations.CommitPayloadAsync(context, payload, slot, () =>
                        {
                            if (screenshotPath != null) AtomicSaveFile.WriteAllBytes(screenshotPath, screenshot);
                        });
                    }
                    catch (Exception importError)
                    {
                        if (context.PayloadCommitted)
                        {
                            SyncSlotToInternalList(slot);
                            SlotManager?.InvalidateCloudCache(slotNumber);
                            throw;
                        }
                        if (screenshotPath != null)
                        {
                            try
                            {
                                AtomicSaveFile.Delete(screenshotPath);
#if UNITY_WEBGL && !UNITY_EDITOR
                                await WebGLFileSystem.FlushAsync();
#endif
                            }
                            catch (Exception cleanupError)
                            {
                                throw new AggregateException("Import failed and its new screenshot could not be removed.", importError, cleanupError);
                            }
                        }
                        throw;
                    }
                    SyncSlotToInternalList(slot);
                    CleanupCommittedScreenshot(previousScreenshot, slot.ScreenshotFileName);
                    await SaveOperations.FlushCommittedCleanupAsync();
                    SlotManager?.InvalidateCloudCache(slotNumber);
                    NotifyOperation(() => SaveEventDispatch.Raise(OnSaveSlotsUpdated));
                    return true;
                }, token);
            }
            catch (Exception ex) { Logger.Log($"Save import rejected or failed: {ex.Message}", LogLevel.Warning); return false; }
        }
        static void ValidateBundleGeneration(SaveBundleManifest manifest, SaveData data)
        {
            if (data == null || manifest == null) throw new InvalidDataException("Missing bundle data or manifest.");
            if (manifest.LastSaved.HasValue && manifest.LastSaved.Value != data.LastSaved ||
                manifest.LastActiveScene != null && manifest.LastActiveScene != data.LastActiveScene)
                throw new InvalidDataException("Bundle manifest and save data describe different generations.");
        }
        internal static byte[] ReadBoundedFile(string path, long limit)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            SaveBundleCodec.CheckLength(file.Length, Math.Min(limit, int.MaxValue));
            using var reader = new BinaryReader(file);
            int length = (int)file.Length;
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length || reader.Read() != -1) throw new InvalidDataException("File changed while reading.");
            return bytes;
        }
        public async Task<bool> ExportSaveToFileAsync(int slotNumber, string destinationPath)
        {
            try { AtomicSaveFile.WriteAllBytes(destinationPath, await ExportSaveAsync(slotNumber)); return true; }
            catch (Exception ex) { Logger.Log($"Save export failed: {ex.Message}", LogLevel.Warning); return false; }
        }
        public async Task<bool> ImportSaveFromFileAsync(string sourcePath, int slotNumber)
        {
            try { return await ImportSaveAsync(ReadBoundedFile(sourcePath, new SaveBundleOptions().MaxEntryBytes), slotNumber); }
            catch (Exception ex) { Logger.Log($"Save import failed: {ex.Message}", LogLevel.Warning); return false; }
        }
    }
}
#endif
