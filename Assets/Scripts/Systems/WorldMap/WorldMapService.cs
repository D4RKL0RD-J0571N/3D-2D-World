namespace Systems.WorldMap
{
    using System.Collections.Generic;
    using Core;
    using Data.Config;
    using Domain.Entities;
    using Domain.LevelGeneration;
    using Gameplay.World;
    using UnityEngine;

    /// <summary>
    /// Runtime data for a single room on the world map.
    /// </summary>
    public sealed class RoomMapData
    {
        public string RoomId;
        public Vector2Int GridPosition;
        public Rect WorldRect;
        public bool IsRevealed;
    }

    /// <summary>
    /// Lightweight connection data between two rooms.
    /// </summary>
    public readonly struct RoomConnection
    {
        public readonly string FromRoomId;
        public readonly string ToRoomId;

        public RoomConnection(string fromRoomId, string toRoomId)
        {
            FromRoomId = fromRoomId;
            ToRoomId = toRoomId;
        }
    }

    /// <summary>
    /// Service that maintains a lightweight, UI Toolkit-friendly world map model.
    /// Tracks revealed rooms and map-registered entities without generating textures.
    /// </summary>
    public sealed class WorldMapService : IGameService, ITickable
    {
        private WorldMapConfig _config;
        private LevelLayout _layout;
        private readonly List<RoomMapData> _rooms = new();
        private readonly List<RoomConnection> _connections = new();
        private readonly List<MapEntity> _entities = new();
        private Transform _playerTransform;
        private Rect _worldBounds;
        private bool _mapReady;

        public IReadOnlyList<RoomMapData> Rooms => _rooms;
        public IReadOnlyList<RoomConnection> Connections => _connections;
        public IReadOnlyList<MapEntity> TrackedEntities => _entities;
        public Rect WorldBounds => _worldBounds;
        public bool IsMapReady => _mapReady;
        public WorldMapConfig Config => _config;

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
            _rooms.Clear();
            _layout = null;
            _mapReady = false;
        }

        public void Tick(float deltaTime)
        {
            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                    _playerTransform = player.transform;
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

        public bool TryGetRoom(string roomId, out RoomMapData room)
        {
            foreach (var r in _rooms)
            {
                if (r.RoomId == roomId)
                {
                    room = r;
                    return true;
                }
            }

            room = null;
            return false;
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
            if (evt == null)
                return;

            RevealRoom(evt.RoomId);

            if (_config != null && _config.RevealNeighborDistance > 0 && TryGetRoom(evt.RoomId, out var center))
            {
                foreach (var room in _rooms)
                {
                    int distance = Mathf.Abs(room.GridPosition.x - center.GridPosition.x) +
                                   Mathf.Abs(room.GridPosition.y - center.GridPosition.y);

                    if (distance <= _config.RevealNeighborDistance)
                        RevealRoom(room.RoomId);
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
            _rooms.Clear();
            _connections.Clear();
            _mapReady = false;

            ComputeWorldBounds(layout);

            foreach (var room in layout.Rooms)
            {
                _rooms.Add(new RoomMapData
                {
                    RoomId = room.Id,
                    GridPosition = room.GridPosition,
                    WorldRect = new Rect(
                        room.WorldPosition.x - room.Size.x * 0.5f,
                        room.WorldPosition.z - room.Size.y * 0.5f,
                        room.Size.x,
                        room.Size.y),
                    IsRevealed = false
                });
            }

            if (layout.Connections != null)
            {
                foreach (var connection in layout.Connections)
                {
                    // Store each connection once
                    if (string.Compare(connection.FromRoomId, connection.ToRoomId, System.StringComparison.Ordinal) < 0)
                    {
                        _connections.Add(new RoomConnection(connection.FromRoomId, connection.ToRoomId));
                    }
                }
            }

            _mapReady = true;

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
            if (TryGetRoom(roomId, out var room) && !room.IsRevealed)
            {
                room.IsRevealed = true;
            }
        }
    }
}
