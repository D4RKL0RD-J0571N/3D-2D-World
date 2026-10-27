namespace Systems.Skyrim
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Domain.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Generates and tracks Skyrim-style "radiant" quests procedurally at runtime.
    /// Quests are objective-driven, compass-pinned, and reward XP and gold on completion.
    /// </summary>
    public class RadiantQuestService : IGameService
    {
        #region Fields

        private readonly List<QuestInstance> _activeQuests = new();

        // Running level used to scale rewards (incremented by SkillProgressionService events)
        private int _playerLevel = 1;

        #endregion

        #region IGameService

        public void Initialize()
        {
            GameEventManager.AddListener<PlayerCharacterLeveledUpEvent>(OnPlayerLeveledUp);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<PlayerCharacterLeveledUpEvent>(OnPlayerLeveledUp);
            _activeQuests.Clear();
        }

        #endregion

        #region Public API

        /// <summary>Read-only view of currently active quests.</summary>
        public IReadOnlyList<QuestInstance> ActiveQuests => _activeQuests;

        /// <summary>
        /// Procedurally generates a quest of the given type for the specified location.
        /// </summary>
        public QuestInstance GenerateQuest(QuestType type, string locationName)
        {
            var quest = new QuestInstance
            {
                QuestId      = Guid.NewGuid().ToString(),
                Type         = type,
                LocationName = locationName,
                Title        = BuildTitle(type, locationName),
                Objectives   = new List<QuestObjective> { BuildObjective(type, locationName) }
            };
            return quest;
        }

        /// <summary>
        /// Accepts a quest into the active list and pins a compass marker.
        /// </summary>
        public void AcceptQuest(QuestInstance quest)
        {
            if (quest == null) return;

            _activeQuests.Add(quest);
            GameEventManager.Publish(new QuestAcceptedEvent(quest));
            PinCompassMarker(quest);

            Debug.Log($"[RadiantQuestService] Quest accepted: {quest.Title}");
        }

        /// <summary>
        /// Advances the first incomplete objective of the quest matching <paramref name="questId"/>.
        /// Auto-completes the quest when all objectives are satisfied.
        /// </summary>
        public void AdvanceQuest(string questId, int count = 1)
        {
            var quest = FindQuest(questId);
            if (quest == null) return;

            bool advanced = quest.AdvanceObjective(count);
            if (!advanced) return;

            var updated = quest.Objectives.Find(o => o.IsComplete);
            GameEventManager.Publish(new QuestObjectiveUpdatedEvent(quest, updated));

            if (quest.AllObjectivesComplete())
                CompleteQuest(questId);
        }

        /// <summary>
        /// Marks a quest complete, dispatches the completion event, and removes it from active.
        /// </summary>
        public void CompleteQuest(string questId)
        {
            var quest = FindQuest(questId);
            if (quest == null) return;

            quest.IsCompleted = true;
            int gold = 100 + _playerLevel * 10;
            int xp   = 200;

            GameEventManager.Publish(new QuestCompletedEvent(quest, gold, xp));
            _activeQuests.Remove(quest);

            Debug.Log($"[RadiantQuestService] Quest completed: {quest.Title} (+{gold}g, +{xp} XP)");
        }

        #endregion

        #region Helpers

        private string BuildTitle(QuestType type, string location) => type switch
        {
            QuestType.DungeonCleanse => $"Purge: {location}",
            QuestType.BountyHunt    => $"Bounty: Slay the Target in {location}",
            QuestType.RelicRecovery => $"Relic: Recover the Artifact from {location}",
            QuestType.EscortRescue  => $"Rescue: Escort the Survivor from {location}",
            _                       => "Unknown Quest"
        };

        private QuestObjective BuildObjective(QuestType type, string location) => type switch
        {
            QuestType.DungeonCleanse => new QuestObjective
            {
                Description   = $"Eliminate all threats in {location}",
                RequiredCount = 5
            },
            QuestType.BountyHunt => new QuestObjective
            {
                Description   = $"Slay the target in {location}",
                RequiredCount = 1
            },
            QuestType.RelicRecovery => new QuestObjective
            {
                Description   = $"Recover the artifact from {location}",
                RequiredCount = 1
            },
            QuestType.EscortRescue => new QuestObjective
            {
                Description   = "Escort the survivor to safety",
                RequiredCount = 1
            },
            _ => new QuestObjective { Description = "Unknown Objective", RequiredCount = 1 }
        };

        private void PinCompassMarker(QuestInstance quest)
        {
            if (!ServiceLocator.TryGet<CompassService>(out var compass)) return;

            // Pin at origin as a placeholder; designers should relocate via CompassMarkerComponent
            var markerData = new CompassMarkerData(
                id:            quest.QuestId,
                title:         quest.Title,
                type:          CompassPOIType.QuestMarker,
                worldPosition: Vector3.zero,
                maxDistance:   500f);

            compass.RegisterMarker(markerData);
        }

        private QuestInstance FindQuest(string questId)
        {
            foreach (var q in _activeQuests)
            {
                if (q.QuestId == questId) return q;
            }
            return null;
        }

        private void OnPlayerLeveledUp(PlayerCharacterLeveledUpEvent evt)
        {
            if (evt != null) _playerLevel = evt.NewLevel;
        }

        #endregion
    }
}
