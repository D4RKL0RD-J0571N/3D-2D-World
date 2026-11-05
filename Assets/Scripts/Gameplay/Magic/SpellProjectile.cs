namespace Gameplay.Magic
{
    using System.Collections.Generic;
    using UnityEngine;
    using Core;
    using Domain.Combat;
    using Gameplay.Entities;

    /// <summary>
    /// Magic projectile launched by spellcasters (e.g. Fireball).
    /// Detonates on impact, dealing area-of-effect elemental damage and spawning visual effects.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Magic/Spell Projectile")]
    [RequireComponent(typeof(SphereCollider))]
    public class SpellProjectile : MonoBehaviour
    {
        #region Fields

        [Header("Projectile Movement")]
        [SerializeField] private float _speed = 22f;
        [SerializeField] private float _lifetime = 6f;
        [SerializeField] private float _explosionRadius = 3.5f;
        [SerializeField] private int _damage = 40;

        [Header("Feedback")]
        [SerializeField] private GameObject _explosionVfxPrefab;
        [SerializeField] private AudioClip _explosionSound;

        private GameObject _caster;
        private bool _detonated;
        private float _spawnTime;

        #endregion

        #region Properties

        public int Damage { get => _damage; set => _damage = value; }
        public float ExplosionRadius { get => _explosionRadius; set => _explosionRadius = value; }
        public GameObject Caster { get => _caster; set => _caster = value; }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureDefaultAssets();
        }

        private void OnEnable()
        {
            _spawnTime = Time.time;
            _detonated = false;
        }

        private void Update()
        {
            if (_detonated) return;

            transform.position += transform.forward * (_speed * Time.deltaTime);

            if (Time.time > _spawnTime + _lifetime)
            {
                Detonate(transform.position, Vector3.up);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_detonated || other == null) return;

            if (_caster != null && (other.gameObject == _caster || other.transform.root == _caster.transform))
                return;

            Detonate(transform.position, -transform.forward);
        }

        #endregion

        #region Detonation & AoE

        /// <summary>
        /// Explodes the fireball at the specified impact point.
        /// </summary>
        public void Detonate(Vector3 impactPoint, Vector3 normal)
        {
            if (_detonated) return;
            _detonated = true;

            PlayExplosionAudio(impactPoint);
            SpawnExplosionVfx(impactPoint, normal);
            DamageAreaTargets(impactPoint);

            Destroy(gameObject, 0.1f);
        }

        private void DamageAreaTargets(Vector3 center)
        {
            Collider[] colliders = Physics.OverlapSphere(center, _explosionRadius);
            var damagedActors = new HashSet<BaseActor>();

            foreach (var col in colliders)
            {
                if (col == null) continue;
                var actor = col.GetComponentInParent<BaseActor>();
                if (actor == null || damagedActors.Contains(actor)) continue;

                // Don't damage caster in small AoE edge
                if (_caster != null && actor.gameObject == _caster) continue;

                damagedActors.Add(actor);
                var damageInfo = new DamageInfo(_damage, DamageType.Magic, _caster, col.transform.position, Vector3.up);
                actor.TakeDamage(damageInfo);
            }
        }

        private void SpawnExplosionVfx(Vector3 pos, Vector3 normal)
        {
            if (_explosionVfxPrefab != null)
            {
                Quaternion rot = normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
                GameObject vfx = Instantiate(_explosionVfxPrefab, pos, rot);
                Destroy(vfx, 3f);
            }
        }

        private void PlayExplosionAudio(Vector3 pos)
        {
            if (_explosionSound != null && ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                audio.Play3D(_explosionSound, pos, 0.95f);
            }
        }

        #endregion

        #region Asset Resolution

        private void EnsureDefaultAssets()
        {
#if UNITY_EDITOR
            if (_explosionVfxPrefab == null)
            {
                _explosionVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/FX/FX_Fire_01.prefab");
            }
            if (_explosionSound == null)
            {
                _explosionSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Duck/Sounds/FeelDuckBoom.wav");
            }
#endif
        }

        #endregion
    }
}
