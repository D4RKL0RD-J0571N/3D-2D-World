#if MEMORYPACK && ARAWN_REMEMBERME && REMEMBERME_FISHNET_PRESENT
using System;
using System.Globalization;
using System.Threading.Tasks;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>Transfers Crystal Save envelopes over an existing FishNet 4.7.3R+ session.</summary>
    [CreateAssetMenu(fileName = "FishNetSyncTransport", menuName = "Crystal Save/Sync/FishNet Sync Transport", order = 823)]
    public sealed class FishNetSyncTransport : SyncTransportAsset, ISyncPeerTransport
    {
        private ISyncSerializer serializer;
        private NetworkManager selectedManager;
        private bool hasSelectedManager;
        private NetworkManager subscribedManager;
        public event Action<string, SyncEnvelope> OnPeerMessage;
        public event Action OnDisconnected;

        public override bool IsConnected
        {
            get => base.IsConnected && subscribedManager &&
                (IsServer ? subscribedManager.IsServerStarted :
                    subscribedManager.IsClientStarted && subscribedManager.ClientManager.Connection.IsAuthenticated);
            protected set => base.IsConnected = value;
        }

        /// <summary>Optionally select a manager before connecting; otherwise uses InstanceFinder.</summary>
        public void SetNetworkManager(NetworkManager manager)
        {
            Unsubscribe();
            selectedManager = manager;
            hasSelectedManager = manager != null;
        }

        public override ValueTask InitializeAsync(SyncTransportConfig config)
        {
            Unsubscribe();
            serializer = config.serializer ?? new SyncJsonSerializer();
            IsServer = false;
            return default;
        }

        public override ValueTask ConnectAsync()
        {
            if (serializer == null)
                throw new InvalidOperationException("Initialize the FishNet transport before connecting.");
            Unsubscribe();
            var manager = hasSelectedManager ? selectedManager : InstanceFinder.NetworkManager;
            if (!manager) return default;
            // A host uses only the server subscription. Batch mode does not identify a server.
            IsServer = manager.IsServerStarted;
            if (!IsServer && (!manager.IsClientStarted || !manager.ClientManager.Connection.IsAuthenticated))
                return default;
            subscribedManager = manager;
            manager.ClientManager.OnClientConnectionState += ClientStateChanged;
            manager.ServerManager.OnServerConnectionState += ServerStateChanged;
            if (IsServer)
                manager.ServerManager.RegisterBroadcast<FishNetSyncMessage>(ReceiveServer, requireAuthentication: true);
            else
                manager.ClientManager.RegisterBroadcast<FishNetSyncMessage>(ReceiveClient);
            IsConnected = true;
            return default;
        }

        public override ValueTask DisconnectAsync()
        {
            Unsubscribe();
            return default;
        }

        public override ValueTask SendAsync(SyncEnvelope message)
        {
            if (!IsConnected)
                throw new InvalidOperationException("The FishNet transport is not connected.");
            byte[] payload = serializer.Serialize(message);
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("The sync serializer returned an empty envelope.");
            var broadcast = new FishNetSyncMessage { Payload = payload };
            // Snapshot/diff ordering must survive independently of the envelope's logical channel.
            if (IsServer)
                subscribedManager.ServerManager.Broadcast(broadcast, requireAuthenticated: true, channel: Channel.Reliable);
            else
                subscribedManager.ClientManager.Broadcast(broadcast, Channel.Reliable);
            return default;
        }

        public ValueTask SendToPeerAsync(string peerId, SyncEnvelope message)
        {
            if (!IsConnected) throw new InvalidOperationException("The FishNet transport is not connected.");
            if (!IsServer)
            {
                if (peerId != SyncPeer.Server) throw new ArgumentException("Clients may send only to the server.", nameof(peerId));
                return SendAsync(message);
            }
            if (!int.TryParse(peerId, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ||
                !subscribedManager.ServerManager.Clients.TryGetValue(id, out var connection) || !connection.IsActive || !connection.IsAuthenticated)
                throw new InvalidOperationException("The FishNet peer is no longer authenticated.");
            subscribedManager.ServerManager.Broadcast(connection, new FishNetSyncMessage { Payload = serializer.Serialize(message) },
                requireAuthenticated: true, channel: Channel.Reliable);
            return default;
        }

        private void ReceiveServer(NetworkConnection connection, FishNetSyncMessage message, Channel channel)
        {
            if (connection.IsAuthenticated) Receive(connection.ClientId.ToString(CultureInfo.InvariantCulture), message);
        }
        private void ReceiveClient(FishNetSyncMessage message, Channel channel) => Receive(SyncPeer.Server, message);

        private void Receive(string sender, FishNetSyncMessage message)
        {
            if (!IsConnected || message.Payload == null || message.Payload.Length == 0) return;
            SyncEnvelope envelope;
            try { envelope = serializer.Deserialize<SyncEnvelope>(message.Payload); }
            catch (Exception exception)
            {
                Debug.LogWarning("[FishNetSyncTransport] Invalid sync envelope: " + exception.Message);
                return;
            }
            RaiseMessage(envelope);
            SaveEventDispatch.Raise(OnPeerMessage, sender, envelope);
        }

        private void ClientStateChanged(ClientConnectionStateArgs args) { if (!IsServer && args.ConnectionState == LocalConnectionState.Stopped) Unsubscribe(); }
        private void ServerStateChanged(ServerConnectionStateArgs args) { if (IsServer && args.ConnectionState == LocalConnectionState.Stopped) Unsubscribe(); }

        private void Unsubscribe()
        {
            bool notify = base.IsConnected;
            IsConnected = false;
            if (subscribedManager)
            {
                if (subscribedManager.ClientManager) subscribedManager.ClientManager.OnClientConnectionState -= ClientStateChanged;
                if (subscribedManager.ServerManager) subscribedManager.ServerManager.OnServerConnectionState -= ServerStateChanged;
                if (IsServer && subscribedManager.ServerManager)
                    subscribedManager.ServerManager.UnregisterBroadcast<FishNetSyncMessage>(ReceiveServer);
                else if (!IsServer && subscribedManager.ClientManager)
                    subscribedManager.ClientManager.UnregisterBroadcast<FishNetSyncMessage>(ReceiveClient);
            }
            subscribedManager = null;
            if (notify) SaveEventDispatch.Raise(OnDisconnected);
        }

        private void OnDisable() => Unsubscribe();
    }

    // FishNet's IL postprocessor generates the broadcast reader/writer in the existing assembly.
    public struct FishNetSyncMessage : IBroadcast
    {
        public byte[] Payload;
    }
}
#endif
