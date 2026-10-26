#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrystalSaveLogger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class SaveManagerLookupOptimizationEditorTests
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly List<GameObject> createdObjects = new();
        private SaveManager saveManager;
        private GameObjectTracker tracker;
        private PrefabManager prefabManager;
        private PropertyInfo instanceProperty;
        private SaveManager previousInstance;

        [SetUp]
        public void SetUp()
        {
            var managerRoot = CreateObject("LookupOptimizationTestManager", active: false);
            saveManager = managerRoot.AddComponent<SaveManager>();
            tracker = managerRoot.GetComponent<GameObjectTracker>();
            prefabManager = managerRoot.GetComponent<PrefabManager>();

            Assert.That(tracker, Is.Not.Null);
            Assert.That(prefabManager, Is.Not.Null);

            SetField(saveManager, "gameObjectTracker", tracker);
            SetField(saveManager, "prefabManager", prefabManager);

            MethodInfo initializeTracker = typeof(GameObjectTracker).GetMethod(
                "Initialize",
                InstanceFlags);
            Assert.That(initializeTracker, Is.Not.Null);
            initializeTracker.Invoke(tracker, new object[] { saveManager, prefabManager, null });

            instanceProperty = typeof(SaveManager).GetProperty(
                nameof(SaveManager.Instance),
                StaticFlags);
            Assert.That(instanceProperty, Is.Not.Null);
            previousInstance = (SaveManager)instanceProperty.GetValue(null);
            instanceProperty.SetValue(null, saveManager);

            saveManager.SetLookupCacheEnabled(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (instanceProperty != null &&
                ReferenceEquals(instanceProperty.GetValue(null), saveManager))
            {
                instanceProperty.SetValue(null, previousInstance);
            }

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void TrackerMappingWinsBeforeSceneScanWhenCacheIsDisabled()
        {
            const string identifier = "lookup-tracker-priority";
            GameObject tracked = CreateUniqueObject("TrackedCandidate", identifier, active: false);
            CreateUniqueObject("CompetingSceneCandidate", identifier, active: false);

            tracker.RegisterGameObject(tracked, new GameObjectPropertySettings());

            GameObject result = saveManager.FindGameObjectByUniqueID(
                identifier,
                SaveManager.IdentifierType.UniqueID);

            Assert.That(result, Is.SameAs(tracked));
            Assert.That(saveManager.LookupCacheEnabled, Is.False);
        }

        [Test]
        public void PrefabManagerMappingWinsBeforeGeneralUniqueIdScan()
        {
            const string identifier = "lookup-prefab-mapping-priority";
            GameObject mapped = CreateObject("MappedPrefab", active: false);
            SaveablePrefab saveablePrefab = mapped.AddComponent<SaveablePrefab>();
            SetField(saveablePrefab, "uniqueID", identifier);

            CreateUniqueObject("CompetingUniqueId", identifier, active: false);
            GetField<Dictionary<string, GameObject>>(prefabManager, "instantiatedPrefabs")
                .Add(identifier, mapped);

            GameObject result = saveManager.FindGameObjectByUniqueID(
                identifier,
                SaveManager.IdentifierType.UniqueID);

            Assert.That(result, Is.SameAs(mapped));
        }

        [Test]
        public void StalePrefabManagerMappingIsPrunedBeforeInactiveFallback()
        {
            const string identifier = "lookup-stale-prefab-mapping";
            GameObject staleMapped = CreateObject("StaleMappedPrefab", active: false);
            SaveablePrefab staleSaveable = staleMapped.AddComponent<SaveablePrefab>();
            SetField(staleSaveable, "uniqueID", "different-owner-id");

            Dictionary<string, GameObject> mappings =
                GetField<Dictionary<string, GameObject>>(
                    prefabManager,
                    "instantiatedPrefabs");
            mappings[identifier] = staleMapped;

            GameObject replacement = CreateUniqueObject(
                "InactivePrefabMappingReplacement",
                identifier,
                active: false);
            GameObject result = saveManager.FindGameObjectByUniqueID(
                identifier,
                SaveManager.IdentifierType.UniqueID);

            Assert.That(result, Is.SameAs(replacement));
            Assert.That(mappings.ContainsKey(identifier), Is.False);
        }

        [Test]
        public void DestroyedCacheAndTrackerEntriesArePrunedBeforeInactiveFallback()
        {
            const string identifier = "lookup-stale-entry";
            saveManager.SetLookupCacheEnabled(true);

            GameObject stale = CreateUniqueObject("StaleTrackedObject", identifier, active: false);
            tracker.RegisterGameObject(stale, new GameObjectPropertySettings());
            FeatureTestSupport.Call(saveManager, "CacheGameObject", identifier, stale);
            UnityEngine.Object.DestroyImmediate(stale);

            GameObject replacement = CreateUniqueObject("InactiveReplacement", identifier, active: false);
            GameObject result = saveManager.FindGameObjectByUniqueID(
                identifier,
                SaveManager.IdentifierType.UniqueID);

            Assert.That(result, Is.SameAs(replacement));
            Assert.That(tracker.TryGetTrackedGameObject(identifier, out _), Is.False);
            Assert.That(
                FeatureTestSupport.CachedObjects(saveManager)[identifier],
                Is.SameAs(replacement));
        }

        [Test]
        public void LiveCacheOwnerWithChangedIdIsPrunedBeforeInactiveFallback()
        {
            const string previousIdentifier = "lookup-live-stale-cache";
            const string currentIdentifier = "lookup-live-cache-current";
            saveManager.SetLookupCacheEnabled(true);

            GameObject stale = CreateUniqueObject(
                "LiveStaleCacheOwner",
                previousIdentifier,
                active: false);
            FeatureTestSupport.Call(saveManager, "CacheGameObject", previousIdentifier, stale);
            stale.GetComponent<UniqueID>().ID = currentIdentifier;

            GameObject replacement = CreateUniqueObject(
                "LiveStaleCacheReplacement",
                previousIdentifier,
                active: false);
            GameObject result = saveManager.FindGameObjectByUniqueID(
                previousIdentifier,
                SaveManager.IdentifierType.UniqueID);

            Assert.That(result, Is.SameAs(replacement));
            Assert.That(
                FeatureTestSupport.CachedObjects(saveManager)
                    [previousIdentifier],
                Is.SameAs(replacement));
            Assert.That(stale.GetComponent<UniqueID>().ID, Is.EqualTo(currentIdentifier));
        }

        [Test]
        public void LiveTrackerOwnerWithChangedIdIsPrunedBeforeInactiveFallback()
        {
            const string previousIdentifier = "lookup-live-stale-tracker";
            const string currentIdentifier = "lookup-live-tracker-current";

            GameObject stale = CreateUniqueObject(
                "LiveStaleTrackerOwner",
                previousIdentifier,
                active: false);
            tracker.RegisterGameObject(stale, new GameObjectPropertySettings());
            stale.GetComponent<UniqueID>().ID = currentIdentifier;

            GameObject replacement = CreateUniqueObject(
                "LiveStaleTrackerReplacement",
                previousIdentifier,
                active: false);
            GameObject result = saveManager.FindGameObjectByUniqueID(
                previousIdentifier,
                SaveManager.IdentifierType.UniqueID);

            Assert.That(result, Is.SameAs(replacement));
            Assert.That(
                tracker.TryGetTrackedGameObject(previousIdentifier, out _),
                Is.False);
            Assert.That(stale.GetComponent<UniqueID>().ID, Is.EqualTo(currentIdentifier));
        }

        [Test]
        public void ExplicitUniqueIdLookupDoesNotReturnTrackedSceneObjectIdOwner()
        {
            const string identifier = "lookup-cross-mode-tracker";
            GameObject sceneObject = CreateObject(
                "TrackedSceneObjectIdOwner",
                active: false);
            SceneObjectID sceneObjectID = sceneObject.AddComponent<SceneObjectID>();
            SetField(sceneObjectID, "uniqueID", identifier);
            tracker.RegisterGameObject(
                sceneObject,
                new GameObjectPropertySettings());

            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    identifier,
                    SaveManager.IdentifierType.UniqueID),
                Is.Null);
            Assert.That(
                tracker.TryGetTrackedGameObject(identifier, out _),
                Is.True,
                "A valid mapping owned by another identifier mode must not be pruned.");
            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    identifier,
                    SaveManager.IdentifierType.SceneObjectID),
                Is.SameAs(sceneObject));
        }

        [Test]
        public void AutoModePrefersUniqueIdWhenIdentifierCollidesAcrossTypedComponents()
        {
            const string identifier = "lookup-auto-mode-collision";

            GameObject uniqueObject = CreateUniqueObject(
                "AutoCollisionUniqueId",
                identifier,
                active: false);

            GameObject sceneObject = CreateObject(
                "AutoCollisionSceneObjectId",
                active: false);
            SceneObjectID sceneObjectID = sceneObject.AddComponent<SceneObjectID>();
            SetField(sceneObjectID, "uniqueID", identifier);

            GameObject assetObject = CreateObject(
                "AutoCollisionPrefabAssetId",
                active: false);
            SaveablePrefab assetPrefab = assetObject.AddComponent<SaveablePrefab>();
            SetField(assetPrefab, "prefabAssetID", identifier);

            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    identifier,
                    SaveManager.IdentifierType.Auto),
                Is.SameAs(uniqueObject),
                "Auto lookups must retain the public UniqueID, SceneObjectID, " +
                "then PrefabAssetID precedence.");
            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    identifier,
                    SaveManager.IdentifierType.UniqueID),
                Is.SameAs(uniqueObject));
            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    identifier,
                    SaveManager.IdentifierType.SceneObjectID),
                Is.SameAs(sceneObject));
            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    identifier,
                    SaveManager.IdentifierType.PrefabAssetID),
                Is.SameAs(assetObject));
        }

        [Test]
        public void SlowFallbacksPreserveTypedInactiveBaseIdAndLegacyNames()
        {
            const string baseIdentifier = "lookup-base";
            GameObject baseObject = CreateUniqueObject(
                "InactiveBaseIdentifier",
                baseIdentifier,
                active: false);

            GameObject sceneObject = CreateObject("InactiveSceneIdentifier", active: false);
            SceneObjectID sceneObjectID = sceneObject.AddComponent<SceneObjectID>();
            SetField(sceneObjectID, "uniqueID", "lookup-scene-id");

            GameObject assetObject = CreateObject("InactiveAssetIdentifier", active: false);
            SaveablePrefab assetPrefab = assetObject.AddComponent<SaveablePrefab>();
            SetField(assetPrefab, "prefabAssetID", "lookup-prefab-asset");

            GameObject prefabBaseObject = CreateObject(
                "InactivePrefabBaseIdentifier",
                active: false);
            SaveablePrefab prefabBase =
                prefabBaseObject.AddComponent<SaveablePrefab>();
            SetField(prefabBase, "uniqueID", "lookup-prefab-base");

            GameObject exactName = CreateObject("lookup-exact-name", active: true);
            GameObject cloneName = CreateObject("lookup-clone-name(Clone)", active: true);
            GameObject inactiveExactName = CreateObject(
                "lookup-inactive-exact-name",
                active: false);
            GameObject inactiveCloneName = CreateObject(
                "lookup-inactive-clone-name(Clone)",
                active: false);

            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    baseIdentifier + "_component",
                    SaveManager.IdentifierType.UniqueID),
                Is.SameAs(baseObject));
            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    "lookup-scene-id",
                    SaveManager.IdentifierType.SceneObjectID),
                Is.SameAs(sceneObject));
            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    "lookup-prefab-asset",
                    SaveManager.IdentifierType.PrefabAssetID),
                Is.SameAs(assetObject));
            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    "lookup-prefab-base_component",
                    SaveManager.IdentifierType.UniqueID),
                Is.SameAs(prefabBaseObject));
            Assert.That(
                saveManager.FindGameObjectByUniqueID("lookup-exact-name"),
                Is.SameAs(exactName));
            Assert.That(
                saveManager.FindGameObjectByUniqueID("lookup-clone-name"),
                Is.SameAs(cloneName));
            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    "lookup-inactive-exact-name"),
                Is.SameAs(inactiveExactName));
            Assert.That(
                saveManager.FindGameObjectByUniqueID(
                    "lookup-inactive-clone-name"),
                Is.SameAs(inactiveCloneName));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CacheToggleDoesNotChangeInactiveTypedLookupCorrectness(
            bool cacheEnabled)
        {
            string identifier =
                cacheEnabled
                    ? "lookup-cache-enabled"
                    : "lookup-cache-disabled";
            GameObject expected = CreateUniqueObject(
                "InactiveCacheToggleCandidate",
                identifier,
                active: false);

            saveManager.SetLookupCacheEnabled(cacheEnabled);
            GameObject first = saveManager.FindGameObjectByUniqueID(
                identifier,
                SaveManager.IdentifierType.UniqueID);
            GameObject second = saveManager.FindGameObjectByUniqueID(
                identifier,
                SaveManager.IdentifierType.UniqueID);

            Assert.That(first, Is.SameAs(expected));
            Assert.That(second, Is.SameAs(expected));
            Assert.That(saveManager.LookupCacheEnabled, Is.EqualTo(cacheEnabled));

            IReadOnlyDictionary<string, GameObject> cache =
                FeatureTestSupport.CachedObjects(saveManager);
            Assert.That(
                cache.ContainsKey(identifier),
                Is.EqualTo(cacheEnabled));
        }

        [Test]
        public void DestroyedIdListsRemainIndependentSnapshots()
        {
            const string identifier = "lookup-destroyed-snapshot";
            GetField<HashSet<string>>(tracker, "destroyedIDs").Add(identifier);

            List<string> first = saveManager.GetDestroyedGameObjectIDs();
            first.Clear();
            List<string> second = saveManager.GetDestroyedGameObjectIDs();

            Assert.That(first, Is.Empty);
            Assert.That(second, Does.Contain(identifier));
            Assert.That(tracker.IsGameObjectDestroyed(identifier), Is.True);
            Assert.That(saveManager.IsGameObjectDestroyed(identifier), Is.True);
        }

        [Test]
        public void CollectActiveStatesRetainsUnloadedOwnerStateAcrossRepeatedCollectionAndReregistration()
        {
            const string identifier = "lookup-unloaded-active-state";
            var settings = new GameObjectPropertySettings
            {
                RememberActive = true
            };
            GameObject unloadedOwner = CreateUniqueObject(
                "UnloadedActiveStateOwner",
                identifier,
                active: false);
            tracker.RegisterGameObject(unloadedOwner, settings);

            Dictionary<string, bool> activeStates =
                GetField<Dictionary<string, bool>>(tracker, "activeStates");
            Assert.That(activeStates[identifier], Is.False);

            UnityEngine.Object.DestroyImmediate(unloadedOwner);

            List<GameObjectState> firstCollection = null;
            Assert.DoesNotThrow(
                () => firstCollection = tracker.CollectActiveStates());
            Assert.That(
                firstCollection.Exists(
                    state =>
                        state.UniqueID == identifier &&
                        state.IsActive == false),
                Is.True);
            Assert.That(
                tracker.TryGetTrackedGameObject(identifier, out _),
                Is.False,
                "The destroyed live mapping should be pruned.");
            Assert.That(
                activeStates.TryGetValue(identifier, out bool retainedState),
                Is.True,
                "An additive-scene unload must not erase its persisted active state.");
            Assert.That(retainedState, Is.False);

            List<GameObjectState> secondCollection = null;
            Assert.DoesNotThrow(
                () => secondCollection = tracker.CollectActiveStates());
            Assert.That(
                secondCollection.Exists(
                    state =>
                        state.UniqueID == identifier &&
                        state.IsActive == false),
                Is.True,
                "Repeated saves after the scene unload must retain the state.");

            GameObject replacement = CreateUniqueObject(
                "ReloadedActiveStateOwner",
                identifier,
                active: true);
            tracker.RegisterGameObject(replacement, settings);
            Assert.That(
                activeStates[identifier],
                Is.False,
                "Re-registration must preserve the state loaded for this ID.");
            Assert.That(
                replacement.activeSelf,
                Is.True,
                "Registration itself should not mutate the returning object's state; " +
                "the retained serialized value remains available for the load apply phase.");
        }

        [Test]
        public void DeferredInactiveUpdateRetainsLastStateWhenOwnerUnloads()
        {
            const string identifier = "lookup-unload-deactivation";
            var settings = new GameObjectPropertySettings
            {
                RememberActive = true
            };
            GameObject owner = CreateUniqueObject(
                "UnloadDeactivationOwner",
                identifier,
                active: false);
            tracker.RegisterGameObject(owner, settings);

            Dictionary<string, bool> activeStates =
                GetField<Dictionary<string, bool>>(tracker, "activeStates");
            owner.SetActive(true);
            tracker.UpdateActiveState(identifier, true);
            Assert.That(activeStates[identifier], Is.True);

            // Scene teardown raises OnDisable before the owner becomes Unity-null.
            // The false notification must remain pending until that distinction is known.
            owner.SetActive(false);
            InvokeOwnerActiveStateUpdate(
                tracker,
                identifier,
                owner,
                isActive: false);
            Assert.That(activeStates[identifier], Is.True);
            UnityEngine.Object.DestroyImmediate(owner);
            InvokePrivate(tracker, "FlushPendingInactiveStateUpdates");

            Assert.That(activeStates[identifier], Is.True);
            List<GameObjectState> collection = tracker.CollectActiveStates();
            Assert.That(
                collection.Exists(
                    state =>
                        state.UniqueID == identifier &&
                        state.IsActive == true),
                Is.True);
            Assert.That(
                tracker.TryGetTrackedGameObject(identifier, out _),
                Is.False);
        }

        [Test]
        public void ExplicitDeactivationIsVisibleToSameFrameCollection()
        {
            const string identifier = "lookup-explicit-deactivation";
            var settings = new GameObjectPropertySettings
            {
                RememberActive = true
            };
            GameObject owner = CreateUniqueObject(
                "ExplicitDeactivationOwner",
                identifier,
                active: false);
            tracker.RegisterGameObject(owner, settings);
            owner.SetActive(true);
            tracker.UpdateActiveState(identifier, true);
            owner.SetActive(false);
            InvokeOwnerActiveStateUpdate(
                tracker,
                identifier,
                owner,
                isActive: false);

            List<GameObjectState> collection = tracker.CollectActiveStates();
            Assert.That(
                collection.Exists(
                    state =>
                        state.UniqueID == identifier &&
                        state.IsActive == false),
                Is.True,
                "A same-frame save must read the still-live inactive owner.");

            InvokePrivate(tracker, "FlushPendingInactiveStateUpdates");
            Assert.That(
                GetField<Dictionary<string, bool>>(tracker, "activeStates")
                    [identifier],
                Is.False);
        }

        [Test]
        public void ReenableBeforeDeferredConfirmationKeepsActiveStateTrue()
        {
            const string identifier = "lookup-deactivation-cancel";
            var settings = new GameObjectPropertySettings
            {
                RememberActive = true
            };
            GameObject owner = CreateUniqueObject(
                "ReenabledOwner",
                identifier,
                active: false);
            tracker.RegisterGameObject(owner, settings);
            owner.SetActive(true);
            tracker.UpdateActiveState(identifier, true);
            owner.SetActive(false);
            InvokeOwnerActiveStateUpdate(
                tracker,
                identifier,
                owner,
                isActive: false);
            owner.SetActive(true);
            InvokeOwnerActiveStateUpdate(
                tracker,
                identifier,
                owner,
                isActive: true);

            InvokePrivate(tracker, "FlushPendingInactiveStateUpdates");
            Assert.That(
                GetField<Dictionary<string, bool>>(tracker, "activeStates")
                    [identifier],
                Is.True);
        }

        [Test]
        public void CollectActiveStatesDoesNotEmitObsoleteStateAfterLiveOwnerChangesId()
        {
            const string previousIdentifier = "lookup-active-state-previous-id";
            const string currentIdentifier = "lookup-active-state-current-id";
            var settings = new GameObjectPropertySettings
            {
                RememberActive = true
            };
            GameObject owner = CreateUniqueObject(
                "ChangedActiveStateOwner",
                previousIdentifier,
                active: false);
            tracker.RegisterGameObject(owner, settings);
            owner.GetComponent<UniqueID>().ID = currentIdentifier;

            List<GameObjectState> collection = null;
            Assert.DoesNotThrow(
                () => collection = tracker.CollectActiveStates());

            Assert.That(
                collection.Exists(
                    state => state.UniqueID == previousIdentifier),
                Is.False,
                "Lookup cleanup removed the obsolete identity, so the collection " +
                "snapshot must not reintroduce it.");
            Assert.That(
                tracker.TryGetTrackedGameObject(previousIdentifier, out _),
                Is.False);
            Assert.That(
                GetField<Dictionary<string, bool>>(tracker, "activeStates")
                    .ContainsKey(previousIdentifier),
                Is.False);
        }

        [Test]
        public void InternalExpectedMissIsQuietWhilePublicLookupStillLogs()
        {
            const string publicIdentifier = "lookup-public-terminal-miss";
            const string internalIdentifier = "lookup-internal-quiet-miss";
            FieldInfo cachedConfigField =
                typeof(CrystalSaveLogger).GetField("cachedConfig", StaticFlags);
            FieldInfo configSearchedField =
                typeof(CrystalSaveLogger).GetField("configSearched", StaticFlags);
            Assert.That(cachedConfigField, Is.Not.Null);
            Assert.That(configSearchedField, Is.Not.Null);

            LogLevel previousThreshold = CrystalSaveLogger.LogThreshold;
            object previousConfig = cachedConfigField.GetValue(null);
            object previousConfigSearched = configSearchedField.GetValue(null);
            var messages = new List<string>();

            void CaptureLog(string condition, string stackTrace, LogType type)
            {
                messages.Add(condition);
            }

            try
            {
                CrystalSaveLogger.LogThreshold = LogLevel.Info;
                cachedConfigField.SetValue(null, null);
                configSearchedField.SetValue(null, true);
                Application.logMessageReceived += CaptureLog;

                Assert.That(
                    saveManager.FindGameObjectByUniqueID(
                        publicIdentifier,
                        SaveManager.IdentifierType.UniqueID),
                    Is.Null);
                Assert.That(
                    messages.Exists(
                        message =>
                            message.Contains(publicIdentifier) &&
                            message.Contains("not found")),
                    Is.True,
                    "The public API must retain its terminal miss diagnostic.");

                messages.Clear();
                MethodInfo quietLookup = typeof(SaveManager).GetMethod(
                    "FindGameObjectByUniqueIDWithoutMissingLog",
                    InstanceFlags);
                Assert.That(quietLookup, Is.Not.Null);
                Assert.That(
                    quietLookup.Invoke(
                        saveManager,
                        new object[]
                        {
                            internalIdentifier,
                            SaveManager.IdentifierType.UniqueID
                        }),
                    Is.Null);
                Assert.That(
                    messages.Exists(
                        message => message.Contains(internalIdentifier)),
                    Is.False,
                    "Expected internal misses must not add terminal lookup noise.");
            }
            finally
            {
                Application.logMessageReceived -= CaptureLog;
                CrystalSaveLogger.LogThreshold = previousThreshold;
                cachedConfigField.SetValue(null, previousConfig);
                configSearchedField.SetValue(null, previousConfigSearched);
            }
        }

        [Test]
        public void AdditiveActiveStateDemoReliesOnBootstrappedSaveManager()
        {
            const string sceneAssetPath =
                "Assets/Plugins/CrystalSave/Modules/Unity/Demos/Scenes/" +
                "CrystalSaveDemo_AdditiveActiveState_Main.unity";
            SceneAsset sceneAsset =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneAssetPath);
            Assert.That(sceneAsset, Is.Not.Null);

            string relativePath =
                sceneAssetPath.Substring("Assets/".Length);
            string yaml = File.ReadAllText(
                Path.Combine(Application.dataPath, relativePath));

            Assert.That(yaml, Does.Not.Contain("&1541417010"));
            Assert.That(yaml, Does.Not.Contain("{fileID: 1541417016}"));
            Assert.That(
                yaml,
                Does.Not.Contain(
                    "Arawn.CrystalSave.Runtime.SaveManager"),
                "The demo should use CrystalSave's pre-scene bootstrap singleton.");
        }

        private GameObject CreateUniqueObject(string name, string identifier, bool active)
        {
            GameObject go = CreateObject(name, active: false);
            UniqueID uniqueID = go.AddComponent<UniqueID>();
            uniqueID.ID = identifier;
            go.SetActive(active);
            return go;
        }

        private GameObject CreateObject(string name, bool active)
        {
            var go = new GameObject(name);
            go.SetActive(active);
            createdObjects.Add(go);
            return go;
        }

        private static T GetField<T>(object owner, string fieldName)
        {
            FieldInfo field = owner.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected {owner.GetType().Name}.{fieldName}.");
            return (T)field.GetValue(owner);
        }

        private static void SetField(object owner, string fieldName, object value)
        {
            FieldInfo field = owner.GetType().GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Expected {owner.GetType().Name}.{fieldName}.");
            field.SetValue(owner, value);
        }

        private static void InvokePrivate(object owner, string methodName)
        {
            MethodInfo method = owner.GetType().GetMethod(methodName, InstanceFlags);
            Assert.That(method, Is.Not.Null, $"Expected {owner.GetType().Name}.{methodName}.");
            method.Invoke(owner, null);
        }

        private static void InvokeOwnerActiveStateUpdate(
            GameObjectTracker owner,
            string identifier,
            GameObject trackedObject,
            bool isActive)
        {
            MethodInfo method = typeof(GameObjectTracker).GetMethod(
                "UpdateActiveState",
                InstanceFlags,
                null,
                new[] { typeof(string), typeof(GameObject), typeof(bool) },
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(
                owner,
                new object[] { identifier, trackedObject, isActive });
        }
    }
}
#endif
