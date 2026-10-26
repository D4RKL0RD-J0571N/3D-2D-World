#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arawn.CrystalSave.Editor
{
	public sealed class PrefabRegistryIdentityEditorTests
	{
		private const string TestRoot = "Assets/CrystalSaveDiagnostics/PrefabRegistryIdentityTests";

		private SaveSettings activeSaveSettings;
		private bool originalAutoRegister;
		private bool originalAutoValidation;
		private ActiveRegistryOverrideScope fixtureOverride;

		[SetUp]
		public void SetUp()
		{
			EndDiagnosticCounterSessionIfNeeded();
			EnsureFolder(TestRoot);
			activeSaveSettings = ScriptableObject.CreateInstance<SaveSettings>();
			activeSaveSettings.editorAutomationSettingsInitialized = true;
			fixtureOverride = new ActiveRegistryOverrideScope(activeSaveSettings, null);
			if (activeSaveSettings != null)
			{
				originalAutoRegister = activeSaveSettings.autoRegisterPrefabs;
				originalAutoValidation = activeSaveSettings.enablePrefabRegistryAutoValidation;
				SetAutoRegister(activeSaveSettings, false);
				SetAutoValidation(activeSaveSettings, true);
			}

			PrefabRegistryValidationCoordinator.ResetTestMetrics();
		}

		[TearDown]
		public void TearDown()
		{
			EndDiagnosticCounterSessionIfNeeded();
			PrefabRegistryValidationCoordinator.ResetTestMetrics();
			Undo.ClearAll();
			if (AssetDatabase.IsValidFolder(TestRoot))
				AssetDatabase.DeleteAsset(TestRoot);

			if (activeSaveSettings != null)
			{
				SetAutoRegister(activeSaveSettings, originalAutoRegister);
				SetAutoValidation(activeSaveSettings, originalAutoValidation);
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
			PrefabRegistryValidationCoordinator.ResetTestMetrics();
			fixtureOverride?.Dispose();
			fixtureOverride = null;
			UnityEngine.Object.DestroyImmediate(activeSaveSettings);
		}

		[Test]
		public void SynchronizePreservesExistingOwnerAndRegeneratesIncomingIdentity()
		{
			string sharedId = "CSP_TEST_OWNER_" + Guid.NewGuid().ToString("N");
			GameObject owner = CreateSaveablePrefab("IdentityOwner", sharedId);
			GameObject incoming = CreateSaveablePrefab("IdentityIncoming", sharedId);
			SaveablePrefab incomingSaveable = incoming.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			try
			{
				Assert.That(registry.TryAddPrefab(sharedId, owner, out string addReason), Is.True, addReason);
				registry.prefabEntries.Insert(0, null);
				registry.RebuildLookup();
				PrefabRegistrySyncResult result = PrefabRegistrySyncUtility.Synchronize(
					registry,
					incomingSaveable,
					incoming,
					sharedId,
					saveAssets: false);

				Assert.That(result.Succeeded, Is.True, result.Message);
				Assert.That(result.Status, Is.EqualTo(PrefabRegistrySyncStatus.ExistingOwnerPreserved));
				Assert.That(registry.FindEntryByID(sharedId)?.prefab, Is.EqualTo(owner));
				Assert.That(incomingSaveable.PrefabAssetID, Is.Not.Empty);
				Assert.That(incomingSaveable.PrefabAssetID, Is.Not.EqualTo(sharedId));

				PrefabRegistry.PrefabEntry incomingRow = registry.FindEntryByPrefab(incoming);
				Assert.That(incomingRow, Is.Not.Null);
				Assert.That(incomingRow.uniqueID, Is.EqualTo(incomingSaveable.PrefabAssetID));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void SynchronizeAdoptsExistingRegistryIdWithoutChangingRegistryOwner()
		{
			string registryId = "CSP_TEST_REGISTRY_" + Guid.NewGuid().ToString("N");
			string incorrectId = "CSP_TEST_INCORRECT_" + Guid.NewGuid().ToString("N");
			GameObject prefab = CreateSaveablePrefab("RegistryAuthority", incorrectId);
			SaveablePrefab saveable = prefab.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			try
			{
				Assert.That(registry.TryAddPrefab(registryId, prefab, out string addReason), Is.True, addReason);
				PrefabRegistrySyncResult result = PrefabRegistrySyncUtility.Synchronize(
					registry,
					saveable,
					prefab,
					incorrectId,
					saveAssets: false);

				Assert.That(result.Status, Is.EqualTo(PrefabRegistrySyncStatus.RegistryIdAdopted));
				Assert.That(saveable.PrefabAssetID, Is.EqualTo(registryId));
				Assert.That(registry.FindEntryByID(registryId)?.prefab, Is.EqualTo(prefab));
				Assert.That(registry.prefabEntries, Has.Count.EqualTo(1));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[TestCase(LogLevel.Off, LogLevel.Info, 0)]
		[TestCase(LogLevel.Error, LogLevel.Info, 0)]
		[TestCase(LogLevel.Warning, LogLevel.Info, 0)]
		[TestCase(LogLevel.Info, LogLevel.Warning, 0)]
		[TestCase(LogLevel.Info, LogLevel.Off, 0)]
		[TestCase(LogLevel.Info, LogLevel.Info, 1)]
		public void PrefabIdentityAssignmentInfoLogHonorsEditorAndGlobalLogLevels(
			LogLevel editorLogLevel,
			LogLevel loggerThreshold,
			int expectedLogCount)
		{
			string registryId = "CSP_TEST_LOG_REGISTRY_" + Guid.NewGuid().ToString("N");
			string incorrectId = "CSP_TEST_LOG_INCORRECT_" + Guid.NewGuid().ToString("N");
			GameObject prefab = CreateSaveablePrefab("IdentityAssignmentLogging", incorrectId);
			SaveablePrefab saveable = prefab.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			SaveSettings settings = ScriptableObject.CreateInstance<SaveSettings>();
			LogLevel previousLoggerThreshold = Arawn.CrystalSave.Runtime.Logger.LogThreshold;

			registry.hideFlags = HideFlags.HideAndDontSave;
			settings.hideFlags = HideFlags.HideAndDontSave;
			settings.editorAutomationSettingsInitialized = true;
			settings.logLevel = editorLogLevel;
			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry>
			{
				new PrefabRegistry.PrefabEntry { uniqueID = registryId, prefab = prefab }
			};

			try
			{
				using (new ActiveRegistryOverrideScope(settings, registry))
				{
					Arawn.CrystalSave.Runtime.Logger.LogThreshold = loggerThreshold;
					PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.Build(registry);
					string assignedId = null;
					int matchingLogs = CaptureMatchingLogs(
						$"[Crystal Save] Assigned PrefabAssetID '{registryId}'",
						() => assignedId = PrefabAssetResolver.EnsurePrefabAssetIDForAsset(
							saveable,
							prefab,
							index));

					Assert.That(assignedId, Is.EqualTo(registryId));
					Assert.That(saveable.PrefabAssetID, Is.EqualTo(registryId));
					Assert.That(matchingLogs, Is.EqualTo(expectedLogCount));
				}
			}
			finally
			{
				Arawn.CrystalSave.Runtime.Logger.LogThreshold = previousLoggerThreshold;
				UnityEngine.Object.DestroyImmediate(settings);
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void CleanInvalidEntriesPreservesDuplicateAndConflictingValidRows()
		{
			GameObject validPrefab = CreateSaveablePrefab(
				"ValidRegistryPrefab",
				"CSP_TEST_VALID_" + Guid.NewGuid().ToString("N"));
			GameObject noSaveablePrefab = CreatePlainPrefab("NoSaveablePrefab");
			GameObject transientObject = new GameObject("TransientRegistryReference");
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry>
			{
				new PrefabRegistry.PrefabEntry
				{
					uniqueID = "valid-row-a",
					prefab = validPrefab
				},
				new PrefabRegistry.PrefabEntry
				{
					uniqueID = "valid-row-b",
					prefab = validPrefab
				},
				new PrefabRegistry.PrefabEntry
				{
					uniqueID = "missing-prefab",
					prefab = null
				},
				new PrefabRegistry.PrefabEntry
				{
					uniqueID = "no-saveable",
					prefab = noSaveablePrefab
				},
				new PrefabRegistry.PrefabEntry
				{
					uniqueID = "transient-object",
					prefab = transientObject
				},
				null
			};
			registry.RebuildLookup();

			try
			{
				Undo.IncrementCurrentGroup();
				int undoGroup = Undo.GetCurrentGroup();
				PrefabRegistryNormalizationUtility.InvalidEntryCleanupResult result =
					PrefabRegistryNormalizationUtility.CleanInvalidEntries(registry, saveAssets: false);
				Undo.CollapseUndoOperations(undoGroup);

				Assert.That(result.InvalidEntryCount, Is.EqualTo(4));
				Assert.That(result.RemovedCount, Is.EqualTo(4));
				Assert.That(registry.prefabEntries, Has.Count.EqualTo(2));
				Assert.That(registry.prefabEntries.All(entry => entry?.prefab == validPrefab), Is.True);
				Assert.That(registry.prefabEntries.Select(entry => entry.uniqueID),
					Is.EquivalentTo(new[] { "valid-row-a", "valid-row-b" }));
				Assert.That(registry.FindEntryByID("missing-prefab"), Is.Null);

				Undo.PerformUndo();
				Assert.That(registry.prefabEntries, Has.Count.EqualTo(6));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(transientObject);
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void NormalizeEntriesAdoptsSoleNonEmptyRegistryIdAndIsIdempotent()
		{
			string authoritativeId = "CSP_TEST_AUTHORITY_" + Guid.NewGuid().ToString("N");
			string originalPrefabId = "CSP_TEST_ORIGINAL_" + Guid.NewGuid().ToString("N");
			GameObject prefab = CreateSaveablePrefab("EmptyRowAdoption", originalPrefabId);
			SaveablePrefab saveable = prefab.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry>
			{
				new PrefabRegistry.PrefabEntry
				{
					uniqueID = authoritativeId,
					prefab = prefab
				},
				new PrefabRegistry.PrefabEntry
				{
					uniqueID = string.Empty,
					prefab = prefab
				}
			};

			try
			{
				PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.Build(registry);
				Assert.That(index.HasConflictingIdsForAsset(prefab), Is.False);
				Assert.That(index.TryGetAuthoritativeId(prefab, out string indexedAuthority), Is.True);
				Assert.That(indexedAuthority, Is.EqualTo(authoritativeId));

				bool firstChanged = PrefabRegistryNormalizationUtility.NormalizeEntries(
					registry,
					index,
					out PrefabRegistryNormalizationUtility.Result firstResult);

				Assert.That(firstChanged, Is.True);
				Assert.That(firstResult.Changed, Is.True);
				Assert.That(firstResult.HasConflicts, Is.False);
				Assert.That(saveable.PrefabAssetID, Is.EqualTo(authoritativeId));
				Assert.That(registry.prefabEntries.Select(entry => entry.uniqueID),
					Is.All.EqualTo(authoritativeId));

				PrefabRegistryIdentityIndex refreshedIndex = PrefabRegistryIdentityIndex.Build(registry);
				bool secondChanged = PrefabRegistryNormalizationUtility.NormalizeEntries(
					registry,
					refreshedIndex,
					out PrefabRegistryNormalizationUtility.Result secondResult);

				Assert.That(secondChanged, Is.False);
				Assert.That(secondResult.Changed, Is.False);
				Assert.That(secondResult.HasConflicts, Is.False);
				Assert.That(saveable.PrefabAssetID, Is.EqualTo(authoritativeId));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void NormalizeEntriesLeavesDistinctSameAssetRegistryIdsUnchanged()
		{
			string firstId = "CSP_TEST_CONFLICT_A_" + Guid.NewGuid().ToString("N");
			string secondId = "CSP_TEST_CONFLICT_B_" + Guid.NewGuid().ToString("N");
			string prefabId = "CSP_TEST_COMPONENT_" + Guid.NewGuid().ToString("N");
			GameObject prefab = CreateSaveablePrefab("ConflictingRows", prefabId);
			SaveablePrefab saveable = prefab.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry>
			{
				new PrefabRegistry.PrefabEntry { uniqueID = firstId, prefab = prefab },
				new PrefabRegistry.PrefabEntry { uniqueID = string.Empty, prefab = prefab },
				new PrefabRegistry.PrefabEntry { uniqueID = secondId, prefab = prefab }
			};

			try
			{
				PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.Build(registry);
				Assert.That(index.HasConflictingIdsForAsset(prefab), Is.True);
				Assert.That(index.TryGetAuthoritativeId(prefab, out string authoritativeId), Is.False);
				Assert.That(authoritativeId, Is.Null);

				bool changed = PrefabRegistryNormalizationUtility.NormalizeEntries(
					registry,
					index,
					out PrefabRegistryNormalizationUtility.Result result);

				Assert.That(changed, Is.False);
				Assert.That(result.Changed, Is.False);
				Assert.That(result.Conflicts, Has.Count.EqualTo(1));
				Assert.That(result.Conflicts[0].RegistryIDs, Is.EquivalentTo(new[] { firstId, secondId }));
				Assert.That(registry.prefabEntries.Select(entry => entry.uniqueID),
					Is.EqualTo(new[] { firstId, string.Empty, secondId }));
				Assert.That(saveable.PrefabAssetID, Is.EqualTo(prefabId));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void NormalizeEntriesCorrectsPrefabChildReferenceToCanonicalAsset()
		{
			string id = "CSP_TEST_CANONICAL_" + Guid.NewGuid().ToString("N");
			GameObject prefab = CreateSaveablePrefabWithChild("CanonicalReference", id);
			GameObject childReference = prefab.transform.GetChild(0).gameObject;
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry>
			{
				new PrefabRegistry.PrefabEntry { uniqueID = id, prefab = childReference }
			};

			try
			{
				PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.Build(registry);
				Assert.That(index.Rows[0].IsCanonicalReference, Is.False);

				bool changed = PrefabRegistryNormalizationUtility.NormalizeEntries(
					registry,
					index,
					out PrefabRegistryNormalizationUtility.Result result);

				Assert.That(changed, Is.True);
				Assert.That(result.HasConflicts, Is.False);
				Assert.That(registry.prefabEntries[0].prefab, Is.EqualTo(prefab));
				Assert.That(registry.prefabEntries[0].uniqueID, Is.EqualTo(id));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void NormalizeEntriesAssignsVariantIdDifferentFromSourcePrefab()
		{
			string sourceId = "CSP_TEST_VARIANT_SOURCE_" + Guid.NewGuid().ToString("N");
			GameObject source = CreateSaveablePrefab("VariantSource", sourceId);
			GameObject variant = CreateVariantPrefab(source, "VariantIdentity", sourceId);
			SaveablePrefab variantSaveable = variant.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry>
			{
				new PrefabRegistry.PrefabEntry { uniqueID = sourceId, prefab = variant }
			};

			try
			{
				Assert.That(PrefabUtility.GetPrefabAssetType(variant), Is.EqualTo(PrefabAssetType.Variant));
				PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.Build(registry);
				Assert.That(index.Rows[0].VariantInheritsSourceId, Is.True);

				bool changed = PrefabRegistryNormalizationUtility.NormalizeEntries(
					registry,
					index,
					out PrefabRegistryNormalizationUtility.Result result);

				Assert.That(changed, Is.True);
				Assert.That(result.HasConflicts, Is.False);
				Assert.That(variantSaveable.PrefabAssetID, Is.Not.Empty);
				Assert.That(variantSaveable.PrefabAssetID, Is.Not.EqualTo(sourceId));
				Assert.That(registry.prefabEntries[0].uniqueID, Is.EqualTo(variantSaveable.PrefabAssetID));
				Assert.That(source.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(sourceId));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[TestCase(20)]
		[TestCase(100)]
		public void IdentityIndexResolvesEachUniquePrefabAssetOnce(int prefabCount)
		{
			List<GameObject> prefabs = CreateSaveablePrefabs(prefabCount);
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			registry.prefabEntries = prefabs
				.Select((prefab, index) => new PrefabRegistry.PrefabEntry
				{
					uniqueID = $"CSP_TEST_INDEX_{prefabCount}_{index}",
					prefab = prefab
				})
				.ToList();

			try
			{
				PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.Build(registry);

				Assert.That(index.Rows, Has.Count.EqualTo(prefabCount));
				Assert.That(index.Metrics.RowCount, Is.EqualTo(prefabCount));
				Assert.That(index.Metrics.UniqueReferenceCount, Is.EqualTo(prefabCount));
				Assert.That(index.Metrics.UniqueAssetCount, Is.EqualTo(prefabCount));
				Assert.That(index.Metrics.AssetResolutionCount, Is.EqualTo(prefabCount));
				Assert.That(index.Metrics.InvalidRowCount, Is.Zero);

				for (int i = 0; i < prefabCount; i++)
				{
					string id = $"CSP_TEST_INDEX_{prefabCount}_{i}";
					Assert.That(index.GetRowsForAsset(prefabs[i]), Has.Count.EqualTo(1));
					Assert.That(index.TryGetIdOwner(id, out PrefabRegistryIdentityRow owner), Is.True);
					Assert.That(owner.ResolvedAsset, Is.EqualTo(prefabs[i]));
				}

				PrefabRegistryIdentityUtility.InspectionSnapshot snapshot =
					PrefabRegistryIdentityUtility.GetInspectionSnapshot(registry);
				for (int repaint = 0; repaint < 8; repaint++)
				{
					Assert.That(
						PrefabRegistryIdentityUtility.GetInspectionSnapshot(registry),
						Is.SameAs(snapshot));
					Assert.That(
						PrefabRegistryIdentityUtility.MatchesFilter(snapshot.Rows[0], "IndexedPrefab"),
						Is.True);
				}
				Assert.That(snapshot.IdentityIndex.Metrics.AssetResolutionCount, Is.EqualTo(prefabCount));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void DiagnosticCountersAreTargetScopedAndProduceDeltas()
		{
			PrefabRegistry targetRegistry = ScriptableObject.CreateInstance<PrefabRegistry>();
			PrefabRegistry otherRegistry = ScriptableObject.CreateInstance<PrefabRegistry>();
			try
			{
				Assert.That(
					PrefabRegistryEditorDiagnosticCounters.TryBeginSession(targetRegistry),
					Is.True);
				PrefabRegistryEditorDiagnosticSnapshot baseline =
					PrefabRegistryEditorDiagnosticCounters.Capture(targetRegistry);

				PrefabRegistryEditorDiagnosticCounters.RecordIndexBuild(targetRegistry, null);
				PrefabRegistryEditorDiagnosticCounters.RecordIndexBuild(otherRegistry, null);
				PrefabRegistryEditorDiagnosticCounters.RecordLookupRebuild(targetRegistry);
				PrefabRegistryEditorDiagnosticCounters.RecordLookupRebuild(otherRegistry);
				PrefabRegistryEditorDiagnosticCounters.RecordInspectionSnapshotBuild(targetRegistry);
				PrefabRegistryEditorDiagnosticCounters.RecordNormalizationPass(targetRegistry);
				PrefabRegistryEditorDiagnosticCounters.RecordStandaloneInspectorEvent(
					targetRegistry, EventType.Layout);
				PrefabRegistryEditorDiagnosticCounters.RecordStandaloneInspectorEvent(
					targetRegistry, EventType.Repaint);
				PrefabRegistryEditorDiagnosticCounters.RecordSettingsWindowEvent(
					otherRegistry, EventType.Repaint);

				PrefabRegistryEditorDiagnosticSnapshot delta =
					PrefabRegistryEditorDiagnosticCounters.Capture(targetRegistry).DeltaFrom(baseline);
				Assert.That(delta.IndexBuilds, Is.EqualTo(1));
				Assert.That(delta.LookupRebuilds, Is.EqualTo(1));
				Assert.That(delta.InspectionSnapshotBuilds, Is.EqualTo(1));
				Assert.That(delta.NormalizationPasses, Is.EqualTo(1));
				Assert.That(delta.StandaloneInspectorLayouts, Is.EqualTo(1));
				Assert.That(delta.StandaloneInspectorRepaints, Is.EqualTo(1));
				Assert.That(delta.SettingsWindowRepaints, Is.Zero);
				Assert.That(
					PrefabRegistryEditorDiagnosticCounters.TryBeginSession(otherRegistry),
					Is.False);
				Assert.That(
					PrefabRegistryEditorDiagnosticCounters.Capture(otherRegistry).IndexBuilds,
					Is.Zero);
			}
			finally
			{
				PrefabRegistryEditorDiagnosticCounters.EndSession(targetRegistry);
				UnityEngine.Object.DestroyImmediate(targetRegistry);
				UnityEngine.Object.DestroyImmediate(otherRegistry);
			}
		}

		[Test]
		public void DiagnosticNoWorkEvaluatorIgnoresPaintsButRejectsIdentityWork()
		{
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			try
			{
				Assert.That(PrefabRegistryEditorDiagnosticCounters.TryBeginSession(registry), Is.True);
				PrefabRegistryEditorDiagnosticSnapshot baseline =
					PrefabRegistryEditorDiagnosticCounters.Capture(registry);

				PrefabRegistryEditorDiagnosticCounters.RecordStandaloneInspectorEvent(
					registry, EventType.Layout);
				PrefabRegistryEditorDiagnosticCounters.RecordStandaloneInspectorEvent(
					registry, EventType.Repaint);
				PrefabRegistryEditorDiagnosticCounters.RecordSettingsWindowEvent(
					registry, EventType.Layout);
				PrefabRegistryEditorDiagnosticCounters.RecordSettingsRegistryEvent(
					registry, EventType.Repaint);
				PrefabRegistryEditorDiagnosticSnapshot paintOnly =
					PrefabRegistryEditorDiagnosticCounters.Capture(registry).DeltaFrom(baseline);
				Assert.That(
					PrefabRegistryEditorPerformanceDiagnosticBuilder.HasNoIdentityOrSaveWork(paintOnly),
					Is.True);

				PrefabRegistryEditorDiagnosticCounters.RecordNormalizationPass(registry);
				PrefabRegistryEditorDiagnosticSnapshot withWork =
					PrefabRegistryEditorDiagnosticCounters.Capture(registry).DeltaFrom(baseline);
				Assert.That(
					PrefabRegistryEditorPerformanceDiagnosticBuilder.HasNoIdentityOrSaveWork(withWork),
					Is.False);
			}
			finally
			{
				PrefabRegistryEditorDiagnosticCounters.EndSession(registry);
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void DiagnosticIdentityPathQueuesAllowedPrefabAndSuppressesUnrelatedPrefab()
		{
			Assert.That(activeSaveSettings, Is.Not.Null, "An active SaveSettings asset is required.");
			string allowedId = "CSP_TEST_DIAG_ALLOWED_" + Guid.NewGuid().ToString("N");
			string unrelatedId = "CSP_TEST_DIAG_UNRELATED_" + Guid.NewGuid().ToString("N");
			GameObject allowed = CreateSaveablePrefab("DiagnosticAllowedPrefab", allowedId);
			string unrelatedPath = AssetDatabase.GenerateUniqueAssetPath(
				"Assets/CrystalSaveDiagnostics/PrefabRegistryIdentityGuardOutside.prefab");
			GameObject unrelated = CreateSaveablePrefabAtPath(
				"DiagnosticUnrelatedPrefab",
				unrelatedId,
				unrelatedPath);
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			try
			{
				ClearSaveablePrefabEditorQueues();
				Assert.That(
					PrefabRegistryEditorDiagnosticCounters.TryBeginSession(registry, TestRoot),
					Is.True);
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, true);
					SaveablePrefab.EnsurePrefabAssetIdentityForEditor(
						allowed.GetComponent<SaveablePrefab>(),
						allowed);
					Assert.That(
						SaveablePrefab.PendingPrefabRegistrySyncCountForEditorDiagnostics,
						Is.EqualTo(1));

					SaveablePrefab.EnsurePrefabAssetIdentityForEditor(
						unrelated.GetComponent<SaveablePrefab>(),
						unrelated);
					Assert.That(
						SaveablePrefab.PendingPrefabRegistrySyncCountForEditorDiagnostics,
						Is.EqualTo(1),
						"The temporary active-registry override must not capture unrelated prefab callbacks.");

					SaveablePrefab.SuspendPrefabRegistrySyncFlushForEditorDiagnostics();
					SaveablePrefab.FlushPrefabRegistrySyncsForEditorDiagnostics();
					Assert.That(registry.prefabEntries, Has.Count.EqualTo(1));
					Assert.That(registry.prefabEntries[0].prefab, Is.EqualTo(allowed));
				}
			}
			finally
			{
				SaveablePrefab.RemovePrefabRegistrySyncsForEditorDiagnostics(TestRoot);
				PrefabRegistryIdentityIndex.ClearAutomaticSaveSuppressionForEditorDiagnostics(registry);
				PrefabRegistryEditorDiagnosticCounters.EndSession(registry);
				AssetDatabase.DeleteAsset(unrelatedPath);
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void DiagnosticPrefabQueueCleanupPreservesUnrelatedPendingWork()
		{
			Assert.That(activeSaveSettings, Is.Not.Null, "An active SaveSettings asset is required.");
			string diagnosticId = "CSP_TEST_DIAG_QUEUE_" + Guid.NewGuid().ToString("N");
			string unrelatedId = "CSP_TEST_OTHER_QUEUE_" + Guid.NewGuid().ToString("N");
			GameObject diagnosticPrefab = CreateSaveablePrefab("DiagnosticQueuedPrefab", diagnosticId);
			string unrelatedPath = AssetDatabase.GenerateUniqueAssetPath(
				"Assets/CrystalSaveDiagnostics/PrefabRegistryUnrelatedQueue.prefab");
			GameObject unrelatedPrefab = CreateSaveablePrefabAtPath(
				"UnrelatedQueuedPrefab",
				unrelatedId,
				unrelatedPath);
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, true);
					ClearSaveablePrefabEditorQueues();
					QueueSaveablePrefabRegistrySync(
						diagnosticPrefab.GetComponent<SaveablePrefab>(), diagnosticPrefab, diagnosticId);
					QueueSaveablePrefabRegistrySync(
						unrelatedPrefab.GetComponent<SaveablePrefab>(), unrelatedPrefab, unrelatedId);
					Assert.That(
						SaveablePrefab.PendingPrefabRegistrySyncCountForEditorDiagnostics,
						Is.EqualTo(2));

					SaveablePrefab.RemovePrefabRegistrySyncsForEditorDiagnostics(TestRoot);
					Assert.That(
						SaveablePrefab.PendingPrefabRegistrySyncCountForEditorDiagnostics,
						Is.EqualTo(1));
					FlushSaveablePrefabRegistryQueue();

					Assert.That(registry.prefabEntries, Has.Count.EqualTo(1));
					Assert.That(registry.prefabEntries[0].prefab, Is.EqualTo(unrelatedPrefab));
				}
			}
			finally
			{
				ClearSaveablePrefabEditorQueues();
				AssetDatabase.DeleteAsset(unrelatedPath);
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void SettingsWindowDiagnosticInjectionRestoresPreviousState()
		{
			RememberMeSettingsWindow window = ScriptableObject.CreateInstance<RememberMeSettingsWindow>();
			SaveSettings firstSettings = ScriptableObject.CreateInstance<SaveSettings>();
			SaveSettings secondSettings = ScriptableObject.CreateInstance<SaveSettings>();
			PrefabRegistry firstRegistry = ScriptableObject.CreateInstance<PrefabRegistry>();
			PrefabRegistry secondRegistry = ScriptableObject.CreateInstance<PrefabRegistry>();
			try
			{
				RememberMeSettingsWindow.PrefabRegistryPerformanceDiagnosticState firstState =
					window.BeginPrefabRegistryPerformanceDiagnostic(firstRegistry, firstSettings);
				window.SetPrefabRegistryPerformanceDiagnosticFilter("temporary-filter");
				window.SetPrefabRegistryPerformanceDiagnosticFoldout(false);
				window.RestorePrefabRegistryPerformanceDiagnostic(firstState);

				RememberMeSettingsWindow.PrefabRegistryPerformanceDiagnosticState recapturedState =
					window.BeginPrefabRegistryPerformanceDiagnostic(secondRegistry, secondSettings);
				Assert.That(recapturedState.SaveSettings, Is.SameAs(firstState.SaveSettings));
				Assert.That(recapturedState.PrefabRegistry, Is.SameAs(firstState.PrefabRegistry));
				Assert.That(recapturedState.SerializedSaveSettings, Is.SameAs(firstState.SerializedSaveSettings));
				Assert.That(recapturedState.SerializedPrefabRegistry, Is.SameAs(firstState.SerializedPrefabRegistry));
				Assert.That(recapturedState.ShowPrefabRegistry, Is.EqualTo(firstState.ShowPrefabRegistry));
				Assert.That(recapturedState.PrefabRegistryFilter, Is.EqualTo(firstState.PrefabRegistryFilter));
				window.RestorePrefabRegistryPerformanceDiagnostic(recapturedState);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(window);
				UnityEngine.Object.DestroyImmediate(firstSettings);
				UnityEngine.Object.DestroyImmediate(secondSettings);
				UnityEngine.Object.DestroyImmediate(firstRegistry);
				UnityEngine.Object.DestroyImmediate(secondRegistry);
			}
		}

		[Test]
		public void DiagnosticSettingsWindowUsesSideEffectFreeInitializationPath()
		{
			SaveSettings settings = ScriptableObject.CreateInstance<SaveSettings>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			RememberMeSettingsWindow window = null;
			try
			{
				window = RememberMeSettingsWindow.CreatePrefabRegistryPerformanceDiagnosticWindow(
					registry,
					settings);
				Assert.That(window, Is.Not.Null);
				Assert.That(window.IsPrefabRegistryPerformanceDiagnosticOnly, Is.True);
			}
			finally
			{
				if (window != null)
					UnityEngine.Object.DestroyImmediate(window);
				UnityEngine.Object.DestroyImmediate(settings);
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void PerformanceDiagnosticReportIncludesScalingChecksAndCleanup()
		{
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			try
			{
				Assert.That(PrefabRegistryEditorDiagnosticCounters.TryBeginSession(registry), Is.True);
				PrefabRegistryEditorDiagnosticSnapshot baseline =
					PrefabRegistryEditorDiagnosticCounters.Capture(registry);
				PrefabRegistryEditorDiagnosticCounters.RecordIndexBuild(registry, null);
				PrefabRegistryEditorDiagnosticSnapshot cold =
					PrefabRegistryEditorDiagnosticCounters.Capture(registry).DeltaFrom(baseline);
				PrefabRegistryEditorDiagnosticCounters.EndSession(registry);

				var run = new PrefabRegistryEditorPerformanceDiagnosticBuilder.PendingRun
				{
					StartedAt = DateTime.Now,
					WorkingAssetsRemoved = true,
					CounterSessionStarted = false
				};
				run.SizeResults.Add(new PrefabRegistryEditorPerformanceDiagnosticBuilder.SizeResult
				{
					RowCount = 20,
					ColdBuildMilliseconds = 1.25,
					ColdDelta = cold,
					ClosedWarmDelta = PrefabRegistryEditorDiagnosticCounters.Capture(null),
					LiveUiDelta = PrefabRegistryEditorDiagnosticCounters.Capture(null)
				});
				run.Checks.Add(new PrefabRegistryEditorPerformanceDiagnosticBuilder.DiagnosticCheck
				{
					Name = "Synthetic report check",
					Passed = true,
					Details = "No deterministic work after warm-up."
				});

				string report = PrefabRegistryEditorPerformanceDiagnosticBuilder.BuildReportText(run);
				StringAssert.Contains("Overall result: **PASS**", report);
				StringAssert.Contains("| 20 |", report);
				StringAssert.Contains("Synthetic report check", report);
				StringAssert.Contains("Working assets removed: PASS", report);
				StringAssert.Contains("Timing is informational only", report);
			}
			finally
			{
				PrefabRegistryEditorDiagnosticCounters.EndSession(registry);
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void DisabledAutomaticValidationLeavesRegistryReadOnly()
		{
			Assert.That(activeSaveSettings, Is.Not.Null, "An active SaveSettings asset is required.");
			GameObject prefab = CreateSaveablePrefab(
				"DisabledValidation",
				"CSP_TEST_DISABLED_COMPONENT_" + Guid.NewGuid().ToString("N"));
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry>
			{
				new PrefabRegistry.PrefabEntry
				{
					uniqueID = string.Empty,
					prefab = prefab
				}
			};

			try
			{
				SetAutoValidation(activeSaveSettings, false);
				EditorUtility.ClearDirty(registry);
				PrefabRegistryValidationCoordinator.ResetTestMetrics();
				PrefabRegistryValidationCoordinator.RequestValidationForTests(
					registry,
					PrefabRegistryValidationCoordinator.ValidationReason.SerializedEdit);
				PrefabRegistryValidationCoordinator.FlushPendingForTests();

				Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.Zero);
				Assert.That(PrefabRegistryValidationCoordinator.ValidationPassCount, Is.Zero);
				Assert.That(PrefabRegistryValidationCoordinator.SaveRequestCount, Is.Zero);
				Assert.That(registry.prefabEntries, Has.Count.EqualTo(1));
				Assert.That(registry.prefabEntries[0].uniqueID, Is.Empty);
				Assert.That(EditorUtility.IsDirty(registry), Is.False);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void CancellingPendingValidationPreventsQueuedPass()
		{
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			try
			{
				PrefabRegistryValidationCoordinator.ResetTestMetrics();
				PrefabRegistryValidationCoordinator.RequestValidationForTests(
					registry,
					PrefabRegistryValidationCoordinator.ValidationReason.ProjectChanged);
				Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.EqualTo(1));

				PrefabRegistryValidationCoordinator.CancelPending();
				PrefabRegistryValidationCoordinator.FlushPendingForTests();

				Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.Zero);
				Assert.That(PrefabRegistryValidationCoordinator.ValidationPassCount, Is.Zero);
				Assert.That(PrefabRegistryValidationCoordinator.SaveRequestCount, Is.Zero);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void TargetedValidationCancellationPreservesOtherRegistryWork()
		{
			PrefabRegistry diagnosticRegistry = ScriptableObject.CreateInstance<PrefabRegistry>();
			PrefabRegistry unrelatedRegistry = ScriptableObject.CreateInstance<PrefabRegistry>();
			try
			{
				PrefabRegistryValidationCoordinator.ResetTestMetrics();
				PrefabRegistryValidationCoordinator.RequestValidationForTests(
					diagnosticRegistry,
					PrefabRegistryValidationCoordinator.ValidationReason.SerializedEdit);
				PrefabRegistryValidationCoordinator.RequestValidationForTests(
					unrelatedRegistry,
					PrefabRegistryValidationCoordinator.ValidationReason.ProjectChanged);
				Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.EqualTo(2));
				Assert.That(
					PrefabRegistryValidationCoordinator.HasPendingForOtherRegistry(diagnosticRegistry),
					Is.True);

				PrefabRegistryValidationCoordinator.CancelPending(diagnosticRegistry);
				Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.EqualTo(1));
				PrefabRegistryValidationCoordinator.FlushPendingForTests(unrelatedRegistry);

				Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.Zero);
				Assert.That(PrefabRegistryValidationCoordinator.ValidationPassCount, Is.EqualTo(1));
			}
			finally
			{
				PrefabRegistryValidationCoordinator.ResetTestMetrics();
				UnityEngine.Object.DestroyImmediate(diagnosticRegistry);
				UnityEngine.Object.DestroyImmediate(unrelatedRegistry);
			}
		}

		[Test]
		public void AutomaticSaveFeedbackDoesNotQueueValidation()
		{
			PrefabRegistryValidationCoordinator.ResetTestMetrics();
			PrefabRegistryIdentityIndex.NotifyAutomaticSaveRequested();

			MethodInfo projectChanged = typeof(PrefabRegistryValidationCoordinator).GetMethod(
				"OnProjectChanged",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(projectChanged, Is.Not.Null);
			projectChanged.Invoke(null, null);

			Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.Zero);
			Assert.That(PrefabRegistryValidationCoordinator.ValidationPassCount, Is.Zero);
			Assert.That(PrefabRegistryValidationCoordinator.SaveRequestCount, Is.Zero);
		}

		[Test]
		public void ValidationCoordinatorCoalescesRepeatedNoOpRequestsWithoutSaving()
		{
			string id = "CSP_TEST_COORDINATOR_" + Guid.NewGuid().ToString("N");
			GameObject prefab = CreateSaveablePrefab("CoordinatorNoOp", id);
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry>
			{
				new PrefabRegistry.PrefabEntry { uniqueID = id, prefab = prefab }
			};
			registry.RebuildLookup();

			try
			{
				PrefabRegistryIdentityIndex.Invalidate(registry);
				EditorUtility.ClearDirty(registry);
				PrefabRegistryValidationCoordinator.ResetTestMetrics();

				PrefabRegistryValidationCoordinator.RequestValidationForTests(
					registry,
					PrefabRegistryValidationCoordinator.ValidationReason.Activation);
				PrefabRegistryValidationCoordinator.RequestValidationForTests(
					registry,
					PrefabRegistryValidationCoordinator.ValidationReason.SerializedEdit);
				PrefabRegistryValidationCoordinator.RequestValidationForTests(
					registry,
					PrefabRegistryValidationCoordinator.ValidationReason.ProjectChanged);

				Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.EqualTo(1));
				Assert.That(PrefabRegistryValidationCoordinator.ValidationPassCount, Is.Zero);
				Assert.That(PrefabRegistryValidationCoordinator.SaveRequestCount, Is.Zero);

				PrefabRegistryValidationCoordinator.FlushPendingForTests();

				Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.Zero);
				Assert.That(PrefabRegistryValidationCoordinator.ValidationPassCount, Is.EqualTo(1));
				Assert.That(PrefabRegistryValidationCoordinator.SaveRequestCount, Is.Zero);
				Assert.That(EditorUtility.IsDirty(registry), Is.False);
			}
			finally
			{
				PrefabRegistryValidationCoordinator.ResetTestMetrics();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void RegisteredPrefabCopyWithAutoRegisterDisabledGetsFreshIdWithoutRegistryRow()
		{
			Assert.That(activeSaveSettings, Is.Not.Null, "An active SaveSettings asset is required.");
			string ownerId = "CSP_TEST_COPY_OWNER_" + Guid.NewGuid().ToString("N");
			GameObject owner = CreateSaveablePrefab("RegisteredCopyOwner", ownerId);
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			GameObject copy = null;

			try
			{
				Assert.That(registry.TryAddPrefab(ownerId, owner, out string reason), Is.True, reason);
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, false);
					ClearSaveablePrefabEditorQueues();
					copy = CopyPrefabAsset(owner, "RegisteredCopy_AutoOff");
					SaveablePrefab copySaveable = copy.GetComponent<SaveablePrefab>();

					InvokeSaveablePrefabOnValidate(copySaveable);
					FlushSaveablePrefabRegistryQueue();

					Assert.That(copySaveable.PrefabAssetID, Is.Not.Empty);
					Assert.That(copySaveable.PrefabAssetID, Is.Not.EqualTo(ownerId));
					Assert.That(owner.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(ownerId));
					Assert.That(registry.prefabEntries, Has.Count.EqualTo(1));
					Assert.That(PrefabRegistryIdentityIndex.Build(registry).GetRowsForAsset(copy), Is.Empty);
				}
			}
			finally
			{
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void RepeatedNoOpPrefabValidationReusesIdentityIndex()
		{
			Assert.That(activeSaveSettings, Is.Not.Null, "An active SaveSettings asset is required.");
			string id = "CSP_TEST_NOOP_VALIDATE_" + Guid.NewGuid().ToString("N");
			GameObject prefab = CreateSaveablePrefab("NoOpValidation", id);
			SaveablePrefab saveable = prefab.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry>
			{
				new PrefabRegistry.PrefabEntry { uniqueID = id, prefab = prefab }
			};

			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, false);
					ClearSaveablePrefabEditorQueues();
					PrefabRegistryIdentityIndex warmIndex =
						PrefabRegistryIdentityIndex.GetOrCreate(registry);

					for (int validation = 0; validation < 8; validation++)
					{
						InvokeSaveablePrefabOnValidate(saveable);
						Assert.That(
							PrefabRegistryIdentityIndex.GetOrCreate(registry),
							Is.SameAs(warmIndex));
					}

					Assert.That(warmIndex.Metrics.AssetResolutionCount, Is.EqualTo(1));
					Assert.That(registry.prefabEntries, Has.Count.EqualTo(1));
				}
			}
			finally
			{
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void DisablingAutoRegisterBeforeDelayedFlushPreventsRegistryRow()
		{
			Assert.That(activeSaveSettings, Is.Not.Null, "An active SaveSettings asset is required.");
			string id = "CSP_TEST_DELAY_DISABLED_" + Guid.NewGuid().ToString("N");
			GameObject prefab = CreateSaveablePrefab("DelayedRegistrationDisabled", id);
			SaveablePrefab saveable = prefab.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, true);
					ClearSaveablePrefabEditorQueues();
					QueueSaveablePrefabRegistrySync(saveable, prefab, id);

					SetAutoRegister(activeSaveSettings, false);
					FlushSaveablePrefabRegistryQueue();

					Assert.That(saveable.PrefabAssetID, Is.EqualTo(id));
					Assert.That(registry.prefabEntries, Is.Empty);
				}
			}
			finally
			{
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void EnabledAutoRegisterAddsOneRowDuringDelayedFlush()
		{
			Assert.That(activeSaveSettings, Is.Not.Null, "An active SaveSettings asset is required.");
			string id = "CSP_TEST_DELAY_ENABLED_" + Guid.NewGuid().ToString("N");
			GameObject prefab = CreateSaveablePrefab("DelayedRegistrationEnabled", id);
			SaveablePrefab saveable = prefab.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, true);
					ClearSaveablePrefabEditorQueues();
					QueueSaveablePrefabRegistrySync(saveable, prefab, id);
					QueueSaveablePrefabRegistrySync(saveable, prefab, id);

					FlushSaveablePrefabRegistryQueue();

					Assert.That(registry.prefabEntries, Has.Count.EqualTo(1));
					Assert.That(registry.prefabEntries[0].uniqueID, Is.EqualTo(id));
					Assert.That(registry.prefabEntries[0].prefab, Is.EqualTo(prefab));
					Assert.That(registry.FindEntryByID(id)?.prefab, Is.EqualTo(prefab));
				}
			}
			finally
			{
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void DisablingRegisterWithSaveSystemBeforeDelayedFlushPreventsRegistryRow()
		{
			Assert.That(activeSaveSettings, Is.Not.Null, "An active SaveSettings asset is required.");
			string id = "CSP_TEST_COMPONENT_DISABLED_" + Guid.NewGuid().ToString("N");
			GameObject prefab = CreateSaveablePrefab("ComponentRegistrationDisabled", id);
			SaveablePrefab saveable = prefab.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, true);
					ClearSaveablePrefabEditorQueues();
					QueueSaveablePrefabRegistrySync(saveable, prefab, id);

					SetRegisterWithSaveSystem(saveable, false);
					FlushSaveablePrefabRegistryQueue();

					Assert.That(saveable.PrefabAssetID, Is.EqualTo(id));
					Assert.That(registry.prefabEntries, Is.Empty);
				}
			}
			finally
			{
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void UnregisteredPrefabCopyWithAutoRegisterDisabledKeepsSharedId()
		{
			Assert.That(activeSaveSettings, Is.Not.Null, "An active SaveSettings asset is required.");
			string sharedId = "CSP_TEST_UNREGISTERED_COPY_" + Guid.NewGuid().ToString("N");
			GameObject original = CreateSaveablePrefab("UnregisteredCopyOwner", sharedId);
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			GameObject copy = null;

			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, false);
					ClearSaveablePrefabEditorQueues();
					copy = CopyPrefabAsset(original, "UnregisteredCopy_AutoOff");
					SaveablePrefab copySaveable = copy.GetComponent<SaveablePrefab>();

					InvokeSaveablePrefabOnValidate(copySaveable);
					FlushSaveablePrefabRegistryQueue();

					Assert.That(copySaveable.PrefabAssetID, Is.EqualTo(sharedId));
					Assert.That(original.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(sharedId));
					Assert.That(registry.prefabEntries, Is.Empty);
				}
			}
			finally
			{
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void MultipleQueuedPrefabsUseOneBatchSave()
		{
			Assert.That(activeSaveSettings, Is.Not.Null, "An active SaveSettings asset is required.");
			string firstId = "CSP_TEST_BATCH_A_" + Guid.NewGuid().ToString("N");
			string secondId = "CSP_TEST_BATCH_B_" + Guid.NewGuid().ToString("N");
			GameObject first = CreateSaveablePrefab("BatchPrefabA", firstId);
			GameObject second = CreateSaveablePrefab("BatchPrefabB", secondId);
			SaveablePrefab firstSaveable = first.GetComponent<SaveablePrefab>();
			SaveablePrefab secondSaveable = second.GetComponent<SaveablePrefab>();
			PrefabRegistry registry = ScriptableObject.CreateInstance<PrefabRegistry>();

			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, true);
					ClearSaveablePrefabEditorQueues();
					SetSaveablePrefabBatchSaveCount(0);
					QueueSaveablePrefabRegistrySync(firstSaveable, first, firstId);
					QueueSaveablePrefabRegistrySync(secondSaveable, second, secondId);

					FlushSaveablePrefabRegistryQueue();

					Assert.That(registry.prefabEntries, Has.Count.EqualTo(2));
					Assert.That(
						registry.prefabEntries.Select(entry => entry.uniqueID),
						Is.EquivalentTo(new[] { firstId, secondId }));
					Assert.That(GetSaveablePrefabBatchSaveCount(), Is.EqualTo(1));
				}
			}
			finally
			{
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[UnityTest, Category("CrystalSaveGraphics")]
		public IEnumerator LiveRegistryPerformanceDiagnosticPaintsBothInspectorsAndRestoresItsState()
		{
			const string folder = "Assets/CrystalSaveDiagnostics/PrefabRegistryEditorPerformance";
			Assert.That(AssetDatabase.IsValidFolder(folder), Is.False,
				"The live diagnostic test must not overwrite an existing diagnostic directory.");
			SetAutoValidation(activeSaveSettings, false);
			var sentinel = CreateSaveablePrefab("UnrelatedDiagnosticSentinel", "CSP_TEST_UNRELATED_DIAGNOSTIC");
			string sentinelPath = AssetDatabase.GetAssetPath(sentinel);
			string sentinelBytes = System.IO.File.ReadAllText(sentinelPath);
			string settingsBefore = EditorJsonUtility.ToJson(activeSaveSettings);
			var originalOverride = CrystalSaveOverrides.Current;
			var originalSelection = Selection.objects;
			PrefabRegistryEditorPerformanceDiagnosticBuilder window = null;
			PrefabRegistryEditorPerformanceDiagnosticBuilder.PendingRun run = null;
			var pendingField = typeof(PrefabRegistryEditorPerformanceDiagnosticBuilder)
				.GetField("pendingRun", BindingFlags.Static | BindingFlags.NonPublic);
			try
			{
				double deadline = EditorApplication.timeSinceStartup + 30;
				while ((EditorApplication.isCompiling || EditorApplication.isUpdating ||
				        PrefabRegistryValidationCoordinator.PendingRegistryCount > 0 ||
				        SaveablePrefab.PendingPrefabRegistrySyncCountForEditorDiagnostics > 0 ||
				        SaveablePrefab.PendingUniqueIdRemovalCountForEditorDiagnostics > 0) &&
				       EditorApplication.timeSinceStartup < deadline) yield return null;
				Assert.That(EditorApplication.isCompiling || EditorApplication.isUpdating, Is.False);
				Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.Zero);
				Assert.That(SaveablePrefab.PendingPrefabRegistrySyncCountForEditorDiagnostics, Is.Zero);
				Assert.That(SaveablePrefab.PendingUniqueIdRemovalCountForEditorDiagnostics, Is.Zero);
				Assert.That(EditorApplication.ExecuteMenuItem(
					"Tools/Crystal Save/Runtime Debug/Diagnostics/Prefab Registry Editor Performance Diagnostic"), Is.True);
				window = Resources.FindObjectsOfTypeAll<PrefabRegistryEditorPerformanceDiagnosticBuilder>().Single();
				typeof(PrefabRegistryEditorPerformanceDiagnosticBuilder).GetMethod("StartDiagnostic",
					BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { false });
				run = (PrefabRegistryEditorPerformanceDiagnosticBuilder.PendingRun)pendingField.GetValue(null);
				Assert.That(run, Is.Not.Null);
				run.SuppressInteractiveFinish = true;
				deadline = EditorApplication.timeSinceStartup + 240;
				while (pendingField.GetValue(null) != null && EditorApplication.timeSinceStartup < deadline) yield return null;
				Assert.That(run.Finished && pendingField.GetValue(null) == null, Is.True,
					"The live diagnostic exceeded its bounded execution time.");
				string report = System.IO.File.ReadAllText(folder + "/PrefabRegistryEditorPerformanceReport.md");
				TestContext.Out.WriteLine(report);
				Assert.That(run.FatalError, Is.Null.Or.Empty, report);
				Assert.That(run.AnyCheckFailed, Is.False, report);
				Assert.That(run.Checks, Is.Not.Empty);
				Assert.That(run.Checks.All(check => check.Passed), Is.True, report);
				Assert.That(run.SizeResults.Select(size => size.RowCount), Is.EqualTo(new[] { 4, 20, 100 }));
				foreach (var size in run.SizeResults)
				{
					var ui = size.LiveUiDelta;
					Assert.That(ui.StandaloneInspectorLayouts, Is.GreaterThanOrEqualTo(8));
					Assert.That(ui.StandaloneInspectorRepaints, Is.GreaterThanOrEqualTo(8));
					Assert.That(ui.SettingsWindowLayouts, Is.GreaterThanOrEqualTo(8));
					Assert.That(ui.SettingsWindowRepaints, Is.GreaterThanOrEqualTo(8));
					Assert.That(ui.SettingsRegistryLayouts, Is.GreaterThanOrEqualTo(8));
					Assert.That(ui.SettingsRegistryRepaints, Is.GreaterThanOrEqualTo(8));
					Assert.That(ui.AutomaticSaveRequests + ui.PrefabBatchSaveRequests + ui.NormalizationPasses, Is.Zero);
				}
				Assert.That(run.WorkingAssetsRemoved && run.OverrideRestored, Is.True);
				Assert.That(run.CleanupErrors, Is.Empty);
				Assert.That(AssetDatabase.IsValidFolder(folder + "/Working"), Is.False);
				Assert.That(CrystalSaveOverrides.Current, Is.SameAs(originalOverride));
				Assert.That(PrefabRegistryEditorDiagnosticCounters.ActiveRegistry, Is.Null);
				Assert.That(EditorJsonUtility.ToJson(activeSaveSettings), Is.EqualTo(settingsBefore));
				Assert.That(System.IO.File.ReadAllText(sentinelPath), Is.EqualTo(sentinelBytes));
				Assert.That(Selection.objects, Is.EqualTo(originalSelection));
			}
			finally
			{
				if (run != null && !run.Finished)
				{
					run.SuppressInteractiveFinish = true;
					run.CancelRequested = true;
					LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("PrefabRegistry Performance.*FAIL"));
					typeof(PrefabRegistryEditorPerformanceDiagnosticBuilder).GetMethod("UpdateDiagnostic",
						BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
				}
				if (window != null) window.Close();
				Selection.objects = originalSelection;
				if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
			}
		}

		[UnityTest, Category("CrystalSaveGraphics")]
		public IEnumerator LiveIdentityDiagnosticRunsAllScenariosAndRestoresRegistryAndSettings()
		{
			const string folder = "Assets/CrystalSaveDiagnostics/PrefabRegistryIdentity";
			Assert.That(AssetDatabase.IsValidFolder(folder), Is.False,
				"The fixture must not replace an existing diagnostic output directory.");
			SetAutoValidation(activeSaveSettings, false);
			var sentinel = CreateSaveablePrefab("IdentityDiagnosticSentinel", "CSP_TEST_DIAGNOSTIC_SENTINEL");
			string sentinelPath = AssetDatabase.GetAssetPath(sentinel);
			string sentinelBytes = System.IO.File.ReadAllText(sentinelPath);
			var registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			registry.prefabEntries.Add(new PrefabRegistry.PrefabEntry
			{
				uniqueID = "CSP_TEST_DIAGNOSTIC_SENTINEL", prefab = sentinel,
				poolSize = 17, disablePooling = true
			});
			registry.prefabEntries.Add(null);
			registry.RebuildLookup();
			var selection = Selection.objects;
			var type = typeof(PrefabRegistryIdentityDiagnosticBuilder);
			var pending = type.GetField("pendingRun", BindingFlags.Static | BindingFlags.NonPublic);
			PrefabRegistryIdentityDiagnosticBuilder window = null;
			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					double deadline = EditorApplication.timeSinceStartup + 30;
					while ((EditorApplication.isCompiling || EditorApplication.isUpdating ||
					        SaveablePrefab.PendingPrefabRegistrySyncCountForEditorDiagnostics > 0 ||
					        PrefabRegistryValidationCoordinator.PendingRegistryCount > 0) &&
					       EditorApplication.timeSinceStartup < deadline) yield return null;
					Assert.That(EditorApplication.isCompiling || EditorApplication.isUpdating, Is.False);
					Assert.That(SaveablePrefab.PendingPrefabRegistrySyncCountForEditorDiagnostics, Is.Zero);
					Assert.That(PrefabRegistryValidationCoordinator.PendingRegistryCount, Is.Zero);
					SetAutoRegister(activeSaveSettings, true);
					string settingsBefore = EditorJsonUtility.ToJson(activeSaveSettings);
					string registryBefore = EditorJsonUtility.ToJson(registry);
					Assert.That(EditorApplication.ExecuteMenuItem(
						"Tools/Crystal Save/Runtime Debug/Diagnostics/Prefab Registry Identity Diagnostic"), Is.True);
					window = Resources.FindObjectsOfTypeAll<PrefabRegistryIdentityDiagnosticBuilder>().Single();
					type.GetField("openReportAfterRun", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, false);
					type.GetMethod("StartDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
					Assert.That(pending.GetValue(null), Is.Not.Null);
					deadline = EditorApplication.timeSinceStartup + 60;
					while (pending.GetValue(null) != null && EditorApplication.timeSinceStartup < deadline) yield return null;
					Assert.That(pending.GetValue(null), Is.Null, "The real diagnostic must complete within its deadline.");
					string report = System.IO.File.ReadAllText(folder + "/PrefabRegistryIdentityReport.md");
					TestContext.Out.WriteLine(report);
					Assert.That(report, Does.Contain("Overall result: **PASS**"));
					Assert.That(System.Text.RegularExpressions.Regex.Matches(report, "Scenario result: \\*\\*PASS\\*\\*").Count, Is.EqualTo(4));
					Assert.That(report, Does.Not.Contain("### Diagnostic Error").And.Not.Contain("### Restore Error"));
					for (int tick = 0; tick < 10; tick++) yield return null;
					Assert.That(EditorJsonUtility.ToJson(activeSaveSettings), Is.EqualTo(settingsBefore));
					Assert.That(EditorJsonUtility.ToJson(registry), Is.EqualTo(registryBefore));
					Assert.That(System.IO.File.ReadAllText(sentinelPath), Is.EqualTo(sentinelBytes));
					Assert.That(Selection.objects, Is.EqualTo(selection));
					foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
					{
						var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
						Assert.That(asset.GetComponentsInChildren<SaveablePrefab>(true), Is.Empty,
							"Retained diagnostic output must not register helpers after the snapshots have been restored.");
					}
				}
			}
			finally
			{
				if (pending.GetValue(null) != null)
					type.GetMethod("RestorePendingBeforeAssemblyReload", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
				if (window != null) window.Close();
				Selection.objects = selection;
				if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void RegistryRowStatusesAndEveryFilterFieldReflectActualAssets()
		{
			var registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			var sceneObject = new GameObject("NonPersistentRegistryReference");
			sceneObject.AddComponent<SaveablePrefab>();
			string sourceId = "source-token-" + Guid.NewGuid().ToString("N");
			var source = CreateSaveablePrefab("FilterSource", sourceId);
			var variant = CreateVariantPrefab(source, "FilterVariant", sourceId);
			var valid = CreateSaveablePrefab("FilterHealthy", "healthy-token");
			var mismatch = CreateSaveablePrefab("FilterMismatch", "serialized-token");
			var duplicate = CreateSaveablePrefab("FilterDuplicate", "duplicate-token");
			var duplicateOther = CreateSaveablePrefab("FilterDuplicateOther", "duplicate-token");
			var repeated = CreateSaveablePrefab("FilterRepeated", "repeated-token");
			var independentVariant = CreateVariantPrefab(source, "FilterIndependentVariant", "independent-variant-token");
			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry>
			{
				new PrefabRegistry.PrefabEntry { prefab = valid, uniqueID = "healthy-token" },
				null,
				new PrefabRegistry.PrefabEntry { prefab = sceneObject, uniqueID = "scene-token" },
				new PrefabRegistry.PrefabEntry { prefab = CreatePlainPrefab("FilterPlain"), uniqueID = "plain-token" },
				new PrefabRegistry.PrefabEntry { prefab = mismatch, uniqueID = "registry-token" },
				new PrefabRegistry.PrefabEntry { prefab = duplicate, uniqueID = "duplicate-token" },
				new PrefabRegistry.PrefabEntry { prefab = duplicateOther, uniqueID = "duplicate-token" },
				new PrefabRegistry.PrefabEntry { prefab = repeated, uniqueID = "repeated-token" },
				new PrefabRegistry.PrefabEntry { prefab = repeated, uniqueID = "repeated-token" },
				new PrefabRegistry.PrefabEntry { prefab = variant, uniqueID = "variant-registry-token" },
				new PrefabRegistry.PrefabEntry { prefab = independentVariant, uniqueID = "independent-variant-token" }
			};
			try
			{
				// Later asset saves legitimately normalize inherited variant IDs.
				// Seed the malformed serialized input after fixture construction.
				SetPrefabAssetIdWithoutValidation(variant.GetComponent<SaveablePrefab>(), sourceId);
				PrefabRegistryIdentityIndex.InvalidateAll();
				var rows = PrefabRegistryIdentityUtility.AnalyzeRows(registry);
				Assert.That(rows, Has.Count.EqualTo(11));
				Assert.That(rows[0].StatusSummary, Is.EqualTo("OK"));
				Assert.That(rows[0].HasWarnings, Is.False);
				Assert.That(rows[1].MissingPrefab, Is.True);
				Assert.That(rows[2].InvalidAssetReference, Is.True);
				Assert.That(rows[3].HasSaveablePrefab, Is.False);
				Assert.That(rows[4].IsIDMismatch, Is.True);
				Assert.That(rows[5].IsDuplicateID && rows[6].IsDuplicateID, Is.True);
				Assert.That(rows[7].IsDuplicatePrefabRow && rows[8].IsDuplicatePrefabRow, Is.True);
				Assert.That(rows[9].VariantInheritsSourceID, Is.True);
				string[] statusTerms = { "OK", "Missing prefab", "Not a persistent prefab asset", "No SaveablePrefab",
					"ID mismatch", "Duplicate ID", "Duplicate ID", "Duplicate prefab row", "Duplicate prefab row", "Variant inherits source ID", "OK" };
				for (int i = 0; i < rows.Count; i++)
				{
					Assert.That(PrefabRegistryIdentityUtility.MatchesFilter(rows[i], statusTerms[i]), Is.True, statusTerms[i]);
					Assert.That(rows[i].IsInvalidRegistryEntry, Is.EqualTo(i >= 1 && i <= 3));
					Assert.That(PrefabRegistryIdentityUtility.MatchesFilter(rows[i], "definitely-absent-token"), Is.False);
				}
				var variantRow = rows[10];
				foreach (string field in new[] { variantRow.DisplayName, variantRow.AssetPath, variantRow.AssetGuid,
					variantRow.RegistryID, variantRow.PrefabAssetID, variantRow.VariantSourceName,
					variantRow.VariantSourcePath, variantRow.VariantSourcePrefabAssetID })
				{
					Assert.That(field, Is.Not.Empty);
					Assert.That(PrefabRegistryIdentityUtility.MatchesFilter(variantRow, field.ToUpperInvariant()), Is.True, field);
				}
				Assert.That(PrefabRegistryIdentityUtility.MatchesFilter(rows[4], "FilterMismatch\tregistry-token serialized-token"), Is.True);
				Assert.That(PrefabRegistryIdentityUtility.MatchesFilter(rows[4], "FilterMismatch healthy-token"), Is.False,
					"Tokens must match the same row, not independent rows.");
				Assert.That(PrefabRegistryIdentityUtility.MatchesFilter(rows[0], "  \t"), Is.True);
				Assert.That(mismatch.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo("serialized-token"));
				Assert.That(variant.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(sourceId));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(sceneObject);
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[TestCase("manual", false)]
		[TestCase("manual", true)]
		[TestCase("update", false)]
		[TestCase("update", true)]
		[TestCase("generate", false)]
		[TestCase("generate", true)]
		[TestCase("adopt", false)]
		[TestCase("adopt", true)]
		public void InspectorIdentityRepairActionsUpdateAssetAndInstanceAndUndo(string action, bool selectInstance)
		{
			string prefabId = "CSP_TEST_SERIALIZED_" + Guid.NewGuid().ToString("N");
			string registryId = "CSP_TEST_AUTHORITY_" + Guid.NewGuid().ToString("N");
			var asset = CreateSaveablePrefab("RepairActionAsset", prefabId);
			var registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			var entry = new PrefabRegistry.PrefabEntry { prefab = asset, uniqueID = registryId };
			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry> { entry };
			GameObject instance = null;
			UnityEditor.Editor editor = null;
			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoValidation(activeSaveSettings, false);
					if (selectInstance) instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
					var selected = (instance != null ? instance : asset).GetComponent<SaveablePrefab>();
					editor = UnityEditor.Editor.CreateEditor(selected, typeof(SaveablePrefabEditor));
					Assert.That(selected.PrefabAssetID, Is.EqualTo(prefabId), "Initial selected ID");
					Assert.That(asset.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(prefabId), "Initial asset ID");
					Undo.ClearAll();
					string proposed = action == "adopt" ? registryId : action == "update" ? prefabId :
						"CSP_TEST_MANUAL_" + Guid.NewGuid().ToString("N");
					if (action == "generate")
					{
						var args = new object[] { asset, entry, null };
						proposed = (string)InvokeInspectorAction(editor, "GenerateUnusedPrefabAssetID", args);
						Assert.That(Guid.TryParse(proposed, out _), Is.True, args[2] as string);
						Assert.That(proposed, Is.Not.EqualTo(prefabId).And.Not.EqualTo(registryId));
					}
					if (action == "adopt")
					{
						var mismatches = InvokeInspectorAction(editor, "CollectPrefabAssetIDMismatches");
						Assert.That(((System.Collections.ICollection)mismatches).Count, Is.EqualTo(1));
						InvokeInspectorAction(editor, "AdoptRegistryIDs", mismatches);
					}
					else
					{
						var args = new object[] { selected, asset, entry, "  " + proposed + "  ", "Test " + action, null };
						Assert.That((bool)InvokeInspectorAction(editor, "TryApplyPrefabAssetIDRepair", args), Is.True, args[5] as string);
					}
					Assert.That(selected.PrefabAssetID, Is.EqualTo(proposed));
					Assert.That(asset.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(proposed));
					Assert.That(registry.prefabEntries, Has.Count.EqualTo(1));
					Assert.That(registry.prefabEntries[0].uniqueID, Is.EqualTo(proposed));
					Undo.FlushUndoRecordObjects();
					Undo.PerformUndo();
					Assert.That(selected.PrefabAssetID, Is.EqualTo(prefabId), "Selected object Undo");
					Assert.That(asset.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(prefabId), "Asset Undo");
					Assert.That(registry.prefabEntries[0].uniqueID, Is.EqualTo(registryId), "Registry Undo");
					Assert.That(registry.prefabEntries[0].prefab, Is.EqualTo(asset));
					Undo.PerformRedo();
					Assert.That(selected.PrefabAssetID, Is.EqualTo(proposed), "Selected object Redo");
					Assert.That(asset.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(proposed), "Asset Redo");
					Assert.That(registry.prefabEntries[0].uniqueID, Is.EqualTo(proposed), "Registry Redo");
				}
			}
			finally
			{
				if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
				if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[TestCase("empty")]
		[TestCase("registry-owner")]
		[TestCase("unregistered-asset-owner")]
		[TestCase("duplicate-row")]
		[TestCase("stale-row")]
		public void InspectorIdentityRepairRejectsConflictsWithoutChangingEitherOwner(string conflict)
		{
			string ownId = "CSP_TEST_OWN_" + Guid.NewGuid().ToString("N");
			string otherId = "CSP_TEST_OTHER_" + Guid.NewGuid().ToString("N");
			var asset = CreateSaveablePrefab("RepairRejectedAsset", ownId);
			var other = CreateSaveablePrefab("RepairUnchangedOwner", otherId);
			var registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			var entry = new PrefabRegistry.PrefabEntry { prefab = asset, uniqueID = ownId };
			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry> { entry };
			if (conflict == "registry-owner") registry.prefabEntries.Add(new PrefabRegistry.PrefabEntry { prefab = other, uniqueID = otherId });
			if (conflict == "duplicate-row") registry.prefabEntries.Add(new PrefabRegistry.PrefabEntry { prefab = asset, uniqueID = "conflicting-row" });
			UnityEditor.Editor editor = null;
			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoValidation(activeSaveSettings, false);
					editor = UnityEditor.Editor.CreateEditor(asset.GetComponent<SaveablePrefab>(), typeof(SaveablePrefabEditor));
					var requestedRow = conflict == "stale-row" ? new PrefabRegistry.PrefabEntry { prefab = asset, uniqueID = ownId } : entry;
					string beforeRegistry = EditorJsonUtility.ToJson(registry);
					string proposed = conflict == "empty" ? " \t" : otherId;
					var args = new object[] { asset.GetComponent<SaveablePrefab>(), asset, requestedRow, proposed, "Rejected repair", null };
					Assert.That((bool)InvokeInspectorAction(editor, "TryApplyPrefabAssetIDRepair", args), Is.False);
					Assert.That(args[5] as string, Is.Not.Empty);
					Assert.That(EditorJsonUtility.ToJson(registry), Is.EqualTo(beforeRegistry));
					Assert.That(asset.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(ownId));
					Assert.That(other.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(otherId));
				}
			}
			finally
			{
				if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[UnityTest]
		public IEnumerator InspectorRepairUndoSurvivesDeferredAutomaticValidation()
			=> InspectorUndoWithAutomation(false);

		[UnityTest]
		public IEnumerator InspectorAdoptUndoSurvivesDeferredAutomaticValidation()
			=> InspectorUndoWithAutomation(true);

		private IEnumerator InspectorUndoWithAutomation(bool adopt)
		{
			string oldId = "CSP_TEST_BEFORE_UNDO_" + Guid.NewGuid().ToString("N");
			string registryId = "CSP_TEST_REGISTRY_UNDO_" + Guid.NewGuid().ToString("N");
			string newId = adopt ? registryId : "CSP_TEST_AFTER_UNDO_" + Guid.NewGuid().ToString("N");
			var asset = CreateSaveablePrefab("DeferredUndoAsset", oldId);
			var selected = asset.GetComponent<SaveablePrefab>();
			var registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			var entry = new PrefabRegistry.PrefabEntry { prefab = asset, uniqueID = registryId };
			registry.prefabEntries = new List<PrefabRegistry.PrefabEntry> { entry };
			UnityEditor.Editor editor = null;
			var identityLogs = new List<string>();
			LogLevel oldLogThreshold = Arawn.CrystalSave.Runtime.Logger.LogThreshold;
			Application.LogCallback logCallback = (message, stack, kind) =>
			{
				if (message.Contains("DeferredUndoAsset") && message.Contains("PrefabAssetID"))
					identityLogs.Add(message + "\n" + stack);
			};
			using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
			{
				try
				{
					SetAutoValidation(activeSaveSettings, false);
					editor = UnityEditor.Editor.CreateEditor(selected, typeof(SaveablePrefabEditor));
					// Drain fixture creation/import events before testing the Undo event.
					for (int i = 0; i < 3; i++) yield return null;
					SetAutoRegister(activeSaveSettings, true);
					SetAutoValidation(activeSaveSettings, true);
					ClearSaveablePrefabEditorQueues();
					PrefabRegistryValidationCoordinator.CancelPending(registry);
					Undo.ClearAll();
					Assert.That(selected.PrefabAssetID, Is.EqualTo(oldId));
					if (adopt)
						InvokeInspectorAction(editor, "AdoptRegistryIDs", InvokeInspectorAction(editor, "CollectPrefabAssetIDMismatches"));
					else
					{
						var args = new object[] { selected, asset, entry, newId, "Deferred repair", null };
						Assert.That((bool)InvokeInspectorAction(editor, "TryApplyPrefabAssetIDRepair", args), Is.True, args[5] as string);
					}
					Undo.FlushUndoRecordObjects();
					activeSaveSettings.logLevel = LogLevel.Info;
					CrystalSaveEditorAutomationSettings.UpdateEditorLogLevel(activeSaveSettings);
					Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Info;
					Application.logMessageReceived += logCallback;
					Undo.PerformUndo();
					Assert.That(selected.PrefabAssetID, Is.EqualTo(oldId), "Immediate Undo");
					for (int i = 0; i < 3; i++) yield return null;
					Assert.That(selected.PrefabAssetID, Is.EqualTo(oldId),
						"Undo after automatic editor callbacks\n" + string.Join("\n", identityLogs));
					Assert.That(registry.prefabEntries[0].uniqueID, Is.EqualTo(registryId));
					Undo.PerformRedo();
					for (int i = 0; i < 3; i++) yield return null;
					Assert.That(selected.PrefabAssetID, Is.EqualTo(newId),
						"Redo after automatic editor callbacks\n" + string.Join("\n", identityLogs));
					Assert.That(registry.prefabEntries[0].uniqueID, Is.EqualTo(newId));
					SetPrefabAssetIdWithoutValidation(selected, oldId);
					PrefabRegistryValidationCoordinator.RequestValidation(registry,
						PrefabRegistryValidationCoordinator.ValidationReason.ProjectChanged);
					double deadline = EditorApplication.timeSinceStartup + 5;
					while (selected.PrefabAssetID != newId && EditorApplication.timeSinceStartup < deadline) yield return null;
					Assert.That(selected.PrefabAssetID, Is.EqualTo(newId),
						"A passive notification of changed values must resume normal validation.");
					string nextAuthoredId = "CSP_TEST_NEXT_EDIT_" + Guid.NewGuid().ToString("N");
					var serializedRegistry = new SerializedObject(registry);
					serializedRegistry.FindProperty("prefabEntries").GetArrayElementAtIndex(0)
						.FindPropertyRelative("uniqueID").stringValue = nextAuthoredId;
					serializedRegistry.ApplyModifiedProperties();
					Assert.That(registry.prefabEntries[0].uniqueID, Is.EqualTo(nextAuthoredId), "Authored registry value");
					PrefabRegistryValidationCoordinator.NotifyRegistryEdited(registry);
					deadline = EditorApplication.timeSinceStartup + 5;
					while (selected.PrefabAssetID != nextAuthoredId && EditorApplication.timeSinceStartup < deadline) yield return null;
					Assert.That(selected.PrefabAssetID, Is.EqualTo(nextAuthoredId),
						"An authored edit after Undo/Redo must resume normal validation.\n" + string.Join("\n", identityLogs));
				}
				finally
				{
					Application.logMessageReceived -= logCallback;
					Arawn.CrystalSave.Runtime.Logger.LogThreshold = oldLogThreshold;
					SetAutoRegister(activeSaveSettings, false);
					SetAutoValidation(activeSaveSettings, false);
					if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
					ClearSaveablePrefabEditorQueues();
					PrefabRegistryValidationCoordinator.CancelPending(registry);
					UnityEngine.Object.DestroyImmediate(registry);
				}
			}
		}

		private static object InvokeInspectorAction(UnityEditor.Editor editor, string action, params object[] args)
		{
			var method = typeof(SaveablePrefabEditor).GetMethod(action, BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null, action);
			return method.Invoke(editor, args);
		}

		[TestCase("move")]
		[TestCase("reimport")]
		[TestCase("id-edit")]
		[TestCase("move-reimport-id-edit")]
		public void DelayedRegistrationReloadsCurrentAssetByGuidBeforeApplyingIdentity(string mutation)
		{
			string originalId = "CSP_TEST_QUEUED_" + Guid.NewGuid().ToString("N");
			string expectedId = originalId;
			GameObject prefab = CreateSaveablePrefab("QueuedAsset", originalId);
			string originalPath = AssetDatabase.GetAssetPath(prefab);
			string guid = AssetDatabase.AssetPathToGUID(originalPath);
			var registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, true);
					ClearSaveablePrefabEditorQueues();
					SetSaveablePrefabBatchSaveCount(0);
					QueueSaveablePrefabRegistrySync(prefab.GetComponent<SaveablePrefab>(), prefab, originalId);
					// Preserve the original queued request: validation during the edits
					// must not replace it with a fresh request that hides stale-state bugs.
					SetAutoRegister(activeSaveSettings, false);
					string currentPath = originalPath;
					if (mutation.Contains("move"))
					{
						currentPath = TestRoot + "/MovedQueuedAsset.prefab";
						Assert.That(AssetDatabase.MoveAsset(originalPath, currentPath), Is.Empty);
						var unrelated = CreateSaveablePrefabAtPath("ReplacementAtOldPath",
							"CSP_TEST_UNRELATED_" + Guid.NewGuid().ToString("N"), originalPath);
						Assert.That(AssetDatabase.AssetPathToGUID(originalPath), Is.Not.EqualTo(guid));
						Assert.That(unrelated, Is.Not.Null);
					}
					prefab = AssetDatabase.LoadAssetAtPath<GameObject>(currentPath);
					if (mutation.Contains("id-edit"))
					{
						expectedId = "CSP_TEST_EDITED_" + Guid.NewGuid().ToString("N");
						SetPrefabAssetId(prefab.GetComponent<SaveablePrefab>(), expectedId);
						AssetDatabase.SaveAssets();
					}
					if (mutation.Contains("reimport"))
						AssetDatabase.ImportAsset(currentPath,
							ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
					SetAutoRegister(activeSaveSettings, true);
					FlushSaveablePrefabRegistryQueue();
					var current = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
					Assert.That(registry.prefabEntries, Has.Count.EqualTo(1));
					Assert.That(registry.prefabEntries[0].prefab, Is.EqualTo(current));
					Assert.That(registry.prefabEntries[0].uniqueID, Is.EqualTo(expectedId));
					Assert.That(current.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(expectedId));
					Assert.That(GetSaveablePrefabBatchSaveCount(), Is.EqualTo(1));
					FlushSaveablePrefabRegistryQueue();
					Assert.That(registry.prefabEntries, Has.Count.EqualTo(1));
					Assert.That(GetSaveablePrefabBatchSaveCount(), Is.EqualTo(1));
				}
			}
			finally
			{
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		[Test]
		public void DelayedRegistrationDoesNotClaimReplacementAtDeletedAssetPath()
		{
			string id = "CSP_TEST_DELETED_" + Guid.NewGuid().ToString("N");
			GameObject original = CreateSaveablePrefab("DeletedQueuedAsset", id);
			string path = AssetDatabase.GetAssetPath(original);
			string guid = AssetDatabase.AssetPathToGUID(path);
			var registry = ScriptableObject.CreateInstance<PrefabRegistry>();
			try
			{
				using (new ActiveRegistryOverrideScope(activeSaveSettings, registry))
				{
					SetAutoRegister(activeSaveSettings, true);
					ClearSaveablePrefabEditorQueues();
					SetSaveablePrefabBatchSaveCount(0);
					QueueSaveablePrefabRegistrySync(original.GetComponent<SaveablePrefab>(), original, id);
					SetAutoRegister(activeSaveSettings, false);
					Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
					string replacementId = "CSP_TEST_REPLACEMENT_" + Guid.NewGuid().ToString("N");
					var replacement = CreateSaveablePrefab("ReplacementAsset", replacementId);
					Assert.That(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(replacement), path), Is.Empty);
					Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.EqualTo(guid));
					SetAutoRegister(activeSaveSettings, true);
					FlushSaveablePrefabRegistryQueue();
					Assert.That(registry.prefabEntries, Is.Empty);
					Assert.That(replacement.GetComponent<SaveablePrefab>().PrefabAssetID, Is.EqualTo(replacementId));
					Assert.That(GetSaveablePrefabBatchSaveCount(), Is.Zero);
				}
			}
			finally
			{
				ClearSaveablePrefabEditorQueues();
				UnityEngine.Object.DestroyImmediate(registry);
			}
		}

		private static GameObject CreateSaveablePrefab(
			string name,
			string prefabAssetId,
			bool saveAssets = true)
		{
			string path = AssetDatabase.GenerateUniqueAssetPath($"{TestRoot}/{name}.prefab");
			GameObject temp = new GameObject(name);
			try
			{
				SaveablePrefab saveable = temp.AddComponent<SaveablePrefab>();
				SetPrefabAssetId(saveable, prefabAssetId);
				GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
				if (asset == null)
					throw new InvalidOperationException($"Could not create test prefab '{path}'.");

				SaveablePrefab assetSaveable = asset.GetComponent<SaveablePrefab>();
				SetPrefabAssetId(assetSaveable, prefabAssetId);
				EditorUtility.SetDirty(asset);
				if (saveAssets)
					AssetDatabase.SaveAssets();
				return AssetDatabase.LoadAssetAtPath<GameObject>(path);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(temp);
			}
		}

		private static GameObject CreateSaveablePrefabAtPath(
			string name,
			string prefabAssetId,
			string path)
		{
			GameObject temp = new GameObject(name);
			try
			{
				SaveablePrefab saveable = temp.AddComponent<SaveablePrefab>();
				SetPrefabAssetId(saveable, prefabAssetId);
				GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
				if (asset == null)
					throw new InvalidOperationException($"Could not create test prefab '{path}'.");

				SetPrefabAssetId(asset.GetComponent<SaveablePrefab>(), prefabAssetId);
				EditorUtility.SetDirty(asset);
				AssetDatabase.SaveAssetIfDirty(asset);
				return AssetDatabase.LoadAssetAtPath<GameObject>(path);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(temp);
			}
		}

		private static List<GameObject> CreateSaveablePrefabs(int count)
		{
			var prefabs = new List<GameObject>(count);
			for (int i = 0; i < count; i++)
			{
				prefabs.Add(CreateSaveablePrefab(
					$"IndexedPrefab_{count}_{i}",
					$"CSP_TEST_INDEX_COMPONENT_{count}_{i}",
					saveAssets: false));
			}

			AssetDatabase.SaveAssets();
			return prefabs;
		}

		private static GameObject CreateSaveablePrefabWithChild(string name, string prefabAssetId)
		{
			string path = AssetDatabase.GenerateUniqueAssetPath($"{TestRoot}/{name}.prefab");
			GameObject temp = new GameObject(name);
			try
			{
				SaveablePrefab saveable = temp.AddComponent<SaveablePrefab>();
				SetPrefabAssetId(saveable, prefabAssetId);
				var child = new GameObject("RegistryChildReference");
				child.transform.SetParent(temp.transform, false);
				GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
				if (asset == null)
					throw new InvalidOperationException($"Could not create test prefab '{path}'.");

				SetPrefabAssetId(asset.GetComponent<SaveablePrefab>(), prefabAssetId);
				AssetDatabase.SaveAssets();
				return AssetDatabase.LoadAssetAtPath<GameObject>(path);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(temp);
			}
		}

		private static GameObject CreateVariantPrefab(
			GameObject source,
			string name,
			string prefabAssetId)
		{
			string path = AssetDatabase.GenerateUniqueAssetPath($"{TestRoot}/{name}.prefab");
			GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
			Assert.That(instance, Is.Not.Null);
			try
			{
				SetPrefabAssetIdWithoutValidation(instance.GetComponent<SaveablePrefab>(), prefabAssetId);
				GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
				Assert.That(variant, Is.Not.Null);
				AssetDatabase.SaveAssets();
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
				variant = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				SetPrefabAssetIdWithoutValidation(
					variant.GetComponent<SaveablePrefab>(),
					prefabAssetId);
				return variant;
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(instance);
			}
		}

		private static GameObject CopyPrefabAsset(GameObject source, string name)
		{
			string sourcePath = AssetDatabase.GetAssetPath(source);
			Assert.That(sourcePath, Is.Not.Empty);
			string destinationPath = AssetDatabase.GenerateUniqueAssetPath($"{TestRoot}/{name}.prefab");
			Assert.That(AssetDatabase.CopyAsset(sourcePath, destinationPath), Is.True);
			AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
			GameObject copy = AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath);
			Assert.That(copy, Is.Not.Null);
			return copy;
		}

		private static void InvokeSaveablePrefabOnValidate(SaveablePrefab saveable)
		{
			MethodInfo method = GetSaveablePrefabEditorMethod("OnValidate", BindingFlags.Instance);
			method.Invoke(saveable, null);
		}

		private static void QueueSaveablePrefabRegistrySync(
			SaveablePrefab saveable,
			GameObject prefabAsset,
			string id)
		{
			MethodInfo method = GetSaveablePrefabEditorMethod("QueuePrefabRegistrySync", BindingFlags.Static);
			method.Invoke(null, new object[] { saveable, prefabAsset, id });
		}

		private static void FlushSaveablePrefabRegistryQueue()
		{
			MethodInfo method = GetSaveablePrefabEditorMethod("FlushPendingPrefabRegistrySyncs", BindingFlags.Static);
			method.Invoke(null, null);
		}

		private static void ClearSaveablePrefabEditorQueues()
		{
			MethodInfo method = GetSaveablePrefabEditorMethod("ClearEditorDelayQueues", BindingFlags.Static);
			method.Invoke(null, null);
		}

		private static void EndDiagnosticCounterSessionIfNeeded()
		{
			PrefabRegistry activeRegistry = PrefabRegistryEditorDiagnosticCounters.ActiveRegistry;
			if (activeRegistry != null)
				PrefabRegistryEditorDiagnosticCounters.EndSession(activeRegistry);
		}

		private static MethodInfo GetSaveablePrefabEditorMethod(string name, BindingFlags scope)
		{
			MethodInfo method = typeof(SaveablePrefab).GetMethod(
				name,
				BindingFlags.NonPublic | scope);
			Assert.That(method, Is.Not.Null, $"Could not find SaveablePrefab.{name} for the editor test.");
			return method;
		}

		private static int GetSaveablePrefabBatchSaveCount()
		{
			FieldInfo field = GetSaveablePrefabBatchSaveCountField();
			return (int)field.GetValue(null);
		}

		private static void SetSaveablePrefabBatchSaveCount(int value)
		{
			GetSaveablePrefabBatchSaveCountField().SetValue(null, value);
		}

		private static FieldInfo GetSaveablePrefabBatchSaveCountField()
		{
			FieldInfo field = typeof(SaveablePrefab).GetField(
				"s_PrefabRegistryBatchSaveCount",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null, "Could not access delayed prefab batch save metrics.");
			return field;
		}

		private static int CaptureMatchingLogs(string messageFragment, Action action)
		{
			int matchingLogs = 0;
			Application.LogCallback callback = (condition, stackTrace, type) =>
			{
				if (type == LogType.Log &&
				    condition != null &&
				    condition.IndexOf(messageFragment, StringComparison.Ordinal) >= 0)
				{
					matchingLogs++;
				}
			};

			Application.logMessageReceived += callback;
			try
			{
				action();
			}
			finally
			{
				Application.logMessageReceived -= callback;
			}

			return matchingLogs;
		}

		private sealed class ActiveRegistryOverrideScope : IDisposable
		{
			private readonly FieldInfo cacheField;
			private readonly CrystalSaveAssetOverrides previousOverride;
			private readonly CrystalSaveAssetOverrides testOverride;

			internal ActiveRegistryOverrideScope(SaveSettings settings, PrefabRegistry registry)
			{
				cacheField = typeof(CrystalSaveOverrides).GetField(
					"cached",
					BindingFlags.Static | BindingFlags.NonPublic);
				Assert.That(cacheField, Is.Not.Null, "Could not access the CrystalSaveOverrides editor cache.");

				previousOverride = cacheField.GetValue(null) as CrystalSaveAssetOverrides;
				testOverride = ScriptableObject.CreateInstance<CrystalSaveAssetOverrides>();
				testOverride.saveSettings = settings;
				testOverride.prefabRegistry = registry;
				cacheField.SetValue(null, testOverride);
				CrystalSaveEditorAutomationSettings.ClearCache();
				CrystalSaveEditorAutomationSettings.UpdateEditorLogLevel(settings);
				PrefabRegistryIdentityIndex.InvalidateAll();
			}

			public void Dispose()
			{
				cacheField.SetValue(null, previousOverride);
				CrystalSaveEditorAutomationSettings.ClearCache();
				PrefabRegistryIdentityIndex.InvalidateAll();
				UnityEngine.Object.DestroyImmediate(testOverride);
			}
		}

		private static GameObject CreatePlainPrefab(string name)
		{
			string path = AssetDatabase.GenerateUniqueAssetPath($"{TestRoot}/{name}.prefab");
			GameObject temp = new GameObject(name);
			try
			{
				GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
				if (asset == null)
					throw new InvalidOperationException($"Could not create test prefab '{path}'.");
				return AssetDatabase.LoadAssetAtPath<GameObject>(path);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(temp);
			}
		}

		private static void SetPrefabAssetId(SaveablePrefab saveable, string id)
		{
			SerializedObject serialized = new SerializedObject(saveable);
			SerializedProperty property = serialized.FindProperty("prefabAssetID");
			Assert.That(property, Is.Not.Null);
			property.stringValue = id;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(saveable);
		}

		private static void SetPrefabAssetIdWithoutValidation(SaveablePrefab saveable, string id)
		{
			FieldInfo field = typeof(SaveablePrefab).GetField(
				"prefabAssetID",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null);
			field.SetValue(saveable, id);
			EditorUtility.SetDirty(saveable);
		}

		private static void SetRegisterWithSaveSystem(SaveablePrefab saveable, bool value)
		{
			SerializedObject serialized = new SerializedObject(saveable);
			SerializedProperty property = serialized.FindProperty("RegisterWithSaveSystem");
			Assert.That(property, Is.Not.Null);
			property.boolValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(saveable);
		}

		private static SaveSettings LoadActiveSaveSettings()
		{
			SaveSettings overrideSettings = CrystalSaveOverrides.GetOverride<SaveSettings>();
			if (overrideSettings != null)
				return overrideSettings;

			SaveSettings resourceSettings = Resources.Load<SaveSettings>("SaveSettings");
			if (resourceSettings != null)
				return resourceSettings;

			foreach (string guid in AssetDatabase.FindAssets("t:SaveSettings"))
			{
				SaveSettings settings = AssetDatabase.LoadAssetAtPath<SaveSettings>(
					AssetDatabase.GUIDToAssetPath(guid));
				if (settings != null)
					return settings;
			}

			return null;
		}

		private static void SetAutoRegister(SaveSettings settings, bool value)
		{
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

		private static void SetAutoValidation(SaveSettings settings, bool value)
		{
			SerializedObject serialized = new SerializedObject(settings);
			SerializedProperty property = serialized.FindProperty("enablePrefabRegistryAutoValidation");
			if (property == null)
				return;

			property.boolValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(settings);
			AssetDatabase.SaveAssets();
			CrystalSaveEditorAutomationSettings.ClearCache();
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
				return;

			string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
			if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
				EnsureFolder(parent);

			string folderName = System.IO.Path.GetFileName(path);
			if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
				AssetDatabase.CreateFolder(parent, folderName);
		}
	}
}
#endif
