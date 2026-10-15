namespace Domain.LevelGeneration
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    public class LevelGenerationParameters
    {
        public int Seed = -1;
        public int MainPathRooms = 6;
        public int BranchRooms = 5;
        public int MinRoomSize = 12;
        public int MaxRoomSize = 22;
        public int GridSpacing = 32;
        public float BranchChance = 0.6f;
        public float CycleChance = 0.35f;
        public Vector2Int GridHalfExtents = new Vector2Int(6, 6);

        // Add these two
        [Tooltip("Maximum distance to cast ray to find the ground.")]
        public float RaycastDistance = 50f;

        [Tooltip("Vertical offset to start the ground check from.")]
        public Vector3 SpawnOffset = new Vector3(0, 5f, 0);
    }

    [Serializable]
    public class LevelRoomNode
    {
        public string Id;
        public Vector2Int GridPosition;
        public Vector3 WorldPosition;
        public Vector2Int Size;
        public int FloorLevel = 0; // Vertical floor tier (e.g. 0, 1, -1)
        public bool IsStart;
        public bool IsExit;
        public RoomArchetype Archetype;
        public List<Domain.Grid.RoomSocket> Sockets = new();
    }

    [Serializable]
    public class LevelConnection
    {
        public string FromRoomId;
        public string ToRoomId;
        public Vector2Int Direction;
        public bool IsMainPath;
    }

    [Serializable]
    public class LevelLayout
    {
        public int Seed;
        public List<LevelRoomNode> Rooms = new();
        public List<LevelConnection> Connections = new();

        public LevelRoomNode StartRoom => Rooms.Find(room => room.IsStart);
        public LevelRoomNode ExitRoom => Rooms.Find(room => room.IsExit);

        public bool AreConnected(string roomA, string roomB)
        {
            return Connections.Exists(c =>
                (c.FromRoomId == roomA && c.ToRoomId == roomB) ||
                (c.FromRoomId == roomB && c.ToRoomId == roomA));
        }
    }

    public sealed class RoomGraphGenerator : IRoomGraphGenerator
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };


        public LevelLayout Generate(LevelGenerationParameters parameters)
        {
            var seed = parameters.Seed >= 0 ? parameters.Seed : UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            var layout = new LevelLayout { Seed = seed };
            var random = new System.Random(seed);
            var occupied = new Dictionary<Vector2Int, LevelRoomNode>();

            var startPosition = Vector2Int.zero;
            var startRoom = CreateRoom(layout, occupied, random, startPosition, true, false, parameters);
            var currentRoom = startRoom;
            var previousDirection = Vector2Int.zero;

            // 1. Main Path
            int currentTier = 0;
            for (var i = 1; i < Mathf.Max(2, parameters.MainPathRooms); i++)
            {
                if (!TryExtendPath(layout, occupied, random, currentRoom, parameters, true, out var nextRoom, out var direction, previousDirection))
                {
                    break;
                }

                // ~40% chance to climb or descend a floor tier on the main path
                if (random.NextDouble() < 0.45)
                {
                    currentTier += (random.NextDouble() < 0.5) ? 1 : -1;
                    currentTier = Mathf.Clamp(currentTier, -2, 2);
                }
                nextRoom.FloorLevel = currentTier;

                currentRoom = nextRoom;
                previousDirection = direction;
            }

            if (layout.Rooms.Count > 0)
            {
                layout.Rooms[layout.Rooms.Count - 1].IsExit = true;
            }

            // 2. Branches
            var branchAttempts = 0;
            var maxAttempts = parameters.BranchRooms * 3;
            var branchesCreated = 0;
            while (branchesCreated < parameters.BranchRooms && branchAttempts < maxAttempts)
            {
                branchAttempts++;
                var branchSource = layout.Rooms[random.Next(layout.Rooms.Count)];
                if (TryExtendBranch(layout, occupied, random, branchSource, parameters, out var branchRoom))
                {
                    // Branch can match source tier or deviate by 1
                    int branchTier = branchSource.FloorLevel;
                    if (random.NextDouble() < 0.4)
                    {
                        branchTier += (random.NextDouble() < 0.5) ? 1 : -1;
                        branchTier = Mathf.Clamp(branchTier, -2, 2);
                    }
                    branchRoom.FloorLevel = branchTier;

                    branchesCreated++;
                }
            }

            // 3. Cyclic Dungeon Generation (Adding loops)
            AddCycles(layout, occupied, random, parameters.CycleChance);

            return layout;
        }

        private static void AddCycles(LevelLayout layout, Dictionary<Vector2Int, LevelRoomNode> occupied, System.Random random, float cycleChance)
        {
            foreach (var room in layout.Rooms)
            {
                foreach (var dir in CardinalDirections)
                {
                    var neighborPos = room.GridPosition + dir;
                    if (occupied.TryGetValue(neighborPos, out var neighborRoom))
                    {
                        if (!layout.AreConnected(room.Id, neighborRoom.Id))
                        {
                            if (random.NextDouble() < cycleChance)
                            {
                                layout.Connections.Add(new LevelConnection
                                {
                                    FromRoomId = room.Id,
                                    ToRoomId = neighborRoom.Id,
                                    Direction = dir,
                                    IsMainPath = false
                                });
                            }
                        }
                    }
                }
            }
        }

        private static LevelRoomNode CreateRoom(
            LevelLayout layout,
            Dictionary<Vector2Int, LevelRoomNode> occupied,
            System.Random random,
            Vector2Int gridPosition,
            bool isStart,
            bool isExit,
            LevelGenerationParameters parameters)
        {
            // Enforce even sizes to ensure doorways perfectly align to grid coordinates
            var sizeX = random.Next(parameters.MinRoomSize / 2, (parameters.MaxRoomSize / 2) + 1) * 2;
            var sizeY = random.Next(parameters.MinRoomSize / 2, (parameters.MaxRoomSize / 2) + 1) * 2;

            var room = new LevelRoomNode
            {
                Id = $"Room_{layout.Rooms.Count}",
                GridPosition = gridPosition,
                Size = new Vector2Int(sizeX, sizeY),
                WorldPosition = new Vector3(gridPosition.x * parameters.GridSpacing, 0f, gridPosition.y * parameters.GridSpacing),
                IsStart = isStart,
                IsExit = isExit
            };

            layout.Rooms.Add(room);
            occupied[gridPosition] = room;
            return room;
        }

        private static bool TryExtendPath(
            LevelLayout layout,
            Dictionary<Vector2Int, LevelRoomNode> occupied,
            System.Random random,
            LevelRoomNode sourceRoom,
            LevelGenerationParameters parameters,
            bool isMainPath,
            out LevelRoomNode nextRoom,
            out Vector2Int direction,
            Vector2Int previousDirection)
        {
            nextRoom = null;
            direction = Vector2Int.zero;

            var shuffledDirections = ShuffleDirections(random, previousDirection);
            foreach (var candidateDirection in shuffledDirections)
            {
                var candidateGridPosition = sourceRoom.GridPosition + candidateDirection;
                if (!IsWithinBounds(candidateGridPosition, parameters.GridHalfExtents))
                {
                    continue;
                }

                if (occupied.ContainsKey(candidateGridPosition))
                {
                    continue;
                }

                nextRoom = CreateRoom(layout, occupied, random, candidateGridPosition, false, false, parameters);
                direction = candidateDirection;
                layout.Connections.Add(new LevelConnection
                {
                    FromRoomId = sourceRoom.Id,
                    ToRoomId = nextRoom.Id,
                    Direction = candidateDirection,
                    IsMainPath = isMainPath
                });
                return true;
            }

            return false;
        }

        private static bool TryExtendBranch(
            LevelLayout layout,
            Dictionary<Vector2Int, LevelRoomNode> occupied,
            System.Random random,
            LevelRoomNode sourceRoom,
            LevelGenerationParameters parameters,
            out LevelRoomNode branchRoom)
        {
            branchRoom = null;
            if (random.NextDouble() > parameters.BranchChance)
            {
                return false;
            }
            return TryExtendPath(layout, occupied, random, sourceRoom, parameters, false, out branchRoom, out _, Vector2Int.zero);
        }

        private static Vector2Int[] ShuffleDirections(System.Random random, Vector2Int avoidDirection)
        {
            var directions = new List<Vector2Int>(CardinalDirections);
            for (var i = directions.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = directions[i];
                directions[i] = directions[swapIndex];
                directions[swapIndex] = temp;
            }

            if (avoidDirection != Vector2Int.zero)
            {
                directions.Remove(avoidDirection);
                directions.Add(avoidDirection);
            }

            return directions.ToArray();
        }

        private static bool IsWithinBounds(Vector2Int position, Vector2Int bounds)
        {
            return position.x >= -bounds.x && position.x <= bounds.x && position.y >= -bounds.y && position.y <= bounds.y;
        }


    }
}
