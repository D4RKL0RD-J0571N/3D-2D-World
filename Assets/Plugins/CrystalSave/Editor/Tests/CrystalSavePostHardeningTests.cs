#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public IEnumerator ConflictReadCancellationAndTransportFailureCannotWriteOrPoisonQueue()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var gate = new TaskCompletionSource<SaveConflictCandidates>();
            var storage = new PostReviewStorage(original) { CandidateGate = gate.Task };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL; Set(manager, "saveSystem", storage);
            using var cancellation = new System.Threading.CancellationTokenSource();
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var request = manager.SaveAsync(1, null, cancellation.Token);
                yield return Until(() => storage.LocalReads != 0, "conflict read started");
                cancellation.Cancel(); yield return Until(() => request.IsCompleted, "conflict read cancelled");
                Assert.That(request.IsCanceled, Is.True); Assert.That(storage.Written, Is.Null);
                gate.SetException(new IOException("Late transport failure")); yield return null;
                Assert.That(storage.Written, Is.Null);
                storage.CandidateGate = Task.FromException<SaveConflictCandidates>(new IOException("Transport failure"));
                var failure = manager.SaveImmediateAsync(1); yield return Until(() => failure.IsCompleted, "conflict transport failure");
                Assert.That(failure.IsFaulted, Is.True); Assert.That(storage.Written, Is.Null);
                storage.CandidateGate = Task.FromResult(new SaveConflictCandidates(null, null));
                yield return Await(manager.SaveImmediateAsync(1)); Assert.That(storage.Written, Is.Not.Null);
            }
            finally { manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); LogAssert.ignoreFailingMessages = false; }
        }
        [UnityTest]
        public IEnumerator VerificationFailureRollsBackAllSaveEntryPointsAcrossFilenameChanges()
        {
            var manager = SaveManager.Instance; var settings = manager.SaveSettings;
            settings.saveFileName = "Slot{n}_{meta:profile}"; settings.metadataFileNamePattern = "Slot{n}_{meta:profile}_Meta.bin";
            manager.GetSlotMetadata(1).CustomMetadata["profile"] = "before";
            yield return Await(manager.SaveImmediateAsync(1));
            var before = manager.SaveSystem.Load(manager.GetSlotMetadata(1));
            settings.enableSaveFileVerification = true;
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction").GetField("FaultInjection", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            LogAssert.ignoreFailingMessages = true;
            try
            {
                foreach (string entry in new[] {"normal", "immediate", "filtered", "sync"})
                {
                    manager.GetSlotMetadata(1).CustomMetadata["profile"] = "after";
                    hook.SetValue(null, (Action<string>)(stage =>
                    {
                        if (stage == "committed")
                            File.WriteAllBytes(Directory.GetFiles(manager.SaveSystem.GetSaveGamesPath(), "Slot1_after.sav", SearchOption.AllDirectories).Single(), new byte[] {0x7f});
                    }));
                    if (entry == "sync") manager.SaveSync(1);
                    else
                    {
                        Task save = entry == "normal" ? manager.SaveAsync(1) : entry == "immediate" ? manager.SaveImmediateAsync(1) : manager.SaveSlotExcludingTagAsync(1, "Player");
                        yield return Until(() => save.IsCompleted, "verification rollback " + entry);
                        if (entry == "filtered") Assert.That(((Task<bool>)save).Result, Is.False);
                        else Assert.That(save.IsFaulted, Is.True, entry);
                    }
                    hook.SetValue(null, null);
                    Assert.That(manager.SaveSystem.Load(manager.GetSlotMetadata(1)), Is.EqualTo(before), entry);
                    Assert.That(Directory.GetFiles(manager.SaveSystem.GetSaveGamesPath(), "Slot1_after*", SearchOption.AllDirectories), Is.Empty);
                }
                manager.GetSlotMetadata(1).CustomMetadata["profile"] = "before";
                yield return Await(manager.SaveImmediateAsync(1));
            }
            finally { hook.SetValue(null, null); LogAssert.ignoreFailingMessages = false; }
        }
        [UnityTest]
        public IEnumerator EveryAutomaticConflictPolicyPersistsSelectionWithoutCapturingLiveState()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var probe = new PostReviewSaveable { ThrowSave = true };
            manager.ComponentManager.RegisterSaveableComponent(probe);
            var live = manager.CurrentSaveData;
            var local = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2024,1,1), LastActiveScene = "A" };
            var remote = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2025,1,1), LastActiveScene = "B" };
            local.ComponentsData["local_id"] = new byte[] {1}; remote.ComponentsData["remote_id"] = new byte[] {2};
            var storage = new PostReviewStorage(original) { LocalRead = manager.Serializer.Serialize(local), CloudRead = manager.Serializer.Serialize(remote) };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL; manager.SaveSettings.autoResolveConflicts = true;
            Set(manager, "saveSystem", storage);
            try
            {
                foreach (var policy in new[] { AutoConflictPolicy.Latest, AutoConflictPolicy.Oldest, AutoConflictPolicy.LocalWins, AutoConflictPolicy.CloudWins, AutoConflictPolicy.Custom })
                {
                    manager.SaveSettings.autoConflictPolicy = policy;
                    manager.SaveSettings.metadataRules = new[] { new MetadataRule { type = MetadataRuleType.CloudWins } };
                    yield return Await(manager.SaveImmediateAsync(1));
                    bool remoteWins = policy != AutoConflictPolicy.Oldest && policy != AutoConflictPolicy.LocalWins;
                    var written = manager.Serializer.Deserialize<SaveData>(storage.Written);
                    Assert.That(written.ComponentsData.Keys, Is.EquivalentTo(new[] { remoteWins ? "remote_id" : "local_id" }), policy.ToString());
                    Assert.That(written.LastSaved, Is.EqualTo(remoteWins ? remote.LastSaved : local.LastSaved));
                    Assert.That(written.LastActiveScene, Is.EqualTo(remoteWins ? "B" : "A"));
                    Assert.That(manager.CurrentSaveData, Is.SameAs(live));
                }
                var target = SceneComponent("ConflictFilterTarget");
                try
                {
                    remote.ComponentsData[target.UniqueIdentifier] = new byte[] {8};
                    storage.CloudRead = manager.Serializer.Serialize(remote);
                    manager.SaveSettings.autoConflictPolicy = AutoConflictPolicy.CloudWins;
                    var filtered = manager.SaveSlotForGameObjectAsync(target.gameObject, 1);
                    yield return Await(filtered); Assert.That(filtered.Result, Is.True);
                    var written = manager.Serializer.Deserialize<SaveData>(storage.Written);
                    Assert.That(written.ComponentsData.Keys, Is.EquivalentTo(new[] {target.UniqueIdentifier}));
                    Assert.That(written.LastSaved, Is.EqualTo(remote.LastSaved));
                    Assert.That(manager.CurrentSaveData, Is.SameAs(live));
                }
                finally { UnityEngine.Object.Destroy(target.gameObject); }
            }
            finally { manager.ComponentManager.UnregisterSaveableComponent(probe); manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); }
        }

        [UnityTest]
        public IEnumerator ManualConflictChoicesAndCancellationOwnOnlyTheirResolverUI()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var resolver = manager.GetComponent<LiveConflictResolver>() ?? manager.gameObject.AddComponent<LiveConflictResolver>();
            Set(Get<SaveOperationService>(manager, "saveOperationService"), "conflictResolver", resolver);
            var canvasObject = new GameObject("SuppliedResolverCanvas", typeof(Canvas));
            var unrelated = new GameObject("UserContent"); unrelated.transform.SetParent(canvasObject.transform); canvasObject.SetActive(false);
            Set(resolver, "overlayCanvas", canvasObject.GetComponent<Canvas>());
            var local = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2024,1,1) };
            var remote = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2025,1,1) };
            local.ComponentsData["local_id"] = new byte[] {1}; remote.ComponentsData["remote_id"] = new byte[] {2};
            var storage = new PostReviewStorage(original) { LocalRead = manager.Serializer.Serialize(local), CloudRead = manager.Serializer.Serialize(remote) };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL; manager.SaveSettings.autoResolveConflicts = false;
            Set(manager, "saveSystem", storage);
            try
            {
                foreach (string choice in new[] { "Accept Remote", "Keep Local", "Merge", "Cancel" })
                {
                    storage.Written = null;
                    using var cancel = new System.Threading.CancellationTokenSource();
                    var save = manager.SaveAsync(1, null, cancel.Token);
                    yield return Until(() => canvasObject.activeSelf || save.IsCompleted, "manual conflict prompt");
                    Assert.That(save.IsCompleted, Is.False, save.Exception?.ToString());
                    var button = canvasObject.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == (choice == "Cancel" ? "Accept Remote" : choice));
                    if (choice == "Cancel") cancel.Cancel(); else button.onClick.Invoke();
                    yield return Until(() => save.IsCompleted, "manual conflict completion");
                    if (choice == "Cancel")
                    {
                        Assert.That(save.IsCanceled, Is.True); button.onClick.Invoke(); yield return null;
                        Assert.That(storage.Written, Is.Null, "Late UI callbacks may not write.");
                    }
                    else
                    {
                        Assert.That(save.IsFaulted, Is.False, save.Exception?.ToString());
                        var written = manager.Serializer.Deserialize<SaveData>(storage.Written);
                        Assert.That(written.ComponentsData.ContainsKey(choice == "Keep Local" ? "local_id" : "remote_id"), Is.True);
                    }
                    yield return null;
                    Assert.That(canvasObject.activeSelf, Is.False); Assert.That(unrelated, Is.Not.Null);
                    Assert.That(canvasObject.transform.childCount, Is.EqualTo(1));
                }
            }
            finally { manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); UnityEngine.Object.Destroy(canvasObject); }
        }

        [UnityTest]
        public IEnumerator UnsupportedCloudBackendAndInvalidCandidateFailBeforeWrite()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var unsupported = new UnsupportedConflictStorage(original); Set(manager, "saveSystem", unsupported);
                var request = manager.SaveImmediateAsync(1); yield return Until(() => request.IsCompleted, "unsupported backend");
                Assert.That(request.IsFaulted, Is.True); Assert.That(request.Exception.ToString(), Does.Contain("ISaveConflictSource")); Assert.That(unsupported.Writes, Is.Zero);
                var invalid = new PostReviewStorage(original) { LocalRead = new byte[] {1}, CloudRead = new byte[] {2} }; Set(manager, "saveSystem", invalid);
                var corrupt = manager.SaveAsync(1); yield return Until(() => corrupt.IsCompleted, "invalid conflict candidate");
                Assert.That(corrupt.IsFaulted, Is.True); Assert.That(invalid.Written, Is.Null);
            }
            finally { manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); LogAssert.ignoreFailingMessages = false; }
            yield return Await(manager.SaveImmediateAsync(1));
        }
        [UnityTest]
        public IEnumerator CaptureFailuresAcrossEntryPointsPreservePayloadMetadataAndQueue()
        {
            var manager = SaveManager.Instance;
            var go = new GameObject("RequiredCapture"); Composite(go, typeof(FeatureProbe));
            var probe = go.GetComponent<FeatureProbe>(); probe.Value = 17;
            Set(probe, "enableEventBridge", true);
            var saving = Get<UnityEngine.Events.UnityEvent>(probe, "onSaving");
            yield return null;
            yield return Await(manager.SaveAsync(1));
            var before = manager.SaveSystem.Load(manager.GetSlotMetadata(1));
            var metadata = manager.GetSlotMetadata(1).LastSaved;
            var current = manager.CurrentSaveData;
            manager.SaveSettings.enableScreenshots = true;
            Get<ScreenshotManager>(manager, "screenshotManager").CustomCapture = () => new Texture2D(4,4);
            var files = Directory.GetFiles(manager.SaveSystem.GetSaveGamesPath(), "*", SearchOption.AllDirectories);
            int failures = 0, successes = 0;
            EventHandler<OperationFailedEventArgs> failed = (_, _) => failures++;
            EventHandler<SaveLoadEventArgs> saved = (_, _) => successes++;
            manager.OnSaveFailed += failed; manager.OnSaveCompleted += saved;
            saving.AddListener(() => throw new InvalidOperationException("Required event capture failed"));
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var normal = manager.SaveAsync(1); yield return Until(() => normal.IsCompleted, "normal rejected capture"); Assert.That(normal.IsFaulted, Is.True);
                var immediate = manager.SaveImmediateAsync(1); yield return Until(() => immediate.IsCompleted, "immediate rejected capture"); Assert.That(immediate.IsFaulted, Is.True);
                var filtered = manager.SaveSlotForGameObjectAsync(go, 1); yield return Await(filtered); Assert.That(filtered.Result, Is.False);
                manager.SaveSync(1);
                Assert.That(failures, Is.EqualTo(4)); Assert.That(successes, Is.Zero);
                Assert.That(manager.SaveSystem.Load(manager.GetSlotMetadata(1)), Is.EqualTo(before));
                Assert.That(manager.GetSlotMetadata(1).LastSaved, Is.EqualTo(metadata));
                Assert.That(manager.CurrentSaveData, Is.SameAs(current));
                Assert.That(Directory.GetFiles(manager.SaveSystem.GetSaveGamesPath(), "*", SearchOption.AllDirectories), Is.EquivalentTo(files), "Rejected capture must remove its screenshot.");
                saving.RemoveAllListeners();
                yield return Await(manager.SaveImmediateAsync(2));
                Assert.That(successes, Is.EqualTo(1));
            }
            finally { manager.OnSaveFailed -= failed; manager.OnSaveCompleted -= saved; UnityEngine.Object.Destroy(go); LogAssert.ignoreFailingMessages = false; }
        }

        [UnityTest]
        public IEnumerator RestoreFailuresContinueHealthyEntriesRetainPendingBytesAndAllowRetry()
        {
            var manager = SaveManager.Instance;
            var badObject = new GameObject("FailedRestore"); Composite(badObject, typeof(FeatureProbe));
            var healthyObject = new GameObject("HealthyRestore"); Composite(healthyObject, typeof(FeatureProbe));
            var bad = badObject.GetComponent<FeatureProbe>(); var healthy = healthyObject.GetComponent<FeatureProbe>();
            bad.Value = 12; healthy.Value = 34;
            yield return null; yield return Await(manager.SaveImmediateAsync(1));
            bad.Loaded = () => throw new InvalidOperationException("Required application failed");
            bad.Value = healthy.Value = 99;
            int failures = 0, successes = 0;
            EventHandler<OperationFailedEventArgs> failed = (_, _) => failures++;
            EventHandler<SaveLoadEventArgs> completed = (_, _) => successes++;
            manager.OnLoadFailed += failed; manager.OnLoadCompleted += completed;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var load = manager.LoadSaveSlotAsync(1); yield return Await(load);
                Assert.That(load.Result.Success, Is.False); Assert.That(load.Result.ErrorMessage, Does.Contain(bad.UniqueIdentifier));
                Assert.That(healthy.Value, Is.EqualTo(34));
                Assert.That(failures, Is.EqualTo(1)); Assert.That(successes, Is.Zero);
                var pending = PendingBytes(manager.ComponentManager);
                Assert.That(pending.ContainsKey(bad.UniqueIdentifier), Is.True);
                Assert.That(pending.ContainsKey(healthy.UniqueIdentifier), Is.False);
                bad.Loaded = null;
                var retry = manager.LoadSaveSlotAsync(1); yield return Await(retry);
                Assert.That(retry.Result.Success, Is.True, retry.Result.ErrorMessage);
                Assert.That(failures, Is.EqualTo(1)); Assert.That(successes, Is.EqualTo(1));
                Assert.That(pending.ContainsKey(bad.UniqueIdentifier), Is.False);
            }
            finally { manager.OnLoadFailed -= failed; manager.OnLoadCompleted -= completed; UnityEngine.Object.Destroy(badObject); UnityEngine.Object.Destroy(healthyObject); LogAssert.ignoreFailingMessages = false; }
        }

        [UnityTest]
        public IEnumerator PrefabBlobCaptureAndBridgeRestoreFailuresReachOperationOutcome()
        {
            var manager = SaveManager.Instance; var prefab = Spawn();
            var helper = prefab.GetComponentInChildren<RememberTransform>();
            Set(helper, "enableEventBridge", true);
            var saving = Get<UnityEngine.Events.UnityEvent>(helper, "onSaving");
            saving.AddListener(() => helper.SetEventInt(12));
            var loaded = Get<UnityEngine.Events.UnityEvent<int>>(helper, "onLoadedInt");
            loaded.AddListener(_ => throw new InvalidOperationException("Prefab bridge failed"));
            yield return null; yield return Await(manager.SaveImmediateAsync(1));
            var before = manager.SaveSystem.Load(manager.GetSlotMetadata(1));
            LogAssert.ignoreFailingMessages = true;
            try
            {
                saving.AddListener(() => throw new InvalidOperationException("Prefab capture failed"));
                var rejected = manager.SaveImmediateAsync(1); yield return Until(() => rejected.IsCompleted, "prefab capture rejection");
                Assert.That(rejected.IsFaulted, Is.True); Assert.That(rejected.Exception.ToString(), Does.Contain(helper.UniqueIdentifier)); Assert.That(manager.SaveSystem.Load(manager.GetSlotMetadata(1)), Is.EqualTo(before));
                saving.RemoveAllListeners();
                var load = manager.LoadSaveSlotAsync(1); yield return Await(load);
                Assert.That(load.Result.Success, Is.False, "The reused prefab's bridge must be part of the restore outcome.");
                Assert.That(load.Result.ErrorMessage, Does.Contain(helper.UniqueIdentifier));
            }
            finally { UnityEngine.Object.Destroy(prefab.gameObject); LogAssert.ignoreFailingMessages = false; }
        }

        [UnityTest]
        public IEnumerator DelayedTransformRetriesFailLoadAndInactiveHelperDoesNotBlock()
        {
            var manager = SaveManager.Instance; var helper = SceneComponent("DelayedRestore");
            Set(helper, "rememberScaleSettings", new TransformPropertySettings { Save = true, SpaceMode = SpaceMode.World });
            helper.transform.localScale = Vector3.one * 2;
            yield return null; yield return Await(manager.SaveImmediateAsync(1));
            var parent = new GameObject("ZeroScaleParent"); helper.transform.SetParent(parent.transform, false); parent.transform.localScale = Vector3.zero;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var load = manager.LoadSaveSlotAsync(1); yield return Await(load);
                Assert.That(load.Result.Success, Is.False); Assert.That(load.Result.ErrorMessage, Does.Contain(helper.UniqueIdentifier));
                parent.transform.localScale = Vector3.one;
                helper.gameObject.SetActive(false);
                var deferred = manager.LoadSaveSlotAsync(1); yield return Await(deferred);
                Assert.That(deferred.Result.Success, Is.True, deferred.Result.ErrorMessage);
                helper.gameObject.SetActive(true); yield return null; yield return null;
                Assert.That(helper.transform.lossyScale, Is.EqualTo(Vector3.one * 2));
            }
            finally { UnityEngine.Object.Destroy(parent); LogAssert.ignoreFailingMessages = false; }
        }
        [UnityTest]
        public IEnumerator PostHardening_SaveCallbackFailureMustPreservePreviousPayload()
        {
            var manager = SaveManager.Instance;
            var probe = new PostReviewSaveable();
            manager.ComponentManager.RegisterSaveableComponent(probe);
            try
            {
                yield return Await(manager.SaveAsync(1));
                var slot = manager.GetSlotMetadata(1);
                var before = manager.SaveSystem.Load(slot);
                Assert.That(manager.Serializer.Deserialize<SaveData>(before).ComponentsData.ContainsKey(probe.UniqueIdentifier), Is.True);
                probe.ThrowSave = true;
                LogAssert.ignoreFailingMessages = true;
                var save = manager.SaveAsync(1);
                yield return Until(() => save.IsCompleted, "save callback failure completion");
                var after = manager.SaveSystem.Load(slot);
                bool hasEntry = manager.Serializer.Deserialize<SaveData>(after).ComponentsData.ContainsKey(probe.UniqueIdentifier);
                TestContext.WriteLine($"Task={save.Status}; previousBytesPreserved={before.SequenceEqual(after)}; componentEntryPresent={hasEntry}");
                Assert.That(save.IsFaulted, Is.True, "A component serialization exception must fail the save, preserving the previous payload.");
                Assert.That(after, Is.EqualTo(before));
            }
            finally { probe.ThrowSave = false; manager.ComponentManager.UnregisterSaveableComponent(probe); LogAssert.ignoreFailingMessages = false; }
        }
        [UnityTest]
        public IEnumerator PostHardening_CorruptComponentMustNotReportSuccessfulLoad()
        {
            var manager = SaveManager.Instance;
            var go = new GameObject("PostReviewComponent");
            Composite(go, typeof(FeatureProbe));
            var probe = go.GetComponent<FeatureProbe>(); probe.Value = 17;
            yield return null;
            try
            {
                yield return Await(manager.SaveAsync(1));
                var slot = manager.GetSlotMetadata(1);
                var data = manager.Serializer.Deserialize<SaveData>(manager.SaveSystem.Load(slot));
                Assert.That(data.ComponentsData.ContainsKey(probe.UniqueIdentifier), Is.True);
                data.ComponentsData[probe.UniqueIdentifier] = new byte[] { 1 };
                manager.SaveSystem.Save(manager.Serializer.Serialize(data), slot);
                probe.Value = 99;
                LogAssert.ignoreFailingMessages = true;
                var load = manager.LoadSaveSlotAsync(1);
                yield return Await(load);
                TestContext.WriteLine($"LoadSuccess={load.Result.Success}; valueAfterLoad={probe.Value}; successfulComponentLoads={probe.Loads}; error={load.Result.ErrorMessage}");
                Assert.That(load.Result.Success, Is.False, "Component decode failed, so the whole restore cannot be reported as successful.");
            }
            finally { UnityEngine.Object.Destroy(go); LogAssert.ignoreFailingMessages = false; }
        }
        [UnityTest]
        public IEnumerator PostHardening_CloudWinsMustPreserveSelectedCloudState()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var storage = new PostReviewStorage(original);
            var local = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2024, 1, 1) };
            var cloud = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2025, 1, 1) };
            cloud.ComponentsData["remote-object_remote-component"] = new byte[] { 7, 8, 9 };
            storage.LocalRead = manager.Serializer.Serialize(local); storage.CloudRead = manager.Serializer.Serialize(cloud);
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL;
            manager.SaveSettings.autoResolveConflicts = true; manager.SaveSettings.autoConflictPolicy = AutoConflictPolicy.CloudWins;
            Set(manager, "saveSystem", storage);
            try
            {
                yield return Await(manager.SaveAsync(1));
                var written = manager.Serializer.Deserialize<SaveData>(storage.Written);
                bool retained = written.ComponentsData.ContainsKey("remote-object_remote-component");
                TestContext.WriteLine($"LocalReads={storage.LocalReads}; cloudReads={storage.CloudReads}; selectedCloudEntryWritten={retained}");
                Assert.That(storage.LocalReads, Is.GreaterThan(0)); Assert.That(storage.CloudReads, Is.GreaterThan(0));
                Assert.That(retained, Is.True, "CloudWins selected cloud data, but the next write discarded that result.");
            }
            finally { manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); }
        }
        [UnityTest]
        public IEnumerator PostHardening_VerificationRollbackMustSurviveMetadataFilenameChange()
        {
            var manager = SaveManager.Instance; var settings = manager.SaveSettings;
            settings.saveFileName = "Slot{n}_{meta:profile}";
            settings.metadataFileNamePattern = "Slot{n}_{meta:profile}_Meta.bin";
            var slot = manager.GetSlotMetadata(1); slot.CustomMetadata["profile"] = "before";
            yield return Await(manager.SaveAsync(1));
            var root = manager.SaveSystem.GetSaveGamesPath();
            string oldPath = Directory.GetFiles(root, "*.sav", SearchOption.AllDirectories).Single();
            byte[] before = File.ReadAllBytes(oldPath);
            // Public storage protocol: backup, commit, verification failure, rollback.
            slot.CustomMetadata["profile"] = "after";
            settings.enableSaveFileVerification = true;
            manager.SaveSystem.BackupSlot(slot);
            manager.SaveSystem.Save(new byte[] { 0x7f }, slot);
            manager.SaveSystem.RestoreBackup(slot);
            byte[] recovered = manager.SaveSystem.Load(slot);
            TestContext.WriteLine($"OldFilenameExists={File.Exists(oldPath)}; recoveredLength={recovered?.Length}; originalLength={before.Length}; oldPayloadRestored={before.SequenceEqual(recovered ?? Array.Empty<byte>())}");
            Assert.That(recovered, Is.EqualTo(before), "Verification backup/rollback must resolve the old committed filename before removing it.");
        }
        [UnityTest]
        public IEnumerator PostHardening_ControlSuccessfulComponentSaveAndLoad()
        {
            var manager = SaveManager.Instance; var probe = new PostReviewSaveable();
            manager.ComponentManager.RegisterSaveableComponent(probe);
            try { yield return Await(manager.SaveAsync(1)); var load = manager.LoadSaveSlotAsync(1); yield return Await(load); Assert.That(load.Result.Success, Is.True); Assert.That(probe.Loads, Is.GreaterThan(0)); }
            finally { manager.ComponentManager.UnregisterSaveableComponent(probe); }
        }
        [UnityTest]
        public IEnumerator PostHardening_ControlVerificationRollbackWithoutFilenameChange()
        {
            var manager = SaveManager.Instance; yield return Await(manager.SaveAsync(1));
            var slot = manager.GetSlotMetadata(1); byte[] before = manager.SaveSystem.Load(slot);
            manager.SaveSettings.enableSaveFileVerification = true;
            manager.SaveSystem.BackupSlot(slot); manager.SaveSystem.Save(new byte[] { 0x7f }, slot); manager.SaveSystem.RestoreBackup(slot);
            Assert.That(manager.SaveSystem.Load(slot), Is.EqualTo(before));
        }
    }
    internal sealed class UnsupportedConflictStorage : ISaveSystem
    {
        readonly ISaveSystem inner; internal byte[] LocalRead, CloudRead, Written; internal int Writes; internal int LocalReads, CloudReads;
        internal UnsupportedConflictStorage(ISaveSystem inner) { this.inner = inner; }
        public Task SaveAsync(byte[] bytes, SaveSlot slot) { Writes++; Written = bytes; return Task.CompletedTask; }
        public Task<byte[]> LoadAsync(SaveSlot slot) { CloudReads++; return Task.FromResult(CloudRead); }
        public byte[] Load(SaveSlot slot) { LocalReads++; return LocalRead; }
        public void Save(byte[] bytes, SaveSlot slot) => inner.Save(bytes, slot);
        public Task SaveSlotMetadataAsync(SaveSlot slot) => Task.CompletedTask;
        public Task<SaveSlot> LoadSlotMetadataAsync(int number) => Task.FromResult<SaveSlot>(null);
        public Task DeleteAsync(SaveSlot slot) => inner.DeleteAsync(slot);
        public void Delete(SaveSlot slot) => inner.Delete(slot);
        public string GetSaveGamesPath() => inner.GetSaveGamesPath();
        public void BackupSlot(SaveSlot slot) => inner.BackupSlot(slot);
        public void RestoreBackup(SaveSlot slot) => inner.RestoreBackup(slot);
        public void DeleteBackup(SaveSlot slot) => inner.DeleteBackup(slot);
        public Task WriteTempAsync(byte[] bytes, SaveSlot slot) => inner.WriteTempAsync(bytes, slot);
        public Task<byte[]> LoadTempAsync(SaveSlot slot) => inner.LoadTempAsync(slot);
        public void DeleteTemp(SaveSlot slot) => inner.DeleteTemp(slot);
    }
    internal sealed class PostReviewSaveable : ISaveable
    {
        public string UniqueIdentifier => "postreview-object_postreview-component";
        internal bool ThrowSave; internal int Loads;
        public byte[] SaveData() { if (ThrowSave) throw new InvalidOperationException("POST_REVIEW_CAPTURE_FAILURE"); return new byte[] { 4, 5, 6 }; }
        public void LoadData(byte[] bytes) { Loads++; }
    }
    internal sealed class PostReviewStorage : ISaveSystem, ISaveConflictSource
    {
        internal Task<SaveConflictCandidates> CandidateGate;
        readonly ISaveSystem inner; internal byte[] LocalRead, CloudRead, Written; internal int LocalReads, CloudReads;
        internal PostReviewStorage(ISaveSystem inner) { this.inner = inner; }
        public Task<SaveConflictCandidates> ReadCandidatesAsync(SaveSlot slot, System.Threading.CancellationToken token)
        { token.ThrowIfCancellationRequested(); LocalReads++; CloudReads++; return CandidateGate ?? Task.FromResult(new SaveConflictCandidates(LocalRead == null ? null : new SaveConflictCandidate(LocalRead), CloudRead == null ? null : new SaveConflictCandidate(CloudRead))); }
        public Task SaveAsync(byte[] bytes, SaveSlot slot) { Written = bytes; return Task.CompletedTask; }
        public Task<byte[]> LoadAsync(SaveSlot slot) { CloudReads++; return Task.FromResult(CloudRead); }
        public byte[] Load(SaveSlot slot) { LocalReads++; return LocalRead; }
        public void Save(byte[] bytes, SaveSlot slot) => inner.Save(bytes, slot);
        public Task SaveSlotMetadataAsync(SaveSlot slot) => Task.CompletedTask;
        public Task<SaveSlot> LoadSlotMetadataAsync(int number) => Task.FromResult<SaveSlot>(null);
        public Task DeleteAsync(SaveSlot slot) => inner.DeleteAsync(slot);
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
