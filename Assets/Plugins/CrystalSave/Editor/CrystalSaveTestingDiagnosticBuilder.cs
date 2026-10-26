#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
    public sealed class CrystalSaveTestingDiagnosticBuilder : EditorWindow
    {
        private const string RootFolder = "Assets/CrystalSaveDiagnostics/Testing";
        private const string EditorFolder = RootFolder + "/Editor";
        private const string TestScriptPath = EditorFolder + "/CrystalSaveGeneratedEditModeTests.cs";
        private const string ReadmePath = RootFolder + "/README.md";

        private bool createEditModeTests = true;
        private bool createReadme = true;
        private bool overwriteExisting = true;
        private bool revealFolderAfterCreate = true;

        [MenuItem("Tools/Crystal Save/Runtime Debug/Diagnostics/Crystal Save Test Setup")]
        public static void ShowWindow()
        {
            CrystalSaveTestingDiagnosticBuilder window = GetWindow<CrystalSaveTestingDiagnosticBuilder>("Crystal Save Test Setup");
            window.minSize = new Vector2(500f, 260f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Crystal Save Test Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates disposable Unity Test Runner EditMode tests for Crystal Save's safe testing seams. The generated tests use isolated temp folders and do not touch real project saves.",
                MessageType.Info);

            createEditModeTests = EditorGUILayout.ToggleLeft("Create generated EditMode tests", createEditModeTests);
            createReadme = EditorGUILayout.ToggleLeft("Create generated test README", createReadme);
            overwriteExisting = EditorGUILayout.ToggleLeft("Overwrite existing generated files", overwriteExisting);
            revealFolderAfterCreate = EditorGUILayout.ToggleLeft("Reveal output folder after create", revealFolderAfterCreate);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(TestScriptPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.SelectableLabel(ReadmePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Full SaveManager scene lifecycle tests should remain PlayMode or runtime diagnostics. This setup intentionally avoids creating a test asmdef because many Unity projects keep runtime code in Assembly-CSharp.",
                MessageType.Warning);

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!createEditModeTests && !createReadme))
            {
                if (GUILayout.Button("Create Crystal Save Test Setup", GUILayout.Height(32f)))
                {
                    CreateTestSetup(createEditModeTests, createReadme, overwriteExisting, revealFolderAfterCreate);
                }
            }
        }

        private static void CreateTestSetup(
            bool createEditModeTests,
            bool createReadme,
            bool overwriteExisting,
            bool revealFolderAfterCreate)
        {
            EnsureFolder(RootFolder);
            EnsureFolder(EditorFolder);

            List<string> createdOrUpdated = new List<string>();
            List<string> skipped = new List<string>();

            if (createEditModeTests)
                WriteTextAsset(TestScriptPath, GeneratedEditModeTests, overwriteExisting, createdOrUpdated, skipped);

            if (createReadme)
                WriteTextAsset(
                    ReadmePath,
                    GeneratedReadme.Replace("{HASH}", "#"),
                    overwriteExisting,
                    createdOrUpdated,
                    skipped);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (revealFolderAfterCreate)
                EditorUtility.RevealInFinder(Path.GetFullPath(RootFolder));

            string message = createdOrUpdated.Count > 0
                ? "Created or updated:\n" + string.Join("\n", createdOrUpdated)
                : "No files were created or updated.";

            if (skipped.Count > 0)
                message += "\n\nSkipped because overwrite is disabled:\n" + string.Join("\n", skipped);

            EditorUtility.DisplayDialog("Crystal Save Test Setup Ready", message, "OK");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new InvalidOperationException("Crystal Save test setup can only create folders below Assets.");

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private static void WriteTextAsset(
            string assetPath,
            string content,
            bool overwriteExisting,
            List<string> createdOrUpdated,
            List<string> skipped)
        {
            if (File.Exists(assetPath) && !overwriteExisting)
            {
                skipped.Add(assetPath);
                return;
            }

            string directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(assetPath, NormalizeLineEndings(content));
            createdOrUpdated.Add(assetPath);
        }

        private static string NormalizeLineEndings(string content)
        {
            return content.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        }

        private const string GeneratedEditModeTests = @"#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using MemoryPack;
using NUnit.Framework;
using UnityEngine;

namespace Arawn.CrystalSave.GeneratedTests
{
    public class CrystalSaveGeneratedEditModeTests
    {
        private string rootPath;
        private SaveSettings settings;
        private SaveSystem saveSystem;

        [SetUp]
        public void SetUp()
        {
            rootPath = Path.Combine(
                Path.GetTempPath(),
                ""CrystalSaveTests"",
                ""Generated_"" + Guid.NewGuid().ToString(""N""));

            settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.saveMethod = SaveMethod.BinaryFileFormat;
            settings.saveFileName = ""Slot_{n}"";
            settings.saveKey = ""Slot_{n}"";
            settings.slotFolderNamePattern = ""slot{n}"";
            settings.metadataFileNamePattern = ""Slot{n}_Meta.bin"";
            settings.enableCloudSave = false;
            settings.keepLocalMirror = true;

            saveSystem = new SaveSystem(settings, rootPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (settings != null)
                UnityEngine.Object.DestroyImmediate(settings);

            DeleteTestRoot(rootPath);
        }

        [Test]
        public void GeneratedISaveablePayloadRoundTripsWithoutSaveManager()
        {
            GameObject go = new GameObject(""Crystal Save Generated ISaveable Test"");
            GeneratedHealthSaveable saveable = go.AddComponent<GeneratedHealthSaveable>();

            try
            {
                saveable.health = 37;
                byte[] payload = saveable.SaveData();

                saveable.health = 1;
                saveable.LoadData(payload);

                Assert.AreEqual(37, saveable.health);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GeneratedSaveSystemUsesAnIsolatedTempRoot()
        {
            string fullRoot = Path.GetFullPath(rootPath);
            string persistentRoot = Path.GetFullPath(Application.persistentDataPath);

            Assert.IsTrue(
                fullRoot.IndexOf(""CrystalSaveTests"", StringComparison.Ordinal) >= 0,
                ""Generated tests should use the CrystalSaveTests temporary root."");
            Assert.IsFalse(
                fullRoot.StartsWith(persistentRoot, StringComparison.OrdinalIgnoreCase),
                ""Generated tests must not write into the project's real persistentDataPath."");
        }

        [Test]
        public void GeneratedBinarySaveSystemRoundTripsBytesInsideIsolatedRoot()
        {
            SaveSlot slot = CreateSlot(1);
            byte[] expected = { 1, 2, 3, 5, 8, 13 };

            saveSystem.Save(expected, slot);
            byte[] actual = saveSystem.Load(slot);

            CollectionAssert.AreEqual(expected, actual);
            Assert.IsTrue(File.Exists(Path.Combine(rootPath, ""slot1"", ""Slot_1.sav"")));
            Assert.IsTrue(File.Exists(Path.Combine(rootPath, ""slot1"", ""Slot1_Meta.bin"")));
        }

        [Test]
        public async Task GeneratedBinarySaveSystemRoundTripsSlotMetadataInsideIsolatedRoot()
        {
            SaveSlot slot = CreateSlot(2);
            slot.CustomMetadata[""profile""] = ""qa"";
            slot.CustomMetadata[""branch""] = ""generated-tests"";

            await saveSystem.SaveSlotMetadataAsync(slot);
            SaveSlot loaded = await saveSystem.LoadSlotMetadataAsync(slot.SlotNumber);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(slot.SlotNumber, loaded.SlotNumber);
            Assert.AreEqual(slot.SlotName, loaded.SlotName);
            Assert.AreEqual(slot.LastActiveScene, loaded.LastActiveScene);
            Assert.AreEqual(""qa"", loaded.CustomMetadata[""profile""]);
            Assert.AreEqual(""generated-tests"", loaded.CustomMetadata[""branch""]);
            Assert.IsTrue(File.Exists(Path.Combine(rootPath, ""slot2"", ""Slot2_Meta.bin"")));
        }

        [Test]
        public void GeneratedBinarySaveSystemDeleteRemovesSavedBytesAndMetadata()
        {
            SaveSlot slot = CreateSlot(3);
            byte[] payload = { 21, 34, 55 };

            saveSystem.Save(payload, slot);
            Assert.IsNotNull(saveSystem.Load(slot));

            saveSystem.Delete(slot);

            Assert.IsNull(saveSystem.Load(slot));
            Assert.IsFalse(File.Exists(Path.Combine(rootPath, ""slot3"", ""Slot_3.sav"")));
            Assert.IsFalse(File.Exists(Path.Combine(rootPath, ""slot3"", ""Slot3_Meta.bin"")));
        }

        [Test]
        public void GeneratedBinarySaveSystemRemovesStaleMetadataNamedSaveVariants()
        {
            settings.saveFileName = ""Slot_{n}_{meta:profile}"";

            SaveSlot oldSlot = CreateSlot(4);
            oldSlot.CustomMetadata[""profile""] = ""old"";
            SaveSlot newSlot = CreateSlot(4);
            newSlot.CustomMetadata[""profile""] = ""new"";

            saveSystem.Save(new byte[] { 1 }, oldSlot);
            string oldPath = Path.Combine(rootPath, ""slot4"", ""Slot_4_old.sav"");
            Assert.IsTrue(File.Exists(oldPath));

            saveSystem.Save(new byte[] { 2 }, newSlot);
            string newPath = Path.Combine(rootPath, ""slot4"", ""Slot_4_new.sav"");

            Assert.IsFalse(oldPath == newPath);
            Assert.IsFalse(File.Exists(oldPath), ""Old metadata-named save variant should be removed."");
            Assert.IsTrue(File.Exists(newPath), ""New metadata-named save variant should exist."");
            CollectionAssert.AreEqual(new byte[] { 2 }, saveSystem.Load(newSlot));
        }

        private static SaveSlot CreateSlot(int slotNumber)
        {
            return new SaveSlot(
                slotNumber,
                ""Generated Test Slot "" + slotNumber,
                new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc),
                string.Empty,
                ""CrystalSaveGeneratedTestScene"");
        }

        private static void DeleteTestRoot(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            string fullPath = Path.GetFullPath(path);
            if (fullPath.IndexOf(""CrystalSaveTests"", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(""Refusing to delete a non-test Crystal Save path: "" + fullPath);

            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }

        private sealed class GeneratedHealthSaveable : MonoBehaviour, ISaveable
        {
            public int health = 100;

            public string UniqueIdentifier => ""CrystalSaveGeneratedTests_Health"";

            public byte[] SaveData()
            {
                return SaveDataSerializer.Instance.Serialize(new CrystalSaveGeneratedHealthData
                {
                    health = health
                });
            }

            public void LoadData(byte[] data)
            {
                CrystalSaveGeneratedHealthData restored = SaveDataSerializer.Instance.Deserialize<CrystalSaveGeneratedHealthData>(data);
                health = restored.health;
            }
        }
    }

    [MemoryPackable]
    public partial class CrystalSaveGeneratedHealthData
    {
        public int health;
    }
}
" + "#endif" + @"
";

        private const string GeneratedReadme = @"{HASH} Crystal Save Generated Test Setup

This folder was created by:

`Tools > Crystal Save > Runtime Debug > Diagnostics > Crystal Save Test Setup`

The generated tests are intentionally disposable. Delete this folder whenever you no longer need it, or recreate it from the menu after updating Crystal Save.

***

{HASH}{HASH}{HASH} What Was Created

| File | Purpose |
| --- | --- |
| `Editor/CrystalSaveGeneratedEditModeTests.cs` | Unity Test Runner EditMode tests for safe Crystal Save test seams. |
| `README.md` | Notes for running and extending the generated tests. |

***

{HASH}{HASH}{HASH} How To Run

1. Open `Window > General > Test Runner`.
2. Select `EditMode`.
3. Run the tests under `Arawn.CrystalSave.GeneratedTests`.

The tests write to a temporary folder below `Path.GetTempPath()/CrystalSaveTests`. They do not write into the project's real Crystal Save save folder.

***

{HASH}{HASH}{HASH} What These Tests Cover

* A small `ISaveable` component can serialize and deserialize its own payload without `SaveManager`.
* `SaveSystem` can round-trip binary save bytes in an isolated root folder.
* Slot metadata can be saved and loaded in an isolated root folder.
* Delete removes save bytes and metadata.
* Metadata-driven save filenames remove stale variants when metadata changes.
* The generated test root is not `Application.persistentDataPath`.

***

{HASH}{HASH}{HASH} What These Tests Do Not Cover

Full `SaveManager` save/load behavior depends on Play Mode, Unity lifecycle, scene objects, scene loads, coroutine timing, prefabs, destroyed-object tracking, `KeepAcrossScenes`, and scene-baked reuse.

For that class of problem, use PlayMode tests or the runtime diagnostics under:

`Tools > Crystal Save > Runtime Debug > Diagnostics`

This setup intentionally does not create a test asmdef. Many Unity projects keep runtime scripts in Unity's predefined `Assembly-CSharp`, and a generated asmdef can accidentally make tests unable to see runtime code.

***

{HASH}{HASH}{HASH} Extending The Tests

Good EditMode additions:

* Serializer round-trips.
* Save data migration utilities.
* `ISaveable.SaveData` and `ISaveable.LoadData` payload checks.
* `SaveSystem` file behavior with a temporary root.
* Storage path provider behavior.

Good PlayMode additions:

* `SaveManager.SaveAsync`.
* `SaveManager.LoadSaveSlotAsync`.
* Scene transitions through an `ISceneLoadOrchestrator`.
* Runtime-spawned prefabs.
* Scene-baked prefab reuse.
* Destroyed objects.
* `KeepAcrossScenes`.

Always delete test saves before and after tests. Stale `UniqueID`, destroyed-object, prefab, and scene data can make a real test look broken when the old save is the actual problem.
";
    }
}
#endif
