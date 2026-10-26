#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator FilteredSaveUsesStableTrackingMembershipWhenItsPredicateSpawnsAPrefab()
        {
            var manager = SaveManager.Instance;
            var first = Spawn();
            var second = Spawn();
            SaveablePrefab late = null;
            var visits = new Dictionary<GameObject, int>();
            var save = manager.SaveSlotWithFilterAsync(1, go =>
            {
                visits.TryGetValue(go, out int count);
                visits[go] = count + 1;
                if (go == first.gameObject && late == null) late = Spawn();
                return go == first.gameObject || go == second.gameObject;
            });
            yield return Await(save);
            Assert.That(save.Result, Is.True);
            Assert.That(late, Is.Not.Null, "The predicate must actually mutate tracker membership.");
            Assert.That(visits[first.gameObject], Is.EqualTo(1));
            Assert.That(visits[second.gameObject], Is.EqualTo(1));
            Assert.That(visits.ContainsKey(late.gameObject), Is.False);
            var read = manager.LoadSaveDataForSlotAsync(1);
            yield return Await(read);
            Assert.That(read.Result.Prefabs.Select(p => p.InstanceID), Is.EquivalentTo(new[] { first.UniqueID, second.UniqueID }));
            Assert.That(manager.CurrentSaveData.Prefabs.Any(p => p.InstanceID == late.UniqueID), Is.True,
                "Filtered files must not turn the full in-memory snapshot into a partial one.");
        }
    }
}
#endif
