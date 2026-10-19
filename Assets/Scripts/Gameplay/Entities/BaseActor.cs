namespace Gameplay.Entities
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Data.Config;
    using Domain.Combat;

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
            Core.GameEventManager.AddListener<Domain.Persistence.EntityStateLoadedEvent>(OnEntityStateLoaded);
        }

        protected virtual void OnDisable()
        {
            Core.GameEventManager.RemoveListener<Domain.Persistence.EntityStateLoadedEvent>(OnEntityStateLoaded);
        }

        private void OnEntityStateLoaded(Domain.Persistence.EntityStateLoadedEvent evt)
        {
            if (evt == null || evt.State == null) return;

            // Check if this event targets this entity (e.g., "Player" or matching InstanceId)
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
                inventory.RestoreSaveData(evt.State.Inventory, id =>
                {
#if UNITY_EDITOR
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<Data.Config.ItemData>(
                        $"Assets/Data/Definitions/ItemData/Item_{id.Replace("ITM_", "")}.asset") ??
                        Resources.Load<Data.Config.ItemData>($"Definitions/ItemData/{id}");
#else
                    return Resources.Load<Data.Config.ItemData>($"Definitions/ItemData/{id}");
#endif
                });
            }
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

            // Direct, lossless initialization of peer components
            EnsureComponentsCached();
            _attributeComponent?.Initialize(this);
            _statsComponent?.Initialize(this);
        }

        #endregion

        #region Combat API

        /// <summary>
        /// Applies incoming damage to this actor's attributes and executes hit reactions.
        /// Concrete peer method adhering to direct component composition without forced interfaces.
        /// </summary>
        public virtual void TakeDamage(DamageInfo damage)
        {
            EnsureComponentsCached();

            if (_attributeComponent != null)
            {
                _attributeComponent.ApplyDamage("Health", damage.Amount);
            }

            PlayHitReaction(damage);
        }

        protected virtual void PlayHitReaction(DamageInfo damage)
        {
            if (gameObject.activeInHierarchy)
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
