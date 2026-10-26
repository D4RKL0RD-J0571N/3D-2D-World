#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
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
        [UnityTest] public IEnumerator CloudCleanup_TemporarySuccess() => CloudCleanupScenario("success");
        [UnityTest] public IEnumerator CloudCleanup_TemporaryWriteFailure() => CloudCleanupScenario("write");
        [UnityTest] public IEnumerator CloudCleanup_TemporaryValidationFailure() => CloudCleanupScenario("validation");
        [UnityTest] public IEnumerator CloudCleanup_TemporaryUploadFailure() => CloudCleanupScenario("upload");
        [UnityTest] public IEnumerator CloudCleanup_TemporaryCleanupFailureKeepsCommit() => CloudCleanupScenario("cleanup");
        [UnityTest] public IEnumerator CloudCleanup_TemporaryCleanupFailureKeepsOriginalError() => CloudCleanupScenario("both");
        [UnityTest] public IEnumerator CloudCleanup_CancellationWaitsForStaging() => CloudCleanupScenario("cancel");
        [UnityTest] public IEnumerator CloudCleanup_HoldsQueueUntilAcknowledged() => CloudCleanupScenario("hold");

        IEnumerator CloudCleanupScenario(string mode)
        {
            var manager = SaveManager.Instance;
            var originalStorage = manager.SaveSystem;
            var originalSettings = UnityEngine.Object.Instantiate(manager.SaveSettings);
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.CloudMirrorFiles")
                .GetField("FlushOverride", BindingFlags.Static | BindingFlags.NonPublic);
            object originalHook = hook.GetValue(null);
            var storage = new CloudCleanupStorage(originalStorage.GetSaveGamesPath())
            {
                FailWrite = mode == "write", InvalidTemp = mode == "validation",
                FailUpload = mode == "upload" || mode == "both"
            };
            var stageGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var flushGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (mode == "cancel") storage.WriteGate = stageGate.Task;
            int flushes = 0, successes = 0, failures = 0;
            EventHandler<SaveLoadEventArgs> success = (_, __) => successes++;
            EventHandler<OperationFailedEventArgs> failed = (_, __) => failures++;
            manager.OnSaveCompleted += success; manager.OnSaveFailed += failed;
            hook.SetValue(null, (Func<Task>)(() =>
            {
                flushes++;
                Assert.That(File.Exists(storage.Temp(1)), Is.False, "Deletion precedes persistence acknowledgement.");
                if (flushes == 1 && mode == "hold") return flushGate.Task;
                if (flushes == 1 && (mode == "cleanup" || mode == "both"))
                    throw new IOException("CLOUD_CLEANUP_ACK_FAILURE");
                return Task.CompletedTask;
            }));
            manager.SaveSettings.enableCloudSave = true;
            manager.SaveSettings.keepLocalMirror = false;
            manager.SaveSettings.backend = SaveBackend.MySQL;
            manager.SaveSettings.enableSaveFileVerification = true;
            manager.SaveSettings.enableScreenshots = false;
            Set(manager, "saveSystem", storage);
            using var cancellation = new CancellationTokenSource();
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var save = manager.SaveAsync(1, null, "Committed cloud name", cancellation.Token);
                if (mode == "cancel")
                {
                    yield return Until(() => storage.WriteStarted, "temporary staging started");
                    cancellation.Cancel();
                    for (int frame = 0; frame < 3; frame++) yield return null;
                    Assert.That(save.IsCompleted, Is.False, "A cancelled request still owns its mutating staging write.");
                    stageGate.TrySetResult(true);
                }
                Task following = null;
                if (mode == "hold")
                {
                    yield return Until(() => flushes == 1, "temporary cleanup acknowledgement started");
                    following = manager.SaveImmediateAsync(2);
                    for (int frame = 0; frame < 3; frame++) yield return null;
                    Assert.That(save.IsCompleted || following.IsCompleted, Is.False);
                    Assert.That(successes, Is.Zero);
                    Assert.That(storage.Writes, Is.EqualTo(1));
                    flushGate.TrySetResult(true);
                }
                yield return Until(() => save.IsCompleted, "temporary cleanup outcome");
                bool succeeded = mode == "success" || mode == "hold";
                Assert.That(save.Status, Is.EqualTo(mode == "cancel" ? TaskStatus.Canceled : succeeded ? TaskStatus.RanToCompletion : TaskStatus.Faulted), save.Exception?.ToString());
                Assert.That(File.Exists(storage.Temp(1)), Is.False);
                Assert.That(flushes, Is.GreaterThanOrEqualTo(1));
                if (mode == "cleanup")
                {
                    Assert.That(storage.Writes, Is.EqualTo(1));
                    Assert.That(manager.GetSlotMetadata(1).SlotName, Is.EqualTo("Committed cloud name"));
                    Assert.That(save.Exception.ToString(), Does.Contain("Save committed"));
                    Assert.That(successes, Is.Zero); Assert.That(failures, Is.EqualTo(1));
                }
                if (mode == "both")
                {
                    Assert.That(save.Exception.ToString(), Does.Contain("CLOUD_CLEANUP_UPLOAD_FAILURE"));
                    Assert.That(save.Exception.ToString(), Does.Contain("CLOUD_CLEANUP_ACK_FAILURE"));
                }
                if (mode == "cancel" || mode == "write" || mode == "validation") Assert.That(storage.Writes, Is.Zero);
                storage.FailUpload = storage.FailWrite = storage.InvalidTemp = false;
                if (following != null) yield return Await(following);
                yield return Await(manager.SaveImmediateAsync(3));
            }
            finally
            {
                stageGate.TrySetResult(true); flushGate.TrySetResult(true);
                hook.SetValue(null, originalHook); Set(manager, "saveSystem", originalStorage);
                manager.OnSaveCompleted -= success; manager.OnSaveFailed -= failed;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(originalSettings), manager.SaveSettings);
                UnityEngine.Object.Destroy(originalSettings); LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator CloudCleanup_BatchCleanupContinuesAfterEntryFailure()
        {
            var manager = SaveManager.Instance;
            var original = manager.SaveSystem;
            var savedSettings = UnityEngine.Object.Instantiate(manager.SaveSettings);
            var storage = new CloudCleanupStorage(original.GetSaveGamesPath());
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.CloudMirrorFiles")
                .GetField("FlushOverride", BindingFlags.Static | BindingFlags.NonPublic);
            object previousHook = hook.GetValue(null);
            int acknowledgements = 0;
            hook.SetValue(null, (Func<Task>)(() => { acknowledgements++; return Task.CompletedTask; }));
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.keepLocalMirror = false;
            manager.SaveSettings.backend = SaveBackend.MySQL;
            manager.SaveSettings.enableSaveFileVerification = true;
            Set(manager, "saveSystem", storage);
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return Await(manager.QuickSaveAsync());
                yield return Await(manager.QuickSaveAsync());
                storage.FailUploadAt = storage.UploadAttempts + 2;
                storage.FailDeleteSlot = manager.SaveSettings.quickSaveSlotOffset + 2;
                storage.Deleted.Clear();
                int before = acknowledgements;
                var save = manager.QuickSaveAsync();
                yield return Until(() => save.IsCompleted, "failed rotating temporary cleanup");
                Assert.That(save.IsFaulted, Is.True);
                Assert.That(save.Exception.ToString(), Does.Contain("CLOUD_CLEANUP_UPLOAD_FAILURE"));
                Assert.That(save.Exception.ToString(), Does.Contain("CLOUD_CLEANUP_DELETE_FAILURE"));
                CollectionAssert.AreEquivalent(new[] { manager.SaveSettings.quickSaveSlotOffset + 1, manager.SaveSettings.quickSaveSlotOffset + 2 }, storage.Deleted);
                foreach (int number in storage.Deleted) Assert.That(File.Exists(storage.Temp(number)), Is.False);
                Assert.That(acknowledgements, Is.GreaterThan(before), "An entry error cannot skip acknowledgement of other deletions.");
                storage.FailDeleteSlot = storage.FailUploadAt = 0;
                yield return Await(manager.QuickSaveAsync());
            }
            finally
            {
                hook.SetValue(null, previousHook); Set(manager, "saveSystem", original);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(savedSettings), manager.SaveSettings);
                UnityEngine.Object.Destroy(savedSettings); LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator CloudCleanup_CachedImagesDeleteAfterSettingsChange()
        {
            using var fixture = new ScreenshotCacheFixture(false);
            yield return Await(fixture.Initialize());
            const string name = "obsolete.png";
            yield return Await(fixture.Storage.UploadScreenshotAsync(name, fixture.Plain, ScreenshotFormat.PNG));
            var download = fixture.Images.DownloadScreenshotAsync(ScreenshotCacheSlot(name));
            yield return Await(download);
            Assert.That(download.Result, Is.Not.Null); UnityEngine.Object.Destroy(download.Result);
            fixture.Settings.enableScreenshots = false;
            fixture.Settings.keepLocalMirror = false;
            File.WriteAllBytes(fixture.PathFor(name) + ".previous", fixture.Plain);
            fixture.Images.DeleteScreenshot(name);
            Assert.That(File.Exists(fixture.PathFor(name)), Is.False);
            Assert.That(File.Exists(fixture.PathFor(name) + ".previous"), Is.False);
            File.WriteAllBytes(fixture.PathFor(name) + ".previous", fixture.Plain);
            fixture.Images.DeleteScreenshot(name);
            Assert.That(File.Exists(fixture.PathFor(name) + ".previous"), Is.False, "Recovery-only cache is also owned.");
        }
    }

    sealed class CloudCleanupStorage : ISaveSystem, ISaveConflictSource
    {
        readonly string root;
        internal bool FailWrite, FailUpload, InvalidTemp, WriteStarted;
        internal Task WriteGate;
        internal int Writes, UploadAttempts, FailUploadAt, FailDeleteSlot;
        internal readonly List<int> Deleted = new List<int>();
        readonly Dictionary<int, byte[]> payloads = new Dictionary<int, byte[]>();
        readonly Dictionary<int, SaveSlot> metadata = new Dictionary<int, SaveSlot>();
        internal CloudCleanupStorage(string root) { this.root = root; }
        internal string Temp(int number) => Path.Combine(root, "cleanup-" + number + ".tmp");
        public Task<SaveConflictCandidates> ReadCandidatesAsync(SaveSlot slot, CancellationToken token)
            => Task.FromResult(new SaveConflictCandidates(null, payloads.TryGetValue(slot.SlotNumber, out var bytes)
                ? new SaveConflictCandidate(bytes, metadata[slot.SlotNumber]) : null));
        public async Task WriteTempAsync(byte[] bytes, SaveSlot slot)
        {
            WriteStarted = true;
            if (WriteGate != null) await WriteGate;
            File.WriteAllBytes(Temp(slot.SlotNumber), bytes);
            if (FailWrite) throw new IOException("CLOUD_CLEANUP_STAGE_FAILURE");
        }
        public Task<byte[]> LoadTempAsync(SaveSlot slot) => Task.FromResult(InvalidTemp ? new byte[] { 1, 2 } : File.ReadAllBytes(Temp(slot.SlotNumber)));
        public void DeleteTemp(SaveSlot slot)
        {
            Deleted.Add(slot.SlotNumber); File.Delete(Temp(slot.SlotNumber));
            if (FailDeleteSlot == slot.SlotNumber) throw new IOException("CLOUD_CLEANUP_DELETE_FAILURE");
        }
        public Task SaveAsync(byte[] bytes, SaveSlot slot)
        {
            UploadAttempts++;
            if (FailUpload || UploadAttempts == FailUploadAt) throw new IOException("CLOUD_CLEANUP_UPLOAD_FAILURE");
            Writes++; payloads[slot.SlotNumber] = bytes; metadata[slot.SlotNumber] = slot;
            return Task.CompletedTask;
        }
        public Task<byte[]> LoadAsync(SaveSlot slot) => Task.FromResult(payloads.TryGetValue(slot.SlotNumber, out var bytes) ? bytes : null);
        public Task<SaveSlot> LoadSlotMetadataAsync(int number) => Task.FromResult(metadata.TryGetValue(number, out var slot) ? slot : null);
        public Task SaveSlotMetadataAsync(SaveSlot slot) => Task.CompletedTask;
        public Task DeleteAsync(SaveSlot slot) => Task.CompletedTask;
        public void Save(byte[] bytes, SaveSlot slot) => throw new NotSupportedException();
        public byte[] Load(SaveSlot slot) => null;
        public void Delete(SaveSlot slot) { }
        public string GetSaveGamesPath() => root;
        public void BackupSlot(SaveSlot slot) { }
        public void RestoreBackup(SaveSlot slot) { }
        public void DeleteBackup(SaveSlot slot) { }
    }
}
#endif
