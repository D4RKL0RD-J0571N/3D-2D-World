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
    }
}
#endif
