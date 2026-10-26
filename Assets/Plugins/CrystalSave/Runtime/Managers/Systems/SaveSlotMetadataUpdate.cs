#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    // Runtime-only intent for metadata edits. Each stored generation keeps its own
    // timestamp, scene, screenshot and all fields not explicitly changed by the caller.
    internal sealed class SaveSlotMetadataUpdate
    {
        readonly string name;
        readonly Dictionary<string, string> values;

        internal SaveSlotMetadataUpdate(string name, IDictionary<string, string> values = null)
        {
            this.name = name;
            this.values = values == null ? new() : new(values);
        }

        internal SaveSlot Apply(SaveSlot stored, int number)
        {
            if (stored != null && stored.SlotNumber != number)
                throw new InvalidDataException("Metadata update returned a different slot.");
            var result = stored == null
                ? new SaveSlot(number, $"Slot {number}", DateTime.MinValue, string.Empty, string.Empty)
                : SaveManager.CopySlot(stored);
            if (name != null) result.SlotName = name;
            foreach (var entry in values) result.CustomMetadata[entry.Key] = entry.Value;
            return result;
        }

        internal static async Task<SaveSlot> ResolveRemoteAsync(SaveSlot requested, Func<Task<SaveSlot>> read)
        {
            var update = SaveIoScope.Current.Value?.MetadataUpdate;
            return update == null ? SaveManager.CopySlot(requested)
                : update.Apply(await read(), requested.SlotNumber);
        }

        internal static SaveSlot ResolveLocal(SaveSlot requested, string payload, string metadata, string legacyMetadata)
        {
            bool hasPayload = CloudMirrorFiles.FindCommittedPath(payload) != null;
            var stored = ConflictCandidateReader.Metadata(
                CloudMirrorFiles.ReadCommittedBytes(metadata) ?? CloudMirrorFiles.ReadCommittedBytes(legacyMetadata));
            if (stored != null && stored.SlotNumber != requested.SlotNumber)
                throw new InvalidDataException("Local metadata does not match the requested slot.");
            var update = SaveIoScope.Current.Value?.MetadataUpdate;
            if (update != null)
            {
                // Missing metadata cannot establish the generation of existing bytes.
                if (stored == null && hasPayload) return null;
                return update.Apply(stored, requested.SlotNumber);
            }
            // Legacy full-header callers may replace the matching generation only.
            if (hasPayload && (stored == null || stored.LastSaved != requested.LastSaved ||
                stored.LastActiveScene != requested.LastActiveScene)) return null;
            return SaveManager.CopySlot(requested);
        }
    }
}
#endif
