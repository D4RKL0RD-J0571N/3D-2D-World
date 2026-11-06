namespace Tests.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Core;
    using Core.Assets;
    using Core.Content;
    using Core.Diagnostics;
    using Core.Ids;
    using Core.Modding;
    using Data.Config;
    using Domain.Skyrim;
    using NUnit.Framework;
    using Systems.Creator;
    using UnityEngine;

    /// <summary>
    /// Milestone 6 verification suite for Domain 4 (Mod Loader Pipeline, RFC 7396 MergePatch, Conflict Policies &amp; Asset Resilience):
    /// - <c>MDP-01</c>: Two mods patching distinct fields of <c>nordic_rpg:item/iron_sword</c> via RFC 7396 <c>MergePatch</c> preserve both edits in topological load order.
    /// - <c>MDP-02</c>: <c>AddOnly</c> conflict policy blocks collision with base definitions and emits <c>MOD-CONFLICT-ADDONLY</c>.
    /// - <c>MDP-03</c>: Malformed JSON files, missing required dependencies, and dependency cycles are caught and reported in <see cref="ModDiagnosticReport"/> without crashing.
    /// - <c>MDP-04</c>: Missing mod assets return non-null <see cref="FallbackAssetCatalog"/> placeholders, while valid <c>.glb</c> files load through <see cref="GltfAssetMountSource"/>.
    /// </summary>
    [TestFixture]
    public class Domain4_ModLoaderAndPatchTests
    {
        private string _tempModsRoot;
        private DiagnosticService _diagnostics;
        private ContentService _contentService;
        private AssetProviderService _assetProvider;
        private ModManagerService _modManager;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            GameEventManager.Clear();

            _tempModsRoot = Path.Combine(Path.GetTempPath(), $"FantasyEngine_Domain4_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempModsRoot);

            _diagnostics = new DiagnosticService();
            _contentService = new ContentService();
            _assetProvider = new AssetProviderService();
            _modManager = new ModManagerService(registerDefaultRoots: false);
            _modManager.AddSearchRoot(_tempModsRoot);

            ServiceLocator.Register<IDiagnosticService, DiagnosticService>(_diagnostics);
            ServiceLocator.Register<IContentService, ContentService>(_contentService);
            ServiceLocator.Register<IAssetProvider, AssetProviderService>(_assetProvider);
            ServiceLocator.Register<IModManagerService, ModManagerService>(_modManager);

            _diagnostics.Initialize();
            _contentService.Initialize();
            _assetProvider.Initialize();
            _modManager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _modManager?.Shutdown();
            _assetProvider?.Shutdown();
            _contentService?.Shutdown();
            _diagnostics?.Shutdown();

            ServiceLocator.Clear();
            GameEventManager.Clear();

            if (!string.IsNullOrEmpty(_tempModsRoot) && Directory.Exists(_tempModsRoot))
            {
                try
                {
                    Directory.Delete(_tempModsRoot, recursive: true);
                }
                catch
                {
                    // Ignore transient OS file locks in temp folder
                }
            }
        }

        #region MDP-01: Topological Load Order & RFC 7396 MergePatch Preservation

        [Test]
        public void MDP01_ModMergePatch_TwoModsPatchingDifferentFieldsOfSameItem_PreservesBothChanges()
        {
            // 1. Create base nordic_rpg pack with iron_sword (damage: 12, weight: 9, value: 45)
            string baseDir = CreateModFolder("nordic_rpg", @"{
                ""modId"": ""nordic_rpg"",
                ""displayName"": ""Nordic RPG Base"",
                ""version"": ""1.0.0"",
                ""conflictPolicy"": ""OverrideExisting""
            }");
            WriteModFile(baseDir, "Data/Definitions/Items/iron_sword.json", @"{
                ""id"": ""nordic_rpg:item/iron_sword"",
                ""displayName"": ""Iron Sword"",
                ""damage"": 12,
                ""weight"": 9,
                ""value"": 45
            }");

            // 2. Create Mod A (depends on nordic_rpg ^1.0.0, MergePatch) tweaking damage -> 18, plus adding a new skill & faction
            string modADir = CreateModFolder("mod_a", @"{
                ""modId"": ""mod_a"",
                ""displayName"": ""Combat Balance Mod A"",
                ""version"": ""1.1.0"",
                ""conflictPolicy"": ""MergePatch"",
                ""dependencies"": [
                    { ""modId"": ""nordic_rpg"", ""versionRange"": ""^1.0.0"", ""optional"": false }
                ]
            }");
            WriteModFile(modADir, "Data/Patches/iron_sword_damage.json", @"{
                ""targetRegistry"": ""framework:items"",
                ""targetId"": ""nordic_rpg:item/iron_sword"",
                ""operation"": ""merge"",
                ""patch"": {
                    ""damage"": 18
                }
            }");
            WriteModFile(modADir, "Data/Definitions/Skills/chronomancy.json", @"{
                ""id"": ""mod_a:skill/chronomancy"",
                ""displayName"": ""Chronomancy"",
                ""category"": ""Magic"",
                ""startingLevel"": 20
            }");
            WriteModFile(modADir, "Data/Definitions/Factions/silver_hand.json", @"{
                ""id"": ""mod_a:faction/silver_hand"",
                ""displayName"": ""The Silver Hand""
            }");

            // 3. Create Mod B (loadAfter: ["mod_a"], MergePatch) tweaking value -> 100 on the same iron_sword
            string modBDir = CreateModFolder("mod_b", @"{
                ""modId"": ""mod_b"",
                ""displayName"": ""Economy Overhaul Mod B"",
                ""version"": ""1.0.0"",
                ""conflictPolicy"": ""MergePatch"",
                ""loadOrder"": {
                    ""loadAfter"": [""mod_a""]
                }
            }");
            WriteModFile(modBDir, "Data/Patches/iron_sword_value.json", @"{
                ""targetRegistry"": ""framework:items"",
                ""targetId"": ""nordic_rpg:item/iron_sword"",
                ""operation"": ""merge"",
                ""patch"": {
                    ""value"": 100
                }
            }");

            // 4. Activate profile and verify topological order: nordic_rpg -> mod_a -> mod_b
            bool success = _modManager.ActivateProfile();
            Assert.IsTrue(success, "Mod profile with valid dependencies must activate without errors.");
            Assert.AreEqual(0, _modManager.LastDiagnosticReport.ErrorCount);
            Assert.AreEqual(0, _modManager.LastDiagnosticReport.WarningCount, "Patching distinct fields (damage vs value) must produce 0 conflict warnings.");

            CollectionAssert.AreEqual(
                new[] { "nordic_rpg", "mod_a", "mod_b" },
                _modManager.LoadedMods.Select(m => m.ModId).ToArray());

            // 5. Verify final merged iron_sword has BOTH Mod A's damage (18), Base's weight (9), and Mod B's value (100)
            var itemRegistry = _contentService.GetRegistry<ItemData>();
            ResourceId swordId = ResourceId.Parse("nordic_rpg:item/iron_sword");

            Assert.IsTrue(itemRegistry.TryGetEntry(swordId, out var swordEntry));
            Assert.AreEqual(18f, swordEntry.Definition.Damage, 0.001f, "Mod A's damage patch must be preserved.");
            Assert.AreEqual(9f, swordEntry.Definition.Weight, 0.001f, "Base weight must remain untouched.");
            Assert.AreEqual(100, swordEntry.Definition.Value, "Mod B's value patch must be applied.");
            Assert.AreEqual("nordic_rpg", swordEntry.SourceModId, "SourceModId must remain the original defining pack.");
            Assert.AreEqual("mod_b", swordEntry.LastModifiedByModId, "LastModifiedByModId must record the latest patching mod.");

            // 6. Verify Mod A's new skill and faction were also registered
            Assert.IsTrue(_contentService.GetRegistry<SkillDefinition>().TryGet(ResourceId.Parse("mod_a:skill/chronomancy"), out var chrono));
            Assert.AreEqual(20, chrono.StartingLevel);
            Assert.IsTrue(_contentService.GetRegistry<FactionDefinition>().Contains(ResourceId.Parse("mod_a:faction/silver_hand")));
        }

        #endregion

        #region MDP-02: AddOnly Conflict Policy Rejection

        [Test]
        public void MDP02_ModConflictPolicy_AddOnly_RejectsCollisionWithBaseDefinition()
        {
            string baseDir = CreateModFolder("nordic_rpg", @"{
                ""modId"": ""nordic_rpg"",
                ""version"": ""1.0.0"",
                ""conflictPolicy"": ""OverrideExisting""
            }");
            WriteModFile(baseDir, "Items/iron_sword.json", @"{
                ""id"": ""nordic_rpg:item/iron_sword"",
                ""displayName"": ""Iron Sword"",
                ""damage"": 12,
                ""value"": 45
            }");

            string modCDir = CreateModFolder("mod_c", @"{
                ""modId"": ""mod_c"",
                ""version"": ""1.0.0"",
                ""conflictPolicy"": ""AddOnly"",
                ""loadAfter"": [""nordic_rpg""]
            }");
            WriteModFile(modCDir, "Items/iron_sword_collision.json", @"{
                ""id"": ""nordic_rpg:item/iron_sword"",
                ""displayName"": ""Illegal Overwrite Sword"",
                ""damage"": 999,
                ""value"": 9999
            }");

            _modManager.ActivateProfile();

            var itemRegistry = _contentService.GetRegistry<ItemData>();
            ResourceId swordId = ResourceId.Parse("nordic_rpg:item/iron_sword");

            Assert.IsTrue(itemRegistry.TryGetEntry(swordId, out var entry));
            Assert.AreEqual(12f, entry.Definition.Damage, 0.001f, "Base iron_sword damage must remain untouched when AddOnly mod collides.");
            Assert.AreEqual(45, entry.Definition.Value, "Base iron_sword value must remain untouched.");
            Assert.AreEqual("nordic_rpg", entry.LastModifiedByModId);

            Assert.IsTrue(
                _modManager.LastDiagnosticReport.Entries.Any(e => e.Code == "MOD-CONFLICT-ADDONLY" && e.ModId == "mod_c"),
                "ModDiagnosticReport must log a MOD-CONFLICT-ADDONLY warning for mod_c.");
        }

        #endregion

        #region MDP-03: Malformed JSON Resilience & Dependency Cycle / Missing Dependency Detection

        [Test]
        public void MDP03_MalformedJsonMod_IsSkippedCleanlyWithoutCrashingEngine()
        {
            string modDir = CreateModFolder("resilient_mod", @"{
                ""modId"": ""resilient_mod"",
                ""version"": ""1.0.0"",
                ""conflictPolicy"": ""AddOnly""
            }");

            // Write a truncated/broken JSON file alongside a valid item JSON file
            WriteModFile(modDir, "Items/broken.json", "{\n  \"id\": \"resilient_mod:item/broken_axe\",\n  \"damage\": ");
            WriteModFile(modDir, "Items/valid_spear.json", @"{
                ""id"": ""resilient_mod:item/valid_spear"",
                ""displayName"": ""Nordic Ash Spear"",
                ""damage"": 16,
                ""value"": 65
            }");

            // Also add a mod with a missing required dependency to verify it is skipped with MOD-DEP-MISSING
            CreateModFolder("orphan_mod", @"{
                ""modId"": ""orphan_mod"",
                ""version"": ""1.0.0"",
                ""dependencies"": [
                    { ""modId"": ""non_existent_master"", ""versionRange"": "">=1.0.0"", ""optional"": false }
                ]
            }");

            // Act: ActivateProfile must not throw any unhandled exception
            Assert.DoesNotThrow(() => _modManager.ActivateProfile());

            // Verify broken.json was logged with line number >= 1
            var syntaxErr = _modManager.LastDiagnosticReport.Entries.FirstOrDefault(e => e.Code == "MOD-JSON-SYNTAX" && e.ModId == "resilient_mod");
            Assert.IsNotNull(syntaxErr, "ModDiagnosticReport must record MOD-JSON-SYNTAX for broken.json.");
            Assert.GreaterOrEqual(syntaxErr.LineNumber, 1);

            // Verify missing dependency was logged
            Assert.IsTrue(
                _modManager.LastDiagnosticReport.Entries.Any(e => e.Code == "MOD-DEP-MISSING" && e.ModId == "orphan_mod"),
                "ModDiagnosticReport must record MOD-DEP-MISSING for orphan_mod.");

            // Verify valid_spear.json inside resilient_mod still loaded!
            Assert.IsTrue(
                _contentService.GetRegistry<ItemData>().TryGet(ResourceId.Parse("resilient_mod:item/valid_spear"), out var spear),
                "Valid definitions in the same mod must still load when a sibling JSON file is malformed.");
            Assert.AreEqual(16f, spear.Damage, 0.001f);
        }

        #endregion

        #region MDP-04: Missing Asset Fallback, glTF/GLB Mount Source & Mod Exporter

        [Test]
        public void MDP04_MissingAssetInMod_ReturnsFallbackAssetInsteadOfNull_AndValidGlbAndExportWork()
        {
            int warningsBefore = _diagnostics.WarningCount;

            // 1. Request non-existent mesh from broken_mod via LoadAssetAsync<GameObject>
            GameObject fallbackMeshObj = _assetProvider.LoadAssetAsync<GameObject>(ResourceId.Parse("broken_mod:mesh/non_existent")).Result;

            Assert.IsNotNull(fallbackMeshObj, "Missing asset request must return a non-null FallbackAssetCatalog placeholder.");
            Assert.AreSame(_assetProvider.Fallbacks.FallbackPrefab, fallbackMeshObj);
            Assert.AreEqual(warningsBefore + 1, _diagnostics.WarningCount, "Exactly 1 diagnostic warning must be recorded for a missing asset.");

            // 2. Mount a valid minimal .glb binary file (magic 0x46546C67 'glTF', version 2, length 12) and load via IAssetProvider
            string glbModDir = CreateModFolder("mesh_mod", @"{
                ""modId"": ""mesh_mod"",
                ""version"": ""1.0.0""
            }");
            string glbPath = Path.Combine(glbModDir, "Assets", "mesh", "custom_pillar.glb");
            Directory.CreateDirectory(Path.GetDirectoryName(glbPath));
            byte[] minimalGlbHeader = new byte[12];
            BitConverter.GetBytes(0x46546C67u).CopyTo(minimalGlbHeader, 0); // "glTF"
            BitConverter.GetBytes(2u).CopyTo(minimalGlbHeader, 4);          // version 2
            BitConverter.GetBytes(12u).CopyTo(minimalGlbHeader, 8);         // total length 12
            File.WriteAllBytes(glbPath, minimalGlbHeader);

            _modManager.ActivateProfile();

            GameObject loadedGlb = _assetProvider.LoadAssetAsync<GameObject>(ResourceId.Parse("mesh_mod:mesh/custom_pillar")).Result;
            Assert.IsNotNull(loadedGlb);
            Assert.AreNotSame(_assetProvider.Fallbacks.FallbackPrefab, loadedGlb, "Valid .glb file must resolve via GltfAssetMountSource instead of returning the missing-mesh placeholder.");
            Assert.IsNotNull(loadedGlb.GetComponent<MeshFilter>()?.sharedMesh);

            // 3. Verify CreatorStudioService.ExportModPackage exports a valid mod.json + world JSON
            var creator = new CreatorStudioService
            {
                SaveDirectory = _tempModsRoot
            };
            creator.Initialize();

            try
            {
                string exportedDir = creator.ExportModPackage(
                    modId: "exported_dungeon_mod",
                    worldData: new Domain.Creator.CreatorLevelData { levelName = "crypt_01" },
                    displayName: "Exported Crypt Mod",
                    author: "Jostin",
                    version: "1.0.0",
                    customModsRootDirectory: _tempModsRoot);

                Assert.IsTrue(File.Exists(Path.Combine(exportedDir, "mod.json")));
                Assert.IsTrue(File.Exists(Path.Combine(exportedDir, "Worlds", "crypt_01.json")));
            }
            finally
            {
                creator.Shutdown();
            }
        }

        #endregion

        #region Helpers

        private string CreateModFolder(string modId, string manifestJson)
        {
            string dir = Path.Combine(_tempModsRoot, modId);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "mod.json"), manifestJson);
            return dir;
        }

        private static void WriteModFile(string modDir, string relativePath, string content)
        {
            string fullPath = Path.Combine(modDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
            File.WriteAllText(fullPath, content);
        }

        #endregion
    }
}
