namespace Gameplay.Skyrim
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Core.Ids;
    using Data.Config;
    using Domain.Combat;
    using Domain.Dialogue;
    using Domain.Skyrim;
    using Gameplay.Entities;
    using UnityEngine;

    /// <summary>
    /// Event published when the player chooses and begins an Alternate Start scenario.
    /// </summary>
    public class AlternateStartCommittedEvent : GameEvent
    {
        public AlternateStartScenario Scenario;

        public AlternateStartCommittedEvent(AlternateStartScenario scenario)
        {
            Scenario = scenario;
        }
    }

    /// <summary>
    /// Coordinates the Alternate Start (Live Another Life) state machine.
    /// Manages the selected life scenario, Mara dialogue choices, starter inventory grants,
    /// and teleportation out of the prison cell into the world.
    /// </summary>
    public class AlternateStartService : IGameService
    {
        #region Fields

        private readonly List<AlternateStartScenario> _scenarios = new();
        private AlternateStartScenario _selectedScenario;
        private bool _hasStartedNewLife;

        #endregion

        #region Properties

        public IReadOnlyList<AlternateStartScenario> Scenarios => _scenarios;
        public AlternateStartScenario SelectedScenario => _selectedScenario;
        public bool HasStartedNewLife => _hasStartedNewLife;

        #endregion

        #region IGameService

        public void Initialize()
        {
            _scenarios.Clear();
            _scenarios.AddRange(AlternateStartScenario.GetDefaultScenarios());
            _selectedScenario = _scenarios.Count > 0 ? _scenarios[0] : null;
            _hasStartedNewLife = false;
        }

        public void Shutdown()
        {
            _scenarios.Clear();
            _selectedScenario = null;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Selects an Alternate Start scenario by its identifier.
        /// </summary>
        public bool SelectScenario(string scenarioId)
        {
            for (int i = 0; i < _scenarios.Count; i++)
            {
                if (string.Equals(_scenarios[i].ScenarioId, scenarioId, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedScenario = _scenarios[i];
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Confirms the selected life, equips starter gear, and teleports player to world location.
        /// </summary>
        public bool CommitLifeChoice(GameObject player)
        {
            if (_hasStartedNewLife || _selectedScenario == null || player == null) return false;

            // 1. Teleport player
            if (player.TryGetComponent<CharacterController>(out var cc)) cc.enabled = false;
            player.transform.position = _selectedScenario.SpawnPosition;
            player.transform.rotation = Quaternion.Euler(_selectedScenario.SpawnRotationEuler);
            if (cc != null) cc.enabled = true;

            // 2. Grant starter gear
            GrantStarterItems(player);

            _hasStartedNewLife = true;
            GameEventManager.Publish(new AlternateStartCommittedEvent(_selectedScenario));
            Debug.Log($"<color=green>[AlternateStart] Begun new life: '{_selectedScenario.Title}' at {player.transform.position}</color>");
            return true;
        }

        #endregion

        #region Helpers

        private void GrantStarterItems(GameObject player)
        {
            if (!player.TryGetComponent<InventoryComponent>(out var inv)) return;

            // Add starter gold/items if content registry is available
            for (int i = 0; i < _selectedScenario.StarterItems.Count; i++)
            {
                string itemPath = _selectedScenario.StarterItems[i];
                var resId = new ResourceId(itemPath);
                if (ServiceLocator.TryGet<IContentService>(out var content) &&
                    content.GetRegistry<ItemData>().TryGet(resId, out ItemData item))
                {
                    inv.AddItem(item, 1);
                }
            }
        }

        #endregion
    }
}
