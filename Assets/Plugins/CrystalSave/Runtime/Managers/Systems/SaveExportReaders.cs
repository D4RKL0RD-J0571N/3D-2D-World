#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    // A bundle needs the header belonging to its payload, not the published slot-list cache.
    // Internal and optional: existing custom ISaveSystem implementations remain supported.
    internal interface ISaveExportSource
    {
        Task<SaveConflictCandidates> ReadExportCandidatesAsync(SaveSlot slot, CancellationToken token);
    }

    public partial class SaveSystem : ISaveExportSource
    {
        Task<SaveConflictCandidates> ISaveExportSource.ReadExportCandidatesAsync(SaveSlot slot, CancellationToken token)
            => ReadCandidatesAsync(slot, token);
    }

    public sealed partial class MySqlSaveSystem : ISaveExportSource
    {
        async Task<SaveConflictCandidates> ISaveExportSource.ReadExportCandidatesAsync(SaveSlot slot, CancellationToken token)
        {
            using var cancellation = SaveIoScope.WithReadCancellation(token);
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            token.ThrowIfCancellationRequested();
            var bytes = await LoadAsync(slot);
            var metadata = bytes == null ? null : await ReadRemoteConflictMetadataAsync(slot.SlotNumber);
            token.ThrowIfCancellationRequested();
            return new SaveConflictCandidates(null, bytes == null ? null : new SaveConflictCandidate(bytes, metadata));
        }
    }

    public sealed partial class FirebaseSaveSystem : ISaveExportSource
    {
        async Task<SaveConflictCandidates> ISaveExportSource.ReadExportCandidatesAsync(SaveSlot slot, CancellationToken token)
        {
            using var cancellation = SaveIoScope.WithReadCancellation(token);
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            token.ThrowIfCancellationRequested();
            var bytes = await LoadAsync(slot);
            var metadata = bytes == null ? null : CloudSlotMetadata.Decode(await DownloadObject(PathFor(slot.SlotNumber, META_EXT)), slot.SlotNumber);
            token.ThrowIfCancellationRequested();
            return new SaveConflictCandidates(null, bytes == null ? null : new SaveConflictCandidate(bytes, metadata));
        }
    }

    public sealed partial class SupabaseSaveSystem : ISaveExportSource
    {
        async Task<SaveConflictCandidates> ISaveExportSource.ReadExportCandidatesAsync(SaveSlot slot, CancellationToken token)
        {
            using var cancellation = SaveIoScope.WithReadCancellation(token);
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            token.ThrowIfCancellationRequested();
            var bytes = await LoadAsync(slot);
            var metadata = bytes == null ? null : CloudSlotMetadata.Decode(await DownloadObject(PathFor(slot.SlotNumber, META_EXT)), slot.SlotNumber);
            token.ThrowIfCancellationRequested();
            return new SaveConflictCandidates(null, bytes == null ? null : new SaveConflictCandidate(bytes, metadata));
        }
    }
}
#endif
