namespace Tests.Editor
{
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Core.Ids;
    using Data.Config;
    using Domain.Crafting;
    using Domain.Entities;
    using Domain.Skyrim;
    using Gameplay.Combat;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using Gameplay.Skyrim;
    using NUnit.Framework;
    using Systems.Crafting;
    using Systems.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Automated integration tests for the end-to-end core gameplay loop:
    /// Quest intake -> Navigation -> Combat &amp; Death -> Quest Completion &amp; Rewards -> Chest Looting -> Crafting.
    /// </summary>
    [TestFixture]
    public sealed class Domain8_CoreGameplayLoopTests
    {
        #region Private Fields

        private readonly List<GameObject> _spawnedGameObjects = new();

        #endregion

        #region Setup & Teardown

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            GameEventManager.Clear();
            _spawnedGameObjects.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawnedGameObjects.Count; i++)
            {
                if (_spawnedGameObjects[i] != null)
                {
                    Object.DestroyImmediate(_spawnedGameObjects[i]);
                }
            }
            _spawnedGameObjects.Clear();
            ServiceLocator.Clear();
            GameEventManager.Clear();
        }

        #endregion

        #region Tests

        [Test]
        public void LOOP01_BountyBoard_AcceptsQuest_AndPinsCompassMarker()
        {
            var compassService = new CompassService();
            var questService = new RadiantQuestService();

            ServiceLocator.Register(compassService);
            ServiceLocator.Register(questService);
            ServiceLocator.CompleteRegistrationAndInitialize();

            var boardObj = new GameObject("Test_BountyBoard");
            _spawnedGameObjects.Add(boardObj);
            var board = boardObj.AddComponent<BountyBoardInteractable>();

            var playerObj = new GameObject("Player");
            playerObj.tag = "Player";
            _spawnedGameObjects.Add(playerObj);
            var playerActor = playerObj.AddComponent<BaseActor>();

            QuestAcceptedEvent acceptedEvt = null;
            GameEventManager.AddListener<QuestAcceptedEvent>(evt => acceptedEvt = evt);

            board.OnInteract(playerActor);

            Assert.That(questService.ActiveQuests.Count, Is.EqualTo(1), "Quest should be added to active list.");
            Assert.That(acceptedEvt, Is.Not.Null, "QuestAcceptedEvent must be published.");
            Assert.That(acceptedEvt.Quest, Is.EqualTo(questService.ActiveQuests[0]));
            Assert.That(compassService.Markers.Count, Is.GreaterThan(0), "Quest marker should be pinned in CompassService.");
        }

        [Test]
        public void LOOP02_AttributeComponent_Depletion_PublishesEntityDiedEventExactlyOnce()
        {
            var victimObj = new GameObject("Test_Victim");
            _spawnedGameObjects.Add(victimObj);
            var victimActor = victimObj.AddComponent<BaseActor>();
            var victimAttrs = victimObj.AddComponent<AttributeComponent>();

            victimAttrs.RegisterAttribute(new BaseAttribute
            {
                AttributeName = "Health",
                MaximumValue = 100,
                CurrentValue = 100,
                RegenerationRate = 0f
            });

            var killerObj = new GameObject("Player_Killer");
            killerObj.tag = "Player";
            _spawnedGameObjects.Add(killerObj);

            int diedEventCount = 0;
            EntityDiedEvent lastDied = null;
            GameEventManager.AddListener<EntityDiedEvent>(evt =>
            {
                diedEventCount++;
                lastDied = evt;
            });

            victimAttrs.ApplyDamage("Health", 50, killerObj);
            Assert.That(diedEventCount, Is.EqualTo(0), "Should not die at 50% HP.");

            victimAttrs.ApplyDamage("Health", 50, killerObj);
            Assert.That(diedEventCount, Is.EqualTo(1), "Should publish EntityDiedEvent on fatal hit.");
            Assert.That(lastDied.Victim, Is.EqualTo(victimActor));
            Assert.That(lastDied.Killer, Is.EqualTo(killerObj));

            // Overkill hit should not dispatch second death event
            victimAttrs.ApplyDamage("Health", 50, killerObj);
            Assert.That(diedEventCount, Is.EqualTo(1), "Duplicate death events must not be published.");
        }

        [Test]
        public void LOOP03_EnemyKill_AdvancesAndCompletesBountyQuest_AndAwardsRewards()
        {
            var compassService = new CompassService();
            var questService = new RadiantQuestService();
            var skillService = new SkillProgressionService();
            var factionService = new FactionService();

            ServiceLocator.Register(compassService);
            ServiceLocator.Register(questService);
            ServiceLocator.Register(skillService);
            ServiceLocator.Register(factionService);
            ServiceLocator.CompleteRegistrationAndInitialize();

            var quest = questService.GenerateQuest(QuestType.BountyHunt, "Draugr Crypt Sanctum");
            questService.AcceptQuest(quest);

            var playerObj = new GameObject("Player");
            playerObj.tag = "Player";
            _spawnedGameObjects.Add(playerObj);

            var enemyObj = new GameObject("Draugr_Wight_Boss");
            enemyObj.tag = "Enemy";
            _spawnedGameObjects.Add(enemyObj);
            var enemyActor = enemyObj.AddComponent<BaseActor>();
            var faction = enemyObj.AddComponent<FactionComponent>();
            faction.SetFaction(FactionType.Draugr);

            QuestCompletedEvent completedEvt = null;
            GameEventManager.AddListener<QuestCompletedEvent>(evt => completedEvt = evt);

            int initialLevel = skillService.GetSkillLevel(SkillProgressionService.OneHandedSkillId);

            // Simulate killing the enemy
            GameEventManager.Publish(new EntityDiedEvent
            {
                Victim = enemyActor,
                Killer = playerObj
            });

            Assert.That(completedEvt, Is.Not.Null, "Killing the enemy must complete the radiant quest.");
            Assert.That(questService.ActiveQuests.Count, Is.EqualTo(0), "Active quests should be cleared upon completion.");
            Assert.That(compassService.Markers.Count, Is.EqualTo(0), "Compass marker should be unpinned.");
            Assert.That(factionService.GetReputation(FactionService.GuardsFactionId), Is.GreaterThan(0), "Player reputation must improve.");
        }

        [Test]
        public void LOOP04_ChestInteraction_LootTransferredToPlayerInventory()
        {
            var playerObj = new GameObject("Player");
            playerObj.tag = "Player";
            _spawnedGameObjects.Add(playerObj);
            var playerActor = playerObj.AddComponent<BaseActor>();
            var playerInv = playerObj.AddComponent<InventoryComponent>();

            var chestObj = new GameObject("Test_Chest");
            _spawnedGameObjects.Add(chestObj);
            var chest = chestObj.AddComponent<ChestInteractable>();

            // Setup test item
            var testItem = ScriptableObject.CreateInstance<ItemData>();
            testItem.name = "Test_GoldIngot";
            SetPrivateField(testItem, "_displayName", "Gold Ingot");
            SetPrivateField(testItem, "_isStackable", true);
            SetPrivateField(testItem, "_maxStackSize", 99);

            // Mock IContentService to resolve item
            var contentMock = new ContentService();
            contentMock.GetRegistry<ItemData>().Register(testItem, ResourceId.DefaultNamespace, ContentRegistrationPolicy.OverrideExisting, "ITM_GoldIngot");
            ServiceLocator.Register<IContentService>(contentMock);
            ServiceLocator.CompleteRegistrationAndInitialize();

            // Set chest loot
            SetPrivateField(chest, "_lootItemIds", new[] { "ITM_GoldIngot" });
            SetPrivateField(chest, "_lockDifficulty", LockDifficulty.Unlocked);

            ChestOpenedEvent openedEvt = null;
            GameEventManager.AddListener<ChestOpenedEvent>(evt => openedEvt = evt);

            chest.OnInteract(playerActor);

            Assert.That(chest.IsOpened, Is.True, "Chest must be marked opened.");
            Assert.That(openedEvt, Is.Not.Null, "ChestOpenedEvent must be published.");
            Assert.That(playerInv.HasItem(testItem, 1), Is.True, "Player inventory should receive the looted item.");
        }

        [Test]
        public void LOOP05_CraftingStation_ForgesRecipeFromPlayerMaterials()
        {
            var craftingService = new CraftingService();
            ServiceLocator.Register(craftingService);
            ServiceLocator.CompleteRegistrationAndInitialize();

            var playerObj = new GameObject("Player");
            playerObj.tag = "Player";
            _spawnedGameObjects.Add(playerObj);
            var playerActor = playerObj.AddComponent<BaseActor>();
            var playerInv = playerObj.AddComponent<InventoryComponent>();

            // Create Wood Log item
            var woodLog = ScriptableObject.CreateInstance<ItemData>();
            woodLog.name = "Item_WoodLog";
            SetPrivateField(woodLog, "_displayName", "Wood Log");
            SetPrivateField(woodLog, "_isStackable", true);
            SetPrivateField(woodLog, "_maxStackSize", 99);

            // Create Wood Plank item
            var woodPlank = ScriptableObject.CreateInstance<ItemData>();
            woodPlank.name = "Item_WoodPlank";
            SetPrivateField(woodPlank, "_displayName", "Wood Plank");
            SetPrivateField(woodPlank, "_isStackable", true);
            SetPrivateField(woodPlank, "_maxStackSize", 99);

            // Create Recipe (1 Wood Log -> 4 Wood Planks)
            var recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            SetPrivateField(recipe, "_recipeId", "Recipe_WoodPlanks");
            SetPrivateField(recipe, "_displayName", "Craft Wood Planks");
            SetPrivateField(recipe, "_resultItem", woodPlank);
            SetPrivateField(recipe, "_resultCount", 4);

            var ingredients = new List<CraftingIngredient>
            {
                new CraftingIngredient { Item = woodLog, Count = 1 }
            };
            SetPrivateField(recipe, "_ingredients", ingredients);

            // Give player 1 Wood Log
            playerInv.AddItem(woodLog, 1);
            Assert.That(playerInv.HasItem(woodLog, 1), Is.True);

            // Create Anvil Station with recipe
            var anvilObj = new GameObject("Blacksmith_Anvil");
            _spawnedGameObjects.Add(anvilObj);
            var station = anvilObj.AddComponent<CraftingStationInteractable>();
            var recipeList = new List<CraftingRecipe> { recipe };
            SetPrivateField(station, "_stationRecipes", recipeList);

            ItemCraftedEvent craftedEvt = null;
            GameEventManager.AddListener<ItemCraftedEvent>(evt => craftedEvt = evt);

            station.OnInteract(playerActor);

            Assert.That(craftedEvt, Is.Not.Null, "ItemCraftedEvent must be published on success.");
            Assert.That(playerInv.HasItem(woodLog, 1), Is.False, "Input ingredient should be consumed.");
            Assert.That(playerInv.HasItem(woodPlank, 4), Is.True, "Output items must be deposited in inventory.");
        }

        #endregion

        #region Helper Methods

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        #endregion
    }
}
