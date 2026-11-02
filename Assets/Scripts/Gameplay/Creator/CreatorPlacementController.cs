namespace Gameplay.Creator
{
    using System.Linq;
    using Core;
    using Core.Ids;
    using Domain.Creator;
    using Systems.Creator;
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Handles 3D ghost preview, mouse raycasting, grid snapping, Undo/Redo hotkeys, and
    /// <see cref="InstanceGuid"/>-backed object placement/demolition in Build Mode (<c>ARCH-05</c>).
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
            ServiceLocator.TryGet(out _creatorService);
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
                if (Application.isPlaying)
                {
                    Destroy(_ghostInstance);
                }
                else
                {
                    DestroyImmediate(_ghostInstance);
                }
                _ghostInstance = null;
            }
        }

        #endregion

        #region Raycasting & Snapping

        private void UpdateGhostPosition()
        {
            if (_ghostInstance == null) return;

            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
                if (_targetCamera == null) return;
            }

            if (IsPointerOverUI())
            {
                _hasValidPlacement = false;
                if (_ghostInstance.activeSelf)
                {
                    _ghostInstance.SetActive(false);
                }
                return;
            }

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

        #region Placement Input & Undo/Redo Hotkeys

        private void HandleGlobalHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.bKey.wasPressedThisFrame || keyboard.f3Key.wasPressedThisFrame)
            {
                _creatorService?.ToggleBuildMode();
            }
        }

        private void HandlePlacementInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            // Undo (Ctrl+Z) / Redo (Ctrl+Y)
            bool ctrlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (ctrlHeld && keyboard.zKey.wasPressedThisFrame)
            {
                _creatorService?.CommandHistory.Undo();
                return;
            }
            if (ctrlHeld && keyboard.yKey.wasPressedThisFrame)
            {
                _creatorService?.CommandHistory.Redo();
                return;
            }

            // Rotation controls (R, Q, E)
            if (keyboard.rKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
            {
                _currentYaw = (_currentYaw + _rotationStep) % 360f;
            }
            else if (keyboard.qKey.wasPressedThisFrame)
            {
                _currentYaw = (_currentYaw - _rotationStep + 360f) % 360f;
            }

            if (IsPointerOverUI()) return;

            // Left click to place via PlaceObjectCommand
            if (mouse.leftButton.wasPressedThisFrame && _hasValidPlacement && _activeItem != null && _activeItem.prefab != null)
            {
                PlaceCurrentItem();
            }

            // Demolish hovered placed object via Delete, X, Backspace, or Middle Click
            if (keyboard.deleteKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame ||
                keyboard.backspaceKey.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
            {
                TryDemolishHoveredObject();
            }
        }

        private bool IsPointerOverUI()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 mousePos = mouse.position.ReadValue();
                Vector2 flippedPos = new Vector2(mousePos.x, Screen.height - mousePos.y);

                var hud = Object.FindFirstObjectByType<Gameplay.UI.CreatorStudioHUD>();
                if (hud != null)
                {
                    var uiDoc = hud.GetComponent<UnityEngine.UIElements.UIDocument>();
                    if (uiDoc != null && uiDoc.rootVisualElement != null && uiDoc.rootVisualElement.panel != null)
                    {
                        var picked = uiDoc.rootVisualElement.panel.Pick(flippedPos);
                        if (picked != null)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void PlaceCurrentItem()
        {
            if (_creatorService == null || _activeItem == null) return;

            Quaternion rotation = Quaternion.Euler(0f, _currentYaw, 0f);
            Vector3 scale = _activeItem.defaultScale.sqrMagnitude > 0.001f ? _activeItem.defaultScale : Vector3.one;

            var placeCmd = new PlaceObjectCommand(_creatorService, _activeItem.id, _currentPlacementPoint, rotation, scale);
            _creatorService.CommandHistory.ExecuteCommand(placeCmd);

            Debug.Log($"[CreatorPlacementController] Placed {_activeItem.displayName} ({placeCmd.InstanceId}) at {_currentPlacementPoint}");
        }

        private void TryDemolishHoveredObject()
        {
            if (_creatorService == null || _targetCamera == null || Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _targetCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance))
            {
                var placedTag = hit.collider.GetComponentInParent<EditorPlacedEntityTag>();
                if (placedTag != null && !placedTag.InstanceId.IsEmpty)
                {
                    var deleteCmd = new DeleteObjectCommand(_creatorService, placedTag.InstanceId);
                    _creatorService.CommandHistory.ExecuteCommand(deleteCmd);
                    Debug.Log($"[CreatorPlacementController] Demolished placed object ({placedTag.InstanceId})");
                    return;
                }

                GameObject target = hit.collider.gameObject;
                while (target.transform.parent != null && !_creatorService.SpawnedObjects.Contains(target))
                {
                    target = target.transform.parent.gameObject;
                }

                if (_creatorService.SpawnedObjects.Contains(target))
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
