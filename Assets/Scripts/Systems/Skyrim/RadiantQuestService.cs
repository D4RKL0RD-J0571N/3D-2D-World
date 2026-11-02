namespace Systems.Skyrim
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Core.Ids;
    using Core.Persistence;
    using Domain.Skyrim;
    using Newtonsoft.Json;
    using UnityEngine;

    /// <summary>
    /// Generates and tracks Skyrim-style "radiant" quests procedurally at runtime.
    /// Backed by <see cref="ContentRegistry{QuestTemplateDefinition}"/> so mods can register custom quest templates
    /// while preserving full compatibility with legacy <see cref="QuestType"/> callers, and implements <see cref="ISaveableService"/>.
    /// </summary>
    public class RadiantQuestService : IGameService, ISaveableService
    {
        #region Fields

        private readonly List<QuestInstance> _activeQuests = new();
        private readonly Dictionary<ResourceId, QuestTemplateDefinition> _localTemplates = new();
        private int _playerLevel = 1;

        #endregion

        #region Properties

        /// <summary>Read-only view of currently active quests.</summary>
        public IReadOnlyList<QuestInstance> ActiveQuests => _activeQuests;

        #endregion

        #region IGameService

        public void Initialize()
        {
            _activeQuests.Clear();
            _localTemplates.Clear();

            IContentService content = ServiceLocator.TryGet<IContentService>(out var cs) ? cs : null;
            ContentRegistry<QuestTemplateDefinition> registry = content?.GetRegistry<QuestTemplateDefinition>();

            IReadOnlyList<QuestTemplateDefinition> defaults = QuestTemplateDefinition.CreateDefaultNordicTemplates();
            for (int i = 0; i < defaults.Count; i++)
            {
                QuestTemplateDefinition def = defaults[i];
                _localTemplates[def.ContentId] = def;

                if (registry != null && !registry.Contains(def.ContentId) && !registry.IsFrozen)
                {
                    registry.Register(def, def.ContentId.Namespace, ContentRegistrationPolicy.OverrideExisting, def.Archetype.ToString());
                }
            }

            if (registry != null)
            {
                foreach (QuestTemplateDefinition customDef in registry.GetAll())
                {
                    _localTemplates[customDef.ContentId] = customDef;
                }
            }

            GameEventManager.AddListener<PlayerCharacterLeveledUpEvent>(OnPlayerLeveledUp);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<PlayerCharacterLeveledUpEvent>(OnPlayerLeveledUp);
            _activeQuests.Clear();
            _localTemplates.Clear();
        }

        #endregion

        #region Public API — ResourceId (Data-Driven)

        /// <summary>Registers a custom or modded <see cref="QuestTemplateDefinition"/>.</summary>
        public void RegisterTemplate(QuestTemplateDefinition template)
        {
            if (template == null || !template.ContentId.IsValid) return;

            _localTemplates[template.ContentId] = template;
            if (ServiceLocator.TryGet<IContentService>(out var content))
            {
                var reg = content.GetRegistry<QuestTemplateDefinition>();
                if (!reg.IsFrozen)
                {
                    reg.Register(template, template.ContentId.Namespace, ContentRegistrationPolicy.OverrideExisting);
                }
            }
        }

        /// <summary>
        /// Procedurally generates a quest from the <see cref="QuestTemplateDefinition"/> matching <paramref name="templateId"/>.
        /// </summary>
        public QuestInstance GenerateQuest(ResourceId templateId, string locationName)
        {
            QuestTemplateDefinition template = ResolveTemplate(templateId);
            if (template != null)
            {
                return new QuestInstance
                {
                    QuestId = Guid.NewGuid().ToString(),
                    TemplateId = template.ContentId,
                    Type = template.Archetype,
                    LocationName = locationName,
                    Title = template.FormatTitle(locationName),
                    BaseGoldReward = template.BaseGoldReward,
                    GoldPerPlayerLevel = template.GoldPerPlayerLevel,
                    XPReward = template.XPReward,
                    Objectives = new List<QuestObjective>
                    {
                        new QuestObjective
                        {
                            Description = template.FormatObjective(locationName),
                            RequiredCount = template.DefaultRequiredCount
                        }
                    }
                };
            }

            return new QuestInstance
            {
                QuestId = Guid.NewGuid().ToString(),
                TemplateId = templateId,
                Type = QuestType.DungeonCleanse,
                LocationName = locationName,
                Title = $"Quest: {locationName}",
                Objectives = new List<QuestObjective>
                {
                    new QuestObjective
                    {
                        Description = $"Complete objective in {locationName}",
                        RequiredCount = 1
                    }
                }
            };
        }

        #endregion

        #region Public API — Legacy QuestType Enum & Lifecycle

        /// <summary>
        /// Procedurally generates a quest of the given legacy <see cref="QuestType"/> for the specified location.
        /// </summary>
        public QuestInstance GenerateQuest(QuestType type, string locationName)
        {
            ResourceId templateId = QuestTemplateDefinition.ToResourceId(type);
            return GenerateQuest(templateId, locationName);
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

            var updated = quest.Objectives.Find(o => o.IsComplete) ?? quest.Objectives[0];
            GameEventManager.Publish(new QuestObjectiveUpdatedEvent(quest, updated));

            if (quest.AllObjectivesComplete())
            {
                CompleteQuest(questId);
            }
        }

        /// <summary>
        /// Marks a quest complete, dispatches the completion event, and removes it from active.
        /// </summary>
        public void CompleteQuest(string questId)
        {
            var quest = FindQuest(questId);
            if (quest == null) return;

            quest.IsCompleted = true;
            int gold = quest.BaseGoldReward + _playerLevel * quest.GoldPerPlayerLevel;
            int xp = quest.XPReward;

            GameEventManager.Publish(new QuestCompletedEvent(quest, gold, xp));
            _activeQuests.Remove(quest);

            Debug.Log($"[RadiantQuestService] Quest completed: {quest.Title} (+{gold}g, +{xp} XP)");
        }

        #endregion

        #region Helpers

        private QuestTemplateDefinition ResolveTemplate(ResourceId templateId)
        {
            if (!templateId.IsValid) return null;

            if (ServiceLocator.TryGet<IContentService>(out var content) &&
                content.GetRegistry<QuestTemplateDefinition>().TryGet(templateId, out QuestTemplateDefinition def))
            {
                return def;
            }

            if (_localTemplates.TryGetValue(templateId, out QuestTemplateDefinition localDef))
            {
                return localDef;
            }

            return null;
        }

        private void PinCompassMarker(QuestInstance quest)
        {
            if (!ServiceLocator.TryGet<CompassService>(out var compass)) return;

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

        #region ISaveableService Implementation

        [Serializable]
        private sealed class RadiantQuestSaveDto
        {
            public int PlayerLevel = 1;
            public List<QuestInstance> ActiveQuests = new();
        }

        public ResourceId SaveStateKey => new("framework", "state/quests");
        public int CurrentStateSchemaVersion => 1;

        public object CaptureState()
        {
            return new RadiantQuestSaveDto
            {
                PlayerLevel = _playerLevel,
                ActiveQuests = new List<QuestInstance>(_activeQuests)
            };
        }

        public void RestoreState(string serializedJsonState, int savedSchemaVersion)
        {
            if (string.IsNullOrWhiteSpace(serializedJsonState)) return;

            var settings = FantasyEngineJsonSettings.CreateDefault();
            var dto = JsonConvert.DeserializeObject<RadiantQuestSaveDto>(serializedJsonState, settings);
            if (dto == null) return;

            _playerLevel = Mathf.Max(1, dto.PlayerLevel);
            _activeQuests.Clear();

            if (dto.ActiveQuests != null)
            {
                for (int i = 0; i < dto.ActiveQuests.Count; i++)
                {
                    if (dto.ActiveQuests[i] != null)
                    {
                        _activeQuests.Add(dto.ActiveQuests[i]);
                    }
                }
            }
        }

        #endregion
    }
}
