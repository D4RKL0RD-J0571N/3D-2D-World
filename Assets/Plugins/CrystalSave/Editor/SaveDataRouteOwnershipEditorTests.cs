#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Arawn.CrystalSave.EditorTests
{
	public sealed class SaveDataRouteOwnershipEditorTests
	{
		private const BindingFlags StaticFlags =
			BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags InstanceFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private readonly List<GameObject> createdRoots = new();

		[SetUp]
		public void SetUp()
		{
			ResetDiagnosticState();
		}

		[TearDown]
		public void TearDown()
		{
			ResetDiagnosticState();

			foreach (GameObject root in createdRoots)
			{
				if (root != null)
					UnityEngine.Object.DestroyImmediate(root);
			}

			createdRoots.Clear();
		}

		[Test]
		public void PackageRouteScopesPreserveCallOrderAndProduceActionableWarning()
		{
			SaveablePrefab owner = CreatePrefabRoot("RouteOwner", trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"RouteCaller",
				"route-game-object",
				"route-component");
			var capture = ArmDeepCapture();

			BeginDiagnostics("RouteClassification");
			using (BeginRoute("GlobalComponentCollection", caller))
				caller.SaveData();
			using (BeginRoute("PrefabComponentBlob", caller, owner))
				caller.SaveData();
			object snapshot = CompleteDiagnostics();

			Assert.That(capture.Snapshot, Is.SameAs(snapshot));
			Assert.That(GetMemberValue(snapshot, "OperationName"),
				Is.EqualTo("RouteClassification"));
			Assert.That(caller.SerializeInvocationCount, Is.EqualTo(2));

			IReadOnlyList<object> calls = GetSequence(snapshot, "Calls");
			Assert.That(calls.Count, Is.EqualTo(2));
			Assert.That(GetInt(calls[0], "CallOrder"), Is.EqualTo(1));
			Assert.That(GetInt(calls[1], "CallOrder"), Is.EqualTo(2));
			Assert.That(GetRouteName(calls[0]), Is.EqualTo("GlobalComponentCollection"));
			Assert.That(GetRouteName(calls[1]), Is.EqualTo("PrefabComponentBlob"));
			Assert.That(GetMemberValue(calls[1], "RouteOwnerName") as string,
				Does.Contain("RouteOwner"));

			object duplicate = AssertSingleDuplicate(snapshot, expectedTotalCalls: 2);
			IReadOnlyList<object> occurrences = GetSequence(duplicate, "Occurrences");
			Assert.That(occurrences.Select(GetRouteName),
				Is.EqualTo(new[]
				{
					"GlobalComponentCollection",
					"PrefabComponentBlob"
				}));

			object duplicateCaller = GetSequence(duplicate, "Callers").Single();
			Assert.That(GetMemberValue(duplicateCaller, "RouteSummary") as string,
				Does.Contain("GlobalComponentCollection x1"));
			Assert.That(GetMemberValue(duplicateCaller, "RouteSummary") as string,
				Does.Contain("PrefabComponentBlob x1"));
			Assert.That(ResolveWarningContext(duplicate), Is.SameAs(caller));

			string warning = FormatWarning("RouteClassification", duplicate);
			Assert.That(warning, Does.Contain("GlobalComponentCollection"));
			Assert.That(warning, Does.Contain("PrefabComponentBlob"));
			Assert.That(warning, Does.Contain("global ComponentManager collection"));
			Assert.That(warning, Does.Contain("SaveablePrefab component-blob capture"));
		}

		[Test]
		public void RouteScopeOnlyClassifiesItsExpectedComponent()
		{
			SaveDataRouteDiagnosticTestComponent expected = CreateCaller(
				null,
				"ExpectedCaller",
				"expected-game-object",
				"expected-component");
			SaveDataRouteDiagnosticTestComponent other = CreateCaller(
				null,
				"OtherCaller",
				"other-game-object",
				"other-component");
			ArmDeepCapture();

			BeginDiagnostics("TargetScope");
			using (BeginRoute("GlobalComponentCollection", expected))
			{
				other.SaveData();
				expected.SaveData();
			}
			object snapshot = CompleteDiagnostics();

			IReadOnlyList<object> calls = GetSequence(snapshot, "Calls");
			Assert.That(calls.Count, Is.EqualTo(2));
			Assert.That(GetInt(calls[0], "UnityObjectId"),
				Is.EqualTo(UnityObjectHelper.GetUniqueId(other)));
			Assert.That(GetRouteName(calls[0]), Is.EqualTo("CustomOrReentrant"));
			Assert.That(GetInt(calls[1], "UnityObjectId"),
				Is.EqualTo(UnityObjectHelper.GetUniqueId(expected)));
			Assert.That(GetRouteName(calls[1]), Is.EqualTo("GlobalComponentCollection"));
			Assert.That(GetSequence(snapshot, "Duplicates"), Is.Empty);
		}

		[Test]
		public void ReentrantSaveDataIsCustomAndDoesNotCorruptTheOuterRoute()
		{
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				null,
				"ReentrantCaller",
				"reentrant-game-object",
				"reentrant-component");
			caller.BeforeSerialize = () => caller.SaveData();
			ArmDeepCapture();

			BeginDiagnostics("Reentrant");
			using (BeginRoute("GlobalComponentCollection", caller))
			{
				caller.SaveData();
				caller.SaveData();
			}
			object snapshot = CompleteDiagnostics();

			Assert.That(caller.SerializeInvocationCount, Is.EqualTo(3));
			IReadOnlyList<object> calls = GetSequence(snapshot, "Calls");
			Assert.That(calls.Select(GetRouteName),
				Is.EqualTo(new[]
				{
					"GlobalComponentCollection",
					"CustomOrReentrant",
					"GlobalComponentCollection"
				}));
			Assert.That(GetMemberValue(calls[1], "StackTrace") as string,
				Is.Not.Null.And.Not.Empty);
			Assert.That(GetInt(GetStaticField("saveDataInvocationDepth")), Is.Zero);
			Assert.That(GetStaticField("saveDataCallRouteActive"), Is.False);
			Assert.That(GetStaticField("currentSaveDataExpectedComponent"), Is.Null);
			Assert.That(GetStaticField("currentSaveDataPrefabOwner"), Is.Null);
		}

		[Test]
		public void DuplicateSnapshotKeepsPerCallerCountsAndRoutes()
		{
			SaveablePrefab owner = CreatePrefabRoot("PerCallerOwner", trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent first = CreateCaller(
				owner.transform,
				"FirstCaller",
				"shared-route-game-object",
				"shared-route-component");
			SaveDataRouteDiagnosticTestComponent second = CreateCaller(
				null,
				"SecondCaller",
				"shared-route-game-object",
				"shared-route-component");
			ArmDeepCapture();

			BeginDiagnostics("PerCaller");
			using (BeginRoute("GlobalComponentCollection", first))
			{
				first.SaveData();
				first.SaveData();
			}
			using (BeginRoute("PrefabComponentBlob", second, owner))
				second.SaveData();
			object snapshot = CompleteDiagnostics();

			object duplicate = AssertSingleDuplicate(snapshot, expectedTotalCalls: 3);
			IReadOnlyList<object> callers = GetSequence(duplicate, "Callers");
			Assert.That(callers.Count, Is.EqualTo(2));

			object firstDiagnostic = FindCaller(callers, first);
			object secondDiagnostic = FindCaller(callers, second);
			Assert.That(GetInt(firstDiagnostic, "CallCount"), Is.EqualTo(2));
			Assert.That(GetInt(secondDiagnostic, "CallCount"), Is.EqualTo(1));
			Assert.That(GetMemberValue(firstDiagnostic, "RouteSummary") as string,
				Does.Contain("GlobalComponentCollection x2"));
			Assert.That(GetMemberValue(secondDiagnostic, "RouteSummary") as string,
				Does.Contain("PrefabComponentBlob x1"));

			IReadOnlyList<object> occurrences = GetSequence(duplicate, "Occurrences");
			Assert.That(occurrences.Select(GetIntCallOrder),
				Is.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(occurrences.Select(GetRouteName),
				Is.EqualTo(new[]
				{
					"GlobalComponentCollection",
					"GlobalComponentCollection",
					"PrefabComponentBlob"
				}));
			Assert.That(ResolveWarningContext(duplicate), Is.SameAs(first));
		}

		[Test]
		public void CompletingAndReleasingSnapshotDropsCallerReferences()
		{
			SaveablePrefab owner = CreatePrefabRoot("ReleaseOwner", trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"ReleaseCaller",
				"release-game-object",
				"release-component");

			BeginDiagnostics("Release");
			using (BeginRoute("PrefabComponentBlob", caller, owner))
			{
				caller.SaveData();
				caller.SaveData();
			}
			object snapshot = CompleteDiagnostics();
			object duplicate = AssertSingleDuplicate(snapshot, expectedTotalCalls: 2);
			object callerDiagnostic = GetSequence(duplicate, "Callers").Single();
			Assert.That(GetMemberValue(callerDiagnostic, "Component"), Is.SameAs(caller));
			Assert.That(GetActiveRecordCount(), Is.Zero);

			ReleaseSnapshotReferences(snapshot);

			Assert.That(GetMemberValue(callerDiagnostic, "Component"), Is.Null);
			IReadOnlyList<object> occurrences = GetSequence(duplicate, "Occurrences");
			Assert.That(occurrences.Select(GetRouteName),
				Is.All.EqualTo("PrefabComponentBlob"));
			Assert.That(
				occurrences.All(
					occurrence =>
						(GetMemberValue(occurrence, "RouteOwnerName") as string)
							?.Contains("ReleaseOwner") == true),
				Is.True);
		}

		[Test]
		public void AbortAfterSerializationFailureClearsRecordsCaptureAndRouteState()
		{
			SaveablePrefab owner = CreatePrefabRoot("AbortOwner", trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"AbortCaller",
				"abort-game-object",
				"abort-component");
			caller.ThrowOnSerialize = true;
			var abortedCapture = ArmDeepCapture();

			BeginDiagnostics("AbortedCollection");
			try
			{
				using (BeginRoute("PrefabComponentBlob", caller, owner))
				Assert.Throws<InvalidOperationException>(() => caller.SaveData());
			}
			finally
			{
				AbortDiagnostics();
			}

			Assert.That(abortedCapture.Snapshot, Is.Null);
			Assert.That(GetActiveRecordCount(), Is.Zero);
			Assert.That(GetStaticField("saveCallDiagnosticsActive"), Is.False);
			Assert.That(GetStaticField("activeSaveCallOperation"), Is.Null);
			Assert.That(GetStaticField("activeSaveCallCapture"), Is.Null);
			Assert.That(GetInt(GetStaticField("saveDataInvocationDepth")), Is.Zero);
			Assert.That(GetStaticField("saveDataCallRouteActive"), Is.False);
			Assert.That(GetStaticField("currentSaveDataExpectedComponent"), Is.Null);
			Assert.That(GetStaticField("currentSaveDataPrefabOwner"), Is.Null);

			caller.ThrowOnSerialize = false;
			ArmDeepCapture();
			BeginDiagnostics("AfterAbort");
			using (BeginRoute("GlobalComponentCollection", caller))
				caller.SaveData();
			object cleanSnapshot = CompleteDiagnostics();

			Assert.That(GetMemberValue(cleanSnapshot, "OperationName"), Is.EqualTo("AfterAbort"));
			Assert.That(GetSequence(cleanSnapshot, "Calls").Select(GetRouteName),
				Is.EqualTo(new[] { "GlobalComponentCollection" }));
			Assert.That(GetSequence(cleanSnapshot, "Duplicates"), Is.Empty);
		}

		[Test]
		public void ComponentManagerRejectsBlobOwnedComponentAndLeavesFlagsClean()
		{
			SaveablePrefab owner = CreatePrefabRoot("BlobOwnedRoot", trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"BlobOwnedCaller",
				"blob-owned-game-object",
				"blob-owned-component");
			var manager = new ComponentManager();

			manager.RegisterSaveableComponent(caller);

			Assert.That(manager.Contains(caller), Is.False);
			Assert.That(manager.GetSaveableComponents(), Is.Empty);
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.True);
			Assert.That(GetMemberValue(caller, "NearestOwningPrefab"), Is.SameAs(owner));
			Assert.That(GetPrivateRegistrationFlag(caller), Is.False);
			Assert.That(GetMemberValue(caller, "LastManagerRegistrationSource") as string,
				Is.EqualTo("ComponentManager"));
		}

		[Test]
		public void PublicUnregisterExcludesBlobOwnedHelperUntilExplicitRegister()
		{
			SaveablePrefab owner = CreatePrefabRoot(
				"ExplicitBlobDetachRoot",
				trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"ExplicitBlobDetachCaller",
				"explicit-blob-detach-game-object",
				"explicit-blob-detach-component");
			var manager = new ComponentManager();

			manager.RegisterSaveableComponent(caller);
			caller.ResetSerializeInvocationCount();
			owner.CaptureRuntimeModifications();
			Assert.That(caller.SerializeInvocationCount, Is.EqualTo(1));

			manager.UnregisterSaveableComponent(caller);
			caller.ResetSerializeInvocationCount();
			owner.CaptureRuntimeModifications();
			Assert.That(caller.SerializeInvocationCount, Is.Zero,
				"An explicit public unregister must detach a blob-owned helper too.");

			manager.RegisterSaveableComponent(caller);
			caller.ResetSerializeInvocationCount();
			owner.CaptureRuntimeModifications();
			Assert.That(caller.SerializeInvocationCount, Is.EqualTo(1),
				"An explicit register should restore blob participation without adding a global route.");
			Assert.That(manager.Contains(caller), Is.False);
		}

		[Test]
		public void RuntimeSuppressionPersistsAcrossEnableUntilExplicitRegister()
		{
			SaveablePrefab owner = CreatePrefabRoot(
				"RuntimeSuppressionRoot",
				trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"RuntimeSuppressionCaller",
				"runtime-suppression-game-object",
				"runtime-suppression-component");
			var manager = new ComponentManager();

			manager.RegisterSaveableComponent(caller);
			caller.SuppressRuntimeSaveRegistration();
			caller.enabled = true;
			caller.ResetSerializeInvocationCount();
			owner.CaptureRuntimeModifications();
			Assert.That(caller.SerializeInvocationCount, Is.Zero,
				"SuppressRuntimeSaveRegistration must survive a later OnEnable attempt.");

			manager.RegisterSaveableComponent(caller);
			caller.ResetSerializeInvocationCount();
			owner.CaptureRuntimeModifications();
			Assert.That(caller.SerializeInvocationCount, Is.EqualTo(1));
		}

		[Test]
		public void ComponentManagerTracksRegistrationFlagsForGlobalComponent()
		{
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				null,
				"GlobalRegistrationCaller",
				"global-registration-game-object",
				"global-registration-component");
			var manager = new ComponentManager();

			manager.RegisterSaveableComponent(caller);

			Assert.That(manager.Contains(caller), Is.True);
			Assert.That(GetPrivateRegistrationFlag(caller), Is.True);
			Assert.That(GetMemberValue(caller, "LastManagerRegistrationSource") as string,
				Is.Not.Null.And.Not.Empty);

			manager.UnregisterSaveableComponent(caller);

			Assert.That(manager.Contains(caller), Is.False);
			Assert.That(GetPrivateRegistrationFlag(caller), Is.False);
			Assert.That(GetMemberValue(caller, "LastManagerRegistrationSource") as string,
				Is.EqualTo("ComponentManager"));
		}

		[TestCase("global")]
		[TestCase("pending")]
		[TestCase("deferred")]
		[TestCase("blob")]
		[TestCase("reentrant")]
		public void CollectionCollisionsWarnWithoutRewritingEitherIdentity(string route)
		{
			var manager = new ComponentManager();
			using IDisposable harness = InstallSaveManagerHarness(manager);
			SaveablePrefab owner = route == "blob" ? CreatePrefabRoot("CopiedIdentityOwner", true) : null;
			if (owner != null) IncludePrefabInCollectionManifest(owner);
			var first = CreateCaller(owner ? owner.transform : null, "CopiedIdentityFirst", "copied-game-object", "copied-component");
			var second = CreateCaller(owner ? owner.transform : null, "CopiedIdentitySecond", "copied-game-object", "copied-component");
			manager.RegisterSaveableComponent(first);
			manager.RegisterSaveableComponent(second);
			string identifier = first.UniqueIdentifier;
			Assert.That(second.UniqueIdentifier, Is.EqualTo(identifier));
			string firstObjectId = first.GameObjectUniqueID;
			string secondObjectId = second.GameObjectUniqueID;
			byte[] preserved = { 19, 29, 43 };
			if (route == "pending") SetPendingBytes(manager, identifier, preserved);
			if (route == "deferred")
			{
				var incoming = new SaveData(new VersionData(1, 0, 0));
				incoming.ComponentsData[identifier] = preserved;
				incoming.ComponentMetadata[identifier] = new ComponentDataMetadata(50, true, null);
				manager.ApplyComponentData(incoming);
				Assert.That(manager.HasDeferredComponents, Is.True);
			}

			var warnings = new List<string>();
			Application.LogCallback observer = (message, stack, type) =>
			{
				if (type == LogType.Warning) warnings.Add(message);
			};
			var previous = Arawn.CrystalSave.Runtime.Logger.LogThreshold;
			Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Warning;
			Application.logMessageReceived += observer;
			try
			{
				for (int capture = 0; capture < 2; capture++)
				{
					first.ResetSerializeInvocationCount();
					second.ResetSerializeInvocationCount();
					warnings.Clear();
					if (route == "reentrant") first.BeforeSerialize = () => second.SaveData();
					ArmDeepCapture();
					BeginDiagnostics("Collision identity preservation " + route);
					if (owner != null)
					{
						var bytes = owner.CaptureRuntimeModifications();
						var modifications = SaveDataSerializer.Instance.Deserialize<RuntimeModificationData>(bytes);
						Assert.That(modifications.ComponentBlobs.Select(b => b.GuidSuffix),
							Is.EqualTo(new[] { "copied-component", "copied-component" }));
					}
					else
					{
						var collected = new SaveData(new VersionData(1, 0, 0));
						manager.CollectComponentData(collected);
						Assert.That(collected.ComponentsData.Keys, Is.EquivalentTo(new[] { identifier }));
						if (route == "pending" || route == "deferred")
							Assert.That(collected.ComponentsData[identifier], Is.EqualTo(preserved));
					}
					object snapshot = CompleteDiagnostics();
					object collision = AssertSingleDuplicate(snapshot, route == "reentrant" ? 3 : 2);
					Assert.That(GetSequence(collision, "Callers"), Has.Count.EqualTo(2));
					typeof(SaveableComponent).GetMethod("ReportSaveCallDiagnostics", StaticFlags)
						.Invoke(null, new object[] { "Collision identity preservation", snapshot });
					Assert.That(warnings.Any(message => message.Contains("copied-component")), Is.True,
						"The collision must remain visible to the user.");
					Assert.That(first.UniqueIdentifier, Is.EqualTo(identifier));
					Assert.That(second.UniqueIdentifier, Is.EqualTo(identifier));
					Assert.That(first.GameObjectUniqueID, Is.EqualTo(firstObjectId));
					Assert.That(second.GameObjectUniqueID, Is.EqualTo(secondObjectId));
					Assert.That(first.ComponentID, Is.EqualTo("copied-component"));
					Assert.That(second.ComponentID, Is.EqualTo("copied-component"));
					if (route == "pending" || route == "deferred")
					{
						Assert.That(first.SerializeInvocationCount, Is.Zero);
						Assert.That(second.SerializeInvocationCount, Is.Zero);
					}
				}
			}
			finally
			{
				Application.logMessageReceived -= observer;
				Arawn.CrystalSave.Runtime.Logger.LogThreshold = previous;
			}
		}

		[Test]
		public void PendingPayloadCollisionIsTracedWithoutInvokingSaveData()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveDataRouteDiagnosticTestComponent first = CreateCaller(
				null,
				"FirstPendingCollisionCaller",
				"pending-collision-game-object",
				"pending-collision-component");
			SaveDataRouteDiagnosticTestComponent second = CreateCaller(
				null,
				"SecondPendingCollisionCaller",
				"pending-collision-game-object",
				"pending-collision-component");
			byte[] pendingBytes = { 17, 31, 47 };
			var saveData = new SaveData(new VersionData(1, 0, 0));
			object snapshot;
			LogLevel previousThreshold =
				Arawn.CrystalSave.Runtime.Logger.LogThreshold;
			Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Off;
			try
			{
				manager.RegisterSaveableComponent(first);
				manager.RegisterSaveableComponent(second);
				SetPendingBytes(manager, first.UniqueIdentifier, pendingBytes);
				ArmDeepCapture();
				BeginDiagnostics("Pending payload collision");
				manager.CollectComponentData(saveData);
				snapshot = CompleteDiagnostics();
			}
			finally
			{
				Arawn.CrystalSave.Runtime.Logger.LogThreshold = previousThreshold;
			}

			Assert.That(first.SerializeInvocationCount, Is.Zero);
			Assert.That(second.SerializeInvocationCount, Is.Zero);
			Assert.That(saveData.ComponentsData[first.UniqueIdentifier],
				Is.EqualTo(pendingBytes));

			object duplicate = AssertSingleDuplicate(snapshot, expectedTotalCalls: 2);
			Assert.That(GetSequence(duplicate, "Callers"), Has.Count.EqualTo(2));
			Assert.That(
				GetSequence(duplicate, "Occurrences").Select(GetRouteName),
				Is.EqualTo(new[]
				{
					"PendingGlobalPayload",
					"PendingGlobalPayload"
				}));

			string warning = FormatWarning("Pending payload collision", duplicate);
			Assert.That(warning, Does.Contain("Duplicate save-data collection entries"));
			Assert.That(warning, Does.Contain("without invoking SaveData()"));
			Assert.That(warning, Does.Contain("PendingGlobalPayload"));
		}

		[Test]
		public void MixedUntracedPendingCollisionCannotProduceFalseCleanCapture()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveDataRoutePlainSaveableTestComponent plain = CreatePlainCaller(
				"PlainPendingCollisionCaller",
				"mixed-pending-game-object",
				"mixed-pending-component");
			SaveDataRouteDiagnosticTestComponent traced = CreateCaller(
				null,
				"TracedPendingCollisionCaller",
				"mixed-pending-game-object",
				"mixed-pending-component");
			byte[] pendingBytes = { 5, 19, 73 };
			var saveData = new SaveData(new VersionData(1, 0, 0));
			object snapshot;

			LogLevel previousThreshold =
				Arawn.CrystalSave.Runtime.Logger.LogThreshold;
			Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Off;
			try
			{
				manager.RegisterSaveableComponent(plain);
				manager.RegisterSaveableComponent(traced);
				SetPendingBytes(manager, traced.UniqueIdentifier, pendingBytes);
				ArmDeepCapture();
				BeginDiagnostics("Mixed pending payload collision");
				manager.CollectComponentData(saveData);
				snapshot = CompleteDiagnostics();
			}
			finally
			{
				Arawn.CrystalSave.Runtime.Logger.LogThreshold = previousThreshold;
			}

			Assert.That(plain.SaveInvocationCount, Is.Zero);
			Assert.That(traced.SerializeInvocationCount, Is.Zero);
			Assert.That(GetSequence(snapshot, "Duplicates"), Is.Empty);
			Assert.That(
				Convert.ToBoolean(GetMemberValue(snapshot, "HasDuplicates")),
				Is.True,
				"An armed capture must fail even when only one writer can be richly attributed.");
			Assert.That(
				GetSequence(snapshot, "UntracedDuplicateKeys"),
				Is.EqualTo(new object[] { traced.UniqueIdentifier }));
			Assert.That(
				GetSequence(snapshot, "Calls").Select(GetRouteName),
				Is.EqualTo(new[] { "PendingGlobalPayload" }));
		}

		[Test]
		public void RegistrationLoadFailureKeepsPendingBytesForLaterRetry()
		{
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				null,
				"PendingRetryCaller",
				"pending-retry-game-object",
				"pending-retry-component");
			var manager = new ComponentManager();
			byte[] pendingBytes = { 7, 11, 29 };
			SetPendingBytes(manager, caller.UniqueIdentifier, pendingBytes);
			caller.ThrowOnLoad = true;

			LogLevel previousThreshold =
				Arawn.CrystalSave.Runtime.Logger.LogThreshold;
			Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Off;
			try
			{
				manager.RegisterSaveableComponent(caller);

				Assert.That(caller.LoadInvocationCount, Is.EqualTo(1));
				Assert.That(caller.DeserializeInvocationCount, Is.Zero);
				Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.True,
					"A failed registration callback must retain the only copy of the loaded bytes.");
				Assert.That(manager.HasComponentDeserialized(caller.UniqueIdentifier), Is.False);

				manager.UnregisterSaveableComponent(caller);
				caller.ThrowOnLoad = false;
				manager.RegisterSaveableComponent(caller);
			}
			finally
			{
				Arawn.CrystalSave.Runtime.Logger.LogThreshold = previousThreshold;
			}

			Assert.That(caller.LoadInvocationCount, Is.EqualTo(2));
			Assert.That(caller.DeserializeInvocationCount, Is.EqualTo(1));
			Assert.That(caller.LastDeserializedData, Is.EqualTo(pendingBytes));
			Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.False);
			Assert.That(manager.HasComponentDeserialized(caller.UniqueIdentifier), Is.True);
		}

		[Test]
		public void CollectionSnapshotDoesNotApplyPendingBytesToExistingGlobalComponent()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				null,
				"ExistingGlobalPendingCaller",
				"existing-global-pending-game-object",
				"existing-global-pending-component");
			manager.RegisterSaveableComponent(caller);
			byte[] pendingBytes = { 3, 5, 8 };
			SetPendingBytes(manager, caller.UniqueIdentifier, pendingBytes);

			using (BeginOwnershipCollectionScope(manager))
			{
				Assert.That(caller.LoadInvocationCount, Is.Zero);
				Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.True);
			}

			Assert.That(caller.LoadInvocationCount, Is.Zero,
				"A collection boundary is not a blob-to-global ownership transfer.");
			Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.True);
			Assert.That(manager.HasComponentDeserialized(caller.UniqueIdentifier), Is.False);
		}

		[Test]
		public void OwnershipTransferDoesNotCaptureSharedStateOrReplaceUnrelatedSnapshots()
		{
			using var settings = new FeatureSettingsScope();
			var manager = new ComponentManager();
			using IDisposable harness = InstallSaveManagerHarness(manager);
			FeatureTestSupport.Set(SaveManager.Instance, "saveSettings", settings.Settings);
			SaveManager.Instance.StateMachine.TransitionTo(SaveState.Idle);
			var owner = CreatePrefabRoot("SharedOwnershipTransfer", trackComponentBlobs: false);
			var caller = CreateCaller(owner.transform, "SharedTransferState", "shared-transfer", "state");
			caller.ShareStateAcrossScenes = true;
			manager.RegisterSaveableComponent(caller);
			Assert.That(manager.Contains(caller), Is.True);
			var shared = FeatureTestSupport.Get<Dictionary<string, byte[]>>(manager, "sharedStateCache");
			byte[] unrelated = { 42 };
			shared.Add("unrelated", unrelated);
			caller.ResetSerializeInvocationCount();

			owner.TrackComponentBlobs = true;
			RequestOwnershipRefresh(manager, owner);
			Assert.That(manager.Contains(caller), Is.False);
			Assert.That(caller.ShareStateAcrossScenes, Is.True);
			Assert.That(caller.SerializeInvocationCount, Is.Zero);
			Assert.That(shared.Keys, Is.EquivalentTo(new[] { "unrelated" }));
			Assert.That(shared["unrelated"], Is.SameAs(unrelated));

			owner.TrackComponentBlobs = false;
			RequestOwnershipRefresh(manager, owner);
			Assert.That(manager.Contains(caller), Is.True);
			Assert.That(caller.SerializeInvocationCount, Is.Zero);
			Assert.That(caller.LoadInvocationCount, Is.Zero);
			Assert.That(shared.Keys, Is.EquivalentTo(new[] { "unrelated" }));
			Assert.That(shared["unrelated"], Is.SameAs(unrelated));
		}

		[Test]
		public void FailedBlobToGlobalPendingLoadRetriesOnDeferredOwnershipPass()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveablePrefab owner = CreatePrefabRoot(
				"PendingOwnershipRetryOwner",
				trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"PendingOwnershipRetryCaller",
				"pending-ownership-retry-game-object",
				"pending-ownership-retry-component");
			manager.RegisterSaveableComponent(caller);
			byte[] pendingBytes = { 13, 21, 34 };
			SetPendingBytes(manager, caller.UniqueIdentifier, pendingBytes);
			caller.ThrowOnLoad = true;
			SaveManager.Instance.StateMachine.TransitionTo(SaveState.Idle);

			LogLevel previousThreshold =
				Arawn.CrystalSave.Runtime.Logger.LogThreshold;
			Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Off;
			try
			{
				owner.TrackComponentBlobs = false;
				RequestOwnershipRefresh(manager, owner);

				Assert.That(
					IsGloballyRegisteredWithoutReconciliation(manager, caller),
					Is.True);
				Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.True);
				Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.True);
				Assert.That(caller.DeserializeInvocationCount, Is.Zero);

				caller.ThrowOnLoad = false;
				FlushOwnershipRefreshes(manager);
			}
			finally
			{
				Arawn.CrystalSave.Runtime.Logger.LogThreshold = previousThreshold;
			}

			Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.False);
			Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.False);
			Assert.That(caller.DeserializeInvocationCount, Is.EqualTo(1));
			Assert.That(caller.LastDeserializedData, Is.EqualTo(pendingBytes));
			Assert.That(manager.HasComponentDeserialized(caller.UniqueIdentifier), Is.True);

			FlushOwnershipRefreshes(manager);
			Assert.That(caller.DeserializeInvocationCount, Is.EqualTo(1),
				"A completed retry must not apply consumed pending data again.");
		}

		[Test]
		public void FailedOwnershipTransferSchedulesAndRunsOneFrameRetry()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness =
				InstallSaveManagerHarness(manager, active: true);
			SaveablePrefab owner = CreatePrefabRoot(
				"AutomaticPendingRetryOwner",
				trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"AutomaticPendingRetryCaller",
				"automatic-pending-retry-game-object",
				"automatic-pending-retry-component");
			manager.RegisterSaveableComponent(caller);
			byte[] pendingBytes = { 55, 89, 144 };
			SetPendingBytes(manager, caller.UniqueIdentifier, pendingBytes);
			caller.ThrowOnLoad = true;
			SaveManager.Instance.StateMachine.TransitionTo(SaveState.Idle);

			LogLevel previousThreshold =
				Arawn.CrystalSave.Runtime.Logger.LogThreshold;
			Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Off;
			try
			{
				owner.TrackComponentBlobs = false;
				RequestOwnershipRefresh(manager, owner);

				Assert.That(
					GetComponentManagerBool(
						manager,
						"prefabOwnershipAutomaticRetryScheduled"),
					Is.True,
					"A failed immediate transfer should schedule its bounded next-frame retry.");
				Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.True);
				Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.True);

				caller.ThrowOnLoad = false;
				IEnumerator retry = CreateAutomaticOwnershipRetry(manager);
				Assert.That(retry.MoveNext(), Is.True,
					"The retry coroutine should defer work for exactly one frame.");
				Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.True);
				Assert.That(retry.MoveNext(), Is.False);
			}
			finally
			{
				Arawn.CrystalSave.Runtime.Logger.LogThreshold = previousThreshold;
			}

			Assert.That(
				GetComponentManagerBool(
					manager,
					"prefabOwnershipAutomaticRetryScheduled"),
				Is.False);
			Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.False);
			Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.False);
			Assert.That(caller.DeserializeInvocationCount, Is.EqualTo(1));
			Assert.That(caller.LastDeserializedData, Is.EqualTo(pendingBytes));
		}

		[Test]
		public void FailedDeferredIdleFlushSchedulesOnlyOneBoundedRetry()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness =
				InstallSaveManagerHarness(manager, active: true);
			SaveablePrefab owner = CreatePrefabRoot(
				"DeferredIdleRetryOwner",
				trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"DeferredIdleRetryCaller",
				"deferred-idle-retry-game-object",
				"deferred-idle-retry-component");
			manager.RegisterSaveableComponent(caller);
			byte[] pendingBytes = { 8, 13, 21 };
			SetPendingBytes(manager, caller.UniqueIdentifier, pendingBytes);
			caller.ThrowOnLoad = true;

			LogLevel previousThreshold =
				Arawn.CrystalSave.Runtime.Logger.LogThreshold;
			Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Off;
			try
			{
				SetSaveManagerLoading(isLoading: true);
				SaveManager.Instance.StateMachine.TransitionTo(SaveState.Loading);
				owner.TrackComponentBlobs = false;
				RequestOwnershipRefresh(manager, owner);
				Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.True);

				SetSaveManagerLoading(isLoading: false);
				SaveManager.Instance.StateMachine.TransitionTo(SaveState.Idle);

				Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.True);
				Assert.That(
					GetComponentManagerBool(
						manager,
						"prefabOwnershipAutomaticRetryScheduled"),
					Is.True,
					"An idle flush that re-queues failed work must schedule its bounded retry.");

				IEnumerator retry = CreateAutomaticOwnershipRetry(manager);
				Assert.That(retry.MoveNext(), Is.True);
				Assert.That(retry.MoveNext(), Is.False);

				Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.True);
				Assert.That(
					GetComponentManagerBool(
						manager,
						"prefabOwnershipAutomaticRetryScheduled"),
					Is.False,
					"A failed automatic retry must remain queued without scheduling itself every frame.");
				Assert.That(
					GetComponentManagerBool(
						manager,
						"prefabOwnershipAutomaticRetryInProgress"),
					Is.False);

				caller.ThrowOnLoad = false;
				FlushOwnershipRefreshes(manager);
			}
			finally
			{
				Arawn.CrystalSave.Runtime.Logger.LogThreshold = previousThreshold;
			}

			Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.False);
			Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.False);
			Assert.That(caller.DeserializeInvocationCount, Is.EqualTo(1));
			Assert.That(caller.LastDeserializedData, Is.EqualTo(pendingBytes));
		}

		[Test]
		public void FailedHierarchyTransferSchedulesBoundedRetry()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness =
				InstallSaveManagerHarness(manager, active: true);
			SaveablePrefab owner = CreatePrefabRoot(
				"HierarchyRetryOwner",
				trackComponentBlobs: true);
			var container = new GameObject("HierarchyRetryContainer");
			container.SetActive(false);
			container.transform.SetParent(owner.transform, false);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				container.transform,
				"HierarchyRetryCaller",
				"hierarchy-retry-game-object",
				"hierarchy-retry-component");
			manager.RegisterSaveableComponent(caller);
			byte[] pendingBytes = { 34, 55, 89 };
			SetPendingBytes(manager, caller.UniqueIdentifier, pendingBytes);
			caller.ThrowOnLoad = true;
			SaveManager.Instance.StateMachine.TransitionTo(SaveState.Idle);

			LogLevel previousThreshold =
				Arawn.CrystalSave.Runtime.Logger.LogThreshold;
			Arawn.CrystalSave.Runtime.Logger.LogThreshold = LogLevel.Off;
			try
			{
				// Moving only the ancestor avoids an editor-time identity callback on
				// the descendant while still changing its nearest blob owner.
				container.transform.SetParent(null, false);
				createdRoots.Add(container);
				RequestHierarchyOwnershipRefresh(manager, caller);

				Assert.That(IsHierarchyOwnershipRefreshDeferred(manager, caller), Is.True);
				Assert.That(
					IsGloballyRegisteredWithoutReconciliation(manager, caller),
					Is.True);
				Assert.That(
					GetComponentManagerBool(
						manager,
						"prefabOwnershipAutomaticRetryScheduled"),
					Is.True,
					"A failed hierarchy transfer must receive the same bounded retry.");

				caller.ThrowOnLoad = false;
				IEnumerator retry = CreateAutomaticOwnershipRetry(manager);
				Assert.That(retry.MoveNext(), Is.True);
				Assert.That(retry.MoveNext(), Is.False);
			}
			finally
			{
				Arawn.CrystalSave.Runtime.Logger.LogThreshold = previousThreshold;
			}

			Assert.That(
				IsHierarchyOwnershipRefreshDeferred(manager, caller),
				Is.False);
			Assert.That(
				GetComponentManagerBool(
					manager,
					"prefabOwnershipAutomaticRetryScheduled"),
				Is.False);
			Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.False);
			Assert.That(caller.DeserializeInvocationCount, Is.EqualTo(1));
			Assert.That(caller.LastDeserializedData, Is.EqualTo(pendingBytes));
		}

		[Test]
		public void ExplicitUnregisterRemovesDeferredHierarchyRefresh()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				null,
				"DeferredUnregisterCaller",
				"deferred-unregister-game-object",
				"deferred-unregister-component");
			manager.RegisterSaveableComponent(caller);

			SetSaveManagerLoading(isLoading: true);
			SaveManager.Instance.StateMachine.TransitionTo(SaveState.Loading);
			RequestHierarchyOwnershipRefresh(manager, caller);
			Assert.That(IsHierarchyOwnershipRefreshDeferred(manager, caller), Is.True);

			manager.UnregisterSaveableComponent(caller);
			Assert.That(IsHierarchyOwnershipRefreshDeferred(manager, caller), Is.False);
			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.False);

			SetSaveManagerLoading(isLoading: false);
			SaveManager.Instance.StateMachine.TransitionTo(SaveState.Idle);
			NotifyLoadingFlagCleared(manager);
			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.False,
				"A stale delayed hierarchy request must not resurrect an explicitly unregistered component.");
		}

		[Test]
		public void BlobOwnershipToggleIsReevaluatedAtCentralRegistration()
		{
			SaveablePrefab owner = CreatePrefabRoot("ToggleOwner", trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"ToggleCaller",
				"toggle-game-object",
				"toggle-component");
			var manager = new ComponentManager();

			manager.RegisterSaveableComponent(caller);
			Assert.That(manager.Contains(caller), Is.False);
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.True);

			owner.TrackComponentBlobs = false;
			manager.RegisterSaveableComponent(caller);
			Assert.That(manager.Contains(caller), Is.True);
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.False);

			manager.UnregisterSaveableComponent(caller);
			owner.TrackComponentBlobs = true;
			manager.RegisterSaveableComponent(caller);
			Assert.That(manager.Contains(caller), Is.False);
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.True);
			Assert.That(GetPrivateRegistrationFlag(caller), Is.False);
		}

		[Test]
		public void NestedPrefabUsesNearestOwnerAndOuterBlobCaptureSkipsItsSubtree()
		{
			SaveablePrefab outer = CreatePrefabRoot("OuterPrefab", trackComponentBlobs: true);
			var innerObject = new GameObject("InnerPrefab");
			innerObject.transform.SetParent(outer.transform, false);
			SaveablePrefab inner = innerObject.AddComponent<SaveablePrefab>();
			inner.TrackComponentBlobs = false;
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				inner.transform,
				"NestedCaller",
				"nested-game-object",
				"nested-component");
			var manager = new ComponentManager();

			manager.RegisterSaveableComponent(caller);
			Assert.That(manager.Contains(caller), Is.True,
				"The nearest prefab has blob tracking disabled, so the outer prefab must not claim the helper.");
			Assert.That(GetMemberValue(caller, "NearestOwningPrefab"), Is.SameAs(inner));
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.False);

			manager.UnregisterSaveableComponent(caller);
			inner.TrackComponentBlobs = true;
			manager.RegisterSaveableComponent(caller);
			Assert.That(manager.Contains(caller), Is.False);
			Assert.That(GetMemberValue(caller, "NearestOwningPrefab"), Is.SameAs(inner));
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.True);

			caller.ResetSerializeInvocationCount();
			outer.CaptureRuntimeModifications();
			Assert.That(caller.SerializeInvocationCount, Is.Zero,
				"An outer prefab must not serialize helpers owned by a nested SaveablePrefab.");

			inner.CaptureRuntimeModifications();
			Assert.That(caller.SerializeInvocationCount, Is.EqualTo(1),
				"The nearest nested prefab should serialize its helper exactly once.");
		}

		[Test]
		public void AncestorContainerReparentIsReconciledAtOutermostCollectionBoundary()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveablePrefab owner = CreatePrefabRoot(
				"AncestorBoundaryOwner",
				trackComponentBlobs: true);
			IncludePrefabInCollectionManifest(owner);

			var container = new GameObject("AncestorBoundaryContainer");
			container.SetActive(false);
			createdRoots.Add(container);
			container.transform.SetParent(owner.transform, false);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				container.transform,
				"AncestorBoundaryCaller",
				"ancestor-boundary-game-object",
				"ancestor-boundary-component");

			manager.RegisterSaveableComponent(caller);
			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.False);
			Assert.That(GetMemberValue(caller, "NearestOwningPrefab"), Is.SameAs(owner));
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.True);

			// Only the ancestor moves. The descendant SaveableComponent receives no
			// OnTransformParentChanged callback, so the outer collection boundary must
			// discover and reconcile the ownership change centrally.
			container.transform.SetParent(null, false);
			using (BeginOwnershipCollectionScope(manager))
			{
				Assert.That(
					IsGloballyRegisteredWithoutReconciliation(manager, caller),
					Is.True);
				Assert.That(GetMemberValue(caller, "NearestOwningPrefab"), Is.Null);
				Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.False);
			}

			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.True);

			container.transform.SetParent(owner.transform, false);
			using (BeginOwnershipCollectionScope(manager))
			{
				Assert.That(
					IsGloballyRegisteredWithoutReconciliation(manager, caller),
					Is.False);
				Assert.That(GetMemberValue(caller, "NearestOwningPrefab"), Is.SameAs(owner));
				Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.True);
			}

			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.False);
		}

		[Test]
		public void BlobOwnedAncestorMoveDuringCollectionUsesFrozenBlobRouteThenTransfersGlobal()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveablePrefab owner = CreatePrefabRoot(
				"FrozenBlobAncestorOwner",
				trackComponentBlobs: true);
			IncludePrefabInCollectionManifest(owner);

			var container = new GameObject("FrozenBlobAncestorContainer");
			container.SetActive(false);
			createdRoots.Add(container);
			container.transform.SetParent(owner.transform, false);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				container.transform,
				"FrozenBlobAncestorCaller",
				"frozen-blob-ancestor-game-object",
				"frozen-blob-ancestor-component");
			manager.RegisterSaveableComponent(caller);

			ArmDeepCapture();
			BeginDiagnostics("Frozen blob ownership after ancestor move");
			byte[] runtimeModifications = null;
			var globalData = new SaveData(new VersionData(1, 0, 0));
			IDisposable outerCollection = BeginOwnershipCollectionScope(manager);
			try
			{
				container.transform.SetParent(null, false);

				runtimeModifications = owner.CaptureRuntimeModifications();
				manager.CollectComponentData(globalData);

				Assert.That(
					IsGloballyRegisteredWithoutReconciliation(manager, caller),
					Is.False,
					"The collection-start blob route must remain authoritative until the outer scope exits.");
				Assert.That(globalData.ComponentsData, Does.Not.ContainKey(caller.UniqueIdentifier));
			}
			finally
			{
				outerCollection.Dispose();
			}

			object snapshot = CompleteDiagnostics();
			RuntimeModificationData decoded =
				SaveDataSerializer.Instance.Deserialize<RuntimeModificationData>(
					runtimeModifications);
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.ComponentBlobs, Has.Count.EqualTo(1));
			Assert.That(decoded.ComponentBlobs[0].GuidSuffix, Is.EqualTo(caller.ComponentID));
			Assert.That(caller.SerializeInvocationCount, Is.EqualTo(1));
			Assert.That(GetSequence(snapshot, "Calls"), Has.Count.EqualTo(1));
			Assert.That(
				GetRouteName(GetSequence(snapshot, "Calls").Single()),
				Is.EqualTo("PrefabComponentBlob"));
			Assert.That(GetSequence(snapshot, "Duplicates"), Is.Empty);

			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.True,
				"The final live hierarchy must transfer the helper globally after the frozen scope exits.");
			Assert.That(GetMemberValue(caller, "NearestOwningPrefab"), Is.Null);
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.False);
		}

		[Test]
		public void GlobalHelperMovedIntoBlobOwnerDuringCollectionUsesFrozenGlobalRouteThenTransfers()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveablePrefab owner = CreatePrefabRoot(
				"FrozenGlobalRouteOwner",
				trackComponentBlobs: true);
			IncludePrefabInCollectionManifest(owner);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				null,
				"FrozenGlobalRouteCaller",
				"frozen-global-route-game-object",
				"frozen-global-route-component");
			manager.RegisterSaveableComponent(caller);
			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.True);

			ArmDeepCapture();
			BeginDiagnostics("Frozen global ownership after hierarchy move");
			byte[] runtimeModifications = null;
			var globalData = new SaveData(new VersionData(1, 0, 0));
			IDisposable outerCollection = BeginOwnershipCollectionScope(manager);
			try
			{
				caller.transform.SetParent(owner.transform, false);

				runtimeModifications = owner.CaptureRuntimeModifications();
				manager.CollectComponentData(globalData);

				Assert.That(
					IsGloballyRegisteredWithoutReconciliation(manager, caller),
					Is.True,
					"The collection-start global route must remain authoritative until the outer scope exits.");
				Assert.That(globalData.ComponentsData, Does.ContainKey(caller.UniqueIdentifier));
			}
			finally
			{
				outerCollection.Dispose();
			}

			object snapshot = CompleteDiagnostics();
			RuntimeModificationData decoded =
				SaveDataSerializer.Instance.Deserialize<RuntimeModificationData>(
					runtimeModifications);
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.ComponentBlobs, Is.Empty);
			Assert.That(caller.SerializeInvocationCount, Is.EqualTo(1));
			Assert.That(GetSequence(snapshot, "Calls"), Has.Count.EqualTo(1));
			Assert.That(
				GetRouteName(GetSequence(snapshot, "Calls").Single()),
				Is.EqualTo("GlobalComponentCollection"));
			Assert.That(GetSequence(snapshot, "Duplicates"), Is.Empty);

			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.False,
				"The final live hierarchy must transfer the helper to blob ownership after scope exit.");
			Assert.That(GetMemberValue(caller, "NearestOwningPrefab"), Is.SameAs(owner));
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.True);
		}

		[Test]
		public void FalseToTrueToggleInsideGlobalSaveDataWaitsForOuterCollectionExit()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveablePrefab owner = CreatePrefabRoot(
				"DeferredGlobalToBlobOwner",
				trackComponentBlobs: false);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"DeferredGlobalToBlobCaller",
				"deferred-global-to-blob-go",
				"deferred-global-to-blob-component");
			manager.RegisterSaveableComponent(caller);

			bool callbackRan = false;
			bool callbackObservedCollectionScope = false;
			bool callbackObservedOriginalGlobalOwnership = false;
			bool callbackObservedDeferredRefresh = false;
			caller.BeforeSerialize = () =>
			{
				owner.TrackComponentBlobs = true;
				RequestOwnershipRefresh(manager, owner);
				callbackRan = true;
				callbackObservedCollectionScope =
					GetCollectionOwnershipDepth(manager) > 0;
				callbackObservedOriginalGlobalOwnership =
					IsGloballyRegisteredWithoutReconciliation(manager, caller);
				callbackObservedDeferredRefresh =
					IsOwnershipRefreshDeferred(manager, owner);
			};

			ArmDeepCapture();
			BeginDiagnostics("Deferred global-to-blob toggle");
			var saveData = new SaveData(new VersionData(1, 0, 0));
			IDisposable outerCollection = BeginOwnershipCollectionScope(manager);
			try
			{
				Assert.DoesNotThrow(
					() => manager.CollectComponentData(saveData),
					"Changing ownership from SaveData must not mutate the list being enumerated.");

				Assert.That(
					IsGloballyRegisteredWithoutReconciliation(manager, caller),
					Is.True,
					"The transfer must remain deferred until the outer/full collection exits.");
				Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.True);
			}
			finally
			{
				outerCollection.Dispose();
			}

			object snapshot = CompleteDiagnostics();
			Assert.That(callbackRan, Is.True);
			Assert.That(callbackObservedCollectionScope, Is.True);
			Assert.That(callbackObservedOriginalGlobalOwnership, Is.True);
			Assert.That(callbackObservedDeferredRefresh, Is.True);
			Assert.That(caller.SerializeInvocationCount, Is.EqualTo(1));
			Assert.That(saveData.ComponentsData.Keys,
				Is.EqualTo(new[] { caller.UniqueIdentifier }));
			Assert.That(
				saveData.ComponentsData[caller.UniqueIdentifier],
				Is.EqualTo(new byte[] { 1 }));
			Assert.That(GetSequence(snapshot, "Calls").Count, Is.EqualTo(1));
			Assert.That(
				GetRouteName(GetSequence(snapshot, "Calls").Single()),
				Is.EqualTo("GlobalComponentCollection"));
			Assert.That(GetSequence(snapshot, "Duplicates"), Is.Empty);
			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.False);
			Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.False);
			Assert.That(GetCollectionOwnershipDepth(manager), Is.Zero);
			Assert.That(GetPrivateRegistrationFlag(caller), Is.False);
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.True);
		}

		[Test]
		public void TrueToFalseToggleInsideBlobSaveDataWaitsForOuterCollectionExit()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveablePrefab owner = CreatePrefabRoot(
				"DeferredBlobToGlobalOwner",
				trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"DeferredBlobToGlobalCaller",
				"deferred-blob-to-global-go",
				"deferred-blob-to-global-component");
			manager.RegisterSaveableComponent(caller);

			bool callbackRan = false;
			bool callbackObservedCollectionScope = false;
			bool callbackObservedOriginalBlobOwnership = false;
			bool callbackObservedDeferredRefresh = false;
			caller.BeforeSerialize = () =>
			{
				owner.TrackComponentBlobs = false;
				RequestOwnershipRefresh(manager, owner);
				callbackRan = true;
				callbackObservedCollectionScope =
					GetCollectionOwnershipDepth(manager) > 0;
				callbackObservedOriginalBlobOwnership =
					!IsGloballyRegisteredWithoutReconciliation(manager, caller);
				callbackObservedDeferredRefresh =
					IsOwnershipRefreshDeferred(manager, owner);
			};

			ArmDeepCapture();
			BeginDiagnostics("Deferred blob-to-global toggle");
			byte[] runtimeModifications = null;
			IDisposable outerCollection = BeginOwnershipCollectionScope(manager);
			try
			{
				Assert.DoesNotThrow(
					() => runtimeModifications = owner.CaptureRuntimeModifications(),
					"Changing ownership from blob capture must not mutate global collection state.");

				Assert.That(
					IsGloballyRegisteredWithoutReconciliation(manager, caller),
					Is.False,
					"The transfer must remain deferred until the outer/full collection exits.");
				Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.True);
			}
			finally
			{
				outerCollection.Dispose();
			}

			object snapshot = CompleteDiagnostics();
			RuntimeModificationData decoded =
				SaveDataSerializer.Instance.Deserialize<RuntimeModificationData>(
					runtimeModifications);
			Assert.That(callbackRan, Is.True);
			Assert.That(callbackObservedCollectionScope, Is.True);
			Assert.That(callbackObservedOriginalBlobOwnership, Is.True);
			Assert.That(callbackObservedDeferredRefresh, Is.True);
			Assert.That(caller.SerializeInvocationCount, Is.EqualTo(1));
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.ComponentBlobs, Has.Count.EqualTo(1));
			Assert.That(decoded.ComponentBlobs[0].GuidSuffix,
				Is.EqualTo(caller.ComponentID));
			Assert.That(decoded.ComponentBlobs[0].Payload,
				Is.EqualTo(new byte[] { 1 }));
			Assert.That(GetSequence(snapshot, "Calls").Count, Is.EqualTo(1));
			Assert.That(
				GetRouteName(GetSequence(snapshot, "Calls").Single()),
				Is.EqualTo("PrefabComponentBlob"));
			Assert.That(GetSequence(snapshot, "Duplicates"), Is.Empty);
			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.True);
			Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.False);
			Assert.That(GetCollectionOwnershipDepth(manager), Is.Zero);
			Assert.That(GetPrivateRegistrationFlag(caller), Is.True);
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.False);
		}

		[Test]
		public void PrefabCollectionSnapshotSurvivesCallbackUnregistration()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			PrefabManager prefabManager = SaveManager.Instance.GetPrefabManager;

			SaveablePrefab first = CreatePrefabRoot(
				"CollectionSnapshotFirst",
				trackComponentBlobs: true);
			first.SetUniqueID("collection-snapshot-first");
			SaveablePrefab second = CreatePrefabRoot(
				"CollectionSnapshotSecond",
				trackComponentBlobs: false);
			second.SetUniqueID("collection-snapshot-second");
			SaveDataRouteDiagnosticTestComponent callback = CreateCaller(
				first.transform,
				"CollectionSnapshotCallback",
				"collection-snapshot-first",
				"collection-snapshot-callback");

			prefabManager.SaveablePrefabs.Clear();
			prefabManager.SaveablePrefabs.Add(first);
			prefabManager.SaveablePrefabs.Add(second);
			callback.BeforeSerialize = () =>
				prefabManager.UnregisterPrefab(second);

			List<SaveablePrefabData> collected = null;
			using (BeginOwnershipCollectionScope(manager))
			{
				Assert.DoesNotThrow(
					() => collected = prefabManager.CollectPrefabData(),
					"A component callback must not invalidate prefab collection enumeration.");
			}

			Assert.That(callback.SerializeInvocationCount, Is.EqualTo(1));
			Assert.That(prefabManager.SaveablePrefabs, Has.No.Member(second));
			Assert.That(
				collected.Select(data => data.InstanceID),
				Does.Contain(first.UniqueID));
			Assert.That(
				collected.Select(data => data.InstanceID),
				Does.Contain(second.UniqueID),
				"An unregister-only mutation after the collection boundary must not omit that live prefab from the current save.");
		}

		[Test]
		public void BlobDefaultDisabledDuringLoadAppliesQueuedGlobalBytesOnceAfterBothLoadingGatesClear()
		{
			var manager = new ComponentManager();
			using IDisposable managerHarness = InstallSaveManagerHarness(manager);
			SaveablePrefab owner = CreatePrefabRoot(
				"LoadingOwnershipOwner",
				trackComponentBlobs: true);
			SaveDataRouteDiagnosticTestComponent caller = CreateCaller(
				owner.transform,
				"LoadingOwnershipCaller",
				"loading-ownership-go",
				"loading-ownership-component");
			manager.RegisterSaveableComponent(caller);

			byte[] pendingBytes = { 23, 42, 99 };
			var loadedData = new SaveData(new VersionData(1, 0, 0));
			loadedData.ComponentsData[caller.UniqueIdentifier] = pendingBytes;

			SetSaveManagerLoading(isLoading: true);
			SaveManager.Instance.StateMachine.TransitionTo(SaveState.Loading);
			manager.ApplyComponentData(loadedData);

			Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.True);
			Assert.That(caller.DeserializeInvocationCount, Is.Zero);
			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.False);

			owner.TrackComponentBlobs = false;
			RequestOwnershipRefresh(manager, owner);
			Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.True);
			Assert.That(caller.DeserializeInvocationCount, Is.Zero);

			SaveManager.Instance.StateMachine.TransitionTo(SaveState.Idle);
			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.False,
				"Idle alone must not flush while SaveManager.IsLoading remains true.");
			Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.True);
			Assert.That(caller.DeserializeInvocationCount, Is.Zero);

			SetSaveManagerLoading(isLoading: false);

			Assert.That(
				IsGloballyRegisteredWithoutReconciliation(manager, caller),
				Is.True);
			Assert.That(IsOwnershipRefreshDeferred(manager, owner), Is.False);
			Assert.That(GetPrivateRegistrationFlag(caller), Is.True);
			Assert.That(GetBool(caller, "PrefabHandlesSerialization"), Is.False);
			Assert.That(caller.DeserializeInvocationCount, Is.EqualTo(1));
			Assert.That(caller.LastDeserializedData, Is.EqualTo(pendingBytes));
			Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.False);
			Assert.That(manager.HasComponentDeserialized(caller.UniqueIdentifier),
				Is.True);

			NotifyLoadingFlagCleared(manager);
			RequestOwnershipRefresh(manager, owner);
			Assert.That(caller.DeserializeInvocationCount, Is.EqualTo(1),
				"Repeated idle notifications/refreshes must not apply consumed bytes again.");
			Assert.That(HasPendingBytes(manager, caller.UniqueIdentifier), Is.False);
		}

		private SaveablePrefab CreatePrefabRoot(string name, bool trackComponentBlobs)
		{
			var root = new GameObject(name);
			root.SetActive(false);
			createdRoots.Add(root);

			SaveablePrefab prefab = root.AddComponent<SaveablePrefab>();
			prefab.TrackComponentBlobs = trackComponentBlobs;
			return prefab;
		}

		private SaveDataRouteDiagnosticTestComponent CreateCaller(
			Transform parent,
			string name,
			string gameObjectId,
			string componentId)
		{
			var go = new GameObject(name);
			if (parent != null)
			{
				go.transform.SetParent(parent, false);
			}
			else
			{
				go.SetActive(false);
				createdRoots.Add(go);
			}

			var caller = go.AddComponent<SaveDataRouteDiagnosticTestComponent>();
			caller.SetDiagnosticIdentity(gameObjectId, componentId);
			return caller;
		}

		private SaveDataRoutePlainSaveableTestComponent CreatePlainCaller(
			string name,
			string gameObjectId,
			string componentId)
		{
			var go = new GameObject(name);
			go.SetActive(false);
			createdRoots.Add(go);
			var caller =
				go.AddComponent<SaveDataRoutePlainSaveableTestComponent>();
			caller.SetDiagnosticIdentity(gameObjectId, componentId);
			return caller;
		}

		private IDisposable InstallSaveManagerHarness(
			ComponentManager manager,
			bool active = false)
		{
			var root = new GameObject("SaveManagerOwnershipTestHarness");
			root.SetActive(active);
			createdRoots.Add(root);
			PrefabManager prefabManager = root.GetComponent<PrefabManager>();
			if (prefabManager == null)
				prefabManager = root.AddComponent<PrefabManager>();
			Assert.That(prefabManager, Is.Not.Null);

			SaveManager saveManager = root.AddComponent<SaveManager>();

			PropertyInfo instanceProperty = typeof(SaveManager).GetProperty(
				nameof(SaveManager.Instance),
				StaticFlags);
			Assert.That(instanceProperty, Is.Not.Null);
			SaveManager previous = (SaveManager)instanceProperty.GetValue(null);
			instanceProperty.SetValue(null, saveManager);

			FieldInfo componentManagerField = typeof(SaveManager).GetField(
				"componentManager",
				InstanceFlags);
			Assert.That(componentManagerField, Is.Not.Null);
			componentManagerField.SetValue(saveManager, manager);

			FieldInfo prefabManagerField = typeof(SaveManager).GetField(
				"prefabManager",
				InstanceFlags);
			Assert.That(prefabManagerField, Is.Not.Null);
			prefabManagerField.SetValue(saveManager, prefabManager);

			return new DelegateDisposable(() =>
			{
				if (ReferenceEquals(
					    instanceProperty.GetValue(null),
					    saveManager))
				{
					instanceProperty.SetValue(null, previous);
				}
				});
		}

		private static void IncludePrefabInCollectionManifest(SaveablePrefab prefab)
		{
			PrefabManager prefabManager = SaveManager.Instance?.GetPrefabManager;
			Assert.That(prefabManager, Is.Not.Null);
			if (!prefabManager.SaveablePrefabs.Contains(prefab))
				prefabManager.SaveablePrefabs.Add(prefab);
		}

		private static IDisposable BeginOwnershipCollectionScope(
			ComponentManager manager)
		{
			MethodInfo method = typeof(ComponentManager).GetMethod(
				"BeginSaveDataCollectionOwnershipScope",
				InstanceFlags);
			Assert.That(method, Is.Not.Null);
			object scope = method.Invoke(manager, null);
			Assert.That(scope, Is.InstanceOf<IDisposable>());
			return (IDisposable)scope;
		}

		private static void RequestOwnershipRefresh(
			ComponentManager manager,
			SaveablePrefab owner)
		{
			MethodInfo method = typeof(ComponentManager).GetMethod(
				"RequestPrefabComponentOwnershipRefresh",
				InstanceFlags);
			Assert.That(method, Is.Not.Null);
			method.Invoke(manager, new object[] { owner });
		}

		private static void RequestHierarchyOwnershipRefresh(
			ComponentManager manager,
			SaveableComponent component)
		{
			MethodInfo method = typeof(ComponentManager).GetMethod(
				"ReconcileHierarchyComponentOwnership",
				InstanceFlags);
			Assert.That(method, Is.Not.Null);
			method.Invoke(manager, new object[] { component });
		}

		private static void FlushOwnershipRefreshes(ComponentManager manager)
		{
			MethodInfo method = typeof(ComponentManager).GetMethod(
				"FlushDeferredPrefabOwnershipRefreshes",
				InstanceFlags);
			Assert.That(method, Is.Not.Null);
			method.Invoke(manager, null);
		}

		private static IEnumerator CreateAutomaticOwnershipRetry(
			ComponentManager manager)
		{
			MethodInfo method = typeof(ComponentManager).GetMethod(
				"RetryDeferredPrefabOwnershipOnce",
				InstanceFlags);
			Assert.That(method, Is.Not.Null);
			object retry = method.Invoke(manager, null);
			Assert.That(retry, Is.InstanceOf<IEnumerator>());
			return (IEnumerator)retry;
		}

		private static bool GetComponentManagerBool(
			ComponentManager manager,
			string fieldName)
		{
			FieldInfo field = typeof(ComponentManager).GetField(
				fieldName,
				InstanceFlags);
			Assert.That(field, Is.Not.Null);
			return Convert.ToBoolean(field.GetValue(manager));
		}

		private static bool IsGloballyRegisteredWithoutReconciliation(
			ComponentManager manager,
			ISaveable component)
		{
			MethodInfo method = typeof(ComponentManager).GetMethod(
				"IsGloballyRegisteredForDiagnostics",
				InstanceFlags);
			Assert.That(method, Is.Not.Null);
			return (bool)method.Invoke(manager, new object[] { component });
		}

		private static int GetCollectionOwnershipDepth(ComponentManager manager)
		{
			FieldInfo field = typeof(ComponentManager).GetField(
				"saveDataCollectionDepth",
				InstanceFlags);
			Assert.That(field, Is.Not.Null);
			return (int)field.GetValue(manager);
		}

		private static bool IsOwnershipRefreshDeferred(
			ComponentManager manager,
			SaveablePrefab owner)
		{
			FieldInfo field = typeof(ComponentManager).GetField(
				"deferredPrefabOwnershipRefreshes",
				InstanceFlags);
			Assert.That(field, Is.Not.Null);
			Assert.That(field.GetValue(manager), Is.InstanceOf<IEnumerable>());
			foreach (object entry in (IEnumerable)field.GetValue(manager))
			{
				if (ReferenceEquals(entry, owner))
					return true;
			}

			return false;
		}

		private static bool HasPendingBytes(
			ComponentManager manager,
			string identifier)
		{
            return FeatureTestSupport.PendingBytes(manager).ContainsKey(identifier);
		}

		private static void SetPendingBytes(
			ComponentManager manager,
			string identifier,
			byte[] bytes)
		{
            typeof(ComponentManager).GetMethod("QueuePendingComponentData", InstanceFlags)
                .Invoke(manager, new object[] { identifier, bytes });
		}

		private static bool IsHierarchyOwnershipRefreshDeferred(
			ComponentManager manager,
			SaveableComponent component)
		{
			FieldInfo field = typeof(ComponentManager).GetField(
				"deferredHierarchyOwnershipRefreshes",
				InstanceFlags);
			Assert.That(field, Is.Not.Null);
			Assert.That(field.GetValue(manager), Is.InstanceOf<IEnumerable>());
			foreach (object entry in (IEnumerable)field.GetValue(manager))
			{
				if (ReferenceEquals(entry, component))
					return true;
			}

			return false;
		}

		private static void SetSaveManagerLoading(bool isLoading)
		{
			PropertyInfo property = typeof(SaveManager).GetProperty(
				"IsLoadingInternal",
				InstanceFlags);
			Assert.That(property, Is.Not.Null);
			property.SetValue(SaveManager.Instance, isLoading);
		}

		private static void NotifyLoadingFlagCleared(ComponentManager manager)
		{
			MethodInfo method = typeof(ComponentManager).GetMethod(
				"NotifySaveManagerLoadingFlagCleared",
				InstanceFlags);
			Assert.That(method, Is.Not.Null);
			method.Invoke(manager, null);
		}

		private static SnapshotCaptureSink ArmDeepCapture()
		{
			Type snapshotType = GetNestedType("SaveCallDiagnosticSnapshot");
			var sink = new SnapshotCaptureSink();
			MethodInfo factory = typeof(SaveDataRouteOwnershipEditorTests).GetMethod(
				nameof(CreateCaptureDelegate),
				StaticFlags);
			Assert.That(factory, Is.Not.Null);
			Delegate capture = (Delegate)factory
				.MakeGenericMethod(snapshotType)
				.Invoke(null, new object[] { sink });

			MethodInfo arm = typeof(SaveableComponent).GetMethod(
				"ArmNextSaveCallDiagnosticCapture",
				StaticFlags);
			Assert.That(arm, Is.Not.Null,
				"Expected the internal deep-capture diagnostic seam.");
			arm.Invoke(null, new object[] { capture });
			return sink;
		}

		private static Delegate CreateCaptureDelegate<TSnapshot>(SnapshotCaptureSink sink)
		{
			return new Action<TSnapshot>(snapshot => sink.Snapshot = snapshot);
		}

		private static IDisposable BeginRoute(
			string routeName,
			SaveableComponent expectedComponent,
			SaveablePrefab owner = null)
		{
			MethodInfo beginRoute = typeof(SaveableComponent).GetMethod(
				"BeginSaveDataCallRoute",
				StaticFlags);
			Assert.That(beginRoute, Is.Not.Null,
				"Expected the internal SaveData route scope seam.");

			Type routeType = beginRoute.GetParameters()[0].ParameterType;
			object route = Enum.Parse(routeType, routeName);
			object scope = beginRoute.Invoke(
				null,
				new object[] { route, expectedComponent, owner });
			Assert.That(scope, Is.InstanceOf<IDisposable>());
			return (IDisposable)scope;
		}

		private static void BeginDiagnostics(string operationName)
		{
			MethodInfo method = typeof(SaveableComponent).GetMethod(
				"BeginSaveCallDiagnostics",
				StaticFlags,
				null,
				new[] { typeof(string) },
				null);
			Assert.That(method, Is.Not.Null,
				"Expected BeginSaveCallDiagnostics(string).");
			method.Invoke(null, new object[] { operationName });
		}

		private static object CompleteDiagnostics()
		{
			return GetParameterlessStaticMethod("CompleteSaveCallDiagnostics")
				.Invoke(null, null);
		}

		private static void AbortDiagnostics()
		{
			GetParameterlessStaticMethod("AbortSaveCallDiagnostics")
				.Invoke(null, null);
		}

		private static void ResetDiagnosticState()
		{
			MethodInfo abort = typeof(SaveableComponent).GetMethod(
				"AbortSaveCallDiagnostics",
				StaticFlags);
			abort?.Invoke(null, null);

			MethodInfo cancel = typeof(SaveableComponent).GetMethod(
				"CancelNextSaveCallDiagnosticCapture",
				StaticFlags);
			cancel?.Invoke(null, null);

				SetStaticFieldIfPresent("saveDataCallRouteActive", false);
				SetStaticFieldIfPresent("currentSaveDataExpectedComponent", null);
				SetStaticFieldIfPresent("currentSaveDataPrefabOwner", null);
				SetStaticFieldIfPresent("saveDataInvocationDepth", 0);
		}

		private static object AssertSingleDuplicate(
			object snapshot,
			int expectedTotalCalls)
		{
			IReadOnlyList<object> duplicates = GetSequence(snapshot, "Duplicates");
			Assert.That(duplicates.Count, Is.EqualTo(1));
			Assert.That(GetInt(snapshot, "UniqueKeyCount"), Is.EqualTo(1));
			Assert.That(GetInt(duplicates[0], "TotalCalls"),
				Is.EqualTo(expectedTotalCalls));
			return duplicates[0];
		}

		private static object FindCaller(
			IReadOnlyList<object> callers,
			SaveableComponent component)
		{
			return callers.Single(
				caller => ReferenceEquals(
					GetMemberValue(caller, "Component"),
					component));
		}

		private static string FormatWarning(string operationName, object duplicate)
		{
			MethodInfo formatter = typeof(SaveableComponent).GetMethod(
				"FormatSaveCallDiagnostic",
				StaticFlags,
				null,
				new[] { typeof(string), duplicate.GetType() },
				null);
			Assert.That(formatter, Is.Not.Null);
			return formatter.Invoke(
				null,
				new[] { operationName, duplicate }) as string;
		}

		private static object ResolveWarningContext(object duplicate)
		{
			MethodInfo resolver = typeof(SaveableComponent).GetMethod(
				"FindFirstLiveCaller",
				StaticFlags,
				null,
				new[] { duplicate.GetType() },
				null);
			Assert.That(resolver, Is.Not.Null);
			return resolver.Invoke(null, new[] { duplicate });
		}

		private static void ReleaseSnapshotReferences(object snapshot)
		{
			MethodInfo release = snapshot.GetType().GetMethod(
				"ReleaseComponentReferences",
				InstanceFlags,
				null,
				Type.EmptyTypes,
				null);
			Assert.That(release, Is.Not.Null);
			release.Invoke(snapshot, null);
		}

		private static bool GetPrivateRegistrationFlag(SaveableComponent component)
		{
			FieldInfo field = typeof(SaveableComponent).GetField(
				"_isRegistered",
				InstanceFlags);
			Assert.That(field, Is.Not.Null);
			return (bool)field.GetValue(component);
		}

		private static int GetActiveRecordCount()
		{
			object value = GetStaticField("saveCallRecords");
			Assert.That(value, Is.InstanceOf<IDictionary>());
			return ((IDictionary)value).Count;
		}

		private static IReadOnlyList<object> GetSequence(
			object source,
			string memberName)
		{
			object value = GetMemberValue(source, memberName);
			Assert.That(value, Is.InstanceOf<IEnumerable>());

			var result = new List<object>();
			foreach (object item in (IEnumerable)value)
				result.Add(item);
			return result;
		}

		private static object GetMemberValue(object source, string memberName)
		{
			Assert.That(source, Is.Not.Null);
			PropertyInfo property = null;
			for (Type current = source.GetType();
			     current != null && property == null;
			     current = current.BaseType)
			{
				property = current.GetProperty(
					memberName,
					InstanceFlags | BindingFlags.DeclaredOnly);
			}

			Assert.That(property, Is.Not.Null,
				$"Expected {source.GetType().FullName}.{memberName}.");
			return property.GetValue(source);
		}

		private static bool GetBool(object source, string memberName)
		{
			return Convert.ToBoolean(GetMemberValue(source, memberName));
		}

		private static int GetInt(object source, string memberName)
		{
			return Convert.ToInt32(GetMemberValue(source, memberName));
		}

		private static int GetInt(object value)
		{
			return Convert.ToInt32(value);
		}

		private static int GetIntCallOrder(object occurrence)
		{
			return GetInt(occurrence, "CallOrder");
		}

		private static string GetRouteName(object occurrence)
		{
			return GetMemberValue(occurrence, "Route").ToString();
		}

		private static object GetStaticField(string fieldName)
		{
			Type diagnosticsType = typeof(SaveableComponent).Assembly.GetType(
				"Arawn.CrystalSave.Runtime.SaveCallDiagnostics", true);
			FieldInfo field = diagnosticsType.GetField(
				fieldName,
				StaticFlags);
			Assert.That(field, Is.Not.Null,
				$"Expected SaveCallDiagnostics.{fieldName}.");
			return field.GetValue(null);
		}

		private static void SetStaticFieldIfPresent(string fieldName, object value)
		{
			Type diagnosticsType = typeof(SaveableComponent).Assembly.GetType(
				"Arawn.CrystalSave.Runtime.SaveCallDiagnostics", true);
			FieldInfo field = diagnosticsType.GetField(
				fieldName,
				StaticFlags);
			field?.SetValue(null, value);
		}

		private static MethodInfo GetParameterlessStaticMethod(string methodName)
		{
			MethodInfo method = typeof(SaveableComponent).GetMethod(
				methodName,
				StaticFlags,
				null,
				Type.EmptyTypes,
				null);
			Assert.That(method, Is.Not.Null,
				$"Expected SaveableComponent.{methodName}().");
			return method;
		}

		private static Type GetNestedType(string name)
		{
			Type type = typeof(SaveableComponent).GetNestedType(
				name,
				BindingFlags.Public | BindingFlags.NonPublic);
			Assert.That(type, Is.Not.Null,
				$"Expected nested SaveableComponent.{name}.");
			return type;
		}

		private sealed class SnapshotCaptureSink
		{
			internal object Snapshot { get; set; }
		}

		private sealed class DelegateDisposable : IDisposable
		{
			private Action dispose;

			internal DelegateDisposable(Action dispose)
			{
				this.dispose = dispose;
			}

			public void Dispose()
			{
				Action callback = dispose;
				dispose = null;
				callback?.Invoke();
			}
		}
	}

	internal sealed class SaveDataRouteDiagnosticTestComponent : SaveableComponent
	{
		internal int SerializeInvocationCount { get; private set; }
		internal Action BeforeSerialize { get; set; }
		internal bool ThrowOnSerialize { get; set; }
		internal bool ThrowOnLoad { get; set; }
		internal int LoadInvocationCount { get; private set; }
		internal int DeserializeInvocationCount { get; private set; }
		internal byte[] LastDeserializedData { get; private set; }

		internal void SetDiagnosticIdentity(
			string gameObjectId,
			string componentId)
		{
			uniqueID = gameObjectId;
			ComponentID = componentId;
		}

		internal void ResetSerializeInvocationCount()
		{
			SerializeInvocationCount = 0;
		}

		protected override byte[] SerializeComponentData()
		{
			SerializeInvocationCount++;
			Action callback = BeforeSerialize;
			BeforeSerialize = null;
			callback?.Invoke();

			if (ThrowOnSerialize)
				throw new InvalidOperationException("Intentional diagnostic test failure.");

			return new[] { (byte)Mathf.Clamp(SerializeInvocationCount, 1, byte.MaxValue) };
		}

		public override void LoadData(byte[] data)
		{
			LoadInvocationCount++;
			if (ThrowOnLoad)
				throw new InvalidOperationException("Intentional diagnostic load failure.");

			base.LoadData(data);
		}

		protected override void DeserializeComponentData(byte[] data)
		{
			DeserializeInvocationCount++;
			LastDeserializedData = data?.ToArray();
		}
	}

	internal sealed class SaveDataRoutePlainSaveableTestComponent :
		MonoBehaviour,
		ISaveable
	{
		private string uniqueIdentifier;

		internal int SaveInvocationCount { get; private set; }

		public string UniqueIdentifier => uniqueIdentifier;

		internal void SetDiagnosticIdentity(
			string gameObjectId,
			string componentId)
		{
			uniqueIdentifier = $"{gameObjectId}_{componentId}";
		}

		public byte[] SaveData()
		{
			SaveInvocationCount++;
			return new byte[] { 1 };
		}

		public void LoadData(byte[] data)
		{
		}
	}
}
#endif
