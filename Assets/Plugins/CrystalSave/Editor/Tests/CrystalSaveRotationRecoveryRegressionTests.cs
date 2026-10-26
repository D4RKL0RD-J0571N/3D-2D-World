#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveRotationRecoveryRegressionTests
    {
        string root;
        SaveSettings settings;
        FieldInfo fault;
        Type ownerType;
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = false;
            settings.enableEncryption = false;
            settings.enableScreenshots = false;
            settings.enableSaveFileVerification = false;
            settings.saveMethod = SaveMethod.BinaryFileFormat;
            settings.saveFileName = "Slot{n}_{meta:generation}";
            settings.metadataFileNamePattern = "Slot{n}_{meta:generation}_Meta.bin";
            var assembly = typeof(SaveManager).Assembly;
            ownerType = assembly.GetType("Arawn.CrystalSave.Runtime.ISaveVerificationOwner");
            fault = assembly.GetType("Arawn.CrystalSave.Runtime.SaveVerificationTransaction")
                .GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
        }

        [TearDown]
        public void TearDown()
        {
            fault.SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(settings);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        static SaveSlot Slot(int number, string generation)
        {
            var slot = new SaveSlot(number, generation, new DateTime(2025, 1, number), null, "Scene");
            slot.CustomMetadata["generation"] = generation;
            return slot;
        }

        object Verification(ISaveSystem storage) => ownerType.GetProperty("Verification").GetValue(storage);
        void Prepare(ISaveSystem storage, SaveSlot slot) => ownerType.GetMethod("PrepareRollback").Invoke(storage, new object[] { slot });
        Task CompleteBatch(ISaveSystem storage) => (Task)Verification(storage).GetType()
            .GetMethod("CompleteBatchAsync", PrivateInstance).Invoke(Verification(storage), new object[] { new[] { 1, 2 } });

        SaveSystem PreparePair(bool verify)
        {
            settings.enableSaveFileVerification = verify;
            var storage = new SaveSystem(settings, root);
            storage.Save(new byte[] { 1 }, Slot(1, "before"));
            storage.Save(new byte[] { 2 }, Slot(2, "before"));
            Prepare(storage, Slot(1, "after"));
            Prepare(storage, Slot(2, "after"));
            Assert.That(Directory.GetFiles(root, "*.csav-verify").Length, Is.EqualTo(2));
            return storage;
        }

        void AssertNoRecoveryRecords()
        {
            Assert.That(Directory.GetFiles(root, "*.csav-verify*"), Is.Empty);
            Assert.That(Directory.GetFiles(root, "*.csav-verified-batch*"), Is.Empty);
            Assert.That(Directory.GetFiles(root, "*.bak*", SearchOption.AllDirectories), Is.Empty);
        }

        [TestCase(false, 1)] [TestCase(true, 1)]
        [TestCase(false, 2)] [TestCase(true, 2)]
        public void UnverifiedRotatingWritesRollbackTogetherOnRestart(bool verify, int completedWrites)
        {
            var storage = PreparePair(verify);
            storage.Save(new byte[] { 3 }, Slot(1, "after"));
            if (completedWrites == 2) storage.Save(new byte[] { 1 }, Slot(2, "after"));
            var restarted = new SaveSystem(settings, root);
            Assert.That(restarted.Load(Slot(1, "after")), Is.EqualTo(new byte[] { 1 }));
            Assert.That(restarted.Load(Slot(2, "after")), Is.EqualTo(new byte[] { 2 }));
            AssertNoRecoveryRecords();
        }

        [TestCase(false)] [TestCase(true)]
        public async Task DurableBatchMarkerKeepsEveryNewGenerationAfterPartialCleanup(bool verify)
        {
            var storage = PreparePair(verify);
            storage.Save(new byte[] { 3 }, Slot(1, "after"));
            storage.Save(new byte[] { 1 }, Slot(2, "after"));
            int cleanups = 0;
            fault.SetValue(null, (Action<string>)(stage =>
            {
                if (stage == "cleanup" && ++cleanups == 2) throw new IOException("Interrupted second batch cleanup.");
            }));
            await CompleteBatch(storage);
            Assert.That(cleanups, Is.EqualTo(2));
            Assert.That(Directory.GetFiles(root, "*.csav-verified-batch").Length, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(root, "*.csav-verify").Length, Is.EqualTo(1));
            fault.SetValue(null, null);
            var restarted = new SaveSystem(settings, root);
            Assert.That(restarted.Load(Slot(1, "after")), Is.EqualTo(new byte[] { 3 }));
            Assert.That(restarted.Load(Slot(2, "after")), Is.EqualTo(new byte[] { 1 }));
            AssertNoRecoveryRecords();
        }

        [TestCase(false)] [TestCase(true)]
        public async Task DeletionWithPendingBatchCleanupCannotResurrectEitherOldGeneration(bool verify)
        {
            var storage = PreparePair(verify);
            storage.Save(new byte[] { 3 }, Slot(1, "after"));
            storage.Save(new byte[] { 1 }, Slot(2, "after"));
            fault.SetValue(null, (Action<string>)(stage =>
            {
                if (stage == "batch-verified") throw new IOException("Interrupted before batch cleanup.");
            }));
            await CompleteBatch(storage);
            fault.SetValue(null, null);
            storage.Delete(Slot(1, "after"));
            var restarted = new SaveSystem(settings, root);
            Assert.That(restarted.Load(Slot(1, "before")), Is.Null);
            Assert.That(restarted.Load(Slot(2, "after")), Is.EqualTo(new byte[] { 1 }));
            AssertNoRecoveryRecords();
        }

        [TestCase(typeof(MySqlSaveSystem), false)] [TestCase(typeof(MySqlSaveSystem), true)]
        [TestCase(typeof(FirebaseSaveSystem), false)] [TestCase(typeof(FirebaseSaveSystem), true)]
        [TestCase(typeof(SupabaseSaveSystem), false)] [TestCase(typeof(SupabaseSaveSystem), true)]
        public void MirrorRollbackPreparationDoesNotDependOnOptionalReadbackVerification(Type backend, bool verify)
        {
            settings.enableSaveFileVerification = verify;
            settings.saveFileName = "Mirror";
            settings.metadataFileNamePattern = "Slot{n}_Meta.bin";
            settings.userFolderStrategy = UserFolderStrategy.Shared;
            settings.mySqlApiUrl = settings.supabaseUrl = "http://127.0.0.1:1";
            var storage = (ISaveSystem)Activator.CreateInstance(backend, settings, root);
            var mirror = backend.GetMethod("WriteLocalMirrorAsync", PrivateInstance);
            void Write(byte value)
            {
                var task = (Task)mirror.Invoke(storage, new object[] { new byte[] { value }, Slot(1, "mirror") });
                Assert.That(task.IsCompleted, Is.True, "Native mirror fixture must not contact a provider.");
                task.GetAwaiter().GetResult();
            }
            Write(1);
            Prepare(storage, Slot(1, "mirror"));
            Assert.That(Directory.GetFiles(root, "*.csav-verify").Length, Is.EqualTo(1));
            Write(2);
            Activator.CreateInstance(backend, settings, root);
            Assert.That(File.ReadAllBytes(Path.Combine(root, "Mirror1.sav")), Is.EqualTo(new byte[] { 1 }));
            AssertNoRecoveryRecords();
        }

        [Test]
        public void PublicBackupRemainsOptionalWhileInternalRotationBackupIsRequired()
        {
            var storage = new SaveSystem(settings, root);
            var slot = Slot(1, "before");
            storage.Save(new byte[] { 1 }, slot);
            storage.BackupSlot(slot);
            Assert.That(Directory.GetFiles(root, "*.csav-verify"), Is.Empty);
            Prepare(storage, slot);
            Assert.That(Directory.GetFiles(root, "*.csav-verify").Length, Is.EqualTo(1));
        }
    }
}
#endif
