#if UNITY_EDITOR
namespace Studio.Editor
{
    using System.IO;
    using Domain.Creator;
    using Gameplay.Creator;
    using Gameplay.UI;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Automated setup tool for the In-Game Creator Studio assets, catalog, and HUD prefabs.
    /// Accessible via Tools > 3D 2D World > Setup Creator Studio.
    /// </summary>
    public static class CreatorStudioSetup
    {
        [MenuItem("Tools/3D 2D World/Setup Creator Studio", priority = 100)]
        public static void Setup()
        {
            AssetDatabase.Refresh();

            // 1. Ensure Data/Creator directory exists
            string catalogDir = "Assets/Data/Creator";
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }
            if (!AssetDatabase.IsValidFolder(catalogDir))
            {
                AssetDatabase.CreateFolder("Assets/Data", "Creator");
            }

            // 2. Create or find CreatorCatalog.asset
            string catalogPath = $"{catalogDir}/DefaultCreatorCatalog.asset";
            CreatorCatalogSO catalog = AssetDatabase.LoadAssetAtPath<CreatorCatalogSO>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CreatorCatalogSO>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
                Debug.Log($"[CreatorStudioSetup] Created new CreatorCatalog at {catalogPath}");
            }

            // 3. Register standard prefabs into catalog
            AddOrUpdateItem(catalog, "target_dummy", "Target Combat Dummy", CreatorCategory.Combatants, 
                "Assets/Prefabs/Museum/TargetDummy.prefab", "Interactive training dummy with procedural wobble and health regen.");

            AddOrUpdateItem(catalog, "museum_pedestal", "Showroom Pedestal", CreatorCategory.Props, 
                "Assets/Prefabs/Museum/MuseumPedestal.prefab", "Classic white museum exhibit pedestal.");

            AddOrUpdateItem(catalog, "cube_block", "Standard 1m Block", CreatorCategory.Structures, 
                "Assets/Prefabs/Cube.prefab", "1x1x1 modular building block for voxel/structural prototyping.");

            AddOrUpdateItem(catalog, "slime_enemy", "Green Slime", CreatorCategory.Combatants, 
                "Assets/Prefabs/Entities/Slime.prefab", "Basic enemy combatant entity.");

            AddOrUpdateItem(catalog, "turret_combat", "Automated Defense Turret", CreatorCategory.Combatants, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/Turret.prefab", "Autonomous defensive sentry turret targeting hostiles.");

            // Props & Interactive Elements
            AddOrUpdateItem(catalog, "explosive_barrel", "Explosive Barrel", CreatorCategory.Props, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/ExplosiveBarrel.prefab", "Volatile explosive barrel that detonates on impact or weapon damage.");

            AddOrUpdateItem(catalog, "jump_pad", "Kinetic Jump Pad", CreatorCategory.Props, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/JumpPad.prefab", "High impulse kinetic launcher for vertical platforming.");

            AddOrUpdateItem(catalog, "wooden_crate", "Destructible Crate", CreatorCategory.Props, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/NonFracturedCrate_01.prefab", "Standard breakable wooden supply crate.");

            AddOrUpdateItem(catalog, "lootbox_free", "Free Lootbox Cache", CreatorCategory.Props, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/Lootbox_Free.prefab", "Interactive reward container for modded maps.");

            // Structures
            AddOrUpdateItem(catalog, "modular_door", "Interactive Doorway", CreatorCategory.Structures, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/Door.prefab", "Trigger-activated functional sliding doorway.");

            AddOrUpdateItem(catalog, "basic_ground", "Modular Ground Tile", CreatorCategory.Structures, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/BasicGround.prefab", "Flat structural surface platform for map design.");

            AddOrUpdateItem(catalog, "capture_point", "Strategic Capture Point", CreatorCategory.Structures, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/PointToCapture.prefab", "Territory control objective zone.");

            // Items & Pickups
            AddOrUpdateItem(catalog, "coin_pickup", "Gold Coin Pickup", CreatorCategory.Items, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/Coin.prefab", "Collectible gold reward currency.");

            AddOrUpdateItem(catalog, "powerup_item", "Generic Powerup", CreatorCategory.Items, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/GenericPowerup.prefab", "Temporary player booster powerup.");

            AddOrUpdateItem(catalog, "ammo_pickup", "Bullet Ammo Cache", CreatorCategory.Items, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/Bullet Pickeable.prefab", "Restores firearm ammunition.");

            AddOrUpdateItem(catalog, "weapon_pickup", "Generic Weapon Pickup", CreatorCategory.Weapons, 
                "Assets/Cowsins/Prefabs/DragAndDropExtras/WeaponPickeableGeneric.prefab", "Equippable firearm pickup on the ground.");

            // Weapon prefabs from imported packages if present
            string[] weaponSearch = AssetDatabase.FindAssets("t:Prefab Rifle_WeaponObject");
            if (weaponSearch.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(weaponSearch[0]);
                AddOrUpdateItem(catalog, "rifle_weapon", "Combat Assault Rifle", CreatorCategory.Weapons, path, "Rapid fire combat assault rifle.");
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            // 4. Create CreatorStudioHUD Prefab
            string uiDir = "Assets/Prefabs/UI";
            if (!AssetDatabase.IsValidFolder(uiDir))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            }

            string hudPrefabPath = $"{uiDir}/CreatorStudioHUD.prefab";
            var existingHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(hudPrefabPath);
            if (existingHudPrefab == null)
            {
                var hudGo = new GameObject("CreatorStudioHUD");
                var uiDoc = hudGo.AddComponent<UIDocument>();
                var hudComp = hudGo.AddComponent<CreatorStudioHUD>();

                // Assign UXML and USS
                var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Creator/CreatorStudioOverlay.uxml");
                var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Creator/CreatorStudioOverlay.uss");

                var so = new SerializedObject(hudComp);
                so.FindProperty("_uiDocument").objectReferenceValue = uiDoc;
                so.FindProperty("_creatorUxml").objectReferenceValue = uxml;
                so.FindProperty("_creatorUss").objectReferenceValue = uss;
                so.FindProperty("_catalog").objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Panel settings
                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");
                if (panelSettings != null)
                {
                    uiDoc.panelSettings = panelSettings;
                }
                uiDoc.visualTreeAsset = uxml;

                PrefabUtility.SaveAsPrefabAsset(hudGo, hudPrefabPath);
                Object.DestroyImmediate(hudGo);
                Debug.Log($"[CreatorStudioSetup] Created CreatorStudioHUD prefab at {hudPrefabPath}");
            }

            // 5. Ensure CreatorPlacementController is added to Player prefab
            string playerPrefabPath = "Assets/Prefabs/Entities/Player.prefab";
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab != null)
            {
                var placementComp = playerPrefab.GetComponent<CreatorPlacementController>();
                if (placementComp == null)
                {
                    // Add via PrefabUtility
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                    instance.AddComponent<CreatorPlacementController>();
                    PrefabUtility.SaveAsPrefabAssetAndConnect(instance, playerPrefabPath, InteractionMode.AutomatedAction);
                    Object.DestroyImmediate(instance);
                    Debug.Log($"[CreatorStudioSetup] Added CreatorPlacementController to {playerPrefabPath}");
                }
            }

            // 6. Setup Skyrim Compass markers in scene
            SetupSkyrimMarkers();

            AssetDatabase.SaveAssets();
            Debug.Log("[CreatorStudioSetup] Creator Studio setup completed successfully!");
        }

        private static void AddOrUpdateItem(CreatorCatalogSO catalog, string id, string name, CreatorCategory cat, string prefabPath, string desc)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            var existing = catalog.GetItemById(id);
            if (existing == null)
            {
                var item = new CreatorItemDefinition
                {
                    id = id,
                    displayName = name,
                    category = cat,
                    description = desc,
                    prefab = prefab
                };
                catalog.AddItem(item);
                Debug.Log($"[CreatorStudioSetup] Added '{name}' [{cat}] to catalog.");
            }
            else
            {
                existing.prefab = prefab;
                existing.displayName = name;
                existing.category = cat;
                existing.description = desc;
            }
        }

        [MenuItem("Tools/3D 2D World/Setup Skyrim Compass Markers in Scene", priority = 101)]
        public static void SetupSkyrimMarkers()
        {
            AddMarkerToObject("Wing_Armory", "Armory Sanctum", Domain.Skyrim.CompassPOIType.Dungeon);
            AddMarkerToObject("Wing_Bestiary", "Bestiary Lair", Domain.Skyrim.CompassPOIType.BanditCamp);
            AddMarkerToObject("Wing_Vault", "Treasury Crypt", Domain.Skyrim.CompassPOIType.Crypt);
            AddMarkerToObject("Wing_Atelier", "Atelier Shrine", Domain.Skyrim.CompassPOIType.Shrine);
            AddMarkerToObject("CombatTargetDummy", "Training Dummy", Domain.Skyrim.CompassPOIType.Enemy);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[CreatorStudioSetup] Successfully added and saved Skyrim Compass markers in scene.");
        }

        private static void AddMarkerToObject(string goName, string title, Domain.Skyrim.CompassPOIType poiType)
        {
            var go = GameObject.Find(goName);
            if (go == null) return;

            var marker = go.GetComponent<Gameplay.Skyrim.CompassMarkerComponent>();
            if (marker == null)
            {
                marker = go.AddComponent<Gameplay.Skyrim.CompassMarkerComponent>();
            }

            var idField = typeof(Gameplay.Skyrim.CompassMarkerComponent).GetField("_markerId", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var titleField = typeof(Gameplay.Skyrim.CompassMarkerComponent).GetField("_markerTitle", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var typeField = typeof(Gameplay.Skyrim.CompassMarkerComponent).GetField("_markerType", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            idField?.SetValue(marker, goName);
            titleField?.SetValue(marker, title);
            typeField?.SetValue(marker, poiType);

            EditorUtility.SetDirty(go);
        }
    }
}
#endif
