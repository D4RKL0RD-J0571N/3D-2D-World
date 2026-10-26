#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;
namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator FollowupRegression_ClampedSliderAndToggleResavesMatchAppliedState()
        {
            var manager = SaveManager.Instance;
            var go = new GameObject("CachedSlider", typeof(RectTransform), typeof(Slider));
            var toggleObject = new GameObject("CachedToggle", typeof(RectTransform), typeof(Toggle));
            Composite(go, typeof(RememberUISlider)); Composite(toggleObject, typeof(RememberUIToggle));
            var slider = go.GetComponent<Slider>(); var toggle = toggleObject.GetComponent<Toggle>();
            var sliderHelper = go.GetComponent<RememberUISlider>(); var toggleHelper = toggleObject.GetComponent<RememberUIToggle>();
            Set(sliderHelper, "skipSavingWhenUnchanged", true); Set(toggleHelper, "skipSavingWhenUnchanged", true);
            try
            {
                yield return null;
                slider.value = 0.75f; toggle.isOn = true;
                yield return Await(manager.SaveImmediateAsync(1));
                slider.value = 0.9f; toggle.isOn = false;
                yield return Await(manager.SaveImmediateAsync(2));
                slider.maxValue = 0.3f;
                var load = manager.LoadSaveSlotAsync(1); yield return Await(load); Assert.That(load.Result.Success, Is.True);
                Assert.That(slider.value, Is.EqualTo(0.3f).Within(0.001f)); Assert.That(toggle.isOn, Is.True);
                yield return Await(manager.SaveImmediateAsync(3));
                var data = manager.Serializer.Deserialize<SaveData>(manager.SaveSystem.Load(manager.GetSlotMetadata(3)));
                Assert.That(manager.Serializer.Deserialize<RememberUISliderData>(data.ComponentsData[sliderHelper.UniqueIdentifier]).Value, Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(manager.Serializer.Deserialize<RememberUIToggleData>(data.ComponentsData[toggleHelper.UniqueIdentifier]).IsOn, Is.True);
            }
            finally { Object.Destroy(go); Object.Destroy(toggleObject); }
        }

        [UnityTest]
        public IEnumerator FollowupRegression_DeletedPrefabChildSurvivesLoadAndEarlierSnapshotRecreatesIt()
        {
            var manager = SaveManager.Instance; var prefab = Spawn();
            prefab.TrackChildStateOverrides = true;
            prefab.SetOriginalPrefabAsset(Resources.Load<GameObject>("CrystalSaveLifecyclePrefab"));
            Call(prefab, "AttachDestroyObserversRecursively", prefab.transform);
            var child = prefab.transform.Find("State"); child.localPosition = new Vector3(3,4,5);
            try
            {
                yield return null; yield return Await(manager.SaveImmediateAsync(1));
                Object.Destroy(child.gameObject); yield return null;
                yield return Await(manager.SaveImmediateAsync(2));
                var deleted = manager.LoadSaveSlotAsync(2); yield return Await(deleted); Assert.That(deleted.Result.Success, Is.True, deleted.Result.ErrorMessage);
                yield return Await(manager.SaveImmediateAsync(2));
                var data = manager.Serializer.Deserialize<SaveData>(manager.SaveSystem.Load(manager.GetSlotMetadata(2)));
                var mods = manager.Serializer.Deserialize<RuntimeModificationData>(data.Prefabs.Single(p => p.InstanceID == prefab.UniqueID).RuntimeModificationData);
                Assert.That(mods.ChildStates.Any(c => c.Path == "State" && !c.Exists), Is.True);
                var earlier = manager.LoadSaveSlotAsync(1); yield return Await(earlier); Assert.That(earlier.Result.Success, Is.True, earlier.Result.ErrorMessage);
                child = prefab.transform.Find("State"); Assert.That(child, Is.Not.Null);
                Assert.That(Vector3.Distance(child.localPosition, new Vector3(3,4,5)), Is.LessThan(0.001f));
                yield return Await(manager.SaveImmediateAsync(3));
                data = manager.Serializer.Deserialize<SaveData>(manager.SaveSystem.Load(manager.GetSlotMetadata(3)));
                mods = manager.Serializer.Deserialize<RuntimeModificationData>(data.Prefabs.Single(p => p.InstanceID == prefab.UniqueID).RuntimeModificationData);
                Assert.That(mods.ChildStates.Any(c => c.Path == "State" && !c.Exists), Is.False);
                Object.Destroy(child.gameObject); yield return null;
                yield return Await(manager.SaveImmediateAsync(3));
                data = manager.Serializer.Deserialize<SaveData>(manager.SaveSystem.Load(manager.GetSlotMetadata(3)));
                mods = manager.Serializer.Deserialize<RuntimeModificationData>(data.Prefabs.Single(p => p.InstanceID == prefab.UniqueID).RuntimeModificationData);
                Assert.That(mods.ChildStates.Any(c => c.Path == "State" && !c.Exists), Is.True, "Recreated children retain destroy observers");
            }
            finally { if (prefab) Object.Destroy(prefab.gameObject); }
        }

        [UnityTest]
        public IEnumerator FollowupRegression_TargetedRoutesFailAndHealthyDelayedWorkFinishes()
        {
            var manager = SaveManager.Instance;
            var go = new GameObject("TargetedRoutes"); Composite(go, typeof(FeatureProbe), typeof(RememberTransform)); ConfigureTransform(go);
            var probe = go.GetComponent<FeatureProbe>(); probe.Value = 7; go.transform.localPosition = new Vector3(3,4,5);
            int successes = 0; Action<GameObject> listener = _ => successes++; manager.OnSingleGameObjectRestored += listener;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return null; yield return Await(manager.SaveImmediateAsync(1));
                probe.Loaded = () => throw new InvalidOperationException("TARGETED_FAILURE");
                string id = go.GetComponent<UniqueID>().ID;
                foreach (string route in new[] { "object", "id", "slot", "supplied", "task" })
                {
                    go.transform.localPosition = Vector3.one * 99;
                    System.Threading.Tasks.Task task;
                    switch (route)
                    {
                        case "object": task = manager.RestoreSingleGameObjectFromCurrentDataAsync(go, 1, 0); break;
                        case "id": task = manager.RestoreSingleGameObjectFromCurrentDataAsync(id, 1, 0); break;
                        case "slot": task = manager.RestoreSingleGameObjectWithRetryAsync(go, 1, 1, 0); break;
                        case "supplied": task = manager.RestoreSingleGameObjectAsync(go, manager.CurrentSaveData); break;
                        default: task = manager.RestoreSingleGameObjectAsync(id, 1); break;
                    }
                    yield return Until(() => task.IsCompleted, "targeted " + route);
                    if (task is System.Threading.Tasks.Task<bool> boolean) Assert.That(boolean.Result, Is.False, route);
                    else { Assert.That(task.IsFaulted, Is.True, route); _ = task.Exception; }
                    Assert.That(Vector3.Distance(go.transform.localPosition, new Vector3(3,4,5)), Is.LessThan(0.001f), "Healthy delayed component " + route);
                    Assert.That(successes, Is.Zero);
                }
                probe.Loaded = null;
                go.transform.localPosition = Vector3.one * 99;
                var healthy = manager.RestoreSingleGameObjectFromCurrentDataAsync(go, 1, 0);
                yield return Await(healthy); Assert.That(healthy.Result, Is.True); Assert.That(successes, Is.EqualTo(1));
                Assert.That(Vector3.Distance(go.transform.localPosition, new Vector3(3,4,5)), Is.LessThan(0.001f));
            }
            finally { manager.OnSingleGameObjectRestored -= listener; Object.Destroy(go); LogAssert.ignoreFailingMessages = false; }
        }

        [UnityTest]
        public IEnumerator FollowupRegression_TargetedRestoreWaitsBehindSaveAndAwaitsPrefabInstantiation()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var helper = SceneComponent("QueuedTarget"); helper.transform.localPosition = new Vector3(8,9,10);
            var prefab = Spawn(); string prefabID = prefab.UniqueID;
            yield return null; yield return Await(manager.SaveImmediateAsync(1));
            helper.transform.localPosition = Vector3.one * 99;
            var gate = new System.Threading.Tasks.TaskCompletionSource<SaveConflictCandidates>();
            var storage = new PostReviewStorage(original) { CandidateGate = gate.Task };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL; Set(manager, "saveSystem", storage);
            using var cancellation = new System.Threading.CancellationTokenSource();
            try
            {
                var save = manager.SaveAsync(1, null, cancellation.Token);
                yield return Until(() => storage.LocalReads > 0, "save owns coordinator");
                var targeted = manager.RestoreSingleGameObjectFromCurrentDataAsync(helper.gameObject, 1, 0);
                yield return null; yield return null;
                Assert.That(targeted.IsCompleted, Is.False); Assert.That(helper.transform.localPosition.x, Is.EqualTo(99));
                cancellation.Cancel(); yield return Until(() => save.IsCompleted, "save cancellation"); Assert.That(save.IsCanceled, Is.True);
                gate.SetResult(new SaveConflictCandidates(null, null));
                yield return Await(targeted); Assert.That(targeted.Result, Is.True);
                Assert.That(Vector3.Distance(helper.transform.localPosition, new Vector3(8,9,10)), Is.LessThan(0.001f));
                manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original);
                Object.Destroy(prefab.gameObject); yield return null;
                var restored = manager.RestoreSingleGameObjectAsync(prefabID, 1); yield return Await(restored);
                var live = manager.FindGameObjectByUniqueID(prefabID); Assert.That(live, Is.Not.Null);
                Assert.That(live.GetComponentInChildren<RememberTransform>(), Is.Not.Null);
                Object.Destroy(live);
            }
            finally { manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); Object.Destroy(helper.gameObject); if (prefab) Object.Destroy(prefab.gameObject); }
        }

        [UnityTest]
        public IEnumerator FollowupRegression_ConflictRetainsOnlyRoutingAndExplicitMetadataOverrides()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            manager.GetSlotMetadata(1).CustomMetadata["profile"] = "local-route";
            manager.GetSlotMetadata(1).CustomMetadata["world"] = "requested-world";
            manager.GetSlotMetadata(1).CustomMetadata["score"] = "10";
            manager.GetSlotMetadata(1).LastSaved = new DateTime(2024,1,1);
            manager.SaveSettings.saveFileName = "Slot{n}_{meta:profile}";
#if !UNITY_LOCALIZATION
            var defaults = ScriptableObject.CreateInstance<SaveSlotMetadataSO>();
            defaults.entries.Add(new SaveSlotMetadataSO.Entry { key = "score", value = "999" });
            defaults.entries.Add(new SaveSlotMetadataSO.Entry { key = "world", value = "default-world" });
            manager.SaveSettings.defaultSlotMetadata = defaults;
#endif
            var local = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2024,1,1) };
            var remote = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2025,1,1) };
            var localMeta = new SaveSlot(1, "Local", local.LastSaved, "", ""); localMeta.CustomMetadata["score"] = "10";
            var remoteMeta = new SaveSlot(1, "Remote", remote.LastSaved, "", "");
            remoteMeta.CustomMetadata["profile"] = "remote-route"; remoteMeta.CustomMetadata["world"] = "remote-world"; remoteMeta.CustomMetadata["score"] = "100";
            var storage = new PostReviewStorage(original) { CandidateGate = System.Threading.Tasks.Task.FromResult(new SaveConflictCandidates(
                new SaveConflictCandidate(manager.Serializer.Serialize(local), localMeta), new SaveConflictCandidate(manager.Serializer.Serialize(remote), remoteMeta))) };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL;
            manager.SaveSettings.autoResolveConflicts = true; manager.SaveSettings.autoConflictPolicy = AutoConflictPolicy.CloudWins; Set(manager, "saveSystem", storage);
            try
            {
                var save = manager.SaveByMetadataAsync("world", "requested-world", customSlotName: "Explicit name"); yield return Await(save);
                var meta = manager.GetSlotMetadata(save.Result);
                Assert.That(meta.CustomMetadata["profile"], Is.EqualTo("local-route"));
                Assert.That(meta.CustomMetadata["world"], Is.EqualTo("requested-world"));
                Assert.That(meta.CustomMetadata["score"], Is.EqualTo("100"));
                Assert.That(meta.SlotName, Is.EqualTo("Explicit name"));
            }
            finally
            {
                manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original);
#if !UNITY_LOCALIZATION
                manager.SaveSettings.defaultSlotMetadata = null; Object.Destroy(defaults);
#endif
            }
        }
        [UnityTest]
        public IEnumerator FollowupRegression_CachedRigidbodyResaveKeepsLoadedMass()
        {
            var manager = SaveManager.Instance;
            var go = new GameObject("ReviewRigidbody", typeof(Rigidbody));
            var body = go.GetComponent<Rigidbody>(); body.useGravity = false;
            Composite(go, typeof(RememberRigidbody));
            var helper = go.GetComponent<RememberRigidbody>();
            foreach (string field in new[] { "rememberIsKinematic", "rememberUseGravity", "rememberDrag", "rememberAngularDrag", "rememberConstraints", "rememberVelocity", "rememberAngularVelocity", "rememberDetectCollisions" }) Set(helper, field, false);
            Set(helper, "skipSavingWhenUnchanged", true);
            try
            {
                yield return null;
                body.mass = 10;
                yield return Await(manager.SaveImmediateAsync(1));
                body.mass = 20;
                yield return Await(manager.SaveImmediateAsync(2));
                var load = manager.LoadSaveSlotAsync(1);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                Assert.That(body.mass, Is.EqualTo(10), "Load control");
                yield return Await(manager.SaveImmediateAsync(3));
                var data = manager.Serializer.Deserialize<SaveData>(manager.SaveSystem.Load(manager.GetSlotMetadata(3)));
                var written = manager.Serializer.Deserialize<RememberRigidbodyData>(data.ComponentsData[helper.UniqueIdentifier]);
                Assert.That(written.Mass, Is.EqualTo(10), "Resave must contain the loaded/live mass");
            }
            finally { Object.Destroy(go); }
        }

        [UnityTest]
        public IEnumerator FollowupRegression_CloudWinsKeepsSelectedGameplayMetadata()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            manager.GetSlotMetadata(1).CustomMetadata["score"] = "10";
            var local = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2024, 1, 1) };
            var remote = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2025, 1, 1) };
            local.ComponentsData["local_id"] = new byte[] {1}; remote.ComponentsData["remote_id"] = new byte[] {2};
            var localMeta = new SaveSlot(1, "Local", local.LastSaved, "", ""); localMeta.CustomMetadata["score"] = "10";
            var remoteMeta = new SaveSlot(1, "Remote", remote.LastSaved, "", ""); remoteMeta.CustomMetadata["score"] = "100";
            var storage = new PostReviewStorage(original) { CandidateGate = System.Threading.Tasks.Task.FromResult(new SaveConflictCandidates(
                new SaveConflictCandidate(manager.Serializer.Serialize(local), localMeta),
                new SaveConflictCandidate(manager.Serializer.Serialize(remote), remoteMeta))) };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL;
            manager.SaveSettings.autoResolveConflicts = true; manager.SaveSettings.autoConflictPolicy = AutoConflictPolicy.CloudWins;
            Set(manager, "saveSystem", storage);
            try
            {
                yield return Await(manager.SaveImmediateAsync(1));
                var saved = manager.Serializer.Deserialize<SaveData>(storage.Written);
                Assert.That(saved.ComponentsData.ContainsKey("remote_id"), Is.True, "Control: remote gameplay selected");
                Assert.That(manager.GetSlotMetadata(1).SlotName, Is.EqualTo("Remote"));
                Assert.That(manager.GetSlotMetadata(1).CustomMetadata["score"], Is.EqualTo("100"),
                    "An ordinary save with no metadata request must retain the selected candidate's gameplay metadata");
            }
            finally { manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); }
        }

        [UnityTest] public IEnumerator FollowupRegression_CachedSliderResaveKeepsLoadedValue() => ReviewSliderResave(true);
        [UnityTest] public IEnumerator FollowupRegression_UncachedSliderResaveControl() => ReviewSliderResave(false);
        IEnumerator ReviewSliderResave(bool cache)
        {
            var manager = SaveManager.Instance;
            var go = new GameObject("ReviewSlider", typeof(RectTransform), typeof(Slider));
            Composite(go, typeof(RememberUISlider));
            var helper = go.GetComponent<RememberUISlider>();
            var slider = go.GetComponent<Slider>();
            Set(helper, "skipSavingWhenUnchanged", cache);
            try
            {
                yield return null;
                slider.value = 0.25f;
                yield return Await(manager.SaveImmediateAsync(1));
                slider.value = 0.75f;
                yield return Await(manager.SaveImmediateAsync(2));
                var load = manager.LoadSaveSlotAsync(1);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                Assert.That(slider.value, Is.EqualTo(0.25f).Within(0.001f), "Load control");
                yield return Await(manager.SaveImmediateAsync(3));
                var data = manager.Serializer.Deserialize<SaveData>(manager.SaveSystem.Load(manager.GetSlotMetadata(3)));
                var written = manager.Serializer.Deserialize<RememberUISliderData>(data.ComponentsData[helper.UniqueIdentifier]);
                Assert.That(written.Value, Is.EqualTo(0.25f).Within(0.001f), "Resave must contain the loaded/live value");
            }
            finally { Object.Destroy(go); }
        }

        [UnityTest] public IEnumerator FollowupRegression_RejectedCaptureRetainsChildDeletion() => ReviewChildDeletion(true, false);
        [UnityTest] public IEnumerator FollowupRegression_RepeatedSaveRetainsChildDeletion() => ReviewChildDeletion(false, true);
        [UnityTest] public IEnumerator FollowupRegression_FirstSaveChildDeletionControl() => ReviewChildDeletion(false, false);
        IEnumerator ReviewChildDeletion(bool reject, bool saveTwice)
        {
            var manager = SaveManager.Instance;
            var prefab = Spawn();
            prefab.TrackChildStateOverrides = true;
            var failure = new PostReviewSaveable();
            manager.ComponentManager.RegisterSaveableComponent(failure);
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return null;
                yield return Await(manager.SaveImmediateAsync(1));
                Object.Destroy(prefab.transform.Find("State").gameObject);
                yield return null;
                // Same public notification used by the shipped DestroyObserver.
                prefab.NotifyChildDestroyed("State");
                if (reject)
                {
                    failure.ThrowSave = true;
                    var failed = manager.SaveImmediateAsync(1);
                    yield return Until(() => failed.IsCompleted, "review rejected save");
                    Assert.That(failed.IsFaulted, Is.True);
                    _ = failed.Exception;
                    failure.ThrowSave = false;
                }
                yield return Await(manager.SaveImmediateAsync(1));
                if (saveTwice) yield return Await(manager.SaveImmediateAsync(1));
                var data = manager.Serializer.Deserialize<SaveData>(manager.SaveSystem.Load(manager.GetSlotMetadata(1)));
                var savedPrefab = data.Prefabs.Single(p => p.InstanceID == prefab.UniqueID);
                var mods = manager.Serializer.Deserialize<RuntimeModificationData>(savedPrefab.RuntimeModificationData);
                Assert.That(mods?.ChildStates.Any(child => child.Path == "State" && !child.Exists), Is.True,
                    "The saved prefab must retain the removed authored child's tombstone");
            }
            finally { manager.ComponentManager.UnregisterSaveableComponent(failure); Object.Destroy(prefab.gameObject); LogAssert.ignoreFailingMessages = false; }
        }

        [UnityTest] public IEnumerator FollowupRegression_TargetedRestoreReportsComponentFailure() => ReviewTargetedRestore(true);
        [UnityTest] public IEnumerator FollowupRegression_TargetedRestoreHealthyControl() => ReviewTargetedRestore(false);
        IEnumerator ReviewTargetedRestore(bool fail)
        {
            var manager = SaveManager.Instance;
            var go = new GameObject("ReviewTargeted"); Composite(go, typeof(FeatureProbe));
            var helper = go.GetComponent<FeatureProbe>(); helper.Value = 23;
            int notifications = 0;
            Action<GameObject> listener = _ => notifications++;
            manager.OnSingleGameObjectRestored += listener;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return null;
                yield return Await(manager.SaveImmediateAsync(1));
                helper.Value = 99;
                if (fail) helper.Loaded = () => throw new InvalidOperationException("REVIEW_REQUIRED_RESTORE_FAILURE");
                var restore = manager.RestoreSingleGameObjectFromCurrentDataAsync(go, 1, 1);
                yield return Await(restore);
                TestContext.WriteLine("targeted restore: result=" + restore.Result + ", successNotifications=" + notifications);
                if (fail)
                {
                    Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(helper.UniqueIdentifier), Is.True,
                        "Control: the component failure was detected and its bytes retained");
                    Assert.That(restore.Result, Is.False, "Targeted restore must propagate a required component failure");
                    Assert.That(notifications, Is.Zero);
                }
                else { Assert.That(restore.Result, Is.True); Assert.That(helper.Value, Is.EqualTo(23)); Assert.That(notifications, Is.EqualTo(1)); }
            }
            finally { manager.OnSingleGameObjectRestored -= listener; Object.Destroy(go); LogAssert.ignoreFailingMessages = false; }
        }
    }
}
#endif
