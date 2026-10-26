#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEngine;
#if UNITY_6000_6_OR_NEWER
using UnityEngine.Assemblies;
#endif
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;      // reflection for NamedBuildTarget.Editor

namespace Arawn.CrystalSave.Editor
{
    /// <summary>
    /// One-shot, destructive uninstaller for Crystal Save.
    /// Runs ONLY when CrystalSaveUninstallMenu calls <see cref="Run"/>.
    /// </summary>
    internal static class CrystalSaveUninstaller
    {
        /*──────── CONFIG ───────────────────────────────────────────────*/
        private const string kDefineSymbolsFolder = "Assets/Plugins/CrystalSave/Editor/DefineSymbols";
        private const string kCrystalSaveRoot     = "Assets/Plugins/CrystalSave";
        private const string kEditorFolder        = "Assets/Editor";
        private const string kWorkerFolder        = "Assets/Editor/CrystalSaveUninstallWorker";
        private const string kWorkerFile          = "CrystalSaveUninstaller.cs";
        private const string kAsmdefName          = "CrystalSave.Uninstall.Editor.asmdef";
        private const string kLegacyWorkerPath    = kEditorFolder + "/" + kWorkerFile;
        private const string kLegacyAsmdefPath    = kEditorFolder + "/" + kAsmdefName;
        private const string kWorkerPath          = kWorkerFolder + "/" + kWorkerFile;
        private const string kWorkerAsmdefPath    = kWorkerFolder + "/" + kAsmdefName;

        private static readonly string[] kSymbolsToRemove =
        {
            "REMEMBERME_STANDARD_PRESENT","REMEMBERME_URP_PRESENT","REMEMBERME_HDRP_PRESENT",
            "MEMORYPACK","BOUNCYCASTLE","ARAWN_REMEMBERME","REMEMBERME_GC2CORE_PRESENT","REMEMBERME_GC2DIALOGUE_PRESENT",
            "REMEMBERME_GC2INVENTORY_PRESENT","REMEMBERME_GC2QUESTS_PRESENT","REMEMBERME_GC2STATS_PRESENT",
            "REMEMBERME_GC2MELEE_PRESENT","REMEMBERME_GC2SHOOTER_PRESENT","REMEMBERME_GC2MAILBOX_PRESENT","REMEMBERME_GC2MODULE_PRESENT",
            "REMEMBERME_CLOUDSAVE_PRESENT","REMEMBERME_AUTHENTICATION_PRESENT","REMEMBERME_CORESERVICES_PRESENT",
            "REMEMBERME_LOCALIZATION_PRESENT","REMEMBERME_NVIDIA_DLSS_PRESENT","REMEMBERME_GOOGLEPLAY_PRESENT",
            "REMEMBERME_APPLE_SIGNIN_PRESENT","REMEMBERME_FACEBOOK_SDK_PRESENT","REMEMBERME_STEAMWORKS_PRESENT",
            "REMEMBERME_EDITOR_COROUTINES_PRESENT","REMEMBERME_ADDRESSABLES_PRESENT","REMEMBERME_NGO_PRESENT",
            "REMEMBERME_FISHNET_PRESENT",
            "NANINOVEL","REMEMBERME_NANINOVEL_PRESENT"
        };

        private static readonly BuildTargetGroup[] kGroups =
        {
            BuildTargetGroup.Standalone, BuildTargetGroup.Android, BuildTargetGroup.iOS,
            BuildTargetGroup.WebGL,      BuildTargetGroup.PS5,     BuildTargetGroup.XboxOne,
            BuildTargetGroup.Switch
        };

        /*──────── ENTRY ───────────────────────────────────────────────*/
        public static void Run()
        {
            EditorApplication.LockReloadAssemblies();
            try
            {
                DeleteDefineFolder();      // ① remove helper scripts
                StripComponents();         // ② wipe scenes & prefabs
                DeleteCrystalSaveRoot();   // ③ delete CrystalSave files
                RemoveDefineSymbols();     // ④ clear scripting-define symbols
            }
            catch (Exception ex)
            {
                Debug.LogError($"CrystalSave Uninstaller: {ex}");
            }
            finally
            {
                try
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                finally { EditorApplication.UnlockReloadAssemblies(); }
            }

            EditorUtility.DisplayDialog(
                "Crystal Save Uninstalled",
                "Crystal Save and all its data have been removed from this project.\n\n"
              + "If Unity was interrupted during the uninstall, delete "
              + "Assets/Editor/CrystalSaveUninstallWorker manually.",
                "OK");

            SelfDestruct();
        }

        /*──────── Phase 1 – Delete DefineSymbols ───────────────────────*/
        private static void DeleteDefineFolder()
        {
            if (AssetDatabase.IsValidFolder(kDefineSymbolsFolder))
                AssetDatabase.DeleteAsset(kDefineSymbolsFolder);
        }

        /*──────── Phase 2 – Strip components ──────────────────────────*/
        private static void StripComponents()
        {
            var crystalTypes = GetCrystalSaveComponentTypes();
            int total = 0;

            /* Prefabs */
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path   = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsOpenForEdit(path, StatusQueryOptions.UseCachedIfPossible))
                    continue;

                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int removed = StripFromGameObject(prefab, crystalTypes);
                    if (removed > 0) PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    total += removed;
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }

            /* Scenes (skip read-only) */
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsOpenForEdit(scenePath, StatusQueryOptions.UseCachedIfPossible))
                    continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int sceneRemoved = 0;
                foreach (var root in scene.GetRootGameObjects())
                    sceneRemoved += StripFromGameObject(root, crystalTypes);

                if (sceneRemoved > 0) EditorSceneManager.MarkSceneDirty(scene);
                total += sceneRemoved;
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"CrystalSave Uninstaller: removed {total} components.");
        }

        private static HashSet<Type> GetCrystalSaveComponentTypes() =>
            new HashSet<Type>(GetLoadedAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(Component).IsAssignableFrom(t) &&
                            t.Namespace?.StartsWith("Arawn.CrystalSave") == true));

        private static IEnumerable<Assembly> GetLoadedAssemblies()
        {
#if UNITY_6000_6_OR_NEWER
            return CurrentAssemblies.GetLoadedAssemblies();
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }

        private static int StripFromGameObject(GameObject go, HashSet<Type> types)
        {
            int removed = 0;
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                if (types.Contains(comp.GetType()))
                {
                    UnityEngine.Object.DestroyImmediate(comp);
                    removed++;
                }
            }
            return removed;
        }

        /*──────── Phase 3 – Delete root folder ───────────────────────*/
        private static void DeleteCrystalSaveRoot()
        {
            if (AssetDatabase.IsValidFolder(kCrystalSaveRoot))
                DeleteFolderRecursively(kCrystalSaveRoot);

            if (Directory.Exists(kCrystalSaveRoot))
            {
                FileUtil.DeleteFileOrDirectory(kCrystalSaveRoot);
                FileUtil.DeleteFileOrDirectory(kCrystalSaveRoot + ".meta");
            }
        }

        private static void DeleteFolderRecursively(string folderAssetPath)
        {
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { folderAssetPath }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path != folderAssetPath) AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.DeleteAsset(folderAssetPath);
        }

        /*──────── Phase 4 – Remove define symbols ─────────────────────*/
        private static void RemoveDefineSymbols()
        {
            /* Player / platform targets */
            foreach (var group in kGroups)
                ClearSymbolsOnTarget(NamedBuildTarget.FromBuildTargetGroup(group));

            /* Editor target (if supported) */
            if (TryGetEditorNamedBuildTarget(out var editorNBT))
                ClearSymbolsOnTarget(editorNBT);
        }

        private static void ClearSymbolsOnTarget(NamedBuildTarget nbt)
        {
            string current = PlayerSettings.GetScriptingDefineSymbols(nbt);
            var list = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (list.RemoveAll(s => kSymbolsToRemove.Contains(s)) == 0) return;
            PlayerSettings.SetScriptingDefineSymbols(nbt, string.Join(";", list));
        }

        private static bool TryGetEditorNamedBuildTarget(out NamedBuildTarget editorNBT)
        {
            FieldInfo field = typeof(NamedBuildTarget).GetField("Editor",
                               BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.FieldType == typeof(NamedBuildTarget))
            {
                editorNBT = (NamedBuildTarget)field.GetValue(null);
                return true;
            }
            editorNBT = default;
            return false;
        }

        /*──────── Final – Self-destruct ───────────────────────────────*/
        private static void SelfDestruct()
        {
            DeleteAssetOrFile(kLegacyWorkerPath);
            DeleteAssetOrFile(kLegacyAsmdefPath);
            DeleteAssetOrFile(kWorkerPath);
            DeleteAssetOrFile(kWorkerAsmdefPath);
            DeleteAssetOrFile(kWorkerFolder);

            AssetDatabase.Refresh();
        }

        private static bool DeleteAssetOrFile(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            bool changed = false;
            if (AssetDatabase.IsValidFolder(assetPath) ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                changed |= AssetDatabase.DeleteAsset(assetPath);
            }

            changed |= DeletePath(assetPath);
            changed |= DeletePath(assetPath + ".meta");
            return changed;
        }

        private static bool DeletePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (!File.Exists(path) && !Directory.Exists(path))
                return false;

            FileUtil.DeleteFileOrDirectory(path);
            return true;
        }
    }
}
#endif
