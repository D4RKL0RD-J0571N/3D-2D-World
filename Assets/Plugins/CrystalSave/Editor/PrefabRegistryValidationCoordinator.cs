#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	/// <summary>
	/// Coalesces automatic PrefabRegistry maintenance so editor callbacks never perform
	/// asset mutation from an IMGUI Layout/Repaint event.
	/// </summary>
	[InitializeOnLoad]
	internal static class PrefabRegistryValidationCoordinator
	{
		internal enum ValidationReason
		{
			Activation,
			SerializedEdit,
			UndoRedo,
			ProjectChanged,
			HierarchyChanged,
			PrefabIdentityChanged,
			ExplicitRequest
		}

		private static readonly ProfilerMarker ValidationFlushMarker =
			new ProfilerMarker("CrystalSave.PrefabRegistry.ValidationFlush");

		private static readonly Dictionary<int, PendingValidation> PendingByRegistryId = new();
		private static bool flushScheduled;
		private static bool isFlushing;
		private static bool shuttingDown;
		private static UndoRegistryState restoredUndoState;

		// Passive project/hierarchy notifications can arrive after undoRedoPerformed.
		// Remember the restored values, rather than suppressing events for an arbitrary
		// time window. An actual registry/prefab edit makes the snapshot stop matching.
		private sealed class UndoRegistryState
		{
			internal readonly PrefabRegistry Registry;
			private readonly PrefabRegistryIdentityIndex snapshot;

			internal UndoRegistryState(PrefabRegistry registry)
			{
				Registry = registry;
				snapshot = PrefabRegistryIdentityIndex.Build(registry);
			}

			internal bool MatchesCurrentValues()
			{
				if (Registry == null || Registry.prefabEntries == null ||
				    Registry.prefabEntries.Count != snapshot.Rows.Count)
					return false;
				for (int i = 0; i < snapshot.Rows.Count; i++)
				{
					PrefabRegistryIdentityRow row = snapshot.Rows[i];
					PrefabRegistry.PrefabEntry entry = Registry.prefabEntries[i];
					SaveablePrefab currentComponent = row.ResolvedAsset != null
						? row.ResolvedAsset.GetComponent<SaveablePrefab>() : null;
					if ((entry == null) != (row.Entry == null) ||
					    !string.Equals(entry?.uniqueID, row.RegistryId, StringComparison.Ordinal) ||
					    (entry?.prefab) != row.PrefabReference ||
					    (!ReferenceEquals(row.PrefabReference, null) && row.PrefabReference == null) ||
					    currentComponent != row.SaveablePrefab ||
					    (currentComponent == null && !ReferenceEquals(row.SaveablePrefab, null)) ||
					    (currentComponent != null &&
					     !string.Equals(currentComponent.PrefabAssetID, row.PrefabAssetId, StringComparison.Ordinal)))
						return false;
				}
				return true;
			}
		}

		internal static int PendingRegistryCount => PendingByRegistryId.Count;
		internal static int ValidationPassCount { get; private set; }
		internal static int SaveRequestCount { get; private set; }

		private sealed class PendingValidation
		{
			internal PrefabRegistry Registry;
			internal ValidationReason Reason;
			internal bool SkipActiveRegistryCheck;
		}

		static PrefabRegistryValidationCoordinator()
		{
			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;
			EditorApplication.projectChanged -= OnProjectChanged;
			EditorApplication.projectChanged += OnProjectChanged;
			EditorApplication.hierarchyChanged -= OnHierarchyChanged;
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			EditorApplication.quitting -= OnEditorQuitting;
			EditorApplication.quitting += OnEditorQuitting;
			AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
			AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

			QueueInitialValidation();
		}

		internal static void RequestValidation(
			PrefabRegistry registry,
			ValidationReason reason = ValidationReason.ExplicitRequest)
		{
			if (registry == null || shuttingDown)
				return;

			PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(registry);

			if (isFlushing || EditorApplication.isPlayingOrWillChangePlaymode)
				return;
			if (!CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation)
				return;

			PrefabRegistry activeRegistry = PrefabAssetResolver.LoadActivePrefabRegistry();
			if (activeRegistry == null || activeRegistry != registry)
				return;

			if (restoredUndoState != null && restoredUndoState.Registry == registry)
			{
				bool passiveNotification = reason == ValidationReason.Activation ||
					reason == ValidationReason.UndoRedo || reason == ValidationReason.ProjectChanged ||
					reason == ValidationReason.HierarchyChanged;
				if (passiveNotification && restoredUndoState.MatchesCurrentValues())
					return;
				restoredUndoState = null;
			}

			int registryId = GetObjectId(registry);
			PendingByRegistryId[registryId] = new PendingValidation
			{
				Registry = registry,
				Reason = reason
			};

			ScheduleFlush();
		}

		internal static void RequestValidationForTests(
			PrefabRegistry registry,
			ValidationReason reason = ValidationReason.ExplicitRequest)
		{
			if (registry == null || shuttingDown || isFlushing ||
			    EditorApplication.isPlayingOrWillChangePlaymode ||
			    !CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation)
			{
				return;
			}

			PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(registry);
			int registryId = GetObjectId(registry);
			PendingByRegistryId[registryId] = new PendingValidation
			{
				Registry = registry,
				Reason = reason,
				SkipActiveRegistryCheck = true
			};
			ScheduleFlush();
		}

		internal static void RequestActiveRegistryValidation(
			ValidationReason reason = ValidationReason.ExplicitRequest)
		{
			RequestValidation(PrefabAssetResolver.LoadActivePrefabRegistry(), reason);
		}

		internal static void NotifyRegistryEdited(PrefabRegistry registry)
		{
			if (registry == null)
				return;

			PrefabRegistryIdentityIndex.NotifyRegistryMutated(registry);
			PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(registry);
			RequestValidation(registry, ValidationReason.SerializedEdit);
		}

		internal static void NotifyPrefabIdentityChanged(GameObject prefabAsset)
		{
			PrefabRegistryIdentityIndex.NotifyPrefabIdentityChanged(prefabAsset);
			PrefabRegistryIdentityUtility.InvalidateAllInspectionSnapshots();
			RequestActiveRegistryValidation(ValidationReason.PrefabIdentityChanged);
		}

		internal static void CancelPending()
		{
			restoredUndoState = null;
			EditorApplication.delayCall -= RequestInitialValidation;
			EditorApplication.delayCall -= FlushPending;
			flushScheduled = false;
			PendingByRegistryId.Clear();
		}

		internal static bool HasPendingForOtherRegistry(PrefabRegistry registry)
		{
			int registryId = GetObjectId(registry);
			foreach (int pendingId in PendingByRegistryId.Keys)
			{
				if (pendingId != registryId)
					return true;
			}
			return false;
		}

		internal static void CancelPending(PrefabRegistry registry)
		{
			if (registry == null)
				return;
			if (restoredUndoState != null && restoredUndoState.Registry == registry)
				restoredUndoState = null;

			PendingByRegistryId.Remove(GetObjectId(registry));
			if (PendingByRegistryId.Count == 0)
			{
				EditorApplication.delayCall -= FlushPending;
				flushScheduled = false;
			}
			else
			{
				ScheduleFlush();
			}
		}

		internal static void FlushPendingForTests()
		{
			EditorApplication.delayCall -= FlushPending;
			flushScheduled = false;
			FlushPending();
		}

		internal static void FlushPendingForTests(PrefabRegistry registry)
		{
			if (registry == null ||
			    !PendingByRegistryId.TryGetValue(GetObjectId(registry), out PendingValidation request))
			{
				return;
			}

			PendingByRegistryId.Remove(GetObjectId(registry));
			if (PendingByRegistryId.Count == 0)
			{
				EditorApplication.delayCall -= FlushPending;
				flushScheduled = false;
			}

			using (ValidationFlushMarker.Auto())
			{
				isFlushing = true;
				try
				{
					PrefabRegistryEditorDiagnosticCounters.RecordValidationFlush(request.Registry);
					FlushRegistry(request);
				}
				finally
				{
					isFlushing = false;
				}
			}

			if (PendingByRegistryId.Count > 0)
				ScheduleFlush();
		}

		internal static void ResetTestMetrics()
		{
			CancelPending();
			PrefabRegistryIdentityIndex.ConsumeAutomaticSaveSuppression();
			ValidationPassCount = 0;
			SaveRequestCount = 0;
		}

		private static void QueueInitialValidation()
		{
			EditorApplication.delayCall -= RequestInitialValidation;
			EditorApplication.delayCall += RequestInitialValidation;
		}

		private static void RequestInitialValidation()
		{
			EditorApplication.delayCall -= RequestInitialValidation;
			RequestActiveRegistryValidation(ValidationReason.Activation);
		}

		private static void ScheduleFlush()
		{
			if (flushScheduled || PendingByRegistryId.Count == 0)
				return;

			flushScheduled = true;
			EditorApplication.delayCall -= FlushPending;
			EditorApplication.delayCall += FlushPending;
		}

		private static void FlushPending()
		{
			EditorApplication.delayCall -= FlushPending;
			flushScheduled = false;

			if (shuttingDown || EditorApplication.isPlayingOrWillChangePlaymode || isFlushing)
			{
				PendingByRegistryId.Clear();
				return;
			}

			List<PendingValidation> pending = new List<PendingValidation>(PendingByRegistryId.Values);
			PendingByRegistryId.Clear();

			using (ValidationFlushMarker.Auto())
			{
				isFlushing = true;
				try
				{
					foreach (PendingValidation request in pending)
					{
						PrefabRegistryEditorDiagnosticCounters.RecordValidationFlush(request?.Registry);
						FlushRegistry(request);
					}
				}
				finally
				{
					isFlushing = false;
				}
			}
		}

		private static void FlushRegistry(PendingValidation request)
		{
			PrefabRegistry registry = request?.Registry;
			if (registry == null || !CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation)
				return;

			if (!request.SkipActiveRegistryCheck)
			{
				PrefabRegistry activeRegistry = PrefabAssetResolver.LoadActivePrefabRegistry();
				if (activeRegistry == null || activeRegistry != registry)
					return;
			}

			ValidationPassCount++;
			PrefabRegistryEditorDiagnosticCounters.RecordValidationPass(registry);

			Undo.IncrementCurrentGroup();
			int undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Validate Prefab Registry");

			bool changed = false;
			try
			{
				PrefabRegistryNormalizationUtility.InvalidEntryCleanupResult cleanup =
					PrefabRegistryNormalizationUtility.CleanInvalidEntries(
						registry,
						saveAssets: false,
						rebuildLookup: false);
				changed |= cleanup.Changed;

				if (cleanup.Changed)
				{
					PrefabRegistryIdentityIndex.NotifyRegistryMutated(registry);
					PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(registry);
				}

				PrefabRegistryIdentityIndex index = cleanup.Changed
					? PrefabRegistryIdentityIndex.Build(registry)
					: PrefabRegistryIdentityIndex.GetOrCreate(registry);

				bool normalized = PrefabRegistryNormalizationUtility.NormalizeEntries(
					registry,
					index,
					out _,
					rebuildLookup: false);
				changed |= normalized;

				if (!changed)
					return;

				registry.RebuildLookup();
				EditorUtility.SetDirty(registry);
				PrefabRegistryIdentityIndex.NotifyRegistryMutated(registry);
				PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(registry);

				PrefabRegistryIdentityIndex.NotifyAutomaticSaveRequested(registry);
				SaveRequestCount++;
				AssetDatabase.SaveAssets();
			}
			finally
			{
				Undo.SetCurrentGroupName("Validate Prefab Registry");
				Undo.CollapseUndoOperations(undoGroup);
			}
		}

		private static void OnUndoRedo()
		{
			PrefabRegistryIdentityIndex.InvalidateAll();
			PrefabRegistryIdentityUtility.InvalidateAllInspectionSnapshots();
			// Undo owns the restored values, even when they contain the mismatch or
			// invalid row that an explicit repair removed. Discard pre-Undo work;
			// later authored changes can request normal validation again.
			PrefabRegistry registry = PrefabAssetResolver.LoadActivePrefabRegistry();
			CancelPending(registry);
			restoredUndoState = registry != null ? new UndoRegistryState(registry) : null;
		}

		private static void OnProjectChanged()
		{
			if (PrefabRegistryIdentityIndex.ConsumeAutomaticSaveSuppression())
			{
				return;
			}

			PrefabRegistryIdentityIndex.InvalidateAll();
			PrefabRegistryIdentityUtility.InvalidateAllInspectionSnapshots();
			RequestActiveRegistryValidation(ValidationReason.ProjectChanged);
		}

		private static void OnHierarchyChanged()
		{
			RequestActiveRegistryValidation(ValidationReason.HierarchyChanged);
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingEditMode ||
			    state == PlayModeStateChange.EnteredPlayMode ||
			    state == PlayModeStateChange.ExitingPlayMode)
			{
				CancelPending();
				PrefabRegistryIdentityIndex.InvalidateAll();
				PrefabRegistryIdentityUtility.InvalidateAllInspectionSnapshots();
				return;
			}

			if (state == PlayModeStateChange.EnteredEditMode)
			{
				PrefabRegistryIdentityIndex.InvalidateAll();
				PrefabRegistryIdentityUtility.InvalidateAllInspectionSnapshots();
				RequestActiveRegistryValidation(ValidationReason.Activation);
			}
		}

		private static void OnBeforeAssemblyReload()
		{
			shuttingDown = true;
			CancelPending();
			EditorApplication.delayCall -= RequestInitialValidation;
			PrefabRegistryIdentityIndex.InvalidateAll();
			PrefabRegistryIdentityUtility.InvalidateAllInspectionSnapshots();
		}

		private static void OnEditorQuitting()
		{
			shuttingDown = true;
			CancelPending();
			EditorApplication.delayCall -= RequestInitialValidation;
		}

		private static int GetObjectId(UnityEngine.Object obj)
		{
#if UNITY_6000_5_OR_NEWER
			return obj != null ? obj.GetEntityId().GetHashCode() : 0;
#else
			return obj != null ? obj.GetInstanceID() : 0;
#endif
		}
	}
}
#endif
#endif
