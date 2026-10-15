namespace Gameplay.World
{
    using System.Collections.Generic;
    using Domain.Grid;
    using UnityEngine;

    /// <summary>
    /// Utility for placing procedural voxel staircases and platforms into RoomGridData.
    /// All stairs are generated as standard axis-aligned stepped cubes for optimal Greedy Meshing.
    /// </summary>
    public static class VoxelStairBuilder
    {
        #region Public Methods

        /// <summary>
        /// Builds a straight staircase run between two points and heights in the grid.
        /// Uses a tread depth (run per step) to create gentle, natural slopes instead of steep 1x1 walls.
        /// </summary>
        public static void BuildStraightStairRun(
            RoomGridData grid,
            Vector2Int startCoord,
            Vector2Int direction,
            int length,
            int width,
            int startY,
            int heightDelta,
            ushort stepBlockId,
            int treadDepth = 2)
        {
            if (grid == null || length <= 0 || width <= 0)
                return;

            Vector2Int perpendicular = new Vector2Int(-direction.y, direction.x);
            int totalSteps = Mathf.Abs(heightDelta);
            int stepSign = heightDelta >= 0 ? 1 : -1;

            for (int step = 0; step < length; step++)
            {
                // Each step elevation holds for 'treadDepth' blocks before rising
                int currentStepIndex = Mathf.Min(step / Mathf.Max(1, treadDepth), totalSteps);
                int currentY = startY + (currentStepIndex * stepSign);

                Vector2Int basePos = startCoord + direction * step;

                for (int w = 0; w < width; w++)
                {
                    int offset = w - (width / 2);
                    Vector2Int blockPos = basePos + perpendicular * offset;

                    // Fill from lowest point up to current step height to ensure solid footing
                    for (int y = Mathf.Min(startY, currentY); y <= currentY; y++)
                    {
                        if (IsValidCoordinate(grid, blockPos.x, y, blockPos.y))
                        {
                            grid.SetBlock(blockPos.x, y, blockPos.y, stepBlockId);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Carves a sunken pit into a room with a raised perimeter walkway and stepped stairs leading down.
        /// </summary>
        public static void BuildSunkenPit(
            RoomGridData grid,
            int sizeX,
            int sizeZ,
            int walkwayMargin,
            int pitDepth,
            ushort floorBlockId,
            ushort stepBlockId)
        {
            if (grid == null || sizeX <= walkwayMargin * 2 + 2 || sizeZ <= walkwayMargin * 2 + 2)
                return;

            int minX = walkwayMargin;
            int maxX = sizeX - 1 - walkwayMargin;
            int minZ = walkwayMargin;
            int maxZ = sizeZ - 1 - walkwayMargin;

            // Step 1: Fill the walkway at base Y = pitDepth
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    bool isPerimeter = x < minX || x > maxX || z < minZ || z > maxZ;
                    if (isPerimeter)
                    {
                        for (int y = 0; y <= pitDepth; y++)
                        {
                            grid.SetBlock(x, y, z, floorBlockId);
                        }
                    }
                    else
                    {
                        // Pit bottom
                        grid.SetBlock(x, 0, z, floorBlockId);
                    }
                }
            }

            // Step 2: Add 4 stair ramps leading from perimeter down to center pit
            int centerX = sizeX / 2;

            int stairRunLength = Mathf.Max(pitDepth * 2, 4);

            // North stair (descending from maxZ down into center)
            BuildStraightStairRun(
                grid,
                new Vector2Int(centerX, maxZ),
                Vector2Int.down,
                stairRunLength,
                2,
                pitDepth,
                -pitDepth,
                stepBlockId,
                treadDepth: 2);

            // South stair (descending from minZ up into center)
            BuildStraightStairRun(
                grid,
                new Vector2Int(centerX, minZ),
                Vector2Int.up,
                stairRunLength,
                2,
                pitDepth,
                -pitDepth,
                stepBlockId,
                treadDepth: 2);
        }

        /// <summary>
        /// Builds a raised dais in the center of the room with stepped approach stairs.
        /// </summary>
        public static void BuildRaisedDais(
            RoomGridData grid,
            int sizeX,
            int sizeZ,
            int daisMargin,
            int daisHeight,
            ushort daisBlockId,
            ushort stepBlockId)
        {
            if (grid == null || sizeX <= daisMargin * 2 + 2 || sizeZ <= daisMargin * 2 + 2)
                return;

            int minX = daisMargin;
            int maxX = sizeX - 1 - daisMargin;
            int minZ = daisMargin;
            int maxZ = sizeZ - 1 - daisMargin;

            // Fill platform
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int y = 1; y <= daisHeight; y++)
                    {
                        grid.SetBlock(x, y, z, daisBlockId);
                    }
                }
            }

            int centerX = sizeX / 2;
            int stairRunLength = Mathf.Max(daisHeight * 2, 3);

            // South approach stairs ascending onto the dais
            BuildStraightStairRun(
                grid,
                new Vector2Int(centerX, Mathf.Max(0, minZ - stairRunLength)),
                Vector2Int.up,
                stairRunLength,
                2,
                0,
                daisHeight,
                stepBlockId,
                treadDepth: 2);
        }

        /// <summary>
        /// Builds columns/pillars inside a room.
        /// </summary>
        public static void BuildPillars(
            RoomGridData grid,
            int sizeX,
            int sizeZ,
            int roomHeight,
            int inset,
            int thickness,
            ushort pillarBlockId)
        {
            if (sizeX <= inset * 2 + thickness || sizeZ <= inset * 2 + thickness)
                return;

            Vector2Int[] pillarCorners = new Vector2Int[]
            {
                new Vector2Int(inset, inset),
                new Vector2Int(sizeX - 1 - inset - (thickness - 1), inset),
                new Vector2Int(inset, sizeZ - 1 - inset - (thickness - 1)),
                new Vector2Int(sizeX - 1 - inset - (thickness - 1), sizeZ - 1 - inset - (thickness - 1))
            };

            foreach (var corner in pillarCorners)
            {
                for (int px = 0; px < thickness; px++)
                {
                    for (int pz = 0; pz < thickness; pz++)
                    {
                        int targetX = corner.x + px;
                        int targetZ = corner.y + pz;

                        for (int y = 0; y < roomHeight; y++)
                        {
                            if (IsValidCoordinate(grid, targetX, y, targetZ))
                            {
                                grid.SetBlock(targetX, y, targetZ, pillarBlockId);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Carves alcoves/niches into solid walls, creating spots for torches, barrels, or chests.
        /// </summary>
        public static void CarveWallNiches(
            RoomGridData grid,
            int sizeX,
            int sizeZ,
            HashSet<Vector2Int> openings,
            ushort floorBlockId)
        {
            // North and South walls
            for (int x = 3; x < sizeX - 3; x += 4)
            {
                // South wall niche
                if (openings == null || !openings.Contains(Vector2Int.down))
                {
                    grid.SetBlock(x, 1, 0, 0); // Carve opening
                    grid.SetBlock(x, 2, 0, 0);
                    grid.SetBlock(x, 0, 0, floorBlockId);
                }

                // North wall niche
                if (openings == null || !openings.Contains(Vector2Int.up))
                {
                    grid.SetBlock(x, 1, sizeZ - 1, 0);
                    grid.SetBlock(x, 2, sizeZ - 1, 0);
                    grid.SetBlock(x, 0, sizeZ - 1, floorBlockId);
                }
            }
        }

        /// <summary>
        /// Places low half-wall obstacles and parapets inside larger rooms to create tactical combat zones and cover.
        /// </summary>
        public static void BuildLowCoverParapets(
            RoomGridData grid,
            int sizeX,
            int sizeZ,
            int height,
            ushort wallBlockId)
        {
            if (sizeX < 10 || sizeZ < 10) return;

            int centerX = sizeX / 2;
            int centerZ = sizeZ / 2;
            int halfLength = 2;

            // Horizontal cover bar with a central gap
            for (int x = centerX - halfLength - 2; x <= centerX - 2; x++)
            {
                for (int y = 1; y <= height; y++)
                {
                    grid.SetBlock(x, y, centerZ, wallBlockId);
                }
            }

            for (int x = centerX + 2; x <= centerX + halfLength + 2; x++)
            {
                for (int y = 1; y <= height; y++)
                {
                    grid.SetBlock(x, y, centerZ, wallBlockId);
                }
            }
        }

        /// <summary>
        /// Scans the room grid to discover valid floor positions for entity, chest, and breakable sockets.
        /// </summary>
        public static void DiscoverRoomSockets(
            RoomGridData grid,
            Vector3 worldOrigin,
            System.Collections.Generic.List<Domain.Grid.RoomSocket> outSockets,
            int maxSockets = 5)
        {
            if (grid == null || outSockets == null) return;

            int added = 0;
            // Scan interior (avoiding outermost walls and doorway corridors)
            for (int x = 2; x < grid.Size.x - 2 && added < maxSockets; x += 2)
            {
                for (int z = 2; z < grid.Size.z - 2 && added < maxSockets; z += 2)
                {
                    // Find highest solid block with air above it
                    for (int y = grid.Size.y - 2; y >= 0; y--)
                    {
                        if (grid.GetBlock(x, y, z) != 0 && grid.GetBlock(x, y + 1, z) == 0 && grid.GetBlock(x, y + 2, z) == 0)
                        {
                            Vector3Int localCoord = new Vector3Int(x, y + 1, z);
                            Vector3 worldPos = worldOrigin + new Vector3(x + 0.5f, y + 1f, z + 0.5f);

                            var socketType = (added == 0) ? Domain.Grid.SocketType.Chest : Domain.Grid.SocketType.EnemySpawn;
                            outSockets.Add(new Domain.Grid.RoomSocket(localCoord, worldPos, socketType, Vector3.forward));
                            added++;
                            break;
                        }
                    }
                }
            }
        }

        #endregion

        #region Private Helpers

        private static bool IsValidCoordinate(RoomGridData grid, int x, int y, int z)
        {
            return x >= 0 && x < grid.Size.x &&
                   y >= 0 && y < grid.Size.y &&
                   z >= 0 && z < grid.Size.z;
        }

        #endregion
    }
}
