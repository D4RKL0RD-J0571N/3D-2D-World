#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && REMEMBERME_EDITOR_COROUTINES_PRESENT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.Editor
{
	/// <summary>
	/// Runs a reversible, target-scoped performance and correctness diagnostic against
	/// the real PrefabRegistry editor UI paths.
	/// </summary>
	public sealed class PrefabRegistryEditorPerformanceDiagnosticBuilder : EditorWindow
	{
		private const string RootFolder = "Assets/CrystalSaveDiagnostics/PrefabRegistryEditorPerformance";
		private const string WorkingFolder = RootFolder + "/Working";
		private const string PrefabsFolder = WorkingFolder + "/Prefabs";
		private const string RegistryPath = WorkingFolder + "/PrefabRegistry.asset";
		private const string SettingsPath = WorkingFolder + "/SaveSettings.asset";
		private const string ReportPath = RootFolder + "/PrefabRegistryEditorPerformanceReport.md";
		private const int FixtureCount = 100;
		private const int RepaintCycles = 8;
		private const int MaxUiWaitTicks = 240;
		private const int MaxBatchWaitTicks = 120;

		private static readonly int[] FixtureSizes = { 4, 20, 100 };
		private static PendingRun pendingRun;

		private bool openReportAfterRun = true;
		private bool revealFolderAfterRun;
		private string lastStatus = "Idle";
		private string lastReportPath = ReportPath;

		[MenuItem("Tools/Crystal Save/Runtime Debug/Diagnostics/Prefab Registry Editor Performance Diagnostic")]
		public static void ShowWindow()
		{
			PrefabRegistryEditorPerformanceDiagnosticBuilder window =
				GetWindow<PrefabRegistryEditorPerformanceDiagnosticBuilder>("Prefab Registry Performance");
			window.minSize = new Vector2(620f, 330f);
			window.Show();
		}

		/// <summary>
		/// Entry point for a non-batch, virtual-display validation run. Unity exits when the
		/// report is complete so the live IMGUI windows receive real Layout/Repaint events.
		/// </summary>
		public static void RunFromCommandLine()
		{
			PrefabRegistryEditorPerformanceDiagnosticBuilder window =
				CreateInstance<PrefabRegistryEditorPerformanceDiagnosticBuilder>();
			window.openReportAfterRun = false;
			window.revealFolderAfterRun = false;
			window.StartDiagnostic(exitEditorWhenFinished: true);
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("Prefab Registry Editor Performance Diagnostic", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"Creates an isolated 100-prefab fixture, exercises 4/20/100-row registry sizes, " +
				"opens both real registry UI paths, and verifies deterministic index, validation, " +
				"batching, repaint, and save counts. Active Crystal Save assets are never modified.",
				MessageType.Info);

			using (new EditorGUI.DisabledScope(pendingRun != null))
			{
				openReportAfterRun = EditorGUILayout.ToggleLeft("Open report after run", openReportAfterRun);
				revealFolderAfterRun = EditorGUILayout.ToggleLeft("Reveal output folder after run", revealFolderAfterRun);
			}

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
			EditorGUILayout.SelectableLabel(
				ReportPath,
				EditorStyles.textField,
				GUILayout.Height(EditorGUIUtility.singleLineHeight));
			EditorGUILayout.LabelField("Status", lastStatus, EditorStyles.wordWrappedLabel);

			GUILayout.FlexibleSpace();
			if (pendingRun == null)
			{
				if (GUILayout.Button("Run Editor Performance Diagnostic", GUILayout.Height(34f)))
					StartDiagnostic(exitEditorWhenFinished: false);
			}
			else if (GUILayout.Button("Cancel and Restore", GUILayout.Height(30f)))
			{
				pendingRun.CancelRequested = true;
				lastStatus = "Cancellation requested; restoring temporary state...";
			}

			if (!string.IsNullOrEmpty(lastReportPath) && File.Exists(lastReportPath) &&
			    GUILayout.Button("Open Last Report"))
			{
				AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(lastReportPath));
			}
		}

		private void StartDiagnostic(bool exitEditorWhenFinished)
		{
			if (pendingRun != null)
			{
				FailStart("A Prefab Registry editor performance diagnostic is already running.", exitEditorWhenFinished);
				return;
			}

			if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode ||
			    EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				FailStart(
					"Run the diagnostic from a stable Edit Mode state while Unity is not compiling or importing.",
					exitEditorWhenFinished);
				return;
			}

			if (PrefabRegistryValidationCoordinator.PendingRegistryCount > 0 ||
			    SaveablePrefab.PendingPrefabRegistrySyncCountForEditorDiagnostics > 0 ||
			    SaveablePrefab.PendingUniqueIdRemovalCountForEditorDiagnostics > 0)
			{
				FailStart(
					"Crystal Save has pending editor validation work. Let the current editor tick finish, then run the diagnostic again; no pending work was cleared.",
					exitEditorWhenFinished);
				return;
			}

			var run = new PendingRun
			{
				Window = this,
				ExitEditorWhenFinished = exitEditorWhenFinished,
				OpenReportAfterRun = openReportAfterRun,
				RevealFolderAfterRun = revealFolderAfterRun,
				StartedAt = DateTime.Now,
				Phase = DiagnosticPhase.CreatingFixtures
			};

			try
			{
				EnsureFolder(RootFolder);
				DeleteWorkingFolder();
				EnsureFolder(WorkingFolder);
				EnsureFolder(PrefabsFolder);

				run.Settings = CreateInstance<SaveSettings>();
				run.Settings.name = "PrefabRegistryPerformanceDiagnosticSettings";
				run.Settings.editorAutomationSettingsInitialized = true;
				run.Settings.autoRegisterPrefabs = false;
				run.Settings.enablePrefabRegistryAutoValidation = false;
				AssetDatabase.CreateAsset(run.Settings, SettingsPath);

				run.Registry = CreateInstance<PrefabRegistry>();
				run.Registry.name = "PrefabRegistryPerformanceDiagnosticRegistry";
				AssetDatabase.CreateAsset(run.Registry, RegistryPath);
				AssetDatabase.SaveAssetIfDirty(run.Settings);
				AssetDatabase.SaveAssetIfDirty(run.Registry);

				run.OverrideScope = new ActiveAssetOverrideScope(run.Settings, run.Registry);
				run.OverrideRestored = false;
				if (!PrefabRegistryEditorDiagnosticCounters.TryBeginSession(run.Registry, WorkingFolder))
					throw new InvalidOperationException("Another PrefabRegistry diagnostic counter session is active.");
				run.CounterSessionStarted = true;

				pendingRun = run;
				RegisterLifecycleCallbacks();
				SetStatus(run, "Creating isolated prefab fixture 0/100...");
				QueueUpdate();
			}
			catch (Exception ex)
			{
				run.FatalError = ex.ToString();
				FinishRun(run, "Diagnostic setup failed.");
			}
		}

		private static void FailStart(string message, bool exitEditorWhenFinished)
		{
			Debug.LogError($"[Crystal Save][PrefabRegistry Performance] {message}");
			if (!Application.isBatchMode && !exitEditorWhenFinished)
				EditorUtility.DisplayDialog("Crystal Save Diagnostic", message, "OK");
			if (exitEditorWhenFinished)
				EditorApplication.delayCall += () => EditorApplication.Exit(1);
		}

		private static void QueueUpdate()
		{
			EditorApplication.update -= UpdateDiagnostic;
			EditorApplication.update += UpdateDiagnostic;
		}

		private static void UpdateDiagnostic()
		{
			PendingRun run = pendingRun;
			if (run == null || run.Finished)
				return;

			if (run.CancelRequested)
			{
				run.FatalError = "Cancelled by the user.";
				FinishRun(run, "Diagnostic cancelled.");
				return;
			}

			try
			{
				switch (run.Phase)
				{
					case DiagnosticPhase.CreatingFixtures:
						CreateNextFixture(run);
						break;
					case DiagnosticPhase.ConfigureSize:
						ConfigureCurrentSize(run);
						break;
					case DiagnosticPhase.ColdBuild:
						RunColdBuild(run);
						break;
					case DiagnosticPhase.ClosedWarmReads:
						RunClosedWarmRead(run);
						break;
					case DiagnosticPhase.OpenLiveUIs:
						OpenLiveUIs(run);
						break;
					case DiagnosticPhase.WarmLiveUIs:
						WarmLiveUIs(run);
						break;
					case DiagnosticPhase.ExerciseLiveUIs:
						ExerciseLiveUIs(run);
						break;
					case DiagnosticPhase.CoalescedValidation:
						RunCoalescedValidation(run);
						break;
					case DiagnosticPhase.MutatingValidation:
						RunMutatingValidation(run);
						break;
					case DiagnosticPhase.WaitForSaveFeedback:
						WaitForSaveFeedback(run);
						break;
					case DiagnosticPhase.DisabledAutomation:
						RunDisabledAutomation(run);
						break;
					case DiagnosticPhase.QueuePrefabBatch:
						QueuePrefabBatch(run);
						break;
					case DiagnosticPhase.WaitForPrefabBatch:
						WaitForPrefabBatch(run);
						break;
					case DiagnosticPhase.Complete:
						FinishRun(run, "Diagnostic complete.");
						break;
				}
			}
			catch (Exception ex)
			{
				run.FatalError = ex.ToString();
				FinishRun(run, $"Diagnostic failed during {run.Phase}.");
			}
		}

		private static void CreateNextFixture(PendingRun run)
		{
			if (run.Fixtures.Count >= FixtureCount)
			{
				AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
				run.SizeIndex = 0;
				run.Phase = DiagnosticPhase.ConfigureSize;
				SetStatus(run, "Fixture ready. Starting 4-row cold index build...");
				return;
			}

			int index = run.Fixtures.Count;
			string id = GetFixtureId(index);
			string path = $"{PrefabsFolder}/CSP_PerformancePrefab_{index:000}.prefab";
			GameObject temp = new GameObject($"CSP_PerformancePrefab_{index:000}");
			try
			{
				SaveablePrefab saveable = temp.AddComponent<SaveablePrefab>();
				SetSaveablePrefabIdentity(saveable, id, registerWithSaveSystem: true);
				GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
				if (asset == null)
					throw new InvalidOperationException($"Could not create diagnostic prefab '{path}'.");

				SaveablePrefab assetSaveable = asset.GetComponent<SaveablePrefab>();
				SetSaveablePrefabIdentity(assetSaveable, id, registerWithSaveSystem: true);
				EditorUtility.SetDirty(asset);
				AssetDatabase.SaveAssetIfDirty(asset);
				run.Fixtures.Add(asset);
			}
			finally
			{
				DestroyImmediate(temp);
			}

			SetStatus(run, $"Creating isolated prefab fixture {run.Fixtures.Count}/{FixtureCount}...");
			EditorUtility.DisplayProgressBar(
				"Crystal Save PrefabRegistry Diagnostic",
				$"Creating prefab {run.Fixtures.Count} of {FixtureCount}",
				run.Fixtures.Count / (float)FixtureCount);
		}

		private static void ConfigureCurrentSize(PendingRun run)
		{
			int count = FixtureSizes[run.SizeIndex];
			SetAutomation(run.Settings, autoRegister: false, autoValidation: false);
			PrefabRegistryValidationCoordinator.CancelPending(run.Registry);

			run.Registry.prefabEntries = run.Fixtures
				.Take(count)
				.Select((prefab, index) => new PrefabRegistry.PrefabEntry
				{
					uniqueID = GetFixtureId(index),
					prefab = prefab,
					poolSize = 0,
					disablePooling = false
				})
				.ToList();
			run.Registry.RebuildLookup();
			PrefabRegistryIdentityIndex.Invalidate(run.Registry);
			PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(run.Registry);
			EditorUtility.ClearDirty(run.Registry);

			run.CurrentSizeResult = new SizeResult { RowCount = count };
			run.SizeResults.Add(run.CurrentSizeResult);
			run.StageBaseline = PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry);
			run.Phase = DiagnosticPhase.ColdBuild;
			SetStatus(run, $"Measuring {count}-row cold index build...");
		}

		private static void RunColdBuild(PendingRun run)
		{
			int count = run.CurrentSizeResult.RowCount;
			Stopwatch stopwatch = Stopwatch.StartNew();
			PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.GetOrCreate(run.Registry);
			stopwatch.Stop();
			PrefabRegistryEditorDiagnosticSnapshot coldDelta = CaptureDelta(run);

			run.CurrentSizeResult.ColdBuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
			run.CurrentSizeResult.ColdDelta = coldDelta;
			AddCheck(
				run,
				$"{count} rows: cold index build",
				coldDelta.IndexBuilds == 1 &&
				coldDelta.AssetResolutions == count &&
				index.Metrics.RowCount == count &&
				index.Metrics.UniqueAssetCount == count &&
				index.Metrics.InvalidRowCount == 0,
				$"builds={coldDelta.IndexBuilds}, resolutions={coldDelta.AssetResolutions}, " +
				$"rows={index.Metrics.RowCount}, uniqueAssets={index.Metrics.UniqueAssetCount}, " +
				$"invalid={index.Metrics.InvalidRowCount}, elapsed={stopwatch.Elapsed.TotalMilliseconds:F3} ms");

			run.WarmSnapshot = PrefabRegistryIdentityUtility.GetInspectionSnapshot(run.Registry);
			if (run.WarmSnapshot.Rows.Count > 0)
				PrefabRegistryIdentityUtility.MatchesFilter(run.WarmSnapshot.Rows[0], "PerformancePrefab");

			run.StageBaseline = PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry);
			run.StageTick = 0;
			run.Phase = DiagnosticPhase.ClosedWarmReads;
			SetStatus(run, $"Verifying {count}-row warm reads with registry UIs closed...");
		}

		private static void RunClosedWarmRead(PendingRun run)
		{
			PrefabRegistryIdentityUtility.InspectionSnapshot snapshot =
				PrefabRegistryIdentityUtility.GetInspectionSnapshot(run.Registry);
			if (snapshot.Rows.Count > 0)
				PrefabRegistryIdentityUtility.MatchesFilter(snapshot.Rows[run.StageTick % snapshot.Rows.Count], "CSP_Performance");

			run.StageTick++;
			if (run.StageTick < RepaintCycles)
				return;

			PrefabRegistryEditorDiagnosticSnapshot delta = CaptureDelta(run);
			run.CurrentSizeResult.ClosedWarmDelta = delta;
			AddCheck(
				run,
				$"{run.CurrentSizeResult.RowCount} rows: warm closed-UI reads",
				ReferenceEquals(run.WarmSnapshot, snapshot) && HasNoIdentityOrSaveWork(delta),
				DescribeWork(delta));

			run.Phase = DiagnosticPhase.OpenLiveUIs;
			SetStatus(run, $"Opening live registry UIs for {run.CurrentSizeResult.RowCount} rows...");
		}

		private static void OpenLiveUIs(PendingRun run)
		{
			CloseLiveUIs(run);

			run.InspectorHost = CreateInstance<PrefabRegistryDiagnosticInspectorHost>();
			run.InspectorHost.titleContent = new GUIContent("Registry Diagnostic Inspector");
			run.InspectorHost.position = new Rect(60f, 80f, 560f, 480f);
			run.InspectorHost.Initialize(run.Registry);
			run.InspectorHost.ShowUtility();

			run.SettingsWindow = Resources.FindObjectsOfTypeAll<RememberMeSettingsWindow>().FirstOrDefault();
			run.SettingsWindowCreated = run.SettingsWindow == null;
			if (run.SettingsWindow == null)
			{
				run.SettingsWindow = RememberMeSettingsWindow.CreatePrefabRegistryPerformanceDiagnosticWindow(
					run.Registry,
					run.Settings);
				run.SettingsWindow.position = new Rect(640f, 80f, 760f, 700f);
				run.SettingsWindow.Show();
			}
			else
			{
				run.SettingsWindowState =
					run.SettingsWindow.BeginPrefabRegistryPerformanceDiagnostic(run.Registry, run.Settings);
			}
			run.SettingsWindow.SetPrefabRegistryPerformanceDiagnosticFoldout(true);
			run.SettingsWindow.SetPrefabRegistryPerformanceDiagnosticFilter(string.Empty);

			run.StageBaseline = PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry);
			run.StageTick = 0;
			run.Phase = DiagnosticPhase.WarmLiveUIs;
			RepaintLiveUIs(run);
		}

		private static void WarmLiveUIs(PendingRun run)
		{
			run.StageTick++;
			RepaintLiveUIs(run);
			PrefabRegistryEditorDiagnosticSnapshot delta = CaptureDelta(run);
			bool inspectorPainted = delta.StandaloneInspectorLayouts > 0 && delta.StandaloneInspectorRepaints > 0;
			bool settingsPainted = delta.SettingsWindowLayouts > 0 && delta.SettingsWindowRepaints > 0 &&
			                       delta.SettingsRegistryLayouts > 0 && delta.SettingsRegistryRepaints > 0;
			if (!inspectorPainted || !settingsPainted)
			{
				if (run.StageTick >= MaxUiWaitTicks)
					throw new InvalidOperationException(
						"The live diagnostic windows did not receive confirmed Layout/Repaint events.");
				return;
			}

			run.StageBaseline = PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry);
			run.StageTick = 0;
			run.Phase = DiagnosticPhase.ExerciseLiveUIs;
		}

		private static void ExerciseLiveUIs(PendingRun run)
		{
			run.StageTick++;
			bool expanded = run.StageTick % 2 == 0;
			string filter = run.StageTick % 3 == 0 ? "PerformancePrefab" : string.Empty;
			run.InspectorHost.SetFilter(filter);
			run.SettingsWindow.SetPrefabRegistryPerformanceDiagnosticFilter(filter);
			run.SettingsWindow.SetPrefabRegistryPerformanceDiagnosticFoldout(expanded);
			RepaintLiveUIs(run);

			PrefabRegistryEditorDiagnosticSnapshot delta = CaptureDelta(run);
			bool enoughEvents =
				delta.StandaloneInspectorLayouts >= RepaintCycles &&
				delta.StandaloneInspectorRepaints >= RepaintCycles &&
				delta.SettingsWindowLayouts >= RepaintCycles &&
				delta.SettingsWindowRepaints >= RepaintCycles &&
				delta.SettingsRegistryLayouts >= RepaintCycles &&
				delta.SettingsRegistryRepaints >= RepaintCycles;

			if (!enoughEvents && run.StageTick < MaxUiWaitTicks)
				return;

			run.CurrentSizeResult.LiveUiDelta = delta;
			bool passed = enoughEvents && HasNoIdentityOrSaveWork(delta) && !EditorUtility.IsDirty(run.Registry);
			AddCheck(
				run,
				$"{run.CurrentSizeResult.RowCount} rows: live inspector/settings repaints",
				passed,
				$"{DescribeWork(delta)}, inspector L/R={delta.StandaloneInspectorLayouts}/{delta.StandaloneInspectorRepaints}, " +
				$"settings L/R={delta.SettingsWindowLayouts}/{delta.SettingsWindowRepaints}, " +
				$"registry section L/R={delta.SettingsRegistryLayouts}/{delta.SettingsRegistryRepaints}, " +
				$"dirty={EditorUtility.IsDirty(run.Registry)}");

			CloseLiveUIs(run);
			run.SizeIndex++;
			if (run.SizeIndex < FixtureSizes.Length)
			{
				run.Phase = DiagnosticPhase.ConfigureSize;
				return;
			}

			run.Phase = DiagnosticPhase.CoalescedValidation;
			SetStatus(run, "Verifying delayed validation request coalescing...");
		}

		private static void RunCoalescedValidation(PendingRun run)
		{
			ConfigureRows(run, 20);
			SetAutomation(run.Settings, autoRegister: false, autoValidation: true);
			PrefabRegistryValidationCoordinator.CancelPending(run.Registry);
			PrefabRegistryIdentityIndex.ClearAutomaticSaveSuppressionForEditorDiagnostics(run.Registry);
			PrefabRegistryIdentityIndex.GetOrCreate(run.Registry);
			PrefabRegistryIdentityUtility.GetInspectionSnapshot(run.Registry);
			EditorUtility.ClearDirty(run.Registry);
			run.StageBaseline = PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry);

			PrefabRegistryValidationCoordinator.RequestValidationForTests(
				run.Registry, PrefabRegistryValidationCoordinator.ValidationReason.Activation);
			PrefabRegistryValidationCoordinator.RequestValidationForTests(
				run.Registry, PrefabRegistryValidationCoordinator.ValidationReason.SerializedEdit);
			PrefabRegistryValidationCoordinator.RequestValidationForTests(
				run.Registry, PrefabRegistryValidationCoordinator.ValidationReason.ProjectChanged);
			bool onePending = PrefabRegistryValidationCoordinator.PendingRegistryCount == 1;
			PrefabRegistryValidationCoordinator.FlushPendingForTests(run.Registry);

			PrefabRegistryEditorDiagnosticSnapshot delta = CaptureDelta(run);
			AddCheck(
				run,
				"Coordinator: repeated no-op requests coalesce",
				onePending &&
				delta.ValidationFlushes == 1 &&
				delta.ValidationPasses == 1 &&
				delta.NormalizationPasses == 1 &&
				delta.LookupRebuilds == 0 &&
				delta.AutomaticSaveRequests == 0 &&
				!EditorUtility.IsDirty(run.Registry),
				$"onePending={onePending}, {DescribeWork(delta)}, dirty={EditorUtility.IsDirty(run.Registry)}");

			run.Phase = DiagnosticPhase.MutatingValidation;
			SetStatus(run, "Verifying one-pass normalization and save feedback suppression...");
		}

		private static void RunMutatingValidation(PendingRun run)
		{
			PrefabRegistry.PrefabEntry row = run.Registry.prefabEntries[0];
			run.MutatedExpectedId = GetFixtureId(0);
			row.uniqueID = string.Empty;
			run.Registry.NotifyEditorMutation();
			PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(run.Registry);
			EditorUtility.ClearDirty(run.Registry);
			PrefabRegistryValidationCoordinator.CancelPending(run.Registry);
			PrefabRegistryIdentityIndex.ClearAutomaticSaveSuppressionForEditorDiagnostics(run.Registry);
			run.StageBaseline = PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry);

			PrefabRegistryValidationCoordinator.RequestValidationForTests(
				run.Registry, PrefabRegistryValidationCoordinator.ValidationReason.SerializedEdit);
			PrefabRegistryValidationCoordinator.FlushPendingForTests(run.Registry);
			run.MutationDelta = CaptureDelta(run);

			PrefabRegistry.PrefabEntry lookup = run.Registry.FindEntryByID(run.MutatedExpectedId);
			bool corrected = row.uniqueID == run.MutatedExpectedId && lookup == row;
			AddCheck(
				run,
				"Coordinator: required normalization rebuilds and saves once",
				corrected &&
				run.MutationDelta.ValidationFlushes == 1 &&
				run.MutationDelta.ValidationPasses == 1 &&
				run.MutationDelta.NormalizationPasses == 1 &&
				run.MutationDelta.LookupRebuilds == 1 &&
				run.MutationDelta.AutomaticSaveRequests == 1,
				$"corrected={corrected}, {DescribeWork(run.MutationDelta)}");

			run.FeedbackBaseline = PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry);
			run.StageTick = 0;
			run.Phase = DiagnosticPhase.WaitForSaveFeedback;
		}

		private static void WaitForSaveFeedback(PendingRun run)
		{
			run.StageTick++;
			if (run.StageTick < 8)
				return;

			PrefabRegistryEditorDiagnosticSnapshot delta =
				PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry).DeltaFrom(run.FeedbackBaseline);
			AddCheck(
				run,
				"Coordinator: automatic save does not feed back into validation",
				delta.ValidationFlushes == 0 && delta.ValidationPasses == 0 && delta.AutomaticSaveRequests == 0,
				DescribeWork(delta));

			run.Phase = DiagnosticPhase.DisabledAutomation;
			SetStatus(run, "Verifying disabled auto-validation remains read-only...");
		}

		private static void RunDisabledAutomation(PendingRun run)
		{
			SetAutomation(run.Settings, autoRegister: false, autoValidation: false);
			PrefabRegistryValidationCoordinator.CancelPending(run.Registry);
			PrefabRegistryIdentityIndex.ClearAutomaticSaveSuppressionForEditorDiagnostics(run.Registry);
			PrefabRegistry.PrefabEntry row = run.Registry.prefabEntries[0];
			row.uniqueID = string.Empty;
			run.Registry.NotifyEditorMutation();
			EditorUtility.ClearDirty(run.Registry);
			run.StageBaseline = PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry);

			PrefabRegistryValidationCoordinator.RequestValidationForTests(
				run.Registry, PrefabRegistryValidationCoordinator.ValidationReason.SerializedEdit);
			PrefabRegistryValidationCoordinator.FlushPendingForTests(run.Registry);
			PrefabRegistryEditorDiagnosticSnapshot delta = CaptureDelta(run);
			bool readOnly = string.IsNullOrEmpty(row.uniqueID) && !EditorUtility.IsDirty(run.Registry);
			AddCheck(
				run,
				"Disabled automation: validation is read-only",
				readOnly && delta.ValidationFlushes == 0 && delta.ValidationPasses == 0 &&
				delta.NormalizationPasses == 0 && delta.LookupRebuilds == 0 &&
				delta.AutomaticSaveRequests == 0,
				$"readOnly={readOnly}, {DescribeWork(delta)}");

			row.uniqueID = GetFixtureId(0);
			run.Registry.NotifyEditorMutation();
			EditorUtility.ClearDirty(run.Registry);
			run.Phase = DiagnosticPhase.QueuePrefabBatch;
			SetStatus(run, "Verifying two-prefab delayed registration batching...");
		}

		private static void QueuePrefabBatch(PendingRun run)
		{
			SetAutomation(run.Settings, autoRegister: true, autoValidation: true);
			if (!CrystalSaveEditorAutomationSettings.AutoRegisterPrefabs)
				throw new InvalidOperationException(
					"The temporary SaveSettings did not expose auto-registration after it was enabled.");
			PrefabRegistryValidationCoordinator.CancelPending(run.Registry);
			SaveablePrefab.RemovePrefabRegistrySyncsForEditorDiagnostics(WorkingFolder);
			run.Registry.prefabEntries.Clear();
			run.Registry.RebuildLookup();
			PrefabRegistryIdentityIndex.Invalidate(run.Registry);
			PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(run.Registry);
			EditorUtility.ClearDirty(run.Registry);

			run.BatchFirst = run.Fixtures[0];
			run.BatchSecond = run.Fixtures[1];
			SaveablePrefab firstSaveable = run.BatchFirst.GetComponent<SaveablePrefab>();
			SaveablePrefab secondSaveable = run.BatchSecond.GetComponent<SaveablePrefab>();
			run.StageBaseline = PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry);

			SaveablePrefab.EnsurePrefabAssetIdentityForEditor(
				firstSaveable, run.BatchFirst);
			SaveablePrefab.EnsurePrefabAssetIdentityForEditor(
				secondSaveable, run.BatchSecond);
			run.BatchFirstId = firstSaveable.PrefabAssetID;
			run.BatchSecondId = secondSaveable.PrefabAssetID;
			int queuedCount = SaveablePrefab.PendingPrefabRegistrySyncCountForEditorDiagnostics;
			AddCheck(
				run,
				"Delayed prefab identity batch: public identity path queues two prefabs",
				queuedCount == 2,
				$"queued={queuedCount}");
			SaveablePrefab.SuspendPrefabRegistrySyncFlushForEditorDiagnostics();
			if (queuedCount != 2)
			{
				SaveablePrefab.RemovePrefabRegistrySyncsForEditorDiagnostics(WorkingFolder);
				run.Phase = DiagnosticPhase.Complete;
				return;
			}
			run.StageTick = 0;
			run.Phase = DiagnosticPhase.WaitForPrefabBatch;
		}

		private static void WaitForPrefabBatch(PendingRun run)
		{
			run.StageTick++;
			if (run.StageTick == 1)
			{
				SetAutomation(run.Settings, autoRegister: true, autoValidation: true);
				SaveablePrefab.FlushPrefabRegistrySyncsForEditorDiagnostics();
			}
			PrefabRegistryEditorDiagnosticSnapshot delta = CaptureDelta(run);
			if (delta.PrefabBatchFlushes < 1 && run.StageTick < MaxBatchWaitTicks)
				return;

			if (run.StageTick < 8)
				return;

			PrefabRegistry.PrefabEntry first = run.Registry.FindEntryByID(run.BatchFirstId);
			PrefabRegistry.PrefabEntry second = run.Registry.FindEntryByID(run.BatchSecondId);
			bool correctRows = run.Registry.prefabEntries.Count == 2 &&
			                   first?.prefab == run.BatchFirst &&
			                   second?.prefab == run.BatchSecond &&
			                   !string.Equals(run.BatchFirstId, run.BatchSecondId, StringComparison.Ordinal);
			AddCheck(
				run,
				"Delayed prefab identity batch: two rows, one rebuild and save",
				correctRows && delta.PrefabBatchFlushes == 1 &&
				delta.PrefabBatchSaveRequests == 1 &&
				delta.LookupRebuilds == 1 &&
				delta.AutomaticSaveRequests == 1 &&
				delta.ValidationFlushes == 0 && delta.ValidationPasses == 0,
				$"correctRows={correctRows}, rows={run.Registry.prefabEntries.Count}, {DescribeWork(delta)}");

			run.Phase = DiagnosticPhase.Complete;
		}

		private static void ConfigureRows(PendingRun run, int count)
		{
			run.Registry.prefabEntries = run.Fixtures.Take(count)
				.Select((prefab, index) => new PrefabRegistry.PrefabEntry
				{
					uniqueID = GetFixtureId(index),
					prefab = prefab
				})
				.ToList();
			run.Registry.RebuildLookup();
			PrefabRegistryIdentityIndex.Invalidate(run.Registry);
			PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(run.Registry);
			EditorUtility.ClearDirty(run.Registry);
		}

		private static PrefabRegistryEditorDiagnosticSnapshot CaptureDelta(PendingRun run)
		{
			return PrefabRegistryEditorDiagnosticCounters.Capture(run.Registry).DeltaFrom(run.StageBaseline);
		}

		internal static bool HasNoIdentityOrSaveWork(PrefabRegistryEditorDiagnosticSnapshot delta)
		{
			return delta.IndexBuilds == 0 &&
			       delta.AssetResolutions == 0 &&
			       delta.LookupRebuilds == 0 &&
			       delta.InspectionSnapshotBuilds == 0 &&
			       delta.NormalizationPasses == 0 &&
			       delta.ValidationFlushes == 0 &&
			       delta.ValidationPasses == 0 &&
			       delta.AutomaticSaveRequests == 0 &&
			       delta.PrefabBatchFlushes == 0 &&
			       delta.PrefabBatchSaveRequests == 0;
		}

		private static string DescribeWork(PrefabRegistryEditorDiagnosticSnapshot delta)
		{
			return $"indexBuilds={delta.IndexBuilds}, resolutions={delta.AssetResolutions}, " +
			       $"lookupRebuilds={delta.LookupRebuilds}, snapshots={delta.InspectionSnapshotBuilds}, " +
			       $"normalizations={delta.NormalizationPasses}, validationFlushes={delta.ValidationFlushes}, " +
			       $"validationPasses={delta.ValidationPasses}, saves={delta.AutomaticSaveRequests}, " +
			       $"batchFlushes={delta.PrefabBatchFlushes}, batchSaves={delta.PrefabBatchSaveRequests}";
		}

		private static void AddCheck(PendingRun run, string name, bool passed, string details)
		{
			run.Checks.Add(new DiagnosticCheck { Name = name, Passed = passed, Details = details });
			if (!passed)
				run.AnyCheckFailed = true;
			Debug.unityLogger.Log(
				passed ? LogType.Log : LogType.Error,
				$"[Crystal Save][PrefabRegistry Performance] {(passed ? "PASS" : "FAIL")}: {name} — {details}");
		}

		private static void RepaintLiveUIs(PendingRun run)
		{
			run.InspectorHost?.Repaint();
			run.SettingsWindow?.Repaint();
			InternalEditorUtilityRepaintAllViews();
		}

		private static void InternalEditorUtilityRepaintAllViews()
		{
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
		}

		private static void CloseLiveUIs(PendingRun run)
		{
			if (run.SettingsWindow != null && run.SettingsWindowState != null)
			{
				try
				{
					run.SettingsWindow.RestorePrefabRegistryPerformanceDiagnostic(run.SettingsWindowState);
				}
				catch (Exception ex)
				{
					run.CleanupErrors.Add($"Could not restore Crystal Save Settings window: {ex}");
				}
			}

			if (run.SettingsWindowCreated && run.SettingsWindow != null)
				run.SettingsWindow.Close();
			run.SettingsWindowState = null;
			run.SettingsWindow = null;
			run.SettingsWindowCreated = false;

			if (run.InspectorHost != null)
			{
				run.InspectorHost.Close();
				DestroyImmediate(run.InspectorHost);
				run.InspectorHost = null;
			}
		}

		private static void SetAutomation(SaveSettings settings, bool autoRegister, bool autoValidation)
		{
			if (settings == null)
				return;
			SerializedObject serialized = new SerializedObject(settings);
			SerializedProperty initialized = serialized.FindProperty("editorAutomationSettingsInitialized");
			SerializedProperty autoRegisterProperty = serialized.FindProperty("autoRegisterPrefabs");
			SerializedProperty autoValidationProperty = serialized.FindProperty("enablePrefabRegistryAutoValidation");
			if (initialized != null) initialized.boolValue = true;
			if (autoRegisterProperty != null) autoRegisterProperty.boolValue = autoRegister;
			if (autoValidationProperty != null) autoValidationProperty.boolValue = autoValidation;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			CrystalSaveEditorAutomationSettings.ClearCache();
		}

		private static void SetSaveablePrefabIdentity(
			SaveablePrefab saveable,
			string prefabAssetId,
			bool registerWithSaveSystem)
		{
			if (saveable == null)
				return;
			SerializedObject serialized = new SerializedObject(saveable);
			SerializedProperty id = serialized.FindProperty("prefabAssetID");
			SerializedProperty instanceId = serialized.FindProperty("uniqueID");
			SerializedProperty register = serialized.FindProperty("RegisterWithSaveSystem");
			if (id != null) id.stringValue = prefabAssetId;
			if (instanceId != null) instanceId.stringValue = string.Empty;
			if (register != null) register.boolValue = registerWithSaveSystem;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(saveable);
		}

		private static string GetFixtureId(int index)
		{
			return $"CSP_PERF_DIAG_{index:000}";
		}

		private static void RegisterLifecycleCallbacks()
		{
			AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
			AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			EditorApplication.quitting -= OnEditorQuitting;
			EditorApplication.quitting += OnEditorQuitting;
		}

		private static void UnregisterLifecycleCallbacks()
		{
			AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.quitting -= OnEditorQuitting;
		}

		private static void OnBeforeAssemblyReload()
		{
			AbortForLifecycle("Assembly reload interrupted the diagnostic.");
		}

		private static void OnEditorQuitting()
		{
			AbortForLifecycle("Editor shutdown interrupted the diagnostic.");
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
				AbortForLifecycle($"Play Mode transition ({state}) interrupted the diagnostic.");
		}

		private static void AbortForLifecycle(string reason)
		{
			PendingRun run = pendingRun;
			if (run == null || run.Finished)
				return;
			run.SuppressInteractiveFinish = true;
			run.LifecycleAbort = true;
			run.FatalError = reason;
			FinishRun(run, reason);
		}

		private static void FinishRun(PendingRun run, string status)
		{
			if (run == null || run.Finished)
				return;

			run.Finished = true;
			EditorApplication.update -= UpdateDiagnostic;
			UnregisterLifecycleCallbacks();
			EditorUtility.ClearProgressBar();

			TryCleanup(run, "Live UI restoration", () => CloseLiveUIs(run));
			TryCleanup(run, "Validation cancellation", () =>
				PrefabRegistryValidationCoordinator.CancelPending(run.Registry));
			TryCleanup(run, "Prefab batch cancellation", () =>
				SaveablePrefab.RemovePrefabRegistrySyncsForEditorDiagnostics(WorkingFolder));
			TryCleanup(run, "Save-feedback suppression reset", () =>
				PrefabRegistryIdentityIndex.ClearAutomaticSaveSuppressionForEditorDiagnostics(run.Registry));
			TryCleanup(run, "Diagnostic counter restoration", () =>
			{
				if (run.CounterSessionStarted && run.Registry != null)
					PrefabRegistryEditorDiagnosticCounters.EndSession(run.Registry);
				run.CounterSessionStarted =
					ReferenceEquals(PrefabRegistryEditorDiagnosticCounters.ActiveRegistry, run.Registry);
			});
			TryCleanup(run, "Active-asset override restoration", () =>
			{
				try
				{
					run.OverrideScope?.Dispose();
					run.OverrideRestored = true;
				}
				finally
				{
					run.OverrideScope = null;
				}
			});

			try
			{
				if (run.LifecycleAbort)
					DeleteWorkingFolderWithoutAssetDatabase();
				else
					DeleteWorkingFolder();
				run.WorkingAssetsRemoved = run.LifecycleAbort
					? !Directory.Exists(Path.GetFullPath(WorkingFolder))
					: !AssetDatabase.IsValidFolder(WorkingFolder);
			}
			catch (Exception ex)
			{
				run.CleanupErrors.Add($"Working-folder cleanup failed: {ex}");
			}

			try
			{
				WriteReport(run, importAsset: !run.LifecycleAbort);
			}
			catch (Exception ex)
			{
				run.FatalError = string.IsNullOrEmpty(run.FatalError)
					? $"Report generation failed: {ex}"
					: run.FatalError + Environment.NewLine + $"Report generation failed: {ex}";
			}

			bool passed = string.IsNullOrEmpty(run.FatalError) &&
			              !run.AnyCheckFailed &&
			              run.CleanupErrors.Count == 0 &&
			              run.WorkingAssetsRemoved;

			if (run.Window != null)
			{
				run.Window.lastStatus = passed
					? "PASS — all performance and correctness checks passed; temporary state removed."
					: $"FAIL — {status} Review the report for details.";
				run.Window.lastReportPath = ReportPath;
				run.Window.Repaint();
			}

			pendingRun = null;
			string logMessage = $"[Crystal Save][PrefabRegistry Performance] {(passed ? "PASS" : "FAIL")}. Report: {ReportPath}";
			if (passed) Debug.Log(logMessage); else Debug.LogError(logMessage);

			if (run.LifecycleAbort)
				return;

			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			if (!run.ExitEditorWhenFinished && !run.SuppressInteractiveFinish && !Application.isBatchMode)
			{
				if (run.RevealFolderAfterRun)
					EditorUtility.RevealInFinder(Path.GetFullPath(RootFolder));
				if (run.OpenReportAfterRun)
					AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(ReportPath));
				string cleanupMessage = run.CleanupErrors.Count == 0 && run.WorkingAssetsRemoved
					? "The active registry/settings were not modified and all working assets were removed."
					: "Some cleanup checks failed. Review the report before continuing.";
				EditorUtility.DisplayDialog(
					"Crystal Save Diagnostic Complete",
					$"Result: {(passed ? "PASS" : "FAIL")}\n\nReport:\n{ReportPath}\n\n" +
					cleanupMessage,
					"OK");
			}

			if (run.ExitEditorWhenFinished)
				EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
		}

		private static void TryCleanup(PendingRun run, string label, Action action)
		{
			try
			{
				action?.Invoke();
			}
			catch (Exception ex)
			{
				run.CleanupErrors.Add($"{label} failed: {ex}");
			}
		}

		private static void WriteReport(PendingRun run, bool importAsset = true)
		{
			if (importAsset)
				EnsureFolder(RootFolder);
			else
				Directory.CreateDirectory(Path.GetFullPath(RootFolder));
			File.WriteAllText(Path.GetFullPath(ReportPath), BuildReportText(run));
			if (importAsset)
				AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceSynchronousImport);
		}

		internal static string BuildReportText(PendingRun run)
		{
			if (run == null)
				throw new ArgumentNullException(nameof(run));

			StringBuilder report = new StringBuilder();
			bool passed = string.IsNullOrEmpty(run.FatalError) &&
			              !run.AnyCheckFailed &&
			              run.CleanupErrors.Count == 0 &&
			              run.WorkingAssetsRemoved;

			report.AppendLine("# Crystal Save PrefabRegistry Editor Performance Diagnostic");
			report.AppendLine();
			report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
			report.AppendLine($"Unity: {Application.unityVersion}");
			report.AppendLine($"Platform: {Application.platform}");
			report.AppendLine($"Started: {run.StartedAt:yyyy-MM-dd HH:mm:ss zzz}");
			report.AppendLine($"Overall result: **{(passed ? "PASS" : "FAIL")}**");
			report.AppendLine();

			report.AppendLine("## Fixture Scaling and UI Repaints");
			report.AppendLine();
			report.AppendLine("| Rows | Cold builds | Resolutions | Cold ms* | Closed warm work | Inspector L/R | Settings L/R | Registry section L/R | Live identity/save work |");
			report.AppendLine("|---:|---:|---:|---:|---|---:|---:|---:|---|");
			foreach (SizeResult size in run.SizeResults)
			{
				PrefabRegistryEditorDiagnosticSnapshot zero = PrefabRegistryEditorDiagnosticCounters.Capture(null);
				PrefabRegistryEditorDiagnosticSnapshot cold = size.ColdDelta ?? zero;
				PrefabRegistryEditorDiagnosticSnapshot closed = size.ClosedWarmDelta ?? zero;
				PrefabRegistryEditorDiagnosticSnapshot live = size.LiveUiDelta ?? zero;
				report.AppendLine(
					$"| {size.RowCount} | {cold.IndexBuilds} | {cold.AssetResolutions} | {size.ColdBuildMilliseconds:F3} | " +
					$"{(HasNoIdentityOrSaveWork(closed) ? "zero" : "non-zero")} | " +
					$"{live.StandaloneInspectorLayouts}/{live.StandaloneInspectorRepaints} | " +
					$"{live.SettingsWindowLayouts}/{live.SettingsWindowRepaints} | " +
					$"{live.SettingsRegistryLayouts}/{live.SettingsRegistryRepaints} | " +
					$"{(HasNoIdentityOrSaveWork(live) ? "zero" : "non-zero")} |");
			}
			report.AppendLine();
			report.AppendLine("*Timing is informational only and is never used as a pass/fail threshold.*");
			report.AppendLine();

			report.AppendLine("## Checks");
			report.AppendLine();
			foreach (DiagnosticCheck check in run.Checks)
			{
				report.AppendLine($"### {(check.Passed ? "PASS" : "FAIL")} — {check.Name}");
				report.AppendLine();
				report.AppendLine(check.Details);
				report.AppendLine();
			}

			report.AppendLine("## Cleanup");
			report.AppendLine();
			report.AppendLine($"- In-memory override restored: {(run.OverrideRestored ? "PASS" : "FAIL")}");
			report.AppendLine($"- Diagnostic counter session ended: {(!run.CounterSessionStarted ? "PASS" : "FAIL")}");
			report.AppendLine($"- Working assets removed: {(run.WorkingAssetsRemoved ? "PASS" : "FAIL")}");
			report.AppendLine($"- Cleanup errors: {run.CleanupErrors.Count}");
			foreach (string cleanupError in run.CleanupErrors)
				report.AppendLine($"  - {cleanupError.Replace(Environment.NewLine, " ")}");
			report.AppendLine();

			if (!string.IsNullOrEmpty(run.FatalError))
			{
				report.AppendLine("## Diagnostic Error");
				report.AppendLine();
				report.AppendLine("```text");
				report.AppendLine(run.FatalError);
				report.AppendLine("```");
			}

			return NormalizeLineEndings(report.ToString());
		}

		private static string NormalizeLineEndings(string value)
		{
			return value.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
		}

		private static void SetStatus(PendingRun run, string status)
		{
			if (run?.Window == null)
				return;
			run.Window.lastStatus = status;
			run.Window.Repaint();
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;
			string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
			if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
				EnsureFolder(parent);
			string folder = Path.GetFileName(path);
			if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder))
				AssetDatabase.CreateFolder(parent, folder);
		}

		private static void DeleteWorkingFolder()
		{
			if (AssetDatabase.IsValidFolder(WorkingFolder))
				AssetDatabase.DeleteAsset(WorkingFolder);
		}

		private static void DeleteWorkingFolderWithoutAssetDatabase()
		{
			string fullPath = Path.GetFullPath(WorkingFolder);
			if (Directory.Exists(fullPath))
				Directory.Delete(fullPath, recursive: true);
			string metaPath = fullPath + ".meta";
			if (File.Exists(metaPath))
				File.Delete(metaPath);
		}

		internal enum DiagnosticPhase
		{
			CreatingFixtures,
			ConfigureSize,
			ColdBuild,
			ClosedWarmReads,
			OpenLiveUIs,
			WarmLiveUIs,
			ExerciseLiveUIs,
			CoalescedValidation,
			MutatingValidation,
			WaitForSaveFeedback,
			DisabledAutomation,
			QueuePrefabBatch,
			WaitForPrefabBatch,
			Complete
		}

		internal sealed class PendingRun
		{
			internal PrefabRegistryEditorPerformanceDiagnosticBuilder Window;
			internal bool ExitEditorWhenFinished;
			internal bool OpenReportAfterRun;
			internal bool RevealFolderAfterRun;
			internal bool SuppressInteractiveFinish;
			internal bool LifecycleAbort;
			internal DateTime StartedAt;
			internal DiagnosticPhase Phase;
			internal bool CancelRequested;
			internal bool Finished;
			internal bool AnyCheckFailed;
			internal string FatalError;
			internal PrefabRegistry Registry;
			internal SaveSettings Settings;
			internal ActiveAssetOverrideScope OverrideScope;
			internal bool OverrideRestored = true;
			internal bool CounterSessionStarted;
			internal readonly List<GameObject> Fixtures = new List<GameObject>();
			internal readonly List<SizeResult> SizeResults = new List<SizeResult>();
			internal readonly List<DiagnosticCheck> Checks = new List<DiagnosticCheck>();
			internal readonly List<string> CleanupErrors = new List<string>();
			internal int SizeIndex;
			internal int StageTick;
			internal SizeResult CurrentSizeResult;
			internal PrefabRegistryIdentityUtility.InspectionSnapshot WarmSnapshot;
			internal PrefabRegistryEditorDiagnosticSnapshot StageBaseline;
			internal PrefabRegistryEditorDiagnosticSnapshot MutationDelta;
			internal PrefabRegistryEditorDiagnosticSnapshot FeedbackBaseline;
			internal string MutatedExpectedId;
			internal PrefabRegistryDiagnosticInspectorHost InspectorHost;
			internal RememberMeSettingsWindow SettingsWindow;
			internal RememberMeSettingsWindow.PrefabRegistryPerformanceDiagnosticState SettingsWindowState;
			internal bool SettingsWindowCreated;
			internal GameObject BatchFirst;
			internal GameObject BatchSecond;
			internal string BatchFirstId;
			internal string BatchSecondId;
			internal bool WorkingAssetsRemoved;
		}

		internal sealed class SizeResult
		{
			internal int RowCount;
			internal double ColdBuildMilliseconds;
			internal PrefabRegistryEditorDiagnosticSnapshot ColdDelta;
			internal PrefabRegistryEditorDiagnosticSnapshot ClosedWarmDelta;
			internal PrefabRegistryEditorDiagnosticSnapshot LiveUiDelta;
		}

		internal sealed class DiagnosticCheck
		{
			internal string Name;
			internal bool Passed;
			internal string Details;
		}

		internal sealed class ActiveAssetOverrideScope : IDisposable
		{
			private readonly FieldInfo cacheField;
			private readonly CrystalSaveAssetOverrides previousOverride;
			private readonly CrystalSaveAssetOverrides diagnosticOverride;
			private bool disposed;

			internal ActiveAssetOverrideScope(SaveSettings settings, PrefabRegistry registry)
			{
				cacheField = typeof(CrystalSaveOverrides).GetField(
					"cached", BindingFlags.Static | BindingFlags.NonPublic);
				if (cacheField == null)
					throw new MissingFieldException(typeof(CrystalSaveOverrides).FullName, "cached");

				previousOverride = cacheField.GetValue(null) as CrystalSaveAssetOverrides;
				diagnosticOverride = CreateInstance<CrystalSaveAssetOverrides>();
				diagnosticOverride.saveSettings = settings;
				diagnosticOverride.prefabRegistry = registry;
				cacheField.SetValue(null, diagnosticOverride);
				CrystalSaveEditorAutomationSettings.ClearCache();
				PrefabRegistryIdentityIndex.InvalidateAll();
			}

			public void Dispose()
			{
				if (disposed)
					return;
				disposed = true;
				try
				{
					cacheField.SetValue(null, previousOverride);
					CrystalSaveEditorAutomationSettings.ClearCache();
					PrefabRegistryIdentityIndex.InvalidateAll();
				}
				finally
				{
					if (diagnosticOverride != null)
						DestroyImmediate(diagnosticOverride);
				}
			}
		}
	}

	internal sealed class PrefabRegistryDiagnosticInspectorHost : EditorWindow
	{
		private UnityEditor.Editor registryEditor;

		internal void Initialize(PrefabRegistry registry)
		{
			if (registry == null)
				throw new ArgumentNullException(nameof(registry));
			registryEditor = UnityEditor.Editor.CreateEditor(registry, typeof(PrefabRegistryEditor));
			if (registryEditor == null)
				throw new InvalidOperationException("Could not create the PrefabRegistry custom editor.");
		}

		internal void SetFilter(string filter)
		{
			if (registryEditor is PrefabRegistryEditor prefabRegistryEditor)
				prefabRegistryEditor.SetPrefabRegistryPerformanceDiagnosticFilter(filter);
			Repaint();
		}

		private void OnGUI()
		{
			if (registryEditor == null)
			{
				EditorGUILayout.HelpBox("The diagnostic PrefabRegistry editor is unavailable.", MessageType.Error);
				return;
			}
			registryEditor.OnInspectorGUI();
		}

		private void OnDisable()
		{
			if (registryEditor != null)
			{
				DestroyImmediate(registryEditor);
				registryEditor = null;
			}
		}
	}
}
#endif
