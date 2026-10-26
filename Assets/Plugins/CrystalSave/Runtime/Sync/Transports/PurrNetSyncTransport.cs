#if MEMORYPACK && ARAWN_REMEMBERME && REMEMBERME_PURRNET_PRESENT
using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using PurrNet;
using PurrNet.Transports;

namespace Arawn.CrystalSave.Runtime
{
    [CreateAssetMenu(fileName = "PurrNetSyncTransport", menuName = "Crystal Save/Sync/PurrNet Sync Transport", order = 822)]
    [RegisterNetworkType(typeof(PurrNetSyncMessage))]
    public sealed class PurrNetSyncTransport : SyncTransportAsset, ISyncPeerTransport
    {
        private ISyncSerializer serializer;
        private NetworkManager subscribedManager;
        public event Action<string, SyncEnvelope> OnPeerMessage;
        public event Action OnDisconnected;
        public override bool IsConnected
        {
            get => base.IsConnected && subscribedManager &&
                (IsServer ? subscribedManager.serverState == ConnectionState.Connected : subscribedManager.clientState == ConnectionState.Connected);
            protected set => base.IsConnected = value;
        }
        public override ValueTask InitializeAsync(SyncTransportConfig config)
        {
            Unsubscribe(); serializer = config.serializer ?? new SyncJsonSerializer(); IsServer = false;
            return default;
        }
        public override ValueTask ConnectAsync()
        {
            if (serializer == null) throw new InvalidOperationException("Initialize the PurrNet transport first.");
            Unsubscribe();
            var manager = NetworkManager.main;
            if (!manager || (manager.serverState != ConnectionState.Connected && manager.clientState != ConnectionState.Connected)) return default;
            subscribedManager = manager; IsServer = manager.serverState == ConnectionState.Connected;
            manager.Subscribe<PurrNetSyncMessage>(HandleMessage);
            manager.onClientConnectionState += ClientStateChanged;
            manager.onServerConnectionState += ServerStateChanged;
            IsConnected = true;
            return default;
        }
        public override ValueTask DisconnectAsync() { Unsubscribe(); return default; }
        public override ValueTask SendAsync(SyncEnvelope message)
        {
            RequireConnected();
            var packet = Encode(message);
            if (IsServer) subscribedManager.SendToAll(packet, Channel.ReliableOrdered);
            else subscribedManager.SendToServer(packet, Channel.ReliableOrdered);
            return default;
        }
        public ValueTask SendToPeerAsync(string peerId, SyncEnvelope message)
        {
            RequireConnected();
            if (!IsServer)
            {
                if (peerId != SyncPeer.Server) throw new ArgumentException("Clients may send only to the server.", nameof(peerId));
                return SendAsync(message);
            }
            foreach (var player in subscribedManager.players)
            {
                if (player.id.value.ToString(CultureInfo.InvariantCulture) != peerId || player.isServer) continue;
                subscribedManager.Send(player, Encode(message), Channel.ReliableOrdered);
                return default;
            }
            throw new InvalidOperationException("The PurrNet peer is no longer connected.");
        }
        PurrNetSyncMessage Encode(SyncEnvelope envelope)
        {
            byte[] bytes = serializer.Serialize(envelope);
            if (bytes == null || bytes.Length == 0) throw new InvalidOperationException("Empty sync envelope.");
            return new PurrNetSyncMessage { Payload = new ByteData(bytes, 0, bytes.Length) };
        }
        void RequireConnected() { if (!IsConnected) throw new InvalidOperationException("The PurrNet transport is not connected."); }
        private void HandleMessage(PlayerID player, PurrNetSyncMessage message, bool asServer)
        {
            // A host subscribes through both SDK roles but processes only its authoritative side.
            if (!IsConnected || asServer != IsServer || (!asServer && !player.isServer) || message.Payload.length == 0) return;
            SyncEnvelope envelope;
            try
            {
                byte[] data = new byte[message.Payload.length];
                Array.Copy(message.Payload.data, message.Payload.offset, data, 0, message.Payload.length);
                envelope = serializer.Deserialize<SyncEnvelope>(data);
            }
            catch (Exception error) { Debug.LogWarning("[PurrNetSyncTransport] Invalid envelope: " + error.Message); return; }
            RaiseMessage(envelope);
            SaveEventDispatch.Raise(OnPeerMessage, IsServer ? player.id.value.ToString(CultureInfo.InvariantCulture) : SyncPeer.Server, envelope);
        }
        void ClientStateChanged(ConnectionState state) { if (!IsServer && state == ConnectionState.Disconnected) Unsubscribe(); }
        void ServerStateChanged(ConnectionState state) { if (IsServer && state == ConnectionState.Disconnected) Unsubscribe(); }
        void Unsubscribe()
        {
            bool notify = base.IsConnected; IsConnected = false;
            if (subscribedManager)
            {
                subscribedManager.Unsubscribe<PurrNetSyncMessage>(HandleMessage);
                subscribedManager.onClientConnectionState -= ClientStateChanged;
                subscribedManager.onServerConnectionState -= ServerStateChanged;
            }
            subscribedManager = null;
            if (notify) SaveEventDispatch.Raise(OnDisconnected);
        }
        private void OnDisable() => Unsubscribe();
    }
    public struct PurrNetSyncMessage { public ByteData Payload; }
}
#endif
