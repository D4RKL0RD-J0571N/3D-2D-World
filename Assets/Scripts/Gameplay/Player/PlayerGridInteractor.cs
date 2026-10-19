namespace Gameplay.Player
{
    using UnityEngine;
    using Core;
    using Domain.Interaction;
    using Systems.Interaction;
    using Core.Utils;


    /// <summary>
    /// Component placed on the Player's Camera or Head to handle looking at and clicking blocks.
    /// </summary>
    public class PlayerGridInteractor : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _interactionRange = 5f;
        [SerializeField] private LayerMask _interactionMask = -1; // Default to everything

        [Header("Visual Selection")]
        [SerializeField] private GameObject _selectorPrefab;
        [SerializeField] private GameObject _hitIndicatorPrefab;

        private Transform _selector;
        private GameObject _currentHitIndicator;
        private UnityEngine.Camera _mainCamera;
        private GridInteractionService _interactionService;

        private void Start()
        {
            _mainCamera = GetComponent<UnityEngine.Camera>();
            if (_mainCamera == null)
            {
                _mainCamera = UnityEngine.Camera.main;
            }

            ServiceLocator.TryGet(out _interactionService);

            if (_selectorPrefab != null)
            {
                _selector = Instantiate(_selectorPrefab).transform;
                _selector.name = "Voxel_Selection_Indicator";

                // Scale slightly larger than 1x1x1 to prevent Z-fighting with the block meshes
                _selector.localScale = new Vector3(1.02f, 1.02f, 1.02f);

                _selector.gameObject.SetActive(false);
            }
            // Optimization: Pre-instantiate the indicator to avoid Instantiate/Destroy overhead
            if (_hitIndicatorPrefab != null)
            {
                _currentHitIndicator = Instantiate(_hitIndicatorPrefab);
                _currentHitIndicator.transform.localScale = Vector3.one * 0.1f;
                _currentHitIndicator.SetActive(false);
            }
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<PlayerInteractPrimaryInputEvent>(OnPrimaryInteract);
            GameEventManager.AddListener<PlayerInteractSecondaryInputEvent>(OnSecondaryInteract);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<PlayerInteractPrimaryInputEvent>(OnPrimaryInteract);
            GameEventManager.RemoveListener<PlayerInteractSecondaryInputEvent>(OnSecondaryInteract);
        }

        private void Update()
        {
            if (_interactionService == null || _mainCamera == null) return;

            if (TryGetGridHit(out var gridHit, out var rayHit))
            {
                // Unified updates
                UpdateSelector(gridHit.BlockCoordinate);
                // Handle Hit Indicator (The "Precise" Visual)
                UpdateHitIndicator(rayHit.point);

                // Gizmo Visualization: Draw a red line from camera to hit point
                // We use a small duration so it updates every frame
                Vector3 direction = rayHit.point - _mainCamera.transform.position;
                Debug.DrawRay(_mainCamera.transform.position, direction, Color.red, 0.1f);
            }
            else
            {
                // Pass null to hide both
                UpdateSelector(null);
                UpdateHitIndicator(null);
            }
        }

        private void OnPrimaryInteract(PlayerInteractPrimaryInputEvent evt) => HandleInteraction(0);
        private void OnSecondaryInteract(PlayerInteractSecondaryInputEvent evt) => HandleInteraction(1);

        private void HandleInteraction(int mouseButton)
        {
            if (_interactionService == null || _mainCamera == null) return;

            // When primary click (left mouse) is used, check if combat takes precedence
            if (mouseButton == 0)
            {
                // Check if aiming at an actor / entity
                Ray centerRay = InteractionPhysicsUtils.GetCenterRay(_mainCamera, new Vector2(0.5f, 0.5f));
                if (Physics.Raycast(centerRay, out RaycastHit entityHit, _interactionRange, _interactionMask, QueryTriggerInteraction.Ignore))
                {
                    if (entityHit.collider != null && entityHit.collider.GetComponentInParent<Gameplay.Entities.BaseActor>() != null)
                    {
                        // Aiming at an actor; combat takes priority over voxel mining
                        return;
                    }
                }

                // If player has an active weapon component, weapon combat takes precedence over mining
                var weapon = GetComponentInParent<Gameplay.Combat.WeaponComponent>();
                if (weapon != null && weapon.enabled)
                {
                    return;
                }
            }

            if (TryGetGridHit(out var gridHit, out var rayHit))
            {
                // Extra guard if grid hit happens to belong to an actor
                if (rayHit.collider != null && rayHit.collider.GetComponentInParent<Gameplay.Entities.BaseActor>() != null)
                {
                    return;
                }

                GameEventManager.Publish(new GridBlockClickedEvent { HitInfo = gridHit, MouseButton = mouseButton });
            }
        }

        /// <summary>       
        /// Updates the selector's state. Hides if coord is null.
        /// </summary>
        private void UpdateSelector(Vector3Int? coord)
        {
            if (_selector == null) return;

            if (coord.HasValue)
            {
                _selector.gameObject.SetActive(true);
                _selector.position = new Vector3(coord.Value.x + 0.5f, coord.Value.y + 0.5f, coord.Value.z + 0.5f);
            }
            else
            {
                _selector.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Updates the hit indicator's state. Hides if point is null.
        /// </summary>
        private void UpdateHitIndicator(Vector3? point)
        {
            if (_currentHitIndicator == null) return;

            if (point.HasValue)
            {
                _currentHitIndicator.gameObject.SetActive(true);
                _currentHitIndicator.transform.position = point.Value;
            }
            else
            {
                _currentHitIndicator.gameObject.SetActive(false);
            }
        }

        private bool TryGetGridHit(out GridHitInfo gridHit, out RaycastHit rayHit)
        {
            gridHit = default;
            rayHit = default;

            // Get the ray from the center of the screen
            Ray ray = InteractionPhysicsUtils.GetCenterRay(_mainCamera, new Vector2(0.5f, 0.5f));

            if (InteractionPhysicsUtils.PerformInteractionRaycast(ray, _interactionRange, _interactionMask, out rayHit))
            {
                gridHit = _interactionService.ResolveHit(rayHit);
                return gridHit.IsHit;
            }

            return false;
        }
    }
}
