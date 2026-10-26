#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>Optional world capture/application override; the default uses SaveManager.</summary>
    public interface ISyncWorldSnapshotProvider
    {
        Task<byte[]> CaptureAsync(CancellationToken cancellationToken);
        Task ApplyAsync(byte[] snapshot, CancellationToken cancellationToken);
    }
}
#endif
