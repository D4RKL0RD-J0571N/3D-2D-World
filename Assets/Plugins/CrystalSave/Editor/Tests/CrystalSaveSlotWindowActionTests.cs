#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Arawn.CrystalSave.Editor;
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
        // Invoke the actual handlers used by the buttons, with the real manager/coordinator.
        // Only storage completion is held; no window ownership helper completes an operation.
        [UnityTest]
        public IEnumerator SlotWindowActionsAwaitRealLoadAndDisposeTheirOwners()
        {
            foreach (string route in SlotWindowRoutes)
                yield return ExerciseSlotWindowAction(route, "success");
        }

        [UnityTest]
        public IEnumerator SlotWindowActionsTimeoutWithoutApplyingLatePayloads()
        {
            foreach (string route in SlotWindowRoutes)
                yield return ExerciseSlotWindowAction(route, "timeout");
        }

        [UnityTest]
        public IEnumerator SlotWindowActionsAllowManualCancellationWithTimeoutDisabled()
        {
            foreach (string route in SlotWindowRoutes)
                yield return ExerciseSlotWindowAction(route, "cancel");
        }

        [UnityTest]
        public IEnumerator SlotWindowActionsCannotRetireLoadStartedByCompletionListener()
        {
            foreach (string route in SlotWindowRoutes)
                yield return ExerciseSlotWindowAction(route, "replacement");
        }

        private static readonly string[] SlotWindowRoutes =
            { "automatic-local", "automatic-cloud", "explicit-async", "metadata" };

        private IEnumerator ExerciseSlotWindowAction(string route, string outcome)
        {
            var manager = SaveManager.Instance;
            var original = manager.SaveSystem;
            var helper = SceneComponent("Window load " + route);
            Vector3 saved = new Vector3(3, 5, 7), changed = new Vector3(11, 13, 17);
            helper.transform.position = saved;
            yield return null;
            yield return Await(manager.SaveAsync(1));
            yield return Await(manager.SaveAsync(2));
            foreach (int number in new[] { 1, 2 })
            {
                var slot = manager.GetSaveSlotByNumber(number);
                slot.CustomMetadata["ci-window"] = number.ToString();
                yield return Await(original.SaveSlotMetadataAsync(slot));
            }
            helper.transform.position = changed;
            var previous = manager.CurrentSaveData;
            var storage = new SlotWindowReadStorage(original);
            bool previousCloud = manager.SaveSettings.enableCloudSave;
            SaveBackend previousBackend = manager.SaveSettings.backend;
            Set(manager, "saveSystem", storage);
            // Choosing a cloud backend selects the automatic handler's async branch. The
            // test-owned storage still reads only this fixture's local persisted bytes.
            manager.SaveSettings.backend = route == "automatic-cloud" ? SaveBackend.MySQL : SaveBackend.UnityCloudSave;
            manager.SaveSettings.enableCloudSave = false;
            var window = ScriptableObject.CreateInstance<SaveSlotManagerWindow>();
            Set(window, "enableLoadTimeout", outcome == "timeout");
            Set(window, "loadTimeoutSeconds", 1);
            Set(window, "restoreLastActiveScene", false);
            Set(window, "loadAsync", false);
            int completions = 0;
            CancellationTokenSource replacement = null;
            string replacementFeedback = null;
            EventHandler<SaveLoadEventArgs> completed = (_, args) =>
            {
                completions++;
                if (outcome != "replacement" || args.Slot.SlotNumber != 1) return;
                InvokeSlotWindowLoad(window, route, 2);
                replacement = (CancellationTokenSource)Get<object>(window, "loadCancellationTokenSource");
                replacementFeedback = (string)Get<object>(window, "feedbackMessage");
            };
            manager.OnLoadCompleted += completed;
            try
            {
                if ((outcome == "timeout" || outcome == "cancel") && route == "explicit-async")
                    LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                        "SaveSlotManagerWindow: Async load failed for slot 1:.*cancelled"));
                InvokeSlotWindowLoad(window, route, 1);
                var owner = (CancellationTokenSource)Get<object>(window, "loadCancellationTokenSource");
                Assert.That(owner, Is.Not.Null, route);
                yield return Until(() => storage.Reads.Count == 1, route + " actual read", 5);
                Assert.That(Get<object>(window, "isLoading"), Is.True, route);
                Assert.That(helper.transform.position, Is.EqualTo(changed));

                if (outcome == "cancel")
                {
                    // Longer than the configured seconds: disabling the timer must matter.
                    float until = Time.realtimeSinceStartup + 1.25f;
                    while (Time.realtimeSinceStartup < until) yield return null;
                    Assert.That(Get<object>(window, "isLoading"), Is.True, route + " disabled deadline");
                    Assert.That(owner.IsCancellationRequested, Is.False);
                    Call(window, "CancelOngoingLoad");
                }
                else if (outcome != "timeout") storage.Release(0);

                if (outcome == "replacement")
                {
                    yield return Until(() => storage.Reads.Count == 2, route + " replacement read", 5);
                    for (int frame = 0; frame < 10; frame++) yield return null;
                    Assert.That(completions, Is.EqualTo(1));
                    Assert.That(replacement, Is.Not.Null);
                    Assert.That(Get<object>(window, "loadCancellationTokenSource"), Is.SameAs(replacement));
                    Assert.That(Get<object>(window, "isLoading"), Is.True, route + " old continuation cleared replacement");
                    Assert.That(Get<object>(window, "loadingSlotNumber"), Is.EqualTo(route == "metadata" ? -1 : 2));
                    Assert.That(Get<object>(window, "feedbackMessage"), Is.EqualTo(replacementFeedback));
                    Assert.DoesNotThrow(() => _ = replacement.Token);
                    storage.Release(1);
                }

                yield return Until(() => !(bool)Get<object>(window, "isLoading"), route + " window completion", 5);
                Assert.That(Get<object>(window, "loadingSlotNumber"), Is.EqualTo(-1));
                Assert.That(Get<object>(window, "loadCancellationTokenSource"), Is.Null);
                Assert.Throws<ObjectDisposedException>(() => _ = owner.Token);
                if (outcome == "success" || outcome == "replacement")
                {
                    Assert.That(completions, Is.EqualTo(outcome == "replacement" ? 2 : 1));
                    Assert.That(helper.transform.position, Is.EqualTo(saved));
                    Assert.That(Get<object>(window, "feedbackMessageType"), Is.EqualTo(UnityEditor.MessageType.Info));
                    if (replacement != null) Assert.Throws<ObjectDisposedException>(() => _ = replacement.Token);
                }
                else
                {
                    Assert.That(completions, Is.Zero);
                    Assert.That(manager.CurrentSaveData, Is.SameAs(previous));
                    Assert.That(helper.transform.position, Is.EqualTo(changed));
                    string feedback = (string)Get<object>(window, "feedbackMessage");
                    Assert.That(feedback.ToLowerInvariant(), Does.Contain("cancel"));
                    storage.Release(0);
                    for (int frame = 0; frame < 10; frame++) yield return null;
                    Assert.That(manager.CurrentSaveData, Is.SameAs(previous));
                    Assert.That(helper.transform.position, Is.EqualTo(changed));
                    Assert.That(Get<object>(window, "feedbackMessage"), Is.EqualTo(feedback));
                }
            }
            finally
            {
                manager.OnLoadCompleted -= completed;
                Object.DestroyImmediate(window);
                storage.ReleaseAll();
                Set(manager, "saveSystem", original);
                manager.SaveSettings.backend = previousBackend;
                manager.SaveSettings.enableCloudSave = previousCloud;
                if (helper) Object.Destroy(helper.gameObject);
            }
            yield return null;
        }

        private static void InvokeSlotWindowLoad(SaveSlotManagerWindow window, string route, int number)
        {
            if (route == "metadata")
            {
                Set(window, "metadataKey", "ci-window");
                Set(window, "metadataValue", number.ToString());
                Call(window, "LoadByMetadataFromPanelAsync");
            }
            else Call(window, route == "explicit-async" ? "StartLoadOperationAsync" : "StartLoadOperation", number);
        }
    }

    internal sealed class SlotWindowReadStorage : ISaveSystem
    {
        readonly ISaveSystem inner;
        internal readonly List<TaskCompletionSource<byte[]>> Reads = new List<TaskCompletionSource<byte[]>>();
        readonly List<byte[]> payloads = new List<byte[]>();
        internal SlotWindowReadStorage(ISaveSystem inner) { this.inner = inner; }
        public Task<byte[]> LoadAsync(SaveSlot slot)
        {
            payloads.Add(inner.Load(slot));
            var gate = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            Reads.Add(gate);
            return gate.Task;
        }
        internal void Release(int index) => Reads[index].TrySetResult(payloads[index]);
        internal void ReleaseAll() { for (int i = 0; i < Reads.Count; i++) Release(i); }
        public Task SaveAsync(byte[] data, SaveSlot slot) => inner.SaveAsync(data, slot);
        public Task SaveSlotMetadataAsync(SaveSlot slot) => inner.SaveSlotMetadataAsync(slot);
        public Task<SaveSlot> LoadSlotMetadataAsync(int number) => inner.LoadSlotMetadataAsync(number);
        public Task DeleteAsync(SaveSlot slot) => inner.DeleteAsync(slot);
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
