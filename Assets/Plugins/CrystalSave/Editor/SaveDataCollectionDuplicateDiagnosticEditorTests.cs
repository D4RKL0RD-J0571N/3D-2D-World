#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using Arawn.CrystalSave.Runtime.Examples;
using NUnit.Framework;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	public sealed class SaveDataCollectionDuplicateDiagnosticEditorTests
	{
		private const BindingFlags StaticFlags =
			BindingFlags.Static | BindingFlags.NonPublic;
		private const BindingFlags InstanceFlags =
			BindingFlags.Instance | BindingFlags.Public |
			BindingFlags.NonPublic;

		[Test]
		public void AggregateReportPassesWhenIntentionalCollisionWasPreciselyDetected()
		{
			SaveDataCollectionDuplicateDiagnosticBuilder.DiagnosticRunManifest manifest =
				CreateCompletedManifest();
			var result = new SaveDataCollectionDuplicateDiagnosticResult
			{
				runId = manifest.runId,
				mode = manifest.mode,
				passed = true,
				stages = new List<SaveDataCollectionDuplicateStageResult>
				{
					new()
					{
						name = "Normal global component",
						operationName = "CollectSaveData",
						expectation = "One component is collected once.",
						passed = true,
						details = "One global route and no duplicate key.",
						uniqueKeyCount = 1,
						globalComponentEntryCount = 1
					},
					CreateCopiedIdentityStage(passed: true)
				}
			};

			string report =
				SaveDataCollectionDuplicateDiagnosticBuilder.BuildReportText(
					manifest,
					result);

			StringAssert.Contains("Overall result: **PASS**", report);
			StringAssert.Contains("| PASS | Normal global component |", report);
			StringAssert.Contains("| PASS | Intentional copied identity |", report);
			StringAssert.Contains(
				"Classification: **Copied identity collision across instances**",
				report);
			StringAssert.Contains(
				"Prefer SaveablePrefabFactory for runtime prefab creation",
				report);
			StringAssert.Contains(
				"Original scene setup restored: **PASS**",
				report);
			StringAssert.Contains(
				"Temporary working assets removed: **PASS**",
				report);
			StringAssert.Contains(
				"Runtime result JSON removed: **PASS**",
				report);
		}

		[Test]
		public void FailedCustomReentrantStageReportsClassificationStackAndRecommendation()
		{
			SaveDataCollectionDuplicateDiagnosticBuilder.DiagnosticRunManifest manifest =
				CreateCompletedManifest();
			SaveDataCollectionDuplicateStageResult stage =
				CreateCustomReentrantStage();
			var result = new SaveDataCollectionDuplicateDiagnosticResult
			{
				runId = manifest.runId,
				mode = manifest.mode,
				passed = false,
				stages = new List<SaveDataCollectionDuplicateStageResult>
				{
					stage
				}
			};

			string report =
				SaveDataCollectionDuplicateDiagnosticBuilder.BuildReportText(
					manifest,
					result);

			StringAssert.Contains("Overall result: **FAIL**", report);
			StringAssert.Contains(
				"Classification: **Custom/re-entrant invocation**",
				report);
			StringAssert.Contains(
				"remove the nested or custom SaveData call",
				report);
			StringAssert.Contains("Targeted stack for event 2", report);
			StringAssert.Contains(
				"CustomSerializer.OnBeforeSerialize",
				report);
		}

		[Test]
		public void MultiInstanceCollisionRemainsPrimaryWhenOneCallIsReentrant()
		{
			SaveDataCollectionDuplicateDiagnosticBuilder.DiagnosticRunManifest manifest =
				CreateCompletedManifest();
			SaveDataCollectionDuplicateStageResult stage =
				CreateCopiedIdentityStage(passed: false);
			stage.name = "Mixed copied identity and re-entry";
			SaveDataCollectionDuplicateOccurrenceResult reentrant =
				stage.duplicates[0].occurrences[1];
			reentrant.route = "CustomOrReentrant";
			reentrant.stackTrace =
				"RuntimeCloneB.OnBeforeSerialize()\n" +
				"SaveableComponent.SaveData()";
			stage.duplicates[0].callers[1].routeSummary =
				"CustomOrReentrant x1";

			var result = new SaveDataCollectionDuplicateDiagnosticResult
			{
				runId = manifest.runId,
				mode = manifest.mode,
				passed = false,
				stages =
					new List<SaveDataCollectionDuplicateStageResult>
					{
						stage
					}
			};

			string report =
				SaveDataCollectionDuplicateDiagnosticBuilder.BuildReportText(
					manifest,
					result);

			StringAssert.Contains(
				"Classification: **Copied identity collision across instances**",
				report);
			StringAssert.Contains(
				"Prefer SaveablePrefabFactory for runtime prefab creation",
				report);
			StringAssert.Contains("Targeted stack for event 2", report);
		}

		[Test]
		public void PreservedPayloadEventsReceiveSpecificClassification()
		{
			SaveDataCollectionDuplicateDiagnosticBuilder.DiagnosticRunManifest manifest =
				CreateCompletedManifest();
			SaveDataCollectionDuplicateStageResult stage =
				CreateCustomReentrantStage();
			stage.name = "Preserved pending payload collision";
			foreach (SaveDataCollectionDuplicateOccurrenceResult occurrence in
			         stage.calls)
			{
				occurrence.route = "PendingGlobalPayload";
				occurrence.stackTrace = null;
			}
			foreach (SaveDataCollectionDuplicateOccurrenceResult occurrence in
			         stage.duplicates[0].occurrences)
			{
				occurrence.route = "PendingGlobalPayload";
				occurrence.stackTrace = null;
			}
			stage.duplicates[0].callers[0].routeSummary =
				"PendingGlobalPayload x2";

			var result = new SaveDataCollectionDuplicateDiagnosticResult
			{
				runId = manifest.runId,
				mode = manifest.mode,
				passed = false,
				stages =
					new List<SaveDataCollectionDuplicateStageResult>
					{
						stage
					}
			};

			string report =
				SaveDataCollectionDuplicateDiagnosticBuilder.BuildReportText(
					manifest,
					result);

			StringAssert.Contains(
				"Classification: **Preserved payload key collision**",
				report);
			StringAssert.Contains(
				"Preserved bytes bypass SaveData",
				report);
			StringAssert.Contains("PendingGlobalPayload", report);
		}

		[Test]
		public void UntracedCollisionIsRetainedInReport()
		{
			SaveDataCollectionDuplicateDiagnosticBuilder.DiagnosticRunManifest manifest =
				CreateCompletedManifest();
			var stage = new SaveDataCollectionDuplicateStageResult
			{
				name = "Custom ISaveable collision",
				operationName = "CollectSaveData",
				expectation = "No key is written twice.",
				passed = false,
				details = "A non-SaveableComponent writer shared a key.",
				uniqueKeyCount = 1,
				duplicateCount = 1,
				untracedDuplicateKeys =
					new List<string>
					{
						"plain-go_plain-component"
					}
			};
			var result = new SaveDataCollectionDuplicateDiagnosticResult
			{
				runId = manifest.runId,
				mode = manifest.mode,
				passed = false,
				stages =
					new List<SaveDataCollectionDuplicateStageResult>
					{
						stage
					}
			};

			string report =
				SaveDataCollectionDuplicateDiagnosticBuilder.BuildReportText(
					manifest,
					result);

			StringAssert.Contains("Untraced collection-key collisions", report);
			StringAssert.Contains("plain-go_plain-component", report);
			StringAssert.Contains(
				"No duplicate key had two fully attributed SaveableComponent trace events",
				report);
		}

		[Test]
		public void RepeatedCollectionComparatorIncludesBlobSuffixesAndPayloads()
		{
			MethodInfo comparator =
				typeof(SaveDataCollectionDuplicateDiagnostic).GetMethod(
					"HaveEquivalentPrefabIdentitiesAndBlobPayloads",
					StaticFlags);
			Assert.That(comparator, Is.Not.Null);

			SaveData baseline =
				CreateSaveDataWithBlob("component-a", new byte[] { 1, 2, 3 });
			SaveData identical =
				CreateSaveDataWithBlob("component-a", new byte[] { 1, 2, 3 });
			SaveData changedSuffix =
				CreateSaveDataWithBlob("component-b", new byte[] { 1, 2, 3 });
			SaveData changedPayload =
				CreateSaveDataWithBlob("component-a", new byte[] { 1, 2, 4 });

			Assert.That(
				comparator.Invoke(null, new object[] { baseline, identical }),
				Is.True);
			Assert.That(
				comparator.Invoke(
					null,
					new object[] { baseline, changedSuffix }),
				Is.False);
			Assert.That(
				comparator.Invoke(
					null,
					new object[] { baseline, changedPayload }),
				Is.False);
		}

		[Test]
		public void EditorIdentityIsolationScopeRestoresPreviousOverridesAndIsIdempotent()
		{
			FieldInfo cachedField = typeof(CrystalSaveOverrides).GetField(
				"cached",
				StaticFlags);
			Assert.That(cachedField, Is.Not.Null);
			object previousOverride = cachedField.GetValue(null);

			Type scopeType =
				typeof(SaveDataCollectionDuplicateDiagnosticBuilder)
					.GetNestedType(
						"EditorIdentityIsolationScope",
						BindingFlags.NonPublic);
			Assert.That(scopeType, Is.Not.Null);

			IDisposable scope = null;
			try
			{
				scope = (IDisposable)Activator.CreateInstance(
					scopeType,
					InstanceFlags,
					binder: null,
					args: Array.Empty<object>(),
					culture: null);
				PropertyInfo registryProperty =
					scopeType.GetProperty("Registry", InstanceFlags);
				Assert.That(registryProperty, Is.Not.Null);
				PrefabRegistry isolatedRegistry =
					registryProperty.GetValue(scope) as PrefabRegistry;

				Assert.That(isolatedRegistry, Is.Not.Null);
				Assert.That(
					CrystalSaveOverrides.GetOverride<PrefabRegistry>(),
					Is.SameAs(isolatedRegistry));

				scope.Dispose();
				scope.Dispose();
				scope = null;

				Assert.That(
					cachedField.GetValue(null),
					Is.SameAs(previousOverride));
			}
			finally
			{
				scope?.Dispose();
				cachedField.SetValue(null, previousOverride);
				CrystalSaveEditorAutomationSettings.ClearCache();
				PrefabRegistryIdentityIndex.InvalidateAll();
			}
		}

		[Test]
		public void CaptureReportDoesNotClaimThatTheObservedSaveWroteNoSlot()
		{
			SaveDataCollectionDuplicateDiagnosticBuilder.DiagnosticRunManifest manifest =
				CreateCompletedManifest();
			manifest.mode =
				SaveDataCollectionDuplicateDiagnosticBuilder
					.DiagnosticRunMode.CaptureNextRealSave.ToString();
			var result = new SaveDataCollectionDuplicateDiagnosticResult
			{
				runId = manifest.runId,
				mode = manifest.mode,
				passed = true,
				stages = new List<SaveDataCollectionDuplicateStageResult>
				{
					new()
					{
						name = "Captured user save",
						operationName = "SaveCoroutine",
						passed = true
					}
				}
			};

			string report =
				SaveDataCollectionDuplicateDiagnosticBuilder.BuildReportText(
					manifest,
					result);

			StringAssert.Contains(
				"Save-slot activity: **Determined by the captured user save**",
				report);
			StringAssert.DoesNotContain(
				"Save slots written: **0**",
				report);
		}

		[Test]
		public void AggregateReportRequiresAtLeastOneSuccessfulStage()
		{
			SaveDataCollectionDuplicateDiagnosticBuilder.DiagnosticRunManifest manifest =
				CreateCompletedManifest();
			var result = new SaveDataCollectionDuplicateDiagnosticResult
			{
				runId = manifest.runId,
				mode = manifest.mode,
				passed = true,
				stages = new List<SaveDataCollectionDuplicateStageResult>
				{
					new()
					{
						name = "Failed stage",
						operationName = "CollectSaveData",
						passed = false
					}
				}
			};

			string failedStageReport =
				SaveDataCollectionDuplicateDiagnosticBuilder.BuildReportText(
					manifest,
					result);
			StringAssert.Contains(
				"Overall result: **FAIL**",
				failedStageReport);

			result.stages.Clear();
			string emptyStageReport =
				SaveDataCollectionDuplicateDiagnosticBuilder.BuildReportText(
					manifest,
					result);
			StringAssert.Contains(
				"Overall result: **FAIL**",
				emptyStageReport);
		}

		[Test]
		public void RetainedRuntimeResultCanFinalizeAfterResultFileWasRemoved()
		{
			SaveDataCollectionDuplicateDiagnosticBuilder.DiagnosticRunManifest manifest =
				CreateCompletedManifest();
			manifest.resultFilePath =
				Path.Combine(
					Path.GetTempPath(),
					$"missing-crystal-save-result-{Guid.NewGuid():N}.json");
			var expected = new SaveDataCollectionDuplicateDiagnosticResult
			{
				runId = manifest.runId,
				mode = manifest.mode,
				passed = true,
				stages = new List<SaveDataCollectionDuplicateStageResult>
				{
					new()
					{
						name = "Recovered stage",
						operationName = "CollectSaveData",
						passed = true
					}
				}
			};
			manifest.retainedRuntimeResultJson =
				JsonUtility.ToJson(expected, prettyPrint: false);

			MethodInfo readMethod =
				typeof(SaveDataCollectionDuplicateDiagnosticBuilder)
					.GetMethod(
						"TryReadRuntimeResult",
						StaticFlags);
			Assert.That(readMethod, Is.Not.Null);
			object[] arguments = { manifest, null };

			bool read = (bool)readMethod.Invoke(null, arguments);
			var recovered =
				arguments[1] as SaveDataCollectionDuplicateDiagnosticResult;

			Assert.That(read, Is.True);
			Assert.That(recovered, Is.Not.Null);
			Assert.That(recovered.runId, Is.EqualTo(expected.runId));
			Assert.That(recovered.stages, Has.Count.EqualTo(1));
			Assert.That(
				recovered.stages[0].name,
				Is.EqualTo("Recovered stage"));
		}

		[Test]
		public void ManifestRecoveryUsesValidTemporaryFileBeforeOlderPrimary()
		{
			string directory =
				Path.Combine(
					Path.GetTempPath(),
					$"crystal-save-manifest-test-{Guid.NewGuid():N}");
			Directory.CreateDirectory(directory);
			string primaryPath = Path.Combine(directory, "ActiveRun.json");
			string temporaryPath = primaryPath + ".tmp";
			try
			{
				var older =
					new SaveDataCollectionDuplicateDiagnosticBuilder
						.DiagnosticRunManifest
						{
							runId = "older-primary"
						};
				var newer =
					new SaveDataCollectionDuplicateDiagnosticBuilder
						.DiagnosticRunManifest
						{
							runId = "newer-temporary"
						};
				File.WriteAllText(
					primaryPath,
					JsonUtility.ToJson(older, prettyPrint: true));
				File.WriteAllText(
					temporaryPath,
					JsonUtility.ToJson(newer, prettyPrint: true));

				bool recovered =
					SaveDataCollectionDuplicateDiagnosticBuilder
						.TryReadFirstValidManifest(
							new[] { temporaryPath, primaryPath },
							out SaveDataCollectionDuplicateDiagnosticBuilder
								.DiagnosticRunManifest manifest,
							out string recoveredPath,
							out string failureSummary);

				Assert.That(recovered, Is.True);
				Assert.That(manifest, Is.Not.Null);
				Assert.That(manifest.runId, Is.EqualTo("newer-temporary"));
				Assert.That(recoveredPath, Is.EqualTo(temporaryPath));
				Assert.That(failureSummary, Is.Empty);
			}
			finally
			{
				if (Directory.Exists(directory))
					Directory.Delete(directory, recursive: true);
			}
		}

		private static
			SaveDataCollectionDuplicateDiagnosticBuilder.DiagnosticRunManifest
			CreateCompletedManifest()
		{
			return new
				SaveDataCollectionDuplicateDiagnosticBuilder.DiagnosticRunManifest
				{
					runId = "editor-report-test",
					mode =
						SaveDataCollectionDuplicateDiagnosticBuilder
							.DiagnosticRunMode.IsolatedSuite.ToString(),
					sceneSetupRestored = true,
					workingAssetsRemoved = true,
					resultFileRemoved = true,
					cleanupErrors = new List<string>()
				};
		}

		private static SaveDataCollectionDuplicateStageResult
			CreateCopiedIdentityStage(bool passed)
		{
			const string key = "copied-go_copied-component";
			var firstOccurrence =
				CreateOccurrence(
					key,
					callOrder: 1,
					route: "GlobalComponentCollection",
					unityObjectId: 101,
					hierarchyPath: "RuntimeCloneA/RememberTransform");
			var secondOccurrence =
				CreateOccurrence(
					key,
					callOrder: 2,
					route: "GlobalComponentCollection",
					unityObjectId: 202,
					hierarchyPath: "RuntimeCloneB/RememberTransform");
			var duplicate = new SaveDataCollectionDuplicateEntryResult
			{
				compositeKey = key,
				totalCalls = 2,
				callers = new List<SaveDataCollectionDuplicateCallerResult>
				{
					CreateCaller(firstOccurrence),
					CreateCaller(secondOccurrence)
				},
				occurrences =
					new List<SaveDataCollectionDuplicateOccurrenceResult>
					{
						firstOccurrence,
						secondOccurrence
					}
			};

			return new SaveDataCollectionDuplicateStageResult
			{
				name = "Intentional copied identity",
				operationName = "CollectSaveData",
				expectation =
					"The intentional fault is detected as two live callers sharing one key.",
				passed = passed,
				details = passed
					? "The expected collision was attributed to both clones."
					: "The collision was not accepted by the stage evaluator.",
				uniqueKeyCount = 1,
				duplicateCount = 1,
				globalComponentEntryCount = 2,
				calls =
					new List<SaveDataCollectionDuplicateOccurrenceResult>
					{
						firstOccurrence,
						secondOccurrence
					},
				duplicates =
					new List<SaveDataCollectionDuplicateEntryResult>
					{
						duplicate
					}
			};
		}

		private static SaveDataCollectionDuplicateStageResult
			CreateCustomReentrantStage()
		{
			const string key = "reentrant-go_reentrant-component";
			var firstOccurrence =
				CreateOccurrence(
					key,
					callOrder: 1,
					route: "GlobalComponentCollection",
					unityObjectId: 303,
					hierarchyPath: "RuntimeObject/CustomSerializer");
			var secondOccurrence =
				CreateOccurrence(
					key,
					callOrder: 2,
					route: "CustomOrReentrant",
					unityObjectId: 303,
					hierarchyPath: "RuntimeObject/CustomSerializer");
			secondOccurrence.stackTrace =
				"CustomSerializer.OnBeforeSerialize()\n" +
				"SaveableComponent.SaveData()";
			var duplicate = new SaveDataCollectionDuplicateEntryResult
			{
				compositeKey = key,
				totalCalls = 2,
				callers =
					new List<SaveDataCollectionDuplicateCallerResult>
					{
						new()
						{
							componentType = "CustomSerializer",
							hierarchyPath =
								"RuntimeObject/CustomSerializer",
							gameObjectId = "reentrant-go",
							componentId = "reentrant-component",
							unityObjectId = 303,
							callCount = 2,
							routeSummary =
								"GlobalComponentCollection x1, CustomOrReentrant x1"
						}
					},
				occurrences =
					new List<SaveDataCollectionDuplicateOccurrenceResult>
					{
						firstOccurrence,
						secondOccurrence
					}
			};

			return new SaveDataCollectionDuplicateStageResult
			{
				name = "Unexpected custom re-entry",
				operationName = "CollectSaveData",
				expectation = "One call through the global route.",
				passed = false,
				details = "A serialization callback called SaveData again.",
				uniqueKeyCount = 1,
				duplicateCount = 1,
				calls =
					new List<SaveDataCollectionDuplicateOccurrenceResult>
					{
						firstOccurrence,
						secondOccurrence
					},
				duplicates =
					new List<SaveDataCollectionDuplicateEntryResult>
					{
						duplicate
					}
			};
		}

		private static SaveDataCollectionDuplicateOccurrenceResult
			CreateOccurrence(
				string compositeKey,
				int callOrder,
				string route,
				int unityObjectId,
				string hierarchyPath)
		{
			return new SaveDataCollectionDuplicateOccurrenceResult
			{
				compositeKey = compositeKey,
				callOrder = callOrder,
				route = route,
				componentType = "RememberTransform",
				hierarchyPath = hierarchyPath,
				sceneName = "Diagnostic",
				scenePath = "Assets/Diagnostic.unity",
				gameObjectId = compositeKey.Split('_')[0],
				componentId = compositeKey.Substring(
					compositeKey.IndexOf('_') + 1),
				unityObjectId = unityObjectId,
				registrationSource = "Lifecycle",
				globallyRegistered = true
			};
		}

		private static SaveDataCollectionDuplicateCallerResult
			CreateCaller(
				SaveDataCollectionDuplicateOccurrenceResult occurrence)
		{
			return new SaveDataCollectionDuplicateCallerResult
			{
				componentType = occurrence.componentType,
				hierarchyPath = occurrence.hierarchyPath,
				sceneName = occurrence.sceneName,
				scenePath = occurrence.scenePath,
				gameObjectId = occurrence.gameObjectId,
				componentId = occurrence.componentId,
				unityObjectId = occurrence.unityObjectId,
				callCount = 1,
				globallyRegistered = occurrence.globallyRegistered,
				registrationSource = occurrence.registrationSource,
				routeSummary = occurrence.route + " x1"
			};
		}

		private static SaveData CreateSaveDataWithBlob(
			string suffix,
			byte[] payload)
		{
			var modifications = new RuntimeModificationData();
			modifications.ComponentBlobs.Add(
				new ComponentBlob(suffix, payload));
			var prefab = new SaveablePrefabData(
				"diagnostic-instance",
				"diagnostic-prefab",
				"Diagnostic Prefab",
				Vector3.zero,
				Quaternion.identity,
				Vector3.one,
				null,
				false,
				null,
				"Diagnostic")
			{
				RuntimeModificationData =
					SaveDataSerializer.Instance.Serialize(modifications)
			};
			var data = new SaveData(new VersionData(1, 0, 0));
			data.Prefabs.Add(prefab);
			return data;
		}
	}
}
#endif
