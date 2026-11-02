namespace Tests.Editor
{
    using System;
    using System.IO;
    using Core;
    using Core.Ids;
    using Core.Persistence;
    using Domain.Creator;
    using Domain.Entities;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using NUnit.Framework;
    using Studio.Editor;
    using Systems.Creator;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Domain 5 verification suite (<c>EDT-01</c> through <c>EDT-04</c>) from <c>ARCHITECTURE_TEST_STRATEGY.md</c>.
    /// Verifies persistent <see cref="InstanceGuid"/> Undo/Redo determinism, Playtest state isolation,
    /// <see cref="CrossObjectLink"/> execution, and Synty <c>POLYGON Fantasy Kingdom</c> catalog migration (<c>ADR-008</c>).
    /// </summary>
    public class Domain5_InGameEditorTests
    {
        private string _tempSaveDir;
        private CreatorStudioService _studio;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            GameEventManager.Clear();
            PersistentEntityComponent.ClearRegistry();

            _tempSaveDir = Path.Combine(Path.GetTempPath(), "FantasyEngine_Domain5_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSaveDir);

            _studio = new CreatorStudioService { SaveDirectory = _tempSaveDir };
            ServiceLocator.Register(_studio);
            _studio.Initialize();
            _studio.SetBuildMode(true);
        }

        [TearDown]
        public void TearDown()
        {
            _studio?.Shutdown();
            PersistentEntityComponent.ClearRegistry();
            GameEventManager.Clear();
            ServiceLocator.Clear();

            if (!string.IsNullOrEmpty(_tempSaveDir) && Directory.Exists(_tempSaveDir))
            {
                try { Directory.Delete(_tempSaveDir, recursive: true); }
                catch { /* Ignore file lock during cleanup */ }
            }
        }

        #region EDT-01: CommandHistory Place -> Transform -> Delete Undo & Redo

        [Test]
        public void EDT01_CommandHistory_PlaceTransformDelete_UndoAndRedoRestoresExactInstanceGuid()
        {
            var placeCmd = new PlaceObjectCommand(_studio, "wooden_crate", Vector3.zero, Quaternion.identity);
            InstanceGuid expectedGuid = placeCmd.InstanceId;
            Assert.IsFalse(expectedGuid.IsEmpty);

            // 1. Place at (0, 0, 0)
            _studio.CommandHistory.ExecuteCommand(placeCmd);
            Assert.IsTrue(_studio.TryGetPlacedObject(expectedGuid, out GameObject placedGo));
            Assert.AreEqual(Vector3.zero, placedGo.transform.position);

            // 2. Transform to (10, 0, 10)
            var targetPos = new Vector3(10f, 0f, 10f);
            var transformCmd = new TransformObjectCommand(_studio, expectedGuid, targetPos);
            _studio.CommandHistory.ExecuteCommand(transformCmd);
            Assert.AreEqual(targetPos, placedGo.transform.position);

            // 3. Delete
            var deleteCmd = new DeleteObjectCommand(_studio, expectedGuid);
            _studio.CommandHistory.ExecuteCommand(deleteCmd);
            Assert.IsFalse(_studio.TryGetPlacedObject(expectedGuid, out _));

            // Undo 1 (Undoes Delete) -> Object restored at (10, 0, 10) with exact same InstanceGuid
            Assert.IsTrue(_studio.CommandHistory.Undo());
            Assert.IsTrue(_studio.TryGetPlacedObject(expectedGuid, out GameObject restoredGo));
            Assert.IsNotNull(restoredGo);
            Assert.AreEqual(expectedGuid, restoredGo.GetComponent<EditorPlacedEntityTag>().InstanceId);
            Assert.AreEqual(targetPos, restoredGo.transform.position);

            // Undo 2 (Undoes Transform) -> Object back at (0, 0, 0) with exact same InstanceGuid
            Assert.IsTrue(_studio.CommandHistory.Undo());
            Assert.AreEqual(Vector3.zero, restoredGo.transform.position);
            Assert.AreEqual(expectedGuid, restoredGo.GetComponent<EditorPlacedEntityTag>().InstanceId);

            // Undo 3 (Undoes Place) -> Object removed
            Assert.IsTrue(_studio.CommandHistory.Undo());
            Assert.IsFalse(_studio.TryGetPlacedObject(expectedGuid, out _));

            // Redo 1 (Redoes Place) -> Object back at (0, 0, 0) with exact same InstanceGuid
            Assert.IsTrue(_studio.CommandHistory.Redo());
            Assert.IsTrue(_studio.TryGetPlacedObject(expectedGuid, out GameObject redoneGo));
            Assert.AreEqual(Vector3.zero, redoneGo.transform.position);
            Assert.AreEqual(expectedGuid, redoneGo.GetComponent<EditorPlacedEntityTag>().InstanceId);

            // Redo 2 (Redoes Transform) -> Object moved to (10, 0, 10)
            Assert.IsTrue(_studio.CommandHistory.Redo());
            Assert.AreEqual(targetPos, redoneGo.transform.position);

            // Redo 3 (Redoes Delete) -> Object deleted again
            Assert.IsTrue(_studio.CommandHistory.Redo());
            Assert.IsFalse(_studio.TryGetPlacedObject(expectedGuid, out _));
        }

        #endregion

        #region EDT-02: EditorPlaytestCoordinator State Isolation

        [Test]
        public void EDT02_EditorPlaytestCoordinator_MutatingWorldDuringPlaytest_RestoresAuthoredStateOnExit()
        {
            // Create templates for a locked chest and an actor with 100 Health
            var chestTemplate = new GameObject("ChestTemplate");
            chestTemplate.SetActive(false);
            var chestTemplateComp = chestTemplate.AddComponent<ChestInteractable>();
            chestTemplateComp.ConfigureChest(LockDifficulty.Master, isOpened: false);

            var actorTemplate = new GameObject("ActorTemplate");
            actorTemplate.SetActive(false);
            actorTemplate.AddComponent<StateComponent>();
            actorTemplate.AddComponent<BaseActor>();
            var actorTemplateAttr = actorTemplate.AddComponent<AttributeComponent>();

            _studio.RegisterTemplatePrefab("test_chest", chestTemplate);
            _studio.RegisterTemplatePrefab("test_actor", actorTemplate);

            try
            {
                // Place Chest and Actor in Authoring Mode
                var placeChest = new PlaceObjectCommand(_studio, "test_chest", new Vector3(2f, 0f, 0f), Quaternion.identity);
                var placeActor = new PlaceObjectCommand(_studio, "test_actor", new Vector3(5f, 0f, 0f), Quaternion.identity);

                _studio.CommandHistory.ExecuteCommand(placeChest);
                _studio.CommandHistory.ExecuteCommand(placeActor);

                InstanceGuid chestGuid = placeChest.InstanceId;
                InstanceGuid actorGuid = placeActor.InstanceId;

                Assert.IsTrue(_studio.TryGetPlacedObject(chestGuid, out GameObject chestGo));
                Assert.IsTrue(_studio.TryGetPlacedObject(actorGuid, out GameObject actorGo));

                var chest = chestGo.GetComponent<ChestInteractable>();
                chest.ConfigureChest(LockDifficulty.Master, isOpened: false);

                var attrComp = actorGo.GetComponent<AttributeComponent>();
                attrComp.RegisterAttribute(new BaseAttribute
                {
                    AttributeName = "Health",
                    MaximumValue = 100,
                    CurrentValue = 100
                });

                // 1. Enter Playtest
                _studio.PlaytestCoordinator.EnterPlaytest();
                Assert.IsTrue(_studio.PlaytestCoordinator.IsPlaytesting);
                Assert.IsFalse(_studio.IsBuildMode);

                // 2. Mutate world during Playtest: unlock/open chest and deal 100 damage to kill actor
                chest.ConfigureChest(LockDifficulty.Unlocked, isOpened: true);
                attrComp.ApplyDamage("Health", 100);

                Assert.IsFalse(chest.IsLocked);
                Assert.IsTrue(chest.IsOpened);
                Assert.AreEqual(0, attrComp.GetAttribute("Health").CurrentValue);

                // 3. Exit Playtest -> Authored state must be 100% restored
                _studio.PlaytestCoordinator.ExitPlaytest();
                Assert.IsFalse(_studio.PlaytestCoordinator.IsPlaytesting);
                Assert.IsTrue(_studio.IsBuildMode);

                Assert.IsTrue(_studio.TryGetPlacedObject(chestGuid, out GameObject restoredChestGo));
                Assert.IsTrue(_studio.TryGetPlacedObject(actorGuid, out GameObject restoredActorGo));

                var restoredChest = restoredChestGo.GetComponent<ChestInteractable>();
                var restoredAttr = restoredActorGo.GetComponent<AttributeComponent>();

                Assert.IsTrue(restoredChest.IsLocked, "Chest must be restored to locked state after Playtest.");
                Assert.IsFalse(restoredChest.IsOpened, "Chest must be restored to unopened state after Playtest.");
                Assert.IsNotNull(restoredAttr.GetAttribute("Health"));
                Assert.AreEqual(100, restoredAttr.GetAttribute("Health").CurrentValue, "Actor Health must be restored to 100 after Playtest.");

                // Undo stack must still be intact after Playtest
                Assert.AreEqual(2, _studio.CommandHistory.UndoCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chestTemplate);
                UnityEngine.Object.DestroyImmediate(actorTemplate);
            }
        }

        #endregion

        #region EDT-03: CrossObjectLink Wiring & Execution

        private sealed class MockGateLinkReceiver : MonoBehaviour, ILinkReceiver
        {
            public int ActivationCount { get; private set; }
            public string LastAction { get; private set; }

            public void ExecuteLinkAction(string actionName, GameObject sourceObject = null)
            {
                ActivationCount++;
                LastAction = actionName;
            }
        }

        [Test]
        public void EDT03_CrossObjectLink_WiresLeverToTargetInteractableByInstanceGuid()
        {
            var leverTemplate = new GameObject("LeverTemplate");
            leverTemplate.SetActive(false);
            leverTemplate.AddComponent<LeverInteractable>();

            var gateTemplate = new GameObject("GateTemplate");
            gateTemplate.SetActive(false);
            gateTemplate.AddComponent<MockGateLinkReceiver>();

            _studio.RegisterTemplatePrefab("dungeon_lever", leverTemplate);
            _studio.RegisterTemplatePrefab("portcullis_gate", gateTemplate);

            try
            {
                var guidLever = InstanceGuid.NewGuid();
                var guidGate = InstanceGuid.NewGuid();

                var recordGate = new PlacedObjectRecord
                {
                    InstanceId = guidGate,
                    itemId = "portcullis_gate"
                };

                var recordLever = new PlacedObjectRecord
                {
                    InstanceId = guidLever,
                    itemId = "dungeon_lever"
                };
                recordLever.Links.Add(new CrossObjectLink("OnLeverPulled", guidGate, "Activate"));

                GameObject gateGo = _studio.SpawnFromRecord(recordGate);
                GameObject leverGo = _studio.SpawnFromRecord(recordLever);

                var receiver = gateGo.GetComponent<MockGateLinkReceiver>();
                var lever = leverGo.GetComponent<LeverInteractable>();

                Assert.AreEqual(0, receiver.ActivationCount);

                // Pull Lever -> Fires OnLeverPulled -> Resolves guidGate -> Invokes ExecuteLinkAction("Activate")
                lever.PullLever();

                Assert.AreEqual(1, receiver.ActivationCount);
                Assert.AreEqual("Activate", receiver.LastAction);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(leverTemplate);
                UnityEngine.Object.DestroyImmediate(gateTemplate);
            }
        }

        #endregion

        #region EDT-04: Synty Catalog Migration & Zero Cowsins/SoftKitty References

        [Test]
        public void EDT04_DefaultCreatorCatalog_HasZeroMissingPrefabsAndZeroCowsinsReferences()
        {
            CreatorCatalogSO catalog = CreatorStudioSetup.PopulateDefaultCatalog();
            Assert.IsNotNull(catalog, "DefaultCreatorCatalog.asset must exist.");
            Assert.GreaterOrEqual(catalog.Items.Count, 12, "DefaultCreatorCatalog must contain at least 12 items.");

            for (int i = 0; i < catalog.Items.Count; i++)
            {
                CreatorItemDefinition item = catalog.Items[i];
                Assert.IsNotNull(item, $"Catalog item at index {i} is null.");
                Assert.IsNotNull(item.prefab, $"Catalog item '{item.id}' ({item.displayName}) has a null prefab.");

                string path = AssetDatabase.GetAssetPath(item.prefab);
                Assert.IsFalse(string.IsNullOrEmpty(path), $"Catalog item '{item.id}' prefab has an empty asset path.");
                Assert.IsFalse(
                    path.StartsWith("Assets/Cowsins/", StringComparison.OrdinalIgnoreCase),
                    $"Catalog item '{item.id}' still references Cowsins asset: {path}");
                Assert.IsFalse(
                    path.StartsWith("Assets/SoftKitty/", StringComparison.OrdinalIgnoreCase),
                    $"Catalog item '{item.id}' still references SoftKitty asset: {path}");
            }
        }

        #endregion
    }
}
