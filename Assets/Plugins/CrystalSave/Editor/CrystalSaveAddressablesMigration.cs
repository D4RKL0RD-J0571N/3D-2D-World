#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Text;
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;
#if REMEMBERME_ADDRESSABLES_PRESENT
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace Arawn.CrystalSave.Editor
{
    public static class CrystalSaveAddressablesMigration
    {
        public const string CoreAssetsFolder = "Assets/My Settings";

        public static void RunInteractive()
        {
            Run(true);
        }

        public static bool Run(bool showDialog)
        {
#if REMEMBERME_ADDRESSABLES_PRESENT
            MigrationReport report = new MigrationReport();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                report.Errors.Add("Addressables settings were not found. Open Window > Asset Management > Addressables > Groups and create Addressables settings, then run this migration again.");
                Finish(report, showDialog);
                return false;
            }

            if (settings.DefaultGroup == null)
            {
                report.Errors.Add("Addressables has no default group. Create or assign a default Addressables group, then run this migration again.");
                Finish(report, showDialog);
                return false;
            }

            EnsureCoreAssetsFolder(report);

            SaveSettings saveSettings;
            PrefabRegistry prefabRegistry;
            SaveSlotMetadataSO saveSlotMetadata;
            TagRegistry tagRegistry;
            SceneObjectRegistry sceneObjectRegistry;
            MigrationManager migrationManager;

            AssetDatabase.StartAssetEditing();
            try
            {
                saveSettings = MoveOrCreateCoreAsset<SaveSettings>("SaveSettings", report);
                prefabRegistry = MoveOrCreateCoreAsset<PrefabRegistry>("PrefabRegistry", report);
                saveSlotMetadata = MoveOrCreateCoreAsset<SaveSlotMetadataSO>("SaveSlotMetadata", report);
                tagRegistry = MoveOrCreateCoreAsset<TagRegistry>("TagRegistry", report);
                sceneObjectRegistry = MoveOrCreateCoreAsset<SceneObjectRegistry>("SceneObjectRegistry", report);
                migrationManager = MoveOrCreateCoreAsset<MigrationManager>("MigrationManager", report);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();

            ConfigureSaveSettings(saveSettings, saveSlotMetadata, report);

            EnsureAddressable(settings, saveSettings, "SaveSettings", report);
            EnsureAddressable(settings, prefabRegistry, "PrefabRegistry", report);
            EnsureAddressable(settings, saveSlotMetadata, "SaveSlotMetadata", report);
            EnsureAddressable(settings, tagRegistry, "TagRegistry", report);
            EnsureAddressable(settings, sceneObjectRegistry, "SceneObjectRegistry", report);
            EnsureAddressable(settings, migrationManager, "MigrationManager", report);

            ValidatePrefabRegistryAddress(settings, report);
            WarnAboutRemainingResourcesCopies(report);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            report.Actions.Add("Rebuild Addressables content before entering Play Mode or making a build: Window > Asset Management > Addressables > Groups > Build.");
            Finish(report, showDialog);
            return report.Errors.Count == 0;
#else
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Addressables Not Available",
                    "Install the Unity Addressables package before running the Crystal Save Addressables migration.",
                    "OK");
            }

            Debug.LogWarning("[Crystal Save] Addressables migration skipped because the Unity Addressables package is not available.");
            return false;
#endif
        }

#if REMEMBERME_ADDRESSABLES_PRESENT
        private static void EnsureCoreAssetsFolder(MigrationReport report)
        {
            if (AssetDatabase.IsValidFolder(CoreAssetsFolder))
            {
                return;
            }

            AssetDatabase.CreateFolder("Assets", "My Settings");
            report.Actions.Add($"Created {CoreAssetsFolder}.");
        }

        private static T MoveOrCreateCoreAsset<T>(string assetName, MigrationReport report) where T : ScriptableObject
        {
            T asset = FindPreferredAsset<T>(assetName);
            if (asset == null)
            {
                string path = GetAvailableTargetPath(assetName);
                asset = ScriptableObject.CreateInstance<T>();
                asset.name = assetName;
                AssetDatabase.CreateAsset(asset, path);
                report.Actions.Add($"Created {assetName} at {path}.");
                return asset;
            }

            string currentPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(currentPath))
            {
                report.Errors.Add($"{assetName} exists but has no AssetDatabase path.");
                return asset;
            }

            if (!IsInResources(currentPath))
            {
                report.Actions.Add($"Using existing {assetName} at {currentPath}.");
                return asset;
            }

            string destinationPath = GetAvailableTargetPath(assetName);
            string error = AssetDatabase.MoveAsset(currentPath, destinationPath);
            if (!string.IsNullOrEmpty(error))
            {
                report.Errors.Add($"Failed to move {assetName} from {currentPath} to {destinationPath}: {error}");
                return asset;
            }

            T movedAsset = AssetDatabase.LoadAssetAtPath<T>(destinationPath);
            report.Actions.Add($"Moved {assetName} from {currentPath} to {destinationPath}.");
            return movedAsset != null ? movedAsset : asset;
        }

        private static T FindPreferredAsset<T>(string assetName) where T : ScriptableObject
        {
            T resourceAsset = Resources.Load<T>(assetName);
            if (resourceAsset != null)
            {
                return resourceAsset;
            }

            T targetAsset = AssetDatabase.LoadAssetAtPath<T>(GetDefaultTargetPath(assetName));
            if (targetAsset != null)
            {
                return targetAsset;
            }

            T addressableAsset = FindAddressableAsset<T>(assetName);
            if (addressableAsset != null)
            {
                return addressableAsset;
            }

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            T first = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                {
                    continue;
                }

                if (first == null)
                {
                    first = asset;
                }

                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.Equals(fileName, assetName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(asset.name, assetName, StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            return first;
        }

        private static T FindAddressableAsset<T>(string address) where T : UnityEngine.Object
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return null;
            }

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry == null || !string.Equals(entry.address, address, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null)
                    {
                        return asset;
                    }
                }
            }

            return null;
        }

        private static void ConfigureSaveSettings(SaveSettings saveSettings, SaveSlotMetadataSO saveSlotMetadata, MigrationReport report)
        {
            if (saveSettings == null)
            {
                report.Errors.Add("SaveSettings could not be created or resolved.");
                return;
            }

            saveSettings.useAddressables = true;
            saveSettings.tagRegistryKey = "TagRegistry";
            if (saveSettings.defaultSlotMetadata == null && saveSlotMetadata != null)
            {
                saveSettings.defaultSlotMetadata = saveSlotMetadata;
            }

            EditorUtility.SetDirty(saveSettings);
            report.Actions.Add("Enabled Addressables on SaveSettings.");
        }

        private static void EnsureAddressable<T>(
            AddressableAssetSettings settings,
            T asset,
            string address,
            MigrationReport report) where T : UnityEngine.Object
        {
            if (asset == null)
            {
                report.Errors.Add($"{address} could not be created or resolved.");
                return;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                report.Errors.Add($"{address} has no AssetDatabase path.");
                return;
            }

            if (IsInResources(path))
            {
                report.Errors.Add($"{address} is still inside a Resources folder at {path}. Move it outside Resources before using Addressables.");
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                report.Errors.Add($"Could not resolve GUID for {address} at {path}.");
                return;
            }

            AddressableAssetGroup group = settings.DefaultGroup;
            if (group == null)
            {
                report.Errors.Add("Addressables has no default group. Create or assign a default Addressables group, then run this migration again.");
                return;
            }

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, group, false, true);
            }

            if (entry == null)
            {
                report.Errors.Add($"Failed to create Addressables entry for {address} at {path}.");
                return;
            }

            if (!string.Equals(entry.address, address, StringComparison.Ordinal))
            {
                entry.SetAddress(address);
                report.Actions.Add($"Set Addressables address for {path} to {address}.");
            }
            else
            {
                report.Actions.Add($"Verified Addressables address {address} at {path}.");
            }

            WarnDuplicateAddress(settings, address, guid, report);
        }

        private static void ValidatePrefabRegistryAddress(AddressableAssetSettings settings, MigrationReport report)
        {
            List<AddressableAssetEntry> entries = FindEntriesByAddress(settings, "PrefabRegistry");
            if (entries.Count == 0)
            {
                report.Errors.Add("Addressables does not contain an entry with address PrefabRegistry.");
                return;
            }

            foreach (AddressableAssetEntry entry in entries)
            {
                string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(path))
                {
                    report.Errors.Add("Addressables entry PrefabRegistry points to a missing asset.");
                    continue;
                }

                if (IsInResources(path))
                {
                    report.Errors.Add($"Addressables entry PrefabRegistry points to a Resources asset: {path}.");
                    continue;
                }

                PrefabRegistry registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(path);
                if (registry == null)
                {
                    report.Errors.Add($"Addressables entry PrefabRegistry points to {path}, but that asset is not a PrefabRegistry.");
                    continue;
                }

                report.Actions.Add($"Validated PrefabRegistry Addressables entry at {path}.");
            }
        }

        private static void WarnDuplicateAddress(
            AddressableAssetSettings settings,
            string address,
            string expectedGuid,
            MigrationReport report)
        {
            List<AddressableAssetEntry> entries = FindEntriesByAddress(settings, address);
            foreach (AddressableAssetEntry entry in entries)
            {
                if (entry == null || string.Equals(entry.guid, expectedGuid, StringComparison.Ordinal))
                {
                    continue;
                }

                string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                report.Warnings.Add($"Duplicate Addressables address {address} also exists at {path}. Remove or rename the duplicate to avoid ambiguous loads.");
            }
        }

        private static List<AddressableAssetEntry> FindEntriesByAddress(AddressableAssetSettings settings, string address)
        {
            List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();
            if (settings == null)
            {
                return entries;
            }

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry != null && string.Equals(entry.address, address, StringComparison.Ordinal))
                    {
                        entries.Add(entry);
                    }
                }
            }

            return entries;
        }

        private static void WarnAboutRemainingResourcesCopies(MigrationReport report)
        {
            WarnResourcesCopies<SaveSettings>("SaveSettings", report);
            WarnResourcesCopies<PrefabRegistry>("PrefabRegistry", report);
            WarnResourcesCopies<SaveSlotMetadataSO>("SaveSlotMetadata", report);
            WarnResourcesCopies<TagRegistry>("TagRegistry", report);
            WarnResourcesCopies<SceneObjectRegistry>("SceneObjectRegistry", report);
            WarnResourcesCopies<MigrationManager>("MigrationManager", report);
        }

        private static void WarnResourcesCopies<T>(string assetName, MigrationReport report) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsInResources(path))
                {
                    continue;
                }

                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.Equals(fileName, assetName, StringComparison.OrdinalIgnoreCase))
                {
                    report.Warnings.Add($"{assetName} still has a Resources copy at {path}. Addressables mode should use the Addressable asset instead.");
                }
            }
        }

        private static string GetDefaultTargetPath(string assetName)
        {
            return $"{CoreAssetsFolder}/{assetName}.asset";
        }

        private static string GetAvailableTargetPath(string assetName)
        {
            string path = GetDefaultTargetPath(assetName);
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                return path;
            }

            return AssetDatabase.GenerateUniqueAssetPath($"{CoreAssetsFolder}/{assetName} Migrated.asset");
        }

        private static bool IsInResources(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/Resources/", StringComparison.Ordinal) >= 0;
        }

        private static void Finish(MigrationReport report, bool showDialog)
        {
            string message = report.ToMessage();
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    report.Errors.Count == 0 ? "Crystal Save Addressables Migration Complete" : "Crystal Save Addressables Migration Needs Attention",
                    message,
                    "OK");
            }

            if (report.Errors.Count == 0)
            {
                Debug.Log("[Crystal Save] Addressables migration complete.\n" + message);
            }
            else
            {
                Debug.LogError("[Crystal Save] Addressables migration needs attention.\n" + message);
            }
        }

        private sealed class MigrationReport
        {
            public readonly List<string> Actions = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Errors = new List<string>();

            public string ToMessage()
            {
                StringBuilder builder = new StringBuilder();
                AppendSection(builder, "Actions", Actions);
                AppendSection(builder, "Warnings", Warnings);
                AppendSection(builder, "Errors", Errors);

                if (builder.Length == 0)
                {
                    builder.AppendLine("No changes were needed.");
                }

                return builder.ToString();
            }

            private static void AppendSection(StringBuilder builder, string title, List<string> items)
            {
                if (items.Count == 0)
                {
                    return;
                }

                builder.AppendLine(title + ":");
                foreach (string item in items)
                {
                    builder.Append("- ");
                    builder.AppendLine(item);
                }

                builder.AppendLine();
            }
        }
#endif
    }
}
#endif
