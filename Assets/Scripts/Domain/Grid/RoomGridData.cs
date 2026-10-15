namespace Domain.Grid
{
    using UnityEngine;

    public class RoomGridData
    {
        public Vector3Int Size { get; private set; }
        private readonly ushort[] _voxels;

        public RoomGridData(int sizeX, int sizeY, int sizeZ)
        {
            Size = new Vector3Int(sizeX, sizeY, sizeZ);
            _voxels = new ushort[sizeX * sizeY * sizeZ];
        }

        public void SetBlock(int x, int y, int z, ushort blockId)
        {
            if (IsOutOfBounds(x, y, z)) return;
            _voxels[GetIndex(x, y, z)] = blockId;
        }

        public ushort GetBlock(int x, int y, int z)
        {
            if (IsOutOfBounds(x, y, z)) return 0; // 0 is Air
            return _voxels[GetIndex(x, y, z)];
        }

        private int GetIndex(int x, int y, int z)
        {
            return x + Size.x * (y + Size.y * z);
        }

        private bool IsOutOfBounds(int x, int y, int z)
        {
            return x < 0 || x >= Size.x || y < 0 || y >= Size.y || z < 0 || z >= Size.z;
        }

        public ushort[] GetRawData() => _voxels;
    }
}
