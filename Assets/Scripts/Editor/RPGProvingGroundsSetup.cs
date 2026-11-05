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
    /// POLYGON Fantasy Kingdom medieval assets, GameBootstrapper, Player rig,
    /// interactive chests, levers, bounty boards, faction NPCs, and 360 compass POI markers.
    /// </summary>
    public static class RPGProvingGroundsSetup
    {
        private const string ScenePath = "Assets/Scenes/RPGProvingGrounds.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Entities/Player.prefab";

        #region Asset Paths
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
        private const string CobblestoneFloorPrefab = "Assets/Synty/PolygonGeneric/Prefabs/Base/SM_Bld_Base_Floor_Combined_01.prefab";
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

            Debug.Log("[RPGProvingGroundsSetup] Building Skyrim Proving Grounds with POLYGON Fantasy Kingdom assets...");

            CleanLegacyObjects();
            EnsureLightingAndSky();
            EnsureBootstrapper();
            EnsurePlayer();
            DisableStandaloneCameras();

            SetupCobblestoneCourtyard();
            SetupMedievalBuildings();
            SetupBountyNoticeBoard();
            SetupDungeonChests();
            SetupCastleGateAndLever();
            SetupDungeonTraps();
            SetupCombatTrainingYard();
            SetupFactionNPCs();
            SetupCompassPOIMarkers();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=green>✦ Skyrim Fantasy Proving Grounds successfully built and saved!</color>");
        }

        #region Setup Implementation

        private static void CleanLegacyObjects()
        {
            string[] legacyNames = {
                "Environment_Root", "Interactables_Root", "NPCs_Root",
                "Demo", "SA_Building", "Zombie_Hostile_Alley", "Survivor_Sheriff_Guard",
                "BountyBoard_Hub", "Chest_Novice_Courtyard", "Chest_Adept_Bank",
                "SecurityGate_Barricade", "Lever_SecurityGate", "Trap_PressurePlate_Alley",
                "TrainingDummy_Yard", "Ground_Cobblestone"
            };

            foreach (var name in legacyNames)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private static void EnsureLightingAndSky()
        {
            var sun = GameObject.Find("Directional Light");
            if (sun == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.color = new Color(1f, 0.95f, 0.88f);
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            RenderSettings.ambientIntensity = 1f;
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

        private static void SetupCobblestoneCourtyard()
        {
            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CobblestoneFloorPrefab);
            var envRoot = new GameObject("Environment_Root");

            if (floorPrefab != null)
            {
                // Create 6x6 floor grid (each tile is ~4x4m)
                for (int x = -3; x <= 3; x++)
                {
                    for (int z = -3; z <= 3; z++)
                    {
                        var tile = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                        tile.name = $"Floor_{x}_{z}";
                        tile.transform.parent = envRoot.transform;
                        tile.transform.position = new Vector3(x * 4f, 0f, z * 4f);
                    }
                }
            }
            else
            {
                // Fallback plane
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = "Courtyard_Plane";
                plane.transform.parent = envRoot.transform;
                plane.transform.localScale = new Vector3(4f, 1f, 4f);
            }
        }

        private static void SetupMedievalBuildings()
        {
            var envRoot = GameObject.Find("Environment_Root") ?? new GameObject("Environment_Root");

            // Blacksmith Forge on East
            var blacksmithPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlacksmithBuildingPrefab);
            if (blacksmithPrefab != null)
            {
                var blacksmith = (GameObject)PrefabUtility.InstantiatePrefab(blacksmithPrefab);
                blacksmith.name = "Building_Blacksmith_Forge";
                blacksmith.transform.parent = envRoot.transform;
                blacksmith.transform.position = new Vector3(10f, 0f, 2f);
                blacksmith.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            }

            // Village Inn / House on West
            var housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageHousePrefab);
            if (housePrefab != null)
            {
                var house = (GameObject)PrefabUtility.InstantiatePrefab(housePrefab);
                house.name = "Building_Village_Inn";
                house.transform.parent = envRoot.transform;
                house.transform.position = new Vector3(-10f, 0f, 2f);
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

            // Novice Chest outside Inn
            SpawnChest(Chest01Prefab, "Chest_Novice_Courtyard", new Vector3(-5f, 0f, 3.5f), 45f, LockDifficulty.Novice, interactablesRoot.transform);

            // Apprentice Chest near forge
            SpawnChest(Chest02Prefab, "Chest_Apprentice_Forge", new Vector3(6.5f, 0f, -2.5f), -30f, LockDifficulty.Apprentice, interactablesRoot.transform);

            // Adept Chest inside ruined crypt
            SpawnChest(Chest02Prefab, "Chest_Adept_Crypt", new Vector3(-6f, 0f, 16f), 10f, LockDifficulty.Adept, interactablesRoot.transform);

            // Master Chest behind the castle gate
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

            var chestComp = chest.GetComponent<ChestInteractable>() ?? chest.AddComponent<ChestInteractable>();
            var field = typeof(ChestInteractable).GetField("_lockDifficulty",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(chestComp, diff);
        }

        private static void SetupCastleGateAndLever()
        {
            var envRoot = GameObject.Find("Environment_Root") ?? new GameObject("Environment_Root");
            var interactablesRoot = GetOrCreateRoot("Interactables_Root");

            // Castle Wall with Archway/Gate at North
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

            // Iron Bar Gate (movable barrier)
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

            // Lever to toggle gate
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

            // Town Guard (Guards faction)
            SpawnCharacter(SoldierPrefab, "Guard_Whiterun_Soldier", new Vector3(-2f, 0f, 13f), 180f, FactionType.Guards, false, npcsRoot.transform);

            // Blacksmith Artisan (Neutral faction)
            SpawnCharacter(BlacksmithNpcPrefab, "Artisan_Blacksmith", new Vector3(8.5f, 0f, 0.5f), -45f, FactionType.Neutral, false, npcsRoot.transform);

            // Bandit / Draugr Lair Boss (Draugr faction - Enemy)
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

            if (isEnemy)
            {
                character.tag = "Enemy";
            }

            if (character.GetComponent<Collider>() == null)
            {
                var col = character.AddComponent<CapsuleCollider>();
                col.height = 1.8f;
                col.center = new Vector3(0f, 0.9f, 0f);
            }

            var faction = character.GetComponent<FactionComponent>() ?? character.AddComponent<FactionComponent>();
            faction.SetFaction(factionType);

            if (isEnemy)
            {
                if (character.GetComponent<BaseActor>() == null)
                {
                    character.AddComponent<BaseActor>();
                }

                if (character.GetComponent<AttributeComponent>() == null)
                {
                    character.AddComponent<AttributeComponent>();
                }

                if (character.GetComponent<EnemyCombatAI>() == null)
                {
                    character.AddComponent<EnemyCombatAI>();
                }
            }
        }

        private static void SetupCompassPOIMarkers()
        {
            AddMarker("BountyBoard_Hub", "Bounty Notice Board", CompassPOIType.QuestMarker, new Vector3(2.5f, 1f, 2.5f));
            AddMarker("Building_Blacksmith_Forge", "Blacksmith's Forge", CompassPOIType.Town, new Vector3(10f, 1f, 2f));
            AddMarker("Building_Village_Inn", "Sleeping Giant Inn", CompassPOIType.Town, new Vector3(-10f, 1f, 2f));
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

            var marker = go.GetComponent<CompassMarkerComponent>() ?? go.AddComponent<CompassMarkerComponent>();

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

        private static GameObject GetOrCreateRoot(string rootName)
        {
            var root = GameObject.Find(rootName);
            if (root == null)
            {
                root = new GameObject(rootName);
            }
            return root;
        }

        #endregion
    }
}
#endif
