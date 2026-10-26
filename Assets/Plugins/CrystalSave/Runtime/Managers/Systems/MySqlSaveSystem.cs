#if ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// MySQL backend accessed through a mediating web API.
    /// The client communicates with an HTTP endpoint that performs
    /// all database operations server-side, avoiding direct database
    /// exposure from the game client.
    /// </summary>
    public sealed partial class MySqlSaveSystem : ISaveSystem, ISaveConflictSource, ICloudRequestIdentitySource
    {
        readonly SaveSettings _cfg;
        readonly string apiUrl;
        readonly string authApiUrl;
        readonly string apiKey;
        readonly string table;
        readonly bool   keepLocalMirror;
        readonly string localFileStem;
        readonly string persistentPath;
        readonly string userIdStatic;
        readonly IUserFolderResolver liveResolver;
        readonly MySqlAuthMode authMode;
    readonly HttpClient httpClient;
    const int DefaultHttpTimeoutSeconds = 15;

        string userIdOverride;
        string UserId
        {
            get { var identity = CloudRequestScope.Find(this); return identity != null ? identity.UserFolder : ResolveUserId(); }
        }
        string ResolveUserId() => userIdOverride ?? (liveResolver != null ? liveResolver.ResolveUserFolder() : userIdStatic);
        Task<CloudRequestIdentity> ICloudRequestIdentitySource.CaptureRequestIdentityAsync() => CaptureRequestIdentityAsync();
        Task<CloudRequestIdentity> CaptureRequestIdentityAsync() => CloudRequestScope.CaptureAsync(this,
            () => new CloudRequestIdentity(ResolveUserId(), apiKey), liveResolver != null && userIdOverride == null);

    const string LEGACY_META_MASK   = "Slot{0}_Meta.bin";
        const string DEVICE_GUID_PREF = "CrystalSave_DeviceGuid";

        public MySqlSaveSystem(SaveSettings cfg, string persistentPath)
        {
            _cfg = cfg;

            apiUrl        = cfg.mySqlApiUrl?.TrimEnd('/') ?? string.Empty;
            authApiUrl    = string.IsNullOrEmpty(cfg.mySqlAuthApiUrl) ? apiUrl : cfg.mySqlAuthApiUrl.TrimEnd('/');
            apiKey        = cfg.mySqlApiKey;
            table         = cfg.tableName;
            authMode      = cfg.mySqlLoginMode;
            keepLocalMirror = cfg.keepLocalMirror && cfg.saveMethod == SaveMethod.BinaryFileFormat;
            localFileStem   = cfg.saveFileName;
            this.persistentPath  = persistentPath;
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();

            if (cfg.userFolderStrategy == UserFolderStrategy.Custom &&
                cfg.customUserFolderResolver is IUserFolderResolver r)
            {
                liveResolver = r;
            }
            else
            {
                userIdStatic = ResolveUserFolder(cfg);
            }

            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(DefaultHttpTimeoutSeconds);
            if (!string.IsNullOrEmpty(apiKey))
                httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
        }

    // LocalWritesEnabled: local file operations should occur when cloud save is disabled
    // OR when keepLocalMirror is requested while cloud save is enabled.
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
        /* Slot data                                                         */
        /* ================================================================== */

        async Task PostJsonAsync(string endpoint, string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var url = $"{apiUrl}{endpoint}";
            var (ok, code, _, error) = await PostJsonWebGLAsync(url, json);
            if (!ok)
                throw new IOException($"POST {url} → {code} {error}");
#else
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync($"{apiUrl}{endpoint}", content, SaveIoScope.Token).ConfigureAwait(SaveIoScope.PreserveUnityContext);
            response.EnsureSuccessStatusCode();
#endif
        }

        async Task<(bool ok, long code, string text)> PostJsonForTextAsync(string fullUrl, string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, code, text, _) = await PostJsonWebGLAsync(fullUrl, json);
            return (ok, code, text);
#else
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await httpClient.PostAsync(fullUrl, content, SaveIoScope.Token).ConfigureAwait(SaveIoScope.PreserveUnityContext);
            string txt = await resp.Content.ReadAsStringAsync().ConfigureAwait(SaveIoScope.PreserveUnityContext);
            return (resp.IsSuccessStatusCode, (long)resp.StatusCode, txt);
#endif
        }

        async Task<(bool ok, long code, string text)> PostAuthJsonForTextAsync(string endpoint, string json)
        {
            var full = $"{authApiUrl}{endpoint}";
            return await PostJsonForTextAsync(full, json);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        async Task<(bool ok, long code, string text, string error)> PostJsonWebGLAsync(string fullUrl, string json)
        {
            using var req = new UnityWebRequest(fullUrl, UnityWebRequest.kHttpVerbPOST);
            byte[] body = string.IsNullOrEmpty(json) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(apiKey)) req.SetRequestHeader("X-API-KEY", apiKey);
            req.timeout = DefaultHttpTimeoutSeconds;

            using var abort = SaveIoScope.AbortOnCancellation(req);
            SaveIoScope.CheckCancellation();
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
            return (ok, req.responseCode, req.downloadHandler?.text, req.error);
        }

        async Task<(bool ok, long code, string text, string error)> GetTextWebGLAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(apiKey)) req.SetRequestHeader("X-API-KEY", apiKey);
            req.timeout = DefaultHttpTimeoutSeconds;
            using var abort = SaveIoScope.AbortOnCancellation(req);
            SaveIoScope.CheckCancellation();
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
            return (ok, req.responseCode, req.downloadHandler?.text, req.error);
        }

        async Task<(bool ok, long code, byte[] data, string error)> GetBytesWebGLAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            if (!string.IsNullOrEmpty(apiKey)) req.SetRequestHeader("X-API-KEY", apiKey);
            req.timeout = DefaultHttpTimeoutSeconds;
            using var abort = SaveIoScope.AbortOnCancellation(req);
            SaveIoScope.CheckCancellation();
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
            return (ok, req.responseCode, req.downloadHandler?.data, req.error);
        }
#endif

        [Serializable]
        class AuthReq
        {
            public string username;
            public string password;
        }

        [Serializable]
        class LoginResp
        {
            public string uid;
        }

        public async Task<bool> SignUpAsync(string username, string password)
        {
            var req = new AuthReq { username = username, password = password };
            var (ok, _, _) = await PostAuthJsonForTextAsync("/signup", CloudMetadataJson.Encode(req));
            return ok;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var req = new AuthReq { username = username, password = password };
            var (ok, _, text) = await PostAuthJsonForTextAsync("/login", CloudMetadataJson.Encode(req));
            if (!ok) return false;
            var r = CloudMetadataJson.Decode<LoginResp>(text);
            if (r != null && !string.IsNullOrEmpty(r.uid))
            {
                userIdOverride = r.uid;
                return true;
            }
            return false;
        }

        public async Task SaveAsync(byte[] data, SaveSlot slot)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            var req = new SaveReq
            {
                uid  = UserId,
                slot = slot.SlotNumber,
                table = table,
                data = Convert.ToBase64String(data)
            };
            await PostJsonAsync("/save", CloudMetadataJson.Encode(req));

            try
            {
                await SaveSlotMetadataCoreAsync(slot, false).ConfigureAwait(SaveIoScope.PreserveUnityContext);
                await WriteLocalMirrorAsync(data, slot);
            }
            catch (Exception ex)
            { throw new IOException("Remote payload was uploaded, but metadata or local persistence failed; the remote slot may be partially updated.", ex); }
        }

        public async Task<byte[]> LoadAsync(SaveSlot slot)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            string url = $"{apiUrl}/load?uid={WebUtility.UrlEncode(UserId)}&slot={slot.SlotNumber}&table={WebUtility.UrlEncode(table)}";
#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, code, text, err) = await GetTextWebGLAsync(url);
            if (code == 404) return null;
            if (!ok)
                throw new IOException($"GET {url} → {code} {err}");
            string base64 = text;
#else
            using var response = await httpClient.GetAsync(url, SaveIoScope.Token).ConfigureAwait(SaveIoScope.PreserveUnityContext);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            string base64 = await response.Content.ReadAsStringAsync().ConfigureAwait(SaveIoScope.PreserveUnityContext);
#endif
            if (string.IsNullOrEmpty(base64)) return null;
            return Convert.FromBase64String(base64);
        }

        public async Task DeleteAsync(SaveSlot slot)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            Verification.Discard(slot.SlotNumber);
            SaveIoScope.CheckCancellation();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            var req = new DeleteReq
            {
                uid  = UserId,
                slot = slot.SlotNumber,
                table = table
            };
            await PostJsonAsync("/delete", CloudMetadataJson.Encode(req));

            await DeleteLocalSlotAsync(slot);
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
        /* Metadata                                                          */
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
                slot = await SaveSlotMetadataUpdate.ResolveRemoteAsync(slot,
                    () => ReadRemoteConflictMetadataAsync(slot.SlotNumber));
            }
            if (slot.SlotName == null)
                slot.SlotName = $"Slot {slot.SlotNumber}";

            var req = new MetaReq
            {
                uid   = UserId,
                slot  = slot.SlotNumber,
                table = table,
                name  = slot.SlotName,
                ticks = slot.LastSaved.Ticks,
                scene = slot.LastActiveScene,
                shot  = slot.ScreenshotFileName,
                meta  = slot.CustomMetadata
            };

            string json = CloudMetadataJson.Encode(req);
            await PostJsonAsync("/metadata", json);

            if (local != null)
                await CloudMirrorFiles.WriteMetadataAsync(MetaLocalPath(local.SlotNumber), SaveSystem.SerializeSlotMetadata(_cfg, local));
        }

        [Serializable]
        class MetaPair
        {
            public string key;
            public string value;
        }

        [Serializable]
        class SaveReq
        {
            public string uid;
            public int    slot;
            public string table;
            public string data;
        }

        [Serializable]
        class DeleteReq
        {
            public string uid;
            public int    slot;
            public string table;
        }

        // JSON-only request: CloudMetadataJson serializes its public fields, including meta.
        // Do not opt this DTO into Unity's separate field/dictionary serialization rules.
        class MetaReq
        {
            public string uid;
            public int    slot;
            public string table;
            public string name;
            public long   ticks;
            public string scene;
            public string shot;
            public Dictionary<string, string> meta;
        }

        [Serializable]
        class SlotMeta
        {
            public int            slot;
            public string         name;
            public long           ticks;
            public string         scene;
            public string         shot;
            public List<MetaPair> meta;
        }

        static SaveSlot ConvertRemoteMetadata(SlotMeta metadata, int? expectedSlot = null)
        {
            if (metadata == null || metadata.slot < 1 ||
                (expectedSlot.HasValue && metadata.slot != expectedSlot.Value))
                throw new InvalidDataException("Cloud metadata does not match the requested slot.");
            if (metadata.ticks < DateTime.MinValue.Ticks || metadata.ticks > DateTime.MaxValue.Ticks)
                throw new InvalidDataException("Cloud metadata contains an invalid timestamp.");
            return new SaveSlot(metadata.slot,
                metadata.name ?? $"Slot {metadata.slot}",
                new DateTime(metadata.ticks, DateTimeKind.Utc), metadata.shot ?? string.Empty,
                metadata.scene ?? string.Empty) { CustomMetadata = PairsToDict(metadata.meta) };
        }

        static Dictionary<string, string> PairsToDict(List<MetaPair> list)
        {
            var dict = new Dictionary<string, string>();
            if (list == null) return dict;
            foreach (var kv in list)
            {
                if (kv == null || kv.key == null || dict.ContainsKey(kv.key))
                    throw new InvalidDataException("Cloud metadata contains a missing or duplicate key.");
                dict.Add(kv.key, kv.value);
            }
            return dict;
        }

        public async Task<SaveSlot> LoadSlotMetadataAsync(int num)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            SaveIoScope.CheckCancellation();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            // Browser mirrors support the same acknowledged metadata files as native storage.
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
                    catch { }
                }
                else if (File.Exists(legacyLoc))
                {
                    try
                    {
                        var local = SaveDataSerializer.Instance.Deserialize<SaveSlot>(DecodeLocalMetadata(File.ReadAllBytes(legacyLoc)));
                        if (local != null && local.SlotNumber == num) return local;
                    }
                    catch { }
                }
            }
            return await ReadRemoteConflictMetadataAsync(num).ConfigureAwait(SaveIoScope.PreserveUnityContext);
        }

        public async Task<List<SaveSlot>> ListRemoteSlotsAsync()
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            string url = $"{apiUrl}/list?uid={WebUtility.UrlEncode(UserId)}";
            var list = new List<SaveSlot>();
#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, code, text, err) = await GetTextWebGLAsync(url);
            if (code == 404) return list;
            if (!ok)
                throw new IOException($"GET {url} → {code} {err}");
            string json = text;
#else
            using var resp = await httpClient.GetAsync(url, SaveIoScope.Token).ConfigureAwait(SaveIoScope.PreserveUnityContext);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return list;
            if (!resp.IsSuccessStatusCode)
                throw new IOException($"GET {url} → {(int)resp.StatusCode} {resp.ReasonPhrase}");
            string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(SaveIoScope.PreserveUnityContext);
#endif
            var entries = CloudMetadataJson.Decode<SlotMeta[]>(json);
            if (entries == null) throw new InvalidDataException("Invalid remote slot listing.");
            var seen = new HashSet<int>();
            foreach (var m in entries)
            {
                var slot = ConvertRemoteMetadata(m);
                if (!seen.Add(slot.SlotNumber)) throw new InvalidDataException("Duplicate remote slot identity.");
                list.Add(slot);
            }
            SaveIoScope.CheckCancellation();
            return list;
        }

        /* ================================================================== */
        /* Metadata helpers                                                  */
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
            string path   = Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav");
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
            string backup = Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav.bak");
            AtomicSaveFile.Delete(backup);
            AtomicSaveFile.Delete(MetaLocalPath(slot.SlotNumber) + ".bak");
        }

        /* ================================================================== */
        /*  Temporary files (verification without mirror)                     */
        /* ================================================================== */

        string TempPathFor(SaveSlot slot) => Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.tmp");

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
        /*  Sync wrappers                                                     */
        /* ================================================================== */

#if UNITY_WEBGL && !UNITY_EDITOR
    public void   Save  (byte[] d, SaveSlot s) => throw new NotSupportedException("Synchronous Save is not supported on WebGL. Use SaveAsync.");
    public byte[] Load  (SaveSlot s)           => throw new NotSupportedException("Synchronous Load is not supported on WebGL. Use LoadAsync.");
    public void   Delete(SaveSlot s)           => throw new NotSupportedException("Synchronous Delete is not supported on WebGL. Use DeleteAsync.");
#else
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
#endif

        public string GetSaveGamesPath() => persistentPath;

        /* ───────────── user id helper (same as Supabase) ───────────── */
        static string ResolveUserFolder(SaveSettings cfg)
        {
            switch (cfg.userFolderStrategy)
            {
                case UserFolderStrategy.Shared:
                    return "users/guest";
                case UserFolderStrategy.PublicPerBuild:
#if UNITY_2022_1_OR_NEWER
                    return $"build/{Application.buildGUID}";
#else
                    return "build/unknown";
#endif
                case UserFolderStrategy.GuidPerDevice:
                    string guid = PlayerPrefs.GetString(DEVICE_GUID_PREF, string.Empty);
                    if (string.IsNullOrEmpty(guid))
                    {
                        guid = Guid.NewGuid().ToString("N");
                        PlayerPrefs.SetString(DEVICE_GUID_PREF, guid);
                    }
                    return $"users/{guid}";
                case UserFolderStrategy.UnityAuthentication:
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                    return $"users/{Unity.Services.Authentication.AuthenticationService.Instance.PlayerId}";
#else
                    return "users/guest";
#endif
                case UserFolderStrategy.Custom:
                default:
                    return "users/guest";
            }
        }

        public (string apiUrl, string apiKey) GetUploadInfo()
        {
            return (apiUrl, apiKey);
        }

        /// <summary>
        /// Uploads a screenshot for the given slot to the configured MySQL API.
        /// </summary>
        /// <param name="slot">Numeric slot index the image belongs to.</param>
        /// <param name="fileName">Original name of the image file.</param>
        /// <param name="bytes">Encoded image data.</param>
        /// <param name="fmt">Image encoding format.</param>
        public async Task UploadScreenshotAsync(int slot, string fileName, byte[] bytes, ScreenshotFormat fmt)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            string uid = UserId;
            string url = $"{apiUrl.TrimEnd('/')}/uploadImage";
            var dispatcher = UnityMainThreadDispatcher.Instance();
            var req = await dispatcher.EnqueueAsync(() =>
            {
                var form = new WWWForm();
                form.AddField("uid", uid);
                form.AddField("slot", slot);
                form.AddField("name", fileName);
                form.AddBinaryData("shot", bytes, fileName,
                    fmt == ScreenshotFormat.PNG ? "image/png" : "image/jpeg");
                var request = UnityWebRequest.Post(url, form);
                request.timeout = DefaultHttpTimeoutSeconds;
                if (!string.IsNullOrEmpty(apiKey)) request.SetRequestHeader("X-API-KEY", apiKey);
                return request;
            });
            try
            {
                using var abort = SaveIoScope.AbortOnCancellation(req);
                SaveIoScope.CheckCancellation();
                var op = await dispatcher.EnqueueAsync(req.SendWebRequest);
                while (!await dispatcher.EnqueueAsync(() => op.isDone)) await Task.Yield();
                SaveIoScope.CheckCancellation();
                await dispatcher.EnqueueAsync(() =>
                {
                    if (req.result != UnityWebRequest.Result.Success)
                        throw new IOException($"POST {url} → {req.responseCode} {req.error}");
                });
            }
            finally { await dispatcher.EnqueueAsync(req.Dispose); }
        }

        /// <summary>
        /// Downloads a screenshot for the given file name directly from the API server.
        /// </summary>
        /// <param name="fileName">Name of the screenshot file to fetch.</param>
        public async Task<byte[]> DownloadScreenshotAsync(string fileName)
        {
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            if (string.IsNullOrEmpty(fileName)) return null;
            // Ensure the API URL is treated as a directory so that any path
            // segments (e.g. `/saves`) are preserved when combining with the
            // screenshot location. Without the trailing slash `new Uri(apiUrl)`
            // would consider the last segment a file and strip it, producing
            // URLs like `http://host/img/...` which results in HTTP 404s.
            var baseUri = new Uri(apiUrl.EndsWith("/") ? apiUrl : apiUrl + "/");
            // The server stores the exact upload UID, including raw login names or
            // an explicitly supplied users/build scope. Escape path segments without
            // inventing a different user folder when reading the same image back.
            string uid = string.Join("/", (UserId ?? string.Empty).Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
            string imageName = Uri.EscapeDataString(Path.GetFileName(fileName.Replace('\\', '/')));
            var shotUri = new Uri(baseUri, $"img/{uid}/{imageName}");
            Logger.Log($"MySqlSaveSystem: Requesting screenshot from '{shotUri}'.", LogCategory.CloudSave, LogLevel.Info);


#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, code, bytes, err) = await GetBytesWebGLAsync(shotUri.ToString());
            Logger.Log($"MySqlSaveSystem: Received HTTP {code} for screenshot '{fileName}'.", LogCategory.CloudSave, ok ? LogLevel.Info : LogLevel.Warning);
            if (!ok) return null;
            Logger.Log($"MySqlSaveSystem: Downloaded {bytes?.Length ?? 0} bytes for screenshot '{fileName}'.", LogCategory.CloudSave, LogLevel.Info);
            return bytes;
#else
            using HttpResponseMessage resp = await httpClient.GetAsync(shotUri, SaveIoScope.Token).ConfigureAwait(SaveIoScope.PreserveUnityContext);
            Logger.Log($"MySqlSaveSystem: Received HTTP {(int)resp.StatusCode} for screenshot '{fileName}'.", LogCategory.CloudSave, resp.IsSuccessStatusCode ? LogLevel.Info : LogLevel.Warning);
            if (!resp.IsSuccessStatusCode)
                return null;

            byte[] bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(SaveIoScope.PreserveUnityContext);
            Logger.Log($"MySqlSaveSystem: Downloaded {bytes?.Length ?? 0} bytes for screenshot '{fileName}'.", LogCategory.CloudSave, LogLevel.Info);
            return bytes;
#endif
        }
    }
}
#endif
