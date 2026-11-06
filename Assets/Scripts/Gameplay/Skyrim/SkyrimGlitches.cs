namespace Gameplay.Skyrim
{
    using System.Collections;
    using UnityEngine;
    using Core;
    using Domain.Combat;
    using Domain.Entities;
    using Gameplay.Entities;

    /// <summary>
    /// Iconic Skyrim Glitch #1: The Skyrim Space Program.
    /// Whenever an actor suffers fatal overkill or is struck by a boss slam,
    /// launches the ragdoll straight into low Earth orbit at astronomical velocity.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Skyrim/Skyrim Space Program")]
    public class SkyrimSpaceProgram : MonoBehaviour
    {
        #region Fields

        [Header("Orbital Parameters")]
        [SerializeField] private float _verticalVelocity = 75f;
        [SerializeField] private float _spinRate = 25f;
        [SerializeField] private bool _enableSpaceProgram = true;

        [Header("Audio")]
        [SerializeField] private AudioClip _launchWhooshClip;

        #endregion

        #region Properties

        public bool EnableSpaceProgram { get => _enableSpaceProgram; set => _enableSpaceProgram = value; }
        public float VerticalVelocity { get => _verticalVelocity; set => _verticalVelocity = value; }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            GameEventManager.AddListener<EntityDiedEvent>(OnEntityDied);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<EntityDiedEvent>(OnEntityDied);
        }

        #endregion

        #region Launch Handling

        private void OnEntityDied(EntityDiedEvent evt)
        {
            if (!_enableSpaceProgram || evt?.Victim == null) return;

            LaunchIntoOrbit(evt.Victim.gameObject);
        }

        /// <summary>
        /// Catapults the target GameObject vertically into the sky with high angular spin.
        /// </summary>
        public void LaunchIntoOrbit(GameObject victim)
        {
            if (victim == null) return;

            var rb = victim.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = victim.AddComponent<Rigidbody>();
            }

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.up * _verticalVelocity + Random.insideUnitSphere * 8f;
            rb.angularVelocity = Random.insideUnitSphere * _spinRate;

            PlayLaunchAudio(victim.transform.position);
        }

        private void PlayLaunchAudio(Vector3 pos)
        {
            if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                if (_launchWhooshClip == null)
                {
#if UNITY_EDITOR
                    _launchWhooshClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Duck/Sounds/FeelDuckWoosh.wav");
#endif
                }
                if (_launchWhooshClip != null) audio.Play3D(_launchWhooshClip, pos, 1f);
            }
        }

        #endregion
    }

    /// <summary>
    /// Iconic Skyrim Glitch #2: Bucket on the Head Stealth Exploit.
    /// When a bucket, kettle, or pot touches or encompasses an NPC's head,
    /// completely blinds their vision cone so the player can loot with 0% detection.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Skyrim/Bucket-Head Stealth Exploit")]
    public class BucketHeadStealthExploit : MonoBehaviour
    {
        #region Fields

        [Header("Exploit Parameters")]
        [SerializeField] private bool _isBlinded;
        [SerializeField] private float _headTriggerRadius = 0.5f;

        #endregion

        #region Properties

        public bool IsBlinded => _isBlinded;

        #endregion

        #region Trigger Detection

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            string objName = other.gameObject.name.ToLower();
            if (objName.Contains("bucket") || objName.Contains("pot") || objName.Contains("dish"))
            {
                _isBlinded = true;
                Debug.Log($"[BucketHeadExploit] {name} has a bucket on their head! Vision completely obscured. 'Who turned out the lights?!'");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;

            string objName = other.gameObject.name.ToLower();
            if (objName.Contains("bucket") || objName.Contains("pot") || objName.Contains("dish"))
            {
                _isBlinded = false;
                Debug.Log($"[BucketHeadExploit] Bucket removed from {name}'s head. Vision restored.");
            }
        }

        #endregion
    }

    /// <summary>
    /// Iconic Skyrim Glitch #3: Wooden Platter Wall Phasing.
    /// Holding a wooden dish/platter in front of you and sprinting into a wall
    /// causes the physics solver to push the player right through the barrier.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Skyrim/Platter Wall-Clip Controller")]
    public class PlatterWallClip : MonoBehaviour
    {
        #region Fields

        [Header("Clip Parameters")]
        [SerializeField] private float _clipDistance = 2.0f;
        [SerializeField] private float _cooldown = 1.0f;
        [SerializeField] private bool _isHoldingPlatter = true;

        private float _lastClipTime = -10f;

        #endregion

        #region Properties

        public bool IsHoldingPlatter { get => _isHoldingPlatter; set => _isHoldingPlatter = value; }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (!_isHoldingPlatter) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.leftShiftKey.isPressed && keyboard.wKey.isPressed)
            {
                if (Physics.Raycast(transform.position + Vector3.up * 1.0f, transform.forward, out RaycastHit wallHit, 0.85f))
                {
                    TryPhaseThroughWall(transform.forward);
                }
            }
        }

        #endregion

        #region Wall Phase Execution

        /// <summary>
        /// Attempts to phase through an obstacle if carrying a platter and sprinting.
        /// </summary>
        public bool TryPhaseThroughWall(Vector3 forward)
        {
            if (Time.time < _lastClipTime + _cooldown) return false;

            _lastClipTime = Time.time;
            transform.position += forward.normalized * _clipDistance;

            Debug.Log("<color=yellow>[PlatterWallClip] Phased through wall using wooden platter speedrun exploit!</color>");
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Iconic Skyrim Glitch #4: Killer Cart / Clattering Physics Hazard.
    /// Bumping or sprinting into loose wooden carts, cauldrons, or pots causes
    /// chaotic micro-impulses, loud clattering, and 5 points of hilarious chip damage.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Skyrim/Physics Clatter Hazard")]
    public class PhysicsClatterHazard : MonoBehaviour
    {
        #region Fields

        [Header("Hazard Parameters")]
        [SerializeField] private int _chipDamage = 5;
        [SerializeField] private float _damageCooldown = 0.8f;
        [SerializeField] private float _impulseStrength = 12f;

        private float _lastDamageTime = -10f;
        private Rigidbody _rigidbody;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || collision.collider == null) return;

            var playerActor = collision.collider.GetComponentInParent<BaseActor>();
            if (playerActor != null && playerActor.CompareTag("Player"))
            {
                ApplyClatterAndDamage(playerActor);
            }
        }

        #endregion

        #region Clatter Handling

        private void ApplyClatterAndDamage(BaseActor player)
        {
            if (Time.time < _lastDamageTime + _damageCooldown) return;

            _lastDamageTime = Time.time;

            if (_rigidbody != null)
            {
                _rigidbody.AddForce(Random.insideUnitSphere * _impulseStrength, ForceMode.Impulse);
                _rigidbody.AddTorque(Random.insideUnitSphere * _impulseStrength * 2f, ForceMode.Impulse);
            }

            var damageInfo = new DamageInfo(_chipDamage, DamageType.Physical, gameObject, transform.position, Vector3.up);
            player.TakeDamage(damageInfo);

            Debug.Log($"<color=orange>[PhysicsClatterHazard] Cart clattered violently into player! Dealt {_chipDamage} physics chip damage.</color>");
        }

        #endregion
    }
}
