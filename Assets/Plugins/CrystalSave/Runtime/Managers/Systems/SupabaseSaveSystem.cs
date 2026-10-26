#if ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// REST-only Supabase backend.
	/// • Writes/reads full metadata as JSON via a pure-fields DTO
	/// • Mirrors .sav *and* MemoryPack header when keepLocalMirror = true
	/// • Keeps remote/local file-name scheme aligned (slot-N.sav / .meta.json)
	/// </summary>
	public sealed partial class SupabaseSaveSystem : ISaveSystem, ISaveConflictSource, ICloudRequestIdentitySource
	{
	/* ───────────── ctor inputs ───────────── */
	readonly string url;
	readonly string key;
	readonly string bucket;
	readonly string userDirStatic;
	readonly IUserFolderResolver liveResolver;
	readonly bool useJson;
	readonly string dataExt;
	readonly SaveSettings _cfg;

        readonly bool   keepLocalMirror;
        readonly string localFileStem;
        readonly string persistentPath;       // cache path on ctor (main thread)

	const int REQUEST_TIMEOUT_SECONDS = 10;

    const string META_EXT      = ".meta.json";        // cloud
    const string LEGACY_META_MASK = "Slot{0}_Meta.bin";  // local legacy fallback

	/* ───────────── DTO used only for JSON ───────────── */


    // Local metadata filename helpers (configurable pattern with legacy fallback)
    string MetaLocalFileName(int n)
    {
        string pattern = _cfg.metadataFileNamePattern;
        if (string.IsNullOrWhiteSpace(pattern) || !pattern.Contains("{n}"))
            pattern = LEGACY_META_MASK.Replace("{0}", "{n}");
        return pattern.Replace("{n}", n.ToString());
    }

    string SlotFolder(int n)
    {
        string configured = Path.Combine(persistentPath, _cfg.ResolveSlotFolderName(n));
        if (Directory.Exists(configured)) return configured;
        string legacy = Path.Combine(persistentPath, $"slot{n}");
        if (configured != legacy && Directory.Exists(legacy)) return legacy;
        return configured;
    }
    string EnsureSlotFolder(int n)
    {
        var dir = SlotFolder(n);
        try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch {}
        return dir;
    }

    string MetaLocalPath(int n) => Path.Combine(SlotFolder(n), MetaLocalFileName(n));
    string LegacyMetaLocalPath(int n) => Path.Combine(persistentPath, string.Format(LEGACY_META_MASK, n));

        static byte[] DecodeLocalMetadata(byte[] bytes)
        {
            if (!SaveCrypto.HasEnvelope(bytes)) return bytes;
            return SaveManager.Instance?.EncryptionService?.MaybeDecrypt(bytes) ?? throw new InvalidDataException("Metadata authentication failed.");
        }


    // Paths for remote objects
    string PathFor(SaveSlot s, string ext) => $"{UserDir}/{localFileStem}{s.SlotNumber}{ext}";
    string PathFor(int slot,   string ext) => $"{UserDir}/{localFileStem}{slot}{ext}";

    // Constructor
    public SupabaseSaveSystem(SaveSettings cfg, string persistentPath)
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[SupabaseSaveSystem] WebGL: Constructor started");
#endif
            _cfg = cfg;

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: SaveSettings loaded, userFolderStrategy: {cfg.userFolderStrategy}");
#endif

            url    = cfg.supabaseUrl.TrimEnd('/');
            key    = cfg.supabaseAnonKey;
            bucket = cfg.bucket;

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Basic config set - URL: {url}, Bucket: {bucket}");
#endif

            /* dynamic folder only for Strategy.Custom */
            if (cfg.userFolderStrategy == UserFolderStrategy.Custom &&
                cfg.customUserFolderResolver is IUserFolderResolver r)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SupabaseSaveSystem] WebGL: Using custom user folder resolver");
#endif
                liveResolver = r;               // keep reference – its value may change later
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log("[SupabaseSaveSystem] WebGL: Resolving static user folder...");
#endif
                userDirStatic = ResolveUserFolder(cfg);
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.Log($"[SupabaseSaveSystem] WebGL: User folder resolved: {userDirStatic}");
#endif
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Setting transport and file options...");
#endif
            useJson  = cfg.cloudSaveTransport == CloudSaveTransport.JSON;
            dataExt  = useJson ? ".json" : ".sav";

            keepLocalMirror = cfg.keepLocalMirror &&
                              cfg.saveMethod == SaveMethod.BinaryFileFormat;
            localFileStem   = cfg.saveFileName;
            this.persistentPath  = persistentPath;
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: Constructor completed successfully! KeepLocalMirror: {keepLocalMirror}, UseJson: {useJson}");
#endif
        }
        catch (System.Exception ex)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.LogError($"[SupabaseSaveSystem] WebGL: Constructor failed with exception: {ex.Message}\nStack Trace: {ex.StackTrace}");
            Debug.LogError($"[SupabaseSaveSystem] WebGL: Stack trace: {ex.StackTrace}");
#else
            Debug.LogError($"[SupabaseSaveSystem] Constructor failed with exception: {ex.Message}\nStack Trace: {ex.StackTrace}");
#endif
            throw;
        }
    }

    // LocalWritesEnabled: perform local file writes when cloud save is disabled
    // or when keepLocalMirror is requested.
    bool LocalWritesEnabled() => !_cfg.enableCloudSave || keepLocalMirror;

	/* ================================================================== */
	/* Slot data
	/* ================================================================== */

	public async Task SaveAsync(byte[] data, SaveSlot slot)
	{
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
        // 1 ─ blob
        // Ensure network upload does not block the Unity main thread; keep continuations
        // off the main thread and only use the dispatcher for brief UnityWebRequest creation.
        if (useJson)
            await UploadJson(PathFor(slot, dataExt), data).ConfigureAwait(SaveIoScope.PreserveUnityContext);
        else
            await UploadObject(PathFor(slot, dataExt), data, "application/octet-stream").ConfigureAwait(SaveIoScope.PreserveUnityContext);

            try
            {
                await SaveSlotMetadataCoreAsync(slot, false).ConfigureAwait(SaveIoScope.PreserveUnityContext);
                await WriteLocalMirrorAsync(data, slot);
            }
            catch (Exception ex)
            { throw new IOException("Remote payload was uploaded, but metadata or local persistence failed; the remote slot may be partially updated.", ex); }
        }

        public async Task<byte[]> LoadAsync(SaveSlot s)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            return useJson ? await DownloadJson(PathFor(s, dataExt)) : await DownloadObject(PathFor(s, dataExt));
        }

        public async Task DeleteAsync(SaveSlot s)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            Verification.Discard(s.SlotNumber);
            SaveIoScope.CheckCancellation();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            // cloud
            await DeleteObject(PathFor(s, dataExt)).ConfigureAwait(SaveIoScope.PreserveUnityContext);
            await DeleteObject(PathFor(s, META_EXT)).ConfigureAwait(SaveIoScope.PreserveUnityContext);

	    if (ScreenshotManager.IsValidScreenshotFileName(s.ScreenshotFileName))
                await DeleteScreenshotAsync(s.ScreenshotFileName).ConfigureAwait(SaveIoScope.PreserveUnityContext);
            await DeleteLocalSlotAsync(s);
        }

        public Task DeleteLocalSlotAsync(SaveSlot slot)
        {
            SaveIoScope.CheckCancellation();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            string image = ScreenshotManager.IsValidScreenshotFileName(slot.ScreenshotFileName) ? slot.ScreenshotFileName : null;
            return CloudMirrorFiles.DeleteAsync(new[]
            {
                Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav"),
                MetaLocalPath(slot.SlotNumber), LegacyMetaLocalPath(slot.SlotNumber),
                CloudMirrorFiles.LegacyScreenshotPath(persistentPath, image),
                image == null ? null : Path.Combine(persistentPath, _cfg.screenshotFolderName, image)
            });
        }

	/* ================================================================== */
	/* Metadata
	/* ================================================================== */

	public Task SaveSlotMetadataAsync(SaveSlot slot) => SaveSlotMetadataCoreAsync(slot, true);

        async Task SaveSlotMetadataCoreAsync(SaveSlot slot, bool writeLocal)
	{
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            SaveSlot local = null;
            if (writeLocal)
            {
                SaveFileTransaction.RecoverAll(persistentPath);
                Verification.Recover();
                if (LocalWritesEnabled())
                    local = SaveSlotMetadataUpdate.ResolveLocal(slot,
                        Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav"),
                        MetaLocalPath(slot.SlotNumber), LegacyMetaLocalPath(slot.SlotNumber));
                slot = await SaveSlotMetadataUpdate.ResolveRemoteAsync(slot, async () =>
                    CloudSlotMetadata.Decode(await DownloadObject(PathFor(slot.SlotNumber, META_EXT)), slot.SlotNumber));
            }
	    if (slot.SlotName == null)
	        slot.SlotName = $"Slot {slot.SlotNumber}";

	    UnityEngine.Debug.Log($"[SupabaseSaveSystem] SaveSlotMetadataAsync for slot {slot.SlotNumber}: Name='{slot.SlotName}', LastSaved={slot.LastSaved}, Screenshot='{slot.ScreenshotFileName ?? "null"}'");

	    // cloud JSON – all members survive because DTO uses fields
	    var dto = CloudSlotMetadata.FromSlot(slot);

        // Upload metadata JSON to cloud
        string metaJson = CloudMetadataJson.Encode(dto);
        await UploadObject(PathFor(slot, META_EXT), Encoding.UTF8.GetBytes(metaJson), "application/json").ConfigureAwait(SaveIoScope.PreserveUnityContext);
        UnityEngine.Debug.Log($"[SupabaseSaveSystem] Metadata uploaded to cloud for slot {slot.SlotNumber}");

            if (local != null)
                await CloudMirrorFiles.WriteMetadataAsync(MetaLocalPath(local.SlotNumber), SaveSystem.SerializeSlotMetadata(_cfg, local));
	}

        public async Task<SaveSlot> LoadSlotMetadataAsync(int num)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            SaveIoScope.CheckCancellation();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            var bytes = await DownloadObject(PathFor(num, META_EXT));
            SaveIoScope.CheckCancellation();
            if (bytes == null) return null;
            var slot = ReadRemoteMetadata(bytes);
            if (slot.SlotNumber != num)
                throw new InvalidDataException("Remote slot metadata has an unexpected slot number.");
            // A metadata refresh cannot replace the header of an older local payload.
            // Commit/explicit metadata writes and deletion own local mirror mutations.
            return slot;
        }

        static SaveSlot ReadRemoteMetadata(byte[] bytes) => CloudSlotMetadata.Decode(bytes);

        [Serializable] sealed class ListRequest
        {
            public string prefix;
            public int limit;
            public int offset;
            public ListOrder sortBy = new();
        }
        [Serializable] sealed class ListOrder
        {
            public string column = "name";
            public string order = "asc";
        }

        public async Task<List<SaveSlot>> ListRemoteSlotsAsync()
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            const int pageSize = 1000;
            string prefix = UserDir.TrimEnd('/') + "/";
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int offset = 0; ; offset = checked(offset + pageSize))
            {
                SaveIoScope.CheckCancellation();
                var page = await ReadRemoteListingPageAsync(prefix, offset, pageSize);
                int previousCount = names.Count;
                foreach (var item in page)
                {
                    if (string.IsNullOrEmpty(item.name) || item.name.Contains('/') || item.name.Contains('\\') ||
                        item.name == "." || item.name == "..")
                        throw new InvalidDataException("Remote listing contains an invalid relative object name.");
                    names.Add(item.name);
                }
                if (page.Length < pageSize) break;
                if (names.Count == previousCount)
                    throw new InvalidDataException("Remote listing did not advance to the next page.");
            }

            var slots = new List<SaveSlot>();
            foreach (string name in names.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!name.EndsWith(META_EXT, StringComparison.OrdinalIgnoreCase)) continue;
                SaveIoScope.CheckCancellation();
                var bytes = await DownloadObject(prefix + name);
                SaveIoScope.CheckCancellation();
                if (bytes == null) continue; // An object can be deleted between listing and reading.
                var slot = ReadRemoteMetadata(bytes);
                string stem = localFileStem + slot.SlotNumber;
                if (name != stem + META_EXT)
                    throw new InvalidDataException("Remote metadata does not match its object name.");
                if (names.Contains(stem + dataExt)) slots.Add(slot);
            }
            return slots.OrderBy(slot => slot.SlotNumber).ToList();
        }

        async Task<FileEntry[]> ReadRemoteListingPageAsync(string prefix, int offset, int limit)
        {
            var identity = await CaptureRequestIdentityAsync();
            var request = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var body = JsonUtility.ToJson(new ListRequest { prefix = prefix, offset = offset, limit = limit });
                var req = new UnityWebRequest($"{url}/storage/v1/object/list/{Escape(bucket)}", "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                req.SetRequestHeader("Content-Type", "application/json");
                AddAuth(req, identity.AccessKey);
                return req;
            });
            try
            {
                await Send(request).ConfigureAwait(SaveIoScope.PreserveUnityContext);
                return await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                {
                    SaveIoScope.CheckCancellation();
                    var response = JsonUtility.FromJson<ResponseWrap>("{\"items\":" + request.downloadHandler.text + "}");
                    if (response.items == null || response.items.Length > limit)
                        throw new InvalidDataException("Remote listing response is invalid.");
                    return response.items;
                });
            }
            finally { await UnityMainThreadDispatcher.Instance().EnqueueAsync(request.Dispose); }
        }

	/* ================================================================== */
	/* Metadata helpers
	/* ================================================================== */

    async Task WriteLocalMirrorAsync(byte[] data, SaveSlot s)
    {
            SaveIoScope.CheckCancellation();
            if (!LocalWritesEnabled()) return;
            string path = Path.Combine(persistentPath, $"{localFileStem}{s.SlotNumber}.sav");
            await SaveFileTransaction.CommitAsync(persistentPath, s.SlotNumber, path, data, MetaLocalPath(s.SlotNumber), SaveSystem.SerializeSlotMetadata(_cfg, s));
        }


        public void BackupSlot(SaveSlot slot)
        {
            if (!LocalWritesEnabled() || !_cfg.enableSaveFileVerification) return;
            PrepareRollbackSlot(slot);
        }

        void PrepareRollbackSlot(SaveSlot slot)
        {
            if (!LocalWritesEnabled()) throw new InvalidOperationException("Rotating save rollback requires an enabled local storage generation.");
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            string payload = Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav");
            string metadata = MetaLocalPath(slot.SlotNumber);
            CloudMirrorFiles.PrepareRollback(Verification, slot.SlotNumber, payload, metadata, LegacyMetaLocalPath(slot.SlotNumber));
        }

        public void RestoreBackup(SaveSlot slot)
        {
            if (!LocalWritesEnabled() || !_cfg.enableSaveFileVerification) return;
            if (Verification.Restore(slot.SlotNumber)) return;
            string path   = Path.Combine(persistentPath,
                                        $"{localFileStem}{slot.SlotNumber}.sav");
            string backup = path + ".bak";
            if (!File.Exists(backup)) throw new IOException("Required verification backup is missing.");
            if (File.Exists(backup))
            {
                string metadata = MetaLocalPath(slot.SlotNumber);
                if (AtomicSaveFile.Exists(metadata + ".bak"))
                    SaveFileTransaction.Commit(persistentPath, slot.SlotNumber, path, File.ReadAllBytes(backup), metadata, File.ReadAllBytes(metadata + ".bak"));
                else AtomicSaveFile.Copy(backup, path, true);
            }
        }

        public void DeleteBackup(SaveSlot slot)
        {
            if (!LocalWritesEnabled() || !_cfg.enableSaveFileVerification) return;
            if (Verification.IsActive(slot.SlotNumber)) { Verification.Complete(slot.SlotNumber); return; }
            string backup = Path.Combine(persistentPath,
                                        $"{localFileStem}{slot.SlotNumber}.sav.bak");
            AtomicSaveFile.Delete(backup);
            AtomicSaveFile.Delete(MetaLocalPath(slot.SlotNumber) + ".bak");
        }

        /* ================================================================== */
        /*  Temporary files (verification without mirror)                     */
        /* ================================================================== */

        string TempPathFor(SaveSlot slot) =>
            Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.tmp");

        public async Task WriteTempAsync(byte[] data, SaveSlot slot)
        {
            string path = TempPathFor(slot);
            #if UNITY_WEBGL && !UNITY_EDITOR
            AtomicSaveFile.WriteAllBytes(path, data);
            await WebGLFileSystem.FlushAsync();
#else
            await Task.Run(() => AtomicSaveFile.WriteAllBytes(path, data));
#endif
        }

        public async Task<byte[]> LoadTempAsync(SaveSlot slot)
        {
            string path = TempPathFor(slot);
            if (!File.Exists(path)) return null;
            #if UNITY_WEBGL && !UNITY_EDITOR
            await Task.Yield();
            return File.ReadAllBytes(path);
#else
            return await Task.Run(() => File.ReadAllBytes(path));
#endif
        }

        public void DeleteTemp(SaveSlot slot)
        {
            string path = TempPathFor(slot);
            AtomicSaveFile.Delete(path);
        }

	/* ================================================================== */
	/*  Screenshot API
	/* ================================================================== */

        public string GetSaveGamesPath() => persistentPath;

        public async Task UploadScreenshotAsync(string file, byte[] bytes, ScreenshotFormat fmt)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            await UploadObject($"{UserDir}/{file}", bytes, fmt == ScreenshotFormat.PNG ? "image/png" : "image/jpeg");
        }

        public async Task<byte[]> DownloadScreenshotAsync(string file)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            return await DownloadObject($"{UserDir}/{file}");
        }

        public async Task DeleteScreenshotAsync(string file)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            await DeleteObject($"{UserDir}/{file}");
        }

        [Serializable] struct ResponseWrap { public FileEntry[] items; }
        [Serializable] struct FileEntry { public string name; }

        Task UploadObject(string path, byte[] data, string mime)
            => SendObjectRequestAsync(UnityWebRequest.kHttpVerbPUT, path, data, mime);

        Task<byte[]> DownloadObject(string path)
            => SendObjectRequestAsync(UnityWebRequest.kHttpVerbGET, path, allow404: true);

        Task DeleteObject(string path)
            => SendObjectRequestAsync(UnityWebRequest.kHttpVerbDELETE, path, allow404: true);

        async Task<byte[]> SendObjectRequestAsync(string method, string path, byte[] body = null,
            string mime = null, bool allow404 = false)
        {
            var identity = await CaptureRequestIdentityAsync();
            var request = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var req = new UnityWebRequest($"{url}/storage/v1/object/{Escape(bucket)}/{Escape(path)}", method)
                {
                    downloadHandler = new DownloadHandlerBuffer()
                };
                if (body != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(body);
                    req.SetRequestHeader("Content-Type", mime);
                    req.SetRequestHeader("x-upsert", "true");
                }
                AddAuth(req, identity.AccessKey);
                return req;
            });
            try
            {
                await Send(request, allow404).ConfigureAwait(SaveIoScope.PreserveUnityContext);
                SaveIoScope.CheckCancellation();
                return await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                    request.result == UnityWebRequest.Result.Success ? request.downloadHandler.data ?? Array.Empty<byte>() : null);
            }
            finally { await UnityMainThreadDispatcher.Instance().EnqueueAsync(request.Dispose); }
        }

	async Task UploadJson(string path, byte[] bytes)
	{
	    string json = $"\"{Convert.ToBase64String(bytes)}\"";
            await UploadObject(path, Encoding.UTF8.GetBytes(json), "application/json").ConfigureAwait(SaveIoScope.PreserveUnityContext);
	}

	async Task<byte[]> DownloadJson(string path)
	{
            byte[] blob = await DownloadObject(path).ConfigureAwait(SaveIoScope.PreserveUnityContext);
	    if (blob == null || blob.Length == 0) return null;
	    string txt = Encoding.UTF8.GetString(blob).Trim().Trim('"');
	    return Convert.FromBase64String(txt);
	}

	static string Escape(string p) =>
	    string.Join("/", p.Split('/').Select(Uri.EscapeDataString));

        string UserDir
        {
            get { var identity = CloudRequestScope.Find(this); return identity != null ? identity.UserFolder : CaptureIdentity().UserFolder; }
        }
        Task<CloudRequestIdentity> ICloudRequestIdentitySource.CaptureRequestIdentityAsync() => CaptureRequestIdentityAsync();
        Task<CloudRequestIdentity> CaptureRequestIdentityAsync() => CloudRequestScope.CaptureAsync(this, CaptureIdentity,
            liveResolver != null || _cfg.customUserFolderResolver is IUserAuthorizationResolver);

        CloudRequestIdentity CaptureIdentity()
        {
            string bearer = key;
            if (_cfg.userFolderStrategy == UserFolderStrategy.Custom &&
                _cfg.customUserFolderResolver is IUserAuthorizationResolver customAuth)
            {
                string resolved = customAuth.ResolveAccessKey();
                if (!string.IsNullOrEmpty(resolved)) bearer = resolved;
            }
            return new CloudRequestIdentity(liveResolver != null ? liveResolver.ResolveUserFolder() : userDirStatic, bearer);
        }

        void AddAuth(UnityWebRequest r, string bearer)
        {
            r.SetRequestHeader("apikey", key);
            r.SetRequestHeader("Authorization", $"Bearer {bearer}");
        }

        static async Task Send(UnityWebRequest request, bool allow404 = false)
        {
            using var abort = SaveIoScope.AbortOnCancellation(request);
            SaveIoScope.CheckCancellation();
            var operation = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                request.timeout = REQUEST_TIMEOUT_SECONDS;
                return request.SendWebRequest();
            });
            while (!await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => operation.isDone))
                await Task.Yield();
            SaveIoScope.CheckCancellation();
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                if (request.result != UnityWebRequest.Result.Success && !(allow404 && request.responseCode == 404))
                    throw new IOException($"{request.method} {request.url} → {request.responseCode} {request.error}");
            });
        }

	/* ================================================================== */
	/*  Sync wrappers
	/* ================================================================== */

        // Retain the legacy fire-and-forget signatures; await their Async counterparts
        // when a caller needs completion. Start on Unity's context, including on WebGL.
        public void Save(byte[] data, SaveSlot slot) => ObserveLegacyRequest(SaveAsync(data, slot));

        public byte[] Load(SaveSlot slot)
        {
            CloudSaveThreading.RequireBackgroundThread(nameof(Load));
            return LoadAsync(slot).GetAwaiter().GetResult();
        }

        public void Delete(SaveSlot slot) => ObserveLegacyRequest(DeleteAsync(slot));

        static async void ObserveLegacyRequest(Task request)
        {
            try { await request; }
            catch (Exception ex) { Logger.Log("Supabase request failed: " + ex.Message, LogLevel.Error); }
        }

    /* ───────────── user-folder strategy (unchanged) ───────────── */

	const string DEVICE_GUID_PREF = "CrystalSave_DeviceGuid";

	static string ResolveUserFolder(SaveSettings cfg)
	{
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log($"[SupabaseSaveSystem] WebGL: ResolveUserFolder called with strategy: {cfg.userFolderStrategy}");
#endif
            switch (cfg.userFolderStrategy)
            {
                case UserFolderStrategy.Shared:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using Shared strategy, returning 'users/guest'");
#endif
                    return "users/guest";

                case UserFolderStrategy.PublicPerBuild:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using PublicPerBuild strategy");
#endif
#if UNITY_2022_1_OR_NEWER
                    return $"build-{Application.buildGUID}";
#else
                    return $"build-{Application.version}";
#endif
                case UserFolderStrategy.GuidPerDevice:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using GuidPerDevice strategy - accessing PlayerPrefs");
#endif
                    if (!PlayerPrefs.HasKey(DEVICE_GUID_PREF))
                        PlayerPrefs.SetString(DEVICE_GUID_PREF, Guid.NewGuid().ToString("N"));
                    return $"users/{PlayerPrefs.GetString(DEVICE_GUID_PREF)}";

                case UserFolderStrategy.UnityAuthentication:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using UnityAuthentication strategy");
#endif
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                    var auth = Unity.Services.Authentication.AuthenticationService.Instance;
                    return auth.IsSignedIn ? $"users/{auth.PlayerId}" : "users/guest";
#else
                    Debug.LogWarning("[SupabaseSaveSystem] Unity Authentication not present – using guest.");
                    return "users/guest";
#endif
                case UserFolderStrategy.Custom:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using Custom strategy");
#endif
                    if (cfg.customUserFolderResolver is IUserFolderResolver r)
                    {
                        string folder = r.ResolveUserFolder();
                        if (!string.IsNullOrEmpty(folder)) return folder;
                    }
                    Debug.LogWarning("[SupabaseSaveSystem] Custom resolver failed – using guest.");
                    return "users/guest";

                default:
#if UNITY_WEBGL && !UNITY_EDITOR
                    Debug.Log("[SupabaseSaveSystem] WebGL: Using default strategy, returning 'users/guest'");
#endif
                    return "users/guest";
            }
        }
	}
}
#endif
