#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Arawn.CrystalSave.Editor;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.EditorTests
{
	public sealed class SaveSlotManagerWindowTimeoutEditorTests
	{
		private bool hadEnabledPreference;
		private bool previousEnabledPreference;
		private bool hadSecondsPreference;
		private int previousSecondsPreference;

		[SetUp]
		public void SetUp()
		{
			hadEnabledPreference = EditorPrefs.HasKey(
				SaveSlotManagerWindow.EnableLoadTimeoutEditorPrefKey);
			previousEnabledPreference = EditorPrefs.GetBool(
				SaveSlotManagerWindow.EnableLoadTimeoutEditorPrefKey,
				SaveSlotManagerWindow.DefaultEnableLoadTimeout);

			hadSecondsPreference = EditorPrefs.HasKey(
				SaveSlotManagerWindow.LoadTimeoutSecondsEditorPrefKey);
			previousSecondsPreference = EditorPrefs.GetInt(
				SaveSlotManagerWindow.LoadTimeoutSecondsEditorPrefKey,
				SaveSlotManagerWindow.DefaultLoadTimeoutSeconds);
		}

		[TearDown]
		public void TearDown()
		{
			RestoreBoolPreference(
				SaveSlotManagerWindow.EnableLoadTimeoutEditorPrefKey,
				hadEnabledPreference,
				previousEnabledPreference);
			RestoreIntPreference(
				SaveSlotManagerWindow.LoadTimeoutSecondsEditorPrefKey,
				hadSecondsPreference,
				previousSecondsPreference);
		}

		[Test]
		public void MissingPreferencesUseEnabledThirtySecondDefault()
		{
			EditorPrefs.DeleteKey(
				SaveSlotManagerWindow.EnableLoadTimeoutEditorPrefKey);
			EditorPrefs.DeleteKey(
				SaveSlotManagerWindow.LoadTimeoutSecondsEditorPrefKey);

			SaveSlotManagerWindow.ReadLoadTimeoutPreferences(
				out bool enabled,
				out int seconds);

			Assert.That(enabled, Is.True);
			Assert.That(seconds, Is.EqualTo(30));
			Assert.That(
				SaveSlotManagerWindow.ResolveLoadTimeout(enabled, seconds),
				Is.EqualTo(TimeSpan.FromSeconds(30)));
		}

		[TestCase(0, 1)]
		[TestCase(-100, 1)]
		[TestCase(86401, 86400)]
		public void PersistedSecondsAreClampedAndRepaired(
			int storedSeconds,
			int expectedSeconds)
		{
			EditorPrefs.SetBool(
				SaveSlotManagerWindow.EnableLoadTimeoutEditorPrefKey,
				true);
			EditorPrefs.SetInt(
				SaveSlotManagerWindow.LoadTimeoutSecondsEditorPrefKey,
				storedSeconds);

			SaveSlotManagerWindow.ReadLoadTimeoutPreferences(
				out bool enabled,
				out int seconds);

			Assert.That(enabled, Is.True);
			Assert.That(seconds, Is.EqualTo(expectedSeconds));
			Assert.That(
				EditorPrefs.GetInt(
					SaveSlotManagerWindow.LoadTimeoutSecondsEditorPrefKey),
				Is.EqualTo(expectedSeconds));
		}

		[Test]
		public void WritePreferencesPersistsValidatedValues()
		{
			SaveSlotManagerWindow.WriteLoadTimeoutPreferences(
				enabled: false,
				seconds: int.MaxValue);

			SaveSlotManagerWindow.ReadLoadTimeoutPreferences(
				out bool enabled,
				out int seconds);

			Assert.That(enabled, Is.False);
			Assert.That(seconds, Is.EqualTo(86400));
		}

		[Test]
		public void DisabledTimeoutResolvesToNull()
		{
			Assert.That(
				SaveSlotManagerWindow.ResolveLoadTimeout(
					enabled: false,
					seconds: 30),
				Is.Null);
		}

		[Test]
		public void OnlySynchronousLocalEntryPointSchedulesWindowTimer()
		{
			TimeSpan? timeout = TimeSpan.FromSeconds(30);

			Assert.That(
				SaveSlotManagerWindow.ShouldScheduleWindowTimeout(
					runtimeOwnsTimeout: false,
					timeout: timeout),
				Is.True,
				"Synchronous local loads must arm the window cancellation source.");
			Assert.That(
				SaveSlotManagerWindow.ShouldScheduleWindowTimeout(
					runtimeOwnsTimeout: true,
					timeout: timeout),
				Is.False,
				"Async and metadata loads pass the timeout to the Runtime API.");
			Assert.That(
				SaveSlotManagerWindow.ShouldScheduleWindowTimeout(
					runtimeOwnsTimeout: false,
					timeout: null),
				Is.False,
					"Disabling the timeout must leave manual cancellation only.");
		}

		[Test]
		public void ReplacingAndCompletingLoadOperationsDisposesCancellationSources()
		{
			SaveSlotManagerWindow window =
				ScriptableObject.CreateInstance<SaveSlotManagerWindow>();
			MethodInfo begin = typeof(SaveSlotManagerWindow).GetMethod(
				"BeginLoadCancellation",
				BindingFlags.Instance |
				BindingFlags.Public |
				BindingFlags.NonPublic);
			MethodInfo dispose = typeof(SaveSlotManagerWindow).GetMethod(
				"DisposeLoadCancellationTokenSource",
				BindingFlags.Instance |
				BindingFlags.Public |
				BindingFlags.NonPublic);
			Assert.That(begin, Is.Not.Null);
			Assert.That(dispose, Is.Not.Null);

			try
			{
				var first =
					(CancellationTokenSource)begin.Invoke(window, null);
				var second =
					(CancellationTokenSource)begin.Invoke(window, null);

				Assert.Throws<ObjectDisposedException>(
					() => _ = first.Token,
					"Starting a replacement operation must dispose the old source.");
				Assert.DoesNotThrow(() => _ = second.Token);

				dispose.Invoke(window, new object[] { second });
				Assert.Throws<ObjectDisposedException>(
					() => _ = second.Token,
					"Completing an operation must dispose its source.");

				FieldInfo sourceField =
					typeof(SaveSlotManagerWindow).GetField(
						"loadCancellationTokenSource",
						BindingFlags.Instance |
						BindingFlags.Public |
						BindingFlags.NonPublic);
				Assert.That(sourceField, Is.Not.Null);
				Assert.That(sourceField.GetValue(window), Is.Null);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(window);
			}
		}

		[Test]
		public void CompletingOnlyOwnedOperationCannotClobberReplacement()
		{
			SaveSlotManagerWindow window =
				ScriptableObject.CreateInstance<SaveSlotManagerWindow>();
			MethodInfo begin = typeof(SaveSlotManagerWindow).GetMethod(
				"BeginLoadCancellation",
				BindingFlags.Instance |
				BindingFlags.Public |
				BindingFlags.NonPublic);
			MethodInfo complete = typeof(SaveSlotManagerWindow).GetMethod(
				"CompleteOwnedLoadOperation",
				BindingFlags.Instance |
				BindingFlags.Public |
				BindingFlags.NonPublic);
			FieldInfo loadingField = typeof(SaveSlotManagerWindow).GetField(
				"isLoading",
				BindingFlags.Instance |
				BindingFlags.Public |
				BindingFlags.NonPublic);
			FieldInfo slotField = typeof(SaveSlotManagerWindow).GetField(
				"loadingSlotNumber",
				BindingFlags.Instance |
				BindingFlags.Public |
				BindingFlags.NonPublic);
			FieldInfo sourceField =
				typeof(SaveSlotManagerWindow).GetField(
					"loadCancellationTokenSource",
					BindingFlags.Instance |
					BindingFlags.Public |
					BindingFlags.NonPublic);
			Assert.That(begin, Is.Not.Null);
			Assert.That(complete, Is.Not.Null);
			Assert.That(loadingField, Is.Not.Null);
			Assert.That(slotField, Is.Not.Null);
			Assert.That(sourceField, Is.Not.Null);

			try
			{
				var first =
					(CancellationTokenSource)begin.Invoke(window, null);
				loadingField.SetValue(window, true);
				slotField.SetValue(window, 7);

				var replacement =
					(CancellationTokenSource)begin.Invoke(window, null);
				Assert.That(
					(bool)complete.Invoke(window, new object[] { first }),
					Is.False);
				Assert.That(loadingField.GetValue(window), Is.True);
				Assert.That(slotField.GetValue(window), Is.EqualTo(7));
				Assert.That(
					sourceField.GetValue(window),
					Is.SameAs(replacement));

				Assert.That(
					(bool)complete.Invoke(
						window,
						new object[] { replacement }),
					Is.True);
				Assert.That(loadingField.GetValue(window), Is.False);
				Assert.That(slotField.GetValue(window), Is.EqualTo(-1));
				Assert.That(sourceField.GetValue(window), Is.Null);
				Assert.Throws<ObjectDisposedException>(
					() => _ = replacement.Token);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(window);
			}
		}

		[Test]
		public async Task RuntimeOwnedCancellationInterruptsMetadataStageAwait()
		{
			MethodInfo createSource = typeof(SaveManager).GetMethod(
				"CreateRuntimeLoadOperationCancellationSource",
				BindingFlags.Static |
				BindingFlags.Public |
				BindingFlags.NonPublic);
			MethodInfo awaitStage = typeof(SaveManager).GetMethod(
				"AwaitWithCancellation",
				BindingFlags.Static |
				BindingFlags.Public |
				BindingFlags.NonPublic);
			Assert.That(createSource, Is.Not.Null);
			Assert.That(awaitStage, Is.Not.Null);

			using var callerCancellation =
				new CancellationTokenSource();
			using var operationCancellation =
				(CancellationTokenSource)createSource.Invoke(
					null,
					new object[]
					{
						null,
						callerCancellation.Token
					});
			var unfinishedStage =
				new TaskCompletionSource<bool>(
					TaskCreationOptions.RunContinuationsAsynchronously);
			Task stageAwait = (Task)awaitStage.Invoke(
				null,
				new object[]
				{
					unfinishedStage.Task,
					operationCancellation.Token
				});

			callerCancellation.Cancel();
			try
			{
				await stageAwait;
				Assert.Fail(
					"The Runtime-owned linked token did not cancel the metadata stage await.");
			}
			catch (OperationCanceledException)
			{
				Assert.That(
					operationCancellation.IsCancellationRequested,
					Is.True);
			}
		}

		[Test]
		public void AllThreeLoadEntryPointsWireTheResolvedTimeoutToOneOwner()
		{
			MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
				"Assets/Plugins/CrystalSave/Editor/SaveSlotManagerWindow.cs");
			Assert.That(script, Is.Not.Null);
			string source = script.text;

			string automaticEntry = Slice(
				source,
				"private async void StartLoadOperation(int slotNumber)",
				"private async void StartLoadOperationAsync(int slotNumber)");
			Assert.That(
				CountOccurrences(
					automaticEntry,
					"ResolveConfiguredLoadTimeout()"),
				Is.EqualTo(1));
			Assert.That(automaticEntry, Does.Contain("LoadSaveSlotAsync("));
			Assert.That(
				automaticEntry,
				Does.Contain("timeout: useRuntimeAsync ? operationTimeout : null"));
			Assert.That(
				automaticEntry,
				Does.Contain("await saveManager.LoadSaveSlotAsync("));
			Assert.That(
				automaticEntry,
				Does.Not.Contain("_ = saveManager.LoadSaveSlotAsync("));
			Assert.That(
				automaticEntry,
				Does.Contain(
					"CompleteOwnedLoadOperation(operationCancellation)"));
			Assert.That(
				automaticEntry,
				Does.Contain("runtimeOwnsTimeout: useRuntimeAsync"));
			Assert.That(
				automaticEntry,
				Does.Contain(
					"operationCancellation.CancelAfter(operationTimeout.Value)"));
			Assert.That(automaticEntry, Does.Not.Contain("saveManager.Load("));

			string asyncEntry = Slice(
				source,
				"private async void StartLoadOperationAsync(int slotNumber)",
				"private void OnLoadCompleted(");
			Assert.That(
				CountOccurrences(
					asyncEntry,
					"ResolveConfiguredLoadTimeout()"),
				Is.EqualTo(1));
			Assert.That(asyncEntry, Does.Contain("LoadSaveSlotAsync("));
			Assert.That(
				asyncEntry,
				Does.Contain("timeout: operationTimeout"));
			Assert.That(asyncEntry, Does.Not.Contain("CancelAfter("));
			Assert.That(
				asyncEntry,
				Does.Contain(
					"CompleteOwnedLoadOperation(operationCancellation)"));

			string metadataEntry = Slice(
				source,
				"private async void LoadByMetadataFromPanelAsync()",
				"private void OpenSaveFolder()");
			Assert.That(
				CountOccurrences(
					metadataEntry,
					"ResolveConfiguredLoadTimeout()"),
				Is.EqualTo(1));
			Assert.That(metadataEntry, Does.Contain("LoadByMetadataAsync("));
			Assert.That(
				metadataEntry,
				Does.Contain("timeout: operationTimeout"));
			Assert.That(metadataEntry, Does.Not.Contain("CancelAfter("));
			Assert.That(
				metadataEntry,
				Does.Contain(
					"CompleteOwnedLoadOperation(operationCancellation)"));
		}

		[Test]
		public async Task RuntimeLoadCancellationScopeLinksCallerAndTimeout()
		{
            var factory = typeof(SaveManager).GetMethod("CreateRuntimeLoadOperationCancellationSource", BindingFlags.Static | BindingFlags.NonPublic);
            using var caller = new CancellationTokenSource();
            using var linked = (CancellationTokenSource)factory.Invoke(null, new object[] { TimeSpan.FromSeconds(5), caller.Token });
            caller.Cancel(); Assert.That(linked.IsCancellationRequested, Is.True);
            using var deadline = (CancellationTokenSource)factory.Invoke(null, new object[] { TimeSpan.FromMilliseconds(20), CancellationToken.None });
            var observed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (deadline.Token.Register(() => observed.TrySetResult(true)))
                Assert.That(await Task.WhenAny(observed.Task, Task.Delay(2000)), Is.SameAs(observed.Task));
        }

		private static string Slice(
			string source,
			string startMarker,
			string endMarker)
		{
			int start = source.IndexOf(
				startMarker,
				StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0));
			int end = source.IndexOf(
				endMarker,
				start + startMarker.Length,
				StringComparison.Ordinal);
			Assert.That(end, Is.GreaterThan(start));
			return source.Substring(start, end - start);
		}

		private static int CountOccurrences(string source, string value)
		{
			int count = 0;
			int index = 0;
			while ((index = source.IndexOf(
				       value,
				       index,
				       StringComparison.Ordinal)) >= 0)
			{
				count++;
				index += value.Length;
			}

			return count;
		}

		private static void RestoreBoolPreference(
			string key,
			bool existed,
			bool value)
		{
			if (existed)
				EditorPrefs.SetBool(key, value);
			else
				EditorPrefs.DeleteKey(key);
		}

		private static void RestoreIntPreference(
			string key,
			bool existed,
			int value)
		{
			if (existed)
				EditorPrefs.SetInt(key, value);
			else
				EditorPrefs.DeleteKey(key);
		}
	}
}
#endif
