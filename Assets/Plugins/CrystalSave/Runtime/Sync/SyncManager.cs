#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    public sealed partial class SyncManager : IDisposable
    {
        private readonly SaveManager saveManager;
        private readonly SyncSettings settings;
        private readonly ISyncSerializer serializer;
        private sealed class TransportRegistration
        {
            internal readonly ISyncTransport Transport;
            internal bool Initialized;
            internal bool Connected;
            internal Action<string, SyncEnvelope> PeerHandler;
            internal Action DisconnectedHandler;
            internal TransportRegistration(ISyncTransport transport) { Transport = transport; }
        }

        private readonly List<TransportRegistration> transports = new();
        private readonly Dictionary<string, byte[]> lastSnapshots = new();
        private readonly Dictionary<string, int> sequenceBySlot = new();
        private readonly SyncReconciler reconciler;
        private readonly AsyncOperationQueue sends = new();
        private readonly AsyncOperationQueue initialization = new();
        private bool disposed;
        private int transportGeneration;

        public bool IsInitialized { get; private set; }

        public event Action<SyncEnvelope> OnMessage;
        public SyncReconciler Reconciler => reconciler;

        public SyncManager(SaveManager manager, SyncSettings syncSettings)
        {
            saveManager = manager;
            settings = syncSettings;
            serializer = new SyncJsonSerializer();
            reconciler = new SyncReconciler(serializer);
            SnapshotProvider = manager ? new SaveManagerWorldSnapshotProvider(manager) : null;
            RegisterConfiguredTransports();
        }

        private void RegisterConfiguredTransports()
        {
            if (settings == null || settings.transports == null) return;
            foreach (var transport in settings.transports)
            {
                if (transport == null) continue;
                RegisterTransport(transport);
            }
        }

        public void RegisterTransport(ISyncTransport transport)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SyncManager));
            if (transport == null || transports.Exists(entry => ReferenceEquals(entry.Transport, transport))) return;
            var registration = new TransportRegistration(transport);
            transports.Add(registration);
            AttachLateJoin(registration);
            IsInitialized = false;
            transportGeneration++;
            lastSnapshots.Clear();
            transport.OnMessage += HandleIncoming;
        }

        public void UnregisterTransport(ISyncTransport transport)
        {
            if (transport == null) return;
            var registration = transports.Find(entry => ReferenceEquals(entry.Transport, transport));
            if (registration == null) return;
            DetachLateJoin(registration);
            transport.OnMessage -= HandleIncoming;
            transports.Remove(registration);
            transportGeneration++;
            lastSnapshots.Clear();
        }

        public async Task InitializeAsync()
        {
            using (await initialization.EnterAsync())
            {
                if (disposed) throw new ObjectDisposedException(nameof(SyncManager));
                if (settings == null) return;
                var config = new SyncTransportConfig
                {
                    settings = settings,
                    serializer = serializer,
                    isServer = Application.isBatchMode
                };

                // Re-evaluate current registrations after every await. An old completion
                // cannot initialize a replacement registration of the same transport.
                while (true)
                {
                    if (disposed) throw new ObjectDisposedException(nameof(SyncManager));
                    var pending = transports.Find(entry => !entry.Initialized);
                    if (pending != null)
                    {
                        IsInitialized = false;
                        await pending.Transport.InitializeAsync(config);
                        if (disposed) throw new ObjectDisposedException(nameof(SyncManager));
                        if (transports.Contains(pending)) pending.Initialized = true;
                        continue;
                    }

                    pending = settings.autoConnectTransports ? transports.Find(entry => !entry.Connected) : null;
                    if (pending != null)
                    {
                        IsInitialized = false;
                        await pending.Transport.ConnectAsync();
                        if (disposed) throw new ObjectDisposedException(nameof(SyncManager));
                        if (transports.Contains(pending)) pending.Connected = true;
                        continue;
                    }

                    IsInitialized = true;
                    StartLateJoinPump();
                    return;
                }
            }
        }

        public ValueTask SendSnapshotAsync(byte[] payload, string slotId = null)
        {
            if (payload == null || settings == null || !settings.enableSnapshots) return default;
            var copy = (byte[])payload.Clone();
            return new ValueTask(QueueSendAsync(() => SendSnapshotCoreAsync(copy, slotId)));
        }

        public ValueTask SendDiffFromSnapshotAsync(byte[] newSnapshot, string slotId = null)
        {
            if (newSnapshot == null || settings == null || !settings.enableDiffs)
                return SendSnapshotAsync(newSnapshot, slotId);
            var copy = (byte[])newSnapshot.Clone();
            return new ValueTask(QueueSendAsync(() => SendDiffCoreAsync(copy, slotId)));
        }

        private async Task SendDiffCoreAsync(byte[] newSnapshot, string slotId)
        {
            string slotKey = NormalizeSlot(slotId);
            if (!lastSnapshots.TryGetValue(slotKey, out var baseSnapshot) || baseSnapshot == null)
            {
                if (settings.enableSnapshots) await SendSnapshotCoreAsync(newSnapshot, slotId);
                return;
            }

            if (!SyncDeltaCodec.TryCreateDelta(baseSnapshot, newSnapshot, out var delta))
            {
                if (settings.enableSnapshots) await SendSnapshotCoreAsync(newSnapshot, slotId);
                return;
            }
            byte[] deltaBytes = serializer.Serialize(delta);
            var envelope = CreateEnvelope(SyncMessageType.Diff, SyncChannel.Diff, deltaBytes, slotId);
            await BroadcastSnapshotAsync(envelope, newSnapshot, slotId);
        }

        public ValueTask SendDiffAsync(byte[] payload, string slotId = null)
        {
            if (payload == null || settings == null || !settings.enableDiffs) return default;
            var copy = (byte[])payload.Clone();
            return new ValueTask(QueueSendAsync(async () =>
            {
                // A caller-owned delta does not establish a known full snapshot baseline.
                lastSnapshots.Remove(NormalizeSlot(slotId));
                await BroadcastAsync(CreateEnvelope(SyncMessageType.Diff, SyncChannel.Diff, copy, slotId));
            }));
        }

        private Task SendSnapshotCoreAsync(byte[] payload, string slotId)
            => BroadcastSnapshotAsync(CreateEnvelope(SyncMessageType.Snapshot, SyncChannel.Snapshot, payload, slotId), payload, slotId);

        private async Task BroadcastSnapshotAsync(SyncEnvelope envelope, byte[] snapshot, string slotId)
        {
            int generation = transportGeneration;
            try
            {
                await BroadcastAsync(envelope);
                if (!disposed && generation == transportGeneration && transports.Count != 0) CacheSnapshot(slotId, snapshot);
            }
            catch
            {
                // A failed broadcast may have reached only some peers. The next send must
                // be a full snapshot, rather than a diff against a generation they missed.
                lastSnapshots.Remove(NormalizeSlot(slotId));
                throw;
            }
        }

        private async Task QueueSendAsync(Func<Task> send)
        {
            using (await sends.EnterAsync())
            {
                if (disposed) throw new ObjectDisposedException(nameof(SyncManager));
                await send();
            }
        }

        private async ValueTask BroadcastAsync(SyncEnvelope envelope)
        {
            foreach (var registration in transports.ToArray())
            {
                if (disposed) throw new ObjectDisposedException(nameof(SyncManager));
                if (!transports.Contains(registration)) continue;
                await registration.Transport.SendAsync(envelope);
            }
        }

        private SyncEnvelope CreateEnvelope(SyncMessageType type, SyncChannel channel, byte[] payload, string slotId)
        {
            return new SyncEnvelope
            {
                messageType = type,
                channel = channel,
                slotId = slotId,
                sequence = NextSequence(slotId),
                timestampUtcTicks = DateTime.UtcNow.Ticks,
                payloadHash = SyncDeltaCodec.ComputeHash(payload),
                payloadBase64 = Convert.ToBase64String(payload)
            };
        }

        private int NextSequence(string slotId)
        {
            string key = NormalizeSlot(slotId);
            if (!sequenceBySlot.TryGetValue(key, out int seq))
                seq = 0;
            seq++;
            sequenceBySlot[key] = seq;
            return seq;
        }

        private static string NormalizeSlot(string slotId)
        {
            return string.IsNullOrEmpty(slotId) ? "default" : slotId;
        }

        private void CacheSnapshot(string slotId, byte[] snapshot)
        {
            if (snapshot == null) return;
            lastSnapshots[NormalizeSlot(slotId)] = snapshot;
        }

        private void HandleIncoming(SyncEnvelope envelope)
        {
            if (disposed || IsLateJoinMessage(envelope.messageType)) return;
            reconciler.TryApplyEnvelope(envelope, out _);
            SaveEventDispatch.Raise(OnMessage, envelope);
        }

        public void Dispose() => Retire();

        internal void Retire()
        {
            disposed = true;
            RetireLateJoin();
            foreach (var registration in transports) registration.Transport.OnMessage -= HandleIncoming;
            transports.Clear();
            lastSnapshots.Clear();
            sequenceBySlot.Clear();
            IsInitialized = false;
            OnMessage = null;
        }
    }
}
#endif
