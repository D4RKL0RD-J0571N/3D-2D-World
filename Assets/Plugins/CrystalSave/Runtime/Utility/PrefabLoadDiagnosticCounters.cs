#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION || (!UNITY_6000_6_OR_NEWER && DEBUG)
#define CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
#endif
#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Deterministic prefab-load counters for editor tests and instrumented player profiling.
    /// Unity 6.6+ uses the managed instrumentation variant; older Unity uses development builds.
    /// Calls compile to no-ops when instrumentation is disabled.
    /// </summary>
    public static class PrefabLoadDiagnosticCounters
    {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
        private static int batchPreparations;
        private static int contextBuilds;
        private static int sceneSnapshots;
        private static int indexedResolutionProbes;
        private static int destroyedIdProbes;
        private static int registrationOwnerProbes;
        private static int prefabDescriptions;
        private static int prefabDataDescriptions;
        private static int transformPathDescriptions;
        private static int rigidbodyProbes;
        private static int publicLookupCalls;
        private static int legacyRegistrationCollectionScans;
        private static int clearCandidateProbes;
        private static int preserveIdClaimProbes;
#endif

#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
        public static int ComponentBlobHierarchyScans { get; private set; }
#else
        public static int ComponentBlobHierarchyScans => 0;
#endif

        internal static void RecordComponentBlobHierarchyScan()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            ComponentBlobHierarchyScans++;
#endif
        }

        public static void Reset()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            ComponentBlobHierarchyScans = 0;
            Interlocked.Exchange(ref batchPreparations, 0);
            Interlocked.Exchange(ref contextBuilds, 0);
            Interlocked.Exchange(ref sceneSnapshots, 0);
            Interlocked.Exchange(ref indexedResolutionProbes, 0);
            Interlocked.Exchange(ref destroyedIdProbes, 0);
            Interlocked.Exchange(ref registrationOwnerProbes, 0);
            Interlocked.Exchange(ref prefabDescriptions, 0);
            Interlocked.Exchange(ref prefabDataDescriptions, 0);
            Interlocked.Exchange(ref transformPathDescriptions, 0);
            Interlocked.Exchange(ref rigidbodyProbes, 0);
            Interlocked.Exchange(ref publicLookupCalls, 0);
            Interlocked.Exchange(ref legacyRegistrationCollectionScans, 0);
            Interlocked.Exchange(ref clearCandidateProbes, 0);
            Interlocked.Exchange(ref preserveIdClaimProbes, 0);
#endif
        }

        public static PrefabLoadDiagnosticSnapshot Capture()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            return new PrefabLoadDiagnosticSnapshot(
                Volatile.Read(ref batchPreparations),
                Volatile.Read(ref contextBuilds),
                Volatile.Read(ref sceneSnapshots),
                Volatile.Read(ref indexedResolutionProbes),
                Volatile.Read(ref destroyedIdProbes),
                Volatile.Read(ref registrationOwnerProbes),
                Volatile.Read(ref prefabDescriptions),
                Volatile.Read(ref prefabDataDescriptions),
                Volatile.Read(ref transformPathDescriptions),
                Volatile.Read(ref rigidbodyProbes),
                Volatile.Read(ref publicLookupCalls),
                Volatile.Read(ref legacyRegistrationCollectionScans),
                Volatile.Read(ref clearCandidateProbes),
                Volatile.Read(ref preserveIdClaimProbes));
#else
            return default;
#endif
        }

        internal static void RecordBatchPreparation()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref batchPreparations);
#endif
        }

        internal static void RecordContextBuild()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref contextBuilds);
#endif
        }

        internal static void RecordSceneSnapshot()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref sceneSnapshots);
#endif
        }

        internal static void RecordIndexedResolutionProbe()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref indexedResolutionProbes);
#endif
        }

        internal static void RecordDestroyedIdProbe()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref destroyedIdProbes);
#endif
        }

        internal static void RecordRegistrationOwnerProbe()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref registrationOwnerProbes);
#endif
        }

        internal static void RecordPrefabDescription()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref prefabDescriptions);
#endif
        }

        internal static void RecordPrefabDataDescription()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref prefabDataDescriptions);
#endif
        }

        internal static void RecordTransformPathDescription()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref transformPathDescriptions);
#endif
        }

        internal static void RecordRigidbodyProbe()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref rigidbodyProbes);
#endif
        }

        internal static void RecordPublicLookupCall()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref publicLookupCalls);
#endif
        }

        internal static void RecordLegacyRegistrationCollectionScan()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref legacyRegistrationCollectionScans);
#endif
        }

        internal static void RecordClearCandidateProbe()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref clearCandidateProbes);
#endif
        }

        internal static void RecordPreserveIdClaimProbe()
        {
#if CRYSTALSAVE_PREFAB_LOAD_DIAGNOSTICS
            Interlocked.Increment(ref preserveIdClaimProbes);
#endif
        }
    }

    public readonly struct PrefabLoadDiagnosticSnapshot
    {
        public PrefabLoadDiagnosticSnapshot(
            int batchPreparations,
            int contextBuilds,
            int sceneSnapshots,
            int indexedResolutionProbes,
            int destroyedIdProbes,
            int registrationOwnerProbes,
            int prefabDescriptions,
            int prefabDataDescriptions,
            int transformPathDescriptions,
            int rigidbodyProbes,
            int publicLookupCalls,
            int legacyRegistrationCollectionScans,
            int clearCandidateProbes,
            int preserveIdClaimProbes)
        {
            BatchPreparations = batchPreparations;
            ContextBuilds = contextBuilds;
            SceneSnapshots = sceneSnapshots;
            IndexedResolutionProbes = indexedResolutionProbes;
            DestroyedIdProbes = destroyedIdProbes;
            RegistrationOwnerProbes = registrationOwnerProbes;
            PrefabDescriptions = prefabDescriptions;
            PrefabDataDescriptions = prefabDataDescriptions;
            TransformPathDescriptions = transformPathDescriptions;
            RigidbodyProbes = rigidbodyProbes;
            PublicLookupCalls = publicLookupCalls;
            LegacyRegistrationCollectionScans = legacyRegistrationCollectionScans;
            ClearCandidateProbes = clearCandidateProbes;
            PreserveIdClaimProbes = preserveIdClaimProbes;
        }

        public int BatchPreparations { get; }
        public int ContextBuilds { get; }
        public int SceneSnapshots { get; }
        public int IndexedResolutionProbes { get; }
        public int DestroyedIdProbes { get; }
        public int RegistrationOwnerProbes { get; }
        public int PrefabDescriptions { get; }
        public int PrefabDataDescriptions { get; }
        public int TransformPathDescriptions { get; }
        public int RigidbodyProbes { get; }
        public int PublicLookupCalls { get; }
        public int LegacyRegistrationCollectionScans { get; }
        public int ClearCandidateProbes { get; }
        public int PreserveIdClaimProbes { get; }
    }
}
#endif
