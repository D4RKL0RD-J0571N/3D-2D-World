namespace Systems.Skyrim
{
    using System.Collections.Generic;
    using Core;
    using Domain.Combat;
    using Domain.Entities;
    using Domain.Skyrim;
    using UnityEngine;

    /// <summary>
    /// "Learn by doing" Skyrim-style skill progression service.
    /// Awards XP to appropriate skills based on observed combat and stealth events,
    /// then raises SkillLeveledUpEvent and PlayerCharacterLeveledUpEvent when thresholds are crossed.
    /// </summary>
    public class SkillProgressionService : IGameService, ITickable
    {
        #region Fields

        private readonly Dictionary<SkyrimSkill, SkillRecord> _skills = new();

        // Sneak XP drip: only award every N seconds while hidden+crouching
        private float _sneakXPTimer = 0f;
        private const float SneakXPInterval = 3f;
        private const float SneakXPPerInterval = 4f;

        // Cache last-known character level to detect level-up
        private int _characterLevel = 0;
        private int _perkPointsTotal = 0;

        #endregion

        #region IGameService

        public void Initialize()
        {
            // Seed all skills with default level 15 (Skyrim start)
            foreach (SkyrimSkill skill in System.Enum.GetValues(typeof(SkyrimSkill)))
                _skills[skill] = new SkillRecord(skill);

            _characterLevel = ComputeCharacterLevel();

            GameEventManager.AddListener<EntityDamagedEvent>(OnEntityDamaged);
            GameEventManager.AddListener<StealthStateChangedEvent>(OnStealthStateChanged);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<EntityDamagedEvent>(OnEntityDamaged);
            GameEventManager.RemoveListener<StealthStateChangedEvent>(OnStealthStateChanged);
        }

        #endregion

        #region ITickable

        /// <summary>Drives the sneak drip-XP timer each frame.</summary>
        public void Tick(float deltaTime)
        {
            if (_sneakXPTimer > 0f)
                _sneakXPTimer -= deltaTime;
        }

        #endregion

        #region Public API

        /// <summary>Returns the integer level (1–100) for the given skill.</summary>
        public int GetSkillLevel(SkyrimSkill skill) =>
            _skills.TryGetValue(skill, out var rec) ? rec.Level : 0;

        /// <summary>Returns normalized XP progress (0–1) toward the next level.</summary>
        public float GetSkillXPRatio(SkyrimSkill skill) =>
            _skills.TryGetValue(skill, out var rec) ? rec.XPRatio : 0f;

        /// <summary>Read-only view of all skill records.</summary>
        public IReadOnlyDictionary<SkyrimSkill, SkillRecord> AllSkills => _skills;

        /// <summary>
        /// Awards XP to a specific skill, raises events on level-up,
        /// then checks for overall character level increases.
        /// </summary>
        public void AwardXP(SkyrimSkill skill, float amount)
        {
            if (!_skills.TryGetValue(skill, out var rec)) return;

            GameEventManager.Publish(new SkillXPGainedEvent(skill, amount));

            bool leveledUp = rec.AddXP(amount);
            if (leveledUp)
            {
                GameEventManager.Publish(new SkillLeveledUpEvent(skill, rec.Level));
                CheckCharacterLevelUp();
            }
        }

        #endregion

        #region Event Handlers

        private void OnEntityDamaged(EntityDamagedEvent evt)
        {
            if (evt == null) return;

            // Attacker is Player → award offensive skill
            if (evt.Instigator != null && evt.Instigator.CompareTag("Player"))
                AwardOffensiveXP(evt.DamageType);

            // Target is Player → award defensive armor skill
            if (evt.Target != null && evt.Target.CompareTag("Player"))
                AwardXP(SkyrimSkill.LightArmor, 3f);
        }

        private void OnStealthStateChanged(StealthStateChangedEvent evt)
        {
            // Drip sneak XP only while actively crouching and hidden near an enemy
            if (evt == null || !evt.IsCrouching || evt.State != StealthState.Hidden) return;
            if (_sneakXPTimer > 0f) return;

            AwardXP(SkyrimSkill.Sneak, SneakXPPerInterval);
            _sneakXPTimer = SneakXPInterval;
        }

        #endregion

        #region Helpers

        private void AwardOffensiveXP(DamageType type)
        {
            // Magic damage → Destruction; physical → One-Handed (melee default for prototype)
            SkyrimSkill skill = type == DamageType.Magic
                ? SkyrimSkill.Destruction
                : SkyrimSkill.OneHanded;

            AwardXP(skill, 6f);
        }

        /// <summary>
        /// Character level = floor(sum of all skill levels / 15), matching Skyrim's rough formula.
        /// </summary>
        private int ComputeCharacterLevel()
        {
            int total = 0;
            foreach (var rec in _skills.Values)
                total += rec.Level;

            return Mathf.Max(1, total / 15);
        }

        private void CheckCharacterLevelUp()
        {
            int newLevel = ComputeCharacterLevel();
            if (newLevel <= _characterLevel) return;

            int gained = newLevel - _characterLevel;
            _perkPointsTotal += gained;
            _characterLevel   = newLevel;

            GameEventManager.Publish(new PlayerCharacterLeveledUpEvent(newLevel, _perkPointsTotal));
        }

        #endregion
    }
}
