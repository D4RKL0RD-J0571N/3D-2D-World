#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveVerificationRecoveryTests
    {
        string root;
        SaveSettings settings;
        FieldInfo fault;
        [SetUp] public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = false; settings.enableEncryption = false;
            settings.saveMethod = SaveMethod.BinaryFileFormat; settings.enableSaveFileVerification = true;
            settings.saveFileName = "Slot{n}_{meta:profile}"; settings.metadataFileNamePattern = "Slot{n}_{meta:profile}_Meta.bin";
            fault = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveVerificationTransaction").GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
        }
        [TearDown] public void Teardown()
        {
            fault.SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(settings);
            Directory.Delete(root, true);
        }
        SaveSlot Slot(string profile)
        {
            var slot = new SaveSlot(1, profile, new DateTime(2025,1,1), null, "Scene");
            slot.CustomMetadata["profile"] = profile; return slot;
        }
        [TestCase(typeof(MySqlSaveSystem))] [TestCase(typeof(FirebaseSaveSystem))] [TestCase(typeof(SupabaseSaveSystem))]
        public void CloudMirrorRecoveryUsesLocalBackupsWithoutContactingProvider(Type backend)
        {
            settings.enableCloudSave = true; settings.keepLocalMirror = true;
            settings.userFolderStrategy = UserFolderStrategy.Shared;
            settings.saveFileName = "Mirror"; settings.metadataFileNamePattern = "Slot{n}_Meta.bin";
            settings.mySqlApiUrl = settings.supabaseUrl = "http://127.0.0.1:1";
            var storage = (ISaveSystem)Activator.CreateInstance(backend, settings, root);
            var write = backend.GetMethod("WriteLocalMirrorAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            void Mirror(byte[] data, SaveSlot slot)
            {
                var task = (System.Threading.Tasks.Task)write.Invoke(storage, new object[] {data, slot});
                Assert.That(task.IsCompleted, Is.True, "Native mirror commit must complete locally, without networking.");
                task.GetAwaiter().GetResult();
            }
            Mirror(new byte[] {1,2}, Slot("before"));
            storage.BackupSlot(Slot("after")); Mirror(new byte[] {9}, Slot("after"));
            Activator.CreateInstance(backend, settings, root);
            Assert.That(File.ReadAllBytes(Path.Combine(root, "Mirror1.sav")), Is.EqualTo(new byte[] {1,2}));
            var metadata = Directory.GetFiles(root, "Slot1_Meta.bin", SearchOption.AllDirectories).Single();
            Assert.That(SaveDataSerializer.Instance.Deserialize<SaveSlot>(File.ReadAllBytes(metadata)).SlotName, Is.EqualTo("before"));
            Assert.That(Directory.GetFiles(root, "*.bak*", SearchOption.AllDirectories), Is.Empty);
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
        public void UnverifiedGenerationRollsBackOnRestart(bool rename, bool legacyRoot)
        {
            var storage = new SaveSystem(settings, root); var old = Slot("before");
            storage.Save(new byte[] {1,2,3}, old);
            if (legacyRoot)
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(f => f.EndsWith(".sav") || f.EndsWith(".bin")))
                    File.Move(file, Path.Combine(root, Path.GetFileName(file)));
            var incoming = Slot(rename ? "after" : "before");
            storage.BackupSlot(incoming); storage.Save(new byte[] {9}, incoming);
            var restarted = new SaveSystem(settings, root);
            Assert.That(restarted.Load(incoming), Is.EqualTo(new byte[] {1,2,3}));
            Assert.That(SaveDataSerializer.Instance.Deserialize<SaveSlot>(File.ReadAllBytes(Directory.GetFiles(root, "*.bin", SearchOption.AllDirectories).Single())).SlotName, Is.EqualTo("before"));
            Assert.That(Directory.GetFiles(root, "*.csav-verify"), Is.Empty);
            Assert.That(Directory.GetFiles(root, "*.bak*", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.GetFiles(root, "*.sav", SearchOption.AllDirectories).Length, Is.EqualTo(1));
            restarted.Delete(incoming);
            Assert.That(new SaveSystem(settings, root).Load(old), Is.Null, "Deletion must not resurrect rollback copies.");
        }
        [TestCase("prepared")] [TestCase("rollback")] [TestCase("rollback-entry")]
        public void InterruptedRollbackCanBeRetriedAfterRestart(string boundary)
        {
            var storage = new SaveSystem(settings, root); var old = Slot("before"); var incoming = Slot("after");
            storage.Save(new byte[] {1,2,3}, old);
            bool injected = false;
            fault.SetValue(null, (Action<string>)(stage => { if (stage == boundary && !injected) { injected = true; throw new IOException("Injected interruption"); } }));
            try { storage.BackupSlot(incoming); storage.Save(new byte[] {9}, incoming); storage.RestoreBackup(incoming); }
            catch (IOException) { }
            Assert.That(injected, Is.True);
            fault.SetValue(null, null);
            Assert.That(new SaveSystem(settings, root).Load(incoming), Is.EqualTo(new byte[] {1,2,3}));
        }
        [TestCase("verified")] [TestCase("cleanup")]
        public void VerifiedGenerationSurvivesInterruptedCleanup(string boundary)
        {
            var storage = new SaveSystem(settings, root); var incoming = Slot("after");
            storage.Save(new byte[] {1}, Slot("before")); storage.BackupSlot(incoming); storage.Save(new byte[] {2}, incoming);
            fault.SetValue(null, (Action<string>)(stage => { if (stage == boundary) throw new IOException("Injected cleanup interruption"); }));
            storage.DeleteBackup(incoming);
            fault.SetValue(null, null);
            Assert.That(new SaveSystem(settings, root).Load(incoming), Is.EqualTo(new byte[] {2}));
            Assert.That(Directory.GetFiles(root, "*.sav", SearchOption.AllDirectories).Length, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(root, "*.bak*", SearchOption.AllDirectories), Is.Empty);
        }
        [Test] public void FailedFirstSaveRestoresEmptySlot()
        {
            var storage = new SaveSystem(settings, root); var slot = Slot("first");
            storage.BackupSlot(slot); storage.Save(new byte[] {9}, slot);
            Assert.That(new SaveSystem(settings, root).Load(slot), Is.Null);
            Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories), Is.Empty);
        }
        [TestCase(false)] [TestCase(true)] public void MissingOrCorruptBackupReportsFailureAndRetainsRecoveryRecord(bool corrupt)
        {
            var storage = new SaveSystem(settings, root); var incoming = Slot("after");
            storage.Save(new byte[] {1}, Slot("before")); storage.BackupSlot(incoming); storage.Save(new byte[] {9}, incoming);
            var backup = Directory.GetFiles(root, "*.sav.bak", SearchOption.AllDirectories).Single();
            if (corrupt) File.WriteAllBytes(backup, new byte[] {7}); else File.Delete(backup);
            Assert.Catch<IOException>(() => storage.RestoreBackup(incoming));
            Assert.That(Directory.GetFiles(root, "*.csav-verify").Length, Is.EqualTo(1));
            Assert.Catch<IOException>(() => new SaveSystem(settings, root));
        }
    }
}
#endif
