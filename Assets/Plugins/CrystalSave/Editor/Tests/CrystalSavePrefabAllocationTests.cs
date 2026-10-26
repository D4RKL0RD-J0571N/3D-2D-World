#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSavePrefabAllocationTests
    {
        const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        readonly List<GameObject> roots = new List<GameObject>();
        FeatureSettingsScope settings;
        SaveManager previousManager;
        PropertyInfo singleton;
        static readonly Type PoolType = typeof(PrefabManager).Assembly.GetType("Arawn.CrystalSave.Runtime.PrefabReuseCandidates", true);
        delegate bool Claim(SaveablePrefabData data, bool handoff, out SaveablePrefab prefab, out string reason, out int compatible);

        [SetUp]
        public void SetUp()
        {
            settings = new FeatureSettingsScope();
            singleton = typeof(SaveManager).GetProperty("Instance", Methods);
            previousManager = (SaveManager)singleton.GetValue(null);
            singleton.SetValue(null, null);
            Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Off;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots) if (root) Object.DestroyImmediate(root);
            roots.Clear();
            singleton.SetValue(null, previousManager);
            settings.Dispose();
        }

        SaveablePrefab Prefab(string name, string id)
        {
            var root = new GameObject(name); root.SetActive(false); roots.Add(root);
            var prefab = root.AddComponent<SaveablePrefab>();
            prefab.RegisterWithSaveSystem = false;
            Set(prefab, "uniqueID", id);
            return prefab;
        }

        static T Bind<T>(Type type, string method, object target = null) where T : Delegate
            => (T)Delegate.CreateDelegate(typeof(T), target, type.GetMethod(method, Methods));

        static SaveablePrefabData Data(string id, Vector3 position) => new SaveablePrefabData(
            id, "allocation-asset", id, position, Quaternion.identity, Vector3.one, null, false, null, null);

        [TestCase(0)]
        [TestCase(8)]
        [TestCase(64)]
        public void RestoreIdentityReusesItsComponentBufferWithinCalibratedBudget(int helperCount)
        {
            const int iterations = 16;
            var prefab = Prefab("AllocationRoot", "first-root");
            var helpers = new FeatureProbe[helperCount];
            for (int i = 0; i < helperCount; i++)
            {
                var child = new GameObject("Child " + i); child.transform.SetParent(prefab.transform);
                helpers[i] = child.AddComponent<FeatureProbe>(); helpers[i].enabled = false;
                Set(helpers[i], "uniqueID", "first-root");
                helpers[i].ComponentID = "part" + i;
            }
            var restore = Bind<Action<string, List<SaveableComponent>>>(typeof(SaveablePrefab), "SetUniqueIDForRestore", prefab);
            var buffer = new List<SaveableComponent>(helperCount);
            restore("second-root", buffer); restore("first-root", null); restore("second-root", buffer);
            Action buffered = () => { for (int i = 0; i < iterations; i++) restore((i & 1) == 0 ? "first-root" : "second-root", buffer); };
            Action fallback = () => { for (int i = 0; i < iterations; i++) restore((i & 1) == 0 ? "first-root" : "second-root", null); };
            buffered(); fallback();
            long reused = CrystalSaveAllocationMeasurement.Count(buffered);
            long fresh = CrystalSaveAllocationMeasurement.Count(fallback);
            TestContext.WriteLine($"helpers={helperCount}, iterations={iterations}, reused={reused}, fallback={fresh}");
            // Existing identity parsing allocates its split result; the reusable hierarchy buffer
            // must not add a list/backing array per restore. The fallback is a real positive control.
            Assert.That(reused, Is.LessThanOrEqualTo(1L * helperCount * iterations));
            Assert.That(fresh - reused, Is.GreaterThanOrEqualTo(iterations * (helperCount == 0 ? 1 : 2)));
            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.Capacity, Is.EqualTo(helperCount));
            Assert.That(prefab.UniqueID, Is.EqualTo("second-root"));
            for (int i = 0; i < helperCount; i++)
            {
                Assert.That(helpers[i].GameObjectUniqueID, Is.EqualTo("second-root"));
                Assert.That(helpers[i].ComponentID, Is.EqualTo("part" + i));
                Assert.That(helpers[i].UniqueIdentifier, Is.EqualTo("second-root_part" + i));
            }
        }

        [TestCase("exact")]
        [TestCase("nearest")]
        [TestCase("mixed-empty")]
        public void PreparedCandidateClaimsAllocateNoPerEntryTemporaries(string route)
        {
            const int count = 64;
            object pool = Activator.CreateInstance(PoolType, true);
            var add = Bind<Action<SaveablePrefab>>(PoolType, "Add", pool);
            var prepare = Bind<Action>(PoolType, "Prepare", pool);
            var claim = Bind<Claim>(PoolType, "TryClaim", pool);
            var expected = new SaveablePrefab[count];
            var data = new SaveablePrefabData[count];
            var actual = new SaveablePrefab[count];
            var succeeded = new bool[count];
            var reasons = new string[count];
            for (int i = 0; i < count; i++)
            {
                expected[i] = Prefab("Candidate " + i, route == "mixed-empty" && i < count / 2 ? "" : "id-" + i);
                expected[i].transform.position = new Vector3(i * 3, i % 7, 0);
                data[i] = Data(route == "exact" ? "id-" + i : "incoming-" + i, expected[i].transform.position + Vector3.right * .1f);
                add(expected[i]);
            }
            prepare();
            // Warm delegate/JIT and the failed lookup without consuming any candidate.
            Assert.That(claim(null, false, out _, out _, out _), Is.False);
            Action claims = () =>
            {
                for (int i = 0; i < count; i++)
                    succeeded[i] = claim(data[i], false, out actual[i], out reasons[i], out _);
            };
            long events = CrystalSaveAllocationMeasurement.Count(claims);
            TestContext.WriteLine($"route={route}, claims={count}, allocations={events}");
            Assert.That(events, Is.Zero, "Prepared candidate lookup must not rebuild per-entry lists, arrays or descriptions.");
            for (int i = 0; i < count; i++)
            {
                Assert.That(succeeded[i], Is.True);
                Assert.That(actual[i], Is.SameAs(expected[i]));
                Assert.That(reasons[i], Is.Not.Empty);
            }
            Assert.That((int)PoolType.GetProperty("Count", Methods).GetValue(pool), Is.Zero);
        }

        [TestCase(0)]
        [TestCase(6)]
        public void StableHierarchyComparisonAllocatesOnlyUnityNameStrings(int depth)
        {
            const int iterations = 64;
            var left = Prefab(depth == 0 ? "A" : "Common", "left");
            var right = Prefab(depth == 0 ? "B" : "Common", "right");
            for (int i = 0; i < depth; i++)
            {
                var first = new GameObject(i == depth - 1 ? "A" : "Common"); first.SetActive(false);
                var second = new GameObject(i == depth - 1 ? "B" : "Common"); second.SetActive(false);
                first.transform.SetParent(left.transform); second.transform.SetParent(right.transform);
                left = first.AddComponent<SaveablePrefab>(); right = second.AddComponent<SaveablePrefab>();
                left.RegisterWithSaveSystem = right.RegisterWithSaveSystem = false;
            }
            var compare = Bind<Comparison<SaveablePrefab>>(PoolType, "CompareStableSceneOrder");
            Assert.That(compare(left, right), Is.LessThan(0));
            int result = 0;
            Action comparison = () => { for (int i = 0; i < iterations; i++) result += compare(left, right); };
            comparison(); result = 0;
            long events = CrystalSaveAllocationMeasurement.Count(comparison);
            TestContext.WriteLine($"depth={depth}, comparisons={iterations}, allocations={events}");
            Assert.That(result, Is.LessThan(0));
            Assert.That(events, Is.LessThanOrEqualTo(2L * (depth + 1) * iterations),
                "Sorting may read Unity names, but must not build hierarchy paths or temporary ancestor lists.");
        }

        [TestCase(16)]
        [TestCase(256)]
        public void BatchNormalizationAndSortingStayWithinAllocationBudget(int count)
        {
            const int iterations = 16;
            Type type = typeof(PrefabManager).Assembly.GetType("Arawn.CrystalSave.Runtime.PrefabRestoreQueue", true);
            var prepare = Bind<Func<IEnumerable<SaveablePrefabData>, bool, object>>(type, "PrepareBatch");
            var source = new List<SaveablePrefabData>(count + 1);
            for (int i = 0; i < count; i++)
            {
                var data = Data("sort-" + i, Vector3.zero); data.LoadPriority = i;
                data.DeferLowPriorityUntilRequested = i % 4 == 0; source.Add(data);
            }
            source.Add(source[0]); // Normalization must still deduplicate while partitioning/sorting.
            object batch = null;
            Action normalize = () => { for (int i = 0; i < iterations; i++) batch = prepare(source, true); };
            normalize();
            long events = CrystalSaveAllocationMeasurement.Count(normalize);
            TestContext.WriteLine($"entries={count}, iterations={iterations}, allocations={events}");
            Assert.That(events, Is.LessThanOrEqualTo((count == 16 ? 17L : 33L) * iterations));
            var immediate = (List<SaveablePrefabData>)batch.GetType().GetProperty("Immediate", Methods).GetValue(batch);
            var deferred = (List<SaveablePrefabData>)batch.GetType().GetProperty("Deferred", Methods).GetValue(batch);
            Assert.That(immediate.Count, Is.EqualTo(count * 3 / 4)); Assert.That(deferred.Count, Is.EqualTo(count / 4));
            Assert.That(immediate.Select(d => d.LoadPriority), Is.Ordered.Descending);
            Assert.That(deferred.Select(d => d.LoadPriority), Is.Ordered.Descending);
            Assert.That(immediate.Concat(deferred).Select(d => d.InstanceID).Distinct().Count(), Is.EqualTo(count));
        }

        [Test]
        public void DisabledLoadDiagnosticsAllocateNoDescriptionsOrValidationCollections()
        {
            var root = new GameObject("AllocationManager"); root.SetActive(false); roots.Add(root);
            var manager = root.AddComponent<PrefabManager>();
            for (int i = 0; i < 64; i++) manager.SaveablePrefabs.Add(Prefab("Diagnostic " + i, "diagnostic-" + i));
            var diagnostics = Bind<Action<string>>(typeof(PrefabManager), "LogRegisteredPrefabDiagnostics", manager);
            diagnostics("warm");
            PrefabLoadDiagnosticCounters.Reset();
            Action calls = () => { for (int i = 0; i < 64; i++) diagnostics("load allocation budget"); };
            calls(); // Include the measured delegate and its string literal in warm-up.
            long events = CrystalSaveAllocationMeasurement.Count(calls);
            Assert.That(events, Is.Zero);
            var counters = PrefabLoadDiagnosticCounters.Capture();
            Assert.That(counters.PrefabDescriptions + counters.PrefabDataDescriptions + counters.TransformPathDescriptions, Is.Zero);
        }
    }
}
#endif
