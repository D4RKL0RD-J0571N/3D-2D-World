namespace Gameplay.Entities
{
    using System.Collections;
    using UnityEngine;
    using Core;
    using Data.Config;
    using Domain.Combat;

    /// <summary>
    /// Represents non-living gatherable world nodes (trees, rock boulders) that can be
    /// struck via the combat/weapon pipeline using <see cref="DamageInfo"/>.
    /// Handles hit reactions, strict anti-duplication depleted state, and physical item drop spawning.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Harvestable Node")]
    [DisallowMultipleComponent]
    public class HarvestableNode : BaseEntity
    {
        #region Fields

        [Header("Integrity")]
        [SerializeField] private int _maxIntegrity = 50;
        [SerializeField] private int _currentIntegrity = 50;

        [Header("Harvest Drops")]
        [SerializeField] private ItemData _dropItem;
        [SerializeField] private int _dropAmount = 3;
        [SerializeField] private GameObject _dropPrefab;

        [Header("Tool Requirements")]
        [SerializeField] private DamageType _effectiveDamageType = DamageType.Physical;
        [SerializeField] private float _ineffectiveDamageMultiplier = 0.5f;

        [Header("State")]
        private bool _isDepleted;
        private Collider _cachedCollider;

        #endregion

        #region Properties

        public int MaxIntegrity => _maxIntegrity;
        public int CurrentIntegrity => _currentIntegrity;
        public bool IsDepleted => _isDepleted;
        public ItemData DropItem => _dropItem;
        public int DropAmount => _dropAmount;

        public override string InteractionPrompt => !_isDepleted && Blueprint != null
            ? $"Harvest {Blueprint.DisplayName} (Swing weapon/tool to chop/mine)"
            : string.Empty;

        #endregion

        #region Configuration

        /// <summary>
        /// Configures the harvestable node dynamically (e.g. from procedural generators).
        /// </summary>
        public void ConfigureNode(int maxIntegrity, ItemData dropItem, int dropAmount, GameObject dropPrefab = null)
        {
            _maxIntegrity = maxIntegrity;
            _currentIntegrity = maxIntegrity;
            _dropItem = dropItem;
            _dropAmount = dropAmount;
            _dropPrefab = dropPrefab;
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            _cachedCollider = GetComponent<Collider>();
            _currentIntegrity = _maxIntegrity;
        }

        #endregion

        #region Combat / Gathering API

        /// <summary>
        /// Applies damage from a weapon strike. Reuses the shared DamageInfo value object.
        /// Guaranteed exactly-once drop behavior upon reaching 0 integrity.
        /// </summary>
        /// <param name="damage">The incoming combat damage information.</param>
        public virtual void TakeDamage(DamageInfo damage)
        {
            if (_isDepleted) return;

            int effectiveAmount = damage.Amount;
            if (damage.Type != _effectiveDamageType)
            {
                effectiveAmount = Mathf.Max(1, Mathf.RoundToInt(damage.Amount * _ineffectiveDamageMultiplier));
            }

            _currentIntegrity = Mathf.Max(0, _currentIntegrity - effectiveAmount);

            PlayHitReaction();

            bool depletedNow = _currentIntegrity <= 0;
            if (depletedNow)
            {
                DepleteNode();
            }

            GameEventManager.Publish(new HarvestNodeDamagedEvent
            {
                NodeObject = gameObject,
                DamageAmount = effectiveAmount,
                RemainingIntegrity = _currentIntegrity,
                IsDepleted = depletedNow
            });
        }

        #endregion

        #region Depletion & Drops

        private void DepleteNode()
        {
            if (_isDepleted) return;
            _isDepleted = true;

            // Immediately disable collider to prevent any further hits or duplication
            if (_cachedCollider != null)
            {
                _cachedCollider.enabled = false;
            }

            SpawnHarvestDrops();

            // Despawn or shrink
            StartCoroutine(DepletionRoutine());
        }

        private void SpawnHarvestDrops()
        {
            if (_dropItem == null || _dropAmount <= 0) return;

            Vector3 spawnBase = transform.position + Vector3.up * 0.5f;

            for (int i = 0; i < _dropAmount; i++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-0.6f, 0.6f),
                    Random.Range(0.2f, 0.6f),
                    Random.Range(-0.6f, 0.6f)
                );

                GameObject dropObj;
                if (_dropPrefab != null)
                {
                    dropObj = Instantiate(_dropPrefab, spawnBase + offset, Quaternion.identity);
                }
                else
                {
                    // Fallback world item
                    dropObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    dropObj.transform.position = spawnBase + offset;
                    dropObj.transform.localScale = Vector3.one * 0.35f;
                    dropObj.name = $"Dropped_{_dropItem.DisplayName}";
                }

                var baseItem = dropObj.GetComponent<BaseItem>();
                if (baseItem == null)
                {
                    baseItem = dropObj.AddComponent<BaseItem>();
                }
                baseItem.Initialize(_dropItem);

                // Add slight scatter impulse if rigidbody exists or add one
                var rb = dropObj.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = dropObj.AddComponent<Rigidbody>();
                }
                rb.AddForce(new Vector3(offset.x * 3f, 4f, offset.z * 3f), ForceMode.Impulse);
            }
        }

        private void PlayHitReaction()
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(HitShakeRoutine());
            }
        }

        private IEnumerator HitShakeRoutine()
        {
            Vector3 originalPos = transform.position;
            float elapsed = 0f;
            float duration = 0.12f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float xOffset = Random.Range(-0.06f, 0.06f);
                float zOffset = Random.Range(-0.06f, 0.06f);
                transform.position = originalPos + new Vector3(xOffset, 0, zOffset);
                yield return null;
            }

            transform.position = originalPos;
        }

        private IEnumerator DepletionRoutine()
        {
            float elapsed = 0f;
            float duration = 0.25f;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            Destroy(gameObject);
        }

        #endregion
    }
}
