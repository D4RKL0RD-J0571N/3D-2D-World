#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
    internal static class CrystalSaveUninstallRecovery
    {
        internal const string PendingKey = "CRYSTALSAVE_UNINSTALL_PENDING";
        internal const string EditorFolder = "Assets/Editor";
        internal const string WorkerFolderName = "CrystalSaveUninstallWorker";
        internal const string WorkerFolder = EditorFolder + "/" + WorkerFolderName;
        internal const string WorkerFile = "CrystalSaveUninstaller.cs";
        internal const string AsmdefFile = "CrystalSave.Uninstall.Editor.asmdef";
        internal const string LegacyWorkerPath = EditorFolder + "/" + WorkerFile;
        internal const string LegacyAsmdefPath = EditorFolder + "/" + AsmdefFile;

        [InitializeOnLoadMethod]
        private static void QueueStartupCleanup()
        {
            EditorApplication.delayCall -= CleanupStrandedUninstallWorkerFiles;
            EditorApplication.delayCall += CleanupStrandedUninstallWorkerFiles;
        }

        internal static void CleanupStrandedUninstallWorkerFiles()
        {
            if (SessionState.GetBool(PendingKey, false))
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueStartupCleanup();
                return;
            }

            bool changed = false;
            changed |= DeleteAssetOrFile(LegacyWorkerPath);
            changed |= DeleteAssetOrFile(LegacyAsmdefPath);
            changed |= DeleteAssetOrFile(WorkerFolder);

            if (changed)
            {
                AssetDatabase.Refresh();
                Debug.Log("[Crystal Save] Removed stranded uninstall worker files from the Editor staging area.");
            }
        }

        internal static bool DeleteAssetOrFile(string assetPath)
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
