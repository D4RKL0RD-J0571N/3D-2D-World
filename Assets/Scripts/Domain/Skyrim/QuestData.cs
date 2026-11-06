namespace Domain.Skyrim
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Core.Ids;

    // ── Enums ────────────────────────────────────────────────────────────────

    public enum QuestType
    {
        DungeonCleanse,  // Clear all enemies from a location
        BountyHunt,      // Kill a named target
        RelicRecovery,   // Find and retrieve an item
        EscortRescue     // Escort an NPC safely to a destination
    }

    public enum QuestObjectiveStatus
    {
        Active,
        Completed,
        Failed
    }

    // ── Data-Driven Quest Template Definition ────────────────────────────────

    /// <summary>
    /// Data-driven radiant quest template registered in <see cref="ContentRegistry{QuestTemplateDefinition}"/>.
    /// Mods and content packs can register custom quest templates with custom title/objective format strings,
    /// required counts, and reward formulas without editing code.
    /// </summary>
    [Serializable]
    public sealed class QuestTemplateDefinition : IContentDefinition
    {
        #region Properties

        public ResourceId ContentId { get; set; }
        public string Id => ContentId.Value;
        public string SourceModId { get; set; } = ResourceId.DefaultNamespace;
        public string DisplayName { get; set; }
        public QuestType Archetype { get; set; }
        public string TitleFormat { get; set; }
        public string ObjectiveFormat { get; set; }
        public int DefaultRequiredCount { get; set; } = 1;
        public int BaseGoldReward { get; set; } = 100;
        public int GoldPerPlayerLevel { get; set; } = 10;
        public int XPReward { get; set; } = 200;

        #endregion

        #region Constructors

        public QuestTemplateDefinition() { }

        public QuestTemplateDefinition(
            ResourceId contentId,
            string displayName,
            QuestType archetype,
            string titleFormat,
            string objectiveFormat,
            int defaultRequiredCount = 1,
            int baseGoldReward = 100,
            int goldPerPlayerLevel = 10,
            int xpReward = 200)
        {
            ContentId = contentId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? contentId.Path : displayName;
            Archetype = archetype;
            TitleFormat = titleFormat;
            ObjectiveFormat = objectiveFormat;
            DefaultRequiredCount = Math.Max(1, defaultRequiredCount);
            BaseGoldReward = baseGoldReward;
            GoldPerPlayerLevel = goldPerPlayerLevel;
            XPReward = xpReward;
        }

        #endregion

        #region Canonical Helpers

        private static readonly Dictionary<QuestType, ResourceId> EnumToId = new()
        {
            { QuestType.DungeonCleanse, new ResourceId("nordic_rpg", "quest_template/dungeon_cleanse") },
            { QuestType.BountyHunt,     new ResourceId("nordic_rpg", "quest_template/bounty_hunt") },
            { QuestType.RelicRecovery,  new ResourceId("nordic_rpg", "quest_template/relic_recovery") },
            { QuestType.EscortRescue,   new ResourceId("nordic_rpg", "quest_template/escort_rescue") }
        };

        /// <summary>Maps a legacy <see cref="QuestType"/> enum value to its canonical <see cref="ResourceId"/>.</summary>
        public static ResourceId ToResourceId(QuestType type) =>
            EnumToId.TryGetValue(type, out ResourceId id)
                ? id
                : ResourceId.FromLegacyId(ResourceId.DefaultNamespace, "quest_template", type.ToString());

        /// <summary>Creates the 4 default Nordic RPG radiant quest templates.</summary>
        public static IReadOnlyList<QuestTemplateDefinition> CreateDefaultNordicTemplates() => new[]
        {
            new QuestTemplateDefinition(
                new ResourceId("nordic_rpg", "quest_template/dungeon_cleanse"),
                "Dungeon Cleanse",
                QuestType.DungeonCleanse,
                "Purge: {0}",
                "Eliminate all threats in {0}",
                defaultRequiredCount: 5,
                baseGoldReward: 100,
                goldPerPlayerLevel: 10,
                xpReward: 200),
            new QuestTemplateDefinition(
                new ResourceId("nordic_rpg", "quest_template/bounty_hunt"),
                "Bounty Hunt",
                QuestType.BountyHunt,
                "Bounty: Slay the Target in {0}",
                "Slay the target in {0}",
                defaultRequiredCount: 1,
                baseGoldReward: 100,
                goldPerPlayerLevel: 10,
                xpReward: 200),
            new QuestTemplateDefinition(
                new ResourceId("nordic_rpg", "quest_template/relic_recovery"),
                "Relic Recovery",
                QuestType.RelicRecovery,
                "Relic: Recover the Artifact from {0}",
                "Recover the artifact from {0}",
                defaultRequiredCount: 1,
                baseGoldReward: 100,
                goldPerPlayerLevel: 10,
                xpReward: 200),
            new QuestTemplateDefinition(
                new ResourceId("nordic_rpg", "quest_template/escort_rescue"),
                "Escort Rescue",
                QuestType.EscortRescue,
                "Rescue: Escort the Survivor from {0}",
                "Escort the survivor to safety",
                defaultRequiredCount: 1,
                baseGoldReward: 100,
                goldPerPlayerLevel: 10,
                xpReward: 200)
        };

        public string FormatTitle(string locationName) =>
            string.IsNullOrWhiteSpace(TitleFormat)
                ? DisplayName
                : string.Format(TitleFormat, locationName ?? "Unknown Location");

        public string FormatObjective(string locationName) =>
            string.IsNullOrWhiteSpace(ObjectiveFormat)
                ? DisplayName
                : string.Format(ObjectiveFormat, locationName ?? "Unknown Location");

        #endregion
    }

    // ── Runtime Data Structures ──────────────────────────────────────────────

    [Serializable]
    public class QuestObjective
    {
        public string Description;
        public QuestObjectiveStatus Status = QuestObjectiveStatus.Active;
        public int RequiredCount;
        public int CurrentCount;

        public bool IsComplete => Status == QuestObjectiveStatus.Completed
                               || CurrentCount >= RequiredCount;
    }

    [Serializable]
    public class QuestInstance
    {
        public string QuestId;
        public ResourceId TemplateId;
        public string Title;
        public QuestType Type;
        public string LocationName;
        public List<QuestObjective> Objectives = new();
        public int BaseGoldReward = 100;
        public int GoldPerPlayerLevel = 10;
        public int XPReward = 200;
        public bool IsCompleted;
        public bool IsFailed;

        /// <summary>
        /// Advances the first incomplete objective by <paramref name="count"/>.
        /// </summary>
        /// <returns>True if an objective was advanced.</returns>
        public bool AdvanceObjective(int count = 1)
        {
            foreach (var obj in Objectives)
            {
                if (obj.IsComplete) continue;

                obj.CurrentCount += count;
                if (obj.CurrentCount >= obj.RequiredCount)
                    obj.Status = QuestObjectiveStatus.Completed;

                return true;
            }
            return false;
        }

        /// <summary>Returns true if every objective is complete.</summary>
        public bool AllObjectivesComplete()
        {
            foreach (var obj in Objectives)
            {
                if (!obj.IsComplete) return false;
            }
            return Objectives.Count > 0;
        }
    }

    // ── Events ───────────────────────────────────────────────────────────────

    public class QuestAcceptedEvent : GameEvent
    {
        public QuestInstance Quest;
        public QuestAcceptedEvent(QuestInstance q) { Quest = q; }
    }

    public class QuestObjectiveUpdatedEvent : GameEvent
    {
        public QuestInstance Quest;
        public QuestObjective Objective;
        public QuestObjectiveUpdatedEvent(QuestInstance q, QuestObjective o)
        {
            Quest     = q;
            Objective = o;
        }
    }

    public class QuestCompletedEvent : GameEvent
    {
        public QuestInstance Quest;
        public int GoldReward;
        public int XPReward;
        public QuestCompletedEvent(QuestInstance q, int gold, int xp)
        {
            Quest      = q;
            GoldReward = gold;
            XPReward   = xp;
        }
    }
}
