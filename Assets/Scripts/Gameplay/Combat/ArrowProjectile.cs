namespace Gameplay.Combat
{
    using UnityEngine;
    using Core;
    using Domain.Combat;
    using Gameplay.Entities;

    /// <summary>
    /// Ballistic arrow projectile launched by ranged combatants or player bows.
    /// Inflicts damage on BaseActor contact and embeds into static environment geometry.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Combat/Arrow Projectile")]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class ArrowProjectile : MonoBehaviour
    {
        #region Fields

        [Header("Ballistics")]
        [SerializeField] private float _speed = 35f;
        [SerializeField] private int _damage = 20;
        [SerializeField] private float _gravityMultiplier = 0.5f;
        [SerializeField] private float _maxLifetime = 12f;

        [Header("Audio")]
        [SerializeField] private AudioClip _impactFleshClip;
        [SerializeField] private AudioClip _impactWoodClip;

        private Rigidbody _rigidbody;
        private Collider _collider;
        private GameObject _shooter;
        private bool _hasImpacted;
        private float _spawnTime;

        #endregion

        #region Properties

        public int Damage
        {
            get => _damage;
            set => _damage = value;
        }

        public GameObject Shooter
        {
            get => _shooter;
            set => _shooter = value;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            EnsureDefaultAssets();
        }

        private void OnEnable()
        {
            _spawnTime = Time.time;
            _hasImpacted = false;
        }

        private void FixedUpdate()
        {
            if (_hasImpacted) return;

            // Align rotation with velocity trajectory
            if (_rigidbody != null && _rigidbody.linearVelocity.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(_rigidbody.linearVelocity);
                // Apply extra custom gravity arc
                _rigidbody.AddForce(Physics.gravity * _gravityMultiplier, ForceMode.Acceleration);
            }

            if (Time.time > _spawnTime + _maxLifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasImpacted || other == null) return;

            // Ignore shooter
            if (_shooter != null && (other.gameObject == _shooter || other.transform.root == _shooter.transform))
                return;

            HandleImpact(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasImpacted || collision.collider == null) return;

            if (_shooter != null && (collision.gameObject == _shooter || collision.transform.root == _shooter.transform))
                return;

            HandleImpact(collision.collider);
        }

        #endregion

        #region Launch & Impact

        /// <summary>
        /// Launches the arrow in the specified direction from an origin.
        /// </summary>
        public void Launch(Vector3 direction, GameObject shooter)
        {
            _shooter = shooter;
            _hasImpacted = false;
            transform.forward = direction;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.linearVelocity = direction.normalized * _speed;
            }
        }

        private void HandleImpact(Collider other)
        {
            _hasImpacted = true;

            var targetActor = other.GetComponentInParent<BaseActor>();
            Vector3 hitPoint = transform.position;
            Vector3 hitNormal = -transform.forward;

            if (targetActor != null)
            {
                var damageInfo = new DamageInfo(_damage, DamageType.Physical, _shooter, hitPoint, hitNormal);
                targetActor.TakeDamage(damageInfo);
                PlayImpactSound(true);

                // Parent to victim so arrow sticks into them (classic Skyrim arrow in the knee!)
                transform.parent = targetActor.transform;
            }
            else
            {
                PlayImpactSound(false);
                // Embed into terrain / wall
                transform.parent = other.transform;
            }

            FreezeAndStick();
        }

        private void FreezeAndStick()
        {
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
            }

            if (_collider != null)
            {
                _collider.enabled = false;
            }

            Destroy(gameObject, 15f);
        }

        private void PlayImpactSound(bool isActor)
        {
            if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                AudioClip clip = isActor ? _impactFleshClip : _impactWoodClip;
                if (clip != null) audio.Play3D(clip, transform.position, 0.8f);
            }
        }

        #endregion

        #region Asset Resolution

        private void EnsureDefaultAssets()
        {
#if UNITY_EDITOR
            if (_impactFleshClip == null)
            {
                _impactFleshClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbarianHit.wav");
            }
            if (_impactWoodClip == null)
            {
                _impactWoodClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/NiceVibrations/HapticSamples/Impacts/ImpactMetal1.wav");
            }
#endif
        }

        #endregion
    }
}
