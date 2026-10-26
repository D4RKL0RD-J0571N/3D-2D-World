#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>Optional reliable, ordered, targeted delivery for late-join snapshots.</summary>
    public interface ISyncPeerTransport : ISyncTransport
    {
        // The sender must come from the SDK connection, never from the envelope.
        // Clients accept only the authority and identify it as SyncPeer.Server.
        event Action<string, SyncEnvelope> OnPeerMessage;
        event Action OnDisconnected;
        ValueTask SendToPeerAsync(string peerId, SyncEnvelope message);
    }

    public static class SyncPeer
    {
        public const string Server = "server";
    }
}
#endif
