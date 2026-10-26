#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	public sealed class PrefabRegistryIdentityDiagnosticBuilder : EditorWindow
	{
		private const string RootFolder = "Assets/CrystalSaveDiagnostics/PrefabRegistryIdentity";
		private const string PrefabsFolder = RootFolder + "/Prefabs";
		private const string ReportPath = RootFolder + "/PrefabRegistryIdentityReport.md";
		private const string BasePrefabPath = PrefabsFolder + "/CSP_IdentityDiagnosticBase.prefab";
		private const string VariantPrefabPath = PrefabsFolder + "/CSP_IdentityDiagnosticVariant.prefab";
		private const string DuplicatePrefabPath = PrefabsFolder + "/CSP_IdentityDiagnosticVariant_Copy.prefab";

		private static readonly ScenarioDefinition[] ScenarioDefinitions =
		{
			new ScenarioDefinition(
				"Registered original, auto-register off",
				"The original has an authoritative registry row before the Project-window style copy is imported with auto-registration disabled.",
				registerOriginal: true,
				autoRegisterDuringImport: false,
				disableBeforeDelayedCallbacks: false,
				expectDuplicateRegistration: false,
				unregisteredIdentityIsInformational: false),
			new ScenarioDefinition(
				"Registered original, auto-register on",
				"The original has an authoritative registry row and the duplicate is expected to receive a fresh ID and its own row.",
				registerOriginal: true,
				autoRegisterDuringImport: true,
				disableBeforeDelayedCallbacks: false,
				expectDuplicateRegistration: true,
				unregisteredIdentityIsInformational: false),
			new ScenarioDefinition(
				"Queued while on, disabled before delayed sync",
				"The duplicate imports while auto-registration is enabled, then the setting is disabled before editor delay calls are allowed to write.",
				registerOriginal: true,
				autoRegisterDuringImport: true,
				disableBeforeDelayedCallbacks: true,
				expectDuplicateRegistration: false,
				unregisteredIdentityIsInformational: false),
			new ScenarioDefinition(
				"Unregistered original, auto-register off",
				"Neither asset has a durable registry owner. The original must remain unchanged and no row may be added; a shared copied ID is reported as an identity-management warning.",
				registerOriginal: false,
				autoRegisterDuringImport: false,
				disableBeforeDelayedCallbacks: false,
				expectDuplicateRegistration: false,
				unregisteredIdentityIsInformational: true)
		};

		private bool openReportAfterRun = true;
		private bool revealFolderAfterRun;
		private int waitEditorTicks = 8;
		private string lastStatus = "Idle";
		private string lastReportPath = ReportPath;

		private static PendingRun pendingRun;

		[MenuItem("Tools/Crystal Save/Runtime Debug/Diagnostics/Prefab Registry Identity Diagnostic")]
		public static void ShowWindow()
		{
			PrefabRegistryIdentityDiagnosticBuilder window =
				GetWindow<PrefabRegistryIdentityDiagnosticBuilder>("Prefab Registry Identity");
			window.minSize = new Vector2(560f, 285f);
			window.Show();
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("Prefab Registry Identity Diagnostic", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"Audits the active PrefabRegistry and runs four isolated prefab-variant duplication scenarios. The diagnostic temporarily changes auto-registration and registry rows, then restores both from complete snapshots.",
				MessageType.Info);

			using (new EditorGUI.DisabledScope(pendingRun != null))
			{
				waitEditorTicks = Mathf.Clamp(EditorGUILayout.IntField("Editor ticks per scenario", waitEditorTicks), 1, 30);
				openReportAfterRun = EditorGUILayout.ToggleLeft("Open report after run", openReportAfterRun);
				revealFolderAfterRun = EditorGUILayout.ToggleLeft("Reveal output folder after run", revealFolderAfterRun);
			}

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
			EditorGUILayout.SelectableLabel(ReportPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
			EditorGUILayout.LabelField("Status", lastStatus);

			GUILayout.FlexibleSpace();

			using (new EditorGUI.DisabledScope(pendingRun != null))
			{
				if (GUILayout.Button("Run Prefab Registry Identity Diagnostic", GUILayout.Height(32f)))
					StartDiagnostic();
			}

			if (!string.IsNullOrEmpty(lastReportPath) && File.Exists(lastReportPath) &&
			    GUILayout.Button("Open Last Report"))
			{
				AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(lastReportPath));
			}
		}

		private void StartDiagnostic()
		{
			if (pendingRun != null)
			{
				EditorUtility.DisplayDialog(
					"Crystal Save Diagnostic",
					"A Prefab Registry Identity diagnostic is already running.",
					"OK");
				return;
			}

			PrefabRegistry registry = PrefabAssetResolver.LoadActivePrefabRegistry();
			if (registry == null)
			{
				EditorUtility.DisplayDialog(
					"Crystal Save Diagnostic",
					"No active PrefabRegistry could be found. Open Crystal Save Settings or create a PrefabRegistry first.",
					"OK");
				return;
			}

			SaveSettings settings = LoadActiveSaveSettings();
			PendingRun run = new PendingRun
			{
				Window = this,
				Registry = registry,
				SaveSettings = settings,
				RegistryPath = AssetDatabase.GetAssetPath(registry),
				SaveSettingsPath = settings != null ? AssetDatabase.GetAssetPath(settings) : null,
				InitialAutoRegisterPrefabs = settings != null && settings.autoRegisterPrefabs,
				InitialRows = PrefabRegistryIdentityUtility.AnalyzeRows(registry),
				RegistrySnapshot = SnapshotRegistry(registry),
				WaitTicksPerScenario = Mathf.Clamp(waitEditorTicks, 1, 30),
				OpenReportAfterRun = openReportAfterRun,
				RevealFolderAfterRun = revealFolderAfterRun,
				ScenarioIndex = -1
			};

			try
			{
				EnsureFolders();
				if (settings == null)
				{
					run.SkipControlledReproReason =
						"No active SaveSettings could be found, so the diagnostic could not safely control auto-registration.";
					FinishRun(run);
					return;
				}

				pendingRun = run;
				RegisterPendingCleanup();
				SetAutoRegisterPrefabs(settings, false);
				EnsureDiagnosticPrefabs(run);
				BeginNextScenario(run);
			}
			catch (Exception ex)
			{
				run.Failure = ex.ToString();
				FinishRun(run);
			}
		}

		private static void BeginNextScenario(PendingRun run)
		{
			if (run == null || run.Finished)
				return;

			run.ScenarioIndex++;
			if (run.ScenarioIndex >= ScenarioDefinitions.Length)
			{
				FinishRun(run);
				return;
			}

			ScenarioDefinition definition = ScenarioDefinitions[run.ScenarioIndex];
			try
			{
				PrepareScenario(run, definition);
			}
			catch (Exception ex)
			{
				run.Failure = $"Scenario '{definition.Name}' failed to start.\n{ex}";
				FinishRun(run);
			}
		}

		private static void PrepareScenario(PendingRun run, ScenarioDefinition definition)
		{
			SetAutoRegisterPrefabs(run.SaveSettings, false);
			RestoreRegistryRows(run, saveAssets: true);
			DeleteAssetIfExists(DuplicatePrefabPath);

			run.BaseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
			run.VariantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPrefabPath);
			SaveablePrefab variantSaveable = run.VariantAsset != null
				? run.VariantAsset.GetComponent<SaveablePrefab>()
				: null;
			if (variantSaveable == null)
				throw new InvalidOperationException("Diagnostic variant prefab does not contain a SaveablePrefab.");

			string scenarioId =
				$"CSP_DIAG_PREFAB_IDENTITY_{run.ScenarioIndex + 1}_{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()}";
			SetSaveableIdentity(variantSaveable, scenarioId, registerWithSaveSystem: true);
			EditorUtility.SetDirty(run.VariantAsset);
			AssetDatabase.SaveAssets();
			AssetDatabase.ImportAsset(VariantPrefabPath, ImportAssetOptions.ForceSynchronousImport);
			run.VariantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPrefabPath);

			if (definition.RegisterOriginal)
				RegisterDiagnosticVariant(run, scenarioId);

			ScenarioResult result = new ScenarioResult
			{
				Definition = definition,
				ScenarioId = scenarioId,
				OriginalAssetPath = VariantPrefabPath,
				OriginalAssetGuid = AssetDatabase.AssetPathToGUID(VariantPrefabPath),
				OriginalIdBefore = ReadPrefabAssetID(run.VariantAsset)
			};
			CaptureOriginalRegistryStateBefore(run.Registry, run.VariantAsset, result);

			SetAutoRegisterPrefabs(run.SaveSettings, definition.AutoRegisterDuringImport);
			if (!AssetDatabase.CopyAsset(VariantPrefabPath, DuplicatePrefabPath))
				throw new InvalidOperationException(
					$"AssetDatabase.CopyAsset failed for '{VariantPrefabPath}' to '{DuplicatePrefabPath}'.");

			AssetDatabase.SaveAssets();
			run.DuplicateAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DuplicatePrefabPath);
			if (run.DuplicateAsset == null)
				throw new InvalidOperationException("The copied diagnostic prefab could not be loaded after import.");

			if (definition.DisableBeforeDelayedCallbacks)
				SetAutoRegisterPrefabs(run.SaveSettings, false);

			run.CurrentScenario = result;
			run.RemainingTicks = run.WaitTicksPerScenario;
			if (run.Window != null)
			{
				run.Window.lastStatus =
					$"Running scenario {run.ScenarioIndex + 1}/{ScenarioDefinitions.Length}: {definition.Name}";
				run.Window.Repaint();
			}

			QueueWaitTick();
		}

		private static void QueueWaitTick()
		{
			EditorApplication.update -= WaitTick;
			EditorApplication.update += WaitTick;
		}

		private static void WaitTick()
		{
			PendingRun run = pendingRun;
			if (run == null || run.Finished)
				return;

			if (run.RemainingTicks > 0)
			{
				run.RemainingTicks--;
				return;
			}

			EditorApplication.update -= WaitTick;

			try
			{
				CompleteScenario(run);
				BeginNextScenario(run);
			}
			catch (Exception ex)
			{
				run.Failure = ex.ToString();
				FinishRun(run);
			}
		}

		private static void CompleteScenario(PendingRun run)
		{
			ScenarioResult result = run.CurrentScenario;
			if (result == null)
				throw new InvalidOperationException("The current diagnostic scenario was lost before completion.");

			AssetDatabase.SaveAssets();
			if (run.VariantAsset == null)
				run.VariantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPrefabPath);
			if (run.DuplicateAsset == null)
				run.DuplicateAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DuplicatePrefabPath);

			result.DuplicateAssetPath = DuplicatePrefabPath;
			result.DuplicateAssetGuid = AssetDatabase.AssetPathToGUID(DuplicatePrefabPath);
			result.OriginalIdAfter = ReadPrefabAssetID(run.VariantAsset);
			result.DuplicateIdAfter = ReadPrefabAssetID(run.DuplicateAsset);
			CaptureOriginalRegistryStateAfter(run.Registry, run.VariantAsset, result);

			PrefabRegistry.PrefabEntry duplicateEntry = FindRegistryEntryByPrefab(run.Registry, run.DuplicateAsset);
			result.DuplicateWasRegistered = duplicateEntry != null;
			result.DuplicateRegistryId = duplicateEntry?.uniqueID;
			result.BaseWasRegistered = ContainsPrefab(run.Registry, run.BaseAsset);
			result.DuplicateSharesOriginalId =
				!string.IsNullOrEmpty(result.OriginalIdAfter) &&
				string.Equals(result.OriginalIdAfter, result.DuplicateIdAfter, StringComparison.Ordinal);

			List<PrefabRegistryIdentityUtility.RegistryRowInfo> allRows =
				PrefabRegistryIdentityUtility.AnalyzeRows(run.Registry);
			result.DiagnosticRows = allRows
				.Where(row => IsDiagnosticRow(row, result))
				.ToList();

			EvaluateScenario(result);
			run.Results.Add(result);
			run.CurrentScenario = null;
		}

		private static void EvaluateScenario(ScenarioResult result)
		{
			ScenarioDefinition definition = result.Definition;
			result.OriginalIdStable =
				!string.IsNullOrEmpty(result.OriginalIdBefore) &&
				string.Equals(result.OriginalIdBefore, result.OriginalIdAfter, StringComparison.Ordinal);
			result.OriginalRegistryOwnerStable =
				string.Equals(result.OriginalRegistryIdBefore, result.OriginalRegistryIdAfter, StringComparison.Ordinal) &&
				string.Equals(result.OriginalRegistryPrefabGuidBefore, result.OriginalRegistryPrefabGuidAfter, StringComparison.Ordinal) &&
				string.Equals(result.OriginalIdOwnerGuidBefore, result.OriginalIdOwnerGuidAfter, StringComparison.Ordinal);
			result.OriginalRegistrationMatchesExpectation = definition.RegisterOriginal
				? !string.IsNullOrEmpty(result.OriginalRegistryIdAfter) &&
				  string.Equals(result.OriginalRegistryIdAfter, result.OriginalIdAfter, StringComparison.Ordinal) &&
				  string.Equals(result.OriginalRegistryPrefabGuidAfter, result.OriginalAssetGuid, StringComparison.Ordinal)
				: string.IsNullOrEmpty(result.OriginalRegistryIdAfter);
			result.DuplicateRegistrationMatchesExpectation =
				result.DuplicateWasRegistered == definition.ExpectDuplicateRegistration;
			result.DuplicateIdentityIsValid = !string.IsNullOrEmpty(result.DuplicateIdAfter);

			if (definition.RegisterOriginal && result.DuplicateSharesOriginalId)
				result.DuplicateIdentityIsValid = false;
			if (definition.ExpectDuplicateRegistration &&
			    !string.Equals(result.DuplicateRegistryId, result.DuplicateIdAfter, StringComparison.Ordinal))
				result.DuplicateIdentityIsValid = false;

			result.InformationalIdentityWarning =
				definition.UnregisteredIdentityIsInformational && result.DuplicateSharesOriginalId;
			result.Passed =
				result.OriginalIdStable &&
				result.OriginalRegistryOwnerStable &&
				result.OriginalRegistrationMatchesExpectation &&
				result.DuplicateRegistrationMatchesExpectation &&
				result.DuplicateIdentityIsValid &&
				!result.BaseWasRegistered;
		}

		private static void FinishRun(PendingRun run)
		{
			if (run == null || run.Finished)
				return;

			run.Finished = true;
			EditorApplication.update -= WaitTick;
			try
			{
				RestoreSnapshots(run);
			}
			finally
			{
				if (pendingRun == run)
					pendingRun = null;
				UnregisterPendingCleanup();
			}

			try
			{
				WriteReport(run);
			}
			catch (Exception ex)
			{
				run.Failure = string.IsNullOrEmpty(run.Failure)
					? ex.ToString()
					: run.Failure + Environment.NewLine + Environment.NewLine + ex;
			}

			FinishWindow(run);
		}

		private static void RegisterPendingCleanup()
		{
			AssemblyReloadEvents.beforeAssemblyReload -= RestorePendingBeforeAssemblyReload;
			AssemblyReloadEvents.beforeAssemblyReload += RestorePendingBeforeAssemblyReload;
		}

		private static void UnregisterPendingCleanup()
		{
			AssemblyReloadEvents.beforeAssemblyReload -= RestorePendingBeforeAssemblyReload;
		}

		private static void RestorePendingBeforeAssemblyReload()
		{
			EditorApplication.update -= WaitTick;
			PendingRun run = pendingRun;
			pendingRun = null;
			UnregisterPendingCleanup();
			if (run != null)
				RestoreSnapshots(run);
		}

		private static void FinishWindow(PendingRun run)
		{
			bool hasFailures =
				!string.IsNullOrEmpty(run.Failure) ||
				run.Results.Any(result => !result.Passed);
			if (run.Window != null)
			{
				run.Window.lastStatus = hasFailures
					? "Report written with warnings or failures; settings and registry restored."
					: "All scenarios passed; settings and registry restored.";
				run.Window.lastReportPath = ReportPath;
				run.Window.Repaint();
			}

			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			if (!Application.isBatchMode)
			{
				if (run.RevealFolderAfterRun)
					EditorUtility.RevealInFinder(Path.GetFullPath(RootFolder));
				if (run.OpenReportAfterRun)
					AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(ReportPath));

				EditorUtility.DisplayDialog(
					"Crystal Save Diagnostic Ready",
					$"Prefab Registry Identity diagnostic finished.\n\nReport:\n{ReportPath}\n\n" +
					"The original SaveSettings auto-register value and all PrefabRegistry rows have been restored.",
					"OK");
			}
		}

		private static void EnsureDiagnosticPrefabs(PendingRun run)
		{
			DeleteAssetIfExists(DuplicatePrefabPath);
			DeleteAssetIfExists(VariantPrefabPath);
			DeleteAssetIfExists(BasePrefabPath);

			GameObject baseTemp = GameObject.CreatePrimitive(PrimitiveType.Cube);
			baseTemp.name = "CSP_IdentityDiagnosticBase";
			GameObject baseAsset = PrefabUtility.SaveAsPrefabAsset(baseTemp, BasePrefabPath);
			UnityEngine.Object.DestroyImmediate(baseTemp);
			if (baseAsset == null)
				throw new InvalidOperationException("Could not create diagnostic base prefab.");

			GameObject variantInstance = PrefabUtility.InstantiatePrefab(baseAsset) as GameObject;
			if (variantInstance == null)
				throw new InvalidOperationException("Could not instantiate diagnostic base prefab.");

			try
			{
				SaveablePrefab saveable = variantInstance.GetComponent<SaveablePrefab>() ??
				                           variantInstance.AddComponent<SaveablePrefab>();
				SetSaveableIdentity(saveable, "CSP_DIAG_PREFAB_IDENTITY_SETUP", registerWithSaveSystem: true);
				GameObject variantAsset = PrefabUtility.SaveAsPrefabAsset(variantInstance, VariantPrefabPath);
				if (variantAsset == null)
					throw new InvalidOperationException("Could not create diagnostic variant prefab.");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(variantInstance);
			}

			run.BaseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
			run.VariantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPrefabPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
		}

		private static void RegisterDiagnosticVariant(PendingRun run, string prefabAssetId)
		{
			if (run.Registry == null || run.VariantAsset == null)
				throw new InvalidOperationException("The diagnostic variant or registry is unavailable.");

			run.Registry.RebuildLookup();
			if (!run.Registry.TryAddPrefab(prefabAssetId, run.VariantAsset, out _, out string reason))
				throw new InvalidOperationException($"Could not register diagnostic variant: {reason}");

			run.Registry.RebuildLookup();
			EditorUtility.SetDirty(run.Registry);
			AssetDatabase.SaveAssets();
		}

		private static void CaptureOriginalRegistryStateBefore(
			PrefabRegistry registry,
			GameObject originalAsset,
			ScenarioResult result)
		{
			PrefabRegistry.PrefabEntry row = FindRegistryEntryByPrefab(registry, originalAsset);
			result.OriginalRegistryIdBefore = row?.uniqueID;
			result.OriginalRegistryPrefabGuidBefore = GetAssetGuid(row?.prefab);
			result.OriginalIdOwnerGuidBefore = GetAssetGuid(FindRegistryEntryById(registry, result.OriginalIdBefore)?.prefab);
		}

		private static void CaptureOriginalRegistryStateAfter(
			PrefabRegistry registry,
			GameObject originalAsset,
			ScenarioResult result)
		{
			PrefabRegistry.PrefabEntry row = FindRegistryEntryByPrefab(registry, originalAsset);
			result.OriginalRegistryIdAfter = row?.uniqueID;
			result.OriginalRegistryPrefabGuidAfter = GetAssetGuid(row?.prefab);
			result.OriginalIdOwnerGuidAfter = GetAssetGuid(FindRegistryEntryById(registry, result.OriginalIdBefore)?.prefab);
		}

		private static PrefabRegistry.PrefabEntry FindRegistryEntryByPrefab(
			PrefabRegistry registry,
			GameObject prefabAsset)
		{
			if (registry?.prefabEntries == null || prefabAsset == null)
				return null;

			return registry.prefabEntries.FirstOrDefault(entry =>
				entry?.prefab != null && PrefabRegistrySyncUtility.AreSamePrefabAsset(entry.prefab, prefabAsset));
		}

		private static PrefabRegistry.PrefabEntry FindRegistryEntryById(PrefabRegistry registry, string id)
		{
			if (registry?.prefabEntries == null || string.IsNullOrEmpty(id))
				return null;

			return registry.prefabEntries.FirstOrDefault(entry =>
				entry != null && string.Equals(entry.uniqueID, id, StringComparison.Ordinal));
		}

		private static bool IsDiagnosticRow(
			PrefabRegistryIdentityUtility.RegistryRowInfo row,
			ScenarioResult result)
		{
			if (row == null)
				return false;

			return string.Equals(row.AssetGuid, result.OriginalAssetGuid, StringComparison.Ordinal) ||
			       string.Equals(row.AssetGuid, result.DuplicateAssetGuid, StringComparison.Ordinal) ||
			       string.Equals(row.AssetPath, BasePrefabPath, StringComparison.Ordinal) ||
			       (!string.IsNullOrEmpty(row.RegistryID) && row.RegistryID.StartsWith("CSP_DIAG_PREFAB_IDENTITY_", StringComparison.Ordinal));
		}

		private static void RestoreSnapshots(PendingRun run)
		{
			try
			{
				StripDiagnosticSaveablePrefabs();
			}
			catch (Exception ex)
			{
				AppendRestoreFailure(run, ex);
			}

			try
			{
				RestoreRegistryRows(run, saveAssets: false);
				if (run.SaveSettings != null)
					SetAutoRegisterPrefabs(run.SaveSettings, run.InitialAutoRegisterPrefabs);

				AssetDatabase.SaveAssets();
				CrystalSaveEditorAutomationSettings.ClearCache();
			}
			catch (Exception ex)
			{
				AppendRestoreFailure(run, ex);
			}
		}

		private static void RestoreRegistryRows(PendingRun run, bool saveAssets)
		{
			if (run?.Registry == null || run.RegistrySnapshot == null)
				return;

			run.Registry.prefabEntries.Clear();
			foreach (RegistryEntrySnapshot snapshot in run.RegistrySnapshot)
			{
				if (snapshot.IsNull)
				{
					run.Registry.prefabEntries.Add(null);
					continue;
				}

				run.Registry.prefabEntries.Add(new PrefabRegistry.PrefabEntry
				{
					uniqueID = snapshot.UniqueID,
					prefab = snapshot.Prefab,
					poolSize = snapshot.PoolSize,
					disablePooling = snapshot.DisablePooling
				});
			}

			run.Registry.RebuildLookup();
			EditorUtility.SetDirty(run.Registry);
			if (saveAssets)
				AssetDatabase.SaveAssets();
		}

		private static void StripDiagnosticSaveablePrefabs()
		{
			StripSaveablePrefabAtPath(VariantPrefabPath);
			StripSaveablePrefabAtPath(DuplicatePrefabPath);
		}

		private static void StripSaveablePrefabAtPath(string prefabPath)
		{
			if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(prefabPath)))
				return;

			GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
			try
			{
				SaveablePrefab saveable = contents.GetComponent<SaveablePrefab>();
				if (saveable == null)
					return;

				UnityEngine.Object.DestroyImmediate(saveable, true);
				PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(contents);
			}
		}

		private static void AppendRestoreFailure(PendingRun run, Exception ex)
		{
			if (run == null || ex == null)
				return;

			run.RestoreFailure = string.IsNullOrEmpty(run.RestoreFailure)
				? ex.ToString()
				: run.RestoreFailure + Environment.NewLine + Environment.NewLine + ex;
		}

		private static List<RegistryEntrySnapshot> SnapshotRegistry(PrefabRegistry registry)
		{
			List<RegistryEntrySnapshot> snapshot = new List<RegistryEntrySnapshot>();
			if (registry?.prefabEntries == null)
				return snapshot;

			foreach (PrefabRegistry.PrefabEntry entry in registry.prefabEntries)
			{
				if (entry == null)
				{
					snapshot.Add(new RegistryEntrySnapshot { IsNull = true });
					continue;
				}

				snapshot.Add(new RegistryEntrySnapshot
				{
					UniqueID = entry.uniqueID,
					Prefab = entry.prefab,
					PoolSize = entry.poolSize,
					DisablePooling = entry.disablePooling
				});
			}

			return snapshot;
		}

		private static void WriteReport(PendingRun run)
		{
			EnsureFolder(RootFolder);
			StringBuilder report = new StringBuilder();
			report.AppendLine("# Crystal Save Prefab Registry Identity Diagnostic");
			report.AppendLine();
			report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
			report.AppendLine($"Active PrefabRegistry: {ValueOrNone(run.RegistryPath)}");
			report.AppendLine($"Active SaveSettings: {ValueOrNone(run.SaveSettingsPath)}");
			report.AppendLine($"Initial Auto-Register SaveablePrefab Prefabs: {run.InitialAutoRegisterPrefabs}");
			report.AppendLine();

			report.AppendLine("## Controlled Variant Duplication Scenarios");
			report.AppendLine();
			if (!string.IsNullOrEmpty(run.SkipControlledReproReason))
			{
				report.AppendLine($"Skipped: {run.SkipControlledReproReason}");
			}
			else
			{
				bool allPassed = run.Results.Count == ScenarioDefinitions.Length && run.Results.All(result => result.Passed);
				report.AppendLine($"Overall result: **{(allPassed ? "PASS" : "FAIL OR INCOMPLETE")}**");
				report.AppendLine();
				for (int i = 0; i < run.Results.Count; i++)
					AppendScenarioReport(report, run.Results[i], i + 1);
			}

			if (!string.IsNullOrEmpty(run.Failure))
			{
				report.AppendLine("### Diagnostic Error");
				report.AppendLine();
				report.AppendLine("```text");
				report.AppendLine(run.Failure);
				report.AppendLine("```");
				report.AppendLine();
			}

			if (!string.IsNullOrEmpty(run.RestoreFailure))
			{
				report.AppendLine("### Restore Error");
				report.AppendLine();
				report.AppendLine("```text");
				report.AppendLine(run.RestoreFailure);
				report.AppendLine("```");
				report.AppendLine();
			}

			report.AppendLine("## Active Registry Audit Before Repro");
			report.AppendLine();
			AppendAuditSummary(report, run.InitialRows);
			report.AppendLine(PrefabRegistryIdentityUtility.ToMarkdownTable(run.InitialRows));

			File.WriteAllText(ReportPath, NormalizeLineEndings(report.ToString()));
			AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceSynchronousImport);
		}

		private static void AppendScenarioReport(StringBuilder report, ScenarioResult result, int index)
		{
			report.AppendLine($"### {index}. {result.Definition.Name}");
			report.AppendLine();
			report.AppendLine(result.Definition.Description);
			report.AppendLine();
			report.AppendLine($"- Original registered before copy: {result.Definition.RegisterOriginal}");
			report.AppendLine($"- Auto-register during import: {result.Definition.AutoRegisterDuringImport}");
			report.AppendLine($"- Disabled before delayed callbacks: {result.Definition.DisableBeforeDelayedCallbacks}");
			report.AppendLine($"- Original asset GUID: `{ValueOrNone(result.OriginalAssetGuid)}`");
			report.AppendLine($"- Original ID before: `{ValueOrNone(result.OriginalIdBefore)}`");
			report.AppendLine($"- Original ID after: `{ValueOrNone(result.OriginalIdAfter)}`");
			report.AppendLine($"- Original registry ID before: `{ValueOrNone(result.OriginalRegistryIdBefore)}`");
			report.AppendLine($"- Original registry ID after: `{ValueOrNone(result.OriginalRegistryIdAfter)}`");
			report.AppendLine($"- Original row prefab GUID before: `{ValueOrNone(result.OriginalRegistryPrefabGuidBefore)}`");
			report.AppendLine($"- Original row prefab GUID after: `{ValueOrNone(result.OriginalRegistryPrefabGuidAfter)}`");
			report.AppendLine($"- Owner GUID for original ID before: `{ValueOrNone(result.OriginalIdOwnerGuidBefore)}`");
			report.AppendLine($"- Owner GUID for original ID after: `{ValueOrNone(result.OriginalIdOwnerGuidAfter)}`");
			report.AppendLine($"- Duplicate asset GUID: `{ValueOrNone(result.DuplicateAssetGuid)}`");
			report.AppendLine($"- Duplicate serialized ID: `{ValueOrNone(result.DuplicateIdAfter)}`");
			report.AppendLine($"- Duplicate registry ID: `{ValueOrNone(result.DuplicateRegistryId)}`");
			report.AppendLine();
			report.AppendLine("Checks:");
			report.AppendLine($"- Original serialized ID stable: {PassFail(result.OriginalIdStable)}");
			report.AppendLine($"- Original registry ownership stable: {PassFail(result.OriginalRegistryOwnerStable)}");
			report.AppendLine($"- Original registration matches scenario: {PassFail(result.OriginalRegistrationMatchesExpectation)}");
			report.AppendLine($"- Duplicate registration matches scenario: {PassFail(result.DuplicateRegistrationMatchesExpectation)}");
			report.AppendLine($"- Duplicate identity is valid: {PassFail(result.DuplicateIdentityIsValid)}");
			report.AppendLine($"- Base prefab remained unregistered: {PassFail(!result.BaseWasRegistered)}");
			report.AppendLine();
			if (result.InformationalIdentityWarning)
			{
				report.AppendLine(
					"Identity note: the unregistered original and its copy share an ID because auto-registration is off and no registry row records which asset owns that identity. Register the original before duplication when durable identity ownership is required.");
				report.AppendLine();
			}

			report.AppendLine($"Scenario result: **{(result.Passed ? "PASS" : "FAIL")}**");
			report.AppendLine();
			report.AppendLine("Diagnostic rows for this scenario:");
			report.AppendLine();
			report.AppendLine(PrefabRegistryIdentityUtility.ToMarkdownTable(result.DiagnosticRows));
		}

		private static void AppendAuditSummary(
			StringBuilder report,
			List<PrefabRegistryIdentityUtility.RegistryRowInfo> rows)
		{
			rows ??= new List<PrefabRegistryIdentityUtility.RegistryRowInfo>();
			report.AppendLine($"Rows: {rows.Count}");
			report.AppendLine($"Rows with warnings: {rows.Count(row => row.HasWarnings)}");
			report.AppendLine($"Invalid rows eligible for cleanup: {rows.Count(row => row.IsInvalidRegistryEntry)}");
			report.AppendLine($"Missing prefab references: {rows.Count(row => row.MissingPrefab)}");
			report.AppendLine($"Non-persistent or non-prefab references: {rows.Count(row => row.InvalidAssetReference)}");
			report.AppendLine($"Rows without SaveablePrefab: {rows.Count(row => !row.MissingPrefab && !row.InvalidAssetReference && !row.HasSaveablePrefab)}");
			report.AppendLine($"Registry/prefab ID mismatches: {rows.Count(row => row.IsIDMismatch)}");
			report.AppendLine($"Duplicate IDs: {rows.Count(row => row.IsDuplicateID)}");
			report.AppendLine($"Duplicate prefab rows: {rows.Count(row => row.IsDuplicatePrefabRow)}");
			report.AppendLine($"Variant source ID inheritance warnings: {rows.Count(row => row.VariantInheritsSourceID)}");
			report.AppendLine();
		}

		private static bool ContainsPrefab(PrefabRegistry registry, GameObject prefabAsset)
		{
			return FindRegistryEntryByPrefab(registry, prefabAsset) != null;
		}

		private static string ReadPrefabAssetID(GameObject prefabAsset)
		{
			SaveablePrefab saveable = prefabAsset != null ? prefabAsset.GetComponent<SaveablePrefab>() : null;
			return saveable != null ? saveable.PrefabAssetID : null;
		}

		private static string GetAssetGuid(GameObject prefab)
		{
			if (prefab == null)
				return null;

			GameObject asset = PrefabAssetResolver.ResolveNearestPrefabAsset(prefab) ?? prefab;
			string path = AssetDatabase.GetAssetPath(asset);
			return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
		}

		private static void SetSaveableIdentity(
			SaveablePrefab saveable,
			string prefabAssetId,
			bool registerWithSaveSystem)
		{
			if (saveable == null)
				return;

			SerializedObject serialized = new SerializedObject(saveable);
			SetString(serialized, "prefabAssetID", prefabAssetId);
			SetString(serialized, "uniqueID", string.Empty);
			SetBool(serialized, "RegisterWithSaveSystem", registerWithSaveSystem);
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(saveable);
		}

		private static void SetAutoRegisterPrefabs(SaveSettings settings, bool value)
		{
			if (settings == null)
				return;

			SerializedObject serialized = new SerializedObject(settings);
			SerializedProperty property = serialized.FindProperty("autoRegisterPrefabs");
			if (property == null)
				return;

			property.boolValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(settings);
			AssetDatabase.SaveAssets();
			CrystalSaveEditorAutomationSettings.ClearCache();
		}

		private static SaveSettings LoadActiveSaveSettings()
		{
			SaveSettings overrideSettings = CrystalSaveOverrides.GetOverride<SaveSettings>();
			if (overrideSettings != null)
				return overrideSettings;

#if REMEMBERME_ADDRESSABLES_PRESENT
			if (PrefabAssetResolver.ShouldUseAddressableCoreAssets())
			{
				SaveSettings addressableSettings =
					PrefabAssetResolver.LoadAddressableEditorAsset<SaveSettings>("SaveSettings");
				if (addressableSettings != null)
					return addressableSettings;
			}
#endif

			SaveSettings resourceSettings = Resources.Load<SaveSettings>("SaveSettings");
			if (resourceSettings != null)
				return resourceSettings;

			SaveSettings defaultSettings =
				AssetDatabase.LoadAssetAtPath<SaveSettings>("Assets/Plugins/CrystalSave/Resources/SaveSettings.asset");
			if (defaultSettings != null)
				return defaultSettings;

			foreach (string guid in AssetDatabase.FindAssets("t:SaveSettings"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				SaveSettings settings = AssetDatabase.LoadAssetAtPath<SaveSettings>(path);
				if (settings != null)
					return settings;
			}

			return null;
		}

		private static void EnsureFolders()
		{
			EnsureFolder(RootFolder);
			EnsureFolder(PrefabsFolder);
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;

			string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
			if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
				EnsureFolder(parent);

			string folderName = Path.GetFileName(path);
			if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
				AssetDatabase.CreateFolder(parent, folderName);
		}

		private static void DeleteAssetIfExists(string assetPath)
		{
			if (!string.IsNullOrEmpty(assetPath) &&
			    !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
				AssetDatabase.DeleteAsset(assetPath);
		}

		private static void SetString(SerializedObject serialized, string propertyName, string value)
		{
			SerializedProperty property = serialized.FindProperty(propertyName);
			if (property != null)
				property.stringValue = value;
		}

		private static void SetBool(SerializedObject serialized, string propertyName, bool value)
		{
			SerializedProperty property = serialized.FindProperty(propertyName);
			if (property != null)
				property.boolValue = value;
		}

		private static string NormalizeLineEndings(string value)
		{
			return value.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
		}

		private static string ValueOrNone(string value)
		{
			return string.IsNullOrEmpty(value) ? "<none>" : value;
		}

		private static string PassFail(bool value)
		{
			return value ? "PASS" : "FAIL";
		}

		private sealed class PendingRun
		{
			internal PrefabRegistryIdentityDiagnosticBuilder Window;
			internal PrefabRegistry Registry;
			internal SaveSettings SaveSettings;
			internal string RegistryPath;
			internal string SaveSettingsPath;
			internal bool InitialAutoRegisterPrefabs;
			internal List<PrefabRegistryIdentityUtility.RegistryRowInfo> InitialRows;
			internal List<RegistryEntrySnapshot> RegistrySnapshot;
			internal readonly List<ScenarioResult> Results = new();
			internal int ScenarioIndex;
			internal int RemainingTicks;
			internal int WaitTicksPerScenario;
			internal GameObject BaseAsset;
			internal GameObject VariantAsset;
			internal GameObject DuplicateAsset;
			internal ScenarioResult CurrentScenario;
			internal bool OpenReportAfterRun;
			internal bool RevealFolderAfterRun;
			internal bool Finished;
			internal string SkipControlledReproReason;
			internal string Failure;
			internal string RestoreFailure;
		}

		private sealed class ScenarioDefinition
		{
			internal ScenarioDefinition(
				string name,
				string description,
				bool registerOriginal,
				bool autoRegisterDuringImport,
				bool disableBeforeDelayedCallbacks,
				bool expectDuplicateRegistration,
				bool unregisteredIdentityIsInformational)
			{
				Name = name;
				Description = description;
				RegisterOriginal = registerOriginal;
				AutoRegisterDuringImport = autoRegisterDuringImport;
				DisableBeforeDelayedCallbacks = disableBeforeDelayedCallbacks;
				ExpectDuplicateRegistration = expectDuplicateRegistration;
				UnregisteredIdentityIsInformational = unregisteredIdentityIsInformational;
			}

			internal string Name { get; }
			internal string Description { get; }
			internal bool RegisterOriginal { get; }
			internal bool AutoRegisterDuringImport { get; }
			internal bool DisableBeforeDelayedCallbacks { get; }
			internal bool ExpectDuplicateRegistration { get; }
			internal bool UnregisteredIdentityIsInformational { get; }
		}

		private sealed class ScenarioResult
		{
			internal ScenarioDefinition Definition;
			internal string ScenarioId;
			internal string OriginalAssetPath;
			internal string OriginalAssetGuid;
			internal string DuplicateAssetPath;
			internal string DuplicateAssetGuid;
			internal string OriginalIdBefore;
			internal string OriginalIdAfter;
			internal string DuplicateIdAfter;
			internal string OriginalRegistryIdBefore;
			internal string OriginalRegistryIdAfter;
			internal string OriginalRegistryPrefabGuidBefore;
			internal string OriginalRegistryPrefabGuidAfter;
			internal string OriginalIdOwnerGuidBefore;
			internal string OriginalIdOwnerGuidAfter;
			internal string DuplicateRegistryId;
			internal bool DuplicateWasRegistered;
			internal bool DuplicateSharesOriginalId;
			internal bool BaseWasRegistered;
			internal bool OriginalIdStable;
			internal bool OriginalRegistryOwnerStable;
			internal bool OriginalRegistrationMatchesExpectation;
			internal bool DuplicateRegistrationMatchesExpectation;
			internal bool DuplicateIdentityIsValid;
			internal bool InformationalIdentityWarning;
			internal bool Passed;
			internal List<PrefabRegistryIdentityUtility.RegistryRowInfo> DiagnosticRows;
		}

		private sealed class RegistryEntrySnapshot
		{
			internal bool IsNull;
			internal string UniqueID;
			internal GameObject Prefab;
			internal int PoolSize;
			internal bool DisablePooling;
		}
	}
}
#endif
