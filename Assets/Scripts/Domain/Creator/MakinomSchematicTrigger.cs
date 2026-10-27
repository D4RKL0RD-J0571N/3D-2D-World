namespace Domain.Creator
{
    using UnityEngine;
    using Core;

    /// <summary>
    /// Interactive trigger component for the Creator Studio and runtime modding.
    /// Can trigger Makinom Schematics, Game Creator Actions, or native game events when entered or interacted with.
    /// </summary>
    [AddComponentMenu("3D 2D World/Creator/Makinom Schematic Trigger")]
    [RequireComponent(typeof(Collider))]
    public class MakinomSchematicTrigger : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Trigger Settings")]
        [Tooltip("Identifier for the schematic or action event.")]
        [SerializeField] private string _schematicId = "DefaultTriggerEvent";

        [Tooltip("Whether the trigger fires on physical player collision or interact button.")]
        [SerializeField] private bool _triggerOnCollision = true;

        [Tooltip("Whether the trigger can only fire once.")]
        [SerializeField] private bool _fireOnce = false;

        [Header("Feedback")]
        [SerializeField] private AudioClip _triggerSound;
        [SerializeField] private ParticleSystem _triggerVfx;

        #endregion

        #region Private State

        private bool _hasFired = false;
        private Collider _triggerCollider;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_triggerOnCollision || (_fireOnce && _hasFired)) return;

            if (other.CompareTag("Player") || other.GetComponentInParent<Gameplay.Player.Player_Movement>() != null)
            {
                ExecuteTrigger(other.gameObject);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Manually execute the trigger logic (e.g. from interaction or player action).
        /// </summary>
        public void ExecuteTrigger(GameObject activator)
        {
            if (_fireOnce && _hasFired) return;
            _hasFired = true;

            Debug.Log($"[MakinomSchematicTrigger] Firing trigger '{_schematicId}' by {activator.name}");

            if (_triggerSound != null)
            {
                AudioSource.PlayClipAtPoint(_triggerSound, transform.position);
            }

            if (_triggerVfx != null)
            {
                _triggerVfx.Play();
            }

            // Publish trigger event through global event bus
            GameEventManager.Publish(new CreatorTriggerFiredEvent(_schematicId, gameObject, activator));
        }

        /// <summary>
        /// Reset trigger state so it can fire again.
        /// </summary>
        public void ResetTrigger()
        {
            _hasFired = false;
        }

        #endregion
    }
}
