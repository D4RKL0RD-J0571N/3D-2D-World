#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Handles creation and management of <see cref="SaveSlot"/> instances.
    /// </summary>
    public class SaveSlotManager
    {
        private readonly ISaveSystem     saveSystem;
        private readonly SaveSettings    saveSettings;
        private readonly ScreenshotManager screenshotManager;
        private readonly string rootPath;
        private readonly SaveManager owner;
        private SaveManager Owner => owner ?? SaveManager.Instance;
        private readonly IReadOnlyList<SaveSlot> managedSlots;

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
        private static bool SignedIn =>
            Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn;
#else
        private static bool SignedIn => false;
#endif

        private bool CloudSignedIn => saveSettings.backend == SaveBackend.MySQL || 
                                      saveSettings.backend == SaveBackend.Supabase ||
                                      saveSettings.backend == SaveBackend.Firebase ||
                                      SignedIn;

        private static string Sanitize(string key)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                key = key.Replace(c, '_');
            return key;
        }

        // Tracks in-flight and completed cloud existence probes
        private readonly Dictionary<int, Task> cloudProbes = new();
        private readonly Dictionary<int, bool> cloudExistence = new();
        private readonly Dictionary<int, object> cloudProbeOwners = new();
        
        // Instance-level initialization tracking
        private bool isInitializing = false;

        private async Task<bool> SaveFileExistsAsync(SaveSlot slot, bool hasMetaData)
        {
            _ = hasMetaData; // Retained for API compatibility; accessible payload bytes determine existence.
            bool localExists = saveSystem is ILocalSavePresence presence
                ? presence.HasLocalSave(slot) : HasConventionalLocalSave(slot);
            if (localExists) return true;
            if (!saveSettings.enableCloudSave) return false;
            if (cloudExistence.TryGetValue(slot.SlotNumber, out bool exists)) return exists;

            BeginCloudProbe(slot);
            if (cloudExistence.TryGetValue(slot.SlotNumber, out exists)) return exists;
            Task probe;
            lock (cloudProbes) cloudProbes.TryGetValue(slot.SlotNumber, out probe);
            if (probe != null) await probe;
            return cloudExistence.TryGetValue(slot.SlotNumber, out exists) && exists;
        }

        // Custom backends retain the historical convention without requiring a new interface member.
        private bool HasConventionalLocalSave(SaveSlot slot)
        {
            if (saveSettings.saveMethod == SaveMethod.PlayerPrefs)
                return PlayerPrefs.HasKey(SaveSlotKey.Resolve(saveSettings.saveKey, slot.SlotNumber)) ||
                       PlayerPrefs.HasKey(saveSettings.saveKey + slot.SlotNumber);
            string name = NamePatternResolver.Resolve(saveSettings.saveFileName, slot) + ".sav";
            string folder = Path.Combine(rootPath, saveSettings.ResolveSlotFolderName(slot.SlotNumber));
            if (!Directory.Exists(folder)) folder = Path.Combine(rootPath, $"slot{slot.SlotNumber}");
            return AtomicSaveFile.Exists(Path.Combine(folder, name)) ||
                   AtomicSaveFile.Exists(Path.Combine(rootPath, name));
        }

    private void BeginCloudProbe(SaveSlot slot)
        {
            lock (cloudProbes)
            {
                if (cloudProbes.ContainsKey(slot.SlotNumber)) return;
                var owner = new object();
                cloudProbeOwners[slot.SlotNumber] = owner;
                var probeTask = ProbeCloudAsync(slot, owner);
                if (!probeTask.IsCompleted) cloudProbes[slot.SlotNumber] = probeTask;
            }
        }

        private async Task ProbeCloudAsync(SaveSlot slot, object owner)
        {
            bool exists = false;
            bool readSucceeded = false;
            try
            {
                var bytes = await saveSystem.LoadAsync(slot);
                exists = bytes != null && bytes.Length > 0;
                readSucceeded = true;
                Logger.Log($"SaveSlotManager: cloud probe for slot {slot.SlotNumber}: exists={exists}, bytes={bytes?.Length ?? 0}", LogCategory.SaveSlotManager, LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log($"SaveSlotManager: cloud probe failed for slot {slot.SlotNumber}: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveSlotManager, LogLevel.Warning);
            }
            finally
            {
                bool publish = readSucceeded;
                try { SaveIoScope.CheckCancellation(); } catch (OperationCanceledException) { publish = false; }
                lock (cloudProbes)
                {
                    if (cloudProbeOwners.TryGetValue(slot.SlotNumber, out var current) && ReferenceEquals(current, owner))
                    {
                        cloudProbeOwners.Remove(slot.SlotNumber);
                        cloudProbes.Remove(slot.SlotNumber);
                        if (publish) lock (cloudExistence) cloudExistence[slot.SlotNumber] = exists;
                    }
                }
            }
        }

        private Task<bool> SlotHasAccessibleDataAsync(SaveSlot slot, bool hasMetaData)
            => SaveFileExistsAsync(slot, hasMetaData);

        private bool CompleteLocalQuery(Func<Task<bool>> query)
        {
            if (saveSettings.enableCloudSave)
                throw new InvalidOperationException("Use the asynchronous slot query API when cloud saving is enabled.");
            // Local queries complete inline. Keep PlayerPrefs and Unity logging on the caller's main thread.
            return query().GetAwaiter().GetResult();
        }

        // Synchronous wrappers for legacy callers
        private bool SaveFileExists(SaveSlot slot, bool hasMetaData)
            => CompleteLocalQuery(() => SaveFileExistsAsync(slot, hasMetaData));

        private bool SlotHasAccessibleData(SaveSlot slot, bool hasMetaData)
            => SaveFileExists(slot, hasMetaData);

        /// <summary>
        /// The list of managed save slots.
        /// </summary>
        public List<SaveSlot> Slots { get; } = new();

        /// <summary>A live read-only view of published slot membership.</summary>
        public IReadOnlyList<SaveSlot> ManagedSlots => managedSlots;

        public SaveSlotManager(ISaveSystem saveSystem,
                               SaveSettings settings,
                               ScreenshotManager screenshotManager,
                               string rootPath)
        {
            this.saveSystem       = saveSystem;
            this.saveSettings     = settings;
            this.screenshotManager = screenshotManager;
            this.rootPath         = rootPath;
            managedSlots = Slots.AsReadOnly();
        }

        internal SaveSlotManager(ISaveSystem saveSystem, SaveSettings settings,
            ScreenshotManager screenshotManager, string rootPath, SaveManager owner)
            : this(saveSystem, settings, screenshotManager, rootPath)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>
        /// Loads slot metadata from the underlying save system or creates
        /// default placeholders when none exist.
        /// </summary>
        public Task InitializeAsync(int numberOfSlots, int startIndex = 1)
            => InitializeCoreAsync(numberOfSlots, startIndex, false);

        internal Task InitializeStrictAsync(int numberOfSlots, int startIndex = 1)
            => InitializeCoreAsync(numberOfSlots, startIndex, true);

        private async Task InitializeCoreAsync(int numberOfSlots, int startIndex, bool rejectMetadataFailure)
        {
            // Instance-level protection - prevent this specific manager from initializing multiple times
            if (isInitializing)
            {
                Logger.Log($"[SaveSlotManager] This instance already initializing, skipping duplicate call", LogCategory.SaveSlotManager, LogLevel.Warning);
                return;
            }
            
            isInitializing = true;
            
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
            Logger.Log($"[SaveSlotManager] WebGL: InitializeAsync called with {numberOfSlots} slots, startIndex={startIndex}. Current Slots.Count before clear: {Slots.Count}", LogCategory.SaveSlotManager, LogLevel.Info);
#endif
            Logger.Log($"[SaveSlotManager] InitializeAsync: {numberOfSlots} slots, backend={saveSettings.backend}, keepLocalMirror={saveSettings.keepLocalMirror}", LogCategory.SaveSlotManager, LogLevel.Info);
            
            ReplaceSlots(Array.Empty<SaveSlot>());
#if UNITY_WEBGL && !UNITY_EDITOR
            Logger.Log($"[SaveSlotManager] WebGL: After Slots.Clear(), Slots.Count: {Slots.Count}", LogCategory.SaveSlotManager, LogLevel.Info);
#endif
            for (int i = 0; i < numberOfSlots; i++)
            {
                int slotNumber = startIndex + i;
                SaveSlot loaded = null;

#if UNITY_WEBGL && !UNITY_EDITOR
                Logger.Log($"[SaveSlotManager] WebGL: Processing slot {slotNumber} ({i+1}/{numberOfSlots})", LogCategory.SaveSlotManager, LogLevel.Info);
#endif

                try
                {
                    loaded = await saveSystem.LoadSlotMetadataAsync(slotNumber);
                    if (loaded != null)
                    {
                        Logger.Log($"[SaveSlotManager] Loaded slot {slotNumber}: Name='{loaded.SlotName}', LastSaved={loaded.LastSaved}, Screenshot='{loaded.ScreenshotFileName ?? "null"}'", LogCategory.SaveSlotManager, LogLevel.Info);
                    }
                    else
                    {
                        Logger.Log($"[SaveSlotManager] No metadata found for slot {slotNumber}", LogCategory.SaveSlotManager, LogLevel.Info);
                    }
                }
                catch (Exception ex)
                {
                    if (rejectMetadataFailure)
                        throw new IOException($"Could not initialize save slot {slotNumber}: metadata read failed.", ex);
                    Logger.Log($"Failed to load slot metadata for {slotNumber}: {ex.Message}\nStack Trace: {ex.StackTrace}",
                               LogCategory.SaveSlotManager, LogLevel.Warning);
#if UNITY_WEBGL && !UNITY_EDITOR
                    Logger.Log($"[SaveSlotManager] WebGL: Exception loading slot {slotNumber}: {ex.Message}\n{ex.StackTrace}", LogCategory.SaveSlotManager, LogLevel.Error);
#endif
                }

                // Without a local mirror and no cloud connection there's no
                // accessible data for this slot; drop any cached metadata so
                // it doesn't appear as a valid save.
                bool useLocalMirror = saveSettings.keepLocalMirror &&
                                      saveSettings.cloudCryptoMode != CloudCryptoMode.ServerSide;
                if (loaded != null &&
                    saveSettings.enableCloudSave &&
                    !useLocalMirror &&
                    !CloudSignedIn)
                {
                    Logger.Log($"[SaveSlotManager] WARNING: Dropping loaded metadata for slot {slotNumber} because CloudSignedIn={CloudSignedIn}, backend={saveSettings.backend}", LogCategory.SaveSlotManager, LogLevel.Warning);
                    loaded = null;
                }
                else if (loaded != null)
                {
                    Logger.Log($"[SaveSlotManager] Keeping loaded metadata for slot {slotNumber}: CloudSignedIn={CloudSignedIn}, backend={saveSettings.backend}", LogCategory.SaveSlotManager, LogLevel.Info);
                }

                var slot = loaded ?? new SaveSlot(slotNumber, $"Slot {slotNumber}",
                                                  DateTime.MinValue, string.Empty, string.Empty);
                if (loaded == null)
                {
                    Logger.Log($"[SaveSlotManager] Created empty slot {slotNumber}", LogCategory.SaveSlotManager, LogLevel.Info);
                }
                Slots.Add(slot);

#if UNITY_WEBGL && !UNITY_EDITOR
                Logger.Log($"[SaveSlotManager] WebGL: Added slot {slotNumber} to Slots list. Current count: {Slots.Count}", LogCategory.SaveSlotManager, LogLevel.Info);
#endif

                if (saveSettings.enableCloudSave && slot.LastSaved == DateTime.MinValue)
                    BeginCloudProbe(slot);
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            Logger.Log($"[SaveSlotManager] WebGL: InitializeAsync completed. Final Slots.Count: {Slots.Count}", LogCategory.SaveSlotManager, LogLevel.Info);
#endif
            }
            finally
            {
                isInitializing = false;
            }
        }

        public SaveSlot GetByNumber(int slotNumber)
            => Slots.Find(s => s.SlotNumber == slotNumber);

        public SaveSlot GetByName(string slotName)
            => Slots.Find(s => s.SlotName.Equals(slotName, StringComparison.OrdinalIgnoreCase));

        public List<SaveSlot> GetAll() => new List<SaveSlot>(Slots);

        public SaveSlot GetLatest()
        {
            if (Slots.Count == 0) return null;
            SaveSlot latest = Slots[0];
            foreach (var s in Slots)
            {
                if (s.LastSaved > latest.LastSaved)
                    latest = s;
            }
            return latest;
        }

    public async Task<bool> HasSaveAsync(bool hasMetaData = true)
        {
            Logger.Log($"SaveSlotManager: HasSaveAsync called with {Slots.Count} slots", LogCategory.SaveSlotManager, LogLevel.Info);
            foreach (var s in GetAll())
            {
        bool exists = await SaveFileExistsAsync(s, hasMetaData);
                Logger.Log($"SaveSlotManager: Slot {s.SlotNumber} exists: {exists}", LogCategory.SaveSlotManager, LogLevel.Info);
                if (exists)
                    return true;
            }
            Logger.Log($"SaveSlotManager: HasSaveAsync returning false", LogCategory.SaveSlotManager, LogLevel.Info);
            return false;
        }

        public bool HasSave(bool hasMetaData = true)
            => CompleteLocalQuery(() => HasSaveAsync(hasMetaData));

        public async Task<bool> HasSaveAtAsync(int slotNumber, bool hasMetaData = true)
        {
            var slot = GetByNumber(slotNumber);
            return slot != null && await SaveFileExistsAsync(slot, hasMetaData);
        }

        public bool HasSaveAt(int slotNumber, bool hasMetaData = true)
            => CompleteLocalQuery(() => HasSaveAtAsync(slotNumber, hasMetaData));

    public async Task<bool> HasSaveAfterAsync(DateTime date, bool hasMetaData = true)
        {
            if (hasMetaData)
            {
                foreach (var s in GetAll())
                {
            if (s.LastSaved > date && await SlotHasAccessibleDataAsync(s, true))
                        return true;
                }
                return false;
            }
            else
            {
                foreach (var s in GetAll())
                {
            // Check both date and file existence when hasMetaData=false
            if (s.LastSaved > date && await SaveFileExistsAsync(s, false))
                        return true;
                }
                return false;
            }
        }

        public bool HasSaveAfter(DateTime date, bool hasMetaData = true)
            => CompleteLocalQuery(() => HasSaveAfterAsync(date, hasMetaData));

        public async Task<bool> HasSaveInSceneAsync(string sceneName, bool hasMetaData = true)
        {
            Logger.Log($"HasSaveInSceneAsync: Checking for scene '{sceneName}', hasMetaData={hasMetaData}, Slots.Count={Slots.Count}", LogCategory.SaveSlotManager, LogLevel.Info);
            
            if (string.IsNullOrEmpty(sceneName))
            {
                Logger.Log("HasSaveInSceneAsync: sceneName is null or empty, returning false", LogCategory.SaveSlotManager, LogLevel.Warning);
                return false;
            }

            if (hasMetaData)
            {
                foreach (var s in GetAll())
                {
                    Logger.Log($"HasSaveInSceneAsync: Checking slot {s.SlotNumber}: LastActiveScene='{s.LastActiveScene}', LastSaved={s.LastSaved}", LogCategory.SaveSlotManager, LogLevel.Info);
                    bool sceneMatches = string.Equals(s.LastActiveScene, sceneName, StringComparison.OrdinalIgnoreCase);
                    bool hasData = await SlotHasAccessibleDataAsync(s, true);
                    Logger.Log($"HasSaveInSceneAsync: Slot {s.SlotNumber} - sceneMatches={sceneMatches}, hasData={hasData}", LogCategory.SaveSlotManager, LogLevel.Info);
                    
                    if (sceneMatches && hasData)
                    {
                        Logger.Log($"HasSaveInSceneAsync: Found match in slot {s.SlotNumber}, returning true", LogCategory.SaveSlotManager, LogLevel.Info);
                        return true;
                    }
                }
                Logger.Log("HasSaveInSceneAsync: No matching slots found, returning false", LogCategory.SaveSlotManager, LogLevel.Info);
                return false;
            }
            else
            {
                // When hasMetaData=false: check scene name matches AND save file exists
                foreach (var s in GetAll())
                {
                    Logger.Log($"HasSaveInSceneAsync (no metadata): Checking slot {s.SlotNumber}: LastActiveScene='{s.LastActiveScene}'", LogCategory.SaveSlotManager, LogLevel.Info);
                    bool sceneMatches = string.Equals(s.LastActiveScene, sceneName, StringComparison.OrdinalIgnoreCase);
                    bool fileExists = await SaveFileExistsAsync(s, false);
                    Logger.Log($"HasSaveInSceneAsync (no metadata): Slot {s.SlotNumber} - sceneMatches={sceneMatches}, fileExists={fileExists}", LogCategory.SaveSlotManager, LogLevel.Info);
                    
                    if (sceneMatches && fileExists)
                    {
                        Logger.Log($"HasSaveInSceneAsync (no metadata): Found match in slot {s.SlotNumber}, returning true", LogCategory.SaveSlotManager, LogLevel.Info);
                        return true;
                    }
                }
                Logger.Log("HasSaveInSceneAsync (no metadata): No matching slots found, returning false", LogCategory.SaveSlotManager, LogLevel.Info);
                return false;
            }
        }

        public bool HasSaveInScene(string sceneName, bool hasMetaData = true)
            => CompleteLocalQuery(() => HasSaveInSceneAsync(sceneName, hasMetaData));

        public bool Rename(int slotNumber, string newName)
        {
            if (string.IsNullOrEmpty(newName))
                return false;

            var slot = GetByNumber(slotNumber);
            if (slot == null)
                return false;

            slot.SlotName = newName;
            return true;
        }

        /// <summary>
        /// Clear cloud existence cache for a slot to force re-probing.
        /// Call this after save operations to ensure fresh checks.
        /// </summary>
        public void InvalidateCloudCache(int slotNumber)
        {
            lock (cloudProbes)
            {
                cloudProbeOwners.Remove(slotNumber);
                cloudProbes.Remove(slotNumber);
                lock (cloudExistence) cloudExistence.Remove(slotNumber);
            }
        }

        internal void ReplaceSlots(IEnumerable<SaveSlot> slots)
        {
            // Materialize before invalidation: callers may pass the live view, and
            // an enumeration failure must leave the published generation intact.
            var replacement = new List<SaveSlot>(slots);
            lock (cloudProbes)
            {
                cloudProbeOwners.Clear();
                cloudProbes.Clear();
                lock (cloudExistence) cloudExistence.Clear();
            }
            Slots.Clear();
            Slots.AddRange(replacement);
        }

        internal void AddSlotInNumberOrder(SaveSlot slot)
        {
            InvalidateCloudCache(slot.SlotNumber);
            Slots.Add(slot);
            Slots.Sort((left, right) => left.SlotNumber.CompareTo(right.SlotNumber));
        }

        internal void PublishSlot(SaveSlot slot)
        {
            int index = Slots.FindIndex(candidate => candidate.SlotNumber == slot.SlotNumber);
            if (index < 0) AddSlotInNumberOrder(slot);
            else
            {
                InvalidateCloudCache(slot.SlotNumber);
                Slots[index] = slot;
            }
        }

        internal void AppendHistorySlot(SaveSlot slot)
        {
            InvalidateCloudCache(slot.SlotNumber);
            var existing = GetByNumber(slot.SlotNumber);
            if (existing != null) Slots.Remove(existing);
            Slots.Add(slot);
        }

        internal void SynchronizeSlot(SaveSlot slot, bool addMissing)
        {
            var existing = GetByNumber(slot.SlotNumber);
            if (existing != null)
            {
                InvalidateCloudCache(slot.SlotNumber);
                CopyPublishedMetadata(slot, existing);
            }
            else if (addMissing) AddSlotInNumberOrder(slot);
        }

        internal static void CopyPublishedMetadata(SaveSlot source, SaveSlot destination)
        {
            destination.SlotName = source.SlotName;
            destination.LastSaved = source.LastSaved;
            destination.LastActiveScene = source.LastActiveScene;
            destination.ScreenshotFileName = source.ScreenshotFileName;
            destination.CustomMetadata = source.CustomMetadata == null
                ? new Dictionary<string, string>() : new Dictionary<string, string>(source.CustomMetadata);
        }

        public async Task<bool> DeleteAsync(int slotNumber)
        {
            var slot = GetByNumber(slotNumber);
            if (slot == null)
                return false;

            await saveSystem.DeleteAsync(slot);

            if (saveSettings.enableScreenshots && !string.IsNullOrEmpty(slot.ScreenshotFileName))
                screenshotManager.DeleteScreenshot(slot.ScreenshotFileName);

            // Retire in-flight probes along with cached existence for the deleted generation.
            InvalidateCloudCache(slotNumber);

            // Reset basic slot information after deletion
            slot.SlotName = $"Slot {slotNumber}";
            slot.LastSaved = DateTime.MinValue;
            slot.ScreenshotFileName = string.Empty;
            slot.LastActiveScene = string.Empty;

            slot.CustomMetadata?.Clear();
            // The owner already holds the operation coordinator. A queued refresh here would
            // wait for this delete to finish and deadlock it; the local result is authoritative.
            var manager = Owner;
            if (manager != null)
                manager.PublishDeletedSlot(slot);

            return true;
        }
    }
}
#endif
