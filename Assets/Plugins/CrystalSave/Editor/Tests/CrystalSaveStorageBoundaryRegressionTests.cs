#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveStorageBoundaryRegressionTests
    {
        string root;
        SaveSettings settings;
        static readonly Type Transaction = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction");
        static readonly FieldInfo Fault = Transaction.GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = false;
            settings.enableEncryption = false;
            settings.enableScreenshots = false;
            settings.enableSaveFileVerification = false;
            settings.saveMethod = SaveMethod.BinaryFileFormat;
            settings.saveFileName = "Slot{n}";
        }

        [TearDown] public void TearDown()
        {
            Fault.SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(settings);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        static object Call(string method, params object[] args)
        {
            try { return Transaction.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        static SaveSlot Slot(string name = "old", string header = "old.meta")
        {
            var slot = new SaveSlot(1, name, new DateTime(2025, 1, 1), null, "Scene");
            slot.CustomMetadata["name"] = name;
            slot.CustomMetadata["header"] = header;
            return slot;
        }

        Dictionary<string, byte[]> Files() => Directory.Exists(root)
            ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes)
            : new Dictionary<string, byte[]>();

        void AssertFiles(Dictionary<string, byte[]> expected)
        {
            var current = Files();
            Assert.That(current.Keys, Is.EquivalentTo(expected.Keys));
            foreach (var file in expected) Assert.That(current[file.Key], Is.EqualTo(file.Value), file.Key);
        }

        [TestCase(true, "cleanup-retired")]
        [TestCase(true, "cleanup-file:committed")]
        [TestCase(true, "cleanup-file:prepared")]
        [TestCase(true, "cleanup-file:0.old")]
        [TestCase(true, "cleanup-file:1.old")]
        [TestCase(true, "cleanup-file:0.new")]
        [TestCase(true, "cleanup-file:1.new")]
        [TestCase(true, "cleanup-directory")]
        [TestCase(false, "cleanup-retired")]
        [TestCase(false, "cleanup-file:prepared")]
        [TestCase(false, "cleanup-file:0.old")]
        [TestCase(false, "cleanup-file:1.old")]
        [TestCase(false, "cleanup-file:0.new")]
        [TestCase(false, "cleanup-file:1.new")]
        [TestCase(false, "cleanup-directory")]
        public void StorageBoundary_JournalCleanupRestartKeepsItsDecision(bool committed, string boundary)
        {
            Directory.CreateDirectory(root);
            string payload = Path.Combine(root, "Slot1.sav"), metadata = Path.Combine(root, "Slot1_Meta.bin");
            File.WriteAllBytes(payload, new byte[] { 1 });
            File.WriteAllBytes(metadata, new byte[] { 1 });
            // Retain the legacy journal layout. Updating the cleanup protocol must still
            // safely adopt committed and uncommitted journals written by older builds.
            Fault.SetValue(null, (Action<string>)(stage =>
            {
                if (stage == (committed ? "cleanup" : "payload")) throw new IOException("Retain journal");
            }));
            if (committed) Call("Commit", root, 1, payload, new byte[] { 2 }, metadata, new byte[] { 2 });
            else Assert.Throws<IOException>(() => Call("Commit", root, 1, payload, new byte[] { 2 }, metadata, new byte[] { 2 }));
            bool interrupted = false;
            Fault.SetValue(null, (Action<string>)(stage =>
            {
                if (stage != boundary) return;
                interrupted = true;
                throw new IOException("Interrupt individual journal cleanup operation");
            }));
            Assert.Throws<IOException>(() => Call("RecoverAll", root));
            Assert.That(interrupted, Is.True);
            Assert.That(Directory.Exists(Path.Combine(root, "1.csav-journal")), Is.False,
                "Retired cleanup material must never be eligible for rollback again.");
            Fault.SetValue(null, null);
            Call("RecoverAll", root);
            byte expected = (byte)(committed ? 2 : 1);
            Assert.That(File.ReadAllBytes(payload), Is.EqualTo(new[] { expected }));
            Assert.That(File.ReadAllBytes(metadata), Is.EqualTo(new[] { expected }));
            Assert.That(Directory.GetDirectories(root), Is.Empty);
            Call("RecoverAll", root);
            // A new write can reuse the slot after interrupted retirement cleanup.
            Call("Commit", root, 1, payload, new byte[] { 3 }, metadata, new byte[] { 3 });
            Assert.That(File.ReadAllBytes(payload), Is.EqualTo(new byte[] { 3 }));
        }

        [TestCase(1)] [TestCase(2)]
        public void StorageBoundary_InterruptedRollbackRetainsBothBackups(int entry)
        {
            Directory.CreateDirectory(root);
            string payload = Path.Combine(root, "Slot1.sav"), metadata = Path.Combine(root, "Slot1_Meta.bin");
            File.WriteAllBytes(payload, new byte[] { 1 }); File.WriteAllBytes(metadata, new byte[] { 1 });
            Fault.SetValue(null, (Action<string>)(stage => { if (stage == "metadata") throw new IOException("Before decision"); }));
            Assert.Throws<IOException>(() => Call("Commit", root, 1, payload, new byte[] { 2 }, metadata, new byte[] { 2 }));
            int restored = 0;
            Fault.SetValue(null, (Action<string>)(stage => { if (stage == "rollback-entry" && ++restored == entry) throw new IOException("Partial rollback"); }));
            Assert.Throws<IOException>(() => Call("RecoverAll", root));
            Fault.SetValue(null, null);
            Call("RecoverAll", root);
            Assert.That(File.ReadAllBytes(payload), Is.EqualTo(new byte[] { 1 }));
            Assert.That(File.ReadAllBytes(metadata), Is.EqualTo(new byte[] { 1 }));
        }

        [TestCase("Slot{n}.sav", false)] [TestCase("Slot{n}.sav", true)]
        [TestCase("Slot{n}.sav.bak", false)] [TestCase("Slot{n}.sav.bak", true)]
        [TestCase("Slot{n}.sav.previous", false)] [TestCase("Slot{n}.sav.previous", true)]
        [TestCase("Slot{n}.sav.bak.previous", false)] [TestCase("Slot{n}.sav.bak.previous", true)]
        [TestCase("Slot{n}.tmp", false)] [TestCase("Slot{n}.tmp", true)]
        [TestCase("Slot{n}.tmp.previous", false)] [TestCase("Slot{n}.tmp.previous", true)]
        public async Task StorageBoundary_AliasesAreRejectedBeforeAnyWrite(string pattern, bool verification)
        {
            settings.metadataFileNamePattern = pattern;
            settings.enableSaveFileVerification = verification;
            var storage = new SaveSystem(settings, root);
            var slot = Slot();
            var before = Files();
            if (verification) Assert.Throws<InvalidOperationException>(() => storage.BackupSlot(slot));
            Assert.Throws<InvalidOperationException>(() => storage.Save(new byte[] { 1, 2 }, slot));
            try { await storage.SaveAsync(new byte[] { 1, 2 }, slot); Assert.Fail("Async save accepted a colliding path"); }
            catch (InvalidOperationException exception) { StringAssert.Contains("filename collision", exception.Message); }
            Assert.Throws<InvalidOperationException>(() => storage.SaveSlotMetadata(slot));
            try { await storage.SaveSlotMetadataAsync(slot); Assert.Fail("Metadata mutation accepted a colliding path"); }
            catch (InvalidOperationException exception) { StringAssert.Contains("filename collision", exception.Message); }
            AssertFiles(before);
            Assert.That(Directory.Exists(root), Is.False);
        }

        [TestCase(".bin", false)] [TestCase(".bin", true)]
        [TestCase(".bak", false)] [TestCase(".bak", true)]
        [TestCase(".previous", false)] [TestCase(".previous", true)]
        public async Task StorageBoundary_DistinctMetadataSuffixesRemainSupported(string extension, bool verification)
        {
            settings.metadataFileNamePattern = "Metadata{n}" + extension;
            settings.enableSaveFileVerification = verification;
            var storage = new SaveSystem(settings, root); var slot = Slot();
            storage.Save(new byte[] { 1, 2 }, slot);
            if (verification) storage.BackupSlot(slot);
            await storage.SaveAsync(new byte[] { 3, 4 }, slot);
            if (verification) storage.DeleteBackup(slot);
            Assert.That(storage.Load(slot), Is.EqualTo(new byte[] { 3, 4 }));
            Assert.That((await storage.LoadSlotMetadataAsync(1)).SlotName, Is.EqualTo(slot.SlotName));
            Assert.That(File.Exists(Path.Combine(root, settings.ResolveSlotFolderName(1), "Metadata1" + extension)), Is.True);
        }

        [TestCase("profile")] [TestCase("profile.bak")] [TestCase("profile.previous")]
        public async Task StorageBoundary_MetadataWildcardDoesNotClaimItsOwnRecoveryFiles(string label)
        {
            settings.metadataFileNamePattern = "Meta{n}_{meta:label}";
            settings.enableSaveFileVerification = true;
            var storage = new SaveSystem(settings, root);
            var slot = Slot();
            slot.CustomMetadata["label"] = label;
            storage.Save(new byte[] { 1 }, slot);
            storage.BackupSlot(slot);
            await storage.SaveAsync(new byte[] { 2 }, slot);
            storage.DeleteBackup(slot);
            string metadata = Path.Combine(root, settings.ResolveSlotFolderName(1), "Meta1_" + label);
            Assert.That(storage.Load(slot), Is.EqualTo(new byte[] { 2 }));
            Assert.That((await storage.LoadSlotMetadataAsync(1)).CustomMetadata["label"], Is.EqualTo(label));
            File.Copy(metadata, metadata + ".previous", true);
            File.Delete(metadata);
            var restarted = new SaveSystem(settings, root);
            Assert.That((await restarted.LoadSlotMetadataAsync(1)).CustomMetadata["label"], Is.EqualTo(label));
            restarted.BackupSlot(slot);
            await restarted.SaveAsync(new byte[] { 3 }, slot);
            restarted.DeleteBackup(slot);
            Assert.That(restarted.Load(slot), Is.EqualTo(new byte[] { 3 }));
        }

        [TestCase(false)] [TestCase(true)]
        public void StorageBoundary_RenameCannotClaimPreviousGenerationsBackup(bool verification)
        {
            settings.saveFileName = "Slot{n}_{meta:name}";
            settings.metadataFileNamePattern = "Slot{n}_{meta:header}";
            settings.enableSaveFileVerification = verification;
            var storage = new SaveSystem(settings, root);
            storage.Save(new byte[] { 7, 8 }, Slot("a", "old.meta"));
            var before = Files();
            var renamed = Slot("b", "a.sav.bak");
            if (verification) Assert.Throws<InvalidOperationException>(() => storage.BackupSlot(renamed));
            Assert.Throws<InvalidOperationException>(() => storage.Save(new byte[] { 9 }, renamed));
            AssertFiles(before);
            Assert.That(storage.Load(Slot("a", "old.meta")), Is.EqualTo(new byte[] { 7, 8 }));
        }
    }
}
#endif
