#if UNITY_EDITOR
namespace Studio.Editor
{
    using System;
    using System.IO;
    using Domain.Creator;
    using Gameplay.Creator;
    using Gameplay.UI;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Automated setup tool for the In-Game Creator Studio assets, Synty fantasy catalog (<c>ADR-008</c>), and HUD prefabs.
    /// Accessible via Tools > 3D 2D World > Setup Creator Studio.
    /// </summary>
    public static class CreatorStudioSetup
    {
        [MenuItem("Tools/3D 2D World/Setup Creator Studio", priority = 100)]
        public static void Setup()
        {
            AssetDatabase.Refresh();
            PopulateDefaultCatalog();

            // 4. Create CreatorStudioHUD Prefab
            string catalogPath = "Assets/Data/Creator/DefaultCreatorCatalog.asset";
            CreatorCatalogSO catalog = AssetDatabase.LoadAssetAtPath<CreatorCatalogSO>(catalogPath);

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

                var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Creator/CreatorStudioOverlay.uxml");
                var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Creator/CreatorStudioOverlay.uss");

                var so = new SerializedObject(hudComp);
                so.FindProperty("_uiDocument").objectReferenceValue = uiDoc;
                so.FindProperty("_creatorUxml").objectReferenceValue = uxml;
                so.FindProperty("_creatorUss").objectReferenceValue = uss;
                so.FindProperty("_catalog").objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();

                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");
                if (panelSettings != null)
                {
                    uiDoc.panelSettings = panelSettings;
                }
                uiDoc.visualTreeAsset = uxml;

                PrefabUtility.SaveAsPrefabAsset(hudGo, hudPrefabPath);
                UnityEngine.Object.DestroyImmediate(hudGo);
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
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                    instance.AddComponent<CreatorPlacementController>();
                    PrefabUtility.SaveAsPrefabAssetAndConnect(instance, playerPrefabPath, InteractionMode.AutomatedAction);
                    UnityEngine.Object.DestroyImmediate(instance);
                    Debug.Log($"[CreatorStudioSetup] Added CreatorPlacementController to {playerPrefabPath}");
                }
            }

            // 6. Setup Skyrim Compass markers in scene
            SetupSkyrimMarkers();

            AssetDatabase.SaveAssets();
            Debug.Log("[CreatorStudioSetup] Creator Studio setup completed successfully!");
        }

        /// <summary>
        /// Populates <c>Assets/Data/Creator/DefaultCreatorCatalog.asset</c> with core and Synty
        /// <c>POLYGON Fantasy Kingdom</c> prefabs, purging any legacy <c>Cowsins</c> or <c>SoftKitty</c> entries (<c>ADR-008</c>).
        /// </summary>
        public static CreatorCatalogSO PopulateDefaultCatalog()
        {
            string catalogDir = "Assets/Data/Creator";
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }
            if (!AssetDatabase.IsValidFolder(catalogDir))
            {
                AssetDatabase.CreateFolder("Assets/Data", "Creator");
            }

            string catalogPath = $"{catalogDir}/DefaultCreatorCatalog.asset";
            CreatorCatalogSO catalog = AssetDatabase.LoadAssetAtPath<CreatorCatalogSO>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CreatorCatalogSO>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
                Debug.Log($"[CreatorStudioSetup] Created new CreatorCatalog at {catalogPath}");
            }

            // Purge any null or legacy Cowsins/SoftKitty items before populating
            catalog.RemoveWhere(item =>
            {
                if (item == null || item.prefab == null) return true;
                string path = AssetDatabase.GetAssetPath(item.prefab);
                return string.IsNullOrEmpty(path) ||
                       path.StartsWith("Assets/Cowsins/", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("Assets/SoftKitty/", StringComparison.OrdinalIgnoreCase);
            });

            // 1. Core Proving Grounds Prefabs
            AddOrUpdateItem(catalog, "target_dummy", "Target Combat Dummy", CreatorCategory.Combatants,
                "Assets/Prefabs/Museum/TargetDummy.prefab", "Interactive training dummy with procedural wobble and health regen.");

            AddOrUpdateItem(catalog, "museum_pedestal", "Showroom Pedestal", CreatorCategory.Props,
                "Assets/Prefabs/Museum/MuseumPedestal.prefab", "Classic white museum exhibit pedestal.");

            AddOrUpdateItem(catalog, "cube_block", "Standard 1m Block", CreatorCategory.Structures,
                "Assets/Prefabs/Cube.prefab", "1x1x1 modular building block for voxel/structural prototyping.");

            AddOrUpdateItem(catalog, "slime_enemy", "Green Slime", CreatorCategory.Combatants,
                "Assets/Prefabs/Entities/Slime.prefab", "Basic enemy combatant entity.");

            // 2. Synty POLYGON Fantasy Kingdom Prefabs (Replacing all legacy FPS Cowsins prefabs per ADR-008)
            AddOrUpdateItem(catalog, "turret_combat", "Mounted Defense Ballista", CreatorCategory.Combatants,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/SiegeEngines/SM_Wep_Ballista_Mounted_01.prefab",
                "Heavy siege ballista for castle rampart defense.");

            AddOrUpdateItem(catalog, "explosive_barrel", "Alchemical Powder Barrel", CreatorCategory.Props,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Barrel_01.prefab",
                "Wooden storage barrel for dungeon corridors and cellars.");

            AddOrUpdateItem(catalog, "jump_pad", "Iron Camp Brazier", CreatorCategory.Props,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/BattleGround/SM_Prop_Camp_Brazier_01.prefab",
                "Forged iron brazier providing warmth and dungeon illumination.");

            AddOrUpdateItem(catalog, "wooden_crate", "Reinforced Supply Crate", CreatorCategory.Props,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Crate_Wood_01.prefab",
                "Standard wooden merchant crate for dungeon storage rooms.");

            AddOrUpdateItem(catalog, "lootbox_free", "Ironbound Treasure Chest", CreatorCategory.Props,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/Furniture/SM_Prop_Chest_01.prefab",
                "Lockable dungeon treasure chest for rewarding exploration.");

            AddOrUpdateItem(catalog, "modular_door", "Timber Wall Doorway", CreatorCategory.Structures,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Buildings/House/SM_Bld_House_Wall_Door_01.prefab",
                "Modular fantasy doorway wall section.");

            AddOrUpdateItem(catalog, "basic_ground", "Stone Flagstone Floor", CreatorCategory.Structures,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Buildings/House/SM_Bld_House_Floor_Stone_01.prefab",
                "Modular cut-stone floor tile for dungeons and keeps.");

            AddOrUpdateItem(catalog, "capture_point", "Warlord Battle Banner", CreatorCategory.Structures,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/Banners/SM_Prop_Battle_Banner_01.prefab",
                "Heraldic faction standard marking territory control.");

            AddOrUpdateItem(catalog, "coin_pickup", "Ancient Spell Tome", CreatorCategory.Items,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Items/SM_Item_Book_01.prefab",
                "Leather-bound arcane tome containing forgotten lore.");

            AddOrUpdateItem(catalog, "powerup_item", "Alchemist Retort Stand", CreatorCategory.Items,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Alchemy_Stand_01.prefab",
                "Alchemical brewing apparatus for potions and elixirs.");

            AddOrUpdateItem(catalog, "ammo_pickup", "Fletcher Arrow Barrel", CreatorCategory.Items,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Barrel_Arrows_01.prefab",
                "Quiver cache stocked with bodkin arrows.");

            AddOrUpdateItem(catalog, "weapon_pickup", "Forged Broadsword Display", CreatorCategory.Weapons,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Props/SM_Prop_Forge_Sword_01.prefab",
                "Tempered steel broadsword fresh from the blacksmith's anvil.");

            AddOrUpdateItem(catalog, "hunter_longbow", "Ashwood Hunter Longbow", CreatorCategory.Weapons,
                "Assets/Synty/PolygonFantasyKingdom/Prefabs/Weapons/SM_Prop_Bow_01.prefab",
                "Balanced ashwood longbow for ranged marksmen.");

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void AddOrUpdateItem(CreatorCatalogSO catalog, string id, string name, CreatorCategory cat, string prefabPath, string desc)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CreatorStudioSetup] Prefab not found at '{prefabPath}' for item '{id}'.");
                return;
            }

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
            CreateMarkerIfMissing("Bounty Board", new Vector3(0f, 1.5f, 12f), Domain.Skyrim.CompassPOIType.QuestMarker);
            CreateMarkerIfMissing("Blacksmith Forge", new Vector3(-15f, 1.5f, 8f), Domain.Skyrim.CompassPOIType.Town);
            CreateMarkerIfMissing("Bandit Outpost", new Vector3(28f, 1.5f, 35f), Domain.Skyrim.CompassPOIType.BanditCamp);
            CreateMarkerIfMissing("Ancient Crypt", new Vector3(-32f, 1.5f, -24f), Domain.Skyrim.CompassPOIType.Crypt);
            CreateMarkerIfMissing("Town Guard Captain", new Vector3(8f, 1.5f, -10f), Domain.Skyrim.CompassPOIType.Shrine);

            Debug.Log("[CreatorStudioSetup] Skyrim Compass Markers placed in scene.");
        }

        private static void CreateMarkerIfMissing(string title, Vector3 pos, Domain.Skyrim.CompassPOIType type)
        {
            string objName = $"[POI] {title}";
            if (GameObject.Find(objName) != null) return;

            var go = new GameObject(objName);
            go.transform.position = pos;
            var marker = go.AddComponent<Gameplay.Skyrim.CompassMarkerComponent>();
            marker.Configure(title, type);
            Undo.RegisterCreatedObjectUndo(go, "Create Compass Marker");
        }
    }
}
#endif
