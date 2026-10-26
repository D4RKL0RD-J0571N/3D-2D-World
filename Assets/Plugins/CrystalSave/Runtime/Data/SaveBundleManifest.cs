#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using MemoryPack;
namespace Arawn.CrystalSave.Runtime
{
    // New bundle-only schema. Never reorder/reuse member indices or change SaveSlot's schema.
    [MemoryPackable(GenerateType.VersionTolerant)]
    public partial class SaveBundleManifest
    {
        [MemoryPackOrder(0)] public int SourceSlotNumber { get; set; }
        [MemoryPackOrder(1)] public string SlotName { get; set; }
        [MemoryPackOrder(2)] public DateTime? LastSaved { get; set; }
        [MemoryPackOrder(3)] public string LastActiveScene { get; set; }
        [MemoryPackOrder(4)] public string ScreenshotExtension { get; set; }
        [MemoryPackOrder(5)] public Dictionary<string, string> CustomMetadata { get; set; }
        internal static SaveBundleManifest FromSlot(SaveSlot slot, string extension) => new SaveBundleManifest
        {
            SourceSlotNumber = slot.SlotNumber, SlotName = slot.SlotName, LastSaved = slot.LastSaved,
            LastActiveScene = slot.LastActiveScene, ScreenshotExtension = extension,
            CustomMetadata = slot.CustomMetadata == null ? new Dictionary<string, string>() : new Dictionary<string, string>(slot.CustomMetadata)
        };
        internal void Apply(SaveSlot slot)
        {
            if (SlotName != null) slot.SlotName = SlotName;
            if (LastSaved.HasValue) slot.LastSaved = LastSaved.Value;
            if (LastActiveScene != null) slot.LastActiveScene = LastActiveScene;
            slot.CustomMetadata = CustomMetadata == null ? new Dictionary<string, string>() : new Dictionary<string, string>(CustomMetadata);
        }
    }
}
#endif
