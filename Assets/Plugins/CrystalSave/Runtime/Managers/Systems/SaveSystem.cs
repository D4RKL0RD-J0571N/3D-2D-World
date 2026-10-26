#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Collections;
#if REMEMBERME_CLOUDSAVE_PRESENT
using Unity.Services.CloudSave;
#endif
using System.Linq;
#if REMEMBERME_CLOUDSAVE_PRESENT
using PlayerDeleteOptions = Unity.Services.CloudSave.Models.Data.Player.DeleteOptions;
using UnityCloudSaveService = Unity.Services.CloudSave.CloudSaveService;
#endif
using UnityEngine;
using System.Text.RegularExpressions;

namespace Arawn.CrystalSave.Runtime
{
	public partial class SaveSystem : ISaveSystem, ISaveConflictSource
	{
                private readonly SaveSettings settings;
                private readonly string persistentPath;

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
		static bool SignedIn =>
			Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn;
#else
	// keep it a property so IntelliSense / LINQ analyzers are happy
		static bool SignedIn => false;       // compile-time fallback
#endif

                public SaveSystem(SaveSettings saveSettings, string rootPath)
                {
                        settings = saveSettings;
                        persistentPath = rootPath;
                        SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
                        if (settings.enableCloudSave &&
                            settings.cloudCryptoMode == CloudCryptoMode.ServerSide &&
                            settings.keepLocalMirror)
                        {
                                Logger.Log("Server-side cloud crypto enabled; Keep Local Mirror is ignored to avoid local disk writes.",
                                           LogCategory.SaveSystem, LogLevel.Warning);
                        }
#if UNITY_WEBGL && !UNITY_EDITOR
                        // Let Unity handle initial IDBFS population.
                        // ManualSync() is gated by settings.useManualSync and Unity version.
                        ManualSync(true);
#endif
                }

		// Convenience: “are we allowed to touch the local mirror?”
                private bool UseLocalMirror =>
                        !settings.enableCloudSave ||          // classic offline modes
                        (settings.enableCloudSave &&
                         settings.keepLocalMirror &&
                         settings.cloudCryptoMode != CloudCryptoMode.ServerSide);

        // Signed-out cloud imports retain the existing local fallback even when
        // the configured cloud mirror is disabled. Verification owns that write.
        SaveWritePlan CaptureWritePlan() => SaveWritePlan.Capture(this, settings.enableCloudSave, UseLocalMirror, SignedIn);
        bool WritesLocalSave => CaptureWritePlan().Local;

                private static string Sanitize(string key)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                key = key.Replace(c, '_');
            return key;
        }

        // Local helpers to resolve {n} and {meta:key} placeholders and to build globs
        private static string SanitizeFilePart(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private static string ResolvePattern(string pattern, SaveSlot slot)
        {
            if (string.IsNullOrEmpty(pattern)) return string.Empty;
            if (slot == null) return SanitizeFilePart(pattern);

            string resolved = pattern.Replace("{n}", slot.SlotNumber.ToString());
            resolved = Regex.Replace(resolved, "\\{meta:([^}]+)\\}", m =>
            {
                string key = m.Groups[1].Value;
                string val = null;
                if (slot.CustomMetadata != null && slot.CustomMetadata.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                    val = v;
                return SanitizeFilePart(val ?? string.Empty);
            });
            return SanitizeFilePart(resolved);
        }

        private static string PatternToGlob(string pattern, int slotNumber)
        {
            if (string.IsNullOrEmpty(pattern)) return string.Empty;
            string glob = pattern.Replace("{n}", slotNumber.ToString());
            glob = Regex.Replace(glob, "\\{meta:([^}]+)\\}", "*");
            return glob;
        }

        private string SlotFolder(int slotNumber)
        {
            string configured = Path.Combine(persistentPath, settings.ResolveSlotFolderName(slotNumber));
            if (Directory.Exists(configured)) return configured;
            // Legacy fallback: if the old slot{n} folder exists and configured name differs, use it
            string legacy = Path.Combine(persistentPath, $"slot{slotNumber}");
            if (configured != legacy && Directory.Exists(legacy)) return legacy;
            return configured;
        }

        private string EnsureSlotFolder(int slotNumber)
        {
            string dir = SlotFolder(slotNumber);
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch {}
            return dir;
        }

        // Resolve save key with optional {n} placeholder; fallback to legacy concatenation
        private string ResolveSaveKey(int slot)
        {
            return SaveSlotKey.Resolve(settings.saveKey, slot);
        }

        private string ResolveExistingPlayerPrefsKey(int slot)
        {
            string key = ResolveSaveKey(slot);
            string legacy = settings.saveKey + slot;
            return PlayerPrefs.HasKey(key) || !PlayerPrefs.HasKey(legacy) ? key : legacy;
        }

        // Resolve save file stem (without extension) with optional {n} placeholder
        private string ResolveSaveFileStem(int slot)
        {
            string stem = settings.saveFileName ?? string.Empty;
            return stem.Contains("{n}") ? stem.Replace("{n}", slot.ToString()) : stem + slot;
        }

    private string ResolveSaveFileStem(SaveSlot slot)
        {
            string stem = settings.saveFileName ?? string.Empty;
            // Resolve meta placeholders regardless; we handle {n} legacy below
            string resolved = ResolvePattern(stem, slot);
            if (stem.Contains("{n}")) return resolved;
            // Legacy behavior: no {n} provided, append slot number
            return resolved + slot.SlotNumber;
    }

        // Resolve a Cloud Save key from SaveSettings.saveKey allowing {n} and {meta:key}.
        // Then sanitize to allowed characters for Unity Cloud Save Data/Files APIs.
        private string ResolveCloudKey(SaveSlot slot)
        {
            string pattern = settings.saveKey ?? string.Empty;
            string resolved = ResolvePattern(pattern, slot);
            if (!pattern.Contains("{n}"))
            {
                // Legacy behavior: append slot number when {n} is missing
                resolved += slot.SlotNumber.ToString();
            }
            // Cloud Save keys must avoid spaces and special characters.
            // Allow letters, digits, dot, underscore, hyphen; replace others with '_'.
            string sanitized = Regex.Replace(resolved, "[^A-Za-z0-9_.-]", "_");
            // Trim to a reasonable length if needed (defensive; typical limits ~255)
            if (sanitized.Length > 240) sanitized = sanitized.Substring(0, 240);
            return sanitized;
        }

        private static string CloudSanitize(string s)
        {
            s = s ?? string.Empty;
            string sanitized = Regex.Replace(s, "[^A-Za-z0-9_.-]", "_");
            return sanitized.Length > 240 ? sanitized.Substring(0, 240) : sanitized;
        }

#if REMEMBERME_CLOUDSAVE_PRESENT
        static bool IsNotFound(CloudSaveException e)
        {
            var statusCodeProp = e.GetType().GetProperty("StatusCode");
            if (statusCodeProp != null && statusCodeProp.GetValue(e) is int code && code == 404)
                return true;

            var reasonProp = e.GetType().GetProperty("Reason");
            var reasonStr = reasonProp?.GetValue(e)?.ToString();
            if (!string.IsNullOrEmpty(reasonStr) &&
                reasonStr.IndexOf("notfound", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (e.Message != null &&
                (e.Message.Contains("404") || e.Message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;

            return false;
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        private bool UseFileSystemAccess =>
            settings.saveMethod == SaveMethod.BinaryFileFormat ||
            settings.enableSaveFileVerification ||
            (settings.enableCloudSave && settings.keepLocalMirror);

        private void ManualSync(bool populate)
        {
#if !UNITY_2023_1_OR_NEWER
            if (settings.useManualSync)
            {
                Debug.LogWarning("Manual FS.syncfs is deprecated and will be removed in a future Unity version.");
                WebGLFileSystem.Sync(populate);
            }
#endif
        }

        private async Task ManualSyncAsync(bool populate)
        {
#if !UNITY_2023_1_OR_NEWER
            if (settings.useManualSync)
            {
                Debug.LogWarning("Manual FS.syncfs is deprecated and will be removed in a future Unity version.");
                await WebGLFileSystem.SyncAsync(populate);
            }
#else
            if (!populate) await WebGLFileSystem.FlushAsync();
#endif
        }

        private async Task WriteAllBytesAsync(string path, byte[] data)
        {
            if (UseFileSystemAccess)
            {
                WebGLFileSystem.WriteAllBytes(path, data);
                await ManualSyncAsync(false);
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // Task.Run is not supported on WebGL builds, perform the
                // file write synchronously instead and yield once so the
                // async signature is preserved.
                AtomicSaveFile.WriteAllBytes(path, data);
                await Task.Yield();
#else
                await Task.Run(() => AtomicSaveFile.WriteAllBytes(path, data));
#endif
            }
        }

        private void WriteAllBytes(string path, byte[] data)
        {
            if (UseFileSystemAccess)
            {
                WebGLFileSystem.WriteAllBytes(path, data);
                ManualSync(false);
            }
            else
            {
                AtomicSaveFile.WriteAllBytes(path, data);
            }
        }

        private async Task WriteAllTextAsync(string path, string text)
        {
            if (UseFileSystemAccess)
            {
                WebGLFileSystem.WriteAllText(path, text);
                await ManualSyncAsync(false);
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // Task.Run is not available in WebGL builds so execute
                // the write synchronously and yield control back to the
                // caller to avoid blocking.
                AtomicSaveFile.WriteAllText(path, text);
                await Task.Yield();
#else
                await Task.Run(() => AtomicSaveFile.WriteAllText(path, text));
#endif
            }
        }

        private void WriteAllText(string path, string text)
        {
            if (UseFileSystemAccess)
            {
                WebGLFileSystem.WriteAllText(path, text);
                ManualSync(false);
            }
            else
            {
                AtomicSaveFile.WriteAllText(path, text);
            }
        }

        private async Task<byte[]> ReadAllBytesAsync(string path)
        {
            if (UseFileSystemAccess)
            {
                await ManualSyncAsync(true);
                return WebGLFileSystem.ReadAllBytes(path);
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            // Reading via Task.Run is unsupported on WebGL; read
            // synchronously instead.
            return AtomicSaveFile.ReadAllBytes(path);
#else
            return await Task.Run(() => AtomicSaveFile.ReadAllBytes(path));
#endif
        }

        private byte[] ReadAllBytes(string path)
        {
            if (UseFileSystemAccess)
            {
                ManualSync(true);
                return WebGLFileSystem.ReadAllBytes(path);
            }
            return AtomicSaveFile.ReadAllBytes(path);
        }

        private async Task<string> ReadAllTextAsync(string path)
        {
            if (UseFileSystemAccess)
            {
                await ManualSyncAsync(true);
                return WebGLFileSystem.ReadAllText(path);
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            // Task.Run is not supported on WebGL; perform a synchronous
            // read and return the result directly.
            return AtomicSaveFile.ReadAllText(path);
#else
            return await Task.Run(() => AtomicSaveFile.ReadAllText(path));
#endif
        }
        private string ReadAllText(string path)
        {
            if (UseFileSystemAccess)
            {
                ManualSync(true);
                return WebGLFileSystem.ReadAllText(path);
            }
            return AtomicSaveFile.ReadAllText(path);
        }

        private async Task<bool> FileExistsAsync(string path)
        {
            if (UseFileSystemAccess)
            {
                await ManualSyncAsync(true);
                return WebGLFileSystem.Exists(path);
            }
            return AtomicSaveFile.Exists(path);
        }

        private bool FileExists(string path)
        {
            if (UseFileSystemAccess)
            {
                ManualSync(true);
                return WebGLFileSystem.Exists(path);
            }
            return AtomicSaveFile.Exists(path);
        }

        private void DeleteFile(string path)
        {
            if (UseFileSystemAccess)
            {
                WebGLFileSystem.Delete(path);
                ManualSync(false);
            }
            else AtomicSaveFile.Delete(path);
        }

        private void CopyFile(string source, string dest, bool overwrite)
        {
            if (UseFileSystemAccess)
            {
                if (!overwrite && WebGLFileSystem.Exists(dest)) return;
                var bytes = WebGLFileSystem.ReadAllBytes(source);
                if (bytes != null) WebGLFileSystem.WriteAllBytes(dest, bytes);
                ManualSync(false);
            }
            else
            {
                AtomicSaveFile.Copy(source, dest, overwrite);
            }
        }
#else
        private Task WriteAllBytesAsync(string path, byte[] data)
            => Task.Run(() => AtomicSaveFile.WriteAllBytes(path, data));

        private void WriteAllBytes(string path, byte[] data)
            => AtomicSaveFile.WriteAllBytes(path, data);

        private Task WriteAllTextAsync(string path, string text)
            => Task.Run(() => AtomicSaveFile.WriteAllText(path, text));

        private void WriteAllText(string path, string text)
            => AtomicSaveFile.WriteAllText(path, text);

        private Task<byte[]> ReadAllBytesAsync(string path)
            => Task.Run(() => AtomicSaveFile.ReadAllBytes(path));

        private byte[] ReadAllBytes(string path) => AtomicSaveFile.ReadAllBytes(path);

        private Task<string> ReadAllTextAsync(string path)
            => Task.Run(() => AtomicSaveFile.ReadAllText(path));

        private string ReadAllText(string path) => AtomicSaveFile.ReadAllText(path);

        private Task<bool> FileExistsAsync(string path)
            => Task.Run(() => AtomicSaveFile.Exists(path));

        private bool FileExists(string path) => AtomicSaveFile.Exists(path);

        private void DeleteFile(string path)
        {
            AtomicSaveFile.Delete(path);
        }

        private void CopyFile(string source, string dest, bool overwrite)
            => AtomicSaveFile.Copy(source, dest, overwrite);
#endif

		private Task<string> GetSaveFilePathAsync(string fileName) =>
			UnityMainThreadDispatcher.Instance().EnqueueAsync(
				() => Path.Combine(persistentPath, fileName));

        // Helper: run an async function on the Unity main thread and await its completion
        private static Task RunOnMainThreadAsync(Func<Task> asyncAction)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            UnityMainThreadDispatcher.Instance().Enqueue(Main());
            IEnumerator Main()
            {
                Task t = null;
                try { t = asyncAction(); }
                catch (Exception ex) { tcs.TrySetException(ex); yield break; }
                while (t != null && !t.IsCompleted) yield return null;
                if (t == null) tcs.TrySetResult(true);
                else if (t.IsFaulted) tcs.TrySetException(t.Exception?.InnerException ?? t.Exception);
                else if (t.IsCanceled) tcs.TrySetCanceled();
                else tcs.TrySetResult(true);
            }
            return tcs.Task;
        }

        // Helper: run an async function returning T on the Unity main thread and await its result
        private static Task<T> RunOnMainThreadAsync<T>(Func<Task<T>> asyncFunc)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            UnityMainThreadDispatcher.Instance().Enqueue(Main());
            IEnumerator Main()
            {
                Task<T> t = null;
                try { t = asyncFunc(); }
                catch (Exception ex) { tcs.TrySetException(ex); yield break; }
                while (t != null && !t.IsCompleted) yield return null;
                if (t == null) tcs.TrySetResult(default);
                else if (t.IsFaulted) tcs.TrySetException(t.Exception?.InnerException ?? t.Exception);
                else if (t.IsCanceled) tcs.TrySetCanceled();
                else tcs.TrySetResult(t.Result);
            }
            return tcs.Task;
        }

        private string GetSlotMetadataFileName(int n)
        {
            string pattern = settings.metadataFileNamePattern;
            if (string.IsNullOrWhiteSpace(pattern) || !pattern.Contains("{n}"))
                pattern = "Slot{n}_Meta.bin"; // legacy default
            // No SaveSlot instance here; only replace {n}
            return pattern.Replace("{n}", n.ToString());
        }

        private string GetSlotMetadataFileName(SaveSlot slot)
        {
            string pattern = settings.metadataFileNamePattern;
            if (string.IsNullOrWhiteSpace(pattern) || !pattern.Contains("{n}"))
                pattern = "Slot{n}_Meta.bin"; // legacy default
            // Resolve {n} and {meta:key}
            return ResolvePattern(pattern, slot);
        }

        void ValidateSlotFilePaths(SaveSlot slot)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            if (settings.saveMethod != SaveMethod.BinaryFileFormat) return;
            string folder = SlotFolder(slot.SlotNumber);
            string payload = Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav");
            string metadata = Path.Combine(folder, GetSlotMetadataFileName(slot));
            string temporary = Path.Combine(folder, ResolveSaveFileStem(slot) + ".tmp");
            SaveStoragePaths.ValidatePair(payload, metadata, temporary);

            // Inspect existing names without recovering/copying files. Metadata-dependent
            // renames must not overwrite another live generation through a backup suffix.
            var metadataFiles = LocalMetadataFiles(slot, false)
                .Where(file => ReadOwnedHeader(file.Path, slot.SlotNumber) != null)
                .ToArray();
            var payloadFiles = LocalPayloadFiles(slot, false);
            var previousMetadata = metadataFiles.Select(file => file.PrimaryPath);
            var previousPayload = payloadFiles.Select(file => file.PrimaryPath);
            SaveStoragePaths.ValidateGenerations(previousPayload.Append(payload), previousMetadata.Append(metadata), temporary);
            SaveStoragePaths.ValidateIndependentFiles(previousPayload.Append(payload).Concat(previousMetadata).Append(metadata),
                payloadFiles.Concat(metadataFiles).Where(file => file.Path != file.PrimaryPath && !File.Exists(file.PrimaryPath))
                    .Select(file => file.PrimaryPath));
        }
    public async Task SaveAsync(byte[] data, SaveSlot slot)
	{
            SaveIoScope.CheckCancellation();
            ValidateSlotFilePaths(slot);
            byte[] metadata = SerializeSlotMetadata(settings, slot);
            string key = ResolveCloudKey(slot);
            var plan = CaptureWritePlan();
            bool remoteWritten = false;
            try
            {
                plan.RequireRemoteAvailable(settings.enableCloudSave && SignedIn);
                if (plan.Remote)
                    await CreateCloudPayloadTransport().SaveAsync(key, data);
                remoteWritten = plan.Remote;
                plan.RequireRemoteAvailable(settings.enableCloudSave && SignedIn);
                if (plan.Local)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    var restorePrevious = CaptureBrowserRollback(slot);
                    try
                    {
#endif
                    if (settings.saveMethod != SaveMethod.PlayerPrefs || UnityMainThreadDispatcher.IsMainThread) SaveLocalPair(data, slot, metadata);
                    else await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => SaveLocalPair(data, slot, metadata));
#if UNITY_WEBGL && !UNITY_EDITOR
                    await WebGLFileSystem.FlushAsync();
                    CleanupObsoleteFiles(slot);
                    }
                    catch
                    {
                        restorePrevious();
                        try { await WebGLFileSystem.FlushAsync(); } catch { /* Original failure remains authoritative; browser durability is uncertain. */ }
                        throw;
                    }
#endif
                }
                await SaveSlotMetadataCoreAsync(slot, false, plan.Remote);
            }
            catch (Exception ex)
            {
                if (remoteWritten) throw new IOException("Remote payload was uploaded, but metadata or local persistence failed; the remote slot may be partially updated.", ex);
                throw;
            }
        }

		/// <summary>
		/// Asynchronously loads the serialized data from a specific save slot using the configured method.
		/// </summary>
        public async Task<byte[]> LoadAsync(SaveSlot slot)
    {
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            SaveIoScope.CheckCancellation();
        try
        {
            string key     = ResolveCloudKey(slot);
            string safeKey = Sanitize(ResolveSaveKey(slot.SlotNumber));

            bool useRemote = settings.enableCloudSave && SignedIn;
            if (settings.enableCloudSave && !SignedIn)
                Logger.Log("Cloud Save load skipped – player not signed-in. Trying local candidates.", LogCategory.SaveSystem, LogLevel.Info);

            // Load both candidates (cloud/local) when auto-conflict is enabled so we can arbitrate.
            byte[] cloudBytes = null;
            byte[] localBytes = null;

            // Kick off independent reads where possible
            Task<byte[]> cloudTask = null;
            if (useRemote)
                cloudTask = LoadCloudBytesAsync(key);

            Task<byte[]> localTask = LoadLocalCandidateBytesAsync(slot, safeKey);

            if (cloudTask != null) cloudBytes = await cloudTask;
            localBytes = await localTask;
            SaveIoScope.CheckCancellation();

            // Apply Auto Conflict Policy when both sides are present
            if (settings.enableCloudSave && settings.autoResolveConflicts && cloudBytes != null && localBytes != null)
            {
                var enc  = SaveManager.Instance?.EncryptionService;
                var comp = SaveManager.Instance?.CompressionService;

                DateTime localTs = default, cloudTs = default;
                SaveData localData = null, cloudData = null;
                try
                {
                    byte[] b = localBytes;
                    if (enc?.UseEncryption == true) b = enc.MaybeDecrypt(b);
                    b = comp?.MaybeDecompress(b) ?? b;
                    localData = SaveDataSerializer.Instance.Deserialize<SaveData>(b);
                    localTs = localData?.LastSaved ?? default;
                }
                catch { throw; }
                try
                {
                    byte[] b = cloudBytes;
                    if (enc?.UseEncryption == true) b = enc.MaybeDecrypt(b);
                    b = comp?.MaybeDecompress(b) ?? b;
                    cloudData = SaveDataSerializer.Instance.Deserialize<SaveData>(b);
                    cloudTs = cloudData?.LastSaved ?? default;
                }
                catch { throw; }

                var selected = SaveOperationService.SelectAutomaticConflict(settings, localData, cloudData,
                    await LoadLocalSlotMetadataOnlyAsync(slot.SlotNumber), await LoadCloudSlotMetadataOnlyAsync(slot.SlotNumber));
                bool chooseLocal = selected == null ? localTs >= cloudTs : ReferenceEquals(selected, localData);

                var chosen = chooseLocal ? localBytes : cloudBytes;

                // Backfill: keep sources in sync per policy
                if (!chooseLocal && UseLocalMirror)
                {
                    // Chose cloud → refresh local mirror only (no re-upload)
                    await DeferPreparedCacheWrite(() => BackfillLocalMirrorAsync(slot, chosen));
                }
                else if (chooseLocal && useRemote)
                {
                    // Chose local while cloud available → schedule upload on main thread (no background thread)
                    await DeferPreparedCacheWrite(() => SaveAsync(localBytes, slot));
                }

                return chosen;
            }

            // If only one side exists, return it (cloud preferred when present and signed-in)
            if (cloudBytes != null)
            {
                if (UseLocalMirror)
                    await DeferPreparedCacheWrite(() => BackfillLocalMirrorAsync(slot, cloudBytes));
                return cloudBytes;
            }

            if (localBytes != null)
                return localBytes;

            return null; // nothing found
        }
        catch (Exception ex)
        {
            Logger.Log($"LoadAsync failed for slot {slot.SlotNumber}: {ex}", LogCategory.SaveSystem, LogLevel.Error);
            throw;
        }
    }

    static Task DeferPreparedCacheWrite(Func<Task> write)
    {
        var context = SaveIoScope.Current.Value;
        if (context != null && !context.Committed)
        {
            context.CheckCancellation();
            context.PreparedCacheWrites.Add(write);
            return Task.CompletedTask;
        }
        return write();
    }

    // Load the best available local candidate: mirror (PlayerPrefs/File) or cloud fallbacks (.sav/.json) if present
    private async Task<byte[]> LoadLocalCandidateBytesAsync(SaveSlot slot, string safeKey)
    {
        // 1) Local mirror
        if (UseLocalMirror)
        {
            if (settings.saveMethod == SaveMethod.PlayerPrefs)
            {
                string base64 = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                {
                    string k = ResolveExistingPlayerPrefsKey(slot.SlotNumber);
                    return PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null;
                });
                if (!string.IsNullOrEmpty(base64))
                    return Convert.FromBase64String(base64);
            }
            else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (UseFileSystemAccess) await ManualSyncAsync(true);
#endif
                string path = FindCommittedPayloadPath(slot);
                if (path != null) return await ReadAllBytesAsync(path);
            }
        }

        // 2) Cloud local fallbacks created when cloud was enabled but not signed-in
        if (settings.enableCloudSave)
        {
            string blobPath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".sav");
            if (await FileExistsAsync(blobPath))
                return await ReadAllBytesAsync(blobPath);
            string jsonPath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".json");
            if (await FileExistsAsync(jsonPath))
            {
                string txt = await ReadAllTextAsync(jsonPath);
                return Convert.FromBase64String(txt.Trim('"'));
            }
        }

        return null;
    }

    private UnityCloudPayloadTransport CreateCloudPayloadTransport()
        => new UnityCloudPayloadTransport(settings.cloudSaveTransport, UnityCloudPayloadTransport.IsWebGLPlayer,
            SaveCloudPayloadAsync, ReadCloudPayloadAsync, DeleteCloudPayloadAsync,
            exception =>
            {
#if REMEMBERME_CLOUDSAVE_PRESENT
                return exception is CloudSaveException cloudError && IsNotFound(cloudError);
#else
                return false;
#endif
            });

    // The selected transport is shared by writes, reads (including existence probes),
    // and deletion. JSON denotes the Data API's base64 representation.
    private static async Task SaveCloudPayloadAsync(CloudSaveTransport transport, string key, byte[] data)
    {
#if REMEMBERME_CLOUDSAVE_PRESENT
        if (transport == CloudSaveTransport.JSON)
            await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Data.Player.SaveAsync(
                    new Dictionary<string, object> { { key, Convert.ToBase64String(data) } }));
        else
            await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Files.Player.SaveAsync(key, data));
#else
        await Task.CompletedTask;
#endif
    }

    private static async Task<byte[]> ReadCloudPayloadAsync(CloudSaveTransport transport, string key)
    {
#if REMEMBERME_CLOUDSAVE_PRESENT
        if (transport == CloudSaveTransport.JSON)
        {
            var result = await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key }));
            return result != null && result.TryGetValue(key, out var entry)
                ? Convert.FromBase64String(entry.Value.GetAs<string>()) : null;
        }
        return await RunOnMainThreadAsync(async () =>
            await UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(key));
#else
        return await Task.FromResult<byte[]>(null);
#endif
    }

    private static async Task DeleteCloudPayloadAsync(CloudSaveTransport transport, string key)
    {
#if REMEMBERME_CLOUDSAVE_PRESENT
        if (transport == CloudSaveTransport.JSON)
            await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Data.Player.DeleteAsync(key, new PlayerDeleteOptions()));
        else
            await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Files.Player.DeleteAsync(key));
#else
        await Task.CompletedTask;
#endif
    }

    private Task<byte[]> LoadCloudBytesAsync(string key) => CreateCloudPayloadTransport().LoadAsync(key);

    private async Task BackfillLocalMirrorAsync(SaveSlot slot, byte[] data)
    {
        if (!UseLocalMirror || data == null || data.Length == 0) return;
        if (settings.saveMethod == SaveMethod.PlayerPrefs)
        {
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                PlayerPrefs.SetString(ResolveSaveKey(slot.SlotNumber), Convert.ToBase64String(data));
                PlayerPrefs.Save();
            });
        }
        else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
        {
            string path = Path.Combine(EnsureSlotFolder(slot.SlotNumber), ResolveSaveFileStem(slot) + ".sav");
            await WriteAllBytesAsync(path, data);
        }
    }

    private async Task<SaveSlot> LoadLocalSlotMetadataOnlyAsync(int slotNumber)
    {
        if (settings.saveMethod == SaveMethod.PlayerPrefs)
        {
            string metaKey = settings.saveKey + "_Metadata_" + slotNumber;
            string base64 = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => PlayerPrefs.GetString(metaKey, null));
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                var enc = SaveManager.Instance?.EncryptionService;
                if (enc?.UseEncryption == true) bytes = enc.MaybeDecrypt(bytes);
                var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
                if (slot == null) throw new InvalidDataException("Invalid conflict metadata.");
                if (slot.CustomMetadata == null) slot.CustomMetadata = new Dictionary<string,string>();
                return slot;
            }
            catch { throw; }
        }
        else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
        {
            string metaFilePath = FindCommittedMetadataPath(new SaveSlot(slotNumber, null, default, null, null));
            if (metaFilePath == null) return null;
            try
            {
                byte[] bytes = await ReadAllBytesAsync(metaFilePath);
                var enc = SaveManager.Instance?.EncryptionService;
                if (enc?.UseEncryption == true) bytes = enc.MaybeDecrypt(bytes);
                var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
                if (slot == null) throw new InvalidDataException("Invalid conflict metadata.");
                if (slot.CustomMetadata == null) slot.CustomMetadata = new Dictionary<string,string>();
                return slot;
            }
            catch { throw; }
        }
        return null;
    }

    private async Task<SaveSlot> LoadCloudSlotMetadataOnlyAsync(int slotNumber)
    {
        if (!settings.enableCloudSave || !settings.cloudSaveMetadata || !SignedIn)
            return await Task.FromResult<SaveSlot>(null);

#if REMEMBERME_CLOUDSAVE_PRESENT
        string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + slotNumber;
        byte[] bytes = null;
        // Data API first
        var res = await RunOnMainThreadAsync(async () =>
            await UnityCloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>{ metaKey }));
        if (res != null && res.TryGetValue(metaKey, out var e))
            bytes = Convert.FromBase64String(e.Value.GetAs<string>());
#if !UNITY_WEBGL || UNITY_EDITOR
        if (bytes == null || bytes.Length == 0)
        {
            try { bytes = await RunOnMainThreadAsync(async () =>
                        await UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(metaKey)); } catch (CloudSaveException e) { if (!IsNotFound(e)) throw; }
        }
#endif
        if (bytes == null || bytes.Length == 0) return await Task.FromResult<SaveSlot>(null);
        try
        {
            var enc = SaveManager.Instance?.EncryptionService;
            if (enc?.UseEncryption == true) bytes = enc.MaybeDecrypt(bytes);
            var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
            if (slot == null) throw new InvalidDataException("Invalid conflict metadata.");
                if (slot.CustomMetadata == null) slot.CustomMetadata = new Dictionary<string,string>();
            return slot;
        }
        catch { throw; }
#else
        // SDK not present – nothing to load
        return await Task.FromResult<SaveSlot>(null);
#endif
    }





        void DeleteNativeSlotGenerations(SaveSlot slot)
        {
            SaveIoScope.CheckCancellation();
            // Resolve ownership before removing headers or verification records needed as evidence.
            var files = LocalPayloadFiles(slot, true).Concat(LocalMetadataFiles(slot, true))
                .Select(file => file.Path).Distinct().ToArray();
            Verification.Discard(slot.SlotNumber);
            foreach (string file in files) AtomicSaveFile.Delete(file);
        }

        private async Task DeleteLocalDataAsync(SaveSlot slot, string safeKey)
        {
            SaveIoScope.CheckCancellation();
            // A failed pair write can leave a journal even on this same storage instance.
            // Retire that slot's journal first so a later read cannot resurrect deletion.
            SaveFileTransaction.RecoverSlot(persistentPath, slot.SlotNumber);
            // Resolve all slot-owned generations before deleting the metadata that proves
            // ownership. The image belongs to this same acknowledged browser mutation.
            string folder = EnsureSlotFolder(slot.SlotNumber);
            var files = LocalPayloadFiles(slot, true).Concat(LocalMetadataFiles(slot, true))
                .Select(file => file.Path).Concat(Verification.DeletionFiles(slot.SlotNumber)).ToList();
            files.Add(Path.Combine(folder, safeKey + ".sav"));
            files.Add(Path.Combine(folder, safeKey + ".json"));
            files.Add(Path.Combine(persistentPath, safeKey + ".sav"));
            files.Add(Path.Combine(persistentPath, safeKey + ".json"));
            if (settings.enableScreenshots && ScreenshotManager.IsValidScreenshotFileName(slot.ScreenshotFileName))
                files.Add(Path.Combine(persistentPath, settings.screenshotFolderName, slot.ScreenshotFileName));

            string mainKey = ResolveSaveKey(slot.SlotNumber);
            string legacyKey = settings.saveKey + slot.SlotNumber;
            string metaKey = settings.saveKey + "_Metadata_" + slot.SlotNumber;
            string[] preferenceKeys = new[] { mainKey, legacyKey, metaKey, mainKey + "_bak", legacyKey + "_bak", metaKey + "_bak" }
                .Concat(Verification.DeletionPreferenceKeys(slot.SlotNumber)).Distinct().ToArray();
            await RunOnMainThreadAsync(async () =>
            {
                Dictionary<string, string> previousPreferences = null;
                try
                {
                    previousPreferences = preferenceKeys.ToDictionary(key => key,
                        key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null);
                }
                catch (PlatformNotSupportedException) when (settings.saveMethod != SaveMethod.PlayerPrefs)
                {
                    // Optional legacy-key cleanup is unavailable on this platform.
                }
                void RestorePreferences()
                {
                    if (previousPreferences == null) return;
                    foreach (var entry in previousPreferences)
                        if (entry.Value == null) PlayerPrefs.DeleteKey(entry.Key);
                        else PlayerPrefs.SetString(entry.Key, entry.Value);
                    PlayerPrefs.Save();
                }
                await CloudMirrorFiles.DeleteAsync(files, () =>
                {
                    if (previousPreferences != null)
                    {
                        foreach (string key in preferenceKeys) PlayerPrefs.DeleteKey(key);
                        PlayerPrefs.Save();
                    }
                    Verification.Discard(slot.SlotNumber);
                }, RestorePreferences);
            });
        }

        public async Task DeleteLocalSlotAsync(SaveSlot slot)
        {
            string safeKey = Sanitize(ResolveSaveKey(slot.SlotNumber));
            await DeleteLocalDataAsync(slot, safeKey);
        }

		/// <summary>
		/// Asynchronously deletes the saved data from a specific save slot using the configured method.
		/// Also deletes the associated screenshot if enabled.
		/// </summary>
        public async Task DeleteAsync(SaveSlot slot)
	{
#if !UNITY_WEBGL || UNITY_EDITOR
            Verification.Discard(slot.SlotNumber);
#endif
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            SaveIoScope.CheckCancellation();
	    try
	    {
            // Use cloud-safe resolved key for remote deletes
            string key     = ResolveCloudKey(slot);
            string safeKey = Sanitize(ResolveSaveKey(slot.SlotNumber));

            // If the slot looks empty (no known last save and no screenshot), skip remote deletes to avoid 404 noise.
            // Local cleanup will still run to be safe.
            bool looksEmpty = (slot == null) || (slot.LastSaved == default && string.IsNullOrEmpty(slot.ScreenshotFileName));
            if (looksEmpty)
            {
                Logger.Log($"DeleteAsync: Slot {slot?.SlotNumber} appears empty; skipping remote delete calls.", LogCategory.SaveSystem, LogLevel.Info);
                await DeleteLocalDataAsync(slot, safeKey);
                return;
            }

            /* 1 Cloud OR fallback */
#if REMEMBERME_CLOUDSAVE_PRESENT
            if (settings.enableCloudSave)
            {
                await CreateCloudPayloadTransport().DeleteAsync(key);

                if (settings.cloudSaveScreenshots && !string.IsNullOrEmpty(slot.ScreenshotFileName))
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    // WebGL: slot.ScreenshotFileName already contains the correct key format
                    string deleteKey = slot.ScreenshotFileName;
                    // If the key doesn't start with screenshot_, add the prefix for backward compatibility
                    if (!deleteKey.StartsWith("screenshot_"))
                    {
                        deleteKey = deleteKey.Contains(".") ?
                            deleteKey.Substring(0, deleteKey.LastIndexOf('.')) : deleteKey;
                        deleteKey = $"screenshot_{deleteKey}";
                    }
                    try
                    {
                        await RunOnMainThreadAsync(async () =>
                            await UnityCloudSaveService.Instance.Data.Player.DeleteAsync(deleteKey, new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions()));
                        Logger.Log($"WebGL screenshot deleted via Data API: {deleteKey}", LogCategory.SaveSystem, LogLevel.Info);
                    }
                    catch (CloudSaveException e)
                    {
                        if (!IsNotFound(e)) throw;
                        Logger.Log($"Screenshot not found in cloud (ignored): {deleteKey}", LogCategory.SaveSystem, LogLevel.Info);
                    }
#else
                    // Non-WebGL platforms: try both APIs for maximum compatibility
                    try
                    {
                        string dataApiKey = slot.ScreenshotFileName;
                        if (!dataApiKey.StartsWith("screenshot_"))
                        {
                            dataApiKey = dataApiKey.Contains(".") ?
                                dataApiKey.Substring(0, dataApiKey.LastIndexOf('.')) : dataApiKey;
                            dataApiKey = $"screenshot_{dataApiKey}";
                        }
                        try
                        {
                            await RunOnMainThreadAsync(async () =>
                                await UnityCloudSaveService.Instance.Data.Player.DeleteAsync(dataApiKey, new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions()));
                            Logger.Log($"Screenshot deleted via Data API: {dataApiKey}", LogCategory.SaveSystem, LogLevel.Info);
                        }
                        catch (CloudSaveException e)
                        {
                            if (!IsNotFound(e)) throw;
                            Logger.Log($"Screenshot not found in cloud (ignored): {dataApiKey}", LogCategory.SaveSystem, LogLevel.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Data API screenshot deletion failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveSystem, LogLevel.Warning);
                    }
#endif
                }

                if (settings.cloudSaveMetadata)
                {
                    // Cloud metadata now always uses Data API keys; delete from both Data and Files for safety
                    string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + slot.SlotNumber;
                    switch (settings.cloudSaveTransport)
                    {
                        case CloudSaveTransport.Binary:
#if REMEMBERME_CLOUDSAVE_PRESENT
                            // Primary: Data API only on WebGL to avoid CORS
                            try
                            {
                                await RunOnMainThreadAsync(async () =>
                                    await UnityCloudSaveService.Instance.Data.Player.DeleteAsync(metaKey, new PlayerDeleteOptions()));
                            }
                            catch (CloudSaveException e)
                            {
                                if (!IsNotFound(e)) throw;
                                Logger.Log($"Metadata key not found in Data API (ignored): {metaKey}", LogCategory.SaveSystem, LogLevel.Info);
                            }
                            #if !UNITY_WEBGL || UNITY_EDITOR
            try { await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Files.Player.DeleteAsync(metaKey)); } catch {}
                            #endif
#endif
                            break;
                        case CloudSaveTransport.JSON:
#if REMEMBERME_CLOUDSAVE_PRESENT
                            try
                            {
                                await RunOnMainThreadAsync(async () =>
                                    await UnityCloudSaveService.Instance.Data.Player.DeleteAsync(metaKey, new PlayerDeleteOptions()));
                            }
                            catch (CloudSaveException e)
                            {
                                if (!IsNotFound(e)) throw;
                                Logger.Log($"Metadata key not found in Data API (ignored): {metaKey}", LogCategory.SaveSystem, LogLevel.Info);
                            }
                            #if !UNITY_WEBGL || UNITY_EDITOR
            try { await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Files.Player.DeleteAsync(metaKey)); } catch {}
                            #endif
#endif
                            break;
                    }
                }
            }
#endif

	        /* 2 Local mirror delete */
	        await DeleteLocalDataAsync(slot, safeKey);

            }
            catch (Exception ex)
            {
	        Logger.Log($"Failed to delete slot {slot.SlotNumber}: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveSystem, LogLevel.Error);
	        throw;
	    }
	}

		/// <summary>
		/// Saves the serialized data to a specific save slot using the configured method.
		/// </summary>
		/// <param name="data">Serialized byte array.</param>
		/// <param name="slot">The save slot to save to.</param>
        internal static byte[] SerializeSlotMetadata(SaveSettings settings, SaveSlot slot)
        {
            var bytes = SaveDataSerializer.Instance.Serialize(slot);
            if (settings.enableEncryption && settings.encryptSlotMetadata &&
                !(settings.enableCloudSave && settings.cloudCryptoMode == CloudCryptoMode.ServerSide))
            {
                var encryption = SaveManager.Instance?.EncryptionService ?? throw new InvalidOperationException("Metadata encryption requires an initialized encryption service.");
                bytes = encryption.MaybeEncrypt(bytes);
            }
            return bytes;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        Action CaptureBrowserRollback(SaveSlot slot)
        {
            if (settings.saveMethod == SaveMethod.PlayerPrefs)
            {
                string key = ResolveSaveKey(slot.SlotNumber), metaKey = settings.saveKey + "_Metadata_" + slot.SlotNumber;
                bool hadData = PlayerPrefs.HasKey(key), hadMeta = PlayerPrefs.HasKey(metaKey);
                string data = PlayerPrefs.GetString(key), meta = PlayerPrefs.GetString(metaKey);
                return () => { if (hadData) PlayerPrefs.SetString(key, data); else PlayerPrefs.DeleteKey(key); if (hadMeta) PlayerPrefs.SetString(metaKey, meta); else PlayerPrefs.DeleteKey(metaKey); PlayerPrefs.Save(); };
            }
            string folder = EnsureSlotFolder(slot.SlotNumber);
            string payload = Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav"), metadata = Path.Combine(folder, GetSlotMetadataFileName(slot));
            byte[] oldPayload = File.Exists(payload) ? File.ReadAllBytes(payload) : null, oldMetadata = File.Exists(metadata) ? File.ReadAllBytes(metadata) : null;
            return () => { if (oldPayload == null) DeleteFile(payload); else WriteAllBytes(payload, oldPayload); if (oldMetadata == null) DeleteFile(metadata); else WriteAllBytes(metadata, oldMetadata); };
        }
#endif
        void SaveLocalPair(byte[] data, SaveSlot slot, byte[] metadata)
        {
            SaveIoScope.CheckCancellation();
            ValidateSlotFilePaths(slot);
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            if (settings.saveMethod == SaveMethod.PlayerPrefs)
            {
                string key = ResolveSaveKey(slot.SlotNumber), metaKey = settings.saveKey + "_Metadata_" + slot.SlotNumber;
                bool hadData = PlayerPrefs.HasKey(key), hadMetadata = PlayerPrefs.HasKey(metaKey);
                string oldData = PlayerPrefs.GetString(key), oldMeta = PlayerPrefs.GetString(metaKey);
                try { PlayerPrefs.SetString(key, Convert.ToBase64String(data)); PlayerPrefs.SetString(metaKey, Convert.ToBase64String(metadata)); PlayerPrefs.Save(); }
                catch
                {
                    if (hadData) PlayerPrefs.SetString(key, oldData); else PlayerPrefs.DeleteKey(key);
                    if (hadMetadata) PlayerPrefs.SetString(metaKey, oldMeta); else PlayerPrefs.DeleteKey(metaKey);
                    PlayerPrefs.Save(); throw;
                }
                return;
            }
            string folder = EnsureSlotFolder(slot.SlotNumber);
            string payloadPath = Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav");
            string metadataPath = Path.Combine(folder, GetSlotMetadataFileName(slot));
#if UNITY_WEBGL && !UNITY_EDITOR
            byte[] priorPayload = File.Exists(payloadPath) ? File.ReadAllBytes(payloadPath) : null;
            byte[] priorMetadata = File.Exists(metadataPath) ? File.ReadAllBytes(metadataPath) : null;
            try
            {
                WriteAllBytes(payloadPath, data);
                WriteAllBytes(metadataPath, metadata);
                SaveFileTransaction.FaultInjection?.Invoke("committed");
            }
            catch
            {
                if (priorPayload == null) DeleteFile(payloadPath); else WriteAllBytes(payloadPath, priorPayload);
                if (priorMetadata == null) DeleteFile(metadataPath); else WriteAllBytes(metadataPath, priorMetadata);
                throw;
            }
#else
            SaveFileTransaction.Commit(persistentPath, slot.SlotNumber, payloadPath, data, metadataPath, metadata);
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
            CleanupObsoleteFiles(slot);
#endif
        }

        void CleanupObsoleteFiles(SaveSlot slot)
        {
            if (Verification.IsActive(slot.SlotNumber)) return;
            if (settings.saveMethod == SaveMethod.PlayerPrefs) return;
            string folder = EnsureSlotFolder(slot.SlotNumber);
            string payloadPath = Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav");
            string metadataPath = Path.Combine(folder, GetSlotMetadataFileName(slot));
            // Obsolete filenames remain available until the complete new generation is committed.
            try
            {
                var obsolete = LocalPayloadFiles(slot, true).Concat(LocalMetadataFiles(slot, true))
                    .Where(file => Path.GetFullPath(file.PrimaryPath) != Path.GetFullPath(payloadPath) &&
                                   Path.GetFullPath(file.PrimaryPath) != Path.GetFullPath(metadataPath))
                    .Select(file => file.Path).Distinct().ToArray();
                foreach (string file in obsolete) DeleteFile(file);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            { Logger.Log("Save committed; obsolete file cleanup will be retried: " + ex.Message, LogLevel.Warning); }
        }

                public void Save(byte[] data, SaveSlot slot)
                {
            SaveIoScope.CheckCancellation();
            if (!UseLocalMirror)
            {
                if (settings.enableCloudSave) UnityMainThreadDispatcher.Instance().Enqueue(async () => { try { await SaveAsync(data, slot); } catch (Exception ex) { Logger.Log(ex.Message, LogLevel.Error); } });
                return;
            }
            SaveLocalPair(data, slot, SerializeSlotMetadata(settings, slot));
        }

		/// <summary>
		/// Loads the serialized data from a specific save slot using the configured method.
		/// </summary>
		/// <param name="slot">The save slot to load from.</param>
		/// <returns>Serialized byte array.</returns>
		public byte[] Load(SaveSlot slot)
		{
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            SaveIoScope.CheckCancellation();
                        if (!UseLocalMirror)
                        {
                                Logger.Log("Load() skipped – local mirror disabled.", LogCategory.SaveSystem, LogLevel.Info);
                                return null;
                        }

			try
			{
				if (settings.saveMethod == SaveMethod.PlayerPrefs)
				{
					string fullKey = ResolveExistingPlayerPrefsKey(slot.SlotNumber);
					if (PlayerPrefs.HasKey(fullKey))
					{
						string base64Data = PlayerPrefs.GetString(fullKey);
						byte[] data = Convert.FromBase64String(base64Data);
						Logger.Log($"Loaded data from PlayerPrefs slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Info);
						return data;
					}
					else
					{
						Logger.Log($"No saved data found in PlayerPrefs slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Info);
						return null;
					}
				}
                else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
                {
                    string path = FindCommittedPayloadPath(slot);
                    return path == null ? null : ReadAllBytes(path);
                }
				return null;
			}
			catch (Exception ex)
			{
				Logger.Log($"Failed to load data from slot {slot.SlotNumber}. Exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveSystem, LogLevel.Error);
				throw;
			}
		}

		/// <summary>
		/// Deletes the saved data from a specific save slot using the configured method.
		/// Also deletes the associated screenshot (if enabled) and the slot's metadata.
		/// On WebGL this schedules the acknowledged asynchronous operation and logs failures.
		/// Await <see cref="DeleteAsync"/> to observe completion and failure directly.
		/// </summary>
		/// <param name="slot">The save slot to delete.</param>
		public void Delete(SaveSlot slot)
		{
#if UNITY_WEBGL && !UNITY_EDITOR
            // Browser durability cannot complete synchronously. Route before touching
            // files or preferences so the asynchronous mutation owns rollback as well.
            var request = SaveManager.CopySlot(slot);
            UnityMainThreadDispatcher.Instance().Enqueue(async () =>
            {
                try { await DeleteAsync(request); }
                catch (Exception ex)
                {
                    Logger.Log($"Delete() asynchronous operation failed: {ex}", LogCategory.SaveSystem, LogLevel.Error);
                }
            });
#else
            Verification.Discard(slot.SlotNumber);
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            SaveIoScope.CheckCancellation();
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            DeleteNativeSlotGenerations(slot);
            if (!UseLocalMirror)
            {
                // Legacy UnityEvent-compatible entry point. Never block the Unity thread:
                // cloud SDKs and local PlayerPrefs cleanup both require it to keep updating.
                UnityMainThreadDispatcher.Instance().Enqueue(async () =>
                {
                    try { await DeleteAsync(slot); }
                    catch (Exception ex)
                    {
                        Logger.Log($"Delete() asynchronous operation failed: {ex}", LogCategory.SaveSystem, LogLevel.Error);
                    }
                });
                return;
            }

            string safeKey = Sanitize(settings.saveKey) + slot.SlotNumber;
            DeleteFile(Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".sav"));
            DeleteFile(Path.Combine(EnsureSlotFolder(slot.SlotNumber), safeKey + ".json"));

			try
			{
				// 1) Delete the main save data
				if (settings.saveMethod == SaveMethod.PlayerPrefs)
				{
					// Main data key
					string fullKey = ResolveSaveKey(slot.SlotNumber);
                    string legacyKey = settings.saveKey + slot.SlotNumber;
                    if (legacyKey != fullKey) PlayerPrefs.DeleteKey(legacyKey);
                    PlayerPrefs.DeleteKey(settings.saveKey + slot.SlotNumber + "_bak");
                    PlayerPrefs.DeleteKey(fullKey + "_bak");
                    PlayerPrefs.Save();
					if (PlayerPrefs.HasKey(fullKey))
					{
						PlayerPrefs.DeleteKey(fullKey);
						PlayerPrefs.Save();
						Logger.Log($"Deleted saved data from PlayerPrefs slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Info);
					}
					else
					{
						Logger.Log($"No saved data found in PlayerPrefs slot {slot.SlotNumber} to delete.", LogCategory.SaveSystem, LogLevel.Warning);
					}

					string metaKey = settings.saveKey + "_Metadata_" + slot.SlotNumber;
					if (PlayerPrefs.HasKey(metaKey))
					{
						PlayerPrefs.DeleteKey(metaKey);
						PlayerPrefs.Save();
						Logger.Log($"Deleted metadata for PlayerPrefs slot {slot.SlotNumber} (key: {metaKey}).", LogCategory.SaveSystem, LogLevel.Info);
					}
					else
					{
						Logger.Log($"No metadata found in PlayerPrefs for slot {slot.SlotNumber} to delete.", LogCategory.SaveSystem, LogLevel.Warning);
					}
				}

				// 2) Delete screenshot if enabled and exists
				if (settings.enableScreenshots && ScreenshotManager.IsValidScreenshotFileName(slot.ScreenshotFileName))
				{
					string screenshotPath = Path.Combine(
						persistentPath,
						settings.screenshotFolderName,
						slot.ScreenshotFileName
					);
                                        if (FileExists(screenshotPath))
                                        {
                                                DeleteFile(screenshotPath);
                                                Logger.Log($"Deleted screenshot '{screenshotPath}' for slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Info);
                                        }
					else
					{
						Logger.Log($"No screenshot file '{screenshotPath}' found to delete for slot {slot.SlotNumber}.", LogCategory.SaveSystem, LogLevel.Warning);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Log($"Failed to delete saved data from slot {slot.SlotNumber}. Exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveSystem, LogLevel.Error);
				throw;
			}
#endif
		}

		#region Metadata
		public string GetSaveGamesPath()
		{
			return persistentPath;
		}

		/// <summary>
		/// Asynchronously saves the entire SaveSlot object as "metadata"
		/// (i.e. just the fields in SaveSlot, not the entire game state)
		/// using MemoryPack.
		/// </summary>
        public async Task SaveSlotMetadataAsync(SaveSlot slot)
        {
            ValidateSlotFilePaths(slot);
#if UNITY_WEBGL && !UNITY_EDITOR
            var rollback = CaptureBrowserRollback(slot);
            try { await SaveSlotMetadataCoreAsync(slot, true); await WebGLFileSystem.FlushAsync(); }
            catch { rollback(); try { await WebGLFileSystem.FlushAsync(); } catch { } throw; }
#else
            await SaveSlotMetadataCoreAsync(slot, true);
#endif
        }

        async Task SaveSlotMetadataCoreAsync(SaveSlot slot, bool writeLocal, bool? plannedRemote = null)
        {
            if (plannedRemote == true && (!settings.enableCloudSave || !SignedIn))
                throw new InvalidOperationException("Cloud authentication became unavailable before the save metadata could be written.");
            var remoteSlot = slot;
            bool writeLocalGeneration = writeLocal;
            var update = SaveIoScope.Current.Value?.MetadataUpdate;
            if (writeLocal && settings.enableCloudSave && update != null)
            {
                var stored = await LoadLocalSlotMetadataOnlyAsync(slot.SlotNumber);
                bool hasPayload = settings.saveMethod == SaveMethod.PlayerPrefs
                    ? await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                        PlayerPrefs.HasKey(Sanitize(ResolveSaveKey(slot.SlotNumber))) ||
                        PlayerPrefs.HasKey(Sanitize(settings.saveKey + slot.SlotNumber)))
                    : FindCommittedPayloadPath(slot) != null;
                writeLocalGeneration = stored != null || !hasPayload;
                remoteSlot = update.Apply(await LoadCloudSlotMetadataOnlyAsync(slot.SlotNumber), slot.SlotNumber);
                slot = update.Apply(stored, slot.SlotNumber);
            }
            ValidateSlotFilePaths(slot);
            byte[] metaBytes = SerializeSlotMetadata(settings, slot);
                        if (writeLocalGeneration && (!settings.enableCloudSave || UseLocalMirror))
                        {
                                if (settings.saveMethod == SaveMethod.PlayerPrefs)
                                {
                                        string metaKey = settings.saveKey + "_Metadata_" + slot.SlotNumber;

                                        // MAIN THREAD write
                                        await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                                        {
                                                string base64 = Convert.ToBase64String(metaBytes);
                                                PlayerPrefs.SetString(metaKey, base64);
                                                PlayerPrefs.Save();
                                                Logger.Log($"[SaveSlotMetadataAsync] Stored slot metadata in PlayerPrefs key: {metaKey}",
                                                                       LogCategory.SaveSystem,
                                                                       LogLevel.Info);
                                        });
                                }
                                else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
                                {
                                        string metaFileName = GetSlotMetadataFileName(slot);
                                        string metaFilePath = Path.Combine(EnsureSlotFolder(slot.SlotNumber), metaFileName);

                                        await WriteAllBytesAsync(metaFilePath, metaBytes);
                                        Logger.Log($"[SaveSlotMetadataAsync] Stored slot metadata file at: {metaFilePath}",
                                                                       LogCategory.SaveSystem,
                                                                       LogLevel.Info);
                                        // Sweep previous metadata variants for this slot (pattern-based)
                                        try
                                        {
                                            string pat = settings.metadataFileNamePattern;
                                            if (string.IsNullOrWhiteSpace(pat) || !pat.Contains("{n}"))
                                                pat = "Slot{n}_Meta.bin";
                                            string glob = PatternToGlob(pat, slot.SlotNumber);
                                            foreach (var f in Directory.GetFiles(SlotFolder(slot.SlotNumber), glob))
                                            {
                                                if (!string.Equals(Path.GetFullPath(f), Path.GetFullPath(metaFilePath), StringComparison.OrdinalIgnoreCase))
                                                {
                                                    if (FileExists(f)) DeleteFile(f);
                                                }
                                            }
                                        }
                                        catch {}
                                }
                        }
            if (settings.cloudSaveMetadata && (plannedRemote ?? (settings.enableCloudSave && SignedIn)))
            {
                byte[] remoteBytes = SerializeSlotMetadata(settings, remoteSlot);
                // Always store metadata via Data API as base64 string for consistency
                string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + slot.SlotNumber;
                string b64 = Convert.ToBase64String(remoteBytes);
#if REMEMBERME_CLOUDSAVE_PRESENT
                await RunOnMainThreadAsync(async () =>
                    await UnityCloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>{{metaKey, b64}}));
                // Optional: also save to Files API for backward compatibility with older clients
                #if !UNITY_WEBGL || UNITY_EDITOR
                try { await RunOnMainThreadAsync(async () =>
                        await UnityCloudSaveService.Instance.Files.Player.SaveAsync(metaKey, remoteBytes)); } catch {}
                #endif
#endif
            }
                }

		/// <summary>
		/// Asynchronously loads the entire SaveSlot object from disk or PlayerPrefs
		/// using MemoryPack. Returns null if it doesn't exist.
		/// </summary>
                public async Task<SaveSlot> LoadSlotMetadataAsync(int slotNumber)
                {
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            SaveIoScope.CheckCancellation();
                        if (settings.saveMethod == SaveMethod.PlayerPrefs)
                        {
				string metaKey = settings.saveKey + "_Metadata_" + slotNumber;

				// MAIN THREAD read
				string base64 = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
					PlayerPrefs.GetString(metaKey, null));

				if (string.IsNullOrEmpty(base64))
				{
                    // No metadata present for this slot in PlayerPrefs – treat as empty without logging
                    return null;
				}

	                        try
	                        {
	                                byte[] bytes = Convert.FromBase64String(base64);
                                    // Decrypt if needed
                                    try
                                    {
                                        var enc = SaveManager.Instance?.EncryptionService;
                                        if (enc?.UseEncryption == true)
                                            bytes = enc.MaybeDecrypt(bytes);
                                    }
                                    catch { }
	                                var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
	                                if (slot != null && slot.CustomMetadata == null)
	                                        slot.CustomMetadata = new Dictionary<string, string>();
	                                return slot;
	                        }
	                        catch (Exception ex)
	                        {
	                                Logger.Log($"[LoadSlotMetadataAsync] Failed to deserialize slot {slotNumber}: {ex.Message}\nStack Trace: {ex.StackTrace}",
	                                                   LogCategory.SaveSystem,
	                                                   LogLevel.Error);
	                                return null;
	                        }
                        }
                        else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
                        {
                                string metaFilePath = FindCommittedMetadataPath(new SaveSlot(slotNumber, null, default, null, null));
                                if (metaFilePath == null) return null;

                                try
                                {
                                        byte[] bytes = await ReadAllBytesAsync(metaFilePath);
                                        // Decrypt if needed
                                        try
                                        {
                                            var enc = SaveManager.Instance?.EncryptionService;
                                            if (enc?.UseEncryption == true)
                                                bytes = enc.MaybeDecrypt(bytes);
                                        }
                                        catch { }
                                        var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
	                                if (slot != null && slot.CustomMetadata == null)
	                                        slot.CustomMetadata = new Dictionary<string, string>();
	                                return slot;
	                        }
                                catch (Exception ex)
                                {
                                        Logger.Log($"[LoadSlotMetadataAsync] Failed to deserialize {metaFilePath}: {ex.Message}\nStack Trace: {ex.StackTrace}",
                                                           LogLevel.Error);
                                        return null;
                                }
                        }

            if (settings.enableCloudSave && settings.cloudSaveMetadata && SignedIn)
            {
                string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + slotNumber;
                byte[] bytes = null;
#if REMEMBERME_CLOUDSAVE_PRESENT
                // Primary: Data API
                var res = await RunOnMainThreadAsync(async () =>
                    await UnityCloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>{metaKey}));
                if (res != null && res.TryGetValue(metaKey, out var e))
                    bytes = Convert.FromBase64String(e.Value.GetAs<string>());
                #if !UNITY_WEBGL || UNITY_EDITOR
                if (bytes == null || bytes.Length == 0)
                {
                    // Fallback: Files API (legacy)
            try { bytes = await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(metaKey)); } catch {}
                }
                #endif
#endif
                if (bytes != null && bytes.Length > 0)
                {
                    // Decrypt if needed
                    try
                    {
                        var enc = SaveManager.Instance?.EncryptionService;
                        if (enc?.UseEncryption == true)
                            bytes = enc.MaybeDecrypt(bytes);
                    }
                    catch { }
                    var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
                    if (slot != null && slot.CustomMetadata == null)
                        slot.CustomMetadata = new Dictionary<string, string>();
                    return slot;
                }
            }

                        return null;
                }

        string[] VerificationKeys(SaveSlot slot) => new[] { ResolveSaveKey(slot.SlotNumber), settings.saveKey + "_Metadata_" + slot.SlotNumber };

        public void BackupSlot(SaveSlot slot)
        {
            if (!WritesLocalSave || !settings.enableSaveFileVerification) return;
            PrepareRollbackSlot(slot);
        }

        void PrepareRollbackSlot(SaveSlot slot)
        {
            if (!WritesLocalSave) throw new InvalidOperationException("Rotating save rollback requires an enabled local storage generation.");
            ValidateSlotFilePaths(slot);
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            if (settings.saveMethod == SaveMethod.PlayerPrefs)
            {
                Verification.BeginPreferences(slot.SlotNumber, ResolveExistingPlayerPrefsKey(slot.SlotNumber), ResolveSaveKey(slot.SlotNumber), settings.saveKey + "_Metadata_" + slot.SlotNumber);
                return;
            }
            string metadata = FindCommittedMetadataPath(slot);
            var committedSlot = metadata == null ? slot : ConflictCandidateReader.Metadata(File.ReadAllBytes(metadata));
            string payload = FindCommittedPayloadPath(committedSlot);
            string metadataPrimary = metadata == null ? null : LocalMetadataFiles(slot, false)
                .FirstOrDefault(file => file.Path == metadata).PrimaryPath ?? metadata;
            string payloadPrimary = payload == null ? null : LocalPayloadFiles(committedSlot, false)
                .FirstOrDefault(file => file.Path == payload).PrimaryPath ?? payload;
            string folder = EnsureSlotFolder(slot.SlotNumber);
            // Browser discovery can select a legacy .previous file without recreating its
            // primary. Preserve that provenance and exact pre-write state for rollback.
            Verification.BeginFileGenerations(slot.SlotNumber, payload, payloadPrimary, metadata, metadataPrimary,
                Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav"), Path.Combine(folder, GetSlotMetadataFileName(slot)));
        }

        public void SaveSlotMetadata(SaveSlot slot)
        {
            ValidateSlotFilePaths(slot);
            byte[] metaBytes = SerializeSlotMetadata(settings, slot);
            if (!settings.enableCloudSave || UseLocalMirror)
            {
                if (settings.saveMethod == SaveMethod.PlayerPrefs)
                {
                    string metaKey = settings.saveKey + "_Metadata_" + slot.SlotNumber;
                    string base64 = Convert.ToBase64String(metaBytes);
                    PlayerPrefs.SetString(metaKey, base64);
                    PlayerPrefs.Save();
                    Logger.Log($"[SaveSlotMetadata] Stored slot metadata in PlayerPrefs key: {metaKey}", LogCategory.SaveSystem, LogLevel.Info);
                }
                else if (settings.saveMethod == SaveMethod.BinaryFileFormat)
                {
                    string metaFileName = GetSlotMetadataFileName(slot);
                    string folder = EnsureSlotFolder(slot.SlotNumber);
                    string metaFilePath = Path.Combine(folder, metaFileName);
                    WriteAllBytes(metaFilePath, metaBytes);
                    Logger.Log($"[SaveSlotMetadata] Stored slot metadata file at: {metaFilePath}", LogCategory.SaveSystem, LogLevel.Info);

                    // Sweep previous metadata variants for this slot (pattern-based)
                    try
                    {
                        string pat = settings.metadataFileNamePattern;
                        if (string.IsNullOrWhiteSpace(pat) || !pat.Contains("{n}"))
                            pat = "Slot{n}_Meta.bin";
                        string glob = PatternToGlob(pat, slot.SlotNumber);
                        foreach (var f in Directory.GetFiles(folder, glob))
                        {
                            if (!string.Equals(Path.GetFullPath(f), Path.GetFullPath(metaFilePath), StringComparison.OrdinalIgnoreCase))
                            {
                                if (FileExists(f)) DeleteFile(f);
                            }
                        }
                    }
                    catch { }
                }
            }
            else
            {
                Logger.Log("[SaveSlotMetadata] Local metadata skipped – cloud only mode.", LogCategory.SaveSystem, LogLevel.Info);
            }

            if (settings.enableCloudSave && settings.cloudSaveMetadata && SignedIn)
            {
                // Always prefer Data API with base64, keep Files API as best-effort fallback
                string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + slot.SlotNumber;
                string b64 = Convert.ToBase64String(metaBytes);
#if REMEMBERME_CLOUDSAVE_PRESENT
        try { _ = RunOnMainThreadAsync(async () =>
            await UnityCloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>{{metaKey, b64}})); }
        catch (Exception ex) { Logger.Log($"[SaveSlotMetadata] UnityCloudSave Data save failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveSystem, LogLevel.Warning); }
        try { _ = RunOnMainThreadAsync(async () =>
            await UnityCloudSaveService.Instance.Files.Player.SaveAsync(metaKey, metaBytes)); } catch {}
#endif
            }
        }

        public void RestoreBackup(SaveSlot slot)
        {
            if (!Verification.IsActive(slot.SlotNumber) && (!WritesLocalSave || !settings.enableSaveFileVerification)) return;
            ValidateSlotFilePaths(slot);
            if (Verification.Restore(slot.SlotNumber)) return;
            if (settings.saveMethod == SaveMethod.BinaryFileFormat)
            {
                string folder = EnsureSlotFolder(slot.SlotNumber);
                string payload = Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav");
                string metadata = Path.Combine(folder, GetSlotMetadataFileName(slot));
                if (!FileExists(payload + ".bak"))
                {
                    if (Verification.IsActive(slot.SlotNumber)) throw new IOException("Required verification backup is missing.");
                    return; // No verification is in flight; preserve the legacy optional restore call.
                }
                var data = ReadAllBytes(payload + ".bak");
                if (FileExists(metadata + ".bak")) SaveLocalPair(data, slot, ReadAllBytes(metadata + ".bak"));
                else CopyFile(payload + ".bak", payload, true); // Existing payload-only verification backup.
            }
            else
            {
                foreach (string key in VerificationKeys(slot))
                    if (PlayerPrefs.HasKey(key + "_bak")) PlayerPrefs.SetString(key, PlayerPrefs.GetString(key + "_bak"));
                PlayerPrefs.Save();
            }
        }

        public void DeleteBackup(SaveSlot slot)
        {
            if (!Verification.IsActive(slot.SlotNumber) && (!WritesLocalSave || !settings.enableSaveFileVerification)) return;
            ValidateSlotFilePaths(slot);
            if (Verification.IsActive(slot.SlotNumber)) { Verification.Complete(slot.SlotNumber); return; }
            if (settings.saveMethod == SaveMethod.BinaryFileFormat)
            {
                string folder = EnsureSlotFolder(slot.SlotNumber);
                DeleteFile(Path.Combine(folder, ResolveSaveFileStem(slot) + ".sav.bak"));
                DeleteFile(Path.Combine(folder, GetSlotMetadataFileName(slot) + ".bak"));
            }
            else
            {
                foreach (string key in VerificationKeys(slot)) PlayerPrefs.DeleteKey(key + "_bak");
                PlayerPrefs.Save();
            }
        }

        /* ================================================================== */
        /*  Temporary files (verification without mirror)                     */
        /* ================================================================== */

        string TempPathFor(SaveSlot slot)
        {
            ValidateSlotFilePaths(slot);
            return Path.Combine(EnsureSlotFolder(slot.SlotNumber), $"{ResolveSaveFileStem(slot)}.tmp");
        }

        public async Task WriteTempAsync(byte[] data, SaveSlot slot)
        {
            string path = TempPathFor(slot);
            await WriteAllBytesAsync(path, data);
        }

        public async Task<byte[]> LoadTempAsync(SaveSlot slot)
        {
            string path = TempPathFor(slot);
            if (!FileExists(path)) return null;
            return await ReadAllBytesAsync(path);
        }

        public void DeleteTemp(SaveSlot slot)
        {
            string path = TempPathFor(slot);
            if (FileExists(path)) DeleteFile(path);
        }

        /* ------------------------------------------------------------------ */
        /*  Remote slot listing (Unity Cloud Save)                            */
        /* ------------------------------------------------------------------ */

        public async Task<List<SaveSlot>> ListRemoteSlotsAsync()
        {
            var list = new List<SaveSlot>();

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSystem] WebGL: ListRemoteSlotsAsync started - enableCloudSave: {settings.enableCloudSave}, SignedIn: {SignedIn}, cloudSaveMetadata: {settings.cloudSaveMetadata}");
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            Debug.Log($"[SaveSystem] WebGL: AuthenticationService.Instance.IsSignedIn: {Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn}");
            Debug.Log($"[SaveSystem] WebGL: AuthenticationService.Instance.PlayerId: {Unity.Services.Authentication.AuthenticationService.Instance.PlayerId}");
#else
            Debug.Log("[SaveSystem] WebGL: REMEMBERME_AUTHENTICATION_PRESENT and/or REMEMBERME_CORESERVICES_PRESENT not defined!");
#endif
#endif

            if (!settings.enableCloudSave || !SignedIn || !settings.cloudSaveMetadata)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SaveSystem] WebGL: Returning empty list - enableCloudSave: {settings.enableCloudSave}, SignedIn: {SignedIn}, cloudSaveMetadata: {settings.cloudSaveMetadata}");
#endif
                return list;
            }

#if REMEMBERME_CLOUDSAVE_PRESENT
            string prefix = CloudSanitize(settings.saveKey) + "_Meta_";
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSystem] WebGL: Using prefix: {prefix}, transport: {settings.cloudSaveTransport}");
#endif

            // First pass: collect all keys to identify screenshots (WebGL/Data API path)
            var allKeys = new List<string>();
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                var keysResult = await RunOnMainThreadAsync(async () =>
                    await UnityCloudSaveService.Instance.Data.Player.ListAllKeysAsync());
                allKeys = keysResult.Select(k => k.Key).ToList();
                Debug.Log($"[SaveSystem] WebGL: Found {allKeys.Count} total keys in cloud save");
            }
            catch (Exception ex)
            {
                Debug.Log($"[SaveSystem] WebGL: Failed to list all keys for screenshot matching: {ex.Message}\nStack Trace: {ex.StackTrace}");
                allKeys = new List<string>();
            }
#endif
            // Use Data API listing for metadata keys regardless of transport
            var keys = await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Data.Player.ListAllKeysAsync());
            foreach (var k in keys)
            {
                if (!k.Key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                var res = await RunOnMainThreadAsync(async () =>
                    await UnityCloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { k.Key }));
                if (res != null && res.TryGetValue(k.Key, out var e))
                {
                    string b64 = e.Value.GetAs<string>();
                    if (!string.IsNullOrEmpty(b64))
                    {
                        byte[] bytes = Convert.FromBase64String(b64);
                        try
                        {
                            var enc = SaveManager.Instance?.EncryptionService;
                            if (enc?.UseEncryption == true)
                                bytes = enc.MaybeDecrypt(bytes);
                        }
                        catch { }
                        var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
                        if (slot != null)
                            list.Add(slot);
                    }
                }
            }
#endif
#if !REMEMBERME_CLOUDSAVE_PRESENT
            await Task.CompletedTask;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSystem] WebGL: ListRemoteSlotsAsync completed, returning {list.Count} slots before grouping");

            // Match screenshot keys to slots
            #if REMEMBERME_CLOUDSAVE_PRESENT
            // Always populate the latest screenshot key from cloud for Unity Cloud Save so the UI can fetch it on reload.
            foreach (var slot in list)
            {
                Debug.Log($"[SaveSystem] WebGL: Checking slot {slot.SlotNumber} for screenshot matches");

                // Look for screenshot keys that match this slot
                var screenshotKeys = allKeys.Where(k => k.StartsWith("screenshot_Slot_" + slot.SlotNumber + "_")).ToList();

                if (screenshotKeys.Any())
                {
                    // Use the most recent screenshot key (assuming timestamp in filename)
                    var latestScreenshotKey = screenshotKeys.OrderByDescending(k => k).First();
                    Debug.Log($"[SaveSystem] WebGL: Found screenshot key for slot {slot.SlotNumber}: {latestScreenshotKey}");
                    slot.ScreenshotFileName = latestScreenshotKey;
                }
                else
                {
                    Debug.Log($"[SaveSystem] WebGL: No screenshot key found for slot {slot.SlotNumber}");
                }
            }
            #endif
#endif

            // Legacy fallback: if some slots weren't found via Data API, try Files API for metadata
#if REMEMBERME_CLOUDSAVE_PRESENT
            #if !UNITY_WEBGL || UNITY_EDITOR
            try
            {
                var present = new HashSet<int>(list.Select(s => s.SlotNumber));
                for (int n = 1; n <= settings.numberOfSaveSlots; n++)
                {
                    if (present.Contains(n)) continue;
                    string metaKey = CloudSanitize(settings.saveKey) + "_Meta_" + n;
                    byte[] bytes = null;
            try { bytes = await RunOnMainThreadAsync(async () =>
                await UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(metaKey)); }
                    catch { bytes = null; }
                    if (bytes != null && bytes.Length > 0)
                    {
                        try
                        {
                            var enc = SaveManager.Instance?.EncryptionService;
                            if (enc?.UseEncryption == true)
                                bytes = enc.MaybeDecrypt(bytes);
                        }
                        catch { }
                        var slot = SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes);
                        if (slot != null) list.Add(slot);
                    }
                }
            }
            catch { }
            #endif
#endif

            var result = list
                .GroupBy(s => s.SlotNumber)
                .Select(g => g.OrderByDescending(x => x.LastSaved).First())
                .OrderBy(s => s.SlotNumber)
                .ToList();

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SaveSystem] WebGL: ListRemoteSlotsAsync final result: {result.Count} slots after grouping");
#endif

            return result;
        }

                #endregion
        }
}
#endif
