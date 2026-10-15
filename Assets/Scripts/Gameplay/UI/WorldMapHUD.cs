namespace Gameplay.UI
{
    using System.Collections.Generic;
    using Core;
    using Data.Config;
    using Systems.WorldMap;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// UI Toolkit HUD for the world map and minimap.
    /// Renders explored rooms, the player, and tracked entities.
    /// Supports zoom, opacity, and layer customization.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("3D2DWorld/UI/World Map HUD (UI Toolkit)")]
    public sealed class WorldMapHUD : MonoBehaviour
    {
        [Header("Assets")]
        [SerializeField] private WorldMapConfig _config;

        private UIDocument _uiDocument;
        private WorldMapService _service;

        // Containers
        private VisualElement _minimapContainer;
        private VisualElement _minimapImage;
        private VisualElement _minimapIconsContainer;
        private VisualElement _worldMapContainer;
        private VisualElement _worldMapImage;
        private VisualElement _worldMapIconsContainer;

        // State
        private bool _showWorldMap;
        private bool _showMinimap = true;
        private float _worldMapZoom = 1f;
        private float _minimapZoom = 0.5f;
        private float _opacity = 1f;
        private bool _showPlayer = true;
        private bool _showEnemies = true;
        private bool _showNpcs = true;
        private int _frameCounter;
        private readonly Dictionary<MapEntity, VisualElement> _entityIcons = new();
        private readonly Queue<VisualElement> _iconPool = new();

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            SetupUIDocument();
            BuildUI();
        }

        private void Start()
        {
            if (!ServiceLocator.TryGet<WorldMapService>(out _service))
            {
                var service = new WorldMapService();
                ServiceLocator.Register(service);
                _service = service;
            }

            if (_config != null)
                _service.SetConfig(_config);
        }

        private void Update()
        {
            if (_config == null || _service == null)
                return;

            HandleInput();

            if (!_showMinimap && !_showWorldMap)
                return;

            UpdateMapImages();
            UpdateIconVisibility();

            _frameCounter++;
            if (_frameCounter % Mathf.Max(1, _config.EntityUpdateInterval) == 0)
            {
                UpdateEntityIcons();
            }
        }

        private void HandleInput()
        {
            if (UnityEngine.Input.GetKeyDown(_config.ToggleWorldMapKey))
                ToggleWorldMap();

            if (UnityEngine.Input.GetKeyDown(_config.ToggleMinimapKey))
                ToggleMinimap();
        }

        private void ToggleWorldMap()
        {
            _showWorldMap = !_showWorldMap;
            ApplyVisibility();
        }

        private void ToggleMinimap()
        {
            _showMinimap = !_showMinimap;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            _minimapContainer.style.display = _showMinimap && !_showWorldMap ? DisplayStyle.Flex : DisplayStyle.None;
            _worldMapContainer.style.display = _showWorldMap ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetupUIDocument()
        {
            if (_uiDocument == null)
                return;

            if (_uiDocument.panelSettings == null)
            {
                var panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
#if UNITY_EDITOR
                if (panelSettings == null)
                {
                    panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");
                }
#endif
                if (panelSettings != null)
                    _uiDocument.panelSettings = panelSettings;
            }
        }

        private void BuildUI()
        {
            var root = _uiDocument.rootVisualElement;
            if (root == null)
                return;

            root.pickingMode = PickingMode.Ignore;

            // Minimap
            _minimapContainer = new VisualElement
            {
                name = "MinimapContainer",
                pickingMode = PickingMode.Ignore
            };
            _minimapContainer.AddToClassList("worldmap-minimap");

            _minimapImage = new VisualElement
            {
                name = "MinimapImage",
                pickingMode = PickingMode.Ignore
            };
            _minimapImage.AddToClassList("worldmap-image");

            _minimapIconsContainer = new VisualElement
            {
                name = "MinimapIconsContainer",
                pickingMode = PickingMode.Ignore
            };
            _minimapIconsContainer.AddToClassList("worldmap-icons");

            _minimapContainer.Add(_minimapImage);
            _minimapContainer.Add(_minimapIconsContainer);

            // World map
            _worldMapContainer = new VisualElement
            {
                name = "WorldMapContainer",
                pickingMode = PickingMode.Position,
                style = { display = DisplayStyle.None }
            };
            _worldMapContainer.AddToClassList("worldmap-fullscreen");

            _worldMapImage = new VisualElement
            {
                name = "WorldMapImage",
                pickingMode = PickingMode.Ignore
            };
            _worldMapImage.AddToClassList("worldmap-image");

            _worldMapIconsContainer = new VisualElement
            {
                name = "WorldMapIconsContainer",
                pickingMode = PickingMode.Ignore
            };
            _worldMapIconsContainer.AddToClassList("worldmap-icons");

            _worldMapContainer.Add(_worldMapImage);
            _worldMapContainer.Add(_worldMapIconsContainer);
            _worldMapContainer.Add(BuildWorldMapControls());

            root.Add(_minimapContainer);
            root.Add(_worldMapContainer);

            ApplyStyles();
            ApplyVisibility();
        }

        private VisualElement BuildWorldMapControls()
        {
            var controls = new VisualElement();
            controls.AddToClassList("worldmap-controls");

            var title = new Label("World Map");
            title.AddToClassList("worldmap-title");
            controls.Add(title);

            controls.Add(BuildSlider("Opacity", 0.1f, 1f, _opacity, v => _opacity = v));
            controls.Add(BuildSlider("Zoom", _config != null ? _config.WorldMapMinZoom : 0.25f,
                _config != null ? _config.WorldMapMaxZoom : 4f, _worldMapZoom, v => _worldMapZoom = v));

            controls.Add(BuildToggle("Player", _showPlayer, v => _showPlayer = v));
            controls.Add(BuildToggle("Enemies", _showEnemies, v => _showEnemies = v));
            controls.Add(BuildToggle("NPCs", _showNpcs, v => _showNpcs = v));

            var closeButton = new Button(() => ToggleWorldMap()) { text = "Close" };
            closeButton.AddToClassList("worldmap-button");
            controls.Add(closeButton);

            return controls;
        }

        private VisualElement BuildSlider(string label, float min, float max, float value, System.Action<float> onChange)
        {
            var row = new VisualElement();
            row.AddToClassList("worldmap-control-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("worldmap-control-label");
            row.Add(labelElement);

            var slider = new Slider(min, max, SliderDirection.Horizontal, 0.01f)
            {
                value = value
            };
            slider.AddToClassList("worldmap-slider");
            slider.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            row.Add(slider);

            return row;
        }

        private VisualElement BuildToggle(string label, bool value, System.Action<bool> onChange)
        {
            var toggle = new Toggle(label)
            {
                value = value
            };
            toggle.AddToClassList("worldmap-toggle");
            toggle.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            return toggle;
        }

        private void ApplyStyles()
        {
            if (_config == null)
                return;

            int size = _config.MinimapSize;
            _minimapContainer.style.position = Position.Absolute;
            _minimapContainer.style.width = size;
            _minimapContainer.style.height = size;

            var anchor = _config.MinimapAnchor;
            var offset = _config.MinimapOffset;

            _minimapContainer.style.left = anchor.x <= 0.5f ? offset.x : StyleKeyword.Auto;
            _minimapContainer.style.right = anchor.x > 0.5f ? Mathf.Abs(offset.x) : StyleKeyword.Auto;
            _minimapContainer.style.top = anchor.y > 0.5f ? Mathf.Abs(offset.y) : StyleKeyword.Auto;
            _minimapContainer.style.bottom = anchor.y <= 0.5f ? offset.y : StyleKeyword.Auto;

            _minimapContainer.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            _minimapContainer.style.borderTopLeftRadius = 8;
            _minimapContainer.style.borderTopRightRadius = 8;
            _minimapContainer.style.borderBottomLeftRadius = 8;
            _minimapContainer.style.borderBottomRightRadius = 8;
            _minimapContainer.style.overflow = Overflow.Hidden;

            _minimapImage.style.position = Position.Absolute;
            _minimapImage.style.left = 0;
            _minimapImage.style.top = 0;
            _minimapImage.style.width = Length.Percent(100);
            _minimapImage.style.height = Length.Percent(100);

            _minimapIconsContainer.style.position = Position.Absolute;
            _minimapIconsContainer.style.left = 0;
            _minimapIconsContainer.style.top = 0;
            _minimapIconsContainer.style.width = Length.Percent(100);
            _minimapIconsContainer.style.height = Length.Percent(100);

            _worldMapContainer.style.position = Position.Absolute;
            _worldMapContainer.style.left = 0;
            _worldMapContainer.style.top = 0;
            _worldMapContainer.style.width = Length.Percent(100);
            _worldMapContainer.style.height = Length.Percent(100);
            _worldMapContainer.style.backgroundColor = new Color(0, 0, 0, 0.85f);

            _worldMapImage.style.position = Position.Absolute;
            _worldMapImage.style.left = 0;
            _worldMapImage.style.top = 0;
            _worldMapImage.style.width = Length.Percent(100);
            _worldMapImage.style.height = Length.Percent(100);

            _worldMapIconsContainer.style.position = Position.Absolute;
            _worldMapIconsContainer.style.left = 0;
            _worldMapIconsContainer.style.top = 0;
            _worldMapIconsContainer.style.width = Length.Percent(100);
            _worldMapIconsContainer.style.height = Length.Percent(100);
        }

        private void UpdateMapImages()
        {
            if (_service?.WorldTexture == null)
                return;

            var tex = _service.WorldTexture;

            _minimapImage.style.backgroundImage = tex;
            _minimapImage.style.unityBackgroundImageTintColor = new Color(1, 1, 1, _opacity);

            _worldMapImage.style.backgroundImage = tex;
            _worldMapImage.style.unityBackgroundImageTintColor = new Color(1, 1, 1, _opacity);

            PositionMapImage(_minimapImage, _minimapContainer, _minimapZoom);
            PositionMapImage(_worldMapImage, _worldMapContainer, _worldMapZoom);
        }

        private void PositionMapImage(VisualElement image, VisualElement container, float zoom)
        {
            if (_service == null || !_service.IsMapReady)
                return;

            var player = GetPlayerTexturePosition();
            if (!player.HasValue)
                return;

            float pixelsPerUnit = _config.PixelsPerUnit * zoom;
            float containerWidth = container.resolvedStyle.width;
            float containerHeight = container.resolvedStyle.height;

            if (containerWidth <= 0 || containerHeight <= 0)
                return;

            float mapWidth = _service.WorldBounds.width * pixelsPerUnit;
            float mapHeight = _service.WorldBounds.height * pixelsPerUnit;

            // Center container on player
            float offsetX = containerWidth * 0.5f - player.Value.x * zoom;
            float offsetY = containerHeight * 0.5f - player.Value.y * zoom;

            image.style.width = mapWidth;
            image.style.height = mapHeight;
            image.style.left = offsetX;
            image.style.top = offsetY;
        }

        private Vector2? GetPlayerTexturePosition()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
                return null;

            return _service.WorldToTexture(player.transform.position);
        }

        private void UpdateEntityIcons()
        {
            if (_service == null || !_service.IsMapReady)
                return;

            var playerPos = GetPlayerTexturePosition();
            if (!playerPos.HasValue)
                return;

            var activeContainer = _showWorldMap ? _worldMapIconsContainer : _minimapIconsContainer;
            float zoom = _showWorldMap ? _worldMapZoom : _minimapZoom;
            var container = _showWorldMap ? _worldMapContainer : _minimapContainer;
            float containerCenterX = container.resolvedStyle.width * 0.5f;
            float containerCenterY = container.resolvedStyle.height * 0.5f;

            var seen = new HashSet<MapEntity>();

            // Player icon
            if (_showPlayer)
            {
                var playerIcon = GetOrCreateIcon(null, "Player");
                ConfigureIcon(playerIcon, _config.PlayerIcon, Color.white, 10f);
                playerIcon.style.left = containerCenterX - 5f;
                playerIcon.style.top = containerCenterY - 5f;
                activeContainer.Add(playerIcon);
            }

            foreach (var entity in _service.TrackedEntities)
            {
                if (entity == null)
                    continue;

                bool visible = entity.Type switch
                {
                    MapEntityType.Enemy => _showEnemies,
                    MapEntityType.Npc => _showNpcs,
                    MapEntityType.Player => _showPlayer,
                    _ => true,
                };

                if (!visible)
                    continue;

                if (_showMinimap && !entity.ShowOnMinimap)
                    continue;
                if (_showWorldMap && !entity.ShowOnWorldMap)
                    continue;

                var entityTexPos = _service.WorldToTexture(entity.WorldPosition);
                Vector2 delta = (entityTexPos - playerPos.Value) * zoom;

                float iconX = containerCenterX + delta.x;
                float iconY = containerCenterY + delta.y;

                var icon = GetOrCreateIcon(entity);
                ConfigureIcon(icon, entity.CustomIcon ?? _config.GetIconForType(entity.Type), entity.CustomColor, entity.IconSize);
                icon.style.left = iconX - entity.IconSize * 0.5f;
                icon.style.top = iconY - entity.IconSize * 0.5f;
                activeContainer.Add(icon);

                seen.Add(entity);
            }

            // Return unused icons to pool
            var toRemove = new List<MapEntity>();
            foreach (var kvp in _entityIcons)
            {
                if (!seen.Contains(kvp.Key))
                {
                    ReturnIcon(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
                _entityIcons.Remove(key);
        }

        private void UpdateIconVisibility()
        {
            _minimapIconsContainer.style.display = _showMinimap && !_showWorldMap ? DisplayStyle.Flex : DisplayStyle.None;
            _worldMapIconsContainer.style.display = _showWorldMap ? DisplayStyle.Flex : DisplayStyle.None;

            if (_minimapIconsContainer.childCount > 0 && _minimapIconsContainer.parent != _minimapContainer && _showMinimap && !_showWorldMap)
            {
                _minimapIconsContainer.RemoveFromHierarchy();
                _minimapContainer.Add(_minimapIconsContainer);
            }
            else if (_worldMapIconsContainer.childCount > 0 && _worldMapIconsContainer.parent != _worldMapContainer && _showWorldMap)
            {
                _worldMapIconsContainer.RemoveFromHierarchy();
                _worldMapContainer.Add(_worldMapIconsContainer);
            }
        }

        private VisualElement GetOrCreateIcon(MapEntity entity, string fallbackName = null)
        {
            if (entity != null && _entityIcons.TryGetValue(entity, out var existing))
                return existing;

            VisualElement icon;
            if (_iconPool.Count > 0)
            {
                icon = _iconPool.Dequeue();
            }
            else
            {
                icon = new VisualElement();
                icon.AddToClassList("worldmap-icon");
            }

            icon.name = entity != null ? $"Icon_{entity.GetInstanceID()}" : fallbackName ?? "Icon";

            if (entity != null)
                _entityIcons[entity] = icon;

            return icon;
        }

        private void ConfigureIcon(VisualElement icon, Sprite sprite, Color color, float size)
        {
            icon.style.position = Position.Absolute;
            icon.style.width = size;
            icon.style.height = size;
            icon.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : null;
            icon.style.backgroundColor = sprite == null ? color : Color.clear;
            icon.style.borderTopLeftRadius = size * 0.5f;
            icon.style.borderTopRightRadius = size * 0.5f;
            icon.style.borderBottomLeftRadius = size * 0.5f;
            icon.style.borderBottomRightRadius = size * 0.5f;
        }

        private void ReturnIcon(VisualElement icon)
        {
            if (icon == null)
                return;

            icon.RemoveFromHierarchy();
            _iconPool.Enqueue(icon);
        }
    }
}
