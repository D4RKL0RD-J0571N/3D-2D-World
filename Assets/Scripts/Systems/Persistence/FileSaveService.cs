namespace Systems.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Core;
    using Core.Content;
    using Core.Diagnostics;
    using Core.Ids;
    using Core.Persistence;
    using Data.Config;
    using Domain.Persistence;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Systems.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Modular persistence service implementing the Split Save Architecture (<c>SaveEnvelope</c> JSON + <c>.vxlbin</c> binary voxel sidecar).
    /// Supports atomic <c>.tmp -> .bak -> .json</c> replacement, two-pass <see cref="InstanceGuid"/> resolution,
    /// <see cref="ISaveableService"/> module blocks, <see cref="ISaveableComponent"/> entity states,
    /// and non-destructive <see cref="UnknownContentPlaceholder"/> tombstones when mods are uninstalled (<c>SAV-05</c>).
    /// </summary>
    public class FileSaveService : IGameService, IWorldInstanceResolver
    {
        #region Fields

        private readonly List<ISaveableService> _saveableServices = new();
        private readonly List<UnknownContentPlaceholder> _lastLoadedUnknownPlaceholders = new();
        private Func<IReadOnlyList<VoxelChunkPayload>> _voxelCaptureCallback;
        private Action<IReadOnlyList<VoxelChunkPayload>> _voxelRestoreCallback;
        private string _customSaveDirectory;

        #endregion

        #region Properties

        /// <summary>Directory where save slots (<c>.json</c> and <c>.vxlbin</c>) are stored.</summary>
        public string SaveDirectory
        {
            get => string.IsNullOrWhiteSpace(_customSaveDirectory)
                ? Application.persistentDataPath
                : _customSaveDirectory;
            set => _customSaveDirectory = value;
        }

        /// <summary>Legacy single-file save path retained for backward compatibility.</summary>
        private string LegacySavePath => Path.Combine(SaveDirectory, "save_game.json");

        /// <summary>Unresolved mod content tombstones captured during the most recent load operation (<c>SAV-05</c>).</summary>
        public IReadOnlyList<UnknownContentPlaceholder> LastLoadedUnknownPlaceholders => _lastLoadedUnknownPlaceholders;

        #endregion

        #region IGameService Lifecycle

        public void Initialize()
        {
            _saveableServices.Clear();
            _lastLoadedUnknownPlaceholders.Clear();

            GameEventManager.AddListener<GameSaveRequestedEvent>(OnGameSaveRequested);
            GameEventManager.AddListener<GameLoadRequestedEvent>(OnGameLoadRequested);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<GameSaveRequestedEvent>(OnGameSaveRequested);
            GameEventManager.RemoveListener<GameLoadRequestedEvent>(OnGameLoadRequested);

            _saveableServices.Clear();
            _voxelCaptureCallback = null;
            _voxelRestoreCallback = null;
        }

        #endregion

        #region Registration API

        /// <summary>Registers an <see cref="ISaveableService"/> to participate in <see cref="SaveEnvelope.ModuleStates"/>.</summary>
        public void RegisterSaveableService(ISaveableService service)
        {
            if (service == null || _saveableServices.Contains(service)) return;
            _saveableServices.Add(service);
        }

        /// <summary>Registers callbacks for capturing and restoring binary voxel chunks (<c>.vxlbin</c>).</summary>
        public void RegisterVoxelProvider(
            Func<IReadOnlyList<VoxelChunkPayload>> captureCallback,
            Action<IReadOnlyList<VoxelChunkPayload>> restoreCallback)
        {
            _voxelCaptureCallback = captureCallback;
            _voxelRestoreCallback = restoreCallback;
        }

        #endregion

        #region SaveEnvelope Capture, Atomic Disk Write & Load

        /// <summary>
        /// Captures the complete runtime state of all <see cref="ISaveableService"/>s and active <see cref="PersistentEntityComponent"/>s
        /// into an in-memory <see cref="SaveEnvelope"/>.
        /// </summary>
        public SaveEnvelope CaptureEnvelope(string slotName = "slot_01")
        {
            EnsureBuiltInSaveableServicesRegistered();
            JsonSerializerSettings settings = FantasyEngineJsonSettings.CreateDefault(indented: false);

            var envelope = new SaveEnvelope
            {
                SchemaVersion = 2,
                EngineVersion = "1.0.0",
                SaveSlotName = string.IsNullOrWhiteSpace(slotName) ? "slot_01" : slotName,
                SavedAtUtc = DateTime.UtcNow.ToString("O"),
                ActiveWorldId = new ResourceId("nordic_rpg", "world/proving_grounds")
            };

            if (ServiceLocator.TryGet<IContentService>(out var content))
            {
                envelope.VoxelPaletteMap = VoxelPayloadCodec.BuildPaletteMap(content);
            }

            // 1. Capture all ISaveableService module states
            for (int i = 0; i < _saveableServices.Count; i++)
            {
                ISaveableService svc = _saveableServices[i];
                if (svc == null || svc.SaveStateKey.IsEmpty) continue;

                object stateObj = svc.CaptureState();
                if (stateObj != null)
                {
                    string key = svc.SaveStateKey.Value;
                    envelope.ModuleStates[key] = new ModuleSaveBlock
                    {
                        ModuleKey = key,
                        SchemaVersion = svc.CurrentStateSchemaVersion,
                        PayloadJson = JsonConvert.SerializeObject(stateObj, settings)
                    };
                }
            }

            // 2. Capture all active PersistentEntityComponents (snapshot values to avoid collection modification during capture)
            var activeSnapshot = new List<PersistentEntityComponent>(PersistentEntityComponent.ActiveEntities.Values);
            for (int i = 0; i < activeSnapshot.Count; i++)
            {
                PersistentEntityComponent entity = activeSnapshot[i];
                if (entity == null || !entity.gameObject.activeInHierarchy) continue;

                envelope.Entities.Add(entity.CaptureRecord(settings));
            }

            // Preserve any existing unresolved placeholders from earlier loads
            for (int i = 0; i < _lastLoadedUnknownPlaceholders.Count; i++)
            {
                UnknownContentPlaceholder existing = _lastLoadedUnknownPlaceholders[i];
                if (existing != null && existing.Category == "entity")
                {
                    envelope.UnknownContentPlaceholders.Add(existing);
                }
            }

            return envelope;
        }

        /// <summary>
        /// Restores all <see cref="ISaveableService"/> module states, reconciles <see cref="EntitySaveRecord"/>s by <see cref="InstanceGuid"/>,
        /// and preserves <see cref="UnknownContentPlaceholder"/> tombstones for any missing mod definitions (<c>SAV-05</c>).
        /// </summary>
        public void RestoreEnvelope(SaveEnvelope envelope, IReadOnlyList<VoxelChunkPayload> voxelChunks = null)
        {
            if (envelope == null) return;

            EnsureBuiltInSaveableServicesRegistered();
            _lastLoadedUnknownPlaceholders.Clear();

            if (envelope.UnknownContentPlaceholders != null)
            {
                _lastLoadedUnknownPlaceholders.AddRange(envelope.UnknownContentPlaceholders);
            }

            IContentService content = ServiceLocator.TryGet<IContentService>(out var cs) ? cs : null;

            // 1. Restore voxel chunks if provided (remapping palette indices if needed)
            if (voxelChunks != null && voxelChunks.Count > 0)
            {
                if (content != null && envelope.VoxelPaletteMap != null && envelope.VoxelPaletteMap.Count > 0)
                {
                    for (int i = 0; i < voxelChunks.Count; i++)
                    {
                        VoxelPayloadCodec.RemapVoxelBufferIfNeeded(voxelChunks[i].Voxels, envelope.VoxelPaletteMap, content);
                    }
                }

                _voxelRestoreCallback?.Invoke(voxelChunks);
            }

            // 2. Restore ISaveableService module states
            if (envelope.ModuleStates != null)
            {
                for (int i = 0; i < _saveableServices.Count; i++)
                {
                    ISaveableService svc = _saveableServices[i];
                    if (svc == null || svc.SaveStateKey.IsEmpty) continue;

                    if (envelope.ModuleStates.TryGetValue(svc.SaveStateKey.Value, out ModuleSaveBlock block) &&
                        block != null && !string.IsNullOrWhiteSpace(block.PayloadJson))
                    {
                        svc.RestoreState(block.PayloadJson, block.SchemaVersion);
                    }
                }
            }

            // 3. Reconcile & restore EntitySaveRecords (Two-Pass via PersistentEntityComponent)
            if (envelope.Entities != null)
            {
                JsonSerializerSettings settings = FantasyEngineJsonSettings.CreateDefault(indented: false);
                var entityRegistry = content?.GetRegistry<EntityData>();

                for (int i = 0; i < envelope.Entities.Count; i++)
                {
                    EntitySaveRecord record = envelope.Entities[i];
                    if (record == null) continue;

                    if (PersistentEntityComponent.TryFindByGuid(record.InstanceId, out PersistentEntityComponent liveEntity) &&
                        liveEntity != null)
                    {
                        liveEntity.RestoreRecord(record, this);
                        continue;
                    }

                    // Fallback match for Player if Player GUID was regenerated across scene reloads
                    if (record.IsPlayer)
                    {
                        GameObject playerObj = GameObject.FindWithTag("Player");
                        if (playerObj != null)
                        {
                            if (!playerObj.TryGetComponent(out PersistentEntityComponent playerPec))
                            {
                                playerPec = playerObj.AddComponent<PersistentEntityComponent>();
                            }
                            playerPec.InstanceId = record.InstanceId;
                            playerPec.RestoreRecord(record, this);
                            continue;
                        }
                    }

                    // Check if the entity references a missing mod definition (SAV-05)
                    if (!record.DefinitionId.IsEmpty &&
                        (entityRegistry == null || !entityRegistry.Contains(record.DefinitionId)))
                    {
                        RecordUnknownContentPlaceholder(new UnknownContentPlaceholder
                        {
                            Category = "entity",
                            MissingId = record.DefinitionId,
                            OwnerGuid = record.InstanceId,
                            Count = 1,
                            RawPayloadJson = JsonConvert.SerializeObject(record, settings)
                        });
                    }
                }
            }

            // Synchronize envelope's UnknownContentPlaceholders list with all recorded tombstones
            envelope.UnknownContentPlaceholders = new List<UnknownContentPlaceholder>(_lastLoadedUnknownPlaceholders);
        }

        /// <summary>
        /// Atomically writes <see cref="SaveEnvelope"/> to <c>{slotName}.json</c> (via <c>.tmp</c> and <c>.bak</c>)
        /// and encodes any voxel chunks to <c>{slotName}_voxels.vxlbin</c>.
        /// </summary>
        public SaveEnvelope SaveSlot(string slotName = "slot_01", VoxelCompressionCodec voxelCodec = VoxelCompressionCodec.RLE16_Deflate)
        {
            string normalizedSlot = SanitizeSlotName(slotName);
            Directory.CreateDirectory(SaveDirectory);

            SaveEnvelope envelope = CaptureEnvelope(normalizedSlot);
            IReadOnlyList<VoxelChunkPayload> chunks = _voxelCaptureCallback?.Invoke();

            if (chunks != null && chunks.Count > 0)
            {
                string voxelFileName = $"{normalizedSlot}_voxels.vxlbin";
                string voxelFullPath = Path.Combine(SaveDirectory, voxelFileName);
                byte[] voxelBytes = VoxelPayloadCodec.Encode(chunks, voxelCodec);
                WriteAtomicBytes(voxelFullPath, voxelBytes);
                envelope.VoxelPayloadFile = voxelFileName;
            }

            JsonSerializerSettings settings = FantasyEngineJsonSettings.CreateDefault(indented: true);
            string json = JsonConvert.SerializeObject(envelope, settings);
            string jsonFullPath = GetSlotJsonPath(normalizedSlot);
            WriteAtomicText(jsonFullPath, json);

            return envelope;
        }

        /// <summary>
        /// Loads <c>{slotName}.json</c> (and its binary <c>.vxlbin</c> sidecar if present) and restores all state.
        /// Supports graceful v0/v1 legacy <see cref="LevelSaveData"/> detection.
        /// </summary>
        public SaveEnvelope LoadSlot(string slotName = "slot_01")
        {
            string normalizedSlot = SanitizeSlotName(slotName);
            string jsonPath = GetSlotJsonPath(normalizedSlot);

            if (!File.Exists(jsonPath))
            {
                return null;
            }

            string json = File.ReadAllText(jsonPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            // Inspect root JSON for schemaVersion to handle legacy v0 LevelSaveData gracefully
            JObject rootObj = JObject.Parse(json);
            if (rootObj["SchemaVersion"] == null && rootObj["schemaVersion"] == null)
            {
                LevelSaveData legacy = JsonUtility.FromJson<LevelSaveData>(json);
                if (legacy != null)
                {
                    GameEventManager.Publish(new GameDataLoadedEvent { SaveData = legacy });
                }
                return null;
            }

            JsonSerializerSettings settings = FantasyEngineJsonSettings.CreateDefault(indented: false);
            SaveEnvelope envelope = JsonConvert.DeserializeObject<SaveEnvelope>(json, settings);
            if (envelope == null) return null;

            List<VoxelChunkPayload> voxelChunks = null;
            if (!string.IsNullOrWhiteSpace(envelope.VoxelPayloadFile))
            {
                string voxelPath = Path.Combine(SaveDirectory, envelope.VoxelPayloadFile);
                if (File.Exists(voxelPath))
                {
                    byte[] voxelBytes = File.ReadAllBytes(voxelPath);
                    voxelChunks = VoxelPayloadCodec.Decode(voxelBytes);
                }
            }

            RestoreEnvelope(envelope, voxelChunks);
            return envelope;
        }

        public string GetSlotJsonPath(string slotName = "slot_01") =>
            Path.Combine(SaveDirectory, $"{SanitizeSlotName(slotName)}.json");

        #endregion

        #region IWorldInstanceResolver Implementation

        public bool TryResolveGameObject(InstanceGuid instanceId, out GameObject instance)
        {
            if (PersistentEntityComponent.TryFindByGuid(instanceId, out PersistentEntityComponent pec) && pec != null)
            {
                instance = pec.gameObject;
                return true;
            }

            instance = null;
            return false;
        }

        public bool TryResolveComponent<T>(InstanceGuid instanceId, out T component) where T : Component
        {
            if (TryResolveGameObject(instanceId, out GameObject go) && go != null)
            {
                return go.TryGetComponent(out component);
            }

            component = null;
            return false;
        }

        public void RecordUnknownContentPlaceholder(UnknownContentPlaceholder placeholder)
        {
            if (placeholder == null) return;
            _lastLoadedUnknownPlaceholders.Add(placeholder);

            if (ServiceLocator.TryGet<IDiagnosticService>(out var diag))
            {
                diag.RecordWarning(
                    context: "FileSaveService",
                    message: $"[SAV-404] Unresolved {placeholder.Category} '{placeholder.MissingId}' from uninstalled mod preserved in UnknownContentPlaceholders (Owner: {placeholder.OwnerGuid}).",
                    logToUnityConsole: false);
            }
        }

        #endregion

        #region Legacy LevelSaveData API (Preserved for Backward Compatibility)

        public bool HasSaveFile()
        {
            return File.Exists(GetSlotJsonPath("slot_01")) || File.Exists(LegacySavePath);
        }

        public void WriteData(LevelSaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                WriteAtomicText(LegacySavePath, json);
                Debug.Log($"[FileSaveService] Game saved successfully to {LegacySavePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileSaveService] Failed to save game: {ex.Message}");
            }
        }

        public LevelSaveData ReadData()
        {
            if (!File.Exists(LegacySavePath)) return null;

            try
            {
                string json = File.ReadAllText(LegacySavePath);
                return JsonUtility.FromJson<LevelSaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileSaveService] Failed to load game: {ex.Message}");
                return null;
            }
        }

        public void DeleteSave()
        {
            if (File.Exists(LegacySavePath))
            {
                File.Delete(LegacySavePath);
            }

            string slotPath = GetSlotJsonPath("slot_01");
            if (File.Exists(slotPath))
            {
                File.Delete(slotPath);
            }
        }

        #endregion

        #region Event Handlers & Atomic File Helpers

        private void OnGameSaveRequested(GameSaveRequestedEvent evt)
        {
            SaveSlot("slot_01");
        }

        private void OnGameLoadRequested(GameLoadRequestedEvent evt)
        {
            if (File.Exists(GetSlotJsonPath("slot_01")))
            {
                LoadSlot("slot_01");
            }
            else if (File.Exists(LegacySavePath))
            {
                LevelSaveData legacy = ReadData();
                if (legacy != null)
                {
                    GameEventManager.Publish(new GameDataLoadedEvent { SaveData = legacy });
                }
            }
        }

        private void EnsureBuiltInSaveableServicesRegistered()
        {
            if (ServiceLocator.TryGet<SkillProgressionService>(out var skills))
            {
                RegisterSaveableService(skills);
            }
            if (ServiceLocator.TryGet<FactionService>(out var factions))
            {
                RegisterSaveableService(factions);
            }
            if (ServiceLocator.TryGet<RadiantQuestService>(out var quests))
            {
                RegisterSaveableService(quests);
            }
        }

        private static string SanitizeSlotName(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName)) return "slot_01";
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                slotName = slotName.Replace(invalid, '_');
            }
            return slotName.Trim();
        }

        private static void WriteAtomicText(string targetPath, string content)
        {
            string tmpPath = targetPath + ".tmp";
            string bakPath = targetPath + ".bak";

            File.WriteAllText(tmpPath, content);
            CommitAtomicSwap(tmpPath, targetPath, bakPath);
        }

        private static void WriteAtomicBytes(string targetPath, byte[] bytes)
        {
            string tmpPath = targetPath + ".tmp";
            string bakPath = targetPath + ".bak";

            File.WriteAllBytes(tmpPath, bytes);
            CommitAtomicSwap(tmpPath, targetPath, bakPath);
        }

        private static void CommitAtomicSwap(string tmpPath, string targetPath, string bakPath)
        {
            if (File.Exists(targetPath))
            {
                File.Copy(targetPath, bakPath, overwrite: true);
                File.Delete(targetPath);
            }
            File.Move(tmpPath, targetPath);
        }

        #endregion
    }
}
