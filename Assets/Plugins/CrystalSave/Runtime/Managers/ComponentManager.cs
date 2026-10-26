#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
    public class ComponentManager
    {
#if UNITY_EDITOR
        private const string DebugLogTag = "[CrystalSaveDebug]";
#endif
        /*─────────────────────────────── CONFIG ───────────────────────────────*/
        /// <summary>The maximum number of full passes over <see cref="parentingQueue"/> we will
        /// perform while trying to resolve missing parents.  In practice 2–3 is enough because
        /// each successful parenting shrinks the queue for the next pass.</summary>
	       private const int MaxParentingResolveIterations = 3;
               private enum ComponentRegistrationRoute
               {
                       Normal,
                       IdempotentEnsure,
                       PrefabOwnershipTransfer
               }

               private sealed class SaveableReferenceComparer :
                       IEqualityComparer<ISaveable>
               {
                       internal static readonly SaveableReferenceComparer Instance = new();

                       public bool Equals(ISaveable left, ISaveable right)
                       {
                               return ReferenceEquals(left, right);
                       }

                       public int GetHashCode(ISaveable value)
                       {
                               return value == null
                                       ? 0
                                       : RuntimeHelpers.GetHashCode(value);
                       }
               }

               private readonly struct ComponentOwnershipRoute
               {
                       internal ComponentOwnershipRoute(
                               SaveablePrefab nearestOwner,
                               bool blobOwned)
                       {
                               NearestOwner = nearestOwner;
                               BlobOwned = blobOwned;
                       }

                       internal SaveablePrefab NearestOwner { get; }
                       internal bool BlobOwned { get; }
               }

               private readonly List<ISaveable> saveableComponents = new();
               private readonly HashSet<ISaveable> globalRegistrationMembership =
                       new(SaveableReferenceComparer.Instance);
               private readonly HashSet<ISaveable> knownOwnershipComponents =
                       new(SaveableReferenceComparer.Instance);
               private readonly Dictionary<ISaveable, ComponentOwnershipRoute>
                       collectionOwnershipSnapshot =
                               new(SaveableReferenceComparer.Instance);
               private readonly Dictionary<SaveablePrefab, ISaveable[]>
                       collectionBlobHelpersByOwner = new();
               private ISaveable[] collectionGlobalComponents =
                       Array.Empty<ISaveable>();
               private readonly Dictionary<string, ISaveable> saveableLookup = new();
        private readonly ComponentRestoreState restoration = new(SaveableReferenceComparer.Instance);
        internal ComponentRestoreState Restoration => restoration;
        internal object RestoreGeneration => restoration.Generation;
        internal IReadOnlyDictionary<string, byte[]> PendingComponentData => restoration.Pending;
               private readonly HashSet<SaveablePrefab> deferredPrefabOwnershipRefreshes = new();
               private readonly HashSet<SaveablePrefab> prefabOwnershipRefreshesInProgress = new();
               private readonly HashSet<SaveableComponent> deferredHierarchyOwnershipRefreshes = new();
               private readonly HashSet<ISaveable> pendingOwnershipDataRetries =
                       new(SaveableReferenceComparer.Instance);
               private SaveStateMachine prefabOwnershipStateMachine;
               private bool prefabOwnershipAutomaticRetryScheduled;
               private bool prefabOwnershipAutomaticRetryInProgress;
               private int saveDataCollectionDepth;

               // In-memory cache for Share State Across Scenes.
               // Populated when a component with ShareStateAcrossScenes unregisters;
               // consumed when a matching component registers in another scene.
               private readonly Dictionary<string, byte[]> sharedStateCache = new();
               // Tracks identifiers that have already triggered the runtime-prefab warning
               // so we only warn once per unique component.
               private readonly HashSet<string> sharedStateRuntimePrefabWarnings = new();
        private readonly DeferredComponentQueue deferredComponents = new();
        public event Action<IReadOnlyCollection<string>> OnDeferredComponentsQueued;

        private ComponentHomeSceneCache homeScenes;
        private ComponentHomeSceneCache HomeScenes => homeScenes ??= new ComponentHomeSceneCache(restoration);
        internal IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, byte[]>>> HomeSceneSnapshots => HomeScenes.Snapshots;

        // Runtime instances stay bound to their creating host, including after its
        // Unity object is destroyed. The public standalone constructor keeps its
        // historical singleton fallback for integrations and authoring tools.
        private readonly SaveManager owner;
        private SaveManager Owner => owner ?? SaveManager.Instance;

        public ComponentManager() { }

        internal ComponentManager(SaveManager owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        #region Singleton Implementation

                /// <summary>
                /// Provides access to the active <see cref="ComponentManager"/> via the <see cref="SaveManager"/>.
                /// </summary>
                public static ComponentManager Instance => SaveManager.Instance?.ComponentManager;

               #endregion

               #region Validation Helpers

               internal static bool IsValidSaveable(ISaveable component, string context)
               {
                       if (component == null)
                       {
                               Logger.Log($"{context}: ISaveable is null.", LogCategory.ComponentManager, LogLevel.Warning);
                               return false;
                       }

                       if (string.IsNullOrEmpty(component.UniqueIdentifier))
                       {
                               Logger.Log($"{context}: ISaveable has empty UniqueIdentifier.", LogCategory.ComponentManager, LogLevel.Error);
                               return false;
                       }

                       return true;
               }

               private static string DescribeSaveable(ISaveable component)
               {
                       if (component is MonoBehaviour mb && mb != null)
                       {
                               string sceneName = mb.gameObject.scene.IsValid()
                                       ? mb.gameObject.scene.name
                                       : "<no scene>";
                               return $"'{mb.gameObject.name}' in scene '{sceneName}'";
                       }

                       return "'Unknown'";
               }

               private static void LogDuplicateSaveKey(string context, string uniqueIdentifier, ISaveable component)
               {
                       SaveableComponent.RecordUntracedSaveDataCollectionCollision(
                               uniqueIdentifier);
                       Logger.Log(
                               $"{context}: Duplicate ISaveable UniqueIdentifier '{uniqueIdentifier}' on {DescribeSaveable(component)}. " +
                               "Only one value can be stored for a save key; regenerate one Component ID if these are different components.",
                               LogCategory.ComponentManager,
                               LogLevel.Warning);
               }

               private bool TryGetPrefabBlobOwner(ISaveable component, out SaveablePrefab owner)
               {
                       return TryResolveComponentBlobOwnership(component, out owner);
               }

               private bool TryResolveComponentBlobOwnership(
                       ISaveable component,
                       out SaveablePrefab owner)
               {
                       if (component == null)
                       {
                               owner = null;
                               return false;
                       }

                       if (saveDataCollectionDepth > 0 &&
                           collectionOwnershipSnapshot.TryGetValue(
                                   component,
                                   out ComponentOwnershipRoute frozen))
                       {
                               owner = frozen.NearestOwner;
                               if (component is SaveableComponent frozenComponent &&
                                   frozenComponent != null)
                               {
                                       frozenComponent.ApplyPrefabSerializationOwnership(
                                               owner,
                                               frozen.BlobOwned);
                               }

                               return frozen.BlobOwned;
                       }

                       bool blobOwned =
                               TryResolveLiveComponentBlobOwnership(component, out owner);
                       if (saveDataCollectionDepth > 0)
                       {
                               collectionOwnershipSnapshot[component] =
                                       new ComponentOwnershipRoute(owner, blobOwned);
                               knownOwnershipComponents.Add(component);
                       }

                       return blobOwned;
               }

               private static bool TryResolveLiveComponentBlobOwnership(
                       ISaveable component,
                       out SaveablePrefab owner)
               {
                       owner = null;
                       if (component == null)
                               return false;

                       if (component is SaveableComponent saveableComponent && saveableComponent != null)
                       {
                               owner =
                                       saveableComponent.GetComponentInParent<SaveablePrefab>(
                                               true);
                               bool blobOwned =
                                       owner != null &&
                                       owner.EffectiveTrackComponentBlobs;
                               saveableComponent.ApplyPrefabSerializationOwnership(
                                       owner,
                                       blobOwned);
                               return blobOwned;
                       }

                       owner = SaveablePrefab.FindNearestComponentBlobOwner(component);
                       return owner != null && owner.EffectiveTrackComponentBlobs;
               }

               internal bool IsComponentBlobOwnedBy(
                       ISaveable component,
                       SaveablePrefab expectedOwner)
               {
                       return expectedOwner != null &&
                              TryResolveComponentBlobOwnership(
                                      component,
                                      out SaveablePrefab owner) &&
                              owner == expectedOwner;
               }

               internal bool TryResolveComponentBlobOwnershipForDiagnostics(
                       ISaveable component,
                       out SaveablePrefab nearestOwner)
               {
                       return TryResolveComponentBlobOwnership(
                               component,
                               out nearestOwner);
               }

               internal void ObservePrefabBlobHelper(ISaveable component)
               {
                       if (component == null)
                               return;

                       knownOwnershipComponents.Add(component);
                       if (saveDataCollectionDepth > 0 &&
                           !collectionOwnershipSnapshot.ContainsKey(component))
                       {
                               bool blobOwned =
                                       TryResolveLiveComponentBlobOwnership(
                                               component,
                                               out SaveablePrefab owner);
                               collectionOwnershipSnapshot[component] =
                                       new ComponentOwnershipRoute(owner, blobOwned);
                       }
               }

               internal bool TryGetCollectionBlobHelpers(
                       SaveablePrefab owner,
                       out ISaveable[] helpers)
               {
                       if (saveDataCollectionDepth > 0 &&
                           owner != null &&
                           collectionBlobHelpersByOwner.TryGetValue(owner, out helpers))
                       {
                               return true;
                       }

                       helpers = null;
                       return false;
               }

               private bool TryAddGlobalRegistration(
                       ISaveable component,
                       ComponentRegistrationRoute route,
                       string registrationSource)
               {
                       if (component != null)
                               knownOwnershipComponents.Add(component);

                       if (component is SaveableComponent suppressedComponent &&
                           suppressedComponent.RuntimeSaveRegistrationSuppressed)
                       {
                               RemoveGlobalRegistrationCore(
                                       component,
                                       clearDeserializedState: false,
                                       registrationSource: registrationSource,
                                       logMissing: false);
                               return false;
                       }

                       if (TryGetPrefabBlobOwner(component, out _))
                       {
                               RemoveGlobalRegistrationCore(
                                       component,
                                       clearDeserializedState: false,
                                       registrationSource: registrationSource,
                                       logMissing: false);
                               return false;
                       }

                       if (!IsValidSaveable(component, nameof(RegisterSaveableComponent)))
                       {
                               RemoveGlobalRegistrationCore(
                                       component,
                                       clearDeserializedState: false,
                                       registrationSource: registrationSource,
                                       logMissing: false);
                               return false;
                       }

                       if (saveableComponents.Contains(component))
                       {
                               globalRegistrationMembership.Add(component);
                               if (component is SaveableComponent alreadyRegistered && alreadyRegistered != null)
                                       alreadyRegistered.SetManagerRegistrationState(true);

                               if (route == ComponentRegistrationRoute.Normal)
                               {
                                       Logger.Log(
                                               $"ComponentManager: ISaveable '{component.UniqueIdentifier}' is already registered.",
                                               LogCategory.ComponentManager,
                                               LogLevel.Warning);
                               }

                               return false;
                       }

                       if (saveableLookup.TryGetValue(component.UniqueIdentifier, out var existing) &&
                           existing != null &&
                           !ReferenceEquals(existing, component))
                       {
                               Logger.Log(
                                       $"ComponentManager: Duplicate ISaveable UniqueIdentifier '{component.UniqueIdentifier}' while registering {DescribeSaveable(component)}. " +
                                       $"Existing component is on {DescribeSaveable(existing)}. One component's save data may overwrite the other; regenerate one Component ID.",
                                       LogCategory.ComponentManager,
                                       LogLevel.Warning);
                       }

                       saveableComponents.Add(component);
                       globalRegistrationMembership.Add(component);
                       saveableLookup[component.UniqueIdentifier] = component;
                       if (component is SaveableComponent saveableComponent && saveableComponent != null)
                               saveableComponent.SetManagerRegistrationState(true, registrationSource);

                       Logger.Log(
                               $"ComponentManager: Registered ISaveable '{component.UniqueIdentifier}' on GameObject '{(component as MonoBehaviour)?.gameObject.name ?? "Unknown"}'.",
                               LogCategory.ComponentManager,
                               LogLevel.Info);
                       return true;
               }

               private bool RemoveGlobalRegistrationCore(
                       ISaveable component,
                       bool clearDeserializedState,
                       string registrationSource,
                       bool logMissing)
               {
                       if (component == null)
                               return false;

                       string identifier = GetIdentifierSafely(component);
                       bool removed = saveableComponents.Remove(component);
                       globalRegistrationMembership.Remove(component);
                       var removedLookupKeys = RemoveLookupEntriesReferencing(component);

                       if (clearDeserializedState)
                       {
                               RemoveDeserializedComponentOwner(component, identifier);
                               if (!string.IsNullOrEmpty(identifier) &&
                                   !HasRegisteredComponentWithIdentifier(identifier))
                                       restoration.RemoveIdentifierMarker(identifier);

                               foreach (string key in removedLookupKeys)
                               {
                                       RemoveDeserializedComponentOwner(component, key);
                                       if (!HasRegisteredComponentWithIdentifier(key))
                                               restoration.RemoveIdentifierMarker(key);
                               }
                       }

                       if (component is SaveableComponent saveableComponent && saveableComponent != null)
                               saveableComponent.SetManagerRegistrationState(false, registrationSource);

                       if (removed)
                       {
                               Logger.Log(
                                       $"ComponentManager: Unregistered ISaveable '{identifier ?? "<unavailable>"}'.",
                                       LogCategory.ComponentManager,
                                       LogLevel.Info);
                       }
                       else if (logMissing)
                       {
                               Logger.Log(
                                       $"ComponentManager: Attempted to unregister ISaveable '{identifier ?? "<unavailable>"}', but it was not found.",
                                       LogCategory.ComponentManager,
                                       LogLevel.Warning);
                       }

                       return removed;
               }

               private bool HasRegisteredComponentWithIdentifier(string identifier)
               {
                       if (string.IsNullOrEmpty(identifier))
                               return false;

                       if (saveableLookup.TryGetValue(identifier, out ISaveable indexed) &&
                           indexed != null &&
                           globalRegistrationMembership.Contains(indexed) &&
                           (indexed is not MonoBehaviour indexedBehaviour ||
                            indexedBehaviour != null))
                       {
                               return true;
                       }

                       for (int i = 0; i < saveableComponents.Count; i++)
                       {
                               ISaveable candidate = saveableComponents[i];
                               if (candidate == null ||
                                   !globalRegistrationMembership.Contains(candidate))
                               {
                                       continue;
                               }

                               if (candidate is MonoBehaviour behaviour && behaviour == null)
                                       continue;

                               if (string.Equals(
                                       GetIdentifierSafely(candidate),
                                       identifier,
                                       StringComparison.Ordinal))
                               {
                                       return true;
                               }
                       }

                       return false;
               }

               private List<string> RemoveLookupEntriesReferencing(ISaveable component)
               {
                       var removedKeys = new List<string>();
                       foreach (var pair in saveableLookup)
                       {
                               if (ReferenceEquals(pair.Value, component))
                                       removedKeys.Add(pair.Key);
                       }

                       foreach (string key in removedKeys)
                               saveableLookup.Remove(key);

                       // If duplicate identifiers existed, keep the lookup usable by restoring
                       // another live, globally-owned component for each removed key.
                       foreach (string key in removedKeys)
                       {
                               for (int i = saveableComponents.Count - 1; i >= 0; i--)
                               {
                                       ISaveable candidate = saveableComponents[i];
                                       if (candidate == null || ReferenceEquals(candidate, component))
                                               continue;
                                       if (candidate is MonoBehaviour mb && mb == null)
                                               continue;
                                       if (TryGetPrefabBlobOwner(candidate, out _))
                                               continue;
                                       if (!string.Equals(GetIdentifierSafely(candidate), key, StringComparison.Ordinal))
                                               continue;

                                       saveableLookup[key] = candidate;
                                       break;
                               }
                       }

                       return removedKeys;
               }

               private static string GetIdentifierSafely(ISaveable component)
               {
                       if (component == null)
                               return null;

                       try
                       {
                               return component.UniqueIdentifier;
                       }
                       catch
                       {
                               return null;
                       }
               }

               internal void RequestPrefabComponentOwnershipRefresh(SaveablePrefab prefab)
               {
                       if (prefab == null)
                               return;

                       SaveManager manager = Owner;
                       bool managerUnavailable = manager == null || manager.ComponentManager != this;
                       bool loading = manager != null &&
                                      (manager.IsLoading ||
                                       manager.StateMachine?.CurrentState == SaveState.Loading);
                       if (prefabOwnershipRefreshesInProgress.Contains(prefab))
                       {
                               deferredPrefabOwnershipRefreshes.Add(prefab);
                               return;
                       }

                       if (managerUnavailable ||
                           loading ||
                           prefab.IsLoading ||
                           saveDataCollectionDepth > 0)
                       {
                               deferredPrefabOwnershipRefreshes.Add(prefab);
                               if (managerUnavailable || loading || prefab.IsLoading)
                                       SubscribeToPrefabOwnershipStateChanges(manager?.StateMachine);
                               return;
                       }

                       deferredPrefabOwnershipRefreshes.Remove(prefab);
                       ApplyPrefabComponentOwnershipRefresh(prefab);
                       EnsureDeferredOwnershipRetry(manager);
                       ReleasePrefabOwnershipStateSubscriptionIfIdle();
               }

               private void ApplyPrefabComponentOwnershipRefresh(SaveablePrefab prefab)
               {
                       if (prefab == null)
                               return;

                       if (!prefabOwnershipRefreshesInProgress.Add(prefab))
                       {
                               deferredPrefabOwnershipRefreshes.Add(prefab);
                               return;
                       }

                       bool completed = false;
                       try
                       {
                               const int maxReconciliationPasses = 8;
                               for (int pass = 0; pass < maxReconciliationPasses; pass++)
                               {
                                       deferredPrefabOwnershipRefreshes.Remove(prefab);
                                       prefab.BeginApplyingComponentBlobOwnershipRefresh();
                                       bool prefabOwnsComponents = prefab.TrackComponentBlobs;
                                       bool passFailed = false;
                                       ISaveable[] helpers =
                                               prefab.GetComponentsInChildren<ISaveable>(
                                                       includeInactive: true);
                                       foreach (ISaveable helper in helpers)
                                       {
                                               if (helper != null)
                                                       knownOwnershipComponents.Add(helper);

                                               if (helper == null ||
                                                   SaveablePrefab.FindNearestComponentBlobOwner(
                                                           helper) != prefab)
                                               {
                                                       continue;
                                               }

                                               try
                                               {
                                                       if (helper is SaveableComponent saveableComponent &&
                                                           saveableComponent != null)
                                                       {
                                                               saveableComponent
                                                                       .RefreshPrefabSerializationOwnership();
                                                       }

                                                       if (prefabOwnsComponents)
                                                       {
                                                               pendingOwnershipDataRetries.Remove(helper);
                                                               RemoveGlobalRegistrationCore(
                                                                       helper,
                                                                       clearDeserializedState: false,
                                                                       registrationSource:
                                                                               "PrefabOwnershipTransfer",
                                                                       logMissing: false);
                                                       }
                                                       else
                                                       {
                                                               bool added = TryAddGlobalRegistration(
                                                                       helper,
                                                                       ComponentRegistrationRoute
                                                                               .PrefabOwnershipTransfer,
                                                                       registrationSource:
                                                                               "PrefabOwnershipTransfer");
                                                               if (added ||
                                                                   pendingOwnershipDataRetries.Contains(
                                                                           helper))
                                                               {
                                                                       if (TryApplyPendingDataAfterOwnershipTransfer(
                                                                               helper))
                                                                       {
                                                                               pendingOwnershipDataRetries.Remove(
                                                                                       helper);
                                                                       }
                                                                       else
                                                                       {
                                                                               pendingOwnershipDataRetries.Add(
                                                                                       helper);
                                                                               passFailed = true;
                                                                       }
                                                               }
                                                       }
                                               }
                                               catch (Exception ex)
                                               {
                                                       passFailed = true;
                                                       Logger.Log(
                                                               $"ComponentManager: Failed to reconcile prefab component ownership for {DescribeSaveable(helper)}: {ex.Message}",
                                                               LogCategory.ComponentManager,
                                                               LogLevel.Error);
                                               }
                                       }

                                       if (passFailed)
                                       {
                                               deferredPrefabOwnershipRefreshes.Add(prefab);
                                               break;
                                       }

                                       bool requestedAgain =
                                               deferredPrefabOwnershipRefreshes.Remove(prefab);
                                       if (!requestedAgain &&
                                           prefab.TrackComponentBlobs ==
                                           prefabOwnsComponents)
                                       {
                                               completed = true;
                                               break;
                                       }
                               }
                       }
                       finally
                       {
                               prefabOwnershipRefreshesInProgress.Remove(prefab);
                               if (prefab != null && completed)
                               {
                                       prefab.CompleteComponentBlobOwnershipRefresh();
                               }
                               else if (prefab != null)
                               {
                                       deferredPrefabOwnershipRefreshes.Add(prefab);
                                       Logger.Log(
                                               $"ComponentManager: Prefab component ownership for '{prefab.name}' changed repeatedly or could not fully reconcile; it remains queued for a later idle pass.",
                                               LogCategory.ComponentManager,
                                               LogLevel.Warning);
                               }
                       }
               }

               private void ScheduleDeferredPrefabOwnershipRetry(SaveManager manager)
               {
                       if (prefabOwnershipAutomaticRetryScheduled ||
                           prefabOwnershipAutomaticRetryInProgress ||
                           manager == null ||
                           manager.ComponentManager != this ||
                           !manager.isActiveAndEnabled)
                       {
                               return;
                       }

                       prefabOwnershipAutomaticRetryScheduled = true;
                       manager.StartCoroutine(RetryDeferredPrefabOwnershipOnce());
               }

               private IEnumerator RetryDeferredPrefabOwnershipOnce()
               {
                       // A single delayed retry covers transient lifecycle/order changes without
                       // creating an endless per-frame loop when user LoadData code keeps failing.
                       yield return null;
                       prefabOwnershipAutomaticRetryScheduled = false;

                       SaveManager manager = Owner;
                       if (manager == null || manager.ComponentManager != this)
                               yield break;

                       prefabOwnershipAutomaticRetryInProgress = true;
                       try
                       {
                               FlushDeferredPrefabOwnershipRefreshes();
                               FlushDeferredHierarchyOwnershipRefreshes();
                       }
                       finally
                       {
                               // Flush methods may leave failed work queued, but the in-progress
                               // guard deliberately prevents them from creating an unbounded
                               // retry-every-frame loop. A later state change, explicit request,
                               // or collection boundary can still retry the retained work.
                               prefabOwnershipAutomaticRetryInProgress = false;
                       }
               }

               private void EnsureDeferredOwnershipRetry(SaveManager manager)
               {
                       if (deferredPrefabOwnershipRefreshes.Count == 0 &&
                           deferredHierarchyOwnershipRefreshes.Count == 0)
                       {
                               return;
                       }

                       SubscribeToPrefabOwnershipStateChanges(manager?.StateMachine);
                       if (manager == null ||
                           manager.ComponentManager != this ||
                           manager.IsLoading ||
                           manager.StateMachine?.CurrentState != SaveState.Idle ||
                           saveDataCollectionDepth > 0)
                       {
                               return;
                       }

                       ScheduleDeferredPrefabOwnershipRetry(manager);
               }

               private void SubscribeToPrefabOwnershipStateChanges(SaveStateMachine stateMachine)
               {
                       if (stateMachine == null || ReferenceEquals(prefabOwnershipStateMachine, stateMachine))
                               return;

                       if (prefabOwnershipStateMachine != null)
                               prefabOwnershipStateMachine.OnStateChanged -= HandlePrefabOwnershipSaveStateChanged;

                       prefabOwnershipStateMachine = stateMachine;
                       prefabOwnershipStateMachine.OnStateChanged += HandlePrefabOwnershipSaveStateChanged;
               }

               private void HandlePrefabOwnershipSaveStateChanged(SaveState state)
               {
                       if (state != SaveState.Idle)
                               return;

                       // The outer load coroutine clears SaveManager.IsLoading after entering Idle.
                       // Its explicit notifier performs the transfer only after both gates are clear.
                       FlushDeferredPrefabOwnershipRefreshes();
                       FlushDeferredHierarchyOwnershipRefreshes();
               }

               private void FlushDeferredPrefabOwnershipRefreshes()
               {
                       if (deferredPrefabOwnershipRefreshes.Count == 0)
                       {
                               ReleasePrefabOwnershipStateSubscriptionIfIdle();
                               return;
                       }

                       SaveManager manager = Owner;
                       if (manager == null ||
                           manager.ComponentManager != this ||
                           manager.IsLoading ||
                           manager.StateMachine?.CurrentState != SaveState.Idle ||
                           saveDataCollectionDepth > 0)
                       {
                               SubscribeToPrefabOwnershipStateChanges(
                                       manager?.StateMachine);
                               return;
                       }

                       var pending = deferredPrefabOwnershipRefreshes.ToArray();
                       deferredPrefabOwnershipRefreshes.Clear();
                       foreach (SaveablePrefab prefab in pending)
                       {
                               if (prefab == null)
                                       continue;

                               if (prefab.IsLoading)
                               {
                                       deferredPrefabOwnershipRefreshes.Add(prefab);
                                       continue;
                               }

                               ApplyPrefabComponentOwnershipRefresh(prefab);
                       }

                       EnsureDeferredOwnershipRetry(manager);
                       ReleasePrefabOwnershipStateSubscriptionIfIdle();
               }

               internal void NotifySaveManagerLoadingFlagCleared()
               {
                       FlushDeferredPrefabOwnershipRefreshes();
                       FlushDeferredHierarchyOwnershipRefreshes();
               }

               internal SaveDataCollectionOwnershipScope BeginSaveDataCollectionOwnershipScope()
               {
                       if (saveDataCollectionDepth == 0)
                       {
                               FlushDeferredPrefabOwnershipRefreshes();
                               FlushDeferredHierarchyOwnershipRefreshes();
                               saveDataCollectionDepth = 1;
                               try
                               {
                                       PrepareCollectionOwnershipSnapshot();
                               }
                               catch
                               {
                                       saveDataCollectionDepth = 0;
                                       ClearCollectionOwnershipSnapshot();
                                       throw;
                               }
                       }
                       else
                       {
                               saveDataCollectionDepth++;
                       }

                       return new SaveDataCollectionOwnershipScope(this);
               }

               internal bool IsSaveDataCollectionActive => saveDataCollectionDepth > 0;

               private void PrepareCollectionOwnershipSnapshot()
               {
                       collectionOwnershipSnapshot.Clear();
                       collectionBlobHelpersByOwner.Clear();
                       collectionGlobalComponents = Array.Empty<ISaveable>();

                       foreach (ISaveable registered in saveableComponents)
                       {
                               if (registered != null)
                                       knownOwnershipComponents.Add(registered);
                       }

                       SaveablePrefab[] prefabs =
                               Owner?.GetPrefabManager?.RegisteredPrefabs?
                                       .Where(prefab => prefab != null)
                                       .ToArray() ??
                               Array.Empty<SaveablePrefab>();
                       var prefabHelperCandidates =
                               new Dictionary<SaveablePrefab, ISaveable[]>();
                       foreach (SaveablePrefab prefab in prefabs)
                       {
                               ISaveable[] helpers =
                                       prefab.GetComponentsInChildren<ISaveable>(
                                               includeInactive: true);
                               prefabHelperCandidates[prefab] = helpers;
                               foreach (ISaveable helper in helpers)
                               {
                                       if (helper != null &&
                                           TryResolveLiveComponentBlobOwnership(
                                                   helper,
                                                   out _))
                                       {
                                               knownOwnershipComponents.Add(helper);
                                       }
                               }
                       }

                       ISaveable[] known = knownOwnershipComponents.ToArray();
                       foreach (ISaveable component in known)
                       {
                               if (component == null ||
                                   component is MonoBehaviour destroyedComponent &&
                                   destroyedComponent == null)
                               {
                                       knownOwnershipComponents.Remove(component);
                                       continue;
                               }

                               bool blobOwned =
                                       TryResolveLiveComponentBlobOwnership(
                                               component,
                                               out SaveablePrefab owner);
                               collectionOwnershipSnapshot[component] =
                                       new ComponentOwnershipRoute(owner, blobOwned);
                       }

                       // Reconcile the live global list before any user SaveData callback can
                       // mutate the hierarchy. The frozen map above remains authoritative for
                       // the whole outer collection; changes are queued and applied on exit.
                       foreach (ISaveable component in known)
                       {
                               if (component == null ||
                                   !collectionOwnershipSnapshot.TryGetValue(
                                           component,
                                           out ComponentOwnershipRoute route))
                               {
                                       continue;
                               }

                               if (route.BlobOwned)
                               {
                                       pendingOwnershipDataRetries.Remove(component);
                                       if (globalRegistrationMembership.Contains(component))
                                       {
                                               RemoveGlobalRegistrationCore(
                                                       component,
                                                       clearDeserializedState: false,
                                                       registrationSource:
                                                               "CollectionOwnershipBoundary",
                                                       logMissing: false);
                                       }
                                       continue;
                               }

                               bool added = TryAddGlobalRegistration(
                                       component,
                                       ComponentRegistrationRoute.PrefabOwnershipTransfer,
                                       registrationSource:
                                               "CollectionOwnershipBoundary");
                               if (added)
                               {
                                       if (TryApplyPendingDataAfterOwnershipTransfer(component))
                                       {
                                               pendingOwnershipDataRetries.Remove(component);
                                       }
                                       else
                                       {
                                               pendingOwnershipDataRetries.Add(component);
                                               if (component is SaveableComponent saveableComponent)
                                               {
                                                       deferredHierarchyOwnershipRefreshes.Add(
                                                               saveableComponent);
                                               }
                                       }
                               }
                       }

                       foreach (KeyValuePair<SaveablePrefab, ISaveable[]> pair in
                                prefabHelperCandidates)
                       {
                               collectionBlobHelpersByOwner[pair.Key] =
                                       pair.Value
                                               .Where(helper =>
                                                       helper != null &&
                                                       collectionOwnershipSnapshot
                                                               .TryGetValue(
                                                                       helper,
                                                                       out ComponentOwnershipRoute
                                                                               route) &&
                                                       route.BlobOwned &&
                                                       route.NearestOwner == pair.Key)
                                               .ToArray();
                       }

                       SaveablePrefab[] additionalOwners =
                               collectionOwnershipSnapshot.Values
                                       .Where(route =>
                                               route.BlobOwned &&
                                               route.NearestOwner != null &&
                                               !collectionBlobHelpersByOwner
                                                       .ContainsKey(
                                                               route.NearestOwner))
                                       .Select(route => route.NearestOwner)
                                       .Distinct()
                                       .ToArray();
                       foreach (SaveablePrefab owner in additionalOwners)
                       {
                               collectionBlobHelpersByOwner[owner] =
                                       owner.GetComponentsInChildren<ISaveable>(
                                                       includeInactive: true)
                                               .Where(helper =>
                                                       helper != null &&
                                                       collectionOwnershipSnapshot
                                                               .TryGetValue(
                                                                       helper,
                                                                       out ComponentOwnershipRoute
                                                                               route) &&
                                                       route.BlobOwned &&
                                                       route.NearestOwner == owner)
                                               .ToArray();
                       }

                       collectionGlobalComponents = saveableComponents
                               .Where(component =>
                                       component != null &&
                                       globalRegistrationMembership.Contains(component) &&
                                       collectionOwnershipSnapshot.TryGetValue(
                                               component,
                                               out ComponentOwnershipRoute route) &&
                                       !route.BlobOwned)
                               .ToArray();
               }

               internal void ReconcileHierarchyComponentOwnership(
                       SaveableComponent component)
               {
                       if (component == null)
                               return;

                       knownOwnershipComponents.Add(component);

                       SaveManager manager = Owner;
                       bool loading =
                               manager == null ||
                               manager.ComponentManager != this ||
                               manager.IsLoading ||
                               manager.StateMachine?.CurrentState != SaveState.Idle;
                       if (loading || saveDataCollectionDepth > 0)
                       {
                               deferredHierarchyOwnershipRefreshes.Add(component);
                               if (loading)
                               {
                                       SubscribeToPrefabOwnershipStateChanges(
                                               manager?.StateMachine);
                               }
                               return;
                       }

                       ApplyHierarchyComponentOwnership(component);
                       EnsureDeferredOwnershipRetry(manager);
                       ReleasePrefabOwnershipStateSubscriptionIfIdle();
               }

               private void ApplyHierarchyComponentOwnership(
                       SaveableComponent component)
               {
                       if (component == null)
                               return;

                       try
                       {
                               if (TryGetPrefabBlobOwner(component, out _))
                               {
                                       pendingOwnershipDataRetries.Remove(component);
                                       if (globalRegistrationMembership.Contains(component))
                                       {
                                               RemoveGlobalRegistrationCore(
                                                       component,
                                                       clearDeserializedState: false,
                                                       registrationSource:
                                                               "HierarchyOwnershipChange",
                                                       logMissing: false);
                                       }
                                       return;
                               }

                               bool alreadyRegistered =
                                       globalRegistrationMembership.Contains(component);
                               bool added = alreadyRegistered
                                       ? false
                                       : TryAddGlobalRegistration(
                                               component,
                                               ComponentRegistrationRoute
                                                       .PrefabOwnershipTransfer,
                                               registrationSource:
                                                       "HierarchyOwnershipChange");
                               if (alreadyRegistered)
                                       component.SetManagerRegistrationState(true);

                               if (added ||
                                   pendingOwnershipDataRetries.Contains(component))
                               {
                                       if (TryApplyPendingDataAfterOwnershipTransfer(
                                               component))
                                       {
                                               pendingOwnershipDataRetries.Remove(component);
                                       }
                                       else
                                       {
                                               pendingOwnershipDataRetries.Add(component);
                                               deferredHierarchyOwnershipRefreshes.Add(
                                                       component);
                                       }
                               }
                       }
                       catch (Exception ex)
                       {
                               deferredHierarchyOwnershipRefreshes.Add(component);
                               Logger.Log(
                                       $"ComponentManager: Failed to reconcile hierarchy ownership for {DescribeSaveable(component)}: {ex.Message}",
                                       LogCategory.ComponentManager,
                                       LogLevel.Error);
                       }
               }

               private void FlushDeferredHierarchyOwnershipRefreshes()
               {
                       if (deferredHierarchyOwnershipRefreshes.Count == 0)
                       {
                               ReleasePrefabOwnershipStateSubscriptionIfIdle();
                               return;
                       }

                       SaveManager manager = Owner;
                       if (manager == null ||
                           manager.ComponentManager != this ||
                           manager.IsLoading ||
                           manager.StateMachine?.CurrentState != SaveState.Idle ||
                           saveDataCollectionDepth > 0)
                       {
                               SubscribeToPrefabOwnershipStateChanges(
                                       manager?.StateMachine);
                               return;
                       }

                       SaveableComponent[] pending =
                               deferredHierarchyOwnershipRefreshes.ToArray();
                       deferredHierarchyOwnershipRefreshes.Clear();
                       foreach (SaveableComponent component in pending)
                       {
                               if (component != null)
                                       ApplyHierarchyComponentOwnership(component);
                       }

                       EnsureDeferredOwnershipRetry(manager);
                       ReleasePrefabOwnershipStateSubscriptionIfIdle();
               }

               private void EndSaveDataCollectionOwnershipScope()
               {
                       if (saveDataCollectionDepth > 0)
                               saveDataCollectionDepth--;

                       if (saveDataCollectionDepth == 0)
                       {
                               ClearCollectionOwnershipSnapshot();
                               FlushDeferredPrefabOwnershipRefreshes();
                               ReconcileKnownOwnershipComponentsAfterCollection();
                               FlushDeferredHierarchyOwnershipRefreshes();
                       }
               }

               private void ReconcileKnownOwnershipComponentsAfterCollection()
               {
                       ISaveable[] known = knownOwnershipComponents.ToArray();
                       foreach (ISaveable component in known)
                       {
                               if (component == null ||
                                   component is MonoBehaviour destroyedComponent &&
                                   destroyedComponent == null)
                               {
                                       knownOwnershipComponents.Remove(component);
                                       continue;
                               }

                               if (component is SaveableComponent saveableComponent)
                               {
                                       ApplyHierarchyComponentOwnership(
                                               saveableComponent);
                                       continue;
                               }

                               if (TryGetPrefabBlobOwner(component, out _))
                               {
                                       if (globalRegistrationMembership.Contains(component))
                                       {
                                               RemoveGlobalRegistrationCore(
                                                       component,
                                                       clearDeserializedState: false,
                                                       registrationSource:
                                                               "HierarchyOwnershipChange",
                                                       logMissing: false);
                                       }
                               }
                               else if (!globalRegistrationMembership.Contains(component))
                               {
                                       TryAddGlobalRegistration(
                                               component,
                                               ComponentRegistrationRoute
                                                       .PrefabOwnershipTransfer,
                                               registrationSource:
                                                       "HierarchyOwnershipChange");
                               }
                       }
               }

               private void ClearCollectionOwnershipSnapshot()
               {
                       collectionOwnershipSnapshot.Clear();
                       collectionBlobHelpersByOwner.Clear();
                       collectionGlobalComponents = Array.Empty<ISaveable>();
               }

               internal readonly struct SaveDataCollectionOwnershipScope : IDisposable
               {
                       private readonly ComponentManager manager;

                       internal SaveDataCollectionOwnershipScope(ComponentManager manager)
                       {
                               this.manager = manager;
                       }

                       public void Dispose()
                       {
                               manager?.EndSaveDataCollectionOwnershipScope();
                       }
               }

               private bool TryApplyPendingDataAfterOwnershipTransfer(ISaveable component)
               {
                       string identifier = GetIdentifierSafely(component);
                       if (string.IsNullOrEmpty(identifier) ||
                           !restoration.Pending.TryGetValue(identifier, out byte[] pending) ||
                           pending == null)
                       {
                               return true;
                       }

                       if (HasComponentDeserialized(component, identifier))
                       {
                               RemovePendingComponentData(identifier);
                               return true;
                       }

                       if (restoration.IsApplying(component, identifier, pending))
                               return false;

                       try
                       {
                               bool applied = RestoreComponent(component, pending, identifier);
                               Logger.Log(
                                       $"ComponentManager: Applied pending load data to '{identifier}' after prefab component ownership transferred to global collection.",
                                       LogCategory.ComponentManager,
                                       LogLevel.Info);
                               return applied;
                       }
                       catch (Exception ex)
                       {
                               // Keep the bytes queued so a later normal registration/request can retry.
                               Logger.Log(
                                       $"ComponentManager: Failed to apply pending data to '{identifier}' after prefab ownership transfer: {ex.Message}",
                                       LogCategory.ComponentManager,
                                       LogLevel.Error);
                               return false;
                       }
               }

               private void ReleasePrefabOwnershipStateSubscriptionIfIdle()
               {
                       deferredPrefabOwnershipRefreshes.RemoveWhere(prefab => prefab == null);
                       deferredHierarchyOwnershipRefreshes.RemoveWhere(
                               component => component == null);
                       if (deferredPrefabOwnershipRefreshes.Count != 0 ||
                           deferredHierarchyOwnershipRefreshes.Count != 0 ||
                           prefabOwnershipStateMachine == null)
                       {
                               return;
                       }

                       prefabOwnershipStateMachine.OnStateChanged -= HandlePrefabOwnershipSaveStateChanged;
                       prefabOwnershipStateMachine = null;
               }

               #endregion

        #region Public Methods

               // Called only after a full candidate has been accepted, before scene
               // activation can register receivers from the new loaded generation.
               internal void BeginFullLoadGeneration()
               {
                       restoration.BeginGeneration();
                       pendingOwnershipDataRetries.Clear();
                       deferredComponents.Clear();
                       sharedStateCache.Clear();
                       sharedStateRuntimePrefabWarnings.Clear();
                       parentingQueue.Clear();
               }

        public void ResetDeserializedComponents() => restoration.ResetDeserializedComponents();
        public void MarkComponentDeserialized(string id) => restoration.MarkComponentDeserialized(id);
        public bool HasComponentDeserialized(string id) => restoration.HasComponentDeserialized(id);
        internal void MarkComponentDeserialized(ISaveable component, string id) => restoration.MarkComponentDeserialized(component, id);
        private bool HasComponentDeserialized(ISaveable component, string id) => restoration.HasComponentDeserialized(component, id);
        private void RemoveDeserializedComponentOwner(ISaveable component, string id) => restoration.RemoveDeserializedComponentOwner(component, id);
        internal bool RestoreComponent(ISaveable component, byte[] data, string identifier = null) => restoration.RestoreComponent(component, data, identifier);
        private void QueuePendingComponentData(string identifier, byte[] data) => restoration.QueuePendingComponentData(identifier, data);
        public void RemovePendingComponentData(string uniqueIdentifier) => restoration.RemovePendingComponentData(uniqueIdentifier);

        public bool HasDeferredComponents => deferredComponents.HasEntries;
        public IEnumerable<string> GetDeferredSceneKeys() => deferredComponents.SceneKeys;
        public IReadOnlyList<string> PeekDeferredComponentsForScene(string sceneName) => deferredComponents.Peek(sceneName);

               public void ProcessDeferredComponents()
               {
                       var entries = deferredComponents.TakeAll();
                       if (entries.Count == 0)
                               return;

                       ApplyComponentEntries(entries);
               }

               public IEnumerator ProcessDeferredComponentsCoroutine(int batchSize)
                       => ProcessDeferredComponentsCoroutineCore(batchSize, RestoreGeneration);

               private IEnumerator ProcessDeferredComponentsCoroutineCore(int batchSize, object generation)
               {
                       if (!ReferenceEquals(generation, RestoreGeneration))
                               yield break;

                       var entries = deferredComponents.TakeAll();
                       if (entries.Count == 0)
                               yield break;

                       yield return ApplyComponentEntriesCoroutine(entries, batchSize, generation);
               }

               public void ProcessDeferredComponentsForScene(string sceneName)
               {
                       var entries = deferredComponents.TakeScene(sceneName);
                       if (entries.Count == 0)
                               return;

                       ApplyComponentEntries(entries);
               }

               public IEnumerator ProcessDeferredComponentsForSceneCoroutine(string sceneName, int batchSize)
                       => ProcessDeferredComponentsForSceneCoroutineCore(sceneName, batchSize, RestoreGeneration);

               private IEnumerator ProcessDeferredComponentsForSceneCoroutineCore(string sceneName, int batchSize, object generation)
               {
                       if (!ReferenceEquals(generation, RestoreGeneration))
                               yield break;

                       var entries = deferredComponents.TakeScene(sceneName);
                       if (entries.Count == 0)
                               yield break;

                       yield return ApplyComponentEntriesCoroutine(entries, batchSize, generation);
               }

               public void ProcessDeferredComponentsForGameObject(string gameObjectUniqueID)
               {
                       if (string.IsNullOrEmpty(gameObjectUniqueID))
                               return;

                       var entries = deferredComponents.TakeMatching(entry => string.Equals(entry.GameObjectUniqueID, gameObjectUniqueID, StringComparison.Ordinal));
                       if (entries.Count == 0)
                               return;

                       ApplyComponentEntries(entries);
               }

              /// <summary>
              /// Processes deferred entries that belong to the provided <paramref name="componentID"/>.
              /// </summary>
              /// <param name="componentID">The <see cref="SaveableComponent.ComponentID"/> to process.</param>
              public void ProcessDeferredComponentsForComponentID(string componentID)
              {
                      if (string.IsNullOrEmpty(componentID))
                              return;

                      var entries = deferredComponents.TakeMatching(entry => string.Equals(entry.ComponentID, componentID, StringComparison.Ordinal));
                      if (entries.Count == 0)
                              return;

                      ApplyComponentEntries(entries);
              }

              /// <summary>
              /// Processes deferred entries that belong to any of the provided component IDs.
              /// </summary>
              /// <param name="componentIDs">Collection of <see cref="SaveableComponent.ComponentID"/> values to process.</param>
              public void ProcessDeferredComponentsForComponentIDs(IReadOnlyCollection<string> componentIDs)
              {
                      if (componentIDs == null || componentIDs.Count == 0)
                              return;

                      var set = new HashSet<string>(StringComparer.Ordinal);
                      foreach (string id in componentIDs)
                      {
                              if (!string.IsNullOrEmpty(id))
                                      set.Add(id);
                      }
                      if (set.Count == 0)
                              return;

                      var entries = deferredComponents.TakeMatching(entry => set.Contains(entry.ComponentID));
                      if (entries.Count == 0)
                              return;

                      ApplyComponentEntries(entries);
              }

               public void ProcessDeferredComponent(string uniqueIdentifier)
               {
                       if (string.IsNullOrEmpty(uniqueIdentifier))
                               return;

                       var entries = deferredComponents.TakeMatching(entry => string.Equals(entry.UniqueIdentifier, uniqueIdentifier, StringComparison.Ordinal));
                       if (entries.Count == 0)
                               return;

                       ApplyComponentEntries(entries);
               }

               public void ProcessDeferredComponentsByUniqueIDs(IReadOnlyCollection<string> uniqueIdentifiers)
               {
                       if (uniqueIdentifiers == null || uniqueIdentifiers.Count == 0)
                               return;

                       var set = new HashSet<string>(StringComparer.Ordinal);
                       foreach (string id in uniqueIdentifiers)
                       {
                               if (!string.IsNullOrEmpty(id))
                                       set.Add(id);
                       }
                       if (set.Count == 0)
                               return;

                       var entries = deferredComponents.TakeMatching(entry => set.Contains(entry.UniqueIdentifier));
                       if (entries.Count == 0)
                               return;

                       ApplyComponentEntries(entries);
               }

               public void ClearDeferredComponentData()
               {
                       deferredComponents.Clear();
               }

        /// <summary>
               /// Registers a SaveableComponent.
               /// </summary>
               public void RegisterSaveableComponent(ISaveable component)
               {
                       RegisterSaveableComponentInternal(
                               component,
                               "ComponentManager",
                               ComponentRegistrationRoute.Normal,
                               restoreExplicitlyDetachedComponent: true);
               }

               internal bool EnsureSaveableComponentRegistered(
                       ISaveable component,
                       string registrationSource)
               {
                       return RegisterSaveableComponentInternal(
                               component,
                               string.IsNullOrEmpty(registrationSource)
                                       ? "ComponentManager"
                                       : registrationSource,
                               ComponentRegistrationRoute.IdempotentEnsure,
                               restoreExplicitlyDetachedComponent:
                                       string.Equals(
                                               registrationSource,
                                               "ExplicitUtility",
                                               StringComparison.Ordinal));
               }

               private bool RegisterSaveableComponentInternal(
                       ISaveable component,
                       string registrationSource,
                       ComponentRegistrationRoute route = ComponentRegistrationRoute.Normal,
                       bool restoreExplicitlyDetachedComponent = false)
               {
                       if (restoreExplicitlyDetachedComponent &&
                           component is SaveableComponent saveableComponent)
                       {
                               saveableComponent.SetRuntimeSaveRegistrationSuppressed(false);
                       }

                       if (!TryAddGlobalRegistration(
                                   component,
                                   route,
                                   registrationSource))
                       {
                               return false;
                       }

                       // Runtime safety: components that start disabled skip Awake, so their HomeScene
                       // might be empty even when RememberHomeScene is enabled. Auto-fill for DesignScene
                       // so RememberHomeScene can apply snapshots correctly after registration.
                       if (Application.isPlaying &&
                           component is SaveableComponent sc &&
                           sc.RememberHomeScene &&
                           sc.HomeSceneMode == HomeSceneMode.DesignScene &&
                           string.IsNullOrEmpty(sc.HomeScene))
                       {
                               var activeScene = SceneManager.GetActiveScene();
                               if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.name))
                               {
                                       sc.HomeScene = activeScene.name;
                                       Logger.Log(
                                               $"[RememberHome] Auto-filled HomeScene='{activeScene.name}' for '{sc.UniqueIdentifier}' during registration (component may have started disabled).",
                                               LogCategory.ComponentManager,
                                               LogLevel.Info);
                               }
                       }

                       string pendingIdentifier = component.UniqueIdentifier;
                       if (restoration.Pending.TryGetValue(pendingIdentifier, out var pending))
                       {
                               //Debug.Log($"[ComponentManager] FOUND PENDING DATA for '{pendingIdentifier}' on '{(component as MonoBehaviour)?.gameObject.name ?? "Unknown"}', dataLen={pending?.Length ?? 0}");
                               Logger.Log($"ComponentManager: Found pending data for '{pendingIdentifier}' during registration on GameObject '{(component as MonoBehaviour)?.gameObject.name ?? "Unknown"}'.", LogCategory.ComponentManager, LogLevel.Info);
                               bool pendingHandled = false;
                               
                               // Check if this component should be handled by prefab blob system instead
                               if (component is SaveableComponent saveableComp && saveableComp.PrefabHandlesSerialization)
                               {
                                       Logger.Log($"ComponentManager: Skipping pending LoadData for '{pendingIdentifier}' - component is managed by prefab blob system.", LogCategory.ComponentManager, LogLevel.Info);
                                       MarkComponentDeserialized(component, pendingIdentifier);
                                       pendingHandled = true;
                               }
                               else if (HasComponentDeserialized(component, pendingIdentifier))
                               {
                                       Logger.Log($"ComponentManager: Skipping duplicate pending LoadData for '{pendingIdentifier}' - already deserialized.", LogCategory.ComponentManager, LogLevel.Warning);
                                       pendingHandled = true;
                               }
                               else if (!restoration.IsApplying(component, pendingIdentifier, pending))
                               {
                                       Logger.Log($"ComponentManager: Calling LoadData for '{pendingIdentifier}' from pending data during registration.", LogCategory.ComponentManager, LogLevel.Info);
                                       
                                       try
                                       {
                                               pendingHandled = RestoreComponent(component, pending, pendingIdentifier);
                                               Logger.Log($"ComponentManager: Successfully applied pending data to '{pendingIdentifier}'.", LogCategory.ComponentManager, LogLevel.Info);
                                       }
                                       catch (Exception ex)
                                       {
                                               Logger.Log($"ComponentManager: Error applying pending data to '{pendingIdentifier}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ComponentManager, LogLevel.Error);
                                       }
                               }

                               // A throwing LoadData callback must not consume the only copy of
                               // the loaded bytes. Leave it queued so a later registration can
                               // retry once the component is ready.
                               if (pendingHandled)
                                       RemovePendingComponentData(pendingIdentifier);
                       }

                       // If the component is configured to remember its home scene and we have a snapshot
                       // for this GameObject in the current scene, apply it now.
                       TryApplyRememberedSnapshot(component);

                       // Share State Across Scenes: apply cached in-memory state from a previous
                       // scene if no disk data (or home-scene snapshot) was loaded above.
                       if (!restoration.HasRestoreData(component, component.UniqueIdentifier) &&
                           component is SaveableComponent shareComp &&
                           shareComp.ShareStateAcrossScenes &&
                           sharedStateCache.TryGetValue(component.UniqueIdentifier, out var cachedData))
                       {
                               try
                               {
                                       Logger.Log(
                                               $"[SharedState] Applying cached cross-scene state to '{component.UniqueIdentifier}' on '{(component as MonoBehaviour)?.gameObject.name ?? "Unknown"}'.",
                                               LogCategory.ComponentManager, LogLevel.Info);
                                       RestoreComponent(component, cachedData, component.UniqueIdentifier);
                               }
                               catch (Exception ex)
                               {
                                       Logger.Log(
                                               $"[SharedState] Error applying cached state to '{component.UniqueIdentifier}': {ex.Message}\nStack Trace: {ex.StackTrace}",
                                               LogCategory.ComponentManager, LogLevel.Error);
                               }
                       }

                       ProcessQueuedParenting();
                       return true;
               }

               /// <summary>
               /// Reindexes internal lookup keys when a component's <see cref="ISaveable.UniqueIdentifier"/>
               /// changes at runtime (for example after deserializing a runtime-added component).
               /// </summary>
               public void ReindexComponentIdentifier(ISaveable component, string previousIdentifier)
               {
                       if (component == null)
                               return;

                       if (TryGetPrefabBlobOwner(component, out _))
                       {
                               RemoveGlobalRegistrationCore(
                                       component,
                                       clearDeserializedState: false,
                                       registrationSource: "IdRebind",
                                       logMissing: false);
                               return;
                       }

                       // Reindexing updates an existing global registration; it must not create a
                       // lookup-only registration for a component that is intentionally detached.
                       if (component is SaveableComponent saveableComponent &&
                           !saveableComponents.Contains(component))
                       {
                               RemoveGlobalRegistrationCore(
                                       component,
                                       clearDeserializedState: false,
                                       registrationSource: "IdRebind",
                                       logMissing: false);
                               return;
                       }

                       string currentIdentifier = null;
                       try
                       {
                               currentIdentifier = component.UniqueIdentifier;
                       }
                       catch
                       {
                               // If the component cannot report an identifier (e.g. destroyed), nothing to reindex.
                               return;
                       }

                       if (string.IsNullOrEmpty(currentIdentifier))
                       {
                               if (component is SaveableComponent)
                               {
                                       RemoveGlobalRegistrationCore(
                                               component,
                                               clearDeserializedState: false,
                                               registrationSource: "IdRebind",
                                               logMissing: false);
                               }
                               return;
                       }

                       if (component is SaveableComponent registeredComponent &&
                           registeredComponent != null)
                       {
                               registeredComponent.SetManagerRegistrationState(true, "IdRebind");
                       }

                       // Ensure the current key always resolves to this live component.
                       saveableLookup[currentIdentifier] = component;

                       if (string.IsNullOrEmpty(previousIdentifier) ||
                           string.Equals(previousIdentifier, currentIdentifier, StringComparison.Ordinal))
                       {
                               return;
                       }

                       // Remove stale key(s) that still point to this component.
                       if (saveableLookup.TryGetValue(previousIdentifier, out var mapped) &&
                           ReferenceEquals(mapped, component))
                       {
                               saveableLookup.Remove(previousIdentifier);
                       }
                       else
                       {
	                               var staleKeys = new List<string>();
	                               foreach (var kvp in saveableLookup)
	                               {
	                                       if (!string.Equals(kvp.Key, currentIdentifier, StringComparison.Ordinal)
	                                           && ReferenceEquals(kvp.Value, component))
	                                       {
	                                               staleKeys.Add(kvp.Key);
	                                       }
	                               }

	                               foreach (var staleKey in staleKeys)
	                               {
	                                       saveableLookup.Remove(staleKey);
	                               }
                       }

                       restoration.Reindex(component, previousIdentifier, currentIdentifier);

                       Logger.Log(
                               $"ComponentManager: Reindexed component identifier '{previousIdentifier}' -> '{currentIdentifier}'.",
                               LogCategory.ComponentManager,
                               LogLevel.Info);
               }

               public IEnumerator ApplyComponentDataCoroutine(SaveData saveData, int batchSize)
                       => ApplyComponentDataCoroutineCore(saveData, batchSize, RestoreGeneration);

               private IEnumerator ApplyComponentDataCoroutineCore(SaveData saveData, int batchSize, object generation)
               {
                       if (!ReferenceEquals(generation, RestoreGeneration))
                               yield break;

                       if (batchSize <= 0)
                       {
                               ApplyComponentData(saveData);
                               yield break;
                       }

                       if (saveData?.ComponentsData == null)
                       {
                               Logger.Log(
                                       "ComponentManager: No component data to apply (saveData or ComponentsData is null).",
                                       LogCategory.ComponentManager,
                                       LogLevel.Warning
                               );
                               yield break;
                       }

                       ClearDeferredComponentData();

                       // Disk data takes precedence — clear the in-memory shared-state cache.
                       sharedStateCache.Clear();
                       sharedStateRuntimePrefabWarnings.Clear();

                       DeferredComponentQueue.Partition(saveData, out var immediateEntries, out var deferredEntries);
                       if (deferredEntries.Count > 0)
                               QueueDeferredComponentData(deferredEntries);

                       if (immediateEntries.Count == 0)
                       {
                               ProcessQueuedParenting();
                               yield break;
                       }

                       yield return ApplyComponentEntriesCoroutine(immediateEntries, batchSize, generation);
               }

        /// <summary>
        /// Unregisters a SaveableComponent.
        /// </summary>
               public void UnregisterSaveableComponent(ISaveable component)
               {
                       UnregisterSaveableComponentInternal(
                               component,
                               explicitlyDetached: true);
               }

               internal void UnregisterSaveableComponentForLifecycle(
                       ISaveable component)
               {
                       UnregisterSaveableComponentInternal(
                               component,
                               explicitlyDetached: false);
               }

               private void UnregisterSaveableComponentInternal(
                       ISaveable component,
                       bool explicitlyDetached)
               {
                       if (component == null)
                       {
                               Logger.Log("ComponentManager: Attempted to unregister a null ISaveable.", LogCategory.ComponentManager, LogLevel.Warning);
                               return;
                       }

                       if (explicitlyDetached &&
                           component is SaveableComponent explicitlyDetachedComponent)
                       {
                               explicitlyDetachedComponent
                                       .SetRuntimeSaveRegistrationSuppressed(true);
                       }

                       // On a normal scene teardown SaveableComponent.OnDisable unregisters
                       // first, then OnDestroy performs a defensive second unregister. Only the
                       // first callback still represents a live registered owner. Capturing on
                       // the second callback can overwrite the good snapshot after equipment or
                       // child renderers have already begun destruction.
                       bool wasGloballyRegistered =
                               globalRegistrationMembership.Contains(component);

                       // Stop automatically reconciling this instance until a later lifecycle
                       // or explicit registration observes it again.
                       knownOwnershipComponents.Remove(component);
                       pendingOwnershipDataRetries.Remove(component);
                       if (component is SaveableComponent hierarchyComponent)
                               deferredHierarchyOwnershipRefreshes.Remove(hierarchyComponent);

                       // Blob ownership changes must not capture Share State Across Scenes data or
                       // consume/clear load bookkeeping. They only alter global routing.
                       if (TryGetPrefabBlobOwner(component, out _))
                       {
                               RemoveGlobalRegistrationCore(
                                       component,
                                       clearDeserializedState: false,
                                       registrationSource: "PrefabOwnershipTransfer",
                                       logMissing: false);
                               return;
                       }

                       // Share State Across Scenes: capture current state before removal so a
                       // future component with the same identifier can pick it up.
                       if (wasGloballyRegistered &&
                           component is SaveableComponent shareComp &&
                           shareComp.ShareStateAcrossScenes &&
                           !string.IsNullOrEmpty(component.UniqueIdentifier))
                       {
                               try
                               {
                                       byte[] snapshot = component.SaveData();
                                       if (snapshot != null && snapshot.Length > 0)
                                       {
                                               sharedStateCache[component.UniqueIdentifier] = snapshot;
                                               Logger.Log(
                                                       $"[SharedState] Cached state for '{component.UniqueIdentifier}' ({snapshot.Length} bytes).",
                                                       LogCategory.ComponentManager, LogLevel.Info);

                                               // Scene activation and teardown callbacks are not guaranteed to
                                               // register/unregister matching scene instances in one fixed order.
                                               // If the destination registered before this source finished
                                               // unregistering, its registration-time cache lookup already missed.
                                               // Deliver the new snapshot now so both lifecycle orders behave the
                                               // same, while respecting disk/home-scene data that was already applied.
                                               TryApplySharedStateToRegisteredMatch(
                                                       component,
                                                       component.UniqueIdentifier,
                                                       snapshot);
                                       }
                               }
                               catch (Exception ex)
                               {
                                       Logger.Log(
                                               $"[SharedState] Failed to capture state for '{component.UniqueIdentifier}': {ex.Message}\nStack Trace: {ex.StackTrace}",
                                               LogCategory.ComponentManager, LogLevel.Warning);
                               }

                               // Warn once if this looks like a runtime-instantiated prefab where
                               // Share State Across Scenes will silently have no effect.
                               if (shareComp.gameObject != null &&
                                   sharedStateRuntimePrefabWarnings.Add(component.UniqueIdentifier))
                               {
                                       string goName = shareComp.gameObject.name;
                                       var root = shareComp.transform.root;
                                       bool looksInstantiated =
                                               goName.EndsWith("(Clone)", StringComparison.Ordinal) ||
                                               (root != null && root.name.EndsWith("(Clone)", StringComparison.Ordinal));

                                       if (looksInstantiated)
                                       {
                                               Debug.LogWarning(
                                                       $"[Crystal Save] Share State Across Scenes is enabled on '{shareComp.GetType().Name}' " +
                                                       $"on '{goName}', but this object appears to be a runtime-instantiated prefab. " +
                                                       $"Each Instantiate() call generates a new random UniqueID, so the cached state " +
                                                       $"will never match a future instance and the feature has no effect.\n" +
                                                       $"This feature is designed for scene-placed objects or scene-baked SaveablePrefab " +
                                                       $"instances whose UniqueIDs are stable across scenes.\n" +
                                                       $"To fix: either place the prefab in each scene at edit time (scene-baked), or " +
                                                       $"disable Share State Across Scenes on this component.",
                                                       shareComp.gameObject);
                                       }
                               }
                       }

                       RemoveGlobalRegistrationCore(
                               component,
                               clearDeserializedState: true,
                               registrationSource: "ComponentManager",
                               logMissing: wasGloballyRegistered);
               }

               private void TryApplySharedStateToRegisteredMatch(
                       ISaveable source,
                       string identifier,
                       byte[] data)
               {
                       if (string.IsNullOrEmpty(identifier) ||
                           data == null ||
                           data.Length == 0)
                       {
                               return;
                       }

                       // Use a stable snapshot because LoadData callbacks are allowed to alter
                       // component registration. A full identifier is expected to have at most
                       // one live destination; duplicate-ID diagnostics cover ambiguous setups.
                       ISaveable[] registeredSnapshot = saveableComponents.ToArray();
                       for (int i = 0; i < registeredSnapshot.Length; i++)
                       {
                               ISaveable candidate = registeredSnapshot[i];
                               if (candidate == null ||
                                   ReferenceEquals(candidate, source) ||
                                   !globalRegistrationMembership.Contains(candidate) ||
                                   candidate is not SaveableComponent sharedCandidate ||
                                   !sharedCandidate.ShareStateAcrossScenes)
                               {
                                       continue;
                               }

                               if (candidate is MonoBehaviour behaviour && behaviour == null)
                                       continue;
                               if (TryGetPrefabBlobOwner(candidate, out _))
                                       continue;
                               if (!string.Equals(
                                       GetIdentifierSafely(candidate),
                                       identifier,
                                       StringComparison.Ordinal))
                               {
                                       continue;
                               }
                               if (restoration.HasRestoreData(candidate, identifier))
                                       return;

                               try
                               {
                                       Logger.Log(
                                               $"[SharedState] Applying newly cached cross-scene state to already-registered '{identifier}' on '{sharedCandidate.gameObject.name}'.",
                                               LogCategory.ComponentManager,
                                               LogLevel.Info);
                                       RestoreComponent(candidate, data, identifier);
                               }
                               catch (Exception ex)
                               {
                                       // Keep the cache entry available so a later registration can retry.
                                       Logger.Log(
                                               $"[SharedState] Error applying newly cached state to '{identifier}': {ex.Message}\nStack Trace: {ex.StackTrace}",
                                               LogCategory.ComponentManager,
                                               LogLevel.Error);
                               }

                               return;
                       }
               }

                /// <summary>
                /// Destroys the <see cref="GameObject"/> whose
                /// <see cref="UniqueID"/> or <see cref="SaveableComponent.GameObjectUniqueID"/>
                /// matches <paramref name="uniqueID"/>.
                /// </summary>
                /// <param name="uniqueID">Unique ID from a <see cref="UniqueID"/> component.</param>
               public void DestroyGameObjectByUniqueID(string uniqueID)
               {
                       if (string.IsNullOrEmpty(uniqueID))
                       {
                               Logger.Log("ComponentManager: DestroyGameObjectByUniqueID called with an empty ID.", LogCategory.ComponentManager, LogLevel.Warning);
                               return;
                       }

                       GameObject obj = Owner?.FindGameObjectByUniqueID(uniqueID, SaveManager.IdentifierType.UniqueID);
                       if (obj == null)
                       {
                               Logger.Log($"ComponentManager: No GameObject found with UniqueID '{uniqueID}'.", LogCategory.ComponentManager, LogLevel.Warning);
                               return;
                       }

                       Owner?.SoftUnregisterGameObject(obj);
                       DestroyHelper.DestroyWithLogging(obj, $"ComponentManager.DestroyGameObjectByUniqueID({uniqueID})");
               }

                /// <summary>
                /// Destroys each <see cref="GameObject"/> whose
                /// <see cref="UniqueID"/> or <see cref="SaveableComponent.GameObjectUniqueID"/>
                /// matches any of the provided <paramref name="uniqueIDs"/>.
                /// </summary>
                /// <param name="uniqueIDs">List of Unique IDs from <see cref="UniqueID"/> components.</param>
                public void DestroyGameObjectByUniqueID(List<string> uniqueIDs)
                {
                        if (uniqueIDs == null || uniqueIDs.Count == 0)
                        {
                                Logger.Log("ComponentManager: DestroyGameObjectByUniqueID called with an empty ID list.", LogCategory.ComponentManager, LogLevel.Warning);
                                return;
                        }

                        foreach (var id in uniqueIDs)
                        {
                                DestroyGameObjectByUniqueID(id);
                        }
                }

        /// <summary>
        /// Collects serialized data from all registered ISaveable components.
        /// </summary>
               public void CollectComponentData(SaveData saveData)
               {
                       if (saveData == null)
                               return;

                       FlushDeferredPrefabOwnershipRefreshes();
                       using SaveDataCollectionOwnershipScope ownershipScope =
                               BeginSaveDataCollectionOwnershipScope();
                       PurgeDestroyedComponents();

                       if (saveData.ComponentMetadata == null)
                               saveData.ComponentMetadata = new Dictionary<string, ComponentDataMetadata>();
                       else
                               saveData.ComponentMetadata.Clear();

                       // Build a fast lookup of any deferred component entries we still have queued,
                       // so we can persist their last-known bytes instead of overwriting them with
                       // the current (design-time) runtime state when saving mid-stream.
                       var deferredLookup = deferredComponents.SnapshotByIdentifier();

                       // Track component IDs already written so duplicate keys are visible and
                       // deferred/pending fallbacks do not add redundant entries.
                       var written = new HashSet<string>(StringComparer.Ordinal);
                       var untracedWrites = new HashSet<string>(StringComparer.Ordinal);

                       // User callbacks may enable, disable, register, or unregister components
                       // while SaveData runs. A stable operation snapshot prevents those legal
                       // lifecycle changes from invalidating the live-list enumerator. Components
                       // added during the pass participate in the next save.
                       ISaveable[] collectionSnapshot =
                               saveDataCollectionDepth > 0
                                       ? collectionGlobalComponents
                                       : saveableComponents.ToArray();
                       foreach (var component in collectionSnapshot)
                       {
                               // Skip entries whose underlying Unity object has been destroyed
                               if (component is MonoBehaviour mb && mb == null)
                                       continue;
                               // A prior SaveData callback may have disabled or explicitly
                               // unregistered this later snapshot entry.
                               if (!globalRegistrationMembership.Contains(component))
                                       continue;
                               // Defensive routing guard: a blob-owned helper must never leak into
                               // global component payloads, even if a stale registration survived.
                               if (TryGetPrefabBlobOwner(component, out _))
                                       continue;
                               // Also skip if the owning GameObject is marked destroyed in tracker
                               try
                               {
                                       var goUid = ExtractGameObjectID(component.UniqueIdentifier);
                                       if (!string.IsNullOrEmpty(goUid) && Owner != null && Owner.IsGameObjectDestroyed(goUid))
                                               continue;
                               }
                               catch { /* best-effort guard */ }
                               if (!IsValidSaveable(component, nameof(CollectComponentData)))
                                       continue;

                               try
                               {
                                       var id = component.UniqueIdentifier;
                                       bool duplicateKey = written.Contains(id);

                                       // If this component's data is still deferred from a previous load,
                                       // prefer the deferred bytes instead of live-serializing its current
                                       // design-time state. This preserves correctness across save/load cycles
                                       // when using 'Defer Until Requested'.
                                       if (deferredLookup.TryGetValue(id, out var deferredEntry) && deferredEntry.Data != null && deferredEntry.Data.Length > 0)
                                       {
                                               bool tracedWrite =
                                                       component is SaveableComponent deferredComponent &&
                                                       SaveableComponent.RecordSaveDataCollectionWrite(
                                                               deferredComponent,
                                                               SaveableComponent.SaveDataCallRoute
                                                                       .DeferredGlobalPayload);
                                               if (duplicateKey &&
                                                   (!tracedWrite ||
                                                    untracedWrites.Contains(id)))
                                               {
                                                       LogDuplicateSaveKey(
                                                               nameof(CollectComponentData),
                                                               id,
                                                               component);
                                               }
                                               saveData.ComponentsData[id] = deferredEntry.Data;
                                               if (component is SaveableComponent sc)
                                               {
                                                       string homeScene = sc.RememberHomeScene ? sc.HomeScene : deferredEntry.HomeScene;
                                                       // Force Defer flag to true to keep the entry deferred on next load
                                                       saveData.ComponentMetadata[id] = new ComponentDataMetadata(
                                                               sc.LoadPriority,
                                                               true,
                                                               homeScene);
                                               }
                                               else
                                               {
                                                       // Non SaveableComponent implementers: persist metadata if it existed; otherwise omit.
                                                       saveData.ComponentMetadata[id] = new ComponentDataMetadata(50, true, null);
                                               }
#if UNITY_EDITOR
                                               Logger.Log($"[CrystalSaveDebug][CompSave] Using deferred bytes for '{id}'.", LogCategory.ComponentManager, LogLevel.Info);
#endif
                                               written.Add(id);
                                               if (!tracedWrite)
                                                       untracedWrites.Add(id);
                                               continue;
                                       }

                                       // Also prefer any pending data that was queued because the component
                                       // hadn't registered yet during load (rare for scene components).
                                       if (restoration.Pending.TryGetValue(id, out var pendingBytes) && pendingBytes != null && pendingBytes.Length > 0)
                                       {
                                               bool tracedWrite =
                                                       component is SaveableComponent pendingComponent &&
                                                       SaveableComponent.RecordSaveDataCollectionWrite(
                                                               pendingComponent,
                                                               SaveableComponent.SaveDataCallRoute
                                                                       .PendingGlobalPayload);
                                               if (duplicateKey &&
                                                   (!tracedWrite ||
                                                    untracedWrites.Contains(id)))
                                               {
                                                       LogDuplicateSaveKey(
                                                               nameof(CollectComponentData),
                                                               id,
                                                               component);
                                               }
                                               saveData.ComponentsData[id] = pendingBytes;
                                               if (component is SaveableComponent sc)
                                               {
                                                       string homeScene = sc.RememberHomeScene ? sc.HomeScene : null;
                                                       saveData.ComponentMetadata[id] = new ComponentDataMetadata(sc.LoadPriority, sc.DeferLowPriorityUntilRequested, homeScene);
                                               }
                                               else
                                               {
                                                       saveData.ComponentMetadata.Remove(id);
                                               }
#if UNITY_EDITOR
                                               Logger.Log($"[CrystalSaveDebug][CompSave] Using pending bytes for '{id}'.", LogCategory.ComponentManager, LogLevel.Info);
#endif
                                               written.Add(id);
                                               if (!tracedWrite)
                                                       untracedWrites.Add(id);
                                               continue;
                                       }

                                       // Fall back to live serialization of the component
                                       if (duplicateKey &&
                                           (component is not SaveableComponent ||
                                            !SaveableComponent.IsSaveCallDiagnosticsActive ||
                                            untracedWrites.Contains(id)))
                                       {
                                               LogDuplicateSaveKey(
                                                       nameof(CollectComponentData),
                                                       id,
                                                       component);
                                       }

                                       using var componentCapture = ComponentOperationScope.Capture(id, "Global component");
                                       byte[] serializedData;
                                       if (component is SaveableComponent saveableComponent)
                                       {
                                               serializedData =
                                                       SaveableComponent.InvokeSaveDataWithRoute(
                                                               saveableComponent,
                                                               SaveableComponent.SaveDataCallRoute
                                                                       .GlobalComponentCollection);
                                       }
                                       else
                                       {
                                               serializedData = component.SaveData();
                                       }
                                       if (serializedData != null && serializedData.Length > 0)
                                       {
                                               saveData.ComponentsData[id] = serializedData;
                                               if (component is SaveableComponent sc)
                                               {
                                                       string homeScene = sc.RememberHomeScene ? sc.HomeScene : null;
                                                       saveData.ComponentMetadata[id] = new ComponentDataMetadata(sc.LoadPriority, sc.DeferLowPriorityUntilRequested, homeScene);
                                               }
                                               else
                                               {
                                                       saveData.ComponentMetadata.Remove(id);
                                               }
                                               Logger.Log($"ComponentManager: Serialized data for '{id}'.", LogCategory.ComponentManager, LogLevel.Info);
                                               written.Add(id);
                                               if (component is not SaveableComponent)
                                                       untracedWrites.Add(id);
                                       }
                                       else
                                       {
                                               saveData.ComponentMetadata.Remove(id);
                                       }
                               }
                               catch (Exception ex)
                               {
                                       ComponentOperationScope.Record(new InvalidOperationException($"Component {component.UniqueIdentifier}: {ex.Message}", ex));
                                       Logger.Log($"ComponentManager: Error serializing '{component.UniqueIdentifier}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ComponentManager, LogLevel.Error);
                               }
                       }

                       // Also persist any deferred entries for components that are not currently registered
                       // so they are not lost on save. This preserves "defer across saves" semantics.
                       foreach (var kv in deferredLookup)
                       {
                               var id = kv.Key;
                               var entry = kv.Value;
                               if (written.Contains(id)) continue;
                               if (entry?.Data == null || entry.Data.Length == 0) continue;

                               saveData.ComponentsData[id] = entry.Data;
                               // Use the preserved LoadPriority and force Defer = true
                               saveData.ComponentMetadata[id] = new ComponentDataMetadata(entry.LoadPriority, true, entry.HomeScene);
#if UNITY_EDITOR
                               Logger.Log($"[CrystalSaveDebug][CompSave] Added unregistered deferred entry '{id}'.", LogCategory.ComponentManager, LogLevel.Info);
#endif
                               written.Add(id);
                       }

                       // Finally, persist any pending component data for unregistered components
                       // that wasn't covered by deferredLookup, to avoid losing their state.
                       //
                       // IMPORTANT: Only persist pending entries whose owning prefab instance is
                       // still tracked by PrefabManager. Entries for prefab instances that were never
                       // re-instantiated during loading are orphans — they would accumulate in the
                       // save file indefinitely, growing by ~60-80 entries per save/load cycle.
                       // We log how many entries we skip so the diagnostic trail is visible.
                       int pendingPersisted = 0;
                       int pendingDropped = 0;
                       foreach (var kv in restoration.Pending)
                       {
                               var id = kv.Key;
                               var bytes = kv.Value;
                               if (written.Contains(id)) continue;
                               if (bytes == null || bytes.Length == 0) continue;

                               // Check if the owning prefab instance still exists in the scene.
                               // The composite key format is "{instanceGUID}_{componentID}".
                               // If PrefabManager doesn't have a live mapping for the instanceGUID,
                               // and SaveManager can't find the GameObject, this entry is orphaned
                               // — UNLESS it belongs to a prefab whose home scene is not loaded.
                               bool hasLiveInstance = false;
                               try
                               {
                                       string instanceGUID = ExtractGameObjectID(id);
                                       if (!string.IsNullOrEmpty(instanceGUID))
                                       {
                                               // Check if a live component registered with this key
                                               if (saveableLookup.ContainsKey(id))
                                               {
                                                       hasLiveInstance = true;
                                               }
                                               else
                                               {
                                                       // Check if the owning GameObject still exists
                                                       var go = Owner?.FindGameObjectByUniqueID(instanceGUID, SaveManager.IdentifierType.UniqueID);
                                                       if (go != null)
                                                       {
                                                               hasLiveInstance = true;
                                                       }
                                                       else
                                                       {
                                                               // Check if this entry belongs to a prefab in an
                                                               // unloaded scene (Remember Home Scene). Look up
                                                               // the prefab entry in the current save data to
                                                               // find its HomeScene.
                                                               var currentSave = Owner?.CurrentSaveData;
                                                               if (currentSave?.Prefabs != null)
                                                               {
                                                                       foreach (var pd in currentSave.Prefabs)
                                                                       {
                                                                               if (pd != null && pd.InstanceID == instanceGUID
                                                                                       && !string.IsNullOrEmpty(pd.HomeScene))
                                                                               {
                                                                                       var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(pd.HomeScene);
                                                                                       if (!scene.IsValid() || !scene.isLoaded)
                                                                                       {
                                                                                               hasLiveInstance = true; // belongs to unloaded scene — preserve
                                                                                       }
                                                                                       break;
                                                                               }
                                                                       }
                                                               }
                                                       }
                                               }
                                       }
                               }
                               catch { /* best-effort */ }

                               if (!hasLiveInstance)
                               {
                                       pendingDropped++;
                                       continue; // Don't persist orphaned pending data
                               }

                               saveData.ComponentsData[id] = bytes;
                               // No strong signal to defer; keep metadata minimal here
                               if (!saveData.ComponentMetadata.ContainsKey(id))
                                       saveData.ComponentMetadata[id] = new ComponentDataMetadata(50, false, null);
#if UNITY_EDITOR
                               Logger.Log($"[CrystalSaveDebug][CompSave] Added unregistered pending entry '{id}'.", LogCategory.ComponentManager, LogLevel.Info);
#endif
                               pendingPersisted++;
                       }
                       if (pendingDropped > 0 || pendingPersisted > 0)
                       {
                               UnityEngine.Debug.Log($"[ComponentManager] CollectComponentData: Pending entries — persisted={pendingPersisted}, dropped(orphaned)={pendingDropped}");
                       }

                       HomeScenes.Persist(saveData);
               }

        /// <summary>
        /// Applies serialized data to the corresponding ISaveable components.
        /// </summary>
               public void ApplyComponentData(SaveData saveData)
               {
                       Logger.Log(
                               $"[ComponentManager] ApplyComponentData called, saveData null: {saveData == null}, ComponentsData null: {saveData?.ComponentsData == null}, ComponentsData count: {saveData?.ComponentsData?.Count ?? 0}",
                               LogCategory.ComponentManager,
                               LogLevel.Info);
                       
                       if (saveData?.ComponentsData == null)
                       {
                               Logger.Log(
                                       "ComponentManager: No component data to apply (saveData or ComponentsData is null).",
                                       LogLevel.Warning
                               );
                               return;
                       }

                       ClearDeferredComponentData();

                       // Disk data takes precedence — clear the in-memory shared-state cache.
                       sharedStateCache.Clear();
                       sharedStateRuntimePrefabWarnings.Clear();

                       DeferredComponentQueue.Partition(saveData, out var immediateEntries, out var deferredEntries);
                       Logger.Log(
                               $"[ComponentManager] ApplyComponentData: immediateEntries={immediateEntries.Count}, deferredEntries={deferredEntries.Count}",
                               LogCategory.ComponentManager,
                               LogLevel.Info);
                       if (deferredEntries.Count > 0)
                               QueueDeferredComponentData(deferredEntries);

                       ApplyComponentEntries(immediateEntries);
               }

        private void QueueDeferredComponentData(IEnumerable<ComponentRestoreEntry> entries)
        {
            var scenes = deferredComponents.Enqueue(entries);
            if (scenes.Count > 0) OnDeferredComponentsQueued?.Invoke(scenes);
        }

               /// <summary>
               /// Removes stale ISaveable references whose backing Unity object has already
               /// been destroyed, plus helpers now owned by a SaveablePrefab component blob.
               /// </summary>
               private void PurgeDestroyedComponents()
               {
                       for (int i = saveableComponents.Count - 1; i >= 0; i--)
                       {
                               var component = saveableComponents[i];
                               bool destroyed = component is MonoBehaviour mb && mb == null;
                               bool blobOwned = !destroyed && TryGetPrefabBlobOwner(component, out _);
                               if (!destroyed && !blobOwned)
                                       continue;

                               pendingOwnershipDataRetries.Remove(component);
                               RemoveGlobalRegistrationCore(
                                       component,
                                       clearDeserializedState: false,
                                       registrationSource: blobOwned
                                               ? "PrefabOwnershipTransfer"
                                               : "ComponentManagerPurge",
                                       logMissing: false);
                       }
               }

               private void ApplyComponentEntries(List<ComponentRestoreEntry> entries)
               {
                       if (entries == null || entries.Count == 0)
                       {
                               ProcessQueuedParenting();
                               return;
                       }

                       PurgeDestroyedComponents();

                       foreach (var entry in entries)
                               ApplyComponentEntry(entry);

                       ProcessQueuedParenting();
               }

               private IEnumerator ApplyComponentEntriesCoroutine(List<ComponentRestoreEntry> entries, int batchSize, object generation)
               {
                       // These public batches may be driven outside the SaveManager queue.
                       // Keep the generation captured at API invocation even when Unity resumes
                       // the nested iterator after a different full snapshot has been accepted.
                       if (!ReferenceEquals(generation, RestoreGeneration))
                               yield break;

                       if (entries == null || entries.Count == 0)
                       {
                               ProcessQueuedParenting();
                               yield break;
                       }

                       PurgeDestroyedComponents();

                       int processed = 0;
                       foreach (var entry in entries)
                       {
                               if (!ReferenceEquals(generation, RestoreGeneration))
                                       yield break;

                               ApplyComponentEntry(entry);
                               processed++;
                               if (batchSize > 0 && processed >= batchSize)
                               {
                                       processed = 0;
                                       yield return null;
                               }
                       }

                       if (ReferenceEquals(generation, RestoreGeneration))
                               ProcessQueuedParenting();
               }

               private void ApplyComponentEntry(ComponentRestoreEntry entry)
               {
                       if (entry == null || string.IsNullOrEmpty(entry.UniqueIdentifier) || entry.Data == null || entry.Data.Length == 0)
                               return;

                       string uniqueID = entry.UniqueIdentifier;
                       //Debug.Log($"[ComponentManager] ApplyComponentEntry: Processing uniqueID='{uniqueID}', dataLen={entry.Data.Length}");
                       var comp = FindComponentByUniqueID(uniqueID);
                       if (comp is MonoBehaviour foundMb && foundMb == null)
                               comp = null;

                       if (comp == null)
                       {
                               // Fallback: try matching by componentID alone, but ONLY for scene-baked
                               // objects (not SaveablePrefab instances). For prefabs, each instance has
                               // a unique instanceGUID prefix so the full key must match. Matching by
                               // componentID alone would silently route ALL instances' data to the
                               // first-registered component, causing data loss.
                               string compId = ExtractComponentID(uniqueID);
                               if (!string.IsNullOrEmpty(compId))
                               {
	                                       for (int i = 0; i < saveableComponents.Count; i++)
	                                       {
	                                               SaveableComponent candidate = saveableComponents[i] as SaveableComponent;
	                                               if (candidate == null)
	                                                       continue;

	                                               if (candidate.ComponentID == compId &&
	                                                   candidate.GetComponentInParent<SaveablePrefab>(true) == null)
	                                               {
	                                                       comp = candidate;
	                                                       break;
	                                               }
	                                       }
                               }

                               if (comp == null)
                               {
                                       //Debug.Log($"[ComponentManager] QUEUING PENDING DATA for '{uniqueID}', dataLen={entry.Data?.Length ?? 0} (component not found)");
                                       Logger.Log($"ComponentManager: Queuing data for '{uniqueID}' until component registers.", LogCategory.ComponentManager, LogLevel.Info);
                                       QueuePendingComponentData(uniqueID, entry.Data);
                                       return;
                               }
                       }

                       if (comp is MonoBehaviour resolvedMb && resolvedMb == null)
                       {
                               Logger.Log($"ComponentManager: Queuing data for '{uniqueID}' until component registers.", LogCategory.ComponentManager, LogLevel.Info);
                               QueuePendingComponentData(uniqueID, entry.Data);
                               return;
                       }

                       // Check if this component is managed by a prefab with TrackComponentBlobs enabled
                       if (comp is SaveableComponent saveableComp && saveableComp.PrefabHandlesSerialization)
                       {
                               Logger.Log($"ComponentManager: Skipping LoadData for '{uniqueID}' - component is managed by prefab blob system on GameObject '{saveableComp.gameObject.name}'.", LogCategory.ComponentManager, LogLevel.Info);
                               return;
                       }

                       if (HasComponentDeserialized(comp, uniqueID))
                       {
                               Logger.Log($"ComponentManager: Skipping duplicate LoadData for '{uniqueID}' - already deserialized.", LogCategory.ComponentManager, LogLevel.Warning);
                               return;
                       }


                       
                       try
                       {
                               if (RestoreComponent(comp, entry.Data, uniqueID))
                                   Logger.Log($"ComponentManager: Successfully applied data to '{uniqueID}'.", LogCategory.ComponentManager, LogLevel.Info);
                       }
                       catch (Exception ex)
                       {
                               Logger.Log($"ComponentManager: Error applying data to '{uniqueID}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ComponentManager, LogLevel.Error);
                       }
               }

        private static string ExtractGameObjectID(string identifier) => ComponentRestoreEntry.ExtractGameObjectID(identifier);
        private static string ExtractComponentID(string identifier) => ComponentRestoreEntry.ExtractComponentID(identifier);

               /// <summary>
               /// Captures the runtime state of all ISaveable components on the provided GameObject
               /// and stores it in the scene snapshot cache under the current active scene.
               /// </summary>
        public void SnapshotObjectToSceneCache(GameObject obj) => HomeScenes.SnapshotObjectToSceneCache(obj, Owner);
        public void SnapshotCurrentSceneAll() => HomeScenes.SnapshotCurrentSceneAll(Owner);
        public void SnapshotSceneAll(Scene scene) => HomeScenes.SnapshotSceneAll(scene, Owner);
        public void ClearSceneSnapshot(string sceneName) => HomeScenes.ClearSceneSnapshot(sceneName);
        private void TryApplyRememberedSnapshot(ISaveable component) => HomeScenes.TryApplyRememberedSnapshot(component);

        /// <summary>
        /// Retrieves all registered SaveableComponents.
        /// </summary>
        public IReadOnlyList<ISaveable> GetSaveableComponents()
        {
            FlushDeferredPrefabOwnershipRefreshes();
            PurgeDestroyedComponents();
            return saveableComponents.AsReadOnly();
        }

        // A replacement manager gets a new registry while the scene helpers can
        // remain alive. Transfer registration intent only for this owner's members;
        // a broad scene scan would change disabled/suppressed registration policies.
        internal void PrepareRegistrationsForManagerReplacement()
        {
            foreach (var component in globalRegistrationMembership.ToArray())
                if (component is SaveableComponent helper && helper != null)
                    helper.AwaitReplacementSaveManager();
        }

                /// <summary>
                /// Replaces the internal sceneSnapshots cache with data provided from loaded SaveData.
                /// Call this early during load before components register so TryApplyRememberedSnapshot can succeed.
                /// </summary>
        public void ImportHomeSceneSnapshots(
            Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>> snapshots,
            Dictionary<string, Dictionary<string, string>> prefabRefs = null)
            => HomeScenes.ImportHomeSceneSnapshots(snapshots, prefabRefs);

        public void InstantiateMissingHomeSceneObjectsForLoadedScenes()
            => HomeScenes.InstantiateMissingHomeSceneObjectsForLoadedScenes(Owner);

        #endregion

        #region Parenting Management

        private readonly List<ParentingRequest> parentingQueue = new List<ParentingRequest>();

        /// <summary>
        /// Queues a parenting request to be processed after all components are loaded.
        /// </summary>
        /// <param name="childTransform">The Transform of the child GameObject.</param>
        /// <param name="parentUniqueID">The UniqueID of the parent GameObject.</param>
        public void QueueParenting(Transform child, string parentUniqueID)
        {
            if (child == null || string.IsNullOrEmpty(parentUniqueID))
            {
                Logger.Log("ComponentManager: Invalid parenting request. ChildTransform or ParentUniqueID is null.", LogCategory.ComponentManager, LogLevel.Warning);
                return;
            }

            parentingQueue.Add(new ParentingRequest { Child = child, ParentUniqueID = parentUniqueID });
            //Debug.Log($"[ComponentManager] QueueParenting - Queued parenting for '{child.gameObject.name}' (Active: {child.gameObject.activeSelf}) to parent ID '{parentUniqueID}'.");
        }

        /// <summary>
        /// Processes all queued parenting requests.
        /// </summary>
        private void ProcessQueuedParenting()
        {
            if (parentingQueue.Count == 0) return;

            //Debug.Log($"[ComponentManager] ProcessQueuedParenting - Processing {parentingQueue.Count} parenting requests.");
            var manager = Owner;
            for (int iteration = 0; iteration < MaxParentingResolveIterations && parentingQueue.Count > 0; iteration++)
            {
                int unresolvedAtStart = parentingQueue.Count;

                for (int i = parentingQueue.Count - 1; i >= 0; i--)
                {
                    var req = parentingQueue[i];

                    // Skip and remove if the child was destroyed in the meantime
                    if (req.Child == null)
                    {
                        parentingQueue.RemoveAt(i);
                        Logger.Log($"ComponentManager: Skipped parenting request for destroyed child (parent ID '{req.ParentUniqueID}').", LogCategory.ComponentManager, LogLevel.Warning);
                        continue;
                    }

                    GameObject parent = manager?.FindGameObjectByUniqueID(req.ParentUniqueID, SaveManager.IdentifierType.UniqueID);
                    if (parent == null)
                    {
                        //Debug.Log($"[ComponentManager] ProcessQueuedParenting - Iteration {iteration}: Parent with ID '{req.ParentUniqueID}' not found for child '{req.Child.gameObject.name}' (Active: {req.Child.gameObject.activeSelf}).");
                        continue;
                    }

                    //Debug.Log($"[ComponentManager] ProcessQueuedParenting - Parenting '{req.Child.gameObject.name}' (ActiveInHierarchy: {req.Child.gameObject.activeInHierarchy}) to '{parent.name}' (ActiveInHierarchy: {parent.gameObject.activeInHierarchy}).");
                    req.Child.SetParent(parent.transform);
                    parentingQueue.RemoveAt(i);
                    //Debug.Log($"[ComponentManager] ProcessQueuedParenting - Successfully parented '{req.Child.gameObject.name}' to '{parent.name}'. Current parent: '{req.Child.parent?.name ?? "null"}'.");
                }

                if (parentingQueue.Count == unresolvedAtStart) break;
            }

            if (parentingQueue.Count > 0)
                Logger.Log($"[ComponentManager] ProcessQueuedParenting - {parentingQueue.Count} parenting requests unresolved after retries.", LogCategory.ComponentManager, LogLevel.Warning);
        }

        #endregion

        #region Helper Methods

        private ISaveable FindComponentByUniqueID(string uniqueID)
        {
            FlushDeferredPrefabOwnershipRefreshes();
            PurgeDestroyedComponents();

            if (Owner?.SaveSettings?.enableComponentLookupCache == true)
            {
                if (saveableLookup.TryGetValue(uniqueID, out var comp))
                {
                    // Safety check: ensure the component hasn't been destroyed (Unity null check)
                    if (comp is MonoBehaviour mb && mb == null)
                    {
                                // Stale reference - remove it from lookup and return null
                                saveableLookup.Remove(uniqueID);
                                saveableComponents.Remove(comp);
                                globalRegistrationMembership.Remove(comp);
                                return null;
                    }
                    return comp;
                }

                // Cache miss recovery: scan the live list once and repopulate lookup entries.
                // This heals key drift when a component registered before its identifier changed.
                for (int i = saveableComponents.Count - 1; i >= 0; i--)
                {
                        var candidate = saveableComponents[i];
                        if (candidate == null)
                        {
                                globalRegistrationMembership.Remove(candidate);
                                saveableComponents.RemoveAt(i);
                                continue;
                        }

                        if (candidate is MonoBehaviour candidateMb && candidateMb == null)
                        {
                                globalRegistrationMembership.Remove(candidate);
                                saveableComponents.RemoveAt(i);
                                continue;
                        }

                        string candidateId = null;
                        try
                        {
                                candidateId = candidate.UniqueIdentifier;
                        }
                        catch
                        {
                                continue;
                        }

                        if (string.IsNullOrEmpty(candidateId))
                                continue;

                        saveableLookup[candidateId] = candidate;

                        if (string.Equals(candidateId, uniqueID, StringComparison.Ordinal))
                                return candidate;
                }

                return null;
            }

	            for (int i = 0; i < saveableComponents.Count; i++)
	            {
	                ISaveable candidate = saveableComponents[i];
	                if (candidate is MonoBehaviour mb && mb == null)
	                    continue;

	                if (candidate.UniqueIdentifier == uniqueID)
	                    return candidate;
	            }

	            return null;
	        }

        public bool Contains(ISaveable comp)
        {
            if (comp == null)
                return false;

            if (TryGetPrefabBlobOwner(comp, out _))
            {
                bool hasStaleRegistration =
                    globalRegistrationMembership.Contains(comp);
                if (!hasStaleRegistration)
                {
                    foreach (var pair in saveableLookup)
                    {
                        if (!ReferenceEquals(pair.Value, comp))
                            continue;

                        hasStaleRegistration = true;
                        break;
                    }
                }

                if (hasStaleRegistration)
                {
                    RemoveGlobalRegistrationCore(
                        comp,
                        clearDeserializedState: false,
                        registrationSource: "PrefabOwnershipTransfer",
                        logMissing: false);
                }

                return false;
            }

            return globalRegistrationMembership.Contains(comp);
        }

        internal bool IsGloballyRegisteredForDiagnostics(ISaveable component)
        {
            return component != null &&
                   globalRegistrationMembership.Contains(component);
        }

        #endregion

        #region Single-Object Methods

        /// <summary>
        /// Gathers serialized data for every ISaveable component on a single GameObject (including children).
        /// Returns a Dictionary keyed by the component's UniqueIdentifier, with each value being serialized bytes.
        /// </summary>
               public Dictionary<string, byte[]> CollectComponentDataForObject(GameObject obj)
               {
                       var result = new Dictionary<string, byte[]>();
                       var written = new HashSet<string>(StringComparer.Ordinal);
                       if (obj == null)
                       {
                               Logger.Log("CollectComponentDataForObject: Received null GameObject.", LogCategory.ComponentManager, LogLevel.Info);
                               return result;
                       }
                       foreach (var comp in obj.GetComponentsInChildren<ISaveable>(includeInactive: true))
                       {
                               // Skip entries whose underlying Unity object has been destroyed
                               if (comp is MonoBehaviour mb && mb == null)
                                       continue;
                               if (!IsValidSaveable(comp, nameof(CollectComponentDataForObject)))
                                       continue;

                               try
                               {
                                       var id = comp.UniqueIdentifier;
                                       byte[] serializedData =
                                               comp is SaveableComponent saveableComponent
                                                       ? SaveableComponent.InvokeSaveDataWithRoute(
                                                               saveableComponent,
                                                               SaveableComponent.SaveDataCallRoute
                                                                       .GlobalComponentCollection)
                                                       : comp.SaveData();
                                       if (serializedData != null && serializedData.Length > 0)
                                       {
                                               if (!written.Add(id) &&
                                                   (comp is not SaveableComponent ||
                                                    !SaveableComponent
                                                            .IsSaveCallDiagnosticsActive))
                                                       LogDuplicateSaveKey(nameof(CollectComponentDataForObject), id, comp);

                                               result[id] = serializedData;
                                       }
                               }
                               catch (Exception ex)
                               {
                                       ComponentOperationScope.Record(ex);
                                       Logger.Log($"CollectComponentDataForObject: Error serializing '{comp.UniqueIdentifier}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ComponentManager, LogLevel.Error);
                               }
                       }

                       return result;
               }

        /// <summary>
#if CRYSTALSAVE_TIMEMACHINE
        /// <summary>
        /// Optimized collection method for TimeMachine recording that only captures specific component types.
        /// This prevents performance issues by avoiding expensive components like ParticleSystem, Rigidbody, etc.
        /// when we only need to record Transform data for timeline playback.
        /// </summary>
        /// <param name="obj">GameObject to collect data from</param>
        /// <param name="allowedTypes">Set of component types to record. If null, records all ISaveable components (legacy behavior)</param>
        /// <returns>Dictionary of component data keyed by UniqueIdentifier</returns>
               public Dictionary<string, byte[]> CollectComponentDataForTimeMachine(GameObject obj, HashSet<System.Type> allowedTypes = null)
               {
                       var result = new Dictionary<string, byte[]>();
                       var written = new HashSet<string>(StringComparer.Ordinal);
                       if (obj == null)
                       {
                               //Logger.Log("[TIMEMACHINE] CollectComponentDataForTimeMachine: Received null GameObject.", LogLevel.Info);
                               return result;
                       }

                       // If no filtering specified, fall back to collecting all components
                       if (allowedTypes == null)
                       {
                               return CollectComponentDataForObject(obj);
                       }

                       foreach (var comp in obj.GetComponentsInChildren<ISaveable>(includeInactive: true))
                       {
                               // Skip entries whose underlying Unity object has been destroyed
                               if (comp is MonoBehaviour mb && mb == null)
                                       continue;
                               if (!IsValidSaveable(comp, nameof(CollectComponentDataForTimeMachine)))
                                       continue;

                               // Filter by component type - this is the key performance optimization
                               System.Type componentType = comp.GetType();
                               if (!allowedTypes.Contains(componentType))
                               {
                                       //Logger.Log($"[TIMEMACHINE] Skipping component type {componentType.Name} - not in allowed types.", LogLevel.Debug);
                                       continue;
                               }

                               try
                               {
                                       var id = comp.UniqueIdentifier;
                                       byte[] serializedData = comp.SaveData();
                                       if (serializedData != null && serializedData.Length > 0)
                                       {
                                               if (!written.Add(id) &&
                                                   (comp is not SaveableComponent ||
                                                    !SaveableComponent
                                                            .IsSaveCallDiagnosticsActive))
                                                       LogDuplicateSaveKey(nameof(CollectComponentDataForTimeMachine), id, comp);

                                               result[id] = serializedData;
                                               //Logger.Log($"[TIMEMACHINE] Recorded {componentType.Name} data ({serializedData.Length} bytes)", LogLevel.Debug);
                                       }
                               }
                               catch (Exception ex)
                               {
                                       Logger.Log($"[TIMEMACHINE] Error serializing '{comp.UniqueIdentifier}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ComponentManager, LogLevel.Error);
                               }
                       }

                       return result;
               }
#endif

        /// <summary>
        /// Applies data for every ISaveable component on a single GameObject (including children),
        /// matching by UniqueIdentifier. If a matching component is found, LoadData() is invoked.
        /// </summary>
               public void ApplyComponentDataToObject(
                       GameObject obj,
                       Dictionary<string, byte[]> componentData,
                       bool forceDuplicateLoads = false)
               {
                       if (obj == null || componentData == null || componentData.Count == 0) return;

                       bool forceAll = forceDuplicateLoads;
                       if (!forceAll && obj != null)
                       {
                               var prefabContext = obj.transform.GetComponentInParent<SaveablePrefab>(true);
                               if (prefabContext != null && prefabContext.ApplySavedComponentDataOnRespawn)
                               {
                                       forceAll = true;
                               }
                       }

                       foreach (var comp in obj.GetComponentsInChildren<ISaveable>(includeInactive: true))
                       {
                               if (!IsValidSaveable(comp, nameof(ApplyComponentDataToObject))) continue;
                               if (!componentData.TryGetValue(comp.UniqueIdentifier, out var data)) continue;

                               bool allowMultipleLoads = forceAll;

                               if (!allowMultipleLoads && comp is SaveableComponent saveableComponent && saveableComponent.ApplySavedDataOnRestore)
                               {
                                       allowMultipleLoads = true;
                               }

                               if (!allowMultipleLoads &&
                                   HasComponentDeserialized(comp, comp.UniqueIdentifier))
                               {
                                       Logger.Log($"ApplyComponentDataToObject: Skipping duplicate LoadData for '{comp.UniqueIdentifier}'.", LogCategory.ComponentManager, LogLevel.Warning);
                                       continue;
                               }

                               try
                               {
                                       RestoreComponent(comp, data, comp.UniqueIdentifier);
                               }
                               catch (Exception ex)
                               {
                                       Logger.Log($"ApplyComponentDataToObject: Error applying data to '{comp.UniqueIdentifier}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.ComponentManager, LogLevel.Error);
                               }
                       }

                       ProcessQueuedParenting();
               }

        #endregion

        #region Nested Classes

        private class ParentingRequest
        {
            public Transform Child { get; set; }
            public string ParentUniqueID { get; set; }
        }

        #endregion
    }
}
#endif
