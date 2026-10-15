namespace Gameplay.UI
{
    using System.Collections.Generic;
    using Core;
    using Data.Config;
    using Gameplay.Player;
    using Systems.WorldMap;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// UI Toolkit HUD for the world map and minimap.
    /// Draws rooms, corridors, and tracked entities as VisualElements.
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
        private VisualElement _minimapContent;
        private VisualElement _minimapIconsContainer;
        private VisualElement _worldMapContainer;
        private VisualElement _worldMapContent;
        private VisualElement _worldMapIconsContainer;

        // Room / corridor visuals (one set per map mode)
        private readonly Dictionary<RoomMapData, VisualElement> _minimapRoomVisuals = new();
        private readonly Dictionary<RoomMapData, VisualElement> _worldMapRoomVisuals = new();
        private readonly List<VisualElement> _minimapCorridorVisuals = new();
        private readonly List<VisualElement> _worldMapCorridorVisuals = new();
        private bool _roomsBuilt;

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
        private VisualElement _playerIcon;
        private readonly Dictionary<MapEntity, VisualElement> _entityIcons = new();
        private readonly Queue<VisualElement> _iconPool = new();

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            ResolveConfig();
            SetupUIDocument();
            BuildUI();
        }

        private void ResolveConfig()
        {
            if (_config != null)
                return;

            if (ServiceLocator.TryGet<WorldMapService>(out var service))
                _config = service.Config;

            if (_config == null)
                _config = Resources.Load<WorldMapConfig>("Config/WorldMapConfig");

#if UNITY_EDITOR
            if (_config == null)
            {
                _config = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldMapConfig>("Assets/Resources/Config/WorldMapConfig.asset");
            }
#endif
        }

        private void Start()
        {
            if (!ServiceLocator.TryGet<WorldMapService>(out _service))
            {
                var service = new WorldMapService();
                ServiceLocator.Register(service);
                _service = service;
            }

            _config = _config ?? _service.Config;

            if (_config != null)
                _service.SetConfig(_config);
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<ToggleWorldMapInputEvent>(OnToggleWorldMap);
            GameEventManager.AddListener<ToggleMinimapInputEvent>(OnToggleMinimap);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<ToggleWorldMapInputEvent>(OnToggleWorldMap);
            GameEventManager.RemoveListener<ToggleMinimapInputEvent>(OnToggleMinimap);
        }

        private void Update()
        {
            if (_service == null || _config == null)
                return;

            if (_service.IsMapReady && !_roomsBuilt)
                BuildRoomVisuals();

            if (!_showMinimap && !_showWorldMap)
                return;

            UpdateMapTransforms();
            UpdateIconVisibility();

            _frameCounter++;
            if (_frameCounter % Mathf.Max(1, _config.EntityUpdateInterval) == 0)
            {
                UpdateEntityIcons();
            }
        }

        private void OnToggleWorldMap(ToggleWorldMapInputEvent evt)
        {
            ToggleWorldMap();
        }

        private void OnToggleMinimap(ToggleMinimapInputEvent evt)
        {
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

            // Minimap
            _minimapContainer = new VisualElement { name = "MinimapContainer" };
            _minimapContainer.AddToClassList("worldmap-minimap");

            _minimapContent = new VisualElement { name = "MinimapContent" };
            _minimapContent.AddToClassList("worldmap-content");

            _minimapIconsContainer = new VisualElement { name = "MinimapIconsContainer" };
            _minimapIconsContainer.AddToClassList("worldmap-icons");

            _minimapContainer.Add(_minimapContent);
            _minimapContainer.Add(_minimapIconsContainer);

            // World map
            _worldMapContainer = new VisualElement { name = "WorldMapContainer" };
            _worldMapContainer.AddToClassList("worldmap-fullscreen");

            _worldMapContent = new VisualElement { name = "WorldMapContent" };
            _worldMapContent.AddToClassList("worldmap-content");

            _worldMapIconsContainer = new VisualElement { name = "WorldMapIconsContainer" };
            _worldMapIconsContainer.AddToClassList("worldmap-icons");

            _worldMapContainer.Add(_worldMapContent);
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
            int size = _config != null ? _config.MinimapSize : 200;
            _minimapContainer.style.position = Position.Absolute;
            _minimapContainer.style.width = new Length(size);
            _minimapContainer.style.height = new Length(size);

            var anchor = _config != null ? _config.MinimapAnchor : new Vector2(1f, 1f);
            var offset = _config != null ? _config.MinimapOffset : new Vector2(-20f, -20f);

            _minimapContainer.style.left = anchor.x <= 0.5f ? new Length(offset.x) : StyleKeyword.Auto;
            _minimapContainer.style.right = anchor.x > 0.5f ? new Length(Mathf.Abs(offset.x)) : StyleKeyword.Auto;
            _minimapContainer.style.top = anchor.y > 0.5f ? new Length(Mathf.Abs(offset.y)) : StyleKeyword.Auto;
            _minimapContainer.style.bottom = anchor.y <= 0.5f ? new Length(offset.y) : StyleKeyword.Auto;

            _minimapContainer.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            _minimapContainer.style.borderTopLeftRadius = 8;
            _minimapContainer.style.borderTopRightRadius = 8;
            _minimapContainer.style.borderBottomLeftRadius = 8;
            _minimapContainer.style.borderBottomRightRadius = 8;
            _minimapContainer.style.overflow = Overflow.Hidden;

            _minimapContent.style.position = Position.Absolute;
            _minimapContent.style.left = 0;
            _minimapContent.style.top = 0;

            _minimapIconsContainer.style.position = Position.Absolute;
            _minimapIconsContainer.style.left = 0;
            _minimapIconsContainer.style.top = 0;
            _minimapIconsContainer.style.width = new Length(100, LengthUnit.Percent);
            _minimapIconsContainer.style.height = new Length(100, LengthUnit.Percent);

            _worldMapContainer.style.position = Position.Absolute;
            _worldMapContainer.style.left = 0;
            _worldMapContainer.style.top = 0;
            _worldMapContainer.style.width = new Length(100, LengthUnit.Percent);
            _worldMapContainer.style.height = new Length(100, LengthUnit.Percent);
            _worldMapContainer.style.backgroundColor = new Color(0, 0, 0, 0.85f);

            _worldMapContent.style.position = Position.Absolute;
            _worldMapContent.style.left = 0;
            _worldMapContent.style.top = 0;

            _worldMapIconsContainer.style.position = Position.Absolute;
            _worldMapIconsContainer.style.left = 0;
            _worldMapIconsContainer.style.top = 0;
            _worldMapIconsContainer.style.width = new Length(100, LengthUnit.Percent);
            _worldMapIconsContainer.style.height = new Length(100, LengthUnit.Percent);
        }

        private void BuildRoomVisuals()
        {
            _minimapContent.Clear();
            _worldMapContent.Clear();
            _minimapRoomVisuals.Clear();
            _worldMapRoomVisuals.Clear();
            _minimapCorridorVisuals.Clear();
            _worldMapCorridorVisuals.Clear();

            foreach (var room in _service.Rooms)
            {
                var minimapVisual = CreateRoomVisual($"Room_MM_{room.RoomId}");
                var worldMapVisual = CreateRoomVisual($"Room_WM_{room.RoomId}");

                _minimapRoomVisuals[room] = minimapVisual;
                _worldMapRoomVisuals[room] = worldMapVisual;

                _minimapContent.Add(minimapVisual);
                _worldMapContent.Add(worldMapVisual);
            }

            BuildCorridorVisuals();
            _roomsBuilt = true;
        }

        private VisualElement CreateRoomVisual(string name)
        {
            var visual = new VisualElement
            {
                name = name,
                pickingMode = PickingMode.Ignore
            };
            visual.AddToClassList("worldmap-room");
            return visual;
        }

        private void UpdateRoomVisuals()
        {
            foreach (var kvp in _minimapRoomVisuals)
                UpdateRoomVisual(kvp.Value, kvp.Key, _minimapZoom);

            foreach (var kvp in _worldMapRoomVisuals)
                UpdateRoomVisual(kvp.Value, kvp.Key, _worldMapZoom);
        }

        private void UpdateRoomVisual(VisualElement visual, RoomMapData room, float zoom)
        {
            var rect = room.WorldRect;
            float scale = _config.PixelsPerUnit * zoom;

            visual.style.position = Position.Absolute;
            visual.style.left = rect.xMin * scale;
            visual.style.top = rect.yMin * scale;
            visual.style.width = rect.width * scale;
            visual.style.height = rect.height * scale;
            visual.style.backgroundColor = room.IsRevealed ? _config.FloorColor : _config.UnexploredColor;
            visual.style.opacity = room.IsRevealed ? _opacity : _opacity * 0.5f;
            visual.style.display = room.IsRevealed || _showWorldMap ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BuildCorridorVisuals()
        {
            if (_service.Connections == null)
                return;

            foreach (var connection in _service.Connections)
            {
                var from = FindRoom(connection.FromRoomId);
                var to = FindRoom(connection.ToRoomId);

                if (from == null || to == null)
                    continue;

                var minimapVisual = CreateCorridorVisual($"Corridor_MM_{from.RoomId}_{to.RoomId}");
                var worldMapVisual = CreateCorridorVisual($"Corridor_WM_{from.RoomId}_{to.RoomId}");

                _minimapCorridorVisuals.Add(minimapVisual);
                _worldMapCorridorVisuals.Add(worldMapVisual);

                _minimapContent.Add(minimapVisual);
                _worldMapContent.Add(worldMapVisual);
            }
        }

        private RoomMapData FindRoom(string roomId)
        {
            foreach (var room in _service.Rooms)
            {
                if (room.RoomId == roomId)
                    return room;
            }
            return null;
        }

        private VisualElement CreateCorridorVisual(string name)
        {
            var visual = new VisualElement
            {
                name = name,
                pickingMode = PickingMode.Ignore
            };
            visual.AddToClassList("worldmap-corridor");
            return visual;
        }

        private void UpdateCorridorVisuals()
        {
            for (int i = 0; i < _minimapCorridorVisuals.Count && i < _service.Connections.Count; i++)
            {
                var connection = _service.Connections[i];
                var from = FindRoom(connection.FromRoomId);
                var to = FindRoom(connection.ToRoomId);
                if (from != null && to != null)
                    UpdateCorridorVisual(_minimapCorridorVisuals[i], from, to, _minimapZoom);
            }

            for (int i = 0; i < _worldMapCorridorVisuals.Count && i < _service.Connections.Count; i++)
            {
                var connection = _service.Connections[i];
                var from = FindRoom(connection.FromRoomId);
                var to = FindRoom(connection.ToRoomId);
                if (from != null && to != null)
                    UpdateCorridorVisual(_worldMapCorridorVisuals[i], from, to, _worldMapZoom);
            }
        }

        private void UpdateCorridorVisual(VisualElement visual, RoomMapData from, RoomMapData to, float zoom)
        {
            float scale = _config.PixelsPerUnit * zoom;
            Vector2 start = new Vector2(from.WorldRect.center.x * scale, from.WorldRect.center.y * scale);
            Vector2 end = new Vector2(to.WorldRect.center.x * scale, to.WorldRect.center.y * scale);

            Vector2 delta = end - start;
            float length = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float thickness = Mathf.Max(2f, _config.PixelsPerUnit * zoom);

            visual.style.position = Position.Absolute;
            visual.style.width = new Length(length);
            visual.style.height = new Length(thickness);
            visual.style.left = start.x;
            visual.style.top = start.y - thickness * 0.5f;
            visual.style.backgroundColor = _config.CorridorColor;
            visual.style.opacity = _opacity;
            visual.style.rotate = new Rotate(angle);
            visual.style.display = (from.IsRevealed && to.IsRevealed) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateMapTransforms()
        {
            if (_service == null || !_service.IsMapReady)
                return;

            var player = GameObject.FindWithTag("Player");
            if (player == null)
                return;

            var playerMapPos = _service.WorldToMap(player.transform.position) * _config.PixelsPerUnit;

            UpdateContentTransform(_minimapContent, _minimapContainer, _minimapZoom, playerMapPos);
            UpdateContentTransform(_worldMapContent, _worldMapContainer, _worldMapZoom, playerMapPos);

            // Keep room/corridor visuals in sync with reveal state and opacity
            UpdateRoomVisuals();
            UpdateCorridorVisuals();

            _minimapContent.style.opacity = _opacity;
            _worldMapContent.style.opacity = _opacity;
        }

        private void UpdateContentTransform(VisualElement content, VisualElement container, float zoom, Vector2 playerMapPos)
        {
            float containerWidth = container.resolvedStyle.width;
            float containerHeight = container.resolvedStyle.height;

            if (containerWidth <= 0 || containerHeight <= 0)
                return;

            float offsetX = containerWidth * 0.5f - playerMapPos.x * zoom;
            float offsetY = containerHeight * 0.5f - playerMapPos.y * zoom;

            content.style.left = offsetX;
            content.style.top = offsetY;
        }

        private void UpdateIconVisibility()
        {
            _minimapIconsContainer.style.display = _showMinimap && !_showWorldMap ? DisplayStyle.Flex : DisplayStyle.None;
            _worldMapIconsContainer.style.display = _showWorldMap ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateEntityIcons()
        {
            if (_service == null || !_service.IsMapReady)
                return;

            var player = GameObject.FindWithTag("Player");
            if (player == null)
                return;

            var playerMapPos = _service.WorldToMap(player.transform.position) * _config.PixelsPerUnit;

            var activeContainer = _showWorldMap ? _worldMapIconsContainer : _minimapIconsContainer;
            activeContainer.Clear();

            float zoom = _showWorldMap ? _worldMapZoom : _minimapZoom;
            var container = _showWorldMap ? _worldMapContainer : _minimapContainer;
            float containerWidth = container.resolvedStyle.width;
            float containerHeight = container.resolvedStyle.height;

            if (containerWidth <= 0 || containerHeight <= 0)
                return;

            float centerX = containerWidth * 0.5f;
            float centerY = containerHeight * 0.5f;

            if (_showPlayer)
            {
                if (_playerIcon == null)
                    _playerIcon = CreateIcon("PlayerIcon");

                ConfigureIcon(_playerIcon, _config.PlayerIcon, Color.white, 10f);
                activeContainer.Add(_playerIcon);
            }

            var seen = new HashSet<MapEntity>();

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

                var entityMapPos = _service.WorldToMap(entity.WorldPosition) * _config.PixelsPerUnit;
                Vector2 delta = (entityMapPos - playerMapPos) * zoom;

                float iconX = centerX + delta.x;
                float iconY = centerY + delta.y;

                if (!_entityIcons.TryGetValue(entity, out var icon))
                {
                    icon = CreateIcon($"Icon_{entity.GetInstanceID()}");
                    _entityIcons[entity] = icon;
                }

                ConfigureIcon(icon, entity.CustomIcon ?? _config.GetIconForType(entity.Type), entity.CustomColor, entity.IconSize);
                icon.style.left = iconX - entity.IconSize * 0.5f;
                icon.style.top = iconY - entity.IconSize * 0.5f;
                activeContainer.Add(icon);

                seen.Add(entity);
            }

            var toRemove = new List<MapEntity>();
            foreach (var kvp in _entityIcons)
            {
                if (kvp.Key == null || !seen.Contains(kvp.Key))
                {
                    ReturnIcon(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
                _entityIcons.Remove(key);
        }

        private VisualElement CreateIcon(string name)
        {
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

            icon.name = name;
            return icon;
        }

        private void ConfigureIcon(VisualElement icon, Sprite sprite, Color color, float size)
        {
            icon.style.position = Position.Absolute;
            icon.style.width = new Length(size);
            icon.style.height = new Length(size);
            icon.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : null;
            icon.style.backgroundColor = sprite == null ? color : Color.clear;
            icon.style.borderTopLeftRadius = new Length(size * 0.5f);
            icon.style.borderTopRightRadius = new Length(size * 0.5f);
            icon.style.borderBottomLeftRadius = new Length(size * 0.5f);
            icon.style.borderBottomRightRadius = new Length(size * 0.5f);
        }

        private void ReturnIcon(VisualElement icon)
        {
            if (icon == null)
                return;

            icon.RemoveFromHierarchy();
            icon.style.backgroundImage = null;
            _iconPool.Enqueue(icon);
        }
    }
}
