#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    // Firebase and Supabase share this JSON wire format. Keep the original basic
    // field names; absent CustomMetadata is the legacy empty-dictionary default.
    // This DTO is independent of the public MemoryPack SaveSlot layout.
    [Serializable]
    internal sealed class CloudSlotMetadata
    {
        public int SlotNumber;
        public string SlotName;
        public string ScreenshotFileName;
        public string LastActiveScene;
        public long LastSavedTicks;
        public Entry[] CustomMetadata;

        [Serializable]
        internal sealed class Entry
        {
            public string Key;
            public string Value;
            public bool ValueIsNull;
        }

        internal static CloudSlotMetadata FromSlot(SaveSlot slot) => new()
        {
            SlotNumber = slot.SlotNumber,
            SlotName = slot.SlotName,
            ScreenshotFileName = slot.ScreenshotFileName,
            LastActiveScene = slot.LastActiveScene,
            LastSavedTicks = slot.LastSaved.Ticks,
            CustomMetadata = (slot.CustomMetadata ?? new Dictionary<string, string>())
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new Entry { Key = pair.Key, Value = pair.Value, ValueIsNull = pair.Value == null })
                .ToArray()
        };

        internal SaveSlot ToSlot()
        {
            if (SlotNumber < 1) throw new InvalidDataException("Cloud metadata contains an invalid slot number.");
            var slot = new SaveSlot(SlotNumber, SlotName, new DateTime(LastSavedTicks, DateTimeKind.Utc),
                ScreenshotFileName, LastActiveScene);
            if (CustomMetadata == null) return slot;
            foreach (var entry in CustomMetadata)
            {
                if (entry == null || entry.Key == null || slot.CustomMetadata.ContainsKey(entry.Key))
                    throw new InvalidDataException("Cloud metadata contains a missing or duplicate key.");
                slot.CustomMetadata.Add(entry.Key, entry.ValueIsNull ? null : entry.Value);
            }
            return slot;
        }

        internal static SaveSlot Decode(byte[] bytes, int? expectedSlot = null)
        {
            if (bytes == null) return null;
            if (bytes.Length == 0) throw new InvalidDataException("Cloud slot metadata is empty.");
            try
            {
                var metadata = CloudMetadataJson.Decode<CloudSlotMetadata>(Encoding.UTF8.GetString(bytes));
                if (metadata == null || (expectedSlot.HasValue && metadata.SlotNumber != expectedSlot.Value))
                    throw new InvalidDataException("Cloud slot metadata does not match the requested slot.");
                return metadata.ToSlot();
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException("Cloud slot metadata is invalid.", ex);
            }
        }
    }
}
#endif
