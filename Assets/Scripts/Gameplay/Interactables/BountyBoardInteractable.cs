namespace Gameplay.Interactables
{
    using System.Collections.Generic;
    using Core;
    using Domain.Skyrim;
    using Systems.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Interactive Skyrim-style Bounty / Notice Board.
    /// Interacting generates radiant quests and accepts them into the active quest journal,
    /// updating the compass and RPG HUD.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Interactables/Bounty Board")]
    public class BountyBoardInteractable : MonoBehaviour, IInteractable
    {
        #region Serialized Fields

        [Header("Board Configuration")]
        [Tooltip("Available dungeon/ruin locations to generate radiant bounties for.")]
        [SerializeField] private string[] _targetLocations = {
            "Bleak Falls Crypt",
            "Shadowfen Ruins",
            "Whispering Caverns",
            "Embershard Mine",
            "Dragon's Tooth Keep"
        };

        [Tooltip("Quest types this notice board can dispatch.")]
        [SerializeField] private QuestType[] _supportedQuestTypes = {
            QuestType.DungeonCleanse,
            QuestType.BountyHunt,
            QuestType.RelicRecovery
        };

        #endregion

        #region Private State

        private int _bountiesPostedCount = 0;

        #endregion

        #region IInteractable

        public bool CanInteract => true;

        public string InteractionPrompt => "📜 Read Bounty Board (Take Quest)";

        public void OnInteract(Entities.BaseActor interactor)
        {
            DispatchRadiantBounty();
        }

        #endregion

        #region Quest Generation

        private void DispatchRadiantBounty()
        {
            if (!ServiceLocator.TryGet<RadiantQuestService>(out var questService))
            {
                Debug.LogWarning("[BountyBoardInteractable] RadiantQuestService not registered in ServiceLocator!");
                return;
            }

            // Pick a random location and quest type
            string location = GetRandomLocation();
            QuestType type = GetRandomQuestType();

            // Generate procedural quest instance
            var quest = questService.GenerateQuest(type, location);
            questService.AcceptQuest(quest);

            _bountiesPostedCount++;

            // Audio feedback
            if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                audio.PlayInteract();
            }

            Debug.Log($"<color=cyan>✦ BOUNTY ACCEPTED: '{quest.Title}' at {location}!</color>");
        }

        private string GetRandomLocation()
        {
            if (_targetLocations == null || _targetLocations.Length == 0)
                return "Unknown Ruins";

            int index = Random.Range(0, _targetLocations.Length);
            return _targetLocations[index];
        }

        private QuestType GetRandomQuestType()
        {
            if (_supportedQuestTypes == null || _supportedQuestTypes.Length == 0)
                return QuestType.DungeonCleanse;

            int index = Random.Range(0, _supportedQuestTypes.Length);
            return _supportedQuestTypes[index];
        }

        #endregion
    }
}
