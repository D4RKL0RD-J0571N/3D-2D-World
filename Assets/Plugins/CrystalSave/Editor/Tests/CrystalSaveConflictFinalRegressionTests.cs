#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator ConflictFinal_FilteredRequestRetainsExplicitName() => ConflictFinalName(true);
        [UnityTest] public IEnumerator ConflictFinal_NormalRequestNameControl() => ConflictFinalName(false);

        IEnumerator ConflictFinalName(bool filtered)
        {
            using var fixture = new ConflictFinalFixture();
            fixture.Settings.autoResolveConflicts = true;
            fixture.Settings.autoConflictPolicy = AutoConflictPolicy.CloudWins;
            var live = fixture.Manager.CurrentSaveData;
            if (filtered)
            {
                var request = fixture.Manager.SaveSlotWithFilterAsync(1, _ => true, slotName: "Explicit request");
                yield return Await(request);
                Assert.That(request.Result, Is.True);
            }
            else yield return Await(fixture.Manager.SaveAsync(1, null, "Explicit request"));
            Assert.That(fixture.Storage.Written, Is.Not.Null);
            Assert.That(fixture.Manager.GetSlotMetadata(1).SlotName, Is.EqualTo("Explicit request"));
            Assert.That(fixture.Manager.Serializer.Deserialize<SaveData>(fixture.Storage.Written).LastSaved, Is.EqualTo(fixture.Remote.LastSaved));
            Assert.That(fixture.Manager.CurrentSaveData, Is.SameAs(live));
        }

        [UnityTest]
        public IEnumerator ConflictFinal_CancelledFilteredRequestCannotPublishNameOrWrite()
        {
            using var fixture = new ConflictFinalFixture();
            using var cancellation = new CancellationTokenSource();
            var gate = new TaskCompletionSource<SaveConflictCandidates>();
            fixture.Storage.CandidateGate = gate.Task;
            string before = fixture.Manager.GetSlotMetadata(1).SlotName;
            var request = fixture.Manager.SaveSlotWithFilterAsync(1, _ => true, slotName: "Rejected name", ct: cancellation.Token);
            yield return Until(() => fixture.Storage.CloudReads != 0, "filtered candidate read");
            cancellation.Cancel(); yield return Await(request);
            Assert.That(request.Result, Is.False);
            gate.TrySetResult(fixture.Candidates);
            yield return null; yield return null;
            Assert.That(fixture.Storage.Written, Is.Null);
            Assert.That(fixture.Manager.GetSlotMetadata(1).SlotName, Is.EqualTo(before));
        }

        [UnityTest] public IEnumerator ConflictFinal_HistoryMergePersistsSelectedFields() => ConflictFinalHistoryChoice("Merge");
        [UnityTest] public IEnumerator ConflictFinal_HistoryLocalControl() => ConflictFinalHistoryChoice("Keep Local");
        [UnityTest] public IEnumerator ConflictFinal_HistoryRemoteControl() => ConflictFinalHistoryChoice("Accept Remote");

        IEnumerator ConflictFinalHistoryChoice(string choice)
        {
            using var fixture = new ConflictFinalFixture();
            fixture.Settings.autoResolveConflicts = false;
            var service = Get<SaveOperationService>(fixture.Manager, "saveOperationService");
            var oldResolver = Get<LiveConflictResolver>(service, "conflictResolver");
            var resolverObject = new GameObject("History conflict resolver");
            var resolver = resolverObject.AddComponent<LiveConflictResolver>();
            var canvas = new GameObject("History conflict canvas", typeof(Canvas)); canvas.SetActive(false);
            resolver.OverlayCanvas = canvas.GetComponent<Canvas>();
            Set(service, "conflictResolver", resolver);
            using var context = CreateOperationContext();
            try
            {
                var prepare = (Task<SaveConflictCandidate>)typeof(SaveOperationService)
                    .GetMethod("ResolveHistoryCandidateAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(service, new object[] { context, fixture.Manager.GetSlotMetadata(1) });
                yield return Until(() => canvas.activeSelf || prepare.IsCompleted, "history conflict prompt");
                Assert.That(prepare.IsCompleted, Is.False);
                canvas.GetComponentsInChildren<UnityEngine.UI.Button>().Single(button => button.name == choice).onClick.Invoke();
                yield return Await(prepare);
                var selected = fixture.Manager.Serializer.Deserialize<SaveData>(prepare.Result.Payload);
                bool remoteFields = choice != "Keep Local";
                Assert.That(selected.ComponentsData.Keys, Is.EquivalentTo(new[] { remoteFields ? "remote_id" : "local_id" }));
                Assert.That(selected.LastSaved, Is.EqualTo(remoteFields ? fixture.Remote.LastSaved : fixture.Local.LastSaved));
                Assert.That(prepare.Result.Metadata.LastSaved, Is.EqualTo(selected.LastSaved), "Rotated payload and metadata describe the same selection");
                if (choice == "Merge")
                {
                    Assert.That(selected.ScreenshotFileName, Is.Null);
                    Assert.That(prepare.Result.Metadata.ScreenshotFileName, Is.Null);
                }
                else Assert.That(prepare.Result.Payload, Is.EqualTo(remoteFields ? fixture.Candidates.Remote.Payload : fixture.Candidates.Local.Payload));
                Assert.That(fixture.Storage.Written, Is.Null, "Preparing history cannot write before the batch commit");
                Assert.That(canvas.activeSelf, Is.False);
            }
            finally
            {
                Set(service, "conflictResolver", oldResolver);
                Object.Destroy(canvas); Object.Destroy(resolverObject);
            }
        }

        sealed class ConflictFinalFixture : IDisposable
        {
            internal readonly SaveManager Manager = SaveManager.Instance;
            internal SaveSettings Settings => Manager.SaveSettings;
            internal readonly SaveData Local, Remote;
            internal readonly SaveConflictCandidates Candidates;
            internal readonly PostReviewStorage Storage;
            readonly ISaveSystem previous;
            readonly bool cloud, automatic, encryption, compression, verify;
            readonly SaveBackend backend;
            readonly AutoConflictPolicy policy;
            internal ConflictFinalFixture()
            {
                previous = Manager.SaveSystem;
                cloud = Settings.enableCloudSave; automatic = Settings.autoResolveConflicts;
                encryption = Settings.enableEncryption; compression = Settings.enableCompression; verify = Settings.enableSaveFileVerification;
                backend = Settings.backend; policy = Settings.autoConflictPolicy;
                Local = new SaveData(Settings.version) { LastSaved = new DateTime(2024, 1, 1), ScreenshotFileName = "local.png" };
                Remote = new SaveData(Settings.version) { LastSaved = new DateTime(2025, 1, 1), ScreenshotFileName = "remote.png" };
                Local.ComponentsData["local_id"] = new byte[] { 1 };
                Remote.ComponentsData["remote_id"] = new byte[] { 2 };
                Candidates = new SaveConflictCandidates(
                    new SaveConflictCandidate(Manager.Serializer.Serialize(Local), new SaveSlot(1, "Local", Local.LastSaved, "local.png", "")),
                    new SaveConflictCandidate(Manager.Serializer.Serialize(Remote), new SaveSlot(1, "Remote", Remote.LastSaved, "remote.png", "")));
                Storage = new PostReviewStorage(previous) { CandidateGate = Task.FromResult(Candidates) };
                Settings.enableCloudSave = true; Settings.backend = SaveBackend.MySQL;
                Settings.enableEncryption = Settings.enableCompression = Settings.enableSaveFileVerification = false;
                Set(Manager, "saveSystem", Storage);
            }
            public void Dispose()
            {
                Set(Manager, "saveSystem", previous);
                Settings.enableCloudSave = cloud; Settings.autoResolveConflicts = automatic;
                Settings.enableEncryption = encryption; Settings.enableCompression = compression; Settings.enableSaveFileVerification = verify;
                Settings.backend = backend; Settings.autoConflictPolicy = policy;
            }
        }
    }
}
#endif
