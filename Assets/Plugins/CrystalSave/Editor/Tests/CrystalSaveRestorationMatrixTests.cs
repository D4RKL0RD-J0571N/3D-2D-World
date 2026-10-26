#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [UnityTest]
        public IEnumerator RestoreMatrix_TargetsUseInstanceIdentitiesAndRespectBothOwners()
        {
            var manager = SaveManager.Instance;
            foreach (bool blobs in new[] { false, true })
            {
                var first = Spawn(); var second = Spawn(); SaveablePrefab replacement = null;
                first.TrackComponentBlobs = blobs; second.TrackComponentBlobs = blobs;
                var a = first.transform.Find("State"); var b = second.transform.Find("State");
                a.localPosition = new Vector3(3, 4, 5); b.localPosition = new Vector3(6, 7, 8);
                try
                {
                    yield return null; yield return Await(manager.SaveImmediateAsync(1));
                    var snapshot = manager.CurrentSaveData;
                    a.localPosition = Vector3.one * 99; b.localPosition = Vector3.one * 88;
                    var child = manager.RestoreSingleGameObjectAsync(a.gameObject, snapshot);
                    yield return Await(child); Assert.That(child.Result, Is.True, "child request, blobs=" + blobs);
                    Assert.That(a.localPosition, Is.EqualTo(new Vector3(3, 4, 5)));
                    Assert.That(b.localPosition, Is.EqualTo(Vector3.one * 88), "Other instance must remain untouched");
                    var root = manager.RestoreSingleGameObjectFromCurrentDataAsync(second.UniqueID, 1, 0);
                    yield return Await(root); Assert.That(root.Result, Is.True);
                    Assert.That(b.localPosition, Is.EqualTo(new Vector3(6, 7, 8)));

                    // An explicit target with a new runtime ID can reuse the saved template,
                    // without pretending that the template asset ID is a component instance ID.
                    var one = manager.Serializer.Deserialize<SaveData>(manager.Serializer.Serialize(snapshot));
                    one.Prefabs.RemoveAll(p => p.InstanceID != first.UniqueID);
                    replacement = Spawn(); replacement.TrackComponentBlobs = blobs;
                    replacement.transform.Find("State").localPosition = Vector3.one * 77;
                    var fallback = manager.RestoreSingleGameObjectAsync(replacement.gameObject, one);
                    yield return Await(fallback); Assert.That(fallback.Result, Is.True);
                    Assert.That(replacement.transform.Find("State").localPosition, Is.EqualTo(new Vector3(3, 4, 5)));
                }
                finally { Object.Destroy(first.gameObject); Object.Destroy(second.gameObject); if (replacement) Object.Destroy(replacement.gameObject); }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RestoreMatrix_DestroyedBatchRetainsFailuresAndFinishesHealthyDelayedWork()
        {
            var manager = SaveManager.Instance;
            var failed = new GameObject("FailedDestroyed"); Composite(failed, typeof(RememberGameObject), typeof(FeatureProbe));
            var healthy = new GameObject("HealthyDestroyed"); Composite(healthy, typeof(RememberGameObject), typeof(RememberTransform)); ConfigureTransform(healthy);
            var probe = failed.GetComponent<FeatureProbe>(); probe.Value = 7;
            healthy.transform.localPosition = new Vector3(4, 5, 6);
            string failedID = failed.GetComponent<UniqueID>().ID, healthyID = healthy.GetComponent<UniqueID>().ID;
            var notifications = new List<string>(); Action<string> listener = id => notifications.Add(id);
            manager.OnGameObjectRestored += listener;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return null; yield return Await(manager.SaveImmediateAsync(1));
                foreach (var go in new[] { failed, healthy })
                {
                    manager.RegisterGameObject(go, go.GetComponent<RememberGameObject>().PropertySettings);
                    manager.DestroyWithSnapshot(go, destroy: false);
                }
                probe.Value = 99; healthy.transform.localPosition = Vector3.one * 99;
                probe.Loaded = () => throw new InvalidOperationException("Required destroyed component");
                var batch = manager.RestoreAllDestroyedGameObjectsFromCurrentDataWithRetryAsync(1, 0);
                yield return Await(batch); Assert.That(batch.Result, Is.False);
                Assert.That(manager.CurrentSaveData.DestroyedObjectData.ContainsKey(failedID), Is.True);
                Assert.That(manager.CurrentSaveData.DestroyedObjectData.ContainsKey(healthyID), Is.False);
                Assert.That(healthy.transform.localPosition, Is.EqualTo(new Vector3(4, 5, 6)));
                Assert.That(notifications, Is.EquivalentTo(new[] { healthyID }));
                Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(probe.UniqueIdentifier), Is.True);
                probe.Loaded = null; probe.Value = 99;
                var retry = manager.RestoreDestroyedGameObjectFromCurrentDataAsync(failedID, 1, 0);
                yield return Await(retry); Assert.That(retry.Result, Is.True); Assert.That(probe.Value, Is.EqualTo(7));
                Assert.That(manager.CurrentSaveData.DestroyedObjectData.ContainsKey(failedID), Is.False);
                Assert.That(notifications, Is.EquivalentTo(new[] { healthyID, failedID }));
                yield return Await(manager.SaveImmediateAsync(2));
            }
            finally { manager.OnGameObjectRestored -= listener; Object.Destroy(failed); Object.Destroy(healthy); LogAssert.ignoreFailingMessages = false; }
        }

        [UnityTest]
        public IEnumerator RestoreMatrix_DestroyedPrefabsAwaitInstantiationAndComponentApplication()
        {
            var manager = SaveManager.Instance;
            foreach (bool blobs in new[] { false, true })
            {
                var prefab = Spawn(); prefab.TrackComponentBlobs = blobs;
                string id = prefab.UniqueID; GameObject restored = null;
                var expected = new Vector3(7, 8, 9); prefab.transform.Find("State").localPosition = expected;
                try
                {
                    yield return null; yield return Await(manager.SaveImmediateAsync(1));
                    prefab.transform.Find("State").localPosition = Vector3.one * 99; // The requested slot must win over a later death snapshot.
                    manager.DestroyWithSnapshot(prefab.gameObject, allowPooling: false); yield return null;
                    var task = Get<PrefabRestoreService>(manager, "prefabRestoreService")
                        .RestoreDestroyedPrefabWithRetryAsync(id, 1, 1, 0);
                    yield return Await(task); Assert.That(task.Result, Is.True, "blobs=" + blobs);
                    restored = manager.FindGameObjectByUniqueID(id);
                    Assert.That(restored, Is.Not.Null);
                    Assert.That(restored.transform.Find("State").localPosition, Is.EqualTo(expected));
                    Assert.That(manager.IsGameObjectDestroyed(id), Is.False);
                }
                finally { if (prefab) Object.Destroy(prefab.gameObject); if (restored) Object.Destroy(restored); }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RestoreMatrix_DestroyedPrefabFailureCanRetryBothOwnershipModes()
        {
            var manager = SaveManager.Instance;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                foreach (bool blobs in new[] { false, true })
                {
                    var prefab = Spawn(); prefab.TrackComponentBlobs = blobs;
                    var probe = prefab.transform.Find("State").gameObject.AddComponent<FeatureProbe>(); probe.Value = 7;
                    string id = prefab.UniqueID;
                    try
                    {
                        yield return null; yield return Await(manager.SaveImmediateAsync(1));
                        manager.DestroyWithSnapshot(prefab.gameObject, destroy: false);
                        probe.Value = 99; probe.Loaded = () => throw new InvalidOperationException("Required prefab restore");
                        var failed = manager.RestoreDestroyedPrefabAsync(id);
                        yield return Await(failed); Assert.That(failed.Result, Is.False, "blobs=" + blobs);
                        Assert.That(manager.CurrentSaveData.DestroyedObjectData.ContainsKey(id), Is.True);
                        Assert.That(manager.IsGameObjectDestroyed(id), Is.True);
                        probe.Loaded = null; probe.Value = 99;
                        var retry = manager.RestoreDestroyedPrefabAsync(id);
                        yield return Await(retry); Assert.That(retry.Result, Is.True); Assert.That(probe.Value, Is.EqualTo(7));
                        Assert.That(manager.IsGameObjectDestroyed(id), Is.False);
                    }
                    finally { Object.Destroy(prefab.gameObject); }
                    yield return null;
                }
            }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        [UnityTest]
        public IEnumerator RestoreMatrix_TargetedInstantiationDoesNotApplyOtherDestroyedEntries()
        {
            var manager = SaveManager.Instance; var target = Spawn(); var unrelated = Spawn();
            string id = target.UniqueID; GameObject restored = null;
            try
            {
                yield return null; yield return Await(manager.SaveImmediateAsync(1));
                manager.CurrentSaveData.DestroyedGameObjects.Add(unrelated.UniqueID);
                Object.Destroy(target.gameObject); yield return null;
                var request = manager.RestoreSingleGameObjectFromCurrentDataAsync(id, 1, 0);
                yield return Await(request); Assert.That(request.Result, Is.True);
                restored = manager.FindGameObjectByUniqueID(id);
                Assert.That(unrelated != null, Is.True, "A targeted request cannot destroy another live instance");
                Assert.That(unrelated.gameObject.activeSelf, Is.True);
            }
            finally { if (target) Object.Destroy(target.gameObject); if (unrelated) Object.Destroy(unrelated.gameObject); if (restored) Object.Destroy(restored); }
        }

        [UnityTest]
        public IEnumerator RestoreMatrix_ChildBaselinePreservesInactiveChildrenAndNestedBoundaries()
        {
            var manager = SaveManager.Instance; var prefab = Spawn(); prefab.TrackChildStateOverrides = true;
            var state = prefab.transform.Find("State"); state.gameObject.SetActive(false);
            var nestedObject = new GameObject("Nested"); nestedObject.SetActive(false); nestedObject.transform.SetParent(prefab.transform);
            var nested = nestedObject.AddComponent<SaveablePrefab>(); nested.RegisterWithSaveSystem = false;
            var grandchild = new GameObject("NestedState"); grandchild.transform.SetParent(nestedObject.transform);
            try
            {
                yield return null;
                var mods = manager.Serializer.Deserialize<RuntimeModificationData>(prefab.CaptureRuntimeModifications());
                Assert.That(mods.ChildStates.Single(c => c.Path == "State").Exists, Is.True);
                Assert.That(mods.ChildStates.Single(c => c.Path == "State").ActiveWhenSaved, Is.False);
                Assert.That(mods.ChildStates.Any(c => c.Path.StartsWith("Nested", StringComparison.Ordinal)), Is.False);
                Object.Destroy(grandchild); yield return null;
                mods = manager.Serializer.Deserialize<RuntimeModificationData>(prefab.CaptureRuntimeModifications());
                Assert.That(mods.ChildStates.Any(c => c.Path.StartsWith("Nested", StringComparison.Ordinal)), Is.False);
                Object.Destroy(state.gameObject); yield return null;
                for (int i = 0; i < 2; i++)
                {
                    mods = manager.Serializer.Deserialize<RuntimeModificationData>(prefab.CaptureRuntimeModifications());
                    Assert.That(mods.ChildStates.Any(c => c.Path == "State" && !c.Exists), Is.True);
                }
            }
            finally { Object.Destroy(prefab.gameObject); }
        }
    }
}
#endif
