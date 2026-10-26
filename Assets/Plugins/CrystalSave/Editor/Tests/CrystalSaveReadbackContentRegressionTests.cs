#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator Readback_SynchronousSaveRejectsValidChangedContent() => RejectChangedReadback("sync");
        [UnityTest] public IEnumerator Readback_AsyncSaveRejectsValidChangedContent() => RejectChangedReadback("async");
        [UnityTest] public IEnumerator Readback_RotatingSaveRejectsValidChangedContent() => RejectChangedReadback("rotation");

        IEnumerator RejectChangedReadback(string route)
        {
            var manager = SaveManager.Instance;
            var settings = manager.SaveSettings;
            bool originalVerification = settings.enableSaveFileVerification;
            settings.enableSaveFileVerification = true;
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction")
                .GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
            var previousHook = hook.GetValue(null);
            bool ignoreLogs = LogAssert.ignoreFailingMessages;
            int failures = 0, successes = 0, changes = 0;
            EventHandler<OperationFailedEventArgs> failure = (_, _) => failures++;
            EventHandler<SaveLoadEventArgs> success = (_, _) => successes++;
            try
            {
                if (route == "rotation")
                {
                    yield return Await(manager.InitializeQuickSaveSlotsAsync(2));
                    yield return Await(manager.QuickSaveAsync());
                    yield return Await(manager.QuickSaveAsync());
                }
                else yield return Await(manager.SaveImmediateAsync(1));
                string root = manager.SaveSystem.GetSaveGamesPath();
                var previous = Directory.GetFiles(root, "*.sav", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
                var current = manager.CurrentSaveData;
                manager.OnSaveFailed += failure;
                manager.OnSaveCompleted += success;
                LogAssert.ignoreFailingMessages = true;
                hook.SetValue(null, (Action<string>)(stage =>
                {
                    if (stage != "committed") return;
                    foreach (string path in previous.Keys)
                    {
                        // Preserve all generation fields while replacing valid gameplay data.
                        var data = manager.Serializer.Deserialize<SaveData>(File.ReadAllBytes(path));
                        data.ComponentsData["valid-readback-substitution"] = new byte[] { 91 };
                        File.WriteAllBytes(path, manager.Serializer.Serialize(data));
                    }
                    changes++;
                }));
                if (route == "sync") manager.SaveSync(1);
                else
                {
                    var save = route == "rotation" ? manager.QuickSaveAsync() : manager.SaveImmediateAsync(1);
                    yield return Until(() => save.IsCompleted, "changed valid readback", 15);
                    Assert.That(save.IsFaulted, Is.True);
                    _ = save.Exception;
                }
                Assert.That(changes, Is.GreaterThan(0));
                Assert.That(failures, Is.GreaterThan(0));
                Assert.That(successes, Is.Zero);
                Assert.That(manager.CurrentSaveData, Is.SameAs(current));
                foreach (var file in previous) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key);
                Assert.That(Directory.GetFiles(root, "*.csav-verify", SearchOption.AllDirectories), Is.Empty);
                Assert.That(Directory.GetFiles(root, "*.bak", SearchOption.AllDirectories), Is.Empty);
            }
            finally
            {
                manager.OnSaveFailed -= failure;
                manager.OnSaveCompleted -= success;
                hook.SetValue(null, previousHook);
                settings.enableSaveFileVerification = originalVerification;
                LogAssert.ignoreFailingMessages = ignoreLogs;
            }
        }

        [UnityTest] public IEnumerator Readback_LocalImportRejectsBackendInputMutation() => RejectInputMutation(false);
        [UnityTest] public IEnumerator Readback_TemporaryImportRejectsBackendInputMutationBeforeUpload() => RejectInputMutation(true);

        [UnityTest] public IEnumerator Readback_SignedOutCloudImportVerifiesLocalFallback() => SignedOutImportReadback(false);
        [UnityTest] public IEnumerator Readback_SignedOutServerCryptoImportVerifiesLocalFallback() => SignedOutImportReadback(true);

        IEnumerator SignedOutImportReadback(bool serverCrypto)
        {
            var manager = SaveManager.Instance;
            var originalSettings = manager.SaveSettings;
            var originalStorage = manager.SaveSystem;
            var settings = UnityEngine.Object.Instantiate(originalSettings);
            settings.enableCloudSave = settings.enableEncryption = settings.enableCompression = settings.encryptSlotMetadata = false;
            settings.enableSaveFileVerification = true;
            settings.keepLocalMirror = serverCrypto;
            settings.cloudCryptoMode = serverCrypto ? CloudCryptoMode.ServerSide : CloudCryptoMode.ClientSide;
            settings.backend = SaveBackend.UnityCloudSave;
            settings.saveMethod = SaveMethod.BinaryFileFormat;
            settings.saveFileName = "Fallback{n}";
            settings.metadataFileNamePattern = "Header{n}.bin";
            string root = Path.Combine(originalStorage.GetSaveGamesPath(), "signed-out-import");
            var storage = new SaveSystem(settings, root);
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction")
                .GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
            var previousHook = hook.GetValue(null);
            bool ignoreLogs = LogAssert.ignoreFailingMessages;
            try
            {
                Assert.That(typeof(SaveSystem).GetProperty("SignedIn", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.False,
                    "This regression uses the real signed-out fallback, without a live cloud account.");
                Set(manager, "saveSettings", settings);
                Set(manager, "saveSystem", storage);
                var incoming = new SaveData(settings.version) { LastActiveScene = "A" };
                incoming.ComponentsData["fallback-readback"] = new byte[] { 1 };
                settings.enableCloudSave = true;
                var import = manager.ImportSaveAsync(manager.Serializer.Serialize(incoming), 1);
                yield return Await(import);
                Assert.That(import.Result, Is.True, "An uncorrupted signed-out import still succeeds.");
                var previous = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
                Assert.That(previous.Keys.Count(path => path.EndsWith(".sav")), Is.EqualTo(1));
                int corruptions = 0;
                hook.SetValue(null, (Action<string>)(stage =>
                {
                    if (stage != "committed") return;
                    foreach (string path in previous.Keys.Where(path => path.EndsWith(".sav")))
                    {
                        var data = manager.Serializer.Deserialize<SaveData>(File.ReadAllBytes(path));
                        data.ComponentsData["fallback-readback"][0] = 91;
                        File.WriteAllBytes(path, manager.Serializer.Serialize(data));
                        corruptions++;
                    }
                }));
                LogAssert.ignoreFailingMessages = true;
                incoming.ComponentsData["fallback-readback"][0] = 2;
                var rejected = manager.ImportSaveAsync(manager.Serializer.Serialize(incoming), 1);
                yield return Await(rejected);
                Assert.That(corruptions, Is.EqualTo(1));
                Assert.That(rejected.Result, Is.False);
                Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories), Is.EquivalentTo(previous.Keys));
                foreach (var file in previous) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key);
            }
            finally
            {
                hook.SetValue(null, previousHook);
                Set(manager, "saveSettings", originalSettings);
                Set(manager, "saveSystem", originalStorage);
                UnityEngine.Object.Destroy(settings);
                LogAssert.ignoreFailingMessages = ignoreLogs;
            }
        }

        IEnumerator RejectInputMutation(bool temporary)
        {
            var manager = SaveManager.Instance;
            var settings = manager.SaveSettings;
            var original = manager.SaveSystem;
            bool cloud = settings.enableCloudSave, mirror = settings.keepLocalMirror, verification = settings.enableSaveFileVerification;
            var backend = settings.backend;
            bool ignoreLogs = LogAssert.ignoreFailingMessages;
            var storage = new ReadbackMutatingStorage(original, temporary);
            try
            {
                settings.enableSaveFileVerification = true;
                yield return Await(manager.SaveImmediateAsync(1));
                byte[] before = original.Load(manager.GetSlotMetadata(1));
                var incoming = new SaveData(settings.version) { LastActiveScene = "A" };
                incoming.ComponentsData["input-mutation"] = new byte[] { 5 };
                settings.enableCloudSave = temporary;
                settings.keepLocalMirror = !temporary;
                settings.backend = temporary ? SaveBackend.MySQL : backend;
                Set(manager, "saveSystem", storage);
                LogAssert.ignoreFailingMessages = true;
                var import = manager.ImportSaveAsync(manager.Serializer.Serialize(incoming), 1);
                yield return Await(import);
                Assert.That(storage.Mutations, Is.EqualTo(1), "The backend replaces valid bytes through the same input array.");
                Assert.That(import.Result, Is.False);
                Assert.That(storage.Writes, Is.EqualTo(temporary ? 0 : 1), "Temporary readback rejects before any upload.");
                settings.enableCloudSave = cloud;
                settings.keepLocalMirror = mirror;
                Assert.That(original.Load(manager.GetSlotMetadata(1)), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(original.GetSaveGamesPath(), "*.tmp", SearchOption.AllDirectories), Is.Empty);
            }
            finally
            {
                Set(manager, "saveSystem", original);
                settings.enableCloudSave = cloud;
                settings.keepLocalMirror = mirror;
                settings.enableSaveFileVerification = verification;
                settings.backend = backend;
                LogAssert.ignoreFailingMessages = ignoreLogs;
            }
        }
    }

    public sealed class CrystalSaveWritePlanRegressionTests
    {
        const BindingFlags InternalStatic = BindingFlags.Static | BindingFlags.NonPublic;
        static Type PlanType => typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveWritePlan", true);
        static object Ambient => typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveIoScope", true)
            .GetField("Current", InternalStatic).GetValue(null);
        static object Capture(object provider, bool mirror, bool signedIn) => PlanType.GetMethod("Capture", InternalStatic)
            .Invoke(null, new[] { provider, (object)true, mirror, signedIn });

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void OperationRetainsWritePlanWhenAuthenticationChanges(bool signedIn, bool mirror)
        {
            object ambient = Ambient;
            var current = ambient.GetType().GetProperty("Value");
            object previous = current.GetValue(ambient), provider = new object();
            using var first = CreateOperationContext();
            using var next = CreateOperationContext();
            try
            {
                current.SetValue(ambient, first);
                Capture(provider, mirror, signedIn);
                var retained = Capture(provider, mirror, !signedIn);
                Assert.That(Get<bool>(retained, "Remote"), Is.EqualTo(signedIn));
                Assert.That(Get<bool>(retained, "Local"), Is.EqualTo(mirror || !signedIn));
                var guard = PlanType.GetMethod("RequireRemoteAvailable", BindingFlags.Instance | BindingFlags.NonPublic);
                if (signedIn)
                    Assert.That(Assert.Throws<TargetInvocationException>(() => guard.Invoke(retained, new object[] { false })).InnerException,
                        Is.TypeOf<InvalidOperationException>());
                else Assert.DoesNotThrow(() => guard.Invoke(retained, new object[] { false }));

                current.SetValue(ambient, next);
                Assert.That(Get<bool>(Capture(provider, mirror, !signedIn), "Remote"), Is.EqualTo(!signedIn));
                current.SetValue(ambient, null);
                Assert.That(Get<bool>(Capture(provider, mirror, signedIn), "Remote"), Is.EqualTo(signedIn));
                Assert.That(Get<bool>(Capture(provider, mirror, !signedIn), "Remote"), Is.EqualTo(!signedIn),
                    "Separate direct calls capture their current routing policy.");
            }
            finally { current.SetValue(ambient, previous); }
        }

        [Test]
        public void PlannedRemoteSaveRejectsLostAuthenticationBeforeLocalMutation()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = true;
            settings.keepLocalMirror = settings.enableEncryption = false;
            settings.saveMethod = SaveMethod.BinaryFileFormat;
            string root = Path.Combine(Path.GetTempPath(), "CrystalSave-routing-" + Guid.NewGuid().ToString("N"));
            object ambient = Ambient;
            var current = ambient.GetType().GetProperty("Value");
            object previous = current.GetValue(ambient);
            using var context = CreateOperationContext();
            try
            {
                Assert.That(typeof(SaveSystem).GetProperty("SignedIn", InternalStatic).GetValue(null), Is.False);
                var storage = new SaveSystem(settings, root);
                current.SetValue(ambient, context);
                Capture(storage, false, true); // Simulate the earlier authenticated preparation.
                var write = storage.SaveAsync(new byte[] { 1, 2 }, new SaveSlot(1, "Planned remote", default, "", "A"));
                Assert.That(write.IsCompleted, Is.True, "Unavailable planned transport rejects before awaiting work.");
                Assert.That(write.IsFaulted, Is.True);
                Assert.That(write.Exception.GetBaseException(), Is.TypeOf<InvalidOperationException>());
                Assert.That(Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : Array.Empty<string>(), Is.Empty);
            }
            finally
            {
                current.SetValue(ambient, previous);
                UnityEngine.Object.DestroyImmediate(settings);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }

    internal sealed class ReadbackMutatingStorage : ISaveSystem
    {
        readonly ISaveSystem inner;
        readonly bool temporary;
        internal int Mutations, Writes;
        internal ReadbackMutatingStorage(ISaveSystem inner, bool temporary) { this.inner = inner; this.temporary = temporary; }
        void Mutate(byte[] bytes)
        {
            var serializer = SaveDataSerializer.Instance;
            var data = serializer.Deserialize<SaveData>(bytes);
            data.ComponentsData["input-mutation"][0] = 9;
            byte[] replacement = serializer.Serialize(data);
            Assert.That(replacement.Length, Is.EqualTo(bytes.Length));
            Array.Copy(replacement, bytes, bytes.Length);
            Mutations++;
        }
        public Task SaveAsync(byte[] data, SaveSlot slot) { Writes++; if (!temporary) Mutate(data); return inner.SaveAsync(data, slot); }
        public Task WriteTempAsync(byte[] data, SaveSlot slot) { if (temporary) Mutate(data); return inner.WriteTempAsync(data, slot); }
        public Task<byte[]> LoadTempAsync(SaveSlot slot) => inner.LoadTempAsync(slot);
        public void DeleteTemp(SaveSlot slot) => inner.DeleteTemp(slot);
        public Task<byte[]> LoadAsync(SaveSlot slot) => inner.LoadAsync(slot);
        public Task DeleteAsync(SaveSlot slot) => inner.DeleteAsync(slot);
        public Task<SaveSlot> LoadSlotMetadataAsync(int number) => inner.LoadSlotMetadataAsync(number);
        public Task SaveSlotMetadataAsync(SaveSlot slot) => inner.SaveSlotMetadataAsync(slot);
        public void Save(byte[] data, SaveSlot slot) => inner.Save(data, slot);
        public byte[] Load(SaveSlot slot) => inner.Load(slot);
        public void Delete(SaveSlot slot) => inner.Delete(slot);
        public string GetSaveGamesPath() => inner.GetSaveGamesPath();
        public void BackupSlot(SaveSlot slot) => inner.BackupSlot(slot);
        public void RestoreBackup(SaveSlot slot) => inner.RestoreBackup(slot);
        public void DeleteBackup(SaveSlot slot) => inner.DeleteBackup(slot);
    }
}
#endif
