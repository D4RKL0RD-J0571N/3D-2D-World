#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System.Collections;
using System.Linq;
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
        public IEnumerator FactoryDelayedDestroyUsesScaledTimeAndSurvivesDisabledCallerAndTarget()
        {
            var manager = SaveManager.Instance;
            manager.SaveSettings.usePrefabPooling = false;
            var target = Spawn();
            string id = target.UniqueID;
            Assert.That(manager.GameObjectTracker.IsTracked(id), Is.True);
            var caller = SceneComponent("DelayedDestroyCaller");
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                caller.StartCoroutine(RequestFactoryDestroy(target, .15f));
                caller.gameObject.SetActive(false);
                target.gameObject.SetActive(false);
                float pausedAt = Time.realtimeSinceStartup;
                yield return Until(() => Time.realtimeSinceStartup >= pausedAt + .25f, "paused destruction timer", 5);
                Assert.That(target != null, Is.True, "Delay is scaled and must not expire while paused.");
                Assert.That(manager.GetDestroyedGameObjectIDs(), Does.Not.Contain(id));
                yield return Await(manager.SaveAsync(1));
                Assert.That(manager.CurrentSaveData.Prefabs.Any(p => p.InstanceID == id), Is.True);
                Assert.That(manager.CurrentSaveData.DestroyedGameObjects, Does.Not.Contain(id));
                Assert.That(manager.CurrentSaveData.DestroyedObjectData.ContainsKey(id), Is.False);

                var positionAtDeadline = new Vector3(31, 41, 59);
                target.transform.position = positionAtDeadline;
                Time.timeScale = 1;
                yield return Until(() => target == null, "disabled target's factory destruction", 5);
                Assert.That(manager.GetDestroyedGameObjectIDs(), Does.Contain(id));
                Assert.That(manager.CurrentSaveData.DestroyedObjectData.ContainsKey(id), Is.True,
                    "The complete destruction operation, including its restore snapshot, happens at the deadline.");
                Assert.That(manager.CurrentSaveData.Prefabs.Single(p => p.InstanceID == id).Position,
                    Is.EqualTo(positionAtDeadline), "Capture must use the deadline state, not the scheduling state.");
                yield return Await(manager.SaveAsync(1));
                Assert.That(manager.CurrentSaveData.DestroyedGameObjects, Does.Contain(id));
            }
            finally { Time.timeScale = previousScale; }
        }

        private static IEnumerator RequestFactoryDestroy(SaveablePrefab target, float delay)
        {
            SaveablePrefabFactory.Destroy(target, delay);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FactoryRepeatedDestroyRequestsKeepTheEarliestDeadline()
        {
            SaveManager.Instance.SaveSettings.usePrefabPooling = false;
            var shortFirst = Spawn();
            var longFirst = Spawn();
            SaveablePrefabFactory.Destroy(shortFirst, .1f);
            SaveablePrefabFactory.Destroy(shortFirst, 20f);
            SaveablePrefabFactory.Destroy(longFirst, 20f);
            SaveablePrefabFactory.Destroy(longFirst, .1f);
            yield return Until(() => shortFirst == null && longFirst == null, "earliest factory deadlines", 5);
        }

        [UnityTest]
        public IEnumerator FactoryImmediateDestroyRetiresItsScheduledRequest()
        {
            SaveManager.Instance.SaveSettings.usePrefabPooling = false;
            var target = Spawn();
            SaveablePrefabFactory.Destroy(target, 20f);
            var pending = (IDictionary)Field(typeof(SaveablePrefabFactory), "pendingDestroyRequests").GetValue(null);
            Assert.That(pending.Count, Is.EqualTo(1));
            SaveablePrefabFactory.Destroy(target);
            Assert.That(pending.Count, Is.Zero, "Immediate destruction must release the scheduled request and its references.");
            yield return Until(() => target == null, "immediate factory destruction", 5);
        }

        [UnityTest]
        public IEnumerator PoolReleaseRetiresFactoryDelayAndRepeatedReleaseCannotDuplicateAnInstance()
        {
            SaveManager.Instance.SaveSettings.usePrefabPooling = false;
            var asset = Resources.Load<GameObject>("CrystalSaveLifecyclePrefab").GetComponent<SaveablePrefab>();
            using var pool = new SaveablePrefabPool(asset, 0, true, collectionCheck: true);
            var outgoing = pool.Spawn(Vector3.zero, Quaternion.identity);
            SaveablePrefabFactory.Destroy(outgoing, .1f);
            pool.Despawn(outgoing);
            pool.Despawn(outgoing);
            var replacement = pool.Spawn(Vector3.one, Quaternion.identity);
            var other = pool.Spawn(Vector3.right, Quaternion.identity);
            Assert.That(replacement, Is.SameAs(outgoing), "Fixture must actually reuse the timed instance.");
            Assert.That(other, Is.Not.SameAs(replacement), "Double release must not put one object in the pool twice.");
            float resumedAt = Time.time;
            yield return Until(() => Time.time >= resumedAt + .25f, "retired pool timer deadline", 5);
            Assert.That(replacement != null && replacement.gameObject.activeSelf, Is.True);
            Assert.That(other != null && other.gameObject.activeSelf, Is.True);
            Assert.That(replacement.transform.position, Is.EqualTo(Vector3.one));
            Assert.That(other.transform.position, Is.EqualTo(Vector3.right));
        }
    }
}
#endif
