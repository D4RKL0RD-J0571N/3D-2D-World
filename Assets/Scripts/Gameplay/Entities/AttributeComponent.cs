namespace Gameplay.Entities
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Domain.Entities;
    using Core;
    using Core.Ids;
    using Core.Persistence;
    using Newtonsoft.Json;

    /// <summary>
    /// Manages runtime attributes (Health, Stamina, Mana) for an actor,
    /// handling lossless sub-step float regeneration, damage, and healing.
    /// Implements <see cref="ISaveableComponent"/> for deterministic persistence.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Attribute Component")]
    [RequireComponent(typeof(BaseActor))]
    public class AttributeComponent : MonoBehaviour, ISaveableComponent
    {
        #region Fields

        private BaseActor _actor;
        private StateComponent _stateComponent;
        private readonly Dictionary<string, BaseAttribute> _attributes = new Dictionary<string, BaseAttribute>();
        private readonly Dictionary<string, float> _regenAccumulators = new Dictionary<string, float>();
        private bool _isDead;

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
            else if (_attributes.Count == 0)
            {
                EnsureDefaultAttributes();
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
            _isDead = false;

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

        public float CurrentHealth
        {
            get
            {
                EnsureDefaultAttributes();
                return GetAttribute("Health")?.CurrentValue ?? 0f;
            }
        }

        public float MaxHealth
        {
            get
            {
                EnsureDefaultAttributes();
                return GetAttribute("Health")?.MaximumValue ?? 0f;
            }
        }

        /// <summary>
        /// Ensures default RPG attributes exist on this component if not hydrated.
        /// </summary>
        public void EnsureDefaultAttributes()
        {
            if (_attributes.Count == 0)
            {
                RegisterAttribute(new BaseAttribute { AttributeName = "Health", MaximumValue = 100, CurrentValue = 100, RegenerationRate = 1f });
                RegisterAttribute(new BaseAttribute { AttributeName = "Stamina", MaximumValue = 100, CurrentValue = 100, RegenerationRate = 5f });
                RegisterAttribute(new BaseAttribute { AttributeName = "Magicka", MaximumValue = 100, CurrentValue = 100, RegenerationRate = 5f });
            }
        }

        /// <summary>
        /// Registers or replaces an attribute dynamically at runtime.
        /// </summary>
        public void RegisterAttribute(BaseAttribute attribute)
        {
            if (attribute == null || string.IsNullOrEmpty(attribute.AttributeName)) return;
            _attributes[attribute.AttributeName] = attribute;
            _regenAccumulators[attribute.AttributeName] = 0f;
        }

        /// <summary>
        /// Retrieves an attribute by name, or null if not registered.
        /// </summary>
        public BaseAttribute GetAttribute(string name)
        {
            EnsureDefaultAttributes();
            _attributes.TryGetValue(name, out var attr);
            return attr;
        }

        /// <summary>
        /// Applies damage to an attribute, clamping at 0 and triggering death state on Health depletion.
        /// </summary>
        public void ApplyDamage(string attributeName, int amount, GameObject instigator = null, Domain.Combat.DamageType damageType = Domain.Combat.DamageType.Physical)
        {
            EnsureDefaultAttributes();
            if (_attributes.TryGetValue(attributeName, out var attr))
            {
                attr.CurrentValue = Mathf.Max(0, attr.CurrentValue - amount);

                // Only broadcast the damage event for Health — stamina consumption
                // is an internal resource cost, not external damage.
                if (attributeName == "Health")
                {
                    if (_actor == null) _actor = GetComponent<BaseActor>();

                    GameEventManager.Publish(new EntityDamagedEvent 
                    { 
                        Target = _actor, 
                        Amount = amount,
                        Instigator = instigator,
                        DamageType = damageType
                    });

                    if (attr.CurrentValue == 0 && !_isDead)
                    {
                        _isDead = true;
                        if (_stateComponent != null)
                        {
                            _stateComponent.SetState(ActorState.Death);
                        }

                        GameEventManager.Publish(new EntityDiedEvent
                        {
                            Victim = _actor,
                            Killer = instigator
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Restores an attribute's value up to its maximum value.
        /// </summary>
        public void ApplyHealing(string attributeName, int amount)
        {
            EnsureDefaultAttributes();
            if (_attributes.TryGetValue(attributeName, out var attr))
            {
                attr.CurrentValue = Mathf.Min(attr.MaximumValue, attr.CurrentValue + amount);
                if (attributeName == "Health" && attr.CurrentValue > 0)
                {
                    _isDead = false;
                }
                GameEventManager.Publish(new EntityHealedEvent { Target = _actor, Amount = amount });
            }
        }

        /// <summary>Convenience alias for ApplyHealing.</summary>
        public void Heal(string attributeName, int amount) => ApplyHealing(attributeName, amount);

        #endregion

        #region ISaveableComponent Implementation

        [Serializable]
        private sealed class AttributeSaveDto
        {
            public List<AttributeRecord> Attributes = new();
        }

        [Serializable]
        private sealed class AttributeRecord
        {
            public string Name;
            public int CurrentValue;
            public int MaximumValue;
        }

        public string ComponentSaveKey => "attributes";

        public object CaptureComponentState()
        {
            var dto = new AttributeSaveDto();
            foreach (var kvp in _attributes)
            {
                dto.Attributes.Add(new AttributeRecord
                {
                    Name = kvp.Key,
                    CurrentValue = kvp.Value.CurrentValue,
                    MaximumValue = kvp.Value.MaximumValue
                });
            }
            return dto;
        }

        public void RestoreComponentState(string serializedJsonState, IWorldInstanceResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(serializedJsonState)) return;

            var settings = FantasyEngineJsonSettings.CreateDefault();
            var dto = JsonConvert.DeserializeObject<AttributeSaveDto>(serializedJsonState, settings);
            if (dto?.Attributes == null) return;

            for (int i = 0; i < dto.Attributes.Count; i++)
            {
                AttributeRecord rec = dto.Attributes[i];
                if (string.IsNullOrWhiteSpace(rec.Name)) continue;

                if (!_attributes.TryGetValue(rec.Name, out BaseAttribute attr))
                {
                    int maxVal = rec.MaximumValue > 0 ? rec.MaximumValue : 100;
                    attr = new BaseAttribute
                    {
                        AttributeName = rec.Name,
                        MaximumValue = maxVal,
                        CurrentValue = maxVal
                    };
                    _attributes[rec.Name] = attr;
                }

                if (rec.MaximumValue > 0)
                {
                    attr.MaximumValue = rec.MaximumValue;
                }
                attr.CurrentValue = Mathf.Clamp(rec.CurrentValue, 0, attr.MaximumValue);
            }
        }

        #endregion
    }
}
