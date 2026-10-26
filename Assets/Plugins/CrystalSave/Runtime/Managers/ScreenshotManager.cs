#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if REMEMBERME_CLOUDSAVE_PRESENT
using Unity.Services.CloudSave;
using UnityCloudSaveService = Unity.Services.CloudSave.CloudSaveService;
#endif

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Captures thumbnails, keeps a local copy when needed, and uploads them to
    /// Supabase if that backend is active.  When <i>Keep Local Mirror</i> is
    /// off, a <b>tiny</b> on‑disk copy is still written for Supabase so slot
    /// UIs can load it synchronously.
    /// </summary>
    public class ScreenshotManager
    {
        private readonly SaveManager owner;
        private SaveManager Owner => owner ?? SaveManager.Instance;
        private readonly SaveSettings saveSettings;
        private readonly string screenshotFolderPath;
        private readonly string rootPath;
        
        // WebGL memory cache for screenshots since file I/O is unreliable
        private static readonly Dictionary<string, byte[]> webglScreenshotCache = new Dictionary<string, byte[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateForFastEnterPlayMode()
        {
            webglScreenshotCache.Clear();
        }

        /// <summary>
        /// Optional custom method used to capture a screenshot texture.
        /// When null, <see cref="ScreenCapture.CaptureScreenshotAsTexture"/> is used.
        /// </summary>
        public Func<Texture2D> CustomCapture { get; set; }
        /// <summary>
        /// Optional async custom method used to capture a screenshot texture.
        /// Checked when <see cref="CustomCapture"/> is null.
        /// </summary>
        public Func<Task<Texture2D>> CustomCaptureAsync { get; set; }

        /* ------------------------------------------------------------------ */
        // Helper flags
        /* ------------------------------------------------------------------ */
        private bool UseLocalMirror =>
            !saveSettings.enableCloudSave ||
            (saveSettings.enableCloudSave &&
             saveSettings.keepLocalMirror &&
             saveSettings.cloudCryptoMode != CloudCryptoMode.ServerSide);

        // Store screenshots locally only when using a local save backend or
        // when a local mirror is explicitly enabled.
        private bool SaveLocally =>
            !saveSettings.enableCloudSave ||
            UseLocalMirror;
        /* ------------------------------------------------------------------ */
        public ScreenshotManager(SaveSettings settings, string rootPath) : this(settings, rootPath, null) { }

        internal ScreenshotManager(SaveSettings settings, string rootPath, SaveManager owner)
        {
            this.owner = owner;
            saveSettings = settings;
            this.rootPath = rootPath;
            // Settings can enable screenshots after construction; retain the path
            // independently of whether a directory needs creating yet.
            screenshotFolderPath = Path.Combine(rootPath, saveSettings.screenshotFolderName);

#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, prefer memory cache for instant access. When cloud save is disabled
            // (or a local mirror is used), also set up a local file cache under
            // persistentDataPath (IndexedDB) so screenshots can persist across reloads
            // if config.autoSyncPersistentDataPath = true is enabled in createUnityInstance().
            if (saveSettings.enableScreenshots)
            {
                Logger.Log("ScreenshotManager: WebGL mode - using memory cache; local file cache enabled when SaveLocally is true", LogCategory.ScreenshotManager, LogLevel.Info);

                if (SaveLocally)
                {
                    screenshotFolderPath = Path.Combine(rootPath, saveSettings.screenshotFolderName);
                    try
                    {
                        if (!Directory.Exists(screenshotFolderPath))
                            Directory.CreateDirectory(screenshotFolderPath);
                        Logger.Log($"ScreenshotManager: WebGL local screenshot folder: '{screenshotFolderPath}'.", LogCategory.ScreenshotManager, LogLevel.Info);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"ScreenshotManager: Failed to create WebGL screenshot folder '{screenshotFolderPath}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ScreenshotManager, LogLevel.Warning);
                    }
                }
            }
#else
            // Create screenshot folder if needed for local saves OR for Supabase cache
            if (saveSettings.enableScreenshots && (SaveLocally || saveSettings.backend == SaveBackend.Supabase))
            {
                screenshotFolderPath = Path.Combine(rootPath,
                                                    saveSettings.screenshotFolderName);
                if (!Directory.Exists(screenshotFolderPath))
                {
                    Directory.CreateDirectory(screenshotFolderPath);
                    string purpose = SaveLocally ? "local storage" : "Supabase cache";
                    Logger.Log($"Created screenshot folder at '{screenshotFolderPath}' for {purpose}.", LogCategory.ScreenshotManager, LogLevel.Info);
                }
            }
#endif
        }

        public bool AreScreenshotsEnabled => saveSettings.enableScreenshots;

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Cache screenshot data in memory for WebGL builds where file I/O is unreliable
        /// </summary>
        public void CacheScreenshotInMemory(string fileName, byte[] data)
        {
            if (IsValidScreenshotFileName(fileName) && data != null && data.Length > 0)
            {
                webglScreenshotCache[fileName] = PrepareScreenshotCacheBytes(data, saveSettings);
                Logger.Log($"ScreenshotManager: Cached screenshot '{fileName}' in WebGL memory ({data.Length} bytes)", LogCategory.ScreenshotManager, LogLevel.Info);
            }
        }
        
        /// <summary>
        /// Static method to cache screenshot data in WebGL memory from external classes
        /// </summary>
        public static void CacheScreenshotInWebGLMemory(string fileName, byte[] data)
        {
            if (IsValidScreenshotFileName(fileName) && data != null && data.Length > 0)
            {
                webglScreenshotCache[fileName] = ScreenshotImageCodec.PrepareCacheBytes(data, SaveManager.Instance?.SaveSettings, SaveManager.Instance?.EncryptionService);
                Logger.Log($"ScreenshotManager: Static cached screenshot '{fileName}' in WebGL memory ({data.Length} bytes)", LogCategory.ScreenshotManager, LogLevel.Info);
            }
        }
#endif

        /* ================================================================== */
        #region Capture
        /* ================================================================== */
        /// <summary>
        /// Captures a single end-of-frame screenshot and returns the encoded bytes and file extension.
        /// This does not keep any loop running; it yields until end-of-frame and captures once.
        /// </summary>
        public static IEnumerator CaptureOnce(ScreenshotFormat format, Func<Texture2D> customCapture, Action<byte[], string> onReady)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = customCapture != null ? customCapture() : ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null)
            {
                onReady?.Invoke(null, null);
                yield break;
            }
            try
            {
                byte[] bytes = format == ScreenshotFormat.PNG ? tex.EncodeToPNG() : tex.EncodeToJPG();
                string ext = format == ScreenshotFormat.PNG ? "png" : "jpg";
                onReady?.Invoke(bytes, ext);
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }
        public IEnumerator CaptureScreenshot(int slotNumber, Action<string> onComplete)
        {
            var context = SaveIoScope.Current.Value;
            if (!saveSettings.enableScreenshots)
            {
                onComplete?.Invoke(null);
                yield break;
            }
            
            // In the Editor, WaitForEndOfFrame can hang indefinitely in certain contexts
            // (e.g., when triggered from OnGUI or when Game view isn't active/focused)
            // We'll add our own internal timeout as a safeguard
#if UNITY_EDITOR
            bool completed = false;
            string resultFileName = null;
            
            // Start the actual capture coroutine
            var captureCoroutine = CaptureScreenshotCoroutine(slotNumber, fileName =>
            {
                resultFileName = fileName;
                completed = true;
            }, context);
            
            // Run with timeout
            float timeout = 5f; // 5 second internal timeout for screenshot capture
            float elapsed = 0f;
            
            try
            {
                while (elapsed < timeout && captureCoroutine.MoveNext())
                {
                    yield return captureCoroutine.Current;
                    elapsed += Time.unscaledDeltaTime;
                }
            }
            finally { (captureCoroutine as IDisposable)?.Dispose(); }
            
            if (!completed)
            {
                Logger.Log($"[CrystalSave] Screenshot capture timed out - No screenshot was saved.", LogCategory.SaveManager, LogLevel.Warning);
                Logger.Log($"[CrystalSave] This happens when the Scene view is active instead of the Game view during save.", LogCategory.SaveManager, LogLevel.Warning);
                Logger.Log($"[CrystalSave] Tip: Keep the Game view visible while saving to capture screenshots.", LogCategory.SaveManager, LogLevel.Info);
                Logger.Log($"[CrystalSave] This is an Editor-only issue and will NOT occur in a built game.", LogCategory.SaveManager, LogLevel.Info);
            }
            
            onComplete?.Invoke(resultFileName);
#else
            yield return CaptureScreenshotCoroutine(slotNumber, onComplete, context);
#endif
        }

        private IEnumerator CaptureScreenshotCoroutine(int slotNumber, Action<string> onComplete, SaveOperationContext context)
        {
            /* 1 ─ Render is finished */
            if (CustomCapture == null && CustomCaptureAsync == null) yield return new WaitForEndOfFrame();
            context?.CheckCancellation();

            /* 2 ─ Capture + encode on MAIN thread */
            Texture2D tex = null;
            yield return CaptureTextureCoroutine(captured => tex = captured);
            if (tex == null)
            {
                onComplete?.Invoke(null);
                yield break;
            }
            // Normalize to centered 16:9 (landscape) or 9:16 (portrait), then clamp to max 1920px on longer side
            Texture2D normalizedTex = CropAndResize(tex, saveSettings.screenshotFormat);

            string ext = saveSettings.screenshotFormat == ScreenshotFormat.PNG ? "png" : "jpg";
            string fileName;
            if (saveSettings.backend == SaveBackend.MySQL)
            {
                // Align with CrystalSave's remote naming convention so the server
                // can preserve the original timestamped file name.
                fileName = $"{slotNumber}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.{ext}";
            }
            else
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                fileName = $"Slot_{slotNumber}_{ts}_{Guid.NewGuid():N}.{ext}";
            }
            
            // Generate file path for local storage or Supabase cache
            bool needsLocalFile = SaveLocally || (saveSettings.backend == SaveBackend.Supabase && !SaveLocally);
            string filePath = needsLocalFile && !string.IsNullOrEmpty(screenshotFolderPath)
                ? Path.Combine(screenshotFolderPath, fileName)
                : null;

            byte[] bytes = saveSettings.screenshotFormat == ScreenshotFormat.PNG
                ? normalizedTex.EncodeToPNG()
                : normalizedTex.EncodeToJPG();

            try { bytes = EncryptScreenshot(bytes); }
            finally { UnityEngine.Object.Destroy(tex); UnityEngine.Object.Destroy(normalizedTex); }
            context?.CheckCancellation();
            if (filePath != null)
            {
                context?.Rollback.Add(() => AtomicSaveFile.Delete(filePath));
                RegisterScreenshotRollbackFlush(context);
            }

            /* 3 ─ Write locally (if needed) */
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: if using local storage (cloud disabled or local mirror), write to IndexedDB-backed FS
            if (SaveLocally && filePath != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                    AtomicSaveFile.WriteAllBytes(filePath, bytes);
                    Logger.Log($"Saved WebGL local screenshot file: {filePath}", LogCategory.ScreenshotManager, LogLevel.Info);
                }
                catch (Exception ex)
                {
                    throw new IOException("WebGL screenshot persistence failed.", ex);
                }
            }
            // Always keep an in-memory copy for instant UI display in this session
            if (SaveLocally || saveSettings.backend == SaveBackend.Supabase)
            {
                webglScreenshotCache[fileName] = bytes;
                context?.Rollback.Add(() => webglScreenshotCache.Remove(fileName));
                Logger.Log($"Cached screenshot in WebGL memory: {fileName} ({bytes.Length} bytes)", LogCategory.ScreenshotManager, LogLevel.Info);
            }
#else
            if (SaveLocally && filePath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                AtomicSaveFile.WriteAllBytes(filePath, bytes);
            }
            // For Supabase, save a small local copy even when SaveLocally is false
            // so the UI can load it synchronously without downloading
            else if (saveSettings.backend == SaveBackend.Supabase && 
                     !SaveLocally && 
                     filePath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                AtomicSaveFile.WriteAllBytes(filePath, bytes);
                Logger.Log($"Saved Supabase screenshot cache copy: {filePath}", LogCategory.ScreenshotManager, LogLevel.Info);
            }
#endif

            // Capture prepares the image; a coordinated save publishes it only after
            // required capture, serialization, encryption and verification preparation succeed.
            var publish = PrepareScreenshotUpload(slotNumber, fileName, bytes);
            if (publish != null)
            {
                if (context != null) context.CommitSideEffects.Add(publish);
                else
                {
                    var upload = publish();
                    while (!upload.IsCompleted) yield return null;
                    upload.GetAwaiter().GetResult();
                }
            }

            // For WebGL Unity Cloud Save, return the actual key used for storage
            string finalFileName = fileName;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (saveSettings.backend == SaveBackend.UnityCloudSave && saveSettings.cloudSaveScreenshots && saveSettings.enableCloudSave)
            {
                string dataApiKey = fileName.Contains(".") ? fileName.Substring(0, fileName.LastIndexOf('.')) : fileName;
                finalFileName = $"screenshot_{dataApiKey}";
            }
#endif

            onComplete?.Invoke(finalFileName);             // success
        }

        private Func<Task> PrepareScreenshotUpload(int slotNumber, string fileName, byte[] bytes)
        {
            if (!saveSettings.enableCloudSave) return null;
            var storage = Owner?.SaveSystem;
            var format = saveSettings.screenshotFormat;
            if (storage is SupabaseSaveSystem supabase)
                return () => supabase.UploadScreenshotAsync(fileName, bytes, format);
            if (storage is FirebaseSaveSystem firebase)
                return () => firebase.UploadScreenshotAsync(fileName, bytes, format);
            if (storage is MySqlSaveSystem mysql)
                return () => mysql.UploadScreenshotAsync(slotNumber, fileName, bytes, format);
#if REMEMBERME_CLOUDSAVE_PRESENT
            if (saveSettings.backend == SaveBackend.UnityCloudSave && saveSettings.cloudSaveScreenshots)
            {
                bool encrypt = saveSettings.enableEncryption && saveSettings.encryptScreenshots &&
                    saveSettings.cloudCryptoMode != CloudCryptoMode.ServerSide;
                var encryption = Owner?.EncryptionService;
                if (encrypt && encryption == null)
                    throw new InvalidOperationException("Screenshot encryption service is unavailable.");
                byte[] plain = encrypt ? encryption.MaybeDecrypt(bytes) : bytes;
                if (plain == null) throw new InvalidDataException("Screenshot authentication failed.");
                byte[] compressed = CompressForCloudDataApi(plain);
                if (encrypt) compressed = encryption.MaybeEncrypt(compressed);
                if (compressed == null) throw new InvalidDataException("Screenshot encryption failed.");
                string base64 = Convert.ToBase64String(compressed);
                string key = "screenshot_" + Path.GetFileNameWithoutExtension(fileName);
                if (!ValidateCloudSaveKey(key) || base64.Length > 800000)
                    throw new InvalidDataException("Screenshot exceeds the Unity Cloud Save Data API key or payload limits.");
                var entry = new Dictionary<string, object>
                {
                    { "data", base64 }, { "filename", fileName }, { "format", "jpg" },
                    { "compressed", true }, { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
                };
                var upload = new Dictionary<string, object> { { key, entry } };
                return () => UnityCloudSaveService.Instance.Data.Player.SaveAsync(upload);
            }
#endif
            return null;
        }

        private static void RegisterScreenshotRollbackFlush(SaveOperationContext context)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (context == null) return;
            Func<Task> flush = WebGLFileSystem.FlushAsync;
            if (!context.AsyncRollback.Contains(flush)) context.AsyncRollback.Add(flush);
#endif
        }

        private IEnumerator CaptureTextureCoroutine(Action<Texture2D> onCaptured)
        {
            if (CustomCapture != null)
            {
                onCaptured?.Invoke(CustomCapture());
                yield break;
            }

            if (CustomCaptureAsync != null)
            {
                Task<Texture2D> captureTask;
                try
                {
                    captureTask = CustomCaptureAsync();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Custom screenshot capture failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ScreenshotManager, LogLevel.Error);
                    onCaptured?.Invoke(null);
                    yield break;
                }

                bool handedOff = false;
                try
                {
                    while (captureTask != null && !captureTask.IsCompleted) yield return null;
                    if (captureTask == null) { onCaptured?.Invoke(null); yield break; }
                    if (captureTask.IsCanceled)
                    {
                        Logger.Log("Custom screenshot capture was canceled.", LogCategory.ScreenshotManager, LogLevel.Warning);
                        onCaptured?.Invoke(null); yield break;
                    }
                    if (captureTask.IsFaulted)
                    {
                        Logger.Log($"Custom screenshot capture failed: {captureTask.Exception?.GetBaseException().Message}", LogCategory.ScreenshotManager, LogLevel.Error);
                        onCaptured?.Invoke(null); yield break;
                    }
                    handedOff = true;
                    onCaptured?.Invoke(captureTask.Result);
                }
                finally
                {
                    // A cancelled or timed-out save still owns any texture produced later.
                    if (!handedOff && captureTask != null) DiscardCaptureAsync(captureTask);
                }
                yield break;
            }

            onCaptured?.Invoke(ScreenCapture.CaptureScreenshotAsTexture());
        }


        private static async void DiscardCaptureAsync(Task<Texture2D> capture)
        {
            try
            {
                var texture = await capture; // retain Unity's synchronization context
                if (texture) UnityEngine.Object.Destroy(texture);
            }
            catch (Exception) { /* Observe a late provider failure after the request has ended. */ }
        }

        private byte[] EncryptScreenshot(byte[] bytes)
        {
            if (!saveSettings.enableEncryption || !saveSettings.encryptScreenshots ||
                saveSettings.enableCloudSave && saveSettings.cloudCryptoMode == CloudCryptoMode.ServerSide) return bytes;
            var encryption = Owner?.EncryptionService ?? throw new InvalidOperationException("Screenshot encryption service is unavailable.");
            return encryption.MaybeEncrypt(bytes);
        }
    public string CaptureScreenshotSync(int slotNumber)
        {
            if (!SaveLocally) return null; // makes sense only for local paths

            string folder = screenshotFolderPath ??
                            Path.Combine(rootPath, saveSettings.screenshotFolderName);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string ts   = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string file = $"Slot{slotNumber}_{ts}_{Guid.NewGuid():N}.{saveSettings.screenshotFormat.ToString().ToLower()}";
            string path = Path.Combine(folder, file);

            var tex = CustomCapture != null ? CustomCapture() : null;
            if (tex == null && CustomCaptureAsync != null)
                Logger.Log("CaptureScreenshotSync: async custom capture is not supported in sync mode; using ScreenCapture fallback.", LogCategory.ScreenshotManager, LogLevel.Warning);
            tex ??= ScreenCapture.CaptureScreenshotAsTexture();
            if (tex != null)
            {
                Texture2D normalizedTex = CropAndResize(tex, saveSettings.screenshotFormat);
                byte[] data = saveSettings.screenshotFormat == ScreenshotFormat.PNG
                    ? normalizedTex.EncodeToPNG()
                    : normalizedTex.EncodeToJPG();
                try
                {
                    data = EncryptScreenshot(data);
                    SaveIoScope.Current.Value?.Rollback.Add(() => AtomicSaveFile.Delete(path));
                    AtomicSaveFile.WriteAllBytes(path, data);
                }
                finally { UnityEngine.Object.Destroy(tex); UnityEngine.Object.Destroy(normalizedTex); }
            }
            return file;
        }
        #endregion

        /* ================================================================== */
        #region Load / Cleanup / Delete
        /* ================================================================== */
        /// <summary>Screenshot headers contain a filename or a Cloud Save key, never a path.</summary>
        public static bool IsValidScreenshotFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName == "." || fileName == ".." ||
                fileName.EndsWith(".", StringComparison.Ordinal) || fileName.EndsWith(" ", StringComparison.Ordinal) ||
                System.Text.Encoding.UTF8.GetByteCount(fileName) > 255)
                return false;
            foreach (char c in fileName)
                if (char.IsControl(c) || c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
                    return false;
            return !Path.IsPathRooted(fileName);
        }

        /// <summary>Decode an encrypted or legacy image for display without changing its stored bytes.</summary>
        public static Texture2D DecodeScreenshotTexture(byte[] encoded)
            => ScreenshotImageCodec.DecodeTexture(encoded, SaveManager.Instance?.EncryptionService);

        private Texture2D DecodeOwnedScreenshotTexture(byte[] encoded)
            => ScreenshotImageCodec.DecodeTexture(encoded, Owner?.EncryptionService);

        private byte[] PrepareScreenshotCacheBytes(byte[] encoded, SaveSettings settings)
            => ScreenshotImageCodec.PrepareCacheBytes(encoded, settings, Owner?.EncryptionService);

        private static void ReleaseScreenshotTexture(Texture2D texture) => ScreenshotImageCodec.ReleaseTexture(texture);

        public Texture2D LoadScreenshot(SaveSlot slot)
        {
            if (!saveSettings.enableScreenshots || slot == null || !IsValidScreenshotFileName(slot.ScreenshotFileName) ||
                !(SaveLocally || saveSettings.backend == SaveBackend.Supabase)) return null;

            var names = new List<string> { slot.ScreenshotFileName };
            // WebGL Cloud Save headers may contain the Data API key instead of the
            // original local filename. Keep both historical image formats readable.
            if (slot.ScreenshotFileName.StartsWith("screenshot_", StringComparison.Ordinal) && !slot.ScreenshotFileName.Contains('.'))
            {
                string inferred = slot.ScreenshotFileName.Substring("screenshot_".Length);
                names.Add(inferred + ".png");
                names.Add(inferred + ".jpg");
            }
            foreach (string name in names)
            {
                if (!IsValidScreenshotFileName(name)) continue;
                try
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    if (webglScreenshotCache.TryGetValue(name, out byte[] cached))
                    {
                        var memoryTexture = DecodeOwnedScreenshotTexture(cached);
                        if (memoryTexture != null)
                        {
                            try
                            {
                                byte[] encoded = PrepareScreenshotCacheBytes(cached, saveSettings);
                                webglScreenshotCache[name] = encoded;
                                webglScreenshotCache[slot.ScreenshotFileName] = encoded;
                                return memoryTexture;
                            }
                            catch { ReleaseScreenshotTexture(memoryTexture); throw; }
                        }
                    }
#endif
                    string path = Path.Combine(screenshotFolderPath, name);
                    if (!File.Exists(path)) continue;
                    byte[] stored = AtomicSaveFile.ReadAllBytes(path);
                    var texture = DecodeOwnedScreenshotTexture(stored);
                    if (texture == null) continue;
#if UNITY_WEBGL && !UNITY_EDITOR
                    try
                    {
                        byte[] encoded = PrepareScreenshotCacheBytes(stored, saveSettings);
                        webglScreenshotCache[name] = encoded;
                        webglScreenshotCache[slot.ScreenshotFileName] = encoded;
                    }
                    catch { ReleaseScreenshotTexture(texture); throw; }
#endif
                    return texture;
                }
                catch (Exception ex)
                {
                    Logger.Log($"Screenshot '{name}' could not be loaded: {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                }
            }
            return null;
        }

        private static async Task<T> AwaitScreenshotDownload<T>(Func<Task<T>> start, int timeoutSeconds)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(SaveIoScope.Token);
            using var scope = SaveIoScope.WithReadCancellation(cancellation.Token);
            Task<T> download = null;
            try
            {
                if (timeoutSeconds <= 0) throw new TimeoutException("Screenshot download timed out.");
                DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
                download = start();
                // Unity's frame loop also advances this deadline in WebGL, where managed
                // timers and thread-pool continuations cannot drive Task.Delay/WhenAny.
                while (!download.IsCompleted)
                {
                    SaveIoScope.CheckCancellation();
                    if (DateTime.UtcNow >= deadline) throw new TimeoutException("Screenshot download timed out.");
                    await Task.Yield();
                }
                SaveIoScope.CheckCancellation();
                if (DateTime.UtcNow >= deadline) throw new TimeoutException("Screenshot download timed out.");
                return await download;
            }
            catch
            {
                cancellation.Cancel();
                if (download != null) SaveOperationContext.ObserveLateFailure(download);
                throw;
            }
        }

        private async Task<Texture2D> CacheDownloadedScreenshotAsync(string fileName, byte[] data)
        {
            if (!IsValidScreenshotFileName(fileName)) return null;
            var texture = DecodeOwnedScreenshotTexture(data);
            if (texture == null) return null;
            string path = Path.Combine(screenshotFolderPath, fileName);
            byte[] previous = null;
            bool touched = false;
            try
            {
                byte[] encoded = PrepareScreenshotCacheBytes(data, saveSettings);
                SaveIoScope.CheckCancellation();
                previous = File.Exists(path) ? AtomicSaveFile.ReadAllBytes(path) : null;
                Directory.CreateDirectory(screenshotFolderPath);
                touched = true;
                AtomicSaveFile.WriteAllBytes(path, encoded);
#if UNITY_WEBGL && !UNITY_EDITOR
                await WebGLFileSystem.FlushAsync();
                webglScreenshotCache[fileName] = encoded;
#else
                await Task.CompletedTask;
#endif
                return texture;
            }
            catch
            {
                ReleaseScreenshotTexture(texture);
                if (touched)
                {
                    try
                    {
                        if (previous == null) AtomicSaveFile.Delete(path);
                        else AtomicSaveFile.WriteAllBytes(path, previous);
#if UNITY_WEBGL && !UNITY_EDITOR
                        await WebGLFileSystem.FlushAsync();
#endif
                    }
                    catch (Exception rollback)
                    {
                        Logger.Log($"Screenshot cache rollback failed: {rollback.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                    }
                }
                throw;
            }
        }

        public async Task<Texture2D> DownloadScreenshotAsync(SaveSlot slot, int timeoutSeconds = 10)
        {
            string fileName = slot?.ScreenshotFileName;
            if (!IsValidScreenshotFileName(fileName)) return null;
            try
            {
                var storage = Owner?.SaveSystem;
                Func<Task<byte[]>> download = null;
                if (saveSettings.backend == SaveBackend.Supabase && storage is SupabaseSaveSystem supabase)
                    download = () => supabase.DownloadScreenshotAsync(fileName);
                else if (saveSettings.backend == SaveBackend.Firebase && storage is FirebaseSaveSystem firebase)
                    download = () => firebase.DownloadScreenshotAsync(fileName);
                else if (saveSettings.backend == SaveBackend.MySQL && storage is MySqlSaveSystem mysql)
                    download = () => mysql.DownloadScreenshotAsync(fileName);
                if (download != null)
                {
                    byte[] data = await AwaitScreenshotDownload(download, timeoutSeconds);
                    return await CacheDownloadedScreenshotAsync(fileName, data);
                }
#if REMEMBERME_CLOUDSAVE_PRESENT
                if (saveSettings.backend == SaveBackend.UnityCloudSave && saveSettings.cloudSaveScreenshots)
                {
                    string key = fileName.StartsWith("screenshot_", StringComparison.Ordinal)
                        ? fileName
                        : "screenshot_" + Path.GetFileNameWithoutExtension(fileName);
                    try
                    {
                        var items = await AwaitScreenshotDownload(() => UnityCloudSaveService.Instance.Data.Player.LoadAsync(
                            new HashSet<string> { key }), timeoutSeconds);
                        if (items.TryGetValue(key, out var item))
                        {
                            string base64 = null;
                            try
                            {
                                var structured = item.Value.GetAs<Dictionary<string, object>>();
                                if (structured != null && structured.TryGetValue("data", out var value)) base64 = value?.ToString();
                            }
                            catch (Exception) { /* Legacy entries store the base64 string directly. */ }
                            if (string.IsNullOrEmpty(base64)) base64 = item.Value.GetAs<string>();
                            if (!string.IsNullOrEmpty(base64))
                                return await CacheDownloadedScreenshotAsync(fileName, Convert.FromBase64String(base64));
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (TimeoutException) { throw; }
                    catch (Exception ex)
                    {
                        Logger.Log($"Cloud Save screenshot Data API read failed: {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
                    }
#if !UNITY_WEBGL || UNITY_EDITOR
                    byte[] data = await AwaitScreenshotDownload(() => UnityCloudSaveService.Instance.Files.Player.LoadBytesAsync(fileName), timeoutSeconds);
                    return await CacheDownloadedScreenshotAsync(fileName, data);
#endif
                }
#endif
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to download screenshot '{fileName}': {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
            }
            return null;
        }


        public void CleanupUnusedScreenshots(List<SaveSlot> slots)
        {
            if (!SaveLocally || !saveSettings.enableScreenshots || string.IsNullOrEmpty(screenshotFolderPath))
                return;

            try
            {
                string searchPattern = saveSettings.screenshotFormat == ScreenshotFormat.PNG ? "*.png" : "*.jpg";
                var files = new DirectoryInfo(screenshotFolderPath).GetFiles(searchPattern);

                foreach (FileInfo f in files)
                {
                    if (!slots.Any(s => s.ScreenshotFileName == f.Name))
                    {
                        AtomicSaveFile.Delete(f.FullName);
                        Logger.Log($"Deleted unused screenshot '{f.Name}'.", LogCategory.ScreenshotManager, LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to clean up screenshots: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ScreenshotManager, LogLevel.Error);
            }
        }

        public void DeleteScreenshot(string screenshotFileName)
        {
            // Capture/mirror settings control future images, not ownership of caches
            // already downloaded or created under an earlier configuration.
            if (!IsValidScreenshotFileName(screenshotFileName))
                return;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (webglScreenshotCache.Remove(screenshotFileName))
                Logger.Log($"Deleted screenshot from WebGL memory cache: '{screenshotFileName}'.", LogCategory.ScreenshotManager, LogLevel.Info);
#endif
            if (string.IsNullOrEmpty(screenshotFolderPath)) return;
            string filePath = Path.Combine(screenshotFolderPath, screenshotFileName);
            try
            {
                // Include recovery-only caches even when their primary file is absent.
                AtomicSaveFile.Delete(filePath);
                Logger.Log($"Deleted screenshot at '{filePath}'.", LogCategory.ScreenshotManager, LogLevel.Info);
            }
            catch (Exception ex)
            {
                throw new IOException($"Screenshot cache cleanup failed for '{screenshotFileName}'.", ex);
            }
        }

        internal async Task<string> PrepareScreenshotCopyAsync(string existingFileName, int slotNumber)
        {
            if (!IsValidScreenshotFileName(existingFileName)) return null;
            byte[] bytes = null;
            string folder = screenshotFolderPath ?? Path.Combine(rootPath, saveSettings.screenshotFolderName);
            string source = Path.Combine(folder, Path.GetFileName(existingFileName));
            if (File.Exists(source)) bytes = AtomicSaveFile.ReadAllBytes(source);
            if (bytes == null && saveSettings.enableCloudSave)
            {
                var storage = Owner?.SaveSystem;
                if (storage is MySqlSaveSystem mysql) bytes = await mysql.DownloadScreenshotAsync(existingFileName);
                else if (storage is FirebaseSaveSystem firebase) bytes = await firebase.DownloadScreenshotAsync(existingFileName);
                else if (storage is SupabaseSaveSystem supabase) bytes = await supabase.DownloadScreenshotAsync(existingFileName);
            }
            SaveIoScope.CheckCancellation();
            if (bytes == null || bytes.Length == 0) return null;
            bytes = PrepareScreenshotCacheBytes(bytes, saveSettings);
            string extension = Path.GetExtension(existingFileName);
            string name = $"Slot_{slotNumber}_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}{extension}";
            var context = SaveIoScope.Current.Value;
            if (SaveLocally || saveSettings.backend == SaveBackend.Supabase)
            {
                string path = Path.Combine(folder, name);
                Directory.CreateDirectory(folder);
                context?.Rollback.Add(() => AtomicSaveFile.Delete(path));
                RegisterScreenshotRollbackFlush(context);
                AtomicSaveFile.WriteAllBytes(path, bytes);
            }
            var publish = PrepareScreenshotUpload(slotNumber, name, bytes);
            if (publish != null)
            {
                if (context == null) throw new InvalidOperationException("Screenshot copies require an active save operation.");
                context.CommitSideEffects.Add(publish);
            }
            return name;
        }

        public string DuplicateScreenshot(string existingFileName, int slotNumber)
        {
            if (!SaveLocally || !saveSettings.enableScreenshots || !IsValidScreenshotFileName(existingFileName))
                return null;
            if (string.IsNullOrEmpty(screenshotFolderPath))
                return null;

            string srcPath = Path.Combine(screenshotFolderPath, existingFileName);
            if (!File.Exists(srcPath))
                return null;

            string ext = Path.GetExtension(existingFileName);
            string newName = $"Slot_{slotNumber}_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}";
            string dstPath = Path.Combine(screenshotFolderPath, newName);
            AtomicSaveFile.WriteAllBytes(dstPath, PrepareScreenshotCacheBytes(AtomicSaveFile.ReadAllBytes(srcPath), saveSettings));
            return newName;
        }

        /// <summary>
        /// Compresses screenshot data for Unity Cloud Save Data API uploads.
        /// Cross-platform: clamps longest side to 512px and encodes JPG at ~50% quality.
        /// </summary>
        private byte[] CompressForCloudDataApi(byte[] originalBytes)
        {
            try
            {
                // Load the original texture
                Texture2D originalTex = new Texture2D(2, 2);
                if (!originalTex.LoadImage(originalBytes))
                {
                    UnityEngine.Object.Destroy(originalTex);
                    Logger.Log("Failed to load original screenshot for compression", LogCategory.ScreenshotManager, LogLevel.Warning);
                    return originalBytes;
                }

                // Clamp longest side to 512 (maintain aspect)
                const int MaxDim = 512;
                float scale = Mathf.Min(1f, (float)MaxDim / Mathf.Max(originalTex.width, originalTex.height));
                int newWidth = Mathf.Max(1, Mathf.RoundToInt(originalTex.width * scale));
                int newHeight = Mathf.Max(1, Mathf.RoundToInt(originalTex.height * scale));

                // Downscale via RenderTexture for quality
                RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(originalTex, rt);
                
                RenderTexture.active = rt;
                Texture2D downscaled = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
                downscaled.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
                downscaled.Apply();
                
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(originalTex);

                // Encode JPG at ~50% quality
                byte[] compressedBytes = downscaled.EncodeToJPG(50);
                UnityEngine.Object.Destroy(downscaled);

                Logger.Log($"Cloud Data API screenshot compressed: {originalBytes.Length} -> {compressedBytes.Length} bytes", LogCategory.ScreenshotManager, LogLevel.Info);
                return compressedBytes;
            }
            catch (System.Exception ex)
            {
                Logger.Log($"Screenshot compression failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ScreenshotManager, LogLevel.Warning);
                return originalBytes;
            }
        }
        
        /// <summary>
        /// Validates a key for Unity Cloud Save compatibility
        /// </summary>
        private bool ValidateCloudSaveKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Logger.Log("Cloud Save key is null or empty", LogCategory.ScreenshotManager, LogLevel.Error);
                return false;
            }
            
            // Unity Cloud Save key length limit (typically 255 characters)
            if (key.Length > 255)
            {
                Logger.Log($"Cloud Save key too long: {key.Length} characters (max 255)", LogCategory.ScreenshotManager, LogLevel.Error);
                return false;
            }
            
            // Check for invalid characters - Data API is more restrictive than Files API
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    Logger.Log($"Cloud Save key contains invalid character '{c}' at position {i} in key '{key}'. Data API only allows alphanumeric and underscore.", LogCategory.ScreenshotManager, LogLevel.Error);
                    return false;
                }
            }
            
            // Check for leading/trailing underscores or starting with numbers (often not allowed)
            if (key.StartsWith("_") || key.EndsWith("_") || char.IsDigit(key[0]))
            {
                Logger.Log($"Cloud Save key has invalid format: '{key}'. Cannot start with underscore or digit, or end with underscore.", LogLevel.Error);
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates if a string is valid base64
        /// </summary>
        private bool IsValidBase64(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return false;
                
            // Base64 length must be multiple of 4
            if (base64String.Length % 4 != 0)
                return false;
                
            try
            {
                Convert.FromBase64String(base64String);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        #endregion

        /* ================================================================== */
        #region Crop & Resize Helpers
        /* ================================================================== */
        /// <summary>
        /// Center-crops the source to 16:9 (landscape) or 9:16 (portrait) based on orientation,
        /// then scales down so neither dimension exceeds 1920px. Never scales up.
        /// </summary>
        private Texture2D CropAndResize(Texture2D source, ScreenshotFormat format)
        {
            if (source == null) return null;

            int srcW = source.width;
            int srcH = source.height;
            bool portrait = srcH > srcW;
            float targetAspect = portrait ? (9f / 16f) : (16f / 9f);
            float srcAspect = (float)srcW / srcH;

            // Determine crop rectangle (centered)
            int cropW = srcW;
            int cropH = srcH;
            int cropX = 0;
            int cropY = 0;

            if (srcAspect > targetAspect)
            {
                // Too wide -> crop width
                cropW = Mathf.FloorToInt(srcH * targetAspect);
                cropX = (srcW - cropW) / 2;
            }
            else if (srcAspect < targetAspect)
            {
                // Too tall -> crop height
                cropH = Mathf.FloorToInt(srcW / targetAspect);
                cropY = (srcH - cropH) / 2;
            }

            // Create cropped texture (CPU copy to keep it simple and robust)
            var croppedFormat = format == ScreenshotFormat.PNG ? TextureFormat.RGBA32 : TextureFormat.RGB24;
            Texture2D cropped = new Texture2D(cropW, cropH, croppedFormat, false);
            var pixels = source.GetPixels(cropX, cropY, cropW, cropH);
            cropped.SetPixels(pixels);
            cropped.Apply(false, false);

            // Determine final size (clamp to max 1920 on both axes, no upscaling)
            const int MAX_DIM = 1920;
            float scale = Mathf.Min(1f, Mathf.Min((float)MAX_DIM / cropW, (float)MAX_DIM / cropH));
            int finalW = Mathf.Max(1, Mathf.RoundToInt(cropW * scale));
            int finalH = Mathf.Max(1, Mathf.RoundToInt(cropH * scale));

            if (finalW == cropW && finalH == cropH)
            {
                // Already within limits
                return cropped;
            }

            // Scale with RenderTexture for quality
            RenderTexture rt = RenderTexture.GetTemporary(finalW, finalH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevActive = RenderTexture.active;
            try
            {
                Graphics.Blit(cropped, rt);
                RenderTexture.active = rt;
                Texture2D finalTex = new Texture2D(finalW, finalH, croppedFormat, false);
                finalTex.ReadPixels(new Rect(0, 0, finalW, finalH), 0, 0);
                finalTex.Apply(false, false);

                UnityEngine.Object.Destroy(cropped);
                return finalTex;
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
        #endregion
    }
}
#endif
