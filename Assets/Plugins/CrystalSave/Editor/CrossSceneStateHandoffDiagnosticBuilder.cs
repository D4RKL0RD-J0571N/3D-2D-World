#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using Arawn.CrystalSave.Runtime.Examples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Editor
{
    public sealed class CrossSceneStateHandoffDiagnosticBuilder : EditorWindow
    {
        private const string RootFolder = "Assets/CrystalSaveDiagnostics/CrossSceneStateHandoff";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string MaterialsFolder = RootFolder + "/Materials";

        private const string SourceSceneName = "CSP_Diag_Handoff_Source";
        private const string TargetSceneName = "CSP_Diag_Handoff_Target";
        private const string SourceScenePath = ScenesFolder + "/" + SourceSceneName + ".unity";
        private const string TargetScenePath = ScenesFolder + "/" + TargetSceneName + ".unity";
        private const string PrefabPath = PrefabsFolder + "/CSP_StateHandoffDiagnosticActor.prefab";
        private const string PrefabMaterialPath = MaterialsFolder + "/CSP_StateHandoffActorPrefab.mat";
        private const string SourceMaterialPath = MaterialsFolder + "/CSP_StateHandoffActorSource.mat";
        private const string TargetMaterialPath = MaterialsFolder + "/CSP_StateHandoffActorTarget.mat";
        private const string MarkerMaterialPath = MaterialsFolder + "/CSP_StateHandoffMarker.mat";
        private const string FloorMaterialPath = MaterialsFolder + "/CSP_StateHandoffFloor.mat";

        private const string PrefabAssetId = "CSP_DIAG_STATE_HANDOFF_ACTOR";
        private const string SourceInstanceId = "CSP_DIAG_HANDOFF_SOURCE_INSTANCE";
        private const string TargetInstanceId = "CSP_DIAG_HANDOFF_TARGET_INSTANCE";
        private const string MarkerChildName = "CSP_Diag_HandoffMarker";

        private bool processAsDeferredPass = true;
        private bool forceAssetBasedReuseDuringDeferredOff = true;
        private bool carryRootTransform = true;
        private bool carryRuntimeOverrides = true;
        private bool openSourceSceneAfterBuild = true;
        private int preTransitionWaitFrames = 6;
        private int postRestoreWaitFrames = 8;

        [MenuItem("Tools/Crystal Save/Runtime Debug/Diagnostics/Cross-Scene State Handoff Diagnostic")]
        public static void ShowWindow()
        {
            CrossSceneStateHandoffDiagnosticBuilder window = GetWindow<CrossSceneStateHandoffDiagnosticBuilder>("State Handoff Diagnostic");
            window.minSize = new Vector2(480f, 290f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Cross-Scene State Handoff Diagnostic", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates source and target scenes with non-KeepAcrossScenes scene-baked SaveablePrefabs sharing the same PrefabAssetID. The source changes at runtime, builds handoff data, loads the target scene, and verifies that the target baked copy is claimed instead of spawning a duplicate.",
                MessageType.Info);

            processAsDeferredPass = EditorGUILayout.ToggleLeft("Process as deferred restore pass", processAsDeferredPass);
            forceAssetBasedReuseDuringDeferredOff = EditorGUILayout.ToggleLeft("Force broad deferred asset reuse OFF", forceAssetBasedReuseDuringDeferredOff);
            carryRootTransform = EditorGUILayout.ToggleLeft("Carry root transform", carryRootTransform);
            carryRuntimeOverrides = EditorGUILayout.ToggleLeft("Carry runtime child marker override", carryRuntimeOverrides);
            preTransitionWaitFrames = Mathf.Max(0, EditorGUILayout.IntField("Pre-transition wait frames", preTransitionWaitFrames));
            postRestoreWaitFrames = Mathf.Max(0, EditorGUILayout.IntField("Post-restore wait frames", postRestoreWaitFrames));
            openSourceSceneAfterBuild = EditorGUILayout.ToggleLeft("Open source scene after build", openSourceSceneAfterBuild);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(SourceScenePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.SelectableLabel(TargetScenePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Create State Handoff Diagnostic", GUILayout.Height(32f)))
            {
                CreateDiagnosticScenes(
                    processAsDeferredPass,
                    forceAssetBasedReuseDuringDeferredOff,
                    carryRootTransform,
                    carryRuntimeOverrides,
                    preTransitionWaitFrames,
                    postRestoreWaitFrames,
                    openSourceSceneAfterBuild);
            }
        }

        private static void CreateDiagnosticScenes(
            bool processAsDeferredPass,
            bool forceAssetBasedReuseDuringDeferredOff,
            bool carryRootTransform,
            bool carryRuntimeOverrides,
            int preTransitionWaitFrames,
            int postRestoreWaitFrames,
            bool openSourceSceneAfterBuild)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolders();

            Material prefabMaterial = EnsureMaterial(PrefabMaterialPath, new Color(0.45f, 0.52f, 0.58f));
            Material sourceMaterial = EnsureMaterial(SourceMaterialPath, new Color(0.15f, 0.62f, 0.86f));
            Material targetMaterial = EnsureMaterial(TargetMaterialPath, new Color(0.94f, 0.59f, 0.18f));
            Material markerMaterial = EnsureMaterial(MarkerMaterialPath, new Color(0.18f, 0.88f, 0.46f));
            Material floorMaterial = EnsureMaterial(FloorMaterialPath, new Color(0.34f, 0.36f, 0.38f));

            GameObject prefabAsset = EnsureDiagnosticPrefab(prefabMaterial, markerMaterial);
            if (prefabAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "Crystal Save Diagnostic Failed",
                    "Could not create or load the Cross-Scene State Handoff diagnostic prefab asset.",
                    "OK");
                return;
            }

            EnsurePrefabRegistered(prefabAsset);
            EnsureSaveSettingsAvailable();

            CreateScene(
                SourceScenePath,
                prefabAsset,
                sourceMaterial,
                markerMaterial,
                floorMaterial,
                SourceInstanceId,
                "CSP_Diag_Handoff_Actor_SourceBaked",
                new Vector3(-2f, 1f, 0f),
                enableHandoff: true,
                carryRootTransform,
                carryRuntimeOverrides,
                includeDriver: true,
                processAsDeferredPass,
                forceAssetBasedReuseDuringDeferredOff,
                preTransitionWaitFrames,
                postRestoreWaitFrames);

            CreateScene(
                TargetScenePath,
                prefabAsset,
                targetMaterial,
                markerMaterial,
                floorMaterial,
                TargetInstanceId,
                "CSP_Diag_Handoff_Actor_TargetBaked",
                new Vector3(2f, 1f, 0f),
                enableHandoff: false,
                carryRootTransform,
                carryRuntimeOverrides,
                includeDriver: false,
                processAsDeferredPass,
                forceAssetBasedReuseDuringDeferredOff,
                preTransitionWaitFrames,
                postRestoreWaitFrames);

            AddScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openSourceSceneAfterBuild)
            {
                EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
                GameObject driverObject = GameObject.Find("CSP_StateHandoffDiagnosticDriver");
                if (driverObject != null)
                    Selection.activeGameObject = driverObject;
            }

            EditorUtility.DisplayDialog(
                "Crystal Save Diagnostic Ready",
                "Created the Cross-Scene State Handoff diagnostic.\n\nOpen or stay in CSP_Diag_Handoff_Source, press Play, and inspect Console logs with prefix [CrystalSave][StateHandoffDiag].",
                "OK");
        }

        private static void EnsureFolders()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(ScenesFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(MaterialsFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private static Material EnsureMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                material.color = color;
                EditorUtility.SetDirty(material);
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Standard") ??
                            Shader.Find("Diffuse");

            material = new Material(shader)
            {
                color = color
            };

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject EnsureDiagnosticPrefab(Material actorMaterial, Material markerMaterial)
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existingPrefab == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                temp.name = "CSP_StateHandoffDiagnosticActor";
                ConfigureDiagnosticActor(temp, actorMaterial, markerMaterial, string.Empty, false, true, true);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
                UnityEngine.Object.DestroyImmediate(temp);
                return prefab;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            ConfigureDiagnosticActor(contents, actorMaterial, markerMaterial, string.Empty, false, true, true);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static void ConfigureDiagnosticActor(
            GameObject actor,
            Material actorMaterial,
            Material markerMaterial,
            string uniqueId,
            bool enableHandoff,
            bool carryRootTransform,
            bool carryRuntimeOverrides)
        {
            CapsuleCollider capsuleCollider = actor.GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
                UnityEngine.Object.DestroyImmediate(capsuleCollider);

            CharacterController controller = actor.GetComponent<CharacterController>();
            if (controller == null)
                controller = actor.AddComponent<CharacterController>();

            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = Vector3.zero;

            Renderer rootRenderer = actor.GetComponent<Renderer>();
            if (rootRenderer != null)
                rootRenderer.sharedMaterial = actorMaterial;

            Transform marker = actor.transform.Find(MarkerChildName);
            if (marker == null)
            {
                GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                markerObject.name = MarkerChildName;
                markerObject.transform.SetParent(actor.transform, false);
                marker = markerObject.transform;
            }

            marker.localPosition = new Vector3(0f, 0.75f, 0.45f);
            marker.localRotation = Quaternion.identity;
            marker.localScale = new Vector3(0.22f, 0.22f, 0.22f);

            BoxCollider markerCollider = marker.GetComponent<BoxCollider>();
            if (markerCollider != null)
                UnityEngine.Object.DestroyImmediate(markerCollider);

            Renderer markerRenderer = marker.GetComponent<Renderer>();
            if (markerRenderer != null)
                markerRenderer.sharedMaterial = markerMaterial;

            SaveablePrefab saveablePrefab = actor.GetComponent<SaveablePrefab>();
            if (saveablePrefab == null)
                saveablePrefab = actor.AddComponent<SaveablePrefab>();

            saveablePrefab.RegisterWithSaveSystem = true;
            saveablePrefab.KeepAcrossScenes = false;
            saveablePrefab.ReuseSceneInstanceOnLoad = false;
            saveablePrefab.RememberHomeScene = false;
            saveablePrefab.DeferLowPriorityUntilRequested = false;
            saveablePrefab.DisablePooling = true;
            saveablePrefab.EnableCrossSceneStateHandoff = enableHandoff;
            saveablePrefab.HandoffToSceneBakedPrefabs = true;
            saveablePrefab.HandoffTransform = carryRootTransform;
            saveablePrefab.HandoffRuntimeModifications = carryRuntimeOverrides;
            saveablePrefab.TrackChildStateOverrides = true;
            saveablePrefab.TrackChildTransformOverrides = true;

            SerializedObject serializedPrefab = new SerializedObject(saveablePrefab);
            SetString(serializedPrefab, "prefabAssetID", PrefabAssetId);
            SetString(serializedPrefab, "uniqueID", uniqueId);
            SetBool(serializedPrefab, "keepAcrossScenes", false);
            SetBool(serializedPrefab, "reuseSceneInstanceOnLoad", false);
            SetBool(serializedPrefab, "rememberHomeScene", false);
            SetBool(serializedPrefab, "deferLowPriorityUntilRequested", false);
            SetBool(serializedPrefab, "disablePooling", true);
            SetBool(serializedPrefab, "enableCrossSceneStateHandoff", enableHandoff);
            SetBool(serializedPrefab, "handoffToSceneBakedPrefabs", true);
            SetBool(serializedPrefab, "handoffTransform", carryRootTransform);
            SetBool(serializedPrefab, "handoffRuntimeModifications", carryRuntimeOverrides);
            SetBool(serializedPrefab, "trackChildStateOverrides", true);
            SetBool(serializedPrefab, "trackChildTransformOverrides", true);
            serializedPrefab.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(saveablePrefab);
        }

        private static void CreateScene(
            string scenePath,
            GameObject prefabAsset,
            Material actorMaterial,
            Material markerMaterial,
            Material floorMaterial,
            string instanceId,
            string actorName,
            Vector3 actorPosition,
            bool enableHandoff,
            bool carryRootTransform,
            bool carryRuntimeOverrides,
            bool includeDriver,
            bool processAsDeferredPass,
            bool forceAssetBasedReuseDuringDeferredOff,
            int preTransitionWaitFrames,
            int postRestoreWaitFrames)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateFloor(floorMaterial);
            CreateLighting();
            CreateCamera();

            GameObject actor = PrefabUtility.InstantiatePrefab(prefabAsset, scene) as GameObject;
            if (actor == null)
            {
                actor = UnityEngine.Object.Instantiate(prefabAsset);
                SceneManager.MoveGameObjectToScene(actor, scene);
            }

            actor.name = actorName;
            actor.transform.position = actorPosition;
            actor.transform.rotation = Quaternion.identity;
            actor.transform.localScale = Vector3.one;
            ConfigureDiagnosticActor(actor, actorMaterial, markerMaterial, instanceId, enableHandoff, carryRootTransform, carryRuntimeOverrides);

            if (includeDriver)
            {
                GameObject driverObject = new GameObject("CSP_StateHandoffDiagnosticDriver");
                SceneManager.MoveGameObjectToScene(driverObject, scene);
                CrossSceneStateHandoffDiagnostic driver = driverObject.AddComponent<CrossSceneStateHandoffDiagnostic>();
                ConfigureDriver(driver, processAsDeferredPass, forceAssetBasedReuseDuringDeferredOff, carryRootTransform, carryRuntimeOverrides, preTransitionWaitFrames, postRestoreWaitFrames);
                Selection.activeGameObject = driverObject;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void CreateFloor(Material floorMaterial)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "CSP_Diag_Handoff_Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(1.5f, 1f, 1.5f);

            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = floorMaterial;
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);

            GameObject lightObject = new GameObject("CSP_Diag_Handoff_DirectionalLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("CSP_Diag_Handoff_Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = new Vector3(0f, 3.2f, -7f);
            camera.transform.LookAt(new Vector3(0f, 0.8f, 0f));
            camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
        }

        private static void ConfigureDriver(
            CrossSceneStateHandoffDiagnostic driver,
            bool processAsDeferredPass,
            bool forceAssetBasedReuseDuringDeferredOff,
            bool carryRootTransform,
            bool carryRuntimeOverrides,
            int preTransitionWaitFrames,
            int postRestoreWaitFrames)
        {
            SerializedObject serializedDriver = new SerializedObject(driver);
            SetString(serializedDriver, "diagnosticPrefabAssetID", PrefabAssetId);
            SetString(serializedDriver, "targetSceneName", TargetSceneName);
            SetString(serializedDriver, "sourceInstanceID", SourceInstanceId);
            SetString(serializedDriver, "targetInstanceID", TargetInstanceId);
            SetString(serializedDriver, "markerChildName", MarkerChildName);
            SetString(serializedDriver, "runtimeChangedName", "CSP_Diag_Handoff_Actor_RuntimeChanged");
            SetBool(serializedDriver, "runOnStart", true);
            SetBool(serializedDriver, "processAsDeferredPass", processAsDeferredPass);
            SetBool(serializedDriver, "forceAssetBasedReuseDuringDeferredOff", forceAssetBasedReuseDuringDeferredOff);
            SetBool(serializedDriver, "carryRootTransform", carryRootTransform);
            SetBool(serializedDriver, "carryRuntimeOverrides", carryRuntimeOverrides);
            SetInt(serializedDriver, "preTransitionWaitFrames", Mathf.Max(0, preTransitionWaitFrames));
            SetInt(serializedDriver, "postRestoreWaitFrames", Mathf.Max(0, postRestoreWaitFrames));
            SetVector3(serializedDriver, "sourceRuntimePosition", new Vector3(0f, 1f, 0f));
            SetVector3(serializedDriver, "sourceRuntimeScale", new Vector3(1.25f, 1.25f, 1.25f));
            SetVector3(serializedDriver, "targetDesignPosition", new Vector3(2f, 1f, 0f));
            SetVector3(serializedDriver, "targetDesignScale", Vector3.one);
            SetVector3(serializedDriver, "markerDesignLocalPosition", new Vector3(0f, 0.75f, 0.45f));
            SetVector3(serializedDriver, "markerDesignLocalScale", new Vector3(0.22f, 0.22f, 0.22f));
            SetVector3(serializedDriver, "markerRuntimeLocalPosition", new Vector3(0.55f, 1.05f, 0f));
            SetVector3(serializedDriver, "markerRuntimeLocalScale", new Vector3(0.34f, 0.18f, 0.34f));
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
        }

        private static void EnsurePrefabRegistered(GameObject prefabAsset)
        {
            PrefabRegistry registry = PrefabAssetResolver.LoadActivePrefabRegistry();
            if (registry == null)
            {
                EnsureFolder("Assets/Plugins/CrystalSave/Resources");
                registry = ScriptableObject.CreateInstance<PrefabRegistry>();
                AssetDatabase.CreateAsset(registry, "Assets/Plugins/CrystalSave/Resources/PrefabRegistry.asset");
            }

            PrefabRegistry.PrefabEntry entry = registry.FindEntryByID(PrefabAssetId);
            if (entry != null)
            {
                if (entry.prefab == prefabAsset)
                    return;

                Debug.LogWarning($"[CrystalSave][StateHandoffDiag] PrefabAssetID '{PrefabAssetId}' is already registered to a different prefab. The diagnostic scene will still be created, but runtime restore will use the active registry entry.");
                return;
            }

            if (!registry.TryAddPrefab(PrefabAssetId, prefabAsset, out _, out string reason))
            {
                Debug.LogWarning($"[CrystalSave][StateHandoffDiag] Could not register diagnostic prefab: {reason}");
                return;
            }

            EditorUtility.SetDirty(registry);
        }

        private static void EnsureSaveSettingsAvailable()
        {
            SaveSettings settings = Resources.Load<SaveSettings>("SaveSettings");
            if (settings != null)
                return;

            string[] guids = AssetDatabase.FindAssets("t:SaveSettings");
            if (guids.Length > 0)
            {
                string existingPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                Debug.LogWarning($"[CrystalSave][StateHandoffDiag] Found SaveSettings at '{existingPath}', but it is not in a Resources path. If your project does not load settings through Addressables, the diagnostic may need a Resources/SaveSettings asset.");
                return;
            }

            EnsureFolder("Assets/Plugins/CrystalSave/Resources");
            SaveSettings newSettings = ScriptableObject.CreateInstance<SaveSettings>();
            AssetDatabase.CreateAsset(newSettings, "Assets/Plugins/CrystalSave/Resources/SaveSettings.asset");
            EditorUtility.SetDirty(newSettings);
        }

        private static void AddScenesToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            AddSceneIfMissing(scenes, SourceScenePath);
            AddSceneIfMissing(scenes, TargetScenePath);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddSceneIfMissing(List<EditorBuildSettingsScene> scenes, string path)
        {
            if (scenes.Any(scene => scene.path == path))
                return;

            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value;
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.vector3Value = value;
        }
    }
}
#endif
