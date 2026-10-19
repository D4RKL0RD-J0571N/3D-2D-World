namespace Gameplay.Combat
{
    using System.Collections;
    using UnityEngine;
    using Core;
    using Domain.Combat;
    using Gameplay.Entities;
    using Gameplay.Player;

    /// <summary>
    /// Coordinates melee attack execution, stamina consumption, and collision detection.
    /// Operates via direct peer-component coordination and event subscriptions.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Combat/Weapon Component")]
    [RequireComponent(typeof(BaseActor))]
    public class WeaponComponent : MonoBehaviour
    {
        #region Fields

        [Header("Melee Parameters")]
        [SerializeField] private float _attackRange = 2.5f;
        [SerializeField] private float _attackRadius = 0.6f;
        [SerializeField] private int _damageAmount = 25;
        [SerializeField] private float _attackCooldown = 0.4f;

        [Header("Stamina Costs")]
        [SerializeField] private int _staminaCost = 20;
        [SerializeField] private int _minStaminaRequired = 10;

        [Header("Targeting")]
        [SerializeField] private LayerMask _hitMask = ~0;

        private BaseActor _actor;
        private AttributeComponent _attributeComponent;
        private UnityEngine.Camera _cachedCamera;
        private float _lastAttackTime = -10f;
        private bool _isAttacking;

        #endregion

        #region Properties

        public bool CanAttack
        {
            get
            {
                if (_isAttacking || Time.time < _lastAttackTime + _attackCooldown)
                    return false;

                if (_attributeComponent == null)
                    _attributeComponent = GetComponent<AttributeComponent>();

                var stamina = _attributeComponent?.GetAttribute("Stamina");
                if (stamina != null && stamina.CurrentValue < _minStaminaRequired)
                    return false;

                return true;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
            _attributeComponent = GetComponent<AttributeComponent>();
        }

        private void Start()
        {
            ResolveCamera();
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<PlayerInteractPrimaryInputEvent>(OnPrimaryInteract);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<PlayerInteractPrimaryInputEvent>(OnPrimaryInteract);
        }

        #endregion

        #region Camera Resolution

        private void ResolveCamera()
        {
            if (_cachedCamera == null)
            {
                _cachedCamera = GetComponentInChildren<UnityEngine.Camera>();
                if (_cachedCamera == null)
                {
                    _cachedCamera = UnityEngine.Camera.main;
                }
            }
        }

        #endregion

        #region Input Handling

        private void OnPrimaryInteract(PlayerInteractPrimaryInputEvent evt)
        {
            if (!enabled || !gameObject.activeInHierarchy)
                return;

            TryExecuteAttack();
        }

        #endregion

        #region Combat Execution

        /// <summary>
        /// Attempts to execute a melee swing if cooldown and stamina requirements are met.
        /// </summary>
        /// <returns>True if the attack was executed, false otherwise.</returns>
        public bool TryExecuteAttack()
        {
            if (!CanAttack)
            {
                if (_attributeComponent == null)
                    _attributeComponent = GetComponent<AttributeComponent>();

                var stamina = _attributeComponent?.GetAttribute("Stamina");
                if (stamina != null && stamina.CurrentValue < _minStaminaRequired)
                {
                    GameEventManager.Publish(new PlayerStaminaExhaustedEvent());
                }
                return false;
            }

            ExecuteAttack();
            return true;
        }

        private void ExecuteAttack()
        {
            _lastAttackTime = Time.time;

            ConsumeStamina();
            DetectAndDamageTargets();
            StartCoroutine(AttackRoutine());
        }

        private void ConsumeStamina()
        {
            if (_attributeComponent != null)
            {
                _attributeComponent.ApplyDamage("Stamina", _staminaCost);
            }
        }

        private void DetectAndDamageTargets()
        {
            ResolveCamera();

            Vector3 origin = _cachedCamera != null
                ? _cachedCamera.transform.position
                : transform.position + Vector3.up * 1.5f;

            Vector3 direction = _cachedCamera != null
                ? _cachedCamera.transform.forward
                : transform.forward;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                _attackRadius,
                direction,
                _attackRange,
                _hitMask,
                QueryTriggerInteraction.Ignore);

            BaseActor closestTarget = null;
            HarvestableNode closestHarvestable = null;
            RaycastHit closestHit = default;
            float minDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;

                // Ignore self and child colliders
                if (hit.collider.transform.root == transform.root)
                    continue;

                var targetActor = hit.collider.GetComponentInParent<BaseActor>();
                if (targetActor != null && targetActor != _actor)
                {
                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                        closestTarget = targetActor;
                        closestHarvestable = null;
                        closestHit = hit;
                    }
                    continue;
                }

                var harvestable = hit.collider.GetComponentInParent<HarvestableNode>();
                if (harvestable != null && !harvestable.IsDepleted)
                {
                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                        closestHarvestable = harvestable;
                        closestTarget = null;
                        closestHit = hit;
                    }
                }
            }

            if (closestTarget != null || closestHarvestable != null)
            {
                var damageInfo = new DamageInfo(
                    _damageAmount,
                    DamageType.Physical,
                    gameObject,
                    closestHit.point,
                    closestHit.normal);

                if (closestTarget != null)
                {
                    closestTarget.TakeDamage(damageInfo);
                }
                else if (closestHarvestable != null)
                {
                    closestHarvestable.TakeDamage(damageInfo);
                }
            }
        }

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            yield return new WaitForSeconds(_attackCooldown);
            _isAttacking = false;
        }

        #endregion
    }
}
