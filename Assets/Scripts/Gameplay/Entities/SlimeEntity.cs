namespace Gameplay.Entities
{
    using System.Collections;
    using UnityEngine;
    using Domain.Entities;
    using Domain.Combat;
    using Core;
    using Core.Utils;

    public enum SlimeAIState
    {
        Idle,
        WanderHop,
        Chasing,
        Leaping
    }

    /// <summary>
    /// Slime entity controller with hop movement, physics jumping, player aggro, and melee acid bite attacks.
    /// Uses billboarded sprite rendering.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AttributeComponent))]
    [AddComponentMenu("3D2DWorld/Entities/Slime Entity")]
    public class SlimeEntity : BaseActor
    {
        #region Fields

        [Header("Movement")]
        [SerializeField] private float _hopSpeed = 3.5f;
        [SerializeField] private float _chaseSpeed = 5.0f;
        [SerializeField] private float _hopJumpVelocity = 4.5f;
        [SerializeField] private float _gravity = -18.0f;
        [SerializeField] private float _idleTimeMin = 1.2f;
        [SerializeField] private float _idleTimeMax = 2.5f;

        [Header("Perception & Aggro")]
        [SerializeField] private float _detectionRange = 8.0f;
        [SerializeField] private float _attackRange = 1.8f;
        [SerializeField] private float _attackCooldown = 1.2f;
        [SerializeField] private int _attackDamage = 10;

        [Header("Visual Squash & Stretch")]
        [SerializeField] private Transform _visualRoot;

        private CharacterController _controller;
        private Transform _playerTransform;
        private SlimeAIState _currentState = SlimeAIState.Idle;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _stateTimer;
        private Vector3 _moveDirection;
        private bool _isSleeping;
        private bool _isDead;
        private float _lastAttackTime = -10f;
        private SpriteRenderer _spriteRenderer;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            _controller = GetComponent<CharacterController>();

            bool hasValidRoot = false;
            try
            {
                hasValidRoot = _visualRoot != null;
            }
            catch (MissingReferenceException)
            {
                _visualRoot = null;
            }

            if (!hasValidRoot)
            {
                _visualRoot = transform.Find("Visual");
            }

            if (_visualRoot != null)
            {
                _spriteRenderer = _visualRoot.GetComponent<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<AreaTierChangedEvent>(OnAreaTierChanged);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<AreaTierChangedEvent>(OnAreaTierChanged);
        }

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
            }

            PickNextIdleTime();
            ApplyCurrentTierState();
        }

        private void Update()
        {
            if (_controller == null || _isSleeping || _isDead)
                return;

            UpdateAI(Time.deltaTime);
            ApplyPhysics(Time.deltaTime);
            UpdateVisualFeedback();
        }

        #endregion

        #region AI Logic

        private void UpdateAI(float deltaTime)
        {
            float distToPlayer = _playerTransform != null
                ? Vector3.Distance(transform.position, _playerTransform.position)
                : float.MaxValue;

            bool canSeePlayer = distToPlayer <= _detectionRange;

            switch (_currentState)
            {
                case SlimeAIState.Idle:
                    _horizontalVelocity = Vector3.zero;
                    _stateTimer -= deltaTime;

                    if (canSeePlayer)
                    {
                        _currentState = SlimeAIState.Chasing;
                        _stateTimer = 0.3f;
                    }
                    else if (_stateTimer <= 0f)
                    {
                        StartWanderHop();
                    }
                    break;

                case SlimeAIState.WanderHop:
                    if (_controller.isGrounded && _verticalVelocity <= 0f)
                    {
                        _currentState = SlimeAIState.Idle;
                        PickNextIdleTime();
                    }
                    break;

                case SlimeAIState.Chasing:
                    _stateTimer -= deltaTime;
                    if (_stateTimer <= 0f && _controller.isGrounded)
                    {
                        if (distToPlayer > _detectionRange * 1.5f)
                        {
                            _currentState = SlimeAIState.Idle;
                            PickNextIdleTime();
                        }
                        else if (distToPlayer <= _attackRange)
                        {
                            ExecuteMeleeAttack();
                        }
                        else
                        {
                            ExecuteChaseHop();
                        }
                    }
                    break;

                case SlimeAIState.Leaping:
                    if (_controller.isGrounded && _verticalVelocity <= 0f)
                    {
                        _currentState = SlimeAIState.Chasing;
                        _stateTimer = 0.4f; // Short pause after landing
                    }
                    break;
            }
        }

        private void ExecuteMeleeAttack()
        {
            if (Time.time < _lastAttackTime + _attackCooldown)
            {
                _stateTimer = 0.2f;
                return;
            }

            _lastAttackTime = Time.time;
            _stateTimer = _attackCooldown;

            if (_playerTransform != null)
            {
                var playerActor = _playerTransform.GetComponentInParent<BaseActor>();
                if (playerActor != null)
                {
                    var damageInfo = new DamageInfo(
                        _attackDamage,
                        DamageType.Acid,
                        gameObject,
                        _playerTransform.position,
                        Vector3.up);

                    playerActor.TakeDamage(damageInfo);
                }
            }

            // Visual bite / hop feedback
            _verticalVelocity = _hopJumpVelocity * 0.5f;
        }

        private void StartWanderHop()
        {
            float randomAngle = Random.Range(0f, 360f);
            _moveDirection = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;
            _horizontalVelocity = _moveDirection * _hopSpeed;
            _verticalVelocity = _hopJumpVelocity;
            _currentState = SlimeAIState.WanderHop;
        }

        private void ExecuteChaseHop()
        {
            if (_playerTransform == null)
            {
                _currentState = SlimeAIState.Idle;
                return;
            }

            Vector3 toPlayer = _playerTransform.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.01f)
            {
                _moveDirection = toPlayer.normalized;
            }

            _horizontalVelocity = _moveDirection * _chaseSpeed;
            _verticalVelocity = _hopJumpVelocity * 1.15f;
            _currentState = SlimeAIState.Leaping;
        }

        private void PickNextIdleTime()
        {
            _stateTimer = Random.Range(_idleTimeMin, _idleTimeMax);
        }

        #endregion

        #region Physics & Visuals

        private void ApplyPhysics(float deltaTime)
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2.0f; // Ground stick
                _horizontalVelocity = Vector3.Lerp(_horizontalVelocity, Vector3.zero, deltaTime * 8f);
            }
            else
            {
                _verticalVelocity += _gravity * deltaTime;
            }

            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalVelocity) * deltaTime;
            _controller.Move(motion);
        }

        private void UpdateVisualFeedback()
        {
            if (_visualRoot == null || _isDead)
                return;

            // Simple bouncy squash & stretch based on vertical velocity
            float scaleY = Mathf.Clamp(1.0f + (_verticalVelocity * 0.04f), 0.7f, 1.4f);
            float scaleXZ = 1.0f / Mathf.Sqrt(scaleY);
            _visualRoot.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
        }

        #endregion

        #region Combat & Lifecycle

        public override void TakeDamage(DamageInfo damage)
        {
            if (_isDead) return;

            base.TakeDamage(damage);

            var health = Attributes?.GetAttribute("Health");
            if (health != null && health.CurrentValue <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            if (_controller != null)
            {
                _controller.enabled = false;
            }

            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            float elapsed = 0f;
            float duration = 0.35f;

            Vector3 startScale = _visualRoot != null ? _visualRoot.localScale : Vector3.one;
            Vector3 targetScale = new Vector3(startScale.x * 1.5f, 0.1f, startScale.z * 1.5f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (_visualRoot != null)
                {
                    _visualRoot.localScale = Vector3.Lerp(startScale, targetScale, t);
                }

                if (_spriteRenderer != null)
                {
                    Color c = _spriteRenderer.color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    _spriteRenderer.color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        #endregion

        #region Area Lifecycle

        private void OnAreaTierChanged(AreaTierChangedEvent evt)
        {
            if (evt == null || evt.RoomId != RoomId)
                return;

            _isSleeping = evt.NewTier == AreaSimulationTier.Purged;
            ApplySleepState();
        }

        private void ApplyCurrentTierState()
        {
            if (string.IsNullOrEmpty(RoomId))
                return;

            if (ServiceLocator.TryGet<Systems.Scheduler.AreaSchedulerService>(out var scheduler))
                _isSleeping = scheduler.GetRoomTier(RoomId) == AreaSimulationTier.Purged;
            else
                _isSleeping = false;

            ApplySleepState();
        }

        private void ApplySleepState()
        {
            if (_controller != null)
                _controller.enabled = !_isSleeping;

            if (_spriteRenderer != null)
                _spriteRenderer.enabled = !_isSleeping;
        }

        #endregion
    }
}
