#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveRegistrationBoundaryTests
    {
        private readonly List<GameObject> objects = new();
        private FeatureSettingsScope settings;
        private SaveManager previous;
        private static PropertyInfo Singleton => typeof(SaveManager).GetProperty(nameof(SaveManager.Instance));

        [SetUp]
        public void SetUp()
        {
            previous = SaveManager.Instance;
            settings = new FeatureSettingsScope();
        }

        [TearDown]
        public void TearDown()
        {
            Singleton.SetValue(null, previous);
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            settings.Dispose();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ComponentParentingUsesItsOwnerWhileStandaloneConstructionKeepsLegacyFallback(bool bound)
        {
            var first = CreateManager("First owner");
            var second = CreateManager("Second owner");
            var firstParent = CreateObject("First parent");
            var secondParent = CreateObject("Second parent");
            firstParent.AddComponent<UniqueID>().ID = "shared-parent-id";
            secondParent.AddComponent<UniqueID>().ID = "shared-parent-id";
            first.RegisterGameObject(firstParent, new GameObjectPropertySettings());
            second.RegisterGameObject(secondParent, new GameObjectPropertySettings());
            var child = CreateObject("Child");
            var components = bound ? first.ComponentManager : new ComponentManager();
            components.QueueParenting(child.transform, "shared-parent-id");

            Singleton.SetValue(null, second);
            components.ApplyComponentData(new SaveData());

            Assert.That(child.transform.parent, Is.SameAs((bound ? firstParent : secondParent).transform));
            Assert.That(first.GameObjectTracker.IsTracked("shared-parent-id"), Is.True);
            Assert.That(second.GameObjectTracker.IsTracked("shared-parent-id"), Is.True);
        }

        [Test]
        public void PendingPrefabActivationUsesItsOriginatingHostAfterSingletonChanges()
        {
            var first = CreateManager("Pending owner");
            var second = CreateManager("Current singleton");
            int firstCalls = 0, secondCalls = 0;
            first.SceneActivationPipeline = _ => { firstCalls++; return false; };
            second.SceneActivationPipeline = _ => { secondCalls++; return false; };
            const string unloaded = "CrystalSaveRegistrationUnloadedScene";
            var entry = new SaveablePrefabData("pending-owned-prefab", "pending-asset", "Pending prefab",
                Vector3.zero, Quaternion.identity, Vector3.one, string.Empty, true, null, unloaded, false);
            Drain(first.GetPrefabManager.InstantiatePrefabsCoroutine(
                new List<SaveablePrefabData> { entry }, new List<string>(),
                clearExistingPrefabs: false, handleDeferral: false));
            Singleton.SetValue(null, second);

            first.GetPrefabManager.ProcessPendingPrefabs(unloaded, new List<string>());
            first.GetPrefabManager.ProcessPendingPrefabs(unloaded, new List<string>());

            Assert.That(firstCalls, Is.EqualTo(2));
            Assert.That(secondCalls, Is.Zero);
            Assert.That(first.GetPrefabManager.CollectPrefabData(), Has.Member(entry),
                "Rejected activation must retain this owner's pending entry.");
            Assert.That(second.GetPrefabManager.CollectPrefabData(), Has.No.Member(entry));
        }

        [Test]
        public void RegisteredPrefabViewIsReadOnlyAndTracksSupportedRegistrationChanges()
        {
            var manager = CreateManager("Registration owner").GetPrefabManager;
            var prefab = CreateObject("Registered prefab").AddComponent<SaveablePrefab>();
            Set(prefab, "uniqueID", "registered-view-id");
            var view = manager.RegisteredPrefabs;
            var snapshot = manager.GetSaveablePrefabs();
            Assert.Throws<NotSupportedException>(() => ((ICollection<SaveablePrefab>)view).Add(prefab));
            manager.RegisterPrefab(prefab);
            Assert.That(view, Has.Member(prefab));
            Assert.That(snapshot, Has.No.Member(prefab));
            Assert.That(manager.GetInstantiatedPrefabs()[prefab.UniqueID], Is.SameAs(prefab.gameObject));
            manager.UnregisterPrefab(prefab);
            Assert.That(view, Has.No.Member(prefab));
            Assert.That(manager.GetInstantiatedPrefabs(), Is.Empty);
        }

        [Test]
        public void RebindingReconcilesAnEntryInsertedThroughTheLegacyPrefabList()
        {
            var manager = CreateManager("Legacy registration owner");
            var prefabs = manager.GetPrefabManager;
            var prefab = CreateObject("Legacy list entry").AddComponent<SaveablePrefab>();
            Set(prefab, "uniqueID", "rebound-instance");
            // Deliberately exercise the retained live-list compatibility path.
            prefabs.SaveablePrefabs.Add(prefab);
            var membership = Get<HashSet<SaveablePrefab>>(prefabs, "registeredPrefabInstances");
            Assert.That(membership.Contains(prefab), Is.False);
            prefabs.UpdatePrefabUniqueID(prefab, "legacy-instance", "rebound-instance");
            prefabs.UpdatePrefabUniqueID(prefab, "rebound-instance", "rebound-instance");
            Assert.That(membership.Contains(prefab), Is.True);
            Assert.That(prefabs.RegisteredPrefabs, Is.EqualTo(new[] { prefab }));
            Assert.That(prefabs.GetInstantiatedPrefabs()["rebound-instance"], Is.SameAs(prefab.gameObject));
            prefabs.UnregisterPrefab(prefab);
            Assert.That(membership, Is.Empty);
            Assert.That(prefabs.RegisteredPrefabs, Is.Empty);
            Assert.That(prefabs.GetInstantiatedPrefabs(), Is.Empty);
        }

        [Test]
        public void SceneLoadInstantiationChangesOnlyItsOriginatingStateMachine()
        {
            var first = CreateManager("Load owner");
            var second = CreateManager("Current load singleton");
            var service = new SceneLoadManager(first);
            var machineType = typeof(SceneLoadManager).GetNestedType("LoadStateMachine", BindingFlags.NonPublic);
            var machine = Activator.CreateInstance(machineType, new object[] {
                null, service, first, new SaveSlot(1, "Load owner", DateTime.UtcNow, null, "A"), false, false, true,
                System.Threading.CancellationToken.None, new SaveData(), false
            });
            Singleton.SetValue(null, second);
            Drain((IEnumerator)Call(machine, "InstantiatePrefabsCoroutine"));
            Assert.That(first.StateMachine.CurrentState, Is.EqualTo(SaveState.Loading));
            Assert.That(second.StateMachine.CurrentState, Is.EqualTo(SaveState.Idle));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ScreenshotEncryptionUsesItsOwnerWhileStandaloneConstructionKeepsLegacyFallback(bool bound)
        {
            var first = CreateManager("Image owner");
            var second = CreateManager("Current image singleton");
            var secret = ScriptableObject.CreateInstance<TestSecret>();
            var config = settings.Settings;
            config.masterSecretProvider = secret;
            config.enableCloudSave = false;
            config.enableScreenshots = config.enableEncryption = config.encryptScreenshots = config.useUserIdForEncryption = true;
            var firstEncryption = new EncryptionService(config);
            var secondEncryption = new EncryptionService(config);
            // The fixture provider returns a completed ValueTask; no dispatcher wait.
            var initialized = firstEncryption.InitializeAsync("image-owner");
            Assert.That(initialized.IsCompleted, Is.True); initialized.GetAwaiter().GetResult();
            initialized = secondEncryption.InitializeAsync("image-replacement");
            Assert.That(initialized.IsCompleted, Is.True); initialized.GetAwaiter().GetResult();
            var encryptionProperty = typeof(SaveManager).GetProperty("EncryptionService", BindingFlags.Instance | BindingFlags.NonPublic);
            encryptionProperty.SetValue(first, firstEncryption);
            encryptionProperty.SetValue(second, secondEncryption);
            string root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            var images = bound ? (ScreenshotManager)Activator.CreateInstance(typeof(ScreenshotManager),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { config, root, first }, null)
                : new ScreenshotManager(config, root);
            Texture2D input = null, loaded = null, legacyFacade = null;
            try
            {
                Singleton.SetValue(null, second);
                input = new Texture2D(2, 2);
                input.SetPixel(0, 0, Color.red); input.Apply();
                byte[] plain = input.EncodeToPNG();
                var expected = bound ? firstEncryption : secondEncryption;
                var folder = Path.Combine(root, config.screenshotFolderName);
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "owned.png"), expected.MaybeEncrypt(plain));
                loaded = images.LoadScreenshot(new SaveSlot(1, "Owner image", DateTime.UtcNow, "owned.png", "A"));
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.GetPixel(0, 0), Is.EqualTo(Color.red));
                File.WriteAllBytes(Path.Combine(folder, "legacy.png"), plain);
                string copy = images.DuplicateScreenshot("legacy.png", 2);
                byte[] stored = File.ReadAllBytes(Path.Combine(folder, copy));
                Assert.That(SaveCrypto.HasEnvelope(stored), Is.True);
                Assert.That(expected.MaybeDecrypt(stored), Is.EqualTo(plain));
                Assert.That(expected.MaybeDecrypt((byte[])Call(images, "EncryptScreenshot", plain)), Is.EqualTo(plain));
                legacyFacade = ScreenshotManager.DecodeScreenshotTexture(secondEncryption.MaybeEncrypt(plain));
                Assert.That(legacyFacade, Is.Not.Null, "The static compatibility API still uses the current singleton.");
            }
            finally
            {
                if (input != null) Object.DestroyImmediate(input);
                if (loaded != null) Object.DestroyImmediate(loaded);
                if (legacyFacade != null) Object.DestroyImmediate(legacyFacade);
                Object.DestroyImmediate(secret);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private SaveManager CreateManager(string name)
        {
            var manager = CreateObject(name).AddComponent<SaveManager>();
            Set(manager, "saveSettings", settings.Settings);
            var components = (ComponentManager)Activator.CreateInstance(typeof(ComponentManager),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { manager }, null);
            var prefabs = manager.GetComponent<PrefabManager>();
            var tracker = manager.GetComponent<GameObjectTracker>();
            Set(manager, "componentManager", components);
            Set(manager, "prefabManager", prefabs);
            Set(manager, "gameObjectTracker", tracker);
            Call(tracker, "Initialize", manager, prefabs, components);
            // Exercise the real MonoBehaviour composition callback without starting
            // asynchronous SaveManager bootstrap in this EditMode fixture.
            Call(prefabs, "Awake");
            manager.SetLookupCacheEnabled(true);
            return manager;
        }

        private GameObject CreateObject(string name)
        {
            var instance = new GameObject(name);
            instance.SetActive(false);
            objects.Add(instance);
            return instance;
        }

        private static void Drain(IEnumerator routine)
        {
            int steps = 0;
            while (routine.MoveNext())
            {
                Assert.That(++steps, Is.LessThan(10000));
                if (routine.Current is IEnumerator nested) Drain(nested);
            }
        }
    }
}
#endif
