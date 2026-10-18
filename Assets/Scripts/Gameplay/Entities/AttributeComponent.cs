namespace Gameplay.Entities
{
    using System.Collections.Generic;
    using UnityEngine;
    using Domain.Entities;
    using Core;

    /// <summary>
    /// Manages runtime attributes (Health, Stamina, Mana) for an actor,
    /// handling lossless sub-step float regeneration, damage, and healing.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Attribute Component")]
    [RequireComponent(typeof(BaseActor))]
    public class AttributeComponent : MonoBehaviour
    {
        #region Fields

        private BaseActor _actor;
        private StateComponent _stateComponent;
        private readonly Dictionary<string, BaseAttribute> _attributes = new Dictionary<string, BaseAttribute>();
        private readonly Dictionary<string, float> _regenAccumulators = new Dictionary<string, float>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
            _stateComponent = GetComponent<StateComponent>();
        }

        private void Start()
        {
            // If placed in scene statically and not initialized yet
            if (_actor != null && _actor.Blueprint != null && _attributes.Count == 0)
            {
                Initialize(_actor);
            }
        }

        private void Update()
        {
            ProcessRegeneration(Time.deltaTime);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Explicit, lossless initialization of attributes from an actor's blueprint.
        /// </summary>
        /// <param name="actor">The owning actor containing the blueprint.</param>
        public void Initialize(BaseActor actor)
        {
            _attributes.Clear();
            _regenAccumulators.Clear();

            if (actor == null || actor.ActorBlueprint == null) return;

            foreach (var attrData in actor.ActorBlueprint.StartingAttributes)
            {
                if (attrData == null || string.IsNullOrEmpty(attrData.AttributeName)) continue;
                _attributes[attrData.AttributeName] = attrData.Clone();
                _regenAccumulators[attrData.AttributeName] = 0f;
            }
        }

        /// <summary>
        /// Backward-compatible alias for legacy event/message dispatches.
        /// </summary>
        public void OnActorInitialized(BaseActor actor) => Initialize(actor);

        #endregion

        #region Regeneration Logic

        private void ProcessRegeneration(float deltaTime)
        {
            if (_attributes.Count == 0) return;

            bool inCombat = _stateComponent != null && _stateComponent.HasState(ActorState.InCombat);

            foreach (var attr in _attributes.Values)
            {
                if (attr.CurrentValue >= attr.MaximumValue)
                {
                    _regenAccumulators[attr.AttributeName] = 0f;
                    continue;
                }

                float combatFactor = inCombat ? attr.CombatRegenerationRate : 1.0f;
                float rate = attr.RegenerationRate * attr.RegenerationMultiplier * combatFactor;

                if (rate <= 0f) continue;

                if (!_regenAccumulators.TryGetValue(attr.AttributeName, out float accumulated))
                {
                    accumulated = 0f;
                }

                accumulated += rate * deltaTime;

                if (accumulated >= 1.0f)
                {
                    int wholeUnits = Mathf.FloorToInt(accumulated);
                    accumulated -= wholeUnits;
                    attr.CurrentValue = Mathf.Min(attr.MaximumValue, attr.CurrentValue + wholeUnits);
                }

                _regenAccumulators[attr.AttributeName] = accumulated;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Retrieves an attribute by name, or null if not registered.
        /// </summary>
        public BaseAttribute GetAttribute(string name)
        {
            _attributes.TryGetValue(name, out var attr);
            return attr;
        }

        /// <summary>
        /// Applies damage to an attribute, clamping at 0 and triggering death state on Health depletion.
        /// </summary>
        public void ApplyDamage(string attributeName, int amount)
        {
            if (_attributes.TryGetValue(attributeName, out var attr))
            {
                attr.CurrentValue = Mathf.Max(0, attr.CurrentValue - amount);
                GameEventManager.Publish(new EntityDamagedEvent { Target = _actor, Amount = amount });

                if (attr.CurrentValue == 0 && attributeName == "Health" && _stateComponent != null)
                {
                    _stateComponent.SetState(ActorState.Death);
                }
            }
        }

        /// <summary>
        /// Restores an attribute's value up to its maximum value.
        /// </summary>
        public void ApplyHealing(string attributeName, int amount)
        {
            if (_attributes.TryGetValue(attributeName, out var attr))
            {
                attr.CurrentValue = Mathf.Min(attr.MaximumValue, attr.CurrentValue + amount);
                GameEventManager.Publish(new EntityHealedEvent { Target = _actor, Amount = amount });
            }
        }

        #endregion
    }
}
