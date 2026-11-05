namespace Systems.Skyrim
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Core.Ids;
    using Core.Persistence;
    using Domain.Combat;
    using Domain.Entities;
    using Domain.Skyrim;
    using Newtonsoft.Json;
    using UnityEngine;

    /// <summary>
    /// "Learn by doing" skill progression service backed by <see cref="ContentRegistry{SkillDefinition}"/>.
    /// Supports both data-driven <see cref="ResourceId"/> skills (including custom mod skills)
    /// and legacy <see cref="SkyrimSkill"/> enum calls, and implements <see cref="ISaveableService"/>.
    /// </summary>
    public class SkillProgressionService : IGameService, ITickable, ISaveableService
    {
        #region Constants

        public static readonly ResourceId OneHandedSkillId   = new("nordic_rpg", "skill/one_handed");
        public static readonly ResourceId TwoHandedSkillId   = new("nordic_rpg", "skill/two_handed");
        public static readonly ResourceId ArcherySkillId     = new("nordic_rpg", "skill/archery");
        public static readonly ResourceId SneakSkillId       = new("nordic_rpg", "skill/sneak");
        public static readonly ResourceId LightArmorSkillId  = new("nordic_rpg", "skill/light_armor");
        public static readonly ResourceId HeavyArmorSkillId  = new("nordic_rpg", "skill/heavy_armor");
        public static readonly ResourceId DestructionSkillId = new("nordic_rpg", "skill/destruction");
        public static readonly ResourceId RestorationSkillId = new("nordic_rpg", "skill/restoration");
        public static readonly ResourceId LockpickingSkillId = new("nordic_rpg", "skill/lockpicking");

        private const float SneakXPInterval = 3f;
        private const float SneakXPPerInterval = 4f;
        private const int SkillUpsPerCharacterLevel = 10;

        #endregion

        #region Fields

        private readonly Dictionary<ResourceId, SkillRecord> _skillsById = new();
        private readonly Dictionary<SkyrimSkill, SkillRecord> _legacySkills = new();

        private float _sneakXPTimer;
        private int _characterLevel = 1;
        private int _perkPointsTotal;

        #endregion

        #region Properties

        /// <summary>Current aggregate player character level (starts at 1, increases by 1 every 10 skill-ups).</summary>
        public int CharacterLevel => _characterLevel;

        /// <summary>Total perk points earned from character level-ups.</summary>
        public int PerkPointsTotal => _perkPointsTotal;

        /// <summary>Read-only view of all skill records keyed by canonical <see cref="ResourceId"/>.</summary>
        public IReadOnlyDictionary<ResourceId, SkillRecord> AllSkillsById => _skillsById;

        /// <summary>Legacy read-only view of the 9 core <see cref="SkyrimSkill"/> records.</summary>
        public IReadOnlyDictionary<SkyrimSkill, SkillRecord> AllSkills => _legacySkills;

        #endregion

        #region IGameService

        public void Initialize()
        {
            _skillsById.Clear();
            _legacySkills.Clear();

            IContentService content = ServiceLocator.TryGet<IContentService>(out var cs) ? cs : null;
            ContentRegistry<SkillDefinition> registry = content?.GetRegistry<SkillDefinition>();

            // Seed default Nordic RPG skills into ContentRegistry if not already populated
            IReadOnlyList<SkillDefinition> defaults = SkillDefinition.CreateDefaultNordicSkills();
            for (int i = 0; i < defaults.Count; i++)
            {
                SkillDefinition def = defaults[i];
                if (registry != null && !registry.Contains(def.ContentId) && !registry.IsFrozen)
                {
                    registry.Register(def, def.ContentId.Namespace, ContentRegistrationPolicy.OverrideExisting, def.LegacySkill?.ToString());
                }

                EnsureSkillRecord(def);
            }

            // Also import any custom mod skills already registered in ContentRegistry<SkillDefinition>
            if (registry != null)
            {
                foreach (SkillDefinition customDef in registry.GetAll())
                {
                    EnsureSkillRecord(customDef);
                }
            }

            _characterLevel = ComputeCharacterLevel();
            _perkPointsTotal = 0;

            GameEventManager.AddListener<EntityDamagedEvent>(OnEntityDamaged);
            GameEventManager.AddListener<StealthStateChangedEvent>(OnStealthStateChanged);
            GameEventManager.AddListener<SneakAttackTriggeredEvent>(OnSneakAttackTriggered);
            GameEventManager.AddListener<QuestCompletedEvent>(OnQuestCompleted);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<EntityDamagedEvent>(OnEntityDamaged);
            GameEventManager.RemoveListener<StealthStateChangedEvent>(OnStealthStateChanged);
            GameEventManager.RemoveListener<SneakAttackTriggeredEvent>(OnSneakAttackTriggered);
            GameEventManager.RemoveListener<QuestCompletedEvent>(OnQuestCompleted);
        }

        #endregion

        #region ITickable

        /// <summary>Drives the sneak drip-XP timer each frame.</summary>
        public void Tick(float deltaTime)
        {
            if (_sneakXPTimer > 0f)
            {
                _sneakXPTimer -= deltaTime;
            }
        }

        #endregion

        #region Public API — ResourceId (Data-Driven)

        /// <summary>
        /// Registers or synchronizes a custom <see cref="SkillDefinition"/> at runtime
        /// and ensures a corresponding <see cref="SkillRecord"/> exists.
        /// </summary>
        public SkillRecord RegisterSkill(SkillDefinition definition)
        {
            if (definition == null || !definition.ContentId.IsValid)
            {
                return null;
            }

            if (ServiceLocator.TryGet<IContentService>(out var content))
            {
                var reg = content.GetRegistry<SkillDefinition>();
                if (!reg.IsFrozen)
                {
                    reg.Register(definition, definition.ContentId.Namespace, ContentRegistrationPolicy.OverrideExisting);
                }
            }

            return EnsureSkillRecord(definition);
        }

        /// <summary>Returns the integer level (1–100) for the given <paramref name="skillId"/>.</summary>
        public int GetSkillLevel(ResourceId skillId)
        {
            if (TryGetOrEnsureRecord(skillId, out SkillRecord rec))
            {
                return rec.Level;
            }
            return 0;
        }

        /// <summary>Returns normalized XP progress (0–1) toward the next level for <paramref name="skillId"/>.</summary>
        public float GetSkillXPRatio(ResourceId skillId)
        {
            if (TryGetOrEnsureRecord(skillId, out SkillRecord rec))
            {
                return rec.XPRatio;
            }
            return 0f;
        }

        /// <summary>Alias for <see cref="AwardXP(ResourceId, float)"/> supporting mod/test callers.</summary>
        public void AddSkillXP(ResourceId skillId, float amount) => AwardXP(skillId, amount);

        /// <summary>
        /// Awards XP to any skill by <see cref="ResourceId"/> (including custom mod skills),
        /// raises <see cref="SkillXPGainedEvent"/> and <see cref="SkillLeveledUpEvent"/>,
        /// and checks for overall player character level increases.
        /// </summary>
        public void AwardXP(ResourceId skillId, float amount)
        {
            if (amount <= 0f || !TryGetOrEnsureRecord(skillId, out SkillRecord rec))
            {
                return;
            }

            GameEventManager.Publish(new SkillXPGainedEvent(skillId, amount, rec.Skill));

            int previousLevel = rec.Level;
            int levelsGained = rec.AddXPCountLevels(amount);
            if (levelsGained > 0)
            {
                for (int lv = previousLevel + 1; lv <= rec.Level; lv++)
                {
                    GameEventManager.Publish(new SkillLeveledUpEvent(skillId, lv, rec.Skill));
                }

                CheckCharacterLevelUp();
            }
        }

        /// <summary>Directly restores a skill's level and XP (used by the persistence/save system).</summary>
        public void RestoreSkillState(ResourceId skillId, int level, float xp)
        {
            if (!TryGetOrEnsureRecord(skillId, out SkillRecord rec))
            {
                rec = EnsureSkillRecord(new SkillDefinition(skillId, skillId.Path));
            }

            rec.Level = Mathf.Clamp(level, 1, 100);
            rec.XP = Mathf.Max(0f, xp);
            _characterLevel = ComputeCharacterLevel();
        }

        #endregion

        #region Public API — Legacy SkyrimSkill Enum Overloads

        /// <summary>Returns the integer level (1–100) for the given legacy skill enum.</summary>
        public int GetSkillLevel(SkyrimSkill skill) =>
            GetSkillLevel(SkillDefinition.ToResourceId(skill));

        /// <summary>Returns normalized XP progress (0–1) toward the next level.</summary>
        public float GetSkillXPRatio(SkyrimSkill skill) =>
            GetSkillXPRatio(SkillDefinition.ToResourceId(skill));

        /// <summary>Awards XP to a legacy <see cref="SkyrimSkill"/> enum value.</summary>
        public void AwardXP(SkyrimSkill skill, float amount) =>
            AwardXP(SkillDefinition.ToResourceId(skill), amount);

        #endregion

        #region Event Handlers

        private void OnEntityDamaged(EntityDamagedEvent evt)
        {
            if (evt == null) return;

            if (evt.Instigator != null && evt.Instigator.CompareTag("Player"))
            {
                AwardOffensiveXP(evt.DamageType);
            }

            if (evt.Target != null && evt.Target.CompareTag("Player"))
            {
                AwardXP(LightArmorSkillId, 3f);
            }
        }

        private void OnStealthStateChanged(StealthStateChangedEvent evt)
        {
            if (evt == null || !evt.IsCrouching || evt.State != StealthState.Hidden) return;
            if (_sneakXPTimer > 0f) return;

            AwardXP(SneakSkillId, SneakXPPerInterval);
            _sneakXPTimer = SneakXPInterval;
        }

        private void OnSneakAttackTriggered(SneakAttackTriggeredEvent evt)
        {
            if (evt == null) return;
            AwardXP(SneakSkillId, 15f);
        }

        private void OnQuestCompleted(QuestCompletedEvent evt)
        {
            if (evt == null) return;
            AwardXP(OneHandedSkillId, evt.XPReward);
        }

        #endregion

        #region Helpers

        private SkillRecord EnsureSkillRecord(SkillDefinition def)
        {
            if (def == null || !def.ContentId.IsValid) return null;

            if (!_skillsById.TryGetValue(def.ContentId, out SkillRecord record))
            {
                record = new SkillRecord(def);
                _skillsById[def.ContentId] = record;
            }

            if (def.LegacySkill.HasValue)
            {
                _legacySkills[def.LegacySkill.Value] = record;
            }
            else if (SkillDefinition.TryToLegacyEnum(def.ContentId, out SkyrimSkill mapped))
            {
                _legacySkills[mapped] = record;
            }

            return record;
        }

        private bool TryGetOrEnsureRecord(ResourceId skillId, out SkillRecord record)
        {
            if (_skillsById.TryGetValue(skillId, out record))
            {
                return true;
            }

            if (ServiceLocator.TryGet<IContentService>(out var content) &&
                content.GetRegistry<SkillDefinition>().TryGet(skillId, out SkillDefinition def))
            {
                record = EnsureSkillRecord(def);
                return record != null;
            }

            return false;
        }

        private void AwardOffensiveXP(DamageType type)
        {
            ResourceId skillId = type == DamageType.Magic
                ? DestructionSkillId
                : OneHandedSkillId;

            AwardXP(skillId, 6f);
        }

        /// <summary>
        /// Character level = 1 + floor(total skill levels gained above starting level / 10).
        /// Every 10 skill-ups across any registered skill grants +1 Character Level and +1 Perk Point.
        /// </summary>
        private int ComputeCharacterLevel()
        {
            int totalSkillUps = 0;
            foreach (SkillRecord rec in _skillsById.Values)
            {
                if (rec.Level > rec.StartingLevel)
                {
                    totalSkillUps += (rec.Level - rec.StartingLevel);
                }
            }

            return 1 + (totalSkillUps / SkillUpsPerCharacterLevel);
        }

        private void CheckCharacterLevelUp()
        {
            int newLevel = ComputeCharacterLevel();
            if (newLevel <= _characterLevel) return;

            int gained = newLevel - _characterLevel;
            _perkPointsTotal += gained;
            _characterLevel = newLevel;

            GameEventManager.Publish(new PlayerCharacterLeveledUpEvent(newLevel, _perkPointsTotal));
        }

        #endregion

        #region ISaveableService Implementation

        [Serializable]
        private sealed class SkillProgressionSaveDto
        {
            public int CharacterLevel = 1;
            public int PerkPointsTotal;
            public List<SkillSaveEntry> Skills = new();
        }

        [Serializable]
        private sealed class SkillSaveEntry
        {
            public string SkillId;
            public int Level;
            public float XP;
            public int StartingLevel = 15;
        }

        public ResourceId SaveStateKey => new("framework", "state/progression");
        public int CurrentStateSchemaVersion => 1;

        public object CaptureState()
        {
            var dto = new SkillProgressionSaveDto
            {
                CharacterLevel = _characterLevel,
                PerkPointsTotal = _perkPointsTotal
            };

            foreach (var kvp in _skillsById)
            {
                dto.Skills.Add(new SkillSaveEntry
                {
                    SkillId = kvp.Key.Value,
                    Level = kvp.Value.Level,
                    XP = kvp.Value.XP,
                    StartingLevel = kvp.Value.StartingLevel
                });
            }

            return dto;
        }

        public void RestoreState(string serializedJsonState, int savedSchemaVersion)
        {
            if (string.IsNullOrWhiteSpace(serializedJsonState)) return;

            var settings = FantasyEngineJsonSettings.CreateDefault();
            var dto = JsonConvert.DeserializeObject<SkillProgressionSaveDto>(serializedJsonState, settings);
            if (dto == null) return;

            if (dto.Skills != null)
            {
                for (int i = 0; i < dto.Skills.Count; i++)
                {
                    SkillSaveEntry entry = dto.Skills[i];
                    if (ResourceId.TryParse(entry.SkillId, out ResourceId skillId))
                    {
                        RestoreSkillState(skillId, entry.Level, entry.XP);
                    }
                }
            }

            _characterLevel = dto.CharacterLevel > 0 ? dto.CharacterLevel : ComputeCharacterLevel();
            _perkPointsTotal = dto.PerkPointsTotal;
        }

        #endregion
    }
}
