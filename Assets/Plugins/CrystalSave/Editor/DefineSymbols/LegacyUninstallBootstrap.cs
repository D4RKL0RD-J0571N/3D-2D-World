#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.DefineSymbols.Editor
{
    // This existing independent assembly remains loadable when a legacy root-level
    // uninstall asmdef prevents the predefined Editor assemblies from compiling.
    internal static class LegacyUninstallBootstrap
    {
        private const string LegacyAssembly = "Assets/Editor/CrystalSave.Uninstall.Editor.asmdef";
        private const string LegacyWorker = "Assets/Editor/CrystalSaveUninstaller.cs";

        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string name;
        }

        [InitializeOnLoadMethod]
        private static void RetireInterruptedLegacyWorker()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess() ||
                SessionState.GetBool("CRYSTALSAVE_UNINSTALL_PENDING", false) ||
                !File.Exists(LegacyAssembly))
                return;

            // AssetDatabase is not ready at this boundary. Only touch the exact legacy
            // files, and verify the assembly's owning name before removing its boundary.
            var definition = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(LegacyAssembly));
            if (definition == null || definition.name != "CrystalSave.Uninstall.Editor")
                return;

            File.Delete(LegacyWorker);
            File.Delete(LegacyWorker + ".meta");
            File.Delete(LegacyAssembly);
            File.Delete(LegacyAssembly + ".meta");
            Debug.Log("[Crystal Save] Retired interrupted legacy uninstall assembly and worker.");
        }
    }
}
#endif
