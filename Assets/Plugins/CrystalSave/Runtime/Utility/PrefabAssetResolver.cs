#if MEMORYPACK && ARAWN_REMEMBERME && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
#if REMEMBERME_ADDRESSABLES_PRESENT
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public enum PrefabAssetIdentityDecisionKind
    {
        Invalid,
        RegistryConflict,
        KeepCurrent,
        UsePreferred,
        GenerateNew
    }

    /// <summary>
    /// Pure result of applying Crystal Save's prefab asset identity ownership rules.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class PrefabAssetIdentityDecision
    {
        public PrefabAssetIdentityDecisionKind Kind { get; internal set; }
        public string CurrentId { get; internal set; }
        public string PreferredId { get; internal set; }
        public string ResultId { get; internal set; }
        public bool HasRegistryConflict { get; internal set; }
        public bool CurrentIdInheritedFromVariantSource { get; internal set; }
        public bool CurrentIdOwnedByDifferentAsset { get; internal set; }
        public bool PreferredIdInheritedFromVariantSource { get; internal set; }
        public bool PreferredIdOwnedByDifferentAsset { get; internal set; }
        public bool RegistryIdOverridesCurrent { get; internal set; }
        public bool RequiresMutation =>
            Kind == PrefabAssetIdentityDecisionKind.UsePreferred ||
            Kind == PrefabAssetIdentityDecisionKind.GenerateNew;
    }

    public static class PrefabAssetResolver
    {
        private const string PrefabRegistryResourceKey = "PrefabRegistry";
        private const string DefaultPrefabRegistryPath = "Assets/Plugins/CrystalSave/Resources/PrefabRegistry.asset";

        public static PrefabRegistry LoadActivePrefabRegistry()
        {
            PrefabRegistry overrideRegistry = CrystalSaveOverrides.GetOverride<PrefabRegistry>();
            if (overrideRegistry != null)
                return overrideRegistry;

#if REMEMBERME_ADDRESSABLES_PRESENT
            if (ShouldUseAddressableCoreAssets())
            {
                PrefabRegistry addressableRegistry = LoadAddressableEditorAsset<PrefabRegistry>(PrefabRegistryResourceKey);
                if (addressableRegistry != null)
                    return addressableRegistry;

                return null;
            }
#endif

            PrefabRegistry resourceRegistry = Resources.Load<PrefabRegistry>(PrefabRegistryResourceKey);
            if (resourceRegistry != null)
                return resourceRegistry;

            PrefabRegistry defaultRegistry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(DefaultPrefabRegistryPath);
            if (defaultRegistry != null)
                return defaultRegistry;

            string[] guids = AssetDatabase.FindAssets("t:PrefabRegistry");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                PrefabRegistry registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(path);
                if (registry != null)
                    return registry;
            }

            return null;
        }

#if REMEMBERME_ADDRESSABLES_PRESENT
        public static bool ShouldUseAddressableCoreAssets()
        {
            SaveSettings overrideSettings = CrystalSaveOverrides.GetOverride<SaveSettings>();
            if (overrideSettings != null)
                return overrideSettings.useAddressables;

            SaveSettings addressableSettings = LoadAddressableEditorAsset<SaveSettings>("SaveSettings");
            if (addressableSettings != null)
                return addressableSettings.useAddressables;

            SaveSettings resourceSettings = Resources.Load<SaveSettings>("SaveSettings");
            return resourceSettings != null && resourceSettings.useAddressables;
        }

        public static T LoadAddressableEditorAsset<T>(string address) where T : UnityEngine.Object
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || string.IsNullOrEmpty(address))
                return null;

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null) continue;

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry == null || !string.Equals(entry.address, address, StringComparison.Ordinal))
                        continue;

                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null)
                        return asset;
                }
            }

            return null;
        }
#endif

        public static GameObject ResolveNearestPrefabAsset(GameObject instance)
        {
            if (instance == null) return null;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && instance.scene == stage.scene)
            {
                GameObject stageAsset = LoadPrefabAssetAtPath(stage.assetPath);
                if (stageAsset != null) return stageAsset;
            }

            if (EditorUtility.IsPersistent(instance))
            {
                GameObject persistentAsset = LoadPrefabAssetAtPath(AssetDatabase.GetAssetPath(instance));
                return persistentAsset != null ? persistentAsset : instance;
            }

            string nearestPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
            GameObject nearestAsset = LoadPrefabAssetAtPath(nearestPath);
            if (nearestAsset != null) return nearestAsset;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (source != null && EditorUtility.IsPersistent(source))
            {
                GameObject sourceAsset = LoadPrefabAssetAtPath(AssetDatabase.GetAssetPath(source));
                return sourceAsset != null ? sourceAsset : source;
            }

            return null;
        }

        public static string EnsurePrefabAssetIDForAsset(
            SaveablePrefab saveablePrefab,
            GameObject prefabAsset,
            PrefabRegistry registry,
            string preferredId = null)
        {
            PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.GetOrCreate(registry);
            return EnsurePrefabAssetIDForAsset(saveablePrefab, prefabAsset, index, preferredId);
        }

        /// <summary>
        /// Indexed overload used by registry normalization and delayed prefab identity batches.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string EnsurePrefabAssetIDForAsset(
            SaveablePrefab saveablePrefab,
            GameObject prefabAsset,
            PrefabRegistryIdentityIndex index,
            string preferredId = null)
        {
            if (saveablePrefab == null || prefabAsset == null)
                return null;

            PrefabAssetIdentityDecision decision = EvaluatePrefabAssetIdentity(
                prefabAsset,
                saveablePrefab.PrefabAssetID,
                index,
                preferredId);
            if (decision.Kind == PrefabAssetIdentityDecisionKind.Invalid ||
                decision.Kind == PrefabAssetIdentityDecisionKind.RegistryConflict)
                return null;
            if (decision.Kind == PrefabAssetIdentityDecisionKind.KeepCurrent)
                return decision.ResultId;

            string newId = decision.Kind == PrefabAssetIdentityDecisionKind.UsePreferred
                ? decision.ResultId
                : Guid.NewGuid().ToString();
            Undo.RecordObjects(
                new UnityEngine.Object[] { saveablePrefab, prefabAsset },
                "Assign Prefab Asset ID");
            var serialized = new SerializedObject(saveablePrefab);
            SerializedProperty idProperty = serialized.FindProperty("prefabAssetID");
            if (idProperty != null)
            {
                idProperty.stringValue = newId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(saveablePrefab);
            EditorUtility.SetDirty(prefabAsset);
            index?.ApplyPrefabIdentityMutation(prefabAsset, newId);
            index?.ReserveIdOwner(newId, prefabAsset);
            PrefabRegistryIdentityIndex.NotifyPrefabIdentityChanged(prefabAsset);

            if ((decision.CurrentIdOwnedByDifferentAsset ||
                 decision.CurrentIdInheritedFromVariantSource ||
                 decision.RegistryIdOverridesCurrent) &&
                ShouldLogPrefabAssetIdAssignment())
            {
                string reason = decision.RegistryIdOverridesCurrent
                    ? "it differed from the existing PrefabRegistry entry"
                    : decision.CurrentIdInheritedFromVariantSource
                        ? "it was inherited from the source prefab"
                        : "the inherited ID was already registered to another prefab";

                Logger.Log(
                    $"[Crystal Save] Assigned PrefabAssetID '{newId}' to prefab asset '{prefabAsset.name}' because {reason}.",
                    LogLevel.Info);
            }

            return newId;
        }

        private static bool ShouldLogPrefabAssetIdAssignment()
        {
            return CrystalSaveEditorAutomationSettings.ShouldLogEditorMessage(LogLevel.Info) &&
                   Logger.ShouldLog(LogLevel.Info);
        }

        /// <summary>
        /// Evaluates prefab identity without mutating Unity objects or the registry.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static PrefabAssetIdentityDecision EvaluatePrefabAssetIdentity(
            GameObject prefabAsset,
            string currentId,
            PrefabRegistryIdentityIndex index,
            string preferredId = null)
        {
            var decision = new PrefabAssetIdentityDecision
            {
                CurrentId = currentId,
                PreferredId = preferredId,
                Kind = prefabAsset == null
                    ? PrefabAssetIdentityDecisionKind.Invalid
                    : PrefabAssetIdentityDecisionKind.GenerateNew
            };
            if (prefabAsset == null)
                return decision;

            if (index != null && index.HasConflictingIdsForAsset(prefabAsset))
            {
                decision.Kind = PrefabAssetIdentityDecisionKind.RegistryConflict;
                decision.HasRegistryConflict = true;
                return decision;
            }

            if (index != null && index.TryGetAuthoritativeId(prefabAsset, out string registryId))
                preferredId = registryId;

            decision.PreferredId = preferredId;
            decision.CurrentIdInheritedFromVariantSource =
                IsVariantUsingSourcePrefabAssetID(prefabAsset, currentId, index);
            decision.CurrentIdOwnedByDifferentAsset =
                !string.IsNullOrEmpty(currentId) &&
                index != null &&
                index.IsIdOwnedByDifferentAsset(currentId, prefabAsset);
            decision.PreferredIdInheritedFromVariantSource =
                IsVariantUsingSourcePrefabAssetID(prefabAsset, preferredId, index);
            decision.PreferredIdOwnedByDifferentAsset =
                !string.IsNullOrEmpty(preferredId) &&
                index != null &&
                index.IsIdOwnedByDifferentAsset(preferredId, prefabAsset);
            decision.RegistryIdOverridesCurrent =
                !string.IsNullOrEmpty(currentId) &&
                !string.IsNullOrEmpty(preferredId) &&
                !string.Equals(currentId, preferredId, StringComparison.Ordinal) &&
                !decision.PreferredIdOwnedByDifferentAsset &&
                !decision.PreferredIdInheritedFromVariantSource;

            if (!string.IsNullOrEmpty(currentId) &&
                !decision.CurrentIdOwnedByDifferentAsset &&
                !decision.CurrentIdInheritedFromVariantSource &&
                !decision.RegistryIdOverridesCurrent)
            {
                decision.Kind = PrefabAssetIdentityDecisionKind.KeepCurrent;
                decision.ResultId = currentId;
                return decision;
            }

            if (!string.IsNullOrEmpty(preferredId) &&
                !decision.PreferredIdOwnedByDifferentAsset &&
                !decision.PreferredIdInheritedFromVariantSource)
            {
                decision.Kind = PrefabAssetIdentityDecisionKind.UsePreferred;
                decision.ResultId = preferredId;
                return decision;
            }

            decision.Kind = PrefabAssetIdentityDecisionKind.GenerateNew;
            return decision;
        }

        public static bool HasConflictingRegistryEntriesForAsset(PrefabRegistry registry, GameObject prefabAsset)
        {
            return HasConflictingRegistryEntriesForAsset(
                PrefabRegistryIdentityIndex.GetOrCreate(registry),
                prefabAsset);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool HasConflictingRegistryEntriesForAsset(
            PrefabRegistryIdentityIndex index,
            GameObject prefabAsset)
        {
            return index != null && index.HasConflictingIdsForAsset(prefabAsset);
        }

        private static bool IsVariantUsingSourcePrefabAssetID(
            GameObject prefabAsset,
            string currentId,
            PrefabRegistryIdentityIndex index)
        {
            if (prefabAsset == null || string.IsNullOrEmpty(currentId)) return false;
            if (index != null)
                return index.IsVariantSourceId(prefabAsset, currentId);
            if (PrefabUtility.GetPrefabAssetType(prefabAsset) != PrefabAssetType.Variant) return false;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(prefabAsset);
            if (source == null) return false;

            SaveablePrefab sourceSaveable = source.GetComponent<SaveablePrefab>();
            return sourceSaveable != null &&
                   string.Equals(sourceSaveable.PrefabAssetID, currentId, StringComparison.Ordinal);
        }

        private static GameObject LoadPrefabAssetAtPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool AreSamePrefabAsset(GameObject left, GameObject right)
        {
            if (left == null || right == null) return false;
            if (left == right) return true;
            return PrefabRegistryIdentityIndex.Build(null).AreSameAsset(left, right);
        }
    }
}
#endif
