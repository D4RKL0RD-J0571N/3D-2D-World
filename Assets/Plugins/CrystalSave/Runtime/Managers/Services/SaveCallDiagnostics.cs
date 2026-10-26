#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using SaveDataCallRoute = Arawn.CrystalSave.Runtime.SaveableComponent.SaveDataCallRoute;
using SaveCallCallerRecord = Arawn.CrystalSave.Runtime.SaveableComponent.SaveCallCallerRecord;
using SaveCallDiagnosticSnapshot = Arawn.CrystalSave.Runtime.SaveableComponent.SaveCallDiagnosticSnapshot;
using DuplicateSaveCallDiagnostic = Arawn.CrystalSave.Runtime.SaveableComponent.DuplicateSaveCallDiagnostic;
using SaveCallCallerDiagnostic = Arawn.CrystalSave.Runtime.SaveableComponent.SaveCallCallerDiagnostic;
using SaveCallOccurrenceDiagnostic = Arawn.CrystalSave.Runtime.SaveableComponent.SaveCallOccurrenceDiagnostic;

namespace Arawn.CrystalSave.Runtime
{
    // Owns the persisted-save diagnostic cycle, route scopes and caller records.
    // Component lifecycle/identity stay on SaveableComponent; diagnostics only observe them.
    // The cycle is shared by all helpers, while nested invocation routes remain thread-local.
    internal static class SaveCallDiagnostics
    {
        // Track SaveData calls only while a persisted save is synchronously collecting data.
        // Snapshot, TimeMachine, screenshot, and other incidental SaveData calls occur outside
        // this narrow window and must not contaminate duplicate-save diagnostics.
        private static readonly object saveCallDiagnosticsLock = new();
        private static readonly Dictionary<string, SaveCallRecord> saveCallRecords =
            new(StringComparer.Ordinal);
        private static readonly HashSet<string> untracedDuplicateSaveKeys =
            new(StringComparer.Ordinal);
        private static volatile bool saveCallDiagnosticsActive;
        private static int saveCallOrder;
        private static string activeSaveCallOperation;
        private static bool deepSaveCallTracingActive;
        private static Action<SaveCallDiagnosticSnapshot> pendingSaveCallCapture;
        private static Action<SaveCallDiagnosticSnapshot> activeSaveCallCapture;
        private static Action<string> pendingSaveCallAbortCapture;
        private static Action<string> activeSaveCallAbortCapture;

        [ThreadStatic]
        private static bool saveDataCallRouteActive;

        [ThreadStatic]
        private static SaveDataCallRoute currentSaveDataCallRoute;

        [ThreadStatic]
        private static SaveableComponent currentSaveDataExpectedComponent;

        [ThreadStatic]
        private static SaveablePrefab currentSaveDataPrefabOwner;

        [ThreadStatic]
        private static bool currentSaveDataInvocationPreRecorded;

        [ThreadStatic]
        private static bool currentSaveDataBaseEntryObserved;

        [ThreadStatic]
        private static int saveDataInvocationDepth;

        internal static bool IsSaveCallDiagnosticsActive =>
            saveCallDiagnosticsActive;

        internal static void BeginSaveCallDiagnostics()
        {
            BeginSaveCallDiagnostics(null, false);
        }

        internal static void BeginSaveCallDiagnostics(string operationName)
        {
            BeginSaveCallDiagnostics(operationName, false);
        }

        internal static void BeginSaveCallDiagnostics(
            string operationName,
            bool forceDeepTracing)
        {
            lock (saveCallDiagnosticsLock)
            {
                saveCallRecords.Clear();
                untracedDuplicateSaveKeys.Clear();
                saveCallOrder = 0;
                activeSaveCallOperation = operationName;
                activeSaveCallCapture = pendingSaveCallCapture;
                activeSaveCallAbortCapture = pendingSaveCallAbortCapture;
                pendingSaveCallCapture = null;
                pendingSaveCallAbortCapture = null;
                deepSaveCallTracingActive =
                    forceDeepTracing || activeSaveCallCapture != null;
                saveCallDiagnosticsActive = true;
            }
        }

        internal static SaveCallDiagnosticSnapshot CompleteSaveCallDiagnostics()
        {
            SaveCallDiagnosticSnapshot snapshot;
            Action<SaveCallDiagnosticSnapshot> capture;

            lock (saveCallDiagnosticsLock)
            {
                if (!saveCallDiagnosticsActive)
                    return SaveCallDiagnosticSnapshot.Empty;

                saveCallDiagnosticsActive = false;
                int uniqueKeyCount =
                    saveCallRecords.Count +
                    untracedDuplicateSaveKeys.Count(
                        key => !saveCallRecords.ContainsKey(key));
                List<DuplicateSaveCallDiagnostic> duplicates = null;
                List<SaveCallOccurrenceDiagnostic> calls =
                    deepSaveCallTracingActive
                        ? new List<SaveCallOccurrenceDiagnostic>(saveCallOrder)
                        : null;

                foreach (var pair in saveCallRecords)
                {
                    if (calls != null)
                        AppendCallDiagnostics(pair.Key, pair.Value, calls);

                    if (pair.Value.TotalCalls <= 1)
                        continue;

                    duplicates ??= new List<DuplicateSaveCallDiagnostic>();
                    duplicates.Add(CreateDuplicateDiagnostic(pair.Key, pair.Value));
                }

                duplicates?.Sort(
                    (left, right) => string.CompareOrdinal(
                        left.CompositeKey,
                        right.CompositeKey));
                calls?.Sort((left, right) => left.CallOrder.CompareTo(right.CallOrder));
                snapshot = new SaveCallDiagnosticSnapshot(
                    activeSaveCallOperation,
                    uniqueKeyCount,
                    duplicates ?? new List<DuplicateSaveCallDiagnostic>(),
                    calls ?? new List<SaveCallOccurrenceDiagnostic>(),
                    untracedDuplicateSaveKeys
                        .OrderBy(key => key, StringComparer.Ordinal)
                        .ToList());
                capture = activeSaveCallCapture;

                saveCallRecords.Clear();
                untracedDuplicateSaveKeys.Clear();
                saveCallOrder = 0;
                activeSaveCallOperation = null;
                activeSaveCallCapture = null;
                activeSaveCallAbortCapture = null;
                deepSaveCallTracingActive = false;
            }

            if (capture != null)
            {
                try
                {
                    capture(snapshot);
                }
                catch (Exception ex)
                {
                    // A diagnostic observer must never make a save fail.
                    Logger.Log(
                        $"SaveData collection diagnostic observer failed: {ex.Message}",
                        LogCategory.SaveManager,
                        LogLevel.Warning);
                }
            }

            return snapshot;
        }

        internal static void AbortSaveCallDiagnostics()
        {
            AbortSaveCallDiagnosticsWithReason(null);
        }

        internal static void AbortSaveCallDiagnosticsWithReason(string reason)
        {
            Action<string> abortedCapture;
            lock (saveCallDiagnosticsLock)
            {
                abortedCapture = activeSaveCallAbortCapture;
                saveCallDiagnosticsActive = false;
                saveCallRecords.Clear();
                untracedDuplicateSaveKeys.Clear();
                saveCallOrder = 0;
                activeSaveCallOperation = null;
                activeSaveCallCapture = null;
                activeSaveCallAbortCapture = null;
                deepSaveCallTracingActive = false;
            }

            ResetSaveDataCallRouteState();
            saveDataInvocationDepth = 0;

            if (abortedCapture != null)
            {
                try
                {
                    abortedCapture(
                        string.IsNullOrEmpty(reason)
                            ? "The save-data collection was aborted."
                            : reason);
                }
                catch (Exception ex)
                {
                    Logger.Log(
                        $"SaveData collection diagnostic abort observer failed: {ex.Message}",
                        LogCategory.SaveManager,
                        LogLevel.Warning);
                }
            }
        }

        internal static void ArmNextSaveCallDiagnosticCapture(
            Action<SaveCallDiagnosticSnapshot> capture)
        {
            ArmNextSaveCallDiagnosticCaptureWithAbort(capture, null);
        }

        internal static void ArmNextSaveCallDiagnosticCaptureWithAbort(
            Action<SaveCallDiagnosticSnapshot> capture,
            Action<string> aborted)
        {
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));

            lock (saveCallDiagnosticsLock)
            {
                pendingSaveCallCapture = capture;
                pendingSaveCallAbortCapture = aborted;
            }
        }

        internal static void CancelNextSaveCallDiagnosticCapture()
        {
            lock (saveCallDiagnosticsLock)
            {
                pendingSaveCallCapture = null;
                activeSaveCallCapture = null;
                pendingSaveCallAbortCapture = null;
                activeSaveCallAbortCapture = null;
                deepSaveCallTracingActive = false;
            }
        }

        internal static SaveDataCallRouteScope BeginSaveDataCallRoute(
            SaveDataCallRoute route,
            SaveableComponent expectedComponent = null,
            SaveablePrefab prefabOwner = null)
        {
            if (!saveCallDiagnosticsActive)
                return default;

            var scope = new SaveDataCallRouteScope(
                saveDataCallRouteActive,
                currentSaveDataCallRoute,
                currentSaveDataExpectedComponent,
                currentSaveDataPrefabOwner,
                currentSaveDataInvocationPreRecorded,
                currentSaveDataBaseEntryObserved);
            saveDataCallRouteActive = true;
            currentSaveDataCallRoute = route;
            currentSaveDataExpectedComponent = expectedComponent;
            currentSaveDataPrefabOwner = prefabOwner;
            currentSaveDataInvocationPreRecorded = false;
            currentSaveDataBaseEntryObserved = false;
            return scope;
        }

        internal static byte[] InvokeSaveDataWithRoute(
            ISaveable component,
            SaveDataCallRoute route,
            SaveablePrefab prefabOwner = null)
        {
            if (component is not SaveableComponent saveableComponent)
                return component?.SaveData();

            using SaveDataCallRouteScope routeScope =
                BeginSaveDataCallRoute(route, saveableComponent, prefabOwner);
            if (saveCallDiagnosticsActive)
            {
                bool reentrant = saveDataInvocationDepth > 0;
                RecordSaveCall(saveableComponent, reentrant);
                currentSaveDataInvocationPreRecorded = true;
            }

            return component.SaveData();
        }

        /// <summary>
        /// Records a collection entry that deliberately reuses preserved bytes instead of
        /// invoking <see cref="SaveableComponent.SaveData"/>. This keeps deferred/pending key collisions
        /// visible to the same diagnostic that traces live serialization calls.
        /// </summary>
        internal static bool RecordSaveDataCollectionWrite(
            SaveableComponent component,
            SaveDataCallRoute route)
        {
            if (route != SaveDataCallRoute.DeferredGlobalPayload &&
              route != SaveDataCallRoute.PendingGlobalPayload)
            {
                return false;
            }

            try
            {
                return RecordSaveCallCore(component, false, route);
            }
            catch
            {
                // Collection diagnostics must never prevent preserved data from
                // being written. The caller retains its generic warning fallback.
                return false;
            }
        }

        /// <summary>
        /// Marks a key collision that could not be attributed to two
        /// <see cref="SaveableComponent"/> trace events (for example a custom
        /// <see cref="ISaveable"/> implementation). This prevents an armed capture from
        /// reporting a false clean result while the generic manager warning supplies the
        /// available caller context.
        /// </summary>
        internal static void RecordUntracedSaveDataCollectionCollision(
            string compositeKey)
        {
            if (!saveCallDiagnosticsActive ||
              string.IsNullOrEmpty(compositeKey))
            {
                return;
            }

            lock (saveCallDiagnosticsLock)
            {
                if (saveCallDiagnosticsActive)
                    untracedDuplicateSaveKeys.Add(compositeKey);
            }
        }

        internal static void ReportSaveCallDiagnostics(
            string operationName,
            SaveCallDiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            try
            {
                foreach (DuplicateSaveCallDiagnostic duplicate in snapshot.Duplicates)
                {
                    string message = FormatSaveCallDiagnostic(operationName, duplicate);
                    SaveableComponent context = FindFirstLiveCaller(duplicate);
                    if (context != null)
                    {
                        Logger.Log(
                            message,
                            context,
                            LogCategory.SaveManager,
                            LogLevel.Warning);
                    }
                    else
                    {
                        Logger.Log(
                            message,
                            LogCategory.SaveManager,
                            LogLevel.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                // Diagnostics must never make an otherwise valid save fail.
                Logger.Log(
                    $"{operationName ?? "Save"}: Failed to format duplicate SaveData diagnostics: {ex.Message}",
                    LogCategory.SaveManager,
                    LogLevel.Warning);
            }
            finally
            {
                // Async save methods can retain locals in their state machine. Release
                // Unity references explicitly once the clickable warnings are emitted.
                snapshot.ReleaseComponentReferences();
            }
        }

        internal static string FormatSaveCallDiagnostic(
            string operationName,
            DuplicateSaveCallDiagnostic duplicate)
        {
            if (duplicate == null)
                return $"{operationName ?? "Save"}: Duplicate SaveData call details were unavailable.";

            string label = string.IsNullOrEmpty(operationName) ? "Save" : operationName;
            int callerCount = duplicate.Callers.Count;
            bool includesPreservedPayload =
                duplicate.Occurrences.Any(
                    occurrence =>
                        occurrence.Route ==
                            SaveDataCallRoute.DeferredGlobalPayload ||
                        occurrence.Route ==
                            SaveDataCallRoute.PendingGlobalPayload);
            var builder = new StringBuilder(512);
            builder.Append(label)
                .Append(
                    includesPreservedPayload
                        ? ": Duplicate save-data collection entries for '"
                        : ": Duplicate SaveData calls for '")
                .Append(duplicate.CompositeKey)
                .Append("' (")
                .Append(duplicate.TotalCalls)
                .Append(includesPreservedPayload ? " collection events from " : " calls from ")
                .Append(callerCount)
                .Append(callerCount == 1 ? " component instance)." : " component instances).")
                .AppendLine()
                .AppendLine(
                    "This is a component save key (GameObject UniqueID + ComponentID), not a PrefabRegistry ID.")
                .AppendLine("Callers:");

            foreach (SaveCallCallerDiagnostic caller in duplicate.Callers)
            {
                builder.Append(" - ")
                    .Append(caller.CallCount)
                    .Append("x ")
                    .Append(DescribeSaveCallCaller(caller));

                if (!string.IsNullOrEmpty(caller.RouteSummary))
                {
                    builder.Append(" Routes: ")
                        .Append(caller.RouteSummary)
                        .Append('.');
                }

                builder
                    .AppendLine();
            }

            if (callerCount <= 1)
            {
                bool crossedPackageRoutes =
                    duplicate.Occurrences.Any(
                        occurrence =>
                            occurrence.Route ==
                            SaveDataCallRoute.GlobalComponentCollection) &&
                    duplicate.Occurrences.Any(
                        occurrence =>
                            occurrence.Route ==
                            SaveDataCallRoute.PrefabComponentBlob);
                bool customOrUnknown =
                    duplicate.Occurrences.Any(
                        occurrence =>
                            occurrence.Route ==
                                SaveDataCallRoute.CustomOrReentrant ||
                            occurrence.Route ==
                                SaveDataCallRoute.Unknown);

                if (includesPreservedPayload)
                {
                    builder.Append(
                        "The same component key was written more than once. At least one " +
                        "collection entry reused preserved deferred or pending " +
                        "bytes without invoking SaveData(). Check the listed route order and " +
                        "why the same composite key was reached more than once.");
                }
                else if (crossedPackageRoutes)
                {
                    builder.Append(
                        "The same component instance was serialized more than once. ");
                    builder.Append(
                        "It was reached through both the global ComponentManager collection " +
                        "and SaveablePrefab component-blob capture. Check Track Component Blobs " +
                        "ownership and registration reconciliation.");
                }
                else if (customOrUnknown)
                {
                    builder.Append(
                        "The same component instance was serialized more than once. ");
                    builder.Append(
                        "At least one call was custom, re-entrant, or outside a recognized " +
                        "package route. Check SaveData overrides and onSaving callbacks; arm " +
                        "Capture Next Real Save for call order and targeted stack details.");
                }
                else
                {
                    builder.Append(
                        "The same component instance was serialized more than once. ");
                    builder.Append(
                        "The same collection route reached it repeatedly. Use the route and " +
                        "call-order details to find the duplicate traversal.");
                }
            }
            else
            {
                if (includesPreservedPayload)
                {
                    builder.Append(
                        "One or more entries reused preserved deferred or pending bytes " +
                        "without invoking SaveData(). ");
                }

                builder.Append(
                    "Different component instances share this save key, so later data can overwrite earlier data. " +
                    "For runtime objects, prefer SaveablePrefabFactory or the existing fresh-ID spawn path. " +
                    "Regenerate one component's Component ID or its owning GameObject UniqueID only when doing so cannot orphan existing save data.");
            }

            return builder.ToString();
        }

        internal static SaveDataInvocationScope BeginSaveDataInvocation(
            SaveableComponent component)
        {
            if (!saveCallDiagnosticsActive || component == null)
                return default;

            bool reentrant = saveDataInvocationDepth > 0;
            saveDataInvocationDepth++;
            bool wasPreRecorded =
                saveDataCallRouteActive &&
                currentSaveDataInvocationPreRecorded &&
                !currentSaveDataBaseEntryObserved &&
                ReferenceEquals(currentSaveDataExpectedComponent, component);
            if (wasPreRecorded)
            {
                currentSaveDataBaseEntryObserved = true;
            }
            else
            {
                RecordSaveCall(component, reentrant);
            }

            return new SaveDataInvocationScope(true);
        }

        private static void RecordSaveCall(
            SaveableComponent component,
            bool reentrant)
        {
            RecordSaveCallCore(component, reentrant, null);
        }

        private static bool RecordSaveCallCore(
            SaveableComponent component,
            bool reentrant,
            SaveDataCallRoute? explicitRoute)
        {
            if (!saveCallDiagnosticsActive || component == null)
                return false;

            string compositeKey = component.UniqueIdentifier;
            if (string.IsNullOrEmpty(compositeKey))
                return false;

            lock (saveCallDiagnosticsLock)
            {
                if (!saveCallDiagnosticsActive)
                    return false;

                saveCallRecords.TryGetValue(compositeKey, out SaveCallRecord record);
                SaveDataCallRoute route;
                if (explicitRoute.HasValue)
                {
                    route = explicitRoute.Value;
                }
                else
                {
                    route = SaveDataCallRoute.Unknown;
                    if (reentrant ||
                      (saveDataCallRouteActive &&
                      currentSaveDataExpectedComponent != null &&
                      !ReferenceEquals(
                          currentSaveDataExpectedComponent,
                          component)))
                    {
                        route = SaveDataCallRoute.CustomOrReentrant;
                    }
                    else if (saveDataCallRouteActive)
                    {
                        route = currentSaveDataCallRoute;
                    }
                }

                string stackTrace = null;
                if (deepSaveCallTracingActive &&
                  (route == SaveDataCallRoute.Unknown ||
                  route == SaveDataCallRoute.CustomOrReentrant))
                {
                    stackTrace = Environment.StackTrace;
                }

                ComponentManager componentManager =
                    ComponentManager.Instance;
                bool resolvedBlobOwnership = false;
                SaveablePrefab resolvedNearestPrefab = null;
                if (componentManager != null)
                {
                    resolvedBlobOwnership =
                        componentManager
                            .TryResolveComponentBlobOwnershipForDiagnostics(
                                component,
                                out resolvedNearestPrefab);
                }
                else
                {
                    resolvedNearestPrefab =
                        component.GetComponentInParent<SaveablePrefab>(
                            true);
                    resolvedBlobOwnership =
                        resolvedNearestPrefab != null &&
                        resolvedNearestPrefab
                            .EffectiveTrackComponentBlobs;
                }

                var occurrence = new SaveCallOccurrenceRecord
                {
                    CallOrder = ++saveCallOrder,
                    Component = component,
                    Route = route,
                    RouteOwner = explicitRoute.HasValue
                        ? null
                        : currentSaveDataPrefabOwner,
                    NearestPrefab = resolvedNearestPrefab,
                    BlobOwned =
                        route == SaveDataCallRoute.PrefabComponentBlob ||
                        resolvedBlobOwnership,
                    GloballyRegistered =
                        componentManager
                            ?.IsGloballyRegisteredForDiagnostics(component) ==
                        true,
                    RegistrationSource =
                        component.LastManagerRegistrationSource ??
                        string.Empty,
                    StackTrace = stackTrace
                };

                if (deepSaveCallTracingActive)
                    CaptureOccurrenceLocation(component, ref occurrence);
                else if (record.TotalCalls > 0)
                {
                    // Build hierarchy descriptions only once a real duplicate exists.
                    // The first occurrence remains inline so clean saves allocate no list.
                    record.CaptureFirstOccurrenceLocationIfNeeded();
                    CaptureOccurrenceLocation(component, ref occurrence);
                }

                record.Add(component, occurrence, deepSaveCallTracingActive);
                saveCallRecords[compositeKey] = record;
                return true;
            }
        }

        private static void CaptureOccurrenceLocation(
            SaveableComponent component,
            ref SaveCallOccurrenceRecord occurrence)
        {
            if (component == null)
                return;

            try
            {
                GameObject target = component.gameObject;
                occurrence.HierarchyPath =
                    BuildFullHierarchyPath(target.transform);
                Scene scene = target.scene;
                occurrence.SceneName =
                    scene.IsValid() && !string.IsNullOrEmpty(scene.name)
                        ? scene.name
                        : "<no scene>";
                occurrence.ScenePath =
                    scene.IsValid() ? scene.path ?? string.Empty : string.Empty;
            }
            catch (Exception)
            {
                occurrence.HierarchyPath = string.Empty;
                occurrence.SceneName = string.Empty;
                occurrence.ScenePath = string.Empty;
            }
        }

        private static SaveCallCallerRecord CreateCallerRecord(SaveableComponent component)
        {
            string gameObjectId = component.GameObjectUniqueID;
            if (gameObjectId == null)
            {
                try
                {
                    gameObjectId = component.gameObject != null
                        ? component.gameObject.name
                        : string.Empty;
                }
                catch (MissingReferenceException)
                {
                    gameObjectId = string.Empty;
                }
            }

            return new SaveCallCallerRecord
            {
                Component = component,
                ComponentType = component.GetType(),
                UnityObjectId = UnityObjectHelper.GetUniqueId(component),
                GameObjectId = gameObjectId,
                ComponentId = component.ComponentID ?? string.Empty,
                CallCount = 1
            };
        }

        private static DuplicateSaveCallDiagnostic CreateDuplicateDiagnostic(
            string compositeKey,
            SaveCallRecord record)
        {
            var callers = new List<SaveCallCallerDiagnostic>(
                1 + (record.AdditionalCallers?.Count ?? 0))
            {
                CreateCallerDiagnostic(record.FirstCaller, record.Occurrences)
            };

            if (record.AdditionalCallers != null)
            {
                foreach (SaveCallCallerRecord caller in record.AdditionalCallers)
                    callers.Add(CreateCallerDiagnostic(caller, record.Occurrences));
            }

            var occurrences = new List<SaveCallOccurrenceDiagnostic>(
                record.Occurrences?.Count ?? 0);
            AppendCallDiagnostics(compositeKey, record, occurrences);
            occurrences.Sort((left, right) => left.CallOrder.CompareTo(right.CallOrder));

            return new DuplicateSaveCallDiagnostic(
                compositeKey,
                record.TotalCalls,
                callers,
                occurrences);
        }

        private static void AppendCallDiagnostics(
            string compositeKey,
            SaveCallRecord record,
            List<SaveCallOccurrenceDiagnostic> destination)
        {
            if (record.Occurrences == null)
                return;

            foreach (SaveCallOccurrenceRecord occurrence in record.Occurrences)
            {
                SaveCallCallerDiagnostic caller = CreateCallerDiagnostic(
                    FindCallerRecord(record, occurrence.Component),
                    record.Occurrences,
                    occurrence);
                destination.Add(
                    new SaveCallOccurrenceDiagnostic(
                        compositeKey,
                        occurrence.CallOrder,
                        occurrence.Route,
                        caller,
                        GetSafePrefabName(occurrence.RouteOwner),
                        GetSafePrefabInstanceId(occurrence.RouteOwner),
                        GetSafePrefabAssetId(occurrence.RouteOwner),
                        occurrence.StackTrace));
            }
        }

        private static SaveCallCallerRecord FindCallerRecord(
            SaveCallRecord record,
            SaveableComponent component)
        {
            if (ReferenceEquals(record.FirstCaller.Component, component))
                return record.FirstCaller;

            if (record.AdditionalCallers != null)
            {
                foreach (SaveCallCallerRecord caller in record.AdditionalCallers)
                {
                    if (ReferenceEquals(caller.Component, component))
                        return caller;
                }
            }

            return CreateCallerRecord(component);
        }

        private static SaveCallCallerDiagnostic CreateCallerDiagnostic(
            SaveCallCallerRecord caller,
            List<SaveCallOccurrenceRecord> occurrences,
            SaveCallOccurrenceRecord? preferredOccurrence = null)
        {
            string hierarchyPath = string.Empty;
            string sceneName = string.Empty;
            string scenePath = string.Empty;
            string nearestPrefabName = string.Empty;
            string nearestPrefabInstanceId = string.Empty;
            string nearestPrefabAssetId = string.Empty;
            bool blobOwned = false;
            bool globallyRegistered = false;
            string registrationSource = string.Empty;

            SaveableComponent component = caller.Component;
            SaveCallOccurrenceRecord? firstOccurrence = preferredOccurrence;
            if (!firstOccurrence.HasValue && occurrences != null)
            {
                foreach (SaveCallOccurrenceRecord occurrence in occurrences)
                {
                    if (ReferenceEquals(occurrence.Component, component))
                    {
                        firstOccurrence = occurrence;
                        break;
                    }
                }
            }

            if (firstOccurrence.HasValue)
            {
                SaveCallOccurrenceRecord occurrence = firstOccurrence.Value;
                hierarchyPath = occurrence.HierarchyPath ?? string.Empty;
                sceneName = occurrence.SceneName ?? string.Empty;
                scenePath = occurrence.ScenePath ?? string.Empty;
                nearestPrefabName = GetSafePrefabName(occurrence.NearestPrefab);
                nearestPrefabInstanceId =
                    GetSafePrefabInstanceId(occurrence.NearestPrefab);
                nearestPrefabAssetId =
                    GetSafePrefabAssetId(occurrence.NearestPrefab);
                blobOwned = occurrence.BlobOwned;
                globallyRegistered = occurrence.GloballyRegistered;
                registrationSource = occurrence.RegistrationSource ?? string.Empty;
            }

            if (component != null && string.IsNullOrEmpty(hierarchyPath))
            {
                try
                {
                    GameObject target = component.gameObject;
                    hierarchyPath = BuildFullHierarchyPath(target.transform);
                    Scene scene = target.scene;
                    sceneName = scene.IsValid() && !string.IsNullOrEmpty(scene.name)
                        ? scene.name
                        : "<no scene>";
                    scenePath = scene.IsValid() ? scene.path ?? string.Empty : string.Empty;

                    if (!firstOccurrence.HasValue)
                    {
                        SaveablePrefab nearestPrefab =
                            component.NearestOwningPrefab;
                        if (nearestPrefab != null)
                        {
                            nearestPrefabName =
                                GetSafePrefabName(nearestPrefab);
                            nearestPrefabInstanceId =
                                GetSafePrefabInstanceId(nearestPrefab);
                            nearestPrefabAssetId =
                                GetSafePrefabAssetId(nearestPrefab);
                            blobOwned =
                                component.PrefabHandlesSerialization;
                        }

                        globallyRegistered =
                            ComponentManager.Instance
                                ?.IsGloballyRegisteredForDiagnostics(
                                    component) == true;
                        registrationSource =
                            component.LastManagerRegistrationSource ??
                            string.Empty;
                    }
                }
                catch (Exception)
                {
                    hierarchyPath = string.Empty;
                    sceneName = string.Empty;
                    scenePath = string.Empty;
                }
            }

            var routeCounts = new Dictionary<SaveDataCallRoute, int>();
            if (occurrences != null)
            {
                foreach (SaveCallOccurrenceRecord occurrence in occurrences)
                {
                    if (!ReferenceEquals(occurrence.Component, component))
                        continue;

                    routeCounts.TryGetValue(occurrence.Route, out int count);
                    routeCounts[occurrence.Route] = count + 1;
                }
            }

            string routeSummary = string.Join(
                ", ",
                routeCounts
                    .OrderBy(pair => (int)pair.Key)
                    .Select(pair => $"{pair.Key} x{pair.Value}"));

            return new SaveCallCallerDiagnostic(
                caller,
                hierarchyPath,
                sceneName,
                scenePath,
                nearestPrefabName,
                nearestPrefabInstanceId,
                nearestPrefabAssetId,
                blobOwned,
                globallyRegistered,
                registrationSource,
                routeSummary);
        }

        internal static SaveableComponent FindFirstLiveCaller(
            DuplicateSaveCallDiagnostic duplicate)
        {
            foreach (SaveCallCallerDiagnostic caller in duplicate.Callers)
            {
                if (caller.Component != null)
                    return caller.Component;
            }

            return null;
        }

        private static string DescribeSaveCallCaller(SaveCallCallerDiagnostic caller)
        {
            string typeName = caller.ComponentType?.FullName ?? "<unknown component type>";
            string objectLocation = string.IsNullOrEmpty(caller.HierarchyPath)
                ? "<destroyed or unavailable GameObject>"
                : $"on '{caller.HierarchyPath}' in scene " +
                 $"'{DisplayDiagnosticId(caller.SceneName)}'" +
                 (string.IsNullOrEmpty(caller.ScenePath)
                     ? string.Empty
                     : $", path '{caller.ScenePath}'");

            return
                $"{typeName} {objectLocation} " +
                $"(GameObject ID: '{DisplayDiagnosticId(caller.GameObjectId)}', " +
                $"Component ID: '{DisplayDiagnosticId(caller.ComponentId)}', " +
                $"Unity Object ID: {caller.UnityObjectId}, " +
                $"Nearest SaveablePrefab: '{DisplayDiagnosticId(caller.NearestPrefabName)}', " +
                $"Prefab Instance ID: '{DisplayDiagnosticId(caller.NearestPrefabInstanceId)}', " +
                $"Prefab Asset ID: '{DisplayDiagnosticId(caller.NearestPrefabAssetId)}', " +
                $"Blob-owned: {caller.BlobOwned}, " +
                $"Globally registered: {caller.GloballyRegistered}, " +
                $"Registration source: '{DisplayDiagnosticId(caller.RegistrationSource)}')";
        }

        private static string GetSafePrefabName(SaveablePrefab prefab)
        {
            if (prefab == null)
                return string.Empty;

            try
            {
                return BuildFullHierarchyPath(prefab.transform);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string GetSafePrefabInstanceId(SaveablePrefab prefab)
        {
            if (prefab == null)
                return string.Empty;

            try
            {
                return prefab.UniqueID ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string GetSafePrefabAssetId(SaveablePrefab prefab)
        {
            if (prefab == null)
                return string.Empty;

            try
            {
                return prefab.PrefabAssetID ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string BuildFullHierarchyPath(Transform target)
        {
            if (target == null)
                return "<unavailable>";

            var names = new List<string>(8);
            for (Transform current = target; current != null; current = current.parent)
                names.Add(current.name);

            names.Reverse();
            return string.Join("/", names);
        }

        private static string DisplayDiagnosticId(string value)
        {
            return string.IsNullOrEmpty(value) ? "<empty>" : value;
        }

        private struct SaveCallRecord
        {
            internal int TotalCalls;
            internal SaveCallCallerRecord FirstCaller;
            internal List<SaveCallCallerRecord> AdditionalCallers;
            internal SaveCallOccurrenceRecord FirstOccurrence;
            internal bool HasFirstOccurrence;
            internal List<SaveCallOccurrenceRecord> Occurrences;

            internal void Add(
                SaveableComponent component,
                SaveCallOccurrenceRecord occurrence,
                bool captureAllOccurrences)
            {
                if (TotalCalls == 0)
                {
                    FirstOccurrence = occurrence;
                    HasFirstOccurrence = true;
                    if (captureAllOccurrences)
                    {
                        Occurrences =
                            new List<SaveCallOccurrenceRecord>(2)
                            {
                                occurrence
                            };
                    }

                    FirstCaller = CreateCallerRecord(component);
                    TotalCalls = 1;
                    return;
                }

                if (Occurrences == null)
                {
                    Occurrences = new List<SaveCallOccurrenceRecord>(2);
                    if (HasFirstOccurrence)
                        Occurrences.Add(FirstOccurrence);
                }
                Occurrences.Add(occurrence);

                TotalCalls++;
                if (ReferenceEquals(FirstCaller.Component, component))
                {
                    FirstCaller.CallCount++;
                    return;
                }

                if (AdditionalCallers != null)
                {
                    for (int i = 0; i < AdditionalCallers.Count; i++)
                    {
                        SaveCallCallerRecord existing = AdditionalCallers[i];
                        if (!ReferenceEquals(existing.Component, component))
                            continue;

                        existing.CallCount++;
                        AdditionalCallers[i] = existing;
                        return;
                    }
                }

                AdditionalCallers ??= new List<SaveCallCallerRecord>(1);
                AdditionalCallers.Add(CreateCallerRecord(component));
            }

            internal void CaptureFirstOccurrenceLocationIfNeeded()
            {
                if (!HasFirstOccurrence ||
                  !string.IsNullOrEmpty(FirstOccurrence.HierarchyPath))
                {
                    return;
                }

                CaptureOccurrenceLocation(
                    FirstOccurrence.Component,
                    ref FirstOccurrence);
            }
        }

        private struct SaveCallOccurrenceRecord
        {
            internal int CallOrder;
            internal SaveableComponent Component;
            internal SaveDataCallRoute Route;
            internal SaveablePrefab RouteOwner;
            internal SaveablePrefab NearestPrefab;
            internal bool BlobOwned;
            internal bool GloballyRegistered;
            internal string RegistrationSource;
            internal string HierarchyPath;
            internal string SceneName;
            internal string ScenePath;
            internal string StackTrace;
        }

        internal readonly struct SaveDataCallRouteScope : IDisposable
        {
            private readonly bool active;
            private readonly bool previousRouteActive;
            private readonly SaveDataCallRoute previousRoute;
            private readonly SaveableComponent previousExpectedComponent;
            private readonly SaveablePrefab previousPrefabOwner;
            private readonly bool previousInvocationPreRecorded;
            private readonly bool previousBaseEntryObserved;

            internal SaveDataCallRouteScope(
                bool previousRouteActive,
                SaveDataCallRoute previousRoute,
                SaveableComponent previousExpectedComponent,
                SaveablePrefab previousPrefabOwner,
                bool previousInvocationPreRecorded,
                bool previousBaseEntryObserved)
            {
                active = true;
                this.previousRouteActive = previousRouteActive;
                this.previousRoute = previousRoute;
                this.previousExpectedComponent = previousExpectedComponent;
                this.previousPrefabOwner = previousPrefabOwner;
                this.previousInvocationPreRecorded =
                    previousInvocationPreRecorded;
                this.previousBaseEntryObserved = previousBaseEntryObserved;
            }

            public void Dispose()
            {
                if (!active)
                    return;

                saveDataCallRouteActive = previousRouteActive;
                currentSaveDataCallRoute = previousRoute;
                currentSaveDataExpectedComponent = previousExpectedComponent;
                currentSaveDataPrefabOwner = previousPrefabOwner;
                currentSaveDataInvocationPreRecorded =
                    previousInvocationPreRecorded;
                currentSaveDataBaseEntryObserved =
                    previousBaseEntryObserved;
            }
        }

        private static void ResetSaveDataCallRouteState()
        {
            saveDataCallRouteActive = false;
            currentSaveDataCallRoute = SaveDataCallRoute.Unknown;
            currentSaveDataExpectedComponent = null;
            currentSaveDataPrefabOwner = null;
            currentSaveDataInvocationPreRecorded = false;
            currentSaveDataBaseEntryObserved = false;
        }

        internal readonly struct SaveDataInvocationScope : IDisposable
        {
            private readonly bool active;

            internal SaveDataInvocationScope(bool active)
            {
                this.active = active;
            }

            public void Dispose()
            {
                if (active && saveDataInvocationDepth > 0)
                    saveDataInvocationDepth--;
            }
        }

        internal static void Reset()
        {
            AbortSaveCallDiagnostics();
            CancelNextSaveCallDiagnosticCapture();
            ResetSaveDataCallRouteState();
            saveDataInvocationDepth = 0;
        }
    }
}
#endif
