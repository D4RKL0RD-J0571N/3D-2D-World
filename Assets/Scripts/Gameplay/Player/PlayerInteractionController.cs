namespace Gameplay.Player
{
    using UnityEngine;
    using Gameplay.Entities;
    using Core;

    /// <summary>
    /// Raycasts forward from the player's view to identify interactable entities
    /// and dispatches interaction upon receiving <see cref="PlayerInteractUseInputEvent"/>.
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
        private string _currentPrompt = string.Empty;

        #endregion

        #region Properties

        /// <summary>
        /// The currently highlighted entity in the player's crosshairs.
        /// </summary>
        public BaseEntity CurrentTarget => _currentTarget;

        /// <summary>
        /// Contextual prompt for the highlighted entity, or empty if none.
        /// </summary>
        public string CurrentPrompt => _currentPrompt;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
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
        }

        #endregion

        #region Raycast Detection

        private void UpdateInteractionTarget()
        {
            if (_playerCamera == null)
            {
                _currentTarget = null;
                _currentPrompt = string.Empty;
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

                BaseEntity entity = hit.collider.GetComponentInParent<BaseEntity>();
                if (entity != null && entity.enabled)
                {
                    _currentTarget = entity;
                    _currentPrompt = entity.InteractionPrompt;
                    return;
                }
            }

            ClearTarget();
        }

        private void ClearTarget()
        {
            _currentTarget = null;
            _currentPrompt = string.Empty;
        }

        #endregion

        #region Input Handling

        private void OnInteractInput(PlayerInteractUseInputEvent evt)
        {
            if (_currentTarget != null)
            {
                _currentTarget.OnInteract(gameObject);
            }
        }

        #endregion
    }
}
