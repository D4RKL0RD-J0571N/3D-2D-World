#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using Logger = Arawn.CrystalSave.Runtime.Logger;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(SaveablePrefab))]
	[CanEditMultipleObjects]
	public class SaveablePrefabEditor : UnityEditor.Editor
	{
		/* ───── State flags ──────────────────────────────────────── */
		private bool componentsRemovedThisFrame = false;

		/* ───── Serialized fields ────────────────────────────────── */
		private SerializedProperty uniqueIDProperty;
		private SerializedProperty prefabAssetIDProperty;
                private SerializedProperty keepAcrossScenesProperty;
                private SerializedProperty rememberHomeSceneProperty;
				// Serialized field is named 'homeSceneCaptureMode' in SaveablePrefab; editor alias keeps previous variable name for backwards compatibility
				private SerializedProperty homeSceneModeProperty; // actually points to 'homeSceneCaptureMode'
                private SerializedProperty registerWithSaveSystemProperty;
                                private SerializedProperty autoAssignInstanceIDWhenUnregisteredProperty;
	                private SerializedProperty applySavedComponentDataOnRespawnProperty;
	                private SerializedProperty reuseSceneInstanceOnLoadProperty;
	                private SerializedProperty enableCrossSceneStateHandoffProperty;
	                private SerializedProperty handoffToSceneBakedPrefabsProperty;
	                private SerializedProperty handoffTransformProperty;
	                private SerializedProperty handoffRuntimeModificationsProperty;
	                private SerializedProperty offScreenMaskProperty;
                private SerializedProperty visibleInScenesProperty;
                private SerializedProperty trackSkinnedMeshOverridesProperty;
                private SerializedProperty trackBlendshapeOverridesProperty;
                private SerializedProperty trackTextureOverridesProperty;
        private SerializedProperty trackMaterialOverridesProperty;      
        private SerializedProperty trackAddedComponentsProperty;
                private SerializedProperty trackParticleSnapshotsProperty;
                private SerializedProperty trackChildStateOverridesProperty;
                private SerializedProperty trackChildTransformOverridesProperty;
                private SerializedProperty trackComponentBlobsProperty;
                private SerializedProperty trackColliderSettingsProperty;
                private SerializedProperty skipSavingWhenUnchangedProperty;
                private SerializedProperty loadPriorityProperty;
                private SerializedProperty deferLowPriorityUntilRequestedProperty;
                private SerializedProperty disablePoolingProperty;
                private SerializedProperty originalPrefabAssetProperty;
                private SerializedProperty propertySettingsProperty;
                private SerializedProperty rememberActiveProperty;
                private SerializedProperty rememberNameProperty;
                private SerializedProperty rememberLayerProperty;
                private SerializedProperty rememberTagProperty;
				private SerializedProperty rememberDestroyedProperty;

				// Performance toggles
				private SerializedProperty enablePerformanceCachingProperty;
				private SerializedProperty enableBatchRegistrationProperty;

#if CRYSTALSAVE_TIMEMACHINE
				// TimeMachine properties
				private SerializedProperty enableTimeMachineRecordingProperty;
				private SerializedProperty autoStartRecordingProperty;
				private SerializedProperty overrideRecordingSettingsProperty;
				private SerializedProperty useIntervalProperty;
				private SerializedProperty snapshotIntervalProperty;
				private SerializedProperty maxSnapshotsProperty;
				private SerializedProperty saveTimeMachineSnapshotsProperty;
				private SerializedProperty overrideMaxSaveDurationProperty;
				private SerializedProperty maxSaveDurationProperty;
#endif

		/* ───── bookkeeping ──────────────────────────────────────── */
		private PrefabRegistry prefabRegistry;
		private PrefabRegistryIdentityIndex prefabIdentityIndex;
		private readonly List<GameObject> cachedGameObjects = new();
		private readonly Dictionary<SaveablePrefab, GameObject> cachedPrefabAssets = new();
		#pragma warning disable UDR0001 // Editor-only caches are cleared on hierarchy, play-mode, and assembly lifecycle events.
		private static readonly Dictionary<string, int> sceneUniqueIdCounts =
			new Dictionary<string, int>(StringComparer.Ordinal);
		private static bool sceneIdentitySnapshotReady;
		private static bool sceneIdentityScanQueued;
		#pragma warning restore UDR0001
	                private bool showVisibilitySettingsFoldout = true;
		#pragma warning disable UDR0001 // Editor-only foldout state does not retain scene objects or callbacks.
		                private static bool showAdvancedSettingsFoldout;
		#pragma warning restore UDR0001
		                private bool showPrefabAssetIDRepairTools;
		                private string repairManualPrefabAssetID = string.Empty;

		/*────────────────────────── OnEnable ───────────────────────*/
		private void OnEnable()
		{
			// Early validation - prevent errors when targets are destroyed
			if (serializedObject == null || target == null) return;
			
			// Additional check for destroyed targets (e.g., after exiting play mode)
			try
			{
				if (serializedObject.targetObject == null) return;
			}
			catch
			{
				// SerializedObject is invalid, exit gracefully
				return;
			}

			uniqueIDProperty = serializedObject.FindProperty("uniqueID");
			prefabAssetIDProperty = serializedObject.FindProperty("prefabAssetID");

			// Build one delayed scene identity snapshot shared by validation and inspector warnings.
			// OnInspectorGUI must stay read-only during Layout/Repaint.
			QueueSceneIdentityScan();
                        keepAcrossScenesProperty = serializedObject.FindProperty("keepAcrossScenes");
                        rememberHomeSceneProperty = serializedObject.FindProperty("rememberHomeScene");
						// Field in component is 'homeSceneCaptureMode' (renamed from earlier 'homeSceneMode')
						homeSceneModeProperty = serializedObject.FindProperty("homeSceneCaptureMode");
                        registerWithSaveSystemProperty = serializedObject.FindProperty("RegisterWithSaveSystem");
                                                autoAssignInstanceIDWhenUnregisteredProperty = serializedObject.FindProperty("autoAssignInstanceIDWhenUnregistered");
	                        applySavedComponentDataOnRespawnProperty = serializedObject.FindProperty("applySavedComponentDataOnRespawn");
	                        reuseSceneInstanceOnLoadProperty = serializedObject.FindProperty("reuseSceneInstanceOnLoad");
	                        enableCrossSceneStateHandoffProperty = serializedObject.FindProperty("enableCrossSceneStateHandoff");
	                        handoffToSceneBakedPrefabsProperty = serializedObject.FindProperty("handoffToSceneBakedPrefabs");
	                        handoffTransformProperty = serializedObject.FindProperty("handoffTransform");
	                        handoffRuntimeModificationsProperty = serializedObject.FindProperty("handoffRuntimeModifications");
	                        offScreenMaskProperty = serializedObject.FindProperty("offScreenMask");
			visibleInScenesProperty = serializedObject.FindProperty("VisibleInScenes");

                        enablePerformanceCachingProperty = serializedObject.FindProperty("enablePerformanceCaching");
                        enableBatchRegistrationProperty   = serializedObject.FindProperty("enableBatchRegistration");
                        skipSavingWhenUnchangedProperty   = serializedObject.FindProperty("skipSavingWhenUnchanged");
                        loadPriorityProperty              = serializedObject.FindProperty("loadPriority");
                        deferLowPriorityUntilRequestedProperty = serializedObject.FindProperty("deferLowPriorityUntilRequested");
                        disablePoolingProperty            = serializedObject.FindProperty("disablePooling");

                        trackSkinnedMeshOverridesProperty = serializedObject.FindProperty("trackSkinnedMeshOverrides");
                        trackBlendshapeOverridesProperty  = serializedObject.FindProperty("trackBlendshapeOverrides");
                        trackTextureOverridesProperty     = serializedObject.FindProperty("trackTextureOverrides");
            trackMaterialOverridesProperty    = serializedObject.FindProperty("trackMaterialOverrides");
            trackAddedComponentsProperty      = serializedObject.FindProperty("trackAddedComponents");      
            trackParticleSnapshotsProperty    = serializedObject.FindProperty("trackParticleSnapshots");
            trackChildStateOverridesProperty  = serializedObject.FindProperty("trackChildStateOverrides");
            trackChildTransformOverridesProperty = serializedObject.FindProperty("trackChildTransformOverrides");
            trackComponentBlobsProperty       = serializedObject.FindProperty("trackComponentBlobs");
            trackColliderSettingsProperty     = serializedObject.FindProperty("trackColliderSettings");
                        originalPrefabAssetProperty       = serializedObject.FindProperty("originalPrefabAsset");

                        propertySettingsProperty  = serializedObject.FindProperty("propertySettings");
                        if (propertySettingsProperty != null)
                        {
                                rememberActiveProperty   = propertySettingsProperty.FindPropertyRelative("RememberActive");
                                rememberNameProperty     = propertySettingsProperty.FindPropertyRelative("RememberName");
                                rememberLayerProperty    = propertySettingsProperty.FindPropertyRelative("RememberLayer");
                                rememberTagProperty      = propertySettingsProperty.FindPropertyRelative("RememberTag");
                                rememberDestroyedProperty= propertySettingsProperty.FindPropertyRelative("RememberDestroyed");
                        }

#if CRYSTALSAVE_TIMEMACHINE
			// Find TimeMachine properties
			enableTimeMachineRecordingProperty = serializedObject.FindProperty("enableTimeMachineRecording");
			autoStartRecordingProperty = serializedObject.FindProperty("autoStartRecording");
			overrideRecordingSettingsProperty = serializedObject.FindProperty("overrideRecordingSettings");
			useIntervalProperty = serializedObject.FindProperty("useInterval");
			snapshotIntervalProperty = serializedObject.FindProperty("snapshotInterval");
			maxSnapshotsProperty = serializedObject.FindProperty("maxSnapshots");
			saveTimeMachineSnapshotsProperty = serializedObject.FindProperty("saveTimeMachineSnapshots");
			overrideMaxSaveDurationProperty = serializedObject.FindProperty("overrideMaxSaveDuration");
			maxSaveDurationProperty = serializedObject.FindProperty("maxSaveDuration");
#endif

			cachedGameObjects.Clear();
			cachedPrefabAssets.Clear();
			foreach (var obj in targets)
			{
				// Skip null or destroyed objects
				if (obj == null) continue;
				if (obj is SaveablePrefab sp && sp && sp.gameObject)
				{
					cachedGameObjects.Add(sp.gameObject);
					cachedPrefabAssets[sp] = GetPrefabAsset(sp.gameObject);
				}
			}

				LoadOrCreatePrefabRegistry();
				prefabIdentityIndex = prefabRegistry != null
					? PrefabRegistryIdentityIndex.GetOrCreate(prefabRegistry)
					: null;
				showPrefabAssetIDRepairTools = false;
				InitializePrefabAssetIDRepairInput();
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
#pragma warning disable UDR0004 // Removed in OnDisable/OnDestroy before editor instances are discarded.
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
#pragma warning restore UDR0004

			if (!Application.isPlaying &&
				targets.OfType<SaveablePrefab>().Where(sp => sp != null).Any(sp => string.IsNullOrEmpty(sp.PrefabAssetID)))
			{
				if (pendingAssignPrefabAssetIDAndRegister != null)
					EditorApplication.delayCall -= pendingAssignPrefabAssetIDAndRegister;
				pendingAssignPrefabAssetIDAndRegister = AssignPrefabAssetIDAndRegister;
#pragma warning disable UDR0004 // Canceled by CancelPendingDelayCalls from OnDisable/OnDestroy.
				EditorApplication.delayCall += pendingAssignPrefabAssetIDAndRegister;
#pragma warning restore UDR0004
			}
			else
			{
				if (CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs)
					EnsureRegistrationForPrefabs();
				SyncPoolingSettingsFromRegistry();
			}

			if (Application.isPlaying)
			{
				cachedGameObjects.Clear();
				cachedPrefabAssets.Clear();
			}
		}

		/*────────────────────────── OnDisable ─────────────────────*/
		private void OnDisable()
		{
			// Safely handle OnDisable even if targets are destroyed
			try
			{
				Undo.undoRedoPerformed -= OnUndoRedoPerformed;
				CancelPendingDelayCalls(includeRemovalChecks: false);
				if (Application.isPlaying)
				{
					cachedGameObjects.Clear();
					cachedPrefabAssets.Clear();
					return;
				}

				if (CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation)
				{
					// Check if any components are being removed and schedule deregistration
					var snapshot = cachedGameObjects.ToArray();
					if (pendingDeregisterIfMissing != null)
						EditorApplication.delayCall -= pendingDeregisterIfMissing;
					pendingDeregisterIfMissing = () => DeregisterIfComponentMissing(snapshot);
#pragma warning disable UDR0004 // Intentionally queued from OnDisable; canceled by OnDestroy if the editor is destroyed first.
					EditorApplication.delayCall += pendingDeregisterIfMissing;
#pragma warning restore UDR0004

					// Also check for immediate removal (when component is deleted via inspector)
					CheckForComponentRemoval();
				}

				cachedGameObjects.Clear();
				cachedPrefabAssets.Clear();
			}
			catch (System.Exception ex)
			{
				// Silently handle exceptions during cleanup (e.g., destroyed objects after exiting play mode)
				if (!ex.Message.Contains("destroyed") && !ex.Message.Contains("Disposed"))
				{
					UnityEngine.Debug.LogWarning($"[SaveablePrefabEditor] Exception in OnDisable: {ex.Message}\nStack Trace: {ex.StackTrace}");
				}
				// Clean up regardless
				cachedGameObjects.Clear();
			}
		}

		/// <summary>
		/// Checks if SaveablePrefab components are being removed and deregisters them immediately.
		/// </summary>
		private void CheckForComponentRemoval()
		{
			if (!CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation) return;
			if (Application.isPlaying || prefabRegistry == null) return;

			// Check each cached GameObject to see if its SaveablePrefab component still exists
			foreach (var gameObject in cachedGameObjects)
			{
				if (gameObject == null) continue;

				var saveablePrefab = gameObject.GetComponent<SaveablePrefab>();
				if (saveablePrefab == null)
				{
					// Component was removed, deregister immediately
					GameObject asset = GetPrefabAsset(gameObject);
					if (asset != null)
					{
						DeregisterPrefabAsset(asset);
					}
				}
			}
		}

		/// <summary>
		/// Deregisters a prefab asset from the registry.
		/// </summary>
		/// <param name="asset">The prefab asset to deregister.</param>
		private void DeregisterPrefabAsset(GameObject asset)
		{
			if (asset == null || prefabRegistry == null) return;

			var entryToRemove = prefabRegistry.prefabEntries.FirstOrDefault(e => e.prefab == asset);
			if (entryToRemove != null)
			{
				Undo.RecordObject(prefabRegistry, "Deregister SaveablePrefab");
				bool removed = prefabRegistry.prefabEntries.Remove(entryToRemove);

					if (removed)
					{
						prefabRegistry.RebuildLookup();
						EditorUtility.SetDirty(prefabRegistry);
						AssetDatabase.SaveAssets();
						Logger.Log($"SaveablePrefabEditor: Deregistered prefab '{asset.name}' after component removal.", LogLevel.Info);
				}
			}
		}

		// Track delegates we attach to static events so we can detach in OnDestroy
		private EditorApplication.CallbackFunction pendingAssignPrefabAssetIDAndRegister;
		private EditorApplication.CallbackFunction pendingDeregisterIfMissing;
		private EditorApplication.CallbackFunction pendingAddPVC;
		private EditorApplication.CallbackFunction pendingRemovePVC;
		private EditorApplication.CallbackFunction pendingRegistrySync;
		private EditorApplication.CallbackFunction pendingRetryDeregisterPrefabs;
		private EditorApplication.CallbackFunction pendingFinalDeregisterIfMissing;
		private EditorApplication.CallbackFunction pendingSerializedObjectRefresh;

		private void OnDestroy()
		{
			// When the editor is destroyed, it usually means the component is being removed
			// Schedule a final cleanup check
			if (!Application.isPlaying &&
				CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation &&
				cachedGameObjects.Count > 0)
			{
				var snapshot = cachedGameObjects.ToArray();
				if (pendingFinalDeregisterIfMissing != null)
					EditorApplication.delayCall -= pendingFinalDeregisterIfMissing;
				pendingFinalDeregisterIfMissing = () =>
				{
					if (prefabRegistry != null)
					{
						DeregisterIfComponentMissing(snapshot);
					}
				};
#pragma warning disable UDR0004 // Canceled by CancelPendingDelayCalls below if destruction continues before the queued check runs.
				EditorApplication.delayCall += pendingFinalDeregisterIfMissing;
#pragma warning restore UDR0004
			}

			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
			CancelPendingDelayCalls(includeRemovalChecks: true);
		}

		private void CancelPendingDelayCalls(bool includeRemovalChecks)
		{
			if (pendingAssignPrefabAssetIDAndRegister != null)
			{
				EditorApplication.delayCall -= pendingAssignPrefabAssetIDAndRegister;
				pendingAssignPrefabAssetIDAndRegister = null;
			}
			if (includeRemovalChecks && pendingDeregisterIfMissing != null)
			{
				EditorApplication.delayCall -= pendingDeregisterIfMissing;
				pendingDeregisterIfMissing = null;
			}
			if (pendingAddPVC != null)
			{
				EditorApplication.delayCall -= pendingAddPVC;
				pendingAddPVC = null;
			}
			if (pendingRemovePVC != null)
			{
				EditorApplication.delayCall -= pendingRemovePVC;
				pendingRemovePVC = null;
			}
			if (pendingRegistrySync != null)
			{
				EditorApplication.delayCall -= pendingRegistrySync;
				pendingRegistrySync = null;
			}
			if (pendingRetryDeregisterPrefabs != null)
			{
				EditorApplication.delayCall -= pendingRetryDeregisterPrefabs;
				pendingRetryDeregisterPrefabs = null;
			}
			if (pendingFinalDeregisterIfMissing != null)
			{
				EditorApplication.delayCall -= pendingFinalDeregisterIfMissing;
				pendingFinalDeregisterIfMissing = null;
			}
			if (pendingSerializedObjectRefresh != null)
			{
				EditorApplication.delayCall -= pendingSerializedObjectRefresh;
				pendingSerializedObjectRefresh = null;
			}
		}

		private void QueueSerializedObjectRefresh()
		{
			if (pendingSerializedObjectRefresh != null)
				EditorApplication.delayCall -= pendingSerializedObjectRefresh;

			pendingSerializedObjectRefresh = RefreshSerializedObjectAfterDelay;
#pragma warning disable UDR0004 // Canceled by CancelPendingDelayCalls from OnDisable/OnDestroy.
			EditorApplication.delayCall += pendingSerializedObjectRefresh;
#pragma warning restore UDR0004
		}

		private void RefreshSerializedObjectAfterDelay()
		{
			if (pendingSerializedObjectRefresh != null)
			{
				EditorApplication.delayCall -= pendingSerializedObjectRefresh;
				pendingSerializedObjectRefresh = null;
			}

			if (this != null && serializedObject != null && serializedObject.targetObject != null)
			{
				serializedObject.Update();
				Repaint();
			}
		}

		/*──────────────────── Inspector GUI ───────────────────────*/
		public override void OnInspectorGUI()
		{
			// Early exit if editor or targets are invalid (e.g., after exiting play mode)
			if (serializedObject == null || target == null || targets == null) return;
			
			// Check if any target has been destroyed (happens when exiting play mode)
			foreach (var t in targets)
			{
				if (t == null)
				{
					// Object was destroyed, skip drawing to prevent errors
					EditorGUILayout.HelpBox("Inspector target was destroyed. This can happen when exiting play mode.", MessageType.Info);
					return;
				}
			}
			
			// Reset the flag at the start of each GUI frame
			componentsRemovedThisFrame = false;
			
			// Safety check for disposed SerializedObject
			try 
			{
					if (serializedObject.targetObject == null) return;
					serializedObject.Update();
					prefabIdentityIndex = prefabRegistry != null
						? PrefabRegistryIdentityIndex.GetOrCreate(prefabRegistry)
						: null;
				}
				catch (System.Exception ex) when (ex.Message.Contains("SerializedObject") || ex.Message.Contains("Disposed"))
				{
				// SerializedObject is disposed, try to refresh it
				EditorGUILayout.HelpBox("Inspector is refreshing...", MessageType.Info);
				Repaint();
				return;
			}

            if (targets.Length == 1)
            {
                CrystalSavePersistenceSetup.Draw((Component)target);
                serializedObject.UpdateIfRequiredOrScript();
            }
			DrawIdentityHelpBox();
			DrawUniqueIDField();
			DrawPrefabAssetIDField();
			DrawPrefabAssetIDRepairSection();
			CheckForDuplicateInstanceUniqueIDs();
			CheckForUniqueIDComponent();
			
			// If components were removed this frame, exit early to prevent SerializedObject disposal errors
			if (componentsRemovedThisFrame)
			{
				EditorGUILayout.HelpBox("Components were removed. Inspector will refresh momentarily...", MessageType.Info);
				return;
			}
			
			// Wrap the remaining GUI drawing in a safety net
			try
			{
				DrawPropertySettingsSection();
#if CRYSTALSAVE_TIMEMACHINE
				DrawTimeMachineSection();
#endif
				DrawRuntimeOverrideToggles();

                        CrystalSaveEditorStyles.BeginSection();
                        EditorGUI.indentLevel++;
                        showAdvancedSettingsFoldout = EditorGUILayout.Foldout(
                                showAdvancedSettingsFoldout,
                                new GUIContent(
                                        "Advanced Settings",
                                        "Cross-scene persistence, load scheduling, pooling, and performance options."
                                ),
                                true);
                        if (showAdvancedSettingsFoldout)
                        {
                                CrystalSaveEditorStyles.BeginSection("Across Scene Settings");
                                EditorGUI.indentLevel++;
                                DrawKeepAcrossScenesToggle();
                                if (keepAcrossScenesProperty.boolValue)
                                        DrawKeepAcrossScenesVisibilitySettings();

	                                DrawRememberHomeSceneToggle();
	                                DrawCrossSceneStateHandoffSettings();
	                                EditorGUI.indentLevel--;
	                                CrystalSaveEditorStyles.EndSection();

                                DrawRegisterWithSaveSystemToggle();
                                DrawReuseSceneInstanceToggle();
                                DrawLoadSchedulingSection();
                                DrawPoolingSettings();
                                DrawPerformanceAndSaveOptimizations();
                        }
                        EditorGUI.indentLevel--;
                        CrystalSaveEditorStyles.EndSection();

				if (!showPrefabAssetIDRepairTools)
					DrawRegisterPrefabButton();

			GUILayout.Space(6);

			// ── Copy ID & Help row ─────────────────────────────────────
			EditorGUILayout.BeginHorizontal();

			// Small Copy-ID button (60×24)
			if (GUILayout.Button("Copy ID", GUILayout.Width(60), GUILayout.Height(24)))
			{
				var sp = target as SaveablePrefab;
					string prefabAssetID = sp != null ? GetPrefabAssetIDDisplayText(sp) : null;
					if (!string.IsNullOrEmpty(prefabAssetID))
					{
						EditorGUIUtility.systemCopyBuffer = prefabAssetID;
						EditorWindow.focusedWindow?.ShowNotification(
							new GUIContent($"Copied Prefab Asset ID: {prefabAssetID}")
						);
					}
				else
				{
					EditorWindow.focusedWindow?.ShowNotification(
						new GUIContent("No PrefabAssetID available")
					);
				}
			}

			// Existing Help button, same size
			var helpContent = new GUIContent(" Help",
				EditorGUIUtility.IconContent("_Help").image,
				"Open a quick reference describing what SaveablePrefabs save/restore");
			if (GUILayout.Button(helpContent, GUILayout.Height(24)))
			{
				SaveablePrefabHelpWindow.ShowWindow();
			}

			EditorGUILayout.EndHorizontal();

				// Safely apply modified properties
				try 
				{
					if (serializedObject.ApplyModifiedProperties())
						QueueSceneIdentityScan();
				}
				catch (System.Exception ex) when (ex.Message.Contains("SerializedObject") || ex.Message.Contains("Disposed"))
				{
					// SerializedObject is disposed, skip applying properties this frame
					Debug.LogWarning("[Crystal Save] SerializedObject was disposed during GUI drawing. Changes may not be saved this frame.");
				}
			}
			catch (System.Exception ex) when (ex.Message.Contains("SerializedObject") || ex.Message.Contains("Disposed"))
			{
				// SerializedObject disposed during GUI drawing - show fallback message
				EditorGUILayout.HelpBox("Inspector is refreshing after component changes...", MessageType.Info);
			}
		}

                private void DrawPerformanceAndSaveOptimizations()
                {
                        CrystalSaveEditorStyles.BeginSection("Performance & Save Optimizations");
                        EditorGUI.indentLevel++;
			if (enablePerformanceCachingProperty != null)
			{
                                EditorGUILayout.PropertyField(
                                        enablePerformanceCachingProperty,
                                        new GUIContent("Enable Performance Caching",
                                                "Cache this SaveablePrefab and its IDs while enabled to avoid repeated GetComponent calls. Caches are cleared on disable or destroy. Safe to leave on."));
			}
			if (enableBatchRegistrationProperty != null)
			{
				EditorGUILayout.PropertyField(
					enableBatchRegistrationProperty,
					new GUIContent("Enable Batch Registration",
						"Register this prefab's subtree in batches to avoid frame spikes when many Saveables exist."));
                        }
                        if (skipSavingWhenUnchangedProperty != null)
                        {
                                EditorGUILayout.PropertyField(
                                        skipSavingWhenUnchangedProperty,
                                        new GUIContent(
                                                "Skip Saving When Unchanged",
                                                "When enabled, scene-placed prefabs skip emitting save data when their core state matches the initial snapshot."
                                        ));
                        }
                        if (applySavedComponentDataOnRespawnProperty != null)
                        {
                                EditorGUILayout.PropertyField(
                                        applySavedComponentDataOnRespawnProperty,
                                        new GUIContent(
                                                "Apply Saved Component Data On Respawn",
                                                "When enabled, the Save Manager re-applies saved component data when this prefab is restored after destruction."
                                        ));
                        }
                        EditorGUI.indentLevel--;
                        CrystalSaveEditorStyles.EndSection();
                }

                private void DrawRuntimeOverrideToggles()
                {
                        CrystalSaveEditorStyles.BeginSection("Runtime Override Tracking");
                        EditorGUI.indentLevel++;

			// ➊ Mesh-swap toggle
			EditorGUILayout.PropertyField(
				trackSkinnedMeshOverridesProperty,
				new GUIContent("Track Mesh Swaps",
					"Record SkinnedMeshRenderer / MeshFilter sharedMesh replacements that occur at runtime.\n\n" +
					"⚠️ IMPORTANT: All child GameObjects in this prefab's hierarchy MUST have UNIQUE names. " +
					"Duplicate names will cause serialization/deserialization errors and data loss."));

			// ➋ ★ show baseline field only when the toggle is ON (or mixed selection)
                        bool showBaseline =
                                trackSkinnedMeshOverridesProperty.boolValue ||
                                trackSkinnedMeshOverridesProperty.hasMultipleDifferentValues ||
                                trackBlendshapeOverridesProperty.boolValue ||
                                trackBlendshapeOverridesProperty.hasMultipleDifferentValues ||
                                // trackTextureOverridesProperty removed - use RememberMaterial component instead
                                trackMaterialOverridesProperty.boolValue ||
                                trackMaterialOverridesProperty.hasMultipleDifferentValues;

                        // ➌ All other toggles (unchanged)
                        EditorGUILayout.PropertyField(trackBlendshapeOverridesProperty,
                                new GUIContent("Track Blendshape Weights",
                                        "Record blendshape weight changes on SkinnedMeshRenderers beneath this prefab.\n\n" +
                                        "⚠️ IMPORTANT: All child GameObjects in this prefab's hierarchy MUST have UNIQUE names. " +
                                        "Duplicate names will cause serialization/deserialization errors and data loss."));

                        // Track Texture Overrides HIDDEN - has limitations with material instances
                        // Users should use RememberMaterial component instead for reliable texture tracking
                        // EditorGUILayout.PropertyField(trackTextureOverridesProperty,
                        //         new GUIContent("Track Texture Overrides",
                        //                 "Record runtime texture property changes on materials beneath this prefab."));

                        EditorGUILayout.PropertyField(trackMaterialOverridesProperty,
                                new GUIContent("Track Material Swaps",
					"Record per-slot material changes on any Renderer beneath this prefab.\n\n" +
					"⚠️ IMPORTANT: All child GameObjects in this prefab's hierarchy MUST have UNIQUE names. " +
					"Duplicate names will cause serialization/deserialization errors and data loss."));

                        EditorGUILayout.PropertyField(trackChildStateOverridesProperty,
                                new GUIContent("Track Child-State Overrides",
                                        "Persist active / tag / layer changes **and** spawned or destroyed children.\n\n" +
                                        "⚠️ IMPORTANT: All child GameObjects in this prefab's hierarchy MUST have UNIQUE names. " +
                                        "Duplicate names will cause serialization/deserialization errors and data loss."));

                        EditorGUILayout.PropertyField(trackChildTransformOverridesProperty,
                                new GUIContent("Track Child Transforms",
                                        "Serialize local position, rotation and scale of child objects.\n\n" +
                                        "⚠️ IMPORTANT: All child GameObjects in this prefab's hierarchy MUST have UNIQUE names. " +
                                        "Duplicate names will cause serialization/deserialization errors and data loss."));

			if (showBaseline)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(
					originalPrefabAssetProperty,
					new GUIContent(
						"Original Prefab Asset",
						"[REQUIRED for Texture Overrides] Reference to the original prefab asset used as a baseline for comparison.\n\n" +
						"• Does NOT need to be in a Resources folder (it's a serialized reference)\n" +
						"• Enables texture/mesh/material change detection\n" +
						"• Enables fast path-diff for child add/remove/active/tag/layer changes\n" +
						"• Leave blank only for pure runtime-spawned clones with no prefab source\n\n" +
						"Auto-set when using SaveablePrefabFactory.Instantiate().\n\n" +
						"NOTE: Asset restoration uses Instance ID (primary, works in builds) with fallback to Resources/Addressables for name-based loading."
					)
				);
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.PropertyField(trackAddedComponentsProperty,
				new GUIContent("Track Added Components",
					"Serialize components that are ADDed to this instance while the game is running."));

                        EditorGUILayout.PropertyField(trackParticleSnapshotsProperty,
                                new GUIContent("Track Particle Snapshots",
                                        "Save ParticleSystem time & play-state so effects resume correctly after loading."));

                        EditorGUILayout.PropertyField(trackColliderSettingsProperty,
                                new GUIContent("Track Collider Settings",
                                        "Persist collider enabled/trigger state and shape data."));

                        EditorGUILayout.PropertyField(trackComponentBlobsProperty,
                                new GUIContent("Track Component Blobs",
                                        "Store arbitrary byte[] data emitted by ISaveable helpers attached to this prefab."));
                        EditorGUI.indentLevel--;
                        CrystalSaveEditorStyles.EndSection();
                }

                private void DrawLoadSchedulingSection()
                {
                        if (loadPriorityProperty == null && deferLowPriorityUntilRequestedProperty == null) return;

                        CrystalSaveEditorStyles.BeginSection("Load Scheduling");
                        EditorGUI.indentLevel++;

                        if (loadPriorityProperty != null)
                        {
                                EditorGUILayout.IntSlider(
                                        loadPriorityProperty,
                                        0,
                                        100,
                                        new GUIContent(
                                                "Load Priority",
                                                "Higher priorities (closer to 100) are restored during the initial load batch. Lower values can be deferred for streaming or progressive reveal."
                                        )
                                );
                        }

                        if (deferLowPriorityUntilRequestedProperty != null)
                        {
                                EditorGUILayout.PropertyField(
                                        deferLowPriorityUntilRequestedProperty,
                                        new GUIContent(
                                                "Defer Until Requested",
                                                "When enabled, this prefab skips the main restoration pass and stays queued until gameplay explicitly requests deferred prefabs (e.g., when the player approaches the area)."
                                        )
                                );
                        }

                        EditorGUI.indentLevel--;
                        CrystalSaveEditorStyles.EndSection();
                }

		/// <summary>
		/// Draws the Pooling Settings section in the inspector.
		/// </summary>
		private void DrawPoolingSettings()
		{
			if (disablePoolingProperty == null) return;

			CrystalSaveEditorStyles.BeginSection("Pooling Settings");
			EditorGUI.indentLevel++;

			// Track changes to automatically sync with PrefabRegistry
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(
				disablePoolingProperty,
				new GUIContent(
					"Disable Pooling",
					"If enabled, this prefab will never use pooling even if global pooling is enabled. Automatically syncs with PrefabRegistry."
				)
			);

			// Auto-sync to PrefabRegistry when checkbox changes
			if (EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
				
				// Update PrefabRegistry for all selected targets
				foreach (var obj in targets)
				{
					var sp = obj as SaveablePrefab;
					if (sp != null && !string.IsNullOrEmpty(sp.PrefabAssetID))
					{
						SyncComponentSettingToPrefabRegistry(sp);
					}
				}
			}

			EditorGUI.indentLevel--;
			CrystalSaveEditorStyles.EndSection();
		}

				private bool IsPrefabRegistered(GameObject asset)
				{
					PrefabRegistryIdentityIndex index = GetPrefabIdentityIndex();
					return index != null && asset != null && index.GetRowsForAsset(asset).Count > 0;
				}

				private void DrawRegisterPrefabButton()
				{
					if (Application.isPlaying || prefabRegistry == null)
						return;

					bool showRegisterButton = false;
					foreach (var obj in targets)
					{
						if (obj is not SaveablePrefab sp || !sp.RegisterWithSaveSystem)
							continue;

						GameObject asset = GetCachedPrefabAsset(sp);
						if (asset != null && !IsPrefabRegistered(asset))
						{
							showRegisterButton = true;
							break;
						}
					}

					if (!showRegisterButton)
						return;

					EditorGUILayout.Space();
					if (!GUILayout.Button(new GUIContent(
						    "Register Prefab",
						    "Register the resolved prefab asset in the active PrefabRegistry.")))
						return;

					foreach (var obj in targets)
					{
						if (obj is not SaveablePrefab sp || !sp.RegisterWithSaveSystem)
							continue;

						GameObject asset = GetCachedPrefabAsset(sp);
						if (asset == null || IsPrefabRegistered(asset))
							continue;

						RegisterPrefab(sp);
					}

					EditorGUIUtility.ExitGUI();
				}

			private void InitializePrefabAssetIDRepairInput()
			{
				repairManualPrefabAssetID = targets.Length == 1 && target is SaveablePrefab prefab
					? prefab.PrefabAssetID ?? string.Empty
					: string.Empty;
			}

			/// <summary>
			/// Checks if there's a mismatch between the prefab's PrefabAssetID and the registry entry's uniqueID.
			/// Returns the mismatched registry entry if found, null otherwise.
			/// </summary>
			private PrefabRegistry.PrefabEntry GetMismatchedRegistryEntry(SaveablePrefab prefab, GameObject asset)
		{
			if (prefabRegistry == null || asset == null || prefab == null) return null;
			
			string prefabAssetID = prefab.PrefabAssetID;
			if (string.IsNullOrEmpty(prefabAssetID)) return null;

			// Indexed lookup keeps this inspector warning read-only and constant-time.
			PrefabRegistryIdentityIndex index = GetPrefabIdentityIndex();
			PrefabRegistry.PrefabEntry entry = index?.GetRowsForAsset(asset)
				.FirstOrDefault(row => row?.Entry != null)
				?.Entry;
			if (entry == null) return null;

			// Check if IDs match
			if (entry.uniqueID != prefabAssetID)
				return entry;

				return null;
			}

			private void SetPrefabAssetID(SaveablePrefab prefab, string prefabAssetID, string undoName = "Restore PrefabAssetID From Registry")
			{
				if (prefab == null || string.IsNullOrEmpty(prefabAssetID)) return;

				Undo.RecordObject(prefab, undoName);
				var serialized = new SerializedObject(prefab);
				SerializedProperty idProperty = serialized.FindProperty("prefabAssetID");
				if (idProperty == null) return;

			idProperty.stringValue = prefabAssetID;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(prefab);

			GameObject asset = GetCachedPrefabAsset(prefab);
			if (asset != null)
					EditorUtility.SetDirty(asset);
			}

				private void DrawPrefabAssetIDRepairSection()
				{
					if (Application.isPlaying || prefabRegistry == null) return;

				List<(SaveablePrefab prefab, GameObject asset, PrefabRegistry.PrefabEntry entry)> mismatches =
					CollectPrefabAssetIDMismatches();

					if (!showPrefabAssetIDRepairTools)
					{
						DrawAdoptRegistryIDControls(mismatches);
						return;
					}

					EditorGUILayout.Space();
					CrystalSaveEditorStyles.BeginSection("Prefab Asset ID Repair");
					EditorGUI.indentLevel++;
					EditorGUILayout.HelpBox(
						"Use these tools only when you intentionally need to repair a PrefabAssetID. Normal inspector and validation behavior keeps the PrefabRegistry authoritative.",
						MessageType.Info);

					DrawAdoptRegistryIDControls(mismatches);
					DrawSingleTargetPrefabAssetIDRepairControls();
					DrawRegisterPrefabButton();

				EditorGUI.indentLevel--;
				CrystalSaveEditorStyles.EndSection();
			}

			private List<(SaveablePrefab prefab, GameObject asset, PrefabRegistry.PrefabEntry entry)> CollectPrefabAssetIDMismatches()
			{
				var mismatches = new List<(SaveablePrefab, GameObject, PrefabRegistry.PrefabEntry)>();
				foreach (var obj in targets)
				{
					if (obj is not SaveablePrefab sp) continue;

					GameObject asset = GetCachedPrefabAsset(sp);
					if (asset == null) continue;

					if (!TryGetSingleRegistryEntryForAsset(asset, out PrefabRegistry.PrefabEntry entry, out _))
						continue;
					if (string.IsNullOrEmpty(entry.uniqueID)) continue;
					if (string.Equals(entry.uniqueID, sp.PrefabAssetID, StringComparison.Ordinal)) continue;

					mismatches.Add((sp, asset, entry));
				}

				return mismatches;
			}

			private void DrawAdoptRegistryIDControls(
				List<(SaveablePrefab prefab, GameObject asset, PrefabRegistry.PrefabEntry entry)> mismatches)
			{
				if (mismatches == null || mismatches.Count == 0)
					return;

				EditorGUILayout.Space();
				var firstMismatch = mismatches[0];
				string warningMsg = mismatches.Count == 1
					? $"ID mismatch detected.\n" +
					  $"Prefab serialized ID: {firstMismatch.prefab.PrefabAssetID}\n" +
					  $"Registry ID: {firstMismatch.entry.uniqueID}\n\n" +
					  "Adopt Registry ID copies the registry value back onto the prefab."
					: $"ID mismatch detected on {mismatches.Count} prefab(s).\n\n" +
					  "Adopt Registry ID copies each registry value back onto its prefab.";

				EditorGUILayout.HelpBox(warningMsg, MessageType.Warning);

				if (GUILayout.Button("Adopt Registry ID"))
				{
					AdoptRegistryIDs(mismatches);
					EditorGUIUtility.ExitGUI();
				}
			}

			private void AdoptRegistryIDs(
				List<(SaveablePrefab prefab, GameObject asset, PrefabRegistry.PrefabEntry entry)> mismatches)
			{
				foreach (var (prefab, asset, entry) in mismatches)
				{
					string registryID = entry.uniqueID;
					SetPrefabAssetID(prefab, registryID, "Adopt Registry PrefabAssetID");
					SaveablePrefab assetComponent = asset != null ? asset.GetComponent<SaveablePrefab>() : null;
					if (assetComponent != null && assetComponent != prefab)
						SetPrefabAssetID(assetComponent, registryID, "Adopt Registry PrefabAssetID");
					Logger.Log(
						$"SaveablePrefabEditor: Adopted registry PrefabAssetID '{registryID}' for '{asset.name}'.",
						LogLevel.Info);
				}
				EditorUtility.SetDirty(prefabRegistry);
				AssetDatabase.SaveAssets();
				RefreshIdentitySerializedProperties();
			}

			private void DrawSingleTargetPrefabAssetIDRepairControls()
			{
				EditorGUILayout.Space();
				if (targets.Length != 1)
				{
					EditorGUILayout.HelpBox(
						"Manual PrefabAssetID repair actions are available for a single selected SaveablePrefab. Adopt Registry ID can repair multiple selected mismatches.",
						MessageType.Info);
					return;
				}

				if (!TryGetSingleRepairTarget(
					    out SaveablePrefab prefab,
					    out GameObject asset,
					    out PrefabRegistry.PrefabEntry entry,
					    out string targetReason))
				{
					EditorGUILayout.HelpBox(targetReason, MessageType.Warning);
					return;
				}

				string registryID = entry.uniqueID ?? string.Empty;
				string serializedID = prefab.PrefabAssetID ?? string.Empty;

				EditorGUILayout.LabelField("Prefab Asset", asset.name);
				EditorGUILayout.LabelField("Registry ID", string.IsNullOrEmpty(registryID) ? "<empty>" : registryID);
				EditorGUILayout.LabelField("Serialized Prefab ID", string.IsNullOrEmpty(serializedID) ? "<empty>" : serializedID);

				DrawUpdateRegistryToPrefabIDControl(prefab, asset, entry, serializedID, registryID);
				DrawManualPrefabAssetIDControl(prefab, asset, entry, serializedID, registryID);
				DrawGeneratePrefabAssetIDControl(prefab, asset, entry, registryID);
			}

			private void DrawUpdateRegistryToPrefabIDControl(
				SaveablePrefab prefab,
				GameObject asset,
				PrefabRegistry.PrefabEntry entry,
				string serializedID,
				string registryID)
			{
				string proposedID = serializedID?.Trim();
				bool differsFromRegistry =
					!string.IsNullOrEmpty(proposedID) &&
					!string.Equals(proposedID, registryID, StringComparison.Ordinal);
				string validationReason = null;
				bool canUsePrefabID =
					differsFromRegistry &&
					TryValidatePrefabAssetIDForRepair(proposedID, asset, entry, out validationReason);

				using (new EditorGUI.DisabledScope(!canUsePrefabID))
				{
					if (GUILayout.Button("Update Registry To Prefab ID"))
					{
						string applyReason = null;
						if (ConfirmPrefabAssetIDRepair("Update Registry To Prefab ID", asset.name, registryID, proposedID) &&
						    TryApplyPrefabAssetIDRepair(prefab, asset, entry, proposedID, "Update Registry PrefabAssetID", out applyReason))
						{
							repairManualPrefabAssetID = proposedID;
							EditorGUIUtility.ExitGUI();
						}

						if (!string.IsNullOrEmpty(applyReason))
							ShowPrefabAssetIDRepairFailure(applyReason);
					}
				}

				if (differsFromRegistry && !canUsePrefabID)
					EditorGUILayout.HelpBox(validationReason, MessageType.Warning);
			}

			private void DrawManualPrefabAssetIDControl(
				SaveablePrefab prefab,
				GameObject asset,
				PrefabRegistry.PrefabEntry entry,
				string serializedID,
				string registryID)
			{
				if (string.IsNullOrEmpty(repairManualPrefabAssetID))
					repairManualPrefabAssetID = !string.IsNullOrEmpty(serializedID) ? serializedID : registryID;

				repairManualPrefabAssetID = EditorGUILayout.TextField(
					new GUIContent("Manual Prefab Asset ID", "Advanced repair value to write to both the prefab and its registry row."),
					repairManualPrefabAssetID ?? string.Empty);

				string proposedID = repairManualPrefabAssetID?.Trim();
				bool hasChange =
					!string.IsNullOrEmpty(proposedID) &&
					(!string.Equals(proposedID, serializedID, StringComparison.Ordinal) ||
					 !string.Equals(proposedID, registryID, StringComparison.Ordinal));
				string validationReason = null;
				bool canApplyManual =
					hasChange &&
					TryValidatePrefabAssetIDForRepair(proposedID, asset, entry, out validationReason);

				using (new EditorGUI.DisabledScope(!canApplyManual))
				{
					if (GUILayout.Button("Set Prefab Asset ID Manually"))
					{
						string applyReason = null;
						if (ConfirmPrefabAssetIDRepair("Set Prefab Asset ID Manually", asset.name, registryID, proposedID) &&
						    TryApplyPrefabAssetIDRepair(prefab, asset, entry, proposedID, "Set PrefabAssetID Manually", out applyReason))
						{
							repairManualPrefabAssetID = proposedID;
							EditorGUIUtility.ExitGUI();
						}

						if (!string.IsNullOrEmpty(applyReason))
							ShowPrefabAssetIDRepairFailure(applyReason);
					}
				}

				if (!string.IsNullOrEmpty(proposedID) && hasChange && !canApplyManual)
					EditorGUILayout.HelpBox(validationReason, MessageType.Warning);
			}

			private void DrawGeneratePrefabAssetIDControl(
				SaveablePrefab prefab,
				GameObject asset,
				PrefabRegistry.PrefabEntry entry,
				string registryID)
			{
				if (GUILayout.Button("Generate New Prefab Asset ID"))
				{
					string newID = GenerateUnusedPrefabAssetID(asset, entry, out string generateReason);
					if (string.IsNullOrEmpty(newID))
					{
						ShowPrefabAssetIDRepairFailure(generateReason);
						return;
					}

					string applyReason = null;
					if (ConfirmPrefabAssetIDRepair("Generate New Prefab Asset ID", asset.name, registryID, newID) &&
					    TryApplyPrefabAssetIDRepair(prefab, asset, entry, newID, "Generate PrefabAssetID", out applyReason))
					{
						repairManualPrefabAssetID = newID;
						EditorGUIUtility.ExitGUI();
					}

					if (!string.IsNullOrEmpty(applyReason))
						ShowPrefabAssetIDRepairFailure(applyReason);
				}
			}

			private bool TryGetSingleRepairTarget(
				out SaveablePrefab prefab,
				out GameObject asset,
				out PrefabRegistry.PrefabEntry entry,
				out string reason)
			{
				prefab = target as SaveablePrefab;
				asset = null;
				entry = null;
				reason = null;

				if (prefab == null)
				{
					reason = "Select a SaveablePrefab to repair its PrefabAssetID.";
					return false;
				}

				if (prefabRegistry == null)
				{
					reason = "No active PrefabRegistry is loaded.";
					return false;
				}

				asset = GetCachedPrefabAsset(prefab);
				if (asset == null)
				{
					reason = "This object does not resolve to a prefab asset. Select a prefab asset, prefab instance, or Prefab Mode object with a real prefab source.";
					return false;
				}

				if (asset.GetComponent<SaveablePrefab>() == null)
				{
					reason = $"The resolved prefab asset '{asset.name}' does not contain a SaveablePrefab component.";
					return false;
				}

				return TryGetSingleRegistryEntryForAsset(asset, out entry, out reason);
			}

			private bool TryGetSingleRegistryEntryForAsset(
				GameObject asset,
				out PrefabRegistry.PrefabEntry entry,
				out string reason)
			{
				entry = null;
				reason = null;

				if (prefabRegistry == null)
				{
					reason = "No active PrefabRegistry is loaded.";
					return false;
				}

				if (asset == null)
				{
					reason = "No prefab asset could be resolved.";
					return false;
				}

				List<PrefabRegistry.PrefabEntry> rows = GetRegistryEntriesForAsset(asset);
				if (rows.Count == 0)
				{
					reason = $"Prefab asset '{asset.name}' is not registered. Register the prefab first, then repair its PrefabAssetID.";
					return false;
				}

				if (rows.Count > 1)
				{
					reason = $"Prefab asset '{asset.name}' has {rows.Count} PrefabRegistry rows. Run Clean Duplicates before repairing its PrefabAssetID.";
					return false;
				}

				entry = rows[0];
				return true;
			}

			private List<PrefabRegistry.PrefabEntry> GetRegistryEntriesForAsset(GameObject asset)
			{
				PrefabRegistryIdentityIndex index = GetPrefabIdentityIndex();
				if (index == null || asset == null)
					return new List<PrefabRegistry.PrefabEntry>();

				return index.GetRowsForAsset(asset)
					.Where(row => row?.Entry != null)
					.Select(row => row.Entry)
					.ToList();
			}

			private PrefabRegistryIdentityIndex GetPrefabIdentityIndex()
			{
				if (prefabRegistry == null)
					return null;

				return prefabIdentityIndex ??=
					PrefabRegistryIdentityIndex.GetOrCreate(prefabRegistry);
			}

			private bool TryValidatePrefabAssetIDForRepair(
				string proposedID,
				GameObject asset,
				PrefabRegistry.PrefabEntry targetEntry,
				out string reason,
				bool scanPrefabAssets = false)
			{
				reason = null;
				proposedID = proposedID?.Trim();

				if (string.IsNullOrEmpty(proposedID))
				{
					reason = "PrefabAssetID cannot be empty.";
					return false;
				}

				if (!TryGetSingleRegistryEntryForAsset(asset, out PrefabRegistry.PrefabEntry activeEntry, out reason))
					return false;

				PrefabRegistry.PrefabEntry repairEntry = targetEntry ?? activeEntry;
				if (!ReferenceEquals(activeEntry, repairEntry))
				{
					reason = "The PrefabRegistry row changed while drawing the inspector. Refresh the selection and try again.";
					return false;
				}

				PrefabRegistry.PrefabEntry conflictingEntry = FindConflictingRegistryEntryForID(proposedID, asset, repairEntry);
				if (conflictingEntry != null)
				{
					reason = $"PrefabAssetID '{proposedID}' is already used by {GetRegistryEntryDisplayName(conflictingEntry)}.";
					return false;
				}

				if (scanPrefabAssets)
				{
					GameObject conflictingAsset = FindConflictingPrefabAssetForID(proposedID, asset);
					if (conflictingAsset != null)
					{
						reason = $"PrefabAssetID '{proposedID}' is already serialized on prefab asset '{conflictingAsset.name}' at {AssetDatabase.GetAssetPath(conflictingAsset)}.";
						return false;
					}
				}

				return true;
			}

			private PrefabRegistry.PrefabEntry FindConflictingRegistryEntryForID(
				string proposedID,
				GameObject targetAsset,
				PrefabRegistry.PrefabEntry targetEntry)
			{
				PrefabRegistryIdentityIndex index = GetPrefabIdentityIndex();
				if (index == null || string.IsNullOrEmpty(proposedID))
					return null;

				foreach (PrefabRegistryIdentityRow row in index.GetRowsForId(proposedID))
				{
					PrefabRegistry.PrefabEntry entry = row?.Entry;
					if (entry == null)
						continue;
					if (ReferenceEquals(entry, targetEntry))
						continue;
					if (row.ResolvedAsset != null && index.AreSameAsset(row.ResolvedAsset, targetAsset))
						continue;

					return entry;
				}

				return null;
			}

			private bool TryApplyPrefabAssetIDRepair(
				SaveablePrefab prefab,
				GameObject asset,
				PrefabRegistry.PrefabEntry entry,
				string proposedID,
				string undoName,
				out string reason)
			{
				reason = null;
				proposedID = proposedID?.Trim();

				if (prefab == null || asset == null || entry == null)
				{
					reason = "Missing prefab, prefab asset, or PrefabRegistry row.";
					return false;
				}

				if (!TryValidatePrefabAssetIDForRepair(proposedID, asset, entry, out reason, scanPrefabAssets: true))
					return false;

				string previousRegistryID = entry.uniqueID;
				Undo.IncrementCurrentGroup();
				int undoGroup = Undo.GetCurrentGroup();
				Undo.SetCurrentGroupName(undoName);

				Undo.RecordObject(prefabRegistry, undoName);
				entry.uniqueID = proposedID;
				entry.prefab = asset;
				prefabRegistry.RebuildLookup();
				EditorUtility.SetDirty(prefabRegistry);
				PrefabRegistryIdentityIndex.NotifyRegistryMutated(prefabRegistry);

				SetPrefabAssetID(prefab, proposedID, undoName);

				SaveablePrefab assetComponent = asset.GetComponent<SaveablePrefab>();
				if (assetComponent != null && assetComponent != prefab)
					SetPrefabAssetID(assetComponent, proposedID, undoName);

				EditorUtility.SetDirty(asset);
				PrefabRegistryIdentityIndex.NotifyPrefabIdentityChanged(asset);
				PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(prefabRegistry);
				prefabIdentityIndex = null;
				AssetDatabase.SaveAssets();
				Undo.CollapseUndoOperations(undoGroup);
				RefreshIdentitySerializedProperties();

				Logger.Log(
					$"SaveablePrefabEditor: Repaired PrefabAssetID for '{asset.name}' from '{previousRegistryID}' to '{proposedID}'.",
					LogLevel.Info);
				return true;
			}

			private string GenerateUnusedPrefabAssetID(
				GameObject asset,
				PrefabRegistry.PrefabEntry targetEntry,
				out string reason)
			{
				reason = null;
				for (int i = 0; i < 16; i++)
				{
					string candidate = Guid.NewGuid().ToString();
					if (TryValidatePrefabAssetIDForRepair(candidate, asset, targetEntry, out reason, scanPrefabAssets: true))
						return candidate;
				}

				return null;
			}

			private static bool ConfirmPrefabAssetIDRepair(string title, string assetName, string oldID, string newID)
			{
				return EditorUtility.DisplayDialog(
					title,
					$"This will change the PrefabAssetID for '{assetName}'. Existing save files that reference the old ID may not restore this prefab unless you migrate them.\n\nOld registry ID: {FormatIDForDialog(oldID)}\nNew ID: {FormatIDForDialog(newID)}",
					"Apply",
					"Cancel");
			}

			private static void ShowPrefabAssetIDRepairFailure(string reason)
			{
				string message = string.IsNullOrEmpty(reason)
					? "PrefabAssetID repair could not be completed."
					: reason;

				EditorUtility.DisplayDialog("Prefab Asset ID Repair", message, "OK");
				Logger.Log($"SaveablePrefabEditor: PrefabAssetID repair failed. {message}", LogLevel.Warning);
			}

			private static string FormatIDForDialog(string value)
			{
				return string.IsNullOrEmpty(value) ? "<empty>" : value;
			}

			private static string GetRegistryEntryDisplayName(PrefabRegistry.PrefabEntry entry)
			{
				if (entry == null) return "<unknown registry row>";
				if (entry.prefab == null) return "<registry row with missing prefab>";

				string path = AssetDatabase.GetAssetPath(entry.prefab);
				return string.IsNullOrEmpty(path)
					? $"'{entry.prefab.name}'"
					: $"'{entry.prefab.name}' at {path}";
			}

			private static GameObject FindConflictingPrefabAssetForID(string proposedID, GameObject targetAsset)
			{
				if (string.IsNullOrEmpty(proposedID))
					return null;

				string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
				foreach (string guid in prefabGuids)
				{
					string path = AssetDatabase.GUIDToAssetPath(guid);
					if (string.IsNullOrEmpty(path))
						continue;

					GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
					if (prefabAsset == null || IsSamePrefabAsset(prefabAsset, targetAsset))
						continue;

					SaveablePrefab saveablePrefab = prefabAsset.GetComponent<SaveablePrefab>();
					if (saveablePrefab == null)
						continue;

					if (string.Equals(saveablePrefab.PrefabAssetID, proposedID, StringComparison.Ordinal))
						return prefabAsset;
				}

				return null;
			}

			private static bool IsSamePrefabAsset(GameObject left, GameObject right)
			{
				if (left == null || right == null) return false;
				if (left == right) return true;

				string leftKey = GetPrefabAssetKey(left);
				string rightKey = GetPrefabAssetKey(right);
				return !string.IsNullOrEmpty(leftKey) &&
				       string.Equals(leftKey, rightKey, StringComparison.Ordinal);
			}

			private static string GetPrefabAssetKey(GameObject prefab)
			{
				if (prefab == null) return null;

				GameObject asset = GetPrefabAsset(prefab) ?? prefab;
				string assetPath = AssetDatabase.GetAssetPath(asset);
				if (!string.IsNullOrEmpty(assetPath))
					return assetPath;

#if UNITY_6000_5_OR_NEWER
				return $"instance:{asset.GetEntityId()}";
#else
				return $"instance:{asset.GetInstanceID()}";
#endif
			}

			/*──────────── Identity & duplicates ───────────────────────*/
		private void DrawIdentityHelpBox()
		{
			EditorGUILayout.HelpBox(
				"UniqueID is assigned at runtime.\n" +
				"PrefabAssetID is generated and stored in the Prefab Registry.",
				MessageType.Info);
		}
		private void DrawUniqueIDField()
		{
			bool showClearControls = ShouldShowClearInstanceUniqueIDControls();

			EditorGUILayout.BeginHorizontal();
			using (new EditorGUI.DisabledGroupScope(true))
			{
				EditorGUILayout.TextField("Instance Unique ID", GetFieldText(uniqueIDProperty));
			}

			if (showClearControls)
			{
				if (GUILayout.Button(new GUIContent("Clear ID", "Reset the Instance Unique ID so a fresh value is generated at runtime."), GUILayout.Width(90)))
				{
					ClearInstanceUniqueIDs();
					serializedObject.Update();
				}
			}
				EditorGUILayout.EndHorizontal();

			if (showClearControls)
			{
				EditorGUILayout.HelpBox(
					"This prefab currently stores an Instance Unique ID. If you instantiate it multiple times, duplicated IDs can break save data. Click 'Clear ID' so each instance generates its own ID at runtime.",
					MessageType.Warning);
				}
			}

			private bool IsAnyTargetInPrefabMode()
			{
				foreach (var obj in targets)
				{
					if (obj is not SaveablePrefab prefab) continue;
					if (IsInPrefabMode(prefab.gameObject)) return true;
				}

				return false;
			}
				private void DrawPrefabAssetIDField()
				{
					EditorGUILayout.BeginHorizontal();
					using (new EditorGUI.DisabledGroupScope(true))
					{
						EditorGUILayout.TextField("Prefab Asset ID", GetPrefabAssetIDDisplayText());
					}

					if (!Application.isPlaying && prefabRegistry != null)
					{
						bool showRepairTools = GUILayout.Toggle(
							showPrefabAssetIDRepairTools,
							new GUIContent(
								"Repair...",
								"Show advanced Prefab Asset ID registration and repair tools."),
							EditorStyles.miniButton,
							GUILayout.Width(70));

						if (showRepairTools != showPrefabAssetIDRepairTools)
						{
							showPrefabAssetIDRepairTools = showRepairTools;
							if (showPrefabAssetIDRepairTools)
								InitializePrefabAssetIDRepairInput();
						}
					}

					EditorGUILayout.EndHorizontal();
				}
			private string GetFieldText(SerializedProperty prop) =>
				targets.Length == 1 ? prop.stringValue :
				(AreAllPropertyValuesEqual(prop) ? prop.stringValue : "—");

			private string GetPrefabAssetIDDisplayText()
			{
				if (targets.Length == 1 && target is SaveablePrefab singlePrefab)
					return GetPrefabAssetIDDisplayText(singlePrefab);

				string first = null;
				bool hasValue = false;
				foreach (var obj in targets)
				{
					if (obj is not SaveablePrefab prefab) continue;

					string value = GetPrefabAssetIDDisplayText(prefab);
					if (!hasValue)
					{
						first = value;
						hasValue = true;
						continue;
					}

					if (!string.Equals(first, value, StringComparison.Ordinal))
						return "—";
				}

				return first ?? string.Empty;
			}

			private string GetPrefabAssetIDDisplayText(SaveablePrefab prefab)
			{
				if (prefab == null) return string.Empty;

				GameObject asset = GetCachedPrefabAsset(prefab);
				PrefabRegistryIdentityIndex index = GetPrefabIdentityIndex();
				return asset != null &&
				       index != null &&
				       index.TryGetAuthoritativeId(asset, out string registryId)
					? registryId
					: prefab.PrefabAssetID;
			}

		/*──────────── Keep-Across-Scenes toggle (patched) ─────────*/
                private void DrawKeepAcrossScenesToggle()
                {
                        EditorGUI.BeginChangeCheck();
                        SerializedProperty copy = keepAcrossScenesProperty.Copy();
                        using (new EditorGUI.DisabledScope(rememberHomeSceneProperty.boolValue))
                        {
                        EditorGUILayout.PropertyField(copy, new GUIContent("Keep Across Scenes",
                                        "Preserve this root GameObject across scene loads. Enabling this automatically disables pooling to ensure correct behaviour."));
                        }
                        if (!EditorGUI.EndChangeCheck()) return;

                        bool keepAcrossScenes = copy.boolValue;
                        var addVisibility = new List<GameObject>();
                        var removeVisibility = new List<PersistentVisibilityController>();
                        bool invalidRoot = false;
                        bool showInvalidRootDialog = false;

			foreach (var obj in targets)
			{
				if (obj is not SaveablePrefab prefab) continue;
                                SerializedObject so = new(prefab);
                                SerializedProperty prop = so.FindProperty("keepAcrossScenes");

                                Undo.RecordObject(prefab, "Toggle Keep Across Scenes");
                                prop.boolValue = keepAcrossScenes;

	                                if (prop.boolValue)
	                                {
	                                        if (prefab.transform.root == prefab.transform)
	                                        {
	                                                bool disablePoolingChanged = false;
	                                                SerializedProperty disableProp = so.FindProperty("disablePooling");
                                                if (disableProp != null && !disableProp.boolValue)
                                                {
                                                        disableProp.boolValue = true;
	                                                        disablePoolingChanged = true;
	                                                }

	                                                SerializedProperty handoffProp = so.FindProperty("enableCrossSceneStateHandoff");
	                                                if (handoffProp != null && handoffProp.boolValue)
	                                                {
	                                                        handoffProp.boolValue = false;
	                                                }

	                                                so.ApplyModifiedProperties();
                                                EditorUtility.SetDirty(prefab);

                                                if (disablePoolingChanged)
                                                {
                                                        SyncComponentSettingToPrefabRegistry(prefab);
                                                }

	                                                if (!prefab.GetComponent<PersistentVisibilityController>())
	                                                {
	                                                        addVisibility.Add(prefab.gameObject);
                                                }
                                        }
                                        else
                                        {
                                                prop.boolValue = false;
                                                so.ApplyModifiedProperties();
                                                EditorUtility.SetDirty(prefab);
                                                Logger.Log("'Keep Across Scenes' reset: object is not root.", LogLevel.Warning);

                                                invalidRoot = true;
                                                showInvalidRootDialog |= Array.IndexOf(targets, obj) == 0;
                                        }
                                }
                                else
                                {
                                        so.ApplyModifiedProperties();
                                        EditorUtility.SetDirty(prefab);

                                        var pvc = prefab.GetComponent<PersistentVisibilityController>();
	                                        if (pvc) removeVisibility.Add(pvc);
                                }
                        }

                        serializedObject.Update();

                        // Finish every selected object before ending this GUI event. Each
                        // deferred batch rechecks intent so an intervening toggle or Undo
                        // cannot add/remove a controller for obsolete persistence settings.
                        if (addVisibility.Count > 0)
                        {
                                if (pendingAddPVC != null) EditorApplication.delayCall -= pendingAddPVC;
                                pendingAddPVC = () =>
                                {
                                        pendingAddPVC = null;
                                        foreach (GameObject go in addVisibility)
                                        {
                                                if (!go) continue;
                                                SaveablePrefab owner = go.GetComponent<SaveablePrefab>();
                                                if (owner && owner.KeepAcrossScenes && owner.transform.root == owner.transform &&
                                                    !go.GetComponent<PersistentVisibilityController>())
                                                        Undo.AddComponent<PersistentVisibilityController>(go);
                                        }
                                };
#pragma warning disable UDR0004 // Canceled by CancelPendingDelayCalls from OnDisable/OnDestroy.
                                EditorApplication.delayCall += pendingAddPVC;
#pragma warning restore UDR0004
                        }
                        if (removeVisibility.Count > 0)
                        {
                                if (pendingRemovePVC != null) EditorApplication.delayCall -= pendingRemovePVC;
                                pendingRemovePVC = () =>
                                {
                                        pendingRemovePVC = null;
                                        foreach (PersistentVisibilityController component in removeVisibility)
                                        {
                                                if (!component) continue;
                                                SaveablePrefab owner = component.GetComponent<SaveablePrefab>();
                                                if (owner && !owner.KeepAcrossScenes)
                                                        Undo.DestroyObjectImmediate(component);
                                        }
                                };
#pragma warning disable UDR0004 // Canceled by CancelPendingDelayCalls from OnDisable/OnDestroy.
                                EditorApplication.delayCall += pendingRemovePVC;
#pragma warning restore UDR0004
                        }
                        if (showInvalidRootDialog)
                                EditorUtility.DisplayDialog("Invalid Configuration",
                                        "This GameObject is not a root object. 'Keep Across Scenes' has been disabled.", "OK");
                        if (invalidRoot || addVisibility.Count > 0 || removeVisibility.Count > 0)
                                EditorGUIUtility.ExitGUI();
                }

                private void DrawKeepAcrossScenesVisibilitySettings()
                {
                        CrystalSaveEditorStyles.DrawSectionHeader("Visibility Settings");

                        DrawVisibleInScenesSection();
                        DrawOffScreenBehaviourSection();
                }

                private void DrawCrossSceneStateHandoffSettings()
                {
                        if (enableCrossSceneStateHandoffProperty == null)
                                return;

                        EditorGUILayout.Space(4);
                        CrystalSaveEditorStyles.DrawSectionHeader("Cross-Scene State Handoff");

                        bool keepAcrossScenesOn = keepAcrossScenesProperty != null && keepAcrossScenesProperty.boolValue;
                        using (new EditorGUI.DisabledScope(keepAcrossScenesOn))
                        {
                                EditorGUILayout.PropertyField(
                                        enableCrossSceneStateHandoffProperty,
                                        new GUIContent(
                                                "Enable State Handoff",
                                                "When enabled, this prefab's saved state can be applied to a matching scene-baked SaveablePrefab with the same PrefabAssetID in a later scene. Mutually exclusive with Keep Across Scenes."
                                        ));
                        }

                        if (keepAcrossScenesOn)
                        {
                                EditorGUILayout.HelpBox(
                                        "State Handoff is disabled because Keep Across Scenes keeps the original object alive across scene loads.",
                                        MessageType.Info);
                                return;
                        }

                        if (!enableCrossSceneStateHandoffProperty.boolValue &&
                            !enableCrossSceneStateHandoffProperty.hasMultipleDifferentValues)
                                return;

                        EditorGUI.indentLevel++;

                        if (handoffToSceneBakedPrefabsProperty != null)
                        {
                                EditorGUILayout.PropertyField(
                                        handoffToSceneBakedPrefabsProperty,
                                        new GUIContent(
                                                "Apply To Scene-Baked Prefabs",
                                                "Claims one matching scene-baked SaveablePrefab in the loaded scene. Runtime-created instances are not claimed by this option."
                                        ));
                        }

                        if (handoffTransformProperty != null)
                        {
                                EditorGUILayout.PropertyField(
                                        handoffTransformProperty,
                                        new GUIContent(
                                                "Carry Root Transform",
                                                "Apply the saved root position, rotation, scale, and parent-aware transform data to the matching scene-baked prefab."
                                        ));
                        }

                        if (handoffRuntimeModificationsProperty != null)
                        {
                                EditorGUILayout.PropertyField(
                                        handoffRuntimeModificationsProperty,
                                        new GUIContent(
                                                "Carry Runtime Overrides",
                                                "Apply tracked runtime override data such as child state, child transforms, mesh/material swaps, blendshapes, and component blobs."
                                        ));
                        }

                        EditorGUILayout.HelpBox(
                                "State handoff is an opt-in alternative to Keep Across Scenes. It claims exactly one matching scene-baked SaveablePrefab by PrefabAssetID and skips ambiguous multi-match cases.",
                                MessageType.Info);

                        EditorGUI.indentLevel--;
                }

                /*──────────── Save-system toggle (unchanged) ──────────────*/
                private void DrawRegisterWithSaveSystemToggle()
                {
                        CrystalSaveEditorStyles.DrawSectionHeader("Save System Settings");

			// 1) Register toggle with change handling (operate on this serializedObject)
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(
				registerWithSaveSystemProperty,
				new GUIContent(
					"Register With Save System",
					"Uncheck to exclude pooled prefabs from saving"));
			bool regChanged = EditorGUI.EndChangeCheck();
			// Cache the value for UI below before applying modifications
			bool regValueForUI = registerWithSaveSystemProperty.boolValue;

			if (regChanged)
			{
				// Commit the change once for all selected objects
				serializedObject.ApplyModifiedProperties();

				QueueRegistrySyncForSelection(
					targets.OfType<SaveablePrefab>().Where(sp => sp != null).ToArray());

					// Update local copy for the UI state in this frame
				serializedObject.Update();
			}

			// 2) Always show the opt-out toggle; disable when registration is ON
						using (new EditorGUI.DisabledScope(regValueForUI))
			{
				if (autoAssignInstanceIDWhenUnregisteredProperty != null)
				{
					EditorGUILayout.PropertyField(
						autoAssignInstanceIDWhenUnregisteredProperty,
						new GUIContent(
							"Auto-Assign Instance ID When Unregistered",
							"When ON and this object is scene-placed (not runtime-instantiated), " +
							"generate an Instance Unique ID during play if Register With Save System is OFF and the ID is empty."));
				}
                        }
                }

		private void QueueRegistrySyncForSelection(SaveablePrefab[] selected)
		{
			if (selected == null || selected.Length == 0)
				return;

			if (pendingRegistrySync != null)
				EditorApplication.delayCall -= pendingRegistrySync;

			pendingRegistrySync = () =>
			{
				pendingRegistrySync = null;
				if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
					return;

				bool autoRegisterPrefabs = CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs;
				foreach (SaveablePrefab prefab in selected)
				{
					if (prefab == null) continue;
					if (prefab.RegisterWithSaveSystem)
					{
						if (!autoRegisterPrefabs) continue;
						GameObject asset = GetCachedPrefabAsset(prefab);
						if (asset != null)
							SaveablePrefab.EnsurePrefabAssetIdentityForEditor(prefab, asset);
					}
					else
					{
						DeregisterPrefab(prefab);
					}
				}
			};
#pragma warning disable UDR0004 // Canceled by CancelPendingDelayCalls from OnDisable/OnDestroy.
			EditorApplication.delayCall += pendingRegistrySync;
#pragma warning restore UDR0004
		}

                private void DrawReuseSceneInstanceToggle()
                {
                        if (reuseSceneInstanceOnLoadProperty == null)
                                return;

                        EditorGUILayout.PropertyField(
                                reuseSceneInstanceOnLoadProperty,
                                new GUIContent(
                                        "Reuse Scene Instance On Load",
					"When enabled and a matching scene instance still exists during load, " +
					"Crystal Save reuses that object instead of destroying and respawning it. " +
					"Design-time (scene-baked) prefabs must keep this ON to avoid duplicate instances when the scene reloads."));
                }

                private void DrawRememberHomeSceneToggle()
                {
                        EditorGUI.BeginChangeCheck();
                        SerializedProperty copy = rememberHomeSceneProperty.Copy();
                        using (new EditorGUI.DisabledScope(keepAcrossScenesProperty.boolValue))
                        {
				EditorGUILayout.PropertyField(
					copy,
					new GUIContent(
						"Remember Home Scene",
						"Restore this prefab only in the scene it was spawned. When ON, Remember Destroyed is turned off automatically to avoid duplicate respawns."
					));
                                if (copy.boolValue)
                                {
                                        using (new EditorGUI.IndentLevelScope())
                                        {
						if (homeSceneModeProperty != null)
						{
							EditorGUILayout.PropertyField(homeSceneModeProperty, new GUIContent("Home Scene Mode"));
						}
						else
						{
							EditorGUILayout.HelpBox("Missing serialized field 'homeSceneCaptureMode' on SaveablePrefab.", MessageType.Warning);
						}
                                        }
                                }
                        }
                        if (!EditorGUI.EndChangeCheck()) return;

                        foreach (var obj in targets)
                        {
                                if (obj is not SaveablePrefab prefab) continue;
                                SerializedObject so = new(prefab);
                                SerializedProperty prop = so.FindProperty("rememberHomeScene");
								SerializedProperty modeProp = so.FindProperty("homeSceneCaptureMode");
                                Undo.RecordObject(prefab, "Toggle Remember Home Scene");
                                prop.boolValue = copy.boolValue;
                                modeProp.enumValueIndex = homeSceneModeProperty.enumValueIndex;
                                if (prop.boolValue)
                                {
                                        so.FindProperty("keepAcrossScenes").boolValue = false;
                                        var ps  = so.FindProperty("propertySettings");
                                        var rem = ps?.FindPropertyRelative("RememberDestroyed");
                                        if (rem != null) rem.boolValue = false;

                                        var off = so.FindProperty("offScreenMask");
                                        if (off != null) off.enumValueFlag = (int)OffScreenDeactivation.None;
                                }
                                so.ApplyModifiedProperties();
                                EditorUtility.SetDirty(prefab);

                                // ── Propagate RememberHomeScene to sibling SaveableComponents ──
                                // The SaveableComponentEditor greys out the toggle when a
                                // SaveablePrefab is present, so we must push the value at
                                // design time so it is serialized before play mode.
                                foreach (var sc in prefab.GetComponents<SaveableComponent>())
                                {
                                        if (sc == null) continue;
                                        Undo.RecordObject(sc, "Sync RememberHomeScene from SaveablePrefab");
                                        var scSo = new SerializedObject(sc);
                                        var scRem  = scSo.FindProperty("rememberHomeScene");
                                        var scHome = scSo.FindProperty("homeScene");
                                        if (scRem != null) scRem.boolValue = prop.boolValue;
                                        if (scHome != null && prop.boolValue)
                                        {
                                                var prefabHome = so.FindProperty("homeScene");
                                                if (prefabHome != null)
                                                        scHome.stringValue = prefabHome.stringValue;
                                        }
                                        scSo.ApplyModifiedProperties();
                                        EditorUtility.SetDirty(sc);
                                }
                        }
                }

                /*──────────── GameObject property settings ─────────────*/
                private void DrawPropertySettingsSection()
                {
                        if (propertySettingsProperty == null || serializedObject == null) return;
                        
                        // Safety check to prevent disposed SerializedObject errors
                        try 
                        {
                                if (serializedObject.targetObject == null) return;
                        }
                        catch (System.Exception)
                        {
                                // SerializedObject might be disposed, skip drawing this frame
                                return;
                        }

                        bool hasRememberGO = targets.Cast<SaveablePrefab>()
                                .Any(sp => sp != null && sp.GetComponent<RememberGameObject>() != null);

                        CrystalSaveEditorStyles.BeginSection("GameObject Property Settings");
                        EditorGUI.indentLevel++;

                        if (hasRememberGO)
                        {
                                EditorGUILayout.HelpBox(
                                        "Settings are managed by the Remember GameObject component and will be ignored.",
                                        MessageType.Info);
                        }

                        SafeDrawPropertyField(rememberActiveProperty, "Remember Active");
                        SafeDrawPropertyField(rememberNameProperty, "Remember Name");
                        SafeDrawPropertyField(rememberLayerProperty, "Remember Layer");
			SafeDrawPropertyField(rememberTagProperty, "Remember Tag");

			bool rememberHomeSceneLocked = rememberHomeSceneProperty != null &&
				!SafeHasMultipleDifferentValues(rememberHomeSceneProperty) &&
				SafeGetBoolValue(rememberHomeSceneProperty, false);

			using (new EditorGUI.DisabledScope(rememberHomeSceneLocked))
			{
				SafeDrawPropertyFieldWithTooltip(
					rememberDestroyedProperty,
					"Remember Destroyed",
					rememberHomeSceneLocked
						? "Disabled because Remember Home Scene turns this off automatically."
						: "Persist destroyed state so the prefab stays removed after loading."
				);
			}

			if (rememberHomeSceneLocked)
			{
				EditorGUILayout.HelpBox(
					"Remember Destroyed stays OFF while Remember Home Scene is enabled, preventing duplicate respawns.",
					MessageType.Info);
			}

                        EditorGUI.indentLevel--;
                        CrystalSaveEditorStyles.EndSection();
                }

#if CRYSTALSAVE_TIMEMACHINE
		/// <summary>
		/// Draws the Time Machine Recording section in the inspector.
		/// </summary>
		private void DrawTimeMachineSection()
		{
			if (enableTimeMachineRecordingProperty == null) return;

			CrystalSaveEditorStyles.BeginSection("Time Machine Recording");
			EditorGUI.indentLevel++;
			
			// Enable Time Machine Recording checkbox is always editable
			EditorGUILayout.PropertyField(enableTimeMachineRecordingProperty, 
				new GUIContent("Enable Time Machine Recording", 
				"Enable TimeMachine recording for this prefab instance. Automatically adds/configures a TimeMachineRecorder component when enabled."));

			// Only show settings if recording is enabled
			// Use Where to filter out null targets before checking properties
			bool anyTimeMachineEnabled = targets.OfType<SaveablePrefab>()
				.Where(sp => sp != null)  // Filter out destroyed objects
				.Any(sp =>
				{
					try
					{
						var so = new SerializedObject(sp);
						var prop = so.FindProperty("enableTimeMachineRecording");
						return prop != null && prop.boolValue;
					}
					catch
					{
						// Object was destroyed or SerializedObject is invalid
						return false;
					}
				});

			if (anyTimeMachineEnabled)
			{
				EditorGUI.indentLevel++;
				
				EditorGUILayout.PropertyField(autoStartRecordingProperty, 
					new GUIContent("Register On Enable", 
						"Automatically register this GameObject with the TimeMachine system when enabled.\n\n" +
						"✅ ENABLED (default): GameObject auto-registers when prefab instantiates/enables\n" +
						"❌ DISABLED: You must manually call StartRecording() via script\n\n" +
						"⚠️ TWO-TIER SYSTEM:\n" +
						"• This flag controls PER-OBJECT registration (Tier 1)\n" +
						"• GameObjectTimeMachine.recordingEnabled controls GLOBAL snapshot capture (Tier 2, defaults to TRUE)\n" +
						"• BOTH must be true for actual recording to happen!\n\n" +
						"When you instantiate this prefab with default settings:\n" +
						"1. This object registers itself (autoStartRecording = true)\n" +
						"2. Global recording is already enabled (recordingEnabled = true)\n" +
						"3. Recording starts IMMEDIATELY (snapshots captured every frame/interval)"));
				
				EditorGUILayout.Space(5);
				EditorGUILayout.PropertyField(overrideRecordingSettingsProperty,
					new GUIContent("Override Recording Settings",
						"Override the global recording settings from SaveSettings. When disabled, uses global defaults for Use Interval, Snapshot Interval, and Max Snapshots."));
				
				// Indent and show recording settings based on override
				EditorGUI.indentLevel++;
				
				// Get global values for display
				SaveSettings saveSettings = SaveManager.Instance?.SaveSettings;
				bool globalUseInterval = saveSettings != null ? saveSettings.timeMachineUseInterval : true;
				float globalSnapshotInterval = saveSettings != null ? saveSettings.timeMachineSnapshotInterval : 0.1f;
				int globalMaxSnapshots = saveSettings != null ? saveSettings.timeMachineMaxSnapshots : 500;
				
				// Show effective values
				if (overrideRecordingSettingsProperty.boolValue)
				{
					// Show editable fields when overriding
					EditorGUILayout.PropertyField(useIntervalProperty, 
						new GUIContent("Use Interval", "Record snapshots at regular intervals instead of every frame."));
					EditorGUILayout.PropertyField(snapshotIntervalProperty, 
						new GUIContent("Snapshot Interval", 
							"Time between snapshots in seconds (only used if 'Use Interval' is enabled).\n\n" +
							"Common Settings:\n" +
							"• 0.05s = 20 snapshots/sec (very smooth, debug mode)\n" +
							"• 0.1s = 10 snapshots/sec (recommended for gameplay)\n" +
							"• 0.2s = 5 snapshots/sec (sufficient for replays)\n" +
							"• 0.5s = 2 snapshots/sec (slow-moving objects)\n\n" +
							"Tip: Combine with Max Snapshots to control rewind window.\n" +
							"Example: 500 snapshots @ 0.1s = 50 seconds of history."));
					EditorGUILayout.PropertyField(maxSnapshotsProperty, 
						new GUIContent("Max Snapshots", 
							"Maximum number of snapshots to keep (oldest are discarded automatically).\n\n" +
							"Memory Guide (per GameObject):\n" +
							"• 500 snapshots @ 0.1s = 50 sec rewind ≈ 300-600 KB\n" +
							"• 1000 snapshots @ 0.1s = 100 sec rewind ≈ 600-1200 KB\n" +
							"• 3000 snapshots @ 0.1s = 5 min rewind ≈ 1.8-3.6 MB\n\n" +
							"CPU Performance (Replay Cost):\n" +
							"• 100 snapshots replay: ~5-30ms total (negligible!)\n" +
							"• Real-time playback: ~0.1-0.5ms/frame (no impact)\n" +
							"• Fast-forward (10x): ~3-5ms/frame (smooth at 60 FPS)\n" +
							"Replaying snapshots is VERY fast - CPU is not a bottleneck!\n\n" +
							"Recommendations:\n" +
							"• Player/Debug: 500-1000 snapshots (very low cost)\n" +
							"• Replay System: 3000-6000 snapshots (~2-6 MB)\n" +
							"• Intensive Recording: 10000+ snapshots (~6-12 MB)\n\n" +
							"On modern systems (32-64 GB RAM), even 10,000 snapshots per object is negligible. " +
							"Use the TimeMachine Player window to monitor actual memory usage."));
				}
				else
				{
					// Show read-only global values when not overriding
					using (new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.Toggle(new GUIContent("Use Interval", $"Using global default: {globalUseInterval}"), globalUseInterval);
						EditorGUILayout.FloatField(new GUIContent("Snapshot Interval", $"Using global default: {globalSnapshotInterval}s"), globalSnapshotInterval);
						EditorGUILayout.IntField(new GUIContent("Max Snapshots", $"Using global default: {globalMaxSnapshots}"), globalMaxSnapshots);
					}
				}
				
				// Preview calculation (use effective values)
				bool effectiveUseInterval = overrideRecordingSettingsProperty.boolValue ? useIntervalProperty.boolValue : globalUseInterval;
				float effectiveInterval = overrideRecordingSettingsProperty.boolValue ? snapshotIntervalProperty.floatValue : globalSnapshotInterval;
				int effectiveMaxSnapshots = overrideRecordingSettingsProperty.boolValue ? maxSnapshotsProperty.intValue : globalMaxSnapshots;
				
				if (effectiveUseInterval)
				{
					if (effectiveInterval > 0 && effectiveMaxSnapshots > 0)
					{
						float totalSeconds = effectiveInterval * effectiveMaxSnapshots;
						
						// Calculate estimated memory (using median values from analysis)
						float estimatedMemoryKB = effectiveMaxSnapshots * 0.75f;
						
						// Format time display
						string timeDisplay;
						if (totalSeconds < 60)
						{
							timeDisplay = $"{totalSeconds:F1} seconds";
						}
						else if (totalSeconds < 3600)
						{
							float minutes = totalSeconds / 60f;
							timeDisplay = $"{minutes:F1} minutes ({totalSeconds:F0} sec)";
						}
						else
						{
							float hours = totalSeconds / 3600f;
							float minutes = (totalSeconds % 3600) / 60f;
							timeDisplay = $"{hours:F1} hours ({minutes:F0} min)";
						}
						
						// Format memory display
						string memoryDisplay;
						if (estimatedMemoryKB < 1024)
						{
							memoryDisplay = $"{estimatedMemoryKB:F0} KB";
						}
						else
						{
							float memoryMB = estimatedMemoryKB / 1024f;
							memoryDisplay = $"{memoryMB:F2} MB";
						}
						
						// Calculate CPU performance estimates
						float replayTimeMs = effectiveMaxSnapshots * 0.25f;
						string cpuDisplay;
						if (replayTimeMs < 16.67f)
						{
							cpuDisplay = $"~{replayTimeMs:F1}ms (instant replay ✅)";
						}
						else
						{
							int framesNeeded = Mathf.CeilToInt(replayTimeMs / 10f);
							cpuDisplay = $"~{replayTimeMs:F1}ms (spread across {framesNeeded} frames)";
						}
						
						string playbackCostDisplay = $"{0.25f:F2}ms/frame";
						
						EditorGUILayout.Space(3);
						EditorGUILayout.HelpBox(
							$"📊 Rewind Window Preview:\n" +
							$"⏱️ Duration: {timeDisplay}\n" +
							$"💾 Est. Memory: {memoryDisplay} (simple: ~{estimatedMemoryKB * 0.6f:F0} KB, complex: ~{estimatedMemoryKB * 1.6f:F0} KB)\n" +
							$"📸 Capture Rate: {(1f / effectiveInterval):F1} snapshots/second\n" +
							$"⚡ Replay All: {cpuDisplay}\n" +
							$"🎮 Real-time Playback: {playbackCostDisplay} (negligible at 60 FPS)",
							MessageType.None);
					}
				}
				
				EditorGUI.indentLevel--;
				EditorGUI.indentLevel--;

				// --- TimeMachine Persistence Settings ---
				EditorGUILayout.Space();
				EditorGUI.indentLevel++;
				
				EditorGUILayout.PropertyField(saveTimeMachineSnapshotsProperty,
					new GUIContent("Save Time Machine Snapshots",
						"Save this GameObject's current state snapshot at save time (position, rotation, scale). This captures the CURRENT STATE only, not the entire recording history.\n\n" +
						"DIFFERENCE FROM RememberTimeMachine:\n" +
						"• This setting: Saves current snapshot (1 point in time)\n" +
						"• RememberTimeMachine: Saves entire recording history (all snapshots for ghost playback)\n\n" +
						"Use this for simple position/state persistence. Use RememberTimeMachine for ghost/clone replay functionality."));
				
				// Only show override and duration if saving is enabled
				if (saveTimeMachineSnapshotsProperty.boolValue)
				{
					EditorGUI.indentLevel++;
					
					EditorGUILayout.PropertyField(overrideMaxSaveDurationProperty,
						new GUIContent("Override Max Save Duration",
							"Override the global Max Save Duration setting from SaveSettings. When disabled, uses SaveSettings.timeMachineMaxSaveDuration."));
					
					// Only show duration field if override is enabled
					if (overrideMaxSaveDurationProperty.boolValue)
					{
						EditorGUILayout.PropertyField(maxSaveDurationProperty,
							new GUIContent("Max Save Duration",
								"Maximum duration (seconds) of timeline snapshots to save.\n" +
								"• Positive value: Save last N seconds\n" +
								"• 0: Don't save any snapshots\n" +
								"• -1: Save entire timeline (unlimited)\n" +
								"Only applies if Override Max Save Duration is enabled."));
						
						// Show help box based on duration value
						float duration = maxSaveDurationProperty.floatValue;
						if (duration < 0)
						{
							EditorGUILayout.HelpBox(
								"⚠️ UNLIMITED MODE: Entire timeline will be saved (may result in large save files!)",
								MessageType.Warning);
						}
						else if (duration == 0)
						{
							EditorGUILayout.HelpBox(
								"❌ PERSISTENCE DISABLED: Recording works but snapshots won't be saved to file.",
								MessageType.Info);
						}
						else
						{
							EditorGUILayout.HelpBox(
								$"💾 Will save last {duration} seconds of timeline snapshots.",
								MessageType.None);
						}
					}
					else
					{
						// Show global setting info
						EditorGUILayout.HelpBox(
							"Using global SaveSettings.timeMachineMaxSaveDuration (default: 30 seconds)",
							MessageType.None);
					}
					
					EditorGUI.indentLevel--;
				}
				
				EditorGUI.indentLevel--;

				// Show info box about TimeMachineRecorder component
				EditorGUILayout.Space(4);
				EditorGUILayout.HelpBox(
					"A TimeMachineRecorder component will be automatically added to this prefab instance when it's instantiated in Play Mode. " +
					"The recorder uses the settings configured above.", 
					MessageType.Info);
			}
			EditorGUI.indentLevel--;
			CrystalSaveEditorStyles.EndSection();
		}
#endif



                /*──────────── Off-screen behaviour UI ─────────────────────*/
                private void DrawOffScreenBehaviourSection()
                {
			CrystalSaveEditorStyles.DrawSectionHeader("Off-Screen Behaviour");

			EditorGUI.BeginChangeCheck();
			var newMask = (OffScreenDeactivation)EditorGUILayout.EnumFlagsField(
				new GUIContent("Components / Rigidbody state"),
				(OffScreenDeactivation)offScreenMaskProperty.enumValueFlag);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObjects(targets, "Change Off-Screen Behaviour");
				foreach (var obj in targets)
				{
					SerializedObject so = new(obj);
					SerializedProperty prop = so.FindProperty("offScreenMask");
					prop.enumValueFlag = (int)newMask;
					so.ApplyModifiedProperties();
				}
			}
		}

		/*──────────── Visible-in-Scenes UI (unchanged) ────────────*/
		private void DrawVisibleInScenesSection()
		{
			if (visibleInScenesProperty == null) return;

			showVisibilitySettingsFoldout =
				EditorGUILayout.Foldout(showVisibilitySettingsFoldout, "Visible In Scenes", true);
			if (!showVisibilitySettingsFoldout) return;

			List<string> allScenes = GetAllSceneNames();
			if (allScenes.Count == 0)
			{
				EditorGUILayout.HelpBox("No scenes in Build Settings.", MessageType.Warning);
				return;
			}

			EditorGUILayout.LabelField("Select scenes where this prefab remains visible:");
			foreach (string sceneName in allScenes)
			{
				bool allHave = targets.Cast<SaveablePrefab>().All(p => p.VisibleInScenes.Contains(sceneName));
				bool noneHave = targets.Cast<SaveablePrefab>().All(p => !p.VisibleInScenes.Contains(sceneName));

				bool state = allHave;
				if (!allHave && !noneHave) EditorGUI.showMixedValue = true;

				EditorGUI.BeginChangeCheck();
				bool newState = EditorGUILayout.ToggleLeft(sceneName, state);
				if (EditorGUI.EndChangeCheck())
				{
					foreach (var obj in targets)
					{
						if (obj is not SaveablePrefab prefab) continue;
						SerializedObject so = new(prefab);
						SerializedProperty list = so.FindProperty("VisibleInScenes");

						if (newState)
						{
							if (!prefab.VisibleInScenes.Contains(sceneName))
							{
								Undo.RecordObject(prefab, "Add Visible Scene");
								list.arraySize++;
								list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = sceneName;
							}
						}
						else
						{
							for (int i = 0; i < list.arraySize; i++)
								if (list.GetArrayElementAtIndex(i).stringValue.Equals(sceneName,
										StringComparison.OrdinalIgnoreCase))
								{
									Undo.RecordObject(prefab, "Remove Visible Scene");
									list.DeleteArrayElementAtIndex(i);
									break;
								}
						}
						so.ApplyModifiedProperties();
						EditorUtility.SetDirty(prefab);
					}
				}
				EditorGUI.showMixedValue = false;
			}
		}

		/*──────────── Helpers (unchanged) ─────────────────────────*/
		private void DisplayNonEditableField(string label, string value)
		{
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.TextField(label, value);
			EditorGUI.EndDisabledGroup();
		}
		private bool ShouldShowClearInstanceUniqueIDControls()
		{
			if (Application.isPlaying) return false;

			bool anyHasId = false;
			foreach (var obj in targets)
			{
				if (obj is not SaveablePrefab prefab) continue;
				if (!PrefabUtility.IsPartOfPrefabAsset(prefab)) return false;
				if (!string.IsNullOrEmpty(prefab.UniqueID)) anyHasId = true;
			}

			return anyHasId;
		}
		private void ClearInstanceUniqueIDs()
		{
			bool anyCleared = false;
			foreach (var obj in targets)
			{
				if (obj is not SaveablePrefab prefab) continue;
				if (!PrefabUtility.IsPartOfPrefabAsset(prefab)) continue;

				using (var so = new SerializedObject(prefab))
				{
					SerializedProperty prop = so.FindProperty("uniqueID");
					if (prop == null || string.IsNullOrEmpty(prop.stringValue))
						continue;

					Undo.RecordObject(prefab, "Clear Instance Unique ID");
					prop.stringValue = string.Empty;
					so.ApplyModifiedProperties();
					EditorUtility.SetDirty(prefab);
					anyCleared = true;
				}
			}

			if (anyCleared)
			{
				RefreshIdentitySerializedProperties();
			}
		}

		private void RefreshIdentitySerializedProperties()
		{
			if (serializedObject == null) return;
			serializedObject.Update();
			uniqueIDProperty = serializedObject.FindProperty("uniqueID");
			prefabAssetIDProperty = serializedObject.FindProperty("prefabAssetID");
		}
		private bool AreAllPropertyValuesEqual(SerializedProperty property)
		{
			string first = property.stringValue;
			return targets.All(o =>
			{
				SerializedObject so = new(o);
				SerializedProperty sp = so.FindProperty(property.propertyPath);
				return sp != null && sp.stringValue == first;
			});
		}

		#region Prefab-registry and warning logic
		/// <summary>
		/// Assigns new PrefabAssetIDs and registers the prefabs in the PrefabRegistry.
		/// This method is called via delayCall to ensure it runs after the current GUI event.
		/// </summary>
		private void AssignPrefabAssetIDAndRegister()
		{
			EditorApplication.delayCall -= AssignPrefabAssetIDAndRegister;
			pendingAssignPrefabAssetIDAndRegister = null;
			if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
				return;

			foreach (var obj in targets)
			{
				if (obj is not SaveablePrefab prefab) continue;
				GameObject asset = GetCachedPrefabAsset(prefab);
				if (asset == null) continue;

				string previousId = prefab.PrefabAssetID;
				string ensuredId = SaveablePrefab.EnsurePrefabAssetIdentityForEditor(prefab, asset);
				if (string.IsNullOrEmpty(ensuredId)) continue;

				if (!string.Equals(previousId, ensuredId, StringComparison.Ordinal))
				{
					Logger.Log(
						$"Assigned PrefabAssetID '{ensuredId}' to '{prefab.name}'.",
						LogLevel.Info);
				}
			}
			}


		/// <summary>
		/// Makes sure the PrefabRegistry entry that represents <paramref name="asset"/>
		/// carries <paramref name="id"/> without transferring an existing ID from another asset.
		/// </summary>
		private PrefabRegistrySyncResult SyncRegistryEntry(GameObject asset, string id, SaveablePrefab sourcePrefab = null)
		{
			if (Application.isPlaying || asset == null || prefabRegistry == null || string.IsNullOrEmpty(id))
			{
				return new PrefabRegistrySyncResult(
					PrefabRegistrySyncStatus.SkippedInvalidAsset,
					id,
					"PrefabRegistry synchronization was skipped because its editor context was no longer valid.",
					false);
			}

			PrefabRegistrySyncResult result = PrefabRegistrySyncUtility.Synchronize(
				prefabRegistry,
				sourcePrefab,
				asset,
				id);

			if (result.Changed)
			{
				PrefabRegistryIdentityIndex.NotifyRegistryMutated(prefabRegistry);
				PrefabRegistryIdentityIndex.NotifyPrefabIdentityChanged(asset);
				PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(prefabRegistry);
				prefabIdentityIndex = null;
			}

			if (!result.Succeeded)
			{
				Logger.Log($"SaveablePrefabEditor: {result.Message}", LogLevel.Warning);
			}
			else if (result.Status == PrefabRegistrySyncStatus.ExistingOwnerPreserved ||
			         result.Status == PrefabRegistrySyncStatus.RegistryIdAdopted)
			{
				Logger.Log($"SaveablePrefabEditor: {result.Message}", LogLevel.Info);
			}

			return result;
		}


		/// <summary>
		/// Registers the prefab into the PrefabRegistry.
		/// </summary>
		/// <param name="prefab">The SaveablePrefab instance.</param>
		/*───────────────────────────────────────────────────────────────────────────
		 * SaveablePrefabEditor.cs  ▸  RegisterPrefab()
		 *─────────────────────────────────────────────────────────────────────────*/
		private void RegisterPrefab(SaveablePrefab prefab)
		{
			if (Application.isPlaying) return;

			if (prefab == null || prefabRegistry == null) return;

				// Always register the *asset* (never the scene/preview instance)
					GameObject asset = GetCachedPrefabAsset(prefab);
					if (asset == null) return;

					string prefabAssetId = PrefabAssetResolver.EnsurePrefabAssetIDForAsset(prefab, asset, prefabRegistry);
					if (string.IsNullOrEmpty(prefabAssetId))
					{
						LogPrefabRegistryConflictIfNeeded(asset);
						return;
					}

					PrefabRegistrySyncResult syncResult = SyncRegistryEntry(asset, prefabAssetId, prefab);

					if (syncResult.Succeeded)
					{
						Logger.Log($"SaveablePrefabEditor: Prefab '{asset.name}' registered / synced " +
						           $"with ID '{syncResult.AppliedId}'.", LogLevel.Info);
					}
			}

		private void LogPrefabRegistryConflictIfNeeded(GameObject asset)
		{
			if (asset == null || prefabRegistry == null)
				return;

			if (!PrefabAssetResolver.HasConflictingRegistryEntriesForAsset(prefabRegistry, asset))
				return;

			LogPrefabRegistryConflict(asset);
		}

		private static void LogPrefabRegistryConflict(GameObject asset)
		{
			string assetName = asset != null ? asset.name : "Unknown Prefab";
			Logger.Log(
				$"SaveablePrefabEditor: Skipped PrefabRegistry sync for '{assetName}' because the registry contains conflicting duplicate rows for the same prefab asset. Use Clean Duplicates before registering or syncing this prefab.",
				LogLevel.Warning);
		}

		/// <summary>
		/// Deregisters the prefab from the PrefabRegistry.
		/// </summary>
		/// <param name="prefab">The SaveablePrefab instance.</param>
		private void DeregisterPrefab(SaveablePrefab prefab)
		{
			if (Application.isPlaying || prefab == null) return;

			GameObject asset = GetCachedPrefabAsset(prefab);   // ← works in Prefab-Mode
			if (asset == null) return;                              // scene object → ignore

			if (string.IsNullOrEmpty(prefab.PrefabAssetID))
			{
				Logger.Log($"SaveablePrefabEditor: Cannot deregister prefab '{asset.name}' without a PrefabAssetID.", LogLevel.Warning);
				return;
			}

			PrefabRegistry.PrefabEntry entryToRemove = prefabRegistry.prefabEntries
				.FirstOrDefault(e => e.prefab == asset);

			if (entryToRemove != null)
			{
				Undo.RecordObject(prefabRegistry, "Deregister SaveablePrefab");
				bool removed = prefabRegistry.prefabEntries.Remove(entryToRemove);

					if (removed)
					{
						prefabRegistry.RebuildLookup();
						EditorUtility.SetDirty(prefabRegistry);
						AssetDatabase.SaveAssets();
						Logger.Log($"SaveablePrefabEditor: Deregistered prefab '{asset.name}' from PrefabRegistry.", LogLevel.Info);
				}
				else
				{
					Logger.Log($"SaveablePrefabEditor: Failed to remove prefab '{asset.name}' from PrefabRegistry despite finding an entry.", LogLevel.Warning);
				}
			}
			else
			{
				Logger.Log($"SaveablePrefabEditor: Prefab '{asset.name}' not found in PrefabRegistry.", LogLevel.Warning);
			}
		}


		// ───────────────────────────────────────────────────────────
			//  Returns the nearest persistent prefab asset for any instance:
		//
		//  • Project-window asset        → itself
		//  • Prefab-Stage root/children  → loaded from its .prefab path
			//  • Scene instance              → nearest prefab asset
		//
			//  null means “don’t touch the registry for this object”.
		// ───────────────────────────────────────────────────────────
			private GameObject GetCachedPrefabAsset(SaveablePrefab prefab)
			{
				if (prefab == null)
					return null;
				if (cachedPrefabAssets.TryGetValue(prefab, out GameObject cachedAsset))
					return cachedAsset;

				GameObject resolvedAsset = GetPrefabAsset(prefab.gameObject);
				cachedPrefabAssets[prefab] = resolvedAsset;
				return resolvedAsset;
			}

				private static GameObject GetPrefabAsset(GameObject instance)
				{
					return PrefabAssetResolver.ResolveNearestPrefabAsset(instance);
				}

			private static bool IsInPrefabMode(GameObject gameObject)
			{
				if (gameObject == null) return false;

				var prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
				if (prefabStage != null) return true;

				prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
				return prefabStage != null && gameObject.scene == prefabStage.scene;
			}

			private void DeregisterIfComponentMissing(GameObject[] prefabs)
		{
			if (!CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation) return;
			LoadOrCreatePrefabRegistry();
			bool registryChanged = false;

			foreach (var go in prefabs)
			{
				if (go == null) continue;                     // prefab deleted?

				if (go.GetComponent<SaveablePrefab>() != null) continue; // component still there

				GameObject asset = GetPrefabAsset(go);        // ← maps Prefab-Mode copy → asset
				if (asset == null) continue;                  // scene object

				int idx = prefabRegistry.prefabEntries.FindIndex(e => e.prefab == asset);
				if (idx < 0) continue;

				PrefabRegistry.PrefabEntry entryToRemove = prefabRegistry.prefabEntries[idx];
				Undo.RecordObject(prefabRegistry, "Deregister SaveablePrefab");
				bool removed = prefabRegistry.prefabEntries.Remove(entryToRemove);
				if (removed)
				{
					registryChanged = true;
					Logger.Log($"SaveablePrefabEditor: Deregistered prefab '{asset.name}' after its SaveablePrefab component was removed.",
						   LogLevel.Info);
				}
				else
				{
					Logger.Log($"SaveablePrefabEditor: Failed to remove prefab '{asset.name}' from PrefabRegistry after its SaveablePrefab component was removed.",
						   LogLevel.Warning);
				}
			}

			if (registryChanged)
			{
				prefabRegistry.RebuildLookup();
				EditorUtility.SetDirty(prefabRegistry);
				AssetDatabase.SaveAssets();
			}
		}

		/// <summary>
		/// Displays duplicate Instance Unique ID warnings from the shared delayed hierarchy snapshot.
		/// </summary>
		private void CheckForDuplicateInstanceUniqueIDs()
		{
			if (!sceneIdentitySnapshotReady)
			{
				QueueSceneIdentityScan();
				return;
			}

			foreach (var obj in targets)
			{
				SaveablePrefab prefab = obj as SaveablePrefab;
				if (prefab != null)
				{
					string currentUniqueID = prefab.UniqueID;
					if (!string.IsNullOrEmpty(currentUniqueID))
					{
						sceneUniqueIdCounts.TryGetValue(currentUniqueID, out int duplicateCount);
						if (duplicateCount > 1)
						{
							EditorGUILayout.HelpBox(
								$"Warning: The Instance Unique ID '{currentUniqueID}' is duplicated in the scene. " +
								$"This may cause issues with the save system. Ensure all Instance Unique IDs are unique.",
								MessageType.Warning
							);

							string warningKey = $"uniqueID-{currentUniqueID}";
							if (ShouldLogWarning(warningKey))
							{
								Debug.LogWarning($"Duplicate Instance Unique ID detected: '{currentUniqueID}' is used by multiple SaveablePrefab components in the scene.");
								UpdateLastLoggedTime(warningKey);
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Determines if a warning should be logged based on the last logged time.
		/// </summary>
		/// <param name="warningKey">Unique key for the warning.</param>
		/// <returns>True if the warning should be logged.</returns>
		private bool ShouldLogWarning(string warningKey)
		{
			if (!lastLoggedWarnings.TryGetValue(warningKey, out var lastLoggedTime))
			{
				return true;
			}

			return (DateTime.Now - lastLoggedTime).TotalSeconds >= WarningIntervalSeconds;
		}

		/// <summary>
		/// Updates the last logged time for a specific warning key.
		/// </summary>
		/// <param name="warningKey">Unique key for the warning.</param>
		private void UpdateLastLoggedTime(string warningKey)
		{
			lastLoggedWarnings[warningKey] = DateTime.Now;
		}

		[InitializeOnLoadMethod]
		private static void RuntimeInit()
		{
	#if UNITY_EDITOR
				// Re-subscribe defensively (remove first to avoid duplicate handlers on domain reloads)
				EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#pragma warning disable UDR0001 // Editor-only static callbacks are unregister-before-register and removed before domain unload/reload.
				EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
				EditorApplication.hierarchyChanged     -= ValidatePrefabRegistry;
				EditorApplication.hierarchyChanged     += ValidatePrefabRegistry;
				Undo.undoRedoPerformed                 -= OnGlobalUndoRedo;
				Undo.undoRedoPerformed                 += OnGlobalUndoRedo;
				AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeDomainReload;
				AssemblyReloadEvents.beforeAssemblyReload += OnBeforeDomainReload;
				AppDomain.CurrentDomain.DomainUnload      -= OnDomainUnload;
				AppDomain.CurrentDomain.DomainUnload      += OnDomainUnload;
#pragma warning restore UDR0001
	#endif
		}

		/// <summary>
		/// Invalidates the shared scene identity snapshot and coalesces hierarchy validation.
		/// Registry cleanup/normalization is delegated to the registry validation coordinator.
		/// </summary>
		private static void ValidatePrefabRegistry()
		{
			if (Application.isPlaying) return;
			ClearLoggedWarnings();
			sceneIdentitySnapshotReady = false;
			QueueSceneIdentityScan();
		}

		private static void OnGlobalUndoRedo()
		{
			sceneIdentitySnapshotReady = false;
			QueueSceneIdentityScan();
		}

		/// <summary>
		/// Coalesces hierarchy scans so an event burst resolves the loaded SaveablePrefabs once.
		/// </summary>
		private static void QueueSceneIdentityScan()
		{
			if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
				return;
			if (sceneIdentityScanQueued)
				return;

			sceneIdentityScanQueued = true;
			EditorApplication.delayCall -= FlushSceneIdentityScan;
#pragma warning disable UDR0001 // Removed on flush and during play-mode/domain-reload cleanup.
			EditorApplication.delayCall += FlushSceneIdentityScan;
#pragma warning restore UDR0001
		}

		private static void FlushSceneIdentityScan()
		{
			EditorApplication.delayCall -= FlushSceneIdentityScan;
			sceneIdentityScanQueued = false;
			if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
				return;

#pragma warning disable CS0618 // Suppress FindObjectsByType deprecation warning for cross-version compatibility
#if UNITY_2023_1_OR_NEWER
			SaveablePrefab[] allSaveablePrefabs = UnityEngine.Object.FindObjectsByType<SaveablePrefab>(
				FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
			SaveablePrefab[] allSaveablePrefabs = Resources.FindObjectsOfTypeAll<SaveablePrefab>();
#endif
#pragma warning restore CS0618

			var groups = new Dictionary<string, List<SaveablePrefab>>(StringComparer.Ordinal);
			foreach (SaveablePrefab prefab in allSaveablePrefabs)
			{
				if (prefab == null ||
				    EditorUtility.IsPersistent(prefab) ||
				    !prefab.gameObject.scene.IsValid() ||
				    string.IsNullOrEmpty(prefab.UniqueID))
					continue;

				if (!groups.TryGetValue(prefab.UniqueID, out List<SaveablePrefab> group))
				{
					group = new List<SaveablePrefab>();
					groups.Add(prefab.UniqueID, group);
				}

				group.Add(prefab);
			}

			sceneUniqueIdCounts.Clear();
			bool autoFix = CrystalSaveEditorAutomationSettings.EnablePrefabRegistryAutoValidation;
			int undoGroup = -1;
			foreach (KeyValuePair<string, List<SaveablePrefab>> pair in groups)
			{
				List<SaveablePrefab> duplicates = pair.Value;
				sceneUniqueIdCounts[pair.Key] = duplicates.Count;
				if (!autoFix || duplicates.Count <= 1)
					continue;

				if (undoGroup < 0)
				{
					Undo.IncrementCurrentGroup();
					undoGroup = Undo.GetCurrentGroup();
					Undo.SetCurrentGroupName("Validate Saveable Prefab Instance IDs");
				}

				for (int i = 1; i < duplicates.Count; i++)
				{
					SaveablePrefab duplicate = duplicates[i];
					if (duplicate == null) continue;

					Undo.RecordObject(duplicate, "Clear Duplicate Instance Unique ID");
					var serialized = new SerializedObject(duplicate);
					SerializedProperty uniqueId = serialized.FindProperty("uniqueID");
					if (uniqueId == null) continue;

					uniqueId.stringValue = string.Empty;
					serialized.ApplyModifiedProperties();
					EditorUtility.SetDirty(duplicate);
					Logger.Log(
						$"SaveablePrefabEditor: Auto-cleared duplicate UniqueID '{pair.Key}' from '{duplicate.name}' (likely from object duplication).",
						LogLevel.Info);
				}

				// The delayed pass just removed every duplicate after the first.
				sceneUniqueIdCounts[pair.Key] = 1;
			}

			if (undoGroup >= 0)
				Undo.CollapseUndoOperations(undoGroup);

			sceneIdentitySnapshotReady = true;
		}

		private static void OnBeforeDomainReload()
		{
#if UNITY_EDITOR
			CancelSceneIdentityScan();
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.hierarchyChanged -= ValidatePrefabRegistry;
			Undo.undoRedoPerformed -= OnGlobalUndoRedo;
			AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeDomainReload;
			AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
#endif
		}

		private static void OnDomainUnload(object sender, EventArgs e)
		{
#if UNITY_EDITOR
			CancelSceneIdentityScan();
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.hierarchyChanged -= ValidatePrefabRegistry;
			Undo.undoRedoPerformed -= OnGlobalUndoRedo;
#endif
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingEditMode ||
			    state == PlayModeStateChange.EnteredEditMode ||
			    state == PlayModeStateChange.ExitingPlayMode)
			{
				CancelSceneIdentityScan();
				ClearLoggedWarnings();
				showAdvancedSettingsFoldout = false;
			}
		}

		private static void CancelSceneIdentityScan()
		{
			EditorApplication.delayCall -= FlushSceneIdentityScan;
			sceneIdentityScanQueued = false;
			sceneIdentitySnapshotReady = false;
			sceneUniqueIdCounts.Clear();
		}

		/// <summary>
		/// Clears all logged warnings. Called when the hierarchy changes.
		/// </summary>
		private static void ClearLoggedWarnings()
		{
			lastLoggedWarnings.Clear();
		}

		// Dictionary to keep track of logged warnings to prevent spamming.
#pragma warning disable UDR0001 // Editor-only warning throttle cleared on assembly reload and play-mode transitions.
		private static readonly Dictionary<string, DateTime> lastLoggedWarnings = new Dictionary<string, DateTime>();
#pragma warning restore UDR0001
		private const int WarningIntervalSeconds = 10;

		/// <summary>
		/// Detects if the SaveablePrefab component is being removed.
		/// </summary>
		/*
		private void DetectComponentRemoval()
		{
			isBeingRemoved = false;
			foreach (var obj in targets)
			{
				SaveablePrefab prefab = obj as SaveablePrefab;
				if (prefab == null)
				{
					// Component is being removed
					isBeingRemoved = true;
					break;
				}
			}
		}
		*/

		/// <summary>
		/// Deregisters the prefabs from the PrefabRegistry.
		/// This method is scheduled via delayCall to ensure it runs safely after the component is removed.
		/// </summary>
		private void DeregisterPrefabs()
		{
			if (Application.isPlaying)
				{
					Logger.Log("SaveablePrefabEditor: Attempting to deregister prefabs during Playmode. Deregistration postponed.", LogLevel.Info);
					if (pendingRetryDeregisterPrefabs != null)
						EditorApplication.delayCall -= pendingRetryDeregisterPrefabs;
					pendingRetryDeregisterPrefabs = () =>
					{
						if (!Application.isPlaying) DeregisterPrefabs(); // Try again when not in Playmode
					};
#pragma warning disable UDR0004 // Canceled by CancelPendingDelayCalls from OnDisable/OnDestroy.
					EditorApplication.delayCall += pendingRetryDeregisterPrefabs;
#pragma warning restore UDR0004
					return;
			}

			// Ensure this method runs only once
			EditorApplication.delayCall -= DeregisterPrefabs;

			if (cachedGameObjects == null || cachedGameObjects.Count == 0)
			{
				Logger.Log("SaveablePrefabEditor: No cached GameObjects found. Cannot deregister prefabs.", LogLevel.Off);
				return;
			}

			// Ensure PrefabRegistry is loaded
			if (prefabRegistry == null)
			{
				Debug.LogError("SaveablePrefabEditor: PrefabRegistry is not loaded. Cannot deregister prefabs.");
				return;
			}

			//if (!isBeingRemoved) return;

			foreach (var gameObject in cachedGameObjects)
			{
				if (gameObject == null) continue;

				// Check if the GameObject is part of a prefab asset
				if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
				{
					// Not a prefab asset; do not deregister
					continue;
				}

				// Find and remove the prefab entry
				PrefabRegistry.PrefabEntry entryToRemove = prefabRegistry.prefabEntries
					.FirstOrDefault(entry => entry.prefab == gameObject);

				if (entryToRemove != null)
				{
					Undo.RecordObject(prefabRegistry, "Deregister SaveablePrefab");
					bool removed = prefabRegistry.prefabEntries.Remove(entryToRemove);

						if (removed)
						{
							prefabRegistry.RebuildLookup();
							EditorUtility.SetDirty(prefabRegistry);
							Logger.Log($"SaveablePrefabEditor: Deregistered prefab '{gameObject.name}' from PrefabRegistry.", LogLevel.Info);
						}
					else
					{
						Logger.Log($"SaveablePrefabEditor: Failed to remove prefab '{gameObject.name}' from PrefabRegistry despite finding an entry.", LogLevel.Warning);
					}
				}
				else
				{
					Logger.Log($"SaveablePrefabEditor: Prefab '{gameObject.name}' not found in PrefabRegistry.", LogLevel.Warning);
				}
			}

			AssetDatabase.SaveAssets();
		}

		/// <summary>
		/// Handles Undo and Redo actions to maintain PrefabRegistry consistency.
		/// </summary>
		private void OnUndoRedoPerformed()
		{
			if (prefabRegistry == null)
			{
				prefabRegistry = PrefabAssetResolver.LoadActivePrefabRegistry();
				if (prefabRegistry == null)
				{
					Debug.LogError("SaveablePrefabEditor: PrefabRegistry not found.");
					return;
				}
			}

			PrefabRegistryIdentityIndex.Invalidate(prefabRegistry);
			PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(prefabRegistry);
			prefabIdentityIndex = null;
			QueueSceneIdentityScan();
			// Refresh the inspector from the restored transaction. Queuing another
			// repair here would immediately redo an identity change the user undid.
			if (pendingRegistrySync != null)
			{
				EditorApplication.delayCall -= pendingRegistrySync;
				pendingRegistrySync = null;
			}
			RefreshIdentitySerializedProperties();
			Repaint();
		}

		/// <summary>
		/// Loads the PrefabRegistry asset or creates one if it doesn't exist.
		/// </summary>
		private void LoadOrCreatePrefabRegistry()
		{
			string prefabRegistryPath = "Assets/Plugins/CrystalSave/Resources/PrefabRegistry.asset";
			prefabRegistry = PrefabAssetResolver.LoadActivePrefabRegistry();
			if (prefabRegistry == null)
			{
				Debug.LogWarning($"SaveablePrefabEditor: PrefabRegistry not found at '{prefabRegistryPath}'. Creating a new one.");
				prefabRegistry = CreateInstance<PrefabRegistry>();
				EnsureDirectoryExists(Path.GetDirectoryName(prefabRegistryPath));
				AssetDatabase.CreateAsset(prefabRegistry, prefabRegistryPath);
				AssetDatabase.SaveAssets();
				EditorUtility.FocusProjectWindow();
				Selection.activeObject = prefabRegistry;
				Logger.Log($"SaveablePrefabEditor: Created new PrefabRegistry at '{prefabRegistryPath}'.", LogLevel.Info);
			}
		}

		/// <summary>
		/// Ensures that a directory exists; if not, it creates the necessary folders.
		/// </summary>
		/// <param name="path">The directory path.</param>
		private void EnsureDirectoryExists(string path)
		{
			if (string.IsNullOrEmpty(path))
				return;

			if (!AssetDatabase.IsValidFolder(path))
			{
				string[] folders = path.Split('/');
				string currentPath = "";
				foreach (string folder in folders)
				{
					currentPath = string.IsNullOrEmpty(currentPath) ? folder : $"{currentPath}/{folder}";
					if (!AssetDatabase.IsValidFolder(currentPath))
					{
						string parentFolder = Path.GetDirectoryName(currentPath);
						string newFolderName = Path.GetFileName(currentPath);
						if (string.IsNullOrEmpty(parentFolder))
						{
							// Handle root folders
							AssetDatabase.CreateFolder("Assets", newFolderName);
							currentPath = $"Assets/{newFolderName}";
						}
						else
						{
							AssetDatabase.CreateFolder(parentFolder, newFolderName);
						}
					}
				}
			}
		}

		/// <summary>
		/// Gets all scene names from the Build Settings.
		/// </summary>
		/// <returns>List of scene names.</returns>
		private List<string> GetAllSceneNames()
		{
			List<string> sceneNames = new List<string>();
			int sceneCount = EditorBuildSettings.scenes.Length;

			for (int i = 0; i < sceneCount; i++)
			{
				string scenePath = EditorBuildSettings.scenes[i].path;
				string sceneName = Path.GetFileNameWithoutExtension(scenePath);
				sceneNames.Add(sceneName);
			}

			return sceneNames;
		}
		/// <summary>
		/// Ensures every selected prefab that wants registration is present in the PrefabRegistry.
		/// </summary>
		private void EnsureRegistrationForPrefabs()
		{
			if (!CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs) return;
			if (Application.isPlaying || prefabRegistry == null) return;

			foreach (var obj in targets)
			{
				var sp = obj as SaveablePrefab;
				if (sp == null) continue;

				GameObject asset = GetCachedPrefabAsset(sp);
				if (asset == null) continue;
				if (!sp.RegisterWithSaveSystem) continue;                 // user opted-out
				if (string.IsNullOrEmpty(sp.PrefabAssetID)) continue;     // will be assigned later

				SaveablePrefab.EnsurePrefabAssetIdentityForEditor(sp, asset);
			}
		}

		/// <summary>
		/// Synchronizes the DisablePooling settings from PrefabRegistry to SaveablePrefab components.
		/// This is called only once per editor session when the component is enabled.
		/// </summary>
		private void SyncPoolingSettingsFromRegistry()
		{
			if (Application.isPlaying || prefabRegistry == null) return;

			bool anyChanges = false;

                        foreach (var obj in targets)
                        {
                                var sp = obj as SaveablePrefab;
                                if (sp == null || string.IsNullOrEmpty(sp.PrefabAssetID)) continue;

                                var entry = prefabRegistry.FindEntryByID(sp.PrefabAssetID);
                                if (entry == null) continue;

                                bool registryDisablesPooling = entry.disablePooling;

                                if (sp.DisablePooling != registryDisablesPooling)
                                {
                                        Undo.RecordObject(sp, "Sync Disable Pooling from Registry");
                                        sp.DisablePooling = registryDisablesPooling;
                                        EditorUtility.SetDirty(sp);
                                        anyChanges = true;
                                        Logger.Log($"SaveablePrefabEditor: Synced DisablePooling={registryDisablesPooling} from PrefabRegistry for '{sp.name}'.", LogLevel.Info);
                                }
                        }

			if (anyChanges)
			{
				serializedObject.Update();
				AssetDatabase.SaveAssets();
			}
		}

		/// <summary>
		/// Syncs the SaveablePrefab component's DisablePooling setting to the PrefabRegistry.
		/// This ensures both locations stay in sync when the component setting is changed.
		/// </summary>
		private void SyncComponentSettingToPrefabRegistry(SaveablePrefab saveablePrefab)
		{
			if (prefabRegistry == null || saveablePrefab == null || string.IsNullOrEmpty(saveablePrefab.PrefabAssetID))
				return;

			// Find the registry entry for this prefab
			var entry = prefabRegistry.prefabEntries.FirstOrDefault(e => e.uniqueID == saveablePrefab.PrefabAssetID);
			if (entry != null)
			{
				// Update registry to match component setting
				if (entry.disablePooling != saveablePrefab.DisablePooling)
				{
					Undo.RecordObject(prefabRegistry, "Sync Disable Pooling to Registry");
					entry.disablePooling = saveablePrefab.DisablePooling;
					EditorUtility.SetDirty(prefabRegistry);
					Logger.Log($"SaveablePrefabEditor: Synced DisablePooling={saveablePrefab.DisablePooling} to PrefabRegistry for '{saveablePrefab.name}'.", LogLevel.Info);
				}
			}
			else
			{
				// Entry doesn't exist, create it if component disables pooling
				if (saveablePrefab.DisablePooling && CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs)
				{
					GameObject asset = GetCachedPrefabAsset(saveablePrefab);
					if (asset != null)
					{
						Undo.RecordObject(prefabRegistry, "Add Registry Entry");
						prefabRegistry.TryAddPrefab(saveablePrefab.PrefabAssetID, asset, out _);
						
						// Find the newly created entry and set disablePooling
						var newEntry = prefabRegistry.prefabEntries.FirstOrDefault(e => e.uniqueID == saveablePrefab.PrefabAssetID);
						if (newEntry != null)
						{
							newEntry.disablePooling = true;
							EditorUtility.SetDirty(prefabRegistry);
							Logger.Log($"SaveablePrefabEditor: Created PrefabRegistry entry with DisablePooling=true for '{saveablePrefab.name}'.", LogLevel.Info);
						}
					}
				}
			}
		}

		/// <summary>
		/// Safely draws a PropertyField with error handling for disposed SerializedObjects.
		/// </summary>
		private void SafeDrawPropertyField(SerializedProperty property, string label)
		{
			if (property == null) return;
			
			try
			{
				EditorGUILayout.PropertyField(property, new GUIContent(label));
			}
			catch (System.Exception ex) when (ex.Message.Contains("SerializedObject") || ex.Message.Contains("Disposed"))
			{
				// SerializedObject is disposed, skip drawing this property
				EditorGUILayout.LabelField(label, "Loading...");
			}
		}

		/// <summary>
		/// Safely draws a PropertyField with tooltip and error handling for disposed SerializedObjects.
		/// </summary>
		private void SafeDrawPropertyFieldWithTooltip(SerializedProperty property, string label, string tooltip)
		{
			if (property == null) return;
			
			try
			{
				EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
			}
			catch (System.Exception ex) when (ex.Message.Contains("SerializedObject") || ex.Message.Contains("Disposed"))
			{
				// SerializedObject is disposed, skip drawing this property
				EditorGUILayout.LabelField(label, "Loading...");
			}
		}

		/// <summary>
		/// Safely accesses a SerializedProperty's boolean value with error handling.
		/// </summary>
		private bool SafeGetBoolValue(SerializedProperty property, bool defaultValue = false)
		{
			if (property == null) return defaultValue;
			
			try
			{
				return property.boolValue;
			}
			catch (System.Exception ex) when (ex.Message.Contains("SerializedObject") || ex.Message.Contains("Disposed"))
			{
				return defaultValue;
			}
		}

		/// <summary>
		/// Safely checks if a SerializedProperty has multiple different values.
		/// </summary>
		private bool SafeHasMultipleDifferentValues(SerializedProperty property)
		{
			if (property == null) return false;
			
			try
			{
				return property.hasMultipleDifferentValues;
			}
			catch (System.Exception ex) when (ex.Message.Contains("SerializedObject") || ex.Message.Contains("Disposed"))
			{
				return false;
			}
		}

		/// <summary>
		/// Checks for UniqueID component and displays warning with cleanup option.
		/// </summary>
		private void CheckForUniqueIDComponent()
		{
			bool hasUniqueIDComponent = false;
			int totalUniqueIDCount = 0;
			
			// Check if any target has a UniqueID component
			foreach (var obj in targets)
			{
				SaveablePrefab prefab = obj as SaveablePrefab;
				if (prefab != null && prefab.GetComponent<UniqueID>() != null)
				{
					hasUniqueIDComponent = true;
					totalUniqueIDCount++;
				}
			}

			if (hasUniqueIDComponent)
			{
				EditorGUILayout.Space(4);
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				
				EditorGUILayout.HelpBox(
					"⚠️ UniqueID Component Detected\n\n" +
					"This SaveablePrefab has a UniqueID component that was likely added automatically in a previous version. " +
					"SaveablePrefab no longer requires or automatically creates UniqueID components. " +
					"These components can cause confusion and are generally not needed.\n\n" +
					"Safe to remove unless you specifically need it for other systems (like RememberComposite).",
					MessageType.Warning
				);

				EditorGUILayout.Space(4);
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				
				GUI.backgroundColor = Color.red;
				string buttonText = totalUniqueIDCount > 1 
					? $"🗑️ Remove UniqueID Components ({totalUniqueIDCount})"
					: "🗑️ Remove UniqueID Component";
				if (GUILayout.Button(buttonText, GUILayout.Width(250), GUILayout.Height(25)))
				{
					string dialogTitle = totalUniqueIDCount > 1 ? "Remove UniqueID Components" : "Remove UniqueID Component";
					string dialogMessage = totalUniqueIDCount > 1 
						? $"Are you sure you want to remove {totalUniqueIDCount} UniqueID components?\n\nThis action can be undone with Ctrl+Z."
						: "Are you sure you want to remove the UniqueID component?\n\nThis action can be undone with Ctrl+Z.";
					
					if (EditorUtility.DisplayDialog(dialogTitle, dialogMessage, "Remove", "Cancel"))
					{
						RemoveUniqueIDComponents();
					}
				}
				GUI.backgroundColor = Color.white;
				
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();
				
				EditorGUILayout.EndVertical();
			}
		}

		/// <summary>
		/// Removes UniqueID components from all selected SaveablePrefab targets.
		/// </summary>
		private void RemoveUniqueIDComponents()
		{
			int removedCount = 0;
			
			foreach (var obj in targets)
			{
				SaveablePrefab prefab = obj as SaveablePrefab;
				if (prefab != null)
				{
					UniqueID uniqueIDComponent = prefab.GetComponent<UniqueID>();
					if (uniqueIDComponent != null)
					{
						Undo.DestroyObjectImmediate(uniqueIDComponent);
						removedCount++;
						Debug.Log($"[Crystal Save] Removed UniqueID component from '{prefab.name}'");
					}
				}
			}
			
			if (removedCount > 0)
			{
				string message = removedCount == 1 
					? "Removed UniqueID component from 1 SaveablePrefab." 
					: $"Removed UniqueID components from {removedCount} SaveablePrefabs.";
				
				Debug.Log($"[Crystal Save] {message}");
				
				// Set flag to indicate components were removed this frame
				componentsRemovedThisFrame = true;
				
				// Mark the objects as dirty to ensure changes are saved
				foreach (var obj in targets)
				{
					EditorUtility.SetDirty(obj as UnityEngine.Object);
				}
				
					// Defer the serialized object refresh to avoid disposed object errors
					QueueSerializedObjectRefresh();
				}
			}

		#endregion
	}

	/// <summary>
	/// Extension methods for SerializedProperty to handle list operations.
	/// </summary>
	[InitializeOnLoad]
	public static class SerializedPropertyExtensions
	{
		/// <summary>
		/// Checks if the SerializedProperty list contains a specific string.
		/// </summary>
		/// <param name="list">The SerializedProperty list.</param>
		/// <param name="value">The string value to check.</param>
		/// <returns>True if the list contains the value; otherwise, false.</returns>
		public static bool Contains(this SerializedProperty list, string value)
		{
			for (int i = 0; i < list.arraySize; i++)
			{
				if (list.GetArrayElementAtIndex(i).stringValue.Equals(value, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Adds a string to the SerializedProperty list.
		/// </summary>
		/// <param name="list">The SerializedProperty list.</param>
		/// <param name="value">The string value to add.</param>
		public static void AddToList(this SerializedProperty list, string value)
		{
			list.InsertArrayElementAtIndex(list.arraySize);
			list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = value;
		}

		/// <summary>
		/// Removes a string from the SerializedProperty list.
		/// </summary>
		/// <param name="list">The SerializedProperty list.</param>
		/// <param name="value">The string value to remove.</param>
		public static void RemoveFromList(this SerializedProperty list, string value)
		{
			for (int i = 0; i < list.arraySize; i++)
			{
				if (list.GetArrayElementAtIndex(i).stringValue.Equals(value, StringComparison.OrdinalIgnoreCase))
				{
					list.DeleteArrayElementAtIndex(i);
					break;
				}
			}
		}

	}
}
#endif
#endif
