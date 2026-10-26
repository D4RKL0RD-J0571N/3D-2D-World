#if MEMORYPACK && ARAWN_REMEMBERME && UNITY_EDITOR
using System;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Immutable target-scoped editor diagnostic counter snapshot.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class PrefabRegistryEditorDiagnosticSnapshot
    {
        internal static readonly PrefabRegistryEditorDiagnosticSnapshot Empty =
            new PrefabRegistryEditorDiagnosticSnapshot();

        internal PrefabRegistryEditorDiagnosticSnapshot(
            long indexBuilds = 0,
            long assetResolutions = 0,
            long lookupRebuilds = 0,
            long inspectionSnapshotBuilds = 0,
            long normalizationPasses = 0,
            long validationFlushes = 0,
            long validationPasses = 0,
            long automaticSaveRequests = 0,
            long prefabBatchFlushes = 0,
            long prefabBatchSaveRequests = 0,
            long standaloneInspectorLayouts = 0,
            long standaloneInspectorRepaints = 0,
            long settingsWindowLayouts = 0,
            long settingsWindowRepaints = 0,
            long settingsRegistryLayouts = 0,
            long settingsRegistryRepaints = 0)
        {
            IndexBuilds = indexBuilds;
            AssetResolutions = assetResolutions;
            LookupRebuilds = lookupRebuilds;
            InspectionSnapshotBuilds = inspectionSnapshotBuilds;
            NormalizationPasses = normalizationPasses;
            ValidationFlushes = validationFlushes;
            ValidationPasses = validationPasses;
            AutomaticSaveRequests = automaticSaveRequests;
            PrefabBatchFlushes = prefabBatchFlushes;
            PrefabBatchSaveRequests = prefabBatchSaveRequests;
            StandaloneInspectorLayouts = standaloneInspectorLayouts;
            StandaloneInspectorRepaints = standaloneInspectorRepaints;
            SettingsWindowLayouts = settingsWindowLayouts;
            SettingsWindowRepaints = settingsWindowRepaints;
            SettingsRegistryLayouts = settingsRegistryLayouts;
            SettingsRegistryRepaints = settingsRegistryRepaints;
        }

        public long IndexBuilds { get; }
        public long AssetResolutions { get; }
        public long LookupRebuilds { get; }
        public long InspectionSnapshotBuilds { get; }
        public long NormalizationPasses { get; }
        public long ValidationFlushes { get; }
        public long ValidationPasses { get; }
        public long AutomaticSaveRequests { get; }
        public long PrefabBatchFlushes { get; }
        public long PrefabBatchSaveRequests { get; }
        public long StandaloneInspectorLayouts { get; }
        public long StandaloneInspectorRepaints { get; }
        public long SettingsWindowLayouts { get; }
        public long SettingsWindowRepaints { get; }
        public long SettingsRegistryLayouts { get; }
        public long SettingsRegistryRepaints { get; }

        /// <summary>
        /// Returns the per-counter difference from an earlier snapshot.
        /// A null snapshot is treated as an all-zero baseline.
        /// </summary>
        public PrefabRegistryEditorDiagnosticSnapshot DeltaFrom(
            PrefabRegistryEditorDiagnosticSnapshot previous)
        {
            previous ??= Empty;
            return new PrefabRegistryEditorDiagnosticSnapshot(
                IndexBuilds - previous.IndexBuilds,
                AssetResolutions - previous.AssetResolutions,
                LookupRebuilds - previous.LookupRebuilds,
                InspectionSnapshotBuilds - previous.InspectionSnapshotBuilds,
                NormalizationPasses - previous.NormalizationPasses,
                ValidationFlushes - previous.ValidationFlushes,
                ValidationPasses - previous.ValidationPasses,
                AutomaticSaveRequests - previous.AutomaticSaveRequests,
                PrefabBatchFlushes - previous.PrefabBatchFlushes,
                PrefabBatchSaveRequests - previous.PrefabBatchSaveRequests,
                StandaloneInspectorLayouts - previous.StandaloneInspectorLayouts,
                StandaloneInspectorRepaints - previous.StandaloneInspectorRepaints,
                SettingsWindowLayouts - previous.SettingsWindowLayouts,
                SettingsWindowRepaints - previous.SettingsWindowRepaints,
                SettingsRegistryLayouts - previous.SettingsRegistryLayouts,
                SettingsRegistryRepaints - previous.SettingsRegistryRepaints);
        }
    }

    /// <summary>
    /// Lightweight counters used by the opt-in PrefabRegistry editor performance diagnostic.
    /// Only work associated with the single active target registry is counted.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class PrefabRegistryEditorDiagnosticCounters
    {
        private static PrefabRegistry activeRegistry;
        private static string allowedPrefabAssetPathPrefix;
        private static MutableCounters counters;

        public static PrefabRegistry ActiveRegistry => activeRegistry;

        public static bool TryBeginSession(PrefabRegistry registry)
        {
            return TryBeginSession(registry, null);
        }

        /// <summary>
        /// Starts a target-scoped session and optionally suppresses automatic prefab
        /// processing for persistent assets outside the diagnostic working folder.
        /// </summary>
        public static bool TryBeginSession(PrefabRegistry registry, string allowedAssetPathPrefix)
        {
            if (registry == null || !object.ReferenceEquals(activeRegistry, null))
                return false;

            activeRegistry = registry;
            allowedPrefabAssetPathPrefix = string.IsNullOrEmpty(allowedAssetPathPrefix)
                ? null
                : allowedAssetPathPrefix.Replace('\\', '/').TrimEnd('/') + "/";
            counters = default;
            return true;
        }

        public static void EndSession(PrefabRegistry registry)
        {
            if (!Matches(registry))
                return;

            activeRegistry = null;
            allowedPrefabAssetPathPrefix = null;
            counters = default;
        }

        /// <summary>
        /// Prevents unrelated prefab validation callbacks from observing the diagnostic's
        /// temporary active-asset override. Non-persistent objects are left untouched.
        /// </summary>
        public static bool ShouldSuppressAutomaticPrefabProcessing(GameObject prefabAsset)
        {
            if (object.ReferenceEquals(activeRegistry, null) ||
                string.IsNullOrEmpty(allowedPrefabAssetPathPrefix) ||
                prefabAsset == null)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            return !string.IsNullOrEmpty(assetPath) &&
                   !assetPath.Replace('\\', '/').StartsWith(
                       allowedPrefabAssetPathPrefix,
                       StringComparison.Ordinal);
        }

        public static PrefabRegistryEditorDiagnosticSnapshot Capture(PrefabRegistry registry)
        {
            if (!Matches(registry))
                return PrefabRegistryEditorDiagnosticSnapshot.Empty;

            return new PrefabRegistryEditorDiagnosticSnapshot(
                counters.IndexBuilds,
                counters.AssetResolutions,
                counters.LookupRebuilds,
                counters.InspectionSnapshotBuilds,
                counters.NormalizationPasses,
                counters.ValidationFlushes,
                counters.ValidationPasses,
                counters.AutomaticSaveRequests,
                counters.PrefabBatchFlushes,
                counters.PrefabBatchSaveRequests,
                counters.StandaloneInspectorLayouts,
                counters.StandaloneInspectorRepaints,
                counters.SettingsWindowLayouts,
                counters.SettingsWindowRepaints,
                counters.SettingsRegistryLayouts,
                counters.SettingsRegistryRepaints);
        }

        public static void RecordIndexBuild(
            PrefabRegistry registry,
            PrefabRegistryIdentityBuildMetrics metrics)
        {
            if (!Matches(registry))
                return;

            counters.IndexBuilds++;
            if (metrics != null)
                counters.AssetResolutions += metrics.AssetResolutionCount;
        }

        public static void RecordLookupRebuild(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.LookupRebuilds++;
        }

        public static void RecordInspectionSnapshotBuild(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.InspectionSnapshotBuilds++;
        }

        public static void RecordNormalizationPass(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.NormalizationPasses++;
        }

        public static void RecordValidationFlush(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.ValidationFlushes++;
        }

        public static void RecordValidationPass(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.ValidationPasses++;
        }

        public static void RecordAutomaticSaveRequest(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.AutomaticSaveRequests++;
        }

        public static void RecordPrefabBatchFlush(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.PrefabBatchFlushes++;
        }

        public static void RecordPrefabBatchSaveRequest(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.PrefabBatchSaveRequests++;
        }

        public static void RecordStandaloneInspectorLayout(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.StandaloneInspectorLayouts++;
        }

        public static void RecordStandaloneInspectorRepaint(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.StandaloneInspectorRepaints++;
        }

        public static void RecordSettingsWindowLayout(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.SettingsWindowLayouts++;
        }

        public static void RecordSettingsWindowRepaint(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.SettingsWindowRepaints++;
        }

        public static void RecordSettingsRegistryLayout(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.SettingsRegistryLayouts++;
        }

        public static void RecordSettingsRegistryRepaint(PrefabRegistry registry)
        {
            if (Matches(registry)) counters.SettingsRegistryRepaints++;
        }

        public static void RecordStandaloneInspectorEvent(PrefabRegistry registry, EventType eventType)
        {
            if (eventType == EventType.Layout)
                RecordStandaloneInspectorLayout(registry);
            else if (eventType == EventType.Repaint)
                RecordStandaloneInspectorRepaint(registry);
        }

        public static void RecordSettingsWindowEvent(PrefabRegistry registry, EventType eventType)
        {
            if (eventType == EventType.Layout)
                RecordSettingsWindowLayout(registry);
            else if (eventType == EventType.Repaint)
                RecordSettingsWindowRepaint(registry);
        }

        public static void RecordSettingsRegistryEvent(PrefabRegistry registry, EventType eventType)
        {
            if (eventType == EventType.Layout)
                RecordSettingsRegistryLayout(registry);
            else if (eventType == EventType.Repaint)
                RecordSettingsRegistryRepaint(registry);
        }

        private static bool Matches(PrefabRegistry registry)
        {
            return !object.ReferenceEquals(registry, null) &&
                   object.ReferenceEquals(activeRegistry, registry);
        }

        private struct MutableCounters
        {
            internal long IndexBuilds;
            internal long AssetResolutions;
            internal long LookupRebuilds;
            internal long InspectionSnapshotBuilds;
            internal long NormalizationPasses;
            internal long ValidationFlushes;
            internal long ValidationPasses;
            internal long AutomaticSaveRequests;
            internal long PrefabBatchFlushes;
            internal long PrefabBatchSaveRequests;
            internal long StandaloneInspectorLayouts;
            internal long StandaloneInspectorRepaints;
            internal long SettingsWindowLayouts;
            internal long SettingsWindowRepaints;
            internal long SettingsRegistryLayouts;
            internal long SettingsRegistryRepaints;
        }
    }
}
#endif
