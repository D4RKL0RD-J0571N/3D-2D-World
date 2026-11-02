namespace Gameplay.Interactables
{
    using System;
    using Core;
    using Core.Ids;
    using Core.Persistence;
    using Newtonsoft.Json;
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
    public class LeverInteractable : MonoBehaviour, IInteractable, ISaveableComponent
    {
        #region Serialized Fields

        [Header("Linked Objects")]
        [Tooltip("GameObjects whose active state is toggled when this lever is pulled.")]
        [SerializeField] private GameObject[] _linkedObjects;

        #endregion

        #region State & Properties

        private bool _isActivated = false;

        public bool IsActivated => _isActivated;

        public void SetActivated(bool activated)
        {
            _isActivated = activated;
            ToggleLinkedObjects();
        }

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

        #region ISaveableComponent Implementation

        [Serializable]
        private sealed class LeverSaveDto
        {
            public bool IsActivated;
        }

        public string ComponentSaveKey => "lever";

        public object CaptureComponentState() => new LeverSaveDto { IsActivated = _isActivated };

        public void RestoreComponentState(string serializedJsonState, IWorldInstanceResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(serializedJsonState)) return;

            var settings = FantasyEngineJsonSettings.CreateDefault();
            var dto = JsonConvert.DeserializeObject<LeverSaveDto>(serializedJsonState, settings);
            if (dto != null)
            {
                SetActivated(dto.IsActivated);
            }
        }

        #endregion
    }
}
