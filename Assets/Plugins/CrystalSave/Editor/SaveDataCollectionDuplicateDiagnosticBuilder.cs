#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Arawn.CrystalSave.Runtime;
using Arawn.CrystalSave.Runtime.Examples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.Editor
{
	/// <summary>
	/// Builds and runs an isolated Play Mode suite for SaveData collection ownership,
	/// and can capture the next real persisted save without changing project settings.
	/// </summary>
	[InitializeOnLoad]
	public sealed class SaveDataCollectionDuplicateDiagnosticBuilder : EditorWindow
	{
		private const string MenuPath =
			"Tools/Crystal Save/Runtime Debug/Diagnostics/SaveData Collection Duplicate Diagnostic";
		private const string RootFolder =
			"Assets/CrystalSaveDiagnostics/SaveDataCollectionDuplicates";
		private const string WorkingFolder = RootFolder + "/Working";
		private const string ScenePath =
			WorkingFolder + "/CSP_SaveDataCollectionDuplicateDiagnostic.unity";
		private const string ReportPath =
			RootFolder + "/SaveDataCollectionDuplicateReport.md";
		private const string LibraryFolder =
			"Library/CrystalSaveDiagnostics/SaveDataCollectionDuplicates";
		private const string ManifestRelativePath =
			LibraryFolder + "/ActiveRun.json";
		private const string ManifestSessionKey =
			"CrystalSave.SaveDataCollectionDuplicates.ActiveManifest";
		private const string LogPrefix =
			"[Crystal Save][SaveData Collection Duplicate Diagnostic]";
		private const int DefaultSuiteTimeoutSeconds = 180;
		private const int DefaultCaptureTimeoutSeconds = 120;

		private static DiagnosticRunManifest activeManifest;
		private static bool callbacksRegistered;
		private static bool coordinatorBusy;

		[SerializeField] private bool openReportAfterRun = true;
		[SerializeField] private bool revealFolderAfterRun;
		[SerializeField] private int captureTimeoutSeconds =
			DefaultCaptureTimeoutSeconds;

		static SaveDataCollectionDuplicateDiagnosticBuilder()
		{
			RegisterCallbacks();
			EditorApplication.delayCall += ResumePersistedRun;
		}

		[MenuItem(MenuPath)]
		public static void ShowWindow()
		{
			SaveDataCollectionDuplicateDiagnosticBuilder window =
				GetWindow<SaveDataCollectionDuplicateDiagnosticBuilder>(
					"SaveData Duplicate Diagnostic");
			window.minSize = new Vector2(620f, 390f);
			window.Show();
		}

		[System.ComponentModel.EditorBrowsable(
			System.ComponentModel.EditorBrowsableState.Never)]
		public static void RunIsolatedSuiteFromCommandLine()
		{
			if (!TryOpenSavedCommandLineBootstrapScene(out string sceneError))
			{
				Debug.LogError($"{LogPrefix} {sceneError}");
				EditorApplication.Exit(2);
				return;
			}

			StartIsolatedSuite(
				openReport: false,
				revealFolder: false,
				exitEditorWhenFinished: true);
			if (GetActiveManifest() == null)
				EditorApplication.Exit(2);
		}

		private static bool TryOpenSavedCommandLineBootstrapScene(
			out string error)
		{
			error = null;
			SceneSetup[] currentSetup =
				EditorSceneManager.GetSceneManagerSetup();
			if (currentSetup.Length > 0 &&
			    currentSetup.All(entry =>
				    !entry.isLoaded ||
				    !string.IsNullOrWhiteSpace(entry.path)))
			{
				return true;
			}

			try
			{
				string candidate = EditorBuildSettings.scenes
					.Select(entry => entry.path)
					.FirstOrDefault(path =>
						!string.IsNullOrWhiteSpace(path) &&
						!path.StartsWith(
							RootFolder,
							StringComparison.OrdinalIgnoreCase) &&
						File.Exists(Path.GetFullPath(path)));
				if (string.IsNullOrEmpty(candidate))
				{
					candidate = AssetDatabase.FindAssets("t:Scene")
						.Select(AssetDatabase.GUIDToAssetPath)
						.Where(path =>
							path.StartsWith(
								"Assets/",
								StringComparison.OrdinalIgnoreCase) &&
							!path.StartsWith(
								RootFolder,
								StringComparison.OrdinalIgnoreCase))
						.OrderBy(path => path, StringComparer.Ordinal)
						.FirstOrDefault();
				}

				if (string.IsNullOrEmpty(candidate))
				{
					error =
						"No saved project scene was available as a command-line diagnostic bootstrap.";
					return false;
				}

				EditorSceneManager.OpenScene(
					candidate,
					OpenSceneMode.Single);
				return true;
			}
			catch (Exception ex)
			{
				error =
					$"Could not open a saved command-line bootstrap scene: {ex.Message}";
				return false;
			}
		}

		private void OnGUI()
		{
			DiagnosticRunManifest manifest = GetActiveManifest();
			EditorGUILayout.LabelField(
				"SaveData Collection Duplicate Diagnostic",
				EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"The isolated suite exercises global, prefab-blob, clone, lifecycle, " +
				"registration, nested-owner, re-entrant, and exception-abort paths through " +
				"SaveManager. Capture Next Real Save records the next persisted save in the " +
				"currently running game. The isolated suite writes no save slots; capture " +
				"only observes the real save you explicitly perform. Neither mode changes " +
				"build settings, SaveSettings, or PrefabRegistry assets.",
				MessageType.Info);

			using (new EditorGUI.DisabledScope(manifest != null))
			{
				openReportAfterRun =
					EditorGUILayout.ToggleLeft(
						"Open report after completion",
						openReportAfterRun);
				revealFolderAfterRun =
					EditorGUILayout.ToggleLeft(
						"Reveal report folder after completion",
						revealFolderAfterRun);
				captureTimeoutSeconds = Mathf.Clamp(
					EditorGUILayout.IntField(
						"Capture timeout (seconds)",
						captureTimeoutSeconds),
					15,
					900);
			}

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
			EditorGUILayout.SelectableLabel(
				ReportPath,
				EditorStyles.textField,
				GUILayout.Height(EditorGUIUtility.singleLineHeight));
			EditorGUILayout.LabelField(
				"Status",
				manifest == null ? "Idle" : manifest.status,
				EditorStyles.wordWrappedLabel);

			GUILayout.FlexibleSpace();
			using (new EditorGUI.DisabledScope(
				       manifest != null ||
				       Application.isPlaying ||
				       EditorApplication.isPlayingOrWillChangePlaymode ||
				       EditorApplication.isCompiling ||
				       EditorApplication.isUpdating))
			{
				if (GUILayout.Button(
					    "Run Isolated Play Mode Suite",
					    GUILayout.Height(34f)))
				{
					StartIsolatedSuite(
						openReportAfterRun,
						revealFolderAfterRun);
				}
			}

			using (new EditorGUI.DisabledScope(
				       manifest != null ||
				       !Application.isPlaying ||
				       EditorApplication.isCompiling))
			{
				if (GUILayout.Button(
					    "Capture Next Real Save",
					    GUILayout.Height(30f)))
				{
					StartNextRealSaveCapture(
						captureTimeoutSeconds,
						openReportAfterRun,
						revealFolderAfterRun);
				}
			}

			if (manifest != null &&
			    GUILayout.Button("Cancel and Restore", GUILayout.Height(28f)))
			{
				RequestCancellation(manifest, "Cancelled by the user.");
			}

			if (File.Exists(Path.GetFullPath(ReportPath)) &&
			    GUILayout.Button("Open Last Report"))
			{
				OpenReport();
			}
		}

		private static void StartIsolatedSuite(
			bool openReport,
			bool revealFolder)
		{
			StartIsolatedSuite(
				openReport,
				revealFolder,
				exitEditorWhenFinished: false);
		}

		private static void StartIsolatedSuite(
			bool openReport,
			bool revealFolder,
			bool exitEditorWhenFinished)
		{
			if (GetActiveManifest() != null)
			{
				FailStart("Another SaveData duplicate diagnostic is already active.");
				return;
			}

			if (Application.isPlaying ||
			    EditorApplication.isPlayingOrWillChangePlaymode ||
			    EditorApplication.isCompiling ||
			    EditorApplication.isUpdating)
			{
				FailStart(
					"Start the isolated suite from a stable Edit Mode state while Unity is not compiling or importing.");
				return;
			}

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				return;

			SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
			if (setup.Length == 0 ||
			    setup.Any(entry => entry.isLoaded &&
			                       string.IsNullOrWhiteSpace(entry.path)))
			{
				FailStart(
					"Every loaded scene must be saved before the diagnostic can restore the editor scene setup safely.");
				return;
			}

			if (!TryResolveActiveAssets(
				    out SaveSettings settings,
				    out PrefabRegistry registry,
				    out string settingsPath,
				    out string registryPath,
				    out string assetError))
			{
				FailStart(assetError);
				return;
			}

			string runId = Guid.NewGuid().ToString("N");
			string resultPath = GetResultPath(runId);
			var manifest = new DiagnosticRunManifest
			{
				runId = runId,
				mode = DiagnosticRunMode.IsolatedSuite.ToString(),
				phase = DiagnosticRunPhase.Preparing.ToString(),
				status = "Creating the isolated diagnostic scene...",
				startedUtcTicks = DateTime.UtcNow.Ticks,
				timeoutSeconds = DefaultSuiteTimeoutSeconds,
				resultFilePath = resultPath,
				activeSettingsPath = settingsPath,
				activeRegistryPath = registryPath,
					openReportAfterRun = openReport,
					revealFolderAfterRun = revealFolder,
					exitEditorWhenFinished = exitEditorWhenFinished,
				originalScenes = setup.Select(
						entry => new DiagnosticSceneSetup
						{
							path = entry.path,
							isLoaded = entry.isLoaded,
							isActive = entry.isActive
						})
					.ToList()
			};

			try
			{
				EnsureFolder(RootFolder);
				DeleteWorkingFolderWithAssetDatabase();
				EnsureFolder(WorkingFolder);
				EnsureLibraryFolder();
				DeleteFileIfPresent(resultPath);

				activeManifest = manifest;
				PersistManifest(manifest);
				CreateDiagnosticScene(
					manifest,
					settings,
					registry);

				manifest.phase = DiagnosticRunPhase.AwaitingPlayMode.ToString();
				manifest.status =
					"Entering Play Mode and waiting for the isolated suite...";
				PersistManifest(manifest);
				EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
				EditorApplication.isPlaying = true;
			}
			catch (Exception ex)
			{
				manifest.failure =
					$"Could not start the isolated suite: {ex}";
				manifest.status = "Setup failed; restoring the editor.";
				PersistManifest(manifest);
				FinishIsolatedSuite(manifest);
			}
		}

		private static void StartNextRealSaveCapture(
			int timeoutSeconds,
			bool openReport,
			bool revealFolder)
		{
			if (GetActiveManifest() != null)
			{
				FailStart("Another SaveData duplicate diagnostic is already active.");
				return;
			}

			if (!Application.isPlaying || EditorApplication.isCompiling)
			{
				FailStart("Capture Next Real Save can only be armed in stable Play Mode.");
				return;
			}

			TryResolveActiveAssets(
				out _,
				out _,
				out string settingsPath,
				out string registryPath,
				out _);

			string runId = Guid.NewGuid().ToString("N");
			string resultPath = GetResultPath(runId);
			var manifest = new DiagnosticRunManifest
			{
				runId = runId,
				mode = DiagnosticRunMode.CaptureNextRealSave.ToString(),
				phase = DiagnosticRunPhase.Running.ToString(),
				status = "Armed; perform the save operation you want to inspect.",
				startedUtcTicks = DateTime.UtcNow.Ticks,
				timeoutSeconds = Mathf.Clamp(timeoutSeconds, 15, 900),
				resultFilePath = resultPath,
				activeSettingsPath = settingsPath,
				activeRegistryPath = registryPath,
				openReportAfterRun = openReport,
				revealFolderAfterRun = revealFolder
			};

			try
			{
				EnsureFolder(RootFolder);
				EnsureLibraryFolder();
				DeleteFileIfPresent(resultPath);
				activeManifest = manifest;
				PersistManifest(manifest);
				if (!SaveDataCollectionDuplicateDiagnostic.TryArmNextRealSaveCapture(
					    runId,
					    resultPath,
					    out string error))
				{
					throw new InvalidOperationException(error);
				}

				Debug.Log(
					$"{LogPrefix} Armed. Perform one real save; the next persisted collection will be captured.");
				RepaintWindows();
			}
			catch (Exception ex)
			{
				manifest.failure =
					$"Could not arm Capture Next Real Save: {ex}";
				PersistManifest(manifest);
				FinishCapture(manifest);
			}
		}

		private static void RegisterCallbacks()
		{
			if (callbacksRegistered)
				return;
			callbacksRegistered = true;
			EditorApplication.update += UpdateCoordinator;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			EditorApplication.quitting += OnEditorQuitting;
			AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
		}

		private static void ResumePersistedRun()
		{
			DiagnosticRunManifest manifest = GetActiveManifest();
			if (manifest == null)
				return;

			// A startup delayCall can run after a new diagnostic requests Play Mode.
			// The transition is still owned by Unity; it is not interrupted Edit Mode.
			bool stableEditMode = !Application.isPlaying &&
			                      !EditorApplication.isPlayingOrWillChangePlaymode;
			if (stableEditMode &&
			    (IsPhase(
				     manifest,
				     DiagnosticRunPhase.ExitingPlayMode) ||
			     IsPhase(
				     manifest,
				     DiagnosticRunPhase.Finalizing) ||
			     manifest.cancelRequested))
			{
				ResumePersistedRunAfterFinalizing(manifest);
				return;
			}

			if (stableEditMode &&
			    (IsPhase(manifest, DiagnosticRunPhase.Running) ||
			     IsPhase(manifest, DiagnosticRunPhase.AwaitingPlayMode)))
			{
				manifest.failure = AppendFailure(
					manifest.failure,
					"The editor restarted or returned to Edit Mode before the diagnostic completed.");
				manifest.status = "Recovering an interrupted diagnostic...";
				PersistManifest(manifest);
				if (IsMode(manifest, DiagnosticRunMode.IsolatedSuite))
					FinishIsolatedSuite(manifest);
				else
					FinishCapture(manifest);
				return;
			}

			RepaintWindows();
		}

		private static void UpdateCoordinator()
		{
			if (coordinatorBusy)
				return;

			DiagnosticRunManifest manifest = GetActiveManifest();
			if (manifest == null)
				return;

			coordinatorBusy = true;
			try
			{
				if (EditorApplication.isCompiling)
				{
					if (!manifest.cancelRequested)
					{
						manifest.cancelRequested = true;
						manifest.failure = AppendFailure(
							manifest.failure,
							"Script compilation interrupted the diagnostic.");
						manifest.status =
							"Compilation interrupted the run; cleanup will resume after compilation.";
						PersistManifest(manifest);
						if (IsMode(
							    manifest,
							    DiagnosticRunMode.CaptureNextRealSave))
							TryCancelRuntimeCapture();
					}
					return;
				}

				if (manifest.cancelRequested)
				{
					ContinueCancellation(manifest);
					return;
				}

				if (HasTimedOut(manifest))
				{
					RequestCancellation(
						manifest,
						$"The diagnostic timed out after {manifest.timeoutSeconds} seconds.");
					return;
				}

				if (!string.IsNullOrEmpty(manifest.resultFilePath) &&
				    File.Exists(manifest.resultFilePath))
				{
					if (IsMode(manifest, DiagnosticRunMode.CaptureNextRealSave))
					{
						FinishCapture(manifest);
					}
					else if (Application.isPlaying)
					{
						manifest.phase =
							DiagnosticRunPhase.ExitingPlayMode.ToString();
						manifest.status =
							"Runtime suite completed; returning to Edit Mode...";
						PersistManifest(manifest);
						EditorApplication.isPlaying = false;
					}
					else
					{
						FinishIsolatedSuite(manifest);
					}
				}
			}
			catch (Exception ex)
			{
				manifest.failure = AppendFailure(
					manifest.failure,
					$"Coordinator failure: {ex}");
				PersistManifest(manifest);
				RequestCancellation(manifest, manifest.failure);
			}
			finally
			{
				coordinatorBusy = false;
			}
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			DiagnosticRunManifest manifest = GetActiveManifest();
			if (manifest == null)
				return;

			if (state == PlayModeStateChange.EnteredPlayMode &&
			    IsMode(manifest, DiagnosticRunMode.IsolatedSuite))
			{
				manifest.phase = DiagnosticRunPhase.Running.ToString();
				manifest.status = "Isolated runtime suite is running...";
				PersistManifest(manifest);
			}
			else if (state == PlayModeStateChange.ExitingPlayMode)
			{
				if (!File.Exists(manifest.resultFilePath) &&
				    !manifest.cancelRequested)
				{
					manifest.failure = AppendFailure(
						manifest.failure,
						"Play Mode ended before a runtime result was produced.");
					manifest.status =
						"Play Mode ended early; restoring diagnostic state...";
					PersistManifest(manifest);
				}

				if (IsMode(manifest, DiagnosticRunMode.CaptureNextRealSave))
					TryCancelRuntimeCapture();
			}
			else if (state == PlayModeStateChange.EnteredEditMode)
			{
				EditorApplication.delayCall += () =>
				{
					DiagnosticRunManifest current = GetActiveManifest();
					if (current == null)
						return;
					if (IsMode(current, DiagnosticRunMode.IsolatedSuite))
						FinishIsolatedSuite(current);
					else
						FinishCapture(current);
				};
			}
		}

		private static void OnBeforeAssemblyReload()
		{
			DiagnosticRunManifest manifest = GetActiveManifest();
			if (manifest == null)
				return;

			bool expectedPlayReload =
				IsMode(manifest, DiagnosticRunMode.IsolatedSuite) &&
				((((IsPhase(
					    manifest,
					    DiagnosticRunPhase.AwaitingPlayMode) ||
				    IsPhase(
					    manifest,
					    DiagnosticRunPhase.Running)) &&
				   !EditorApplication.isCompiling)) ||
				 IsPhase(
					 manifest,
					 DiagnosticRunPhase.ExitingPlayMode));
			if (expectedPlayReload)
			{
				PersistManifest(manifest);
				return;
			}

			manifest.cancelRequested = true;
			manifest.failure = AppendFailure(
				manifest.failure,
				"An assembly reload interrupted the diagnostic.");
			manifest.status =
				"Assembly reload interrupted the run; recovery will continue after reload.";
			PersistManifest(manifest);
			if (IsMode(manifest, DiagnosticRunMode.CaptureNextRealSave))
				TryCancelRuntimeCapture();
		}

		private static void OnEditorQuitting()
		{
			DiagnosticRunManifest manifest = GetActiveManifest();
			if (manifest == null)
				return;

			manifest.cancelRequested = true;
			manifest.failure = AppendFailure(
				manifest.failure,
				"Editor shutdown interrupted the diagnostic.");
			manifest.status =
				"Interrupted by editor shutdown; cleanup will resume when the project reopens.";
			if (IsMode(
				    manifest,
				    DiagnosticRunMode.CaptureNextRealSave))
				TryCancelRuntimeCapture();
			manifest.workingAssetsRemoved =
				TryDeleteWorkingFolderWithoutAssetDatabase(
					manifest.cleanupErrors);
			PersistManifest(manifest);
		}

		private static void RequestCancellation(
			DiagnosticRunManifest manifest,
			string reason)
		{
			if (manifest == null)
				return;
			manifest.cancelRequested = true;
			manifest.failure = AppendFailure(manifest.failure, reason);
			manifest.status = "Cancellation requested; restoring state...";
			PersistManifest(manifest);
			ContinueCancellation(manifest);
		}

		private static void ContinueCancellation(
			DiagnosticRunManifest manifest)
		{
			if (IsMode(manifest, DiagnosticRunMode.CaptureNextRealSave))
				TryCancelRuntimeCapture();

			if (Application.isPlaying &&
			    IsMode(manifest, DiagnosticRunMode.IsolatedSuite))
			{
				manifest.phase =
					DiagnosticRunPhase.ExitingPlayMode.ToString();
				PersistManifest(manifest);
				EditorApplication.isPlaying = false;
				return;
			}

			if (IsMode(manifest, DiagnosticRunMode.IsolatedSuite))
				FinishIsolatedSuite(manifest);
			else
				FinishCapture(manifest);
		}

		private static void FinishIsolatedSuite(
			DiagnosticRunManifest manifest)
		{
			if (manifest == null)
				return;
			if (Application.isPlaying ||
			    EditorApplication.isPlayingOrWillChangePlaymode)
			{
				manifest.phase =
					DiagnosticRunPhase.ExitingPlayMode.ToString();
				PersistManifest(manifest);
				EditorApplication.isPlaying = false;
				return;
			}

			manifest.phase = DiagnosticRunPhase.Finalizing.ToString();
			manifest.status =
				"Restoring scenes, removing working assets, and writing the report...";
			PersistManifest(manifest);

			SaveDataCollectionDuplicateDiagnosticResult runtimeResult =
				ReadRuntimeResultForFinish(manifest);

			try
			{
				SceneSetup[] setup = manifest.originalScenes
					.Select(
						entry => new SceneSetup
						{
							path = entry.path,
							isLoaded = entry.isLoaded,
							isActive = entry.isActive
						})
					.ToArray();
				if (setup.Length == 0)
					throw new InvalidOperationException(
						"The persisted original scene setup is empty.");
				EditorSceneManager.RestoreSceneManagerSetup(setup);
				manifest.sceneSetupRestored = true;
			}
			catch (Exception ex)
			{
				manifest.cleanupErrors.Add(
					$"Scene setup restoration failed: {ex}");
			}

			try
			{
				DeleteWorkingFolderWithAssetDatabase();
				manifest.workingAssetsRemoved =
					!AssetDatabase.IsValidFolder(WorkingFolder) &&
					!Directory.Exists(Path.GetFullPath(WorkingFolder));
			}
			catch (Exception ex)
			{
				manifest.cleanupErrors.Add(
					$"Working-folder cleanup failed: {ex}");
				manifest.workingAssetsRemoved =
					TryDeleteWorkingFolderWithoutAssetDatabase(
						manifest.cleanupErrors);
			}

			RemoveRuntimeResult(manifest);
			PersistManifest(manifest);
			bool reportWritten =
				TryWriteReport(
					manifest,
					runtimeResult,
					importAsset: true);
			CompleteRun(manifest, runtimeResult, reportWritten);
		}

		private static void FinishCapture(
			DiagnosticRunManifest manifest)
		{
			if (manifest == null)
				return;
			manifest.phase = DiagnosticRunPhase.Finalizing.ToString();
			manifest.status = "Writing the captured-save report...";
			PersistManifest(manifest);

			TryCancelRuntimeCapture();
			SaveDataCollectionDuplicateDiagnosticResult runtimeResult =
				ReadRuntimeResultForFinish(manifest);
			manifest.sceneSetupRestored = true;
			manifest.workingAssetsRemoved = true;
			RemoveRuntimeResult(manifest);
			PersistManifest(manifest);
			bool reportWritten =
				TryWriteReport(
					manifest,
					runtimeResult,
					importAsset: true);
			CompleteRun(manifest, runtimeResult, reportWritten);
		}

		private static void CompleteRun(
			DiagnosticRunManifest manifest,
			SaveDataCollectionDuplicateDiagnosticResult runtimeResult,
			bool reportWritten)
		{
			bool passed =
				reportWritten &&
				string.IsNullOrEmpty(manifest.failure) &&
				manifest.cleanupErrors.Count == 0 &&
				manifest.sceneSetupRestored &&
				manifest.workingAssetsRemoved &&
				manifest.resultFileRemoved &&
				RuntimeStagesPassed(runtimeResult);

			bool openReport = manifest.openReportAfterRun;
			bool revealFolder = manifest.revealFolderAfterRun;
			string mode = manifest.mode;
			DeleteManifestFiles();
			activeManifest = null;
			RepaintWindows();

			string message =
				$"{LogPrefix} {(passed ? "PASS" : "FAIL")}. Report: {ReportPath}";
			if (passed)
				Debug.Log(message);
			else
				Debug.LogError(message);

			if (Application.isBatchMode)
			{
				if (manifest.exitEditorWhenFinished)
					EditorApplication.Exit(passed ? 0 : 1);
				return;
			}

			EditorApplication.delayCall += () =>
			{
				AssetDatabase.Refresh(
					ImportAssetOptions.ForceSynchronousImport);
				if (revealFolder)
					EditorUtility.RevealInFinder(
						Path.GetFullPath(RootFolder));
				if (openReport)
					OpenReport();
				EditorUtility.DisplayDialog(
					"Crystal Save Diagnostic Complete",
					$"Mode: {ReadableMode(mode)}\n" +
					$"Result: {(passed ? "PASS" : "FAIL")}\n\n" +
					$"Report:\n{ReportPath}",
					"OK");
			};
		}

		private static SaveDataCollectionDuplicateDiagnosticResult
			ReadRuntimeResultForFinish(DiagnosticRunManifest manifest)
		{
			if (TryReadRuntimeResult(
				    manifest,
				    out SaveDataCollectionDuplicateDiagnosticResult result))
			{
				string retainedJson =
					JsonUtility.ToJson(result, prettyPrint: false);
				if (!string.Equals(
					    manifest.retainedRuntimeResultJson,
					    retainedJson,
					    StringComparison.Ordinal))
				{
					manifest.retainedRuntimeResultJson = retainedJson;
					PersistManifest(manifest);
				}
				return result;
			}

			if (string.IsNullOrEmpty(manifest.failure))
			{
				manifest.failure =
					"No valid runtime result was produced.";
			}
			return null;
		}

		private static bool TryReadRuntimeResult(
			DiagnosticRunManifest manifest,
			out SaveDataCollectionDuplicateDiagnosticResult result)
		{
			result = null;
			if (manifest == null)
				return false;

			Exception retainedResultError = null;
			if (!string.IsNullOrWhiteSpace(
				    manifest.retainedRuntimeResultJson))
			{
				try
				{
					result =
						JsonUtility.FromJson<
							SaveDataCollectionDuplicateDiagnosticResult>(
							manifest.retainedRuntimeResultJson);
					ValidateRuntimeResult(manifest, result);
					return true;
				}
				catch (Exception ex)
				{
					result = null;
					retainedResultError = ex;
				}
			}

			if (string.IsNullOrWhiteSpace(manifest.resultFilePath) ||
			    !File.Exists(manifest.resultFilePath))
			{
				if (retainedResultError != null)
				AppendRuntimeResultReadFailure(
					manifest,
					$"The retained runtime result was invalid: {retainedResultError}");
				return false;
			}

			try
			{
				string json = File.ReadAllText(manifest.resultFilePath);
				result =
					JsonUtility.FromJson<SaveDataCollectionDuplicateDiagnosticResult>(
						json);
				ValidateRuntimeResult(manifest, result);
				return true;
			}
			catch (Exception ex)
			{
				string retainedPrefix = retainedResultError == null
					? string.Empty
					: $" Retained result error: {retainedResultError}";
				AppendRuntimeResultReadFailure(
					manifest,
					$"Could not read runtime result: {ex}{retainedPrefix}");
				return false;
			}
		}

		private static void ValidateRuntimeResult(
			DiagnosticRunManifest manifest,
			SaveDataCollectionDuplicateDiagnosticResult result)
		{
			if (result == null)
				throw new InvalidDataException(
					"The runtime result JSON was empty.");
			if (!string.Equals(
				    result.runId,
				    manifest.runId,
				    StringComparison.Ordinal))
			{
				throw new InvalidDataException(
					$"Runtime result run ID '{result.runId}' did not match manifest '{manifest.runId}'.");
			}
		}

		private static void AppendRuntimeResultReadFailure(
			DiagnosticRunManifest manifest,
			string failure)
		{
			if (manifest == null ||
			    string.IsNullOrWhiteSpace(failure) ||
			    (!string.IsNullOrEmpty(manifest.failure) &&
			     manifest.failure.Contains(
				     failure,
				     StringComparison.Ordinal)))
			{
				return;
			}

			manifest.failure = AppendFailure(
				manifest.failure,
				failure);
		}

		private static void RemoveRuntimeResult(
			DiagnosticRunManifest manifest)
		{
			try
			{
				DeleteFileIfPresent(manifest.resultFilePath);
				DeleteFileIfPresent(manifest.resultFilePath + ".tmp");
				manifest.resultFileRemoved =
					!File.Exists(manifest.resultFilePath) &&
					!File.Exists(manifest.resultFilePath + ".tmp");
			}
			catch (Exception ex)
			{
				manifest.cleanupErrors.Add(
					$"Runtime-result cleanup failed: {ex}");
			}
		}

		private static void ResumePersistedRunAfterFinalizing(
			DiagnosticRunManifest manifest)
		{
			if (IsMode(manifest, DiagnosticRunMode.IsolatedSuite))
				FinishIsolatedSuite(manifest);
			else
				FinishCapture(manifest);
		}

		private static DiagnosticRunManifest GetActiveManifest()
		{
			if (activeManifest != null)
				return activeManifest;

			string sessionPath =
				SessionState.GetString(
					ManifestSessionKey,
					Path.GetFullPath(ManifestRelativePath));
			string canonicalPath =
				Path.GetFullPath(ManifestRelativePath);
			var candidatePaths = new List<string>(6);

			void AddCandidate(string candidate)
			{
				if (string.IsNullOrWhiteSpace(candidate) ||
				    candidatePaths.Contains(
					    candidate,
					    StringComparer.Ordinal))
				{
					return;
				}

				candidatePaths.Add(candidate);
			}

			// A valid temporary file is newer than the live manifest and means a
			// previous atomic replacement was interrupted.
			if (!string.IsNullOrWhiteSpace(sessionPath))
				AddCandidate(sessionPath + ".tmp");
			AddCandidate(canonicalPath + ".tmp");
			AddCandidate(sessionPath);
			AddCandidate(canonicalPath);
			if (!string.IsNullOrWhiteSpace(sessionPath))
				AddCandidate(sessionPath + ".bak");
			AddCandidate(canonicalPath + ".bak");

			if (TryReadFirstValidManifest(
				    candidatePaths,
				    out DiagnosticRunManifest recoveredManifest,
				    out string recoveredPath,
				    out string recoveryFailures))
			{
				activeManifest = recoveredManifest;
				if (!string.Equals(
					    recoveredPath,
					    canonicalPath,
					    StringComparison.Ordinal))
				{
					PersistManifest(recoveredManifest);
				}
				else
				{
					SessionState.SetString(
						ManifestSessionKey,
						canonicalPath);
				}

				TryDeleteFileWithoutThrow(canonicalPath + ".tmp");
				TryDeleteFileWithoutThrow(canonicalPath + ".bak");
				return activeManifest;
			}

			if (!string.IsNullOrEmpty(recoveryFailures))
			{
				Debug.LogError(
					$"{LogPrefix} Could not recover any persisted manifest. " +
					recoveryFailures);
			}

			SessionState.EraseString(ManifestSessionKey);
			activeManifest = null;
			return null;
		}

		internal static bool TryReadFirstValidManifest(
			IEnumerable<string> candidatePaths,
			out DiagnosticRunManifest recoveredManifest,
			out string recoveredPath,
			out string failureSummary)
		{
			recoveredManifest = null;
			recoveredPath = null;
			var failures = new List<string>();
			if (candidatePaths == null)
			{
				failureSummary = string.Empty;
				return false;
			}

			foreach (string path in candidatePaths)
			{
				if (string.IsNullOrWhiteSpace(path) ||
				    !File.Exists(path))
				{
					continue;
				}

				try
				{
					DiagnosticRunManifest candidate =
						JsonUtility.FromJson<DiagnosticRunManifest>(
							File.ReadAllText(path));
					if (candidate == null ||
					    string.IsNullOrWhiteSpace(candidate.runId))
					{
						throw new InvalidDataException(
							"The persisted diagnostic manifest is invalid.");
					}

					recoveredManifest = candidate;
					recoveredPath = path;
					failureSummary = string.Join(" | ", failures);
					return true;
				}
				catch (Exception ex)
				{
					failures.Add($"'{path}': {ex.Message}");
				}
			}

			failureSummary = string.Join(" | ", failures);
			return false;
		}

		private static void PersistManifest(
			DiagnosticRunManifest manifest)
		{
			if (manifest == null)
				return;
			EnsureLibraryFolder();
			string path = Path.GetFullPath(ManifestRelativePath);
			string temporaryPath = path + ".tmp";
			string backupPath = path + ".bak";
			File.WriteAllText(
				temporaryPath,
				JsonUtility.ToJson(manifest, prettyPrint: true));
			if (File.Exists(path))
			{
				TryDeleteFileWithoutThrow(backupPath);
				try
				{
					File.Replace(
						temporaryPath,
						path,
						backupPath);
				}
				catch (NotSupportedException)
				{
					// The temporary file remains a complete recovery source if
					// this non-atomic compatibility fallback is interrupted.
					File.Copy(temporaryPath, path, overwrite: true);
					File.Delete(temporaryPath);
				}
			}
			else
			{
				File.Move(temporaryPath, path);
			}
			TryDeleteFileWithoutThrow(backupPath);
			SessionState.SetString(ManifestSessionKey, path);
			activeManifest = manifest;
			RepaintWindows();
		}

		private static void DeleteManifestFiles()
		{
			TryDeleteFileWithoutThrow(
				Path.GetFullPath(ManifestRelativePath));
			TryDeleteFileWithoutThrow(
				Path.GetFullPath(ManifestRelativePath) + ".tmp");
			TryDeleteFileWithoutThrow(
				Path.GetFullPath(ManifestRelativePath) + ".bak");
			SessionState.EraseString(ManifestSessionKey);
		}

		private static void EnsureLibraryFolder()
		{
			Directory.CreateDirectory(
				Path.GetFullPath(LibraryFolder));
		}

		private static string GetResultPath(string runId)
		{
			return Path.GetFullPath(
				$"{LibraryFolder}/Result-{runId}.json");
		}

		private static bool HasTimedOut(
			DiagnosticRunManifest manifest)
		{
			if (manifest.startedUtcTicks <= 0 ||
			    manifest.timeoutSeconds <= 0)
				return false;
			var started = new DateTime(
				manifest.startedUtcTicks,
				DateTimeKind.Utc);
			return DateTime.UtcNow - started >
			       TimeSpan.FromSeconds(manifest.timeoutSeconds);
		}

		private static bool IsMode(
			DiagnosticRunManifest manifest,
			DiagnosticRunMode mode)
		{
			return string.Equals(
				manifest?.mode,
				mode.ToString(),
				StringComparison.Ordinal);
		}

		private static bool IsPhase(
			DiagnosticRunManifest manifest,
			DiagnosticRunPhase phase)
		{
			return string.Equals(
				manifest?.phase,
				phase.ToString(),
				StringComparison.Ordinal);
		}

		private static string AppendFailure(
			string current,
			string addition)
		{
			if (string.IsNullOrWhiteSpace(addition))
				return current;
			return string.IsNullOrWhiteSpace(current)
				? addition
				: current + Environment.NewLine + addition;
		}

		private static void TryCancelRuntimeCapture()
		{
			if (!Application.isPlaying)
				return;
			try
			{
				SaveDataCollectionDuplicateDiagnostic
					.CancelNextRealSaveCapture();
			}
			catch (Exception ex)
			{
				Debug.LogWarning(
					$"{LogPrefix} Could not cancel the runtime capture hook: {ex.Message}");
			}
		}

		private static bool TryResolveActiveAssets(
			out SaveSettings settings,
			out PrefabRegistry registry,
			out string settingsPath,
			out string registryPath,
			out string error)
		{
			settings = null;
			registry = null;
			settingsPath = "<unavailable>";
			registryPath = "<unavailable>";
			error = null;
			try
			{
				settings =
					CrystalSaveOverrides.GetOverride<SaveSettings>() ??
					AssetProvider.Load<SaveSettings>("SaveSettings");
				registry = PrefabAssetResolver.LoadActivePrefabRegistry();
				if (settings == null)
				{
					error =
						"No runtime-loadable SaveSettings asset is active. Complete the Crystal Save Settings Wizard before running this diagnostic.";
					return false;
				}

				if (registry == null)
				{
					error =
						"No active PrefabRegistry could be resolved. Complete the Crystal Save Settings Wizard before running this diagnostic.";
					return false;
				}

				settingsPath = DescribeAsset(settings);
				registryPath = DescribeAsset(registry);
				return true;
			}
			catch (Exception ex)
			{
				error =
					$"Could not resolve active Crystal Save assets: {ex.Message}";
				return false;
			}
		}

		private static string DescribeAsset(Object asset)
		{
			if (asset == null)
				return "<none>";
			string path = AssetDatabase.GetAssetPath(asset);
			return string.IsNullOrWhiteSpace(path)
				? $"<in-memory> {asset.name}"
				: path;
		}

		private static void CreateDiagnosticScene(
			DiagnosticRunManifest manifest,
			SaveSettings activeSettings,
			PrefabRegistry activeRegistry)
		{
			if (manifest == null ||
			    activeSettings == null ||
			    activeRegistry == null)
			{
				throw new ArgumentNullException(
					nameof(manifest),
					"Diagnostic scene creation requires a manifest and resolved active assets.");
			}

			Scene previousActive = SceneManager.GetActiveScene();
			Scene diagnosticScene =
				EditorSceneManager.NewScene(
					NewSceneSetup.EmptyScene,
					NewSceneMode.Additive);
			diagnosticScene.name =
				"CSP_SaveDataCollectionDuplicateDiagnostic";
			SceneManager.SetActiveScene(diagnosticScene);

			try
			{
				using (var isolation = new EditorIdentityIsolationScope())
				{
					RememberTransform normal = CreateRememberRoot(
						"CSP_Diag_NormalGlobal",
						"CSP_DIAG_NORMAL_GO",
						"CSP_DIAG_SHARED_COMPONENT");
					RememberTransform sameComponentOtherRoot =
						CreateRememberRoot(
							"CSP_Diag_SameComponentOtherRoot",
							"CSP_DIAG_SAME_COMPONENT_OTHER_GO",
							"CSP_DIAG_SHARED_COMPONENT");

					SaveablePrefab blobPrefab = CreatePrefabFixture(
						"CSP_Diag_BlobPrefab",
						"CSP_DIAG_BLOB_INSTANCE",
						"CSP_DIAG_BLOB_ASSET",
						trackComponentBlobs: true,
						childName: "CSP_Diag_BlobHelper",
						componentId: "CSP_DIAG_BLOB_COMPONENT",
						childActive: true,
						out RememberTransform blobComponent);

					SaveablePrefab transitionPrefab = CreatePrefabFixture(
						"CSP_Diag_TransitionPrefab",
						"CSP_DIAG_TRANSITION_INSTANCE",
						"CSP_DIAG_TRANSITION_ASSET",
						trackComponentBlobs: false,
						childName: "CSP_Diag_TransitionHelper",
						componentId: "CSP_DIAG_TRANSITION_COMPONENT",
						childActive: true,
						out RememberTransform transitionComponent);

					RememberTransform collisionFirst = CreateRememberRoot(
						"CSP_Diag_CollisionFirst",
						"CSP_DIAG_COLLISION_GO",
						"CSP_DIAG_COLLISION_COMPONENT");
					RememberTransform collisionSecond = CreateRememberRoot(
						"CSP_Diag_CollisionSecond",
						"CSP_DIAG_COLLISION_GO",
						"CSP_DIAG_COLLISION_COMPONENT");

					SaveablePrefab bulkScanPrefab = CreatePrefabFixture(
						"CSP_Diag_BulkScanPrefab",
						"CSP_DIAG_BULK_SCAN_INSTANCE",
						"CSP_DIAG_BULK_SCAN_ASSET",
						trackComponentBlobs: true,
						childName: "CSP_Diag_InactiveBulkScanHelper",
						componentId: "CSP_DIAG_BULK_SCAN_COMPONENT",
						childActive: true,
						out RememberTransform bulkScanComponent);
					bulkScanComponent.enabled = false;

					GameObject nestedOuterObject =
						new GameObject("CSP_Diag_NestedOuterPrefab");
					SaveablePrefab nestedOuterPrefab =
						nestedOuterObject.AddComponent<SaveablePrefab>();
					ConfigureSaveablePrefab(
						nestedOuterPrefab,
						"CSP_DIAG_NESTED_OUTER_INSTANCE",
						"CSP_DIAG_NESTED_OUTER_ASSET",
						trackComponentBlobs: true);

					GameObject nestedInnerObject =
						new GameObject("CSP_Diag_NestedInnerPrefab");
					nestedInnerObject.transform.SetParent(
						nestedOuterObject.transform,
						worldPositionStays: false);
					SaveablePrefab nestedInnerPrefab =
						nestedInnerObject.AddComponent<SaveablePrefab>();
					ConfigureSaveablePrefab(
						nestedInnerPrefab,
						"CSP_DIAG_NESTED_INNER_INSTANCE",
						"CSP_DIAG_NESTED_INNER_ASSET",
						trackComponentBlobs: true);
					GameObject nestedHelper =
						new GameObject("CSP_Diag_NestedHelper");
					nestedHelper.transform.SetParent(
						nestedInnerObject.transform,
						worldPositionStays: false);
					RememberTransform nestedComponent =
						CreateRememberComponent(
							nestedHelper,
							"CSP_DIAG_NESTED_INNER_INSTANCE",
							"CSP_DIAG_NESTED_COMPONENT");

					GameObject driverObject =
						new GameObject(
							"CSP_SaveDataCollectionDuplicateDiagnosticDriver");
					SaveDataCollectionDuplicateDiagnostic driver =
						driverObject.AddComponent<
							SaveDataCollectionDuplicateDiagnostic>();
					SerializedObject serializedDriver =
						new SerializedObject(driver);
					SetBool(serializedDriver, "runOnStart", true);
					SetString(serializedDriver, "runId", manifest.runId);
					SetString(
						serializedDriver,
						"resultFilePath",
						manifest.resultFilePath);
					SetInt(
						serializedDriver,
						"managerTimeoutFrames",
						900);
					SetObject(
						serializedDriver,
						"normalComponent",
						normal);
					SetObject(
						serializedDriver,
						"sameComponentIdOtherRoot",
						sameComponentOtherRoot);
					SetObject(serializedDriver, "blobPrefab", blobPrefab);
					SetObject(
						serializedDriver,
						"blobComponent",
						blobComponent);
					SetObject(
						serializedDriver,
						"transitionPrefab",
						transitionPrefab);
					SetObject(
						serializedDriver,
						"transitionComponent",
						transitionComponent);
					SetObject(
						serializedDriver,
						"collisionFirst",
						collisionFirst);
					SetObject(
						serializedDriver,
						"collisionSecond",
						collisionSecond);
					SetObject(
						serializedDriver,
						"bulkScanPrefab",
						bulkScanPrefab);
					SetObject(
						serializedDriver,
						"bulkScanComponent",
						bulkScanComponent);
					SetObject(
						serializedDriver,
						"nestedOuterPrefab",
						nestedOuterPrefab);
					SetObject(
						serializedDriver,
						"nestedInnerPrefab",
						nestedInnerPrefab);
					SetObject(
						serializedDriver,
						"nestedComponent",
						nestedComponent);
					serializedDriver.ApplyModifiedPropertiesWithoutUndo();

					EditorSceneManager.MarkSceneDirty(diagnosticScene);
					if (!EditorSceneManager.SaveScene(
						    diagnosticScene,
						    ScenePath,
						    saveAsCopy: false))
					{
						throw new IOException(
							$"Unity could not save diagnostic scene '{ScenePath}'.");
					}

					PrefabRegistryValidationCoordinator.CancelPending(
						isolation.Registry);
					SaveablePrefab
						.RemovePrefabRegistrySyncsForEditorDiagnostics(
							WorkingFolder);
				}
			}
			finally
			{
				if (diagnosticScene.IsValid() &&
				    diagnosticScene.isLoaded)
				{
					EditorSceneManager.CloseScene(
						diagnosticScene,
						removeScene: true);
				}
				if (previousActive.IsValid() &&
				    previousActive.isLoaded)
				{
					SceneManager.SetActiveScene(previousActive);
				}
			}

			AssetDatabase.ImportAsset(
				ScenePath,
				ImportAssetOptions.ForceSynchronousImport);
		}

		private static RememberTransform CreateRememberRoot(
			string name,
			string gameObjectId,
			string componentId)
		{
			GameObject root = new GameObject(name);
			UniqueID uniqueId = root.AddComponent<UniqueID>();
			uniqueId.ID = gameObjectId;
			return CreateRememberComponent(
				root,
				gameObjectId,
				componentId);
		}

		private static SaveablePrefab CreatePrefabFixture(
			string name,
			string instanceId,
			string assetId,
			bool trackComponentBlobs,
			string childName,
			string componentId,
			bool childActive,
			out RememberTransform component)
		{
			GameObject root = new GameObject(name);
			SaveablePrefab prefab =
				root.AddComponent<SaveablePrefab>();
			ConfigureSaveablePrefab(
				prefab,
				instanceId,
				assetId,
				trackComponentBlobs);

			GameObject child = new GameObject(childName);
			child.transform.SetParent(
				root.transform,
				worldPositionStays: false);
			component = CreateRememberComponent(
				child,
				instanceId,
				componentId);
			child.SetActive(childActive);
			return prefab;
		}

		private static RememberTransform CreateRememberComponent(
			GameObject target,
			string gameObjectId,
			string componentId)
		{
			RememberTransform component =
				target.AddComponent<RememberTransform>();
			SerializedObject serialized =
				new SerializedObject(component);
			SetString(serialized, "uniqueID", gameObjectId);
			SetString(serialized, "componentID", componentId);
			SetBool(serialized, "unregisterOnDisable", true);
			SetBool(serialized, "skipSavingWhenUnchanged", false);

			ConfigureTransformProperty(
				serialized.FindProperty("rememberPositionSettings"),
				save: true);
			ConfigureTransformProperty(
				serialized.FindProperty("rememberRotationSettings"),
				save: false);
			ConfigureTransformProperty(
				serialized.FindProperty("rememberScaleSettings"),
				save: false);
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return component;
		}

		private static void ConfigureTransformProperty(
			SerializedProperty property,
			bool save)
		{
			if (property == null)
				return;
			SerializedProperty saveProperty =
				property.FindPropertyRelative("Save");
			SerializedProperty spaceProperty =
				property.FindPropertyRelative("SpaceMode");
			if (saveProperty != null)
				saveProperty.boolValue = save;
			if (spaceProperty != null)
				spaceProperty.enumValueIndex = (int)SpaceMode.Local;
		}

		private static void ConfigureSaveablePrefab(
			SaveablePrefab prefab,
			string instanceId,
			string assetId,
			bool trackComponentBlobs)
		{
			SerializedObject serialized =
				new SerializedObject(prefab);
			SetString(serialized, "uniqueID", instanceId);
			SetString(serialized, "prefabAssetID", assetId);
			SetBool(serialized, "RegisterWithSaveSystem", true);
			SetBool(
				serialized,
				"trackComponentBlobs",
				trackComponentBlobs);
			SetBool(serialized, "trackChildStateOverrides", false);
			SetBool(
				serialized,
				"trackChildTransformOverrides",
				false);
			SetBool(serialized, "trackAddedComponents", false);
			SetBool(serialized, "skipSavingWhenUnchanged", false);
			SetBool(serialized, "enableBatchRegistration", false);
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SetString(
			SerializedObject serialized,
			string propertyName,
			string value)
		{
			SerializedProperty property =
				serialized.FindProperty(propertyName);
			if (property == null)
				throw new MissingFieldException(
					serialized.targetObject.GetType().FullName,
					propertyName);
			property.stringValue = value;
		}

		private static void SetBool(
			SerializedObject serialized,
			string propertyName,
			bool value)
		{
			SerializedProperty property =
				serialized.FindProperty(propertyName);
			if (property == null)
				throw new MissingFieldException(
					serialized.targetObject.GetType().FullName,
					propertyName);
			property.boolValue = value;
		}

		private static void SetInt(
			SerializedObject serialized,
			string propertyName,
			int value)
		{
			SerializedProperty property =
				serialized.FindProperty(propertyName);
			if (property == null)
				throw new MissingFieldException(
					serialized.targetObject.GetType().FullName,
					propertyName);
			property.intValue = value;
		}

		private static void SetObject(
			SerializedObject serialized,
			string propertyName,
			Object value)
		{
			SerializedProperty property =
				serialized.FindProperty(propertyName);
			if (property == null)
				throw new MissingFieldException(
					serialized.targetObject.GetType().FullName,
					propertyName);
			property.objectReferenceValue = value;
		}

		private static bool TryWriteReport(
			DiagnosticRunManifest manifest,
			bool importAsset)
		{
			TryReadRuntimeResult(
				manifest,
				out SaveDataCollectionDuplicateDiagnosticResult result);
			return TryWriteReport(manifest, result, importAsset);
		}

		private static bool TryWriteReport(
			DiagnosticRunManifest manifest,
			SaveDataCollectionDuplicateDiagnosticResult runtimeResult,
			bool importAsset)
		{
			try
			{
				if (importAsset)
					EnsureFolder(RootFolder);
				else
					Directory.CreateDirectory(
						Path.GetFullPath(RootFolder));

				File.WriteAllText(
					Path.GetFullPath(ReportPath),
					BuildReportText(manifest, runtimeResult));
				if (importAsset)
				{
					AssetDatabase.ImportAsset(
						ReportPath,
						ImportAssetOptions.ForceSynchronousImport);
				}
				return true;
			}
			catch (Exception ex)
			{
				manifest.failure = AppendFailure(
					manifest.failure,
					$"Report generation failed: {ex}");
				Debug.LogError(
					$"{LogPrefix} Could not write report: {ex}");
				return false;
			}
		}

		internal static string BuildReportText(
			DiagnosticRunManifest manifest,
			SaveDataCollectionDuplicateDiagnosticResult runtimeResult)
		{
			if (manifest == null)
				throw new ArgumentNullException(nameof(manifest));

			bool passed =
				string.IsNullOrEmpty(manifest.failure) &&
				manifest.cleanupErrors.Count == 0 &&
				manifest.sceneSetupRestored &&
				manifest.workingAssetsRemoved &&
				manifest.resultFileRemoved &&
				RuntimeStagesPassed(runtimeResult);
			var report = new StringBuilder();
			report.AppendLine(
				"# Crystal Save SaveData Collection Duplicate Diagnostic");
			report.AppendLine();
			report.AppendLine(
				$"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
			report.AppendLine($"Unity: {Application.unityVersion}");
			report.AppendLine($"Platform: {Application.platform}");
			report.AppendLine($"Run ID: `{Markdown(manifest.runId)}`");
			report.AppendLine(
				$"Mode: {Markdown(ReadableMode(manifest.mode))}");
			report.AppendLine(
				$"Overall result: **{(passed ? "PASS" : "FAIL")}**");
			report.AppendLine();

			report.AppendLine("## Isolation");
			report.AppendLine();
			report.AppendLine(
				$"- Active SaveSettings (read only): `{Markdown(manifest.activeSettingsPath)}`");
			report.AppendLine(
				$"- Active PrefabRegistry (read only): `{Markdown(manifest.activeRegistryPath)}`");
			if (IsMode(
				    manifest,
				    DiagnosticRunMode.CaptureNextRealSave))
			{
				report.AppendLine(
					"- Save-slot activity: **Determined by the captured user save**. " +
					"The diagnostic only observes that save; it does not initiate, suppress, " +
					"or redirect its slot write.");
			}
			else
			{
				report.AppendLine(
					"- Save slots written: **0** (the isolated suite calls `CollectSaveData()` only)");
			}
			report.AppendLine("- Build Settings changed: **No**");
			report.AppendLine(
				$"- Original scene setup restored: **{PassFail(manifest.sceneSetupRestored)}**");
			report.AppendLine(
				$"- Temporary working assets removed: **{PassFail(manifest.workingAssetsRemoved)}**");
			report.AppendLine(
				$"- Runtime result JSON removed: **{PassFail(manifest.resultFileRemoved)}**");
			report.AppendLine();

			if (runtimeResult != null)
			{
				List<SaveDataCollectionDuplicateStageResult> stages =
					runtimeResult.stages ??
					new List<SaveDataCollectionDuplicateStageResult>();
				report.AppendLine("## Stage Summary");
				report.AppendLine();
				report.AppendLine(
					"| Result | Stage | Unique keys | Duplicate keys | Global payloads | Prefab payloads | Target blobs | Trace events |");
				report.AppendLine(
					"|---|---|---:|---:|---:|---:|---:|---:|");
				foreach (SaveDataCollectionDuplicateStageResult stage in stages)
				{
					report.AppendLine(
						$"| {(stage.passed ? "PASS" : "FAIL")} | {Markdown(stage.name)} | " +
						$"{stage.uniqueKeyCount} | {stage.duplicateCount} | " +
						$"{stage.globalComponentEntryCount} | {stage.prefabEntryCount} | " +
						$"{stage.targetPrefabBlobCount} | {stage.calls?.Count ?? 0} |");
				}
				report.AppendLine();

				foreach (SaveDataCollectionDuplicateStageResult stage in stages)
					AppendStageReport(report, stage);
			}
			else
			{
				report.AppendLine("## Runtime Result");
				report.AppendLine();
				report.AppendLine(
					"No valid runtime result was available for this run.");
				report.AppendLine();
			}

			report.AppendLine("## Cleanup and Errors");
			report.AppendLine();
			report.AppendLine(
				$"- Cleanup errors: {manifest.cleanupErrors.Count}");
			foreach (string cleanupError in manifest.cleanupErrors)
				report.AppendLine($"  - {Markdown(cleanupError)}");
			if (!string.IsNullOrWhiteSpace(manifest.failure))
			{
				report.AppendLine();
				report.AppendLine("### Coordinator error");
				report.AppendLine();
				AppendCodeBlock(report, manifest.failure);
			}
			if (!string.IsNullOrWhiteSpace(runtimeResult?.fatalError))
			{
				report.AppendLine();
				report.AppendLine("### Runtime error");
				report.AppendLine();
				AppendCodeBlock(report, runtimeResult.fatalError);
			}

			return NormalizeLineEndings(report.ToString());
		}

		private static bool RuntimeStagesPassed(
			SaveDataCollectionDuplicateDiagnosticResult runtimeResult)
		{
			List<SaveDataCollectionDuplicateStageResult> stages =
				runtimeResult?.stages;
			return runtimeResult != null &&
			       string.IsNullOrEmpty(runtimeResult.fatalError) &&
			       stages != null &&
			       stages.Count > 0 &&
			       stages.All(stage => stage != null && stage.passed);
		}

		private static void AppendStageReport(
			StringBuilder report,
			SaveDataCollectionDuplicateStageResult stage)
		{
			report.AppendLine(
				$"## {(stage.passed ? "PASS" : "FAIL")} — {Markdown(stage.name)}");
			report.AppendLine();
			report.AppendLine(
				$"- Operation: `{Markdown(stage.operationName)}`");
			report.AppendLine(
				$"- Expectation: {Markdown(stage.expectation)}");
			report.AppendLine($"- Result: {Markdown(stage.details)}");
			report.AppendLine(
				$"- Unique/duplicate keys: {stage.uniqueKeyCount}/{stage.duplicateCount}");
			report.AppendLine(
				$"- Global/prefab payloads: {stage.globalComponentEntryCount}/{stage.prefabEntryCount}");
			report.AppendLine(
				$"- Target prefab component blobs: {stage.targetPrefabBlobCount}");
			if (stage.utilityRegisteredComponents != 0 ||
			    stage.utilityPrefabRegistered)
			{
				report.AppendLine(
					$"- Registration utility: components={stage.utilityRegisteredComponents}, prefab={stage.utilityPrefabRegistered}");
			}
			if (stage.nestedOuterBlobCount != 0 ||
			    stage.nestedInnerBlobCount != 0 ||
			    stage.restoreVerified)
			{
				report.AppendLine(
					$"- Nested blobs: outer={stage.nestedOuterBlobCount}, inner={stage.nestedInnerBlobCount}, restored={stage.restoreVerified}");
			}
			if (stage.intentionalExceptionObserved)
				report.AppendLine("- Intentional exception observed: Yes");

			List<string> keys =
				stage.globalComponentKeys ?? new List<string>();
			report.AppendLine(
				$"- Global keys: {(keys.Count == 0 ? "_none_" : string.Join(", ", keys.Select(key => $"`{Markdown(key)}`")))}");
			report.AppendLine();

			List<string> untracedDuplicateKeys =
				stage.untracedDuplicateKeys ?? new List<string>();
			if (untracedDuplicateKeys.Count > 0)
			{
				report.AppendLine("### Untraced collection-key collisions");
				report.AppendLine();
				report.AppendLine(
					"These keys collided during collection, but at least one writer was not a " +
					"`SaveableComponent` trace event. The generic ComponentManager warning in the " +
					"Console contains the available caller context:");
				foreach (string key in untracedDuplicateKeys)
					report.AppendLine($"- `{Markdown(key)}`");
				report.AppendLine();
			}

			List<SaveDataCollectionDuplicateOccurrenceResult> calls =
				stage.calls ??
				new List<SaveDataCollectionDuplicateOccurrenceResult>();
			report.AppendLine("### Ordered collection trace events and ownership routes");
			report.AppendLine();
			if (calls.Count == 0)
			{
				report.AppendLine("_No deep-traced collection events were captured._");
			}
			else
			{
				report.AppendLine(
					"| # | Composite key | Route | Component | Hierarchy | Scene path | GameObject ID | Component ID | Unity ID | Nearest prefab (name / instance / asset) | Route owner (name / instance / asset) | Blob | Global | Registration source |");
				report.AppendLine(
					"|---:|---|---|---|---|---|---|---|---:|---|---|:---:|:---:|---|");
				foreach (SaveDataCollectionDuplicateOccurrenceResult call in
				         calls.OrderBy(call => call.callOrder))
				{
					string nearest =
						$"{call.nearestPrefabName} / {call.nearestPrefabInstanceId} / {call.nearestPrefabAssetId}";
					string routeOwner =
						$"{call.routeOwnerName} / {call.routeOwnerInstanceId} / {call.routeOwnerAssetId}";
					report.AppendLine(
						$"| {call.callOrder} | `{Markdown(call.compositeKey)}` | {Markdown(call.route)} | " +
						$"{Markdown(call.componentType)} | {Markdown(call.hierarchyPath)} | {Markdown(call.scenePath)} | " +
						$"`{Markdown(call.gameObjectId)}` | `{Markdown(call.componentId)}` | {call.unityObjectId} | " +
						$"{Markdown(nearest)} | {Markdown(routeOwner)} | " +
						$"{YesNo(call.blobOwned)} | {YesNo(call.globallyRegistered)} | " +
						$"{Markdown(call.registrationSource)} |");
				}
			}
			report.AppendLine();

			List<SaveDataCollectionDuplicateEntryResult> duplicates =
				stage.duplicates ??
				new List<SaveDataCollectionDuplicateEntryResult>();
			report.AppendLine("### Duplicate attribution");
			report.AppendLine();
			if (duplicates.Count == 0)
			{
				report.AppendLine(
					untracedDuplicateKeys.Count == 0
						? "_No duplicate keys._"
						: "_No duplicate key had two fully attributed SaveableComponent trace events._");
				report.AppendLine();
				return;
			}

			foreach (SaveDataCollectionDuplicateEntryResult duplicate in duplicates)
			{
				report.AppendLine(
					$"#### `{Markdown(duplicate.compositeKey)}` — {duplicate.totalCalls} collection events");
				report.AppendLine();
				string classification = ClassifyDuplicate(duplicate);
				report.AppendLine(
					$"- Classification: **{Markdown(classification)}**");
				report.AppendLine(
					$"- Recommendation: {Markdown(RecommendationForDuplicate(classification))}");
				report.AppendLine();
				report.AppendLine(
					"| Caller | Unity ID | GO ID | Component ID | Events | Scene path | Nearest prefab IDs | Blob | Global | Registration source | Routes |");
				report.AppendLine(
					"|---|---:|---|---|---:|---|---|:---:|:---:|---|---|");
				foreach (SaveDataCollectionDuplicateCallerResult caller in
				         duplicate.callers ??
				         new List<SaveDataCollectionDuplicateCallerResult>())
				{
					report.AppendLine(
						$"| {Markdown(caller.hierarchyPath)} ({Markdown(caller.componentType)}) | " +
						$"{caller.unityObjectId} | `{Markdown(caller.gameObjectId)}` | `{Markdown(caller.componentId)}` | " +
						$"{caller.callCount} | {Markdown(caller.scenePath)} | " +
						$"{Markdown(caller.nearestPrefabName)} / `{Markdown(caller.nearestPrefabInstanceId)}` / `{Markdown(caller.nearestPrefabAssetId)}` | " +
						$"{YesNo(caller.blobOwned)} | {YesNo(caller.globallyRegistered)} | " +
						$"{Markdown(caller.registrationSource)} | {Markdown(caller.routeSummary)} |");
				}
				report.AppendLine();

				foreach (SaveDataCollectionDuplicateOccurrenceResult occurrence in
				         (duplicate.occurrences ??
				          new List<SaveDataCollectionDuplicateOccurrenceResult>())
				         .OrderBy(item => item.callOrder))
				{
					report.AppendLine(
						$"- Event {occurrence.callOrder}: route **{Markdown(occurrence.route)}**, " +
						$"caller `{Markdown(occurrence.hierarchyPath)}`, " +
						$"route owner `{Markdown(occurrence.routeOwnerName)}` " +
						$"(`{Markdown(occurrence.routeOwnerInstanceId)}` / `{Markdown(occurrence.routeOwnerAssetId)}`)");
					if (!string.IsNullOrWhiteSpace(
						    occurrence.stackTrace))
					{
						report.AppendLine();
						report.AppendLine(
							$"  Targeted stack for event {occurrence.callOrder}:");
						report.AppendLine();
						AppendCodeBlock(
							report,
							occurrence.stackTrace);
					}
				}
				report.AppendLine();
			}
		}

		private static string ClassifyDuplicate(
			SaveDataCollectionDuplicateEntryResult duplicate)
		{
			List<SaveDataCollectionDuplicateOccurrenceResult> occurrences =
				duplicate?.occurrences ??
				new List<SaveDataCollectionDuplicateOccurrenceResult>();
			int distinctInstances =
				(duplicate?.callers ??
				 new List<SaveDataCollectionDuplicateCallerResult>())
				.Select(caller => caller.unityObjectId)
				.Distinct()
				.Count();
			if (distinctInstances > 1)
				return "Copied identity collision across instances";

			if (occurrences.Any(
				    occurrence =>
					    string.Equals(
						    occurrence.route,
						    "DeferredGlobalPayload",
						    StringComparison.Ordinal) ||
					    string.Equals(
						    occurrence.route,
						    "PendingGlobalPayload",
						    StringComparison.Ordinal)))
			{
				return "Preserved payload key collision";
			}

			if (occurrences.Any(
				    occurrence => string.Equals(
					    occurrence.route,
					    "CustomOrReentrant",
					    StringComparison.Ordinal)))
			{
				return "Custom/re-entrant invocation";
			}

			int distinctRoutes = occurrences
				.Select(occurrence => occurrence.route)
				.Distinct(StringComparer.Ordinal)
				.Count();
			if (distinctRoutes > 1 ||
			    occurrences.Any(item => item.blobOwned) &&
			    occurrences.Any(item => item.globallyRegistered))
			{
				return "Same-instance ownership overlap";
			}

			return "Same-instance repeated invocation";
		}

		private static string RecommendationForDuplicate(
			string classification)
		{
			switch (classification)
			{
				case "Copied identity collision across instances":
					return
						"Assign a fresh GameObject/instance ID to runtime copies. Prefer SaveablePrefabFactory for runtime prefab creation so each instance receives a fresh identity; do not raw-clone a live saveable identity.";
				case "Same-instance ownership overlap":
					return
						"Keep the helper on exactly one collection route. If its nearest SaveablePrefab has Track Component Blobs enabled, remove its global ComponentManager registration; otherwise keep it global and disable blob ownership.";
				case "Custom/re-entrant invocation":
					return
						"Start with the targeted stack below and remove the nested or custom SaveData call from serialization callbacks. A component should emit data once per collection operation.";
				case "Preserved payload key collision":
					return
						"Inspect the event order and pending/deferred ownership state. Preserved bytes bypass SaveData, so make sure this composite key is reached by only one live component and one collection entry.";
				default:
					return
						"Inspect the ordered call sites and targeted stacks below; the same live component was invoked repeatedly through one route.";
			}
		}

		private static void AppendCodeBlock(
			StringBuilder report,
			string text)
		{
			report.AppendLine("```text");
			report.AppendLine(
				(text ?? string.Empty).Replace("```", "'''"));
			report.AppendLine("```");
		}

		private static string Markdown(string value)
		{
			return string.IsNullOrEmpty(value)
				? "<none>"
				: value.Replace("|", "\\|")
					.Replace("\r", " ")
					.Replace("\n", " ");
		}

		private static string YesNo(bool value)
		{
			return value ? "Yes" : "No";
		}

		private static string PassFail(bool value)
		{
			return value ? "PASS" : "FAIL";
		}

		private static string ReadableMode(string mode)
		{
			return string.Equals(
				mode,
				DiagnosticRunMode.CaptureNextRealSave.ToString(),
				StringComparison.Ordinal)
				? "Capture Next Real Save"
				: "Isolated Play Mode Suite";
		}

		private static string NormalizeLineEndings(string value)
		{
			return value.Replace("\r\n", "\n")
				.Replace("\n", Environment.NewLine);
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;
			string parent =
				Path.GetDirectoryName(path)?.Replace("\\", "/");
			if (!string.IsNullOrWhiteSpace(parent) &&
			    !AssetDatabase.IsValidFolder(parent))
				EnsureFolder(parent);
			string folder = Path.GetFileName(path);
			if (!string.IsNullOrWhiteSpace(parent) &&
			    !string.IsNullOrWhiteSpace(folder))
				AssetDatabase.CreateFolder(parent, folder);
		}

		private static void DeleteWorkingFolderWithAssetDatabase()
		{
			if (AssetDatabase.IsValidFolder(WorkingFolder) &&
			    !AssetDatabase.DeleteAsset(WorkingFolder))
			{
				throw new IOException(
					$"Could not delete '{WorkingFolder}'.");
			}
		}

		private static bool TryDeleteWorkingFolderWithoutAssetDatabase(
			List<string> errors)
		{
			try
			{
				string fullPath = Path.GetFullPath(WorkingFolder);
				if (Directory.Exists(fullPath))
					Directory.Delete(fullPath, recursive: true);
				DeleteFileIfPresent(fullPath + ".meta");
				return !Directory.Exists(fullPath) &&
				       !File.Exists(fullPath + ".meta");
			}
			catch (Exception ex)
			{
				errors?.Add(
					$"Raw working-folder cleanup failed: {ex}");
				return false;
			}
		}

		private static void DeleteFileIfPresent(string path)
		{
			if (!string.IsNullOrWhiteSpace(path) &&
			    File.Exists(path))
				File.Delete(path);
		}

		private static void TryDeleteFileWithoutThrow(string path)
		{
			try
			{
				DeleteFileIfPresent(path);
			}
			catch
			{
			}
		}

		private static void FailStart(string message)
		{
			Debug.LogError($"{LogPrefix} {message}");
			if (!Application.isBatchMode)
			{
				EditorUtility.DisplayDialog(
					"Crystal Save Diagnostic",
					message,
					"OK");
			}
		}

		private static void OpenReport()
		{
			AssetDatabase.Refresh(
				ImportAssetOptions.ForceSynchronousImport);
			TextAsset report =
				AssetDatabase.LoadAssetAtPath<TextAsset>(ReportPath);
			if (report != null)
				AssetDatabase.OpenAsset(report);
		}

		private static void RepaintWindows()
		{
			foreach (SaveDataCollectionDuplicateDiagnosticBuilder window in
			         Resources.FindObjectsOfTypeAll<
				         SaveDataCollectionDuplicateDiagnosticBuilder>())
			{
				window.Repaint();
			}
		}

		private sealed class EditorIdentityIsolationScope : IDisposable
		{
			private readonly FieldInfo cachedField;
			private readonly CrystalSaveAssetOverrides previous;
			private readonly CrystalSaveAssetOverrides diagnosticOverride;
			private readonly SaveSettings settings;
			private bool disposed;

			internal EditorIdentityIsolationScope()
			{
				cachedField = typeof(CrystalSaveOverrides).GetField(
					"cached",
					BindingFlags.Static | BindingFlags.NonPublic);
				if (cachedField == null)
					throw new MissingFieldException(
						typeof(CrystalSaveOverrides).FullName,
						"cached");

				previous =
					cachedField.GetValue(null) as
						CrystalSaveAssetOverrides;
				settings =
					CreateInstance<SaveSettings>();
				settings.hideFlags = HideFlags.HideAndDontSave;
				settings.editorAutomationSettingsInitialized = true;
				settings.autoRegisterPrefabs = false;
				settings.enablePrefabRegistryAutoValidation = false;
				Registry = CreateInstance<PrefabRegistry>();
				Registry.hideFlags = HideFlags.HideAndDontSave;
				diagnosticOverride =
					CreateInstance<CrystalSaveAssetOverrides>();
				diagnosticOverride.hideFlags =
					HideFlags.HideAndDontSave;
				diagnosticOverride.saveSettings = settings;
				diagnosticOverride.prefabRegistry = Registry;
				cachedField.SetValue(null, diagnosticOverride);
				CrystalSaveEditorAutomationSettings.ClearCache();
				PrefabRegistryIdentityIndex.InvalidateAll();
			}

			internal PrefabRegistry Registry { get; }

			public void Dispose()
			{
				if (disposed)
					return;
				disposed = true;
				try
				{
					PrefabRegistryValidationCoordinator.CancelPending(
						Registry);
					cachedField.SetValue(null, previous);
					CrystalSaveEditorAutomationSettings.ClearCache();
					PrefabRegistryIdentityIndex.InvalidateAll();
				}
				finally
				{
					if (diagnosticOverride != null)
						DestroyImmediate(diagnosticOverride);
					if (Registry != null)
						DestroyImmediate(Registry);
					if (settings != null)
						DestroyImmediate(settings);
				}
			}
		}

		internal enum DiagnosticRunMode
		{
			IsolatedSuite,
			CaptureNextRealSave
		}

		internal enum DiagnosticRunPhase
		{
			Preparing,
			AwaitingPlayMode,
			Running,
			ExitingPlayMode,
			Finalizing
		}

		[Serializable]
		internal sealed class DiagnosticSceneSetup
		{
			public string path;
			public bool isLoaded;
			public bool isActive;
		}

		[Serializable]
		internal sealed class DiagnosticRunManifest
		{
			public string runId;
			public string mode;
			public string phase;
			public string status;
			public long startedUtcTicks;
			public int timeoutSeconds;
			public string resultFilePath;
			public string retainedRuntimeResultJson;
			public string activeSettingsPath;
			public string activeRegistryPath;
			public bool openReportAfterRun;
			public bool revealFolderAfterRun;
			public bool exitEditorWhenFinished;
			public bool cancelRequested;
			public string failure;
			public bool sceneSetupRestored;
			public bool workingAssetsRemoved;
			public bool resultFileRemoved;
			public List<DiagnosticSceneSetup> originalScenes = new();
			public List<string> cleanupErrors = new();
		}
	}
}
#endif
