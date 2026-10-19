namespace Gameplay.World
{
    using UnityEngine;
    using Core;
    using Domain.Grid;
    using Domain.LevelGeneration;
    using Systems.Meshing;
    using Gameplay.Entities;
    using Data.Config;

    /// <summary>
    /// Generates a finite continuous outdoor voxel terrain (e.g. 64x32x64) using existing
    /// <see cref="RoomGridData"/> and <see cref="GreedyMeshingService"/>.
    /// Demonstrates rolling hills, pond depressions with water, and harvestable tree/rock props.
    /// </summary>
    [AddComponentMenu("3D2DWorld/World/Outdoor Voxel Terrain Generator")]
    [DisallowMultipleComponent]
    public class OutdoorVoxelTerrainGenerator : MonoBehaviour
    {
        #region Fields

        [Header("Dimensions")]
        [SerializeField] private Vector3Int _terrainSize = new Vector3Int(64, 32, 64);
        [SerializeField] private int _baseHeight = 8;
        [SerializeField] private float _noiseScale = 0.05f;
        [SerializeField] private int _heightVariation = 5;

        [Header("Blocks & Registry")]
        [SerializeField] private BlockRegistry _registry;
        [SerializeField] private ushort _grassBlockId = 14;  // BLCK_0014 Overgrown Earth
        [SerializeField] private ushort _dirtBlockId = 14;   // Overgrown Earth / Dirt
        [SerializeField] private ushort _stoneBlockId = 10;  // BLCK_0010 Dungeon Stone
        [SerializeField] private ushort _waterBlockId = 30;  // BLCK_0030 Dungeon Water
        [SerializeField] private int _waterLevel = 6;

        [Header("Foliage & Props")]
        [SerializeField] private int _treeCount = 12;
        [SerializeField] private int _rockCount = 8;
        [SerializeField] private ItemData _woodItemData;
        [SerializeField] private ItemData _stoneItemData;

        [Header("Generation Controls")]
        [SerializeField] private bool _generateOnStart = false;
        [SerializeField] private int _seed = 42;

        private RoomGridData _gridData;
        private GreedyMeshingService _meshingService;
        private GameObject _terrainObject;

        #endregion

        #region Properties

        public RoomGridData GridData => _gridData;
        public Vector3Int TerrainSize => _terrainSize;
        public GameObject TerrainObject => _terrainObject;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (_generateOnStart)
            {
                GenerateOutdoorWorld();
            }
        }

        #endregion

        #region World Generation

        /// <summary>
        /// Generates the complete continuous outdoor terrain mesh, water body, and harvestable nodes.
        /// </summary>
        [ContextMenu("Generate Outdoor World")]
        public void GenerateOutdoorWorld()
        {
            ClearExistingTerrain();
            EnsureRegistry();
            EnsureMeshingService();

            _gridData = new RoomGridData(_terrainSize.x, _terrainSize.y, _terrainSize.z);

            PopulateVoxelHeightmap(_gridData, _seed);

            MeshTerrain();

            SpawnHarvestableProps();

            Debug.Log($"[OutdoorVoxelTerrainGenerator] Successfully generated finite outdoor terrain ({_terrainSize.x}x{_terrainSize.y}x{_terrainSize.z})");
        }

        /// <summary>
        /// Clears previously generated terrain gameobject and spawned child objects.
        /// </summary>
        [ContextMenu("Clear Outdoor World")]
        public void ClearExistingTerrain()
        {
            if (_terrainObject != null)
            {
                DestroyImmediate(_terrainObject);
                _terrainObject = null;
            }

            // Remove any leftover generated props under this transform
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        #endregion

        #region Heightmap Algorithm

        private void PopulateVoxelHeightmap(RoomGridData grid, int seed)
        {
            float seedOffsetX = (seed * 17.13f) % 1000f;
            float seedOffsetZ = (seed * 23.47f) % 1000f;

            for (int x = 0; x < _terrainSize.x; x++)
            {
                for (int z = 0; z < _terrainSize.z; z++)
                {
                    // Primary rolling hills noise
                    float nx = (x + seedOffsetX) * _noiseScale;
                    float nz = (z + seedOffsetZ) * _noiseScale;
                    float perlin = Mathf.PerlinNoise(nx, nz);

                    // Secondary detail octave
                    float detail = Mathf.PerlinNoise(nx * 2.5f, nz * 2.5f) * 0.5f;
                    float combined = (perlin + detail) / 1.5f;

                    int height = _baseHeight + Mathf.RoundToInt(combined * _heightVariation);
                    height = Mathf.Clamp(height, 1, _terrainSize.y - 2);

                    // Fill column
                    for (int y = 0; y < _terrainSize.y; y++)
                    {
                        if (y <= height)
                        {
                            if (y == height)
                            {
                                // Surface layer
                                grid.SetBlock(x, y, z, _grassBlockId);
                            }
                            else if (y >= height - 2)
                            {
                                // Sub-surface dirt
                                grid.SetBlock(x, y, z, _dirtBlockId);
                            }
                            else
                            {
                                // Deep foundation stone
                                grid.SetBlock(x, y, z, _stoneBlockId);
                            }
                        }
                        else if (y <= _waterLevel)
                        {
                            // Water in depressions below water level
                            grid.SetBlock(x, y, z, _waterBlockId);
                        }
                        else
                        {
                            // Air above
                            grid.SetBlock(x, y, z, 0);
                        }
                    }
                }
            }
        }

        #endregion

        #region Meshing

        private void MeshTerrain()
        {
            if (_meshingService == null || _registry == null) return;

            _terrainObject = new GameObject("Outdoor_Continuous_Terrain");
            _terrainObject.transform.SetParent(transform, false);
            _terrainObject.transform.position = transform.position;

            _meshingService.GenerateMeshForRoom(_gridData, transform.position, _terrainObject.transform);
        }

        #endregion

        #region Prop Placement

        private void SpawnHarvestableProps()
        {
            System.Random rng = new System.Random(_seed);

            // 1. Spawn Trees
            int treesSpawned = 0;
            int treeAttempts = 0;
            while (treesSpawned < _treeCount && treeAttempts < _treeCount * 5)
            {
                treeAttempts++;
                int rx = rng.Next(4, _terrainSize.x - 4);
                int rz = rng.Next(4, _terrainSize.z - 4);
                int surfaceY = GetSurfaceHeight(rx, rz);

                // Trees must grow above water
                if (surfaceY > _waterLevel)
                {
                    SpawnTreeProp(new Vector3(transform.position.x + rx + 0.5f, transform.position.y + surfaceY + 1f, transform.position.z + rz + 0.5f));
                    treesSpawned++;
                }
            }

            // 2. Spawn Rock Boulders
            int rocksSpawned = 0;
            int rockAttempts = 0;
            while (rocksSpawned < _rockCount && rockAttempts < _rockCount * 5)
            {
                rockAttempts++;
                int rx = rng.Next(4, _terrainSize.x - 4);
                int rz = rng.Next(4, _terrainSize.z - 4);
                int surfaceY = GetSurfaceHeight(rx, rz);

                if (surfaceY > _waterLevel)
                {
                    SpawnRockProp(new Vector3(transform.position.x + rx + 0.5f, transform.position.y + surfaceY + 1f, transform.position.z + rz + 0.5f));
                    rocksSpawned++;
                }
            }
        }

        private int GetSurfaceHeight(int x, int z)
        {
            for (int y = _terrainSize.y - 1; y >= 0; y--)
            {
                ushort id = _gridData.GetBlock(x, y, z);
                if (id != 0 && id != _waterBlockId)
                {
                    return y;
                }
            }
            return 0;
        }

        private void SpawnTreeProp(Vector3 worldPos)
        {
            var treeObj = new GameObject("Harvestable_Tree");
            treeObj.transform.SetParent(transform, true);
            treeObj.transform.position = worldPos;

            // Trunk visual (Cylinder or Cube placeholder)
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(treeObj.transform, false);
            trunk.transform.localPosition = new Vector3(0, 1.25f, 0);
            trunk.transform.localScale = new Vector3(0.5f, 1.25f, 0.5f);

            // Foliage crown visual (placeholder geometry)
            var leaves = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaves.name = "Leaves";
            leaves.transform.SetParent(treeObj.transform, false);
            leaves.transform.localPosition = new Vector3(0, 2.75f, 0);
            leaves.transform.localScale = new Vector3(1.8f, 1.5f, 1.8f);

            // Apply placeholder gray / muted tones to primitive renderers
            ApplyPlaceholderMaterial(trunk, new Color(0.35f, 0.28f, 0.22f));
            ApplyPlaceholderMaterial(leaves, new Color(0.28f, 0.42f, 0.25f));

            // Box Collider around trunk for interaction & weapon spherecasts
            var boxCol = treeObj.AddComponent<BoxCollider>();
            boxCol.center = new Vector3(0, 1.5f, 0);
            boxCol.size = new Vector3(1.2f, 3.0f, 1.2f);

            // Destroy primitive colliders so the root collider governs
            var trunkCol = trunk.GetComponent<Collider>();
            if (trunkCol != null) DestroyImmediate(trunkCol);
            var leavesCol = leaves.GetComponent<Collider>();
            if (leavesCol != null) DestroyImmediate(leavesCol);

            // Harvestable component with configured drops
            var harvestNode = treeObj.AddComponent<HarvestableNode>();
            harvestNode.ConfigureNode(
                maxIntegrity: 60,
                dropItem: _woodItemData,
                dropAmount: 3
            );
        }

        private void SpawnRockProp(Vector3 worldPos)
        {
            var rockObj = new GameObject("Harvestable_Boulder");
            rockObj.transform.SetParent(transform, true);
            rockObj.transform.position = worldPos;

            // Placeholder rock mesh (gray boulder)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "BoulderMesh";
            visual.transform.SetParent(rockObj.transform, false);
            visual.transform.localPosition = new Vector3(0, 0.45f, 0);
            visual.transform.localScale = new Vector3(1.2f, 0.9f, 1.1f);

            // Neutral stone gray placeholder
            ApplyPlaceholderMaterial(visual, new Color(0.48f, 0.48f, 0.50f));

            var sphereCol = rockObj.AddComponent<SphereCollider>();
            sphereCol.center = new Vector3(0, 0.45f, 0);
            sphereCol.radius = 0.65f;

            var visCol = visual.GetComponent<Collider>();
            if (visCol != null) DestroyImmediate(visCol);

            var harvestNode = rockObj.AddComponent<HarvestableNode>();
            harvestNode.ConfigureNode(
                maxIntegrity: 80,
                dropItem: _stoneItemData,
                dropAmount: 4
            );
        }

        private static void ApplyPlaceholderMaterial(GameObject target, Color color)
        {
            var rend = target.GetComponent<Renderer>();
            if (rend != null)
            {
                // Create clean runtime instance of standard / URP Lit material
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    var mat = new Material(shader) { color = color };
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", color);
                    }
                    rend.material = mat;
                }
            }
        }

        #endregion

        #region Setup Helpers

        private void EnsureRegistry()
        {
            if (_registry == null)
            {
                _registry = Resources.Load<BlockRegistry>("Config/BlockRegistry");
#if UNITY_EDITOR
                if (_registry == null)
                {
                    _registry = UnityEditor.AssetDatabase.LoadAssetAtPath<BlockRegistry>(
                        "Assets/Data/Config/WorldGeneration/BlockRegistry.asset");
                }
#endif
            }

#if UNITY_EDITOR
            if (_woodItemData == null)
            {
                _woodItemData = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(
                    "Assets/Data/Definitions/ItemData/Item_WoodLog.asset");
            }

            if (_stoneItemData == null)
            {
                _stoneItemData = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(
                    "Assets/Data/Definitions/ItemData/Item_StoneChunk.asset");
            }
#endif
        }

        private void EnsureMeshingService()
        {
            if (_meshingService != null) return;

            if (!ServiceLocator.TryGet(out _meshingService))
            {
                _meshingService = new GreedyMeshingService();
                if (_registry != null)
                {
                    _meshingService.Initialize(_registry);
                }
            }
        }

        #endregion
    }
}
