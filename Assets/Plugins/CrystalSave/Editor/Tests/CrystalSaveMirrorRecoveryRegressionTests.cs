#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator MirrorRecovery_FirebaseLocalMetadataCannotPublishAnotherSlot()
        {
            using var server = new CloudRefreshServer();
            var cfg = RestorationCloudSettings(server.Url);
            cfg.keepLocalMirror = true;
            try
            {
                foreach (bool legacy in new[] { false, true })
                foreach (bool recovery in new[] { false, true })
                {
                    var storage = new FirebaseSaveSystem(cfg, CloudRefreshRoot("local-identity-" + legacy + "-" + recovery));
                    Set(storage, "url", server.Url);
                    var expected = new SaveSlot(1, "Correct remote", new DateTime(2025, 1, 1), "", "A");
                    yield return Await(storage.SaveAsync(new byte[] { 1 }, expected));
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    string primary = (string)storage.GetType().GetMethod("MetaLocalPath", flags).Invoke(storage, new object[] { 1 });
                    string path = legacy ? (string)storage.GetType().GetMethod("LegacyMetaLocalPath", flags).Invoke(storage, new object[] { 1 }) : primary;
                    if (legacy) File.Move(primary, path);
                    File.WriteAllBytes(path, SaveDataSerializer.Instance.Serialize(new SaveSlot(11, "Foreign local", expected.LastSaved, "", "A")));
                    if (recovery) { File.Move(path, path + ".previous"); path += ".previous"; }
                    byte[] before = File.ReadAllBytes(path);
                    var read = storage.LoadSlotMetadataAsync(1);
                    yield return Await(read);
                    Assert.That(read.Result.SlotNumber, Is.EqualTo(1));
                    Assert.That(read.Result.SlotName, Is.EqualTo(expected.SlotName));
                    Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), "Reading remote fallback cannot rewrite the local generation.");
                }
            }
            finally { UnityEngine.Object.Destroy(cfg); }
        }
    }

    public sealed class CrystalSaveMirrorRecoveryRegressionTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly Type Verification = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveVerificationTransaction");
        static readonly FieldInfo Fault = Verification.GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
        string root;
        SaveSettings settings;

        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = settings.keepLocalMirror = settings.enableSaveFileVerification = true;
            settings.enableEncryption = settings.enableScreenshots = false;
            settings.cloudCryptoMode = CloudCryptoMode.ClientSide;
            settings.userFolderStrategy = UserFolderStrategy.Shared;
            settings.saveMethod = SaveMethod.BinaryFileFormat;
            settings.saveFileName = "Mirror";
            // This is a literal safe suffix, separate from its own recovery copy.
            settings.metadataFileNamePattern = "Header{n}.previous";
            settings.mySqlApiUrl = settings.supabaseUrl = "http://127.0.0.1:1";
        }

        [TearDown] public void TearDown()
        {
            Fault.SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(settings);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        ISaveSystem Storage(Type backend, string folder) => (ISaveSystem)Activator.CreateInstance(backend, settings, folder);
        static SaveSlot Slot(int number, string name) => new(number, name, new DateTime(2025, 1, 1).AddDays(number), "", "Scene")
            { CustomMetadata = new Dictionary<string, string> { ["generation"] = name } };
        static object Invoke(object owner, string method, params object[] arguments)
        {
            try { return owner.GetType().GetMethod(method, Private).Invoke(owner, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        static void Mirror(ISaveSystem storage, SaveSlot slot, byte value)
        {
            var task = (Task)Invoke(storage, "WriteLocalMirrorAsync", new byte[] { value }, slot);
            Assert.That(task.IsCompleted, Is.True, "Native mirror writes must not contact the provider.");
            task.GetAwaiter().GetResult();
        }
        static Dictionary<string, byte[]> Snapshot(string folder) => Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);
        static void AssertFiles(string folder, Dictionary<string, byte[]> expected)
        {
            Assert.That(Directory.GetFiles(folder, "*", SearchOption.AllDirectories), Is.EquivalentTo(expected.Keys));
            foreach (var file in expected) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key);
        }
        static void MoveRecovery(string path) => File.Move(path, path + ".previous");
        string Prepare(Type backend, string label, bool legacyHeader, out ISaveSystem storage, out string payload, out string metadata)
        {
            string folder = Path.Combine(root, label);
            storage = Storage(backend, folder);
            Mirror(storage, Slot(1, "before"), 1);
            Mirror(storage, Slot(11, "neighbor"), 11);
            payload = Path.Combine(folder, "Mirror1.sav");
            metadata = (string)Invoke(storage, "MetaLocalPath", 1);
            if (legacyHeader)
            {
                string legacy = (string)Invoke(storage, "LegacyMetaLocalPath", 1);
                File.Move(metadata, legacy);
                metadata = legacy;
            }
            MoveRecovery(payload);
            MoveRecovery(metadata);
            return folder;
        }

        [TestCase(typeof(MySqlSaveSystem))] [TestCase(typeof(FirebaseSaveSystem))] [TestCase(typeof(SupabaseSaveSystem))]
        public void MirrorRecovery_RollbackPreservesExactPathsAndNeighbor(Type backend)
        {
            foreach (bool legacy in new[] { false, true })
            {
                string folder = Prepare(backend, "rollback-" + legacy, legacy, out var storage, out _, out _);
                var before = Snapshot(folder);
                storage.BackupSlot(Slot(1, "after"));
                Mirror(storage, Slot(1, "after"), 2);
                storage.RestoreBackup(Slot(1, "after"));
                Storage(backend, folder); // Recovery after restart must remain idempotent.
                AssertFiles(folder, before);
            }
        }

        [TestCase(typeof(MySqlSaveSystem))] [TestCase(typeof(FirebaseSaveSystem))] [TestCase(typeof(SupabaseSaveSystem))]
        public void MirrorRecovery_VerifiedCommitRetiresOriginalRecoveryPaths(Type backend)
        {
            foreach (bool legacy in new[] { false, true })
            {
                string folder = Prepare(backend, "commit-" + legacy, legacy, out var storage, out var payload, out var metadata);
                var neighbors = Snapshot(folder).Where(file => file.Key != payload + ".previous" && file.Key != metadata + ".previous")
                    .ToDictionary(file => file.Key, file => file.Value);
                storage.BackupSlot(Slot(1, "after"));
                Mirror(storage, Slot(1, "after"), 2);
                storage.DeleteBackup(Slot(1, "after"));
                Storage(backend, folder);
                string currentMetadata = (string)Invoke(storage, "MetaLocalPath", 1);
                neighbors[payload] = new byte[] { 2 };
                neighbors[currentMetadata] = File.ReadAllBytes(currentMetadata);
                Assert.That(SaveDataSerializer.Instance.Deserialize<SaveSlot>(neighbors[currentMetadata]).SlotName, Is.EqualTo("after"));
                AssertFiles(folder, neighbors);
            }
        }

        [TestCase(typeof(MySqlSaveSystem))] [TestCase(typeof(FirebaseSaveSystem))] [TestCase(typeof(SupabaseSaveSystem))]
        public void MirrorRecovery_InterruptedRollbackRecoversOnRestart(Type backend)
        {
            foreach (bool legacy in new[] { false, true })
            {
                string folder = Prepare(backend, "interrupted-" + legacy, legacy, out var storage, out _, out _);
                var before = Snapshot(folder);
                storage.BackupSlot(Slot(1, "after"));
                Mirror(storage, Slot(1, "after"), 2);
                Fault.SetValue(null, (Action<string>)(stage => { if (stage == "rollback-entry") throw new IOException("Interrupted mirror rollback"); }));
                Assert.Throws<IOException>(() => storage.RestoreBackup(Slot(1, "after")));
                Fault.SetValue(null, null);
                Storage(backend, folder);
                AssertFiles(folder, before);
            }
        }

        [TestCase(typeof(MySqlSaveSystem))] [TestCase(typeof(FirebaseSaveSystem))] [TestCase(typeof(SupabaseSaveSystem))]
        public void MirrorRecovery_PresenceCandidatesAndMetadataEditsDoNotPromoteCopies(Type backend)
        {
            foreach (bool legacy in new[] { false, true })
            {
                string folder = Prepare(backend, "read-" + legacy, legacy, out var storage, out var payload, out _);
                var before = Snapshot(folder);
                var assembly = typeof(SaveManager).Assembly;
                var presence = assembly.GetType("Arawn.CrystalSave.Runtime.ILocalSavePresence");
                Assert.That(presence.GetMethod("HasLocalSave").Invoke(storage, new object[] { Slot(1, "before") }), Is.True);
                var missing = Slot(111, "absent");
                Assert.That(presence.GetMethod("HasLocalSave").Invoke(storage, new object[] { missing }), Is.False);
                string metadata = (string)Invoke(storage, "MetaLocalPath", 1);
                string legacyMetadata = (string)Invoke(storage, "LegacyMetaLocalPath", 1);
                var candidate = (SaveConflictCandidate)assembly.GetType("Arawn.CrystalSave.Runtime.ConflictCandidateReader")
                    .GetMethod("Local", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { payload, metadata, legacyMetadata, 1 });
                Assert.That(candidate.Payload, Is.EqualTo(new byte[] { 1 }));
                Assert.That(candidate.Metadata.SlotName, Is.EqualTo("before"));
                var updated = (SaveSlot)assembly.GetType("Arawn.CrystalSave.Runtime.SaveSlotMetadataUpdate")
                    .GetMethod("ResolveLocal", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { Slot(1, "rename"), payload, metadata, legacyMetadata });
                Assert.That(updated.SlotName, Is.EqualTo("rename"));
                Assert.That(updated.LastSaved, Is.EqualTo(candidate.Metadata.LastSaved));
                AssertFiles(folder, before);
            }
        }

        [TestCase(typeof(MySqlSaveSystem))] [TestCase(typeof(FirebaseSaveSystem))] [TestCase(typeof(SupabaseSaveSystem))]
        public void MirrorRecovery_ConflictsRejectForeignLocalHeaderBeforeNetwork(Type backend)
        {
            foreach (bool legacy in new[] { false, true })
            {
                string folder = Prepare(backend, "foreign-" + legacy, legacy, out var storage, out _, out var metadata);
                File.WriteAllBytes(metadata + ".previous", SaveDataSerializer.Instance.Serialize(Slot(11, "foreign")));
                var before = Snapshot(folder);
                var request = ((ISaveConflictSource)storage).ReadCandidatesAsync(Slot(1, "requested"), System.Threading.CancellationToken.None);
                Assert.That(request.IsCompleted, Is.True, "Reject the local identity before contacting any provider.");
                Assert.That(request.IsFaulted, Is.True);
                Assert.That(request.Exception.GetBaseException(), Is.TypeOf<InvalidDataException>());
                AssertFiles(folder, before);
            }
        }
    }
}
#endif
