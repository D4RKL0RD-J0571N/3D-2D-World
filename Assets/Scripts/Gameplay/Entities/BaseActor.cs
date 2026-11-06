namespace Gameplay.Entities
{
    using System.Collections;
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Core.Ids;
    using Data.Config;
    using Domain.Combat;
    using UnityEngine;

    /// <summary>
    /// Base actor shell coordinating peer components (attributes, stats, states) on an entity.
    /// Provides concrete combat reception methods adhering to direct component composition without forced interfaces.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Base Actor")]
    [DisallowMultipleComponent]
    public class BaseActor : BaseEntity
    {
        #region Fields

        private AttributeComponent _attributeComponent;
        private StatsComponent _statsComponent;

        #endregion

        #region Properties

        public ActorData ActorBlueprint => Blueprint as ActorData;

        public AttributeComponent Attributes
        {
            get
            {
                EnsureComponentsCached();
                return _attributeComponent;
            }
        }

        public StatsComponent Stats
        {
            get
            {
                EnsureComponentsCached();
                return _statsComponent;
            }
        }

        /// <summary>
        /// Runtime room/area assignment used by area-scheduling systems.
        /// </summary>
        public string RoomId { get; set; }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            EnsureComponentsCached();
        }

        protected virtual void OnEnable()
        {
            GameEventManager.AddListener<Domain.Persistence.EntityStateLoadedEvent>(OnEntityStateLoaded);

            if (!CompareTag("Player") && ServiceLocator.TryGet<Systems.Skyrim.StealthDetectionService>(out var stealth))
            {
                stealth.RegisterObserver(transform);
            }
        }

        protected virtual void OnDisable()
        {
            GameEventManager.RemoveListener<Domain.Persistence.EntityStateLoadedEvent>(OnEntityStateLoaded);

            if (ServiceLocator.TryGet<Systems.Skyrim.StealthDetectionService>(out var stealth))
            {
                stealth.UnregisterObserver(transform);
            }
        }

        private void OnEntityStateLoaded(Domain.Persistence.EntityStateLoadedEvent evt)
        {
            if (evt == null || evt.State == null) return;

            bool isTarget = (CompareTag("Player") && evt.State.EntityId == "Player") ||
                            (!string.IsNullOrEmpty(evt.State.InstanceId) && evt.State.InstanceId == name);

            if (!isTarget) return;

            EnsureComponentsCached();

            if (Stats != null && evt.State.Level > 0)
            {
                Stats.SetLevel(evt.State.Level);
                Stats.SetExperience(evt.State.Experience);
            }

            if (Attributes != null && evt.State.CurrentHealth > 0)
            {
                var health = Attributes.GetAttribute("Health");
                if (health != null)
                {
                    health.CurrentValue = Mathf.Clamp(evt.State.CurrentHealth, 0, health.MaximumValue);
                }
            }

            if (TryGetComponent<InventoryComponent>(out var inventory) && evt.State.Inventory != null)
            {
                inventory.RestoreSaveData(evt.State.Inventory, ResolveItemDefinition);
            }
        }

        private static ItemData ResolveItemDefinition(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            if (ServiceLocator.TryGet<IContentService>(out var content))
            {
                if (ResourceId.TryParse(id, out ResourceId canonicalId) &&
                    content.TryResolve(canonicalId, out ItemData byCanonical))
                {
                    return byCanonical;
                }

                if (content.TryResolveByLegacyAlias(id, out ItemData byAlias))
                {
                    return byAlias;
                }
            }

            return Resources.Load<ItemData>($"Definitions/ItemData/{id}");
        }

        #endregion

        #region Component Resolution

        protected void EnsureComponentsCached()
        {
            if (_attributeComponent == null) _attributeComponent = GetComponent<AttributeComponent>();
            if (_statsComponent == null) _statsComponent = GetComponent<StatsComponent>();
        }

        #endregion

        #region Initialization

        public override void Initialize(EntityData blueprint)
        {
            if (!(blueprint is ActorData))
            {
                Debug.LogError($"[BaseActor] Cannot initialize with non-ActorData blueprint on {name}!");
                return;
            }

            base.Initialize(blueprint);

            EnsureComponentsCached();
            _attributeComponent?.Initialize(this);
            _statsComponent?.Initialize(this);
        }

        #endregion

        #region Combat API

        /// <summary>
        /// Applies incoming damage to this actor's attributes and executes hit reactions.
        /// Evaluates sneak attack multipliers if attacker strikes undetected from stealth
        /// and publishes <see cref="SneakAttackTriggeredEvent"/> for progression/audio listeners.
        /// </summary>
        public virtual void TakeDamage(DamageInfo damage)
        {
            if (CompareTag("Player") && Systems.Console.GodModeState.IsGodModeActive)
            {
                return;
            }

            EnsureComponentsCached();

            int finalDamage = damage.Amount;

            if (damage.Instigator != null && damage.Instigator.CompareTag("Player") && !CompareTag("Player"))
            {
                if (ServiceLocator.TryGet<Systems.Skyrim.StealthDetectionService>(out var stealth) &&
                    stealth.CurrentState == Domain.Skyrim.StealthState.Hidden)
                {
                    float multiplier = 3.0f;
                    finalDamage = Mathf.RoundToInt(damage.Amount * multiplier);

                    GameEventManager.Publish(new SneakAttackTriggeredEvent(
                        damage.Instigator, this, multiplier, finalDamage));

                    Debug.Log($"<color=orange>✦ SNEAK ATTACK! Critical {multiplier:0.0}x damage ({finalDamage}) to {name}!</color>");
                }
            }

            if (_attributeComponent != null)
            {
                _attributeComponent.ApplyDamage("Health", finalDamage, damage.Instigator, damage.Type);
            }

            PlayHitReaction(damage);
        }

        protected virtual void PlayHitReaction(DamageInfo damage)
        {
            if (Application.isPlaying && gameObject.activeInHierarchy)
            {
                StartCoroutine(FlashDamageRoutine());
            }
        }

        private IEnumerator FlashDamageRoutine()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) yield break;

            var origColors = new Dictionary<Renderer, Color>();
            foreach (var r in renderers)
            {
                if (r == null) continue;

                if (r is SpriteRenderer sr)
                {
                    origColors[r] = sr.color;
                    sr.color = Color.red;
                }
                else if (r.sharedMaterial != null && r.material != null)
                {
                    if (r.material.HasProperty("_BaseColor"))
                    {
                        origColors[r] = r.material.GetColor("_BaseColor");
                        r.material.SetColor("_BaseColor", Color.red);
                    }
                    else if (r.material.HasProperty("_Color"))
                    {
                        origColors[r] = r.material.color;
                        r.material.color = Color.red;
                    }
                }
            }

            yield return new WaitForSeconds(0.12f);

            foreach (var kvp in origColors)
            {
                if (kvp.Key == null) continue;

                if (kvp.Key is SpriteRenderer sr)
                {
                    sr.color = kvp.Value;
                }
                else if (kvp.Key.material != null)
                {
                    if (kvp.Key.material.HasProperty("_BaseColor"))
                    {
                        kvp.Key.material.SetColor("_BaseColor", kvp.Value);
                    }
                    else if (kvp.Key.material.HasProperty("_Color"))
                    {
                        kvp.Key.material.color = kvp.Value;
                    }
                }
            }
        }

        #endregion
    }
}
