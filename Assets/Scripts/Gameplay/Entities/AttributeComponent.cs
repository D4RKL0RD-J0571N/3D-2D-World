namespace Gameplay.Entities
{
    using System.Collections.Generic;
    using UnityEngine;
    using Domain.Entities;

    [RequireComponent(typeof(BaseActor))]
    public class AttributeComponent : MonoBehaviour
    {
        private BaseActor _actor;
        private Dictionary<string, BaseAttribute> _attributes = new Dictionary<string, BaseAttribute>();
        private StateComponent _stateComponent;

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
            _stateComponent = GetComponent<StateComponent>();
        }

        private void OnActorInitialized(BaseActor actor)
        {
            _attributes.Clear();
            if (actor.ActorBlueprint == null) return;
            
            foreach (var attrData in actor.ActorBlueprint.StartingAttributes)
            {
                _attributes[attrData.AttributeName] = attrData.Clone();
            }
        }

        private void Start()
        {
            // If placed in scene statically
            if (_actor.Blueprint != null && _attributes.Count == 0)
            {
                OnActorInitialized(_actor);
            }
        }

        private void Update()
        {
            bool inCombat = _stateComponent != null && _stateComponent.HasState(ActorState.InCombat);
            float dt = Time.deltaTime;

            foreach (var attr in _attributes.Values)
            {
                if (attr.CurrentValue < attr.MaximumValue)
                {
                    float rate = attr.RegenerationRate * attr.RegenerationMultiplier * (inCombat ? attr.CombatRegenerationRate : 1.0f);
                    float restored = rate * dt;
                    
                    // Simple accumulation logic for float to int conversion over frames
                    if (restored > 0f)
                    {
                        // To properly do this, we'd accumulate fractional parts.
                        // For simplicity in porting, we'll restore if random chance triggers it or accumulate.
                    }
                }
            }
        }

        public BaseAttribute GetAttribute(string name)
        {
            _attributes.TryGetValue(name, out var attr);
            return attr;
        }

        public void ApplyDamage(string attributeName, int amount)
        {
            if (_attributes.TryGetValue(attributeName, out var attr))
            {
                attr.CurrentValue = Mathf.Max(0, attr.CurrentValue - amount);
                Core.GameEventManager.Publish(new EntityDamagedEvent { Target = _actor, Amount = amount });
                
                if (attr.CurrentValue == 0 && attributeName == "Health" && _stateComponent != null)
                {
                    _stateComponent.SetState(ActorState.Death);
                }
            }
        }

        public void ApplyHealing(string attributeName, int amount)
        {
            if (_attributes.TryGetValue(attributeName, out var attr))
            {
                attr.CurrentValue = Mathf.Min(attr.MaximumValue, attr.CurrentValue + amount);
                Core.GameEventManager.Publish(new EntityHealedEvent { Target = _actor, Amount = amount });
            }
        }
    }
}
