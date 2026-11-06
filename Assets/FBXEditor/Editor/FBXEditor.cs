using UnityEngine;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using System.IO;
using System.Reflection; // Required for Gizmo toggling
using System.Collections.Generic; // Required for Async Processor
using System.Linq;

namespace FBXEditor.Editor
{
    public partial class FBXEditor : EditorWindow
{
    // --- Core State Variables ---
    private GameObject sourceObject;
    private GameObject activeEditingCopy;
    private bool shouldOverwrite;
    private bool extractAndRestoreMaterials;
    private bool restoreTexturesOnSave = true;
    private bool disableOriginalsAfterCombine = true;
    private bool centerPivotAfterCombine = true;
    private int currentTab = 0;

    // --- Scroll View Positions for Each Tab ---
    private Vector2 hierarchyEditorScrollPos;
    private Vector2 meshCombinerScrollPos;
    private Vector2 pivotEditorScrollPos;
    private Vector2 textureLinkerScrollPos;
    private Vector2 meshSeparatorScrollPos;

    // --- Editor Resources ---
    private GUIStyle watermarkStyle;
    // private Texture2D undoIcon;
    private bool originalShowSelectionWire;
    private PropertyInfo showSelectionWireProperty;

    private bool modelHasSkinnedMeshes = false;

    // --- Static UI Content (avoid per-frame allocations) ---
    private static readonly string[] TabNames = { "Hierarchy", "Combine", "Pivot", "Materials", "Separate", "Batch" };

    // --- Save Copy Format ---
    private enum SaveCopyFormat { FBX, Prefab }
    private SaveCopyFormat saveCopyFormat = SaveCopyFormat.FBX;
    private static readonly string[] SaveCopyFormatNames = { "FBX", "Prefab" };

    // --- Asynchronous Processing Canvas ---
    private bool isProcessingAsync = false;
    private float asyncProgress = 0f;
    private string asyncMessage = "";
    private IEnumerator<float> asyncProcessor;


    [MenuItem("Tools/FBX Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<FBXEditor>("FBX Editor");
        // Set a more elegant default window size
        window.minSize = new Vector2(400, 400);
        if (window.position.width < 450 || window.position.height < 500)
        {
            window.position = new Rect(window.position.x, window.position.y, 500, 600);
        }
    }

    #region Unity Events

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += EditorUpdate; // Hook into the editor's update loop

        // --- Get Gizmo Property via Reflection ---
        var annotationUtilityType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AnnotationUtility");
        if (annotationUtilityType != null)
        {
            showSelectionWireProperty = annotationUtilityType.GetProperty("showSelectionOutline", BindingFlags.Static | BindingFlags.NonPublic);
        }

        // --- Initialize GUI Styles ---
        InitializeStyles();

        // --- Initialize Feature-Specific Data from Partial Classes ---
        InitializeMeshCombiner();
        InitializeTextureLinker();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= EditorUpdate; // Unhook from the editor's update loop

        TrySetSelectionWireProperty(originalShowSelectionWire);
        CleanUpCopy();
    }
    
    /// <summary>
    /// Main update loop for the editor window, drives asynchronous processes.
    /// </summary>
    void EditorUpdate()
    {
        if (isProcessingAsync && asyncProcessor != null)
        {
            if (asyncProcessor.MoveNext())
            {
                asyncProgress = asyncProcessor.Current;
                Repaint(); // Redraw the window to update the progress bar
            }
            else
            {
                // Process finished
                isProcessingAsync = false;
                asyncProcessor = null;
                Repaint();
            }
        }
    }

    void OnGUI()
    {
        // Disable the entire UI if an async process is running
        using (new EditorGUI.DisabledScope(isProcessingAsync))
        {
            DrawToolbar();

            int previousTab = currentTab;
            currentTab = GUILayout.Toolbar(currentTab, TabNames);

            HandleTabSwitch(previousTab, currentTab);

            // Draw the content for the currently selected tab with scroll views.
            switch (currentTab)
            {
                case 0: 
                    hierarchyEditorScrollPos = EditorGUILayout.BeginScrollView(hierarchyEditorScrollPos);
                    DrawHierarchyEditorTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case 1: 
                    meshCombinerScrollPos = EditorGUILayout.BeginScrollView(meshCombinerScrollPos);
                    DrawMeshCombinerTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case 2: 
                    pivotEditorScrollPos = EditorGUILayout.BeginScrollView(pivotEditorScrollPos);
                    DrawPivotEditorTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case 3: 
                    textureLinkerScrollPos = EditorGUILayout.BeginScrollView(textureLinkerScrollPos);
                    DrawTextureLinkerTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case 4:
                    meshSeparatorScrollPos = EditorGUILayout.BeginScrollView(meshSeparatorScrollPos);
                    DrawMeshSeparatorTab();
                    EditorGUILayout.EndScrollView();
                    break;
                case 5:
                    DrawBatchProcessorTab();
                    break;
            }
        }
        
        // Draw the async progress bar canvas if a process is running
        if (isProcessingAsync)
        {
            DrawAsyncProgressCanvas();
        }
    }
    
    void OnSceneGUI(SceneView sceneView)
    {
        if (activeEditingCopy == null) return;

        DrawSceneWatermark(sceneView);

        // Don't allow scene interaction while processing
        if (isProcessingAsync)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            return;
        }

        switch(currentTab)
        {
            case 0: DrawBoneReorientPreview(sceneView); break;
            case 2: HandlePivotEditorSceneGUI(sceneView); break;
            case 4: HandleMeshSeparatorSceneGUI(sceneView); break;
        }
    }
    
    void OnInspectorUpdate() { Repaint(); }

    #endregion

    #region Core Logic

    private void HandleTabSwitch(int previousTab, int newTab)
    {
        if(previousTab == newTab) return;

        // Cleanup when switching AWAY from a tab
        if(previousTab == 2 && pivotHandle != null) DestroyImmediate(pivotHandle);
        if(previousTab == 4)
        {
            ClearTriangleSelection(); // From MeshSeparator partial class
            TrySetSelectionWireProperty(originalShowSelectionWire);
        }

        // Setup when switching TO a tab
        if (newTab == 4)
        {
            originalShowSelectionWire = TryGetSelectionWireProperty() ?? true;
            TrySetSelectionWireProperty(true);
        }
    }

    /// <summary>
    /// Safely gets the selection wire property value using reflection.
    /// Returns null if the property is not available.
    /// </summary>
    private bool? TryGetSelectionWireProperty()
    {
        try
        {
            if (showSelectionWireProperty != null)
            {
                return (bool)showSelectionWireProperty.GetValue(null);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to get selection wire property: {e.Message}");
        }
        return null;
    }

    /// <summary>
    /// Safely sets the selection wire property value using reflection.
    /// </summary>
    private void TrySetSelectionWireProperty(bool value)
    {
        try
        {
            if (showSelectionWireProperty != null)
            {
                showSelectionWireProperty.SetValue(null, value);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to set selection wire property: {e.Message}");
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUI.BeginChangeCheck();
        sourceObject = (GameObject)EditorGUILayout.ObjectField(sourceObject, typeof(GameObject), true, GUILayout.Width(250));
        if(EditorGUI.EndChangeCheck())
        {
            CleanUpCopy();
        }

        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("Undo", EditorStyles.toolbarButton)) { Undo.PerformUndo(); }

        if (sourceObject != null)
        {
            if (GUILayout.Button("Restore Original", EditorStyles.toolbarButton)) { RestoreOriginal(); }
            shouldOverwrite = GUILayout.Toggle(shouldOverwrite, "Overwrite Original", EditorStyles.toolbarButton);
            if (shouldOverwrite)
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton)) { SaveOverwrite(); }
            }
            else
            {
                saveCopyFormat = (SaveCopyFormat)EditorGUILayout.Popup(
                    (int)saveCopyFormat, SaveCopyFormatNames, EditorStyles.toolbarPopup, GUILayout.Width(60));
                if (GUILayout.Button("Save a Copy", EditorStyles.toolbarButton)) { SaveAsCopy(); }
            }
        }

        // Settings gear button
        if (GUILayout.Button(new GUIContent("\u2699"), EditorStyles.toolbarButton, GUILayout.Width(24)))
        {
            FBXEditorSettingsWindow.Open(this);
        }

        EditorGUILayout.EndHorizontal();
    }

    private class FBXEditorSettingsWindow : EditorWindow
    {
        private FBXEditor editor;

        public static void Open(FBXEditor editor)
        {
            var window = GetWindow<FBXEditorSettingsWindow>(true, "FBX Editor Settings");
            window.editor = editor;
            window.minSize = new Vector2(300, 170);
            window.maxSize = new Vector2(400, 250);
            window.Show();
        }

        private void OnGUI()
        {
            if (editor == null) { Close(); return; }

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250;

            EditorGUILayout.Space(6);

            // --- Save Settings ---
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
            editor.restoreTexturesOnSave = EditorGUILayout.Toggle(
                new GUIContent("Restore Textures on Save", "Re-applies original texture assignments to embedded materials after FBX export. Only used when 'Apply Original Materials on Save' is off."),
                editor.restoreTexturesOnSave);

            EditorGUILayout.Space(8);

            // --- Combine Settings ---
            EditorGUILayout.LabelField("Combine", EditorStyles.boldLabel);
            editor.disableOriginalsAfterCombine = EditorGUILayout.Toggle(
                new GUIContent("Disable Originals After Combine", "Disables the original source GameObjects after combining their meshes."),
                editor.disableOriginalsAfterCombine);
            editor.centerPivotAfterCombine = EditorGUILayout.Toggle(
                new GUIContent("Center Pivot After Combine", "Centers the pivot point of combined static meshes. Does not affect skinned meshes."),
                editor.centerPivotAfterCombine);

            EditorGUIUtility.labelWidth = prevLabelWidth;
        }
    }

    /// <summary>
    /// Draws the progress bar and message at the bottom of the window.
    /// </summary>
    private void DrawAsyncProgressCanvas()
    {
        Rect progressRect = new Rect(0, position.height - 20, position.width, 20);
        EditorGUI.ProgressBar(progressRect, asyncProgress, asyncMessage);
    }

    private void CleanUpCopy()
    {
        if (activeEditingCopy != null) DestroyImmediate(activeEditingCopy);
        if (pivotHandle != null) DestroyImmediate(pivotHandle);
        CleanUpTransferSource();
        originalTextureCache?.Clear();
        ClearTriangleSelection();
        modelHasSkinnedMeshes = false;
    }

    private void RestoreOriginal()
    {
        CleanUpCopy();
        CreateEditableCopy();
    }
    
    private void CreateEditableCopy()
    {
        if (sourceObject == null) return;
        activeEditingCopy = (GameObject)PrefabUtility.InstantiatePrefab(sourceObject);
        activeEditingCopy.name = sourceObject.name + " (Editable Copy)";
        PrefabUtility.UnpackPrefabInstance(activeEditingCopy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        modelHasSkinnedMeshes = activeEditingCopy.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        
        bakeScale = 1.0f;
        bakeRotation = Vector3.zero;
        ApplyLiveTransformPreview();

        CacheOriginalTextures(); 
        
        FrameObject(activeEditingCopy);
    }
    
    private void SaveOverwrite()
    {
        if (activeEditingCopy == null) return;
        string originalPath = AssetDatabase.GetAssetPath(sourceObject);
        if (string.IsNullOrEmpty(originalPath) || !originalPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("Could not find the original FBX file path. Please use 'Save a Copy' instead.");
            return;
        }
        if (EditorUtility.DisplayDialog("Overwrite Original FBX?", $"You are about to overwrite:\n\n{originalPath}\n\nThis action cannot be undone.", "Overwrite", "Cancel"))
        {
            // The user is expected to bake manually before saving.
            // The ModelExporter will handle baking the root object's transform on export.

            // Export under the original root node name and with the full rig still bound, or the
            // reimport invalidates the Avatar stored in the (retained) .meta.
            ExportWithRigPreserved(originalPath, activeEditingCopy, sourceObject,
                GetExportRootName(originalPath, sourceObject.name));

            // The .meta is kept on overwrite, so its stored Avatar still refers to the old bone
            // names. Repair it before anything triggers a reimport and rebuilds the Avatar.
            RemapAvatarForRenamedBones(originalPath);

            if (extractAndRestoreMaterials)
                ExtractAndRestoreMaterials(originalPath);
            else if (restoreTexturesOnSave)
                RestoreMaterialTexturesAfterExport(originalPath);
            Debug.Log($"Successfully saved and overwrote: {originalPath}");
        }
    }

    private void SaveAsCopy()
    {
        if (activeEditingCopy == null) return;

        if (saveCopyFormat == SaveCopyFormat.Prefab)
        {
            SaveAsPrefabCopy();
            return;
        }

        string originalPath = AssetDatabase.GetAssetPath(sourceObject);
        string defaultDirectory = System.IO.Path.GetDirectoryName(originalPath);
        if (!string.IsNullOrEmpty(defaultDirectory) && !System.IO.Path.IsPathRooted(defaultDirectory))
        {
            defaultDirectory = System.IO.Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length), defaultDirectory);
        }

        string defaultName = sourceObject.name + "_Edited.fbx";
        string path = EditorUtility.SaveFilePanel("Save Edited FBX As...", defaultDirectory, defaultName, "fbx");
        if (string.IsNullOrEmpty(path)) return;
        
        // The user is expected to bake manually before saving.
        // The ModelExporter will handle baking the root object's transform on export.
        // Keep the source model's root node name so the copy stays rig-compatible with the
        // original Avatar (e.g. usable with "Copy From Other Avatar").
        ExportWithRigPreserved(path, activeEditingCopy, sourceObject,
            GetExportRootName(originalPath, sourceObject.name));

        // Convert absolute path to relative asset path for AssetDatabase APIs
        if (path.StartsWith(Application.dataPath))
        {
            string relativePath = "Assets" + path.Substring(Application.dataPath.Length);

            // No-op for a brand new file (Unity maps its Avatar from the exported names), but
            // repairs the mapping when the copy overwrote an existing asset.
            RemapAvatarForRenamedBones(relativePath);

            if (extractAndRestoreMaterials)
                ExtractAndRestoreMaterials(relativePath);
            else if (restoreTexturesOnSave)
                RestoreMaterialTexturesAfterExport(relativePath);
        }
        else
        {
            AssetDatabase.Refresh();
        }

        Debug.Log($"Successfully saved a copy to: {path}");
    }

    private void SaveAsPrefabCopy()
    {
        string originalAssetPath = AssetDatabase.GetAssetPath(sourceObject);
        string defaultDirectory = System.IO.Path.GetDirectoryName(originalAssetPath);
        string defaultName = sourceObject.name + "_Edited";

        string relativePath = EditorUtility.SaveFilePanelInProject(
            "Save Edited Prefab As...",
            defaultName,
            "prefab",
            "Save the edited model as a Prefab asset.",
            defaultDirectory);

        if (string.IsNullOrEmpty(relativePath)) return;

        PersistInMemoryMeshes(activeEditingCopy, relativePath);

        string originalRootName = activeEditingCopy.name;
        activeEditingCopy.name = System.IO.Path.GetFileNameWithoutExtension(relativePath);

        PrefabUtility.SaveAsPrefabAsset(activeEditingCopy, relativePath, out bool success);

        activeEditingCopy.name = originalRootName;

        if (success)
        {
            Debug.Log($"Successfully saved a copy to: {relativePath}");
        }
        else
        {
            Debug.LogError($"Failed to save prefab to: {relativePath}");
        }
    }

    /// <summary>
    /// Saves any in-memory (scene-only) meshes on the editing copy as project assets,
    /// so prefab references survive after the editing copy is destroyed. Meshes that are
    /// already project assets are left alone.
    /// </summary>
    private void PersistInMemoryMeshes(GameObject root, string prefabRelativePath)
    {
        string prefabDir = System.IO.Path.GetDirectoryName(prefabRelativePath);
        string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabRelativePath);
        string meshFolderName = prefabName + "_Meshes";
        string meshFolder = System.IO.Path.Combine(prefabDir, meshFolderName);
        bool folderEnsured = false;

        var savedMeshes = new HashSet<Mesh>();

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = GetMeshFromObject(renderer.gameObject);
            if (mesh == null || savedMeshes.Contains(mesh)) continue;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh))) continue;

            if (!folderEnsured)
            {
                if (!AssetDatabase.IsValidFolder(meshFolder))
                    AssetDatabase.CreateFolder(prefabDir, meshFolderName);
                folderEnsured = true;
            }

            SaveMeshAsset(mesh, mesh.name, meshFolder);
            savedMeshes.Add(mesh);
        }
    }

    /// <summary>
    /// After exporting an FBX, restores material texture assignments on the reimported asset.
    /// Unity's FBX Exporter may not properly embed texture references, causing materials
    /// to lose their textures after reimport.
    /// </summary>
    private void RestoreMaterialTexturesAfterExport(string savedAssetPath)
    {
        // 1. Build name-based texture cache from the editing copy's current materials
        var texturesByMaterialName = new Dictionary<string, Dictionary<string, Texture>>();
        var allRenderers = activeEditingCopy.GetComponentsInChildren<Renderer>(true);
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
                        if (tex != null)
                            textures[propName] = tex;
                    }
                }
                if (textures.Count > 0)
                    texturesByMaterialName[mat.name] = textures;
            }
        }

        AssetDatabase.Refresh();

        // 2. Copy source FBX's material remapping to saved FBX (for external .mat files)
        string sourcePath = AssetDatabase.GetAssetPath(sourceObject);
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
            if (hasRemaps)
                savedImporter.SaveAndReimport();
        }

        // 3. Restore textures on embedded materials in the saved FBX
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

        if (anyRestored)
            AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Extracts embedded materials from a saved FBX into separate .mat files,
    /// then restores all material settings from the original editing copy materials.
    /// </summary>
    private void ExtractAndRestoreMaterials(string fbxAssetPath)
    {
        if (activeEditingCopy == null) return;

        // Step 1: Snapshot original materials from editing copy by name
        var originalMaterials = new Dictionary<string, Material>();
        var allRenderers = activeEditingCopy.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in allRenderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat != null && !originalMaterials.ContainsKey(mat.name))
                    originalMaterials[mat.name] = mat;
            }
        }

        if (originalMaterials.Count == 0) return;

        // Step 2: Extract embedded materials
        var savedAssets = AssetDatabase.LoadAllAssetsAtPath(fbxAssetPath);
        var embeddedMaterials = new List<Material>();
        foreach (var asset in savedAssets)
        {
            if (asset is Material mat && AssetDatabase.IsSubAsset(mat))
                embeddedMaterials.Add(mat);
        }

        if (embeddedMaterials.Count == 0) return;

        string sourceDir = Path.GetDirectoryName(fbxAssetPath);
        string modelName = Path.GetFileNameWithoutExtension(fbxAssetPath);
        string matFolderName = modelName + "_Materials";
        string matFolder = Path.Combine(sourceDir, matFolderName);
        if (!AssetDatabase.IsValidFolder(matFolder))
        {
            AssetDatabase.CreateFolder(sourceDir, matFolderName);
        }

        var importer = AssetImporter.GetAtPath(fbxAssetPath) as ModelImporter;
        if (importer == null) return;

        var extractedMats = new List<(string name, Material mat)>();
        foreach (var embeddedMat in embeddedMaterials)
        {
            string matPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(matFolder, embeddedMat.name + ".mat"));
            Material newMat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(newMat, matPath);

            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embeddedMat.name), newMat);
            extractedMats.Add((embeddedMat.name, newMat));
        }

        importer.SaveAndReimport();

        // Step 3: Restore all settings from original materials onto extracted .mat files
        int restoredCount = 0;
        var unmatchedMaterials = new List<string>();
        foreach (var (name, extractedMat) in extractedMats)
        {
            if (originalMaterials.TryGetValue(name, out Material originalMat))
            {
                CopyMaterialWithoutEmission(originalMat, extractedMat);
                restoredCount++;
            }
            else
            {
                unmatchedMaterials.Add(name);
            }
        }

        if (restoredCount > 0)
            AssetDatabase.SaveAssets();

        Debug.Log($"Extracted and restored {restoredCount} material(s) to {matFolder}");

        if (unmatchedMaterials.Count > 0)
            Debug.LogWarning($"[Apply Original Materials] {unmatchedMaterials.Count} material(s) in the exported FBX did not match any original materials: {string.Join(", ", unmatchedMaterials)}");
    }

    #endregion

    #region Export Rig Preservation

    /// <summary>
    /// Bookkeeping for a skinned mesh that was temporarily re-bound for export.
    /// </summary>
    private class SkinnedRebind
    {
        public SkinnedMeshRenderer smr;
        public Transform[] originalBones;
        public Mesh originalMesh;
        public Mesh tempMesh;
    }

    /// <summary>
    /// Exports a hierarchy while keeping its rig intact. Two things have to hold for an Avatar
    /// to survive the round trip:
    ///
    /// 1. The root node must keep its original name. Unity stores the Avatar skeleton in the
    ///    .meta as a name/parentName chain rooted at the FBX root node, so exporting the working
    ///    copy under its "(Editable Copy)" name breaks that chain and the importer reports a
    ///    missing required human bone (Hips is the first one it checks).
    ///
    /// 2. Every bone must still be referenced by a skinned mesh. The FBX Exporter only tags a
    ///    transform as a skeleton bone when it appears in some exported SkinnedMeshRenderer
    ///    bones array; anything else is written as a plain node using its current transform
    ///    rather than its bind pose. Deleting meshes from a modular character therefore silently
    ///    demotes every bone that only those meshes weighted.
    /// </summary>
    private void ExportWithRigPreserved(string filePath, GameObject root, GameObject sourceModel, string rootNodeName)
    {
        if (root == null) return;

        string previousName = root.name;
        var rebinds = new List<SkinnedRebind>();

        try
        {
            if (!string.IsNullOrEmpty(rootNodeName)) root.name = rootNodeName;

            RebindSkinnedMeshesToFullRig(root, sourceModel, rebinds);
            if (rebinds.Count > 0)
            {
                Debug.Log($"[FBX Editor] Re-bound {rebinds.Count} skinned mesh(es) to the full source rig so unreferenced bones still export as bones.");
            }

            WarnAboutIncompatibleNames(root);

            ModelExporter.ExportObject(filePath, root);
        }
        finally
        {
            RestoreSkinnedMeshBindings(rebinds);
            if (root != null) root.name = previousName;
        }
    }

    /// <summary>
    /// The name the exported root node should carry so an existing Avatar still resolves.
    /// Prefers the root recorded in the asset's own humanoid skeleton description, since that is
    /// exactly what the importer matches against; falls back to the model's name.
    /// </summary>
    private string GetExportRootName(string existingAssetPath, string fallbackName)
    {
        if (!string.IsNullOrEmpty(existingAssetPath))
        {
            var importer = AssetImporter.GetAtPath(existingAssetPath) as ModelImporter;
            if (importer != null)
            {
                // Unity serialises the skeleton in hierarchy order, so entry 0 is the root node
                // the Avatar's name/parentName chain hangs off. (SkeletonBone.parentName itself
                // is not public API, so the chain cannot be walked directly.)
                var skeleton = importer.humanDescription.skeleton;
                if (skeleton != null && skeleton.Length > 0 && !string.IsNullOrEmpty(skeleton[0].name))
                {
                    return skeleton[0].name;
                }
            }
        }
        return fallbackName;
    }

    /// <summary>
    /// Every bone name in the source model's rig, including bones whose meshes may since have
    /// been deleted from the editing copy.
    /// </summary>
    private HashSet<string> GetRigBoneNames(GameObject model)
    {
        var names = new HashSet<string>();
        if (model == null) return names;

        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.bones == null) continue;
            foreach (var bone in smr.bones)
            {
                if (bone != null) names.Add(bone.name);
            }
        }
        return names;
    }

    /// <summary>
    /// Appends any rig bone the surviving skinned meshes no longer reference to their bones
    /// arrays, so the exporter still writes them out as skeleton bones. Bone weights are
    /// index-based and the extra bones go on the end, so no existing index moves and no
    /// deformation changes. Records what it touched so the caller can undo it after export.
    /// </summary>
    private void RebindSkinnedMeshesToFullRig(GameObject root, GameObject sourceModel, List<SkinnedRebind> rebinds)
    {
        var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (smrs.Length == 0) return;

        var rigBoneNames = GetRigBoneNames(sourceModel);
        if (rigBoneNames.Count == 0) return;

        // Map the source rig's bone names onto the transforms still present in this hierarchy.
        var rigTransforms = new List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (rigBoneNames.Contains(t.name)) rigTransforms.Add(t);
        }
        if (rigTransforms.Count == 0) return;

        foreach (var smr in smrs)
        {
            Mesh mesh = smr.sharedMesh;
            if (mesh == null) continue;

            Transform[] bones = smr.bones;
            if (bones == null || bones.Length == 0) continue;

            Matrix4x4[] bindposes = mesh.bindposes;
            // A mismatch here makes the exporter skip the skeleton entirely; leave it alone.
            if (bindposes == null || bindposes.Length != bones.Length) continue;

            var alreadyBound = new HashSet<Transform>();
            foreach (var bone in bones)
            {
                if (bone != null) alreadyBound.Add(bone);
            }

            var missing = new List<Transform>();
            foreach (var t in rigTransforms)
            {
                if (!alreadyBound.Contains(t)) missing.Add(t);
            }
            if (missing.Count == 0) continue;

            var newBones = new Transform[bones.Length + missing.Count];
            var newBindposes = new Matrix4x4[bindposes.Length + missing.Count];
            bones.CopyTo(newBones, 0);
            bindposes.CopyTo(newBindposes, 0);

            Matrix4x4 meshLocalToWorld = smr.transform.localToWorldMatrix;
            for (int i = 0; i < missing.Count; i++)
            {
                newBones[bones.Length + i] = missing[i];
                newBindposes[bindposes.Length + i] = missing[i].worldToLocalMatrix * meshLocalToWorld;
            }

            // The mesh may be a sub-asset of the source FBX, so never write bindposes into it.
            var rebind = new SkinnedRebind
            {
                smr = smr,
                originalBones = bones,
                originalMesh = mesh,
                tempMesh = Instantiate(mesh)
            };
            rebind.tempMesh.name = mesh.name;
            rebind.tempMesh.bindposes = newBindposes;

            smr.sharedMesh = rebind.tempMesh;
            smr.bones = newBones;
            rebinds.Add(rebind);
        }
    }

    /// <summary>
    /// Mirrors the FBX Exporter's "Compatible Naming" rule (ConvertToMayaCompatibleName): every
    /// character that is not a letter or digit becomes an underscore, except a namespace colon,
    /// and a leading digit gets an underscore prefix.
    ///
    /// This matters because Unity's importer disambiguates duplicate node names with a SPACE
    /// ("Thumb_01" / "Thumb_01 1"), and the exporter cannot write that space - so the bone comes
    /// back as "Thumb_01_1" and every name-based Avatar reference to it stops resolving.
    /// </summary>
    private static string ToFbxCompatibleName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "_";

        string newName = RemoveDiacritics(name);

        if (char.IsDigit(newName[0])) newName = newName.Insert(0, "_");

        for (int i = 0; i < newName.Length; i++)
        {
            if (!char.IsLetterOrDigit(newName, i))
            {
                // ':' is the Maya namespace separator and is preserved (e.g. "mixamorig:Hips").
                if (i < newName.Length - 1 && newName[i] == ':') continue;
                newName = newName.Replace(newName[i], '_');
            }
        }
        return newName;
    }

    private static string RemoveDiacritics(string text)
    {
        string normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder();

        foreach (char c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }
        return builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>
    /// Logs the transforms whose names the exporter is about to alter, so a renamed bone is not
    /// a silent surprise. Anything referencing those bones by name or path - animation curves,
    /// IK setups, attachment scripts - will need updating.
    /// </summary>
    private void WarnAboutIncompatibleNames(GameObject root)
    {
        var renames = new List<string>();

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string compatible = ToFbxCompatibleName(t.name);
            if (compatible != t.name) renames.Add(t.name + "  ->  " + compatible);
        }

        if (renames.Count == 0) return;

        var message = new System.Text.StringBuilder();
        message.AppendLine($"[FBX Editor] The FBX exporter's Compatible Naming will rename {renames.Count} object(s) on export.");
        message.AppendLine("Avatar mappings are repaired automatically, but animation curve paths and any code that finds these by name will not be:");
        for (int i = 0; i < renames.Count && i < 15; i++)
        {
            message.AppendLine("   " + renames[i]);
        }
        if (renames.Count > 15) message.AppendLine($"   ... and {renames.Count - 15} more");

        Debug.LogWarning(message.ToString());
    }

    /// <summary>
    /// Rewrites the bone names stored in the asset's Avatar description so they still match the
    /// node names the exporter wrote. Without this, a bone the exporter renamed (see
    /// ToFbxCompatibleName) drops out of the Avatar and Unity reports a missing required bone.
    ///
    /// Safe to call unconditionally: names that survive the exporter unchanged are left alone,
    /// so on a file with no renames this is a no-op.
    /// </summary>
    private void RemapAvatarForRenamedBones(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return;

        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null) return;

        var serializedImporter = new SerializedObject(importer);
        int changed = 0;

        // Generic rigs drive root motion from a named bone.
        changed += RewriteBoneNameProperty(serializedImporter.FindProperty("humanDescription.rootMotionBoneName"));

        // humanName is the human bone slot ("Hips") and must not be touched - only boneName,
        // which points at a transform in the model.
        var human = serializedImporter.FindProperty("humanDescription.human");
        if (human != null && human.isArray)
        {
            for (int i = 0; i < human.arraySize; i++)
            {
                changed += RewriteBoneNameProperty(human.GetArrayElementAtIndex(i).FindPropertyRelative("boneName"));
            }
        }

        // The skeleton is a name/parentName chain, so both halves have to move together.
        var skeleton = serializedImporter.FindProperty("humanDescription.skeleton");
        if (skeleton != null && skeleton.isArray)
        {
            for (int i = 0; i < skeleton.arraySize; i++)
            {
                var element = skeleton.GetArrayElementAtIndex(i);
                changed += RewriteBoneNameProperty(element.FindPropertyRelative("name"));
                changed += RewriteBoneNameProperty(element.FindPropertyRelative("parentName"));
            }
        }

        if (changed == 0) return;

        serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        importer.SaveAndReimport();
        Debug.Log($"[FBX Editor] Updated {changed} Avatar bone reference(s) to match the names written by the FBX exporter.");
    }

    /// <summary>
    /// Runs one stored bone name through the exporter's naming rule. Returns 1 if it changed.
    /// </summary>
    private static int RewriteBoneNameProperty(SerializedProperty property)
    {
        if (property == null || property.propertyType != SerializedPropertyType.String) return 0;

        string current = property.stringValue;
        if (string.IsNullOrEmpty(current)) return 0;

        string compatible = ToFbxCompatibleName(current);
        if (compatible == current) return 0;

        property.stringValue = compatible;
        return 1;
    }

    /// <summary>
    /// Puts back the original bones and meshes recorded by RebindSkinnedMeshesToFullRig.
    /// </summary>
    private void RestoreSkinnedMeshBindings(List<SkinnedRebind> rebinds)
    {
        foreach (var rebind in rebinds)
        {
            if (rebind.smr != null)
            {
                rebind.smr.sharedMesh = rebind.originalMesh;
                rebind.smr.bones = rebind.originalBones;
            }
            if (rebind.tempMesh != null) DestroyImmediate(rebind.tempMesh);
        }
        rebinds.Clear();
    }

    #endregion

    /// <summary>
    /// Copies all material properties from source to target, but skips emission entirely.
    /// Uses per-property copy instead of CopySerialized to avoid emission bleeding through.
    /// </summary>
    private void CopyMaterialWithoutEmission(Material source, Material target)
    {
        target.shader = source.shader;
        target.renderQueue = source.renderQueue;
        target.enableInstancing = source.enableInstancing;
        target.doubleSidedGI = source.doubleSidedGI;

        // Copy keywords, excluding emission
        var keywords = source.shaderKeywords.Where(k => k != "_EMISSION").ToArray();
        target.shaderKeywords = keywords;

        // Copy all shader properties except emission
        var shader = source.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string propName = shader.GetPropertyName(i);

            // Skip emission properties
            if (propName == "_EmissionColor" || propName == "_EmissionMap")
                continue;

            var propType = shader.GetPropertyType(i);
            switch (propType)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    target.SetColor(propName, source.GetColor(propName));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    target.SetFloat(propName, source.GetFloat(propName));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    target.SetVector(propName, source.GetVector(propName));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    target.SetTexture(propName, source.GetTexture(propName));
                    target.SetTextureOffset(propName, source.GetTextureOffset(propName));
                    target.SetTextureScale(propName, source.GetTextureScale(propName));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Int:
                    target.SetInt(propName, source.GetInt(propName));
                    break;
            }
        }

        // Ensure emission is fully off
        target.DisableKeyword("_EMISSION");
        target.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        if (target.HasProperty("_EmissionColor"))
            target.SetColor("_EmissionColor", Color.black);
        if (target.HasProperty("_EmissionMap"))
            target.SetTexture("_EmissionMap", null);

        EditorUtility.SetDirty(target);
    }

    }
}
