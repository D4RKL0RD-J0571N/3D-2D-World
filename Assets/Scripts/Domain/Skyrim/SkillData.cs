namespace Domain.Skyrim
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Core.Ids;

    /// <summary>
    /// All learnable Skyrim-style skills. Progress via use, not generic XP grinding.
    /// Retained for backward compatibility alongside data-driven <see cref="SkillDefinition"/>.
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
    /// Data-driven skill definition registered in <see cref="ContentRegistry{SkillDefinition}"/>.
    /// Allows mods and content packs to introduce new skills (e.g. <c>chronomancy_mod:skill/time_weaving</c>)
    /// without modifying the <see cref="SkyrimSkill"/> enum.
    /// </summary>
    [Serializable]
    public sealed class SkillDefinition : IContentDefinition
    {
        #region Fields & Properties

        public ResourceId ContentId { get; set; }
        public string Id => ContentId.Value;
        public string SourceModId { get; set; } = ResourceId.DefaultNamespace;
        public string DisplayName { get; set; }
        public string Category { get; set; } = "General";
        public int StartingLevel { get; set; } = 15;
        public float BaseXP { get; set; } = 50f;
        public float XPPerLevel { get; set; } = 12f;
        public SkyrimSkill? LegacySkill { get; set; }

        #endregion

        #region Constructors

        public SkillDefinition() { }

        public SkillDefinition(
            ResourceId contentId,
            string displayName,
            string category = "General",
            int startingLevel = 15,
            float baseXp = 50f,
            float xpPerLevel = 12f,
            SkyrimSkill? legacySkill = null)
        {
            ContentId = contentId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? contentId.Path : displayName;
            Category = category ?? "General";
            StartingLevel = startingLevel;
            BaseXP = baseXp;
            XPPerLevel = xpPerLevel;
            LegacySkill = legacySkill;
        }

        #endregion

        #region Canonical Helpers

        private static readonly Dictionary<SkyrimSkill, ResourceId> EnumToId = new()
        {
            { SkyrimSkill.OneHanded,   new ResourceId("nordic_rpg", "skill/one_handed") },
            { SkyrimSkill.TwoHanded,   new ResourceId("nordic_rpg", "skill/two_handed") },
            { SkyrimSkill.Archery,     new ResourceId("nordic_rpg", "skill/archery") },
            { SkyrimSkill.Sneak,       new ResourceId("nordic_rpg", "skill/sneak") },
            { SkyrimSkill.LightArmor,  new ResourceId("nordic_rpg", "skill/light_armor") },
            { SkyrimSkill.HeavyArmor,  new ResourceId("nordic_rpg", "skill/heavy_armor") },
            { SkyrimSkill.Destruction, new ResourceId("nordic_rpg", "skill/destruction") },
            { SkyrimSkill.Restoration, new ResourceId("nordic_rpg", "skill/restoration") },
            { SkyrimSkill.Lockpicking, new ResourceId("nordic_rpg", "skill/lockpicking") }
        };

        private static readonly Dictionary<ResourceId, SkyrimSkill> IdToEnum = new()
        {
            { new ResourceId("nordic_rpg", "skill/one_handed"),   SkyrimSkill.OneHanded },
            { new ResourceId("nordic_rpg", "skill/two_handed"),   SkyrimSkill.TwoHanded },
            { new ResourceId("nordic_rpg", "skill/archery"),      SkyrimSkill.Archery },
            { new ResourceId("nordic_rpg", "skill/sneak"),        SkyrimSkill.Sneak },
            { new ResourceId("nordic_rpg", "skill/light_armor"),  SkyrimSkill.LightArmor },
            { new ResourceId("nordic_rpg", "skill/heavy_armor"),  SkyrimSkill.HeavyArmor },
            { new ResourceId("nordic_rpg", "skill/destruction"),  SkyrimSkill.Destruction },
            { new ResourceId("nordic_rpg", "skill/restoration"),  SkyrimSkill.Restoration },
            { new ResourceId("nordic_rpg", "skill/lockpicking"),  SkyrimSkill.Lockpicking }
        };

        /// <summary>Maps a legacy <see cref="SkyrimSkill"/> enum value to its canonical <see cref="ResourceId"/>.</summary>
        public static ResourceId ToResourceId(SkyrimSkill skill) =>
            EnumToId.TryGetValue(skill, out ResourceId id)
                ? id
                : ResourceId.FromLegacyId(ResourceId.DefaultNamespace, "skill", skill.ToString());

        /// <summary>Attempts to map a <see cref="ResourceId"/> back to a legacy <see cref="SkyrimSkill"/> enum value.</summary>
        public static bool TryToLegacyEnum(ResourceId id, out SkyrimSkill skill) =>
            IdToEnum.TryGetValue(id, out skill);

        /// <summary>Creates the default Nordic RPG skill definitions (the 9 core skills + 9 extended archetype skills).</summary>
        public static IReadOnlyList<SkillDefinition> CreateDefaultNordicSkills() => new[]
        {
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/one_handed"),   "One-Handed",   "Combat",  15, 50f, 12f, SkyrimSkill.OneHanded),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/two_handed"),   "Two-Handed",   "Combat",  15, 50f, 12f, SkyrimSkill.TwoHanded),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/archery"),      "Archery",      "Combat",  15, 50f, 12f, SkyrimSkill.Archery),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/sneak"),        "Sneak",        "Stealth", 15, 50f, 12f, SkyrimSkill.Sneak),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/light_armor"),  "Light Armor",  "Stealth", 15, 50f, 12f, SkyrimSkill.LightArmor),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/heavy_armor"),  "Heavy Armor",  "Combat",  15, 50f, 12f, SkyrimSkill.HeavyArmor),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/destruction"),  "Destruction",  "Magic",   15, 50f, 12f, SkyrimSkill.Destruction),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/restoration"),  "Restoration",  "Magic",   15, 50f, 12f, SkyrimSkill.Restoration),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/lockpicking"),  "Lockpicking",  "Stealth", 15, 50f, 12f, SkyrimSkill.Lockpicking),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/block"),        "Block",        "Combat",  15, 50f, 12f),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/smithing"),     "Smithing",     "Combat",  15, 50f, 12f),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/alteration"),   "Alteration",   "Magic",   15, 50f, 12f),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/conjuration"),  "Conjuration",  "Magic",   15, 50f, 12f),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/illusion"),     "Illusion",     "Magic",   15, 50f, 12f),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/enchanting"),   "Enchanting",   "Magic",   15, 50f, 12f),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/alchemy"),      "Alchemy",      "Stealth", 15, 50f, 12f),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/pickpocket"),   "Pickpocket",   "Stealth", 15, 50f, 12f),
            new SkillDefinition(new ResourceId("nordic_rpg", "skill/speech"),       "Speech",       "Stealth", 15, 50f, 12f)
        };

        #endregion
    }

    /// <summary>
    /// Runtime record of a single skill's current XP and level (1–100 scale).
    /// Supports both legacy <see cref="SkyrimSkill"/> enum values and arbitrary <see cref="ResourceId"/> definitions.
    /// </summary>
    [Serializable]
    public class SkillRecord
    {
        #region Fields

        public SkyrimSkill Skill;
        public ResourceId SkillId;
        public string DisplayName;

        /// <summary>Starting skill level (used to calculate net skill-ups for character leveling).</summary>
        public int StartingLevel = 15;

        /// <summary>Skill level, clamped 1–100.</summary>
        public int Level;

        /// <summary>Accumulated XP toward the next level threshold.</summary>
        public float XP;

        public float BaseXP = 50f;
        public float XPPerLevel = 12f;

        #endregion

        #region Constructors

        public SkillRecord(SkyrimSkill skill)
        {
            Skill = skill;
            SkillId = SkillDefinition.ToResourceId(skill);
            DisplayName = skill.ToString();
            StartingLevel = 15;
            Level = 15;
            XP = 0f;
            BaseXP = 50f;
            XPPerLevel = 12f;
        }

        public SkillRecord(SkillDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            SkillId = definition.ContentId;
            DisplayName = definition.DisplayName;
            StartingLevel = definition.StartingLevel > 0 ? definition.StartingLevel : 15;
            Level = StartingLevel;
            XP = 0f;
            BaseXP = definition.BaseXP > 0f ? definition.BaseXP : 50f;
            XPPerLevel = definition.XPPerLevel > 0f ? definition.XPPerLevel : 12f;

            if (definition.LegacySkill.HasValue)
            {
                Skill = definition.LegacySkill.Value;
            }
            else if (SkillDefinition.TryToLegacyEnum(definition.ContentId, out SkyrimSkill mapped))
            {
                Skill = mapped;
            }
        }

        #endregion

        #region XP Math

        /// <summary>XP required for the next level at the current level.</summary>
        public float XPToNextLevel => BaseXP + Level * XPPerLevel;

        /// <summary>Normalized 0-1 progress toward the next level-up.</summary>
        public float XPRatio => XPToNextLevel > 0f ? Math.Min(XP / XPToNextLevel, 1f) : 0f;

        /// <summary>
        /// Adds XP and auto-levels up (supporting multi-level jumps on large XP awards).
        /// </summary>
        /// <returns>True if at least one level-up occurred.</returns>
        public bool AddXP(float amount)
        {
            return AddXPCountLevels(amount) > 0;
        }

        /// <summary>
        /// Adds XP, processes all crossed level thresholds up to level 100, and returns the number of levels gained.
        /// </summary>
        public int AddXPCountLevels(float amount)
        {
            if (amount <= 0f) return 0;

            XP += amount;
            int levelsGained = 0;

            while (XP >= XPToNextLevel && Level < 100)
            {
                XP -= XPToNextLevel;
                Level = Math.Min(Level + 1, 100);
                levelsGained++;
            }

            if (Level >= 100)
            {
                XP = 0f;
            }

            return levelsGained;
        }

        #endregion
    }

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>Fired when a skill levels up via use.</summary>
    public class SkillLeveledUpEvent : GameEvent
    {
        public SkyrimSkill Skill;
        public ResourceId SkillId;
        public int NewLevel;

        public SkillLeveledUpEvent(SkyrimSkill skill, int level)
        {
            Skill = skill;
            SkillId = SkillDefinition.ToResourceId(skill);
            NewLevel = level;
        }

        public SkillLeveledUpEvent(ResourceId skillId, int level, SkyrimSkill fallbackSkill = SkyrimSkill.OneHanded)
        {
            SkillId = skillId;
            Skill = SkillDefinition.TryToLegacyEnum(skillId, out SkyrimSkill mapped) ? mapped : fallbackSkill;
            NewLevel = level;
        }
    }

    /// <summary>Fired when any skill gains XP (HUD flash, sound).</summary>
    public class SkillXPGainedEvent : GameEvent
    {
        public SkyrimSkill Skill;
        public ResourceId SkillId;
        public float Amount;

        public SkillXPGainedEvent(SkyrimSkill skill, float amount)
        {
            Skill = skill;
            SkillId = SkillDefinition.ToResourceId(skill);
            Amount = amount;
        }

        public SkillXPGainedEvent(ResourceId skillId, float amount, SkyrimSkill fallbackSkill = SkyrimSkill.OneHanded)
        {
            SkillId = skillId;
            Skill = SkillDefinition.TryToLegacyEnum(skillId, out SkyrimSkill mapped) ? mapped : fallbackSkill;
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
            NewLevel = level;
            PerkPoints = perks;
        }
    }
}
