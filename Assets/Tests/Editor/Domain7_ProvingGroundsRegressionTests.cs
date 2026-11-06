namespace Tests.Editor
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Core.Assets;
    using Core.Content;
    using Core.Diagnostics;
    using Core.Modding;
    using Core.Modules;
    using Core.Persistence;
    using Core.Scheduling;
    using Data.Config;
    using Domain.Crafting;
    using Domain.Grid;
    using Domain.LevelGeneration;
    using Gameplay.Entities;
    using NUnit.Framework;
    using Systems.Crafting;
    using Systems.Creator;
    using Systems.Interaction;
    using Systems.LevelGeneration;
    using Systems.Meshing;
    using Systems.Museum;
    using Systems.Persistence;
    using Systems.Pooling;
    using Systems.Scheduler;
    using Systems.Skyrim;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Milestone 6 &amp; Architectural Final Regression Suite (<c>Domain 7: Proving Grounds Regression Tests</c>).
    /// Formally implements test scenarios specified in <c>Assets/Docs/Architecture/ARCHITECTURE_TEST_STRATEGY.md</c>:
    /// - <c>REG-01</c>: Existing <see cref="CraftingService"/> recipes and inventory state pass unmodified.
    /// - <c>REG-02</c>: Proving Grounds full-stack boot sequence registers all 17 services and boots cleanly.
    /// - <c>REG-03</c>: Voxel room graph generation and Burst-compiled <see cref="GreedyMeshingService"/> produce valid 3D meshes.
    /// </summary>
    [TestFixture]
    public sealed class Domain7_ProvingGroundsRegressionTests
    {
        #region Fields

        private List<GameObject> _spawnedGameObjects;

        #endregion

        #region Setup & Teardown

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            GameEventManager.Clear();
            _spawnedGameObjects = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawnedGameObjects != null)
            {
                for (int i = 0; i < _spawnedGameObjects.Count; i++)
                {
                    if (_spawnedGameObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_spawnedGameObjects[i]);
                    }
                }
                _spawnedGameObjects.Clear();
            }

            ServiceLocator.Clear();
            GameEventManager.Clear();
        }

        #endregion

        #region REG-01: Crafting Service Regression

        [Test]
        public void REG01_ExistingCraftingServiceTests_PassUnmodified()
        {
            var crafter = new GameObject("REG01_Crafter");
            _spawnedGameObjects.Add(crafter);

            var inventory = crafter.AddComponent<InventoryComponent>();
            var craftingService = new CraftingService();
            craftingService.Initialize();

            var woodLog = ScriptableObject.CreateInstance<ItemData>();
            woodLog.DisplayName = "Iron Ingot";
            woodLog.IsStackable = true;
            woodLog.MaxStackSize = 64;

            var sword = ScriptableObject.CreateInstance<ItemData>();
            sword.DisplayName = "Iron Sword";
            sword.IsStackable = false;

            var recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            var ingredientsField = typeof(CraftingRecipe).GetField("_ingredients", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var resultItemField = typeof(CraftingRecipe).GetField("_resultItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var resultCountField = typeof(CraftingRecipe).GetField("_resultCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            ingredientsField?.SetValue(recipe, new List<CraftingIngredient>
            {
                new CraftingIngredient(woodLog, 2)
            });
            resultItemField?.SetValue(recipe, sword);
            resultCountField?.SetValue(recipe, 1);

            try
            {
                // Attempt craft with insufficient ingredients
                inventory.AddItem(woodLog, 1);
                bool failResult = craftingService.TryCraft(crafter, recipe);
                Assert.IsFalse(failResult, "Crafting must fail when player possesses insufficient ingredients.");

                // Attempt craft with sufficient ingredients
                inventory.AddItem(woodLog, 3); // Total 4 ingots
                bool successResult = craftingService.TryCraft(crafter, recipe);
                Assert.IsTrue(successResult, "Crafting must succeed when player possesses required ingredients.");
                Assert.AreEqual(2, inventory.GetItemCount(woodLog), "Exactly 2 ingots should have been consumed.");
                Assert.AreEqual(1, inventory.GetItemCount(sword), "Exactly 1 sword should have been produced.");
            }
            finally
            {
                craftingService.Shutdown();
                UnityEngine.Object.DestroyImmediate(woodLog);
                UnityEngine.Object.DestroyImmediate(sword);
                UnityEngine.Object.DestroyImmediate(recipe);
            }
        }

        #endregion

        #region REG-02: Proving Grounds Full-Stack Boot Verification

        [Test]
        public void REG02_RPGProvingGrounds_BootsAndEntersGameplayStateWithoutConsoleErrors()
        {
            var host = new ModuleHost();
            host.DiscoverAndAddModules();
            host.Boot(hydrateProjectAssets: true);

            try
            {
                Assert.AreEqual(ModuleHostState.Active, host.State, "ModuleHost must be Active after full boot.");

                // Core Infrastructure Services
                Assert.IsTrue(ServiceLocator.TryGet<IDiagnosticService>(out _), "IDiagnosticService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<IContentService>(out var content), "IContentService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<IAssetProvider>(out _), "IAssetProvider must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<FileSaveService>(out _), "FileSaveService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<IModManagerService>(out _), "IModManagerService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<TickScheduler>(out _), "TickScheduler must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<PoolingManager>(out _), "PoolingManager must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<AreaSchedulerService>(out _), "AreaSchedulerService must be registered.");

                // Fantasy Framework Services
                Assert.IsTrue(ServiceLocator.TryGet<SkillProgressionService>(out var skills), "SkillProgressionService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<FactionService>(out var factions), "FactionService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<RadiantQuestService>(out var quests), "RadiantQuestService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<StealthDetectionService>(out _), "StealthDetectionService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<CraftingService>(out _), "CraftingService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<HotbarService>(out _), "HotbarService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<CompassService>(out _), "CompassService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<LevelGenerationService>(out _), "LevelGenerationService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<GreedyMeshingService>(out _), "GreedyMeshingService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<BlockPlacementService>(out _), "BlockPlacementService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<CreatorStudioService>(out _), "CreatorStudioService must be registered.");
                Assert.IsTrue(ServiceLocator.TryGet<CuratorService>(out _), "CuratorService must be registered.");

                // Verify Hydrated Content Definitions
                Assert.GreaterOrEqual(content.GetRegistry<Domain.Skyrim.SkillDefinition>().Count, 18, "At least 18 skills must be registered.");
                Assert.GreaterOrEqual(content.GetRegistry<Domain.Skyrim.FactionDefinition>().Count, 6, "At least 6 factions must be registered.");
                Assert.GreaterOrEqual(content.GetRegistry<Domain.Skyrim.QuestTemplateDefinition>().Count, 4, "At least 4 quest templates must be registered.");

                // Simulate engine frame tick
                host.Tick(0.016f);
            }
            finally
            {
                host.Shutdown();
            }
        }

        #endregion

        #region REG-03: Voxel Dungeon Generation & Burst Greedy Meshing

        [Test]
        public void REG03_VoxelDungeonGeneration_AndBurstGreedyMeshing_ProducesValidMesh()
        {
            var rootGo = new GameObject("REG03_VoxelRoot");
            _spawnedGameObjects.Add(rootGo);

            var meshingService = new GreedyMeshingService();
            meshingService.Initialize();

            try
            {
                Assert.IsNotNull(meshingService.Registry, "BlockRegistry must be loaded and non-null.");
                Assert.Greater(meshingService.Registry.GetAllBlocks().Count, 0, "BlockRegistry must contain registered block definitions.");

                // Construct a 16x8x16 structured room grid
                var roomGrid = new RoomGridData(16, 8, 16);
                ushort solidBlockId = 1; // Default Stone / Solid block

                // Create a 16x16 floor and four perimeter walls
                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        roomGrid.SetBlock(x, 0, z, solidBlockId); // Floor
                        if (x == 0 || x == 15 || z == 0 || z == 15)
                        {
                            for (int y = 1; y < 4; y++)
                            {
                                roomGrid.SetBlock(x, y, z, solidBlockId); // Walls
                            }
                        }
                    }
                }

                // Execute Burst-compiled Greedy Meshing on room grid
                meshingService.GenerateMeshForRoom(roomGrid, Vector3.zero, rootGo.transform);

                // Assert generated GameObject and Mesh
                MeshFilter meshFilter = rootGo.GetComponentInChildren<MeshFilter>();
                Assert.IsNotNull(meshFilter, "Greedy meshing must instantiate a child GameObject with a MeshFilter.");
                Assert.IsNotNull(meshFilter.sharedMesh, "MeshFilter must contain a generated Unity Mesh.");

                Mesh generatedMesh = meshFilter.sharedMesh;
                Assert.Greater(generatedMesh.vertexCount, 0, "Generated mesh must have vertices.");
                Assert.Greater(generatedMesh.triangles.Length, 0, "Generated mesh must have triangles.");

                MeshRenderer renderer = rootGo.GetComponentInChildren<MeshRenderer>();
                Assert.IsNotNull(renderer, "Generated room must have a MeshRenderer attached.");
                Assert.IsNotNull(renderer.sharedMaterials, "MeshRenderer must have assigned materials.");
            }
            finally
            {
                meshingService.Shutdown();
            }
        }

        #endregion
    }
}
