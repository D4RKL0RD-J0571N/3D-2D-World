#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveLateJoinTests
    {
        readonly List<SyncManager> managers = new();
        readonly List<SyncSettings> assets = new();
        [TearDown] public void TearDown()
        {
            foreach (var manager in managers) typeof(SyncManager).GetMethod("Retire", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            managers.Clear(); assets.Clear();
        }
        [TestCase(SyncSnapshotCompression.GZip, 0)]
        [TestCase(SyncSnapshotCompression.GZip, 1)]
        [TestCase(SyncSnapshotCompression.GZip, 65537)]
        [TestCase(SyncSnapshotCompression.Lz4, 0)]
        [TestCase(SyncSnapshotCompression.Lz4, 1)]
        [TestCase(SyncSnapshotCompression.Lz4, 12)]
        [TestCase(SyncSnapshotCompression.Lz4, 15)]
        [TestCase(SyncSnapshotCompression.Lz4, 255)]
        [TestCase(SyncSnapshotCompression.Lz4, 65537)]
        public void CompressionPreservesRandomAndRepeatingBytes(SyncSnapshotCompression codec, int length)
        {
            var random = new byte[length]; new System.Random(1976).NextBytes(random);
            foreach (var bytes in new[] { random, Enumerable.Range(0,length).Select(i => (byte)(i % 7)).ToArray() })
            {
                var encoded = SyncSnapshotCodec.Compress(bytes, codec);
                Assert.That(SyncSnapshotCodec.Decompress(encoded, codec, length), Is.EqualTo(bytes));
                if (length > 1000 && bytes != random) Assert.That(encoded.Length, Is.LessThan(bytes.Length / 4));
                Assert.Throws<InvalidDataException>(() => SyncSnapshotCodec.Decompress(encoded, codec, length + 1));
            }
        }
        [Test]
        public void Lz4ReadsNative110GoldenBlock()
        {
            // Produced by the independent liblz4 1.10.0 LZ4_compress_default encoder.
            var expected = Enumerable.Range(0,255).Select(i=>(byte)(97+i%8)).ToArray();
            var encoded = Convert.FromBase64String("j2FiY2RlZmdoCADfUGNkZWZn");
            Assert.That(SyncSnapshotCodec.Decompress(encoded,SyncSnapshotCompression.Lz4,255),Is.EqualTo(expected));
        }
        [Test]
        public void Lz4RejectsTruncatedLengthsZeroOffsetsAndOutputOverflow()
        {
            foreach (var bytes in new[] { new byte[]{0,0,0}, new byte[]{0xf0,255}, new byte[]{0x10,65,1,0}, new byte[]{0,1,0}, Array.Empty<byte>() })
                Assert.Throws<InvalidDataException>(() => SyncSnapshotCodec.Decompress(bytes, SyncSnapshotCompression.Lz4, 2));
            Assert.Throws<InvalidDataException>(() => SyncSnapshotCodec.Decompress(new byte[]{0}, SyncSnapshotCompression.Lz4, int.MaxValue));
            Assert.Throws<InvalidDataException>(() => SyncSnapshotCodec.Decompress(new byte[]{0}, (SyncSnapshotCompression)99, 0));
        }
        [TestCase(false)] [TestCase(true)]
        public async Task LateJoinCapturesFreshWorldOnlyForNewcomerAndWaitsForApplication(bool lz4)
        {
            var authority = new Peer(true, "server"); var existing = new Peer(false,"existing",authority); var newcomer = new Peer(false,"new",authority);
            var old = RandomBytes(2000); var current = RandomBytes(70000);
            var hostWorld = new World { Bytes = old }; var existingWorld = new World(); var joiningWorld = new World { ApplyGate = new TaskCompletionSource<bool>() };
            var host = Create(authority, hostWorld, lz4); var first = Create(existing, existingWorld); var joining = Create(newcomer, joiningWorld);
            await host.InitializeAsync(); await first.InitializeAsync(); await joining.InitializeAsync();
            var readyPeers = new List<string>(); host.OnLateJoinPeerReady += (_,peer) => readyPeers.Add(peer);
            await first.RequestLateJoinSnapshotAsync(existing);
            await Until(() => readyPeers.Count == 1);
            Assert.That(readyPeers.Single(), Is.EqualTo("existing"));
            readyPeers.Clear();
            hostWorld.Bytes = current; // no save and no periodic broadcast precedes the new join
            var request = joining.RequestLateJoinSnapshotAsync(newcomer);
            await Until(() => joiningWorld.ApplyStarted);
            Assert.That(request.IsCompleted,Is.False); Assert.That(readyPeers,Is.Empty);
            Assert.That(existingWorld.Bytes,Is.EqualTo(old)); Assert.That(existingWorld.Applies,Is.EqualTo(1));
            joiningWorld.ApplyGate.SetResult(true);
            var result = await request;
            await Until(() => readyPeers.Count == 1);
            Assert.That(readyPeers.Single(), Is.EqualTo("new"));
            Assert.That(result.ChunkCount,Is.GreaterThan(1));
            Assert.That(result.Compression,Is.EqualTo(lz4 ? SyncSnapshotCompression.Lz4 : SyncSnapshotCompression.GZip));
            Assert.That(joiningWorld.Bytes,Is.EqualTo(current)); Assert.That(hostWorld.Captures,Is.EqualTo(2));
            Assert.That(joining.IsLateJoinSynchronized,Is.True);
            Assert.That(newcomer.Sent.Count(e=>e.messageType==SyncMessageType.LateJoinApplied),Is.EqualTo(1));
            Assert.That(hostWorld.Applies,Is.Zero,"Authority must never apply a client snapshot");
        }
        [TestCase("hash")] [TestCase("length")] [TestCase("codec")] [TestCase("duplicate")]
        public async Task CorruptTransferNeverAppliesWorld(string corruption)
        {
            var server=new Peer(true,"server");var client=new Peer(false,"new",server);
            var world=new World();var host=Create(server,new World{Bytes=RandomBytes(4000)});var joining=Create(client,world);
            await host.InitializeAsync();await joining.InitializeAsync();
            server.Mutate = envelope =>
            {
                if(envelope.messageType!=SyncMessageType.LateJoinChunk)return envelope;
                var codec=new SyncJsonSerializer(); var packet=codec.Deserialize<Packet>(Convert.FromBase64String(envelope.payloadBase64));
                if(corruption=="hash")packet.hash=new string('0',64);
                if(corruption=="length")packet.plainBytes=int.MaxValue;
                if(corruption=="codec")packet.compression=(SyncSnapshotCompression)99;
                if(corruption=="duplicate")packet.index=0;
                byte[] bytes=codec.Serialize(packet); envelope.payloadBase64=Convert.ToBase64String(bytes);envelope.payloadHash=SyncDeltaCodec.ComputeHash(bytes);return envelope;
            };
            try { await joining.RequestLateJoinSnapshotAsync(client);Assert.Fail("Malformed transfer was accepted"); }
            catch(InvalidDataException) { }
            Assert.That(world.Applies,Is.Zero);Assert.That(joining.IsLateJoinSynchronized,Is.False);
        }
        [Test]
        public async Task PeerAdmissionRetiresInFlightLegacyBroadcastBaseline()
        {
            var server=new Peer(true,"server"){BroadcastGate=new TaskCompletionSource<bool>()};var client=new Peer(false,"new",server);
            var host=Create(server,new World{Bytes=RandomBytes(1000)});var joining=Create(client,new World());
            assets[0].enableDiffs=true;
            await host.InitializeAsync();await joining.InitializeAsync();
            var previous=RandomBytes(2000);var broadcast=host.SendSnapshotAsync(previous).AsTask();
            int admitted=0;host.OnLateJoinPeerReady+=(_,__)=>admitted++;
            await joining.RequestLateJoinSnapshotAsync(client);await Until(()=>admitted==1);
            server.BroadcastGate.SetResult(true);await broadcast;
            previous[10]^=1;await host.SendDiffFromSnapshotAsync(previous);
            Assert.That(server.Sent.Last().messageType,Is.EqualTo(SyncMessageType.Snapshot),
                "An old in-flight completion cannot restore the baseline retired by peer admission");
        }
        [Test]
        public async Task ReorderedAndIdenticalDuplicateChunksApplyExactlyOnce()
        {
            var server=new Peer(true,"server"){HoldChunks=true};var client=new Peer(false,"new",server);
            var expected=RandomBytes(4000);var host=Create(server,new World{Bytes=expected});var world=new World();var joining=Create(client,world);
            await host.InitializeAsync();await joining.InitializeAsync();
            var task=joining.RequestLateJoinSnapshotAsync(client);
            var codec=new SyncJsonSerializer();
            await Until(()=>server.Sent.Any(e=>e.messageType==SyncMessageType.LateJoinChunk)&&
                server.Sent.Count(e=>e.messageType==SyncMessageType.LateJoinChunk)==
                codec.Deserialize<Packet>(Convert.FromBase64String(server.Sent.First(e=>e.messageType==SyncMessageType.LateJoinChunk).payloadBase64)).count);
            Assert.That(world.Applies,Is.Zero);
            foreach(var envelope in server.Sent.Where(e=>e.messageType==SyncMessageType.LateJoinChunk).Reverse().ToArray())
            {client.Deliver(SyncPeer.Server,envelope);client.Deliver(SyncPeer.Server,envelope);}
            await task;
            Assert.That(world.Applies,Is.EqualTo(1));Assert.That(world.Bytes,Is.EqualTo(expected));
        }
        [Test]
        public async Task CommittedApplicationCannotAdmitReplacementConnection()
        {
            var server=new Peer(true,"server");var client=new Peer(false,"new",server);
            var expected=RandomBytes(1000);var host=Create(server,new World{Bytes=expected});
            var world=new World{ApplyGate=new TaskCompletionSource<bool>(),IgnoreApplyCancellation=true};var joining=Create(client,world);
            await host.InitializeAsync();await joining.InitializeAsync();
            var task=joining.RequestLateJoinSnapshotAsync(client);
            await Until(()=>world.ApplyStarted);
            await client.DisconnectAsync();await client.ConnectAsync();
            world.ApplyGate.SetResult(true);
            await task;
            Assert.That(world.Bytes,Is.EqualTo(expected),"Committed work reports its actual outcome");
            Assert.That(joining.IsLateJoinSynchronized,Is.False,"The replacement connection still needs a fresh snapshot");
            Assert.That(client.Sent.Any(e=>e.messageType==SyncMessageType.LateJoinApplied),Is.False);
        }
        [Test]
        public async Task AutomaticConnectionAndReconnectCaptureFreshWorld()
        {
            var server=new Peer(true,"server");var client=new Peer(false,"new",server);
            var authority=new World{Bytes=RandomBytes(1000)};var world=new World();
            var host=Create(server,authority);var joining=Create(client,world);
            assets.Last().autoConnectTransports=true;assets.Last().autoRequestLateJoinSnapshot=true;
            int ready=0;host.OnLateJoinPeerReady+=(_,__)=>ready++;
            await host.InitializeAsync();await joining.InitializeAsync();
            await Until(()=>joining.IsLateJoinSynchronized&&ready==1);
            Assert.That(world.Bytes,Is.EqualTo(authority.Bytes));
            await client.DisconnectAsync();Assert.That(joining.IsLateJoinSynchronized,Is.False);
            authority.Bytes=RandomBytes(1200);
            await Until(()=>joining.IsLateJoinSynchronized&&ready==2);
            Assert.That(world.Bytes,Is.EqualTo(authority.Bytes));Assert.That(world.Applies,Is.EqualTo(2));
        }
        [Test]
        public async Task StalledCustomTransportSendHasBoundedDeadline()
        {
            var server=new Peer(true,"server");var client=new Peer(false,"new",server);
            var joining=Create(client,new World());assets.Last().lateJoinTimeoutSeconds=1;
            client.SendGate=new TaskCompletionSource<bool>();
            await joining.InitializeAsync();
            try{await joining.RequestLateJoinSnapshotAsync(client);Assert.Fail("Stalled send did not time out");}catch(TimeoutException){}
            Assert.That(joining.IsLateJoinSynchronized,Is.False);
            client.SendGate.SetResult(true);
        }
        [Test]
        public async Task MissingResponseTimesOutAndCancelledRequestCannotConsumeLateChunks()
        {
            var server=new Peer(true,"server");var client=new Peer(false,"new",server);
            var gate=new TaskCompletionSource<byte[]>();var host=Create(server,new World{CaptureGate=gate});var world=new World();var joining=Create(client,world);
            await host.InitializeAsync();await joining.InitializeAsync();
            var cancellation=new CancellationTokenSource();var request=joining.RequestLateJoinSnapshotAsync(client,cancellation.Token);
            cancellation.Cancel();
            try {await request;Assert.Fail("Cancellation ignored");}catch(OperationCanceledException){}
            gate.SetResult(RandomBytes(2000));
            await Until(()=>server.Sent.Any(e=>e.messageType==SyncMessageType.LateJoinChunk));
            Assert.That(world.Applies,Is.Zero);
            // A connected custom transport which drops requests must have a bounded client deadline.
            client.DropRequests=true; assets.Last().lateJoinTimeoutSeconds=1;
            try {await joining.RequestLateJoinSnapshotAsync(client);Assert.Fail("Missing response did not time out");}catch(TimeoutException){}
            Assert.That(world.Applies,Is.Zero);cancellation.Dispose();
        }
        [Test]
        public async Task DisconnectRetiresRequestAndUnregisterDetachesPeerCallbacks()
        {
            var server=new Peer(true,"server");var client=new Peer(false,"new",server);
            var gate=new TaskCompletionSource<byte[]>();var host=Create(server,new World{CaptureGate=gate});var world=new World();var joining=Create(client,world);
            await host.InitializeAsync();await joining.InitializeAsync();
            var task=joining.RequestLateJoinSnapshotAsync(client);
            await client.DisconnectAsync();
            try{await task;Assert.Fail("Disconnect ignored");}catch(OperationCanceledException){}
            joining.UnregisterTransport(client);Assert.That(client.PeerSubscribers,Is.Zero);Assert.That(client.DisconnectSubscribers,Is.Zero);
            gate.SetResult(RandomBytes(1000));await Task.Yield();Assert.That(world.Applies,Is.Zero);
        }
        [Test]
        public async Task UntrustedSenderAndClientSnapshotsCannotMutateAuthority()
        {
            var server=new Peer(true,"server");var client=new Peer(false,"new",server);
            var gate=new TaskCompletionSource<byte[]>();var hostWorld=new World{CaptureGate=gate};var host=Create(server,hostWorld);var world=new World();var joining=Create(client,world);
            await host.InitializeAsync();await joining.InitializeAsync();
            var task=joining.RequestLateJoinSnapshotAsync(client);
            var request=client.Sent.Last();request.messageType=SyncMessageType.LateJoinRejected;
            client.Deliver("another-client",request);await Task.Yield();Assert.That(task.IsCompleted,Is.False);
            server.Deliver("new",new SyncEnvelope{messageType=SyncMessageType.LateJoinChunk,slotId=request.slotId,payloadBase64=request.payloadBase64,payloadHash=request.payloadHash});
            Assert.That(hostWorld.Applies,Is.Zero);
            gate.SetResult(RandomBytes(1000));await task;Assert.That(world.Applies,Is.EqualTo(1));
        }
        [Test]
        public async Task ClientApplicationFailureNeverAcknowledgesSuccess()
        {
            var server=new Peer(true,"server");var client=new Peer(false,"new",server);
            var host=Create(server,new World{Bytes=RandomBytes(1000)});var joining=Create(client,new World{FailApply=true});
            await host.InitializeAsync();await joining.InitializeAsync();
            try {await joining.RequestLateJoinSnapshotAsync(client);Assert.Fail("Application failure swallowed");}catch(InvalidOperationException){}
            Assert.That(client.Sent.Any(e=>e.messageType==SyncMessageType.LateJoinApplied),Is.False);
            Assert.That(joining.IsLateJoinSynchronized,Is.False);
        }
        [Test]
        public async Task ApplicationPastAuthorityDeadlinePreservesOutcomeAndRetriesAdmission()
        {
            var server = new Peer(true, "server"); var client = new Peer(false, "new", server);
            var hostWorld = new World { Bytes = RandomBytes(1000) };
            var world = new World { ApplyGate = new TaskCompletionSource<bool>() };
            var host = Create(server, hostWorld); var joining = Create(client, world);
            assets[0].lateJoinTimeoutSeconds = 1;
            int admitted = 0; host.OnLateJoinPeerReady += (_, __) => admitted++;
            await host.InitializeAsync(); await joining.InitializeAsync();
            var request = joining.RequestLateJoinSnapshotAsync(client);
            await Until(() => world.ApplyStarted);
            await Until(() => server.Sent.Any(e => e.messageType == SyncMessageType.LateJoinRejected));
            world.ApplyGate.SetResult(true);
            await Until(() => request.IsCompleted); await request;
            Assert.That(world.Applies, Is.EqualTo(1), "Committed application keeps its actual successful outcome");
            Assert.That(joining.IsLateJoinSynchronized, Is.False, "An expired authority request cannot admit this connection");
            Assert.That(admitted, Is.Zero);
            hostWorld.Bytes = RandomBytes(2000);
            assets[1].autoRequestLateJoinSnapshot = true;
            await Until(() => joining.IsLateJoinSynchronized && admitted == 1);
            Assert.That(world.Bytes, Is.EqualTo(hostWorld.Bytes), "Recovery captures the current authority world");
            Assert.That(hostWorld.Captures, Is.EqualTo(2));
        }

        [Test]
        public async Task FailedApplicationAcknowledgementKeepsReadinessFalseAndAutomaticallyRetries()
        {
            var server = new Peer(true, "server"); var client = new Peer(false, "new", server);
            client.Mutate = envelope =>
            {
                if (envelope.messageType == SyncMessageType.LateJoinApplied)
                    throw new IOException("Fixture acknowledgement send failed");
                return envelope;
            };
            var hostWorld = new World { Bytes = RandomBytes(1000) }; var world = new World();
            var host = Create(server, hostWorld); var joining = Create(client, world);
            assets[0].lateJoinTimeoutSeconds = 1;
            int admitted = 0; host.OnLateJoinPeerReady += (_, __) => admitted++;
            await host.InitializeAsync(); await joining.InitializeAsync();
            await joining.RequestLateJoinSnapshotAsync(client);
            Assert.That(world.Applies, Is.EqualTo(1));
            Assert.That(joining.IsLateJoinSynchronized, Is.False);
            await Until(() => server.Sent.Any(e => e.messageType == SyncMessageType.LateJoinRejected));
            client.Mutate = null;
            assets[1].autoRequestLateJoinSnapshot = true;
            await Until(() => joining.IsLateJoinSynchronized && admitted == 1);
            Assert.That(hostWorld.Captures, Is.EqualTo(2));
        }

        [Test]
        public async Task ReadinessWaitsForMatchingAuthorityConfirmation()
        {
            var server = new Peer(true, "server") { HoldAccepted = true }; var client = new Peer(false, "new", server);
            var world = new World(); var host = Create(server, new World { Bytes = RandomBytes(1000) }); var joining = Create(client, world);
            int admitted = 0; host.OnLateJoinPeerReady += (_, __) => admitted++;
            await host.InitializeAsync(); await joining.InitializeAsync();
            var request = joining.RequestLateJoinSnapshotAsync(client);
            await Until(() => server.Sent.Any(e => e.messageType == SyncMessageType.LateJoinAccepted));
            var accepted = server.Sent.Last(e => e.messageType == SyncMessageType.LateJoinAccepted);
            Assert.That(world.Applies, Is.EqualTo(1)); Assert.That(admitted, Is.EqualTo(1));
            Assert.That(request.IsCompleted, Is.False); Assert.That(joining.IsLateJoinSynchronized, Is.False);
            client.Deliver("untrusted-client", accepted); await Task.Yield();
            Assert.That(request.IsCompleted, Is.False);
            client.Deliver(SyncPeer.Server, accepted);
            var result = await request;
            Assert.That(result.AuthorityConfirmed, Is.True); Assert.That(joining.IsLateJoinSynchronized, Is.True);
            client.Deliver(SyncPeer.Server, accepted); await Task.Yield();
            Assert.That(world.Applies, Is.EqualTo(1)); Assert.That(admitted, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public async Task LostAuthorityConfirmationKeepsReadinessFalseAndRecovers(bool failSend)
        {
            var server = new Peer(true, "server") { HoldAccepted = !failSend }; var client = new Peer(false, "new", server);
            if (failSend) server.Mutate = e => e.messageType == SyncMessageType.LateJoinAccepted
                ? throw new IOException("Fixture confirmation failure") : e;
            var world = new World(); var host = Create(server, new World { Bytes = RandomBytes(1000) }); var joining = Create(client, world);
            assets[1].lateJoinTimeoutSeconds = 1;
            int admitted = 0; host.OnLateJoinPeerReady += (_, __) => admitted++;
            await host.InitializeAsync(); await joining.InitializeAsync();
            var result = await joining.RequestLateJoinSnapshotAsync(client);
            Assert.That(result.AuthorityConfirmed, Is.False); Assert.That(joining.IsLateJoinSynchronized, Is.False);
            Assert.That(world.Applies, Is.EqualTo(1)); Assert.That(admitted, Is.EqualTo(1));
            Assert.That(server.Sent.Any(e => e.messageType == SyncMessageType.LateJoinRejected), Is.False,
                "A failed confirmation does not retract authority admission");
            server.Mutate = null; server.HoldAccepted = false; assets[1].autoRequestLateJoinSnapshot = true;
            await Until(() => joining.IsLateJoinSynchronized && admitted == 2);
            Assert.That(world.Applies, Is.EqualTo(2));
        }

        [Test]
        public async Task ExpiredConfirmationCannotAdmitReplacementRequest()
        {
            var server = new Peer(true, "server") { HoldAccepted = true }; var client = new Peer(false, "new", server);
            var host = Create(server, new World { Bytes = RandomBytes(1000) }); var joining = Create(client, new World());
            assets[1].lateJoinTimeoutSeconds = 1;
            await host.InitializeAsync(); await joining.InitializeAsync();
            var first = await joining.RequestLateJoinSnapshotAsync(client);
            Assert.That(first.AuthorityConfirmed, Is.False);
            var stale = server.Sent.Last(e => e.messageType == SyncMessageType.LateJoinAccepted);
            var request = joining.RequestLateJoinSnapshotAsync(client);
            await Until(() => server.Sent.Count(e => e.messageType == SyncMessageType.LateJoinAccepted) == 2);
            client.Deliver(SyncPeer.Server, stale); await Task.Yield();
            Assert.That(request.IsCompleted, Is.False); Assert.That(joining.IsLateJoinSynchronized, Is.False);
            client.Deliver(SyncPeer.Server, server.Sent.Last(e => e.messageType == SyncMessageType.LateJoinAccepted));
            Assert.That((await request).AuthorityConfirmed, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public async Task AuthorityConfirmationDoesNotDependOnLateTransportSendCompletion(bool failSend)
        {
            var gate = new TaskCompletionSource<bool>();
            var server = new Peer(true, "server"); var client = new Peer(false, "new", server) { AppliedSendCompletion = gate };
            var host = Create(server, new World { Bytes = RandomBytes(1000) }); var joining = Create(client, new World());
            await host.InitializeAsync(); await joining.InitializeAsync();
            var request = joining.RequestLateJoinSnapshotAsync(client);
            await Until(() => request.IsCompleted);
            Assert.That((await request).AuthorityConfirmed, Is.True);
            Assert.That(gate.Task.IsCompleted, Is.False);
            if (failSend) gate.SetException(new IOException("Late send failure after confirmed admission")); else gate.SetResult(true);
            await Task.Yield(); Assert.That(joining.IsLateJoinSynchronized, Is.True);
        }

        [Test]
        public async Task ReconnectionDuringAcknowledgementCannotPublishReadiness()
        {
            var server = new Peer(true, "server"); var client = new Peer(false, "new", server);
            var world = new World(); var host = Create(server, new World { Bytes = RandomBytes(1000) }); var joining = Create(client, world);
            client.Mutate = e =>
            {
                if (e.messageType == SyncMessageType.LateJoinApplied) { client.DisconnectAsync(); client.ConnectAsync(); }
                return e;
            };
            await host.InitializeAsync(); await joining.InitializeAsync();
            var result = await joining.RequestLateJoinSnapshotAsync(client);
            Assert.That(result.AuthorityConfirmed, Is.False); Assert.That(joining.IsLateJoinSynchronized, Is.False);
            Assert.That(world.Applies, Is.EqualTo(1));
        }

        [Test]
        public async Task ThrowingLateJoinObserversCannotSuppressLaterAdmissionOrApplicationObservers()
        {
            var server = new Peer(true, "server"); var client = new Peer(false, "new", server);
            var host = Create(server, new World { Bytes = RandomBytes(1000) }); var joining = Create(client, new World());
            int admitted = 0, applied = 0;
            host.OnLateJoinPeerReady += (_, __) => throw new InvalidOperationException("Fixture authority observer");
            host.OnLateJoinPeerReady += (_, __) => admitted++;
            joining.OnLateJoinSnapshotApplied += _ => throw new InvalidOperationException("Fixture application observer");
            joining.OnLateJoinSnapshotApplied += _ => applied++;
            await host.InitializeAsync(); await joining.InitializeAsync();
            LogAssert.Expect(LogType.Exception, new Regex("Fixture authority observer"));
            LogAssert.Expect(LogType.Exception, new Regex("Fixture application observer"));
            var result = await joining.RequestLateJoinSnapshotAsync(client);
            Assert.That(result.AuthorityConfirmed, Is.True); Assert.That(admitted, Is.EqualTo(1)); Assert.That(applied, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingTransportMessageObserverCannotSuppressProtocolListener()
        {
            var transport = ScriptableObject.CreateInstance<NotificationTransport>();
            try
            {
                int received = 0;
                transport.OnMessage += _ => throw new InvalidOperationException("Fixture transport observer");
                transport.OnMessage += _ => received++;
                LogAssert.Expect(LogType.Exception, new Regex("Fixture transport observer"));
                Assert.DoesNotThrow(() => transport.Deliver(new SyncEnvelope()));
                Assert.That(received, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(transport); }
        }

        public sealed class NotificationTransport : SyncTransportAsset
        {
            public void Deliver(SyncEnvelope envelope) => RaiseMessage(envelope);
            public override ValueTask InitializeAsync(SyncTransportConfig config) => default;
            public override ValueTask ConnectAsync() => default;
            public override ValueTask DisconnectAsync() => default;
            public override ValueTask SendAsync(SyncEnvelope envelope) => default;
        }

        SyncManager Create(Peer peer,World world,bool lz4=false)
        {
            var settings=ScriptableObject.CreateInstance<SyncSettings>();assets.Add(settings);
            settings.enableLateJoinSnapshots=true;settings.autoRequestLateJoinSnapshot=false;settings.autoConnectTransports=false;
            settings.lateJoinChunkBytes=512;settings.lateJoinMaxSnapshotBytes=128*1024;settings.lateJoinUseLz4=lz4;
            var manager=new SyncManager(null,settings){SnapshotProvider=world};managers.Add(manager);manager.RegisterTransport(peer);return manager;
        }
        static byte[] RandomBytes(int length){var bytes=new byte[length];new System.Random(length).NextBytes(bytes);return bytes;}
        static async Task Until(Func<bool> condition)
        {
            var deadline=DateTime.UtcNow.AddSeconds(10);
            while(!condition()){if(DateTime.UtcNow>deadline)throw new TimeoutException("Late-join regression wait");await Task.Yield();}
        }
        sealed class World:ISyncWorldSnapshotProvider
        {
            internal byte[] Bytes;internal int Captures,Applies;internal bool ApplyStarted,FailApply,IgnoreApplyCancellation;
            internal TaskCompletionSource<byte[]> CaptureGate;internal TaskCompletionSource<bool> ApplyGate;
            public async Task<byte[]> CaptureAsync(CancellationToken token){Captures++;return CaptureGate==null?Bytes:await CaptureGate.Task;}
            public async Task ApplyAsync(byte[] snapshot,CancellationToken token)
            {ApplyStarted=true;if(ApplyGate!=null)await ApplyGate.Task;if(!IgnoreApplyCancellation)token.ThrowIfCancellationRequested();if(FailApply)throw new InvalidOperationException("fixture apply failure");Bytes=snapshot;Applies++;}
        }
        sealed class Peer:ISyncPeerTransport
        {
            readonly string id;readonly Peer server;readonly Dictionary<string,Peer> clients=new();
            internal readonly List<SyncEnvelope> Sent=new();internal Func<SyncEnvelope,SyncEnvelope> Mutate;internal bool DropRequests,HoldChunks,HoldAccepted;
            internal TaskCompletionSource<bool> SendGate,BroadcastGate,AppliedSendCompletion;
            internal int ActiveReplies;public string Name=>id;public bool IsConnected{get;private set;}=true;public bool IsServer{get;}
            public event Action<SyncEnvelope> OnMessage;public event Action<string,SyncEnvelope> OnPeerMessage;public event Action OnDisconnected;
            internal int PeerSubscribers=>OnPeerMessage?.GetInvocationList().Length??0;
            internal int DisconnectSubscribers=>OnDisconnected?.GetInvocationList().Length??0;
            internal Peer(bool authority,string id,Peer server=null){IsServer=authority;this.id=id;this.server=server;if(server!=null)server.clients.Add(id,this);}
            public ValueTask InitializeAsync(SyncTransportConfig config)=>default;
            public ValueTask ConnectAsync(){IsConnected=true;return default;}
            public ValueTask DisconnectAsync(){IsConnected=false;OnDisconnected?.Invoke();return default;}
            public async ValueTask SendAsync(SyncEnvelope envelope)
            {
                if(BroadcastGate!=null)await BroadcastGate.Task;
                if(!IsServer){await SendToPeerAsync(SyncPeer.Server,envelope);return;}
                Sent.Add(envelope);foreach(var client in clients.Values)client.Deliver(SyncPeer.Server,envelope);
            }
            public ValueTask SendToPeerAsync(string peer,SyncEnvelope envelope)
            {
                if(!IsConnected)throw new InvalidOperationException("Disconnected fixture");
                if(SendGate!=null)return new ValueTask(SendGate.Task);
                if(Mutate!=null)envelope=Mutate(envelope);Sent.Add(envelope);
                if(DropRequests&&envelope.messageType==SyncMessageType.LateJoinRequest)return default;
                if(HoldChunks&&envelope.messageType==SyncMessageType.LateJoinChunk)return default;
                if(HoldAccepted&&envelope.messageType==SyncMessageType.LateJoinAccepted)return default;
                ActiveReplies++;
                try {if(IsServer)clients[peer].Deliver(SyncPeer.Server,envelope);else server.Deliver(id,envelope);}
                finally{ActiveReplies--;}
                return envelope.messageType == SyncMessageType.LateJoinApplied && AppliedSendCompletion != null
                    ? new ValueTask(AppliedSendCompletion.Task) : default;
            }
            internal void Deliver(string sender,SyncEnvelope envelope){OnMessage?.Invoke(envelope);OnPeerMessage?.Invoke(sender,envelope);}
        }
        [Serializable] public sealed class Packet
        {
            public int version;public string requestId;public int maxBytes,chunkBytes,plainBytes,compressedBytes,count,index;
            public SyncSnapshotCompression compression;public string hash,dataBase64,error;
        }
    }
}
#endif
