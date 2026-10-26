namespace Gameplay.Creator
{
    using System.Linq;
    using Core;
    using Domain.Creator;
    using Systems.Creator;
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Handles 3D ghost preview, mouse raycasting, grid snapping, and object placement/demolition in Build Mode.
    /// </summary>
    [AddComponentMenu("3D 2D World/Creator/Placement Controller")]
    public class CreatorPlacementController : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private LayerMask _placementMask = ~0;
        [SerializeField] private Material _ghostMaterial;

        [Header("Placement Settings")]
        [SerializeField] private float _defaultGridSnap = 1.0f;
        [SerializeField] private float _maxRayDistance = 100.0f;
        [SerializeField] private float _rotationStep = 45.0f;

        #endregion

        #region Private Fields & State

        private CreatorStudioService _creatorService;
        private CreatorItemDefinition _activeItem;
        private GameObject _ghostInstance;
        private float _currentYaw = 0f;
        private bool _isBuildMode = false;
        private Vector3 _currentPlacementPoint;
        private bool _hasValidPlacement = false;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }
        }

        private void Start()
        {
            _creatorService = ServiceLocator.Get<CreatorStudioService>();
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<CreatorModeChangedEvent>(OnModeChanged);
            GameEventManager.AddListener<CreatorItemSelectedEvent>(OnItemSelected);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<CreatorModeChangedEvent>(OnModeChanged);
            GameEventManager.RemoveListener<CreatorItemSelectedEvent>(OnItemSelected);
            DestroyGhost();
        }

        private void Update()
        {
            HandleGlobalHotkeys();

            if (!_isBuildMode) return;

            UpdateGhostPosition();
            HandlePlacementInput();
        }

        #endregion

        #region Event Handlers

        private void OnModeChanged(CreatorModeChangedEvent evt)
        {
            _isBuildMode = evt.IsBuildMode;
            if (!_isBuildMode)
            {
                DestroyGhost();
            }
            else
            {
                if (_creatorService?.ActiveItem != null)
                {
                    CreateGhost(_creatorService.ActiveItem);
                }
            }
        }

        private void OnItemSelected(CreatorItemSelectedEvent evt)
        {
            _activeItem = evt.Item;
            if (_isBuildMode)
            {
                CreateGhost(_activeItem);
            }
        }

        #endregion

        #region Ghost Preview Management

        private void CreateGhost(CreatorItemDefinition item)
        {
            DestroyGhost();
            if (item == null || item.prefab == null) return;

            _ghostInstance = Instantiate(item.prefab);
            _ghostInstance.name = "[GHOST] " + item.displayName;

            // Strip functional colliders, audio sources, scripts to prevent unwanted execution
            var colliders = _ghostInstance.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            var audios = _ghostInstance.GetComponentsInChildren<AudioSource>();
            foreach (var a in audios)
            {
                a.enabled = false;
            }

            var animators = _ghostInstance.GetComponentsInChildren<Animator>();
            foreach (var anim in animators)
            {
                anim.enabled = false;
            }

            // Apply ghost material if provided
            if (_ghostMaterial != null)
            {
                var renderers = _ghostInstance.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = _ghostMaterial;
                    }
                    r.sharedMaterials = mats;
                }
            }

            _ghostInstance.SetActive(false);
        }

        private void DestroyGhost()
        {
            if (_ghostInstance != null)
            {
                Destroy(_ghostInstance);
                _ghostInstance = null;
            }
        }

        #endregion

        #region Raycasting & Snapping

        private void UpdateGhostPosition()
        {
            if (_ghostInstance == null || _targetCamera == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            Ray ray = _targetCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _placementMask))
            {
                float snap = (_activeItem != null && _activeItem.gridSnapSize > 0f) ? _activeItem.gridSnapSize : _defaultGridSnap;
                Vector3 snappedPoint = SnapToGrid(hit.point, snap);

                if (_activeItem != null)
                {
                    snappedPoint += _activeItem.placementOffset;
                }

                _currentPlacementPoint = snappedPoint;
                _ghostInstance.transform.position = snappedPoint;
                _ghostInstance.transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);

                if (!_ghostInstance.activeSelf)
                {
                    _ghostInstance.SetActive(true);
                }

                _hasValidPlacement = true;
            }
            else
            {
                _hasValidPlacement = false;
                if (_ghostInstance.activeSelf)
                {
                    _ghostInstance.SetActive(false);
                }
            }
        }

        private Vector3 SnapToGrid(Vector3 pos, float snapSize)
        {
            if (snapSize <= 0.001f) return pos;
            return new Vector3(
                Mathf.Round(pos.x / snapSize) * snapSize,
                pos.y,
                Mathf.Round(pos.z / snapSize) * snapSize
            );
        }

        #endregion

        #region Placement Input

        private void HandleGlobalHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Toggle Build Mode via B or F3
            if (keyboard.bKey.wasPressedThisFrame || keyboard.f3Key.wasPressedThisFrame)
            {
                if (_creatorService != null)
                {
                    _creatorService.ToggleBuildMode();
                }
            }
        }

        private void HandlePlacementInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            // Rotation controls (R, Q, E)
            if (keyboard.rKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
            {
                _currentYaw = (_currentYaw + _rotationStep) % 360f;
            }
            else if (keyboard.qKey.wasPressedThisFrame)
            {
                _currentYaw = (_currentYaw - _rotationStep + 360f) % 360f;
            }

            // Left click to place
            if (mouse.leftButton.wasPressedThisFrame && _hasValidPlacement && _activeItem != null && _activeItem.prefab != null)
            {
                PlaceCurrentItem();
            }

            // Right click or Delete key to remove/demolish hovered placed object
            if (mouse.rightButton.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
            {
                TryDemolishHoveredObject();
            }
        }

        private void PlaceCurrentItem()
        {
            var rotation = Quaternion.Euler(0f, _currentYaw, 0f);
            var spawned = Instantiate(_activeItem.prefab, _currentPlacementPoint, rotation);
            spawned.name = $"{_activeItem.displayName}_{spawned.GetInstanceID()}";

            _creatorService?.RegisterSpawnedObject(spawned, _activeItem.id);
            Debug.Log($"[CreatorPlacementController] Placed {_activeItem.displayName} at {_currentPlacementPoint}");
        }

        private void TryDemolishHoveredObject()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _targetCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance))
            {
                // Find root placed object
                GameObject target = hit.collider.gameObject;
                while (target.transform.parent != null && !target.CompareTag("Untagged"))
                {
                    target = target.transform.parent.gameObject;
                }

                if (_creatorService != null && _creatorService.SpawnedObjects.Contains(target))
                {
                    _creatorService.UnregisterSpawnedObject(target);
                    Destroy(target);
                    Debug.Log($"[CreatorPlacementController] Demolished placed object: {target.name}");
                }
            }
        }

        #endregion
    }
}
