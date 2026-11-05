namespace Gameplay.Combat
{
    using System.Collections;
    using UnityEngine;
    using Core;
    using Domain.Combat;
    using Gameplay.Entities;
    using Gameplay.Player;

    /// <summary>
    /// Coordinates melee attack execution, stamina consumption, procedural weapon viewmodel swing,
    /// audio whoosh/impact feedback, and blood/hit particle instantiation.
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
        [SerializeField] private float _attackCooldown = 0.45f;

        [Header("Stamina Costs")]
        [SerializeField] private int _staminaCost = 20;
        [SerializeField] private int _minStaminaRequired = 10;

        [Header("Targeting")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Visuals & Viewmodel")]
        [SerializeField] private Transform _weaponVisualTransform;
        [SerializeField] private Vector3 _restPosition = new Vector3(0.35f, -0.3f, 0.6f);
        [SerializeField] private Vector3 _restRotation = new Vector3(15f, -10f, 5f);

        [Header("Audio & VFX")]
        [SerializeField] private AudioClip[] _swingClips;
        [SerializeField] private AudioClip _hitFleshClip;
        [SerializeField] private AudioClip _hitMetalClip;
        [SerializeField] private GameObject _hitVfxPrefab;

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

                var creatorService = ServiceLocator.Get<Systems.Creator.CreatorStudioService>();
                if (creatorService != null && creatorService.IsBuildMode)
                    return false;

                if (_attributeComponent == null)
                    _attributeComponent = GetComponent<AttributeComponent>();

                var stamina = _attributeComponent?.GetAttribute("Stamina");
                if (stamina != null && stamina.CurrentValue < _minStaminaRequired)
                    return false;

                return true;
            }
        }

        public Transform WeaponVisualTransform
        {
            get => _weaponVisualTransform;
            set => _weaponVisualTransform = value;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
            _attributeComponent = GetComponent<AttributeComponent>();
            EnsureAssetsLoaded();
        }

        private void Start()
        {
            ResolveCamera();
            InitializeWeaponVisualTransform();
        }

        private void Update()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !Gameplay.UI.CuratorHUD.IsOpen && Cursor.lockState == CursorLockMode.Locked)
            {
                TryExecuteAttack();
            }
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<PlayerInteractPrimaryInputEvent>(OnPrimaryInteract);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<PlayerInteractPrimaryInputEvent>(OnPrimaryInteract);
            StopAllCoroutines();
            _isAttacking = false;
        }

        #endregion

        #region Camera & Visual Resolution

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

        private void InitializeWeaponVisualTransform()
        {
            if (_weaponVisualTransform != null)
            {
                _weaponVisualTransform.localPosition = _restPosition;
                _weaponVisualTransform.localRotation = Quaternion.Euler(_restRotation);
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
            PlaySwingFeedback();
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

        private void PlaySwingFeedback()
        {
            if (_swingClips != null && _swingClips.Length > 0)
            {
                int index = Random.Range(0, _swingClips.Length);
                if (_swingClips[index] != null && ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
                {
                    audio.Play2D(_swingClips[index], 0.7f);
                }
            }
        }

        private void DetectAndDamageTargets()
        {
            ResolveCamera();

            Vector3 origin = _cachedCamera != null ? _cachedCamera.transform.position : transform.position + Vector3.up * 1.5f;
            Vector3 direction = _cachedCamera != null ? _cachedCamera.transform.forward : transform.forward;

            RaycastHit[] hits = Physics.SphereCastAll(origin, _attackRadius, direction, _attackRange, _hitMask, QueryTriggerInteraction.Ignore);

            BaseActor closestTarget = null;
            HarvestableNode closestHarvestable = null;
            RaycastHit closestHit = default;
            float minDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.root == transform.root) continue;

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
                if (harvestable != null && !harvestable.IsDepleted && hit.distance < minDistance)
                {
                    minDistance = hit.distance;
                    closestHarvestable = harvestable;
                    closestTarget = null;
                    closestHit = hit;
                }
            }

            ApplyDamageAndFeedback(closestTarget, closestHarvestable, closestHit);
        }

        private void ApplyDamageAndFeedback(BaseActor targetActor, HarvestableNode harvestable, RaycastHit hit)
        {
            if (targetActor == null && harvestable == null) return;

            var damageInfo = new DamageInfo(_damageAmount, DamageType.Physical, gameObject, hit.point, hit.normal);

            if (targetActor != null)
            {
                targetActor.TakeDamage(damageInfo);
                PlayHitFeedback(true, hit.point, hit.normal);
            }
            else if (harvestable != null)
            {
                harvestable.TakeDamage(damageInfo);
                PlayHitFeedback(false, hit.point, hit.normal);
            }
        }

        private void PlayHitFeedback(bool isActor, Vector3 hitPoint, Vector3 hitNormal)
        {
            var audio = ServiceLocator.Get<Systems.Audio.AudioService>();
            if (audio != null)
            {
                AudioClip clip = isActor ? _hitFleshClip : _hitMetalClip;
                if (clip != null) audio.Play3D(clip, hitPoint, 0.9f);
            }

            if (isActor && _hitVfxPrefab != null)
            {
                Quaternion rot = hitNormal != Vector3.zero ? Quaternion.LookRotation(hitNormal) : Quaternion.identity;
                GameObject vfx = Instantiate(_hitVfxPrefab, hitPoint, rot);
                Destroy(vfx, 2.5f);
            }
        }

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;

            float elapsed = 0f;
            float duration = _attackCooldown;
            Vector3 slashPos = _restPosition + new Vector3(-0.25f, -0.15f, 0.1f);
            Quaternion slashRot = Quaternion.Euler(_restRotation + new Vector3(45f, -30f, 35f));
            Quaternion startRot = Quaternion.Euler(_restRotation);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (_weaponVisualTransform != null)
                {
                    if (t < 0.35f)
                    {
                        float forwardT = t / 0.35f;
                        _weaponVisualTransform.localPosition = Vector3.Lerp(_restPosition, slashPos, forwardT);
                        _weaponVisualTransform.localRotation = Quaternion.Slerp(startRot, slashRot, forwardT);
                    }
                    else
                    {
                        float recoverT = (t - 0.35f) / 0.65f;
                        _weaponVisualTransform.localPosition = Vector3.Lerp(slashPos, _restPosition, recoverT);
                        _weaponVisualTransform.localRotation = Quaternion.Slerp(slashRot, startRot, recoverT);
                    }
                }

                yield return null;
            }

            InitializeWeaponVisualTransform();
            _isAttacking = false;
        }

        #endregion

        #region Asset Initialization

        private void EnsureAssetsLoaded()
        {
#if UNITY_EDITOR
            if (_swingClips == null || _swingClips.Length == 0)
            {
                _swingClips = new AudioClip[]
                {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbariansAttack1.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbariansAttack2.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbariansAttack3.wav")
                };
            }

            if (_hitFleshClip == null)
            {
                _hitFleshClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbarianHit.wav");
            }

            if (_hitMetalClip == null)
            {
                _hitMetalClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/NiceVibrations/HapticSamples/Impacts/ImpactMetal1.wav");
            }

            if (_hitVfxPrefab == null)
            {
                _hitVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DamageNumbersPro/Demo/Prefabs/3D/Blood Spread.prefab");
            }
#endif
        }

        #endregion
    }
}
