namespace Gameplay.Entities
{
    using UnityEngine;
    using Domain.Entities;

    [RequireComponent(typeof(BaseActor))]
    public class StatsComponent : MonoBehaviour
    {
        private BaseActor _actor;

        [SerializeField] private int _level;
        [SerializeField] private float _experience;
        [SerializeField] private int _perkPoints;
        [SerializeField] private float _xpNeeded;
        [SerializeField] private bool _canLevelUp;

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
        }

        private void OnActorInitialized(BaseActor actor)
        {
            if (actor.ActorBlueprint == null) return;
            
            _level = actor.ActorBlueprint.StartingLevel;
            _experience = 0f;
            _perkPoints = 0;
            _xpNeeded = CalculateXpRequiredForLevelUp();
            _canLevelUp = false;
        }

        private void Start()
        {
            if (_actor.ActorBlueprint != null && _level == 0)
            {
                OnActorInitialized(_actor);
            }
        }

        public int Level => _level;
        public float Experience => _experience;
        public int PerkPoints => _perkPoints;
        public float XpNeeded => _xpNeeded;
        public bool CanLevelUp => _canLevelUp;

        public void GainExperience(float amount)
        {
            _experience += amount;
            CheckLevelUpEligibility();
        }

        private void CheckLevelUpEligibility()
        {
            _xpNeeded = CalculateXpRequiredForLevelUp();
            _canLevelUp = _experience >= _xpNeeded;
        }

        private float CalculateXpRequiredForLevelUp()
        {
            if (_actor.ActorBlueprint == null) return 75f;
            return _actor.ActorBlueprint.XpLevelUpBase + (_level * _actor.ActorBlueprint.XpLevelUpMult);
        }

        public void LevelUp()
        {
            if (!_canLevelUp) return;

            _experience -= _xpNeeded;
            _level++;
            _perkPoints++;
            
            CheckLevelUpEligibility();

            Core.GameEventManager.Publish(new EntityLevelUpEvent { Target = _actor, NewLevel = _level });
        }

        public int GetLevelFromTotalXp(float totalXp)
        {
            // Ported directly from previous project formula
            return Mathf.FloorToInt(-2.5f + Mathf.Sqrt(8 * totalXp + 1225) / 10);
        }
    }
}
