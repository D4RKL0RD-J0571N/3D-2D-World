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
	public sealed class SaveDataCallDiagnosticsEditorTests
	{
		private const BindingFlags StaticFlags =
			BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags InstanceFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private readonly List<GameObject> createdRoots = new();

		[SetUp]
		public void SetUp()
		{
			InvokeLifecycle("AbortSaveCallDiagnostics");
		}

		[TearDown]
		public void TearDown()
		{
			InvokeLifecycle("AbortSaveCallDiagnostics");

			foreach (GameObject root in createdRoots)
			{
				if (root != null)
					UnityEngine.Object.DestroyImmediate(root);
			}

			createdRoots.Clear();
		}

		[Test]
		public void SaveDataCallsOutsideDiagnosticCycleAreIgnored()
		{
			SaveDataCallDiagnosticTestComponent caller = CreateCaller(
				"OutsideCycleRoot",
				"OutsideCycleCaller",
				"outside-game-object",
				"outside-component");

			caller.SaveData();
			caller.SaveData();

			BeginDiagnostics();
			object snapshot = CompleteDiagnostics();

			Assert.That(GetUniqueKeyCount(snapshot), Is.Zero);
			Assert.That(GetEntries(snapshot), Is.Empty);
		}

		[Test]
		public void OneSaveDataCallInsideDiagnosticCycleIsNotDuplicate()
		{
			SaveDataCallDiagnosticTestComponent caller = CreateCaller(
				"SingleCallRoot",
				"SingleCallCaller",
				"single-game-object",
				"single-component");

			BeginDiagnostics();
			caller.SaveData();
			object snapshot = CompleteDiagnostics();

			Assert.That(GetUniqueKeyCount(snapshot), Is.EqualTo(1));
			Assert.That(GetEntries(snapshot), Is.Empty);
		}

		[Test]
		public void RepeatedCallsFromOneComponentProduceOneRichDuplicate()
		{
			SaveDataCallDiagnosticTestComponent caller = CreateCaller(
				"RepeatedCallRoot",
				"RepeatedCallCaller",
				"same-caller-game-object",
				"same-caller-component");

			BeginDiagnostics();
			caller.SaveData();
			caller.SaveData();
			object snapshot = CompleteDiagnostics();

			object duplicate = AssertSingleDuplicate(snapshot, expectedTotalCalls: 2);
			Assert.That(GetMemberValue(duplicate, "CompositeKey"),
				Is.EqualTo("same-caller-game-object_same-caller-component"));
			IReadOnlyList<object> callers = GetCallers(duplicate);
			Assert.That(callers.Count, Is.EqualTo(1));
			Assert.That(GetCallerCallCount(callers[0]), Is.EqualTo(2));
			Assert.That(GetMemberValue(callers[0], "Component"), Is.SameAs(caller));
			Assert.That(GetMemberValue(callers[0], "ComponentType"),
				Is.EqualTo(typeof(SaveDataCallDiagnosticTestComponent)));
			Assert.That(GetMemberValue(callers[0], "GameObjectId"),
				Is.EqualTo("same-caller-game-object"));
			Assert.That(GetMemberValue(callers[0], "ComponentId"),
				Is.EqualTo("same-caller-component"));
			Assert.That(ResolveWarningContext(duplicate), Is.SameAs(caller),
				"A live diagnostic caller should be supplied as the Unity log context.");

			string warning = FormatWarning(duplicate);
			Assert.That(warning, Does.Contain(nameof(SaveDataCallDiagnosticTestComponent)));
			Assert.That(warning, Does.Contain("RepeatedCallRoot/RepeatedCallCaller"));
			Assert.That(warning, Does.Contain("in scene"));
			Assert.That(warning, Does.Contain("same-caller-game-object"));
			Assert.That(warning, Does.Contain("same-caller-component"));
			Assert.That(warning, Does.Contain("2 calls from 1 component instance"));
			Assert.That(warning, Does.Contain("Unity Object ID:"));
			Assert.That(warning, Does.Contain("not a PrefabRegistry ID").IgnoreCase);
			Assert.That(warning, Does.Contain("same component instance").IgnoreCase);
			Assert.That(warning, Does.Contain("serialized more than once").IgnoreCase);
		}

		[Test]
		public void ComponentsSharingCompositeKeyProduceCollisionGuidanceForBothCallers()
		{
			SaveDataCallDiagnosticTestComponent first = CreateCaller(
				"CollisionRootA",
				"CollisionCallerA",
				"shared-game-object",
				"shared-component");
			SaveDataCallDiagnosticTestComponent second = CreateCaller(
				"CollisionRootB",
				"CollisionCallerB",
				"shared-game-object",
				"shared-component");

			BeginDiagnostics();
			first.SaveData();
			second.SaveData();
			object snapshot = CompleteDiagnostics();

			object duplicate = AssertSingleDuplicate(snapshot, expectedTotalCalls: 2);
			Assert.That(GetMemberValue(duplicate, "CompositeKey"),
				Is.EqualTo("shared-game-object_shared-component"));
			IReadOnlyList<object> callers = GetCallers(duplicate);
			Assert.That(callers.Count, Is.EqualTo(2));
			Assert.That(callers.Select(GetCallerCallCount), Is.All.EqualTo(1));
			Assert.That(callers.Select(caller => GetMemberValue(caller, "Component")),
				Is.EquivalentTo(new object[] { first, second }));
			Assert.That(ResolveWarningContext(duplicate), Is.SameAs(first),
				"The first live caller should make the collision warning clickable.");

			string warning = FormatWarning(duplicate);
			Assert.That(warning, Does.Contain("CollisionRootA/CollisionCallerA"));
			Assert.That(warning, Does.Contain("CollisionRootB/CollisionCallerB"));
			Assert.That(warning, Does.Contain("shared-game-object"));
			Assert.That(warning, Does.Contain("shared-component"));
			Assert.That(warning, Does.Contain("not a PrefabRegistry ID").IgnoreCase);
			Assert.That(warning, Does.Contain("Different component instances share this save key"));
			Assert.That(warning, Does.Contain("later data can overwrite earlier data"));
			Assert.That(warning,
				Does.Contain("Regenerate one component's Component ID or its owning GameObject UniqueID"));
		}

		[Test]
		public void CompletingOrAbortingCycleClearsRecordedCalls()
		{
			SaveDataCallDiagnosticTestComponent caller = CreateCaller(
				"ClearCycleRoot",
				"ClearCycleCaller",
				"clear-game-object",
				"clear-component");

			BeginDiagnostics();
			caller.SaveData();
			caller.SaveData();
			object completedSnapshot = CompleteDiagnostics();
			object completedDuplicate = AssertSingleDuplicate(completedSnapshot, expectedTotalCalls: 2);
			object completedCaller = GetCallers(completedDuplicate).Single();
			Assert.That(GetMemberValue(completedCaller, "Component"), Is.SameAs(caller));
			Assert.That(GetActiveRecordCount(), Is.Zero);

			ReleaseSnapshotReferences(completedSnapshot);
			Assert.That(GetMemberValue(completedCaller, "Component"), Is.Null,
				"Released snapshots must not retain Unity component references.");

			BeginDiagnostics();
			object emptyAfterComplete = CompleteDiagnostics();
			Assert.That(GetUniqueKeyCount(emptyAfterComplete), Is.Zero);
			Assert.That(GetEntries(emptyAfterComplete), Is.Empty,
				"Completing a cycle should clear the records consumed from the prior cycle.");

			BeginDiagnostics();
			caller.SaveData();
			caller.SaveData();
			Assert.That(GetActiveRecordCount(), Is.EqualTo(1));
			AbortDiagnostics();
			Assert.That(GetActiveRecordCount(), Is.Zero);

			BeginDiagnostics();
			Assert.That(GetEntries(CompleteDiagnostics()), Is.Empty,
				"Aborting a cycle should release and clear all recorded callers.");
		}

		[Test]
		public void DestroyedCallerFormatsWithSafeFallback()
		{
			SaveDataCallDiagnosticTestComponent caller = CreateCaller(
				"DestroyedRoot",
				"DestroyedCaller",
				"destroyed-game-object",
				"destroyed-component");
			GameObject root = caller.transform.root.gameObject;

			BeginDiagnostics();
			caller.SaveData();
			caller.SaveData();
			UnityEngine.Object.DestroyImmediate(root);

			object duplicate = AssertSingleDuplicate(CompleteDiagnostics(), expectedTotalCalls: 2);
			string warning = FormatWarning(duplicate);

			Assert.That(warning, Is.Not.Null.And.Not.Empty);
			Assert.That(
				warning.IndexOf("destroyed", StringComparison.OrdinalIgnoreCase) >= 0 ||
				warning.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0,
				Is.True);
			Assert.That(warning, Does.Contain("destroyed-game-object"));
			Assert.That(warning, Does.Contain("destroyed-component"));
		}

		private SaveDataCallDiagnosticTestComponent CreateCaller(
			string rootName,
			string childName,
			string gameObjectId,
			string componentId)
		{
			var root = new GameObject(rootName);
			root.SetActive(false);
			createdRoots.Add(root);

			var child = new GameObject(childName);
			child.transform.SetParent(root.transform, false);

			var caller = child.AddComponent<SaveDataCallDiagnosticTestComponent>();
			caller.SetDiagnosticIdentity(gameObjectId, componentId);
			return caller;
		}

		private static void BeginDiagnostics()
		{
			InvokeLifecycle("BeginSaveCallDiagnostics");
		}

		private static object CompleteDiagnostics()
		{
			MethodInfo method = GetLifecycleMethod("CompleteSaveCallDiagnostics");
			Assert.That(method.ReturnType, Is.Not.EqualTo(typeof(void)),
				"CompleteSaveCallDiagnostics must return an inspectable internal snapshot.");
			return method.Invoke(null, null);
		}

		private static void AbortDiagnostics()
		{
			InvokeLifecycle("AbortSaveCallDiagnostics");
		}

		private static void InvokeLifecycle(string methodName)
		{
			GetLifecycleMethod(methodName).Invoke(null, null);
		}

		private static MethodInfo GetLifecycleMethod(string methodName)
		{
			MethodInfo method = typeof(SaveableComponent).GetMethod(
				methodName,
				StaticFlags,
				null,
				Type.EmptyTypes,
				null);

			Assert.That(method, Is.Not.Null,
				$"Expected internal SaveableComponent.{methodName}() diagnostic seam.");
			return method;
		}

		private static object AssertSingleDuplicate(object snapshot, int expectedTotalCalls)
		{
			IReadOnlyList<object> entries = GetEntries(snapshot);
			Assert.That(entries.Count, Is.EqualTo(1));
			Assert.That(GetUniqueKeyCount(snapshot), Is.EqualTo(1));

			object duplicate = entries[0];
			Assert.That(GetTotalCallCount(duplicate), Is.EqualTo(expectedTotalCalls));
			return duplicate;
		}

		private static IReadOnlyList<object> GetEntries(object snapshot)
		{
			if (snapshot == null)
				return Array.Empty<object>();

			return ToObjectList(GetMemberValue(snapshot, "Duplicates")).ToArray();
		}

		private static IReadOnlyList<object> GetCallers(object duplicate)
		{
			return ToObjectList(GetMemberValue(duplicate, "Callers")).ToArray();
		}

		private static int GetUniqueKeyCount(object snapshot)
		{
			return Convert.ToInt32(GetMemberValue(snapshot, "UniqueKeyCount"));
		}

		private static int GetTotalCallCount(object duplicate)
		{
			return Convert.ToInt32(GetMemberValue(duplicate, "TotalCalls"));
		}

		private static int GetCallerCallCount(object caller)
		{
			return Convert.ToInt32(GetMemberValue(caller, "CallCount"));
		}

		private static string FormatWarning(object duplicate)
		{
			MethodInfo formatter = typeof(SaveableComponent).GetMethod(
				"FormatSaveCallDiagnostic",
				StaticFlags,
				null,
				new[] { typeof(string), duplicate.GetType() },
				null);
			Assert.That(formatter, Is.Not.Null,
				"Expected a private/internal pure warning formatter for save-call diagnostics.");
			return formatter.Invoke(null, new[] { "EditMode diagnostic test", duplicate }) as string;
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

		private static int GetActiveRecordCount()
		{
			Type diagnosticsType = typeof(SaveableComponent).Assembly.GetType(
				"Arawn.CrystalSave.Runtime.SaveCallDiagnostics", true);
			FieldInfo recordsField = diagnosticsType.GetField(
				"saveCallRecords",
				StaticFlags);
			Assert.That(recordsField, Is.Not.Null);
			var records = recordsField.GetValue(null) as IDictionary;
			Assert.That(records, Is.Not.Null);
			return records.Count;
		}

		private static object GetMemberValue(object source, string memberName)
		{
			Assert.That(source, Is.Not.Null);
			PropertyInfo property = source.GetType().GetProperty(memberName, InstanceFlags);
			Assert.That(property, Is.Not.Null,
				$"Expected internal property {source.GetType().FullName}.{memberName}.");
			return property.GetValue(source);
		}

		private static IReadOnlyList<object> ToObjectList(object value)
		{
			if (value == null)
				return Array.Empty<object>();

			if (value is string || value is not IEnumerable enumerable)
				return new[] { value };

			var items = new List<object>();
			foreach (object item in enumerable)
				items.Add(item);
			return items;
		}
	}

	internal sealed class SaveDataCallDiagnosticTestComponent : SaveableComponent
	{
		internal void SetDiagnosticIdentity(string gameObjectId, string componentId)
		{
			uniqueID = gameObjectId;
			ComponentID = componentId;
		}

		protected override byte[] SerializeComponentData()
		{
			return Array.Empty<byte>();
		}

		protected override void DeserializeComponentData(byte[] data)
		{
		}
	}
}
#endif
