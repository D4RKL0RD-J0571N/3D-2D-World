#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Arawn.CrystalSave.Runtime;
using Arawn.CrystalSave.Runtime.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        private const string DiagnosticSourceId = "ci-diagnostic-source";
        private const string DiagnosticTargetId = "ci-diagnostic-target";
        private const string SceneDiagnosticPrefix = "[CrystalSave][SceneBakedPrefabDiag]";

        [UnityTest]
        public IEnumerator SceneReuseDiagnosticRejectsMissingDuplicateAndMalformedTargets()
        {
            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
            yield return new RecompileScripts(expectScriptCompilation: false);
            AuthorSceneReuseDiagnosticAssets(true);
            yield return new RecompileScripts(expectScriptCompilation: false);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);
            yield return Until(() => SaveManager.IsInitialized && SaveManager.AreSaveSlotsReady, "scene-diagnostic initialization");
            var manager = SaveManager.Instance;
            var target = manager.GetPrefabManager.GetSaveablePrefabs().Single(p => p && p.UniqueID == DiagnosticTargetId);
            Assert.That(target.IsAddedAtRuntime, Is.False);
            var driver = CreateSceneReuseDriver(target.PrefabAssetID, false);
            Set(driver, "unchangedTargetInstanceID", "unclaimed-design-id");
            var expected = new SaveablePrefabData(target.UniqueID, target.PrefabAssetID, "ExpectedDiagnosticTarget",
                new Vector3(13, 17, 23), Quaternion.Euler(15, 35, 55), new Vector3(1.5f, 2, 3), null, false, null, "B");
            Scene home = target.gameObject.scene;
            Scene other = SceneManager.CreateScene("DiagnosticWrongScene");
            GameObject duplicate = null;
            try
            {
                foreach (string fault in new[] { "valid", "missing", "null", "duplicate", "id", "unchanged-id",
                    "name", "position", "rotation", "scale", "scene", "runtime", "legacy-empty-name" })
                {
                    Set(target, "uniqueID", expected.InstanceID);
                    Set(target, "isAddedAtRuntime", false);
                    Set(driver, "unchangedTargetInstanceID", "unclaimed-design-id");
                    SceneManager.MoveGameObjectToScene(target.gameObject, home);
                    target.name = "ExpectedDiagnosticTarget";
                    target.transform.SetPositionAndRotation(expected.Position, expected.Rotation);
                    target.transform.localScale = expected.Scale;
                    expected.GameObjectName = target.name;
                    var matches = new List<SaveablePrefab> { target };
                    switch (fault)
                    {
                        case "missing": matches.Clear(); break;
                        case "null": matches[0] = null; break;
                        case "duplicate":
                            duplicate = Object.Instantiate(target.gameObject);
                            matches.Add(duplicate.GetComponent<SaveablePrefab>());
                            break;
                        case "id": Set(target, "uniqueID", "wrong-saved-id"); break;
                        case "unchanged-id": Set(driver, "unchangedTargetInstanceID", target.UniqueID); break;
                        case "name": target.name = "WrongDiagnosticName"; break;
                        case "position": target.transform.position += Vector3.right; break;
                        case "rotation": target.transform.rotation = Quaternion.Euler(70, 80, 90); break;
                        case "scale": target.transform.localScale = Vector3.one; break;
                        case "scene": SceneManager.MoveGameObjectToScene(target.gameObject, other); break;
                        case "runtime": target.MarkAsAddedAtRuntime(); break;
                        case "legacy-empty-name": expected.GameObjectName = string.Empty; break;
                    }
                    bool shouldPass = fault == "valid" || fault == "legacy-empty-name";
                    LogType kind = shouldPass ? LogType.Log : fault == "duplicate" ? LogType.Warning : LogType.Error;
                    string outcome = shouldPass ? " PASS:" : fault == "duplicate" ? " DUPLICATE DETECTED:" : " FAIL:";
                    var observed = new List<string>();
                    Application.LogCallback capture = (message, stack, logType) =>
                    {
                        if (message.StartsWith(SceneDiagnosticPrefix, StringComparison.Ordinal)) observed.Add(message);
                    };
                    LogAssert.Expect(kind, new Regex("^" + Regex.Escape(SceneDiagnosticPrefix + outcome)));
                    Application.logMessageReceived += capture;
                    try
                    {
                        typeof(SceneBakedPrefabReuseDiagnostic).GetMethod("EvaluateRestoreResult", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(driver, new object[] { matches, expected });
                    }
                    finally { Application.logMessageReceived -= capture; }
                    Assert.That(observed, Has.Count.EqualTo(1), fault);
                    Assert.That(observed[0].Contains(" PASS:"), Is.EqualTo(shouldPass), fault);
                    if (duplicate != null) { Object.DestroyImmediate(duplicate); duplicate = null; }
                }
            }
            finally
            {
                if (duplicate != null) Object.DestroyImmediate(duplicate);
                Set(target, "uniqueID", expected.InstanceID);
                Set(target, "isAddedAtRuntime", false);
                SceneManager.MoveGameObjectToScene(target.gameObject, home);
                Object.Destroy(driver.gameObject);
            }
            yield return SceneManager.UnloadSceneAsync(other);
        }

        [UnityTest]
        public IEnumerator SceneReuseDiagnosticImmediateFlowRemovesOnlySyntheticSourceTombstone()
        {
            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
            yield return new RecompileScripts(expectScriptCompilation: false);
            AuthorSceneReuseDiagnosticAssets(false);
            yield return new RecompileScripts(expectScriptCompilation: false);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);
            yield return Until(() => SaveManager.IsInitialized && SaveManager.AreSaveSlotsReady, "scene-diagnostic initialization");
            yield return SceneReuseDiagnosticManualFlow(false);
        }

        [UnityTest]
        public IEnumerator SceneReuseDiagnosticDeferredFlowRemovesOnlySyntheticSourceTombstone()
        {
            yield return new ExitPlayMode();
            Assert.That(Application.isPlaying, Is.False);
            yield return new RecompileScripts(expectScriptCompilation: false);
            AuthorSceneReuseDiagnosticAssets(false);
            yield return new RecompileScripts(expectScriptCompilation: false);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);
            yield return Until(() => SaveManager.IsInitialized && SaveManager.AreSaveSlotsReady, "scene-diagnostic initialization");
            yield return SceneReuseDiagnosticManualFlow(true);
        }

        private IEnumerator SceneReuseDiagnosticManualFlow(bool deferred)
        {
            var manager = SaveManager.Instance;
            var source = manager.GetPrefabManager.GetSaveablePrefabs().Single(p => p && p.UniqueID == DiagnosticSourceId);
            Assert.That(source.IsAddedAtRuntime, Is.False);
            source.name = "SavedDiagnosticActor";
            source.transform.SetPositionAndRotation(new Vector3(17, 23, 31), Quaternion.Euler(15, 35, 55));
            source.transform.localScale = new Vector3(1.5f, 2, 3);
            source.ReuseSceneInstanceOnLoad = true;
            var data = manager.GetPrefabManager.BuildPrefabData(source);
            Assert.That(data, Is.Not.Null);
            data.HomeScene = null;
            data.DeferLowPriorityUntilRequested = deferred;
            data.ReuseSceneInstanceOnLoad = true;
            data.RuntimeModificationData = null;
            data.HasTransformOverride = true;
            data.UsesOptimizationFlags = false;
            const string unrelatedId = "ci-unrelated-tombstone";
            manager.GameObjectTracker.RegisterDestroyedGameObject(DiagnosticSourceId);
            manager.GameObjectTracker.RegisterDestroyedGameObject(unrelatedId);
            var before = manager.GameObjectTracker.GetDestroyedGameObjectIDs();
            Assert.That(before, Does.Contain(DiagnosticSourceId).And.Contain(unrelatedId));
            var driver = CreateSceneReuseDriver(source.PrefabAssetID, deferred);
            Object.DontDestroyOnLoad(driver.gameObject);
            SaveablePrefab originalTarget = null;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> loaded = (scene, mode) =>
            {
                if (scene.name == "B") originalTarget = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SaveablePrefab>(true))
                    .Single(p => p.UniqueID == DiagnosticTargetId);
            };
            int removalMessages = 0;
            int passes = 0;
            Application.LogCallback capture = (message, stack, kind) =>
            {
                if (message.StartsWith(SceneDiagnosticPrefix + " Removed synthetic source InstanceID '" + DiagnosticSourceId + "'", StringComparison.Ordinal)) removalMessages++;
                if (message.StartsWith(SceneDiagnosticPrefix + " PASS:", StringComparison.Ordinal)) passes++;
            };
            SceneManager.sceneLoaded += loaded;
            Application.logMessageReceived += capture;
            try
            {
                var flow = (IEnumerator)typeof(SceneBakedPrefabReuseDiagnostic).GetMethod("RunManualPrefabManagerFlow", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(driver, new object[] { manager, data });
                bool finished = false;
                driver.StartCoroutine(ObserveSceneDiagnosticCompletion(flow, () => finished = true));
                yield return Until(() => finished, "actual scene-reuse diagnostic", 30);
                Assert.That(originalTarget, Is.Not.Null);
                Assert.That(source == null, Is.True, "The actual scene transition must unload the source.");
                Assert.That(removalMessages, Is.EqualTo(1));
                Assert.That(passes, Is.EqualTo(1));
                Assert.That(originalTarget.IsAddedAtRuntime, Is.False);
                Assert.That(originalTarget.UniqueID, Is.EqualTo(DiagnosticSourceId));
                Assert.That(originalTarget.name, Is.EqualTo(data.GameObjectName));
                Assert.That(Vector3.Distance(originalTarget.transform.position, data.Position), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(originalTarget.transform.rotation, data.Rotation), Is.LessThan(.01f));
                Assert.That(Vector3.Distance(originalTarget.transform.localScale, data.Scale), Is.LessThan(.001f));
                Assert.That(manager.GetPrefabManager.GetSaveablePrefabs().Count(p => p && p.PrefabAssetID == data.PrefabID), Is.EqualTo(1));
                Assert.That(manager.GameObjectTracker.GetDestroyedGameObjectIDs(), Does.Contain(DiagnosticSourceId).And.Contain(unrelatedId),
                    "The diagnostic must filter its detached restore list without editing the tracker's history.");
                Assert.That(before, Does.Contain(DiagnosticSourceId).And.Contain(unrelatedId), "Previously returned snapshots remain independent.");
            }
            finally
            {
                SceneManager.sceneLoaded -= loaded;
                Application.logMessageReceived -= capture;
                if (driver != null) Object.Destroy(driver.gameObject);
            }
        }

        private static IEnumerator ObserveSceneDiagnosticCompletion(IEnumerator flow, Action complete)
        {
            yield return flow;
            complete();
        }

        private static SceneBakedPrefabReuseDiagnostic CreateSceneReuseDriver(string assetId, bool deferred)
        {
            var root = new GameObject("CI scene-reuse diagnostic");
            root.SetActive(false);
            var driver = root.AddComponent<SceneBakedPrefabReuseDiagnostic>();
            Set(driver, "runOnStart", false);
            Set(driver, "diagnosticPrefabAssetID", assetId);
            Set(driver, "targetSceneName", "B");
            Set(driver, "unchangedTargetInstanceID", DiagnosticTargetId);
            Set(driver, "processAsDeferredPass", deferred);
            root.SetActive(true);
            return driver;
        }

        private static void AuthorSceneReuseDiagnosticAssets(bool startInTarget)
        {
            Assert.That(Application.isPlaying, Is.False, "Author scene fixtures only after the real Edit Mode transition.");
            var settings = AssetDatabase.LoadAssetAtPath<SaveSettings>(AssetsRoot + "/Settings.asset");
            settings.usePrefabPooling = false;
            settings.enableAssetBasedReuseDuringDeferred = false;
            EditorUtility.SetDirty(settings);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "/Resources/CrystalSaveLifecyclePrefab.prefab");
            var a = EditorSceneManager.OpenScene(AssetsRoot + "/A.unity", OpenSceneMode.Single);
            AddLegacyScenePrefab(asset, a, "DiagnosticSource", DiagnosticSourceId, false);
            EditorSceneManager.SaveScene(a);
            var b = EditorSceneManager.OpenScene(AssetsRoot + "/B.unity", OpenSceneMode.Single);
            AddLegacyScenePrefab(asset, b, "DiagnosticTarget", DiagnosticTargetId, false);
            var target = b.GetRootGameObjects().Single().GetComponent<SaveablePrefab>();
            target.ReuseSceneInstanceOnLoad = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorSceneManager.SaveScene(b);
            EditorSceneManager.OpenScene(AssetsRoot + (startInTarget ? "/B.unity" : "/A.unity"), OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
