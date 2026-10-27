namespace Gameplay.Interactables
{
    using System.Collections;
    using Core;
    using UnityEngine;

    // ── Event ─────────────────────────────────────────────────────────────────

    /// <summary>Fired when the pressure plate is triggered by the player stepping on it.</summary>
    public class PressurePlateTriggeredEvent : GameEvent
    {
        public PressurePlateInteractable Source;
        public PressurePlateTriggeredEvent(PressurePlateInteractable source) { Source = source; }
    }

    // ── Component ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Pressure plate that activates traps when the player walks over it.
    /// Does NOT implement IInteractable — triggered by physics, not key press.
    /// Requires a Collider set to Is Trigger on the same GameObject.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Interactables/Pressure Plate")]
    [RequireComponent(typeof(Collider))]
    public class PressurePlateInteractable : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Trap Objects")]
        [Tooltip("GameObjects to activate when triggered (spike traps, darts, collapsing floors).")]
        [SerializeField] private GameObject[] _trapObjects;

        [Header("Reset")]
        [Tooltip("Seconds before the trap resets and can trigger again.")]
        [SerializeField] private float _resetDelay = 3f;

        #endregion

        #region State

        private bool _isTriggered = false;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure the collider is flagged as trigger
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isTriggered || !other.CompareTag("Player")) return;

            _isTriggered = true;
            ActivateTraps();
            GameEventManager.Publish(new PressurePlateTriggeredEvent(this));
            StartCoroutine(ResetCoroutine());

            Debug.Log($"[PressurePlate] {name} triggered by player.");
        }

        #endregion

        #region Trap Logic

        private void ActivateTraps()
        {
            if (_trapObjects == null) return;

            foreach (var trap in _trapObjects)
            {
                if (trap != null) trap.SetActive(true);
            }
        }

        private void DeactivateTraps()
        {
            if (_trapObjects == null) return;

            foreach (var trap in _trapObjects)
            {
                if (trap != null) trap.SetActive(false);
            }
        }

        private IEnumerator ResetCoroutine()
        {
            yield return new WaitForSeconds(_resetDelay);
            DeactivateTraps();
            _isTriggered = false;
        }

        #endregion
    }
}
