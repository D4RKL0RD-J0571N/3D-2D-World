namespace Gameplay.Player
{
    using UnityEngine;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using Core;

    /// <summary>
    /// Raycasts forward from the player's view to identify interactable entities and world objects,
    /// dispatching interaction upon receiving <see cref="PlayerInteractUseInputEvent"/> or pressing E.
    /// Supports both <see cref="IInteractable"/> objects (chests, notice boards, levers, stations)
    /// and <see cref="BaseEntity"/> actors/props.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Player/Player Interaction Controller")]
    [DisallowMultipleComponent]
    public class PlayerInteractionController : MonoBehaviour
    {
        #region Fields

        [Header("Raycast Settings")]
        [SerializeField] private float _interactDistance = 2.5f;
        [SerializeField] private LayerMask _interactLayers = ~0;
        [SerializeField] private Camera _playerCamera;

        private BaseEntity _currentTarget;
        private IInteractable _currentInteractable;
        private BaseActor _playerActor;
        private string _currentPrompt = string.Empty;

        #endregion

        #region Properties

        /// <summary>
        /// The currently highlighted entity in the player's crosshairs, or null if targeting an IInteractable prop.
        /// </summary>
        public BaseEntity CurrentTarget => _currentTarget;

        /// <summary>
        /// The currently highlighted <see cref="IInteractable"/> in the player's crosshairs, if any.
        /// </summary>
        public IInteractable CurrentInteractable => _currentInteractable;

        /// <summary>
        /// Returns true if either a valid interactable component or entity is currently targeted.
        /// </summary>
        public bool HasTarget => _currentInteractable != null || _currentTarget != null;

        /// <summary>
        /// Contextual prompt for the highlighted entity or interactable, or empty if none.
        /// </summary>
        public string CurrentPrompt => _currentPrompt;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _playerActor = GetComponent<BaseActor>();

            if (_playerCamera == null)
            {
                _playerCamera = GetComponentInChildren<Camera>();
                if (_playerCamera == null)
                    _playerCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<PlayerInteractUseInputEvent>(OnInteractInput);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<PlayerInteractUseInputEvent>(OnInteractInput);
        }

        private void Update()
        {
            UpdateInteractionTarget();

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && !Gameplay.UI.CuratorHUD.IsOpen)
            {
                TriggerInteraction();
            }
        }

        #endregion

        #region Raycast Detection

        private void UpdateInteractionTarget()
        {
            if (_playerCamera == null)
            {
                ClearTarget();
                return;
            }

            Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactLayers, QueryTriggerInteraction.Ignore))
            {
                // Don't interact with ourselves
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    ClearTarget();
                    return;
                }

                // 1. Prioritize dedicated IInteractable components (chests, bounty boards, levers, anvils)
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract)
                {
                    _currentInteractable = interactable;
                    _currentTarget = hit.collider.GetComponentInParent<BaseEntity>();
                    _currentPrompt = interactable.InteractionPrompt;
                    return;
                }

                // 2. Fall back to BaseEntity
                BaseEntity entity = hit.collider.GetComponentInParent<BaseEntity>();
                if (entity != null && entity.enabled)
                {
                    _currentTarget = entity;
                    _currentInteractable = null;
                    _currentPrompt = entity.InteractionPrompt;
                    return;
                }
            }

            ClearTarget();
        }

        private void ClearTarget()
        {
            _currentTarget = null;
            _currentInteractable = null;
            _currentPrompt = string.Empty;
        }

        #endregion

        #region Input Handling & Execution

        private void OnInteractInput(PlayerInteractUseInputEvent evt)
        {
            TriggerInteraction();
        }

        private void TriggerInteraction()
        {
            if (_currentInteractable != null && _currentInteractable.CanInteract)
            {
                if (_playerActor == null)
                {
                    _playerActor = GetComponent<BaseActor>();
                }
                _currentInteractable.OnInteract(_playerActor);
            }
            else if (_currentTarget != null)
            {
                _currentTarget.OnInteract(gameObject);
            }
        }

        #endregion
    }
}
