namespace Systems.Scheduler
{
    using System.Collections.Generic;
    using Core;
    using Domain.Entities;
    using Domain.LevelGeneration;
    using Gameplay.World;
    using UnityEngine;

    /// <summary>
    /// Service that schedules entity lifecycle, simulation tiers (Active, Dormant, Purged),
    /// off-screen entity sleep states, and time-based regeneration catch-up.
    /// </summary>
    public sealed class AreaSchedulerService : IGameService, ITickable
    {
        #region Fields

        private LevelLayout _currentLayout;
        private string _currentRoomId;
        private readonly Dictionary<string, AreaSimulationTier> _roomTiers = new();
        private readonly Dictionary<string, List<VirtualEntityState>> _virtualEntitiesByRoom = new();
        private readonly Dictionary<string, float> _roomDormantTimestamps = new();

        private const float RegenerationRatePerSecond = 2.0f; // 2 HP / sec off-screen regeneration
        private const int DormantManhattanDistance = 1;        // Adjacent rooms remain Dormant
        private const int PurgeManhattanDistance = 3;          // Further rooms become Purged

        #endregion

        #region Public Properties

        public string CurrentRoomId => _currentRoomId;

        #endregion

        #region Lifecycle Methods

        public void Initialize()
        {
            GameEventManager.AddListener<LevelGenerationCompletedEvent>(OnLevelGenerated);
            GameEventManager.AddListener<RoomEnteredEvent>(OnPlayerRoomEntered);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<LevelGenerationCompletedEvent>(OnLevelGenerated);
            GameEventManager.RemoveListener<RoomEnteredEvent>(OnPlayerRoomEntered);

            _roomTiers.Clear();
            _virtualEntitiesByRoom.Clear();
            _roomDormantTimestamps.Clear();
            _currentLayout = null;
            _currentRoomId = null;
        }

        public void Tick(float deltaTime)
        {
            // Lightweight periodical checks if needed
        }

        #endregion

        #region Entity Management

        /// <summary>
        /// Registers a spawned entity into its room's virtual registry.
        /// </summary>
        public void RegisterEntity(string roomId, VirtualEntityState state)
        {
            if (string.IsNullOrEmpty(roomId) || state == null)
                return;

            if (!_virtualEntitiesByRoom.TryGetValue(roomId, out var list))
            {
                list = new List<VirtualEntityState>();
                _virtualEntitiesByRoom[roomId] = list;
            }

            list.Add(state);
        }

        /// <summary>
        /// Puts an active entity to sleep in virtual storage when player leaves or looks away.
        /// </summary>
        public void PutEntityToSleep(string roomId, VirtualEntityState state)
        {
            if (state == null)
                return;

            state.LastActiveTimestamp = Time.time;
            RegisterEntity(roomId, state);
        }

        /// <summary>
        /// Retrieves all sleeping entities for a room and applies time-based regeneration catch-up.
        /// </summary>
        public List<VirtualEntityState> WakeRoomEntities(string roomId)
        {
            if (!_virtualEntitiesByRoom.TryGetValue(roomId, out var entities))
                return new List<VirtualEntityState>();

            float currentTime = Time.time;
            var awakenedList = new List<VirtualEntityState>();

            foreach (var entity in entities)
            {
                if (entity.LifecyclePolicy == EntityLifecyclePolicy.DespawnAndRespawn)
                {
                    // Fresh reset on re-entry
                    entity.CurrentHealth = entity.MaxHealth;
                    awakenedList.Add(entity);
                    continue;
                }

                // PersistAndSleep: Apply time-based regeneration catch-up
                float elapsed = Mathf.Max(0f, currentTime - entity.LastActiveTimestamp);
                int healthRegen = Mathf.RoundToInt(elapsed * RegenerationRatePerSecond);

                entity.CurrentHealth = Mathf.Min(entity.MaxHealth, entity.CurrentHealth + healthRegen);
                entity.LastActiveTimestamp = currentTime;

                awakenedList.Add(entity);
            }

            _virtualEntitiesByRoom[roomId].Clear();
            return awakenedList;
        }

        public AreaSimulationTier GetRoomTier(string roomId)
        {
            return _roomTiers.TryGetValue(roomId, out var tier) ? tier : AreaSimulationTier.Purged;
        }

        #endregion

        #region Event Handlers

        private void OnLevelGenerated(LevelGenerationCompletedEvent evt)
        {
            _currentLayout = evt.Layout;
            _roomTiers.Clear();
            _virtualEntitiesByRoom.Clear();
            _roomDormantTimestamps.Clear();

            if (_currentLayout == null)
                return;

            // Initialize all rooms as Purged
            foreach (var room in _currentLayout.Rooms)
            {
                _roomTiers[room.Id] = AreaSimulationTier.Purged;
            }

            if (_currentLayout.StartRoom != null)
            {
                UpdateRoomTiers(_currentLayout.StartRoom.Id);
            }
        }

        private void OnPlayerRoomEntered(RoomEnteredEvent evt)
        {
            if (evt == null || evt.RoomId == _currentRoomId)
                return;

            UpdateRoomTiers(evt.RoomId);
        }

        #endregion

        #region Private Helpers

        private void UpdateRoomTiers(string activeRoomId)
        {
            _currentRoomId = activeRoomId;
            if (_currentLayout == null)
                return;

            var activeRoom = _currentLayout.Rooms.Find(r => r.Id == activeRoomId);
            if (activeRoom == null)
                return;

            foreach (var room in _currentLayout.Rooms)
            {
                int distance = Mathf.Abs(room.GridPosition.x - activeRoom.GridPosition.x) +
                               Mathf.Abs(room.GridPosition.y - activeRoom.GridPosition.y);

                AreaSimulationTier targetTier;
                if (room.Id == activeRoomId)
                {
                    targetTier = AreaSimulationTier.Active;
                }
                else if (distance <= DormantManhattanDistance)
                {
                    targetTier = AreaSimulationTier.Dormant;
                }
                else
                {
                    targetTier = AreaSimulationTier.Purged;
                }

                var previousTier = _roomTiers.TryGetValue(room.Id, out var existing) ? existing : AreaSimulationTier.Purged;
                if (previousTier != targetTier)
                {
                    _roomTiers[room.Id] = targetTier;

                    if (targetTier == AreaSimulationTier.Dormant || targetTier == AreaSimulationTier.Purged)
                    {
                        _roomDormantTimestamps[room.Id] = Time.time;
                    }

                    GameEventManager.Publish(new AreaTierChangedEvent
                    {
                        RoomId = room.Id,
                        PreviousTier = previousTier,
                        NewTier = targetTier
                    });
                }
            }
        }

        #endregion
    }
}
