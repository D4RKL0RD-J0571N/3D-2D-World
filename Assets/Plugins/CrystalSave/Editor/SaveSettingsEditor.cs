#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Arawn.CrystalSave.Runtime;
#if CRYSTALSAVE_TIMEMACHINE
using Arawn.CrystalSave.Runtime.TimeMachine;
#endif

namespace Arawn.CrystalSave.Editor
{
    [CustomEditor(typeof(SaveSettings))]
    public class SaveSettingsEditor : UnityEditor.Editor
    {
        SerializedProperty persistentPathProp;
        SerializedProperty modeProp;
        SerializedProperty folderProp;
        SerializedProperty rootProp;
        SerializedProperty runMigrationProp;
        SerializedProperty autoSaveMigratedDataProp;
        SerializedProperty useAddressablesProp;
        SerializedProperty enableAssetReuseDuringDeferredProp;
        SerializedProperty optimizeRuntimeCaptureProp;
        SerializedProperty skipDuplicateIDCheckProp;
        SerializedProperty enableLookupCacheProp;
        SerializedProperty enableComponentLookupCacheProp;
        SerializedProperty scanForExistingGameObjectsProp;
        SerializedProperty existingObjectScanBatchSizeProp;
        SerializedProperty prefabInstantiationBatchSizeProp;
        SerializedProperty enableVerbosePrefabLoadDiagnosticsProp;
        SerializedProperty groupInstantiationBySceneProp;
        SerializedProperty componentApplyBatchSizeProp;
        SerializedProperty activeStateApplyBatchSizeProp;
        SerializedProperty syncTransformsAfterPrefabLoadProp;
        SerializedProperty applyParentWhenParentInfoMissingProp;
        SerializedProperty autoRegisterPrefabsProp;
        SerializedProperty showEditorHierarchyIconsProp;
        SerializedProperty enableDefineSymbolsAutomationProp;
        SerializedProperty enableLegacySceneObjectIDEditorProp;
        SerializedProperty enablePrefabRegistryAutoValidationProp;
        bool showPersistent = true;
        bool showEditorAutomation = true;
        bool showPerformance = true;
        
#if CRYSTALSAVE_TIMEMACHINE
        SerializedProperty timeMachinePresetProp;
        SerializedProperty useContinuousTimeProp;
        SerializedProperty defaultResumeModeProp;
        SerializedProperty defaultAutoBranchBehaviorProp;
        SerializedProperty defaultBranchCopyModeProp;
#endif

        void OnEnable()
        {
            persistentPathProp = serializedObject.FindProperty("persistentPath");
            modeProp = persistentPathProp.FindPropertyRelative("mode");
            folderProp = persistentPathProp.FindPropertyRelative("customFolderName");
            rootProp = persistentPathProp.FindPropertyRelative("nonWebGLOutputRoot");
            runMigrationProp = serializedObject.FindProperty("runPersistentPathMigrationOnStartup");
            autoSaveMigratedDataProp = serializedObject.FindProperty("autoSaveMigratedData");
            useAddressablesProp = serializedObject.FindProperty("useAddressables");
            enableAssetReuseDuringDeferredProp = serializedObject.FindProperty("enableAssetBasedReuseDuringDeferred");
            optimizeRuntimeCaptureProp = serializedObject.FindProperty("optimizeRuntimeCapture");
            skipDuplicateIDCheckProp = serializedObject.FindProperty("skipDuplicateIDCheck");
            enableLookupCacheProp = serializedObject.FindProperty("enableLookupCache");
            enableComponentLookupCacheProp = serializedObject.FindProperty("enableComponentLookupCache");
            scanForExistingGameObjectsProp = serializedObject.FindProperty("scanForExistingGameObjects");
            existingObjectScanBatchSizeProp = serializedObject.FindProperty("existingObjectScanBatchSize");
            prefabInstantiationBatchSizeProp = serializedObject.FindProperty("prefabInstantiationBatchSize");
            enableVerbosePrefabLoadDiagnosticsProp = serializedObject.FindProperty("enableVerbosePrefabLoadDiagnostics");
            groupInstantiationBySceneProp = serializedObject.FindProperty("groupInstantiationByScene");
            componentApplyBatchSizeProp = serializedObject.FindProperty("componentApplyBatchSize");
            activeStateApplyBatchSizeProp = serializedObject.FindProperty("activeStateApplyBatchSize");
            syncTransformsAfterPrefabLoadProp = serializedObject.FindProperty("syncTransformsAfterPrefabLoad");
            applyParentWhenParentInfoMissingProp = serializedObject.FindProperty("applyParentWhenParentInfoMissing");
            autoRegisterPrefabsProp = serializedObject.FindProperty("autoRegisterPrefabs");
            showEditorHierarchyIconsProp = serializedObject.FindProperty("showEditorHierarchyIcons");
            enableDefineSymbolsAutomationProp = serializedObject.FindProperty("enableDefineSymbolsAutomation");
            enableLegacySceneObjectIDEditorProp = serializedObject.FindProperty("enableLegacySceneObjectIDEditor");
            enablePrefabRegistryAutoValidationProp = serializedObject.FindProperty("enablePrefabRegistryAutoValidation");
            CrystalSaveEditorAutomationSettings.EnsureInitialized((SaveSettings)target);
            
#if CRYSTALSAVE_TIMEMACHINE
            timeMachinePresetProp = serializedObject.FindProperty("timeMachinePreset");
            useContinuousTimeProp = serializedObject.FindProperty("useContinuousTimeByDefault");
            defaultResumeModeProp = serializedObject.FindProperty("defaultResumeMode");
            defaultAutoBranchBehaviorProp = serializedObject.FindProperty("defaultAutoBranchBehavior");
            defaultBranchCopyModeProp = serializedObject.FindProperty("defaultBranchCopyMode");
#endif
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
#if CRYSTALSAVE_TIMEMACHINE
            // Store preset value before changes
            var previousPreset = timeMachinePresetProp != null ? 
                (TimeMachinePresetType)timeMachinePresetProp.enumValueIndex : 
                TimeMachinePresetType.Custom;
#endif
            
            DrawPersistentPath();
            DrawAddressablesToggle();
            DrawEditorAutomationSettings();
            DrawPerformanceSettings();
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "persistentPath",
                "runPersistentPathMigrationOnStartup",
                "autoSaveMigratedData",
                "useAddressables",
                "editorAutomationSettingsInitialized",
                "autoRegisterPrefabs",
                "showEditorHierarchyIcons",
                "enableDefineSymbolsAutomation",
                "enableLegacySceneObjectIDEditor",
                "enablePrefabRegistryAutoValidation",
                "optimizeRuntimeCapture",
                "skipDuplicateIDCheck",
                "enableLookupCache",
                "enableComponentLookupCache",
                "scanForExistingGameObjects",
                "existingObjectScanBatchSize",
                "prefabInstantiationBatchSize",
                "enableVerbosePrefabLoadDiagnostics",
                "groupInstantiationByScene",
                "componentApplyBatchSize",
                "activeStateApplyBatchSize",
                "syncTransformsAfterPrefabLoad",
                "applyParentWhenParentInfoMissing",
                "enableAssetBasedReuseDuringDeferred");
            
#if CRYSTALSAVE_TIMEMACHINE
            // Check if preset changed and auto-apply
            if (timeMachinePresetProp != null)
            {
                var newPreset = (TimeMachinePresetType)timeMachinePresetProp.enumValueIndex;
                if (newPreset != previousPreset)
                {
                    var settings = (SaveSettings)target;
                    settings.ApplyPresetToSettings();
                    EditorUtility.SetDirty(settings);
                    
                    if (newPreset != TimeMachinePresetType.Custom)
                    {
                        Debug.Log($"[SaveSettings] ✅ Auto-applied preset: {newPreset}");
                    }
                }
                
                // Add preset helper UI after drawing all properties
                DrawTimeMachinePresetHelper();
            }
#endif
            
            serializedObject.ApplyModifiedProperties();
            CrystalSaveEditorAutomationSettings.UpdateEditorLogLevel((SaveSettings)target);
        }

        void DrawPersistentPath()
        {
            showPersistent = EditorGUILayout.Foldout(showPersistent, "Persistent Path", true);
            if (!showPersistent) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(modeProp, new GUIContent("Mode"));
            if ((PersistentPathMode)modeProp.enumValueIndex == PersistentPathMode.Custom)
            {
                folderProp.stringValue = Regex.Replace(folderProp.stringValue.Trim(), "[\\\\/:*?\"<>|]", "");
                if (string.IsNullOrWhiteSpace(folderProp.stringValue))
                    folderProp.stringValue = "CrystalSave";
                EditorGUILayout.PropertyField(folderProp, new GUIContent("Custom Folder Name"));
                EditorGUILayout.PropertyField(rootProp, new GUIContent("Non WebGL Output Root"));
                bool hasRootOverride = !string.IsNullOrWhiteSpace(rootProp.stringValue);
                string nonWebRoot = Application.persistentDataPath;
                if (hasRootOverride)
                {
                    string expandedRoot = CustomStoragePathProvider.ExpandRootPath(rootProp.stringValue);
                    if (Path.IsPathRooted(expandedRoot))
                        nonWebRoot = Path.GetFullPath(expandedRoot);
                    else
                        EditorGUILayout.HelpBox("Non WebGL Output Root must be an absolute path. On Linux use /home/user/... or ~/..., not home/user/....", MessageType.Warning);
                }

                string webGLPath = $"/idbfs/{folderProp.stringValue}";
                string nonWeb = Path.Combine(nonWebRoot, folderProp.stringValue);
                EditorGUILayout.LabelField("Preview WebGL Path", webGLPath);
                EditorGUILayout.LabelField("Preview Non WebGL Path", nonWeb);
                if (GUILayout.Button("Test Resolve Path"))
                {
                    var provider = ((SaveSettings)target).CreatePathProvider();
                    string resolvedPath = provider.GetRootPath();
                    Debug.Log($"Crystal Save path: {resolvedPath}");

                    string message = $"Resolved path:\n{resolvedPath}";
                    if (hasRootOverride && !CustomStoragePathProvider.IsAbsoluteRootPath(rootProp.stringValue))
                    {
                        message += "\n\nWarning: Non WebGL Output Root is not absolute, so Crystal Save ignored it and used Application.persistentDataPath. On Linux use /home/user/... or ~/....";
                    }

                    EditorUtility.DisplayDialog("Crystal Save Path", message, "OK");
                }
                if (GUILayout.Button("Move data from old path to new"))
                {
                    var provider = ((SaveSettings)target).CreatePathProvider();
                    PersistentPathMigration.TryMigrate(Application.persistentDataPath, provider.GetRootPath());
                }
                EditorGUILayout.PropertyField(runMigrationProp, new GUIContent("Run Migration On Startup"));
                EditorGUILayout.PropertyField(autoSaveMigratedDataProp);
            }
            EditorGUI.indentLevel--;
        }

        void DrawAddressablesToggle()
        {
            if (useAddressablesProp == null)
                return;

            bool addressablesPresent = false;
#if REMEMBERME_ADDRESSABLES_PRESENT
            addressablesPresent = true;
#endif

            using (new EditorGUI.DisabledScope(!addressablesPresent))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(useAddressablesProp);
                if (useAddressablesProp.boolValue)
                {
                    if (GUILayout.Button(AddressablesHelpWindow.HelpButtonContent, EditorStyles.miniButton, GUILayout.Width(22f)))
                    {
                        AddressablesHelpWindow.ShowWindow();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (!addressablesPresent)
            {
                if (useAddressablesProp.boolValue)
                {
                    useAddressablesProp.boolValue = false;
                }
                EditorGUILayout.HelpBox("Unity Addressables package is not installed. Install it to enable Addressables integration for Crystal Save.", MessageType.Info);
            }
            else if (useAddressablesProp.boolValue)
            {
                EditorGUILayout.HelpBox("When Addressables is enabled, PrefabRegistry and MigrationManager must be Addressable with matching addresses and should live outside any Resources folder, for example Assets/My Settings/PrefabRegistry.asset. Rebuild Addressables content after moving or changing them.", MessageType.Warning);
                if (GUILayout.Button("Migrate Core Assets To Addressables"))
                {
                    serializedObject.ApplyModifiedProperties();
                    CrystalSaveAddressablesMigration.RunInteractive();
                    serializedObject.Update();
                }
            }
        }

        void DrawPerformanceSettings()
        {
            if (optimizeRuntimeCaptureProp == null)
                return;

            EditorGUILayout.Space();
            showPerformance = EditorGUILayout.Foldout(showPerformance, "Performance", true);
            if (!showPerformance)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "Runtime performance controls for capture, lookup caches, startup scans, load batching, and restore scheduling.",
                MessageType.Info);

            EditorGUILayout.PropertyField(optimizeRuntimeCaptureProp);
            EditorGUILayout.PropertyField(
                skipDuplicateIDCheckProp,
                new GUIContent(
                    "Skip Duplicate ID Check",
                    "Disables duplicate ID validation in OnValidate for large scenes. GUID collisions are statistically negligible, but this check helps detect cloned objects."));
            EditorGUILayout.PropertyField(enableLookupCacheProp, new GUIContent("Enable Lookup Cache"));
            EditorGUILayout.PropertyField(enableComponentLookupCacheProp, new GUIContent("Enable Component Lookup Cache"));
            EditorGUILayout.PropertyField(
                scanForExistingGameObjectsProp,
                new GUIContent(
                    "Scan For Existing Game Objects",
                    "Scan active scenes at startup to find and register existing RememberGameObjects. Disable for faster startup when objects register explicitly."));
            using (new EditorGUI.DisabledScope(!scanForExistingGameObjectsProp.boolValue))
            {
                EditorGUILayout.PropertyField(
                    existingObjectScanBatchSizeProp,
                    new GUIContent(
                        "Existing Object Scan Batch Size",
                        "How many RememberGameObjects to register per frame. Set 0 or negative to scan in a single frame."));
            }
            EditorGUILayout.PropertyField(
                prefabInstantiationBatchSizeProp,
                new GUIContent(
                    "Prefab Instantiation Batch Size",
                    "Number of prefabs instantiated per frame during load. Set 0 or negative for single-frame instantiation."));
            EditorGUILayout.PropertyField(
                enableVerbosePrefabLoadDiagnosticsProp,
                new GUIContent(
                    "Verbose Prefab Load Diagnostics",
                    "Build and emit detailed per-entry prefab descriptions during restore. Leave disabled for normal play and profiling."));
            EditorGUILayout.PropertyField(
                componentApplyBatchSizeProp,
                new GUIContent(
                    "Component Apply Batch Size",
                    "Number of components applied per frame during load. Set 0 or negative for single-frame application."));
            EditorGUILayout.PropertyField(
                activeStateApplyBatchSizeProp,
                new GUIContent(
                    "Active State Apply Batch Size",
                    "Number of GameObject active states applied per frame during load. Set 0 or negative for single-frame application."));
            EditorGUILayout.PropertyField(
                syncTransformsAfterPrefabLoadProp,
                new GUIContent(
                    "Sync Transforms After Prefab Load",
                    "Enable to call Physics.SyncTransforms() once after all prefabs load instead of after each Rigidbody."));
            EditorGUILayout.PropertyField(
                applyParentWhenParentInfoMissingProp,
                new GUIContent(
                    "Apply Parent When Parent Info Missing",
                    "When saved parent info is missing, still apply parenting during the post-load pass. This detaches the object to root."));
            EditorGUILayout.PropertyField(
                groupInstantiationBySceneProp,
                new GUIContent(
                    "Group Instantiation By Scene",
                    "Instantiate prefabs grouped by home scene to reduce SceneManager.SetActiveScene calls."));
            EditorGUILayout.PropertyField(
                enableAssetReuseDuringDeferredProp,
                new GUIContent(
                    "Enable Asset-based Reuse (Deferred)",
                    "Allow PrefabManager to claim existing scene instances by PrefabAssetID during deferred processing.\n" +
                    "When off, deferred entries will always instantiate new instances unless an exact UniqueID match is found.")
            );
            EditorGUI.indentLevel--;
        }

        void DrawEditorAutomationSettings()
        {
            if (autoRegisterPrefabsProp == null)
                return;

            EditorGUILayout.Space();
            showEditorAutomation = EditorGUILayout.Foldout(showEditorAutomation, "Editor Automation & Visuals", true);
            if (!showEditorAutomation)
                return;

            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "Project-wide editor toggles for Crystal Save automation and hierarchy visuals. Runtime save/load behavior is unchanged.",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                autoRegisterPrefabsProp,
                new GUIContent(
                    "Auto-Register SaveablePrefab Prefabs",
                    "When enabled, editor callbacks may add or synchronize SaveablePrefab prefab assets in the PrefabRegistry.\n\n" +
                    "When disabled, opening a prefab inspector or validating a prefab will not add PrefabRegistry rows. Manual Register Prefab and Auto-Register buttons still work."));

            EditorGUILayout.PropertyField(
                showEditorHierarchyIconsProp,
                new GUIContent(
                    "Show Hierarchy Icons",
                    "Shows Crystal Save icons in the Unity Hierarchy for linked UniqueIDs, RememberComposite objects, and SaveablePrefabs."));

            EditorGUILayout.PropertyField(
                enableDefineSymbolsAutomationProp,
                new GUIContent(
                    "Enable Define Symbols Automation",
                    "When enabled, Crystal Save automatically updates scripting define symbols for detected packages, render pipelines, and optional modules.\n\n" +
                    "When disabled, existing symbols are left as-is and scripting defines must be managed manually."));

            EditorGUILayout.PropertyField(
                enableLegacySceneObjectIDEditorProp,
                new GUIContent(
                    "Enable Legacy SceneObjectID Inspector",
                    "When enabled, SceneObjectID uses the Crystal Save custom inspector with ID edit/generate controls and Keep Across Scenes helpers.\n\n" +
                    "When disabled, Unity draws the default inspector for SceneObjectID."));

            EditorGUILayout.PropertyField(
                enablePrefabRegistryAutoValidationProp,
                new GUIContent(
                    "Auto-Validate Prefab Registry",
                    "When enabled, Crystal Save automatically cleans invalid PrefabRegistry entries and clears duplicated SaveablePrefab instance IDs from editor callbacks.\n\n" +
                    "When disabled, automatic cleanup is skipped. Manual validation and cleanup buttons still work."));
            EditorGUI.indentLevel--;
        }
        
#if CRYSTALSAVE_TIMEMACHINE
        void DrawTimeMachinePresetHelper()
        {
            var presetType = (TimeMachinePresetType)timeMachinePresetProp.enumValueIndex;
            
            // Info box with preset description
            if (presetType != TimeMachinePresetType.Custom)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    var config = TimeMachinePresets.GetPresetConfig(presetType);
                    if (config != null)
                    {
                        EditorGUILayout.LabelField("📋 Preset Configuration", EditorStyles.boldLabel);
                        EditorGUILayout.Space(3);
                        
                        EditorGUILayout.LabelField(config.GetDescription(), EditorStyles.wordWrappedLabel);
                        EditorGUILayout.Space(5);
                        
                        EditorGUILayout.LabelField("Settings:", EditorStyles.miniBoldLabel);
                        EditorGUILayout.LabelField($"• Resume Mode: {config.resumeMode}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"• Branch Behavior: {config.autoBranchBehavior}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"• Branch Copy: {config.branchCopyMode}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"• Ghost Mode: {(config.allowRecordingDuringPlayback ? "✅ Enabled" : "❌ Disabled")}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"• Continuous Time: {(config.useContinuousTime ? "✅ Enabled" : "❌ Disabled")}", EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "✅ This preset configuration is automatically applied. Advanced settings below will be overridden by this preset. " +
                    "Select 'Custom' preset to manually control all settings.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "⚙️ Custom preset selected - you have full manual control over all advanced settings below.",
                    MessageType.Info);
            }
        }
#endif
    }
}
#endif
