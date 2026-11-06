namespace Tests.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Core;
    using Core.Assets;
    using Core.Content;
    using Core.Diagnostics;
    using Core.Ids;
    using Data.Config;
    using Domain.Crafting;
    using Domain.Grid;
    using Domain.Skyrim;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Systems.Interaction;
    using Systems.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Milestone 1 &amp; Milestone 3 verification suite covering Core Identity Primitives (<see cref="ResourceId"/>, <see cref="InstanceGuid"/>,
    /// <see cref="DefinitionRef{T}"/>), <see cref="ContentRegistry{T}"/>, <see cref="LegacyDatabaseBridge"/>,
    /// <see cref="BlockDefinition"/> validation inheritance, <see cref="HotbarService"/> typed block resolution,
    /// data-driven skills/factions/quests, and <see cref="IAssetProvider"/> fallback resolution.
    /// </summary>
    [TestFixture]
    public class Domain3_ContentRegistryTests
    {
        private ContentService _contentService;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _contentService = new ContentService();
            ServiceLocator.Register<IContentService>(_contentService);
            ServiceLocator.Register(_contentService);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        #region CNT-01: ResourceId & InstanceGuid Validation & Hashing

        [Test]
        public void CNT01_ResourceId_Validation_AcceptsValidAndRejectsMalformed()
        {
            // Valid identifiers
            ResourceId swordId = ResourceId.Parse("nordic_rpg:item/iron_sword");
            Assert.IsFalse(swordId.IsEmpty);
            Assert.AreEqual("nordic_rpg", swordId.Namespace);
            Assert.AreEqual("item/iron_sword", swordId.Path);
            Assert.AreEqual("nordic_rpg:item/iron_sword", swordId.Value);
            Assert.AreNotEqual(0u, swordId.Hash);

            ResourceId blockId = new ResourceId("core", "block/stone_01.variant-a");
            Assert.AreEqual("core:block/stone_01.variant-a", blockId.Value);
            Assert.AreEqual(swordId, ResourceId.Parse("nordic_rpg:item/iron_sword"));
            Assert.AreEqual(swordId.GetHashCode(), ResourceId.Parse("nordic_rpg:item/iron_sword").GetHashCode());

            // Invalid identifiers
            Assert.IsFalse(ResourceId.TryParse(null, out _));
            Assert.IsFalse(ResourceId.TryParse("", out _));
            Assert.IsFalse(ResourceId.TryParse("   ", out _));
            Assert.IsFalse(ResourceId.TryParse("IronSword", out _), "Missing colon must be rejected.");
            Assert.IsFalse(ResourceId.TryParse("UPPER:case", out _), "Uppercase namespace must be rejected.");
            Assert.IsFalse(ResourceId.TryParse("nordic_rpg:Item/Sword", out _), "Uppercase path must be rejected.");
            Assert.IsFalse(ResourceId.TryParse(":path_only", out _), "Empty namespace must be rejected.");
            Assert.IsFalse(ResourceId.TryParse("ns_only:", out _), "Empty path must be rejected.");
            Assert.IsFalse(ResourceId.TryParse("a:b:c", out _), "Multiple colons must be rejected.");

            Assert.Throws<FormatException>(() => ResourceId.Parse("InvalidNoColon"));
        }

        [Test]
        public void CNT01_InstanceGuid_RoundTripsAndDistinguishesInstances()
        {
            InstanceGuid guidA = InstanceGuid.NewGuid();
            InstanceGuid guidB = InstanceGuid.NewGuid();

            Assert.IsFalse(guidA.IsEmpty);
            Assert.AreNotEqual(guidA, guidB);
            Assert.AreEqual(32, guidA.Value.Length);

            InstanceGuid parsed = InstanceGuid.Parse(guidA.Value);
            Assert.AreEqual(guidA, parsed);
            Assert.IsFalse(InstanceGuid.TryParse("not-a-guid", out _));
        }

        [Test]
        public void CNT01_NewtonsoftConverters_SerializeResourceIdAndInstanceGuidCleanly()
        {
            var settings = FantasyEngineJsonSettings.CreateDefault(indented: false);
            var payload = new TestSerializedRecord
            {
                DefinitionId = ResourceId.Parse("nordic_rpg:item/daedric_bow"),
                InstanceId = InstanceGuid.NewGuid(),
                Position = new Vector3(12.5f, 3.0f, -4.25f)
            };

            string json = JsonConvert.SerializeObject(payload, settings);
            Assert.IsTrue(json.Contains("\"nordic_rpg:item/daedric_bow\""));

            var restored = JsonConvert.DeserializeObject<TestSerializedRecord>(json, settings);
            Assert.AreEqual(payload.DefinitionId, restored.DefinitionId);
            Assert.AreEqual(payload.InstanceId, restored.InstanceId);
            Assert.AreEqual(payload.Position, restored.Position);
        }

        private sealed class TestSerializedRecord
        {
            public ResourceId DefinitionId;
            public InstanceGuid InstanceId;
            public Vector3 Position;
        }

        #endregion

        #region CNT-02: ContentRegistry Freeze & Conflict Policies

        [Test]
        public void CNT02_ContentRegistry_Freeze_PreventsLateMutationUntilUnfrozen()
        {
            var registry = _contentService.GetRegistry<ItemData>();

            var ironSword = ScriptableObject.CreateInstance<ItemData>();
            ironSword.Id = "ITM_0100";
            ironSword.DisplayName = "Iron Sword";
            ironSword.ContentId = ResourceId.Parse("nordic_rpg:item/iron_sword");

            var steelSword = ScriptableObject.CreateInstance<ItemData>();
            steelSword.Id = "ITM_0101";
            steelSword.DisplayName = "Steel Sword";
            steelSword.ContentId = ResourceId.Parse("nordic_rpg:item/steel_sword");

            try
            {
                Assert.IsTrue(registry.Register(ironSword));
                Assert.AreEqual(1, registry.Count);

                _contentService.FreezeAll();
                Assert.IsTrue(registry.IsFrozen);

                Assert.Throws<RegistryFrozenException>(() => registry.Register(steelSword));

                _contentService.UnfreezeAll();
                Assert.IsFalse(registry.IsFrozen);
                Assert.IsTrue(registry.Register(steelSword));
                Assert.AreEqual(2, registry.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ironSword);
                UnityEngine.Object.DestroyImmediate(steelSword);
            }
        }

        [Test]
        public void CNT02_ContentRegistry_AddOnlyVsOverrideExisting_TracksProvenance()
        {
            var registry = _contentService.GetRegistry<ItemData>();
            ResourceId sharedId = ResourceId.Parse("nordic_rpg:item/iron_sword");

            var baseSword = ScriptableObject.CreateInstance<ItemData>();
            baseSword.ContentId = sharedId;
            baseSword.DisplayName = "Base Iron Sword";
            baseSword.Value = 45;

            var modSwordAddOnly = ScriptableObject.CreateInstance<ItemData>();
            modSwordAddOnly.ContentId = sharedId;
            modSwordAddOnly.DisplayName = "Mod AddOnly Sword";
            modSwordAddOnly.Value = 999;

            var modSwordOverride = ScriptableObject.CreateInstance<ItemData>();
            modSwordOverride.ContentId = sharedId;
            modSwordOverride.DisplayName = "Balance Patch Iron Sword";
            modSwordOverride.Value = 120;

            try
            {
                Assert.IsTrue(registry.Register(baseSword, "nordic_rpg", ContentRegistrationPolicy.AddOnly));
                Assert.IsFalse(registry.Register(modSwordAddOnly, "bad_mod", ContentRegistrationPolicy.AddOnly));
                Assert.AreEqual(45, registry.GetOrNull(sharedId).Value);

                Assert.IsTrue(registry.Register(modSwordOverride, "balance_patch_mod", ContentRegistrationPolicy.OverrideExisting));
                Assert.AreEqual(120, registry.GetOrNull(sharedId).Value);

                Assert.IsTrue(registry.TryGetEntry(sharedId, out var entry));
                Assert.AreEqual("nordic_rpg", entry.SourceModId);
                Assert.AreEqual("balance_patch_mod", entry.LastModifiedByModId);
                Assert.AreEqual(2, entry.ModificationCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseSword);
                UnityEngine.Object.DestroyImmediate(modSwordAddOnly);
                UnityEngine.Object.DestroyImmediate(modSwordOverride);
            }
        }

        #endregion

        #region CNT-03: LegacyDatabaseBridge & DefinitionRef<T>

        [Test]
        public void CNT03_LegacyDatabaseBridge_ImportsExistingScriptableObjectsWithAliases()
        {
            var elvenBow = ScriptableObject.CreateInstance<ItemData>();
            elvenBow.name = "Item_ElvenBow";
            elvenBow.Id = "ITM_0042";
            elvenBow.DisplayName = "Elven Bow";

            var stoneBlock = ScriptableObject.CreateInstance<BlockDefinition>();
            stoneBlock.name = "BLCK_0010";
            stoneBlock.Id = "BLCK_0010";
            stoneBlock.DisplayName = "Crypt Cobblestone";
            stoneBlock.SynchronizeRuntimeId();

            try
            {
                LegacyDatabaseBridge.ImportEntity(_contentService, elvenBow);
                LegacyDatabaseBridge.ImportEntity(_contentService, stoneBlock);

                var itemRegistry = _contentService.GetRegistry<ItemData>();
                ResourceId expectedItemId = ResourceId.Parse("nordic_rpg:item/itm_0042");

                Assert.IsTrue(itemRegistry.TryGet(expectedItemId, out ItemData resolvedByCanonical));
                Assert.AreSame(elvenBow, resolvedByCanonical);

                Assert.IsTrue(itemRegistry.TryGetByLegacyAlias("ITM_0042", out ItemData resolvedByLegacyId));
                Assert.AreSame(elvenBow, resolvedByLegacyId);

                Assert.IsTrue(itemRegistry.TryGetByLegacyAlias("Item_ElvenBow", out ItemData resolvedByAssetName));
                Assert.AreSame(elvenBow, resolvedByAssetName);

                // Secondary index lookup for BlockDefinition by ushort RuntimeId (10)
                var blockRegistry = _contentService.GetRegistry<BlockDefinition>();
                Assert.AreEqual((ushort)10, stoneBlock.RuntimeId);
                Assert.IsTrue(blockRegistry.TryGetByIndex(LegacyDatabaseBridge.BlockRuntimeIdIndex, (ushort)10, out BlockDefinition resolvedBlock));
                Assert.AreSame(stoneBlock, resolvedBlock);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(elvenBow);
                UnityEngine.Object.DestroyImmediate(stoneBlock);
            }
        }

        [Test]
        public void CNT03_HotbarService_ResolvesBlockViaTypedDefinitionRefBeforeDisplayName()
        {
            var hotbarService = new HotbarService();
            hotbarService.Initialize();

            var stoneBlock = ScriptableObject.CreateInstance<BlockDefinition>();
            stoneBlock.Id = "BLCK_0010";
            stoneBlock.DisplayName = "Internal_Stone_Block_Name";
            stoneBlock.ContentId = ResourceId.Parse("nordic_rpg:block/crypt_stone");

            var placeableItem = ScriptableObject.CreateInstance<ItemData>();
            placeableItem.Id = "ITM_0099";
            placeableItem.DisplayName = "Localized Stone Brick Item (Different Name)";
            placeableItem.PlaceableBlock = new DefinitionRef<BlockDefinition>(stoneBlock.ContentId);

            try
            {
                LegacyDatabaseBridge.ImportEntity(_contentService, stoneBlock);

                // Even though DisplayName differs completely, HotbarService resolves the block via DefinitionRef<BlockDefinition>!
                BlockDefinition resolved = hotbarService.ResolveBlock(placeableItem);
                Assert.IsNotNull(resolved);
                Assert.AreSame(stoneBlock, resolved);
            }
            finally
            {
                hotbarService.Shutdown();
                UnityEngine.Object.DestroyImmediate(stoneBlock);
                UnityEngine.Object.DestroyImmediate(placeableItem);
            }
        }

        #endregion

        #region CNT-05: BlockDefinition OnValidate Inheritance Verification

        [Test]
        public void CNT05_BlockDefinition_OnValidate_InvokesBaseEntityDataValidationAndSyncsRuntimeId()
        {
            MethodInfo blockValidate = typeof(BlockDefinition).GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            MethodInfo baseValidate = typeof(EntityData).GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            Assert.IsNotNull(blockValidate, "BlockDefinition must have OnValidate.");
            Assert.IsNotNull(baseValidate, "EntityData must have protected virtual OnValidate.");
            Assert.IsTrue(baseValidate.IsVirtual, "EntityData.OnValidate must be virtual so subclasses do not shadow it.");

            var block = ScriptableObject.CreateInstance<BlockDefinition>();
            try
            {
                block.Id = "BLCK_0042";
                blockValidate.Invoke(block, null);
                Assert.AreEqual((ushort)42, block.RuntimeId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(block);
            }
        }

        #endregion

        #region Milestone 3: Data-Driven Skills, Factions, Radiant Quests & IAssetProvider Fallback

        [Test]
        public void CNT04_CustomNonEnumSkill_GainsXPAndTriggersPlayerLevelUp()
        {
            var skillId = new ResourceId("chronomancy_mod", "skill/time_weaving");
            var customSkillDef = new SkillDefinition(
                skillId,
                "Time Weaving",
                category: "Magic",
                startingLevel: 15,
                baseXp: 50f,
                xpPerLevel: 12f);

            _contentService.GetRegistry<SkillDefinition>().Register(customSkillDef, "chronomancy_mod");

            var skillService = new SkillProgressionService();
            skillService.Initialize();

            int skillLevelUpEvents = 0;
            int playerLevelUpEvents = 0;
            int latestPerkPoints = 0;

            void OnSkillUp(SkillLeveledUpEvent evt)
            {
                if (evt.SkillId == skillId) skillLevelUpEvents++;
            }

            void OnPlayerUp(PlayerCharacterLeveledUpEvent evt)
            {
                playerLevelUpEvents++;
                latestPerkPoints = evt.PerkPoints;
            }

            GameEventManager.AddListener<SkillLeveledUpEvent>(OnSkillUp);
            GameEventManager.AddListener<PlayerCharacterLeveledUpEvent>(OnPlayerUp);

            try
            {
                Assert.AreEqual(15, skillService.GetSkillLevel(skillId));
                Assert.AreEqual(1, skillService.CharacterLevel);

                // Award 5000 XP — enough to gain > 10 skill levels (levels 15->25 require ~2,840 XP)
                skillService.AddSkillXP(skillId, 5000f);

                int newSkillLevel = skillService.GetSkillLevel(skillId);
                Assert.GreaterOrEqual(newSkillLevel, 25, "Custom mod skill should advance at least 10 levels on 5000 XP.");
                Assert.AreEqual(newSkillLevel - 15, skillLevelUpEvents, "SkillLeveledUpEvent should fire once per level gained.");
                Assert.GreaterOrEqual(playerLevelUpEvents, 1, "10+ skill-ups on a custom skill must trigger PlayerCharacterLeveledUpEvent.");
                Assert.GreaterOrEqual(latestPerkPoints, 1, "Player must earn at least 1 perk point.");
            }
            finally
            {
                GameEventManager.RemoveListener<SkillLeveledUpEvent>(OnSkillUp);
                GameEventManager.RemoveListener<PlayerCharacterLeveledUpEvent>(OnPlayerUp);
                skillService.Shutdown();
            }
        }

        [Test]
        public void CNT06_CustomFactionDefinition_SupportsHostilityAndReputationThresholds()
        {
            var cultId = new ResourceId("crimson_mod", "faction/crimson_cult");
            var orderId = new ResourceId("crimson_mod", "faction/silver_order");

            var cultDef = new FactionDefinition(
                cultId,
                "Crimson Cult",
                defaultDispositions: new Dictionary<string, FactionDisposition>
                {
                    { orderId.Value, FactionDisposition.Hostile },
                    { FactionService.PlayerFactionId.Value, FactionDisposition.Neutral }
                });

            _contentService.GetRegistry<FactionDefinition>().Register(cultDef, "crimson_mod");

            var factionService = new FactionService();
            factionService.Initialize();

            try
            {
                Assert.IsTrue(factionService.IsHostile(cultId, orderId));
                Assert.IsFalse(factionService.IsHostile(cultId, FactionService.PlayerFactionId));

                // Dropping player reputation to -60 flips faction disposition to Hostile
                factionService.ModifyReputation(cultId, -60);
                Assert.AreEqual(-60, factionService.GetReputation(cultId));
                Assert.IsTrue(factionService.IsHostile(cultId, FactionService.PlayerFactionId));
            }
            finally
            {
                factionService.Shutdown();
            }
        }

        [Test]
        public void CNT07_DataDrivenRadiantQuest_GeneratesFromCustomTemplateInRegistry()
        {
            var templateId = new ResourceId("dragon_hunt_mod", "quest_template/slay_wyrm");
            var customTemplate = new QuestTemplateDefinition(
                templateId,
                "Wyrm Hunt",
                QuestType.BountyHunt,
                titleFormat: "Wyrm Contract: Purge {0}",
                objectiveFormat: "Slay the Elder Wyrm roosting at {0}",
                defaultRequiredCount: 2,
                baseGoldReward: 750,
                goldPerPlayerLevel: 50,
                xpReward: 1200);

            _contentService.GetRegistry<QuestTemplateDefinition>().Register(customTemplate, "dragon_hunt_mod");

            var questService = new RadiantQuestService();
            questService.Initialize();

            try
            {
                QuestInstance quest = questService.GenerateQuest(templateId, "Frostpeak Spire");
                Assert.IsNotNull(quest);
                Assert.AreEqual(templateId, quest.TemplateId);
                Assert.AreEqual("Wyrm Contract: Purge Frostpeak Spire", quest.Title);
                Assert.AreEqual(1, quest.Objectives.Count);
                Assert.AreEqual("Slay the Elder Wyrm roosting at Frostpeak Spire", quest.Objectives[0].Description);
                Assert.AreEqual(2, quest.Objectives[0].RequiredCount);
                Assert.AreEqual(750, quest.BaseGoldReward);
            }
            finally
            {
                questService.Shutdown();
            }
        }

        [Test]
        public void MDP04_MissingAssetInMod_ReturnsFallbackAssetInsteadOfNull()
        {
            var diag = new DiagnosticService();
            ServiceLocator.Register<IDiagnosticService, DiagnosticService>(diag);

            var assetProvider = new AssetProviderService();
            assetProvider.Initialize();

            try
            {
                var missingMeshId = new ResourceId("broken_mod", "mesh/non_existent");
                GameObject fallbackGo = assetProvider.LoadAssetAsync<GameObject>(missingMeshId).GetAwaiter().GetResult();

                Assert.IsNotNull(fallbackGo, "Missing prefab/mesh must return FallbackAssetCatalog placeholder instead of null.");
                Assert.AreEqual("MissingMeshPlaceholder", fallbackGo.name);
                Assert.AreEqual(1, diag.WarningCount, "Missing asset lookup must record 1 structured diagnostic warning.");

                Sprite fallbackSprite = assetProvider.LoadAsset<Sprite>(new ResourceId("broken_mod", "icon/missing_icon"));
                Assert.IsNotNull(fallbackSprite, "Missing sprite must return non-null fallback Sprite.");
            }
            finally
            {
                assetProvider.Shutdown();
            }
        }

        #endregion
    }
}
