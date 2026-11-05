namespace Gameplay.Combat
{
    using System.Collections;
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Data.Config;
    using Domain.Combat;
    using Domain.Entities;
    using Domain.Skyrim;
    using Gameplay.Entities;
    using Gameplay.Skyrim;
    using Systems.Skyrim;
    using UnityEngine;

    public enum EnemyAIState
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Dead
    }

    /// <summary>
    /// Autonomous enemy combat AI handling perception, stealth detection integration,
    /// pursuit pathing, melee engagement, and death reward distribution.
    /// </summary>
    [RequireComponent(typeof(BaseActor))]
    [AddComponentMenu("3D2DWorld/Combat/Enemy Combat AI")]
    public class EnemyCombatAI : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Attributes & Identity")]
        [SerializeField] private int _maxHealth = 150;
        [SerializeField] private string _enemyDisplayName = "Draugr Wight";

        [Header("Combat Settings")]
        [SerializeField] private float _detectionRange = 14f;
        [SerializeField] private float _attackRange = 2.2f;
        [SerializeField] private float _attackCooldown = 1.4f;
        [SerializeField] private int _attackDamage = 15;
        [SerializeField] private float _chaseSpeed = 3.6f;
        [SerializeField] private float _rotationSpeed = 8.0f;

        [Header("Loot Drops")]
        [SerializeField] private string[] _dropItemIds = { "Item_AncientAmulet", "Item_StoneChunk" };

        #endregion

        #region Private Fields

        private BaseActor _actor;
        private AttributeComponent _attributes;
        private FactionComponent _faction;
        private CharacterController _characterController;
        private Animator _animator;
        private Transform _targetTransform;
        private BaseActor _targetActor;

        private EnemyAIState _currentState = EnemyAIState.Idle;
        private float _lastAttackTime = -10f;
        private bool _isDead;
        private Vector3 _spawnPosition;

        #endregion

        #region Properties

        public EnemyAIState CurrentState => _currentState;
        public bool IsDead => _isDead;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
            _characterController = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
            _faction = GetComponent<FactionComponent>();
            _spawnPosition = transform.position;

            EnsureAttributeSetup();
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<EntityDiedEvent>(OnEntityDied);
            GameEventManager.AddListener<EntityDamagedEvent>(OnEntityDamaged);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<EntityDiedEvent>(OnEntityDied);
            GameEventManager.RemoveListener<EntityDamagedEvent>(OnEntityDamaged);
        }

        private void Start()
        {
            AcquireTarget();
        }

        private void Update()
        {
            if (_isDead) return;

            if (_targetTransform == null)
            {
                AcquireTarget();
                if (_targetTransform == null) return;
            }

            UpdateBehavior(Time.deltaTime);
        }

        #endregion

        #region Initialization Helpers

        private void EnsureAttributeSetup()
        {
            if (!TryGetComponent(out _attributes))
            {
                _attributes = gameObject.AddComponent<AttributeComponent>();
            }

            if (_attributes.GetAttribute("Health") == null)
            {
                _attributes.RegisterAttribute(new BaseAttribute
                {
                    AttributeName = "Health",
                    MaximumValue = _maxHealth,
                    CurrentValue = _maxHealth,
                    RegenerationRate = 0f
                });
            }
        }

        private void AcquireTarget()
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _targetTransform = playerObj.transform;
                _targetActor = playerObj.GetComponentInParent<BaseActor>();
            }
        }

        #endregion

        #region State Machine Logic

        private void UpdateBehavior(float deltaTime)
        {
            float distToTarget = Vector3.Distance(transform.position, _targetTransform.position);

            switch (_currentState)
            {
                case EnemyAIState.Idle:
                case EnemyAIState.Patrol:
                    HandlePerception(distToTarget);
                    break;

                case EnemyAIState.Chase:
                    HandlePursuit(distToTarget, deltaTime);
                    break;

                case EnemyAIState.Attack:
                    HandleAttackEngagement(distToTarget, deltaTime);
                    break;
            }
        }

        private void HandlePerception(float distToTarget)
        {
            if (!IsHostileToPlayer()) return;

            float effectiveRange = CalculateEffectiveDetectionRange();
            if (distToTarget <= effectiveRange)
            {
                _currentState = EnemyAIState.Chase;
            }
        }

        private float CalculateEffectiveDetectionRange()
        {
            if (ServiceLocator.TryGet<StealthDetectionService>(out var stealth) &&
                stealth.CurrentState == StealthState.Hidden)
            {
                Vector3 toPlayer = (_targetTransform.position - transform.position).normalized;
                float dot = Vector3.Dot(transform.forward, toPlayer);
                return dot > 0.4f ? _detectionRange * 0.4f : 2.0f;
            }

            return _detectionRange;
        }

        private bool IsHostileToPlayer()
        {
            if (_faction == null) return true;

            if (ServiceLocator.TryGet<FactionService>(out var factionSvc))
            {
                return factionSvc.IsHostile(_faction.FactionId, FactionService.PlayerFactionId);
            }

            return true;
        }

        private void HandlePursuit(float distToTarget, float deltaTime)
        {
            if (distToTarget > _detectionRange * 1.8f)
            {
                _currentState = EnemyAIState.Idle;
                Debug.Log($"<color=cyan>[EnemyCombatAI] {name}: 'Must have been the wind.'</color>");
                return;
            }

            if (distToTarget <= _attackRange)
            {
                _currentState = EnemyAIState.Attack;
                return;
            }

            MoveTowardsTarget(deltaTime);
        }

        private void MoveTowardsTarget(float deltaTime)
        {
            Vector3 direction = (_targetTransform.position - transform.position);
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _rotationSpeed * deltaTime);
            }

            Vector3 moveDelta = transform.forward * (_chaseSpeed * deltaTime);

            if (_characterController != null && _characterController.enabled)
            {
                moveDelta.y = -9.81f * deltaTime;
                _characterController.Move(moveDelta);
            }
            else
            {
                transform.position += moveDelta;
            }

            UpdateMovementAnimation(true);
        }

        private void HandleAttackEngagement(float distToTarget, float deltaTime)
        {
            FaceTarget(deltaTime);

            if (distToTarget > _attackRange * 1.25f)
            {
                _currentState = EnemyAIState.Chase;
                return;
            }

            if (Time.time >= _lastAttackTime + _attackCooldown)
            {
                ExecuteMeleeStrike();
            }
        }

        private void FaceTarget(float deltaTime)
        {
            Vector3 direction = (_targetTransform.position - transform.position);
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _rotationSpeed * deltaTime);
            }

            UpdateMovementAnimation(false);
        }

        private void ExecuteMeleeStrike()
        {
            _lastAttackTime = Time.time;

            if (_targetActor != null)
            {
                var damageInfo = new DamageInfo(
                    _attackDamage,
                    DamageType.Physical,
                    gameObject,
                    _targetTransform.position,
                    Vector3.up);

                _targetActor.TakeDamage(damageInfo);
            }

            TriggerAttackAnimation();
            StartCoroutine(AttackLungeRoutine());
        }

        private IEnumerator AttackLungeRoutine()
        {
            Vector3 originalPos = transform.position;
            Vector3 lungePos = originalPos + transform.forward * 0.4f;

            float elapsed = 0f;
            while (elapsed < 0.15f)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(originalPos, lungePos, elapsed / 0.15f);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.15f)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(lungePos, originalPos, elapsed / 0.15f);
                yield return null;
            }
        }

        #endregion

        #region Animation Wrappers

        private void UpdateMovementAnimation(bool isMoving)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            try
            {
                _animator.SetBool("IsMoving", isMoving);
                _animator.SetFloat("Speed", isMoving ? _chaseSpeed : 0f);
            }
            catch (System.Exception) { }
        }

        private void TriggerAttackAnimation()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            try
            {
                _animator.SetTrigger("Attack");
            }
            catch (System.Exception) { }
        }

        #endregion

        #region Event Reactions & Death

        private void OnEntityDamaged(EntityDamagedEvent evt)
        {
            if (evt == null || evt.Target != _actor) return;

            // Retaliate against instigator immediately
            if (evt.Instigator != null)
            {
                _targetTransform = evt.Instigator.transform;
                _targetActor = evt.Instigator.GetComponentInParent<BaseActor>();
                _currentState = EnemyAIState.Chase;
            }
        }

        private void OnEntityDied(EntityDiedEvent evt)
        {
            if (evt == null || evt.Victim != _actor) return;
            HandleDeath(evt.Killer);
        }

        private void HandleDeath(GameObject killer)
        {
            if (_isDead) return;
            _isDead = true;
            _currentState = EnemyAIState.Dead;

            if (_characterController != null) _characterController.enabled = false;
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            DistributeLootDrop(killer);
            UnregisterObserver();

            StartCoroutine(DeathCollapseRoutine());
        }

        private void DistributeLootDrop(GameObject killer)
        {
            if (killer != null && killer.TryGetComponent<InventoryComponent>(out var playerInventory))
            {
                IContentService contentService = ServiceLocator.TryGet<IContentService>(out var cs) ? cs : null;
                var itemRegistry = contentService?.GetRegistry<ItemData>();

                for (int i = 0; i < _dropItemIds.Length; i++)
                {
                    string id = _dropItemIds[i];
                    ItemData item = null;
                    if (itemRegistry != null)
                    {
                        itemRegistry.TryGetByLegacyAlias(id, out item);
                    }
                    if (item == null)
                    {
                        item = Resources.Load<ItemData>($"Items/{id}");
                    }
                    if (item != null)
                    {
                        playerInventory.AddItem(item, 1);
                    }
                }

                if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
                {
                    audio.PlayLoot();
                }
            }
        }

        private void UnregisterObserver()
        {
            if (ServiceLocator.TryGet<StealthDetectionService>(out var stealth))
            {
                stealth.UnregisterObserver(transform);
            }

            if (ServiceLocator.TryGet<CompassService>(out var compass))
            {
                compass.UnregisterMarker(name);
            }
        }

        private IEnumerator DeathCollapseRoutine()
        {
            float elapsed = 0f;
            float duration = 0.8f;
            Quaternion startRot = transform.rotation;
            Quaternion targetRot = Quaternion.Euler(startRot.eulerAngles.x + 90f, startRot.eulerAngles.y, startRot.eulerAngles.z);
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos - Vector3.up * 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
        }

        #endregion
    }
}
