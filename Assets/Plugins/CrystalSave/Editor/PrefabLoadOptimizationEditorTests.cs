#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class PrefabLoadOptimizationEditorTests
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private readonly List<ScriptableObject> createdScriptableObjects =
            new List<ScriptableObject>();
        private readonly List<string> createdAssetPaths = new List<string>();
        private PrefabManager prefabManager;
        private bool previousIgnoreFailingMessages;
        private PropertyInfo saveManagerInstanceProperty;
        private SaveManager previousSaveManager;
        private LogLevel previousLogThreshold;
        private bool poolCacheTouched;

        [SetUp]
        public void SetUp()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            var root = new GameObject("PrefabLoadOptimizationTestManager");
            root.SetActive(false);
            createdObjects.Add(root);
            prefabManager = root.AddComponent<PrefabManager>();

            saveManagerInstanceProperty = typeof(SaveManager).GetProperty(
                nameof(SaveManager.Instance),
                StaticFlags);
            Assert.That(saveManagerInstanceProperty, Is.Not.Null);
            previousSaveManager =
                (SaveManager)saveManagerInstanceProperty.GetValue(null);
            previousLogThreshold =
                Arawn.CrystalSave.Runtime.Logger.LogThreshold;
            PrefabLoadDiagnosticCounters.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            if (saveManagerInstanceProperty != null)
                saveManagerInstanceProperty.SetValue(null, previousSaveManager);

            Arawn.CrystalSave.Runtime.Logger.LogThreshold =
                previousLogThreshold;

            if (poolCacheTouched)
                ResetPoolCache();

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();

            for (int i = createdScriptableObjects.Count - 1; i >= 0; i--)
            {
                if (createdScriptableObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(createdScriptableObjects[i]);
            }

            createdScriptableObjects.Clear();

            for (int i = createdAssetPaths.Count - 1; i >= 0; i--)
                AssetDatabase.DeleteAsset(createdAssetPaths[i]);

            createdAssetPaths.Clear();
            PrefabLoadDiagnosticCounters.Reset();
            LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
        }

        [TestCase(2)]
        [TestCase(228)]
        [TestCase(1024)]
        public void RealBatchUsesOneSnapshotAndLinearIndexedProbes(int entryCount)
        {
            const string prefabAssetId = "real-batch-asset";
            CreateRegisteredPrefabAsset(prefabAssetId);

            var entries = new List<SaveablePrefabData>(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                SaveablePrefabData data = CreateData(
                    $"batch-{entryCount}-{i}",
                    prefabAssetId);
                data.Position = new Vector3(i, i % 7, -(i % 11));
                entries.Add(data);
            }

            PrefabLoadDiagnosticCounters.Reset();
            Drain(prefabManager.InstantiatePrefabsCoroutine(
                entries,
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Dictionary<string, GameObject> mappings = GetMappings();
            TrackMappedObjects(mappings);

            Assert.That(mappings, Has.Count.EqualTo(entryCount));
            Assert.That(prefabManager.SaveablePrefabs, Has.Count.EqualTo(entryCount));
            for (int i = 0; i < entryCount; i++)
            {
                SaveablePrefabData data = entries[i];
                Assert.That(mappings.TryGetValue(data.InstanceID, out GameObject instance), Is.True);
                Assert.That(instance, Is.Not.Null);
                Assert.That(instance.name, Is.EqualTo(data.GameObjectName));
                Assert.That(instance.transform.position, Is.EqualTo(data.Position));
                Assert.That(instance.TryGetComponent(out SaveablePrefab restored), Is.True);
                Assert.That(restored.UniqueID, Is.EqualTo(data.InstanceID));
                Assert.That(restored.PrefabAssetID, Is.EqualTo(prefabAssetId));
            }

            PrefabLoadDiagnosticSnapshot counters = PrefabLoadDiagnosticCounters.Capture();
            Assert.That(counters.BatchPreparations, Is.EqualTo(1));
            Assert.That(counters.ContextBuilds, Is.EqualTo(1));
            Assert.That(counters.SceneSnapshots, Is.EqualTo(1));
            Assert.That(counters.PublicLookupCalls, Is.Zero);
            Assert.That(counters.IndexedResolutionProbes, Is.EqualTo(entryCount));
            Assert.That(counters.DestroyedIdProbes, Is.EqualTo(entryCount));
            Assert.That(counters.PrefabDescriptions, Is.Zero);
            Assert.That(counters.PrefabDataDescriptions, Is.Zero);
            Assert.That(counters.TransformPathDescriptions, Is.Zero);
            Assert.That(counters.RigidbodyProbes, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OptionalGlobalLookupCacheDoesNotDisableThePrefabBatchIndex(bool enableCache)
        {
            var manager = InstallSaveManager(CreateSettings());
            manager.SetLookupCacheEnabled(enableCache);
            RealBatchUsesOneSnapshotAndLinearIndexedProbes(228);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DistinctInstanceIdsWithSameSavedNameRestoreAsDistinctObjects(
            bool clearExistingPrefabs)
        {
            const string prefabAssetId = "same-name-runtime-prefab-asset";
            const string sharedSavedName = "RepeatedRuntimePrefab(Clone)";
            CreateRegisteredPrefabAsset(prefabAssetId);

            var entries = new List<SaveablePrefabData>(3);
            for (int i = 0; i < 3; i++)
            {
                SaveablePrefabData data = CreateData(
                    $"same-name-runtime-instance-{i}",
                    prefabAssetId);
                data.GameObjectName = sharedSavedName;
                data.Position = new Vector3(i * 10f, i, -i);
                data.ReuseSceneInstanceOnLoad = false;
                entries.Add(data);
            }

            Drain(prefabManager.InstantiatePrefabsCoroutine(
                entries,
                new List<string>(),
                clearExistingPrefabs,
                handleDeferral: false));

            Dictionary<string, GameObject> mappings = GetMappings();
            TrackMappedObjects(mappings);
            var restoredObjects = new HashSet<GameObject>();

            Assert.That(prefabManager.SaveablePrefabs, Has.Count.EqualTo(3));
            for (int i = 0; i < entries.Count; i++)
            {
                SaveablePrefabData data = entries[i];
                Assert.That(
                    mappings.TryGetValue(data.InstanceID, out GameObject restored),
                    Is.True);
                Assert.That(restored, Is.Not.Null);
                Assert.That(restoredObjects.Add(restored), Is.True,
                    $"InstanceID '{data.InstanceID}' aliased a previously restored object.");
                Assert.That(restored.name, Is.EqualTo(sharedSavedName));
                Assert.That(restored.transform.position, Is.EqualTo(data.Position));
                Assert.That(
                    restored.GetComponent<SaveablePrefab>().UniqueID,
                    Is.EqualTo(data.InstanceID));
            }

            Assert.That(restoredObjects, Has.Count.EqualTo(3));
        }

        [Test]
        public void ExactIdRejectionPromotesSeededSceneShadowCandidate()
        {
            const string duplicateId = "owner-chain-duplicate-id";

            SaveablePrefab seeded = CreateInactiveSaveable(
                "A-SeededExactOwner",
                duplicateId);
            SaveablePrefab sceneShadow = CreateInactiveSaveable(
                "B-SceneExactShadow",
                duplicateId);
            GetMappings()[duplicateId] = seeded.gameObject;

            object context = CreateLoadContext(seeded, sceneShadow);
            SaveablePrefabData data = CreateData(
                duplicateId,
                prefabId: string.Empty);

            Assert.That(
                TryResolveContext(context, data, out GameObject first),
                Is.True);
            Assert.That(first, Is.SameAs(seeded.gameObject));

            InvokeContext(
                context,
                "RejectResolution",
                data,
                seeded.gameObject);

            Assert.That(
                TryResolveContext(context, data, out GameObject promoted),
                Is.True);
            Assert.That(promoted, Is.SameAs(sceneShadow.gameObject));
        }

        [Test]
        public void DuplicateCloneAliasPromotesAfterCurrentOwnerIsDeactivated()
        {
            var aliasParent = new GameObject("AliasOwnerChainParent");
            createdObjects.Add(aliasParent);

            SaveablePrefab first = CreateInactiveSaveable(
                "DuplicateLegacyAlias(Clone)",
                "alias-owner-first");
            SaveablePrefab second = CreateInactiveSaveable(
                "DuplicateLegacyAlias(Clone)",
                "alias-owner-second");
            first.transform.SetParent(aliasParent.transform);
            second.transform.SetParent(aliasParent.transform);
            first.transform.SetSiblingIndex(0);
            second.transform.SetSiblingIndex(1);
            first.gameObject.SetActive(true);
            second.gameObject.SetActive(true);

            object context = CreateLoadContext(second, first);
            SaveablePrefabData data = CreateData(
                instanceId: string.Empty,
                prefabId: string.Empty);
            data.GameObjectName = "DuplicateLegacyAlias";

            Assert.That(
                TryResolveContext(context, data, out GameObject initial),
                Is.True);
            Assert.That(initial, Is.SameAs(first.gameObject));

            // TryGetActiveName must discard only this stale owner and promote
            // the second alias without requiring Refresh or another scene scan.
            first.gameObject.SetActive(false);

            Assert.That(
                TryResolveContext(context, data, out GameObject promoted),
                Is.True);
            Assert.That(promoted, Is.SameAs(second.gameObject));
        }

        [Test]
        public void LegacySerializedPrefabIdAvoidsGeneralRegistrationScansDuringRestore()
        {
            const string prefabAssetId = "legacy-serialized-id-asset";
            const string legacySerializedId = "legacy-prefab-instance-id";
            const string restoredId = "restored-prefab-instance-id";

            CreateRegisteredPrefabAsset(
                prefabAssetId,
                serializedInstanceId: legacySerializedId);

            MethodInfo onEnable = typeof(PrefabManager).GetMethod(
                "OnEnable",
                InstanceFlags);
            MethodInfo onDisable = typeof(PrefabManager).GetMethod(
                "OnDisable",
                InstanceFlags);
            Assert.That(onEnable, Is.Not.Null);
            Assert.That(onDisable, Is.Not.Null);

            onEnable.Invoke(prefabManager, null);
            try
            {
                PrefabLoadDiagnosticCounters.Reset();
                Drain(prefabManager.InstantiatePrefabsCoroutine(
                    new List<SaveablePrefabData>
                    {
                        CreateData(restoredId, prefabAssetId)
                    },
                    new List<string>(),
                    clearExistingPrefabs: false,
                    handleDeferral: false));
            }
            finally
            {
                onDisable.Invoke(prefabManager, null);
            }

            Dictionary<string, GameObject> mappings = GetMappings();
            TrackMappedObjects(mappings);
            Assert.That(mappings[restoredId], Is.Not.Null);
            Assert.That(
                mappings[restoredId].GetComponent<SaveablePrefab>().UniqueID,
                Is.EqualTo(restoredId));
            Assert.That(
                mappings.ContainsKey(legacySerializedId),
                Is.False);
            Assert.That(
                PrefabLoadDiagnosticCounters.Capture()
                    .LegacyRegistrationCollectionScans,
                Is.Zero);
        }

        [Test]
        public void IndexedReuseRestoresExistingSceneInstanceWithoutSpawning()
        {
            const string instanceId = "indexed-scene-reuse";
            const string prefabAssetId = "indexed-scene-reuse-asset";

            SaveablePrefab candidate = CreateInactiveSaveable(
                "IndexedSceneReuseCandidate",
                instanceId);
            candidate.PrefabAssetID = prefabAssetId;
            candidate.ReuseSceneInstanceOnLoad = true;
            prefabManager.RegisterPrefab(candidate);

            SaveablePrefabData data = CreateData(instanceId, prefabAssetId);
            data.GameObjectName = "IndexedSceneReuseRestored";
            data.Position = new Vector3(11f, 12f, 13f);
            data.ReuseSceneInstanceOnLoad = true;

            PrefabLoadDiagnosticCounters.Reset();
            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { data },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Dictionary<string, GameObject> mappings = GetMappings();
            Assert.That(mappings[instanceId], Is.SameAs(candidate.gameObject));
            Assert.That(candidate.name, Is.EqualTo(data.GameObjectName));
            Assert.That(candidate.transform.position, Is.EqualTo(data.Position));
            Assert.That(prefabManager.SaveablePrefabs, Has.Count.EqualTo(1));
            Assert.That(
                PrefabLoadDiagnosticCounters.Capture().IndexedResolutionProbes,
                Is.EqualTo(1));
            Assert.That(
                PrefabLoadDiagnosticCounters.Capture().PublicLookupCalls,
                Is.Zero);
        }

        [Test]
        public void ExplicitCrossSceneHandoffClaimsTargetWhenDeferredAssetReuseIsDisabled()
        {
            const string sourceInstanceId = "handoff-source-instance";
            const string targetInstanceId = "handoff-target-instance";
            const string prefabAssetId = "handoff-scene-asset";
            var targetDesignPosition = new Vector3(4f, 5f, 6f);

            SaveSettings settings = CreateSettings();
            settings.enableAssetBasedReuseDuringDeferred = false;
            InstallSaveManager(settings);

            SaveablePrefab target = CreateInactiveSaveable(
                "CrossSceneHandoffTarget",
                targetInstanceId);
            target.PrefabAssetID = prefabAssetId;
            target.KeepAcrossScenes = false;
            target.ReuseSceneInstanceOnLoad = false;
            target.transform.position = targetDesignPosition;
            prefabManager.RegisterPrefab(target);

            SaveablePrefabData data = CreateData(
                sourceInstanceId,
                prefabAssetId);
            data.GameObjectName = "CrossSceneHandoffRestored";
            data.Position = new Vector3(40f, 50f, 60f);
            data.ReuseSceneInstanceOnLoad = false;
            data.EnableCrossSceneStateHandoff = true;
            data.HandoffToSceneBakedPrefabs = true;
            data.HandoffTransform = false;
            data.HandoffRuntimeModifications = false;

            PrefabLoadDiagnosticCounters.Reset();
            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { data },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Dictionary<string, GameObject> mappings = GetMappings();
            Assert.That(mappings.ContainsKey(targetInstanceId), Is.False);
            Assert.That(
                mappings[sourceInstanceId],
                Is.SameAs(target.gameObject));
            Assert.That(target.UniqueID, Is.EqualTo(sourceInstanceId));
            Assert.That(target.transform.position, Is.EqualTo(targetDesignPosition));
            Assert.That(target.EnableCrossSceneStateHandoff, Is.True);
            Assert.That(target.HandoffToSceneBakedPrefabs, Is.True);
            Assert.That(target.HandoffTransform, Is.False);
            Assert.That(target.HandoffRuntimeModifications, Is.False);
            Assert.That(prefabManager.SaveablePrefabs, Has.Count.EqualTo(1));

            PrefabLoadDiagnosticSnapshot counters =
                PrefabLoadDiagnosticCounters.Capture();
            Assert.That(counters.SceneSnapshots, Is.EqualTo(1));
            Assert.That(counters.PublicLookupCalls, Is.Zero);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void CrossSceneHandoffMasksIndependentlyControlActualOverrides(bool transform, bool modifications)
        {
            var settings = CreateSettings();
            settings.enableAssetBasedReuseDuringDeferred = false;
            InstallSaveManager(settings);
            var target = CreateInactiveSaveable("HandoffDesign", "handoff-design-id");
            target.PrefabAssetID = "handoff-mask-asset";
            target.ReuseSceneInstanceOnLoad = false;
            target.transform.SetPositionAndRotation(new Vector3(4, 5, 6), Quaternion.Euler(10, 20, 30));
            target.transform.localScale = new Vector3(2, 3, 4);
            Vector3 designPosition = target.transform.position;
            Quaternion designRotation = target.transform.rotation;
            Vector3 designScale = target.transform.localScale;
            target.gameObject.layer = 0;
            prefabManager.RegisterPrefab(target);

            var data = CreateData("handoff-saved-id", target.PrefabAssetID);
            data.EnableCrossSceneStateHandoff = true;
            data.HandoffToSceneBakedPrefabs = true;
            data.HandoffTransform = transform;
            data.HandoffRuntimeModifications = modifications;
            data.Position = new Vector3(40, 50, 60);
            data.Rotation = Quaternion.Euler(70, 80, 90);
            data.Scale = new Vector3(5, 6, 7);
            data.RuntimeModificationData = SaveDataSerializer.Instance.Serialize(new RuntimeModificationData
            {
                RootState = new RootStateOverride { Layer = 2 }
            });
            Drain(prefabManager.InstantiatePrefabsCoroutine(new List<SaveablePrefabData> { data },
                new List<string>(), clearExistingPrefabs: false, handleDeferral: false));

            Assert.That(GetMappings()[data.InstanceID], Is.SameAs(target.gameObject));
            Assert.That(GetMappings().ContainsKey("handoff-design-id"), Is.False);
            Assert.That(target.transform.position, Is.EqualTo(transform ? data.Position : designPosition));
            Assert.That(Quaternion.Angle(target.transform.rotation, transform ? data.Rotation : designRotation), Is.LessThan(.01f));
            Assert.That(target.transform.localScale, Is.EqualTo(transform ? data.Scale : designScale));
            Assert.That(target.gameObject.layer, Is.EqualTo(modifications ? 2 : 0));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void CrossSceneHandoffRequiresExactlyOneBakedCandidateWithRuntimeControl(int bakedCount)
        {
            var settings = CreateSettings();
            settings.enableAssetBasedReuseDuringDeferred = false;
            InstallSaveManager(settings);
            CreateRegisteredPrefabAsset("handoff-candidate-asset");
            var runtime = CreateInactiveSaveable("RuntimeHandoffControl", "runtime-control");
            runtime.MarkAsAddedAtRuntime();
            var baked = new List<SaveablePrefab>();
            for (int i = 0; i < bakedCount; i++)
                baked.Add(CreateInactiveSaveable("BakedHandoff" + i, "baked-" + i));
            var candidates = new List<SaveablePrefab>(baked) { runtime };
            foreach (var target in candidates)
            {
                target.PrefabAssetID = "handoff-candidate-asset";
                target.ReuseSceneInstanceOnLoad = false;
                prefabManager.RegisterPrefab(target);
            }
            var data = CreateData("handoff-claimed-id", "handoff-candidate-asset");
            data.EnableCrossSceneStateHandoff = true;
            data.HandoffToSceneBakedPrefabs = true;
            Drain(prefabManager.InstantiatePrefabsCoroutine(new List<SaveablePrefabData> { data },
                new List<string>(), clearExistingPrefabs: false, handleDeferral: false));

            var restored = GetMappings()[data.InstanceID];
            TrackMappedObjects(GetMappings());
            Assert.That(runtime.UniqueID, Is.EqualTo("runtime-control"));
            Assert.That(GetMappings()["runtime-control"], Is.SameAs(runtime.gameObject));
            Assert.That(restored, Is.Not.SameAs(runtime.gameObject));
            if (bakedCount == 1)
            {
                Assert.That(restored, Is.SameAs(baked[0].gameObject));
                Assert.That(baked[0].UniqueID, Is.EqualTo(data.InstanceID));
                Assert.That(GetMappings().ContainsKey("baked-0"), Is.False);
            }
            else
            {
                for (int i = 0; i < baked.Count; i++)
                {
                    Assert.That(restored, Is.Not.SameAs(baked[i].gameObject));
                    Assert.That(baked[i].UniqueID, Is.EqualTo("baked-" + i));
                    Assert.That(GetMappings()["baked-" + i], Is.SameAs(baked[i].gameObject));
                }
            }
            Assert.That(prefabManager.GetSaveablePrefabs(), Has.Count.EqualTo(1 + bakedCount + (bakedCount == 1 ? 0 : 1)));
        }

        [Test]
        public void AssetMismatchRejectsIndexedCandidateAndInstantiatesReplacement()
        {
            const string instanceId = "rejected-asset-mismatch";
            const string expectedAssetId = "expected-asset";

            CreateRegisteredPrefabAsset(expectedAssetId);
            SaveablePrefab wrongCandidate = CreateInactiveSaveable(
                "WrongAssetCandidate",
                instanceId);
            wrongCandidate.PrefabAssetID = "wrong-asset";
            GetMappings()[instanceId] = wrongCandidate.gameObject;

            PrefabLoadDiagnosticCounters.Reset();
            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData>
                {
                    CreateData(instanceId, expectedAssetId)
                },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Dictionary<string, GameObject> mappings = GetMappings();
            TrackMappedObjects(mappings);
            Assert.That(mappings[instanceId], Is.Not.SameAs(wrongCandidate.gameObject));
            Assert.That(
                mappings[instanceId].GetComponent<SaveablePrefab>().PrefabAssetID,
                Is.EqualTo(expectedAssetId));
            Assert.That(wrongCandidate, Is.Not.Null);
            Assert.That(
                PrefabLoadDiagnosticCounters.Capture().PublicLookupCalls,
                Is.Zero);
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        public void DeferredReuseHonorsEitherExplicitFlagWithoutClaimingRuntimeObjects(
            bool candidateRequestsReuse, bool dataRequestsReuse, bool runtimeCandidate)
        {
            const string assetId = "deferred-explicit-flags-asset";
            var settings = CreateSettings();
            settings.enableAssetBasedReuseDuringDeferred = false;
            InstallSaveManager(settings);
            CreateRegisteredPrefabAsset(assetId);
            var target = CreateInactiveSaveable("DeferredDesign", "deferred-design-id");
            target.PrefabAssetID = assetId;
            target.ReuseSceneInstanceOnLoad = candidateRequestsReuse;
            if (runtimeCandidate) target.MarkAsAddedAtRuntime();
            prefabManager.RegisterPrefab(target);
            var data = CreateData("deferred-saved-id", assetId);
            data.ReuseSceneInstanceOnLoad = dataRequestsReuse;
            data.GameObjectName = "DeferredRestored";
            data.Position = new Vector3(5, 7, 11);
            data.DeferLowPriorityUntilRequested = true;

            Drain(prefabManager.InstantiatePrefabsCoroutine(new List<SaveablePrefabData> { data },
                new List<string>(), clearExistingPrefabs: false, handleDeferral: false));
            TrackMappedObjects(GetMappings());
            var restored = GetMappings()[data.InstanceID];
            bool shouldClaim = !runtimeCandidate && (candidateRequestsReuse || dataRequestsReuse);
            Assert.That(restored == target.gameObject, Is.EqualTo(shouldClaim));
            Assert.That(restored.name, Is.EqualTo(data.GameObjectName));
            Assert.That(restored.transform.position, Is.EqualTo(data.Position));
            Assert.That(restored.GetComponent<SaveablePrefab>().UniqueID, Is.EqualTo(data.InstanceID));
            Assert.That(prefabManager.RegisteredPrefabs.Count, Is.EqualTo(shouldClaim ? 1 : 2));
            if (shouldClaim)
                Assert.That(GetMappings().ContainsKey("deferred-design-id"), Is.False);
            else
            {
                Assert.That(target.UniqueID, Is.EqualTo("deferred-design-id"));
                Assert.That(target.name, Is.EqualTo("DeferredDesign"));
                Assert.That(target.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(GetMappings()[target.UniqueID], Is.SameAs(target.gameObject));
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ClaimedSceneTargetRestoresCompleteParentAwarePoseDespiteSourceOptimizationFlags(
            bool handoff, bool optimizedSource)
        {
            var settings = CreateSettings();
            settings.enableAssetBasedReuseDuringDeferred = false;
            InstallSaveManager(settings);
            var parent = CreateInactiveSaveable("SavedParent", "saved-parent-id");
            parent.PrefabAssetID = "saved-parent-asset";
            parent.transform.SetPositionAndRotation(new Vector3(100, 200, -300), Quaternion.Euler(0, 65, 0));
            parent.transform.localScale = new Vector3(2, 2, 2);
            prefabManager.RegisterPrefab(parent);
            var designParent = CreateInactiveSaveable("DesignParent", "design-parent-id");
            designParent.PrefabAssetID = "design-parent-asset";
            var target = CreateInactiveSaveable("DesignTarget", "design-target-id");
            target.PrefabAssetID = "parented-reuse-asset";
            target.transform.SetParent(designParent.transform, false);
            target.transform.localPosition = new Vector3(-11, -13, -17);
            target.transform.localRotation = Quaternion.Euler(5, 10, 15);
            target.transform.localScale = Vector3.one * 4;
            prefabManager.RegisterPrefab(target);
            var data = CreateData("parented-saved-id", target.PrefabAssetID);
            data.ReuseSceneInstanceOnLoad = !handoff;
            data.EnableCrossSceneStateHandoff = handoff;
            data.HandoffToSceneBakedPrefabs = true;
            data.HandoffTransform = true;
            data.GameObjectName = "RestoredChild";
            data.ParentID = parent.UniqueID;
            data.Position = new Vector3(3, 5, 7);
            data.Rotation = Quaternion.Euler(23, 37, 59);
            data.Scale = new Vector3(1.5f, 2.5f, 3.5f);
            data.UsesOptimizationFlags = optimizedSource;
            data.HasParentOverride = !optimizedSource;
            data.HasTransformOverride = !optimizedSource;
            Drain(prefabManager.InstantiatePrefabsCoroutine(new List<SaveablePrefabData> { data },
                new List<string>(), clearExistingPrefabs: false, handleDeferral: false));

            Assert.That(GetMappings()[data.InstanceID], Is.SameAs(target.gameObject));
            Assert.That(GetMappings().ContainsKey("design-target-id"), Is.False);
            Assert.That(target.name, Is.EqualTo(data.GameObjectName));
            Assert.That(target.transform.parent, Is.SameAs(parent.transform));
            Assert.That(Vector3.Distance(target.transform.localPosition, data.Position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(target.transform.localRotation, data.Rotation), Is.LessThan(.01f));
            Assert.That(target.transform.localScale, Is.EqualTo(data.Scale));
            Assert.That(Vector3.Distance(target.transform.position, parent.transform.TransformPoint(data.Position)), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(target.transform.rotation, parent.transform.rotation * data.Rotation), Is.LessThan(.01f));
            Assert.That(designParent.transform.childCount, Is.Zero);
            Assert.That(parent.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateAndDestroyedEntriesProduceOnlyOneLiveInstance()
        {
            const string prefabAssetId = "duplicate-destroyed-asset";
            const string liveId = "deduplicated-live-id";
            const string destroyedId = "destroyed-id";

            CreateRegisteredPrefabAsset(prefabAssetId);
            SaveablePrefabData lowerPriority = CreateData(liveId, prefabAssetId);
            lowerPriority.LoadPriority = 1;
            SaveablePrefabData preferred = CreateData(liveId, prefabAssetId);
            preferred.LoadPriority = 99;
            preferred.Position = new Vector3(7f, 8f, 9f);

            PrefabLoadDiagnosticCounters.Reset();
            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData>
                {
                    lowerPriority,
                    CreateData(destroyedId, prefabAssetId),
                    preferred
                },
                new List<string> { destroyedId, destroyedId },
                clearExistingPrefabs: false,
                handleDeferral: false));

            Dictionary<string, GameObject> mappings = GetMappings();
            TrackMappedObjects(mappings);
            Assert.That(mappings.Keys, Is.EquivalentTo(new[] { liveId }));
            Assert.That(mappings[liveId].transform.position, Is.EqualTo(preferred.Position));
            Assert.That(prefabManager.SaveablePrefabs, Has.Count.EqualTo(1));

            PrefabLoadDiagnosticSnapshot counters =
                PrefabLoadDiagnosticCounters.Capture();
            Assert.That(counters.BatchPreparations, Is.EqualTo(1));
            Assert.That(counters.ContextBuilds, Is.EqualTo(1));
            Assert.That(counters.SceneSnapshots, Is.EqualTo(1));
            Assert.That(counters.IndexedResolutionProbes, Is.EqualTo(1));
            Assert.That(counters.DestroyedIdProbes, Is.EqualTo(2));
        }

        [Test]
        public void DestroyedExistingSceneReuseOwnerIsRemovedBeforeClearPreservation()
        {
            const string instanceId = "destroyed-existing-reuse-owner";
            const string prefabAssetId = "destroyed-existing-reuse-asset";

            SaveablePrefab existing = CreateInactiveSaveable(
                "DestroyedExistingReuseOwner",
                instanceId);
            existing.PrefabAssetID = prefabAssetId;
            existing.ReuseSceneInstanceOnLoad = true;
            prefabManager.SaveablePrefabs.Add(existing);
            GetMappings()[instanceId] = existing.gameObject;
            GetMappings()["destroyed-owner-stale-alias"] =
                existing.gameObject;

            SaveablePrefabData data = CreateData(instanceId, prefabAssetId);
            data.ReuseSceneInstanceOnLoad = true;

            PrefabLoadDiagnosticCounters.Reset();
            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { data },
                new List<string> { instanceId },
                clearExistingPrefabs: true,
                handleDeferral: false));

            Assert.That(existing == null, Is.True);
            Assert.That(GetMappings().ContainsKey(instanceId), Is.False);
            Assert.That(
                GetMappings().ContainsKey("destroyed-owner-stale-alias"),
                Is.False);
            Assert.That(prefabManager.SaveablePrefabs, Is.Empty);

            PrefabLoadDiagnosticSnapshot counters =
                PrefabLoadDiagnosticCounters.Capture();
            Assert.That(counters.DestroyedIdProbes, Is.EqualTo(1));
            Assert.That(counters.IndexedResolutionProbes, Is.Zero);
            Assert.That(counters.PreserveIdClaimProbes, Is.Zero);
        }

        [TestCase(2)]
        [TestCase(228)]
        public void ClearPreservationClaimsSceneCandidatesWithLinearProbes(
            int candidateCount)
        {
            const string prefabAssetId = "linear-clear-preserve-asset";
            var preserveIds = new List<string>(candidateCount);
            var preserveMap =
                new Dictionary<string, string>(
                    candidateCount,
                    StringComparer.Ordinal);
            var candidates = new SaveablePrefab[candidateCount];

            for (int i = 0; i < candidateCount; i++)
            {
                string id = $"linear-clear-preserve-{i:D4}";
                SaveablePrefab candidate = CreateInactiveSaveable(
                    $"LinearClearCandidate-{i:D4}",
                    instanceId: string.Empty);
                candidate.PrefabAssetID = prefabAssetId;
                candidate.ReuseSceneInstanceOnLoad = true;
                prefabManager.SaveablePrefabs.Add(candidate);
                candidates[i] = candidate;
                preserveIds.Add(id);
                preserveMap.Add(id, prefabAssetId);
            }

            object context = CreateLoadContext(candidates);
            MethodInfo clear = typeof(PrefabManager).GetMethod(
                "ClearSaveablePrefabsInternal",
                InstanceFlags);
            Assert.That(clear, Is.Not.Null);

            PrefabLoadDiagnosticCounters.Reset();
            clear.Invoke(
                prefabManager,
                new object[]
                {
                    false,
                    preserveIds,
                    preserveMap,
                    context
                });

            Assert.That(
                prefabManager.SaveablePrefabs,
                Has.Count.EqualTo(candidateCount));
            Assert.That(
                GetMappings().Keys,
                Is.EquivalentTo(preserveIds));
            for (int i = 0; i < candidates.Length; i++)
            {
                Assert.That(candidates[i], Is.Not.Null);
                Assert.That(
                    preserveMap.ContainsKey(candidates[i].UniqueID),
                    Is.True);
                Assert.That(
                    candidates[i].UniqueID,
                    Is.EqualTo(preserveIds[i]));
            }

            PrefabLoadDiagnosticSnapshot counters =
                PrefabLoadDiagnosticCounters.Capture();
            Assert.That(
                counters.ClearCandidateProbes,
                Is.EqualTo(candidateCount * 2));
            Assert.That(
                counters.PreserveIdClaimProbes,
                Is.EqualTo(candidateCount));
            Assert.That(counters.PublicLookupCalls, Is.Zero);
            Assert.That(
                counters.LegacyRegistrationCollectionScans,
                Is.Zero);
        }

        [TestCase(2)]
        [TestCase(228)]
        public void DestructiveClearSkipsPerPrefabCollectionScans(
            int candidateCount)
        {
            InstallSaveManager(CreateSettings());
            var candidates = new SaveablePrefab[candidateCount];
            Dictionary<string, GameObject> mappings = GetMappings();

            for (int i = 0; i < candidateCount; i++)
            {
                string id = $"linear-destructive-clear-{i:D4}";
                SaveablePrefab candidate = CreateInactiveSaveable(
                    $"LinearDestructiveCandidate-{i:D4}",
                    id);
                candidate.MarkAsAddedAtRuntime();
                candidate.ReuseSceneInstanceOnLoad = false;
                candidate.PrefabAssetID =
                    "linear-destructive-clear-asset";
                prefabManager.SaveablePrefabs.Add(candidate);
                mappings.Add(id, candidate.gameObject);
                candidates[i] = candidate;
            }

            object context = CreateLoadContext(candidates);
            MethodInfo clear = typeof(PrefabManager).GetMethod(
                "ClearSaveablePrefabsInternal",
                InstanceFlags);
            Assert.That(clear, Is.Not.Null);

            PrefabLoadDiagnosticCounters.Reset();
            clear.Invoke(
                prefabManager,
                new object[]
                {
                    false,
                    Array.Empty<string>(),
                    new Dictionary<string, string>(
                        StringComparer.Ordinal),
                    context
                });

            Assert.That(prefabManager.SaveablePrefabs, Is.Empty);
            Assert.That(mappings, Is.Empty);
            for (int i = 0; i < candidates.Length; i++)
                Assert.That(candidates[i] == null, Is.True);

            PrefabLoadDiagnosticSnapshot counters =
                PrefabLoadDiagnosticCounters.Capture();
            Assert.That(
                counters.ClearCandidateProbes,
                Is.EqualTo(candidateCount * 2));
            Assert.That(counters.PreserveIdClaimProbes, Is.Zero);
            Assert.That(
                counters.LegacyRegistrationCollectionScans,
                Is.Zero);
            Assert.That(counters.PublicLookupCalls, Is.Zero);
        }

        [Test]
        public void SecondLoadKeepsCurrentInstanceWithoutResolvingOrRespawning()
        {
            const string instanceId = "second-load-instance";
            const string prefabAssetId = "second-load-asset";

            CreateRegisteredPrefabAsset(prefabAssetId);
            SaveablePrefabData data = CreateData(instanceId, prefabAssetId);

            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { data },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Dictionary<string, GameObject> mappings = GetMappings();
            TrackMappedObjects(mappings);
            GameObject firstInstance = mappings[instanceId];
            int firstInstanceId = UnityObjectHelper.GetUniqueId(firstInstance);

            PrefabLoadDiagnosticCounters.Reset();
            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { data },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Assert.That(mappings[instanceId], Is.SameAs(firstInstance));
            Assert.That(
                UnityObjectHelper.GetUniqueId(mappings[instanceId]),
                Is.EqualTo(firstInstanceId));
            Assert.That(prefabManager.SaveablePrefabs, Has.Count.EqualTo(1));

            PrefabLoadDiagnosticSnapshot counters =
                PrefabLoadDiagnosticCounters.Capture();
            Assert.That(counters.ContextBuilds, Is.EqualTo(1));
            Assert.That(counters.SceneSnapshots, Is.EqualTo(1));
            Assert.That(counters.IndexedResolutionProbes, Is.Zero);
            Assert.That(counters.PublicLookupCalls, Is.Zero);
        }

        [Test]
        public void DestroyedMappingIsPrunedAndReplacedByFreshInstance()
        {
            const string instanceId = "stale-null-replacement";
            const string prefabAssetId = "stale-null-asset";

            CreateRegisteredPrefabAsset(prefabAssetId);
            SaveablePrefab stale = CreateInactiveSaveable(
                "StaleNullCandidate",
                instanceId);
            stale.PrefabAssetID = prefabAssetId;
            GameObject staleObject = stale.gameObject;
            GetMappings()[instanceId] = staleObject;
            createdObjects.Remove(staleObject);
            UnityEngine.Object.DestroyImmediate(staleObject);

            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData>
                {
                    CreateData(instanceId, prefabAssetId)
                },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Dictionary<string, GameObject> mappings = GetMappings();
            TrackMappedObjects(mappings);
            Assert.That(mappings[instanceId], Is.Not.Null);
            Assert.That(mappings[instanceId], Is.Not.SameAs(staleObject));
            Assert.That(
                mappings[instanceId].GetComponent<SaveablePrefab>().UniqueID,
                Is.EqualTo(instanceId));
        }

        [Test]
        public void StaleLifecycleCannotDestroyOrUnregisterLiveReplacementOwner()
        {
            const string instanceId = "stale-lifecycle-owner";
            SaveManager manager = InstallSaveManager(CreateSettings());
            manager.SetLookupCacheEnabled(true);
            GameObjectTracker tracker = manager.GameObjectTracker;

            SaveablePrefab stale = CreateInactiveSaveable(
                "StaleLifecycleOwner",
                instanceId);
            SaveablePrefab replacement = CreateInactiveSaveable(
                "ReplacementLifecycleOwner",
                instanceId);

            prefabManager.RegisterPrefab(stale);
            manager.RegisterGameObject(stale.gameObject, stale.PropertySettings);
            prefabManager.RegisterPrefab(replacement);
            manager.RegisterGameObject(
                replacement.gameObject,
                replacement.PropertySettings);

            Assert.That(
                tracker.TryGetTrackedGameObject(
                    instanceId,
                    out TrackedGameObject trackedBefore),
                Is.True);
            Assert.That(
                trackedBefore.GameObject,
                Is.SameAs(replacement.gameObject));
            Assert.That(
                GetMappings().TryGetValue(
                    instanceId,
                    out GameObject mappedBefore),
                Is.True);
            Assert.That(mappedBefore, Is.SameAs(replacement.gameObject));

            MethodInfo registerFromOwner = typeof(SaveManager).GetMethod(
                "TryRegisterDestroyedGameObjectFromOwner",
                InstanceFlags);
            Assert.That(registerFromOwner, Is.Not.Null);
            bool registered = (bool)registerFromOwner.Invoke(
                manager,
                new object[] { instanceId, stale.gameObject });

            Assert.That(registered, Is.False);
            Assert.That(tracker.IsGameObjectDestroyed(instanceId), Is.False);

            manager.SoftUnregisterGameObject(stale.gameObject);

            Assert.That(
                tracker.TryGetTrackedGameObject(
                    instanceId,
                    out TrackedGameObject trackedAfter),
                Is.True);
            Assert.That(
                trackedAfter.GameObject,
                Is.SameAs(replacement.gameObject));
            Assert.That(
                GetMappings().TryGetValue(
                    instanceId,
                    out GameObject mappedAfter),
                Is.True);
            Assert.That(mappedAfter, Is.SameAs(replacement.gameObject));

            var lookupCache = FeatureTestSupport.CachedObjects(manager);
            Assert.That(
                lookupCache[instanceId],
                Is.SameAs(replacement.gameObject));

            var activeStates = GetField<Dictionary<string, bool>>(
                tracker,
                "activeStates");
            Assert.That(activeStates.ContainsKey(instanceId), Is.True);

            // The hard-unregister path shares the same owner-conditional removal
            // and must likewise leave the replacement's indexes intact.
            manager.UnregisterGameObject(stale.gameObject);
            Assert.That(
                tracker.TryGetTrackedGameObject(
                    instanceId,
                    out TrackedGameObject trackedAfterHardUnregister),
                Is.True);
            Assert.That(
                trackedAfterHardUnregister.GameObject,
                Is.SameAs(replacement.gameObject));
            Assert.That(
                lookupCache[instanceId],
                Is.SameAs(replacement.gameObject));
            Assert.That(activeStates.ContainsKey(instanceId), Is.True);
        }

        [Test]
        public void CurrentLifecycleOwnerStillRegistersGenuineDestruction()
        {
            const string instanceId = "current-lifecycle-owner";
            SaveManager manager = InstallSaveManager(CreateSettings());
            GameObjectTracker tracker = manager.GameObjectTracker;

            var currentOwner = new GameObject("CurrentLifecycleOwner");
            currentOwner.SetActive(false);
            createdObjects.Add(currentOwner);
            UniqueID uniqueId = currentOwner.AddComponent<UniqueID>();
            uniqueId.ID = instanceId;
            var settings = new GameObjectPropertySettings
            {
                RememberActive = true,
                RememberDestroyed = true
            };
            manager.RegisterGameObject(currentOwner, settings);

            MethodInfo registerFromOwner = typeof(SaveManager).GetMethod(
                "TryRegisterDestroyedGameObjectFromOwner",
                InstanceFlags);
            Assert.That(registerFromOwner, Is.Not.Null);
            bool registered = (bool)registerFromOwner.Invoke(
                manager,
                new object[] { instanceId, currentOwner });

            Assert.That(registered, Is.True);
            Assert.That(tracker.IsGameObjectDestroyed(instanceId), Is.True);
            Assert.That(
                tracker.TryGetTrackedGameObject(instanceId, out _),
                Is.False);
        }

        [Test]
        public void PendingProxyIsNotReusedDuringSameFrameRegistration()
        {
            const string instanceId = "pending-proxy-reregister";
            SaveManager manager = InstallSaveManager(CreateSettings());

            var owner = new GameObject("PendingProxyOwner");
            owner.SetActive(false);
            createdObjects.Add(owner);
            UniqueID uniqueId = owner.AddComponent<UniqueID>();
            uniqueId.ID = instanceId;
            var settings = new GameObjectPropertySettings
            {
                RememberActive = true,
                RememberDestroyed = true
            };
            manager.RegisterGameObject(owner, settings);

            TrackedGameObjectProxy pending =
                owner.GetComponent<TrackedGameObjectProxy>();
            Assert.That(pending, Is.Not.Null);
            MethodInfo disarm = typeof(TrackedGameObjectProxy).GetMethod(
                "Disarm",
                InstanceFlags);
            Assert.That(disarm, Is.Not.Null);
            disarm.Invoke(pending, new object[] { true });

            manager.RegisterGameObject(owner, settings);

            TrackedGameObjectProxy[] proxies =
                owner.GetComponents<TrackedGameObjectProxy>();
            Assert.That(proxies, Has.Length.EqualTo(2));
            Assert.That(
                Array.Exists(proxies, proxy =>
                    proxy != null &&
                    !GetField<bool>(proxy, "pendingRemoval")),
                Is.True);
        }

        [Test]
        public void SceneReuseRebindsOwnerAndUnregisterRemovesBothIndexes()
        {
            const string previousId = "scene-rebind-old-id";
            const string restoredId = "scene-rebind-new-id";
            const string prefabAssetId = "scene-rebind-asset";

            SaveSettings settings = CreateSettings();
            InstallSaveManager(settings);

            SaveablePrefab candidate = CreateInactiveSaveable(
                "SceneRebindCandidate",
                previousId);
            candidate.PrefabAssetID = prefabAssetId;
            candidate.ReuseSceneInstanceOnLoad = true;
            prefabManager.RegisterPrefab(candidate);

            SaveablePrefabData data = CreateData(restoredId, prefabAssetId);
            data.GameObjectName = "SceneRebindRestored";
            data.ReuseSceneInstanceOnLoad = true;

            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { data },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Dictionary<string, GameObject> mappings = GetMappings();
            Assert.That(mappings.ContainsKey(previousId), Is.False);
            Assert.That(mappings[restoredId], Is.SameAs(candidate.gameObject));
            Assert.That(candidate.UniqueID, Is.EqualTo(restoredId));
            Assert.That(prefabManager.SaveablePrefabs, Has.Count.EqualTo(1));
            Assert.That(prefabManager.SaveablePrefabs[0], Is.SameAs(candidate));

            prefabManager.UnregisterPrefab(candidate);
            Assert.That(mappings.ContainsKey(restoredId), Is.False);
            Assert.That(prefabManager.SaveablePrefabs, Is.Empty);
        }

        [Test]
        public void RebindDropsPreviousNameAliasesWithinBatch()
        {
            const string previousId = "name-rebind-old-id";
            const string restoredId = "name-rebind-new-id";
            const string prefabAssetId = "name-rebind-asset";

            SaveSettings settings = CreateSettings();
            InstallSaveManager(settings);

            SaveablePrefab candidate = CreateInactiveSaveable(
                "PreviousActiveAlias",
                previousId);
            candidate.gameObject.SetActive(true);
            candidate.PrefabAssetID = prefabAssetId;
            candidate.ReuseSceneInstanceOnLoad = true;
            prefabManager.RegisterPrefab(candidate);

            SaveablePrefabData rebound = CreateData(restoredId, prefabAssetId);
            rebound.GameObjectName = "CurrentActiveAlias";
            rebound.ReuseSceneInstanceOnLoad = true;
            rebound.LoadPriority = 100;

            SaveablePrefabData legacyAliasProbe = CreateData(
                instanceId: string.Empty,
                prefabId: string.Empty);
            legacyAliasProbe.GameObjectName = "PreviousActiveAlias";
            legacyAliasProbe.ReuseSceneInstanceOnLoad = true;
            legacyAliasProbe.LoadPriority = 0;

            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { rebound, legacyAliasProbe },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Assert.That(candidate.UniqueID, Is.EqualTo(restoredId));
            Assert.That(candidate.name, Is.EqualTo("CurrentActiveAlias"));
            Assert.That(GetMappings()[restoredId], Is.SameAs(candidate.gameObject));
        }

        [Test]
        public void PooledRestoreReusesReturnedInstanceAndRebindsIdentity()
        {
            const string firstId = "pooled-restore-first";
            const string secondId = "pooled-restore-second";
            const string prefabAssetId = "pooled-restore-asset";

            poolCacheTouched = true;
            ResetPoolCache();
            CreateRegisteredPrefabAsset(prefabAssetId);

            SaveSettings settings = CreateSettings();
            settings.usePrefabPooling = true;
            settings.defaultPrefabPoolSize = 0;
            InstallSaveManager(settings);

            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData>
                {
                    CreateData(firstId, prefabAssetId)
                },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Dictionary<string, GameObject> mappings = GetMappings();
            TrackMappedObjects(mappings);
            GameObject firstObject = mappings[firstId];
            SaveablePrefab firstSaveable =
                firstObject.GetComponent<SaveablePrefab>();

            prefabManager.UnregisterPrefab(firstSaveable);
            Assert.That(
                SaveablePrefabPoolCache.TryDespawn(
                    firstSaveable,
                    initialSize: 0,
                    rememberObjects: true),
                Is.True);

            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData>
                {
                    CreateData(secondId, prefabAssetId)
                },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            TrackMappedObjects(mappings);
            Assert.That(mappings.ContainsKey(firstId), Is.False);
            Assert.That(mappings[secondId], Is.SameAs(firstObject));
            Assert.That(
                mappings[secondId].GetComponent<SaveablePrefab>().UniqueID,
                Is.EqualTo(secondId));
            Assert.That(prefabManager.SaveablePrefabs, Has.Count.EqualTo(1));
        }

        [Test]
        public void PreparedBatchDeduplicatesByInstanceIdAndPrefersReuse()
        {
            SaveablePrefabData highPriority = CreateData("duplicate", "asset");
            highPriority.LoadPriority = 500;
            SaveablePrefabData reuse = CreateData("duplicate", "asset");
            reuse.LoadPriority = 1;
            reuse.ReuseSceneInstanceOnLoad = true;
            SaveablePrefabData other = CreateData("other", "asset");
            other.LoadPriority = 100;

            MethodInfo prepare = typeof(PrefabManager).GetMethod(
                "PreparePrefabBatch",
                StaticFlags);
            Assert.That(prepare, Is.Not.Null);

            PrefabLoadDiagnosticCounters.Reset();
            object prepared = prepare.Invoke(
                null,
                new object[]
                {
                    new List<SaveablePrefabData> { highPriority, other, reuse },
                    false
                });

            PropertyInfo immediateProperty = prepared.GetType().GetProperty(
                "Immediate",
                InstanceFlags);
            Assert.That(immediateProperty, Is.Not.Null);
            var immediate = (List<SaveablePrefabData>)immediateProperty.GetValue(prepared);

            Assert.That(immediate, Has.Count.EqualTo(2));
            Assert.That(immediate.Contains(reuse), Is.True);
            Assert.That(immediate.Contains(highPriority), Is.False);
            Assert.That(immediate[0], Is.SameAs(other));
            Assert.That(PrefabLoadDiagnosticCounters.Capture().BatchPreparations, Is.EqualTo(1));
        }

        [TestCase(false, 0)]
        [TestCase(true, 1)]
        public void RigidbodyFlagControlsMissingComponentProbe(bool hasRigidbody, int expectedProbes)
        {
            const string instanceId = "rigidbody-probe";
            const string prefabId = "rigidbody-probe-asset";

            var candidateObject = new GameObject("RigidbodyProbeCandidate");
            candidateObject.SetActive(false);
            createdObjects.Add(candidateObject);
            SaveablePrefab candidate = candidateObject.AddComponent<SaveablePrefab>();
            candidate.PrefabAssetID = prefabId;
            SetField(candidate, "uniqueID", instanceId);
            prefabManager.SaveablePrefabs.Add(candidate);

            var mappings = (Dictionary<string, GameObject>)typeof(PrefabManager)
                .GetField("instantiatedPrefabs", InstanceFlags)
                .GetValue(prefabManager);
            mappings[instanceId] = candidateObject;

            SaveablePrefabData data = CreateData(instanceId, prefabId);
            data.ReuseSceneInstanceOnLoad = true;
            data.HasRigidbody = hasRigidbody;

            PrefabLoadDiagnosticCounters.Reset();
            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { data },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            PrefabLoadDiagnosticSnapshot counters = PrefabLoadDiagnosticCounters.Capture();
            Assert.That(counters.RigidbodyProbes, Is.EqualTo(expectedProbes));
            Assert.That(counters.PublicLookupCalls, Is.Zero);
        }

        [Test]
        public void RigidbodyPresentIsProbedOnceAndRestoredFromCachedReference()
        {
            const string instanceId = "rigidbody-present";
            const string prefabId = "rigidbody-present-asset";

            SaveablePrefab candidate = CreateInactiveSaveable(
                "RigidbodyPresentCandidate",
                instanceId);
            candidate.PrefabAssetID = prefabId;
            Rigidbody rigidbody = candidate.gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = false;
            rigidbody.useGravity = true;
            prefabManager.SaveablePrefabs.Add(candidate);
            GetMappings()[instanceId] = candidate.gameObject;

            SaveablePrefabData data = CreateData(instanceId, prefabId);
            data.ReuseSceneInstanceOnLoad = true;
            data.HasRigidbody = true;
            data.RigidbodyIsKinematic = true;
            data.RigidbodyUseGravity = false;

            PrefabLoadDiagnosticCounters.Reset();
            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { data },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Assert.That(
                PrefabLoadDiagnosticCounters.Capture().RigidbodyProbes,
                Is.EqualTo(1));
            Assert.That(rigidbody.isKinematic, Is.True);
            Assert.That(rigidbody.useGravity, Is.False);
            Assert.That(GetMappings()[instanceId], Is.SameAs(candidate.gameObject));
        }

        [Test]
        public void DuplicateRegistrationKeepsFirstSerializationOwnerAndCurrentMapping()
        {
            const string instanceId = "duplicate-registration-owner";

            SaveablePrefab firstOwner = CreateInactiveSaveable(
                "FirstSerializationOwner",
                instanceId);
            SaveablePrefab currentInstance = CreateInactiveSaveable(
                "CurrentMappedInstance",
                instanceId);
            prefabManager.SaveablePrefabs.Add(firstOwner);
            prefabManager.SaveablePrefabs.Add(currentInstance);

            var mappings = (Dictionary<string, GameObject>)typeof(PrefabManager)
                .GetField("instantiatedPrefabs", InstanceFlags)
                .GetValue(prefabManager);
            mappings[instanceId] = currentInstance.gameObject;

            SaveablePrefabData data = CreateData(instanceId, prefabId: string.Empty);
            data.ReuseSceneInstanceOnLoad = true;

            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { data },
                new List<string>(),
                clearExistingPrefabs: false,
                handleDeferral: false));

            Assert.That(prefabManager.SaveablePrefabs, Has.Count.EqualTo(1));
            Assert.That(prefabManager.SaveablePrefabs[0], Is.SameAs(firstOwner));
            Assert.That(mappings[instanceId], Is.SameAs(currentInstance.gameObject));
            Assert.That(
                PrefabLoadDiagnosticCounters.Capture().RegistrationOwnerProbes,
                Is.EqualTo(1));
            Assert.That(
                PrefabLoadDiagnosticCounters.Capture().PrefabDescriptions,
                Is.Zero);
        }

        [Test]
        public void EnablingVerboseLoadDiagnosticsBuildsAllDetailedDescriptions()
        {
            const string instanceId = "verbose-diagnostic-entry";
            SaveSettings settings = CreateSettings();
            settings.enableVerbosePrefabLoadDiagnostics = true;
            InstallSaveManager(settings);
            Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Info;
            LoggerConfig loggerConfig = Resources.Load<LoggerConfig>("LoggerConfig");
            bool previousPrefabManagerLogging =
                loggerConfig != null && loggerConfig.prefabManager;

            try
            {
                if (loggerConfig != null)
                {
                    loggerConfig.prefabManager = true;
                    Arawn.CrystalSave.Runtime.Logger.RefreshConfig();
                }

                SaveablePrefab firstOwner = CreateInactiveSaveable(
                    "VerboseFirstOwner",
                    instanceId);
                SaveablePrefab currentInstance = CreateInactiveSaveable(
                    "VerboseCurrentInstance",
                    instanceId);
                prefabManager.SaveablePrefabs.Add(firstOwner);
                prefabManager.SaveablePrefabs.Add(currentInstance);
                GetMappings()[instanceId] = currentInstance.gameObject;

                SaveablePrefabData data =
                    CreateData(instanceId, prefabId: string.Empty);
                data.ReuseSceneInstanceOnLoad = true;

                PrefabLoadDiagnosticCounters.Reset();
                Drain(prefabManager.InstantiatePrefabsCoroutine(
                    new List<SaveablePrefabData> { data },
                    new List<string>(),
                    clearExistingPrefabs: false,
                    handleDeferral: false));

                PrefabLoadDiagnosticSnapshot counters =
                    PrefabLoadDiagnosticCounters.Capture();
                Assert.That(counters.PrefabDescriptions, Is.GreaterThan(0));
                Assert.That(counters.PrefabDataDescriptions, Is.GreaterThan(0));
                Assert.That(counters.TransformPathDescriptions, Is.GreaterThan(0));
            }
            finally
            {
                if (loggerConfig != null)
                {
                    loggerConfig.prefabManager = previousPrefabManagerLogging;
                    Arawn.CrystalSave.Runtime.Logger.RefreshConfig();
                }
            }
        }

        [Test]
        public void LoggingOffSkipsDetailedDescriptionsInSaveCollectionPaths()
        {
            Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Off;

            SaveablePrefab nonParticipant = CreateInactiveSaveable(
                "NonParticipatingCollectionPrefab",
                "non-participating-collection-prefab");
            nonParticipant.RegisterWithSaveSystem = false;

            var queue = GetField<object>(prefabManager, "restoreQueue");
            queue.GetType().GetMethod("QueuePending", InstanceFlags).Invoke(queue, new object[]
            {
                "PendingScene", CreateData("pending-description-gate", prefabId: string.Empty)
            });
            var deferred = CreateData("deferred-description-gate", prefabId: string.Empty);
            deferred.HomeScene = "DeferredScene";
            deferred.DeferLowPriorityUntilRequested = true;
            prefabManager.PrepareImmediatePrefabs(new List<SaveablePrefabData> { deferred });

            PrefabLoadDiagnosticCounters.Reset();
            Assert.That(
                prefabManager.BuildPrefabData(nonParticipant),
                Is.Null);
            List<SaveablePrefabData> collected =
                prefabManager.CollectPrefabData();

            Assert.That(collected, Has.Count.EqualTo(2));
            PrefabLoadDiagnosticSnapshot counters =
                PrefabLoadDiagnosticCounters.Capture();
            Assert.That(counters.PrefabDescriptions, Is.Zero);
            Assert.That(counters.PrefabDataDescriptions, Is.Zero);
            Assert.That(counters.TransformPathDescriptions, Is.Zero);
        }

        [Test]
        public void PreparedImmediatePathPreservesCompletionEvent()
        {
            MethodInfo prepare = typeof(PrefabManager).GetMethod(
                "PreparePrefabBatchForLoad",
                InstanceFlags);
            MethodInfo instantiate = typeof(PrefabManager).GetMethod(
                "InstantiatePreparedPrefabsCoroutine",
                InstanceFlags);
            Assert.That(prepare, Is.Not.Null);
            Assert.That(instantiate, Is.Not.Null);

            object prepared = prepare.Invoke(
                prefabManager,
                new object[]
                {
                    new List<SaveablePrefabData>
                    {
                        CreateData("prepared-completion", prefabId: string.Empty)
                    }
                });

            int completionCount = 0;
            prefabManager.OnImmediatePrefabBatchComplete += OnComplete;
            try
            {
                var routine = (IEnumerator)instantiate.Invoke(
                    prefabManager,
                    new object[] { prepared, new List<string>(), false });
                Drain(routine);
            }
            finally
            {
                prefabManager.OnImmediatePrefabBatchComplete -= OnComplete;
            }

            Assert.That(completionCount, Is.EqualTo(1));

            void OnComplete()
            {
                completionCount++;
            }
        }

        [Test]
        public void RawPublicPathKeepsDeferredEntriesAcrossItsClear()
        {
            SaveablePrefabData immediate =
                CreateData("raw-immediate", prefabId: string.Empty);
            SaveablePrefabData deferred =
                CreateData("raw-deferred", prefabId: string.Empty);
            deferred.DeferLowPriorityUntilRequested = true;

            Drain(prefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { deferred, immediate },
                new List<string>(),
                clearExistingPrefabs: true,
                handleDeferral: true));

            Assert.That(prefabManager.HasDeferredPrefabs, Is.True);
            Assert.That(
                prefabManager.PeekDeferredPrefabsForScene(string.Empty),
                Has.Count.EqualTo(1));
            Assert.That(
                prefabManager.PeekDeferredPrefabsForScene(string.Empty)[0],
                Is.SameAs(deferred));
        }

        private static SaveablePrefabData CreateData(string instanceId, string prefabId)
        {
            return new SaveablePrefabData(
                instanceID: instanceId,
                prefabID: prefabId,
                gameObjectName: instanceId,
                position: Vector3.zero,
                rotation: Quaternion.identity,
                scale: Vector3.one,
                parentID: null,
                isParentSceneObject: false,
                visibilitySettingsData: null,
                homeScene: null);
        }

        private SaveablePrefab CreateInactiveSaveable(string name, string instanceId)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            createdObjects.Add(go);
            SaveablePrefab prefab = go.AddComponent<SaveablePrefab>();
            SetField(prefab, "uniqueID", instanceId);
            return prefab;
        }

        private GameObject CreateRegisteredPrefabAsset(
            string prefabAssetId,
            bool includeRigidbody = false,
            string serializedInstanceId = "")
        {
            var source = new GameObject(
                $"PrefabLoadOptimizationSource-{prefabAssetId}");
            SaveablePrefab sourceSaveable = source.AddComponent<SaveablePrefab>();
            SetField(sourceSaveable, "uniqueID", serializedInstanceId);
            SetField(sourceSaveable, "prefabAssetID", prefabAssetId);
            sourceSaveable.RegisterWithSaveSystem = true;
            sourceSaveable.DisablePooling = false;
            if (includeRigidbody)
                source.AddComponent<Rigidbody>();

            string assetPath =
                $"Assets/__CrystalSavePrefabLoadOptimization_{Guid.NewGuid():N}.prefab";
            GameObject prefabAsset =
                PrefabUtility.SaveAsPrefabAsset(source, assetPath);
            UnityEngine.Object.DestroyImmediate(source);

            Assert.That(prefabAsset, Is.Not.Null);
            createdAssetPaths.Add(assetPath);

            PrefabRegistry registry =
                GetField<PrefabRegistry>(prefabManager, "prefabRegistry");
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<PrefabRegistry>();
                createdScriptableObjects.Add(registry);
                SetField(prefabManager, "prefabRegistry", registry);
            }

            registry.AddPrefab(prefabAssetId, prefabAsset);
            return prefabAsset;
        }

        private object CreateLoadContext(params SaveablePrefab[] scenePrefabs)
        {
            var factory = typeof(PrefabManager).GetMethod("CreateLoadContext", InstanceFlags);
            Assert.That(factory, Is.Not.Null);
            return factory.Invoke(prefabManager, new object[] { Array.Empty<string>(), scenePrefabs });
        }

        private static bool TryResolveContext(
            object context,
            SaveablePrefabData data,
            out GameObject instance)
        {
            object[] arguments = { data, null, null };
            bool resolved = (bool)InvokeContext(
                context,
                "TryResolve",
                arguments);
            instance = (GameObject)arguments[1];
            return resolved;
        }

        private static object InvokeContext(
            object context,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = context.GetType().GetMethod(
                methodName,
                InstanceFlags);
            Assert.That(method, Is.Not.Null, $"Missing context method '{methodName}'.");
            return method.Invoke(context, arguments);
        }

        private SaveSettings CreateSettings()
        {
            SaveSettings settings =
                ScriptableObject.CreateInstance<SaveSettings>();
            createdScriptableObjects.Add(settings);
            return settings;
        }

        private SaveManager InstallSaveManager(SaveSettings settings)
        {
            var managerRoot = new GameObject(
                "PrefabLoadOptimizationSaveManager");
            managerRoot.SetActive(false);
            createdObjects.Add(managerRoot);

            SaveManager manager = managerRoot.AddComponent<SaveManager>();
            SetField(manager, "saveSettings", settings);
            SetField(manager, "prefabManager", prefabManager);
            var componentManager = new ComponentManager();
            SetField(manager, "componentManager", componentManager);

            GameObjectTracker tracker =
                managerRoot.GetComponent<GameObjectTracker>();
            if (tracker != null)
            {
                SetField(manager, "gameObjectTracker", tracker);
                MethodInfo initializeTracker =
                    typeof(GameObjectTracker).GetMethod(
                        "Initialize",
                        InstanceFlags);
                Assert.That(initializeTracker, Is.Not.Null);
                initializeTracker.Invoke(
                    tracker,
                    new object[] { manager, prefabManager, componentManager });
            }

            saveManagerInstanceProperty.SetValue(null, manager);
            return manager;
        }

        private Dictionary<string, GameObject> GetMappings()
        {
            return GetField<Dictionary<string, GameObject>>(
                prefabManager,
                "instantiatedPrefabs");
        }

        private void TrackMappedObjects(
            Dictionary<string, GameObject> mappings)
        {
            foreach (GameObject instance in mappings.Values)
            {
                if (instance == null ||
                    !instance.scene.IsValid() ||
                    createdObjects.Contains(instance))
                {
                    continue;
                }

                createdObjects.Add(instance);
            }
        }

        private static void ResetPoolCache()
        {
            FieldInfo poolsField =
                typeof(SaveablePrefabPoolCache).GetField(
                    "_pools",
                    StaticFlags);
            Assert.That(poolsField, Is.Not.Null);
            var pools = (IDictionary)poolsField.GetValue(null);
            var disposables = new List<IDisposable>();
            foreach (DictionaryEntry entry in pools)
            {
                if (entry.Value is IDisposable disposable)
                    disposables.Add(disposable);
            }

            for (int i = 0; i < disposables.Count; i++)
                disposables[i].Dispose();
            pools.Clear();

            FieldInfo depthField =
                typeof(SaveablePrefabPoolCache).GetField(
                    "_disposalSuppressionDepth",
                    StaticFlags);
            Assert.That(depthField, Is.Not.Null);
            depthField.SetValue(null, 0);
            SaveablePrefabPoolCache.SuppressPoolDisposal = false;

            FieldInfo globalRootField =
                typeof(SaveablePrefabPool).GetField(
                    "_globalRoot",
                    StaticFlags);
            Assert.That(globalRootField, Is.Not.Null);
            Transform globalRoot =
                (Transform)globalRootField.GetValue(null);
            if (globalRoot != null)
                UnityEngine.Object.DestroyImmediate(globalRoot.gameObject);
            globalRootField.SetValue(null, null);
        }

        private static void Drain(IEnumerator routine)
        {
            Assert.That(routine, Is.Not.Null);
            while (routine.MoveNext())
            {
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            return (T)field.GetValue(target);
        }
    }
}
#endif
