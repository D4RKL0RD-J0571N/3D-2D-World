namespace Core.Persistence
{
    using System;
    using System.Collections.Generic;
    using Core.Ids;
    using UnityEngine;

    /// <summary>
    /// Serialized state block for a single <see cref="ISaveableService"/> inside <see cref="SaveEnvelope.ModuleStates"/>.
    /// </summary>
    [Serializable]
    public sealed class ModuleSaveBlock
    {
        public string ModuleKey;
        public int SchemaVersion = 1;
        public string PayloadJson;
    }

    /// <summary>
    /// Tombstone placeholder preserving data from a removed or temporarily disabled mod (<c>SAV-05</c>)
    /// so loading a save file never crashes and re-enabling the mod later retains the content.
    /// </summary>
    [Serializable]
    public sealed class UnknownContentPlaceholder
    {
        public string Category;          // e.g. "entity" or "inventory_item"
        public ResourceId MissingId;     // e.g. "deleted_mod:item/dragon_halberd"
        public InstanceGuid OwnerGuid;   // Entity InstanceGuid or owner container InstanceGuid
        public int Count = 1;
        public string RawPayloadJson;
    }

    /// <summary>
    /// Serialized state of a single world entity or interactable matched via <see cref="PersistentEntityComponent.InstanceId"/>.
    /// </summary>
    [Serializable]
    public sealed class EntitySaveRecord
    {
        public InstanceGuid InstanceId;
        public ResourceId DefinitionId;
        public string GameObjectName;
        public bool IsPlayer;
        public bool IsDynamicallySpawned;
        public string RoomOrCellId;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;

        /// <summary>Map of <see cref="ISaveableComponent.ComponentSaveKey"/> to serialized JSON payload.</summary>
        public Dictionary<string, string> ComponentStates = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Top-level extensible save envelope (<c>SaveFormatVersion = 2</c>) written atomically to disk by <see cref="Systems.Persistence.FileSaveService"/>.
    /// Logically separates metadata and entity/module state from binary <c>.vxlbin</c> voxel payloads (<c>ADR-009</c>).
    /// </summary>
    [Serializable]
    public sealed class SaveEnvelope
    {
        public int SchemaVersion = 2;
        public string EngineVersion = "1.0.0";
        public string SaveSlotName = "slot_01";
        public string SavedAtUtc;
        public ResourceId ActiveWorldId = new("nordic_rpg", "world/proving_grounds");

        /// <summary>Relative file name of the binary voxel sidecar (e.g., <c>"slot_01_voxels.vxlbin"</c>).</summary>
        public string VoxelPayloadFile;

        /// <summary>Palette mapping <c>ushort</c> runtime block IDs to canonical <see cref="ResourceId"/> strings.</summary>
        public Dictionary<ushort, string> VoxelPaletteMap = new();

        /// <summary>Global module/service states keyed by <see cref="ISaveableService.SaveStateKey"/>.</summary>
        public Dictionary<string, ModuleSaveBlock> ModuleStates = new(StringComparer.Ordinal);

        /// <summary>Per-entity component state records keyed by <see cref="InstanceGuid"/>.</summary>
        public List<EntitySaveRecord> Entities = new();

        /// <summary>Unresolved mod content tombstones preserved during load (<c>SAV-05</c>).</summary>
        public List<UnknownContentPlaceholder> UnknownContentPlaceholders = new();
    }
}
