namespace Gameplay.Combat
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Core;
    using Core.Content;
    using Data.Config;
    using Domain.Combat;
    using Domain.Entities;
    using Domain.Skyrim;
    using Gameplay.Entities;
    using Systems.Skyrim;

    /// <summary>
    /// Ranged combat AI for skeleton/bandit archers.
    /// Operates a dedicated state machine: Idle, Patrol, KeepDistance, AimAndShoot, and Dead.
    /// Interacts with StealthDetectionService, ballistic ArrowProjectiles, and loot dropping.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Combat/Ranged Combat AI")]
    [RequireComponent(typeof(BaseActor))]
    [RequireComponent(typeof(AttributeComponent))]
    public class RangedCombatAI : MonoBehaviour
    {
        #region State Enum

        public enum ArcherState
        {
            Idle,
            Patrol,
            KeepDistance,
            AimAndShoot,
            Dead
        }

        #endregion

        #region Serialized Fields

        [Header("Perception & Range")]
        [SerializeField] private float _sightRange = 22f;
        [SerializeField] private float _fieldOfView = 120f;
        [SerializeField] private float _minComfortDistance = 6f;
        [SerializeField] private float _maxShootingRange = 25f;

        [Header("Archery Parameters")]
        [SerializeField] private float _aimDuration = 1.0f;
        [SerializeField] private float _shootCooldown = 2.2f;
        [SerializeField] private int _arrowDamage = 18;
        [SerializeField] private GameObject _arrowPrefab;
        [SerializeField] private Transform _bowFirePoint;

        [Header("Patrol & Navigation")]
        [SerializeField] private PatrolPath _patrolPath;
        [SerializeField] private float _moveSpeed = 3.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip _bowDrawClip;
        [SerializeField] private AudioClip _bowReleaseClip;
        [SerializeField] private AudioClip _deathClip;

        [Header("Loot")]
        [SerializeField] private string[] _lootItemIds = new string[] { "Item_StoneChunk", "Item_AncientAmulet" };

        private BaseActor _actor;
        private AttributeComponent _attributes;
        private Transform _playerTransform;
        private BaseActor _playerActor;
        private ArcherState _currentState = ArcherState.Idle;
        private float _lastShotTime = -10f;
        private bool _isAiming;

        #endregion

        #region Properties

        public ArcherState CurrentState => _currentState;
        public PatrolPath AssignedPatrolPath { get => _patrolPath; set => _patrolPath = value; }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
            _attributes = GetComponent<AttributeComponent>();
            EnsureDefaultAssets();
        }

        private void Start()
        {
            ResolvePlayer();
            ResolveFirePoint();
        }

        private void Update()
        {
            if (_currentState == ArcherState.Dead) return;

            ResolvePlayer();
            UpdateStateMachine();
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<EntityDamagedEvent>(OnTakeDamage);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<EntityDamagedEvent>(OnTakeDamage);
            StopAllCoroutines();
            _isAiming = false;
        }

        #endregion

        #region Setup & Helpers

        private void ResolvePlayer()
        {
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    _playerTransform = player.transform;
                    _playerActor = player.GetComponent<BaseActor>();
                }
            }
        }

        private void ResolveFirePoint()
        {
            if (_bowFirePoint == null)
            {
                var bow = transform.Find("SM_Prop_Bow_01");
                _bowFirePoint = bow != null ? bow : transform;
            }
        }

        #endregion

        #region State Machine

        private void UpdateStateMachine()
        {
            if (_attributes != null && _attributes.GetAttribute("Health")?.CurrentValue <= 0)
            {
                TransitionToState(ArcherState.Dead);
                return;
            }

            if (_isAiming) return;

            bool canSeePlayer = CanPerceivePlayer();

            if (canSeePlayer && _playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                if (dist < _minComfortDistance)
                {
                    TransitionToState(ArcherState.KeepDistance);
                    FleeStep();
                }
                else if (dist <= _maxShootingRange)
                {
                    TransitionToState(ArcherState.AimAndShoot);
                    AimAndShootStep();
                }
            }
            else
            {
                if (_patrolPath != null)
                {
                    TransitionToState(ArcherState.Patrol);
                    PatrolStep();
                }
                else
                {
                    TransitionToState(ArcherState.Idle);
                }
            }
        }

        private void TransitionToState(ArcherState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;

            if (_currentState == ArcherState.Dead)
            {
                HandleDeath();
            }
        }

        #endregion

        #region Perception

        private bool CanPerceivePlayer()
        {
            if (_playerTransform == null) return false;

            float effectiveSight = _sightRange;
            if (ServiceLocator.TryGet<StealthDetectionService>(out var stealth))
            {
                if (stealth.CurrentState == StealthState.Hidden)
                    effectiveSight *= 0.5f;
            }

            Vector3 toPlayer = _playerTransform.position - transform.position;
            float dist = toPlayer.magnitude;
            if (dist > effectiveSight) return false;

            float angle = Vector3.Angle(transform.forward, toPlayer);
            if (angle > _fieldOfView * 0.5f && dist > 4f) return false;

            Vector3 eyePos = transform.position + Vector3.up * 1.5f;
            Vector3 targetPos = _playerTransform.position + Vector3.up * 1.2f;

            if (Physics.Raycast(eyePos, (targetPos - eyePos).normalized, out RaycastHit hit, dist))
            {
                return hit.transform.root == _playerTransform.root;
            }

            return false;
        }

        #endregion

        #region Combat & Patrol Steps

        private void PatrolStep()
        {
            var waypoint = _patrolPath.CurrentWaypoint;
            if (waypoint == null) return;

            Vector3 toWp = waypoint.position - transform.position;
            toWp.y = 0f;

            if (toWp.magnitude <= _patrolPath.WaypointTolerance)
            {
                _patrolPath.AdvanceToNextWaypoint();
                return;
            }

            MoveTowards(waypoint.position);
        }

        private void FleeStep()
        {
            Vector3 awayDir = (transform.position - _playerTransform.position).normalized;
            awayDir.y = 0f;
            Vector3 target = transform.position + awayDir * 5f;
            MoveTowards(target);
            FaceTarget(_playerTransform.position);
        }

        private void AimAndShootStep()
        {
            FaceTarget(_playerTransform.position);

            if (Time.time >= _lastShotTime + _shootCooldown)
            {
                StartCoroutine(DrawAndShootRoutine());
            }
        }

        private void MoveTowards(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.05f) return;

            transform.position += dir.normalized * (_moveSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        }

        private void FaceTarget(Vector3 targetPos)
        {
            Vector3 dir = targetPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
            }
        }

        private IEnumerator DrawAndShootRoutine()
        {
            _isAiming = true;
            PlayAudio2D(_bowDrawClip, 0.7f);

            float elapsed = 0f;
            while (elapsed < _aimDuration)
            {
                elapsed += Time.deltaTime;
                if (_playerTransform != null) FaceTarget(_playerTransform.position);
                yield return null;
            }

            FireArrow();
            _lastShotTime = Time.time;
            _isAiming = false;
        }

        private void FireArrow()
        {
            if (_playerTransform == null) return;

            PlayAudio2D(_bowReleaseClip, 0.85f);

            Vector3 spawnPos = _bowFirePoint != null ? _bowFirePoint.position : transform.position + Vector3.up * 1.3f;
            Vector3 targetChest = _playerTransform.position + Vector3.up * 1.2f;
            Vector3 aimDir = (targetChest - spawnPos).normalized;

            GameObject arrowObj = null;
            if (_arrowPrefab != null)
            {
                arrowObj = Instantiate(_arrowPrefab, spawnPos, Quaternion.LookRotation(aimDir));
            }
            else
            {
                arrowObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arrowObj.transform.localScale = new Vector3(0.04f, 0.4f, 0.04f);
                arrowObj.transform.position = spawnPos;
                arrowObj.transform.rotation = Quaternion.LookRotation(aimDir);
            }

            var arrowComp = arrowObj.GetComponent<ArrowProjectile>() ?? arrowObj.AddComponent<ArrowProjectile>();
            arrowComp.Damage = _arrowDamage;
            arrowComp.Launch(aimDir, gameObject);
        }

        #endregion

        #region Damage & Death

        private void OnTakeDamage(EntityDamagedEvent evt)
        {
            if (evt == null || evt.Target != _actor) return;

            if (_currentState != ArcherState.Dead && _playerTransform != null)
            {
                FaceTarget(_playerTransform.position);
                if (Time.time >= _lastShotTime + 0.8f)
                {
                    _lastShotTime = Time.time - _shootCooldown + 0.4f;
                }
            }
        }

        private void HandleDeath()
        {
            PlayAudio2D(_deathClip, 0.9f);

            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            transform.rotation = Quaternion.Euler(75f, transform.eulerAngles.y, 0f);
            transform.position += Vector3.down * 0.4f;

            DistributeLoot();
            Destroy(gameObject, 20f);
        }

        private void DistributeLoot()
        {
            if (!ServiceLocator.TryGet<IContentService>(out var contentService)) return;
            var itemRegistry = contentService.GetRegistry<ItemData>();
            if (itemRegistry == null || _playerTransform == null) return;

            var playerInventory = _playerTransform.GetComponent<InventoryComponent>();
            if (playerInventory == null) return;

            foreach (var itemId in _lootItemIds)
            {
                if (itemRegistry.TryGetByLegacyAlias(itemId, out var item))
                {
                    playerInventory.AddItem(item, 1);
                }
            }
        }

        private void PlayAudio2D(AudioClip clip, float vol)
        {
            if (clip != null && ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                audio.Play2D(clip, vol);
            }
        }

        #endregion

        #region Asset Resolution

        private void EnsureDefaultAssets()
        {
#if UNITY_EDITOR
            if (_arrowPrefab == null)
            {
                _arrowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/Weapons/SM_Prop_Arrow_01.prefab");
            }

            if (_bowDrawClip == null)
            {
                _bowDrawClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Duck/Sounds/FeelDuckWoosh.wav");
            }

            if (_bowReleaseClip == null)
            {
                _bowReleaseClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Strike/Sounds/FeelStrikeHit.wav");
            }

            if (_deathClip == null)
            {
                _deathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbariansGrunt6.wav");
            }
#endif
        }

        #endregion
    }
}
