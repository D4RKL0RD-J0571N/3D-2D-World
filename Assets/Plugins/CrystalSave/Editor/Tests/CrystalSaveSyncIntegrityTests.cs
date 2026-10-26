#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveSyncIntegrityTests
    {
        SyncSettings settings;
        SyncManager manager;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<SyncSettings>();
            settings.enableDiffs = true;
            manager = new SyncManager(null, settings);
        }

        [TearDown]
        public void TearDown()
        {
            typeof(SyncManager).GetMethod("Retire", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public async Task FailedPartialBroadcastForcesFullSnapshotForEveryPeer()
        {
            var first = new Transport();
            var second = new Transport();
            manager.RegisterTransport(first); manager.RegisterTransport(second);
            var original = new byte[20];
            await manager.SendSnapshotAsync(original, "slot");
            second.Fail = true;
            var rejected = (byte[])original.Clone(); rejected[3] = 1;
            Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.SendDiffFromSnapshotAsync(rejected, "slot"));
            second.Fail = false;
            var next = (byte[])rejected.Clone(); next[8] = 2;
            await manager.SendDiffFromSnapshotAsync(next, "slot");
            Assert.That(first.Messages.Last().messageType, Is.EqualTo(SyncMessageType.Snapshot));
            Assert.That(second.Messages.Last().messageType, Is.EqualTo(SyncMessageType.Snapshot));
            foreach (var transport in new[] { first, second })
            {
                var receiver = new SyncReconciler(null);
                byte[] restored = null;
                foreach (var envelope in transport.Messages)
                    Assert.That(receiver.TryApplyEnvelope(envelope, out restored), Is.True);
                Assert.That(restored, Is.EqualTo(next));
            }
        }

        [Test]
        public async Task CallerMutationCannotAlterTheAcknowledgedDiffBaseline()
        {
            var transport = new Transport(); manager.RegisterTransport(transport);
            var initial = new byte[20];
            await manager.SendSnapshotAsync(initial);
            initial[0] = 7;
            var next = new byte[20]; next[3] = 4;
            await manager.SendDiffFromSnapshotAsync(next);
            var receiver = new SyncReconciler(null);
            Assert.That(receiver.TryApplyEnvelope(transport.Messages[0], out _), Is.True);
            Assert.That(receiver.TryApplyEnvelope(transport.Messages[1], out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(next));
        }

        [Test]
        public async Task ConcurrentSendsKeepCapturedInputsAndWaitForEarlierAcknowledgement()
        {
            var gate = new TaskCompletionSource<bool>();
            var transport = new Transport { Gate = gate.Task }; manager.RegisterTransport(transport);
            var initial = new byte[20];
            var first = manager.SendSnapshotAsync(initial).AsTask();
            var next = new byte[20]; next[7] = 1;
            var second = manager.SendDiffFromSnapshotAsync(next).AsTask();
            initial[0] = 8; next[7] = 9;
            Assert.That(transport.Messages.Count, Is.EqualTo(1));
            Assert.That(second.IsCompleted, Is.False);
            gate.SetResult(true);
            await Task.WhenAll(first, second);
            var receiver = new SyncReconciler(null);
            Assert.That(receiver.TryApplyEnvelope(transport.Messages[0], out _), Is.True);
            Assert.That(receiver.TryApplyEnvelope(transport.Messages[1], out var result), Is.True);
            Assert.That(result[0], Is.Zero);
            Assert.That(result[7], Is.EqualTo(1));
        }

        [Test]
        public async Task TransportJoiningDuringSendReceivesFullSnapshotNext()
        {
            var gate = new TaskCompletionSource<bool>();
            var first = new Transport { Gate = gate.Task }; manager.RegisterTransport(first);
            var sending = manager.SendSnapshotAsync(new byte[20]).AsTask();
            var joining = new Transport(); manager.RegisterTransport(joining);
            gate.SetResult(true); await sending;
            var next = new byte[20]; next[0] = 1;
            await manager.SendDiffFromSnapshotAsync(next);
            Assert.That(joining.Messages.Single().messageType, Is.EqualTo(SyncMessageType.Snapshot));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RemovedTransportCannotReceiveAnEarlierBroadcast(bool registerAgain)
        {
            var gate = new TaskCompletionSource<bool>();
            var first = new Transport { Gate = gate.Task };
            var removed = new Transport();
            manager.RegisterTransport(first); manager.RegisterTransport(removed);
            var sending = manager.SendSnapshotAsync(new byte[20]).AsTask();
            manager.UnregisterTransport(removed);
            if (registerAgain) manager.RegisterTransport(removed);
            gate.SetResult(true);
            await Complete(sending);
            Assert.That(removed.Messages, Is.Empty);
            if (registerAgain)
            {
                await manager.SendDiffFromSnapshotAsync(new byte[20]);
                Assert.That(removed.Messages.Single().messageType, Is.EqualTo(SyncMessageType.Snapshot));
            }
        }

        [Test]
        public async Task ConcurrentInitializationIncludesNewRegistrationsAndSkipsRemovedOnes()
        {
            var gate = new TaskCompletionSource<bool>();
            var first = new Transport { InitializeGate = gate.Task };
            var removed = new Transport(); var joining = new Transport();
            manager.RegisterTransport(first); manager.RegisterTransport(removed);
            var initializing = manager.InitializeAsync();
            var queued = manager.InitializeAsync();
            manager.UnregisterTransport(removed); manager.RegisterTransport(joining);
            bool held = first.InitializeCalls == 1 && !queued.IsCompleted && !manager.IsInitialized;
            gate.SetResult(true);
            await Complete(Task.WhenAll(initializing, queued));
            Assert.That(held, Is.True);
            Assert.That(manager.IsInitialized, Is.True);
            Assert.That(removed.InitializeCalls + removed.ConnectCalls, Is.Zero);
            Assert.That(first.InitializeCalls, Is.EqualTo(1));
            Assert.That(first.ConnectCalls, Is.EqualTo(1));
            Assert.That(joining.InitializeCalls, Is.EqualTo(1));
            Assert.That(joining.ConnectCalls, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ReplacedRegistrationDoesNotInheritAnOldInitializationCompletion(bool waitingForConnect)
        {
            var gate = new TaskCompletionSource<bool>();
            var transport = new Transport();
            if (waitingForConnect) transport.ConnectGate = gate.Task;
            else transport.InitializeGate = gate.Task;
            manager.RegisterTransport(transport);
            var initializing = manager.InitializeAsync();
            manager.UnregisterTransport(transport); manager.RegisterTransport(transport);
            gate.SetResult(true);
            await Complete(initializing);
            Assert.That(manager.IsInitialized, Is.True);
            Assert.That(transport.InitializeCalls, Is.EqualTo(2));
            Assert.That(transport.ConnectCalls, Is.EqualTo(waitingForConnect ? 2 : 1));
            Assert.That(transport.Subscribers, Is.EqualTo(1));
        }

        [Test]
        public async Task FailedConnectionRetryPreservesHealthyInitializationAndConnection()
        {
            var healthy = new Transport(); var failing = new Transport { FailConnect = true };
            manager.RegisterTransport(healthy); manager.RegisterTransport(failing);
            Assert.ThrowsAsync<InvalidOperationException>(() => manager.InitializeAsync());
            Assert.That(manager.IsInitialized, Is.False);
            failing.FailConnect = false;
            await Complete(manager.InitializeAsync());
            Assert.That(manager.IsInitialized, Is.True);
            Assert.That(healthy.InitializeCalls, Is.EqualTo(1));
            Assert.That(healthy.ConnectCalls, Is.EqualTo(1));
            Assert.That(failing.InitializeCalls, Is.EqualTo(1));
            Assert.That(failing.ConnectCalls, Is.EqualTo(2));
        }

        [Test]
        public async Task RegistrationAfterInitializationWaitsForExplicitInitialization()
        {
            var first = new Transport(); manager.RegisterTransport(first);
            await Complete(manager.InitializeAsync());
            var joining = new Transport(); manager.RegisterTransport(joining);
            Assert.That(manager.IsInitialized, Is.False);
            Assert.That(joining.InitializeCalls + joining.ConnectCalls, Is.Zero);
            await Complete(manager.InitializeAsync());
            Assert.That(manager.IsInitialized, Is.True);
            Assert.That(first.InitializeCalls, Is.EqualTo(1));
            Assert.That(first.ConnectCalls, Is.EqualTo(1));
            Assert.That(joining.InitializeCalls, Is.EqualTo(1));
            Assert.That(joining.ConnectCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task RetiredOwnerCannotConnectAfterItsInitializationFinishes()
        {
            var gate = new TaskCompletionSource<bool>();
            var transport = new Transport { InitializeGate = gate.Task }; manager.RegisterTransport(transport);
            var initializing = manager.InitializeAsync(); var queued = manager.InitializeAsync();
            typeof(SyncManager).GetMethod("Retire", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            gate.SetResult(true);
            try { await Complete(Task.WhenAll(initializing, queued)); }
            catch (ObjectDisposedException) { }
            Assert.That(initializing.IsFaulted && queued.IsFaulted, Is.True);
            Assert.That(manager.IsInitialized, Is.False);
            Assert.That(transport.ConnectCalls, Is.Zero);
        }

        [Test]
        public void InvalidHashCannotReplaceAnAcceptedSnapshotOrAdvanceItsSequence()
        {
            var receiver = new SyncReconciler(null);
            byte[] initial = new byte[20], next = new byte[20]; next[3] = 9;
            Assert.That(receiver.TryApplyEnvelope(Envelope(initial, 1), out var exposed), Is.True);
            exposed[0] = 7;
            var bad = Envelope(next, 500); bad.payloadHash = "wrong";
            Assert.That(receiver.TryApplyEnvelope(bad, out _), Is.False);
            Assert.That(SyncDeltaCodec.TryCreateDelta(initial, next, out var delta), Is.True);
            var valid = Envelope(new SyncJsonSerializer().Serialize(delta), 2);
            valid.messageType = SyncMessageType.Diff;
            Assert.That(receiver.TryApplyEnvelope(valid, out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(next));
        }

        [TestCase(-1, 20, 20)]
        [TestCase(19, 20, 20)]
        [TestCase(0, -1, 20)]
        [TestCase(0, 20, 19)]
        [TestCase(0, int.MaxValue, 20)]
        public void MalformedDeltaRejectsInsteadOfClampingOrThrowing(int offset, int length, int baseLength)
        {
            var original = new byte[20];
            var delta = new SyncDelta { baseHash = SyncDeltaCodec.ComputeHash(original), baseLength = baseLength,
                newLength = length, chunks = new List<SyncDeltaChunk> { new SyncDeltaChunk { offset = offset, data = new byte[] { 3, 4 } } } };
            Assert.That(SyncDeltaCodec.ApplyDelta(original, delta), Is.Null);
            Assert.That(original, Is.EqualTo(new byte[20]));
        }

        [Test]
        public void LegacyHashlessSnapshotAndEmptySnapshotRemainReadable()
        {
            var receiver = new SyncReconciler(null);
            var legacy = Envelope(new byte[] { 1, 2 }, 1); legacy.payloadHash = null;
            Assert.That(receiver.TryApplyEnvelope(legacy, out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(new byte[] { 1, 2 }));
            Assert.That(receiver.TryApplyEnvelope(Envelope(Array.Empty<byte>(), 2), out restored), Is.True);
            Assert.That(restored, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void MalformedSerializedDeltaDoesNotAdvanceOrReplaceAcceptedState(int decoder)
        {
            var serializer = new RejectingSerializer();
            var receiver = new SyncReconciler(decoder == 0 ? null : serializer);
            byte[] initial = new byte[20], next = new byte[20]; next[4] = 6;
            Assert.That(receiver.TryApplyEnvelope(Envelope(initial, 1), out _), Is.True);
            int requests = 0; receiver.OnRequireSnapshot += _ => requests++;
            serializer.Error = decoder == 1 ? new Newtonsoft.Json.JsonReaderException("Rejected JSON") :
                new MemoryPack.MemoryPackSerializationException("Rejected MemoryPack");
            var malformed = Envelope(System.Text.Encoding.UTF8.GetBytes("{ definitely invalid JSON"), 100);
            malformed.messageType = SyncMessageType.Diff;
            Assert.That(receiver.TryApplyEnvelope(malformed, out _), Is.False);
            Assert.That(requests, Is.EqualTo(1));
            serializer.Error = null;
            Assert.That(SyncDeltaCodec.TryCreateDelta(initial, next, out var delta), Is.True);
            var valid = Envelope(new SyncJsonSerializer().Serialize(delta), 2); valid.messageType = SyncMessageType.Diff;
            Assert.That(receiver.TryApplyEnvelope(valid, out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(next));
        }

        [Test]
        public void RetiredOwnerUnsubscribesFromPersistentTransport()
        {
            var transport = new Transport(); manager.RegisterTransport(transport);
            int notifications = 0; manager.OnMessage += _ => notifications++;
            transport.Receive(Envelope(new byte[] { 1 }, 1));
            Assert.That(notifications, Is.EqualTo(1));
            typeof(SyncManager).GetMethod("Retire", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            transport.Receive(Envelope(new byte[] { 2 }, 2));
            Assert.That(transport.Subscribers, Is.Zero);
            Assert.That(notifications, Is.EqualTo(1));
        }

        static SyncEnvelope Envelope(byte[] payload, int sequence) => new SyncEnvelope
        { messageType = SyncMessageType.Snapshot, sequence = sequence, payloadBase64 = Convert.ToBase64String(payload), payloadHash = SyncDeltaCodec.ComputeHash(payload) };

        static async Task Complete(Task operation)
        {
            Assert.That(await Task.WhenAny(operation, Task.Delay(5000)), Is.SameAs(operation), "Sync operation timed out.");
            await operation;
        }

        sealed class RejectingSerializer : ISyncSerializer
        {
            public Exception Error;
            public byte[] Serialize<T>(T payload) => new SyncJsonSerializer().Serialize(payload);
            public T Deserialize<T>(byte[] data)
            {
                if (Error != null) throw Error;
                return new SyncJsonSerializer().Deserialize<T>(data);
            }
        }

        sealed class Transport : ISyncTransport
        {
            public string Name => "isolated";
            public bool IsConnected => true;
            public bool IsServer => true;
            public event Action<SyncEnvelope> OnMessage;
            public readonly List<SyncEnvelope> Messages = new();
            public bool Fail;
            public Task Gate;
            public Task InitializeGate, ConnectGate;
            public int InitializeCalls, ConnectCalls;
            public bool FailConnect;
            public int Subscribers => OnMessage?.GetInvocationList().Length ?? 0;
            public void Receive(SyncEnvelope envelope) => OnMessage?.Invoke(envelope);
            public ValueTask InitializeAsync(SyncTransportConfig config)
            {
                InitializeCalls++;
                return InitializeGate == null ? default : new ValueTask(InitializeGate);
            }
            public ValueTask ConnectAsync()
            {
                ConnectCalls++;
                if (FailConnect) throw new InvalidOperationException("Injected connection failure");
                return ConnectGate == null ? default : new ValueTask(ConnectGate);
            }
            public ValueTask DisconnectAsync() => default;
            public ValueTask SendAsync(SyncEnvelope message)
            {
                if (Fail) throw new InvalidOperationException("Injected transport failure");
                Messages.Add(message);
                return Gate == null ? default : new ValueTask(Gate);
            }
        }
    }
}
#endif
