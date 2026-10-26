#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using Arawn.CrystalSave.Editor;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveReviewRegressionTests
    {
        private FeatureSettingsScope scope;
        private GameObject root;
        private string saveRoot;
        [SetUp] public void SetUp()
        {
            scope = new FeatureSettingsScope();
            scope.Settings.saveKey = "CrystalSaveReview_" + Guid.NewGuid().ToString("N") + "_{n}";
            root = new GameObject("ReviewRegression"); root.SetActive(false);
            saveRoot = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (string key in new[] { scope.Settings.saveKey.Replace("{n}", "3"), scope.Settings.saveKey + "3" })
                PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            scope.Dispose();
            if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        }
        private static SaveSlot Slot() => new SaveSlot(3, "Review", DateTime.UtcNow, "", "");

        [TestCase(false)] [TestCase(true)]
        public void PersistenceSetupUsesRuntimePolicyAndSupportsUndoWithoutChangingIdentity(bool isPrefab)
        {
            Component owner = isPrefab ? (Component)root.AddComponent<SaveablePrefab>() : root.AddComponent<FeatureProbe>();
            Set(owner, "uniqueID", "stable-id");
            CrystalSavePersistenceSetup.Apply(owner, 2);
            Assert.That((bool)owner.GetType().GetProperty("KeepAcrossScenes").GetValue(owner), Is.True);
            Assert.That(root.GetComponent<PersistentVisibilityController>(), Is.Not.Null);
            if (isPrefab) Assert.That(((SaveablePrefab)owner).DisablePooling, Is.True);
            Undo.PerformUndo();
            Assert.That((bool)owner.GetType().GetProperty("KeepAcrossScenes").GetValue(owner), Is.False);
            Assert.That(root.GetComponent<PersistentVisibilityController>(), Is.Null);
            Assert.That(Get<string>(owner, "uniqueID"), Is.EqualTo("stable-id"));
            CrystalSavePersistenceSetup.Apply(owner, 3);
            Assert.That((bool)owner.GetType().GetProperty("RememberHomeScene").GetValue(owner), Is.True);
            CrystalSavePersistenceSetup.Apply(owner, 5);
            Assert.That((bool)owner.GetType().GetProperty("DeferLowPriorityUntilRequested").GetValue(owner), Is.True);
            Assert.That(Get<string>(owner, "uniqueID"), Is.EqualTo("stable-id"));
        }

        [Test]
        public void ComponentPolicyPreservesHomeAndSharedCombinationAndNormalizesSerializedConflicts()
        {
            var component = root.AddComponent<FeatureProbe>();
            component.RememberHomeScene = true; component.ShareStateAcrossScenes = true;
            Assert.That(component.RememberHomeScene && component.ShareStateAcrossScenes, Is.True);
            component.KeepAcrossScenes = true;
            Assert.That(component.RememberHomeScene || component.ShareStateAcrossScenes, Is.False);
            component.ShareStateAcrossScenes = true;
            Assert.That(component.KeepAcrossScenes, Is.False);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("rememberHomeScene").boolValue = true;
            serialized.FindProperty("keepAcrossScenes").boolValue = true;
            serialized.ApplyModifiedProperties();
            component.ValidatePersistenceSettings();
            Assert.That(component.KeepAcrossScenes, Is.False);
            Assert.That(component.RememberHomeScene && component.ShareStateAcrossScenes, Is.True);
        }

        [Test]
        public void PrefabPolicyPreservesHandoffMasksAndHomeSceneDestructionRule()
        {
            var prefab = root.AddComponent<SaveablePrefab>();
            prefab.HandoffTransform = false; prefab.HandoffRuntimeModifications = false;
            prefab.EnableCrossSceneStateHandoff = true; prefab.KeepAcrossScenes = true;
            Assert.That(prefab.EnableCrossSceneStateHandoff, Is.False);
            Assert.That(prefab.DisablePooling, Is.True);
            prefab.RememberHomeScene = true;
            Assert.That(prefab.KeepAcrossScenes, Is.False);
            Assert.That(prefab.PropertySettings.RememberDestroyed, Is.False);
            prefab.EnableCrossSceneStateHandoff = true;
            Assert.That(prefab.HandoffTransform || prefab.HandoffRuntimeModifications, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public async Task SuccessfulPlayerPrefsSaveIsDiscoverableThroughSyncAndAsyncQueries(bool placeholder)
        {
            var settings = scope.Settings; settings.saveMethod = SaveMethod.PlayerPrefs;
            if (!placeholder) settings.saveKey = settings.saveKey.Replace("{n}", "");
            var storage = new SaveSystem(settings, saveRoot);
            var slots = new SaveSlotManager(storage, settings, null, saveRoot); slots.Slots.Add(Slot());
            Assert.That(await slots.HasSaveAtAsync(3, false), Is.False);
            storage.Save(new byte[] { 1, 2, 3 }, slots.Slots[0]);
            Assert.That(await slots.HasSaveAtAsync(3, false), Is.True);
            Assert.That(slots.HasSaveAt(3, false), Is.True);
            Assert.That(slots.HasSave(false), Is.True);
            storage.Delete(slots.Slots[0]);
            Assert.That(await slots.HasSaveAsync(false), Is.False);
        }

        [Test]
        public async Task LegacyPlayerPrefsKeyRemainsDiscoverable()
        {
            scope.Settings.saveMethod = SaveMethod.PlayerPrefs;
            var slots = new SaveSlotManager(new SaveSystem(scope.Settings, saveRoot), scope.Settings, null, saveRoot);
            slots.Slots.Add(Slot());
            PlayerPrefs.SetString(scope.Settings.saveKey + "3", "AQ==");
            Assert.That(await slots.HasSaveAtAsync(3), Is.True);
        }

        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void VisibilityPreservesDisabledAndKinematicStates(bool prefabOwner, bool overrideCategories)
        {
            Component owner = prefabOwner ? (Component)root.AddComponent<SaveablePrefab>() : root.AddComponent<FeatureProbe>();
            foreach (string option in new[] { "DisableAllColliders", "DisableRenderers", "DisableCharacterController", "SetRigidbodyKinematic" })
                owner.GetType().GetProperty(option).SetValue(owner, overrideCategories);
            Set(owner, "VisibleInScenes", new List<string> { "Home" });
            var box = root.AddComponent<BoxCollider>(); box.enabled = false;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.enabled = false;
            var canvas = root.AddComponent<Canvas>(); canvas.enabled = false;
            var character = root.AddComponent<CharacterController>(); character.enabled = false;
            var body = root.AddComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
            var visibility = root.AddComponent<PersistentVisibilityController>();
            Call(visibility, "InitializeReferences"); Call(visibility, "InitializeComponentStates");
            foreach (string scene in new[] { "Away", "Away", "Home", "Home" })
            {
                visibility.ApplyVisibilityBasedOnScene(scene);
                Assert.That(new[] { box.enabled, renderer.enabled, canvas.enabled, character.enabled, body.isKinematic },
                    Is.EqualTo(new[] { false, false, false, false, true }), scene);
            }
        }

        [Test]
        public void VisibilityCapturesLatestGameplayStateAndDoesNotResetVisibleObjects()
        {
            var owner = root.AddComponent<FeatureProbe>(); owner.DisableRenderers = true;
            Set(owner, "VisibleInScenes", new List<string> { "Home" });
            var renderer = root.AddComponent<MeshRenderer>();
            var visibility = root.AddComponent<PersistentVisibilityController>();
            Call(visibility, "InitializeReferences"); Call(visibility, "InitializeComponentStates");
            visibility.ApplyVisibilityBasedOnScene("Away"); visibility.ApplyVisibilityBasedOnScene("Home");
            Assert.That(renderer.enabled, Is.True);
            renderer.enabled = false;
            visibility.ApplyVisibilityBasedOnScene("Home");
            Assert.That(renderer.enabled, Is.False);
            visibility.ApplyVisibilityBasedOnScene("Away"); visibility.ApplyVisibilityBasedOnScene("Home");
            Assert.That(renderer.enabled, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void FailedStagedWriteLeavesCommittedBytesUntouched(bool existing)
        {
            string path = Path.Combine(saveRoot, "commit.sav");
            var type = typeof(SaveSystem).Assembly.GetType("Arawn.CrystalSave.Runtime.AtomicSaveFile", true);
            var write = type.GetMethod("WriteAllBytes", BindingFlags.NonPublic | BindingFlags.Static);
            if (existing) write.Invoke(null, new object[] { path, new byte[] { 1, 2 }, null });
            Action<string> fail = temporary =>
            {
                Assert.That(File.ReadAllBytes(temporary), Is.EqualTo(new byte[] { 9 }));
                throw new IOException("Injected interruption before commit");
            };
            var error = Assert.Throws<TargetInvocationException>(() => write.Invoke(null, new object[] { path, new byte[] { 9 }, fail }));
            Assert.That(error.InnerException, Is.TypeOf<IOException>());
            if (existing) Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 1, 2 }));
            else Assert.That(File.Exists(path), Is.False);
            Assert.That(Directory.GetFiles(saveRoot, "*.writing-*"), Is.Empty);
        }

        [Test]
        public async Task AtomicSaveRetainsRecoveryGenerationAndDeletionCannotResurrectIt()
        {
            scope.Settings.saveMethod = SaveMethod.BinaryFileFormat;
            var storage = new SaveSystem(scope.Settings, saveRoot); var slot = Slot();
            storage.Save(new byte[] { 1 }, slot);
            await storage.SaveAsync(new byte[] { 2 }, slot);
            storage.Save(new byte[] { 3 }, slot);
            string path = Directory.GetFiles(saveRoot, "*.sav", SearchOption.AllDirectories).Single();
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 3 }));
            Assert.That(File.ReadAllBytes(path + ".previous"), Is.EqualTo(new byte[] { 2 }));
            File.Delete(path); // Simulate a missing committed file with an intact recovery generation.
            Assert.That(storage.Load(slot), Is.EqualTo(new byte[] { 2 }));
            storage.Delete(slot);
            Assert.That(File.Exists(path) || File.Exists(path + ".previous"), Is.False);
            Assert.That(storage.Load(slot), Is.Null);
        }

        [TestCase(8)] [TestCase(128)]
        public void ComponentBlobRestoreScansHierarchyOnceAndRebuildsForNextLoad(int count)
        {
            var prefab = root.AddComponent<SaveablePrefab>(); Set(prefab, "uniqueID", "owner_with_underscores");
            prefab.TrackComponentBlobs = true;
            var helpers = new List<FeatureProbe>();
            for (int i = 0; i < count; i++)
            {
                var child = new GameObject("Helper" + i); child.transform.SetParent(root.transform);
                var helper = child.AddComponent<FeatureProbe>(); Set(helper, "uniqueID", prefab.UniqueID);
                helper.ComponentID = "custom_" + i; helper.Value = i + 1; helpers.Add(helper);
            }
            byte[] bytes = prefab.CaptureRuntimeModifications();
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var helper in helpers) helper.Value = 0;
                PrefabLoadDiagnosticCounters.Reset(); prefab.ApplyRuntimeModifications(bytes);
                Assert.That(helpers.Select(h => h.Value), Is.EqualTo(Enumerable.Range(1, count)));
                Assert.That(PrefabLoadDiagnosticCounters.ComponentBlobHierarchyScans, Is.EqualTo(1));
            }
        }

        [Test]
        public void ComponentBlobLookupFindsHelperCreatedByEarlierRestoreCallback()
        {
            var prefab = root.AddComponent<SaveablePrefab>(); Set(prefab, "uniqueID", "instance"); prefab.TrackComponentBlobs = true;
            var a = new GameObject("First"); a.transform.SetParent(root.transform);
            var first = a.AddComponent<FeatureProbe>(); Set(first, "uniqueID", "instance"); first.ComponentID = "first"; first.Value = 11;
            var b = new GameObject("Second"); b.transform.SetParent(root.transform);
            var second = b.AddComponent<FeatureProbe>(); Set(second, "uniqueID", "instance"); second.ComponentID = "second"; second.Value = 22;
            byte[] bytes = prefab.CaptureRuntimeModifications(); Object.DestroyImmediate(second);
            first.Loaded = () =>
            {
                second = b.AddComponent<FeatureProbe>(); Set(second, "uniqueID", "instance"); second.ComponentID = "second";
            };
            PrefabLoadDiagnosticCounters.Reset(); prefab.ApplyRuntimeModifications(bytes);
            Assert.That(second.Value, Is.EqualTo(22)); Assert.That(second.Loads, Is.EqualTo(1));
            Assert.That(PrefabLoadDiagnosticCounters.ComponentBlobHierarchyScans, Is.EqualTo(2));
        }

        [Test]
        public void ReleasingVisibilityCategoryRestoresItWhileStillAway()
        {
            var owner = root.AddComponent<FeatureProbe>(); owner.DisableRenderers = true;
            Set(owner, "VisibleInScenes", new List<string> { "Home" });
            var renderer = root.AddComponent<MeshRenderer>();
            var visibility = root.AddComponent<PersistentVisibilityController>();
            Call(visibility, "InitializeReferences"); Call(visibility, "InitializeComponentStates");
            visibility.ApplyVisibilityBasedOnScene("Away"); Assert.That(renderer.enabled, Is.False);
            owner.DisableRenderers = false;
            visibility.ApplyVisibilityBasedOnScene("Away"); Assert.That(renderer.enabled, Is.True);
            owner.DisableRenderers = true;
            visibility.ApplyVisibilityBasedOnScene("Away");
            Call(visibility, "OnDisable"); Assert.That(renderer.enabled, Is.True);
        }

        [Test]
        public void ClearingPendingPrefabsDiscardsBothQueueKindsAndAllowsNewBatches()
        {
            var manager = root.AddComponent<PrefabManager>();
            var prefab = root.AddComponent<SaveablePrefab>(); Set(prefab, "uniqueID", "deferred"); Set(prefab, "prefabAssetID", "asset");
            var deferred = manager.BuildPrefabData(prefab); deferred.HomeScene = "Home"; deferred.DeferLowPriorityUntilRequested = true;
            var streamer = root.AddComponent<DeferredPrefabRadiusStreamer>();
            Call(streamer, "AttachToPrefabManager", manager);
            manager.PrepareImmediatePrefabs(new List<SaveablePrefabData> { deferred });
            Assert.That(Get<Dictionary<string, Dictionary<string, SaveablePrefabData>>>(streamer, "deferredPrefabCache"), Is.Not.Empty);
            var queue = Get<object>(manager, "restoreQueue");
            Call(queue, "QueuePending", "Away", deferred);
            Assert.That(Call(queue, "MarkWaitingForActivation", "Away"), Is.True);
            int cleared = 0; manager.OnPendingPrefabsCleared += () => cleared++;
            manager.ClearPendingPrefabs();
            Assert.That(manager.HasDeferredPrefabs, Is.False);
            Assert.That(manager.GetPendingSceneKeys(), Is.Empty);
            Assert.That(manager.PeekDeferredPrefabsForScene("Home"), Is.Empty);
            Assert.That(Call(queue, "MarkWaitingForActivation", "Away"), Is.True, "Clearing must retire previous activation waits.");
            Assert.That(queue.GetType().GetProperty("DeferredStagedSinceClear", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(queue), Is.False);
            Call(queue, "ReleaseActivationWait", "Away");
            Assert.That(cleared, Is.EqualTo(1));
            Assert.That(Get<Dictionary<string, Dictionary<string, SaveablePrefabData>>>(streamer, "deferredPrefabCache"), Is.Empty);
            Call(streamer, "DetachFromPrefabManager");
            Assert.That(root, Is.Not.Null);
            manager.PrepareImmediatePrefabs(new List<SaveablePrefabData> { deferred });
            Assert.That(manager.HasDeferredPrefabs, Is.True);
        }
    }
}
#endif
