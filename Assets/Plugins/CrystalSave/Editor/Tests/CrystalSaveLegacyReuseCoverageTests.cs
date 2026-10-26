#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator LegacyReuse_WrongHomeMappingCannotConsumeImmediateRestore()
            => LegacyReuseMappingScenario(false, false);

        [UnityTest]
        public IEnumerator LegacyReuse_WrongHomeMappingCannotConsumeDeferredRestore()
            => LegacyReuseMappingScenario(false, true);

        [UnityTest]
        public IEnumerator LegacyReuse_InvalidSceneMappingCannotConsumeImmediateRestore()
            => LegacyReuseMappingScenario(true, false);

        [UnityTest]
        public IEnumerator LegacyReuse_InvalidSceneMappingCannotConsumeDeferredRestore()
            => LegacyReuseMappingScenario(true, true);

        private IEnumerator LegacyReuseMappingScenario(bool invalidScene, bool deferred)
        {
            var manager = SaveManager.Instance;
            manager.SaveSettings.enableAssetBasedReuseDuringDeferred = false;
            manager.SaveSettings.usePrefabPooling = false;
            var pm = manager.GetPrefabManager;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "/Resources/CrystalSaveLifecyclePrefab.prefab");
            var assetSaveable = asset.GetComponent<SaveablePrefab>();
            const string savedId = "legacy-mapped-saved-id";
            string assetInstanceId = Get<string>(assetSaveable, "uniqueID");
            var rejected = invalidScene ? assetSaveable : LegacyReuseCandidate(assetSaveable.PrefabAssetID, savedId);
            if (invalidScene) Set(rejected, "uniqueID", savedId);
            else
            {
                rejected.ReuseSceneInstanceOnLoad = true;
                pm.RegisterPrefab(rejected);
            }
            var mappings = Get<Dictionary<string, GameObject>>(pm, "instantiatedPrefabs");
            mappings[savedId] = rejected.gameObject;
            var originalScene = rejected.gameObject.scene;
            var originalName = rejected.name;
            var originalPosition = rejected.transform.position;
            Assert.That(originalScene.IsValid(), Is.EqualTo(!invalidScene));
            var previousScene = SceneManager.GetActiveScene();
            var destination = SceneManager.CreateScene("LegacyReuseDestination");
            try
            {
                Assert.That(SceneManager.SetActiveScene(destination), Is.True);
                var target = LegacyReuseCandidate(assetSaveable.PrefabAssetID, "legacy-target-design-id");
                target.ReuseSceneInstanceOnLoad = true;
                pm.RegisterPrefab(target);
                var data = PrefabGenerationEntry(assetSaveable, savedId, deferred);
                data.HomeScene = destination.name;
                data.ReuseSceneInstanceOnLoad = true;
                data.GameObjectName = "RestoredLegacyDestination";
                data.Position = new Vector3(17, 23, 31);
                // This scenario tests root identity/pose. The template's runtime
                // name override must not independently rename the restored root.
                data.RuntimeModificationData = null;
                yield return pm.InstantiatePrefabsCoroutine(new List<SaveablePrefabData> { data },
                    new List<string>(), clearExistingPrefabs: false);
                if (deferred)
                {
                    Assert.That(pm.HasDeferredPrefabs, Is.True);
                    Assert.That(target.transform.position, Is.EqualTo(Vector3.zero));
                    yield return pm.ProcessDeferredPrefabByUniqueID(savedId);
                    Assert.That(pm.HasDeferredPrefabs, Is.False);
                }
                Assert.That(mappings[savedId], Is.SameAs(target.gameObject),
                    "A stale mapping must not skip restoration or be reclaimed through the asset index.");
                Assert.That(target.name, Is.EqualTo(data.GameObjectName));
                Assert.That(Vector3.Distance(target.transform.position, data.Position), Is.LessThan(.001f));
                Assert.That(target.gameObject.scene, Is.EqualTo(destination));
                Assert.That(mappings.ContainsKey("legacy-target-design-id"), Is.False);
                Assert.That(rejected.name, Is.EqualTo(originalName));
                Assert.That(rejected.transform.position, Is.EqualTo(originalPosition));
                Assert.That(rejected.gameObject.scene, Is.EqualTo(originalScene));
            }
            finally
            {
                if (invalidScene) Set(assetSaveable, "uniqueID", assetInstanceId);
                SceneManager.SetActiveScene(previousScene);
            }
            yield return SceneManager.UnloadSceneAsync(destination);
        }

        [UnityTest]
        public IEnumerator LegacyReuse_HomeScopedClaimsRemainAvailableForOtherHomesAndConsumeGlobalCandidates()
        {
            var manager = SaveManager.Instance;
            manager.SaveSettings.enableAssetBasedReuseDuringDeferred = false;
            manager.SaveSettings.usePrefabPooling = false;
            var pm = manager.GetPrefabManager;
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "/Resources/CrystalSaveLifecyclePrefab.prefab")
                .GetComponent<SaveablePrefab>();
            var firstScene = SceneManager.GetActiveScene();
            var first = LegacyReuseCandidate(template.PrefabAssetID, "multi-home-saved-first");
            first.ReuseSceneInstanceOnLoad = true;
            pm.RegisterPrefab(first);
            var secondScene = SceneManager.CreateScene("LegacyReuseSecondHome");
            try
            {
                SceneManager.SetActiveScene(secondScene);
                var second = LegacyReuseCandidate(template.PrefabAssetID, "multi-home-design-second");
                second.ReuseSceneInstanceOnLoad = true;
                pm.RegisterPrefab(second);
                var a = PrefabGenerationEntry(template, first.UniqueID, false);
                var b = PrefabGenerationEntry(template, "multi-home-saved-second", false);
                var c = PrefabGenerationEntry(template, "multi-home-unscoped", false);
                a.HomeScene = secondScene.name;
                b.HomeScene = firstScene.name;
                c.HomeScene = null;
                a.LoadPriority = 90;
                b.LoadPriority = 80;
                c.LoadPriority = 70;
                var entries = new List<SaveablePrefabData> { a, b, c };
                for (int i = 0; i < entries.Count; i++)
                {
                    entries[i].ReuseSceneInstanceOnLoad = true;
                    entries[i].RuntimeModificationData = null;
                    entries[i].GameObjectName = "MultiHomeRestored" + i;
                    entries[i].Position = new Vector3(11 + i * 10, 23, 37);
                }
                yield return pm.InstantiatePrefabsCoroutine(entries, new List<string>(),
                    clearExistingPrefabs: false, handleDeferral: false);
                var map = pm.GetInstantiatedPrefabs();
                Assert.That(map[a.InstanceID], Is.SameAs(second.gameObject));
                Assert.That(map[b.InstanceID], Is.SameAs(first.gameObject));
                Assert.That(map[c.InstanceID], Is.Not.SameAs(first.gameObject).And.Not.SameAs(second.gameObject));
                Assert.That(first.gameObject.scene, Is.EqualTo(firstScene));
                Assert.That(second.gameObject.scene, Is.EqualTo(secondScene));
                foreach (var entry in entries)
                {
                    Assert.That(map[entry.InstanceID].name, Is.EqualTo(entry.GameObjectName));
                    Assert.That(Vector3.Distance(map[entry.InstanceID].transform.position, entry.Position), Is.LessThan(.001f));
                }
                Assert.That(pm.GetSaveablePrefabs().Count, Is.EqualTo(3));
            }
            finally { SceneManager.SetActiveScene(firstScene); }
            yield return SceneManager.UnloadSceneAsync(secondScene);
        }

        [UnityTest]
        public IEnumerator LegacyReuse_HomeScopedClaimsAcceptOnlyPersistentDontDestroyCandidates()
        {
            var manager = SaveManager.Instance;
            manager.SaveSettings.enableAssetBasedReuseDuringDeferred = false;
            manager.SaveSettings.usePrefabPooling = false;
            var pm = manager.GetPrefabManager;
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "/Resources/CrystalSaveLifecyclePrefab.prefab")
                .GetComponent<SaveablePrefab>();
            string home = SceneManager.GetActiveScene().name;
            for (int flags = 0; flags < 4; flags++)
            {
                bool persistent = (flags & 1) != 0;
                bool deferred = (flags & 2) != 0;
                string designId = "persistent-control-design-" + flags;
                var candidate = LegacyReuseCandidate(template.PrefabAssetID, designId);
                candidate.KeepAcrossScenes = persistent;
                candidate.ReuseSceneInstanceOnLoad = true;
                UnityEngine.Object.DontDestroyOnLoad(candidate.gameObject);
                Assert.That(candidate.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
                pm.RegisterPrefab(candidate);
                var data = PrefabGenerationEntry(template, "persistent-control-saved-" + flags, deferred);
                data.HomeScene = home;
                data.ReuseSceneInstanceOnLoad = true;
                data.RuntimeModificationData = null;
                data.Position = new Vector3(19, 29, 41);
                yield return pm.InstantiatePrefabsCoroutine(new List<SaveablePrefabData> { data },
                    new List<string>(), clearExistingPrefabs: false);
                if (deferred) yield return pm.ProcessDeferredPrefabByUniqueID(data.InstanceID);
                var restored = pm.GetInstantiatedPrefabs()[data.InstanceID];
                Assert.That(restored == candidate.gameObject, Is.EqualTo(persistent), "Flags: " + flags);
                Assert.That(Vector3.Distance(restored.transform.position, data.Position), Is.LessThan(.001f));
                if (persistent)
                {
                    Assert.That(candidate.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
                    Assert.That(pm.GetInstantiatedPrefabs().ContainsKey(designId), Is.False);
                }
                else
                {
                    Assert.That(candidate.UniqueID, Is.EqualTo(designId));
                    Assert.That(candidate.transform.position, Is.EqualTo(Vector3.zero));
                    pm.UnregisterPrefab(candidate);
                    UnityEngine.Object.Destroy(candidate.gameObject);
                }
                pm.UnregisterPrefab(restored.GetComponent<SaveablePrefab>());
                UnityEngine.Object.Destroy(restored);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator LegacyReuse_DeferredRequestHonorsExplicitFlagsAndRuntimeExclusion()
        {
            var manager = SaveManager.Instance;
            manager.SaveSettings.enableAssetBasedReuseDuringDeferred = false;
            manager.SaveSettings.usePrefabPooling = false;
            var pm = manager.GetPrefabManager;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "/Resources/CrystalSaveLifecyclePrefab.prefab");
            var template = asset.GetComponent<SaveablePrefab>();
            for (int flags = 0; flags < 8; flags++)
            {
                bool candidateFlag = (flags & 1) != 0;
                bool dataFlag = (flags & 2) != 0;
                bool runtime = (flags & 4) != 0;
                var target = LegacyReuseCandidate(template.PrefabAssetID, "legacy-flags-design-" + flags);
                target.ReuseSceneInstanceOnLoad = candidateFlag;
                if (runtime) target.MarkAsAddedAtRuntime();
                pm.RegisterPrefab(target);
                var data = PrefabGenerationEntry(template, "legacy-flags-saved-" + flags, true);
                data.ReuseSceneInstanceOnLoad = dataFlag;
                data.Position = new Vector3(7 + flags, 11, 13);
                yield return pm.InstantiatePrefabsCoroutine(new List<SaveablePrefabData> { data },
                    new List<string>(), clearExistingPrefabs: false);
                Assert.That(pm.PeekDeferredPrefabsForScene(null), Has.Count.EqualTo(1));
                Assert.That(pm.GetInstantiatedPrefabs().ContainsKey(data.InstanceID), Is.False);
                yield return pm.ProcessDeferredPrefabByUniqueID(data.InstanceID);
                var restored = pm.GetInstantiatedPrefabs()[data.InstanceID];
                bool shouldClaim = !runtime && (candidateFlag || dataFlag);
                Assert.That(restored == target.gameObject, Is.EqualTo(shouldClaim), "Flag combination " + flags);
                Assert.That(restored.name, Is.EqualTo(data.GameObjectName));
                Assert.That(Vector3.Distance(restored.transform.position, data.Position), Is.LessThan(.001f));
                Assert.That(pm.HasDeferredPrefabs, Is.False);
                if (!shouldClaim)
                {
                    Assert.That(target.UniqueID, Is.EqualTo("legacy-flags-design-" + flags));
                    Assert.That(target.transform.position, Is.EqualTo(Vector3.zero));
                    pm.UnregisterPrefab(target);
                    UnityEngine.Object.Destroy(target.gameObject);
                }
                pm.UnregisterPrefab(restored.GetComponent<SaveablePrefab>());
                UnityEngine.Object.Destroy(restored);
                yield return null;
            }
        }

        private static SaveablePrefab LegacyReuseCandidate(string assetId, string instanceId)
        {
            var go = new GameObject("LegacyReuseCandidate-" + instanceId);
            go.SetActive(false);
            var candidate = go.AddComponent<SaveablePrefab>();
            Set(candidate, "prefabAssetID", assetId);
            Set(candidate, "uniqueID", instanceId);
            candidate.KeepAcrossScenes = false;
            return candidate;
        }
    }
}
#endif
