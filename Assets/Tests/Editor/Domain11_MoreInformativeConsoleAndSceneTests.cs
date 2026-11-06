namespace Tests.Editor
{
    using System.Collections.Generic;
    using Domain.Console;
    using Domain.Skyrim;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using Gameplay.Skyrim;
    using NUnit.Framework;
    using Systems.Console;
    using Systems.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Domain 11 Test Suite: Validates ConsoleUtilSSE (Mod 24858) execution bridge,
    /// More Informative Console (Mod 19250) reflection inspector metadata extraction,
    /// and targeted console manipulation commands (kill, resurrect, disable, enable, setscale, unlock, tai, moveto).
    /// </summary>
    [TestFixture]
    public class Domain11_MoreInformativeConsoleAndSceneTests
    {
        private GameObject _player;
        private GameObject _target;
        private ConsoleCommandRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new ConsoleCommandRegistry();
            _registry.RegisterDefaults();

            _player = new GameObject("TestPlayer");
            _player.tag = "Player";
            _player.transform.position = new Vector3(0f, 0f, 0f);
            SKSE.SetPlayerOverride(_player);

            _target = new GameObject("TestTargetActor");
            _target.transform.position = new Vector3(10f, 0f, 5f);
        }

        [TearDown]
        public void TearDown()
        {
            ConsoleUtil.ClearSelectedReference();
            SKSE.SetPlayerOverride(null);

            if (_target != null) Object.DestroyImmediate(_target);
            if (_player != null) Object.DestroyImmediate(_player);
        }

        [Test]
        public void MIC01_ConsoleUtil_ExecuteCommand_RunsRegisteredCommand_AndPrintsMessage()
        {
            bool executed = false;
            string loggedMessage = null;

            _registry.Register("custom_ping", (args) => {
                executed = true;
                return "pong";
            }, "Custom ping command");

            void OnLog(string msg) => loggedMessage = msg;
            ConsoleUtil.OnLogMessage += OnLog;

            bool success = ConsoleUtil.ExecuteCommand("custom_ping", _registry);
            ConsoleUtil.OnLogMessage -= OnLog;

            Assert.IsTrue(success);
            Assert.IsTrue(executed);
            Assert.AreEqual("pong", loggedMessage);
        }

        [Test]
        public void MIC02_ConsoleUtil_SelectedReference_TracksAndUpdatesTarget()
        {
            GameObject receivedSelection = null;
            void OnChanged(GameObject sel) => receivedSelection = sel;
            ConsoleUtil.OnSelectionChanged += OnChanged;

            ConsoleUtil.SetSelectedReference(_target);
            Assert.AreEqual(_target, ConsoleUtil.SelectedReference);
            Assert.AreEqual(_target, receivedSelection);

            ConsoleUtil.ClearSelectedReference();
            Assert.IsNull(ConsoleUtil.SelectedReference);
            Assert.IsNull(receivedSelection);

            ConsoleUtil.OnSelectionChanged -= OnChanged;
        }

        [Test]
        public void MIC03_InspectedObjectData_ExtractsFullMetadata_ActorAndTransform()
        {
            var actor = _target.AddComponent<BaseActor>();
            var attrs = _target.AddComponent<AttributeComponent>();
            var faction = _target.AddComponent<FactionComponent>();
            faction.SetFaction(FactionType.Draugr);

            var data = InspectedObjectData.FromGameObject(_target, _player);

            Assert.IsNotNull(data);
            Assert.IsTrue(data.FormIdHex.StartsWith("0x"));
            Assert.AreEqual(10, data.FormIdHex.Length); // 0x + 8 hex digits
            Assert.AreEqual("Skyrim.esm", data.DefiningMod);
            Assert.AreEqual("TestTargetActor", data.EditorId);
            Assert.AreEqual(new Vector3(10f, 0f, 5f), data.Position);
            Assert.IsTrue(data.ActorStats.IsActor);
            Assert.AreEqual(FactionType.Draugr.ToString(), data.ActorStats.FactionName);
            Assert.AreEqual((int)attrs.CurrentHealth, data.ActorStats.Health);
            Assert.Contains(nameof(AttributeComponent), data.ComponentNames);
            Assert.Contains(nameof(FactionComponent), data.ComponentNames);
        }

        [Test]
        public void MIC04_InspectedObjectData_ExtractsInteractableAndInventory()
        {
            var chest = _target.AddComponent<ChestInteractable>();
            chest.SetLocked(LockDifficulty.Adept);

            var data = InspectedObjectData.FromGameObject(_target, _player);

            Assert.IsNotNull(data);
            Assert.IsTrue(data.IsInteractable);
            Assert.AreEqual("Chest", data.InteractableType);
            Assert.AreEqual(LockDifficulty.Adept.ToString(), data.LockDifficulty);
            Assert.AreEqual("LOCKED (Adept)", data.LockState);
        }

        [Test]
        public void MIC05_TargetedConsoleCommands_KillAndResurrect_OperateOnSelectedReference()
        {
            var attrs = _target.AddComponent<AttributeComponent>();
            ConsoleUtil.SetSelectedReference(_target);

            string killResult = _registry.Execute("kill");
            Assert.AreEqual(0f, attrs.CurrentHealth);
            Assert.IsTrue(killResult.Contains("Killed"));

            string rezResult = _registry.Execute("resurrect");
            Assert.AreEqual(attrs.MaxHealth, attrs.CurrentHealth);
            Assert.IsTrue(rezResult.Contains("Resurrected"));
        }

        [Test]
        public void MIC06_TargetedConsoleCommands_DisableAndEnable_OperateOnSelectedReference()
        {
            ConsoleUtil.SetSelectedReference(_target);

            string disableResult = _registry.Execute("disable");
            Assert.IsFalse(_target.activeSelf);
            Assert.IsTrue(disableResult.Contains("Disabled"));

            string enableResult = _registry.Execute("enable");
            Assert.IsTrue(_target.activeSelf);
            Assert.IsTrue(enableResult.Contains("Enabled"));
        }

        [Test]
        public void MIC07_TargetedConsoleCommands_SetScale_OperatesOnSelectedReference()
        {
            ConsoleUtil.SetSelectedReference(_target);

            string scaleResult = _registry.Execute("setscale 2.5");
            Assert.AreEqual(2.5f, _target.transform.localScale.x, 0.001f);
            Assert.IsTrue(scaleResult.Contains("2.5"));
        }

        [Test]
        public void MIC08_TargetedConsoleCommands_Unlock_UnlocksChestOnSelectedReference()
        {
            var chest = _target.AddComponent<ChestInteractable>();
            chest.SetLocked(LockDifficulty.Master);
            Assert.IsTrue(chest.IsLocked);

            ConsoleUtil.SetSelectedReference(_target);

            string unlockResult = _registry.Execute("unlock");
            Assert.IsFalse(chest.IsLocked);
            Assert.IsTrue(unlockResult.Contains("Unlocked"));
        }

        [Test]
        public void MIC09_TargetedConsoleCommands_ToggleAI_TogglesImmersiveCitizenAI()
        {
            var ai = _target.AddComponent<ImmersiveCitizenAI>();
            Assert.IsTrue(ai.enabled);

            ConsoleUtil.SetSelectedReference(_target);

            string taiResult1 = _registry.Execute("tai");
            Assert.IsFalse(ai.enabled);
            Assert.IsTrue(taiResult1.Contains("Disabled"));

            string taiResult2 = _registry.Execute("tai");
            Assert.IsTrue(ai.enabled);
            Assert.IsTrue(taiResult2.Contains("Enabled"));
        }

        [Test]
        public void MIC10_TargetedConsoleCommands_MoveToPlayer_TeleportsTargetToPlayer()
        {
            _player.transform.position = new Vector3(42f, 1f, -18f);
            ConsoleUtil.SetSelectedReference(_target);

            string moveResult = _registry.Execute("moveto player");
            Assert.AreEqual(new Vector3(42f, 1f, -18f), _target.transform.position);
            Assert.IsTrue(moveResult.Contains("Teleported"));
        }
    }
}
