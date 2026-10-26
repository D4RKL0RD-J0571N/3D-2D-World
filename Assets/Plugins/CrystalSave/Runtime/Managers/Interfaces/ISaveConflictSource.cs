#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>Optional, side-effect-free conflict reads. An absent copy is null; read failures throw.</summary>
    public interface ISaveConflictSource
    {
        Task<SaveConflictCandidates> ReadCandidatesAsync(SaveSlot slot, CancellationToken cancellationToken);
    }

    /// <summary>Detached bytes and metadata in the backend's existing storage encoding.</summary>
    public sealed class SaveConflictCandidate
    {
        public byte[] Payload { get; }
        public SaveSlot Metadata { get; }
        public SaveConflictCandidate(byte[] payload, SaveSlot metadata = null)
        {
            Payload = payload == null ? throw new ArgumentNullException(nameof(payload)) : (byte[])payload.Clone();
            Metadata = metadata == null ? null : SaveManager.CopySlot(metadata);
        }
    }

    public sealed class SaveConflictCandidates
    {
        public SaveConflictCandidate Local { get; }
        public SaveConflictCandidate Remote { get; }
        public SaveConflictCandidates(SaveConflictCandidate local, SaveConflictCandidate remote)
        { Local = local; Remote = remote; }
    }
}
#endif
