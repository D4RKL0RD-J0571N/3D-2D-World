namespace Systems.Meshing
{
    using System.Collections.Generic;
    using UnityEngine;
    using Unity.Mathematics;
    using Domain.Grid;
    using Domain.Meshing;

    public class GridMeshBuilder
    {
        private static readonly Vector3[] TopCorners = { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) };
        private static readonly Vector3[] BottomCorners = { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1) };
        private static readonly Vector3[] NorthCorners = { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1) };
        private static readonly Vector3[] SouthCorners = { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0) };
        private static readonly Vector3[] EastCorners = { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1) };
        private static readonly Vector3[] WestCorners = { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0) };

        private static readonly Vector3[][] FaceCorners = new Vector3[][] { TopCorners, BottomCorners, NorthCorners, SouthCorners, EastCorners, WestCorners };

        public void BuildMesh(Unity.Collections.NativeArray<NativeFaceDescriptor> faces, BlockRegistry registry, out Mesh mesh, out Material[] materials)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();

            // Map each BlockID to its list of triangles to create SubMeshes
            var submeshTriangles = new Dictionary<ushort, List<int>>();
            var materialList = new List<Material>();
            var blockIdToSubmeshIndex = new Dictionary<ushort, int>();

            int vertexIndex = 0;

            foreach (var face in faces)
            {
                var blockDef = registry.GetBlock(face.BlockId);
                if (blockDef == null || blockDef.RenderType != BlockRenderType.SolidMesh)
                    continue;

                // Ensure submesh list exists for this block ID
                if (!submeshTriangles.TryGetValue(face.BlockId, out var triangles))
                {
                    triangles = new List<int>();
                    submeshTriangles[face.BlockId] = triangles;
                    
                    blockIdToSubmeshIndex[face.BlockId] = materialList.Count;
                    materialList.Add(blockDef.BlockMaterial);
                }

                var corners = FaceCorners[(int)face.Direction];

                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = corners[i];
                    float sx = corner.x;
                    float sy = corner.y;
                    float sz = corner.z;

                    Vector2 faceUV = Vector2.zero;

                    switch (face.Direction)
                    {
                        case FaceDirection.Top:
                        case FaceDirection.Bottom:
                            if (sx > 0.5f) sx = face.Size.x;
                            if (sz > 0.5f) sz = face.Size.y;
                            faceUV = new Vector2(sx, sz);
                            break;
                        case FaceDirection.North:
                        case FaceDirection.South:
                            if (sx > 0.5f) sx = face.Size.x;
                            if (sy > 0.5f) sy = face.Size.y;
                            faceUV = new Vector2(sx, sy);
                            break;
                        case FaceDirection.East:
                        case FaceDirection.West:
                            if (sz > 0.5f) sz = face.Size.x;
                            if (sy > 0.5f) sy = face.Size.y;
                            faceUV = new Vector2(sz, sy);
                            break;
                    }

                    vertices.Add(new Vector3(face.VoxelCoord.x, face.VoxelCoord.y, face.VoxelCoord.z) + new Vector3(sx, sy, sz));
                    uv.Add(faceUV);
                }

                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 3);

                vertexIndex += 4;
            }

            mesh = new Mesh(); // Should use MeshPool later
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);

            mesh.subMeshCount = materialList.Count;
            int submeshIdx = 0;
            foreach (var kvp in submeshTriangles)
            {
                mesh.SetTriangles(kvp.Value, submeshIdx);
                submeshIdx++;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            materials = materialList.ToArray();
        }
    }
}
