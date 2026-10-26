#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
        public IEnumerator LifecycleOwnership_RecreatedHostInitializesInSameSession()
        {
            var old = SaveManager.Instance;
            Assert.That(SaveManager.IsInitialized, Is.True);
            UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            yield return null;
            Assert.That(SaveManager.Instance, Is.Null);
            var replacementObject = new GameObject("Replacement SaveManager");
            var replacement = replacementObject.AddComponent<SaveManager>();
            bool serviceReady = false;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                float deadline = Time.realtimeSinceStartup + 3;
                while (Get<object>(replacement, "saveOperationService") == null && Time.realtimeSinceStartup < deadline) yield return null;
                serviceReady = Get<object>(replacement, "saveOperationService") != null;
                TestContext.WriteLine($"Recreated host: globalReady={SaveManager.IsInitialized}, serviceReady={serviceReady}");
                if (serviceReady) yield return Await(replacement.SaveAsync(1));
            }
            finally
            {
                UnityEngine.Object.Destroy(replacementObject);
            }
            // Drain cancellation of the intentionally stranded bootstrap before asserting
            // its pre-cleanup state, keeping expected cleanup logging out of teardown.
            float cleanupDeadline = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < cleanupDeadline) yield return null;
            LogAssert.ignoreFailingMessages = false;
            Assert.That(serviceReady, Is.True,
                "A replacement must complete bootstrap rather than queue slot initialization behind its own unavailable SaveOperations.");
        }

        [UnityTest]
        public IEnumerator LifecycleOwnership_EnabledHostLoadCompletes_Control()
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveAsync(1));
            var load = manager.LoadSaveSlotAsync(1, timeout: TimeSpan.FromSeconds(2));
            float deadline = Time.realtimeSinceStartup + 4;
            while (!load.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(load.IsCompleted, Is.True);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
            yield return Await(manager.SaveAsync(2));
        }

        [UnityTest]
        public IEnumerator LifecycleOwnership_RecreatedHostRetiresInactiveTransformApplication()
        {
            var manager = SaveManager.Instance;
            var helper = SceneComponent("Inactive transform from retired manager");
            helper.transform.position = new Vector3(11, 13, 17);
            helper.gameObject.SetActive(false);
            yield return Await(manager.SaveAsync(1));
            var oldLoad = manager.LoadSaveSlotAsync(1);
            yield return Await(oldLoad);
            Assert.That(oldLoad.Result.Success, Is.True, oldLoad.Result.ErrorMessage);
            Assert.That(Get<bool>(helper, "shouldApplyTransform"), Is.True);
            Assert.That(Get<object>(helper, "applicationTicket"), Is.Not.Null);
            string identity = helper.UniqueIdentifier;
            UnityEngine.Object.Destroy(manager.gameObject);
            yield return null;
            yield return null;
            var replacement = new GameObject("Pending transform replacement SaveManager").AddComponent<SaveManager>();
            yield return Until(() => SaveManager.IsInitialized && SaveManager.Instance == replacement,
                "replacement with retired inactive application");
            Assert.That(replacement.ComponentManager.Contains(helper), Is.True);
            var current = new Vector3(31, 37, 41);
            helper.gameObject.SetActive(true);
            helper.transform.position = current;
            var save = replacement.SaveAsync(2);
            yield return Await(save);
            // Advance an actual frame as well as awaiting capture: neither a fast
            // capture nor a delayed one may let the retired ticket alter live state.
            yield return null;
            Assert.That(helper.transform.position, Is.EqualTo(current));
            var stored = replacement.LoadSaveDataForSlotAsync(2);
            yield return Await(stored);
            Assert.That(stored.Result.ComponentsData.ContainsKey(identity), Is.True);
            var transformData = replacement.Serializer.Deserialize<TransformData>(stored.Result.ComponentsData[identity]);
            Assert.That(transformData.Position, Is.EqualTo(current));
            Assert.That(Get<bool>(helper, "shouldApplyTransform"), Is.False);
        }

        [UnityTest]
        public IEnumerator LifecycleOwnership_RecreatedHostRetainsSceneAndPrefabRegistration()
            => RecreatedManagerRetainsSceneAndPrefabRegistration(false);

        [UnityTest]
        public IEnumerator LifecycleOwnership_ReplacedComponentRetainsSceneAndPrefabRegistration()
            => RecreatedManagerRetainsSceneAndPrefabRegistration(true);

        private IEnumerator RecreatedManagerRetainsSceneAndPrefabRegistration(bool replaceComponent)
        {
            var manager = SaveManager.Instance;
            var settings = manager.SaveSettings;
            bool scanDisabled = settings.registerDisabledComponents;
            settings.registerDisabledComponents = false;
            var active = SceneComponent("Surviving active transform");
            var inactive = SceneComponent("Surviving retained inactive transform");
            inactive.gameObject.SetActive(false);
            var unregistered = SceneComponent("Unregistered inactive control", unregister: true);
            unregistered.gameObject.SetActive(false);
            var suppressed = SceneComponent("Suppressed registration control");
            suppressed.SuppressRuntimeSaveRegistration();
            var disabledBetweenOwners = SceneComponent("Disabled between owners", unregister: true);
            var coldObject = new GameObject("Never active handoff target");
            coldObject.SetActive(false);
            Composite(coldObject, typeof(RememberTransform));
            var coldDestroyed = ConfigureTransform(coldObject);
            manager.ComponentManager.RegisterSaveableComponent(coldDestroyed);
            var prefab = Spawn();
            var blob = prefab.GetComponentInChildren<RememberTransform>(true);
            string prefabID = prefab.UniqueID;
            string activeID = active.UniqueIdentifier, inactiveID = inactive.UniqueIdentifier;
            var activePosition = new Vector3(3, 7, 11);
            var inactivePosition = new Vector3(13, 17, 19);
            var blobPosition = new Vector3(23, 29, 31);
            active.transform.position = activePosition;
            inactive.transform.position = inactivePosition;
            blob.transform.localPosition = blobPosition;
            yield return null;
            Assert.That(manager.ComponentManager.Contains(active), Is.True);
            Assert.That(manager.ComponentManager.Contains(inactive), Is.True);
            Assert.That(manager.ComponentManager.Contains(blob), Is.False);
            GameObject watched = null;
            if (replaceComponent)
            {
                var oldPrefabs = manager.GetPrefabManager;
                var queued = PrefabGenerationEntry(prefab, "retired-component-owner-prefab", true);
                Call(oldPrefabs, "QueueDeferredPrefabs", new object[] { new[] { queued } });
                Assert.That(oldPrefabs.HasDeferredPrefabs, Is.True);
                watched = new GameObject("Retired manager active-state watch");
                watched.AddComponent<UniqueID>().ID = "retired-component-owner-watch";
                manager.RegisterGameObject(watched, new GameObjectPropertySettings { RememberActive = true });
                Set(manager.GameObjectTracker, "activeStateWatchDuration", 30f);
                manager.GameObjectTracker.StartActiveStateWatch(new List<GameObjectState> {
                    new GameObjectState { UniqueID = "retired-component-owner-watch", IsActive = false }
                });
                Assert.That(watched.activeSelf, Is.False);
            }
            try
            {
                var survivingHost = manager.gameObject;
                UnityEngine.Object.Destroy(replaceComponent ? (UnityEngine.Object)manager : survivingHost);
                yield return null;
                yield return null;
                disabledBetweenOwners.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(coldObject);
                yield return null;
                yield return null;
                var replacement = (replaceComponent ? survivingHost :
                    new GameObject("Registration replacement SaveManager")).AddComponent<SaveManager>();
                yield return Until(() => SaveManager.IsInitialized && SaveManager.Instance == replacement,
                    "replacement with surviving registrations");
                if (replaceComponent)
                {
                    Assert.That(replacement.GetPrefabManager.HasDeferredPrefabs, Is.False,
                        "A retained sibling manager cannot restore the retired owner's queue.");
                    watched.SetActive(true);
                    yield return null;
                    yield return null;
                    Assert.That(watched.activeSelf, Is.True,
                        "The retired owner's watch must not apply old active-state data.");
                    UnityEngine.Object.Destroy(watched);
                    yield return null;
                }
                Assert.That(replacement.ComponentManager.Contains(active), Is.True);
                Assert.That(replacement.ComponentManager.Contains(inactive), Is.True);
                Assert.That(replacement.ComponentManager.Contains(unregistered), Is.False);
                Assert.That(replacement.ComponentManager.Contains(suppressed), Is.False);
                Assert.That(replacement.ComponentManager.Contains(disabledBetweenOwners), Is.False);
                Assert.That(replacement.ComponentManager.Contains(blob), Is.False);
                var initialized = (Delegate)typeof(SaveManager).GetField("Initialized", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That(initialized?.GetInvocationList().Any(listener => ReferenceEquals(listener.Target, coldDestroyed)) ?? false,
                    Is.False, "A never-active destroyed helper must remove its handoff subscription.");
                yield return Await(replacement.SaveAsync(1));
                var stored = replacement.LoadSaveDataForSlotAsync(1);
                yield return Await(stored);
                Assert.That(stored.Result.ComponentsData.ContainsKey(activeID), Is.True);
                Assert.That(stored.Result.ComponentsData.ContainsKey(inactiveID), Is.True);
                Assert.That(stored.Result.ComponentsData.ContainsKey(unregistered.UniqueIdentifier), Is.False);
                Assert.That(stored.Result.ComponentsData.ContainsKey(suppressed.UniqueIdentifier), Is.False);
                Assert.That(stored.Result.ComponentsData.ContainsKey(blob.UniqueIdentifier), Is.False);
                Assert.That(stored.Result.Prefabs.Any(entry => entry.InstanceID == prefabID), Is.True);
                active.transform.position = Vector3.one * 99;
                inactive.transform.position = Vector3.one * 99;
                inactive.gameObject.SetActive(true);
                blob.transform.localPosition = Vector3.one * 99;
                var load = replacement.LoadSaveSlotAsync(1);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                Assert.That(active.UniqueIdentifier, Is.EqualTo(activeID));
                Assert.That(inactive.UniqueIdentifier, Is.EqualTo(inactiveID));
                Assert.That(active.transform.position, Is.EqualTo(activePosition));
                Assert.That(inactive.transform.position, Is.EqualTo(inactivePosition));
                var restored = replacement.FindGameObjectByUniqueID(prefabID);
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.transform.Find("State").localPosition, Is.EqualTo(blobPosition));
            }
            finally
            {
                settings.registerDisabledComponents = scanDisabled;
                if (watched != null) UnityEngine.Object.Destroy(watched);
            }
        }

        [UnityTest]
        public IEnumerator LifecycleOwnership_InactiveHostLoadDoesNotStrandQueue()
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveAsync(1));
            LogAssert.ignoreFailingMessages = true;
            try
            {
                manager.gameObject.SetActive(false);
                var load = manager.LoadSaveSlotAsync(1, timeout: TimeSpan.FromMilliseconds(150));
                float deadline = Time.realtimeSinceStartup + 1.5f;
                while (!load.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                TestContext.WriteLine($"Inactive host: completed={load.IsCompleted}, isLoading={Get<bool>(manager, "isLoading")}");
                Assert.That(load.IsCompleted, Is.True, "Inactive host must reject or cancel the request instead of stranding its completion source after timeout.");
                Assert.That(load.Result.Success, Is.False);
                manager.gameObject.SetActive(true);
                yield return Await(manager.SaveAsync(2));
            }
            finally
            {
                if (manager) manager.gameObject.SetActive(true);
                DisposeLifetimeTestCoordinator(manager);
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator LifecycleOwnership_DeactivatedLoadHostReleasesOperation() => ManagerHostDeactivationScenario(false);

        [UnityTest]
        public IEnumerator LifecycleOwnership_DisabledBehaviourLoadFinishes_Control() => ManagerHostDeactivationScenario(true);

        IEnumerator ManagerHostDeactivationScenario(bool disableBehaviourOnly)
        {
            yield return LoadScene("A");
            var manager = SaveManager.Instance;
            var savedPrefab = Spawn();
            string savedIdentity = savedPrefab.UniqueID;
            var expected = new Vector3(17, 23, 31);
            savedPrefab.transform.Find("State").localPosition = expected;
            var tracker = Get<GameObjectTracker>(manager, "gameObjectTracker");
            Set(tracker, "enforceActiveState", true);
            Set(tracker, "activeStateWatchDuration", .1f);
            yield return Await(manager.SaveAsync(1, "A"));
            savedPrefab.transform.Find("State").localPosition = Vector3.one * 99;
            var physics3D = Physics.simulationMode;
            var physics2D = Physics2D.simulationMode;
            var sceneLoader = Get<object>(manager, "sceneLoadManager");
            bool activationRequested = false;
            Action<string> onActivation = scene => activationRequested = true;
            manager.OnSceneActivationRequested += onActivation;
            using var cancellation = new CancellationTokenSource();
            Task<LoadResult> load = null;
            bool completedBeforeCleanup = false, pendingBeforeCleanup = true;
            SimulationMode physics3DBeforeCleanup = Physics.simulationMode;
            SimulationMode2D physics2DBeforeCleanup = Physics2D.simulationMode;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                load = manager.LoadSaveSlotAsync(1, true, true, false, cancellationToken: cancellation.Token);
                yield return Until(() => activationRequested, "review pending activation", 5);
                if (disableBehaviourOnly) manager.enabled = false;
                else manager.gameObject.SetActive(false);
                cancellation.Cancel();
                float deadline = Time.realtimeSinceStartup + 1.5f;
                while (!load.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                completedBeforeCleanup = load.IsCompleted;
                pendingBeforeCleanup = Get<object>(sceneLoader, "pendingSceneOperation") != null;
                physics3DBeforeCleanup = Physics.simulationMode;
                physics2DBeforeCleanup = Physics2D.simulationMode;
                TestContext.WriteLine($"Deactivated host (behaviourOnly={disableBehaviourOnly}): completed={completedBeforeCleanup}, loading={Get<bool>(manager, "isLoading")}, pendingActivation={pendingBeforeCleanup}, physics={physics3DBeforeCleanup}/{physics2DBeforeCleanup}");
                if (completedBeforeCleanup)
                {
                    Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                    var restored = manager.FindGameObjectByUniqueID(savedIdentity);
                    Assert.That(restored, Is.Not.Null);
                    Assert.That(restored.transform.Find("State").localPosition, Is.EqualTo(expected));
                    manager.gameObject.SetActive(true);
                    manager.enabled = true;
                    yield return Await(manager.SaveAsync(2));
                }
            }
            finally
            {
                manager.OnSceneActivationRequested -= onActivation;
                if (manager)
                {
                    manager.gameObject.SetActive(true);
                    manager.enabled = true;
                    manager.ActivatePendingScene();
                }
                DisposeLifetimeTestCoordinator(manager);
                Physics.simulationMode = physics3D;
                Physics2D.simulationMode = physics2D;
            }
            float cleanupDeadline = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < cleanupDeadline) yield return null;
            LogAssert.ignoreFailingMessages = false;
            Assert.That(completedBeforeCleanup, Is.True, "Stopping the host cannot leave an active load and its completion source indefinitely owned.");
            Assert.That(physics3DBeforeCleanup, Is.EqualTo(physics3D));
            Assert.That(physics2DBeforeCleanup, Is.EqualTo(physics2D));
            Assert.That(pendingBeforeCleanup, Is.False);
        }

        static void DisposeLifetimeTestCoordinator(SaveManager manager)
        {
            if (!manager) return;
            var coordinator = Get<object>(manager, "operationCoordinator");
            (coordinator as IDisposable)?.Dispose();
        }

        [UnityTest]
        public IEnumerator LifecycleOwnership_ShutdownCallbacksCannotRecreateDispatcher()
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveAsync(1));
            var tracker = Get<GameObjectTracker>(manager, "gameObjectTracker");
            var owner = new GameObject("Shutdown active-state owner");
            owner.AddComponent<UniqueID>().ID = "shutdown-active-state-owner";
            manager.RegisterGameObject(owner, new GameObjectPropertySettings { RememberActive = true });
            var dispatcher = UnityMainThreadDispatcher.Instance();
            Assert.That(dispatcher.gameObject, Is.Not.SameAs(manager.gameObject));
            // Model Unity's quit ordering: all owners receive OnApplicationQuit,
            // then the independent dispatcher can die before scene OnDisable.
            typeof(SaveManager).GetMethod("OnApplicationQuit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, null);
            UnityEngine.Object.Destroy(dispatcher.gameObject);
            yield return null;
            yield return null;
            bool recreated;
            try
            {
                owner.SetActive(false); // Real TrackedGameObjectProxy.OnDisable route.
                Set(tracker, "activeStateWatchDuration", .1f);
                tracker.StartActiveStateWatch(new System.Collections.Generic.List<GameObjectState>());
                recreated = typeof(UnityMainThreadDispatcher).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)
                    .GetValue(null) is UnityMainThreadDispatcher current && current;
            }
            finally
            {
                UnityEngine.Object.Destroy(owner);
                UnityEngine.Object.Destroy(manager.gameObject);
            }
            yield return null;
            yield return null;
            Assert.That(recreated, Is.False, "Shutdown callbacks cannot spawn a dispatcher while Unity is closing the scene.");
        }

        [UnityTest]
        public IEnumerator LifecycleOwnership_DuplicateManagerCannotResetOwnerReadiness()
        {
            var owner = SaveManager.Instance;
            var duplicate = new GameObject("Duplicate SaveManager");
            duplicate.AddComponent<SaveManager>();
            yield return null;
            yield return null;
            Assert.That(duplicate == null, Is.True);
            Assert.That(SaveManager.Instance, Is.SameAs(owner));
            Assert.That(SaveManager.IsInitialized && SaveManager.AreSaveSlotsReady, Is.True);
            yield return Await(owner.SaveAsync(1));
        }

        [UnityTest]
        public IEnumerator LifecycleOwnership_DeactivatedPreparationCancelsAndQueueRecovers()
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveAsync(1));
            var storage = manager.SaveSystem;
            var delayed = new HardeningStorage(storage) { GateReads = true };
            Set(manager, "saveSystem", delayed);
            using var cancellation = new CancellationTokenSource();
            var originalData = manager.CurrentSaveData;
            var physics3D = Physics.simulationMode;
            var physics2D = Physics2D.simulationMode;
            try
            {
                var load = manager.LoadSaveSlotAsync(1, cancellationToken: cancellation.Token);
                yield return Until(() => Get<bool>(manager, "isLoading"), "preparing load");
                yield return null;
                manager.gameObject.SetActive(false);
                cancellation.Cancel();
                yield return Until(() => load.IsCompleted, "cancelled disabled-host preparation", 3);
                Assert.That(load.Result.Success, Is.False);
                Assert.That(manager.CurrentSaveData, Is.SameAs(originalData));
                Assert.That(Physics.simulationMode, Is.EqualTo(physics3D));
                Assert.That(Physics2D.simulationMode, Is.EqualTo(physics2D));
                manager.gameObject.SetActive(true);
                Set(manager, "saveSystem", storage);
                delayed.Read.TrySetResult(storage.Load(manager.GetSlotMetadata(1)));
                yield return Await(manager.SaveAsync(2));
            }
            finally
            {
                manager.gameObject.SetActive(true);
                Set(manager, "saveSystem", storage);
                delayed.Release();
            }
        }
    }
}
#endif
