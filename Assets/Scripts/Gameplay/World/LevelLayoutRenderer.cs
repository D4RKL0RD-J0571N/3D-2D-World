namespace Gameplay.World
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Core.Utils;
    using Data.Config;
    using Domain.Grid;
    using Gameplay.Entities;
    using Domain.LevelGeneration;
    using Systems.LevelGeneration;
    using Systems.Meshing;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class LevelLayoutRenderer : MonoBehaviour
    {
        [Header("Generation")]
        [SerializeField] private bool _generateOnStart = true;
        [SerializeField] private LevelGenerationParameters _parameters = new();

        [Header("Configuration")]
        [SerializeField] private LevelRenderConfig _config;

        [Header("Build")]
        [SerializeField] private Transform _generatedRoot;

        [Header("Entities")]
        [SerializeField] private GameObject _slimePrefab;
        [SerializeField] private ActorData _slimeBlueprint;
        [SerializeField] private int _maxEnemiesPerRoom = 3;

        [Header("Harvestable Props")]
        [SerializeField] private ItemData _woodItemData;
        [SerializeField] private ItemData _stoneItemData;
        [SerializeField] private int _maxFoliagePerRoom = 4;
        [SerializeField] private int _maxResourcesPerRoom = 3;

        private LevelGenerationService _levelGenerationService;
        private GreedyMeshingService _meshingService;
        private LevelLayout _currentLayout;
        private BlockDefinition _floorBlock;
        private BlockDefinition _wallBlock;
        private BlockDefinition _ceilingBlock;

        private struct GridPlacement
        {
            public RoomGridData Grid;
            public Vector3 WorldPosition;
        }

        private readonly List<GridPlacement> _generatedGrids = new();

        private void Awake()
        {
            EnsureGeneratedRoot();
            _meshingService = new GreedyMeshingService();
            ServiceLocator.Register(_meshingService);
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<LevelGenerationCompletedEvent>(OnLevelGenerationCompleted);
            GameEventManager.AddListener<LevelGenerationFailedEvent>(OnLevelGenerationFailed);
            GameEventManager.AddListener<Domain.Persistence.GameSaveRequestedEvent>(OnSaveRequested);
            GameEventManager.AddListener<Domain.Persistence.GameDataLoadedEvent>(OnGameLoaded);
        }

        private void Start()
        {
            if (_generateOnStart)
            {
                GameEventManager.Publish(new LevelGenerationRequestedEvent
                {
                    Parameters = _parameters
                });
            }
            else if (TryGetLevelGenerationService(out var service) &&
                     service.CurrentLayout != null)
            {
                BuildLayout(service.CurrentLayout);
            }
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<LevelGenerationCompletedEvent>(OnLevelGenerationCompleted);
            GameEventManager.RemoveListener<LevelGenerationFailedEvent>(OnLevelGenerationFailed);
            GameEventManager.RemoveListener<Domain.Persistence.GameSaveRequestedEvent>(OnSaveRequested);
            GameEventManager.RemoveListener<Domain.Persistence.GameDataLoadedEvent>(OnGameLoaded);
        }

        [ContextMenu("Generate New World")]
        private void GenerateNewWorld()
        {
            Debug.Log(
                "[LevelLayoutRenderer] ContextMenu: Requesting new world generation...");

            ClearExistingLayout();
            _currentLayout = null;

            GameEventManager.Publish(new LevelGenerationRequestedEvent
            {
                Parameters = _parameters
            });
        }

        private void OnSaveRequested(Domain.Persistence.GameSaveRequestedEvent evt)
        {
            if (_currentLayout == null || _generatedGrids.Count == 0)
                return;

            var roomSaves = new List<Domain.Persistence.RoomSaveData>();

            foreach (var placement in _generatedGrids)
            {
                roomSaves.Add(new Domain.Persistence.RoomSaveData
                {
                    WorldPosition = placement.WorldPosition,
                    Size = placement.Grid.Size,
                    VoxelData = placement.Grid.GetRawData()
                });
            }

            var player = GameObject.FindWithTag("Player");

            var playerPos = player != null
                ? player.transform.position
                : _currentLayout.StartRoom.WorldPosition;

            var playerRot = player != null
                ? player.transform.rotation
                : Quaternion.identity;

            var playerSaveData = new Domain.Persistence.EntitySaveData
            {
                EntityId = "Player",
                Position = playerPos,
                Rotation = playerRot
            };

            if (player != null &&
                player.TryGetComponent<Gameplay.Entities.BaseActor>(out var actor))
            {
                if (actor.TryGetComponent<Gameplay.Entities.StatsComponent>(out var stats))
                {
                    playerSaveData.Level = stats.Level;
                    playerSaveData.Experience = stats.Experience;
                }

                if (actor.TryGetComponent<Gameplay.Entities.AttributeComponent>(out var attr))
                {
                    var health = attr.GetAttribute("Health");

                    if (health != null)
                        playerSaveData.CurrentHealth = health.CurrentValue;
                }
            }

            var saveData = new Domain.Persistence.LevelSaveData
            {
                OriginalLayout = _currentLayout,
                Grids = roomSaves.ToArray(),
                Entities = Array.Empty<Domain.Persistence.EntitySaveData>(),
                PlayerState = playerSaveData
            };

            if (ServiceLocator.TryGet<Systems.Persistence.FileSaveService>(out var saveService))
            {
                saveService.WriteData(saveData);
            }
        }

        private void OnGameLoaded(Domain.Persistence.GameDataLoadedEvent evt)
        {
            ClearExistingLayout();
            _currentLayout = evt.SaveData.OriginalLayout;

            _meshingService.Initialize(_config.BlockRegistry);

            foreach (var gridSave in evt.SaveData.Grids)
            {
                var roomData = new RoomGridData(
                    gridSave.Size.x,
                    gridSave.Size.y,
                    gridSave.Size.z);

                Array.Copy(
                    gridSave.VoxelData,
                    roomData.GetRawData(),
                    gridSave.VoxelData.Length);

                _generatedGrids.Add(new GridPlacement
                {
                    Grid = roomData,
                    WorldPosition = gridSave.WorldPosition
                });

                _meshingService.GenerateMeshForRoom(
                    roomData,
                    gridSave.WorldPosition,
                    _generatedRoot);
            }

            // Re-spawn exit trigger for loaded layout so the level can still be completed
            if (_currentLayout?.ExitRoom != null)
            {
                int exitTierHeight = _config != null ? _config.TierHeightInBlocks : 4;
                float exitYOffset = _currentLayout.ExitRoom.FloorLevel * exitTierHeight;
                SpawnExitTrigger(_currentLayout.ExitRoom, exitYOffset, roomHeight: 0);
            }

            var spawnPos = evt.SaveData.PlayerState != null
                ? evt.SaveData.PlayerState.Position
                : evt.SaveData.OriginalLayout.StartRoom.WorldPosition;

            GameEventManager.Publish(new PlayerSpawnRequestedEvent
            {
                SpawnPosition = spawnPos
            });

            if (evt.SaveData.PlayerState != null)
            {
                GameEventManager.Publish(
                    new Domain.Persistence.EntityStateLoadedEvent
                    {
                        State = evt.SaveData.PlayerState
                    });
            }
        }

        private void OnLevelGenerationCompleted(LevelGenerationCompletedEvent gameEvent)
        {
            BuildLayout(gameEvent.Layout);
        }

        private void OnLevelGenerationFailed(LevelGenerationFailedEvent gameEvent)
        {
            Debug.LogError($"Level generation failed: {gameEvent.Reason}");
        }

        private bool TryGetLevelGenerationService(
            out LevelGenerationService service)
        {
            if (_levelGenerationService == null)
            {
                ServiceLocator.TryGet(out _levelGenerationService);
            }

            service = _levelGenerationService;
            return service != null;
        }

        private void BuildLayout(LevelLayout layout)
        {
            Debug.Log(
                $"[LevelLayoutRenderer] Building layout with " +
                $"{layout.Rooms.Count} rooms and " +
                $"{layout.Connections?.Count ?? 0} connections.");

            ClearExistingLayout();
            _currentLayout = layout;

            if (_config == null || _config.BlockRegistry == null)
            {
                Debug.LogError(
                    "[LevelLayoutRenderer] Config or BlockRegistry is missing!");

                return;
            }

            if (!ResolveDefaultBlocks(layout))
                return;

            if (ServiceLocator.TryGet<Systems.Interaction.HotbarService>(out var hotbarService))
            {
                hotbarService.SetRegistry(_config.BlockRegistry);
            }

            int roomIndex = 0;
            foreach (var room in layout.Rooms)
            {
                if (room.Archetype == null && _config.Archetypes != null && _config.Archetypes.Count > 0)
                {
                    // Pick archetype uniquely using room hash and index
                    int archIndex = Math.Abs((layout.Seed ^ room.GridPosition.GetHashCode() ^ (roomIndex * 71)) % _config.Archetypes.Count);
                    room.Archetype = _config.Archetypes[archIndex];
                }

                Debug.Log(
                    $"[LevelLayoutRenderer] Building room: {room.Id} (Archetype: {room.Archetype?.DisplayName ?? "Default"})");

                BuildRoomGrid(room, layout);
                roomIndex++;
            }

            foreach (var connection in layout.Connections)
            {
                Debug.Log(
                    $"[LevelLayoutRenderer] Building connection: " +
                    $"{connection.FromRoomId} -> {connection.ToRoomId}");

                BuildConnectionGrid(layout, connection);
            }

            Debug.Log(
                $"[LevelLayoutRenderer] Final grid count: " +
                $"{_generatedGrids.Count}");

            _meshingService.Initialize(_config.BlockRegistry);

            foreach (var placement in _generatedGrids)
            {
                _meshingService.GenerateMeshForRoom(
                    placement.Grid,
                    placement.WorldPosition,
                    _generatedRoot);
            }

            if (layout.StartRoom != null)
            {
                Vector3 basePos =
                    layout.StartRoom.WorldPosition +
                    _parameters.SpawnOffset;

                Vector3 groundPos =
                    PhysicsUtils.FindGroundPosition(
                        basePos,
                        _parameters.RaycastDistance);

                GameEventManager.Publish(new PlayerSpawnRequestedEvent
                {
                    SpawnPosition = groundPos
                });
            }
        }

        private bool ResolveDefaultBlocks(LevelLayout layout)
        {
            var floorBlocks =
                _config.BlockRegistry.GetBlocksByRole(BlockRole.Floor);

            var wallBlocks =
                _config.BlockRegistry.GetBlocksByRole(BlockRole.Wall);

            var ceilingBlocks =
                _config.BlockRegistry.GetBlocksByRole(BlockRole.Ceiling);

            Debug.Log(
                $"[LevelLayoutRenderer] ResolveDefaultBlocks - " +
                $"Floor: {floorBlocks.Count}, " +
                $"Wall: {wallBlocks.Count}, " +
                $"Ceiling: {ceilingBlocks.Count}");

            if (floorBlocks.Count == 0 ||
                wallBlocks.Count == 0 ||
                ceilingBlocks.Count == 0)
            {
                Debug.LogError(
                    "[LevelLayoutRenderer] Could not find blocks for all " +
                    "required roles (Floor, Wall, Ceiling) in BlockRegistry.");

                return false;
            }

            var rng = new System.Random(layout.Seed);

            _floorBlock = floorBlocks[rng.Next(floorBlocks.Count)];
            _wallBlock = wallBlocks[rng.Next(wallBlocks.Count)];
            _ceilingBlock = ceilingBlocks[rng.Next(ceilingBlocks.Count)];

            return true;
        }

        private void BuildRoomGrid(
            LevelRoomNode room,
            LevelLayout layout)
        {
            var arch = room.Archetype;
            var palette = (arch != null && arch.ThemePalette != null)
                ? arch.ThemePalette
                : ((_config != null && _config.Palettes != null && _config.Palettes.Count > 0) ? _config.Palettes[0] : null);

            var floorBlock = (arch != null && arch.CustomFloorBlock != null)
                ? arch.CustomFloorBlock
                : (palette != null && palette.DefaultFloor != null ? palette.DefaultFloor : _floorBlock);

            var wallBlock = (arch != null && arch.CustomWallBlock != null)
                ? arch.CustomWallBlock
                : (palette != null && palette.DefaultWall != null ? palette.DefaultWall : _wallBlock);

            var ceilingBlock = (arch != null && arch.CustomCeilingBlock != null)
                ? arch.CustomCeilingBlock
                : (palette != null && palette.DefaultCeiling != null ? palette.DefaultCeiling : _ceilingBlock);

            var stepBlock = (arch != null && arch.StepBlock != null)
                ? arch.StepBlock
                : (palette != null && palette.DefaultSteps != null ? palette.DefaultSteps : wallBlock);

            int height = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    _config.RoomCeilingHeight -
                    _config.RoomFloorHeight));

            // Elevate ceiling if room has high features or sunken pit
            if (arch != null && arch.ElevationMode != RoomElevationMode.StandardFlat)
            {
                height += arch.ElevationStepHeight;
            }

            var grid = new RoomGridData(
                room.Size.x,
                height + 1,
                room.Size.y);

            var rng = new System.Random(layout.Seed ^ room.GridPosition.GetHashCode());

            bool placeCeiling = arch == null || (arch.HasCeiling && arch.ElevationMode != RoomElevationMode.OpenSkyGlade);

            // Default base floor with procedural wear variants and ceiling
            for (int x = 0; x < room.Size.x; x++)
            {
                for (int z = 0; z < room.Size.y; z++)
                {
                    // Pick floor block variant if palette supports it
                    var activeFloor = (palette != null) ? palette.PickFloorBlock(rng) : floorBlock;

                    grid.SetBlock(
                        x,
                        0,
                        z,
                        activeFloor.RuntimeId);

                    if (placeCeiling)
                    {
                        grid.SetBlock(
                            x,
                            height,
                            z,
                            ceilingBlock.RuntimeId);
                    }
                }
            }

            // Apply Elevation Architecture
            if (arch != null)
            {
                switch (arch.ElevationMode)
                {
                    case RoomElevationMode.SunkenPit:
                        VoxelStairBuilder.BuildSunkenPit(
                            grid,
                            room.Size.x,
                            room.Size.y,
                            walkwayMargin: 2,
                            pitDepth: arch.ElevationStepHeight,
                            floorBlockId: floorBlock.RuntimeId,
                            stepBlockId: stepBlock.RuntimeId);
                        break;

                    case RoomElevationMode.RaisedDais:
                        VoxelStairBuilder.BuildRaisedDais(
                            grid,
                            room.Size.x,
                            room.Size.y,
                            daisMargin: 2,
                            daisHeight: arch.ElevationStepHeight,
                            daisBlockId: floorBlock.RuntimeId,
                            stepBlockId: stepBlock.RuntimeId);
                        break;
                }

                if (arch.HasPillars)
                {
                    var pillarBlock = (palette != null && palette.DefaultPillar != null)
                        ? palette.DefaultPillar
                        : wallBlock;

                    VoxelStairBuilder.BuildPillars(
                        grid,
                        room.Size.x,
                        room.Size.y,
                        height,
                        inset: arch.PillarInset,
                        thickness: arch.PillarThickness,
                        pillarBlockId: pillarBlock.RuntimeId);
                }
            }

            var openings = GetOpeningsForRoom(room, layout);

            // Carve wall alcoves/niches into solid walls (great for torches/barrels)
            VoxelStairBuilder.CarveWallNiches(grid, room.Size.x, room.Size.y, openings, floorBlock.RuntimeId);

            // In larger rooms without sunken pits, add low cover parapets
            if (arch == null || arch.ElevationMode == RoomElevationMode.StandardFlat)
            {
                VoxelStairBuilder.BuildLowCoverParapets(grid, room.Size.x, room.Size.y, height: 1, wallBlock.RuntimeId);
            }

            BuildGridWallEdge(
                grid,
                room,
                openings,
                Vector2Int.up,
                wallBlock,
                palette,
                rng);

            BuildGridWallEdge(
                grid,
                room,
                openings,
                Vector2Int.down,
                wallBlock,
                palette,
                rng);

            BuildGridWallEdge(
                grid,
                room,
                openings,
                Vector2Int.left,
                wallBlock,
                palette,
                rng);

            BuildGridWallEdge(
                grid,
                room,
                openings,
                Vector2Int.right,
                wallBlock,
                palette,
                rng);

            // Calculate floor tier world offset
            float tierYOffset = room.FloorLevel * (_config != null ? _config.TierHeightInBlocks : 4);

            Vector3 bottomLeft =
                room.WorldPosition +
                new Vector3(0, tierYOffset, 0) -
                new Vector3(
                    room.Size.x * 0.5f,
                    0,
                    room.Size.y * 0.5f);

            // Discover and register sockets (spawn points for entities, breakables, chests, and foliage/resources)
            room.Sockets.Clear();
            bool isOpenSky = arch != null && arch.ElevationMode == RoomElevationMode.OpenSkyGlade;
            VoxelStairBuilder.DiscoverRoomSockets(grid, bottomLeft, room.Sockets, maxSockets: 14, isOpenSky: isOpenSky);

            SpawnRoomEntities(room);

            _generatedGrids.Add(new GridPlacement
            {
                Grid = grid,
                WorldPosition = bottomLeft
            });

            // Spawn RoomTrackerTrigger to notify the AreaSchedulerService
            var trackerObj = new GameObject($"Tracker_{room.Id}");
            trackerObj.transform.SetParent(_generatedRoot, false);
            trackerObj.transform.position = room.WorldPosition + new Vector3(0, tierYOffset, 0);
            var tracker = trackerObj.AddComponent<RoomTrackerTrigger>();
            tracker.Configure(room.Id, room.GridPosition, new Vector3(room.Size.x - 1, height, room.Size.y - 1));

            // Place the level-exit trigger in the exit room
            if (room.IsExit)
            {
                SpawnExitTrigger(room, tierYOffset, height);
            }
        }

        private void SpawnExitTrigger(
            LevelRoomNode room,
            float tierYOffset,
            int roomHeight)
        {
            int triggerHeight = roomHeight > 0 ? roomHeight : 3;
            float triggerWidth = Mathf.Max(2f, Mathf.Min(room.Size.x - 2, 4f));
            float triggerDepth = Mathf.Max(2f, Mathf.Min(room.Size.y - 2, 4f));

            var exitObj = new GameObject($"Exit_{room.Id}");
            exitObj.transform.SetParent(_generatedRoot, false);
            exitObj.transform.position = room.WorldPosition + new Vector3(0, tierYOffset, 0);

            var exitTrigger = exitObj.AddComponent<ExitTrigger>();
            exitTrigger.Configure(new Vector3(triggerWidth, triggerHeight, triggerDepth));
        }

        private void SpawnRoomEntities(LevelRoomNode room)
        {
            int spawnedEnemies = 0;
            int spawnedFoliage = 0;
            int spawnedResources = 0;

            EnsureHarvestDropItems();

            foreach (var socket in room.Sockets)
            {
                switch (socket.Type)
                {
                    case SocketType.EnemySpawn:
                        if (_slimePrefab != null && spawnedEnemies < _maxEnemiesPerRoom)
                        {
                            SpawnEntity<BaseActor>(_slimePrefab, _slimeBlueprint, socket.WorldPosition, room.Id);
                            spawnedEnemies++;
                        }
                        break;

                    case SocketType.PropFoliage:
                        if (spawnedFoliage < _maxFoliagePerRoom)
                        {
                            SpawnFoliageProp(socket.WorldPosition);
                            spawnedFoliage++;
                        }
                        break;

                    case SocketType.PropResource:
                        if (spawnedResources < _maxResourcesPerRoom)
                        {
                            SpawnResourceProp(socket.WorldPosition);
                            spawnedResources++;
                        }
                        break;
                }
            }
        }

        private void SpawnFoliageProp(Vector3 worldPos)
        {
            var treeObj = new GameObject("Room_Harvestable_Tree");
            treeObj.transform.SetParent(_generatedRoot, true);
            treeObj.transform.position = worldPos;

            // Trunk placeholder
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(treeObj.transform, false);
            trunk.transform.localPosition = new Vector3(0, 1.25f, 0);
            trunk.transform.localScale = new Vector3(0.5f, 1.25f, 0.5f);

            // Leaves placeholder
            var leaves = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaves.name = "Leaves";
            leaves.transform.SetParent(treeObj.transform, false);
            leaves.transform.localPosition = new Vector3(0, 2.75f, 0);
            leaves.transform.localScale = new Vector3(1.8f, 1.5f, 1.8f);

            ApplyPlaceholderMaterial(trunk, new Color(0.35f, 0.28f, 0.22f));
            ApplyPlaceholderMaterial(leaves, new Color(0.28f, 0.42f, 0.25f));

            var boxCol = treeObj.AddComponent<BoxCollider>();
            boxCol.center = new Vector3(0, 1.5f, 0);
            boxCol.size = new Vector3(1.2f, 3.0f, 1.2f);

            var trunkCol = trunk.GetComponent<Collider>();
            if (trunkCol != null) DestroyImmediate(trunkCol);
            var leavesCol = leaves.GetComponent<Collider>();
            if (leavesCol != null) DestroyImmediate(leavesCol);

            var harvestNode = treeObj.AddComponent<HarvestableNode>();
            harvestNode.ConfigureNode(
                maxIntegrity: 60,
                dropItem: _woodItemData,
                dropAmount: 3
            );
        }

        private void SpawnResourceProp(Vector3 worldPos)
        {
            var rockObj = new GameObject("Room_Harvestable_Boulder");
            rockObj.transform.SetParent(_generatedRoot, true);
            rockObj.transform.position = worldPos;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "BoulderMesh";
            visual.transform.SetParent(rockObj.transform, false);
            visual.transform.localPosition = new Vector3(0, 0.45f, 0);
            visual.transform.localScale = new Vector3(1.2f, 0.9f, 1.1f);

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

        private void EnsureHarvestDropItems()
        {
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

        private T SpawnEntity<T>(
            GameObject prefab,
            ActorData blueprint,
            Vector3 worldPosition,
            string roomId) where T : BaseActor
        {
            var instance = Instantiate(prefab, _generatedRoot, false);
            var entity = instance.GetComponent<T>();

            if (entity == null)
            {
                Debug.LogError($"[LevelLayoutRenderer] Prefab does not have a {typeof(T).Name} component.");
                Destroy(instance);
                return null;
            }

            instance.transform.position = worldPosition;
            entity.RoomId = roomId;

            if (blueprint != null)
                entity.Initialize(blueprint);

            return entity;
        }

        private void BuildConnectionGrid(
            LevelLayout layout,
            LevelConnection connection)
        {
            var source =
                layout.Rooms.Find(
                    r => r.Id == connection.FromRoomId);

            var target =
                layout.Rooms.Find(
                    r => r.Id == connection.ToRoomId);

            if (source == null || target == null)
                return;

            var delta =
                target.WorldPosition -
                source.WorldPosition;

            var horizontal =
                Mathf.Abs(delta.x) >= Mathf.Abs(delta.z);

            var start =
                source.WorldPosition +
                new Vector3(
                    source.Size.x * 0.5f,
                    0f,
                    0f) *
                Mathf.Sign(delta.x);

            var end =
                target.WorldPosition -
                new Vector3(
                    target.Size.x * 0.5f,
                    0f,
                    0f) *
                Mathf.Sign(delta.x);

            if (!horizontal)
            {
                start =
                    source.WorldPosition +
                    new Vector3(
                        0f,
                        0f,
                        source.Size.y * 0.5f) *
                    Mathf.Sign(delta.z);

                end =
                    target.WorldPosition -
                    new Vector3(
                        0f,
                        0f,
                        target.Size.y * 0.5f) *
                    Mathf.Sign(delta.z);
            }

            int tierHeight = _config != null ? _config.TierHeightInBlocks : 4;
            int sourceYOffset = source.FloorLevel * tierHeight;
            int targetYOffset = target.FloorLevel * tierHeight;
            int heightDelta = targetYOffset - sourceYOffset;

            int length =
                horizontal
                    ? Mathf.RoundToInt(
                        Mathf.Abs(end.x - start.x))
                    : Mathf.RoundToInt(
                        Mathf.Abs(end.z - start.z));

            if (length <= 0)
                return;

            int standardHeight = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    _config.RoomCeilingHeight -
                    _config.RoomFloorHeight));

            int totalGridHeight = standardHeight + Mathf.Abs(heightDelta) + 2;
            int width = 4;

            int sizeX = horizontal ? length : width;
            int sizeZ = horizontal ? width : length;

            var grid = new RoomGridData(
                sizeX,
                totalGridHeight,
                sizeZ);

            int startBaseY = (heightDelta >= 0) ? 0 : Mathf.Abs(heightDelta);

            int totalSteps = Mathf.Abs(heightDelta);
            int stepSign = heightDelta >= 0 ? 1 : -1;
            int treadRun = Mathf.Max(2, length / Mathf.Max(1, totalSteps + 1));

            var corridorPalette = source.Archetype?.ThemePalette
                ?? target.Archetype?.ThemePalette
                ?? ((_config != null && _config.Palettes != null && _config.Palettes.Count > 0) ? _config.Palettes[0] : null);

            var corridorFloor = (corridorPalette != null && corridorPalette.DefaultFloor != null)
                ? corridorPalette.DefaultFloor
                : _floorBlock;

            var corridorWall = (corridorPalette != null && corridorPalette.DefaultWall != null)
                ? corridorPalette.DefaultWall
                : _wallBlock;

            var corridorCeiling = (corridorPalette != null && corridorPalette.DefaultCeiling != null)
                ? corridorPalette.DefaultCeiling
                : _ceilingBlock;

            for (int i = 0; i < length; i++)
            {
                bool isForward = horizontal ? (delta.x > 0) : (delta.z > 0);
                int progressIndex = isForward ? i : (length - 1 - i);

                int stepLevel = Mathf.Min(progressIndex / treadRun, totalSteps);
                int currentFloorY = startBaseY + (stepLevel * stepSign);
                int currentCeilY = currentFloorY + standardHeight;

                int primaryCoord = i;
                for (int w = 0; w < width; w++)
                {
                    int x = horizontal ? primaryCoord : w;
                    int z = horizontal ? w : primaryCoord;

                    for (int y = 0; y <= currentFloorY; y++)
                    {
                        grid.SetBlock(x, y, z, corridorFloor.RuntimeId);
                    }

                    grid.SetBlock(x, currentCeilY, z, corridorCeiling.RuntimeId);

                    bool isWall = (w == 0 || w == width - 1);
                    if (isWall)
                    {
                        for (int y = currentFloorY + 1; y < currentCeilY; y++)
                        {
                            grid.SetBlock(x, y, z, corridorWall.RuntimeId);
                        }
                    }
                }
            }

            int baseWorldY = Mathf.Min(sourceYOffset, targetYOffset);

            Vector3 gridWorldPos =
                horizontal
                    ? new Vector3(
                        Mathf.Min(start.x, end.x),
                        baseWorldY,
                        start.z - width * 0.5f)
                    : new Vector3(
                        start.x - width * 0.5f,
                        baseWorldY,
                        Mathf.Min(start.z, end.z));

            _generatedGrids.Add(new GridPlacement
            {
                Grid = grid,
                WorldPosition = gridWorldPos
            });
        }

        private void BuildGridWallEdge(
            RoomGridData grid,
            LevelRoomNode room,
            HashSet<Vector2Int> openings,
            Vector2Int direction,
            BlockDefinition wallBlock = null,
            BlockPalette palette = null,
            System.Random rng = null)
        {
            var fallbackWall = wallBlock ?? _wallBlock;
            var width =
                direction.x != 0
                    ? room.Size.y
                    : room.Size.x;

            for (var i = 0; i < width; i++)
            {
                if ((direction == Vector2Int.left ||
                     direction == Vector2Int.right) &&
                    (i == 0 || i == width - 1))
                {
                    continue;
                }

                if (HasOpeningForSide(openings, direction) &&
                    IsDoorwayIndex(i, width))
                {
                    continue;
                }

                int localX =
                    direction.x != 0
                        ? (direction.x > 0
                            ? room.Size.x - 1
                            : 0)
                        : i;

                int localZ =
                    direction.x != 0
                        ? i
                        : (direction.y > 0
                            ? room.Size.y - 1
                            : 0);

                int height = grid.Size.y - 1;

                for (int y = 1; y < height; y++)
                {
                    var activeWallBlock = (palette != null && rng != null)
                        ? palette.PickWallBlock(rng)
                        : fallbackWall;

                    grid.SetBlock(
                        localX,
                        y,
                        localZ,
                        activeWallBlock.RuntimeId);
                }
            }
        }

        private static bool HasOpeningForSide(
            HashSet<Vector2Int> openings,
            Vector2Int direction)
        {
            return openings != null &&
                   openings.Contains(direction);
        }

        private static bool IsDoorwayIndex(
            int index,
            int width)
        {
            if (width <= 2)
                return false;

            return index >= width / 2 - 1 &&
                   index <= width / 2;
        }

        private static HashSet<Vector2Int> GetOpeningsForRoom(
            LevelRoomNode room,
            LevelLayout layout)
        {
            var openings = new HashSet<Vector2Int>();

            foreach (var connection in layout.Connections)
            {
                if (connection.FromRoomId == room.Id)
                    openings.Add(connection.Direction);
                else if (connection.ToRoomId == room.Id)
                    openings.Add(-connection.Direction);
            }

            return openings;
        }

        private void ClearExistingLayout()
        {
            _generatedGrids.Clear();

            foreach (Transform child in _generatedRoot)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private void EnsureGeneratedRoot()
        {
            if (_generatedRoot != null)
                return;

            var rootObj =
                new GameObject("GeneratedLevelLayout");

            _generatedRoot = rootObj.transform;
            _generatedRoot.SetParent(transform, false);
        }

        private void OnDrawGizmos()
        {
            if (_currentLayout == null)
                return;

            if (_currentLayout.StartRoom != null)
            {
                Gizmos.color = Color.green;

                Gizmos.DrawWireSphere(
                    _currentLayout.StartRoom.WorldPosition,
                    2f);
            }

            if (_currentLayout.ExitRoom != null)
            {
                Gizmos.color = Color.red;

                Gizmos.DrawWireSphere(
                    _currentLayout.ExitRoom.WorldPosition,
                    2f);
            }

            if (_currentLayout.Connections != null &&
                _currentLayout.Rooms != null)
            {
                Gizmos.color = Color.cyan;

                foreach (var connection in _currentLayout.Connections)
                {
                    var source =
                        _currentLayout.Rooms.Find(
                            r => r.Id == connection.FromRoomId);

                    var target =
                        _currentLayout.Rooms.Find(
                            r => r.Id == connection.ToRoomId);

                    if (source != null && target != null)
                    {
                        Gizmos.DrawLine(
                            source.WorldPosition + Vector3.up * 2f,
                            target.WorldPosition + Vector3.up * 2f);
                    }
                }
            }
        }
    }
}