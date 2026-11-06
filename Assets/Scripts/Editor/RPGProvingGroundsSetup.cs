#if UNITY_EDITOR
namespace Studio.Editor
{
    using Domain.Skyrim;
    using Gameplay.Combat;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using Gameplay.Museum;
    using Gameplay.Skyrim;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    /// <summary>
    /// Automated setup tool that populates the RPGProvingGrounds scene with
    /// a continuous Nordic environment, cohesive landscape terrain, stone roads,
    /// natural pine forests, mountain cliffs, medieval village buildings, and gameplay interactables.
    /// </summary>
    public static class RPGProvingGroundsSetup
    {
        private const string ScenePath = "Assets/Scenes/RPGProvingGrounds.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Entities/Player.prefab";

        #region Asset Paths
        private const string GrassMaterialPath = "Assets/Synty/PolygonFantasyKingdom/Materials/Ground/Grass_01.mat";
        private const string Path01Prefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_Path_01.prefab";
        private const string Path02Prefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_Path_02.prefab";
        private const string FountainPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_Fountain_01.prefab";
        private const string PineCluster01Prefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_Tree_Pine_Cluster_01.prefab";
        private const string PineCluster02Prefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_Tree_Pine_Cluster_02.prefab";
        private const string LargeTreePrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_Tree_Large_01.prefab";
        private const string RockCliff01Prefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_Rock_Cliff_01.prefab";
        private const string RockCliff02Prefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_Rock_Cliff_02.prefab";
        private const string StoneWallPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_StoneWall_01.prefab";
        private const string StoneWallPillarPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Environments/SM_Env_StoneWall_Pillar_01.prefab";
        private const string TorchPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Torch_01.prefab";
        private const string BarrelStackPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Barrel_Stack_01.prefab";
        private const string CrateStackPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Crate_Stack_01.prefab";
        private const string QuestBoardPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Quest_Board_01_Posters_01.prefab";
        private const string Chest01Prefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/Furniture/SM_Prop_Chest_01.prefab";
        private const string Chest02Prefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/Furniture/SM_Prop_Chest_02.prefab";
        private const string Chest03Prefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/Furniture/SM_Prop_Chest_03.prefab";
        private const string BlacksmithBuildingPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Buildings/Presets/SM_Bld_Preset_Blacksmith_01_Optimized.prefab";
        private const string VillageHousePrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Buildings/Presets/SM_Bld_Preset_House_01_A_Optimized.prefab";
        private const string CastleWallPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Castle/SM_Bld_Castle_Wall_01.prefab";
        private const string CastleWallGatePrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Castle/SM_Bld_Castle_Wall_Gate_01.prefab";
        private const string IronBarGatePrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Castle/SM_Prop_Bar_Gate_01.prefab";
        private const string SoldierPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Characters/SM_Chr_Soldier_Male_01.prefab";
        private const string BlacksmithNpcPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Characters/SM_Chr_Blacksmith_Male_01.prefab";
        private const string BanditNpcPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Characters/SM_Chr_Headsman_01.prefab";
        private const string AnvilPrefab = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Anvil_01.prefab";
        private const string FireFXPrefab = "Assets/Synty/PolygonGeneric/Prefabs/FX/FX_Fire_01.prefab";
        private const string LeverPrefab = "Assets/Prefabs/Interactables/DungeonLever.prefab";
        private const string TrapPlatePrefab = "Assets/Prefabs/Interactables/PressurePlate.prefab";
        private const string TargetDummyPrefab = "Assets/Prefabs/Museum/TargetDummy.prefab";
        #endregion

        [MenuItem("3D2DWorld/Setup RPG Proving Grounds World", false, 1)]
        public static void SetupScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[RPGProvingGroundsSetup] Could not open scene at {ScenePath}");
                return;
            }

            Debug.Log("[RPGProvingGroundsSetup] Overhauling Skyrim Proving Grounds with continuous landscape...");

            CleanLegacyObjects();
            EnsureLightingAndSky();
            EnsureBootstrapper();
            EnsurePlayer();
            DisableStandaloneCameras();

            SetupSolidGroundAndLandscape();
            SetupConnectedPathways();
            SetupTownSquare();
            SetupMedievalBuildings();
            SetupNaturalPerimeterForestAndCliffs();
            SetupBountyNoticeBoard();
            SetupDungeonChests();
            SetupCastleGateAndLever();
            SetupDungeonTraps();
            SetupCombatTrainingYard();
            SetupFactionNPCs();
            SetupCompassPOIMarkers();
            WorldBeautificationSetup.Apply();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=green>✦ Skyrim Fantasy Proving Grounds successfully rebuilt with continuous ground!</color>");
        }

        #region Setup Implementation

        private static void CleanLegacyObjects()
        {
            string[] legacyNames = {
                "Environment_Root", "Interactables_Root", "NPCs_Root",
                "Demo", "SA_Building", "Zombie_Hostile_Alley", "Survivor_Sheriff_Guard",
                "BountyBoard_Hub", "Chest_Novice_Courtyard", "Chest_Adept_Bank",
                "SecurityGate_Barricade", "Lever_SecurityGate", "Trap_PressurePlate_Alley",
                "TrainingDummy_Yard", "Ground_Cobblestone", "Courtyard_Plane",
                "Terrain_Base_Grass_Plane", "Ground_Landscape_Terrain",
                "Beautification_Root", "PostProcess_Volume", "Main Camera", "Hero_Vista_Camera"
            };

            foreach (var name in legacyNames)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            foreach (var rootGo in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootGo.name == "Sphere" || rootGo.GetComponent<Gameplay.Magic.SpellProjectile>() != null)
                {
                    Object.DestroyImmediate(rootGo);
                }
            }
        }

        private static void EnsureLightingAndSky()
        {
            var sunGo = GameObject.Find("Directional Light");
            if (sunGo == null)
            {
                sunGo = new GameObject("Directional Light");
                var light = sunGo.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            var dirLight = sunGo.GetComponent<Light>();
            dirLight.intensity = 1.25f;
            dirLight.color = new Color(1f, 0.95f, 0.88f);
            sunGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            // Nordic Mountain Atmospheric Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.70f, 0.77f, 0.83f);

            // Ambient Trilight for Skyrim sky/ground contrast
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.42f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.28f, 0.22f);
        }

        private static void EnsureBootstrapper()
        {
            if (Object.FindFirstObjectByType<Core.GameBootstrapper>() == null)
            {
                var bootGo = new GameObject("GameBootstrapper");
                bootGo.AddComponent<Core.GameBootstrapper>();
                Debug.Log("[RPGProvingGroundsSetup] Added GameBootstrapper.");
            }
        }

        private static void EnsurePlayer()
        {
            var existingPlayer = GameObject.FindWithTag("Player");
            if (existingPlayer != null)
            {
                existingPlayer.transform.position = new Vector3(0f, 0.1f, 0f);
                return;
            }

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null)
            {
                var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerInstance.name = "Player";
                playerInstance.transform.position = new Vector3(0f, 0.1f, 0f);
                Debug.Log("[RPGProvingGroundsSetup] Spawned Player at courtyard center.");
            }
        }

        private static void DisableStandaloneCameras()
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.gameObject.CompareTag("Player") || cam.transform.root.CompareTag("Player"))
                    continue;

                if (cam.name.Contains("Main Camera") || cam.name.Contains("Camera"))
                {
                    cam.gameObject.SetActive(false);
                }
            }
        }

        private static void SetupSolidGroundAndLandscape()
        {
            var envRoot = GetOrCreateRoot("Environment_Root");

            // 120m x 120m continuous terrain plane with Grass_01
            var terrainObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            terrainObj.name = "Ground_Landscape_Terrain";
            terrainObj.transform.parent = envRoot.transform;
            terrainObj.transform.position = new Vector3(0f, -0.02f, 5f);
            terrainObj.transform.localScale = new Vector3(12f, 1f, 12f);

            var grassMat = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            if (grassMat != null)
            {
                var rend = terrainObj.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = grassMat;
            }

            // Ensure solid ground collision
            var meshCol = terrainObj.GetComponent<MeshCollider>();
            if (meshCol != null) Object.DestroyImmediate(meshCol);

            var col = terrainObj.AddComponent<BoxCollider>();
            col.size = new Vector3(10f, 0.5f, 10f);
            col.center = new Vector3(0f, -0.25f, 0f);
        }

        private static void SetupConnectedPathways()
        {
            var envRoot = GetOrCreateRoot("Environment_Root");
            var roadRoot = new GameObject("RoadNetwork_Root");
            roadRoot.transform.parent = envRoot.transform;

            var path01 = AssetDatabase.LoadAssetAtPath<GameObject>(Path01Prefab);
            var path02 = AssetDatabase.LoadAssetAtPath<GameObject>(Path02Prefab) ?? path01;
            if (path01 == null) return;

            // Main North-South Street from South entrance to Castle Gate
            for (float z = -20f; z <= 24f; z += 3.5f)
            {
                var prefab = ((int)z % 2 == 0) ? path01 : path02;
                var segment = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                segment.name = $"Path_NS_{z:0}";
                segment.transform.parent = roadRoot.transform;
                segment.transform.position = new Vector3(0f, 0.01f, z);
                segment.transform.rotation = Quaternion.identity;
            }

            // East-West Thoroughfare connecting Sleeping Giant Inn to Blacksmith's Forge
            for (float x = -8.5f; x <= 8.5f; x += 3.5f)
            {
                var segment = (GameObject)PrefabUtility.InstantiatePrefab(path01);
                segment.name = $"Path_EW_{x:0}";
                segment.transform.parent = roadRoot.transform;
                segment.transform.position = new Vector3(x, 0.01f, 2f);
                segment.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
        }

        private static void SetupTownSquare()
        {
            var envRoot = GetOrCreateRoot("Environment_Root");
            var squareRoot = new GameObject("TownSquare_Root");
            squareRoot.transform.parent = envRoot.transform;

            // Centerpiece Stone Fountain
            var fountainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FountainPrefab);
            if (fountainPrefab != null)
            {
                var fountain = (GameObject)PrefabUtility.InstantiatePrefab(fountainPrefab);
                fountain.name = "Fountain_Town_Center";
                fountain.transform.parent = squareRoot.transform;
                fountain.transform.position = new Vector3(0f, 0f, -3f);
            }

            // Warm Village Torches
            SpawnTorch("Torch_Inn_Entrance", new Vector3(-7.5f, 0f, 2f), squareRoot.transform);
            SpawnTorch("Torch_Forge_Entrance", new Vector3(7.5f, 0f, 2f), squareRoot.transform);
            SpawnTorch("Torch_Fountain_West", new Vector3(-2.8f, 0f, -3f), squareRoot.transform);
            SpawnTorch("Torch_Fountain_East", new Vector3(2.8f, 0f, -3f), squareRoot.transform);
            SpawnTorch("Torch_Gate_West", new Vector3(-3f, 0f, 14.5f), squareRoot.transform);
            SpawnTorch("Torch_Gate_East", new Vector3(3f, 0f, 14.5f), squareRoot.transform);

            // Village Crate & Barrel clutter
            SpawnPrefab(BarrelStackPrefab, "BarrelStack_Village_Inn", new Vector3(-6.5f, 0f, 4.5f), Quaternion.identity, squareRoot.transform);
            SpawnPrefab(CrateStackPrefab, "CrateStack_Blacksmith", new Vector3(7f, 0f, -1.5f), Quaternion.Euler(0f, 30f, 0f), squareRoot.transform);
        }

        private static void SetupMedievalBuildings()
        {
            var envRoot = GetOrCreateRoot("Environment_Root");

            // Blacksmith Forge on East - seated flush with ground
            var blacksmithPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlacksmithBuildingPrefab);
            if (blacksmithPrefab != null)
            {
                var blacksmith = (GameObject)PrefabUtility.InstantiatePrefab(blacksmithPrefab);
                blacksmith.name = "Building_Blacksmith_Forge";
                blacksmith.transform.parent = envRoot.transform;
                blacksmith.transform.position = new Vector3(9.5f, 0f, 2f);
                blacksmith.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            }

            // Village Inn / Sleeping Giant on West - seated flush with ground
            var housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageHousePrefab);
            if (housePrefab != null)
            {
                var house = (GameObject)PrefabUtility.InstantiatePrefab(housePrefab);
                house.name = "Building_Village_Inn";
                house.transform.parent = envRoot.transform;
                house.transform.position = new Vector3(-9.5f, 0f, 2f);
                house.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }

            // Anvil outside blacksmith
            var anvilPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AnvilPrefab);
            if (anvilPrefab != null)
            {
                var anvil = (GameObject)PrefabUtility.InstantiatePrefab(anvilPrefab);
                PrefabUtility.UnpackPrefabInstance(anvil, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                anvil.name = "Prop_Blacksmith_Anvil";
                anvil.transform.parent = envRoot.transform;
                anvil.transform.position = new Vector3(6.5f, 0f, 1f);

                if (anvil.GetComponent<CraftingStationInteractable>() == null)
                {
                    anvil.AddComponent<CraftingStationInteractable>();
                }
            }

            // Forge Fire FX
            var firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FireFXPrefab);
            if (firePrefab != null)
            {
                var fire = (GameObject)PrefabUtility.InstantiatePrefab(firePrefab);
                fire.name = "FX_Forge_Fire";
                fire.transform.parent = envRoot.transform;
                fire.transform.position = new Vector3(8.5f, 0.5f, 1.5f);
            }
        }

        private static void SetupNaturalPerimeterForestAndCliffs()
        {
            var envRoot = GetOrCreateRoot("Environment_Root");
            var perimeterRoot = new GameObject("Perimeter_Root");
            perimeterRoot.transform.parent = envRoot.transform;

            // North Mountain Cliffs framing Castle Wall & Crypt
            SpawnScaledPrefab(RockCliff01Prefab, "Cliff_North_West", new Vector3(-15f, 0f, 18f), Quaternion.Euler(0f, 25f, 0f), 1.3f, perimeterRoot.transform);
            SpawnScaledPrefab(RockCliff02Prefab, "Cliff_North_East", new Vector3(15f, 0f, 18f), Quaternion.Euler(0f, -25f, 0f), 1.3f, perimeterRoot.transform);
            SpawnScaledPrefab(RockCliff01Prefab, "Cliff_Crypt_Backdrop", new Vector3(0f, 0f, 32f), Quaternion.Euler(0f, 180f, 0f), 1.6f, perimeterRoot.transform);

            // West Mountain Ridge & Dense Pine Forest
            SpawnScaledPrefab(RockCliff02Prefab, "Cliff_West_Ridge", new Vector3(-22f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f), 1.4f, perimeterRoot.transform);
            SpawnScaledPrefab(PineCluster01Prefab, "Pine_Forest_West_01", new Vector3(-18f, 0f, 8f), Quaternion.identity, 1.2f, perimeterRoot.transform);
            SpawnScaledPrefab(PineCluster02Prefab, "Pine_Forest_West_02", new Vector3(-17f, 0f, -6f), Quaternion.identity, 1.1f, perimeterRoot.transform);
            SpawnScaledPrefab(LargeTreePrefab, "Tree_Large_West", new Vector3(-14f, 0f, 14f), Quaternion.identity, 1.0f, perimeterRoot.transform);

            // East Mountain Ridge & Dense Pine Forest
            SpawnScaledPrefab(RockCliff01Prefab, "Cliff_East_Ridge", new Vector3(22f, 0f, 2f), Quaternion.Euler(0f, -90f, 0f), 1.4f, perimeterRoot.transform);
            SpawnScaledPrefab(PineCluster02Prefab, "Pine_Forest_East_01", new Vector3(18f, 0f, 8f), Quaternion.identity, 1.2f, perimeterRoot.transform);
            SpawnScaledPrefab(PineCluster01Prefab, "Pine_Forest_East_02", new Vector3(17f, 0f, -6f), Quaternion.identity, 1.1f, perimeterRoot.transform);
            SpawnScaledPrefab(LargeTreePrefab, "Tree_Large_East", new Vector3(14f, 0f, 14f), Quaternion.identity, 1.0f, perimeterRoot.transform);

            // South Valley Boundary Stone Walls & Pine Gates
            SpawnPrefab(StoneWallPrefab, "StoneWall_South_West", new Vector3(-6f, 0f, -22f), Quaternion.identity, perimeterRoot.transform);
            SpawnPrefab(StoneWallPrefab, "StoneWall_South_East", new Vector3(6f, 0f, -22f), Quaternion.identity, perimeterRoot.transform);
            SpawnPrefab(StoneWallPillarPrefab, "Pillar_South_West", new Vector3(-3f, 0f, -22f), Quaternion.identity, perimeterRoot.transform);
            SpawnPrefab(StoneWallPillarPrefab, "Pillar_South_East", new Vector3(3f, 0f, -22f), Quaternion.identity, perimeterRoot.transform);
            SpawnScaledPrefab(PineCluster01Prefab, "Pine_Forest_South_West", new Vector3(-14f, 0f, -20f), Quaternion.identity, 1.1f, perimeterRoot.transform);
            SpawnScaledPrefab(PineCluster02Prefab, "Pine_Forest_South_East", new Vector3(14f, 0f, -20f), Quaternion.identity, 1.1f, perimeterRoot.transform);
        }

        private static void SetupBountyNoticeBoard()
        {
            var interactablesRoot = GetOrCreateRoot("Interactables_Root");

            var boardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuestBoardPrefab);
            GameObject boardInstance;

            if (boardPrefab != null)
            {
                boardInstance = (GameObject)PrefabUtility.InstantiatePrefab(boardPrefab);
            }
            else
            {
                boardInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardInstance.transform.localScale = new Vector3(1.2f, 1.8f, 0.2f);
            }

            boardInstance.name = "BountyBoard_Hub";
            boardInstance.transform.parent = interactablesRoot.transform;
            boardInstance.transform.position = new Vector3(2.5f, 0f, 2.5f);
            boardInstance.transform.rotation = Quaternion.Euler(0f, -40f, 0f);

            if (boardInstance.GetComponent<Collider>() == null)
            {
                var col = boardInstance.AddComponent<BoxCollider>();
                col.size = new Vector3(1.5f, 2f, 0.6f);
                col.center = new Vector3(0f, 1f, 0f);
            }

            if (boardInstance.GetComponent<BountyBoardInteractable>() == null)
            {
                boardInstance.AddComponent<BountyBoardInteractable>();
            }
        }

        private static void SetupDungeonChests()
        {
            var interactablesRoot = GetOrCreateRoot("Interactables_Root");

            SpawnChest(Chest01Prefab, "Chest_Novice_Courtyard", new Vector3(-5f, 0f, 3.5f), 45f, LockDifficulty.Novice, interactablesRoot.transform);
            SpawnChest(Chest02Prefab, "Chest_Apprentice_Forge", new Vector3(6.5f, 0f, -2.5f), -30f, LockDifficulty.Apprentice, interactablesRoot.transform);
            SpawnChest(Chest02Prefab, "Chest_Adept_Crypt", new Vector3(-6f, 0f, 16f), 10f, LockDifficulty.Adept, interactablesRoot.transform);
            SpawnChest(Chest03Prefab, "Chest_Master_Vault", new Vector3(0f, 0f, 22f), 180f, LockDifficulty.Master, interactablesRoot.transform);
        }

        private static void SpawnChest(string prefabPath, string name, Vector3 pos, float rotY, LockDifficulty diff, Transform parent)
        {
            var chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) 
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(Chest01Prefab);

            GameObject chest;
            if (chestPrefab != null)
            {
                chest = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab);
            }
            else
            {
                chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chest.transform.localScale = new Vector3(0.8f, 0.5f, 0.5f);
            }

            chest.name = name;
            chest.transform.parent = parent;
            chest.transform.position = pos;
            chest.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            if (chest.GetComponent<Collider>() == null)
            {
                var col = chest.AddComponent<BoxCollider>();
                col.size = new Vector3(1f, 0.8f, 0.8f);
                col.center = new Vector3(0f, 0.4f, 0f);
            }

            var chestComp = GetOrAddComponent<ChestInteractable>(chest);
            var field = typeof(ChestInteractable).GetField("_lockDifficulty",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(chestComp, diff);
        }

        private static void SetupCastleGateAndLever()
        {
            var envRoot = GetOrCreateRoot("Environment_Root");
            var interactablesRoot = GetOrCreateRoot("Interactables_Root");

            var wallGatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CastleWallGatePrefab)
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(CastleWallPrefab);

            if (wallGatePrefab != null)
            {
                var wallGate = (GameObject)PrefabUtility.InstantiatePrefab(wallGatePrefab);
                wallGate.name = "Castle_Wall_North";
                wallGate.transform.parent = envRoot.transform;
                wallGate.transform.position = new Vector3(0f, 0f, 15f);
                wallGate.transform.rotation = Quaternion.identity;
            }

            var barGatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IronBarGatePrefab);
            GameObject gateGo;

            if (barGatePrefab != null)
            {
                gateGo = (GameObject)PrefabUtility.InstantiatePrefab(barGatePrefab);
                gateGo.transform.position = new Vector3(0f, 0f, 15.2f);
            }
            else
            {
                gateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gateGo.transform.position = new Vector3(0f, 1.5f, 15.2f);
                gateGo.transform.localScale = new Vector3(3f, 3f, 0.3f);
            }
            gateGo.name = "Portcullis_IronGate";
            gateGo.transform.parent = interactablesRoot.transform;

            var leverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeverPrefab);
            if (leverPrefab != null)
            {
                var lever = (GameObject)PrefabUtility.InstantiatePrefab(leverPrefab);
                lever.name = "Lever_Portcullis";
                lever.transform.parent = interactablesRoot.transform;
                lever.transform.position = new Vector3(2.5f, 0f, 14.5f);

                var leverComp = lever.GetComponent<LeverInteractable>();
                if (leverComp != null)
                {
                    var field = typeof(LeverInteractable).GetField("_linkedObjects",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(leverComp, new GameObject[] { gateGo });
                }
            }
        }

        private static void SetupDungeonTraps()
        {
            var interactablesRoot = GetOrCreateRoot("Interactables_Root");

            var platePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrapPlatePrefab);
            if (platePrefab != null)
            {
                var plate = (GameObject)PrefabUtility.InstantiatePrefab(platePrefab);
                plate.name = "Trap_PressurePlate_Vault";
                plate.transform.parent = interactablesRoot.transform;
                plate.transform.position = new Vector3(0f, 0.05f, 18f);
            }
        }

        private static void SetupCombatTrainingYard()
        {
            var interactablesRoot = GetOrCreateRoot("Interactables_Root");

            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetDummyPrefab);
            if (dummyPrefab != null)
            {
                var dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab);
                dummy.name = "TrainingDummy_Courtyard";
                dummy.transform.parent = interactablesRoot.transform;
                dummy.transform.position = new Vector3(7f, 0f, 8f);
            }
        }

        private static void SetupFactionNPCs()
        {
            var npcsRoot = GetOrCreateRoot("NPCs_Root");

            SpawnCharacter(SoldierPrefab, "Guard_Whiterun_Soldier", new Vector3(-2f, 0f, 13f), 180f, FactionType.Guards, false, npcsRoot.transform);
            SpawnCharacter(BlacksmithNpcPrefab, "Artisan_Blacksmith", new Vector3(8.5f, 0f, 0.5f), -45f, FactionType.Neutral, false, npcsRoot.transform);
            SpawnCharacter(BanditNpcPrefab, "Draugr_Wight_Boss", new Vector3(0f, 0f, 25f), 180f, FactionType.Draugr, true, npcsRoot.transform);
        }

        private static void SpawnCharacter(string prefabPath, string name, Vector3 pos, float rotY, FactionType factionType, bool isEnemy, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject character;

            if (prefab != null)
            {
                character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                PrefabUtility.UnpackPrefabInstance(character, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            else
            {
                character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                character.transform.localScale = new Vector3(1f, 1.8f, 1f);
            }

            character.name = name;
            character.transform.parent = parent;
            character.transform.position = pos;
            character.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            if (isEnemy) character.tag = "Enemy";

            if (character.GetComponent<Collider>() == null)
            {
                var col = character.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.center = new Vector3(0f, 0.9f, 0f);
            }

            var faction = GetOrAddComponent<FactionComponent>(character);
            faction.SetFaction(factionType);

            if (isEnemy)
            {
                if (character.GetComponent<BaseActor>() == null) character.AddComponent<BaseActor>();
                if (character.GetComponent<AttributeComponent>() == null) character.AddComponent<AttributeComponent>();
                if (character.GetComponent<EnemyCombatAI>() == null) character.AddComponent<EnemyCombatAI>();
            }
        }

        private static void SetupCompassPOIMarkers()
        {
            AddMarker("BountyBoard_Hub", "Bounty Notice Board", CompassPOIType.QuestMarker, new Vector3(2.5f, 1f, 2.5f));
            AddMarker("Building_Blacksmith_Forge", "Blacksmith's Forge", CompassPOIType.Town, new Vector3(9.5f, 1f, 2f));
            AddMarker("Building_Village_Inn", "Sleeping Giant Inn", CompassPOIType.Town, new Vector3(-9.5f, 1f, 2f));
            AddMarker("Castle_Wall_North", "Castle Gate", CompassPOIType.Town, new Vector3(0f, 1f, 15f));
            AddMarker("Draugr_Wight_Boss", "Draugr Crypt Sanctum", CompassPOIType.Dungeon, new Vector3(0f, 1f, 25f));
            AddMarker("TrainingDummy_Courtyard", "Training Yard", CompassPOIType.Enemy, new Vector3(7f, 1f, 8f));
        }

        private static void AddMarker(string goName, string title, CompassPOIType type, Vector3 fallbackPos)
        {
            var go = GameObject.Find(goName);
            if (go == null)
            {
                go = new GameObject(goName);
                go.transform.position = fallbackPos;
            }

            var marker = GetOrAddComponent<CompassMarkerComponent>(go);

            var idField = typeof(CompassMarkerComponent).GetField("_markerId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var titleField = typeof(CompassMarkerComponent).GetField("_markerTitle",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var typeField = typeof(CompassMarkerComponent).GetField("_markerType",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            idField?.SetValue(marker, goName);
            titleField?.SetValue(marker, title);
            typeField?.SetValue(marker, type);
        }

        #endregion

        #region Helpers

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            return comp != null ? comp : go.AddComponent<T>();
        }

        private static GameObject GetOrCreateRoot(string rootName)
        {
            var root = GameObject.Find(rootName);
            if (root == null)
            {
                root = new GameObject(rootName);
            }
            return root;
        }

        private static GameObject SpawnPrefab(string path, string name, Vector3 pos, Quaternion rot, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = name;
            inst.transform.parent = parent;
            inst.transform.position = pos;
            inst.transform.rotation = rot;
            return inst;
        }

        private static GameObject SpawnScaledPrefab(string path, string name, Vector3 pos, Quaternion rot, float scale, Transform parent)
        {
            var inst = SpawnPrefab(path, name, pos, rot, parent);
            if (inst != null) inst.transform.localScale = Vector3.one * scale;
            return inst;
        }

        private static void SpawnTorch(string name, Vector3 pos, Transform parent)
        {
            var torch = SpawnPrefab(TorchPrefab, name, pos, Quaternion.identity, parent);
            if (torch != null && torch.GetComponentInChildren<Light>() == null)
            {
                var lightGo = new GameObject("Light_Torch_Glow");
                lightGo.transform.parent = torch.transform;
                lightGo.transform.localPosition = new Vector3(0f, 1.8f, 0f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 7f;
                l.intensity = 1.6f;
                l.color = new Color(1f, 0.75f, 0.45f);
            }
        }

        #endregion
    }
}
#endif
