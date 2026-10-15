namespace Systems.Meshing
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Domain.Meshing;

    [BurstCompile]
    public struct GreedyMeshingJob : IJob
    {
        [ReadOnly] public NativeArray<ushort> GridData;
        [ReadOnly] public int3 GridSize;

        public NativeList<NativeFaceDescriptor> OutputFaces;

        public void Execute()
        {
            // Sweep all 6 directions
            for (int d = 0; d < 6; d++)
            {
                ExtractDirectionalFaces((FaceDirection)d);
            }
        }

        private void ExtractDirectionalFaces(FaceDirection dir)
        {
            int i, j, k;
            int sizeI, sizeJ, sizeK;

            switch (dir)
            {
                case FaceDirection.Top:
                case FaceDirection.Bottom:
                    i = 1; j = 0; k = 2;
                    sizeI = GridSize.y; sizeJ = GridSize.x; sizeK = GridSize.z;
                    break;
                case FaceDirection.North:
                case FaceDirection.South:
                    i = 2; j = 0; k = 1;
                    sizeI = GridSize.z; sizeJ = GridSize.x; sizeK = GridSize.y;
                    break;
                case FaceDirection.East:
                case FaceDirection.West:
                    i = 0; j = 2; k = 1;
                    sizeI = GridSize.x; sizeJ = GridSize.z; sizeK = GridSize.y;
                    break;
                default: return;
            }

            int3 offset = GetNeighborOffset(dir);

            for (int valI = 0; valI < sizeI; valI++)
            {
                var mask = new NativeArray<bool>(sizeJ * sizeK, Allocator.Temp);
                var types = new NativeArray<ushort>(sizeJ * sizeK, Allocator.Temp);

                for (int valK = 0; valK < sizeK; valK++)
                {
                    for (int valJ = 0; valJ < sizeJ; valJ++)
                    {
                        int3 localPos = GetPos(i, j, k, valI, valJ, valK);
                        ushort blockId = GetBlockId(localPos);
                        
                        if (blockId == 0) continue; // 0 is Air

                        int3 neighborPos = localPos + offset;
                        ushort neighborId = GetBlockId(neighborPos);

                        if (ShouldEmitFace(blockId, neighborId))
                        {
                            int id = valJ + sizeJ * valK;
                            mask[id] = true;
                            types[id] = blockId;
                        }
                    }
                }

                // Greedy merge
                for (int valK = 0; valK < sizeK; valK++)
                {
                    for (int valJ = 0; valJ < sizeJ; valJ++)
                    {
                        int id = valJ + sizeJ * valK;
                        if (!mask[id]) continue;

                        ushort type = types[id];
                        int width = 1;
                        int height = 1;

                        while (valJ + width < sizeJ && mask[id + width] && types[id + width] == type)
                        {
                            width++;
                        }

                        while (valK + height < sizeK)
                        {
                            bool rowMatch = true;
                            for (int w = 0; w < width; w++)
                            {
                                int nextId = valJ + w + sizeJ * (valK + height);
                                if (!mask[nextId] || types[nextId] != type)
                                {
                                    rowMatch = false;
                                    break;
                                }
                            }
                            if (!rowMatch) break;
                            height++;
                        }

                        OutputFaces.Add(new NativeFaceDescriptor
                        {
                            VoxelCoord = GetPos(i, j, k, valI, valJ, valK),
                            Size = new int2(width, height),
                            Direction = dir,
                            BlockId = type,
                            AO = new int4(3, 3, 3, 3) // Skipping AO computation for now to speed up porting
                        });

                        for (int h = 0; h < height; h++)
                        {
                            for (int w = 0; w < width; w++)
                            {
                                mask[valJ + w + sizeJ * (valK + h)] = false;
                            }
                        }
                    }
                }
                mask.Dispose();
                types.Dispose();
            }
        }

        private ushort GetBlockId(int3 pos)
        {
            if (pos.x >= 0 && pos.x < GridSize.x &&
                pos.y >= 0 && pos.y < GridSize.y &&
                pos.z >= 0 && pos.z < GridSize.z)
            {
                return GridData[pos.x + GridSize.x * (pos.y + GridSize.y * pos.z)];
            }
            return 0; // Return Air if out of bounds
        }

        private bool ShouldEmitFace(ushort blockId, ushort neighborId)
        {
            // Simple logic: emit face if neighbor is Air (0).
            // In a full implementation, we'd check transparency flags from a NativeArray of block properties.
            if (neighborId == 0) return true;
            return false;
        }

        private int3 GetPos(int i, int j, int k, int valI, int valJ, int valK)
        {
            int3 pos = new int3();
            if (i == 0) pos.x = valI; else if (j == 0) pos.x = valJ; else pos.x = valK;
            if (i == 1) pos.y = valI; else if (j == 1) pos.y = valJ; else pos.y = valK;
            if (i == 2) pos.z = valI; else if (j == 2) pos.z = valJ; else pos.z = valK;
            return pos;
        }

        private int3 GetNeighborOffset(FaceDirection dir)
        {
            switch (dir)
            {
                case FaceDirection.Top:    return new int3(0, 1, 0);
                case FaceDirection.Bottom: return new int3(0, -1, 0);
                case FaceDirection.North:  return new int3(0, 0, 1);
                case FaceDirection.South:  return new int3(0, 0, -1);
                case FaceDirection.East:   return new int3(1, 0, 0);
                case FaceDirection.West:   return new int3(-1, 0, 0);
                default: return int3.zero;
            }
        }
    }
}
