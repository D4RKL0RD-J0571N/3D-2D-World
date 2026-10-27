namespace Domain.Skyrim
{
    using System;
    using System.Collections.Generic;
    using Core;

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

    // ── Data Structures ──────────────────────────────────────────────────────

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
        public string Title;
        public QuestType Type;
        public string LocationName;
        public List<QuestObjective> Objectives = new();
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
