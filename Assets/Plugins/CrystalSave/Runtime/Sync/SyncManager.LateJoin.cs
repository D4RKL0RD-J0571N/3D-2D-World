#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    public sealed class LateJoinSnapshotResult
    {
        public string RequestId { get; internal set; }
        public int SnapshotBytes { get; internal set; }
        public int ChunkCount { get; internal set; }
        public SyncSnapshotCompression Compression { get; internal set; }
        /// <summary>True only when the authority confirmed admission on the originating connection.
        /// A false value does not undo successful local application; request again before enabling gameplay.</summary>
        public bool AuthorityConfirmed { get; internal set; }
    }

    public sealed partial class SyncManager
    {
        enum AdmissionState { NotRequested, Pending, Confirmed, Rejected }

        [Serializable]
        sealed class LateJoinPacket
        {
            public int version = 2;
            public string requestId;
            public int maxBytes, chunkBytes, plainBytes, compressedBytes, count, index;
            public SyncSnapshotCompression compression;
            public string hash, dataBase64, error;
        }
        sealed class IncomingSnapshot
        {
            internal TransportRegistration Registration;
            internal string Id;
            internal int MaxBytes, ChunkBytes, Received;
            internal float Deadline;
            internal bool Applying, Retired;
            internal AdmissionState Admission;
            internal LateJoinPacket Header;
            internal byte[] Bytes;
            internal bool[] Chunks;
            internal ISyncWorldSnapshotProvider Provider;
            internal CancellationTokenSource Cancellation;
            internal readonly TaskCompletionSource<LateJoinSnapshotResult> Completion = new();
        }
        sealed class OutgoingSnapshot
        {
            internal TransportRegistration Registration;
            internal string Peer, Id;
            internal float Deadline;
            internal bool Applied, Admitted;
            internal int MaxBytes, ChunkBytes;
            internal SyncSnapshotCompression Compression;
            internal readonly CancellationTokenSource Cancellation = new();
        }

        readonly List<OutgoingSnapshot> outgoingSnapshots = new();
        IncomingSnapshot incomingSnapshot;
        Task lateJoinPump;
        public ISyncWorldSnapshotProvider SnapshotProvider { get; set; }
        public bool IsLateJoinSynchronized { get; private set; }
        public event Action<LateJoinSnapshotResult> OnLateJoinSnapshotApplied;
        public event Action<ISyncTransport, string> OnLateJoinPeerReady;

        static bool IsLateJoinMessage(SyncMessageType type) => type >= SyncMessageType.LateJoinRequest && type <= SyncMessageType.LateJoinAccepted;
        void AttachLateJoin(TransportRegistration registration)
        {
            if (registration.Transport is ISyncPeerTransport peer)
            {
                registration.PeerHandler = (sender, message) => HandleLateJoin(registration, sender, message);
                peer.OnPeerMessage += registration.PeerHandler;
                registration.DisconnectedHandler = () => CancelLateJoin(registration);
                peer.OnDisconnected += registration.DisconnectedHandler;
            }
        }
        void DetachLateJoin(TransportRegistration registration)
        {
            if (registration.Transport is ISyncPeerTransport peer)
            {
                peer.OnPeerMessage -= registration.PeerHandler;
                peer.OnDisconnected -= registration.DisconnectedHandler;
            }
            CancelLateJoin(registration);
        }
        void CancelLateJoin(TransportRegistration registration)
        {
            if (incomingSnapshot?.Registration == registration)
            {
                incomingSnapshot.Retired = true;
                incomingSnapshot.Cancellation.Cancel();
            }
            foreach (var transfer in outgoingSnapshots.ToArray())
                if (transfer.Registration == registration) transfer.Cancellation.Cancel();
            IsLateJoinSynchronized = false;
        }
        void ValidateLateJoinSettings()
        {
            if (settings == null || !settings.enableLateJoinSnapshots)
                throw new InvalidOperationException("Enable late-join snapshots in SyncSettings.");
            if (SnapshotProvider == null) throw new InvalidOperationException("A world snapshot provider is required.");
            if (settings.lateJoinChunkBytes < 512 || settings.lateJoinChunkBytes > 16384 ||
                settings.lateJoinMaxSnapshotBytes < 1024 || settings.lateJoinMaxSnapshotBytes > SyncSnapshotCodec.MaximumSnapshotBytes ||
                float.IsNaN(settings.lateJoinTimeoutSeconds) || float.IsInfinity(settings.lateJoinTimeoutSeconds) ||
                settings.lateJoinTimeoutSeconds < 1 || settings.lateJoinTimeoutSeconds > 300 ||
                settings.lateJoinMaxConcurrentTransfers < 1 || settings.lateJoinMaxConcurrentTransfers > 16)
                throw new InvalidOperationException("Invalid late-join transfer limits in SyncSettings.");
        }

        /// <summary>Request a fresh authority snapshot after SDK scenes/identities are ready.
        /// Completes after active helper application and a bounded admission attempt, independently of public events.
        /// Check AuthorityConfirmed before enabling gameplay; committed application still succeeds if admission fails.</summary>
        public async Task<LateJoinSnapshotResult> RequestLateJoinSnapshotAsync(ISyncTransport transport, CancellationToken cancellationToken = default)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SyncManager));
            ValidateLateJoinSettings();
            if (!(transport is ISyncPeerTransport peer)) throw new NotSupportedException("Late join requires ISyncPeerTransport targeted delivery.");
            var registration = transports.Find(entry => ReferenceEquals(entry.Transport, transport));
            if (registration == null || !registration.Initialized || !transport.IsConnected || transport.IsServer)
                throw new InvalidOperationException("Request late join from a registered, initialized, connected client transport.");
            if (incomingSnapshot != null) throw new InvalidOperationException("A late-join snapshot is already in progress.");
            cancellationToken.ThrowIfCancellationRequested();
            var incoming = new IncomingSnapshot
            {
                Registration = registration, Id = Guid.NewGuid().ToString("N"), Provider = SnapshotProvider,
                MaxBytes = settings.lateJoinMaxSnapshotBytes, ChunkBytes = settings.lateJoinChunkBytes,
                Deadline = Time.realtimeSinceStartup + settings.lateJoinTimeoutSeconds,
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            };
            incomingSnapshot = incoming;
            IsLateJoinSynchronized = false;
            try
            {
                await SendRequestAsync(peer.SendToPeerAsync(SyncPeer.Server, PacketEnvelope(SyncMessageType.LateJoinRequest, new LateJoinPacket
                { requestId = incoming.Id, maxBytes = incoming.MaxBytes, chunkBytes = incoming.ChunkBytes })).AsTask(), incoming);
                while (!incoming.Completion.Task.IsCompleted)
                {
                    // Once application starts, its provider owns cancellation/commit semantics.
                    if (!incoming.Applying)
                    {
                        incoming.Cancellation.Token.ThrowIfCancellationRequested();
                        if (disposed || !transports.Contains(registration) || !transport.IsConnected)
                            throw new OperationCanceledException("The joining connection was retired.");
                        if (Time.realtimeSinceStartup >= incoming.Deadline) throw new TimeoutException("Late-join snapshot transfer timed out.");
                    }
                    await Task.Yield();
                }
                return await incoming.Completion.Task;
            }
            finally
            {
                if (ReferenceEquals(incomingSnapshot, incoming)) incomingSnapshot = null;
                incoming.Cancellation.Cancel(); incoming.Cancellation.Dispose();
            }
        }

        SyncEnvelope PacketEnvelope(SyncMessageType type, LateJoinPacket packet)
        {
            byte[] payload = serializer.Serialize(packet);
            return new SyncEnvelope { messageType = type, channel = SyncChannel.Reliable, slotId = packet.requestId,
                sequence = packet.index, timestampUtcTicks = DateTime.UtcNow.Ticks,
                payloadHash = SyncDeltaCodec.ComputeHash(payload), payloadBase64 = Convert.ToBase64String(payload) };
        }

        void HandleLateJoin(TransportRegistration registration, string sender, SyncEnvelope envelope)
        {
            if (disposed || !transports.Contains(registration) || settings == null || !settings.enableLateJoinSnapshots ||
                !registration.Transport.IsConnected || !IsLateJoinMessage(envelope.messageType)) return;
            if (!registration.Transport.IsServer && sender != SyncPeer.Server) return;
            try
            {
                if (envelope.payloadBase64 == null || envelope.payloadBase64.Length > 65536 ||
                    !Guid.TryParseExact(envelope.slotId, "N", out _)) throw new InvalidDataException("Invalid late-join envelope.");
                byte[] bytes = Convert.FromBase64String(envelope.payloadBase64);
                if (string.IsNullOrEmpty(envelope.payloadHash) || SyncDeltaCodec.ComputeHash(bytes) != envelope.payloadHash)
                    throw new InvalidDataException("Late-join packet hash mismatch.");
                var packet = serializer.Deserialize<LateJoinPacket>(bytes);
                if (packet == null || packet.version != 2 || packet.requestId != envelope.slotId)
                    throw new InvalidDataException("Incompatible late-join packet.");
                if (registration.Transport.IsServer)
                {
                    if (string.IsNullOrEmpty(sender) || sender == SyncPeer.Server) return;
                    if (envelope.messageType == SyncMessageType.LateJoinRequest) StartOutgoingSnapshot(registration, sender, packet);
                    else if (envelope.messageType == SyncMessageType.LateJoinApplied)
                    {
                        var transfer = outgoingSnapshots.Find(t => t.Registration == registration && t.Peer == sender && t.Id == packet.requestId);
                        if (transfer != null) transfer.Applied = true;
                    }
                    return;
                }
                var incoming = incomingSnapshot;
                if (incoming == null || incoming.Registration != registration || incoming.Id != packet.requestId) return;
                if (envelope.messageType == SyncMessageType.LateJoinRejected)
                {
                    incoming.Admission = AdmissionState.Rejected;
                    // A rejected admission cannot erase an application that already committed.
                    if (!incoming.Applying) throw new InvalidOperationException("Authority rejected late join: " + packet.error);
                }
                else if (envelope.messageType == SyncMessageType.LateJoinAccepted)
                {
                    if (incoming.Admission == AdmissionState.Pending && IsCurrentConnection(incoming))
                        incoming.Admission = AdmissionState.Confirmed;
                }
                else if (envelope.messageType == SyncMessageType.LateJoinChunk && !incoming.Applying) ReceiveChunk(incoming, packet);
            }
            catch (Exception error)
            {
                var incoming = incomingSnapshot;
                if (!registration.Transport.IsServer && incoming?.Registration == registration && incoming.Id == envelope.slotId && !incoming.Applying)
                    incoming.Completion.TrySetException(error);
                else Debug.LogWarning("[Crystal Save Sync] Rejected late-join packet: " + error.Message);
            }
        }

        void StartOutgoingSnapshot(TransportRegistration registration, string peer, LateJoinPacket request)
        {
            ValidateLateJoinSettings();
            if (request.maxBytes < 1024 || request.maxBytes > SyncSnapshotCodec.MaximumSnapshotBytes || request.chunkBytes < 512 || request.chunkBytes > 16384)
                throw new InvalidDataException("Invalid requested snapshot limits.");
            if (outgoingSnapshots.Exists(t => t.Registration == registration && t.Peer == peer) ||
                outgoingSnapshots.Count >= settings.lateJoinMaxConcurrentTransfers) return; // client has a bounded retry/deadline
            var transfer = new OutgoingSnapshot { Registration = registration, Peer = peer, Id = request.requestId,
                Deadline = Time.realtimeSinceStartup + settings.lateJoinTimeoutSeconds,
                MaxBytes = Math.Min(settings.lateJoinMaxSnapshotBytes, request.maxBytes),
                ChunkBytes = Math.Min(settings.lateJoinChunkBytes, request.chunkBytes),
                Compression = settings.lateJoinUseLz4 ? SyncSnapshotCompression.Lz4 : SyncSnapshotCompression.GZip };
            outgoingSnapshots.Add(transfer);
            SendOutgoingSnapshot(transfer, SnapshotProvider);
        }

        void CheckOutgoing(OutgoingSnapshot transfer)
        {
            transfer.Cancellation.Token.ThrowIfCancellationRequested();
            if (disposed || !transports.Contains(transfer.Registration) || !transfer.Registration.Transport.IsConnected || !transfer.Registration.Transport.IsServer)
                throw new OperationCanceledException("Snapshot authority connection was retired.");
            if (Time.realtimeSinceStartup >= transfer.Deadline) throw new TimeoutException("Late-join snapshot acknowledgement timed out.");
        }
        async void SendOutgoingSnapshot(OutgoingSnapshot transfer, ISyncWorldSnapshotProvider provider)
        {
            var transport = (ISyncPeerTransport)transfer.Registration.Transport;
            try
            {
                // Capture through the provider's world queue without taking the legacy send queue.
                Task<byte[]> capture = provider.CaptureAsync(transfer.Cancellation.Token);
                while (!capture.IsCompleted)
                {
                    try { CheckOutgoing(transfer); }
                    catch { ObserveAbandonedCapture(capture); throw; }
                    await Task.Yield();
                }
                byte[] snapshot = await capture;
                CheckOutgoing(transfer);
                int limit = transfer.MaxBytes;
                if (snapshot == null || snapshot.Length == 0 || snapshot.Length > limit)
                    throw new InvalidDataException("The current world exceeds the requested snapshot limit.");
                snapshot = (byte[])snapshot.Clone();
                var codec = transfer.Compression;
                byte[] compressed = SyncSnapshotCodec.Compress(snapshot, codec);
                int chunkBytes = transfer.ChunkBytes;
                int count = (compressed.Length + chunkBytes - 1) / chunkBytes;
                string hash = SyncDeltaCodec.ComputeHash(snapshot);
                // Targeted chunks have independent transfer IDs; do not hold the legacy broadcast queue.
                int frameBytes = 0;
                for (int index = 0; index < count; index++)
                {
                    CheckOutgoing(transfer);
                    int length = Math.Min(chunkBytes, compressed.Length - index * chunkBytes);
                    var packet = new LateJoinPacket { requestId = transfer.Id, compression = codec,
                        plainBytes = snapshot.Length, compressedBytes = compressed.Length, chunkBytes = chunkBytes,
                        count = count, index = index, hash = hash,
                        dataBase64 = Convert.ToBase64String(compressed, index * chunkBytes, length) };
                    await AwaitOutgoingSendAsync(transport.SendToPeerAsync(transfer.Peer, PacketEnvelope(SyncMessageType.LateJoinChunk, packet)).AsTask(), transfer);
                    frameBytes += length;
                    if (frameBytes >= 64 * 1024) { frameBytes = 0; await Task.Yield(); }
                }
                while (!transfer.Applied) { CheckOutgoing(transfer); await Task.Yield(); }
                CheckOutgoing(transfer);
                transportGeneration++; // an in-flight legacy broadcast cannot republish an older baseline
                lastSnapshots.Clear(); // a newly admitted peer has no baseline for legacy broadcast diffs
                transfer.Admitted = true;
                SaveEventDispatch.Raise(OnLateJoinPeerReady, (ISyncTransport)transport, transfer.Peer);
                transfer.Cancellation.Token.ThrowIfCancellationRequested();
                // Readiness is an authority decision. A failed confirmation cannot retract that
                // decision; the client remains unready and can request a fresh snapshot.
                await AwaitNotificationAsync(transport.SendToPeerAsync(transfer.Peer, PacketEnvelope(SyncMessageType.LateJoinAccepted,
                    new LateJoinPacket { requestId = transfer.Id })).AsTask());
            }
            catch (Exception error)
            {
                if (!transfer.Admitted && !disposed && transports.Contains(transfer.Registration) && transport.IsConnected)
                    try { await AwaitNotificationAsync(transport.SendToPeerAsync(transfer.Peer, PacketEnvelope(SyncMessageType.LateJoinRejected,
                        new LateJoinPacket { requestId = transfer.Id, error = error is TimeoutException ? "timeout" : "capture or transfer failed" })).AsTask()); }
                    catch (Exception) { /* The disconnected peer will observe its own bounded transfer failure. */ }
                if (!(error is OperationCanceledException)) Debug.LogWarning(transfer.Admitted
                    ? "[Crystal Save Sync] Peer admitted, but confirmation failed: " + error.Message
                    : "[Crystal Save Sync] Late join failed: " + error.Message);
            }
            finally
            {
                outgoingSnapshots.Remove(transfer);
                transfer.Cancellation.Cancel(); transfer.Cancellation.Dispose();
            }
        }
        static async void ObserveAbandonedCapture(Task<byte[]> task) { try { await task; } catch (Exception) { } }

        static async void ObserveAbandonedSend(Task task) { try { await task; } catch (Exception) { } }
        async Task AwaitOutgoingSendAsync(Task task, OutgoingSnapshot transfer)
        {
            try
            {
                while (!task.IsCompleted) { CheckOutgoing(transfer); await Task.Yield(); }
                await task;
            }
            catch { ObserveAbandonedSend(task); throw; }
        }
        static async Task AwaitNotificationAsync(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!task.IsCompleted)
            {
                if (Time.realtimeSinceStartup >= deadline)
                { ObserveAbandonedSend(task); throw new TimeoutException("Late-join notification send timed out."); }
                await Task.Yield();
            }
            await task;
        }
        async Task SendRequestAsync(Task task, IncomingSnapshot incoming)
        {
            try
            {
                while (!task.IsCompleted)
                {
                    if (incoming.Completion.Task.IsCompleted) { ObserveAbandonedSend(task); return; }
                    if (!incoming.Applying)
                    {
                        incoming.Cancellation.Token.ThrowIfCancellationRequested();
                        if (disposed || !transports.Contains(incoming.Registration) || !incoming.Registration.Transport.IsConnected)
                            throw new OperationCanceledException("Joining connection retired.");
                        if (Time.realtimeSinceStartup >= incoming.Deadline) throw new TimeoutException("Late-join request send timed out.");
                    }
                    await Task.Yield();
                }
                await task;
            }
            catch
            {
                ObserveAbandonedSend(task);
                if (incoming.Applying) { await incoming.Completion.Task; return; }
                throw;
            }
        }

        void ReceiveChunk(IncomingSnapshot incoming, LateJoinPacket packet)
        {
            incoming.Cancellation.Token.ThrowIfCancellationRequested();
            if (Time.realtimeSinceStartup >= incoming.Deadline) throw new TimeoutException("Late-join snapshot transfer timed out.");
            if (packet.plainBytes <= 0 || packet.plainBytes > incoming.MaxBytes || packet.chunkBytes < 512 || packet.chunkBytes > incoming.ChunkBytes ||
                packet.compressedBytes <= 0 || packet.compressedBytes > incoming.MaxBytes + incoming.MaxBytes / 255 + 1024 ||
                packet.count != (packet.compressedBytes + packet.chunkBytes - 1) / packet.chunkBytes || packet.index < 0 || packet.index >= packet.count ||
                (packet.compression != SyncSnapshotCompression.GZip && packet.compression != SyncSnapshotCompression.Lz4) ||
                packet.hash == null || packet.hash.Length != 64 || packet.dataBase64 == null || packet.dataBase64.Length > (packet.chunkBytes + 2) / 3 * 4)
                throw new InvalidDataException("Invalid snapshot chunk header.");
            if (incoming.Header == null)
            {
                incoming.Header = packet;
                incoming.Bytes = new byte[packet.compressedBytes];
                incoming.Chunks = new bool[packet.count];
            }
            var header = incoming.Header;
            if (packet.plainBytes != header.plainBytes || packet.chunkBytes != header.chunkBytes || packet.compressedBytes != header.compressedBytes ||
                packet.count != header.count || packet.compression != header.compression || packet.hash != header.hash)
                throw new InvalidDataException("Snapshot chunk metadata changed during transfer.");
            byte[] chunk = Convert.FromBase64String(packet.dataBase64);
            int offset = packet.index * packet.chunkBytes;
            if (chunk.Length != Math.Min(packet.chunkBytes, packet.compressedBytes - offset)) throw new InvalidDataException("Invalid snapshot chunk length.");
            if (incoming.Chunks[packet.index])
            {
                for (int n = 0; n < chunk.Length; n++)
                    if (incoming.Bytes[offset + n] != chunk[n]) throw new InvalidDataException("Conflicting duplicate snapshot chunk.");
                return;
            }
            Buffer.BlockCopy(chunk, 0, incoming.Bytes, offset, chunk.Length);
            incoming.Chunks[packet.index] = true;
            if (++incoming.Received != header.count) return;
            byte[] snapshot = SyncSnapshotCodec.Decompress(incoming.Bytes, header.compression, header.plainBytes, incoming.MaxBytes);
            if (SyncDeltaCodec.ComputeHash(snapshot) != header.hash) throw new InvalidDataException("Snapshot hash mismatch.");
            incoming.Applying = true;
            ApplyIncomingSnapshot(incoming, snapshot);
        }
        async void ApplyIncomingSnapshot(IncomingSnapshot incoming, byte[] snapshot)
        {
            try
            {
                await incoming.Provider.ApplyAsync(snapshot, incoming.Cancellation.Token);
                var result = new LateJoinSnapshotResult { RequestId = incoming.Id, SnapshotBytes = snapshot.Length,
                    ChunkCount = incoming.Header.count, Compression = incoming.Header.compression };
                bool confirmed = await ConfirmAdmissionAsync(incoming);
                result.AuthorityConfirmed = confirmed && IsCurrentConnection(incoming);
                IsLateJoinSynchronized = result.AuthorityConfirmed;
                incoming.Completion.TrySetResult(result);
                if (ReferenceEquals(incomingSnapshot, incoming)) incomingSnapshot = null;
                SaveEventDispatch.Raise(OnLateJoinSnapshotApplied, result);
            }
            catch (Exception error) { incoming.Completion.TrySetException(error); }
        }

        bool IsCurrentConnection(IncomingSnapshot incoming) => !disposed && !incoming.Retired &&
            transports.Contains(incoming.Registration) && incoming.Registration.Transport.IsConnected;

        bool CanAwaitAdmission(IncomingSnapshot incoming) => IsCurrentConnection(incoming) &&
            incoming.Admission != AdmissionState.Rejected && Time.realtimeSinceStartup < incoming.Deadline;

        async Task<bool> ConfirmAdmissionAsync(IncomingSnapshot incoming)
        {
            if (!CanAwaitAdmission(incoming)) return false;
            incoming.Admission = AdmissionState.Pending;
            Task send = null;
            try
            {
                send = ((ISyncPeerTransport)incoming.Registration.Transport).SendToPeerAsync(SyncPeer.Server,
                    PacketEnvelope(SyncMessageType.LateJoinApplied, new LateJoinPacket { requestId = incoming.Id })).AsTask();
                // Transport completion is not remote admission. Conversely a confirmed authority
                // response is conclusive even if a custom transport's send completion arrives late.
                while (incoming.Admission != AdmissionState.Confirmed && CanAwaitAdmission(incoming))
                {
                    if (send.IsCompleted) await send;
                    await Task.Yield();
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Crystal Save Sync] Snapshot applied, but acknowledgement failed: " + error.Message);
            }
            finally
            {
                if (send != null) ObserveAbandonedSend(send);
                if (incoming.Admission != AdmissionState.Confirmed) incoming.Admission = AdmissionState.Rejected;
            }
            // Recheck after every await: a completed send must not admit a replacement connection.
            return incoming.Admission == AdmissionState.Confirmed && IsCurrentConnection(incoming);
        }
        void StartLateJoinPump()
        {
            if (settings?.enableLateJoinSnapshots == true && (lateJoinPump == null || lateJoinPump.IsCompleted)) lateJoinPump = PumpLateJoinAsync();
        }
        async Task PumpLateJoinAsync()
        {
            await Task.Yield(); // SaveManager finishes bootstrap before any automatic world operation.
            ISyncTransport synchronizedTransport = null;
            float retryAt = 0;
            while (!disposed && settings && settings.enableLateJoinSnapshots)
            {
                try
                {
                    if (synchronizedTransport != null && !synchronizedTransport.IsConnected)
                    { IsLateJoinSynchronized = false; synchronizedTransport = null; }
                    if (Time.realtimeSinceStartup >= retryAt)
                    {
                        retryAt = Time.realtimeSinceStartup + 1f;
                        foreach (var registration in transports.ToArray())
                        {
                            if (!registration.Initialized || !(registration.Transport is ISyncPeerTransport)) continue;
                            if (!registration.Transport.IsConnected && settings.autoConnectTransports) await registration.Transport.ConnectAsync();
                            if (settings.autoRequestLateJoinSnapshot && !IsLateJoinSynchronized && incomingSnapshot == null &&
                                registration.Transport.IsConnected && !registration.Transport.IsServer)
                            {
                                var result = await RequestLateJoinSnapshotAsync(registration.Transport);
                                synchronizedTransport = result.AuthorityConfirmed ? registration.Transport : null;
                                if (!result.AuthorityConfirmed) retryAt = Time.realtimeSinceStartup + 5f;
                                break;
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    if (!disposed) Debug.LogWarning("[Crystal Save Sync] Automatic late join will retry: " + error.Message);
                    retryAt = Time.realtimeSinceStartup + 5f;
                }
                await Task.Yield();
            }
        }
        void RetireLateJoin()
        {
            foreach (var registration in transports) DetachLateJoin(registration);
            OnLateJoinSnapshotApplied = null; OnLateJoinPeerReady = null;
        }
    }
}
#endif
