#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator PrefabGeneration_FullLoadReplacesDeferredQueue()
            => PrefabQueueGenerationScenario("replace");

        [UnityTest]
        public IEnumerator PrefabGeneration_RejectedPreparationPreservesDeferredQueue()
            => PrefabQueueGenerationScenario("invalid");

        [UnityTest]
        public IEnumerator PrefabGeneration_CancelledPreparationPreservesDeferredQueue()
            => PrefabQueueGenerationScenario("cancel");

        IEnumerator PrefabQueueGenerationScenario(string next)
        {
            var manager = SaveManager.Instance;
            var template = Spawn();
            var oldEntry = PrefabGenerationEntry(template, "deferred-A", true);
            var newEntry = PrefabGenerationEntry(template, "deferred-B", true);
            var first = new SaveData(manager.SaveSettings.version);
            first.Prefabs.Add(oldEntry);
            var second = new SaveData(manager.SaveSettings.version);
            second.Prefabs.Add(newEntry);
            if (next == "invalid") second.Version = null;
            manager.SaveSystem.Save(manager.Serializer.Serialize(first), manager.GetSlotMetadata(1));
            manager.SaveSystem.Save(manager.Serializer.Serialize(second), manager.GetSlotMetadata(2));
            var loadA = manager.LoadSaveSlotAsync(1);
            yield return Await(loadA);
            Assert.That(loadA.Result.Success, Is.True, loadA.Result.ErrorMessage);
            Assert.That(manager.GetPrefabManager.PeekDeferredPrefabsForScene(null).Select(p => p.InstanceID),
                Is.EqualTo(new[] { oldEntry.InstanceID }));
            bool ignored = LogAssert.ignoreFailingMessages;
            try
            {
                LogAssert.ignoreFailingMessages = next == "invalid" || ignored;
                var loadB = manager.LoadSaveSlotAsync(2, cancellationToken: new CancellationToken(next == "cancel"));
                yield return Await(loadB);
                Assert.That(loadB.Result.Success, Is.EqualTo(next == "replace"), loadB.Result.ErrorMessage);
            }
            finally { LogAssert.ignoreFailingMessages = ignored; }
            string retained = next == "replace" ? newEntry.InstanceID : oldEntry.InstanceID;
            Assert.That(manager.GetPrefabManager.PeekDeferredPrefabsForScene(null).Select(p => p.InstanceID),
                Is.EqualTo(new[] { retained }), "Only the accepted full snapshot supplies queued prefab data.");
            yield return manager.GetPrefabManager.ProcessDeferredPrefabs();
            Assert.That(manager.GetPrefabManager.GetInstantiatedPrefabs().ContainsKey(retained), Is.True,
                "Current-generation streaming remains usable after accepted or rejected loads.");
        }

        [UnityTest]
        public IEnumerator PrefabGeneration_RetiresScenePendingBeforeDestinationCallbacks()
        {
            var manager = SaveManager.Instance;
            var entry = PrefabGenerationEntry(Spawn(), "pending-old-scene", false);
            entry.HomeScene = "A";
            var old = new SaveData(manager.SaveSettings.version);
            old.Prefabs.Add(entry);
            manager.SaveSystem.Save(manager.Serializer.Serialize(old), manager.GetSlotMetadata(1));
            manager.SaveSystem.Save(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)
                { LastActiveScene = "A" }), manager.GetSlotMetadata(2));
            var loadA = manager.LoadSaveSlotAsync(1);
            yield return Await(loadA);
            Assert.That(loadA.Result.Success, Is.True, loadA.Result.ErrorMessage);
            Assert.That(manager.GetPrefabManager.GetPendingSceneKeys(), Does.Contain("A"));
            int staleRestores = 0;
            Action<SaveablePrefab> onRestore = prefab =>
            {
                if (prefab.UniqueID == entry.InstanceID) staleRestores++;
            };
            SaveablePrefab.OnAfterRestore += onRestore;
            try
            {
                var loadB = manager.LoadSaveSlotAsync(2, true, true);
                yield return Await(loadB);
                Assert.That(loadB.Result.Success, Is.True, loadB.Result.ErrorMessage);
                Assert.That(staleRestores, Is.Zero,
                    "OnSceneLoaded must never start the preceding snapshot's scene-pending prefabs.");
                Assert.That(manager.GetPrefabManager.GetPendingSceneKeys(), Is.Empty);
            }
            finally { SaveablePrefab.OnAfterRestore -= onRestore; }
        }

        [UnityTest]
        public IEnumerator PrefabGeneration_RetiresYieldedBatchBeforeItTouchesReplacement()
        {
            var manager = SaveManager.Instance;
            var template = Spawn();
            var oldX = PrefabGenerationEntry(template, "yielded-X", false);
            oldX.Position = new Vector3(1, 2, 3);
            var oldY = PrefabGenerationEntry(template, "yielded-Y", false);
            var replacement = PrefabGenerationEntry(template, oldX.InstanceID, false);
            replacement.Position = new Vector3(100, 200, 300);
            var next = new SaveData(manager.SaveSettings.version);
            next.Prefabs.Add(replacement);
            manager.SaveSystem.Save(manager.Serializer.Serialize(next), manager.GetSlotMetadata(2));
            int previousBatchSize = manager.SaveSettings.prefabInstantiationBatchSize;
            manager.SaveSettings.prefabInstantiationBatchSize = 1;
            IEnumerator previous = manager.GetPrefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { oldX, oldY }, new List<string>(), false, false);
            try
            {
                Assert.That(previous.MoveNext(), Is.True);
                Assert.That(manager.GetPrefabManager.GetInstantiatedPrefabs().ContainsKey(oldX.InstanceID), Is.True);
                var load = manager.LoadSaveSlotAsync(2);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                Assert.That(previous.MoveNext(), Is.False, "An old iterator cannot resume into a new full-load generation.");
                var current = manager.GetPrefabManager.GetInstantiatedPrefabs();
                Assert.That(current.ContainsKey(oldY.InstanceID), Is.False);
                Assert.That(current[replacement.InstanceID].transform.position, Is.EqualTo(replacement.Position));
            }
            finally
            {
                (previous as IDisposable)?.Dispose();
                manager.SaveSettings.prefabInstantiationBatchSize = previousBatchSize;
            }
        }

        [UnityTest]
        public IEnumerator PrefabGeneration_ClearPendingStillAllowsCurrentBatchToFinish()
        {
            var manager = SaveManager.Instance;
            var template = Spawn();
            var first = PrefabGenerationEntry(template, "clear-current-X", false);
            var second = PrefabGenerationEntry(template, "clear-current-Y", false);
            int previousBatchSize = manager.SaveSettings.prefabInstantiationBatchSize;
            manager.SaveSettings.prefabInstantiationBatchSize = 1;
            IEnumerator active = manager.GetPrefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { first, second }, new List<string>(), false, false);
            try
            {
                Assert.That(active.MoveNext(), Is.True);
                manager.GetPrefabManager.ClearPendingPrefabs();
                int moves = 0;
                while (active.MoveNext())
                {
                    Assert.That(++moves, Is.LessThan(20));
                    yield return active.Current;
                }
                var current = manager.GetPrefabManager.GetInstantiatedPrefabs();
                Assert.That(current.ContainsKey(first.InstanceID), Is.True);
                Assert.That(current.ContainsKey(second.InstanceID), Is.True);
            }
            finally
            {
                (active as IDisposable)?.Dispose();
                manager.SaveSettings.prefabInstantiationBatchSize = previousBatchSize;
            }
        }

        [UnityTest]
        public IEnumerator PrefabGeneration_UnstartedIteratorCannotCrossAcceptedLoad()
        {
            var manager = SaveManager.Instance;
            var old = PrefabGenerationEntry(Spawn(), "unstarted-old", false);
            IEnumerator routine = manager.GetPrefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { old }, new List<string>(), false, false);
            manager.SaveSystem.Save(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)), manager.GetSlotMetadata(2));
            var load = manager.LoadSaveSlotAsync(2);
            yield return Await(load);
            Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(manager.GetPrefabManager.GetInstantiatedPrefabs().ContainsKey(old.InstanceID), Is.False);
        }

        [UnityTest]
        public IEnumerator PrefabGeneration_RetirementDisposesTemporarySceneOverride()
        {
            var manager = SaveManager.Instance;
            Scene before = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("A", LoadSceneMode.Additive);
            SceneManager.SetActiveScene(before);
            var entry = PrefabGenerationEntry(Spawn(), "temporary-scene-old", false);
            entry.HomeScene = "A";
            int previousBatchSize = manager.SaveSettings.prefabInstantiationBatchSize;
            manager.SaveSettings.prefabInstantiationBatchSize = 1;
            IEnumerator routine = manager.GetPrefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { entry }, new List<string>(), false, false);
            try
            {
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("A"));
                manager.SaveSystem.Save(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)), manager.GetSlotMetadata(2));
                var load = manager.LoadSaveSlotAsync(2);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(before),
                    "Retirement must release an old batch's active-scene override before applying the next snapshot.");
                Assert.That(routine.MoveNext(), Is.False);
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
                manager.SaveSettings.prefabInstantiationBatchSize = previousBatchSize;
            }
        }

        [UnityTest]
        public IEnumerator PrefabGeneration_SceneTransitionRetainsImportedHomeSnapshot()
        {
            var manager = SaveManager.Instance;
            string previousScene = SceneManager.GetActiveScene().name;
            Assert.That(previousScene, Is.EqualTo("B"));
            var candidate = new SaveData(manager.SaveSettings.version) { LastActiveScene = "A" };
            candidate.HomeSceneComponentSnapshots = new Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>>
            {
                [previousScene] = new Dictionary<string, Dictionary<string, byte[]>>
                {
                    ["snapshot-object"] = new Dictionary<string, byte[]> { ["snapshot-object_component"] = new byte[] { 17 } }
                }
            };
            manager.SaveSystem.Save(manager.Serializer.Serialize(candidate), manager.GetSlotMetadata(1));
            var load = manager.LoadSaveSlotAsync(1, true, true);
            yield return Await(load);
            Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
            var snapshots = FeatureTestSupport.HomeSnapshots(manager.ComponentManager);
            Assert.That(snapshots.ContainsKey(previousScene), Is.True);
            Assert.That(snapshots[previousScene]["snapshot-object"]["snapshot-object_component"], Is.EqualTo(new byte[] { 17 }));
        }

        [UnityTest]
        public IEnumerator PrefabGeneration_RetirementUnwindsNestedBatchSceneOverrides()
            => PrefabNestedBatchSceneScenario(false);

        [UnityTest]
        public IEnumerator PrefabGeneration_RetirementAfterOlderBatchCompletesRestoresOriginalScene()
            => PrefabNestedBatchSceneScenario(true);

        IEnumerator PrefabNestedBatchSceneScenario(bool completeOlderFirst)
        {
            var manager = SaveManager.Instance;
            Scene original = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("A", LoadSceneMode.Additive);
            Scene extra = SceneManager.CreateScene("NestedPrefabGeneration");
            SceneManager.SetActiveScene(original);
            var template = Spawn();
            var first = PrefabGenerationEntry(template, "nested-batch-A", false);
            first.HomeScene = "A";
            var second = PrefabGenerationEntry(template, "nested-batch-C", false);
            second.HomeScene = extra.name;
            int previousBatchSize = manager.SaveSettings.prefabInstantiationBatchSize;
            manager.SaveSettings.prefabInstantiationBatchSize = 1;
            var routines = new[]
            {
                manager.GetPrefabManager.InstantiatePrefabsCoroutine(new List<SaveablePrefabData> { first }, new List<string>(), false, false),
                manager.GetPrefabManager.InstantiatePrefabsCoroutine(new List<SaveablePrefabData> { second }, new List<string>(), false, false)
            };
            try
            {
                Assert.That(routines[0].MoveNext(), Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("A"));
                Assert.That(routines[1].MoveNext(), Is.True);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(extra));
                if (completeOlderFirst)
                {
                    int moves = 0;
                    while (routines[0].MoveNext())
                    {
                        Assert.That(++moves, Is.LessThan(20));
                        yield return routines[0].Current;
                    }
                    Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(extra),
                        "An older batch must not restore over a later batch's active-scene scope.");
                }
                manager.SaveSystem.Save(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)), manager.GetSlotMetadata(2));
                var load = manager.LoadSaveSlotAsync(2);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(original),
                    "Nested scene/pool scopes must unwind in reverse activation order.");
                Assert.That(routines[0].MoveNext(), Is.False);
                Assert.That(routines[1].MoveNext(), Is.False);
            }
            finally
            {
                for (int index = routines.Length - 1; index >= 0; index--) (routines[index] as IDisposable)?.Dispose();
                manager.SaveSettings.prefabInstantiationBatchSize = previousBatchSize;
            }
        }

        [UnityTest]
        public IEnumerator PrefabGeneration_RetirementPreservesExternalActiveSceneChange()
        {
            var manager = SaveManager.Instance;
            Scene original = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("A", LoadSceneMode.Additive);
            Scene external = SceneManager.CreateScene("ExternalPrefabGeneration");
            SceneManager.SetActiveScene(original);
            var entry = PrefabGenerationEntry(Spawn(), "externally-switched-old", false);
            entry.HomeScene = "A";
            int previousBatchSize = manager.SaveSettings.prefabInstantiationBatchSize;
            manager.SaveSettings.prefabInstantiationBatchSize = 1;
            IEnumerator routine = manager.GetPrefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { entry }, new List<string>(), false, false);
            try
            {
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("A"));
                SceneManager.SetActiveScene(external);
                manager.SaveSystem.Save(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)), manager.GetSlotMetadata(2));
                var load = manager.LoadSaveSlotAsync(2);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(external),
                    "Retiring a batch cannot undo an unrelated active-scene change.");
                Assert.That(routine.MoveNext(), Is.False);
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
                manager.SaveSettings.prefabInstantiationBatchSize = previousBatchSize;
            }
        }

        [UnityTest]
        public IEnumerator PrefabGeneration_RetiresAllPendingDataBeforeSceneScopeCallbacks()
        {
            var manager = SaveManager.Instance;
            Scene original = SceneManager.GetActiveScene();
            var receiver = new GenerationSaveable();
            var template = Spawn();
            var deferred = PrefabGenerationEntry(template, "callback-old-deferred", true);
            var pending = PrefabGenerationEntry(template, "callback-old-pending", false);
            pending.HomeScene = "A";
            var active = PrefabGenerationEntry(template, "callback-old-active", false);
            active.HomeScene = "A";
            var first = new SaveData(manager.SaveSettings.version);
            first.ComponentsData[receiver.UniqueIdentifier] = new byte[] { 73 };
            first.Prefabs.Add(deferred);
            first.Prefabs.Add(pending);
            manager.SaveSystem.Save(manager.Serializer.Serialize(first), manager.GetSlotMetadata(1));
            manager.SaveSystem.Save(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)), manager.GetSlotMetadata(2));
            var loadA = manager.LoadSaveSlotAsync(1);
            yield return Await(loadA);
            Assert.That(loadA.Result.Success, Is.True, loadA.Result.ErrorMessage);
            var previousPipeline = manager.SceneActivationPipeline;
            manager.SceneActivationPipeline = _ => false;
            yield return SceneManager.LoadSceneAsync("A", LoadSceneMode.Additive);
            SceneManager.SetActiveScene(original);
            Assert.That(manager.GetPrefabManager.GetPendingSceneKeys(), Does.Contain("A"));
            int previousBatchSize = manager.SaveSettings.prefabInstantiationBatchSize;
            manager.SaveSettings.prefabInstantiationBatchSize = 1;
            IEnumerator routine = manager.GetPrefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { active }, new List<string>(), false, false);
            Assert.That(routine.MoveNext(), Is.True);
            bool callbackObserved = false;
            bool oldPrefabQueued = false;
            UnityEngine.Events.UnityAction<Scene, Scene> onSceneChanged = (_, next) =>
            {
                if (next != original) return;
                callbackObserved = true;
                manager.ComponentManager.RegisterSaveableComponent(receiver);
                oldPrefabQueued |= manager.GetPrefabManager.HasDeferredPrefabs || manager.GetPrefabManager.GetPendingSceneKeys().Any();
                manager.SceneActivationPipeline = previousPipeline;
                manager.GetPrefabManager.ProcessPendingPrefabs("A", new List<string>());
                manager.GetPrefabManager.ProcessDeferredPrefabs();
            };
            SceneManager.activeSceneChanged += onSceneChanged;
            try
            {
                var loadB = manager.LoadSaveSlotAsync(2);
                yield return Await(loadB);
                Assert.That(loadB.Result.Success, Is.True, loadB.Result.ErrorMessage);
                Assert.That(callbackObserved, Is.True);
                Assert.That(receiver.Loads, Is.Zero, "Scene-scope disposal cannot consume the previous component generation.");
                Assert.That(oldPrefabQueued, Is.False, "Scene-scope disposal cannot restart a preceding prefab queue.");
                Assert.That(routine.MoveNext(), Is.False);
            }
            finally
            {
                SceneManager.activeSceneChanged -= onSceneChanged;
                manager.SceneActivationPipeline = previousPipeline;
                manager.ComponentManager.UnregisterSaveableComponent(receiver);
                (routine as IDisposable)?.Dispose();
                manager.SaveSettings.prefabInstantiationBatchSize = previousBatchSize;
            }
        }

        static SaveablePrefabData PrefabGenerationEntry(SaveablePrefab template, string id, bool deferred)
        {
            // This fixture needs fresh runtime spawns. Object.Instantiate alone leaves
            // the fixture eligible for the package's scene-baked handoff/reuse rules.
            template.MarkAsAddedAtRuntime();
            var entry = SaveManager.Instance.GetPrefabManager.BuildPrefabData(template);
            entry.InstanceID = id;
            entry.HomeScene = string.Empty;
            entry.DeferLowPriorityUntilRequested = deferred;
            entry.ReuseSceneInstanceOnLoad = false;
            return entry;
        }
    }
}
#endif
