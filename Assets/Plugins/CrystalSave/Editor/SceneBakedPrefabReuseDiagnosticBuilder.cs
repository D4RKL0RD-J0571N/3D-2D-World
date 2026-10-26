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
    public sealed class SceneBakedPrefabReuseDiagnosticBuilder : EditorWindow
    {
        private const string RootFolder = "Assets/CrystalSaveDiagnostics/SceneBakedPrefabReuse";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string MaterialsFolder = RootFolder + "/Materials";

        private const string SourceSceneName = "CSP_Diag_Source";
        private const string TargetSceneName = "CSP_Diag_Target";
        private const string SourceScenePath = ScenesFolder + "/" + SourceSceneName + ".unity";
        private const string TargetScenePath = ScenesFolder + "/" + TargetSceneName + ".unity";
        private const string PrefabPath = PrefabsFolder + "/CSP_DiagnosticActor.prefab";
        private const string ActorMaterialPath = MaterialsFolder + "/CSP_DiagnosticActor.mat";
        private const string FloorMaterialPath = MaterialsFolder + "/CSP_DiagnosticFloor.mat";

        private const string PrefabAssetId = "CSP_DIAG_SCENE_BAKED_ACTOR";
        private const string SourceInstanceId = "CSP_DIAG_SOURCE_INSTANCE";
        private const string TargetInstanceId = "CSP_DIAG_TARGET_INSTANCE";

        private bool processAsDeferredPass = true;
        private bool forceAssetBasedReuseDuringDeferred;
        private bool reuseSceneInstanceFlagInData = true;
        private bool setHomeSceneInManualSyntheticData;
        private bool openSourceSceneAfterBuild = true;
        private SceneBakedPrefabReuseDiagnostic.DriverMode driverMode = SceneBakedPrefabReuseDiagnostic.DriverMode.SnapshotOrchestrator;
        private bool orchestratorGatePendingPrefabsUntilActivated = true;
        private int orchestratorActivationDelayFrames;
        private int saveLoadDiagnosticSlot = 1;

        [MenuItem("Tools/Crystal Save/Runtime Debug/Diagnostics/Scene-Baked Prefab Reuse Diagnostic")]
        public static void ShowWindow()
        {
            SceneBakedPrefabReuseDiagnosticBuilder window = GetWindow<SceneBakedPrefabReuseDiagnosticBuilder>("Scene-Baked Prefab Diagnostic");
            window.minSize = new Vector2(420f, 240f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Scene-Baked Prefab Reuse Diagnostic", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates a source scene, target scene, registered diagnostic prefab, and play-mode drivers. Press Play in the source scene and watch the Console for duplicate detection.",
                MessageType.Info);

            driverMode = (SceneBakedPrefabReuseDiagnostic.DriverMode)EditorGUILayout.EnumPopup("Source transition driver", driverMode);
            if (driverMode == SceneBakedPrefabReuseDiagnostic.DriverMode.SnapshotOrchestrator)
            {
                orchestratorGatePendingPrefabsUntilActivated = EditorGUILayout.ToggleLeft("Gate pending prefabs until activation", orchestratorGatePendingPrefabsUntilActivated);
                orchestratorActivationDelayFrames = EditorGUILayout.IntField("Orchestrator activation delay frames", Mathf.Max(0, orchestratorActivationDelayFrames));
            }
            processAsDeferredPass = EditorGUILayout.ToggleLeft("Process as deferred restore pass", processAsDeferredPass);
            forceAssetBasedReuseDuringDeferred = EditorGUILayout.ToggleLeft("Enable asset-based reuse during deferred pass in play mode", forceAssetBasedReuseDuringDeferred);
            reuseSceneInstanceFlagInData = EditorGUILayout.ToggleLeft("Set ReuseSceneInstanceOnLoad in the synthetic save data", reuseSceneInstanceFlagInData);
            if (driverMode == SceneBakedPrefabReuseDiagnostic.DriverMode.ManualPrefabManager)
            {
                setHomeSceneInManualSyntheticData = EditorGUILayout.ToggleLeft("Set HomeScene in manual synthetic save data", setHomeSceneInManualSyntheticData);
            }
            saveLoadDiagnosticSlot = EditorGUILayout.IntField("Save/load diagnostic slot", Mathf.Max(1, saveLoadDiagnosticSlot));
            openSourceSceneAfterBuild = EditorGUILayout.ToggleLeft("Open source scene after build", openSourceSceneAfterBuild);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(SourceScenePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.SelectableLabel(TargetScenePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Create Diagnostic Scenes", GUILayout.Height(32f)))
            {
                CreateDiagnosticScenes(
                    processAsDeferredPass,
                    forceAssetBasedReuseDuringDeferred,
                    reuseSceneInstanceFlagInData,
                    setHomeSceneInManualSyntheticData,
                    openSourceSceneAfterBuild,
                    driverMode,
                    orchestratorGatePendingPrefabsUntilActivated,
                    orchestratorActivationDelayFrames,
                    saveLoadDiagnosticSlot);
            }
        }

        private static void CreateDiagnosticScenes(
            bool processAsDeferredPass,
            bool forceAssetBasedReuseDuringDeferred,
            bool reuseSceneInstanceFlagInData,
            bool setHomeSceneInManualSyntheticData,
            bool openSourceSceneAfterBuild,
            SceneBakedPrefabReuseDiagnostic.DriverMode driverMode,
            bool orchestratorGatePendingPrefabsUntilActivated,
            int orchestratorActivationDelayFrames,
            int saveLoadDiagnosticSlot)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolders();

            Material actorMaterial = EnsureMaterial(ActorMaterialPath, new Color(0.12f, 0.56f, 0.90f));
            Material floorMaterial = EnsureMaterial(FloorMaterialPath, new Color(0.42f, 0.44f, 0.46f));
            GameObject prefabAsset = EnsureDiagnosticPrefab(actorMaterial);
            if (prefabAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "Crystal Save Diagnostic Failed",
                    "Could not create or load the diagnostic prefab asset.",
                    "OK");
                return;
            }

            EnsurePrefabRegistered(prefabAsset);
            EnsureSaveSettingsAvailable();

            CreateScene(SourceScenePath, prefabAsset, actorMaterial, floorMaterial, SourceInstanceId, true,
                processAsDeferredPass, forceAssetBasedReuseDuringDeferred, reuseSceneInstanceFlagInData,
                setHomeSceneInManualSyntheticData, driverMode, orchestratorGatePendingPrefabsUntilActivated,
                orchestratorActivationDelayFrames, saveLoadDiagnosticSlot);
            CreateScene(TargetScenePath, prefabAsset, actorMaterial, floorMaterial, TargetInstanceId, false,
                processAsDeferredPass, forceAssetBasedReuseDuringDeferred, reuseSceneInstanceFlagInData,
                setHomeSceneInManualSyntheticData, driverMode, orchestratorGatePendingPrefabsUntilActivated,
                orchestratorActivationDelayFrames, saveLoadDiagnosticSlot);

            AddScenesToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openSourceSceneAfterBuild)
            {
                EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
                GameObject driverObject = GameObject.Find("CSP_DiagnosticDriver");
                if (driverObject != null)
                {
                    Selection.activeGameObject = driverObject;
                }
            }

            EditorUtility.DisplayDialog(
                "Crystal Save Diagnostic Ready",
                "Created the scene-baked prefab diagnostics.\n\nOpen or stay in CSP_Diag_Source, press Play, and inspect Console logs with prefixes [CrystalSave][SceneBakedPrefabDiag] and [CrystalSave][SceneBakedSaveLoadDiag].",
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
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
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
                temp.name = "CSP_DiagnosticActor";
                ConfigureDiagnosticActor(temp, actorMaterial, string.Empty, false);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
                Object.DestroyImmediate(temp);
                return prefab;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            ConfigureDiagnosticActor(contents, actorMaterial, string.Empty, false);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static void ConfigureDiagnosticActor(GameObject actor, Material actorMaterial, string uniqueId, bool sceneInstance)
        {
            actor.name = sceneInstance ? actor.name : "CSP_DiagnosticActor";

            CapsuleCollider capsuleCollider = actor.GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
            {
                Object.DestroyImmediate(capsuleCollider);
            }

            CharacterController controller = actor.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = actor.AddComponent<CharacterController>();
            }

            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = Vector3.zero;

            Renderer renderer = actor.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = actorMaterial;
            }

            SaveablePrefab saveablePrefab = actor.GetComponent<SaveablePrefab>();
            if (saveablePrefab == null)
            {
                saveablePrefab = actor.AddComponent<SaveablePrefab>();
            }

            saveablePrefab.RegisterWithSaveSystem = true;
            saveablePrefab.ReuseSceneInstanceOnLoad = sceneInstance;
            saveablePrefab.DeferLowPriorityUntilRequested = false;
            saveablePrefab.DisablePooling = true;
            saveablePrefab.RememberHomeScene = false;

            SerializedObject serializedPrefab = new SerializedObject(saveablePrefab);
            SetString(serializedPrefab, "prefabAssetID", PrefabAssetId);
            SetString(serializedPrefab, "uniqueID", uniqueId);
            serializedPrefab.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(saveablePrefab);
        }

        private static void CreateScene(
            string scenePath,
            GameObject prefabAsset,
            Material actorMaterial,
            Material floorMaterial,
            string instanceId,
            bool includeDriver,
            bool processAsDeferredPass,
            bool forceAssetBasedReuseDuringDeferred,
            bool reuseSceneInstanceFlagInData,
            bool setHomeSceneInManualSyntheticData,
            SceneBakedPrefabReuseDiagnostic.DriverMode driverMode,
            bool orchestratorGatePendingPrefabsUntilActivated,
            int orchestratorActivationDelayFrames,
            int saveLoadDiagnosticSlot)
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

            actor.name = includeDriver ? "CSP_Diag_Actor_SourceBaked" : "CSP_Diag_Actor_TargetBaked";
            actor.transform.position = includeDriver ? new Vector3(-2f, 1f, 0f) : new Vector3(2f, 1f, 0f);
            actor.transform.rotation = Quaternion.identity;
            actor.transform.localScale = Vector3.one;
            ConfigureDiagnosticActor(actor, actorMaterial, instanceId, true);

            if (includeDriver)
            {
                GameObject driverObject = new GameObject("CSP_DiagnosticDriver");
                SceneManager.MoveGameObjectToScene(driverObject, scene);
                SceneBakedPrefabReuseDiagnostic driver = driverObject.AddComponent<SceneBakedPrefabReuseDiagnostic>();
                ConfigureDriver(driver, processAsDeferredPass, forceAssetBasedReuseDuringDeferred,
                    reuseSceneInstanceFlagInData, setHomeSceneInManualSyntheticData, driverMode,
                    orchestratorGatePendingPrefabsUntilActivated, orchestratorActivationDelayFrames);
                Selection.activeGameObject = driverObject;
            }
            else
            {
                GameObject saveLoadDriverObject = new GameObject("CSP_SaveLoadSlotDiagnosticDriver");
                SceneManager.MoveGameObjectToScene(saveLoadDriverObject, scene);
                SceneBakedPrefabSaveLoadSlotDiagnostic driver = saveLoadDriverObject.AddComponent<SceneBakedPrefabSaveLoadSlotDiagnostic>();
                ConfigureSaveLoadDriver(driver, saveLoadDiagnosticSlot);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void CreateFloor(Material floorMaterial)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "CSP_Diag_Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(1.5f, 1f, 1.5f);

            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = floorMaterial;
            }
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);

            GameObject lightObject = new GameObject("CSP_Diag_DirectionalLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("CSP_Diag_Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = new Vector3(0f, 3.2f, -7f);
            camera.transform.LookAt(new Vector3(0f, 0.8f, 0f));
            camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
        }

        private static void ConfigureDriver(
            SceneBakedPrefabReuseDiagnostic driver,
            bool processAsDeferredPass,
            bool forceAssetBasedReuseDuringDeferred,
            bool reuseSceneInstanceFlagInData,
            bool setHomeSceneInManualSyntheticData,
            SceneBakedPrefabReuseDiagnostic.DriverMode driverMode,
            bool orchestratorGatePendingPrefabsUntilActivated,
            int orchestratorActivationDelayFrames)
        {
            SerializedObject serializedDriver = new SerializedObject(driver);
            SetString(serializedDriver, "diagnosticPrefabAssetID", PrefabAssetId);
            SetString(serializedDriver, "targetSceneName", TargetSceneName);
            SetString(serializedDriver, "unchangedTargetInstanceID", TargetInstanceId);
            SetBool(serializedDriver, "runOnStart", true);
            SetEnum(serializedDriver, "driverMode", (int)driverMode);
            SetBool(serializedDriver, "processAsDeferredPass", processAsDeferredPass);
            SetBool(serializedDriver, "forceAssetBasedReuseDuringDeferred", forceAssetBasedReuseDuringDeferred);
            SetBool(serializedDriver, "reuseSceneInstanceFlagInData", reuseSceneInstanceFlagInData);
            SetBool(serializedDriver, "setHomeSceneInManualSyntheticData", setHomeSceneInManualSyntheticData);
            SetBool(serializedDriver, "orchestratorGatePendingPrefabsUntilActivated", orchestratorGatePendingPrefabsUntilActivated);
            SetInt(serializedDriver, "orchestratorActivationDelayFrames", Mathf.Max(0, orchestratorActivationDelayFrames));
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
        }

        private static void ConfigureSaveLoadDriver(SceneBakedPrefabSaveLoadSlotDiagnostic driver, int saveSlotNumber)
        {
            SerializedObject serializedDriver = new SerializedObject(driver);
            SetString(serializedDriver, "diagnosticPrefabAssetID", PrefabAssetId);
            SetString(serializedDriver, "targetSceneName", TargetSceneName);
            SetString(serializedDriver, "savedInstanceID", SourceInstanceId);
            SetString(serializedDriver, "freshTargetInstanceID", TargetInstanceId);
            SetInt(serializedDriver, "diagnosticSaveSlotNumber", Mathf.Max(1, saveSlotNumber));
            SetBool(serializedDriver, "runOnStart", true);
            SetBool(serializedDriver, "requestDeferredAfterSlotLoad", true);
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
                {
                    return;
                }

                Debug.LogWarning($"[CrystalSave][SceneBakedPrefabDiag] PrefabAssetID '{PrefabAssetId}' is already registered to a different prefab. The diagnostic scene will still be created, but runtime restore will use the active registry entry.");
                return;
            }

            if (!registry.TryAddPrefab(PrefabAssetId, prefabAsset, out _, out string reason))
            {
                Debug.LogWarning($"[CrystalSave][SceneBakedPrefabDiag] Could not register diagnostic prefab: {reason}");
                return;
            }

            EditorUtility.SetDirty(registry);
        }

        private static void EnsureSaveSettingsAvailable()
        {
            SaveSettings settings = Resources.Load<SaveSettings>("SaveSettings");
            if (settings != null)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:SaveSettings");
            if (guids.Length > 0)
            {
                string existingPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                Debug.LogWarning($"[CrystalSave][SceneBakedPrefabDiag] Found SaveSettings at '{existingPath}', but it is not in a Resources path. If your project does not load settings through Addressables, the diagnostic may need a Resources/SaveSettings asset.");
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
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetEnum(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.enumValueIndex = value;
            }
        }
    }
}
#endif
