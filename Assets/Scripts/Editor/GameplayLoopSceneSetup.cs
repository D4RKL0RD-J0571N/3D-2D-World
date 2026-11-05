#if UNITY_EDITOR
namespace Studio.Editor
{
    using Gameplay.Combat;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using Gameplay.Magic;
    using Gameplay.Skyrim;
    using Gameplay.UI;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Editor utility to configure and persist the full Proving Grounds gameplay loop:
    /// Player weapons & viewmodel, Destruction & Restoration magic, Bandit Archers & Spawners,
    /// NPC dialogue interactions, and iconic Skyrim glitches (Space Program, Killer Cart, Bucket-Head).
    /// </summary>
    public static class GameplayLoopSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/RPGProvingGrounds.unity";
        private const string SwordPrefabPath = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Weapons/SM_Wep_Sword_01.prefab";
        private const string BowPrefabPath = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Weapons/SM_Prop_Bow_01.prefab";
        private const string QuiverPrefabPath = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Weapons/SM_Prop_Quiver_01.prefab";
        private const string ArcherCharPrefabPath = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Characters/SM_Chr_Soldier_Male_01.prefab";
        private const string HayCartPrefabPath = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Hay_Cart_01.prefab";
        private const string BucketPrefabPath = "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Parchment_Bucket_01.prefab";
        private const string DishModelPath = "Assets/Synty/PolygonFantasyKingdom/Models/SM_Item_Dish_01.fbx";

        [MenuItem("3D2DWorld/Setup Proving Grounds Gameplay Loop", false, 100)]
        public static void SetupGameplayLoop()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "RPGProvingGrounds")
            {
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            SetupPlayerCombatAndMagic();
            SetupDraugrBoss();
            SetupBlacksmithAnvil();
            SetupNPCInteractions();
            SetupBanditArcherAndSpawner();
            SetupSkyrimGlitchesAndProps();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=green>✦ All 4 Systems & Skyrim Glitches successfully configured and scene saved!</color>");
        }

        #region Player Combat & Magic

        private static void SetupPlayerCombatAndMagic()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            SafeUnpackPrefabInstance(player);

            var weapon = player.GetOrAddComponent<WeaponComponent>();
            var magic = player.GetOrAddComponent<PlayerMagicController>();
            var wallClip = player.GetOrAddComponent<PlatterWallClip>();

            // Setup Viewmodel Sword on Camera Pivot
            var cameraPivot = player.transform.Find("Camera Pivot");
            if (cameraPivot != null && cameraPivot.Find("Viewmodel_Sword") == null)
            {
                var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
                if (swordPrefab != null)
                {
                    var swordInst = Object.Instantiate(swordPrefab, cameraPivot);
                    swordInst.name = "Viewmodel_Sword";
                    swordInst.transform.localPosition = new Vector3(0.35f, -0.3f, 0.6f);
                    swordInst.transform.localRotation = Quaternion.Euler(15f, -10f, 5f);
                    weapon.WeaponVisualTransform = swordInst.transform;
                }
            }

            magic.EnsureInitialized();
            EditorUtility.SetDirty(player);
        }

        #endregion

        #region Boss & Crafting

        private static void SetupDraugrBoss()
        {
            var draugr = GameObject.Find("Draugr_Wight_Boss");
            if (draugr == null) return;

            SafeUnpackPrefabInstance(draugr);
            if (!draugr.TryGetComponent<AttributeComponent>(out _)) draugr.AddComponent<AttributeComponent>();
            if (!draugr.TryGetComponent<EnemyCombatAI>(out _)) draugr.AddComponent<EnemyCombatAI>();

            EditorUtility.SetDirty(draugr);
        }

        private static void SetupBlacksmithAnvil()
        {
            var anvil = GameObject.Find("Prop_Blacksmith_Anvil");
            if (anvil == null) return;

            SafeUnpackPrefabInstance(anvil);
            if (!anvil.TryGetComponent<CraftingStationInteractable>(out _)) anvil.AddComponent<CraftingStationInteractable>();

            EditorUtility.SetDirty(anvil);
        }

        #endregion

        #region NPC Dialogue

        private static void SetupNPCInteractions()
        {
            var guard = GameObject.Find("Guard_Whiterun_Soldier");
            if (guard != null)
            {
                SafeUnpackPrefabInstance(guard);
                guard.GetOrAddComponent<NPCInteractionComponent>();
                guard.GetOrAddComponent<BucketHeadStealthExploit>();
                EditorUtility.SetDirty(guard);
            }

            var smith = GameObject.Find("Artisan_Blacksmith");
            if (smith != null)
            {
                SafeUnpackPrefabInstance(smith);
                smith.GetOrAddComponent<NPCInteractionComponent>();
                EditorUtility.SetDirty(smith);
            }
        }

        #endregion

        #region Archer & Spawner

        private static void SetupBanditArcherAndSpawner()
        {
            var npcsRoot = GameObject.Find("NPCs_Root") ?? new GameObject("NPCs_Root");
            var existingArcher = GameObject.Find("Bandit_Skeleton_Archer");
            if (existingArcher != null) Object.DestroyImmediate(existingArcher);
            var existingSpawner = GameObject.Find("Encounter_Enemy_Spawner");
            if (existingSpawner != null) Object.DestroyImmediate(existingSpawner);

            var charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherCharPrefabPath);
            if (charPrefab == null) return;

            var archer = Object.Instantiate(charPrefab, new Vector3(4f, 0f, 18f), Quaternion.Euler(0f, 180f, 0f), npcsRoot.transform);
            archer.name = "Bandit_Skeleton_Archer";
            SafeUnpackPrefabInstance(archer);

            archer.GetOrAddComponent<BaseActor>();
            archer.GetOrAddComponent<AttributeComponent>();
            archer.GetOrAddComponent<RangedCombatAI>();

            AttachBowAndQuiver(archer.transform);

            // Add Enemy Spawner in crypt courtyard
            var spawnerGo = new GameObject("Encounter_Enemy_Spawner");
            spawnerGo.transform.parent = npcsRoot.transform;
            spawnerGo.transform.position = new Vector3(0f, 0f, 22f);
            var spawner = spawnerGo.AddComponent<EnemySpawner>();
            spawner.EnemyPrefab = archer;
            spawner.MaxActiveEnemies = 2;

            EditorUtility.SetDirty(archer);
            EditorUtility.SetDirty(spawnerGo);
        }

        private static void AttachBowAndQuiver(Transform archerTransform)
        {
            var bowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BowPrefabPath);
            if (bowPrefab != null)
            {
                var bow = Object.Instantiate(bowPrefab, archerTransform);
                bow.name = "SM_Prop_Bow_01";
                bow.transform.localPosition = new Vector3(-0.25f, 0.9f, 0.3f);
                bow.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }

            var quiverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuiverPrefabPath);
            if (quiverPrefab != null)
            {
                var quiver = Object.Instantiate(quiverPrefab, archerTransform);
                quiver.name = "SM_Prop_Quiver_01";
                quiver.transform.localPosition = new Vector3(0.1f, 1.1f, -0.2f);
                quiver.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            }
        }

        #endregion

        #region Glitches & Props

        private static void SetupSkyrimGlitchesAndProps()
        {
            var envRoot = GameObject.Find("Environment_Root") ?? new GameObject("Environment_Root");

            // Skyrim Space Program and Dialogue HUD on GameBootstrapper
            var bootstrapper = GameObject.Find("GameBootstrapper");
            if (bootstrapper != null)
            {
                bootstrapper.GetOrAddComponent<SkyrimSpaceProgram>();
                bootstrapper.GetOrAddComponent<DialogueHUD>();
                EditorUtility.SetDirty(bootstrapper);
            }

            // Killer Cart
            var existingCart = GameObject.Find("Killer_Hay_Cart");
            if (existingCart != null) Object.DestroyImmediate(existingCart);
            var cartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HayCartPrefabPath);
            if (cartPrefab != null)
            {
                var cart = Object.Instantiate(cartPrefab, new Vector3(5f, 0.1f, 3f), Quaternion.Euler(0f, 35f, 0f), envRoot.transform);
                cart.name = "Killer_Hay_Cart";
                SafeUnpackPrefabInstance(cart);
                cart.GetOrAddComponent<BoxCollider>();
                var rb = cart.GetOrAddComponent<Rigidbody>();
                rb.mass = 45f;
                cart.GetOrAddComponent<PhysicsClatterHazard>();
                EditorUtility.SetDirty(cart);
            }

            // Bucket for Head Exploit
            var existingBucket = GameObject.Find("Exploit_Bucket");
            if (existingBucket != null) Object.DestroyImmediate(existingBucket);
            var bucketPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BucketPrefabPath);
            if (bucketPrefab != null)
            {
                var bucket = Object.Instantiate(bucketPrefab, new Vector3(-2f, 0.8f, 1.2f), Quaternion.identity, envRoot.transform);
                bucket.name = "Exploit_Bucket";
                SafeUnpackPrefabInstance(bucket);
                var col = bucket.GetOrAddComponent<MeshCollider>();
                col.convex = true;
                var rb = bucket.GetOrAddComponent<Rigidbody>();
                rb.mass = 2f;
                EditorUtility.SetDirty(bucket);
            }

            // Platter for Wall Clip
            var existingPlatter = GameObject.Find("Speedrun_Platter");
            if (existingPlatter != null) Object.DestroyImmediate(existingPlatter);
            var dishFbx = AssetDatabase.LoadAssetAtPath<GameObject>(DishModelPath);
            if (dishFbx != null)
            {
                var platter = Object.Instantiate(dishFbx, new Vector3(1f, 0.8f, 1.2f), Quaternion.Euler(0f, 45f, 0f), envRoot.transform);
                platter.name = "Speedrun_Platter";
                SafeUnpackPrefabInstance(platter);
                platter.GetOrAddComponent<BoxCollider>();
                var rb = platter.GetOrAddComponent<Rigidbody>();
                rb.mass = 1f;
                EditorUtility.SetDirty(platter);
            }
        }

        #endregion

        #region Helpers

        private static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var comp)) return comp;
            return go.AddComponent<T>();
        }

        private static void SafeUnpackPrefabInstance(GameObject go)
        {
            if (go == null) return;
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go) ?? go;
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameplayLoopSceneSetup] SafeUnpack skipped for {go.name}: {ex.Message}");
            }
        }

        #endregion
    }
}
#endif
