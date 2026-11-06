namespace Gameplay.Entities
{
    using System;
    using UnityEngine;
    using Domain.Entities;
    using Core;
    using Core.Ids;
    using Core.Persistence;
    using Newtonsoft.Json;

    /// <summary>
    /// Manages an actor's experience, leveling, perk progression, and XP curve calculations.
    /// Implements <see cref="ISaveableComponent"/> for deterministic persistence.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Stats Component")]
    [RequireComponent(typeof(BaseActor))]
    public class StatsComponent : MonoBehaviour, ISaveableComponent
    {
        #region Fields

        private BaseActor _actor;

        [SerializeField] private int _level;
        [SerializeField] private float _experience;
        [SerializeField] private int _perkPoints;
        [SerializeField] private float _xpNeeded;
        [SerializeField] private bool _canLevelUp;

        #endregion

        #region Properties

        public int Level => _level;
        public float Experience => _experience;
        public int PerkPoints => _perkPoints;
        public float XpNeeded => _xpNeeded;
        public bool CanLevelUp => _canLevelUp;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
        }

        private void Start()
        {
            if (_actor != null && _actor.ActorBlueprint != null && _level == 0)
            {
                Initialize(_actor);
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Explicit, lossless initialization of stats from an actor blueprint.
        /// </summary>
        public void Initialize(BaseActor actor)
        {
            _actor = actor;
            if (actor == null || actor.ActorBlueprint == null) return;

            _level = actor.ActorBlueprint.StartingLevel;
            _experience = 0f;
            _perkPoints = 0;
            _xpNeeded = CalculateXpRequiredForLevelUp();
            _canLevelUp = false;
        }

        /// <summary>
        /// Backward-compatible alias for legacy event/message dispatches.
        /// </summary>
        public void OnActorInitialized(BaseActor actor) => Initialize(actor);

        #endregion

        #region Progression Logic

        public void GainExperience(float amount)
        {
            _experience += amount;
            CheckLevelUpEligibility();
        }

        public void SetLevel(int level)
        {
            _level = Mathf.Max(1, level);
            CheckLevelUpEligibility();
        }

        public void SetExperience(float experience)
        {
            _experience = Mathf.Max(0f, experience);
            CheckLevelUpEligibility();
        }

        private void CheckLevelUpEligibility()
        {
            _xpNeeded = CalculateXpRequiredForLevelUp();
            _canLevelUp = _experience >= _xpNeeded;
        }

        private float CalculateXpRequiredForLevelUp()
        {
            if (_actor == null || _actor.ActorBlueprint == null) return 75f;
            return _actor.ActorBlueprint.XpLevelUpBase + (_level * _actor.ActorBlueprint.XpLevelUpMult);
        }

        public void LevelUp()
        {
            if (!_canLevelUp) return;

            _experience -= _xpNeeded;
            _level++;
            _perkPoints++;

            CheckLevelUpEligibility();

            GameEventManager.Publish(new EntityLevelUpEvent { Target = _actor, NewLevel = _level });
        }

        public int GetLevelFromTotalXp(float totalXp)
        {
            return Mathf.FloorToInt(-2.5f + Mathf.Sqrt(8 * totalXp + 1225) / 10);
        }

        #endregion

        #region ISaveableComponent Implementation

        [Serializable]
        private sealed class StatsSaveDto
        {
            public int Level = 1;
            public float Experience;
            public int PerkPoints;
        }

        public string ComponentSaveKey => "stats";

        public object CaptureComponentState() => new StatsSaveDto
        {
            Level = _level,
            Experience = _experience,
            PerkPoints = _perkPoints
        };

        public void RestoreComponentState(string serializedJsonState, IWorldInstanceResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(serializedJsonState)) return;

            var settings = FantasyEngineJsonSettings.CreateDefault();
            var dto = JsonConvert.DeserializeObject<StatsSaveDto>(serializedJsonState, settings);
            if (dto == null) return;

            _level = Mathf.Max(1, dto.Level);
            _experience = Mathf.Max(0f, dto.Experience);
            _perkPoints = Mathf.Max(0, dto.PerkPoints);
            CheckLevelUpEligibility();
        }

        #endregion
    }
}
