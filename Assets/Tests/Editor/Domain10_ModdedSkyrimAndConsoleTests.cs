namespace Tests.Editor
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Data.Config;
    using Domain.Combat;
    using Domain.Modding;
    using Domain.Skyrim;
    using Gameplay.Entities;
    using Gameplay.Skyrim;
    using NUnit.Framework;
    using Systems.Console;
    using Systems.Skyrim;
    using UnityEngine;

    [TestFixture]
    public class Domain10_ModdedSkyrimAndConsoleTests
    {
        private GameObject _player;
        private BaseActor _playerActor;
        private AttributeComponent _attributes;
        private InventoryComponent _inventory;
        private CharacterController _characterController;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            GameEventManager.Clear();
            GodModeState.IsGodModeActive = false;
            NoClipState.IsNoClipActive = false;

            _player = new GameObject("Player");
            _player.tag = "Player";
            _playerActor = _player.AddComponent<BaseActor>();
            _attributes = _player.AddComponent<AttributeComponent>();
            _inventory = _player.AddComponent<InventoryComponent>();
            _characterController = _player.AddComponent<CharacterController>();
            SKSE.SetPlayerOverride(_player);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) UnityEngine.Object.DestroyImmediate(_player);
            ServiceLocator.Clear();
            GameEventManager.Clear();
            SKSE.Clear();
            PapyrusUtil.Clear();
            GodModeState.IsGodModeActive = false;
            NoClipState.IsNoClipActive = false;
        }

        #region Fortify Restoration Loop & Exploits

        [Test]
        public void REST01_FortifyRestorationLoop_CompoundingPotions_ExponentiallyIncreasesMagnitude()
        {
            var loopMgr = new FortifyRestorationLoopManager();
            loopMgr.EquippedApparelAlchemyBase = 0.20f; // 20% base gear

            // 1. Initial effective alchemy bonus is base 20%
            Assert.AreEqual(0.20f, loopMgr.CurrentEffectiveAlchemyBonus, 0.001f);

            // 2. Drink first Fortify Restoration potion (+50% magnitude)
            var pot1 = new ActiveEffect(ActiveEffectType.FortifyRestoration, "Potion of Fortify Restoration", 0.50f, 60f);
            loopMgr.ApplyEffect(pot1);

            // In vanilla Skyrim, unequipping and re-equipping recalculates gear with restoration multiplier (1 + 0.5 = 1.5)
            loopMgr.UnequipAndReequipApparel();
            Assert.AreEqual(0.30f, loopMgr.CurrentEffectiveAlchemyBonus, 0.001f); // 0.20 * 1.5 = 0.30

            // 3. Brew second potion with boosted alchemy gear
            var pot2 = loopMgr.BrewPotion(ActiveEffectType.FortifyRestoration, 0.50f);
            Assert.AreEqual(0.65f, pot2.Magnitude, 0.001f); // 0.50 * (1 + 0.30) = 0.65

            // 4. Drink second potion (+65%) and re-equip
            loopMgr.ApplyEffect(pot2);
            loopMgr.UnequipAndReequipApparel();

            // Next effective gear bonus is 0.20 * (1 + 0.65) = 0.33
            Assert.AreEqual(0.33f, loopMgr.CurrentEffectiveAlchemyBonus, 0.001f);
        }

        [Test]
        public void REST02_FortifyRestorationLoop_WhenUSSEPPatchActive_DoesNotAmplifyApparelEnchantments()
        {
            var loopMgr = new FortifyRestorationLoopManager();
            loopMgr.EquippedApparelAlchemyBase = 0.20f;
            loopMgr.IsUSSEPPatchActive = true; // Unofficial patch fix enabled

            var pot = new ActiveEffect(ActiveEffectType.FortifyRestoration, "Potion of Fortify Restoration", 1.0f, 60f);
            loopMgr.ApplyEffect(pot);
            loopMgr.UnequipAndReequipApparel();

            // Under USSEP, gear remains strictly at base magnitude
            Assert.AreEqual(0.20f, loopMgr.CurrentEffectiveAlchemyBonus, 0.001f);
        }

        #endregion

        #region Alternate Start — Live Another Life

        [Test]
        public void ALTS01_AlternateStart_SelectScenarioAndCommitLife_TeleportsAndGrantsStarterGear()
        {
            var altService = new AlternateStartService();
            altService.Initialize();
            ServiceLocator.Register(altService);

            Assert.IsFalse(altService.HasStartedNewLife);
            bool selected = altService.SelectScenario("patron_inn");
            Assert.IsTrue(selected);
            Assert.AreEqual("patron_inn", altService.SelectedScenario.ScenarioId);

            bool committed = altService.CommitLifeChoice(_player);
            Assert.IsTrue(committed);
            Assert.IsTrue(altService.HasStartedNewLife);
            Assert.AreEqual(new Vector3(2f, 0.5f, 5f), _player.transform.position);
        }

        #endregion

        #region SKSE & PapyrusUtil

        [Test]
        public void SKSE01_SKSE_CustomModEvent_DispatchesToRegisteredListeners()
        {
            string receivedStr = null;
            float receivedNum = 0f;

            SKSE.RegisterForModEvent("OnLevelThresholdReached", (s, n) =>
            {
                receivedStr = s;
                receivedNum = n;
            });

            SKSE.SendModEvent("OnLevelThresholdReached", "Dragonborn", 100f);

            Assert.AreEqual("Dragonborn", receivedStr);
            Assert.AreEqual(100f, receivedNum);
        }

        [Test]
        public void PAPY01_PapyrusUtil_JsonStorageAndLists_PersistsDataCorrectly()
        {
            // Test JSON storage
            var data = new List<string> { "Whiterun", "Riften", "Solitude" };
            bool saved = PapyrusUtil.JsonSave("HoldCapitals", data);
            Assert.IsTrue(saved);

            var loaded = PapyrusUtil.JsonLoad<List<string>>("HoldCapitals");
            Assert.IsNotNull(loaded);
            Assert.AreEqual(3, loaded.Count);
            Assert.AreEqual("Whiterun", loaded[0]);

            // Test StringList
            PapyrusUtil.StringListAdd("Glitches", "SpaceProgram");
            PapyrusUtil.StringListAdd("Glitches", "BucketHead");
            Assert.AreEqual(2, PapyrusUtil.StringListCount("Glitches"));
            Assert.AreEqual("SpaceProgram", PapyrusUtil.StringListGet("Glitches", 0));

            // Test ActorVariables
            PapyrusUtil.SetActorVariable("PlayerRef", "StolenSweetrolls", 5);
            Assert.AreEqual(5, PapyrusUtil.GetActorVariable<int>("PlayerRef", "StolenSweetrolls"));
        }

        #endregion

        #region TES Mod File Format & Conflict Resolution

        [Test]
        public void TES01_TESPlugin_ConflictResolver_LaterPluginOverridesEarlierRecords()
        {
            var pluginMaster = new TESPlugin("Skyrim.esm");
            pluginMaster.AddRecord("WEAP", 0x00012EB7, "WeapIronSword", "Iron Sword");

            var pluginMod = new TESPlugin("CombatOverhaul.esp");
            pluginMod.AddRecord("WEAP", 0x00012EB7, "WeapIronSword", "Reforged Iron Sword");

            var loadOrder = new List<TESPlugin> { pluginMaster, pluginMod };
            var winningRecords = TESConflictResolver.ResolveLoadOrder(loadOrder);

            Assert.IsTrue(winningRecords.ContainsKey(0x00012EB7));
            Assert.AreEqual("CombatOverhaul.esp", winningRecords[0x00012EB7].WinningPlugin);
            Assert.AreEqual("Reforged Iron Sword", winningRecords[0x00012EB7].Record.FullName);
        }

        #endregion

        #region Command Console

        [Test]
        public void CNS01_ConsoleRegistry_ExecuteLine_TGM_TogglesGodMode()
        {
            var registry = new ConsoleCommandRegistry();
            registry.RegisterCommand(new ToggleGodModeCommand());

            Assert.IsFalse(GodModeState.IsGodModeActive);
            registry.ExecuteLine("tgm", null);
            Assert.IsTrue(GodModeState.IsGodModeActive);
            registry.ExecuteLine("tgm", null);
            Assert.IsFalse(GodModeState.IsGodModeActive);
        }

        [Test]
        public void CNS02_ConsoleRegistry_ExecuteLine_TCL_TogglesCollision()
        {
            var registry = new ConsoleCommandRegistry();
            registry.RegisterCommand(new ToggleCollisionCommand());

            Assert.IsTrue(_characterController.enabled);
            registry.ExecuteLine("tcl", null);
            Assert.IsFalse(_characterController.enabled);
            Assert.IsTrue(NoClipState.IsNoClipActive);

            registry.ExecuteLine("tcl", null);
            Assert.IsTrue(_characterController.enabled);
            Assert.IsFalse(NoClipState.IsNoClipActive);
        }

        [Test]
        public void CNS03_ConsoleRegistry_ExecuteLine_PlayerAddItem_AddsToInventory()
        {
            var registry = new ConsoleCommandRegistry();
            registry.RegisterCommand(new AddItemCommand());

            registry.ExecuteLine("player.additem iron_sword 3", null);

            Assert.AreEqual(1, _inventory.Slots.Count);
            Assert.AreEqual(3, _inventory.Slots[0].Count);
        }

        [Test]
        public void CNS04_ConsoleRegistry_ExecuteLine_COC_TeleportsPlayerToCell()
        {
            var registry = new ConsoleCommandRegistry();
            registry.RegisterCommand(new CenterOnCellCommand());

            registry.ExecuteLine("coc crypt", null);
            Assert.AreEqual(new Vector3(0f, 0.5f, 22f), _player.transform.position);

            registry.ExecuteLine("coc qasmoke", null);
            Assert.AreEqual(new Vector3(-15f, 0.5f, -15f), _player.transform.position);
        }

        [Test]
        public void CNS05_ConsoleRegistry_CustomUserCommand_RegistersAndExecutes()
        {
            var registry = new ConsoleCommandRegistry();
            bool customExecuted = false;

            var customCmd = new CustomTestCommand(() => customExecuted = true);
            registry.RegisterCommand(customCmd);

            Assert.IsTrue(registry.HasCommand("custom_test"));
            registry.ExecuteLine("custom_test param1", null);
            Assert.IsTrue(customExecuted);
        }

        private class CustomTestCommand : IConsoleCommand
        {
            private readonly Action _action;
            public string Name => "custom_test";
            public string Description => "Custom user command test.";
            public string Usage => "custom_test";

            public CustomTestCommand(Action action) => _action = action;

            public bool Execute(string[] args, Action<string> print)
            {
                _action?.Invoke();
                return true;
            }
        }

        #endregion
    }
}
