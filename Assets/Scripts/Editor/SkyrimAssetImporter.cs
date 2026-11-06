#if UNITY_EDITOR
namespace Studio.Editor
{
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Editor utility for discovering, previewing, and importing high-value RPG assets
    /// from the external Assets Unity v2 library directly into the project.
    /// </summary>
    public static class SkyrimAssetImporter
    {
        #region Constants

        private const string BaseDownloadsPath = 
            @"D:\Development\Game Development\Downloads\Assets unity v2\10_2D & Icon & GUI & Decal\03_Icons & UI";

        private const string RootDownloadsPath = 
            @"D:\Development\Game Development\Downloads";

        #endregion

        #region Menu Items - Core RPG Assets

        [MenuItem("3D2DWorld/Asset Library/1. Import Skill Icons Pack (147 skills)", false, 10)]
        public static void ImportSkillIcons()
        {
            ImportPackageBySubpath(
                @"Skill Icon Pack\Skill Icon Pack v1.02.unitypackage",
                "Skill Icon Pack");
        }

        [MenuItem("3D2DWorld/Asset Library/2. Import Fantasy Spells Pack (686 spells)", false, 11)]
        public static void ImportSpellsIcons()
        {
            ImportPackageBySubpath(
                @"150+ Fantasy Spells Icon Pack\150 Fantasy Spells Icon Pack v1.0.unitypackage",
                "150+ Fantasy Spells Pack");
        }

        [MenuItem("3D2DWorld/Asset Library/3. Import Treasure Chest Icons (Chests & Gems)", false, 12)]
        public static void ImportTreasureChests()
        {
            ImportPackageBySubpath(
                @"Treasure Chest Icons\Treasure Chest Icons v1.0.unitypackage",
                "Treasure Chest Icons");
        }

        [MenuItem("3D2DWorld/Asset Library/4. Import Loot Icons (Ingredients, Slime, Bones)", false, 13)]
        public static void ImportLootIcons()
        {
            ImportPackageBySubpath(
                @"Loot icons\Loot icons v1.01.unitypackage",
                "Loot Icons");
        }

        [MenuItem("3D2DWorld/Asset Library/5. Import RPG Weapons Icons (Axes, Swords, Bows)", false, 14)]
        public static void ImportWeaponsIcons()
        {
            ImportPackageBySubpath(
                @"RPG Weapons Icons\RPG Weapons Icons v1.01.unitypackage",
                "RPG Weapons Icons");
        }

        [MenuItem("3D2DWorld/Asset Library/6. Import Weapon and Armor Pack (Maces, Armor, Rings)", false, 15)]
        public static void ImportWeaponAndArmor()
        {
            ImportPackageBySubpath(
                @"Weapon and Armor Icon Pack\Weapon and Armor Icon Pack v1.01.unitypackage",
                "Weapon and Armor Icon Pack");
        }

        [MenuItem("3D2DWorld/Asset Library/7. Import Herbal Icons (Alchemy Ingredients)", false, 16)]
        public static void ImportHerbalIcons()
        {
            ImportPackageBySubpath(
                @"Herbal icons\Herbal icons v1.01.unitypackage",
                "Herbal Icons");
        }

        [MenuItem("3D2DWorld/Asset Library/8. Import Food Icons (Survival Rations)", false, 17)]
        public static void ImportFoodIcons()
        {
            ImportPackageBySubpath(
                @"Food Icon Pack\Food Icon Pack v1.01.unitypackage",
                "Food Icon Pack");
        }

        [MenuItem("3D2DWorld/Asset Library/9. Import Quest Journal Assets (Frames & Parchment)", false, 18)]
        public static void ImportQuestJournal()
        {
            ImportPackageBySubpath(
                @"Quest Journal\Quest Journal v1.0.unitypackage",
                "Quest Journal Assets");
        }

        #endregion

        #region Menu Items - 3D Worlds, Environment & Magic VFX

        [MenuItem("3D2DWorld/Asset Library/3D & VFX/★ Import POLYGON Fantasy Kingdom (2625 Modular Castle & Props)", false, 20)]
        public static void ImportPolygonFantasyKingdom()
        {
            ImportPackageByRootPath(
                "2026-03-03-POLYGON Fantasy Kingdom Unity 2022 3 v1.12.3.unitypackage",
                "POLYGON Fantasy Kingdom (Synty)");
        }

        [MenuItem("3D2DWorld/Asset Library/3D & VFX/Import Magic Arsenal (600+ Spell Effects & Projectiles)", false, 21)]
        public static void ImportMagicArsenal()
        {
            ImportPackageByRootPath(
                "2024-12-04-Magic Arsenal v2.71.unitypackage",
                "Magic Arsenal Spell VFX");
        }

        [MenuItem("3D2DWorld/Asset Library/3D & VFX/Import Fantasy Forest Environment (Trees, Rocks, Vegetation)", false, 22)]
        public static void ImportFantasyForest()
        {
            ImportPackageByRootPath(
                "Fantasy Forest Environment v2.4.unitypackage",
                "Fantasy Forest Environment");
        }

        [MenuItem("3D2DWorld/Asset Library/3D & VFX/Import Weather Maker (Skyrim Fog, Rain, Snow, Day-Night)", false, 23)]
        public static void ImportWeatherMaker()
        {
            ImportPackageByRootPath(
                "Weather Maker - Volumetric Clouds and Weather System for Unity v8.0.9 (12 Jul 2026).unitypackage",
                "Weather Maker Dynamic Sky");
        }

        [MenuItem("3D2DWorld/Asset Library/3D & VFX/Import Meat & Provisions Pack (Tavern & Camp Food Props)", false, 24)]
        public static void ImportMeatPack()
        {
            ImportPackageByRootPath(
                "Meat Pack 1.0 (2017).unitypackage",
                "Meat & Provisions Pack");
        }

        [MenuItem("3D2DWorld/Asset Library/3D & VFX/Import Northstar Navigation (3D Markers & Beacons)", false, 25)]
        public static void ImportNorthstarNav()
        {
            ImportPackageByRootPath(
                "Northstar Navigation v1.2.1.unitypackage",
                "Northstar Navigation System");
        }

        #endregion

        #region Package Import Helper

        /// <summary>
        /// Validates package existence and invokes Unity's AssetDatabase.ImportPackage.
        /// </summary>
        private static void ImportPackageBySubpath(string relativeSubpath, string displayName)
        {
            string fullPath = Path.Combine(BaseDownloadsPath, relativeSubpath);
            ExecuteImport(fullPath, displayName);
        }

        private static void ImportPackageByRootPath(string fileName, string displayName)
        {
            string fullPath = Path.Combine(RootDownloadsPath, fileName);
            ExecuteImport(fullPath, displayName);
        }

        private static void ExecuteImport(string fullPath, string displayName)
        {
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog(
                    "Package Not Found",
                    $"Could not locate package at:\n{fullPath}",
                    "OK");
                Debug.LogWarning($"[SkyrimAssetImporter] Package not found: {fullPath}");
                return;
            }

            Debug.Log($"[SkyrimAssetImporter] Importing {displayName} from {fullPath}...");

            // interactive: false executes import silently without blocking dialog
            AssetDatabase.ImportPackage(fullPath, false);

            Debug.Log($"[SkyrimAssetImporter] Successfully initiated import for {displayName}!");
        }

        #endregion

        #region Batch Importer

        [MenuItem("3D2DWorld/Asset Library/★ Import Essential Fantasy RPG Suite (Skills + Spells + Chests + Loot)", false, 50)]
        public static void ImportEssentialSuite()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Import Essential Fantasy RPG Suite",
                "This will import:\n" +
                "• Skill Icon Pack (147 icons)\n" +
                "• Fantasy Spells Pack (686 icons)\n" +
                "• Treasure Chest Icons (18 icons)\n" +
                "• Loot Icons (305 icons)\n\n" +
                "Do you wish to proceed?",
                "Import", "Cancel");

            if (!proceed) return;

            ImportSkillIcons();
            ImportSpellsIcons();
            ImportTreasureChests();
            ImportLootIcons();
        }

        #endregion
    }
}
#endif
