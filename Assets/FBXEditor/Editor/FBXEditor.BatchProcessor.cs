// FBXEditor.BatchProcessor.cs
// This partial class contains all logic for the "Batch" tab.
// Provides batch processing of multiple FBX files with configurable operations.

using UnityEngine;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace FBXEditor.Editor
{
    public partial class FBXEditor : EditorWindow
    {
        // --- Batch Processor State ---
        private Object batchSourceFolder;
        private bool batchSearchSubfolders = true;
        private List<BatchFBXEntry> batchEntries = new List<BatchFBXEntry>();
        private Vector2 batchScrollPos;
        private Vector2 batchFileListScrollPos;
        private Vector2 batchLogScrollPos;
        private string batchFileFilter = "";
        private bool batchSelectAll = true;
        private bool batchBackupEnabled;
        private bool batchSaveAsCopy = true;
        private Object batchOutputFolder;

        // --- Operation Configs ---
        private bool opDeleteChildren; // t
        private string deletePattern = "_LOD1";
        private int deletePatternMode; // 0=Equals, 1=Contains, 2=EndsWith, 3=Regex
        private bool deleteDisabledChildren;
        private static readonly string[] PatternModeNames = { "Equals", "Contains", "Ends With", "Regex" };

        private bool opExtractChildren;
        private Object extractOutputFolder;
        private bool extractUseCustomNaming;
        private string extractNamingPattern = "{parent}_{child}";
        private string extractChildFilter = "";

        private bool opBakeTransforms;
        private int batchBakeMode; // 0=Static, 1=Armature
        private static readonly string[] BakeModeNames = { "Static Mesh", "Armature" };
        private float batchBakeScale = 1.0f;
        private Vector3 batchBakeRotation = Vector3.zero;
        private Vector3 batchArmatureRotation = Vector3.zero;

        private bool opRecalculateNormals;

        private bool opCombineMeshes;
        private bool combineIgnoreDisabled = true;

        private bool opAutoLinkTextures;
        private Object batchTextureSearchFolder;

        private bool opExtractMaterials;
        private string extractMaterialsSubfolder = "Materials";

        private bool opSetPivot;
        private int batchPivotPreset; // 0=Center, 1=Bottom Center
        private static readonly string[] PivotPresetNames = { "Center", "Bottom Center" };

        // --- Log ---
        private List<string> batchLog = new List<string>();

        // --- Data Model ---
        private class BatchFBXEntry
        {
            public string assetPath;
            public string displayName;
            public bool isSelected = true;
            public int childCount;
            public int meshCount;
            public List<string> childNames = new List<string>();
            public Dictionary<string, Material> snapshotMaterials;
        }

        // =====================================================================
        // UI
        // =====================================================================

        private void DrawBatchProcessorTab()
        {
            batchScrollPos = EditorGUILayout.BeginScrollView(batchScrollPos);

            DrawBatchSourceSection();
            DrawBatchFileList();
            DrawBatchOperations();
            DrawBatchExecutionSection();
            DrawBatchLog();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBatchSourceSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Source", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            batchSourceFolder = EditorGUILayout.ObjectField("Folder", batchSourceFolder, typeof(DefaultAsset), false);
            batchSearchSubfolders = EditorGUILayout.ToggleLeft("Include Subfolders", batchSearchSubfolders, GUILayout.Width(130));
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Scan for Files"))
            {
                ScanForFBXFiles();
            }

            if (batchEntries.Count > 0)
            {
                EditorGUILayout.LabelField($"Found {batchEntries.Count} file(s)");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBatchFileList()
        {
            if (batchEntries.Count == 0) return;

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Files", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            batchSelectAll = EditorGUILayout.ToggleLeft("Select All", batchSelectAll, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var entry in batchEntries) entry.isSelected = batchSelectAll;
            }

            batchFileFilter = EditorGUILayout.TextField(batchFileFilter, EditorStyles.toolbarSearchField);
            EditorGUILayout.EndHorizontal();

            int selectedCount = batchEntries.Count(e => e.isSelected);
            EditorGUILayout.LabelField($"{selectedCount} / {batchEntries.Count} selected", EditorStyles.miniLabel);

            batchFileListScrollPos = EditorGUILayout.BeginScrollView(batchFileListScrollPos, GUILayout.MaxHeight(200));

            foreach (var entry in batchEntries)
            {
                if (!string.IsNullOrEmpty(batchFileFilter) &&
                    !entry.displayName.ToLower().Contains(batchFileFilter.ToLower()))
                    continue;

                EditorGUILayout.BeginHorizontal();
                entry.isSelected = EditorGUILayout.Toggle(entry.isSelected, GUILayout.Width(20));
                EditorGUILayout.LabelField(entry.displayName, GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField($"{entry.childCount} children, {entry.meshCount} meshes",
                    EditorStyles.miniLabel, GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBatchOperations()
        {
            if (batchEntries.Count == 0) return;

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Operations", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Enable operations below. They execute top-to-bottom on each file.", MessageType.Info);

            // --- Delete Children by Pattern ---
            DrawOperationHeader(ref opDeleteChildren, "Delete Children by Pattern");
            if (opDeleteChildren)
            {
                EditorGUI.indentLevel++;
                deletePatternMode = EditorGUILayout.Popup("Match Mode", deletePatternMode, PatternModeNames);
                deletePattern = EditorGUILayout.TextField("Pattern", deletePattern);
                deleteDisabledChildren = EditorGUILayout.ToggleLeft(
                    new GUIContent("Delete Disabled Children", "Also deletes any children that are disabled (inactive) in the hierarchy, regardless of pattern."),
                    deleteDisabledChildren);
                if (deletePatternMode != 3) // Not Regex
                {
                    EditorGUILayout.HelpBox(
                        "Enter one or more names separated by | (pipe).\n" +
                        "Example: _LOD1|_LOD2|_collision\n" +
                        "Disabled: Deletes any inactive GameObjects in the hierarchy.", MessageType.None);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }

            // --- Extract Children as FBX ---
            DrawOperationHeader(ref opExtractChildren, "Extract Children as FBX");
            if (opExtractChildren)
            {
                EditorGUI.indentLevel++;
                extractOutputFolder = EditorGUILayout.ObjectField("Output Folder", extractOutputFolder, typeof(DefaultAsset), false);
                if (extractOutputFolder == null)
                {
                    EditorGUILayout.HelpBox("If empty, children are saved next to the source FBX.", MessageType.None);
                }
                extractChildFilter = EditorGUILayout.TextField(new GUIContent("Child Name Filter", "Only extract children whose name contains the filter text. Supports multiple patterns separated by | (e.g. arm|leg). Leave empty to extract all children."), extractChildFilter);
                extractUseCustomNaming = EditorGUILayout.ToggleLeft(new GUIContent("Custom Naming Pattern", "Use a custom pattern to name extracted FBX files instead of the child's original name.\n\nTokens: {parent} = FBX name, {child} = child name, {index} = child index\nExample: {parent}_{child} → MyModel_Arm.fbx"), extractUseCustomNaming);
                if (extractUseCustomNaming)
                {
                    EditorGUI.indentLevel++;
                    extractNamingPattern = EditorGUILayout.TextField("Pattern", extractNamingPattern);
                    EditorGUILayout.HelpBox("Tokens: {parent} = FBX name, {child} = child name, {index} = child index", MessageType.None);
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }

            // --- Bake Transforms ---
            DrawOperationHeader(ref opBakeTransforms, "Bake Transforms");
            if (opBakeTransforms)
            {
                EditorGUI.indentLevel++;
                batchBakeMode = GUILayout.Toolbar(batchBakeMode, BakeModeNames);
                EditorGUILayout.Space(3);

                if (batchBakeMode == 0) // Static
                {
                    EditorGUILayout.HelpBox("Bakes scale and rotation into mesh vertices for static (non-skinned) meshes.", MessageType.Info);
                    batchBakeScale = EditorGUILayout.FloatField("Bake Scale", batchBakeScale);
                    batchBakeRotation = EditorGUILayout.Vector3Field("Bake Rotation", batchBakeRotation);
                }
                else // Armature
                {
                    EditorGUILayout.HelpBox("Bakes a correction rotation into rigged characters (fixes Blender -90x imports etc.).", MessageType.Info);
                    batchArmatureRotation = EditorGUILayout.Vector3Field("Correction Rotation", batchArmatureRotation);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }

            // --- Recalculate Normals ---
            DrawOperationHeader(ref opRecalculateNormals, "Recalculate Normals");
            if (opRecalculateNormals)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Recalculates normals for all meshes in the model.", MessageType.Info);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }

            // --- Combine All Meshes ---
            DrawOperationHeader(ref opCombineMeshes, "Combine All Meshes");
            if (opCombineMeshes)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Merges all child meshes into a single mesh. Handles multi-material via sub-meshes. Supports both static and skinned meshes.", MessageType.Info);
                combineIgnoreDisabled = EditorGUILayout.ToggleLeft("Ignore Disabled Objects", combineIgnoreDisabled);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }

            // --- Auto-link Textures ---
            DrawOperationHeader(ref opAutoLinkTextures, "Auto-link Textures");
            if (opAutoLinkTextures)
            {
                EditorGUI.indentLevel++;
                batchTextureSearchFolder = EditorGUILayout.ObjectField("Search Folder (Optional)", batchTextureSearchFolder, typeof(DefaultAsset), false);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }

            // --- Extract Embedded Materials ---
            DrawOperationHeader(ref opExtractMaterials, "Extract Embedded Materials");
            if (opExtractMaterials)
            {
                EditorGUI.indentLevel++;
                extractMaterialsSubfolder = EditorGUILayout.TextField("Subfolder Name", extractMaterialsSubfolder);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }

            // --- Set Pivot Preset ---
            DrawOperationHeader(ref opSetPivot, "Set Pivot (Preset)");
            if (opSetPivot)
            {
                EditorGUI.indentLevel++;
                batchPivotPreset = EditorGUILayout.Popup("Pivot Position", batchPivotPreset, PivotPresetNames);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(3);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawOperationHeader(ref bool enabled, string label)
        {
            EditorGUILayout.BeginHorizontal();
            enabled = EditorGUILayout.ToggleLeft(label, enabled, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBatchExecutionSection()
        {
            if (batchEntries.Count == 0) return;

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Save Options", EditorStyles.boldLabel);

            batchSaveAsCopy = EditorGUILayout.ToggleLeft("Save as copy (instead of overwriting originals)", batchSaveAsCopy);
            if (!batchSaveAsCopy)
            {
                batchBackupEnabled = EditorGUILayout.ToggleLeft("Backup originals before overwriting", batchBackupEnabled);
            }
            extractAndRestoreMaterials = EditorGUILayout.ToggleLeft(
                new GUIContent("Apply Original Materials on Save", "Extracts embedded materials after FBX export and restores original material settings (shader, colors, textures, keywords) onto them."),
                extractAndRestoreMaterials);

            if (batchSaveAsCopy)
            {
                EditorGUI.indentLevel++;
                batchOutputFolder = EditorGUILayout.ObjectField("Output Folder", batchOutputFolder, typeof(DefaultAsset), false);
                if (batchOutputFolder == null)
                {
                    EditorGUILayout.HelpBox("Required: Select a folder to save copies to.", MessageType.Warning);
                }
                EditorGUI.indentLevel--;
            }

            int selectedCount = batchEntries.Count(e => e.isSelected);
            bool hasOperations = opDeleteChildren || opExtractChildren || opBakeTransforms ||
                                 opRecalculateNormals || opCombineMeshes || opAutoLinkTextures ||
                                 opExtractMaterials || opSetPivot || extractAndRestoreMaterials;
            bool canExecute = selectedCount > 0 && hasOperations && !isProcessingAsync &&
                              (!batchSaveAsCopy || batchOutputFolder != null);

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(!canExecute))
            {
                string buttonLabel = batchSaveAsCopy
                    ? $"Save Copy for {selectedCount} File(s)"
                    : $"Overwrite {selectedCount} File(s)";

                GUI.backgroundColor = batchSaveAsCopy ? new Color(0.7f, 0.85f, 1f) : new Color(1f, 0.8f, 0.6f);
                if (GUILayout.Button(buttonLabel, GUILayout.Height(30)))
                {
                    string ops = GetEnabledOperationsSummary();
                    string saveMode = batchSaveAsCopy ? "Copies will be saved to the output folder." : "Original files will be overwritten.";
                    if (EditorUtility.DisplayDialog("Execute Batch Operations?",
                        $"You are about to process {selectedCount} file(s) with:\n\n{ops}\n\n{saveMode}\n\n" +
                        (batchBackupEnabled && !batchSaveAsCopy ? "Backups will be created.\n\n" : "") +
                        "Continue?", "Execute", "Cancel"))
                    {
                        batchLog.Clear();
                        asyncProcessor = ExecuteBatchCoroutine();
                        isProcessingAsync = true;
                        asyncMessage = "Processing batch...";
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBatchLog()
        {
            if (batchLog.Count == 0) return;

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Log", EditorStyles.boldLabel);

            batchLogScrollPos = EditorGUILayout.BeginScrollView(batchLogScrollPos, GUILayout.MaxHeight(200));
            foreach (var line in batchLog)
            {
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear Log", EditorStyles.miniButton))
            {
                batchLog.Clear();
            }
            EditorGUILayout.EndVertical();
        }

        // =====================================================================
        // Scanning
        // =====================================================================

        private void ScanForFBXFiles()
        {
            batchEntries.Clear();

            string searchPath = "Assets";
            if (batchSourceFolder != null)
            {
                string folderPath = AssetDatabase.GetAssetPath(batchSourceFolder);
                if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
                {
                    searchPath = folderPath;
                }
            }

            string[] searchPaths = new string[] { searchPath };
            string[] guids = AssetDatabase.FindAssets("t:GameObject", searchPaths);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool isFbx = path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
                bool isPrefab = path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase);
                if (!isFbx && !isPrefab) continue;

                // If not searching subfolders, ensure file is directly in the search folder
                if (!batchSearchSubfolders)
                {
                    string fileDir = Path.GetDirectoryName(path).Replace("\\", "/");
                    if (fileDir != searchPath) continue;
                }

                // Load prefab to inspect hierarchy
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var entry = new BatchFBXEntry
                {
                    assetPath = path,
                    displayName = Path.GetFileName(path),
                    isSelected = true
                };

                // Count immediate children
                for (int i = 0; i < prefab.transform.childCount; i++)
                {
                    entry.childNames.Add(prefab.transform.GetChild(i).name);
                }
                entry.childCount = entry.childNames.Count;

                // Count meshes
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                var processedMeshes = new HashSet<Mesh>();
                foreach (var r in renderers)
                {
                    Mesh mesh = GetMeshFromObject(r.gameObject);
                    if (mesh != null) processedMeshes.Add(mesh);
                }
                entry.meshCount = processedMeshes.Count;

                batchEntries.Add(entry);
            }

            batchEntries.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase));
        }

        // =====================================================================
        // Batch Execution
        // =====================================================================

        private IEnumerator<float> ExecuteBatchCoroutine()
        {
            var selected = batchEntries.Where(e => e.isSelected).ToList();
            int successCount = 0, failCount = 0;

            // Determine if we need geometry operations (require instantiation + re-export)
            bool needsGeometryOps = opDeleteChildren || opBakeTransforms || opRecalculateNormals ||
                                    opCombineMeshes || opSetPivot;
            // Extract is a special case — it exports children but doesn't modify the original
            bool needsExtract = opExtractChildren;
            // Post-reimport operations work on the asset directly
            bool needsPostOps = opAutoLinkTextures || opExtractMaterials || extractAndRestoreMaterials;

            for (int fileIndex = 0; fileIndex < selected.Count; fileIndex++)
            {
                var entry = selected[fileIndex];
                asyncMessage = $"Processing {fileIndex + 1}/{selected.Count}: {entry.displayName}";
                yield return (float)fileIndex / selected.Count;

                try
                {
                    ProcessSingleFile(entry, needsGeometryOps, needsExtract, needsPostOps);
                    successCount++;
                }
                catch (System.Exception ex)
                {
                    failCount++;
                    batchLog.Add($"ERROR [{entry.displayName}]: {ex.Message}");
                    Debug.LogError($"Batch processing failed for {entry.assetPath}: {ex}");
                }
            }

            AssetDatabase.Refresh();

            batchLog.Add("");
            batchLog.Add($"=== COMPLETE: {successCount} succeeded, {failCount} failed ===");
            asyncMessage = $"Done! {successCount} succeeded, {failCount} failed";

            EditorUtility.DisplayDialog("Batch Processing Complete",
                $"Processed {selected.Count} file(s).\n\n{successCount} succeeded\n{failCount} failed\n\nSee log for details.", "OK");

            // Re-scan to update stats
            ScanForFBXFiles();

            yield return 1f;
        }

        private void ProcessSingleFile(BatchFBXEntry entry, bool needsGeometryOps, bool needsExtract, bool needsPostOps)
        {
            batchLog.Add($"--- {entry.displayName} ---");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.assetPath);
            if (prefab == null)
            {
                batchLog.Add("  Skipped: Could not load prefab");
                return;
            }

            // --- Backup ---
            if (batchBackupEnabled && needsGeometryOps && !batchSaveAsCopy)
            {
                BackupFile(entry.assetPath);
            }

            // --- Extract Children (before geometry ops, uses fresh instantiation) ---
            if (needsExtract)
            {
                BatchExtractChildren(prefab, entry);
            }

            // --- Geometry Operations (need instantiation + re-export) ---
            if (needsGeometryOps)
            {
                ProcessGeometryOperations(prefab, entry);
            }

            // --- Post-reimport operations (work on the asset file directly) ---
            if (needsPostOps)
            {
                // Snapshot materials if not already done by geometry operations
                if (extractAndRestoreMaterials && entry.snapshotMaterials == null)
                {
                    entry.snapshotMaterials = new Dictionary<string, Material>();
                    foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            if (mat != null && !entry.snapshotMaterials.ContainsKey(mat.name))
                                entry.snapshotMaterials[mat.name] = mat;
                        }
                    }
                }

                ProcessPostReimportOperations(entry);
            }
        }

        private void ProcessGeometryOperations(GameObject prefab, BatchFBXEntry entry)
        {
            // Instantiate editable copy
            GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            copy.name = prefab.name + " (Batch Edit)";
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            try
            {
                // Snapshot original materials before any operations modify the hierarchy
                if (extractAndRestoreMaterials)
                {
                    entry.snapshotMaterials = new Dictionary<string, Material>();
                    foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            if (mat != null && !entry.snapshotMaterials.ContainsKey(mat.name))
                                entry.snapshotMaterials[mat.name] = mat;
                        }
                    }
                }

                // 1. Delete children by pattern
                if (opDeleteChildren && (!string.IsNullOrEmpty(deletePattern) || deleteDisabledChildren))
                {
                    int deleted = BatchDeleteChildrenByPattern(copy);
                    if (deleted > 0)
                        batchLog.Add($"  Deleted {deleted} child(ren) matching pattern");
                }

                // 2. Combine meshes
                if (opCombineMeshes)
                {
                    bool combined = BatchCombineAllMeshes(copy);
                    if (combined) batchLog.Add("  Combined meshes");
                }

                // 3. Set pivot
                if (opSetPivot)
                {
                    BatchSetPivotOnAllChildren(copy);
                    batchLog.Add($"  Set pivot: {PivotPresetNames[batchPivotPreset]}");
                }

                // 4. Bake transforms
                if (opBakeTransforms)
                {
                    if (batchBakeMode == 0)
                    {
                        BatchBakeStaticTransforms(copy);
                        batchLog.Add($"  Baked static transforms (scale: {batchBakeScale}, rotation: {batchBakeRotation})");
                    }
                    else
                    {
                        BatchBakeArmatureTransform(copy);
                        batchLog.Add($"  Baked armature transform (rotation: {batchArmatureRotation})");
                    }
                }

                // 5. Recalculate normals
                if (opRecalculateNormals)
                {
                    BatchRecalculateNormals(copy);
                    batchLog.Add("  Recalculated normals");
                }

                // Remove disabled GameObjects before export (FBX doesn't store active state)
                RemoveInactiveObjects(copy);

                // Determine save path
                string savePath;
                // Export under the source model's root node name and with the full rig still
                // bound, or the reimport invalidates the Avatar (missing required human bone).
                string exportRootName = GetExportRootName(entry.assetPath, prefab.name);
                if (batchSaveAsCopy)
                {
                    string outputDir = AssetDatabase.GetAssetPath(batchOutputFolder);
                    string fileName = Path.GetFileNameWithoutExtension(entry.assetPath) + ".fbx";
                    savePath = Path.Combine(outputDir, fileName);
                    // Ensure unique path
                    savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

                    // Convert to absolute for ModelExporter
                    string absolutePath = Path.Combine(
                        Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length),
                        savePath);
                    ExportWithRigPreserved(absolutePath, copy, prefab, exportRootName);
                }
                else
                {
                    // For prefabs, save as .fbx alongside the original rather than overwriting the .prefab
                    if (entry.assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    {
                        savePath = Path.ChangeExtension(entry.assetPath, ".fbx");
                        savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);
                    }
                    else
                    {
                        savePath = entry.assetPath;
                    }
                    ExportWithRigPreserved(savePath, copy, prefab, exportRootName);
                }

                // Repair Avatar bone references before anything reimports the overwritten asset.
                RemapAvatarForRenamedBones(savePath);

                BatchRestoreTextures(copy, prefab, savePath);
                entry.assetPath = savePath;
                batchLog.Add($"  Saved: {savePath}");
            }
            finally
            {
                // Always clean up the temp copy
                DestroyImmediate(copy);
            }
        }

        private void ProcessPostReimportOperations(BatchFBXEntry entry)
        {
            if (opAutoLinkTextures)
            {
                int linked = BatchAutoLinkTextures(entry.assetPath);
                if (linked > 0) batchLog.Add($"  Linked {linked} texture(s)");
            }

            if (opExtractMaterials || extractAndRestoreMaterials)
            {
                int extracted = BatchExtractMaterials(entry.assetPath);
                if (extracted > 0) batchLog.Add($"  Extracted {extracted} material(s)");

                // Restore full material settings from snapshot onto extracted .mat files
                if (extractAndRestoreMaterials && entry.snapshotMaterials != null && extracted > 0)
                {
                    int restored = BatchRestoreMaterialSettings(entry.assetPath, entry.snapshotMaterials);
                    if (restored > 0) batchLog.Add($"  Restored settings on {restored} material(s)");
                }
            }
        }

        // =====================================================================
        // Individual Batch Operations
        // =====================================================================

        private int BatchDeleteChildrenByPattern(GameObject root)
        {
            var toDelete = new List<GameObject>();
            CollectMatchingChildren(root.transform, toDelete);

            foreach (var go in toDelete)
            {
                DestroyImmediate(go);
            }

            return toDelete.Count;
        }

        private void CollectMatchingChildren(Transform parent, List<GameObject> toDelete)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                bool matchesPattern = !string.IsNullOrEmpty(deletePattern) && MatchesPattern(child.name, deletePattern, deletePatternMode);
                bool isDisabled = deleteDisabledChildren && !child.gameObject.activeSelf;

                if (matchesPattern || isDisabled)
                {
                    toDelete.Add(child.gameObject);
                }
                else
                {
                    CollectMatchingChildren(child, toDelete);
                }
            }
        }

        private void BatchExtractChildren(GameObject prefab, BatchFBXEntry entry)
        {
            // Determine output folder
            string outputPath;
            if (extractOutputFolder != null)
            {
                outputPath = AssetDatabase.GetAssetPath(extractOutputFolder);
            }
            else
            {
                outputPath = Path.GetDirectoryName(entry.assetPath);
            }

            // Instantiate to get editable children
            GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            copy.name = prefab.name;
            PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            try
            {
                string parentName = Path.GetFileNameWithoutExtension(entry.assetPath);
                int extractCount = 0;
                var exportedPaths = new List<(string assetPath, Dictionary<string, Material> snapshot)>();

                for (int i = 0; i < copy.transform.childCount; i++)
                {
                    Transform child = copy.transform.GetChild(i);

                    // Apply optional child filter
                    if (!string.IsNullOrEmpty(extractChildFilter) &&
                        !MatchesPattern(child.name, extractChildFilter, 1)) // 1 = Contains
                        continue;

                    // Skip children with no mesh
                    if (child.GetComponentInChildren<Renderer>(true) == null) continue;

                    // Snapshot materials from child before export
                    Dictionary<string, Material> childSnapshot = null;
                    if (extractAndRestoreMaterials)
                    {
                        childSnapshot = new Dictionary<string, Material>();
                        foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                        {
                            foreach (var mat in renderer.sharedMaterials)
                            {
                                if (mat != null && !childSnapshot.ContainsKey(mat.name))
                                    childSnapshot[mat.name] = mat;
                            }
                        }
                    }

                    string exportName;
                    if (extractUseCustomNaming)
                    {
                        exportName = GenerateExportName(parentName, child.name, i, extractNamingPattern);
                    }
                    else
                    {
                        exportName = child.name;
                    }

                    string exportFilePath = Path.Combine(outputPath, exportName + ".fbx");

                    // Convert to absolute path for ModelExporter
                    string absolutePath = exportFilePath;
                    if (!Path.IsPathRooted(absolutePath))
                    {
                        absolutePath = Path.Combine(
                            Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length),
                            absolutePath);
                    }

                    ModelExporter.ExportObject(absolutePath, child.gameObject);
                    exportedPaths.Add((exportFilePath, childSnapshot));
                    extractCount++;
                }

                if (extractCount > 0)
                {
                    batchLog.Add($"  Extracted {extractCount} child(ren) as FBX");
                    AssetDatabase.Refresh();

                    // Apply material settings to extracted FBX files
                    if (extractAndRestoreMaterials)
                    {
                        foreach (var (assetPath, snapshot) in exportedPaths)
                        {
                            if (snapshot == null || snapshot.Count == 0) continue;
                            int extracted = BatchExtractMaterials(assetPath);
                            if (extracted > 0)
                            {
                                int restored = BatchRestoreMaterialSettings(assetPath, snapshot);
                                if (restored > 0) batchLog.Add($"    Restored materials for {Path.GetFileName(assetPath)}");
                            }
                        }
                    }
                }
            }
            finally
            {
                DestroyImmediate(copy);
            }
        }

        /// <summary>
        /// Bakes scale and rotation into static (non-skinned) mesh vertices.
        /// Matches the "Static Mesh" bake mode from the Hierarchy tab.
        /// </summary>
        private void BatchBakeStaticTransforms(GameObject root)
        {
            if (root == null) return;
            if (batchBakeScale == 1.0f && batchBakeRotation == Vector3.zero) return;

            Matrix4x4 bakeMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(batchBakeRotation), Vector3.one * batchBakeScale);

            var allRenderers = root.GetComponentsInChildren<Renderer>(true);
            // Keyed by (source mesh, delta matrix): an offset child needs a different baked result
            // than one at the root origin, even if they share the same source mesh.
            var processedMeshes = new Dictionary<(Mesh, Matrix4x4), Mesh>();

            foreach (var renderer in allRenderers)
            {
                if (renderer is SkinnedMeshRenderer) continue; // Static only

                Mesh sourceMesh = GetMeshFromObject(renderer.gameObject);
                if (sourceMesh == null) continue;

                // Child transform relative to the root, then the root-space bake expressed in the
                // child's local space (v' = C^-1 * bake * C * v) so offset children rotate around
                // the root rather than their own pivot.
                Matrix4x4 childToRoot = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                Matrix4x4 delta = childToRoot.inverse * bakeMatrix * childToRoot;

                var key = (sourceMesh, delta);
                if (!processedMeshes.TryGetValue(key, out Mesh bakedMesh))
                {
                    bakedMesh = Instantiate(sourceMesh);
                    bakedMesh.name = sourceMesh.name;
                    processedMeshes[key] = bakedMesh;

                    Vector3[] vertices = bakedMesh.vertices;
                    Vector3[] normals = bakedMesh.normals;
                    Vector4[] tangents = bakedMesh.tangents;

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = delta.MultiplyPoint3x4(vertices[i]);
                        if (i < normals.Length)
                            normals[i] = delta.MultiplyVector(normals[i]).normalized;
                        if (i < tangents.Length)
                        {
                            Vector3 tanDir = delta.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                            tangents[i] = new Vector4(tanDir.x, tanDir.y, tanDir.z, tangents[i].w);
                        }
                    }

                    bakedMesh.vertices = vertices;
                    if (normals.Length == vertices.Length) bakedMesh.normals = normals;
                    if (tangents.Length == vertices.Length) bakedMesh.tangents = tangents;
                    bakedMesh.RecalculateBounds();
                }

                if (renderer is MeshRenderer)
                {
                    var mf = renderer.GetComponent<MeshFilter>();
                    if (mf != null) mf.sharedMesh = bakedMesh;
                }
            }

            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Bakes a correction rotation into rigged (armature) models.
        /// Matches the "Armature" bake mode from the Hierarchy tab.
        /// Handles both SkinnedMeshRenderers and static MeshRenderers, plus bone transforms.
        /// </summary>
        private void BatchBakeArmatureTransform(GameObject root)
        {
            if (root == null) return;
            if (batchArmatureRotation == Vector3.zero) return;

            Quaternion bakeRotation = Quaternion.Euler(batchArmatureRotation);
            Matrix4x4 bakeMatrix = Matrix4x4.TRS(Vector3.zero, bakeRotation, Vector3.one);
            Matrix4x4 bakeMatrixInv = bakeMatrix.inverse;

            var allRenderers = root.GetComponentsInChildren<Renderer>(true);
            var processedMeshes = new Dictionary<Mesh, Mesh>();

            foreach (var renderer in allRenderers)
            {
                if (renderer is SkinnedMeshRenderer smr)
                {
                    if (smr.sharedMesh == null) continue;

                    if (!processedMeshes.TryGetValue(smr.sharedMesh, out Mesh newMesh))
                    {
                        Mesh oldMesh = smr.sharedMesh;
                        newMesh = Instantiate(oldMesh);
                        newMesh.name = oldMesh.name;
                        processedMeshes[oldMesh] = newMesh;

                        var vertices = newMesh.vertices;
                        var normals = newMesh.normals;
                        var tangents = newMesh.tangents;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            vertices[i] = bakeMatrix.MultiplyPoint3x4(vertices[i]);
                            if (i < normals.Length) normals[i] = bakeMatrix.MultiplyVector(normals[i]).normalized;
                            if (i < tangents.Length)
                            {
                                Vector3 tanDir = bakeMatrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                                tangents[i] = new Vector4(tanDir.x, tanDir.y, tanDir.z, tangents[i].w);
                            }
                        }
                        newMesh.vertices = vertices;
                        if (normals.Length == vertices.Length) newMesh.normals = normals;
                        if (tangents.Length == vertices.Length) newMesh.tangents = tangents;

                        // Bake into bindposes
                        var bindposes = newMesh.bindposes;
                        for (int i = 0; i < bindposes.Length; i++)
                        {
                            bindposes[i] = bindposes[i] * bakeMatrixInv;
                        }
                        newMesh.bindposes = bindposes;
                        newMesh.RecalculateBounds();
                    }

                    smr.sharedMesh = newMesh;
                }
                else if (renderer is MeshRenderer mr)
                {
                    var mf = renderer.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    if (!processedMeshes.TryGetValue(mf.sharedMesh, out Mesh newMesh))
                    {
                        Mesh oldMesh = mf.sharedMesh;
                        newMesh = Instantiate(oldMesh);
                        newMesh.name = oldMesh.name;
                        processedMeshes[oldMesh] = newMesh;

                        var vertices = newMesh.vertices;
                        var normals = newMesh.normals;
                        var tangents = newMesh.tangents;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            vertices[i] = bakeMatrix.MultiplyPoint3x4(vertices[i]);
                            if (i < normals.Length) normals[i] = bakeMatrix.MultiplyVector(normals[i]).normalized;
                            if (i < tangents.Length)
                            {
                                Vector3 tanDir = bakeMatrix.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                                tangents[i] = new Vector4(tanDir.x, tanDir.y, tanDir.z, tangents[i].w);
                            }
                        }
                        newMesh.vertices = vertices;
                        if (normals.Length == vertices.Length) newMesh.normals = normals;
                        if (tangents.Length == vertices.Length) newMesh.tangents = tangents;
                        newMesh.RecalculateBounds();
                    }

                    mf.sharedMesh = newMesh;
                }
            }

            // Bake transform into bone transforms
            var allSmrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var skeletonRootsToBake = new HashSet<Transform>();
            foreach (var smr in allSmrs)
            {
                if (smr.rootBone != null && smr.rootBone.IsChildOf(root.transform))
                {
                    // Find the root-level child ancestor
                    Transform current = smr.rootBone;
                    while (current.parent != null && current.parent != root.transform)
                    {
                        current = current.parent;
                    }
                    if (current.parent == root.transform)
                    {
                        skeletonRootsToBake.Add(current);
                    }
                }
            }

            foreach (var skeletonRoot in skeletonRootsToBake)
            {
                skeletonRoot.localPosition = bakeRotation * skeletonRoot.localPosition;
                skeletonRoot.localRotation = bakeRotation * skeletonRoot.localRotation;
            }

            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
        }

        private void BatchRecalculateNormals(GameObject root)
        {
            if (root == null) return;
            var processedMeshes = new Dictionary<Mesh, Mesh>();

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh sourceMesh = GetMeshFromObject(renderer.gameObject);
                if (sourceMesh == null) continue;

                if (!processedMeshes.TryGetValue(sourceMesh, out Mesh newMesh))
                {
                    newMesh = Instantiate(sourceMesh);
                    newMesh.name = sourceMesh.name;
                    newMesh.RecalculateNormals();
                    processedMeshes[sourceMesh] = newMesh;
                }

                if (renderer is MeshRenderer)
                {
                    var mf = renderer.GetComponent<MeshFilter>();
                    if (mf != null) mf.sharedMesh = newMesh;
                }
                else if (renderer is SkinnedMeshRenderer smr)
                {
                    smr.sharedMesh = newMesh;
                }
            }
        }

        private bool BatchCombineAllMeshes(GameObject root)
        {
            // Gather all renderers that have meshes
            bool includeInactive = !combineIgnoreDisabled;
            var allRenderers = root.GetComponentsInChildren<Renderer>(includeInactive);
            var meshObjects = new List<GameObject>();

            foreach (var r in allRenderers)
            {
                if (GetMeshFromObject(r.gameObject) != null)
                    meshObjects.Add(r.gameObject);
            }

            if (meshObjects.Count < 2) return false;

            // Split into skinned and static groups
            var skinnedObjects = meshObjects.Where(go => go.GetComponent<SkinnedMeshRenderer>() != null).ToList();
            var staticObjects = meshObjects.Where(go => go.GetComponent<SkinnedMeshRenderer>() == null).ToList();

            // Temporarily set activeEditingCopy so the combine methods can parent correctly
            var previousEditingCopy = activeEditingCopy;
            activeEditingCopy = root;
            bool anyCombined = false;

            try
            {
                // Combine skinned meshes if there are 2+
                if (skinnedObjects.Count >= 2)
                {
                    var skinnedMats = new List<Material>();
                    foreach (var go in skinnedObjects)
                    {
                        var renderer = go.GetComponent<Renderer>();
                        if (renderer != null) skinnedMats.AddRange(renderer.sharedMaterials);
                    }

                    CombineSkinnedMeshes(skinnedObjects, skinnedMats.Distinct().ToList(), out var skinnedCombined, out _);
                    if (skinnedCombined != null)
                    {
                        if (disableOriginalsAfterCombine)
                            foreach (var go in skinnedObjects) DestroyImmediate(go);
                        anyCombined = true;
                    }
                }

                // Combine static meshes if there are 2+
                if (staticObjects.Count >= 2)
                {
                    var staticMats = new List<Material>();
                    foreach (var go in staticObjects)
                    {
                        var renderer = go.GetComponent<Renderer>();
                        if (renderer != null) staticMats.AddRange(renderer.sharedMaterials);
                    }

                    CombineStaticMeshes(staticObjects, staticMats.Distinct().ToList(), out var staticCombined, out var staticMesh);
                    if (staticCombined != null && staticMesh != null)
                    {
                        if (centerPivotAfterCombine)
                        {
                            Vector3 center = staticMesh.bounds.center;
                            var verts = staticMesh.vertices;
                            for (int i = 0; i < verts.Length; i++) verts[i] -= center;
                            staticMesh.vertices = verts;
                            staticMesh.RecalculateBounds();
                            staticCombined.transform.position = root.transform.TransformPoint(center);
                        }

                        if (disableOriginalsAfterCombine)
                            foreach (var go in staticObjects) DestroyImmediate(go);
                        anyCombined = true;
                    }
                }
            }
            finally
            {
                activeEditingCopy = previousEditingCopy;
            }

            return anyCombined;
        }

        /// <summary>
        /// Sets the WHOLE MODEL's pivot to a preset (center / bottom-center) in a way that
        /// survives FBX export. ModelExporter bakes the export root's transform to identity, so
        /// moving the root transform does nothing on the imported asset. Instead we rigidly shift
        /// all geometry so the chosen point lands on the root origin, which becomes the FBX pivot.
        /// </summary>
        private void BatchSetPivotOnAllChildren(GameObject root)
        {
            // Use renderers that will actually be exported (inactive ones are stripped before save)
            // to compute the model bounds; fall back to all if none are active.
            var allRenderers = root.GetComponentsInChildren<Renderer>(true);
            var boundsRenderers = allRenderers.Where(r => r.gameObject.activeInHierarchy).ToList();
            if (boundsRenderers.Count == 0) boundsRenderers = allRenderers.ToList();
            if (boundsRenderers.Count == 0) return;

            // Combined world-space bounds of the whole model.
            Bounds combined = boundsRenderers[0].bounds;
            for (int i = 1; i < boundsRenderers.Count; i++)
                combined.Encapsulate(boundsRenderers[i].bounds);

            Vector3 pivotWorldPos = batchPivotPreset == 0
                ? combined.center                                          // Center
                : combined.center - new Vector3(0, combined.extents.y, 0); // Bottom Center

            // The pivot point expressed in the root's local space. We shift everything by
            // -pivotLocal so this point sits at the root origin (the origin is what becomes the
            // model pivot after ModelExporter bakes the root transform on export).
            Vector3 pivotLocal = root.transform.InverseTransformPoint(pivotWorldPos);
            if (pivotLocal.sqrMagnitude < 1e-8f) return;

            // Shift every direct child (carrying its whole subtree) so the model rigidly translates.
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                child.localPosition -= pivotLocal;
            }

            // If the root object itself carries a mesh, its vertices live in root-local space, so
            // shift them directly (the root transform can't be moved — it gets baked on export).
            Mesh rootMesh = GetMeshFromObject(root);
            if (rootMesh != null)
            {
                Mesh newMesh = Instantiate(rootMesh);
                newMesh.name = rootMesh.name;
                var verts = newMesh.vertices;
                for (int i = 0; i < verts.Length; i++) verts[i] -= pivotLocal;
                newMesh.vertices = verts;
                newMesh.RecalculateBounds();

                if (root.TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = newMesh;
                else if (root.TryGetComponent<SkinnedMeshRenderer>(out var smr)) smr.sharedMesh = newMesh;
            }
        }

        private int BatchAutoLinkTextures(string assetPath)
        {
            // Load the asset and find its materials
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null) return 0;

            var allRenderers = asset.GetComponentsInChildren<Renderer>(true);
            var uniqueMaterials = allRenderers.SelectMany(r => r.sharedMaterials).Distinct().Where(m => m != null).ToList();

            string[] searchPaths = null;
            if (batchTextureSearchFolder != null)
            {
                searchPaths = new string[] { AssetDatabase.GetAssetPath(batchTextureSearchFolder) };
            }

            string[] allTextureGuids = AssetDatabase.FindAssets("t:Texture", searchPaths);
            var allTextures = allTextureGuids
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .ToDictionary(p => p, p => Tokenize(Path.GetFileNameWithoutExtension(p)));

            int linkCount = 0;

            foreach (var mat in uniqueMaterials)
            {
                var materialTokens = Tokenize(mat.name);
                var singularMaterialTokens = materialTokens.Select(t => t.EndsWith("s") ? t.Substring(0, t.Length - 1) : t).ToList();

                Shader shader = mat.shader;
                for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv) continue;

                    string propertyName = ShaderUtil.GetPropertyName(shader, i);
                    if (mat.GetTexture(propertyName) != null) continue;
                    if (!textureSuffixMap.ContainsKey(propertyName)) continue;

                    string bestMatch = FindBestTextureMatch(materialTokens, singularMaterialTokens, textureSuffixMap[propertyName], allTextures);
                    if (!string.IsNullOrEmpty(bestMatch))
                    {
                        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(bestMatch);
                        mat.SetTexture(propertyName, texture);
                        linkCount++;
                    }
                }
            }

            if (linkCount > 0) AssetDatabase.SaveAssets();
            return linkCount;
        }

        private int BatchExtractMaterials(string assetPath)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var embeddedMaterials = allAssets.OfType<Material>().Where(m => AssetDatabase.IsSubAsset(m)).ToList();

            if (embeddedMaterials.Count == 0) return 0;

            string sourceDir = Path.GetDirectoryName(assetPath);
            string modelName = Path.GetFileNameWithoutExtension(assetPath);
            string matFolderName = modelName + "_" + extractMaterialsSubfolder;
            string matFolder = Path.Combine(sourceDir, matFolderName);
            if (!AssetDatabase.IsValidFolder(matFolder))
            {
                AssetDatabase.CreateFolder(sourceDir, matFolderName);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return 0;

            int count = 0;
            foreach (var mat in embeddedMaterials)
            {
                string matPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(matFolder, mat.name + ".mat"));
                Material newMat = new Material(mat);
                AssetDatabase.CreateAsset(newMat, matPath);

                // Remap the model importer to use the external material
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mat.name), newMat);
                count++;
            }

            if (count > 0)
            {
                importer.SaveAndReimport();
            }

            return count;
        }

        private int BatchRestoreMaterialSettings(string assetPath, Dictionary<string, Material> snapshotMaterials)
        {
            // Use the model importer's remap to find extracted materials by name
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return 0;

            var externalMap = importer.GetExternalObjectMap();
            int restoredCount = 0;
            var unmatchedMaterials = new List<string>();

            foreach (var mapEntry in externalMap)
            {
                if (mapEntry.Value is Material extractedMat)
                {
                    string matName = mapEntry.Key.name;
                    if (snapshotMaterials.TryGetValue(matName, out Material originalMat))
                    {
                        CopyMaterialWithoutEmission(originalMat, extractedMat);
                        restoredCount++;
                    }
                    else
                    {
                        unmatchedMaterials.Add(matName);
                    }
                }
            }

            if (restoredCount > 0)
                AssetDatabase.SaveAssets();

            if (unmatchedMaterials.Count > 0)
                batchLog.Add($"  Warning: {unmatchedMaterials.Count} material(s) did not match originals: {string.Join(", ", unmatchedMaterials)}");

            return restoredCount;
        }

        // =====================================================================
        // Texture Restoration for Batch
        // =====================================================================

        /// <summary>
        /// Adapted version of RestoreMaterialTexturesAfterExport for batch processing.
        /// Takes explicit parameters instead of relying on instance fields.
        /// </summary>
        private void BatchRestoreTextures(GameObject editingCopy, GameObject sourcePrefab, string savedAssetPath)
        {
            // 1. Build texture cache from editing copy
            var texturesByMaterialName = new Dictionary<string, Dictionary<string, Texture>>();
            var allRenderers = editingCopy.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in allRenderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null || texturesByMaterialName.ContainsKey(mat.name)) continue;
                    var textures = new Dictionary<string, Texture>();
                    var shader = mat.shader;
                    for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                    {
                        if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                        {
                            string propName = ShaderUtil.GetPropertyName(shader, i);
                            Texture tex = mat.GetTexture(propName);
                            if (tex != null) textures[propName] = tex;
                        }
                    }
                    if (textures.Count > 0) texturesByMaterialName[mat.name] = textures;
                }
            }

            AssetDatabase.Refresh();

            // 2. Copy material remapping from source
            string sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
            var sourceImporter = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
            var savedImporter = AssetImporter.GetAtPath(savedAssetPath) as ModelImporter;
            if (sourceImporter != null && savedImporter != null)
            {
                var sourceMap = sourceImporter.GetExternalObjectMap();
                bool hasRemaps = false;
                foreach (var entry in sourceMap)
                {
                    if (entry.Value is Material)
                    {
                        savedImporter.AddRemap(entry.Key, entry.Value);
                        hasRemaps = true;
                    }
                }
                if (hasRemaps) savedImporter.SaveAndReimport();
            }

            // 3. Restore textures on embedded materials
            var savedAssets = AssetDatabase.LoadAllAssetsAtPath(savedAssetPath);
            bool anyRestored = false;
            foreach (var asset in savedAssets)
            {
                if (asset is Material savedMat && texturesByMaterialName.TryGetValue(savedMat.name, out var textures))
                {
                    foreach (var kvp in textures)
                    {
                        if (savedMat.HasProperty(kvp.Key))
                        {
                            savedMat.SetTexture(kvp.Key, kvp.Value);
                            anyRestored = true;
                        }
                    }
                }
            }
            if (anyRestored) AssetDatabase.SaveAssets();
        }

        // =====================================================================
        // Utility Methods
        // =====================================================================

        /// <summary>
        /// Removes all inactive GameObjects from the hierarchy.
        /// FBX format doesn't support disabled state, so we strip them before export.
        /// </summary>
        private void RemoveInactiveObjects(GameObject root)
        {
            // Collect bottom-up to avoid parent deletion issues
            var toDelete = new List<GameObject>();
            CollectInactiveObjects(root.transform, toDelete);

            foreach (var go in toDelete)
            {
                DestroyImmediate(go);
            }
        }

        private void CollectInactiveObjects(Transform parent, List<GameObject> toDelete)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    toDelete.Add(child.gameObject);
                }
                else
                {
                    // Only recurse into active objects (if parent is deleted, children go with it)
                    CollectInactiveObjects(child, toDelete);
                }
            }
        }

        private bool MatchesPattern(string name, string pattern, int mode)
        {
            if (string.IsNullOrEmpty(pattern)) return false;

            if (mode == 3) // Regex
            {
                try { return Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase); }
                catch { return false; }
            }

            // Split pipe-separated patterns
            string[] patterns = pattern.Split('|');
            foreach (string p in patterns)
            {
                string trimmed = p.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (mode == 0) // Equals
                {
                    if (string.Equals(name, trimmed, System.StringComparison.OrdinalIgnoreCase)) return true;
                }
                else if (mode == 1) // Contains
                {
                    if (name.IndexOf(trimmed, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
                else if (mode == 2) // EndsWith
                {
                    if (name.EndsWith(trimmed, System.StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            return false;
        }

        private string GenerateExportName(string parentName, string childName, int index, string pattern)
        {
            string result = pattern;
            result = result.Replace("{parent}", parentName);
            result = result.Replace("{child}", childName);
            result = result.Replace("{index}", index.ToString());
            return result;
        }

        private string GetEnabledOperationsSummary()
        {
            var ops = new List<string>();
            if (opDeleteChildren) ops.Add($"- Delete children matching \"{deletePattern}\" ({PatternModeNames[deletePatternMode]})");
            if (opExtractChildren) ops.Add("- Extract children as separate FBX files");
            if (opBakeTransforms) ops.Add($"- Bake transforms ({BakeModeNames[batchBakeMode]})");
            if (opRecalculateNormals) ops.Add("- Recalculate normals");
            if (opCombineMeshes) ops.Add("- Combine all meshes");
            if (opAutoLinkTextures) ops.Add("- Auto-link textures");
            if (opExtractMaterials) ops.Add("- Extract embedded materials");
            if (extractAndRestoreMaterials) ops.Add("- Apply original materials on save");
            if (opSetPivot) ops.Add($"- Set pivot: {PivotPresetNames[batchPivotPreset]}");
            return string.Join("\n", ops);
        }

        private void BackupFile(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath);
            string backupDir = Path.Combine(dir, "_backup");
            if (!AssetDatabase.IsValidFolder(backupDir))
            {
                AssetDatabase.CreateFolder(dir, "_backup");
            }

            string fileName = Path.GetFileName(assetPath);
            string backupPath = Path.Combine(backupDir, fileName);

            // Convert to absolute paths for File.Copy
            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            string absSource = Path.Combine(projectRoot, assetPath);
            string absDest = Path.Combine(projectRoot, backupPath);

            if (!File.Exists(absDest))
            {
                File.Copy(absSource, absDest);
                AssetDatabase.Refresh();
            }
        }
    }
}
