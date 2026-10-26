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
    public sealed class KeepAcrossScenesPrefabDiagnosticBuilder : EditorWindow
    {
        private const string RootFolder = "Assets/CrystalSaveDiagnostics/KeepAcrossScenesPrefab";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string MaterialsFolder = RootFolder + "/Materials";

        private const string SourceSceneName = "CSP_Diag_KAS_Source";
        private const string TargetSceneName = "CSP_Diag_KAS_Target";
        private const string SourceScenePath = ScenesFolder + "/" + SourceSceneName + ".unity";
        private const string TargetScenePath = ScenesFolder + "/" + TargetSceneName + ".unity";
        private const string PrefabPath = PrefabsFolder + "/CSP_KeepAcrossDiagnosticActor.prefab";
        private const string PrefabMaterialPath = MaterialsFolder + "/CSP_KeepAcrossActorPrefab.mat";
        private const string SourceMaterialPath = MaterialsFolder + "/CSP_KeepAcrossActorSource.mat";
        private const string TargetMaterialPath = MaterialsFolder + "/CSP_KeepAcrossActorTarget.mat";
        private const string FloorMaterialPath = MaterialsFolder + "/CSP_KeepAcrossFloor.mat";

        private const string PrefabAssetId = "CSP_DIAG_KEEP_ACROSS_ACTOR";
        private const string SourceInstanceId = "CSP_DIAG_KAS_SOURCE_INSTANCE";
        private const string TargetInstanceId = "CSP_DIAG_KAS_TARGET_INSTANCE";

        private bool targetAlsoKeepAcrossScenes = true;
        private bool sourceReuseSceneInstanceOnLoad = true;
        private bool targetReuseSceneInstanceOnLoad = true;
        private bool openSourceSceneAfterBuild = true;
        private int preTransitionWaitFrames = 6;
        private int postTransitionWaitFrames = 12;

        [MenuItem("Tools/Crystal Save/Runtime Debug/Diagnostics/Scene-Baked Keep Across Scenes Diagnostic")]
        public static void ShowWindow()
        {
            KeepAcrossScenesPrefabDiagnosticBuilder window = GetWindow<KeepAcrossScenesPrefabDiagnosticBuilder>("Keep Across Scenes Diagnostic");
            window.minSize = new Vector2(460f, 260f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Scene-Baked Keep Across Scenes Diagnostic", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates a source scene and target scene with scene-baked SaveablePrefabs sharing the same PrefabAssetID. The source is Keep Across Scenes, changes at runtime, loads the target scene, and reports whether both the persistent source and target baked copy exist.",
                MessageType.Info);

            targetAlsoKeepAcrossScenes = EditorGUILayout.ToggleLeft("Target baked copy also Keep Across Scenes", targetAlsoKeepAcrossScenes);
            sourceReuseSceneInstanceOnLoad = EditorGUILayout.ToggleLeft("Source Reuse Scene Instance On Load", sourceReuseSceneInstanceOnLoad);
            targetReuseSceneInstanceOnLoad = EditorGUILayout.ToggleLeft("Target Reuse Scene Instance On Load", targetReuseSceneInstanceOnLoad);
            preTransitionWaitFrames = Mathf.Max(0, EditorGUILayout.IntField("Pre-transition wait frames", preTransitionWaitFrames));
            postTransitionWaitFrames = Mathf.Max(0, EditorGUILayout.IntField("Post-transition wait frames", postTransitionWaitFrames));
            openSourceSceneAfterBuild = EditorGUILayout.ToggleLeft("Open source scene after build", openSourceSceneAfterBuild);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(SourceScenePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.SelectableLabel(TargetScenePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Create Keep Across Scenes Diagnostic", GUILayout.Height(32f)))
            {
                CreateDiagnosticScenes(
                    targetAlsoKeepAcrossScenes,
                    sourceReuseSceneInstanceOnLoad,
                    targetReuseSceneInstanceOnLoad,
                    preTransitionWaitFrames,
                    postTransitionWaitFrames,
                    openSourceSceneAfterBuild);
            }
        }

        private static void CreateDiagnosticScenes(
            bool targetAlsoKeepAcrossScenes,
            bool sourceReuseSceneInstanceOnLoad,
            bool targetReuseSceneInstanceOnLoad,
            int preTransitionWaitFrames,
            int postTransitionWaitFrames,
            bool openSourceSceneAfterBuild)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolders();

            Material prefabMaterial = EnsureMaterial(PrefabMaterialPath, new Color(0.45f, 0.52f, 0.58f));
            Material sourceMaterial = EnsureMaterial(SourceMaterialPath, new Color(0.10f, 0.58f, 0.92f));
            Material targetMaterial = EnsureMaterial(TargetMaterialPath, new Color(0.95f, 0.62f, 0.18f));
            Material floorMaterial = EnsureMaterial(FloorMaterialPath, new Color(0.36f, 0.38f, 0.40f));

            GameObject prefabAsset = EnsureDiagnosticPrefab(prefabMaterial);
            if (prefabAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "Crystal Save Diagnostic Failed",
                    "Could not create or load the Keep Across Scenes diagnostic prefab asset.",
                    "OK");
                return;
            }

            EnsurePrefabRegistered(prefabAsset);
            EnsureSaveSettingsAvailable();

            CreateScene(
                SourceScenePath,
                prefabAsset,
                sourceMaterial,
                floorMaterial,
                SourceInstanceId,
                "CSP_Diag_KAS_Actor_SourceBaked",
                new Vector3(-2f, 1f, 0f),
                keepAcrossScenes: true,
                reuseSceneInstanceOnLoad: sourceReuseSceneInstanceOnLoad,
                includeDriver: true,
                preTransitionWaitFrames,
                postTransitionWaitFrames);

            CreateScene(
                TargetScenePath,
                prefabAsset,
                targetMaterial,
                floorMaterial,
                TargetInstanceId,
                "CSP_Diag_KAS_Actor_TargetBaked",
                new Vector3(2f, 1f, 0f),
                keepAcrossScenes: targetAlsoKeepAcrossScenes,
                reuseSceneInstanceOnLoad: targetReuseSceneInstanceOnLoad,
                includeDriver: false,
                preTransitionWaitFrames,
                postTransitionWaitFrames);

            AddScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openSourceSceneAfterBuild)
            {
                EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
                GameObject driverObject = GameObject.Find("CSP_KeepAcrossDiagnosticDriver");
                if (driverObject != null)
                    Selection.activeGameObject = driverObject;
            }

            EditorUtility.DisplayDialog(
                "Crystal Save Diagnostic Ready",
                "Created the Keep Across Scenes prefab diagnostic.\n\nOpen or stay in CSP_Diag_KAS_Source, press Play, and inspect Console logs with prefix [CrystalSave][KeepAcrossPrefabDiag].",
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

        private static GameObject EnsureDiagnosticPrefab(Material actorMaterial)
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existingPrefab == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                temp.name = "CSP_KeepAcrossDiagnosticActor";
                ConfigureDiagnosticActor(temp, actorMaterial, string.Empty, false, false);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
                Object.DestroyImmediate(temp);
                return prefab;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            ConfigureDiagnosticActor(contents, actorMaterial, string.Empty, false, false);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static void ConfigureDiagnosticActor(
            GameObject actor,
            Material actorMaterial,
            string uniqueId,
            bool keepAcrossScenes,
            bool reuseSceneInstanceOnLoad)
        {
            CapsuleCollider capsuleCollider = actor.GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
                Object.DestroyImmediate(capsuleCollider);

            CharacterController controller = actor.GetComponent<CharacterController>();
            if (controller == null)
                controller = actor.AddComponent<CharacterController>();

            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = Vector3.zero;

            Renderer renderer = actor.GetComponentInChildren<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = actorMaterial;

            SaveablePrefab saveablePrefab = actor.GetComponent<SaveablePrefab>();
            if (saveablePrefab == null)
                saveablePrefab = actor.AddComponent<SaveablePrefab>();

            saveablePrefab.RegisterWithSaveSystem = true;
            saveablePrefab.KeepAcrossScenes = keepAcrossScenes;
            saveablePrefab.ReuseSceneInstanceOnLoad = reuseSceneInstanceOnLoad;
            saveablePrefab.RememberHomeScene = false;
            saveablePrefab.DeferLowPriorityUntilRequested = false;
            saveablePrefab.DisablePooling = true;

            SerializedObject serializedPrefab = new SerializedObject(saveablePrefab);
            SetString(serializedPrefab, "prefabAssetID", PrefabAssetId);
            SetString(serializedPrefab, "uniqueID", uniqueId);
            SetBool(serializedPrefab, "keepAcrossScenes", keepAcrossScenes);
            SetBool(serializedPrefab, "reuseSceneInstanceOnLoad", reuseSceneInstanceOnLoad);
            SetBool(serializedPrefab, "rememberHomeScene", false);
            SetBool(serializedPrefab, "deferLowPriorityUntilRequested", false);
            SetBool(serializedPrefab, "disablePooling", true);
            serializedPrefab.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(saveablePrefab);
        }

        private static void CreateScene(
            string scenePath,
            GameObject prefabAsset,
            Material actorMaterial,
            Material floorMaterial,
            string instanceId,
            string actorName,
            Vector3 actorPosition,
            bool keepAcrossScenes,
            bool reuseSceneInstanceOnLoad,
            bool includeDriver,
            int preTransitionWaitFrames,
            int postTransitionWaitFrames)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateFloor(floorMaterial);
            CreateLighting();
            CreateCamera();

            GameObject actor = PrefabUtility.InstantiatePrefab(prefabAsset, scene) as GameObject;
            if (actor == null)
            {
                actor = Object.Instantiate(prefabAsset);
                SceneManager.MoveGameObjectToScene(actor, scene);
            }

            actor.name = actorName;
            actor.transform.position = actorPosition;
            actor.transform.rotation = Quaternion.identity;
            actor.transform.localScale = Vector3.one;
            ConfigureDiagnosticActor(actor, actorMaterial, instanceId, keepAcrossScenes, reuseSceneInstanceOnLoad);

            if (includeDriver)
            {
                GameObject driverObject = new GameObject("CSP_KeepAcrossDiagnosticDriver");
                SceneManager.MoveGameObjectToScene(driverObject, scene);
                KeepAcrossScenesPrefabDiagnostic driver = driverObject.AddComponent<KeepAcrossScenesPrefabDiagnostic>();
                ConfigureDriver(driver, preTransitionWaitFrames, postTransitionWaitFrames);
                Selection.activeGameObject = driverObject;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void CreateFloor(Material floorMaterial)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "CSP_Diag_KAS_Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(1.5f, 1f, 1.5f);

            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = floorMaterial;
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);

            GameObject lightObject = new GameObject("CSP_Diag_KAS_DirectionalLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("CSP_Diag_KAS_Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = new Vector3(0f, 3.2f, -7f);
            camera.transform.LookAt(new Vector3(0f, 0.8f, 0f));
            camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
        }

        private static void ConfigureDriver(
            KeepAcrossScenesPrefabDiagnostic driver,
            int preTransitionWaitFrames,
            int postTransitionWaitFrames)
        {
            SerializedObject serializedDriver = new SerializedObject(driver);
            SetString(serializedDriver, "diagnosticPrefabAssetID", PrefabAssetId);
            SetString(serializedDriver, "targetSceneName", TargetSceneName);
            SetString(serializedDriver, "sourceInstanceID", SourceInstanceId);
            SetString(serializedDriver, "targetInstanceID", TargetInstanceId);
            SetString(serializedDriver, "runtimeChangedName", "CSP_Diag_KAS_Actor_RuntimeChanged");
            SetString(serializedDriver, "targetDesignName", "CSP_Diag_KAS_Actor_TargetBaked");
            SetBool(serializedDriver, "runOnStart", true);
            SetBool(serializedDriver, "forceSourceKeepAcrossScenes", true);
            SetBool(serializedDriver, "forceSourceReuseSceneInstanceOnLoad", true);
            SetInt(serializedDriver, "preTransitionWaitFrames", Mathf.Max(0, preTransitionWaitFrames));
            SetInt(serializedDriver, "postTransitionWaitFrames", Mathf.Max(0, postTransitionWaitFrames));
            SetVector3(serializedDriver, "sourceRuntimePosition", new Vector3(0f, 1f, 0f));
            SetVector3(serializedDriver, "sourceRuntimeScale", new Vector3(1.25f, 1.25f, 1.25f));
            SetVector3(serializedDriver, "targetDesignPosition", new Vector3(2f, 1f, 0f));
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

                Debug.LogWarning($"[CrystalSave][KeepAcrossPrefabDiag] PrefabAssetID '{PrefabAssetId}' is already registered to a different prefab. The diagnostic scene will still be created, but runtime restore will use the active registry entry.");
                return;
            }

            if (!registry.TryAddPrefab(PrefabAssetId, prefabAsset, out _, out string reason))
            {
                Debug.LogWarning($"[CrystalSave][KeepAcrossPrefabDiag] Could not register diagnostic prefab: {reason}");
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
                Debug.LogWarning($"[CrystalSave][KeepAcrossPrefabDiag] Found SaveSettings at '{existingPath}', but it is not in a Resources path. If your project does not load settings through Addressables, the diagnostic may need a Resources/SaveSettings asset.");
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
