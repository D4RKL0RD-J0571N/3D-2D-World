namespace Gameplay.Interactables
{
    using System;
    using System.Collections;
    using Core;
    using Systems.Creator;
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
    /// Pressure plate that activates traps and <see cref="Domain.Creator.CrossObjectLink"/> receivers
    /// when the player walks over it or when triggered via <see cref="ILinkReceiver"/>.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Interactables/Pressure Plate")]
    [RequireComponent(typeof(Collider))]
    public class PressurePlateInteractable : MonoBehaviour, ILinkReceiver
    {
        #region Serialized Fields

        [Header("Trap Objects")]
        [Tooltip("GameObjects to activate when triggered (spike traps, darts, collapsing floors).")]
        [SerializeField] private GameObject[] _trapObjects;

        [Header("Reset")]
        [Tooltip("Seconds before the trap resets and can trigger again.")]
        [SerializeField] private float _resetDelay = 3f;

        #endregion

        #region State & Properties

        private bool _isTriggered = false;

        public bool IsTriggered => _isTriggered;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isTriggered || !other.CompareTag("Player")) return;

            TriggerPlate();
            if (Application.isPlaying)
            {
                StartCoroutine(ResetCoroutine());
            }
        }

        #endregion

        #region Trigger & ILinkReceiver Logic

        public void TriggerPlate()
        {
            _isTriggered = true;
            ActivateTraps();
            GameEventManager.Publish(new PressurePlateTriggeredEvent(this));

            var editorTag = GetComponent<EditorPlacedEntityTag>();
            if (editorTag != null)
            {
                editorTag.TriggerOutputPort("OnSteppedOn");
            }

            Debug.Log($"[PressurePlate] {name} triggered.");
        }

        public void ResetPlate()
        {
            DeactivateTraps();
            _isTriggered = false;
        }

        public void ExecuteLinkAction(string actionName, GameObject sourceObject = null)
        {
            if (string.Equals(actionName, "Reset", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionName, "Deactivate", StringComparison.OrdinalIgnoreCase))
            {
                ResetPlate();
            }
            else if (string.Equals(actionName, "Toggle", StringComparison.OrdinalIgnoreCase))
            {
                if (_isTriggered) ResetPlate();
                else TriggerPlate();
            }
            else
            {
                TriggerPlate();
            }
        }

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
            ResetPlate();
        }

        #endregion
    }
}
