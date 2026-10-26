#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator SaveDiagnostics_Save_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("Save");
        [UnityTest] public IEnumerator SaveDiagnostics_SaveNamed_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("SaveNamed");
        [UnityTest] public IEnumerator SaveDiagnostics_SaveSync_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("SaveSync");
        [UnityTest] public IEnumerator SaveDiagnostics_SaveSyncScreenshot_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("SaveSyncScreenshot");
        [UnityTest] public IEnumerator SaveDiagnostics_SaveAsync_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("SaveAsync");
        [UnityTest] public IEnumerator SaveDiagnostics_SaveAsyncNamed_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("SaveAsyncNamed");
        [UnityTest] public IEnumerator SaveDiagnostics_SaveAsyncDebug_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("SaveAsyncDebug");
        [UnityTest] public IEnumerator SaveDiagnostics_SaveImmediate_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("SaveImmediate");
        [UnityTest] public IEnumerator SaveDiagnostics_SaveImmediateToken_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("SaveImmediateToken");
        [UnityTest] public IEnumerator SaveDiagnostics_QuickSave_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("QuickSave");
        [UnityTest] public IEnumerator SaveDiagnostics_QuickSaveAsync_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("QuickSaveAsync");
        [UnityTest] public IEnumerator SaveDiagnostics_AutoSave_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("AutoSave");
        [UnityTest] public IEnumerator SaveDiagnostics_AutoSaveAsync_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("AutoSaveAsync");
        [UnityTest] public IEnumerator SaveDiagnostics_ForTag_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("ForTag");
        [UnityTest] public IEnumerator SaveDiagnostics_ExcludingTag_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("ExcludingTag");
        [UnityTest] public IEnumerator SaveDiagnostics_ForGameObject_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("ForGameObject");
        [UnityTest] public IEnumerator SaveDiagnostics_ExcludingGameObject_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("ExcludingGameObject");
        [UnityTest] public IEnumerator SaveDiagnostics_WithFilter_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("WithFilter");
        [UnityTest] public IEnumerator SaveDiagnostics_ByMetadata_ExcludeOutsideCaptures() => CheckPersistedDiagnosticIsolation("ByMetadata");

        private IEnumerator CheckPersistedDiagnosticIsolation(string entry)
        {
            yield return LoadScene("A");
            var manager = SaveManager.Instance;
            var root = new GameObject("Diagnostic capture owner") { tag = "Player" };
            root.SetActive(false);
            Composite(root, typeof(PersistedDiagnosticProbe));
            var probe = root.GetComponent<PersistedDiagnosticProbe>();
            root.SetActive(true);
            manager.RegisterGameObject(root, new GameObjectPropertySettings { RememberActive = true });
            var excluded = new GameObject("Excluded control") { tag = "Respawn" };
            yield return null;
            Assert.That(manager.ComponentManager.Contains(probe), Is.True);
            var screenshots = Get<ScreenshotManager>(manager, "screenshotManager");
            var previousCapture = screenshots.CustomCapture;
            bool previousScreenshots = manager.SaveSettings.enableScreenshots;
            var captures = new DiagnosticCaptureObserver();
            var successes = new List<SaveLoadEventArgs>();
            var failures = new List<OperationFailedEventArgs>();
            EventHandler<SaveLoadEventArgs> onSuccess = (_, args) => successes.Add(args);
            EventHandler<OperationFailedEventArgs> onFailure = (_, args) => failures.Add(args);
            manager.OnSaveCompleted += onSuccess;
            manager.OnSaveFailed += onFailure;
            int screenshotCalls = 0;
            bool screenshotSawActiveCycle = false;
            screenshots.CustomCapture = () =>
            {
                screenshotCalls++;
                screenshotSawActiveCycle |= DiagnosticCycleActive();
                probe.SaveData();
                probe.SaveData();
                var texture = new Texture2D(2, 2);
                texture.Apply();
                return texture;
            };
            manager.SaveSettings.enableScreenshots = true;
            try
            {
                for (int generation = 0; generation < 2; generation++)
                {
                    probe.Value = 100 + generation;
                    int expectedValue = probe.Value;
                    int priorCaptures = captures.Snapshots.Count;
                    captures.Arm();
                    ExerciseOutsideDiagnosticCaptures(manager, probe, screenshots);
                    Assert.That(captures.Snapshots.Count, Is.EqualTo(priorCaptures),
                        entry + ": outside work consumed the next-save diagnostic subscription.");
                    Assert.That(root.activeInHierarchy, Is.True, "Tag-filter fixture must be active.");
                    Assert.That(GameObject.FindGameObjectsWithTag("Player"), Does.Contain(root),
                        "Tag-filter fixture must be discoverable by Unity's tag query.");
                    int activeCalls = probe.ActiveCycleCalls;
                    int priorSuccesses = successes.Count;
                    Task operation = StartDiagnosticSave(entry, manager, probe.gameObject, excluded);
                    if (operation != null) yield return Await(operation);
                    yield return Until(() => successes.Count > priorSuccesses || failures.Count > 0,
                        entry + " completion");
                    Assert.That(failures, Is.Empty, entry);
                    Assert.That(successes.Count, Is.EqualTo(priorSuccesses + 1), entry);
                    Assert.That(successes.Last().Success, Is.True, entry);
                    Assert.That(captures.Snapshots.Count, Is.EqualTo(priorCaptures + 1), entry);
                    Assert.That(probe.ActiveCycleCalls - activeCalls, Is.EqualTo(1), entry);
                    Assert.That(DiagnosticCycleActive(), Is.False, entry);
                    Assert.That(screenshotSawActiveCycle, Is.False,
                        entry + ": screenshot serialization leaked into a persisted capture.");

                    object snapshot = captures.Snapshots.Last();
                    Assert.That(DiagnosticMember(snapshot, "OperationName"), Is.EqualTo("Save capture"), entry);
                    Assert.That(DiagnosticMember(snapshot, "HasDuplicates"), Is.EqualTo(false), entry);
                    Assert.That(((IEnumerable)DiagnosticMember(snapshot, "Duplicates")).Cast<object>(), Is.Empty, entry);
                    Assert.That(((IEnumerable)DiagnosticMember(snapshot, "UntracedDuplicateKeys")).Cast<object>(), Is.Empty, entry);
                    var calls = ((IEnumerable)DiagnosticMember(snapshot, "Calls")).Cast<object>()
                        .Where(call => (string)DiagnosticMember(call, "CompositeKey") == probe.UniqueIdentifier).ToArray();
                    Assert.That(calls.Length, Is.EqualTo(1), entry);
                    Assert.That(DiagnosticMember(calls[0], "Route").ToString(), Is.EqualTo("GlobalComponentCollection"), entry);

                    // Queuing a public read also drains legacy void wrappers' queue ownership.
                    var read = manager.LoadSaveDataForSlotAsync(successes.Last().Slot.SlotNumber);
                    yield return Await(read);
                    Assert.That(read.Result, Is.Not.Null, entry);
                    Assert.That(read.Result.ComponentsData.TryGetValue(probe.UniqueIdentifier, out var bytes), Is.True, entry);
                    Assert.That(BitConverter.ToInt32(bytes, 0), Is.EqualTo(expectedValue), entry);
                    ExerciseOutsideDiagnosticCaptures(manager, probe, screenshots);
                    Assert.That(captures.Snapshots.Count, Is.EqualTo(priorCaptures + 1), entry);
                    Assert.That(probe.ActiveCycleCalls - activeCalls, Is.EqualTo(1), entry);
                }
                Assert.That(screenshotCalls, Is.GreaterThanOrEqualTo(4));
                Assert.That(screenshotSawActiveCycle, Is.False);
            }
            finally
            {
                Call(typeof(SaveableComponent), "CancelNextSaveCallDiagnosticCapture");
                screenshots.CustomCapture = previousCapture;
                manager.SaveSettings.enableScreenshots = previousScreenshots;
                manager.OnSaveCompleted -= onSuccess;
                manager.OnSaveFailed -= onFailure;
                Object.Destroy(root);
                Object.Destroy(excluded);
            }
        }

        private static void ExerciseOutsideDiagnosticCaptures(SaveManager manager,
            PersistedDiagnosticProbe probe, ScreenshotManager screenshots)
        {
            Assert.That(DiagnosticCycleActive(), Is.False);
            int before = probe.TotalCalls;
            string image = screenshots.CaptureScreenshotSync(1);
            Assert.That(image, Is.Not.Null.And.Not.Empty);
            Assert.That(probe.TotalCalls - before, Is.EqualTo(2), "Real custom screenshot capture did not serialize the probe.");

            probe.RememberHomeScene = true;
            before = probe.TotalCalls;
            manager.ComponentManager.SnapshotCurrentSceneAll();
            Assert.That(probe.TotalCalls, Is.GreaterThan(before), "Real scene snapshot did not capture the probe.");
            Assert.That(HomeSnapshots(manager.ComponentManager)["A"].Values
                .Any(components => components.ContainsKey(probe.UniqueIdentifier)), Is.True);
            before = probe.TotalCalls;
            Assert.That(manager.SnapshotCurrentData(), Is.True);
            Assert.That(probe.TotalCalls, Is.GreaterThan(before), "Public snapshot did not collect the probe.");
            probe.RememberHomeScene = false;

            probe.ShareStateAcrossScenes = true;
            before = probe.TotalCalls;
            manager.ComponentManager.UnregisterSaveableComponent(probe);
            Assert.That(probe.TotalCalls - before, Is.EqualTo(1), "Real shared-state unregister did not serialize the probe.");
            Assert.That(manager.ComponentManager.Contains(probe), Is.False);
            manager.ComponentManager.RegisterSaveableComponent(probe);
            probe.ShareStateAcrossScenes = false;
            Assert.That(manager.ComponentManager.Contains(probe), Is.True);
            Assert.That(DiagnosticCycleActive(), Is.False);
        }

        private static Task StartDiagnosticSave(string entry, SaveManager manager, GameObject owner, GameObject excluded)
        {
            switch (entry)
            {
                case "Save": manager.Save(1); return null;
                case "SaveNamed": manager.Save(1, null, "Named diagnostic"); return null;
                case "SaveSync": manager.SaveSync(1); return null;
                case "SaveSyncScreenshot": manager.SaveSync(1, captureScreenshot: true); return null;
                case "SaveAsync": return manager.SaveAsync(1);
                case "SaveAsyncNamed": return manager.SaveAsync(1, null, "Named diagnostic");
                case "SaveAsyncDebug": return manager.SaveAsyncDebug(1);
                case "SaveImmediate": return manager.SaveImmediateAsync(1);
                case "SaveImmediateToken": return manager.SaveImmediateAsync(1, null, CancellationToken.None);
                case "QuickSave": manager.QuickSave(); return null;
                case "QuickSaveAsync": return manager.QuickSaveAsync();
                case "AutoSave": manager.AutoSave(); return null;
                case "AutoSaveAsync": return manager.AutoSaveAsync();
                case "ForTag": return manager.SaveSlotForTagAsync(1, "Player");
                case "ExcludingTag": return manager.SaveSlotExcludingTagAsync(1, "Respawn");
                case "ForGameObject": return manager.SaveSlotForGameObjectAsync(owner, 1);
                case "ExcludingGameObject": return manager.SaveSlotExcludingGameObjectAsync(excluded, 1);
                case "WithFilter": return manager.SaveSlotWithFilterAsync(1, go => go == owner);
                case "ByMetadata": return manager.SaveByMetadataAsync("diagnostic", "owned");
                default: throw new ArgumentOutOfRangeException(nameof(entry), entry, null);
            }
        }

        internal static bool DiagnosticCycleActive() =>
            (bool)typeof(SaveableComponent).GetProperty("IsSaveCallDiagnosticsActive",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static object DiagnosticMember(object value, string member) =>
            value.GetType().GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(value);

        private sealed class DiagnosticCaptureObserver
        {
            internal readonly List<object> Snapshots = new List<object>();
            private void Capture(object snapshot) => Snapshots.Add(snapshot);
            internal void Arm()
            {
                var method = typeof(SaveableComponent).GetMethod("ArmNextSaveCallDiagnosticCapture",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var callback = Delegate.CreateDelegate(method.GetParameters()[0].ParameterType, this,
                    GetType().GetMethod(nameof(Capture), BindingFlags.Instance | BindingFlags.NonPublic));
                method.Invoke(null, new object[] { callback });
            }
        }
    }

    internal sealed class PersistedDiagnosticProbe : SaveableComponent
    {
        internal int Value;
        internal int TotalCalls;
        internal int ActiveCycleCalls;
        protected override byte[] SerializeComponentData()
        {
            TotalCalls++;
            if (CrystalSaveLifecycleTests.DiagnosticCycleActive()) ActiveCycleCalls++;
            return BitConverter.GetBytes(Value);
        }
        protected override void DeserializeComponentData(byte[] bytes) => Value = BitConverter.ToInt32(bytes, 0);
    }
}
#endif
