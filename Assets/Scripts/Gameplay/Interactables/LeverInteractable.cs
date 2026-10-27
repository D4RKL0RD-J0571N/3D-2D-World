namespace Gameplay.Interactables
{
    using Core;
    using UnityEngine;

    // ── Event ─────────────────────────────────────────────────────────────────

    /// <summary>Fired when a lever is toggled by the player.</summary>
    public class LeverToggledEvent : GameEvent
    {
        public LeverInteractable Source;
        public bool IsActivated;

        public LeverToggledEvent(LeverInteractable source, bool activated)
        {
            Source      = source;
            IsActivated = activated;
        }
    }

    // ── Component ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A lever or pull-chain that toggles one or more linked GameObjects (gates, portcullises, etc.).
    /// </summary>
    [AddComponentMenu("3D2DWorld/Interactables/Lever")]
    public class LeverInteractable : MonoBehaviour, IInteractable
    {
        #region Serialized Fields

        [Header("Linked Objects")]
        [Tooltip("GameObjects whose active state is toggled when this lever is pulled.")]
        [SerializeField] private GameObject[] _linkedObjects;

        #endregion

        #region State

        private bool _isActivated = false;

        #endregion

        #region IInteractable

        public bool CanInteract => true;

        public string InteractionPrompt => _isActivated ? "Reset Lever" : "Pull Lever";

        public void OnInteract(Entities.BaseActor interactor)
        {
            _isActivated = !_isActivated;

            ToggleLinkedObjects();
            GetComponent<Animator>()?.SetTrigger("Toggle");
            GameEventManager.Publish(new LeverToggledEvent(this, _isActivated));

            Debug.Log($"[LeverInteractable] {name} toggled → {(_isActivated ? "ON" : "OFF")}");
        }

        #endregion

        #region Helpers

        private void ToggleLinkedObjects()
        {
            if (_linkedObjects == null) return;

            foreach (var obj in _linkedObjects)
            {
                if (obj != null) obj.SetActive(_isActivated);
            }
        }

        #endregion
    }
}
