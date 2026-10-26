#if MEMORYPACK && ARAWN_REMEMBERME && REMEMBERME_NGO_PRESENT
using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;

namespace Arawn.CrystalSave.Runtime
{
    [CreateAssetMenu(fileName = "NGOSyncTransport", menuName = "Crystal Save/Sync/NGO Sync Transport", order = 821)]
    public sealed class NGOSyncTransport : SyncTransportAsset, ISyncPeerTransport
    {
        [Tooltip("Named message key used for sync payloads.")]
        public string messageName = "CrystalSaveSync";
        private ISyncSerializer serializer;
        private NetworkManager subscribedManager;
        private CustomMessagingManager messaging;
        private string subscribedName;
        public event Action<string, SyncEnvelope> OnPeerMessage;
        public event Action OnDisconnected;
        public override bool IsConnected
        {
            get => base.IsConnected && subscribedManager && subscribedManager.IsListening &&
                (IsServer ? subscribedManager.IsServer : subscribedManager.IsConnectedClient);
            protected set => base.IsConnected = value;
        }
        public override ValueTask InitializeAsync(SyncTransportConfig config)
        {
            Unsubscribe(); serializer = config.serializer ?? new SyncJsonSerializer(); IsServer = false;
            return default;
        }
        public override ValueTask ConnectAsync()
        {
            if (serializer == null) throw new InvalidOperationException("Initialize the NGO transport first.");
            Unsubscribe();
            var manager = NetworkManager.Singleton;
            if (!manager || !manager.IsListening || (!manager.IsServer && !manager.IsConnectedClient)) return default;
            subscribedManager = manager; messaging = manager.CustomMessagingManager;
            IsServer = manager.IsServer;
            subscribedName = messageName;
            messaging.RegisterNamedMessageHandler(subscribedName, HandleMessage);
            manager.OnClientDisconnectCallback += ClientDisconnected;
            manager.OnServerStopped += ServerStopped;
            IsConnected = true;
            return default;
        }
        public override ValueTask DisconnectAsync() { Unsubscribe(); return default; }
        public override ValueTask SendAsync(SyncEnvelope message)
        {
            RequireConnected();
            if (!IsServer) return SendToPeerAsync(SyncPeer.Server, message);
            byte[] payload = serializer.Serialize(message);
            using var writer = Write(payload);
            // A host's local world is already authoritative; do not echo a snapshot back to it.
            foreach (ulong id in subscribedManager.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId)
                    messaging.SendNamedMessage(subscribedName, id, writer, NetworkDelivery.ReliableFragmentedSequenced);
            return default;
        }
        public ValueTask SendToPeerAsync(string peerId, SyncEnvelope message)
        {
            RequireConnected();
            ulong id;
            if (!IsServer)
            {
                if (peerId != SyncPeer.Server) throw new ArgumentException("Clients may send only to the server.", nameof(peerId));
                id = NetworkManager.ServerClientId;
            }
            else if (!ulong.TryParse(peerId, NumberStyles.None, CultureInfo.InvariantCulture, out id) ||
                id == NetworkManager.ServerClientId || !subscribedManager.ConnectedClients.ContainsKey(id))
                throw new InvalidOperationException("The NGO peer is no longer connected.");
            using var writer = Write(serializer.Serialize(message));
            messaging.SendNamedMessage(subscribedName, id, writer, NetworkDelivery.ReliableFragmentedSequenced);
            return default;
        }
        static FastBufferWriter Write(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Empty sync envelope.");
            var writer = new FastBufferWriter(sizeof(int) + payload.Length, Allocator.Temp);
            writer.WriteValueSafe(payload.Length); writer.WriteBytesSafe(payload);
            return writer;
        }
        void RequireConnected() { if (!IsConnected) throw new InvalidOperationException("The NGO transport is not connected."); }
        private void HandleMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsConnected || (!IsServer && senderClientId != NetworkManager.ServerClientId) ||
                (IsServer && senderClientId == NetworkManager.ServerClientId) || !reader.TryBeginRead(sizeof(int))) return;
            reader.ReadValueSafe(out int length);
            if (length <= 0 || !reader.TryBeginRead(length)) return;
            byte[] data = new byte[length]; reader.ReadBytesSafe(ref data, length);
            SyncEnvelope envelope;
            try { envelope = serializer.Deserialize<SyncEnvelope>(data); }
            catch (Exception error) { Debug.LogWarning("[NGOSyncTransport] Invalid envelope: " + error.Message); return; }
            RaiseMessage(envelope);
            SaveEventDispatch.Raise(OnPeerMessage, IsServer ? senderClientId.ToString(CultureInfo.InvariantCulture) : SyncPeer.Server, envelope);
        }
        void ClientDisconnected(ulong id) { if (!IsServer && subscribedManager && id == subscribedManager.LocalClientId) Unsubscribe(); }
        void ServerStopped(bool wasHost) { if (IsServer) Unsubscribe(); }
        void Unsubscribe()
        {
            bool notify = base.IsConnected; IsConnected = false;
            if (messaging != null) messaging.UnregisterNamedMessageHandler(subscribedName);
            if (subscribedManager)
            {
                subscribedManager.OnClientDisconnectCallback -= ClientDisconnected;
                subscribedManager.OnServerStopped -= ServerStopped;
            }
            messaging = null; subscribedManager = null; subscribedName = null;
            if (notify) SaveEventDispatch.Raise(OnDisconnected);
        }
        void OnDisable() => Unsubscribe();
    }
}
#endif
