namespace Domain.Skyrim
{
    using System;
    using Core;

    /// <summary>
    /// All learnable Skyrim-style skills. Progress via use, not generic XP grinding.
    /// </summary>
    public enum SkyrimSkill
    {
        OneHanded,
        TwoHanded,
        Archery,
        Sneak,
        LightArmor,
        HeavyArmor,
        Destruction,
        Restoration,
        Lockpicking
    }

    /// <summary>
    /// Runtime record of a single skill's current XP and level (1–100 scale).
    /// Modelled after Skyrim's skill-use progression curve.
    /// </summary>
    [Serializable]
    public class SkillRecord
    {
        #region Fields

        public SkyrimSkill Skill;

        /// <summary>Skill level, clamped 1–100.</summary>
        public int Level;

        /// <summary>Accumulated XP toward the next level threshold.</summary>
        public float XP;

        #endregion

        #region Constructor

        public SkillRecord(SkyrimSkill skill)
        {
            Skill = skill;
            Level = 15;   // Skyrim-style starting value
            XP    = 0f;
        }

        #endregion

        #region XP Math

        /// <summary>XP required for the next level at the current level.</summary>
        public float XPToNextLevel => 50f + Level * 12f;

        /// <summary>Normalized 0-1 progress toward the next level-up.</summary>
        public float XPRatio => XPToNextLevel > 0f ? Math.Min(XP / XPToNextLevel, 1f) : 0f;

        /// <summary>
        /// Adds XP and auto-levels up if threshold met.
        /// </summary>
        /// <returns>True if a level-up occurred.</returns>
        public bool AddXP(float amount)
        {
            XP += amount;
            if (XP >= XPToNextLevel && Level < 100)
            {
                XP   -= XPToNextLevel;
                Level = Math.Min(Level + 1, 100);
                return true;
            }
            return false;
        }

        #endregion
    }

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>Fired when a skill levels up via use.</summary>
    public class SkillLeveledUpEvent : GameEvent
    {
        public SkyrimSkill Skill;
        public int NewLevel;

        public SkillLeveledUpEvent(SkyrimSkill skill, int level)
        {
            Skill    = skill;
            NewLevel = level;
        }
    }

    /// <summary>Fired when any skill gains XP (HUD flash, sound).</summary>
    public class SkillXPGainedEvent : GameEvent
    {
        public SkyrimSkill Skill;
        public float Amount;

        public SkillXPGainedEvent(SkyrimSkill skill, float amount)
        {
            Skill  = skill;
            Amount = amount;
        }
    }

    /// <summary>Fired when the player's aggregate character level increases.</summary>
    public class PlayerCharacterLeveledUpEvent : GameEvent
    {
        public int NewLevel;
        public int PerkPoints;

        public PlayerCharacterLeveledUpEvent(int level, int perks)
        {
            NewLevel   = level;
            PerkPoints = perks;
        }
    }
}
