#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveRecoveryGenerationRegressionTests
    {
        static readonly Type Transaction = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveVerificationTransaction");
        static readonly FieldInfo Fault = Transaction.GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
        string root, payload, metadata;

        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            payload = Path.Combine(root, "Slot1.sav");
            metadata = Path.Combine(root, "Meta1_profile.previous");
        }

        [TearDown] public void TearDown()
        {
            Fault.SetValue(null, null);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        object NewTransaction() => Activator.CreateInstance(Transaction, BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { root }, null);

        static object Call(object owner, string method, params object[] args)
        {
            try { return Transaction.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }

        object Prepare()
        {
            File.WriteAllBytes(payload + ".previous", new byte[] { 1 });
            File.WriteAllBytes(metadata + ".previous", new byte[] { 3 });
            var transaction = NewTransaction();
            Call(transaction, "BeginFileGenerations", 1, payload + ".previous", payload,
                metadata + ".previous", metadata, payload, metadata);
            Assert.That(BitConverter.ToInt32(File.ReadAllBytes(Path.Combine(root, "1.csav-verify")), 0), Is.EqualTo(2));
            File.WriteAllBytes(payload, new byte[] { 2 });
            File.WriteAllBytes(metadata, new byte[] { 4 });
            return transaction;
        }

        void AssertOriginal()
        {
            Assert.That(File.Exists(payload), Is.False);
            Assert.That(File.Exists(metadata), Is.False);
            Assert.That(File.ReadAllBytes(payload + ".previous"), Is.EqualTo(new byte[] { 1 }));
            Assert.That(File.ReadAllBytes(metadata + ".previous"), Is.EqualTo(new byte[] { 3 }));
            Assert.That(Directory.GetFiles(root), Is.EquivalentTo(new[] { payload + ".previous", metadata + ".previous" }));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void StorageBoundary_RecoveryOnlyRollbackRetainsExactOriginalPaths(int interruptAfter)
        {
            var transaction = Prepare();
            if (interruptAfter == 0) Call(transaction, "Restore", 1);
            else
            {
                int applied = 0;
                Fault.SetValue(null, (Action<string>)(stage =>
                { if (stage == "rollback-entry" && ++applied == interruptAfter) throw new IOException("Interrupted recovery rollback"); }));
                Assert.Throws<IOException>(() => Call(transaction, "Restore", 1));
                Fault.SetValue(null, null);
                Call(NewTransaction(), "Recover");
            }
            AssertOriginal();
        }

        [Test] public void StorageBoundary_RecoveryOnlyVerifiedCleanupRetainsIncomingPair()
        {
            var transaction = Prepare();
            Call(transaction, "Complete", 1);
            Call(NewTransaction(), "Recover");
            Assert.That(File.ReadAllBytes(payload), Is.EqualTo(new byte[] { 2 }));
            Assert.That(File.ReadAllBytes(metadata), Is.EqualTo(new byte[] { 4 }));
            Assert.That(Directory.GetFiles(root), Is.EquivalentTo(new[] { payload, metadata }));
        }

        [Test] public void StorageBoundary_RecoveryOnlyMissingBackupLeavesPairUntouched()
        {
            var transaction = Prepare();
            File.Delete(metadata + ".bak");
            var before = Directory.GetFiles(root).ToDictionary(path => path, File.ReadAllBytes);
            Assert.Throws<FileNotFoundException>(() => Call(transaction, "Restore", 1));
            Assert.That(Directory.GetFiles(root), Is.EquivalentTo(before.Keys));
            foreach (var file in before) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value));
            File.WriteAllBytes(metadata + ".bak", new byte[] { 3 });
            Call(NewTransaction(), "Recover");
            AssertOriginal();
        }

        [Test] public void StorageBoundary_RecoveryProvenanceDoesNotPermitLiteralSuffixCollision()
        {
            File.WriteAllBytes(payload + ".previous", new byte[] { 1 });
            Assert.Throws<InvalidOperationException>(() => Call(NewTransaction(), "BeginFileGenerations", 1,
                payload + ".previous", payload, null, null, payload, payload + ".previous"));
            Assert.That(Directory.GetFiles(root), Is.EquivalentTo(new[] { payload + ".previous" }));
        }

        [Test] public void StorageBoundary_V1VerificationRecordRemainsReadable()
        {
            File.WriteAllBytes(payload, new byte[] { 1 });
            File.WriteAllBytes(metadata, new byte[] { 3 });
            var transaction = NewTransaction();
            Call(transaction, "BeginFiles", 1, payload, metadata, payload, metadata);
            Assert.That(BitConverter.ToInt32(File.ReadAllBytes(Path.Combine(root, "1.csav-verify")), 0), Is.EqualTo(1));
            File.WriteAllBytes(payload, new byte[] { 2 });
            File.WriteAllBytes(metadata, new byte[] { 4 });
            Call(NewTransaction(), "Recover");
            Assert.That(File.ReadAllBytes(payload), Is.EqualTo(new byte[] { 1 }));
            Assert.That(File.ReadAllBytes(metadata), Is.EqualTo(new byte[] { 3 }));
            Assert.That(Directory.GetFiles(root), Is.EquivalentTo(new[] { payload, metadata }));
        }

        static void AtomicWrite(string path, byte[] bytes)
            => typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.AtomicSaveFile")
                .GetMethod("WriteAllBytes", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { path, bytes, null });

        Dictionary<string, byte[]> Snapshot() => Directory.GetFiles(root).ToDictionary(path => path, File.ReadAllBytes);
        void AssertSnapshot(Dictionary<string, byte[]> expected)
        {
            Assert.That(Directory.GetFiles(root), Is.EquivalentTo(expected.Keys));
            foreach (var file in expected) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key);
        }

        object PrepareSiblings(bool rename, out Dictionary<string, byte[]> before, out string incomingPayload, out string incomingMetadata)
        {
            File.WriteAllBytes(payload, new byte[] { 0 });
            File.WriteAllBytes(metadata, new byte[] { 2 });
            AtomicWrite(payload, new byte[] { 1 });
            AtomicWrite(metadata, new byte[] { 3 });
            File.WriteAllBytes(Path.Combine(root, "Slot11.sav.previous"), new byte[] { 11 });
            before = Snapshot();
            incomingPayload = rename ? Path.Combine(root, "Slot1_renamed.sav") : payload;
            incomingMetadata = rename ? Path.Combine(root, "Meta1_renamed.previous") : metadata;
            var transaction = NewTransaction();
            Call(transaction, "BeginFiles", 1, payload, metadata, incomingPayload, incomingMetadata);
            Assert.That(BitConverter.ToInt32(File.ReadAllBytes(Path.Combine(root, "1.csav-verify")), 0), Is.EqualTo(3));
            AtomicWrite(incomingPayload, new byte[] { 5 });
            AtomicWrite(incomingMetadata, new byte[] { 6 });
            return transaction;
        }

        [TestCase(false, 0)] [TestCase(false, 1)] [TestCase(false, 2)] [TestCase(false, 3)] [TestCase(false, 4)]
        [TestCase(true, 0)] [TestCase(true, 1)] [TestCase(true, 2)] [TestCase(true, 3)] [TestCase(true, 4)] [TestCase(true, 5)] [TestCase(true, 6)]
        public void StorageBoundary_RecoverySiblingsSurviveRollbackAndRestart(bool rename, int interruptAfter)
        {
            var transaction = PrepareSiblings(rename, out var before, out _, out _);
            if (interruptAfter == 0) Call(transaction, "Restore", 1);
            else
            {
                int applied = 0;
                Fault.SetValue(null, (Action<string>)(stage =>
                { if (stage == "rollback-entry" && ++applied == interruptAfter) throw new IOException("Interrupted sibling rollback"); }));
                Assert.Throws<IOException>(() => Call(transaction, "Restore", 1));
                Fault.SetValue(null, null);
            }
            Call(NewTransaction(), "Recover");
            AssertSnapshot(before);
        }

        [TestCase(false, 0)] [TestCase(false, 1)] [TestCase(false, 2)] [TestCase(false, 3)] [TestCase(false, 4)]
        [TestCase(true, 0)] [TestCase(true, 1)] [TestCase(true, 2)] [TestCase(true, 3)] [TestCase(true, 4)] [TestCase(true, 5)] [TestCase(true, 6)]
        public void StorageBoundary_VerifiedSiblingsRetainCurrentHistoryThroughCleanupRestart(bool rename, int interruptAfter)
        {
            var transaction = PrepareSiblings(rename, out var before, out var incomingPayload, out var incomingMetadata);
            int cleaned = 0;
            if (interruptAfter != 0)
                Fault.SetValue(null, (Action<string>)(stage =>
                { if (stage == "cleanup-entry" && ++cleaned == interruptAfter) throw new IOException("Interrupted sibling cleanup"); }));
            Call(transaction, "Complete", 1);
            if (interruptAfter != 0) Assert.That(cleaned, Is.EqualTo(interruptAfter));
            Fault.SetValue(null, null);
            Call(NewTransaction(), "Recover");
            var expected = before.Where(file => file.Key == Path.Combine(root, "Slot11.sav.previous"))
                .ToDictionary(file => file.Key, file => file.Value);
            expected[incomingPayload] = new byte[] { 5 };
            expected[incomingMetadata] = new byte[] { 6 };
            if (!rename)
            {
                expected[payload + ".previous"] = new byte[] { 1 };
                expected[metadata + ".previous"] = new byte[] { 3 };
            }
            AssertSnapshot(expected);
        }

        [TestCase(false)] [TestCase(true)]
        public void StorageBoundary_MissingOrCorruptSiblingBackupCannotPartiallyRestore(bool corrupt)
        {
            var transaction = PrepareSiblings(false, out var original, out _, out _);
            string backup = metadata + ".previous.bak";
            byte[] bytes = File.ReadAllBytes(backup);
            if (corrupt) File.WriteAllBytes(backup, new byte[] { 99 }); else File.Delete(backup);
            var before = Snapshot();
            Assert.Catch<IOException>(() => Call(transaction, "Restore", 1));
            AssertSnapshot(before);
            File.WriteAllBytes(backup, bytes);
            Call(NewTransaction(), "Recover");
            AssertSnapshot(original);
        }

        [TestCase(".previous.bak")] [TestCase(".previous.bak.previous")]
        public void StorageBoundary_SiblingBackupCollisionsRejectBeforeMutation(string suffix)
        {
            File.WriteAllBytes(payload, new byte[] { 1 });
            File.WriteAllBytes(payload + ".previous", new byte[] { 0 });
            var before = Snapshot();
            Assert.Throws<InvalidOperationException>(() => Call(NewTransaction(), "BeginFiles", 1,
                payload, null, payload, payload + suffix));
            AssertSnapshot(before);
        }

        [Test]
        public void StorageBoundary_V2RecordWithSafePreviousBackupFilenameStillRecovers()
        {
            // Frozen v2 layout: a recovery-only payload and a legal metadata name
            // which becomes reserved only when v3 needs a separate sibling backup.
            metadata = payload + ".previous.bak";
            File.WriteAllBytes(payload + ".previous", new byte[] { 1 });
            File.WriteAllBytes(metadata, new byte[] { 3 });
            var before = Snapshot();
            File.WriteAllBytes(payload + ".bak", new byte[] { 1 });
            File.WriteAllBytes(metadata + ".bak", new byte[] { 3 });
            using (var stream = File.Create(Path.Combine(root, "1.csav-verify")))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            using (var hash = SHA256.Create())
            {
                writer.Write(2); writer.Write(false); writer.Write(3);
                void Entry(string path, bool existed, bool obsolete, string primary, byte[] bytes)
                {
                    writer.Write(path); writer.Write(false); writer.Write(existed); writer.Write(obsolete); writer.Write(primary);
                    byte[] digest = existed ? hash.ComputeHash(bytes) : Array.Empty<byte>();
                    writer.Write(digest.Length); writer.Write(digest);
                }
                Entry(payload + ".previous", true, true, payload, new byte[] { 1 });
                Entry(metadata, true, false, "", new byte[] { 3 });
                Entry(payload, false, false, "", Array.Empty<byte>());
            }
            File.WriteAllBytes(payload, new byte[] { 5 });
            File.WriteAllBytes(metadata, new byte[] { 6 });
            Call(NewTransaction(), "Recover");
            AssertSnapshot(before);
        }
    }
}
#endif
