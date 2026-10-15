namespace Systems.Meshing
{
    using Core;
    using Domain.Grid;
    using Domain.Meshing;
    using UnityEngine;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using System.Collections.Generic;
    using System.Linq;


    public sealed class GreedyMeshingService : IGameService
    {
        private GridMeshBuilder _meshBuilder;
        private BlockRegistry _registry;

        public void Initialize(BlockRegistry registry)
        {
            _registry = registry;
            _meshBuilder = new GridMeshBuilder();
        }

        public void Initialize()
        {
            // Usually configuration is injected, but if not we leave it null until provided
            _meshBuilder = new GridMeshBuilder();
        }

        public void Shutdown()
        {
        }

        public void GenerateMeshForRoom(RoomGridData roomData, Vector3 worldPosition, Transform parent)
        {
            if (_registry == null)
            {
                Debug.LogError("[GreedyMeshingService] BlockRegistry is not initialized!");
                return;
            }

            // 1. Prepare data for Burst Job
            var rawData = roomData.GetRawData();
            var nativeGridData = new NativeArray<ushort>(rawData.Length, Allocator.TempJob);
            nativeGridData.CopyFrom(rawData);

            var outputFaces = new NativeList<NativeFaceDescriptor>(Allocator.TempJob);

            var job = new GreedyMeshingJob
            {
                GridData = nativeGridData,
                GridSize = new int3(roomData.Size.x, roomData.Size.y, roomData.Size.z),
                OutputFaces = outputFaces
            };

            try
            {
                // 2. Schedule and Wait
                var handle = job.Schedule();
                handle.Complete();

                // 3. Build Unity Mesh and SubMeshes
                if (outputFaces.Length > 0)
                {
                    _meshBuilder.BuildMesh(outputFaces.AsArray(), _registry, out Mesh mesh, out Material[] materials);

                    // 4. Instantiate Visuals
                    var go = new GameObject($"RoomMesh_{worldPosition}");
                    go.transform.SetParent(parent, false);
                    go.transform.position = worldPosition;

                    var filter = go.AddComponent<MeshFilter>();
                    filter.mesh = mesh;

                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.materials = materials;

                    var collider = go.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;

                    var link = go.AddComponent<GridRoomLink>();
                    link.GridData = roomData;
                    link.WorldPosition = worldPosition;
                }
            }
            finally
            {
                nativeGridData.Dispose();
                outputFaces.Dispose();
            }
        }

        public void UpdateMeshForRoom(RoomGridData roomData, GameObject targetGO)
        {
            if (_registry == null) return;

            var rawData = roomData.GetRawData();
            var nativeGridData = new NativeArray<ushort>(rawData.Length, Allocator.TempJob);
            nativeGridData.CopyFrom(rawData);

            var outputFaces = new NativeList<NativeFaceDescriptor>(Allocator.TempJob);

            var job = new GreedyMeshingJob
            {
                GridData = nativeGridData,
                GridSize = new int3(roomData.Size.x, roomData.Size.y, roomData.Size.z),
                OutputFaces = outputFaces
            };

            try
            {
                var handle = job.Schedule();
                handle.Complete();

                _meshBuilder.BuildMesh(outputFaces.AsArray(), _registry, out Mesh mesh, out Material[] materials);

                var filter = targetGO.GetComponent<MeshFilter>();
                if (filter != null) 
                {
                    if (filter.mesh != null) Object.Destroy(filter.mesh);
                    filter.mesh = mesh;
                }

                var renderer = targetGO.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.materials = materials;

                var collider = targetGO.GetComponent<MeshCollider>();
                if (collider != null) collider.sharedMesh = mesh;
            }
            finally
            {
                nativeGridData.Dispose();
                outputFaces.Dispose();
            }
        }
    }
}
