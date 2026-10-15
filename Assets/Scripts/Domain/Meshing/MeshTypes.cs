namespace Domain.Meshing
{
    using Unity.Mathematics;

    public enum FaceDirection : byte
    {
        Top = 0,
        Bottom = 1,
        North = 2, // +Z
        South = 3, // -Z
        East = 4,  // +X
        West = 5   // -X
    }

    public struct NativeFaceDescriptor
    {
        public int3 VoxelCoord;
        public int2 Size;
        public FaceDirection Direction;
        public ushort BlockId;
        public int4 AO; // Optional: Ambient Occlusion values for the 4 corners
    }
}
