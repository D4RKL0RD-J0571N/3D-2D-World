namespace Domain.Skyrim
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Type of active magic or potion effect applied to the player.
    /// </summary>
    public enum ActiveEffectType
    {
        FortifyRestoration,
        FortifyAlchemy,
        FortifyEnchanting,
        FortifySmithing,
        FortifyHealth,
        BlessingOfMara,
        WellRested
    }

    /// <summary>
    /// Represents an active potion or enchantment buff on an actor with duration and magnitude.
    /// </summary>
    [Serializable]
    public class ActiveEffect
    {
        public ActiveEffectType Type;
        public string Name;
        public float Magnitude;
        public float Duration;
        public float RemainingTime;
        public bool IsPermanent;

        public ActiveEffect(ActiveEffectType type, string name, float magnitude, float duration)
        {
            Type = type;
            Name = name;
            Magnitude = magnitude;
            Duration = duration;
            RemainingTime = duration;
            IsPermanent = duration <= 0f;
        }
    }

    /// <summary>
    /// Coordinates active potion/enchantment effects and manages the legendary Skyrim
    /// Fortify Restoration potion loop exploit. In unpatched vanilla Skyrim, Fortify Restoration
    /// effects mistakenly amplify active apparel enchantments (like Fortify Alchemy), allowing
    /// exponential compounding of potion and enchantment potencies into millions of percent.
    /// </summary>
    public class FortifyRestorationLoopManager
    {
        #region Fields

        private readonly List<ActiveEffect> _activeEffects = new();
        private bool _isUSSEPPatchActive;
        private float _equippedApparelAlchemyBase = 0.15f; // e.g. 15% Fortify Alchemy gear
        private float _currentGearAlchemyBonus = 0.15f;

        #endregion

        #region Properties

        /// <summary>When true, replicates the Unofficial Skyrim Patch which fixes the restoration loop.</summary>
        public bool IsUSSEPPatchActive
        {
            get => _isUSSEPPatchActive;
            set => _isUSSEPPatchActive = value;
        }

        /// <summary>Base un-amplified Fortify Alchemy enchantment percentage on equipped apparel.</summary>
        public float EquippedApparelAlchemyBase
        {
            get => _equippedApparelAlchemyBase;
            set
            {
                _equippedApparelAlchemyBase = Mathf.Max(0f, value);
                RecalculateApparelEnchantments();
            }
        }

        /// <summary>Current effective Fortify Alchemy bonus after applying Fortify Restoration amplification.</summary>
        public float CurrentEffectiveAlchemyBonus => _currentGearAlchemyBonus;

        /// <summary>Read-only view of currently active potion and blessing effects.</summary>
        public IReadOnlyList<ActiveEffect> ActiveEffects => _activeEffects;

        #endregion

        #region Public API

        /// <summary>
        /// Applies an active effect (such as drinking a potion or receiving a shrine blessing).
        /// </summary>
        public void ApplyEffect(ActiveEffect effect)
        {
            if (effect == null) return;
            _activeEffects.RemoveAll(e => e.Type == effect.Type && !e.IsPermanent);
            _activeEffects.Add(effect);

            if (effect.Type == ActiveEffectType.FortifyRestoration && !_isUSSEPPatchActive)
            {
                RecalculateApparelEnchantments();
            }
        }

        /// <summary>
        /// Simulates unequipping and re-equipping apparel to lock in the boosted enchantment values.
        /// In vanilla Skyrim, unequipping and re-equipping apparel forces the game engine to recalculate
        /// enchantment magnitudes using the active Fortify Restoration multiplier.
        /// </summary>
        public void UnequipAndReequipApparel()
        {
            RecalculateApparelEnchantments();
        }

        /// <summary>
        /// Simulates brewing a potion with the current effective alchemy multiplier.
        /// </summary>
        /// <param name="baseMagnitude">Base potency of the potion (e.g. 0.20 for 20%).</param>
        /// <param name="type">The type of potion to brew.</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <returns>The newly created potion effect with amplified magnitude.</returns>
        public ActiveEffect BrewPotion(ActiveEffectType type, float baseMagnitude, float duration = 60f)
        {
            float effectiveMagnitude = baseMagnitude * (1f + _currentGearAlchemyBonus);
            string potionName = $"Potion of {type}";
            return new ActiveEffect(type, potionName, effectiveMagnitude, duration);
        }

        /// <summary>
        /// Advances active effect timers and removes expired buffs.
        /// </summary>
        public void Update(float deltaTime)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var eff = _activeEffects[i];
                if (eff.IsPermanent) continue;

                eff.RemainingTime -= deltaTime;
                if (eff.RemainingTime <= 0f)
                {
                    _activeEffects.RemoveAt(i);
                    if (eff.Type == ActiveEffectType.FortifyRestoration)
                    {
                        RecalculateApparelEnchantments();
                    }
                }
            }
        }

        /// <summary>
        /// Clears all active effects and resets apparel enchantments to base.
        /// </summary>
        public void Clear()
        {
            _activeEffects.Clear();
            _currentGearAlchemyBonus = _equippedApparelAlchemyBase;
        }

        #endregion

        #region Helpers

        private void RecalculateApparelEnchantments()
        {
            if (_isUSSEPPatchActive)
            {
                _currentGearAlchemyBonus = _equippedApparelAlchemyBase;
                return;
            }

            float restorationMultiplier = 1f;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Type == ActiveEffectType.FortifyRestoration)
                {
                    restorationMultiplier += _activeEffects[i].Magnitude;
                }
            }

            _currentGearAlchemyBonus = _equippedApparelAlchemyBase * restorationMultiplier;
        }

        #endregion
    }
}
