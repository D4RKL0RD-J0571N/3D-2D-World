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
	/// REST-only Firebase backend.
	/// • Writes/reads full metadata as JSON via a pure-fields DTO
	/// • Mirrors .sav *and* MemoryPack header when keepLocalMirror = true
	/// • Keeps remote/local file-name scheme aligned (slot-N.sav / .meta.json)
	/// </summary>
	public sealed partial class FirebaseSaveSystem : ISaveSystem, ISaveConflictSource, ICloudRequestIdentitySource
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

    const string META_EXT         = ".meta.json";        // cloud
    const string LEGACY_META_MASK = "Slot{0}_Meta.bin";  // local legacy fallback

	/* ───────────── DTO used only for JSON ───────────── */


	/* ───────────── ctor ───────────── */
	public FirebaseSaveSystem(SaveSettings cfg) : this(cfg, Application.persistentDataPath) { }

    public FirebaseSaveSystem(SaveSettings cfg, string rootPath)
	{
	    _cfg = cfg;

	    bucket = cfg.firebaseBucket;
            url    = "https://firebasestorage.googleapis.com/v0/b/" + bucket;
            key    = cfg.firebaseIdToken;

	    /* dynamic folder only for Strategy.Custom */
            if (cfg.userFolderStrategy == UserFolderStrategy.Custom &&
                cfg.customUserFolderResolver is IUserFolderResolver r)
            {
                liveResolver = r;               // keep reference – its value may change later
            }
	    else
	    {
	        userDirStatic = ResolveUserFolder(cfg);
	    }

	    useJson  = cfg.cloudSaveTransport == CloudSaveTransport.JSON;
	    dataExt  = useJson ? ".json" : ".sav";

            keepLocalMirror = cfg.keepLocalMirror &&
                              cfg.saveMethod == SaveMethod.BinaryFileFormat;
            localFileStem   = cfg.saveFileName;
            persistentPath  = rootPath;
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
        }

        // LocalWritesEnabled: perform local file operations when cloud save is disabled
        // or when keepLocalMirror is requested.
        bool LocalWritesEnabled() => !_cfg.enableCloudSave || keepLocalMirror;

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

	/* ================================================================== */
	/* Slot data
	/* ================================================================== */

	public async Task SaveAsync(byte[] data, SaveSlot slot)
	{
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
	    // 1 ─ blob
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

	    // cloud JSON – all members survive because DTO uses fields
	    var dto = CloudSlotMetadata.FromSlot(slot);
	    string json = CloudMetadataJson.Encode(dto);
            await UploadObject(PathFor(slot, META_EXT),
                               Encoding.UTF8.GetBytes(json),
                               "application/json").ConfigureAwait(SaveIoScope.PreserveUnityContext);

	    // local MemoryPack mirror (only when keepLocalMirror)
            if (local != null)
                await CloudMirrorFiles.WriteMetadataAsync(MetaLocalPath(local.SlotNumber), SaveSystem.SerializeSlotMetadata(_cfg, local));
	}

	public async Task<SaveSlot> LoadSlotMetadataAsync(int num)
	{
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            SaveIoScope.CheckCancellation();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
	    /* 1 ─ try local header first */
            if (keepLocalMirror)
            {
                string loc = CloudMirrorFiles.FindCommittedPath(MetaLocalPath(num));
                string legacyLoc = CloudMirrorFiles.FindCommittedPath(LegacyMetaLocalPath(num));
                if (File.Exists(loc))
                {
                    try
                    {
                        var local = SaveDataSerializer.Instance.Deserialize<SaveSlot>(DecodeLocalMetadata(File.ReadAllBytes(loc)));
                        if (local != null && local.SlotNumber == num) return local;
                    }
                    catch { /* fall through to cloud */ }
                }
                else if (File.Exists(legacyLoc))
                {
                    try
                    {
                        var local = SaveDataSerializer.Instance.Deserialize<SaveSlot>(DecodeLocalMetadata(File.ReadAllBytes(legacyLoc)));
                        if (local != null && local.SlotNumber == num) return local;
                    }
                    catch { /* fall through to cloud */ }
                }
            }

	    /* 2 ─ cloud JSON */
            byte[] bytes = await DownloadObject(PathFor(num, META_EXT)).ConfigureAwait(SaveIoScope.PreserveUnityContext);
            return CloudSlotMetadata.Decode(bytes, num);
        }

        public async Task<List<SaveSlot>> ListRemoteSlotsAsync()
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            SaveIoScope.CheckCancellation();
            string prefix = UserDir.TrimEnd('/') + "/";
            var names = new HashSet<string>(StringComparer.Ordinal);
            var tokens = new HashSet<string>(StringComparer.Ordinal);
            string pageToken = null;
            do
            {
                string requestUrl = $"{url}/o?prefix={Escape(prefix)}&maxResults=1000";
                if (pageToken != null) requestUrl += "&pageToken=" + Escape(pageToken);
                byte[] bytes = await ReadRequestAsync(requestUrl, false).ConfigureAwait(SaveIoScope.PreserveUnityContext);
                if (bytes == null || bytes.Length == 0)
                    throw new InvalidDataException("Firebase returned an empty listing response.");
                var page = JsonUtility.FromJson<ResponseWrap>(Encoding.UTF8.GetString(bytes));
                if (page == null || (page.items?.Length ?? 0) > 1000)
                    throw new InvalidDataException("Firebase returned an invalid listing response.");
                foreach (var item in page.items ?? Array.Empty<FileEntry>())
                {
                    if (string.IsNullOrEmpty(item.name) || !item.name.StartsWith(prefix, StringComparison.Ordinal))
                        throw new InvalidDataException("Firebase returned an object outside the requested folder.");
                    names.Add(item.name);
                }
                pageToken = string.IsNullOrEmpty(page.nextPageToken) ? null : page.nextPageToken;
                if (pageToken != null && !tokens.Add(pageToken))
                    throw new InvalidDataException("Firebase repeated a listing continuation token.");
            } while (pageToken != null);

            var slots = new List<SaveSlot>();
            foreach (string name in names.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!name.EndsWith(META_EXT, StringComparison.Ordinal)) continue;
                byte[] bytes = await DownloadObject(name).ConfigureAwait(SaveIoScope.PreserveUnityContext);
                if (bytes == null) continue; // The object may have been deleted after listing.
                var slot = CloudSlotMetadata.Decode(bytes);
                string stem = prefix + localFileStem + slot.SlotNumber;
                if (name != stem + META_EXT)
                    throw new InvalidDataException("Firebase slot metadata does not match its object name.");
                if (names.Contains(stem + dataExt)) slots.Add(slot);
            }
            SaveIoScope.CheckCancellation();
            return slots.OrderBy(slot => slot.SlotNumber).ToList();
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

        static byte[] DecodeLocalMetadata(byte[] bytes)
        {
            if (!SaveCrypto.HasEnvelope(bytes)) return bytes;
            return SaveManager.Instance?.EncryptionService?.MaybeDecrypt(bytes) ?? throw new InvalidDataException("Metadata authentication failed.");
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
            string mime = fmt == ScreenshotFormat.PNG ? "image/png" : "image/jpeg";
            await UploadObject($"{UserDir}/{file}", bytes, mime).ConfigureAwait(SaveIoScope.PreserveUnityContext);
        }

        public async Task<byte[]> DownloadScreenshotAsync(string file)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            return await DownloadObject($"{UserDir}/{file}").ConfigureAwait(SaveIoScope.PreserveUnityContext);
        }

        public async Task DeleteScreenshotAsync(string file)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            await DeleteObject($"{UserDir}/{file}").ConfigureAwait(SaveIoScope.PreserveUnityContext);
        }

	/* ================================================================== */
	/*  Screenshot
	/* ================================================================== */

	/* ───────────── REST helpers ───────────── */

	[Serializable] sealed class ResponseWrap { public FileEntry[] items; public string nextPageToken; }
	[Serializable] struct FileEntry    { public string name; }

        async Task UploadObject(string path, byte[] data, string mime)
        {
            var identity = await CaptureRequestIdentityAsync();
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Put($"{url}/o?name={Escape(path)}&uploadType=media", data);
                r.method = UnityWebRequest.kHttpVerbPOST;
                r.SetRequestHeader("Content-Type", mime);
                AddAuth(r, identity.AccessKey);
                return r;
            });
            try { await Send(req).ConfigureAwait(SaveIoScope.PreserveUnityContext); }
            finally { await UnityMainThreadDispatcher.Instance().EnqueueAsync(req.Dispose); }
        }

        Task<byte[]> DownloadObject(string path) => ReadRequestAsync($"{url}/o/{Escape(path)}?alt=media", true);

        async Task<byte[]> ReadRequestAsync(string requestUrl, bool allowMissing)
        {
            var identity = await CaptureRequestIdentityAsync();
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Get(requestUrl);
                AddAuth(r, identity.AccessKey);
                return r;
            });
            try
            {
                await Send(req, allow404: allowMissing).ConfigureAwait(SaveIoScope.PreserveUnityContext);
                SaveIoScope.CheckCancellation();
                return await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
                    req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data ?? Array.Empty<byte>() : null);
            }
            finally { await UnityMainThreadDispatcher.Instance().EnqueueAsync(req.Dispose); }
        }

        async Task DeleteObject(string path)
        {
            var identity = await CaptureRequestIdentityAsync();
            var req = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                var r = UnityWebRequest.Delete($"{url}/o/{Escape(path)}");
                AddAuth(r, identity.AccessKey);
                return r;
            });
            try { await Send(req, allow404: true).ConfigureAwait(SaveIoScope.PreserveUnityContext); }
            finally { await UnityMainThreadDispatcher.Instance().EnqueueAsync(req.Dispose); }
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
	    Uri.EscapeDataString(p);

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

        static void AddAuth(UnityWebRequest r, string bearer)
        {
            if (!string.IsNullOrEmpty(bearer))
                r.SetRequestHeader("Authorization", $"Bearer {bearer}");
        }

static async Task Send(UnityWebRequest r, bool allow404 = false)
        {
            using var abort = SaveIoScope.AbortOnCancellation(r);
            SaveIoScope.CheckCancellation();
            var op = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                r.timeout = REQUEST_TIMEOUT_SECONDS;
                return r.SendWebRequest();
            });
            while (!await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => op.isDone))
                await Task.Yield();

            SaveIoScope.CheckCancellation();
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                bool ok = r.result == UnityWebRequest.Result.Success ||
                          r.responseCode is 201 or 206;

                if (allow404 && r.responseCode == 404)
                    ok = true;

                if (!ok)
                    throw new IOException($"{r.method} {r.url} → {r.responseCode} {r.error}");
            });
        }

	/* ================================================================== */
	/*  Sync wrappers
	/* ================================================================== */

        public void Save(byte[] d, SaveSlot s)
        {
            CloudSaveThreading.RequireBackgroundThread(nameof(Save));
            SaveAsync(d, s).GetAwaiter().GetResult();
        }
        public byte[] Load(SaveSlot s)
        {
            CloudSaveThreading.RequireBackgroundThread(nameof(Load));
            return LoadAsync(s).GetAwaiter().GetResult();
        }
        public void Delete(SaveSlot s)
        {
            CloudSaveThreading.RequireBackgroundThread(nameof(Delete));
            DeleteAsync(s).GetAwaiter().GetResult();
        }

	/* ================================================================== */
	/*
	/* ================================================================== */

	string PathFor(SaveSlot s, string ext) => $"{UserDir}/{localFileStem}{s.SlotNumber}{ext}";
	string PathFor(int slot,   string ext) => $"{UserDir}/{localFileStem}{slot}{ext}";

	/* ───────────── user-folder strategy (unchanged) ───────────── */

	const string DEVICE_GUID_PREF = "CrystalSave_DeviceGuid";

	static string ResolveUserFolder(SaveSettings cfg)
	{
            switch (cfg.userFolderStrategy)
            {
                case UserFolderStrategy.Shared:
                    return "users/guest";

                case UserFolderStrategy.PublicPerBuild:
#if UNITY_2022_1_OR_NEWER
                    return $"build-{Application.buildGUID}";
#else
                    return $"build-{Application.version}";
#endif
                case UserFolderStrategy.GuidPerDevice:
                    if (!PlayerPrefs.HasKey(DEVICE_GUID_PREF))
                        PlayerPrefs.SetString(DEVICE_GUID_PREF, Guid.NewGuid().ToString("N"));
                    return $"users/{PlayerPrefs.GetString(DEVICE_GUID_PREF)}";

                case UserFolderStrategy.UnityAuthentication:
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                    var auth = Unity.Services.Authentication.AuthenticationService.Instance;
                    return auth.IsSignedIn ? $"users/{auth.PlayerId}" : "users/guest";
#else
                    Debug.LogWarning("[FirebaseSaveSystem] Unity Authentication not present – using guest.");
                    return "users/guest";
#endif
                case UserFolderStrategy.Custom:
                    if (cfg.customUserFolderResolver is IUserFolderResolver r)
                    {
                        string folder = r.ResolveUserFolder();
                        if (!string.IsNullOrEmpty(folder)) return folder;
                    }
                    Debug.LogWarning("[FirebaseSaveSystem] Custom resolver failed – using guest.");
                    return "users/guest";

                default:
                    return "users/guest";
            }
        }
	}
}
#endif
