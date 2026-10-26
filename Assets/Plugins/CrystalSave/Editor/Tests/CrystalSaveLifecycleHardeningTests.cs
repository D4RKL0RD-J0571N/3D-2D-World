#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;
namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {

        [UnityTest]
        public IEnumerator LegacyLoadCoroutineUsesCoordinatorAndReportsItsOwnCompletion()
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveAsync(1));
            int completed = 0;
            EventHandler<SaveLoadEventArgs> listener = (sender, args) => completed++;
            manager.OnLoadCompleted += listener;
            try
            {
                yield return manager.StartLoadProcess(manager.GetSlotMetadata(1), false, false, true, CancellationToken.None);
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(manager.CurrentSaveData, Is.Not.Null);
                yield return Await(manager.SaveAsync(2));
            }
            finally { manager.OnLoadCompleted -= listener; }
        }
        [UnityTest]
        public IEnumerator DestroyedLoadHostReleasesPhysicsAndPendingActivation()
        {
            var manager = SaveManager.Instance; yield return Await(manager.SaveAsync(1, "A"));
            var physics3D = Physics.simulationMode; var physics2D = Physics2D.simulationMode;
            var sceneLoader = typeof(SaveManager).GetField("sceneLoadManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(manager);
            bool requested = false; Action<string> activation = scene => requested = true;
            manager.OnSceneActivationRequested += activation;
            var load = manager.LoadSaveSlotAsync(1, true, true, false);
            yield return Until(() => requested, "pending activation");
            LogAssert.Expect(LogType.Error, new Regex("HandleLoadCompletion: Load operation for slot 1 failed"));
            Object.Destroy(manager.gameObject);
            yield return Await(load);
            Assert.That(load.Result.Success, Is.False);
            Assert.That(Physics.simulationMode, Is.EqualTo(physics3D));
            Assert.That(Physics2D.simulationMode, Is.EqualTo(physics2D));
            Assert.That(sceneLoader.GetType().GetField("pendingSceneOperation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(sceneLoader), Is.Null);
            yield return Until(() => SceneManager.GetActiveScene().name == "A", "released destination activation");
        }
        [UnityTest]
        public IEnumerator QueuedImportAndDeleteCancellationCannotChangeOtherSlots()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var storage = new HardeningStorage(original) { GateWrites = true };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL; Set(manager, "saveSystem", storage);
            using var cancellation = new CancellationTokenSource();
            try
            {
                var first = manager.SaveAsync(1);
                yield return Until(() => storage.Writes.Count == 1, "save commit");
                var import = manager.ImportSaveAsync(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)), 2, null, cancellation.Token);
                var delete = manager.DeleteAsync(1, cancellation.Token);
                cancellation.Cancel();
                yield return Await(import); yield return Until(() => delete.IsCompleted, "delete cancellation");
                Assert.That(import.Result, Is.False); Assert.That(delete.IsCanceled, Is.True);
                storage.Writes[0].TrySetResult(true); yield return Await(first);
                Assert.That(original.Load(manager.GetSlotMetadata(1)), Is.Not.Null);
                Assert.That(original.Load(manager.GetSlotMetadata(2)), Is.Null);
            }
            finally { storage.Release(); manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); }
        }
        [UnityTest]
        public IEnumerator RepeatedDeferredPrefabRestoresDrainQueuesAndKeepFilesBounded()
        {
            yield return LoadScene("A");
            var manager = SaveManager.Instance;
            int baselineComponents = manager.SaveableComponents.Count();
            var prefab = Spawn(); prefab.DeferLowPriorityUntilRequested = true;
            string id = prefab.UniqueID; yield return null;
            yield return Await(manager.SaveAsync(1));
            string root = SessionState.GetString(SessionKey + ".root", "");
            int files = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length;
            long warm = 0;
            for (int cycle = 0; cycle < 12; cycle++)
            {
                Object.Destroy(prefab.gameObject); yield return null;
                var load = manager.LoadSaveSlotAsync(1); yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                yield return manager.GetPrefabManager.ProcessDeferredPrefabByUniqueID(id);
                prefab = manager.FindGameObjectByUniqueID(id).GetComponent<SaveablePrefab>();
                Assert.That(manager.GetPrefabManager.HasDeferredPrefabs, Is.False);
                Assert.That(manager.GetPendingPrefabCount(), Is.Zero);
                Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length, Is.EqualTo(files));
                if (cycle == 2) { GC.Collect(); warm = GC.GetTotalMemory(false); }
            }
            Object.Destroy(prefab.gameObject); yield return null; yield return null;
            Assert.That(manager.SaveableComponents.Count(), Is.EqualTo(baselineComponents));
            GC.Collect(); TestContext.WriteLine($"Deferred cycles managed bytes: warm={warm}, final={GC.GetTotalMemory(false)}");
        }
        [UnityTest]
        public IEnumerator EncryptedScreenshotRoundTripsAndCancelledCaptureLeavesNoFile()
        {
            var manager = SaveManager.Instance;
            var secret = ScriptableObject.CreateInstance<TestSecret>();
            var settings = manager.SaveSettings; settings.enableEncryption = true; settings.encryptScreenshots = true; settings.screenshotFormat = ScreenshotFormat.PNG; settings.masterSecretProvider = secret;
            var encryption = (EncryptionService)typeof(SaveManager).GetProperty("EncryptionService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(manager);
            yield return Await(encryption.InitializeAsync("isolated-user"));
            string root = manager.SaveSystem.GetSaveGamesPath();
            var screenshots = new ScreenshotManager(settings, root) { CustomCapture = () => new Texture2D(16, 9) };
            settings.enableScreenshots = true;
            try
            {
                string name = screenshots.CaptureScreenshotSync(1);
                var bytes = File.ReadAllBytes(Path.Combine(root, settings.screenshotFolderName, name));
                Assert.That(SaveCrypto.HasEnvelope(bytes), Is.True);
                Assert.That(encryption.MaybeDecrypt(bytes).Take(4), Is.EqualTo(new byte[] {137,80,78,71}));
                Set(manager, "screenshotManager", screenshots);
                // The existing service owns its original screenshot manager; recreate it using the supported count API.
                yield return Await(manager.SetSaveSlotCountAsync(3));
                using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
                int before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length;
                var cancelled = manager.SaveAsync(2, null, cancellation.Token);
                Assert.That(cancelled.IsCanceled, Is.True); yield return null;
                Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length, Is.EqualTo(before));
                var pending = new TaskCompletionSource<Texture2D>(); bool captureStarted = false;
                screenshots.CustomCapture = null;
                screenshots.CustomCaptureAsync = () => { captureStarted = true; return pending.Task; };
                using var duringCapture = new CancellationTokenSource();
                var capturingSave = manager.SaveAsync(2, null, duringCapture.Token);
                yield return Until(() => captureStarted, "custom capture started");
                duringCapture.Cancel();
                yield return Until(() => capturingSave.IsCompleted, "capture cancellation");
                Assert.That(capturingSave.IsCanceled, Is.True);
                var lateTexture = new Texture2D(16, 9);
                pending.SetResult(lateTexture);
                yield return Until(() => !lateTexture, "late capture texture released");
                Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length, Is.EqualTo(before));
            }
            finally { settings.enableScreenshots = false; settings.enableEncryption = false; settings.masterSecretProvider = null; Object.Destroy(secret); }
        }
        [UnityTest]
        public IEnumerator MetadataLoadDeadlineIncludesTimeSpentInQueue()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var storage = new HardeningStorage(original) { GateWrites = true };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL; Set(manager, "saveSystem", storage);
            try
            {
                var save = manager.SaveAsync(1);
                yield return Until(() => storage.Writes.Count == 1, "blocking save");
                var previous = manager.CurrentSaveData;
                var load = manager.LoadByMetadataAsync("world", "queued", timeout: TimeSpan.FromMilliseconds(80));
                yield return Await(load);
                Assert.That(load.Result.Success, Is.False); Assert.That(save.IsCompleted, Is.False);
                Assert.That(manager.CurrentSaveData, Is.SameAs(previous));
                storage.Writes[0].TrySetResult(true); yield return Await(save);
            }
            finally { storage.Release(); manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); }
        }
        [UnityTest]
        public IEnumerator CancellationDuringInitializationDoesNotWriteAndReleasesQueue()
        {
            var manager = SaveManager.Instance;
            var initialized = typeof(SaveManager).GetProperty("IsInitialized");
            using var cancellation = new CancellationTokenSource();
            initialized.SetValue(null, false);
            try
            {
                var task = manager.SaveAsync(1, null, "startup cancelled", cancellation.Token);
                yield return null; yield return null; cancellation.Cancel();
                yield return Until(() => task.IsCompleted, "startup cancellation");
                Assert.That(task.IsCanceled, Is.True);
                Assert.That(manager.SaveSystem.Load(manager.GetSlotMetadata(1)), Is.Null);
            }
            finally { initialized.SetValue(null, true); }
            yield return Await(manager.SaveAsync(1));
        }
        [UnityTest]
        public IEnumerator SynchronousBusyCallReturnsAndMetadataWaitsBehindSave()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var storage = new HardeningStorage(original) { GateWrites = true };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL; Set(manager, "saveSystem", storage);
            try
            {
                var save = manager.SaveAsync(1, null, "before rename");
                yield return Until(() => storage.Writes.Count == 1, "save owns coordinator");
                bool ignored = LogAssert.ignoreFailingMessages; LogAssert.ignoreFailingMessages = true;
                try { manager.SaveSync(2); Assert.That(manager.SnapshotCurrentData(), Is.False); } finally { LogAssert.ignoreFailingMessages = ignored; }
                var rename = manager.RenameSaveSlotAsync(1, "after rename");
                yield return null; yield return null;
                Assert.That(rename.IsCompleted, Is.False);
                Assert.That(storage.Writes.Count, Is.EqualTo(1));
                storage.Writes[0].TrySetResult(true); yield return Await(save); yield return Await(rename);
                Assert.That(manager.GetSlotMetadata(1).SlotName, Is.EqualTo("after rename"));
            }
            finally { storage.Release(); manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); }
        }
        [UnityTest]
        public IEnumerator VerificationRestoreRecoversPayloadAndMetadataTogether()
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveAsync(1, null, "old generation"));
            var oldSlot = manager.GetSlotMetadata(1); var oldBytes = manager.SaveSystem.Load(oldSlot);
            manager.SaveSettings.enableSaveFileVerification = true;
            try
            {
                manager.SaveSystem.BackupSlot(oldSlot);
                var replacement = new SaveSlot(1, "new generation", DateTime.Now, null, null);
                manager.SaveSystem.Save(new byte[] { 9 }, replacement);
                manager.SaveSystem.RestoreBackup(replacement);
                Assert.That(manager.SaveSystem.Load(replacement), Is.EqualTo(oldBytes));
                var metadata = manager.SaveSystem.LoadSlotMetadataAsync(1); yield return Await(metadata);
                Assert.That(metadata.Result.SlotName, Is.EqualTo("old generation"));
                manager.SaveSystem.DeleteBackup(replacement);
                Assert.That(Directory.GetFiles(SessionState.GetString(SessionKey + ".root", ""), "*.bak*", SearchOption.AllDirectories), Is.Empty);
            }
            finally { manager.SaveSettings.enableSaveFileVerification = false; }
        }
        [UnityTest]
        public IEnumerator PreCancelledSaveDoesNotWriteOrRename()
        {
            var manager = SaveManager.Instance; string name = manager.GetSlotMetadata(1).SlotName;
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            var task = manager.SaveAsync(1, null, "Cancelled name", cancellation.Token);
            Assert.That(task.IsCanceled, Is.True);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(manager.GetSlotMetadata(1).SlotName, Is.EqualTo(name));
            Assert.That(manager.SaveSystem.Load(manager.GetSlotMetadata(1)), Is.Null);
        }
        [UnityTest]
        public IEnumerator SavesOnSameAndDifferentSlotsFinishInFifoOrderWithOwnResults()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var storage = new HardeningStorage(original) { GateWrites = true };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL;
            Set(manager, "saveSystem", storage);
            try
            {
                foreach (int secondSlot in new[] { 1, 2 })
                {
                    int before = storage.Writes.Count;
                    var first = manager.SaveAsync(1, null, "first");
                    yield return Until(() => storage.Writes.Count == before + 1, "first commit begins");
                    var second = manager.SaveAsync(secondSlot, null, "second");
                    for (int i = 0; i < 5; i++) yield return null;
                    Assert.That(storage.Writes.Count, Is.EqualTo(before + 1));
                    storage.Writes[before].TrySetResult(true);
                    yield return Until(() => first.IsCompleted && storage.Writes.Count == before + 2, "next queued operation starts");
                    Assert.That(first.IsCompletedSuccessfully, Is.True);
                    Assert.That(second.IsCompleted, Is.False);
                    storage.Writes[before + 1].TrySetResult(true);
                    yield return Await(second);
                    Assert.That(manager.GetSlotMetadata(secondSlot).SlotName, Is.EqualTo("second"));
                }
            }
            finally { storage.Release(); manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); }
        }
        [UnityTest]
        public IEnumerator CancellationWhileQueuedSkipsRequestAndCancellationAfterCommitReportsSuccess()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var storage = new HardeningStorage(original) { GateWrites = true };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL; Set(manager, "saveSystem", storage);
            using var firstCancellation = new CancellationTokenSource(); using var queuedCancellation = new CancellationTokenSource();
            try
            {
                var first = manager.SaveAsync(1, null, firstCancellation.Token);
                yield return Until(() => storage.Writes.Count == 1, "commit gate");
                var queued = manager.SaveAsync(2, null, "must not publish", queuedCancellation.Token);
                queuedCancellation.Cancel(); firstCancellation.Cancel();
                yield return Until(() => queued.IsCompleted, "queued cancellation");
                Assert.That(queued.IsCanceled, Is.True); Assert.That(first.IsCompleted, Is.False);
                storage.Writes[0].TrySetResult(true); yield return Await(first);
                Assert.That(first.IsCompletedSuccessfully, Is.True);
                Assert.That(storage.Writes.Count, Is.EqualTo(1));
                Assert.That(manager.GetSlotMetadata(2).SlotName, Is.Not.EqualTo("must not publish"));
            }
            finally { storage.Release(); manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); }
        }
        [UnityTest]
        public IEnumerator ImmediateFailureFaultsTaskAndEventExceptionCannotStrandQueue()
        {
            var manager = SaveManager.Instance; yield return Await(manager.SaveAsync(1));
            string root = SessionState.GetString(SessionKey + ".root", "");
            string path = Directory.GetFiles(root, "*.sav", SearchOption.AllDirectories).Single();
            Directory.CreateDirectory(path + ".bak"); manager.SaveSettings.enableSaveFileVerification = true;
            bool ignore = LogAssert.ignoreFailingMessages; LogAssert.ignoreFailingMessages = true;
            try
            {
                var failed = manager.SaveImmediateAsync(1); yield return Until(() => failed.IsCompleted, "failed immediate save");
                Assert.That(failed.IsFaulted, Is.True); _ = failed.Exception;
            }
            finally { LogAssert.ignoreFailingMessages = ignore; Directory.Delete(path + ".bak"); manager.SaveSettings.enableSaveFileVerification = false; }
            int received = 0;
            EventHandler<SaveLoadEventArgs> throwing = (sender, args) => throw new InvalidOperationException("Listener test exception");
            EventHandler<SaveLoadEventArgs> following = (sender, args) => received++;
            manager.OnSaveCompleted += throwing; manager.OnSaveCompleted += following;
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Listener test exception"));
                yield return Await(manager.SaveAsync(1)); Assert.That(received, Is.EqualTo(1));
            }
            finally { manager.OnSaveCompleted -= throwing; manager.OnSaveCompleted -= following; }
            yield return Await(manager.SaveAsync(2));
        }
        [UnityTest]
        public IEnumerator InvalidLoadPreservesSceneCurrentDataAndPersistentPrefab()
        {
            var manager = SaveManager.Instance; var sentinel = new GameObject("Unsaved live state");
            var prefab = sentinel.AddComponent<SaveablePrefab>(); prefab.KeepAcrossScenes = true;
            string scene = SceneManager.GetActiveScene().name; var previous = manager.CurrentSaveData;
            var future = new SaveData(new VersionData(999, 0, 0)) { LastActiveScene = "A" };
            var slot = manager.GetSlotMetadata(1); slot.LastActiveScene = "A";
            manager.SaveSystem.Save(manager.Serializer.Serialize(future), slot);
            bool ignore = LogAssert.ignoreFailingMessages; LogAssert.ignoreFailingMessages = true;
            try
            {
                var missingScene = new SaveData(manager.SaveSettings.version) { LastActiveScene = "MissingSceneForValidation" };
                foreach (var payload in new[] { manager.Serializer.Serialize(future), new byte[] { 67, 83, 65, 86, 2, 1, 0 }, new byte[] { 1, 2, 3 }, manager.Serializer.Serialize(missingScene) })
                {
                manager.SaveSystem.Save(payload, slot);
                var load = manager.LoadSaveSlotAsync(1, true, true, timeout: TimeSpan.FromSeconds(10)); yield return Await(load);
                Assert.That(load.Result.Success, Is.False);
                Assert.That(sentinel != null && SceneManager.GetActiveScene().name == scene, Is.True);
                Assert.That(manager.CurrentSaveData, Is.SameAs(previous)); Assert.That(manager.IsLoading, Is.False);
                }
            }
            finally { LogAssert.ignoreFailingMessages = ignore; if (sentinel) Object.Destroy(sentinel); }
        }
        [UnityTest]
        public IEnumerator CloudReadTimeoutLeavesSceneUntouchedAndLateCompletionCannotApply()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem; var storage = new HardeningStorage(original) { GateReads = true };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL; Set(manager, "saveSystem", storage);
            var previous = manager.CurrentSaveData; var scene = SceneManager.GetActiveScene();
            try
            {
                var load = manager.LoadSaveSlotAsync(1, true, true, timeout: TimeSpan.FromMilliseconds(150)); yield return Await(load);
                Assert.That(load.Result.Success, Is.False); Assert.That(manager.IsLoading, Is.False);
                storage.Read.TrySetResult(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version) { LastActiveScene = "A" }));
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(manager.CurrentSaveData, Is.SameAs(previous)); Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            }
            finally { storage.Release(); manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); }
            yield return Await(manager.SaveAsync(2));
        }
        [UnityTest]
        public IEnumerator ManualSceneActivationWaitsAndCancellationReleasesHold()
        {
            var manager = SaveManager.Instance; yield return Await(manager.SaveAsync(1, "A"));
            foreach (bool cancel in new[] { false, true })
            {
                yield return SceneManager.LoadSceneAsync("B");
                bool requested = false; Action<string> onRequest = scene => requested = true;
                manager.OnSceneActivationRequested += onRequest;
                using var cancellation = new CancellationTokenSource();
                try
                {
                    var load = manager.LoadSaveSlotAsync(1, true, true, false, TimeSpan.FromSeconds(20), cancellation.Token);
                    yield return Until(() => requested, "activation request", 15);
                    Assert.That(load.IsCompleted, Is.False); Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("B"));
                    if (cancel) cancellation.Cancel(); else manager.ActivatePendingScene();
                    yield return Await(load);
                    Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                    Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("A"));
                    Assert.That(manager.IsLoading, Is.False);
                }
                finally { manager.ActivatePendingScene(); manager.OnSceneActivationRequested -= onRequest; }
            }
        }
        [UnityTest]
        public IEnumerator DisabledVisibilityControllerCannotReapplyAfterSceneEvent()
        {
            var go = new GameObject("Visibility lifetime"); var helper = go.AddComponent<RememberTransform>();
            helper.KeepAcrossScenes = true; helper.DisableAllColliders = true; helper.VisibleInScenes = new List<string> { "NeverVisible" };
            var collider = go.AddComponent<BoxCollider>(); var controller = go.AddComponent<PersistentVisibilityController>();
            yield return null; Assert.That(collider.enabled, Is.False);
            var old = SceneManager.GetActiveScene(); var extra = SceneManager.CreateScene("VisibilityLifetimeScene");
            try
            {
                for (int i = 0; i < 8; i++)
                {
                    controller.enabled = false; Assert.That(collider.enabled, Is.True);
                    SceneManager.SetActiveScene(extra); yield return null; Assert.That(collider.enabled, Is.True);
                    controller.enabled = true; yield return null; Assert.That(collider.enabled, Is.False);
                    SceneManager.SetActiveScene(old); controller.enabled = false; yield return null; Assert.That(collider.enabled, Is.True);
                }
            }
            finally { SceneManager.SetActiveScene(old); Object.Destroy(go); SceneManager.UnloadSceneAsync(extra); }
        }
        [UnityTest]
        public IEnumerator RejectedImportsLeavePayloadMetadataAndCustomKeysUnchanged()
        {
            var manager = SaveManager.Instance; yield return Await(manager.SaveAsync(1));
            var slot = manager.GetSlotMetadata(1); var before = manager.SaveSystem.Load(slot); var name = slot.SlotName;
            var future = manager.Serializer.Serialize(new SaveData(new VersionData(999, 0, 0)));
            foreach (var bytes in new[] { future, new byte[] { 1, 2, 3 }, manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)).Concat(new byte[] { 9 }).ToArray() })
            {
                var import = manager.ImportSaveAsync(bytes, 1); yield return Await(import); Assert.That(import.Result, Is.False);
                Assert.That(manager.SaveSystem.Load(slot), Is.EqualTo(before)); Assert.That(slot.SlotName, Is.EqualTo(name));
            }
            slot.CustomMetadata["obsolete"] = "remove";
            var bundle = File.ReadAllBytes("ci/fixtures/legacy-v1/bundle.crystalsave");
            var valid = manager.ImportSaveBundleAsync(bundle, 1); yield return Await(valid); Assert.That(valid.Result, Is.True);
            Assert.That(manager.GetSlotMetadata(1).CustomMetadata.ContainsKey("obsolete"), Is.False);
            Assert.That(manager.GetSlotMetadata(1).SlotName, Is.EqualTo("Legacy\nSlot"));
        }
    }
    internal sealed class HardeningStorage : ISaveSystem, ISaveConflictSource
    {
        readonly ISaveSystem inner;
        internal bool GateWrites, GateReads;
        internal Func<SaveSlot, Task> DeleteOverride;
        internal readonly List<TaskCompletionSource<bool>> Writes = new();
        internal readonly TaskCompletionSource<byte[]> Read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal HardeningStorage(ISaveSystem inner) { this.inner = inner; }
        public Task<SaveConflictCandidates> ReadCandidatesAsync(SaveSlot slot, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Task.FromResult(new SaveConflictCandidates(null, null)); }
        public async Task SaveAsync(byte[] data, SaveSlot slot)
        {
            if (GateWrites) { var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); Writes.Add(gate); await gate.Task; }
            inner.Save(data, slot);
        }
        public Task<byte[]> LoadAsync(SaveSlot slot) => GateReads ? Read.Task : Task.FromResult(inner.Load(slot));
        internal void Release() { foreach (var gate in Writes) gate.TrySetResult(true); Read.TrySetResult(null); }
        public Task SaveSlotMetadataAsync(SaveSlot slot) => inner.SaveSlotMetadataAsync(slot);
        public Task<SaveSlot> LoadSlotMetadataAsync(int number) => inner.LoadSlotMetadataAsync(number);
        public Task DeleteAsync(SaveSlot slot) => DeleteOverride != null ? DeleteOverride(slot) : inner.DeleteAsync(slot);
        public void Save(byte[] data, SaveSlot slot) => inner.Save(data, slot);
        public byte[] Load(SaveSlot slot) => inner.Load(slot);
        public void Delete(SaveSlot slot) => inner.Delete(slot);
        public string GetSaveGamesPath() => inner.GetSaveGamesPath();
        public void BackupSlot(SaveSlot slot) => inner.BackupSlot(slot);
        public void RestoreBackup(SaveSlot slot) => inner.RestoreBackup(slot);
        public void DeleteBackup(SaveSlot slot) => inner.DeleteBackup(slot);
        public Task WriteTempAsync(byte[] bytes, SaveSlot slot) => inner.WriteTempAsync(bytes, slot);
        public Task<byte[]> LoadTempAsync(SaveSlot slot) => inner.LoadTempAsync(slot);
        public void DeleteTemp(SaveSlot slot) => inner.DeleteTemp(slot);
    }
}
#endif
