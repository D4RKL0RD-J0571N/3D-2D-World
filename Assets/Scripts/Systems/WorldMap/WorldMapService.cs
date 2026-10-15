namespace Systems.WorldMap
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Data.Config;
    using Domain.Entities;
    using Domain.LevelGeneration;
    using Gameplay.World;
    using UnityEngine;

    /// <summary>
    /// Service that builds and maintains a top-down world map / minimap.
    /// Reveals rooms as the player enters them and tracks map-registered entities.
    /// </summary>
    public sealed class WorldMapService : IGameService, ITickable
    {
        private WorldMapConfig _config;
        private LevelLayout _layout;
        private Texture2D _worldTexture;
        private Rect _worldBounds;
        private readonly HashSet<string> _revealedRooms = new();
        private readonly List<MapEntity> _entities = new();
        private Transform _playerTransform;
        private bool _needsApply;
        private bool _mapReady;

        public Texture2D WorldTexture => _worldTexture;
        public Rect WorldBounds => _worldBounds;
        public bool IsMapReady => _mapReady;
        public IReadOnlyList<MapEntity> TrackedEntities => _entities;

        public void Initialize()
        {
            GameEventManager.AddListener<LevelGenerationCompletedEvent>(OnLevelGenerated);
            GameEventManager.AddListener<RoomEnteredEvent>(OnRoomEntered);
            GameEventManager.AddListener<AreaTierChangedEvent>(OnAreaTierChanged);

            LoadConfig();
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<LevelGenerationCompletedEvent>(OnLevelGenerated);
            GameEventManager.RemoveListener<RoomEnteredEvent>(OnRoomEntered);
            GameEventManager.RemoveListener<AreaTierChangedEvent>(OnAreaTierChanged);

            _entities.Clear();
            _revealedRooms.Clear();
            _layout = null;
            _mapReady = false;

            if (_worldTexture != null)
            {
                UnityEngine.Object.Destroy(_worldTexture);
                _worldTexture = null;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                    _playerTransform = player.transform;
            }

            if (_needsApply && _worldTexture != null)
            {
                _worldTexture.Apply(false);
                _needsApply = false;
            }
        }

        public void SetConfig(WorldMapConfig config)
        {
            _config = config;
            if (_layout != null)
                BuildMap(_layout);
        }

        public void RegisterEntity(MapEntity entity)
        {
            if (entity == null || _entities.Contains(entity))
                return;

            _entities.Add(entity);
        }

        public void UnregisterEntity(MapEntity entity)
        {
            if (entity == null)
                return;

            _entities.Remove(entity);
        }

        public Vector2 WorldToMap(Vector3 worldPosition)
        {
            return new Vector2(
                worldPosition.x - _worldBounds.x,
                worldPosition.z - _worldBounds.y);
        }

        public Vector2 MapToTexture(Vector2 mapPosition)
        {
            if (_worldTexture == null || !_mapReady)
                return Vector2.zero;

            float scale = _config != null ? _config.PixelsPerUnit : 2;
            return new Vector2(
                mapPosition.x * scale,
                mapPosition.y * scale);
        }

        public Vector2 WorldToTexture(Vector3 worldPosition)
        {
            return MapToTexture(WorldToMap(worldPosition));
        }

        private void LoadConfig()
        {
            _config = Resources.Load<WorldMapConfig>("Config/WorldMapConfig");
#if UNITY_EDITOR
            if (_config == null)
            {
                _config = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldMapConfig>("Assets/Resources/Config/WorldMapConfig.asset");
            }
#endif
        }

        private void OnLevelGenerated(LevelGenerationCompletedEvent evt)
        {
            _layout = evt?.Layout;
            if (_layout == null)
                return;

            BuildMap(_layout);
        }

        private void OnRoomEntered(RoomEnteredEvent evt)
        {
            if (evt == null || _layout == null)
                return;

            RevealRoom(evt.RoomId);

            if (_config != null && _config.RevealNeighborDistance > 0)
            {
                var center = _layout.Rooms.Find(r => r.Id == evt.RoomId);
                if (center != null)
                {
                    foreach (var room in _layout.Rooms)
                    {
                        int distance = Mathf.Abs(room.GridPosition.x - center.GridPosition.x) +
                                       Mathf.Abs(room.GridPosition.y - center.GridPosition.y);

                        if (distance <= _config.RevealNeighborDistance)
                            RevealRoom(room.Id);
                    }
                }
            }
        }

        private void OnAreaTierChanged(AreaTierChangedEvent evt)
        {
            if (evt == null || evt.NewTier == AreaSimulationTier.Purged)
                return;

            RevealRoom(evt.RoomId);
        }

        private void BuildMap(LevelLayout layout)
        {
            if (_config == null)
            {
                Debug.LogWarning("[WorldMapService] No WorldMapConfig assigned.");
                return;
            }

            _revealedRooms.Clear();

            if (_worldTexture != null)
            {
                UnityEngine.Object.Destroy(_worldTexture);
                _worldTexture = null;
            }

            ComputeWorldBounds(layout);
            int width = Mathf.CeilToInt(_worldBounds.width * _config.PixelsPerUnit);
            int height = Mathf.CeilToInt(_worldBounds.height * _config.PixelsPerUnit);
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            _worldTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            ClearTexture(_config.UnexploredColor);

            foreach (var room in layout.Rooms)
            {
                DrawRoom(room, revealed: false);
            }

            _mapReady = true;
            _needsApply = true;

            if (layout.StartRoom != null)
                RevealRoom(layout.StartRoom.Id);
        }

        private void ComputeWorldBounds(LevelLayout layout)
        {
            float minX = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxZ = float.MinValue;

            foreach (var room in layout.Rooms)
            {
                var halfSize = new Vector2(room.Size.x * 0.5f, room.Size.y * 0.5f);
                var center = new Vector2(room.WorldPosition.x, room.WorldPosition.z);

                minX = Mathf.Min(minX, center.x - halfSize.x);
                minZ = Mathf.Min(minZ, center.y - halfSize.y);
                maxX = Mathf.Max(maxX, center.x + halfSize.x);
                maxZ = Mathf.Max(maxZ, center.y + halfSize.y);
            }

            _worldBounds = new Rect(minX, minZ, maxX - minX, maxZ - minZ);
        }

        private void RevealRoom(string roomId)
        {
            if (_revealedRooms.Contains(roomId))
                return;

            _revealedRooms.Add(roomId);

            var room = _layout?.Rooms.Find(r => r.Id == roomId);
            if (room != null)
            {
                DrawRoom(room, revealed: true);
                DrawConnections(room);
            }

            _needsApply = true;
        }

        private void DrawRoom(LevelRoomNode room, bool revealed)
        {
            if (_worldTexture == null)
                return;

            var color = revealed ? _config.FloorColor : _config.UnexploredColor;
            var wallColor = revealed ? _config.WallColor : _config.UnexploredColor;

            var rect = GetRoomRect(room);
            int x0 = WorldXToPixel(rect.xMin);
            int y0 = WorldZToPixel(rect.yMin);
            int x1 = WorldXToPixel(rect.xMax);
            int y1 = WorldZToPixel(rect.yMax);

            x0 = Mathf.Clamp(x0, 0, _worldTexture.width - 1);
            y0 = Mathf.Clamp(y0, 0, _worldTexture.height - 1);
            x1 = Mathf.Clamp(x1, 0, _worldTexture.width - 1);
            y1 = Mathf.Clamp(y1, 0, _worldTexture.height - 1);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    bool isBorder = x == x0 || x == x1 || y == y0 || y == y1;
                    _worldTexture.SetPixel(x, y, isBorder ? wallColor : color);
                }
            }
        }

        private void DrawConnections(LevelRoomNode room)
        {
            if (_worldTexture == null || _layout?.Connections == null)
                return;

            var start = new Vector2(room.WorldPosition.x, room.WorldPosition.z);
            int startPx = WorldXToPixel(start.x);
            int startPy = WorldZToPixel(start.y);

            foreach (var connection in _layout.Connections)
            {
                if (connection.FromRoomId != room.Id)
                    continue;

                var target = _layout.Rooms.Find(r => r.Id == connection.ToRoomId);
                if (target == null || !_revealedRooms.Contains(target.Id))
                    continue;

                var end = new Vector2(target.WorldPosition.x, target.WorldPosition.z);
                int endPx = WorldXToPixel(end.x);
                int endPy = WorldZToPixel(end.y);

                DrawLine(startPx, startPy, endPx, endPy, _config.CorridorColor);
            }
        }

        private void DrawLine(int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                if (x0 >= 0 && x0 < _worldTexture.width && y0 >= 0 && y0 < _worldTexture.height)
                    _worldTexture.SetPixel(x0, y0, color);

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private void ClearTexture(Color color)
        {
            if (_worldTexture == null)
                return;

            int count = _worldTexture.width * _worldTexture.height;
            var pixels = new Color[count];
            for (int i = 0; i < count; i++)
                pixels[i] = color;

            _worldTexture.SetPixels(pixels);
        }

        private Rect GetRoomRect(LevelRoomNode room)
        {
            return new Rect(
                room.WorldPosition.x - room.Size.x * 0.5f,
                room.WorldPosition.z - room.Size.y * 0.5f,
                room.Size.x,
                room.Size.y);
        }

        private int WorldXToPixel(float worldX)
        {
            return Mathf.FloorToInt((worldX - _worldBounds.x) * _config.PixelsPerUnit);
        }

        private int WorldZToPixel(float worldZ)
        {
            return Mathf.FloorToInt((worldZ - _worldBounds.y) * _config.PixelsPerUnit);
        }
    }
}
