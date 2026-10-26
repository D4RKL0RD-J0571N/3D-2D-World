#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
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
        public IEnumerator PopulationSceneTransitionKeepsQueueOwnershipUntilTheDestinationIsReady()
        {
            yield return LoadScene("B");
            var manager = SaveManager.Instance;
            var probeObject = new GameObject("Population transition callback");
            Composite(probeObject, typeof(FeatureProbe));
            var probe = probeObject.GetComponent<FeatureProbe>();
            probe.Value = 17;
            Task nestedSave = null;
            probe.Loaded = () => nestedSave ??= manager.SaveAsync(2);
            yield return null;
            try
            {
                yield return Await(manager.LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync(
                    "A", loadAsync: true));
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("A"));
                Assert.That(nestedSave, Is.Not.Null);
                yield return Await(nestedSave);
                var data = manager.LoadSaveDataForSlotAsync(2);
                yield return Await(data);
                Assert.That(data.Result.LastActiveScene, Is.EqualTo("A"),
                    "A callback's queued save must run after the complete transition workflow.");
                Assert.That(manager.IsInSceneTransition, Is.False);
            }
            finally { if (probeObject != null) Object.Destroy(probeObject); }
        }

        [UnityTest]
        public IEnumerator PopulationSnapshotWaitsForTheCurrentSaveOperation()
        {
            var manager = SaveManager.Instance;
            var original = manager.SaveSystem;
            var storage = new HardeningStorage(original) { GateWrites = true };
            manager.SaveSettings.enableCloudSave = true;
            manager.SaveSettings.backend = SaveBackend.MySQL;
            Set(manager, "saveSystem", storage);
            try
            {
                var save = manager.SaveAsync(1);
                yield return Until(() => storage.Writes.Count == 1, "held save commit");
                var captured = manager.CurrentSaveData;
                var population = manager.PopulatePendingPrefabsFromSnapshotAsync();
                yield return null;
                yield return null;
                Assert.That(population.IsCompleted, Is.False, "Population must wait for the FIFO owner.");
                Assert.That(manager.CurrentSaveData, Is.SameAs(captured));
                storage.Release();
                yield return Await(save);
                yield return Await(population);
            }
            finally
            {
                storage.Release();
                manager.SaveSettings.enableCloudSave = false;
                Set(manager, "saveSystem", original);
            }
        }

        [UnityTest]
        public IEnumerator PopulationSnapshotKeepsTheRequestedScene()
        {
            yield return LoadScene("B");
            var manager = SaveManager.Instance;
            var operation = manager.SnapshotAndPopulateAsync("A");
            yield return Await(operation);
            Assert.That(operation.Result, Is.True);
            Assert.That(manager.CurrentSaveData.LastActiveScene, Is.EqualTo("A"),
                "A second capture must not replace the caller's selected scene.");
        }

        [UnityTest]
        public IEnumerator PopulationFromSlotIsAdditiveAndAwaitsActiveApplication()
        {
            var manager = SaveManager.Instance;
            var savedPrefab = Spawn();
            var probeObject = new GameObject("Population delayed helper");
            Composite(probeObject, typeof(FeatureProbe));
            var probe = probeObject.GetComponent<FeatureProbe>();
            probe.Value = 17;
            yield return null;
            yield return Await(manager.SaveAsync(1));
            var unrelated = Spawn();
            string unrelatedId = unrelated.UniqueID;
            object ticket = null;
            probe.Loaded = () => ticket = Call(typeof(ComponentManager).Assembly.GetType(
                "Arawn.CrystalSave.Runtime.ComponentOperationScope", true), "DeferApplication");
            probe.Value = 99;
            try
            {
                var operation = manager.PopulatePendingPrefabsFromSlotAsync(1);
                yield return Until(() => ticket != null, "population application ticket");
                yield return null;
                yield return null;
                Assert.That(operation.IsCompleted, Is.False, "The active application still owns pending bytes.");
                Assert.That(savedPrefab != null && unrelated != null, Is.True);
                Assert.That(unrelated.UniqueID, Is.EqualTo(unrelatedId));
                Call(ticket, "Complete", new object[] { null });
                ticket = null;
                yield return Await(operation);
                Assert.That(probe.Value, Is.EqualTo(17));
                Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(probe.UniqueIdentifier), Is.False);
                Assert.That(manager.GetPrefabManager.RegisteredPrefabs, Has.Member(unrelated));
            }
            finally
            {
                if (ticket != null) Call(ticket, "Complete", new object[] { null });
                probe.Loaded = null;
                Object.Destroy(probeObject);
            }
        }

        [UnityTest]
        public IEnumerator PopulationReportsFailedHelpersAndRetainsTheirRetryBytes()
        {
            var manager = SaveManager.Instance;
            var badObject = new GameObject("Population failed helper");
            var goodObject = new GameObject("Population healthy helper");
            Composite(badObject, typeof(FeatureProbe));
            Composite(goodObject, typeof(FeatureProbe));
            var bad = badObject.GetComponent<FeatureProbe>();
            var good = goodObject.GetComponent<FeatureProbe>();
            bad.Value = 17;
            good.Value = 23;
            yield return null;
            yield return Await(manager.SaveAsync(1));
            bad.Value = good.Value = 99;
            bad.Loaded = () => throw new InvalidOperationException("Population application failed");
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var operation = manager.PopulatePendingPrefabsFromSlotAsync(1);
                yield return Until(() => operation.IsCompleted, "failed population outcome");
                Assert.That(operation.IsFaulted, Is.True);
                Assert.That(operation.Exception.ToString(), Does.Contain(bad.UniqueIdentifier));
                Assert.That(good.Value, Is.EqualTo(23));
                Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(bad.UniqueIdentifier), Is.True);
                bad.Loaded = null;
                yield return Await(manager.PopulatePendingPrefabsFromSlotAsync(1));
                Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(bad.UniqueIdentifier), Is.False);
            }
            finally
            {
                Object.Destroy(badObject);
                Object.Destroy(goodObject);
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
#endif
