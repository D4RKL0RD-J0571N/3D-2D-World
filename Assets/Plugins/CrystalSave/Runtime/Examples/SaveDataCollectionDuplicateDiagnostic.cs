#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime.Examples
{
	/// <summary>
	/// Runs generated fixtures through the real SaveManager collection path.
	/// The companion editor builder creates and removes the scene automatically.
	/// </summary>
	[AddComponentMenu("")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed class SaveDataCollectionDuplicateDiagnostic : MonoBehaviour
	{
		private const string SuiteMode = "Isolated Play Mode suite";
		private const string CaptureMode = "Capture next real save";
		private const string LogPrefix = "[CrystalSave][SaveDataCollectionDuplicateDiag]";

		private const string NormalGameObjectId = "CSP_DIAG_NORMAL_GO";
		private const string SameComponentOtherGameObjectId = "CSP_DIAG_SAME_COMPONENT_OTHER_GO";
		private const string SharedControlComponentId = "CSP_DIAG_SHARED_COMPONENT";
		private const string CollisionGameObjectId = "CSP_DIAG_COLLISION_GO";
		private const string CollisionComponentId = "CSP_DIAG_COLLISION_COMPONENT";
		private const string BlobComponentId = "CSP_DIAG_BLOB_COMPONENT";
		private const string TransitionComponentId = "CSP_DIAG_TRANSITION_COMPONENT";
		private const string BulkScanComponentId = "CSP_DIAG_BULK_SCAN_COMPONENT";
		private const string NestedComponentId = "CSP_DIAG_NESTED_COMPONENT";

		[SerializeField] private bool runOnStart = true;
		[SerializeField] private string runId;
		[SerializeField] private string resultFilePath;
		[SerializeField] private int managerTimeoutFrames = 900;

		[SerializeField] private RememberTransform normalComponent;
		[SerializeField] private RememberTransform sameComponentIdOtherRoot;
		[SerializeField] private SaveablePrefab blobPrefab;
		[SerializeField] private RememberTransform blobComponent;
		[SerializeField] private SaveablePrefab transitionPrefab;
		[SerializeField] private RememberTransform transitionComponent;
		[SerializeField] private RememberTransform collisionFirst;
		[SerializeField] private RememberTransform collisionSecond;
		[SerializeField] private SaveablePrefab bulkScanPrefab;
		[SerializeField] private RememberTransform bulkScanComponent;
		[SerializeField] private SaveablePrefab nestedOuterPrefab;
		[SerializeField] private SaveablePrefab nestedInnerPrefab;
		[SerializeField] private RememberTransform nestedComponent;

		private static readonly object CaptureLock = new();
		private static string armedCaptureRunId;
		private static string armedCaptureResultPath;
		private static bool captureArmed;

		private bool running;

		private IEnumerable<SaveableComponent> KnownComponents
		{
			get
			{
				yield return normalComponent;
				yield return sameComponentIdOtherRoot;
				yield return blobComponent;
				yield return transitionComponent;
				yield return collisionFirst;
				yield return collisionSecond;
				yield return bulkScanComponent;
				yield return nestedComponent;
			}
		}

		private IEnumerable<SaveablePrefab> KnownPrefabs
		{
			get
			{
				yield return blobPrefab;
				yield return transitionPrefab;
				yield return bulkScanPrefab;
				yield return nestedOuterPrefab;
				yield return nestedInnerPrefab;
			}
		}

		private void Start()
		{
			if (runOnStart)
				StartCoroutine(RunSuite());
		}

		/// <summary>
		/// Arms a one-shot observer for the next persisted Crystal Save operation.
		/// Public only to cross Unity's predefined runtime/editor assembly boundary.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryArmNextRealSaveCapture(
			string captureRunId,
			string captureResultFilePath,
			out string error)
		{
			error = null;
			if (!Application.isPlaying)
			{
				error = "Capture Next Real Save can only be armed in Play Mode.";
				return false;
			}

			if (string.IsNullOrWhiteSpace(captureRunId) ||
			    string.IsNullOrWhiteSpace(captureResultFilePath))
			{
				error = "The diagnostic run ID and result path are required.";
				return false;
			}

			lock (CaptureLock)
			{
				if (captureArmed)
				{
					error = "A Capture Next Real Save request is already armed.";
					return false;
				}

				armedCaptureRunId = captureRunId;
				armedCaptureResultPath = captureResultFilePath;
				captureArmed = true;
			}

			try
			{
					SaveableComponent.ArmNextSaveCallDiagnosticCaptureWithAbort(
					HandleNextRealSaveCaptured,
					HandleNextRealSaveAborted);
				Debug.Log($"{LogPrefix} Armed diagnostics for the next persisted save operation.");
				return true;
			}
			catch (Exception ex)
			{
				ClearArmedCaptureState();
				error = $"Could not arm the next-save diagnostic hook: {ex.Message}";
				return false;
			}
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void CancelNextRealSaveCapture()
		{
			try
			{
				SaveableComponent.CancelNextSaveCallDiagnosticCapture();
			}
			finally
			{
				ClearArmedCaptureState();
			}
		}

		private static void ClearArmedCaptureState()
		{
			lock (CaptureLock)
			{
				captureArmed = false;
				armedCaptureRunId = null;
				armedCaptureResultPath = null;
			}
		}

		private static void HandleNextRealSaveCaptured(
			SaveableComponent.SaveCallDiagnosticSnapshot snapshot)
		{
			string capturedRunId;
			string capturedResultPath;
			lock (CaptureLock)
			{
				if (!captureArmed)
					return;

				capturedRunId = armedCaptureRunId;
				capturedResultPath = armedCaptureResultPath;
				captureArmed = false;
				armedCaptureRunId = null;
				armedCaptureResultPath = null;
			}

			SaveDataCollectionDuplicateDiagnosticResult result =
				CreateResult(capturedRunId, CaptureMode);
			try
			{
				SaveDataCollectionDuplicateStageResult stage =
					CreateStageFromSnapshot(
						"Next persisted save operation",
						snapshot,
						null);
				stage.expectation =
					"The next real persisted save completes and exposes every collection route, caller, and duplicate key.";
				stage.passed = !snapshot.HasDuplicates;
				stage.details = snapshot.HasDuplicates
					? "The real save completed, but duplicate save-data collection activity was captured."
					: "The real save completed with no duplicate save-data collection activity.";
				result.stages.Add(stage);
				result.passed = stage.passed;
			}
			catch (Exception ex)
			{
				result.fatalError = ex.ToString();
				result.passed = false;
			}

			result.completedAtUtc = DateTime.UtcNow.ToString("O");
			TryWriteResult(capturedResultPath, result);
		}

		private static void HandleNextRealSaveAborted(string reason)
		{
			string capturedRunId;
			string capturedResultPath;
			lock (CaptureLock)
			{
				if (!captureArmed)
					return;

				capturedRunId = armedCaptureRunId;
				capturedResultPath = armedCaptureResultPath;
				captureArmed = false;
				armedCaptureRunId = null;
				armedCaptureResultPath = null;
			}

			string failureReason = string.IsNullOrWhiteSpace(reason)
				? "The persisted save-data collection was aborted."
				: reason;
			SaveDataCollectionDuplicateDiagnosticResult result =
				CreateResult(capturedRunId, CaptureMode);
			result.passed = false;
			result.fatalError =
				$"The captured save-data collection aborted: {failureReason}";
			result.stages.Add(
				new SaveDataCollectionDuplicateStageResult
				{
					name = "Next persisted save operation",
					operationName = "Aborted save-data collection",
					expectation =
						"The next real persisted save completes so its SaveData routes can be inspected.",
					passed = false,
					details = result.fatalError
				});
			result.completedAtUtc = DateTime.UtcNow.ToString("O");
			TryWriteResult(capturedResultPath, result);
		}

		private IEnumerator RunSuite()
		{
			if (running)
				yield break;

			running = true;
			SaveDataCollectionDuplicateDiagnosticResult result =
				CreateResult(runId, SuiteMode);
			Exception failure = null;
			var iteratorStack = new Stack<IEnumerator>();
			iteratorStack.Push(RunSuiteSteps(result));

			try
			{
				while (iteratorStack.Count > 0)
				{
					IEnumerator iterator = iteratorStack.Peek();
					bool movedNext;
					object yielded = null;
					try
					{
						movedNext = iterator.MoveNext();
						if (movedNext)
							yielded = iterator.Current;
					}
					catch (Exception ex)
					{
						failure = ex;
						break;
					}

					if (!movedNext)
					{
						(iterator as IDisposable)?.Dispose();
						iteratorStack.Pop();
						continue;
					}

					if (yielded is IEnumerator nestedIterator)
					{
						iteratorStack.Push(nestedIterator);
						continue;
					}

					yield return yielded;
				}

				if (failure != null)
				{
					SaveableComponent.AbortSaveCallDiagnostics();
					result.fatalError = failure.ToString();
					result.passed = false;
				}
				else
				{
					result.passed = result.stages.Count > 0 &&
					                result.stages.All(stage => stage.passed);
				}
			}
			finally
			{
				SaveableComponent.AbortSaveCallDiagnostics();
				while (iteratorStack.Count > 0)
					(iteratorStack.Pop() as IDisposable)?.Dispose();
				result.completedAtUtc = DateTime.UtcNow.ToString("O");
				TryWriteResult(resultFilePath, result);
				running = false;
			}
		}

		private IEnumerator RunSuiteSteps(
			SaveDataCollectionDuplicateDiagnosticResult result)
		{
			yield return WaitForSaveManager();
			SaveManager manager = SaveManager.Instance;
			if (manager == null ||
			    !SaveManager.IsInitialized ||
			    manager.ComponentManager == null ||
			    manager.GetPrefabManager == null)
			{
				throw new InvalidOperationException(
					"SaveManager, ComponentManager, or PrefabManager was unavailable before the timeout.");
			}

			ValidateFixtureReferences();
			ApplyDeterministicRuntimeIdentities();
			PrepareBaseline(manager, includeSecondCollision: true);
			yield return null;

			// Ambient calls outside Begin/Complete must never contaminate a persisted pass.
			normalComponent.SaveData();
			normalComponent.SaveData();

			int baselineUniqueKeyCount = 5;
			string collisionKey = collisionFirst.UniqueIdentifier;
			SaveDataCollectionDuplicateStageResult collisionControl =
				CollectStage(manager, "Intentional two-instance key collision");
			collisionControl.expectation =
				"Exactly one known key is reported with two calls from two component instances.";
			collisionControl.passed =
				collisionControl.duplicateCount == 1 &&
				collisionControl.uniqueKeyCount == baselineUniqueKeyCount &&
				HasExactDuplicate(collisionControl, collisionKey, 2, 2, 1) &&
				HasExactCallRoute(
					collisionControl,
					collisionKey,
					SaveableComponent.SaveDataCallRoute.GlobalComponentCollection,
					2);
			collisionControl.details = collisionControl.passed
				? "The negative control was detected exactly, proving diagnostics are active."
				: "The negative control did not produce the expected two-caller duplicate.";
			result.stages.Add(collisionControl);

			collisionSecond.enabled = false;
			yield return null;

			CollectionOutcome cleanBaselineOutcome =
				CollectOutcome(
					manager,
					"Global, blob, and composite-key ownership controls");
			SaveDataCollectionDuplicateStageResult cleanBaseline =
				cleanBaselineOutcome.Stage;
			cleanBaseline.targetPrefabBlobCount = CountComponentBlobs(
				FindPrefabData(
					cleanBaselineOutcome.Data,
					blobPrefab.UniqueID));
			cleanBaseline.expectation =
				"Global and blob routes each call once; equal Component IDs on different GameObject IDs remain distinct.";
			cleanBaseline.passed =
				cleanBaseline.duplicateCount == 0 &&
				cleanBaseline.uniqueKeyCount == baselineUniqueKeyCount &&
				manager.ComponentManager.Contains(normalComponent) &&
				manager.ComponentManager.Contains(sameComponentIdOtherRoot) &&
				manager.ComponentManager.Contains(transitionComponent) &&
				!manager.ComponentManager.Contains(blobComponent) &&
				cleanBaseline.targetPrefabBlobCount == 1 &&
				normalComponent.ComponentID == sameComponentIdOtherRoot.ComponentID &&
				normalComponent.UniqueIdentifier != sameComponentIdOtherRoot.UniqueIdentifier &&
				ContainsGlobalKey(cleanBaseline, normalComponent.UniqueIdentifier) &&
				ContainsGlobalKey(cleanBaseline, sameComponentIdOtherRoot.UniqueIdentifier) &&
				ContainsGlobalKey(cleanBaseline, transitionComponent.UniqueIdentifier) &&
				!ContainsGlobalKey(cleanBaseline, blobComponent.UniqueIdentifier) &&
				HasExactCallRoute(
					cleanBaseline,
					normalComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.GlobalComponentCollection,
					1) &&
				HasExactCallRoute(
					cleanBaseline,
					sameComponentIdOtherRoot.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.GlobalComponentCollection,
					1) &&
				HasExactCallRoute(
					cleanBaseline,
					transitionComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.GlobalComponentCollection,
					1) &&
				HasExactCallRoute(
					cleanBaseline,
					blobComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.PrefabComponentBlob,
					1) &&
				HasExactRegistrationSource(
					cleanBaseline,
					normalComponent.UniqueIdentifier,
					"ComponentManager") &&
				HasExactRegistrationSource(
					cleanBaseline,
					sameComponentIdOtherRoot.UniqueIdentifier,
					"ComponentManager") &&
				HasExactRegistrationSource(
					cleanBaseline,
					transitionComponent.UniqueIdentifier,
					"ComponentManager");
			cleanBaseline.details = cleanBaseline.passed
				? "The baseline used the intended routes and both halves of the composite key."
				: "The baseline had duplicate, missing, or incorrectly routed data.";
			result.stages.Add(cleanBaseline);

			yield return RunLiveRootCloneStage(
				manager,
				result,
				baselineUniqueKeyCount);
			yield return RunClonedChildStage(
				manager,
				result,
				baselineUniqueKeyCount);
			yield return RunDisabledBulkScanStage(manager, result);
			yield return RunExplicitRegistrationUtilityStage(manager, result);
			yield return RunMidCollectionOwnershipToggleStage(manager, result);
			yield return RunCustomReentrantRouteStage(manager, result);
			yield return RunUnknownRouteStage(manager, result);

			PrepareBaseline(manager, includeSecondCollision: false);
			yield return null;

			transitionPrefab.TrackComponentBlobs = true;
			bool falseToTrueReconciledImmediately =
				!manager.ComponentManager.IsGloballyRegisteredForDiagnostics(
					transitionComponent) &&
				transitionComponent.PrefabHandlesSerialization;
			yield return null;
			CollectionOutcome falseToTrueOutcome =
				CollectOutcome(
					manager,
					"Runtime TrackComponentBlobs false-to-true");
			SaveDataCollectionDuplicateStageResult falseToTrue =
				falseToTrueOutcome.Stage;
			falseToTrue.targetPrefabBlobCount = CountComponentBlobs(
				FindPrefabData(
					falseToTrueOutcome.Data,
					transitionPrefab.UniqueID));
			falseToTrue.expectation =
				"An active helper transfers from global ownership to one prefab-blob call immediately.";
			falseToTrue.passed =
				falseToTrueReconciledImmediately &&
				falseToTrue.duplicateCount == 0 &&
				falseToTrue.uniqueKeyCount == baselineUniqueKeyCount &&
				!manager.ComponentManager.Contains(transitionComponent) &&
				falseToTrue.targetPrefabBlobCount == 1 &&
				!ContainsGlobalKey(falseToTrue, transitionComponent.UniqueIdentifier) &&
				HasExactCallRoute(
					falseToTrue,
					transitionComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.PrefabComponentBlob,
					1) &&
				HasExactRegistrationSource(
					falseToTrue,
					transitionComponent.UniqueIdentifier,
					"PrefabOwnershipTransfer");
			falseToTrue.details = falseToTrue.passed
				? "The runtime ownership transfer produced one blob-owned call."
				: "The helper remained global, was omitted, or was serialized more than once.";
			result.stages.Add(falseToTrue);

			transitionComponent.enabled = false;
			yield return null;
			transitionComponent.enabled = true;
			yield return null;
			CollectionOutcome blobLifecycleOutcome =
				CollectOutcome(
					manager,
					"Blob-owned lifecycle reconciliation");
			SaveDataCollectionDuplicateStageResult blobLifecycle =
				blobLifecycleOutcome.Stage;
			blobLifecycle.targetPrefabBlobCount = CountComponentBlobs(
				FindPrefabData(
					blobLifecycleOutcome.Data,
					transitionPrefab.UniqueID));
			blobLifecycle.expectation =
				"Disable/enable while blob-owned remains one prefab call with no global entry.";
			blobLifecycle.passed =
				blobLifecycle.duplicateCount == 0 &&
				blobLifecycle.uniqueKeyCount == baselineUniqueKeyCount &&
				!manager.ComponentManager.Contains(transitionComponent) &&
				blobLifecycle.targetPrefabBlobCount == 1 &&
				!ContainsGlobalKey(blobLifecycle, transitionComponent.UniqueIdentifier) &&
				HasExactCallRoute(
					blobLifecycle,
					transitionComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.PrefabComponentBlob,
					1) &&
				HasExactRegistrationSource(
					blobLifecycle,
					transitionComponent.UniqueIdentifier,
					"PrefabOwnershipTransfer");
			blobLifecycle.details = blobLifecycle.passed
				? "Lifecycle refresh converged on the prefab-owned route."
				: "Lifecycle refresh did not converge on one prefab-owned route.";
			result.stages.Add(blobLifecycle);

			transitionPrefab.TrackComponentBlobs = false;
			bool trueToFalseReconciledImmediately =
				manager.ComponentManager.IsGloballyRegisteredForDiagnostics(
					transitionComponent) &&
				!transitionComponent.PrefabHandlesSerialization;
			yield return null;
			CollectionOutcome trueToFalseOutcome =
				CollectOutcome(
					manager,
					"Runtime TrackComponentBlobs true-to-false");
			SaveDataCollectionDuplicateStageResult trueToFalse =
				trueToFalseOutcome.Stage;
			trueToFalse.targetPrefabBlobCount = CountComponentBlobs(
				FindPrefabData(
					trueToFalseOutcome.Data,
					transitionPrefab.UniqueID));
			trueToFalse.expectation =
				"An active helper transfers from blob ownership to one global call immediately.";
			trueToFalse.passed =
				trueToFalseReconciledImmediately &&
				trueToFalse.duplicateCount == 0 &&
				trueToFalse.uniqueKeyCount == baselineUniqueKeyCount &&
				manager.ComponentManager.Contains(transitionComponent) &&
				trueToFalse.targetPrefabBlobCount == 0 &&
				ContainsGlobalKey(trueToFalse, transitionComponent.UniqueIdentifier) &&
				HasExactCallRoute(
					trueToFalse,
					transitionComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.GlobalComponentCollection,
					1) &&
				HasExactRegistrationSource(
					trueToFalse,
					transitionComponent.UniqueIdentifier,
					"PrefabOwnershipTransfer");
			trueToFalse.details = trueToFalse.passed
				? "The runtime ownership transfer produced one global call."
				: "The helper was omitted, stayed blob-owned, or was serialized more than once.";
			result.stages.Add(trueToFalse);

			transitionComponent.enabled = false;
			yield return null;
			transitionComponent.enabled = true;
			yield return null;
			CollectionOutcome globalLifecycleOutcome =
				CollectOutcome(
					manager,
					"Global lifecycle reconciliation");
			SaveDataCollectionDuplicateStageResult globalLifecycle =
				globalLifecycleOutcome.Stage;
			globalLifecycle.targetPrefabBlobCount = CountComponentBlobs(
				FindPrefabData(
					globalLifecycleOutcome.Data,
					transitionPrefab.UniqueID));
			globalLifecycle.expectation =
				"Disable/enable after disabling blobs yields one global call.";
			globalLifecycle.passed =
				globalLifecycle.duplicateCount == 0 &&
				globalLifecycle.uniqueKeyCount == baselineUniqueKeyCount &&
				manager.ComponentManager.Contains(transitionComponent) &&
				globalLifecycle.targetPrefabBlobCount == 0 &&
				ContainsGlobalKey(globalLifecycle, transitionComponent.UniqueIdentifier) &&
				HasExactCallRoute(
					globalLifecycle,
					transitionComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.GlobalComponentCollection,
					1) &&
				HasExactRegistrationSource(
					globalLifecycle,
					transitionComponent.UniqueIdentifier,
					"Lifecycle");
			globalLifecycle.details = globalLifecycle.passed
				? "Lifecycle refresh converged on the global route."
				: "Lifecycle refresh did not converge on one global route.";
			result.stages.Add(globalLifecycle);

			normalComponent.SaveData();
			CollectionOutcome repeatedCleanOutcome =
				CollectOutcome(
					manager,
					"Immediate repeated clean collection");
			SaveDataCollectionDuplicateStageResult repeatedClean =
				repeatedCleanOutcome.Stage;
			repeatedClean.expectation =
				"A consecutive real collection is identical; the ambient pre-Begin call is ignored.";
			repeatedClean.passed =
				repeatedClean.duplicateCount == 0 &&
				repeatedClean.uniqueKeyCount == globalLifecycle.uniqueKeyCount &&
				HaveEquivalentGlobalKeys(repeatedClean, globalLifecycle) &&
				HaveEquivalentCallRoutes(repeatedClean, globalLifecycle) &&
				HaveEquivalentComponentPayloads(
					repeatedCleanOutcome.Data,
					globalLifecycleOutcome.Data) &&
				HaveEquivalentPrefabIdentitiesAndBlobPayloads(
					repeatedCleanOutcome.Data,
					globalLifecycleOutcome.Data);
			repeatedClean.details = repeatedClean.passed
				? "No records leaked from the prior operation."
				: "The repeated collection differed or retained earlier diagnostic state.";
			result.stages.Add(repeatedClean);

			yield return RunNestedNearestOwnerStage(manager, result);
			yield return RunExceptionAbortStage(
				manager,
				result,
				baselineUniqueKeyCount);
		}

		private IEnumerator RunLiveRootCloneStage(
			SaveManager manager,
			SaveDataCollectionDuplicateDiagnosticResult result,
			int expectedUniqueKeys)
		{
			GameObject clone = Instantiate(normalComponent.gameObject);
			clone.name = "CSP_Diag_LiveRootClone";
			yield return null;

			RememberTransform clonedComponent = clone.GetComponent<RememberTransform>();
			SaveDataCollectionDuplicateStageResult stage =
				CollectStage(manager, "Live root raw-clone collision");
			stage.expectation =
				"A raw clone retaining the live root/component IDs is diagnosed as two callers for one key.";
			stage.passed =
				clonedComponent != null &&
				clonedComponent.UniqueIdentifier == normalComponent.UniqueIdentifier &&
				stage.duplicateCount == 1 &&
				stage.uniqueKeyCount == expectedUniqueKeys &&
				HasExactDuplicate(
					stage,
					normalComponent.UniqueIdentifier,
					2,
					2,
					1) &&
				HasExactCallRoute(
					stage,
					normalComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.GlobalComponentCollection,
					2);
			stage.details = stage.passed
				? "The raw live-root clone collision was attributed to both component instances."
				: "The raw clone did not retain the key or was not diagnosed as two callers.";
			result.stages.Add(stage);

			if (clonedComponent != null)
			{
				clonedComponent.enabled = false;
				if (manager.ComponentManager.Contains(clonedComponent))
					manager.ComponentManager.UnregisterSaveableComponent(
						clonedComponent);
			}
			clone.SetActive(false);
			yield return null;
		}

		private IEnumerator RunClonedChildStage(
			SaveManager manager,
			SaveDataCollectionDuplicateDiagnosticResult result,
			int expectedUniqueKeys)
		{
			GameObject clonedChild = Instantiate(
				blobComponent.gameObject,
				blobPrefab.transform,
				worldPositionStays: false);
			clonedChild.name = "CSP_Diag_ClonedBlobChild";
			yield return null;

			RememberTransform clonedComponent =
				clonedChild.GetComponent<RememberTransform>();
			SaveDataCollectionDuplicateStageResult stage =
				CollectStage(manager, "Cloned child component-key collision");
			stage.expectation =
				"A cloned child under the same prefab root is diagnosed as two blob-route callers for one key.";
			stage.passed =
				clonedComponent != null &&
				clonedComponent.UniqueIdentifier == blobComponent.UniqueIdentifier &&
				stage.duplicateCount == 1 &&
				stage.uniqueKeyCount == expectedUniqueKeys &&
				HasExactDuplicate(
					stage,
					blobComponent.UniqueIdentifier,
					2,
					2,
					1) &&
				HasExactCallRoute(
					stage,
					blobComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.PrefabComponentBlob,
					2);
			stage.details = stage.passed
				? "The cloned child collision retained both caller identities and the blob route."
				: "The cloned child collision was missing or had an unexpected call shape.";
			result.stages.Add(stage);

			GameObject quarantine =
				new GameObject("CSP_Diag_InactiveCloneQuarantine");
			quarantine.SetActive(false);
			if (clonedComponent != null)
			{
				clonedComponent.enabled = false;
				if (manager.ComponentManager.Contains(clonedComponent))
					manager.ComponentManager.UnregisterSaveableComponent(
						clonedComponent);
			}
			clonedChild.transform.SetParent(
				quarantine.transform,
				worldPositionStays: false);
			// Reparenting correctly transfers the disabled helper from blob ownership
			// to the global manager. This fixture is finished, so explicitly remove that
			// newly established global route before subsequent independent stages.
			if (clonedComponent != null &&
			    manager.ComponentManager.Contains(clonedComponent))
			{
				manager.ComponentManager.UnregisterSaveableComponent(
					clonedComponent);
			}
			yield return null;
		}

		private IEnumerator RunDisabledBulkScanStage(
			SaveManager manager,
			SaveDataCollectionDuplicateDiagnosticResult result)
		{
			PrepareIsolatedPrefabStage(manager, bulkScanPrefab);
			yield return null;

			var registrationsBeforeScan =
				new HashSet<ISaveable>(
					manager.ComponentManager.GetSaveableComponents());
			manager.RegisterExistingSaveableComponentsInScene(
				SceneManager.GetActiveScene());
			bool targetRegistered =
				manager.ComponentManager.Contains(bulkScanComponent);
			ISaveable[] registrationsAddedByScan =
				manager.ComponentManager.GetSaveableComponents()
					.Where(
						component =>
							component != null &&
							!registrationsBeforeScan.Contains(component))
					.ToArray();
			foreach (ISaveable addedByScan in registrationsAddedByScan)
				manager.ComponentManager.UnregisterSaveableComponent(
					addedByScan);

			CollectionOutcome outcome =
				CollectOutcome(
					manager,
					"Disabled helper manager-wide registration scan");
			SaveDataCollectionDuplicateStageResult stage = outcome.Stage;
			stage.targetPrefabBlobCount = CountComponentBlobs(
				FindPrefabData(
					outcome.Data,
					bulkScanPrefab.UniqueID));
			stage.expectation =
				"A manager-wide scan must not globally register an inactive helper owned by Track Component Blobs.";
			stage.targetGloballyRegistered = targetRegistered;
			stage.passed =
				!targetRegistered &&
				stage.duplicateCount == 0 &&
				stage.targetPrefabBlobCount == 1 &&
				!ContainsGlobalKey(stage, bulkScanComponent.UniqueIdentifier) &&
				HasExactCallRoute(
					stage,
					bulkScanComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.PrefabComponentBlob,
					1);
			stage.details = stage.passed
				? "The inactive blob-owned helper remained exclusively prefab-owned."
				: "The scan registered the inactive blob-owned helper or produced a duplicate route.";
			result.stages.Add(stage);

			PrepareBaseline(manager, includeSecondCollision: false);
			yield return null;
		}

		private IEnumerator RunExplicitRegistrationUtilityStage(
			SaveManager manager,
			SaveDataCollectionDuplicateDiagnosticResult result)
		{
			PrepareIsolatedPrefabStage(manager, bulkScanPrefab);
			yield return null;

			SaveableRegistrationUtility.SaveableRegistrationResult registration =
				SaveableRegistrationUtility.RegisterSaveables(
					bulkScanPrefab.gameObject,
					includeChildren: true);
			bool targetRegistered =
				manager.ComponentManager.Contains(bulkScanComponent);

			CollectionOutcome outcome =
				CollectOutcome(
					manager,
					"Explicit SaveableRegistrationUtility ownership");
			SaveDataCollectionDuplicateStageResult stage = outcome.Stage;
			stage.targetPrefabBlobCount = CountComponentBlobs(
				FindPrefabData(
					outcome.Data,
					bulkScanPrefab.UniqueID));
			stage.expectation =
				"The explicit utility recognizes prefab-blob ownership and does not add a global helper route.";
			stage.utilityRegisteredComponents = registration.RegisteredComponents;
			stage.utilityPrefabRegistered = registration.PrefabRegistered;
			stage.targetGloballyRegistered = targetRegistered;
			stage.passed =
				registration.RegisteredComponents == 0 &&
				!targetRegistered &&
				stage.duplicateCount == 0 &&
				stage.targetPrefabBlobCount == 1 &&
				!ContainsGlobalKey(stage, bulkScanComponent.UniqueIdentifier) &&
				HasExactCallRoute(
					stage,
					bulkScanComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.PrefabComponentBlob,
					1);
			stage.details = stage.passed
				? "Explicit registration preserved the single prefab-owned route."
				: "Explicit registration added the blob-owned helper to ComponentManager.";
			result.stages.Add(stage);

			PrepareBaseline(manager, includeSecondCollision: false);
			yield return null;
		}

		private IEnumerator RunMidCollectionOwnershipToggleStage(
			SaveManager manager,
			SaveDataCollectionDuplicateDiagnosticResult result)
		{
			PrepareIsolatedPrefabStage(manager);

			GameObject root =
				new GameObject("CSP_Diag_MidCollectionOwnershipPrefab");
			root.SetActive(false);
			UniqueID rootId = root.AddComponent<UniqueID>();
			rootId.ID = "CSP_DIAG_MID_COLLECTION_INSTANCE";
			SaveablePrefab prefab = root.AddComponent<SaveablePrefab>();
			prefab.PrefabAssetID = "CSP_DIAG_MID_COLLECTION_ASSET";
			prefab.SetUniqueID(rootId.ID);
			prefab.TrackComponentBlobs = true;

			var firstObject = new GameObject("CSP_Diag_MidCollectionFirst");
			firstObject.transform.SetParent(root.transform, false);
			var first =
				firstObject.AddComponent<SaveDataCollectionOwnershipToggleComponent>();
			first.Configure(
				rootId.ID,
				"CSP_DIAG_MID_COLLECTION_FIRST");

			var secondObject = new GameObject("CSP_Diag_MidCollectionSecond");
			secondObject.transform.SetParent(root.transform, false);
			var second =
				secondObject.AddComponent<SaveDataCollectionOwnershipToggleComponent>();
			second.Configure(
				rootId.ID,
				"CSP_DIAG_MID_COLLECTION_SECOND");

			root.SetActive(true);
			SetPrefabParticipation(manager, prefab, true);
			yield return null;

			first.BeforeSerialize = () => prefab.TrackComponentBlobs = false;
			CollectionOutcome blobToGlobalOutcome =
				CollectOutcome(
					manager,
					"TrackComponentBlobs true-to-false inside blob collection");
			SaveDataCollectionDuplicateStageResult blobToGlobal =
				blobToGlobalOutcome.Stage;
			blobToGlobal.targetPrefabBlobCount = CountComponentBlobs(
				FindPrefabData(
					blobToGlobalOutcome.Data,
					prefab.UniqueID));
			blobToGlobal.expectation =
				"A two-helper prefab keeps its original blob route for the entire collection when helper one disables blob tracking.";
			blobToGlobal.passed =
				blobToGlobal.duplicateCount == 0 &&
				blobToGlobal.uniqueKeyCount == 2 &&
				blobToGlobal.targetPrefabBlobCount == 2 &&
				!ContainsGlobalKey(blobToGlobal, first.UniqueIdentifier) &&
				!ContainsGlobalKey(blobToGlobal, second.UniqueIdentifier) &&
				HasExactCallRoute(
					blobToGlobal,
					first.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.PrefabComponentBlob,
					1) &&
				HasExactCallRoute(
					blobToGlobal,
					second.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.PrefabComponentBlob,
					1) &&
				!prefab.TrackComponentBlobs &&
				manager.ComponentManager.Contains(first) &&
				manager.ComponentManager.Contains(second);
			blobToGlobal.details = blobToGlobal.passed
				? "Both helpers stayed on the blob route, then transferred globally after collection."
				: "The mid-collection toggle omitted, duplicated, or prematurely rerouted a helper.";
			result.stages.Add(blobToGlobal);

			first.ResetInvocationCount();
			second.ResetInvocationCount();
			first.BeforeSerialize = () => prefab.TrackComponentBlobs = true;
			CollectionOutcome globalToBlobOutcome =
				CollectOutcome(
					manager,
					"TrackComponentBlobs false-to-true inside global collection");
			SaveDataCollectionDuplicateStageResult globalToBlob =
				globalToBlobOutcome.Stage;
			globalToBlob.targetPrefabBlobCount = CountComponentBlobs(
				FindPrefabData(
					globalToBlobOutcome.Data,
					prefab.UniqueID));
			globalToBlob.expectation =
				"Both global helpers remain on the original global route when helper one enables blob tracking during collection.";
			globalToBlob.passed =
				globalToBlob.duplicateCount == 0 &&
				globalToBlob.uniqueKeyCount == 2 &&
				globalToBlob.targetPrefabBlobCount == 0 &&
				ContainsGlobalKey(globalToBlob, first.UniqueIdentifier) &&
				ContainsGlobalKey(globalToBlob, second.UniqueIdentifier) &&
				HasExactCallRoute(
					globalToBlob,
					first.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.GlobalComponentCollection,
					1) &&
				HasExactCallRoute(
					globalToBlob,
					second.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.GlobalComponentCollection,
					1) &&
				prefab.TrackComponentBlobs &&
				!manager.ComponentManager.Contains(first) &&
				!manager.ComponentManager.Contains(second);
			globalToBlob.details = globalToBlob.passed
				? "Both helpers stayed global, then transferred to blob ownership after collection."
				: "The reverse mid-collection toggle omitted, duplicated, or prematurely rerouted a helper.";
			result.stages.Add(globalToBlob);

			SetPrefabParticipation(manager, prefab, false);
			if (manager.ComponentManager.Contains(first))
				manager.ComponentManager.UnregisterSaveableComponent(first);
			if (manager.ComponentManager.Contains(second))
				manager.ComponentManager.UnregisterSaveableComponent(second);
			Destroy(root);
			PrepareBaseline(manager, includeSecondCollision: false);
			yield return null;
		}

		private IEnumerator RunCustomReentrantRouteStage(
			SaveManager manager,
			SaveDataCollectionDuplicateDiagnosticResult result)
		{
			DisableAllPrefabParticipation(manager);
			UnregisterKnownComponents(manager.ComponentManager);

			GameObject probeObject =
				new GameObject("CSP_Diag_CustomReentrantRoute");
			probeObject.SetActive(false);
			UniqueID uniqueId = probeObject.AddComponent<UniqueID>();
			uniqueId.ID = "CSP_DIAG_REENTRANT_GO";
			SaveDataCollectionReentrantComponent probe =
				probeObject.AddComponent<SaveDataCollectionReentrantComponent>();
			probe.Configure(
				"CSP_DIAG_REENTRANT_GO",
				"CSP_DIAG_REENTRANT_COMPONENT");
			probeObject.SetActive(true);
			yield return null;

			EnsureGloballyRegistered(manager.ComponentManager, probe);
			string key = probe.UniqueIdentifier;
			SaveDataCollectionDuplicateStageResult stage =
				CollectStage(manager, "Intentional custom/re-entrant route");
			SaveDataCollectionDuplicateOccurrenceResult customOccurrence =
				stage.calls.FirstOrDefault(
					call =>
						string.Equals(
							call.compositeKey,
							key,
							StringComparison.Ordinal) &&
						string.Equals(
							call.route,
							SaveableComponent.SaveDataCallRoute.CustomOrReentrant.ToString(),
							StringComparison.Ordinal));
			stage.expectation =
				"One intentional nested SaveData call is classified as CustomOrReentrant and retains a targeted stack.";
			stage.passed =
				stage.duplicateCount == 1 &&
				stage.uniqueKeyCount == 1 &&
				HasExactDuplicate(stage, key, 2, 1, 2) &&
				HasExactCallRoute(
					stage,
					key,
					SaveableComponent.SaveDataCallRoute.GlobalComponentCollection,
					1,
					allowOtherRoutes: true) &&
				customOccurrence != null &&
				!string.IsNullOrWhiteSpace(customOccurrence.stackTrace);
			stage.details = stage.passed
				? "The intentional nested call was attributed to CustomOrReentrant with a captured stack."
				: "The intentional nested call was missing, misclassified, or lacked a targeted stack.";
			result.stages.Add(stage);

			if (manager.ComponentManager.Contains(probe))
				manager.ComponentManager.UnregisterSaveableComponent(probe);
			Destroy(probeObject);
			PrepareBaseline(manager, includeSecondCollision: false);
			yield return null;
		}

		private IEnumerator RunUnknownRouteStage(
			SaveManager manager,
			SaveDataCollectionDuplicateDiagnosticResult result)
		{
			DisableAllPrefabParticipation(manager);
			UnregisterKnownComponents(manager.ComponentManager);

			SaveableComponent.SaveCallDiagnosticSnapshot snapshot = null;
			SaveDataCollectionDuplicateStageResult stage;
			SaveableComponent.BeginSaveCallDiagnostics(
				"Intentional unknown route",
				forceDeepTracing: true);
			try
			{
				normalComponent.SaveData();
				snapshot =
					SaveableComponent.CompleteSaveCallDiagnostics();
				stage = CreateStageFromSnapshot(
					"Intentional unknown route",
					snapshot,
					null);
			}
			catch
			{
				SaveableComponent.AbortSaveCallDiagnostics();
				throw;
			}
			finally
			{
				snapshot?.ReleaseComponentReferences();
			}

			SaveDataCollectionDuplicateOccurrenceResult occurrence =
				stage.calls.SingleOrDefault();
			stage.expectation =
				"A direct diagnostic SaveData call outside a package collection route is classified as Unknown with a targeted stack.";
			stage.passed =
				stage.duplicateCount == 0 &&
				stage.uniqueKeyCount == 1 &&
				occurrence != null &&
				string.Equals(
					occurrence.compositeKey,
					normalComponent.UniqueIdentifier,
					StringComparison.Ordinal) &&
				string.Equals(
					occurrence.route,
					SaveableComponent.SaveDataCallRoute.Unknown.ToString(),
					StringComparison.Ordinal) &&
				!string.IsNullOrWhiteSpace(occurrence.stackTrace);
			stage.details = stage.passed
				? "The direct call was classified as Unknown and retained its targeted stack."
				: "The direct call was missing, misclassified, or lacked a targeted stack.";
			result.stages.Add(stage);

			PrepareBaseline(manager, includeSecondCollision: false);
			yield return null;
		}

		private IEnumerator RunNestedNearestOwnerStage(
			SaveManager manager,
			SaveDataCollectionDuplicateDiagnosticResult result)
		{
			PrepareIsolatedPrefabStage(
				manager,
				nestedOuterPrefab,
				nestedInnerPrefab);
			nestedComponent.transform.localPosition =
				new Vector3(1.25f, 2.5f, 3.75f);
			yield return null;

			CollectionOutcome outcome =
				CollectOutcome(manager, "Nested nearest-owner capture and restore");
			SaveDataCollectionDuplicateStageResult stage = outcome.Stage;
			stage.expectation =
				"A nested helper is captured only by its nearest SaveablePrefab and restores from that blob.";

			SaveablePrefabData outerData =
				FindPrefabData(outcome.Data, nestedOuterPrefab.UniqueID);
			SaveablePrefabData innerData =
				FindPrefabData(outcome.Data, nestedInnerPrefab.UniqueID);
			stage.nestedOuterBlobCount = CountComponentBlobs(outerData);
			stage.nestedInnerBlobCount = CountComponentBlobs(innerData);

			Vector3 expected = nestedComponent.transform.localPosition;
			nestedComponent.transform.localPosition =
				new Vector3(19f, 18f, 17f);
			if (innerData?.RuntimeModificationData != null)
				nestedInnerPrefab.ApplyRuntimeModifications(
					innerData.RuntimeModificationData);
			yield return null;

			stage.restoreVerified =
				Approximately(nestedComponent.transform.localPosition, expected);
			stage.passed =
				stage.duplicateCount == 0 &&
				stage.uniqueKeyCount == 1 &&
				stage.nestedOuterBlobCount == 0 &&
				stage.nestedInnerBlobCount == 1 &&
				stage.restoreVerified &&
				HasExactCallRoute(
					stage,
					nestedComponent.UniqueIdentifier,
					SaveableComponent.SaveDataCallRoute.PrefabComponentBlob,
					1) &&
				stage.calls.Any(
					call =>
						string.Equals(
							call.compositeKey,
							nestedComponent.UniqueIdentifier,
							StringComparison.Ordinal) &&
						string.Equals(
							call.routeOwnerInstanceId,
							nestedInnerPrefab.UniqueID,
							StringComparison.Ordinal));
			stage.details = stage.passed
				? "Only the nearest nested owner captured the blob and restored its transform."
				: "The helper overlapped owners, the blob counts were wrong, or restore failed.";
			result.stages.Add(stage);

			PrepareBaseline(manager, includeSecondCollision: false);
			yield return null;
		}

		private IEnumerator RunExceptionAbortStage(
			SaveManager manager,
			SaveDataCollectionDuplicateDiagnosticResult result,
			int expectedUniqueKeys)
		{
			GameObject throwingObject =
				new GameObject("CSP_Diag_IntentionalThrowingComponent");
			throwingObject.SetActive(false);
			UniqueID throwingId = throwingObject.AddComponent<UniqueID>();
			throwingId.ID = "CSP_DIAG_THROWING_GO";
			SaveDataCollectionThrowingComponent throwing =
				throwingObject.AddComponent<SaveDataCollectionThrowingComponent>();
			throwing.Configure(
				"CSP_DIAG_THROWING_GO",
				"CSP_DIAG_THROWING_COMPONENT");
			throwingObject.SetActive(true);
			yield return null;

			bool exceptionObserved = false;
			string throwingKey = throwing.UniqueIdentifier;
			SaveableComponent.BeginSaveCallDiagnostics(
				"Intentional exception abort");
			try
			{
				throwing.SaveData();
			}
			catch (InvalidOperationException)
			{
				exceptionObserved = true;
			}
			finally
			{
				SaveableComponent.AbortSaveCallDiagnostics();
			}

			Destroy(throwingObject);
			yield return null;

			SaveDataCollectionDuplicateStageResult stage =
				CollectStage(manager, "Exception abort isolation");
			stage.expectation =
				"An aborted throwing cycle leaves no key, caller, route, or stack data in the next real collection.";
			stage.intentionalExceptionObserved = exceptionObserved;
			stage.passed =
				exceptionObserved &&
				stage.duplicateCount == 0 &&
				stage.uniqueKeyCount == expectedUniqueKeys &&
				!ContainsGlobalKey(stage, throwingKey) &&
				stage.calls.All(
					call => !string.Equals(
						call.compositeKey,
						throwingKey,
						StringComparison.Ordinal)) &&
				stage.duplicates.All(
					duplicate => !string.Equals(
						duplicate.compositeKey,
						throwingKey,
						StringComparison.Ordinal));
			stage.details = stage.passed
				? "Abort cleared the throwing cycle before the next collection."
				: "The intentional exception was missed or its diagnostic state leaked.";
			result.stages.Add(stage);
		}

		private IEnumerator WaitForSaveManager()
		{
			int frame = 0;
			int limit = Mathf.Max(1, managerTimeoutFrames);
			while ((SaveManager.Instance == null ||
			        !SaveManager.IsInitialized ||
			        SaveManager.Instance.ComponentManager == null ||
			        SaveManager.Instance.GetPrefabManager == null) &&
			       frame < limit)
			{
				frame++;
				yield return null;
			}
		}

		private void ValidateFixtureReferences()
		{
			if (KnownComponents.Any(component => component == null) ||
			    KnownPrefabs.Any(prefab => prefab == null))
			{
				throw new InvalidOperationException(
					"The generated diagnostic scene has missing fixture references.");
			}
		}

		private void ApplyDeterministicRuntimeIdentities()
		{
			ForceIdentity(
				normalComponent,
				NormalGameObjectId,
				SharedControlComponentId);
			ForceIdentity(
				sameComponentIdOtherRoot,
				SameComponentOtherGameObjectId,
				SharedControlComponentId);
			ForceIdentity(
				collisionFirst,
				CollisionGameObjectId,
				CollisionComponentId);
			ForceIdentity(
				collisionSecond,
				CollisionGameObjectId,
				CollisionComponentId);

			blobComponent.ComponentID = BlobComponentId;
			transitionComponent.ComponentID = TransitionComponentId;
			bulkScanComponent.ComponentID = BulkScanComponentId;
			nestedComponent.ComponentID = NestedComponentId;

			if (collisionFirst.UniqueIdentifier != collisionSecond.UniqueIdentifier)
			{
				throw new InvalidOperationException(
					"The runtime collision controls did not converge on one composite key.");
			}
		}

		private static void ForceIdentity(
			SaveableComponent component,
			string gameObjectId,
			string componentId)
		{
			component.ComponentID = componentId;
			UniqueID uniqueId = component.GetComponent<UniqueID>();
			if (uniqueId != null)
				uniqueId.ID = gameObjectId;
			component.OverrideGameObjectID(gameObjectId);
		}

		private void PrepareBaseline(
			SaveManager manager,
			bool includeSecondCollision)
		{
			DisableAllPrefabParticipation(manager);
			UnregisterKnownComponents(manager.ComponentManager);

			transitionPrefab.TrackComponentBlobs = false;
			SetPrefabParticipation(manager, blobPrefab, true);
			SetPrefabParticipation(manager, transitionPrefab, true);

			EnsureGloballyRegistered(manager.ComponentManager, normalComponent);
			EnsureGloballyRegistered(
				manager.ComponentManager,
				sameComponentIdOtherRoot);
			EnsureGloballyRegistered(
				manager.ComponentManager,
				transitionComponent);
			EnsureGloballyRegistered(
				manager.ComponentManager,
				collisionFirst);
			if (includeSecondCollision)
			{
				collisionSecond.enabled = true;
				EnsureGloballyRegistered(
					manager.ComponentManager,
					collisionSecond);
			}
			else
			{
				collisionSecond.enabled = false;
			}
		}

		private void PrepareIsolatedPrefabStage(
			SaveManager manager,
			params SaveablePrefab[] participants)
		{
			DisableAllPrefabParticipation(manager);
			UnregisterKnownComponents(manager.ComponentManager);
			foreach (SaveablePrefab participant in participants)
				SetPrefabParticipation(manager, participant, true);
		}

		private void DisableAllPrefabParticipation(SaveManager manager)
		{
			foreach (SaveablePrefab prefab in KnownPrefabs)
				SetPrefabParticipation(manager, prefab, false);
		}

		private void UnregisterKnownComponents(ComponentManager manager)
		{
			foreach (SaveableComponent component in KnownComponents)
			{
				if (component != null && manager.Contains(component))
					manager.UnregisterSaveableComponent(component);
			}
		}

		private static void SetPrefabParticipation(
			SaveManager manager,
			SaveablePrefab prefab,
			bool enabled)
		{
			if (prefab == null)
				return;

			if (enabled)
			{
				prefab.RegisterWithSaveSystem = true;
				prefab.RegisterForSaving();
				manager.GetPrefabManager.RegisterPrefab(prefab);
			}
			else
			{
				prefab.RegisterWithSaveSystem = false;
				prefab.UnregisterFromSaving();
				manager.GetPrefabManager.UnregisterPrefab(prefab);
			}
		}

		private static void EnsureGloballyRegistered(
			ComponentManager manager,
			SaveableComponent component)
		{
			if (!manager.Contains(component))
				manager.RegisterSaveableComponent(component);
		}

		private static SaveDataCollectionDuplicateStageResult CollectStage(
			SaveManager manager,
			string stageName)
		{
			return CollectOutcome(manager, stageName).Stage;
		}

		private static CollectionOutcome CollectOutcome(
			SaveManager manager,
			string stageName)
		{
			SaveData data;
			SaveableComponent.SaveCallDiagnosticSnapshot snapshot;
			SaveableComponent.BeginSaveCallDiagnostics(
				stageName,
				forceDeepTracing: true);
			try
			{
				data = manager.CollectSaveData();
				snapshot = SaveableComponent.CompleteSaveCallDiagnostics();
			}
			catch
			{
				SaveableComponent.AbortSaveCallDiagnostics();
				throw;
			}

			try
			{
				return new CollectionOutcome(
					CreateStageFromSnapshot(stageName, snapshot, data),
					data);
			}
			finally
			{
				snapshot.ReleaseComponentReferences();
			}
		}

		private static SaveDataCollectionDuplicateStageResult CreateStageFromSnapshot(
			string stageName,
			SaveableComponent.SaveCallDiagnosticSnapshot snapshot,
			SaveData data)
		{
			var stage = new SaveDataCollectionDuplicateStageResult
			{
				name = stageName,
				operationName = snapshot?.OperationName ?? string.Empty,
				uniqueKeyCount = snapshot?.UniqueKeyCount ?? 0,
				prefabEntryCount = data?.Prefabs?.Count ?? 0,
				globalComponentEntryCount = data?.ComponentsData?.Count ?? 0
			};

			if (data?.ComponentsData != null)
			{
				foreach (string key in data.ComponentsData.Keys)
					stage.globalComponentKeys.Add(key);
				stage.globalComponentKeys.Sort(StringComparer.Ordinal);
			}

			if (snapshot?.Calls != null)
			{
				foreach (SaveableComponent.SaveCallOccurrenceDiagnostic call in snapshot.Calls)
					stage.calls.Add(CreateOccurrenceResult(call));
			}

			if (snapshot?.Duplicates != null)
			{
				foreach (SaveableComponent.DuplicateSaveCallDiagnostic duplicate in snapshot.Duplicates)
				{
					if (duplicate == null)
						continue;

					var duplicateResult =
						new SaveDataCollectionDuplicateEntryResult
						{
							compositeKey = duplicate.CompositeKey,
							totalCalls = duplicate.TotalCalls
						};

					foreach (SaveableComponent.SaveCallCallerDiagnostic caller in duplicate.Callers)
					{
						if (caller == null)
							continue;
						duplicateResult.callers.Add(CreateCallerResult(caller));
					}

					foreach (SaveableComponent.SaveCallOccurrenceDiagnostic occurrence in duplicate.Occurrences)
						duplicateResult.occurrences.Add(
							CreateOccurrenceResult(occurrence));

					stage.duplicates.Add(duplicateResult);
				}
			}

			var duplicateKeys = new HashSet<string>(
				stage.duplicates.Select(item => item.compositeKey),
				StringComparer.Ordinal);
			if (snapshot?.UntracedDuplicateKeys != null)
			{
				foreach (string key in snapshot.UntracedDuplicateKeys)
				{
					if (!string.IsNullOrEmpty(key) &&
					    duplicateKeys.Add(key))
					{
						stage.untracedDuplicateKeys.Add(key);
					}
				}
			}
			stage.untracedDuplicateKeys.Sort(StringComparer.Ordinal);
			stage.duplicateCount = duplicateKeys.Count;

			return stage;
		}

		private static SaveDataCollectionDuplicateCallerResult CreateCallerResult(
			SaveableComponent.SaveCallCallerDiagnostic caller)
		{
			return new SaveDataCollectionDuplicateCallerResult
			{
				componentType = caller.ComponentType?.FullName ?? "<unknown>",
				hierarchyPath = caller.HierarchyPath,
				sceneName = caller.SceneName,
				scenePath = caller.ScenePath,
				gameObjectId = caller.GameObjectId,
				componentId = caller.ComponentId,
				unityObjectId = caller.UnityObjectId,
				callCount = caller.CallCount,
				nearestPrefabName = caller.NearestPrefabName,
				nearestPrefabInstanceId = caller.NearestPrefabInstanceId,
				nearestPrefabAssetId = caller.NearestPrefabAssetId,
				blobOwned = caller.BlobOwned,
				globallyRegistered = caller.GloballyRegistered,
				registrationSource = caller.RegistrationSource,
				routeSummary = caller.RouteSummary
			};
		}

		private static SaveDataCollectionDuplicateOccurrenceResult CreateOccurrenceResult(
			SaveableComponent.SaveCallOccurrenceDiagnostic occurrence)
		{
			return new SaveDataCollectionDuplicateOccurrenceResult
			{
				compositeKey = occurrence.CompositeKey,
				callOrder = occurrence.CallOrder,
				route = occurrence.Route.ToString(),
				componentType = occurrence.ComponentType,
				hierarchyPath = occurrence.HierarchyPath,
				sceneName = occurrence.SceneName,
				scenePath = occurrence.ScenePath,
				gameObjectId = occurrence.GameObjectId,
				componentId = occurrence.ComponentId,
				unityObjectId = occurrence.UnityObjectId,
				nearestPrefabName = occurrence.NearestPrefabName,
				nearestPrefabInstanceId = occurrence.NearestPrefabInstanceId,
				nearestPrefabAssetId = occurrence.NearestPrefabAssetId,
				blobOwned = occurrence.BlobOwned,
				globallyRegistered = occurrence.GloballyRegistered,
				registrationSource = occurrence.RegistrationSource,
				routeOwnerName = occurrence.RouteOwnerName,
				routeOwnerInstanceId = occurrence.RouteOwnerInstanceId,
				routeOwnerAssetId = occurrence.RouteOwnerAssetId,
				stackTrace = occurrence.StackTrace
			};
		}

		private static bool HasExactDuplicate(
			SaveDataCollectionDuplicateStageResult stage,
			string compositeKey,
			int expectedTotalCalls,
			int expectedCallerCount,
			int expectedCallsPerCaller)
		{
			foreach (SaveDataCollectionDuplicateEntryResult duplicate in stage.duplicates)
			{
				if (!string.Equals(
					    duplicate.compositeKey,
					    compositeKey,
					    StringComparison.Ordinal) ||
				    duplicate.totalCalls != expectedTotalCalls ||
				    duplicate.callers.Count != expectedCallerCount)
				{
					continue;
				}

				if (duplicate.callers.All(
					    caller => caller.callCount == expectedCallsPerCaller))
				{
					return true;
				}
			}

			return false;
		}

		private static bool ContainsGlobalKey(
			SaveDataCollectionDuplicateStageResult stage,
			string key)
		{
			return stage.globalComponentKeys.Contains(key);
		}

		private static bool HasExactCallRoute(
			SaveDataCollectionDuplicateStageResult stage,
			string compositeKey,
			SaveableComponent.SaveDataCallRoute route,
			int expectedCount,
			bool allowOtherRoutes = false)
		{
			string routeName = route.ToString();
			bool expectedRouteCount = stage.calls.Count(
				call =>
					string.Equals(
						call.compositeKey,
						compositeKey,
						StringComparison.Ordinal) &&
					string.Equals(
						call.route,
						routeName,
						StringComparison.Ordinal)) == expectedCount;
			return expectedRouteCount &&
			       (allowOtherRoutes ||
			        stage.calls.Count(
				       call => string.Equals(
					       call.compositeKey,
					       compositeKey,
				       StringComparison.Ordinal)) == expectedCount);
		}

		private static bool HasExactRegistrationSource(
			SaveDataCollectionDuplicateStageResult stage,
			string compositeKey,
			string expectedSource)
		{
			List<SaveDataCollectionDuplicateOccurrenceResult> calls =
				stage.calls.Where(
						call => string.Equals(
							call.compositeKey,
							compositeKey,
							StringComparison.Ordinal))
					.ToList();
			return calls.Count > 0 &&
			       calls.All(
				       call => string.Equals(
					       call.registrationSource,
					       expectedSource,
					       StringComparison.Ordinal));
		}

		private static bool HaveEquivalentGlobalKeys(
			SaveDataCollectionDuplicateStageResult first,
			SaveDataCollectionDuplicateStageResult second)
		{
			return first.globalComponentKeys.SequenceEqual(
				second.globalComponentKeys,
				StringComparer.Ordinal);
		}

		private static bool HaveEquivalentCallRoutes(
			SaveDataCollectionDuplicateStageResult first,
			SaveDataCollectionDuplicateStageResult second)
		{
			IEnumerable<string> FirstRoutes()
			{
				return first.calls
					.Select(call => $"{call.compositeKey}\u001f{call.route}")
					.OrderBy(value => value, StringComparer.Ordinal);
			}

			IEnumerable<string> SecondRoutes()
			{
				return second.calls
					.Select(call => $"{call.compositeKey}\u001f{call.route}")
					.OrderBy(value => value, StringComparer.Ordinal);
			}

			return FirstRoutes().SequenceEqual(
				SecondRoutes(),
				StringComparer.Ordinal);
		}

		private static bool HaveEquivalentComponentPayloads(
			SaveData first,
			SaveData second)
		{
			if (ReferenceEquals(first?.ComponentsData, second?.ComponentsData))
				return true;
			if (first?.ComponentsData == null ||
			    second?.ComponentsData == null ||
			    first.ComponentsData.Count != second.ComponentsData.Count)
				return false;

			foreach (KeyValuePair<string, byte[]> pair in
			         first.ComponentsData)
			{
				if (!second.ComponentsData.TryGetValue(
					    pair.Key,
					    out byte[] otherPayload))
					return false;
				if (!(pair.Value ?? Array.Empty<byte>())
				    .SequenceEqual(otherPayload ?? Array.Empty<byte>()))
					return false;
			}

			return true;
		}

		private static bool HaveEquivalentPrefabIdentitiesAndBlobPayloads(
			SaveData first,
			SaveData second)
		{
			List<SaveablePrefabData> OrderedPrefabs(SaveData data)
			{
				if (data?.Prefabs == null)
					return new List<SaveablePrefabData>();
				return data.Prefabs
					.Where(entry => entry != null)
					.OrderBy(entry => entry.InstanceID, StringComparer.Ordinal)
					.ThenBy(entry => entry.PrefabID, StringComparer.Ordinal)
					.ToList();
			}

			List<ComponentBlob> OrderedBlobs(SaveablePrefabData data)
			{
				if (data?.RuntimeModificationData == null ||
				    data.RuntimeModificationData.Length == 0)
				{
					return new List<ComponentBlob>();
				}

				RuntimeModificationData modifications =
					SaveDataSerializer.Instance.Deserialize<RuntimeModificationData>(
						data.RuntimeModificationData);
				return (modifications?.ComponentBlobs ??
				        new List<ComponentBlob>())
					.Where(blob => blob != null)
					.OrderBy(blob => blob.GuidSuffix, StringComparer.Ordinal)
					.ThenBy(
						blob => Convert.ToBase64String(
							blob.Payload ?? Array.Empty<byte>()),
						StringComparer.Ordinal)
					.ToList();
			}

			List<SaveablePrefabData> firstPrefabs = OrderedPrefabs(first);
			List<SaveablePrefabData> secondPrefabs = OrderedPrefabs(second);
			if (firstPrefabs.Count != secondPrefabs.Count)
				return false;

			for (int i = 0; i < firstPrefabs.Count; i++)
			{
				SaveablePrefabData firstPrefab = firstPrefabs[i];
				SaveablePrefabData secondPrefab = secondPrefabs[i];
				if (!string.Equals(
					    firstPrefab.InstanceID,
					    secondPrefab.InstanceID,
					    StringComparison.Ordinal) ||
				    !string.Equals(
					    firstPrefab.PrefabID,
					    secondPrefab.PrefabID,
					    StringComparison.Ordinal))
				{
					return false;
				}

				List<ComponentBlob> firstBlobs = OrderedBlobs(firstPrefab);
				List<ComponentBlob> secondBlobs = OrderedBlobs(secondPrefab);
				if (firstBlobs.Count != secondBlobs.Count)
					return false;

				for (int blobIndex = 0;
				     blobIndex < firstBlobs.Count;
				     blobIndex++)
				{
					ComponentBlob firstBlob = firstBlobs[blobIndex];
					ComponentBlob secondBlob = secondBlobs[blobIndex];
					if (!string.Equals(
						    firstBlob.GuidSuffix,
						    secondBlob.GuidSuffix,
						    StringComparison.Ordinal) ||
					    !(firstBlob.Payload ?? Array.Empty<byte>())
					    .SequenceEqual(
						    secondBlob.Payload ??
						    Array.Empty<byte>()))
					{
						return false;
					}
				}
			}

			return true;
		}

		private static SaveablePrefabData FindPrefabData(
			SaveData data,
			string instanceId)
		{
			return data?.Prefabs?.FirstOrDefault(
				entry => entry != null &&
				         string.Equals(
					         entry.InstanceID,
					         instanceId,
					         StringComparison.Ordinal));
		}

		private static int CountComponentBlobs(SaveablePrefabData data)
		{
			if (data?.RuntimeModificationData == null ||
			    data.RuntimeModificationData.Length == 0)
				return 0;

			RuntimeModificationData modifications =
				SaveDataSerializer.Instance.Deserialize<RuntimeModificationData>(
					data.RuntimeModificationData);
			return modifications?.ComponentBlobs?.Count ?? 0;
		}

		private static bool Approximately(
			Vector3 left,
			Vector3 right,
			float tolerance = 0.0001f)
		{
			return (left - right).sqrMagnitude <= tolerance * tolerance;
		}

		private static SaveDataCollectionDuplicateDiagnosticResult CreateResult(
			string diagnosticRunId,
			string mode)
		{
			return new SaveDataCollectionDuplicateDiagnosticResult
			{
				runId = diagnosticRunId,
				mode = mode,
				startedAtUtc = DateTime.UtcNow.ToString("O"),
				unityVersion = Application.unityVersion,
				platform = Application.platform.ToString(),
				sceneName = SceneManager.GetActiveScene().name
			};
		}

		private static void TryWriteResult(
			string path,
			SaveDataCollectionDuplicateDiagnosticResult result)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(path))
					throw new InvalidOperationException(
						"The diagnostic result path is empty.");

				string directory = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory))
					Directory.CreateDirectory(directory);

				string temporaryPath = path + ".tmp";
				File.WriteAllText(
					temporaryPath,
					JsonUtility.ToJson(result, prettyPrint: true));
				if (File.Exists(path))
					File.Delete(path);
				File.Move(temporaryPath, path);

				Debug.Log(
					$"{LogPrefix} {(result.passed ? "PASS" : "FAIL")}. Runtime result: {path}");
			}
			catch (Exception ex)
			{
				Debug.LogError(
					$"{LogPrefix} Could not write runtime result to '{path}': {ex}");
			}
		}

		private sealed class CollectionOutcome
		{
			internal CollectionOutcome(
				SaveDataCollectionDuplicateStageResult stage,
				SaveData data)
			{
				Stage = stage;
				Data = data;
			}

			internal SaveDataCollectionDuplicateStageResult Stage { get; }
			internal SaveData Data { get; }
		}
		}

		[AddComponentMenu("")]
		internal sealed class SaveDataCollectionOwnershipToggleComponent :
			SaveableComponent
		{
			internal Action BeforeSerialize { get; set; }
			internal int SerializeInvocationCount { get; private set; }

			internal void Configure(string gameObjectId, string componentId)
			{
				uniqueID = gameObjectId;
				componentID = componentId;
			}

			internal void ResetInvocationCount()
			{
				SerializeInvocationCount = 0;
			}

			protected override byte[] SerializeComponentData()
			{
				SerializeInvocationCount++;
				Action callback = BeforeSerialize;
				BeforeSerialize = null;
				callback?.Invoke();
				return new[] { (byte)SerializeInvocationCount };
			}

			protected override void DeserializeComponentData(byte[] data)
			{
			}
		}

		/// <summary>
		/// Runtime-only throwing probe used to prove Abort clears diagnostic state.
	/// </summary>
	[AddComponentMenu("")]
	internal sealed class SaveDataCollectionThrowingComponent : SaveableComponent
	{
		internal void Configure(string gameObjectId, string componentId)
		{
			uniqueID = gameObjectId;
			componentID = componentId;
		}

		protected override byte[] SerializeComponentData()
		{
			throw new InvalidOperationException(
				"Intentional SaveData collection diagnostic exception.");
		}

		protected override void DeserializeComponentData(byte[] data)
		{
		}
	}

	/// <summary>
	/// Diagnostic-only probe that makes one nested SaveData call while serializing.
	/// </summary>
	[AddComponentMenu("")]
	internal sealed class SaveDataCollectionReentrantComponent : SaveableComponent
	{
		private bool nestedCallInProgress;

		internal void Configure(string gameObjectId, string componentId)
		{
			uniqueID = gameObjectId;
			componentID = componentId;
		}

		protected override byte[] SerializeComponentData()
		{
			if (!nestedCallInProgress)
			{
				nestedCallInProgress = true;
				try
				{
					SaveData();
				}
				finally
				{
					nestedCallInProgress = false;
				}
			}

			return new byte[] { 1 };
		}

		protected override void DeserializeComponentData(byte[] data)
		{
		}
	}

	[Serializable]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed class SaveDataCollectionDuplicateDiagnosticResult
	{
		public string runId;
		public string mode;
		public string startedAtUtc;
		public string completedAtUtc;
		public string unityVersion;
		public string platform;
		public string sceneName;
		public bool passed;
		public string fatalError;
		public List<SaveDataCollectionDuplicateStageResult> stages = new();
	}

	[Serializable]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed class SaveDataCollectionDuplicateStageResult
	{
		public string name;
		public string operationName;
		public string expectation;
		public bool passed;
		public string details;
		public int uniqueKeyCount;
		public int duplicateCount;
		public int prefabEntryCount;
		public int globalComponentEntryCount;
		public bool targetGloballyRegistered;
		public int utilityRegisteredComponents;
		public bool utilityPrefabRegistered;
		public int targetPrefabBlobCount;
		public int nestedOuterBlobCount;
		public int nestedInnerBlobCount;
		public bool restoreVerified;
		public bool intentionalExceptionObserved;
		public List<string> globalComponentKeys = new();
		public List<string> untracedDuplicateKeys = new();
		public List<SaveDataCollectionDuplicateOccurrenceResult> calls = new();
		public List<SaveDataCollectionDuplicateEntryResult> duplicates = new();
	}

	[Serializable]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed class SaveDataCollectionDuplicateEntryResult
	{
		public string compositeKey;
		public int totalCalls;
		public List<SaveDataCollectionDuplicateCallerResult> callers = new();
		public List<SaveDataCollectionDuplicateOccurrenceResult> occurrences = new();
	}

	[Serializable]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed class SaveDataCollectionDuplicateCallerResult
	{
		public string componentType;
		public string hierarchyPath;
		public string sceneName;
		public string scenePath;
		public string gameObjectId;
		public string componentId;
		public int unityObjectId;
		public int callCount;
		public string nearestPrefabName;
		public string nearestPrefabInstanceId;
		public string nearestPrefabAssetId;
		public bool blobOwned;
		public bool globallyRegistered;
		public string registrationSource;
		public string routeSummary;
	}

	[Serializable]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed class SaveDataCollectionDuplicateOccurrenceResult
	{
		public string compositeKey;
		public int callOrder;
		public string route;
		public string componentType;
		public string hierarchyPath;
		public string sceneName;
		public string scenePath;
		public string gameObjectId;
		public string componentId;
		public int unityObjectId;
		public string nearestPrefabName;
		public string nearestPrefabInstanceId;
		public string nearestPrefabAssetId;
		public bool blobOwned;
		public bool globallyRegistered;
		public string registrationSource;
		public string routeOwnerName;
		public string routeOwnerInstanceId;
		public string routeOwnerAssetId;
		public string stackTrace;
	}
}
#endif
