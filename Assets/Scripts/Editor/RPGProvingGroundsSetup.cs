#if UNITY_EDITOR
namespace Studio.Editor
{
    using Domain.Skyrim;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using Gameplay.Museum;
    using Gameplay.Skyrim;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    /// <summary>
    /// Automated setup tool that populates the RPGProvingGrounds scene with
    /// GameBootstrapper, Player rig, interactive chests, levers, bounty boards,
    /// faction NPCs, and 360 compass POI markers.
    /// </summary>
    public static class RPGProvingGroundsSetup
    {
        private const string ScenePath = "Assets/Scenes/RPGProvingGrounds.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Entities/Player.prefab";

        [MenuItem("3D2DWorld/Setup RPG Proving Grounds World", false, 1)]
        public static void SetupScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[RPGProvingGroundsSetup] Could not open scene at {ScenePath}");
                return;
            }

            Debug.Log("[RPGProvingGroundsSetup] Populating RPG Proving Grounds World...");

            EnsureBootstrapper();
            EnsurePlayer();
            DisableStandaloneCameras();
            SetupTownSquareBountyBoard();
            SetupDungeonChests();
            SetupLeverAndGate();
            SetupTraps();
            SetupCombatYard();
            SetupFactionNPCs();
            SetupCompassMarkers();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=green>✦ RPG Proving Grounds World successfully configured and saved!</color>");
        }

        #region Setup Steps

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
            if (existingPlayer != null) return;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab != null)
            {
                var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerInstance.name = "Player";
                playerInstance.transform.position = new Vector3(0f, 1f, 0f);
                Debug.Log("[RPGProvingGroundsSetup] Spawned Player at town center.");
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
                    Debug.Log($"[RPGProvingGroundsSetup] Disabled standalone camera: {cam.name}");
                }
            }
        }

        private static void SetupTownSquareBountyBoard()
        {
            if (GameObject.Find("BountyBoard_Hub") != null) return;

            var boardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/BountyBoard.prefab");
            if (boardPrefab != null)
            {
                var board = (GameObject)PrefabUtility.InstantiatePrefab(boardPrefab);
                board.name = "BountyBoard_Hub";
                board.transform.position = new Vector3(3f, 0.9f, 2f);
                board.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
            }
        }

        private static void SetupDungeonChests()
        {
            var chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/DungeonChest.prefab");
            if (chestPrefab == null) return;

            // Novice chest in courtyard
            if (GameObject.Find("Chest_Novice_Courtyard") == null)
            {
                var chest1 = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab);
                chest1.name = "Chest_Novice_Courtyard";
                chest1.transform.position = new Vector3(-4f, 0.4f, 5f);
                SetChestDifficulty(chest1, LockDifficulty.Novice);
            }

            // Adept chest in ruined bank
            if (GameObject.Find("Chest_Adept_Bank") == null)
            {
                var chest2 = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab);
                chest2.name = "Chest_Adept_Bank";
                chest2.transform.position = new Vector3(-12f, 0.4f, -8f);
                SetChestDifficulty(chest2, LockDifficulty.Adept);
            }
        }

        private static void SetChestDifficulty(GameObject chestGo, LockDifficulty diff)
        {
            var chestComp = chestGo.GetComponent<ChestInteractable>();
            if (chestComp == null) return;

            var field = typeof(ChestInteractable).GetField("_lockDifficulty",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(chestComp, diff);
        }

        private static void SetupLeverAndGate()
        {
            if (GameObject.Find("SecurityGate_Barricade") != null) return;

            // Gate barrier
            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "SecurityGate_Barricade";
            gate.transform.position = new Vector3(8f, 1.5f, 0f);
            gate.transform.localScale = new Vector3(0.4f, 3f, 4f);

            // Lever
            var leverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/DungeonLever.prefab");
            if (leverPrefab != null)
            {
                var lever = (GameObject)PrefabUtility.InstantiatePrefab(leverPrefab);
                lever.name = "Lever_SecurityGate";
                lever.transform.position = new Vector3(7f, 0.5f, 2.5f);

                var leverComp = lever.GetComponent<LeverInteractable>();
                if (leverComp != null)
                {
                    var field = typeof(LeverInteractable).GetField("_linkedObjects",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(leverComp, new GameObject[] { gate });
                }
            }
        }

        private static void SetupTraps()
        {
            if (GameObject.Find("Trap_PressurePlate_Alley") != null) return;

            var platePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/PressurePlate.prefab");
            if (platePrefab != null)
            {
                var plate = (GameObject)PrefabUtility.InstantiatePrefab(platePrefab);
                plate.name = "Trap_PressurePlate_Alley";
                plate.transform.position = new Vector3(5f, 0.05f, -6f);
            }
        }

        private static void SetupCombatYard()
        {
            if (GameObject.Find("TrainingDummy_Yard") != null) return;

            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Museum/TargetDummy.prefab");
            if (dummyPrefab != null)
            {
                var dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab);
                dummy.name = "TrainingDummy_Yard";
                dummy.transform.position = new Vector3(0f, 0.5f, 10f);
            }
        }

        private static void SetupFactionNPCs()
        {
            // Hostile Zombie in alley
            if (GameObject.Find("Zombie_Hostile_Alley") == null)
            {
                var zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SimpleApocalypse/Prefabs/Characters/Zombies/SA_Zombie_Soldier.prefab");
                if (zombiePrefab != null)
                {
                    var zombie = (GameObject)PrefabUtility.InstantiatePrefab(zombiePrefab);
                    zombie.name = "Zombie_Hostile_Alley";
                    zombie.tag = "Enemy";
                    zombie.transform.position = new Vector3(-8f, 0.5f, 12f);

                    var baseActor = zombie.GetComponent<BaseActor>() ?? zombie.AddComponent<BaseActor>();
                    var faction = zombie.GetComponent<FactionComponent>() ?? zombie.AddComponent<FactionComponent>();
                    faction.SetFaction(FactionType.Draugr);
                }
            }

            // Friendly Sheriff Guard
            if (GameObject.Find("Survivor_Sheriff_Guard") == null)
            {
                var sheriffPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/SimpleApocalypse/Prefabs/Characters/Survivors/SA_Character_Survivor_Sheriff.prefab");
                if (sheriffPrefab != null)
                {
                    var sheriff = (GameObject)PrefabUtility.InstantiatePrefab(sheriffPrefab);
                    sheriff.name = "Survivor_Sheriff_Guard";
                    sheriff.transform.position = new Vector3(2f, 0.5f, -2f);

                    var faction = sheriff.GetComponent<FactionComponent>() ?? sheriff.AddComponent<FactionComponent>();
                    faction.SetFaction(FactionType.Guards);
                }
            }
        }

        private static void SetupCompassMarkers()
        {
            AddMarker("TownSquare_Marker", "Survivor Outpost", CompassPOIType.Town, new Vector3(0f, 1f, 0f));
            AddMarker("BountyBoard_Hub", "Notice Board", CompassPOIType.QuestMarker, new Vector3(3f, 1f, 2f));
            AddMarker("Chest_Adept_Bank", "Ruined Bank Vault", CompassPOIType.Dungeon, new Vector3(-12f, 1f, -8f));
            AddMarker("Zombie_Hostile_Alley", "Infested Alley", CompassPOIType.BanditCamp, new Vector3(-8f, 1f, 12f));
            AddMarker("TrainingDummy_Yard", "Training Yard", CompassPOIType.Enemy, new Vector3(0f, 1f, 10f));
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

        #endregion
    }
}
#endif
