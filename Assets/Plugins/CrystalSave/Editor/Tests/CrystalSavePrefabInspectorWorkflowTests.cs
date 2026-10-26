#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arawn.CrystalSave.Editor;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSavePrefabInspectorWorkflowTests
    {
        private FeatureSettingsScope settings;
        private readonly List<GameObject> objects = new List<GameObject>();
        private PrefabPersistenceInspectorTestWindow window;

        [SetUp]
        public void SetUp()
        {
            settings = new FeatureSettingsScope();
            Call(typeof(SaveablePrefabEditor), "CancelSceneIdentityScan");
        }

        [TearDown]
        public void TearDown()
        {
            if (window) window.Close();
            foreach (GameObject go in objects) if (go) Object.DestroyImmediate(go);
            objects.Clear();
            Call(typeof(SaveablePrefabEditor), "CancelSceneIdentityScan");
            Undo.ClearAll();
            settings.Dispose();
        }

        [UnityTest]
        public IEnumerator HierarchyNotificationBurstQueuesOneReadOnlyDuplicateScan()
        {
            return CheckDuplicateScan(false);
        }

        [UnityTest]
        public IEnumerator HierarchyNotificationBurstQueuesOneAutomaticDuplicateRepair()
        {
            return CheckDuplicateScan(true);
        }

        private IEnumerator CheckDuplicateScan(bool repair)
        {
            SaveablePrefab first = CreatePrefab("DuplicateA");
            SaveablePrefab second = CreatePrefab("DuplicateB");
            SaveablePrefab inactive = CreatePrefab("DuplicateInactive");
            inactive.gameObject.SetActive(false);
            string duplicateID = "CSP_SCAN_" + Guid.NewGuid().ToString("N");
            foreach (SaveablePrefab prefab in new[] { first, second, inactive })
                Set(prefab, "uniqueID", duplicateID);

            // Let object creation notifications settle, then invalidate through the real
            // hierarchy handler repeatedly before the next editor update.
            yield return null;
            yield return null;
            settings.Settings.enablePrefabRegistryAutoValidation = repair;
            CrystalSaveEditorAutomationSettings.ClearCache();
            Call(typeof(SaveablePrefabEditor), "CancelSceneIdentityScan");
            for (int i = 0; i < 100; i++)
                Call(typeof(SaveablePrefabEditor), "ValidatePrefabRegistry");

            Assert.That(QueuedSceneScans(), Is.EqualTo(1), "A hierarchy burst must share one delayed scan.");
            Assert.That(first.UniqueID, Is.EqualTo(duplicateID));
            Assert.That(second.UniqueID, Is.EqualTo(duplicateID));
            Assert.That(inactive.UniqueID, Is.EqualTo(duplicateID), "Identity changes must wait for the deferred pass.");
            double deadline = EditorApplication.timeSinceStartup + 5;
            while (!StaticValue<bool>("sceneIdentitySnapshotReady") && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(StaticValue<bool>("sceneIdentitySnapshotReady"), Is.True);
            var counts = StaticValue<Dictionary<string, int>>("sceneUniqueIdCounts");
            Assert.That(counts[duplicateID], Is.EqualTo(repair ? 1 : 3));
            Assert.That(new[] { first, second, inactive }.Count(p => p.UniqueID == duplicateID),
                Is.EqualTo(repair ? 1 : 3));
            if (repair)
                Assert.That(new[] { first, second, inactive }.Select(p => p.UniqueID).Distinct().Count(), Is.EqualTo(3),
                    "OnValidate may replace a cleared duplicate with a fresh instance ID.");

            // Settled repaint/layout reads must reuse the snapshot without queuing more work.
            yield return null;
            yield return null;
            Assert.That(QueuedSceneScans(), Is.Zero);
            Assert.That(StaticValue<bool>("sceneIdentityScanQueued"), Is.False);
            Assert.That(counts[duplicateID], Is.EqualTo(repair ? 1 : 3));
        }

        [UnityTest, Category("CrystalSaveGraphics")]
        public IEnumerator ActualPersistenceTogglesEnforceExclusionAndSupportUndo()
        {
            SaveablePrefab prefab = CreatePrefab("PersistenceToggle");
            OpenWindow(prefab);
            yield return WaitForPaint();
            Rect handoffRect = window.HandoffToggleRect;

            Click(handoffRect);
            yield return null;
            Assert.That(prefab.EnableCrossSceneStateHandoff, Is.True, "Clicking the real handoff field must change the serialized value.");
            Assert.That(prefab.KeepAcrossScenes, Is.False);
            Undo.IncrementCurrentGroup();
            Click(window.KeepToggleRect);
            yield return WaitUntil(() => prefab.GetComponent<PersistentVisibilityController>() != null,
                "Enabling Keep Across Scenes must add the visibility controller on an editor tick.");
            AssertPersistence(prefab, true, false, true, true);

            Click(handoffRect);
            yield return null;
            Assert.That(prefab.EnableCrossSceneStateHandoff, Is.False, "The disabled handoff field must ignore a click.");
            Undo.PerformUndo();
            yield return null;
            yield return null;
            AssertPersistence(prefab, false, true, false, false);
            Undo.PerformRedo();
            yield return null;
            yield return null;
            AssertPersistence(prefab, true, false, true, true);

            Undo.IncrementCurrentGroup();
            Click(window.KeepToggleRect);
            yield return WaitUntil(() => prefab.GetComponent<PersistentVisibilityController>() == null,
                "Disabling Keep Across Scenes must remove its visibility controller.");
            AssertPersistence(prefab, false, false, true, false);
            Undo.PerformUndo();
            yield return null;
            yield return null;
            AssertPersistence(prefab, true, false, true, true);
        }

        [UnityTest, Category("CrystalSaveGraphics")]
        public IEnumerator ActualPersistenceToggleUpdatesEverySelectedPrefab()
        {
            SaveablePrefab first = CreatePrefab("PersistenceSelectionA");
            SaveablePrefab second = CreatePrefab("PersistenceSelectionB");
            OpenWindow(first, second);
            yield return WaitForPaint();
            Click(window.HandoffToggleRect);
            yield return null;
            Assert.That(first.EnableCrossSceneStateHandoff && second.EnableCrossSceneStateHandoff, Is.True);
            Undo.IncrementCurrentGroup();
            Click(window.KeepToggleRect);
            yield return WaitUntil(() => first.GetComponent<PersistentVisibilityController>() != null &&
                second.GetComponent<PersistentVisibilityController>() != null,
                "Every selected root must receive the flags and delayed controller addition.");
            AssertPersistence(first, true, false, true, true);
            AssertPersistence(second, true, false, true, true);
            Undo.PerformUndo();
            yield return null;
            yield return null;
            AssertPersistence(first, false, true, false, false);
            AssertPersistence(second, false, true, false, false);
            Undo.PerformRedo();
            yield return null;
            yield return null;
            AssertPersistence(first, true, false, true, true);
            AssertPersistence(second, true, false, true, true);
            Undo.IncrementCurrentGroup();
            Click(window.KeepToggleRect);
            yield return WaitUntil(() => first.GetComponent<PersistentVisibilityController>() == null &&
                second.GetComponent<PersistentVisibilityController>() == null,
                "Disabling persistence must remove every selected root's controller.");
            AssertPersistence(first, false, false, true, false);
            AssertPersistence(second, false, false, true, false);
            Undo.PerformUndo();
            yield return null;
            yield return null;
            AssertPersistence(first, true, false, true, true);
            AssertPersistence(second, true, false, true, true);
        }

        [UnityTest, Category("CrystalSaveGraphics")]
        public IEnumerator ActualPersistenceToggleUndoRetiresDeferredControllerChanges()
        {
            SaveablePrefab prefab = CreatePrefab("PersistenceUndoBeforeCallback");
            OpenWindow(prefab);
            yield return WaitForPaint();
            Undo.IncrementCurrentGroup();
            Click(window.KeepToggleRect);
            AssertPersistence(prefab, true, false, true, false);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            yield return null;
            yield return null;
            AssertPersistence(prefab, false, false, false, false);

            Undo.IncrementCurrentGroup();
            Click(window.KeepToggleRect);
            yield return WaitUntil(() => prefab.GetComponent<PersistentVisibilityController>() != null,
                "The next accepted toggle must still create its controller.");
            Undo.IncrementCurrentGroup();
            Click(window.KeepToggleRect);
            AssertPersistence(prefab, false, false, true, true);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            yield return null;
            yield return null;
            AssertPersistence(prefab, true, false, true, true);
        }

        private SaveablePrefab CreatePrefab(string name)
        {
            var go = new GameObject(name);
            objects.Add(go);
            return go.AddComponent<SaveablePrefab>();
        }

        private void OpenWindow(params SaveablePrefab[] prefabs)
        {
            window = ScriptableObject.CreateInstance<PrefabPersistenceInspectorTestWindow>();
            window.Inspector = UnityEditor.Editor.CreateEditor(prefabs, typeof(SaveablePrefabEditor));
            window.position = new Rect(20, 20, 620, 500);
            window.Show();
            window.Focus();
        }

        private IEnumerator WaitForPaint()
        {
            yield return WaitUntil(() => window.Layouts >= 2 && window.Repaints >= 2,
                "The actual inspector controls must receive Layout and Repaint before input.");
            Assert.That(window.KeepToggleRect.height, Is.GreaterThan(0));
            Assert.That(window.HandoffToggleRect.height, Is.GreaterThan(0));
        }

        private IEnumerator WaitUntil(Func<bool> ready, string message)
        {
            double deadline = EditorApplication.timeSinceStartup + 5;
            while (!ready() && EditorApplication.timeSinceStartup < deadline)
            {
                if (window) { Assert.That(window.Failure, Is.Null); window.Repaint(); }
                yield return null;
            }
            Assert.That(ready(), Is.True, message);
        }

        private void Click(Rect field)
        {
            Vector2 point = new Vector2(field.x + PrefabPersistenceInspectorTestWindow.LabelWidth + 6, field.center.y);
            // SendEvent receives host-view coordinates, including the dock tab strip.
            Vector2 hostPoint = point + window.rootVisualElement.worldBound.position;
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = hostPoint });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = hostPoint });
            Assert.That(window.Failure, Is.Null);
            Assert.That(window.LastMousePosition, Is.EqualTo(point), "Input must reach the intended production control.");
        }

        private static void AssertPersistence(SaveablePrefab prefab, bool keep, bool handoff, bool poolingDisabled, bool controller)
        {
            Assert.That(prefab.KeepAcrossScenes, Is.EqualTo(keep), prefab.name + " keep");
            Assert.That(prefab.EnableCrossSceneStateHandoff, Is.EqualTo(handoff), prefab.name + " handoff");
            Assert.That(prefab.DisablePooling, Is.EqualTo(poolingDisabled), prefab.name + " pooling");
            Assert.That(prefab.GetComponent<PersistentVisibilityController>() != null, Is.EqualTo(controller), prefab.name + " controller");
        }

        private static T StaticValue<T>(string name) => (T)Field(typeof(SaveablePrefabEditor), name).GetValue(null);
        private static int QueuedSceneScans() => EditorApplication.delayCall == null ? 0 :
            EditorApplication.delayCall.GetInvocationList().Count(callback =>
                callback.Method.DeclaringType == typeof(SaveablePrefabEditor) && callback.Method.Name == "FlushSceneIdentityScan");
    }

    // Hosts the production field renderers. Input travels through real IMGUI events;
    // this window never assigns the tested properties or implements toggle behavior.
    internal sealed class PrefabPersistenceInspectorTestWindow : EditorWindow
    {
        internal const float LabelWidth = 220;
        internal UnityEditor.Editor Inspector;
        internal Rect KeepToggleRect;
        internal Rect HandoffToggleRect;
        internal int Layouts;
        internal int Repaints;
        internal Exception Failure;
        internal Vector2 LastMousePosition;

        private void OnGUI()
        {
            if (!Inspector) return;
            EventType type = Event.current.type;
            if (type == EventType.MouseDown || type == EventType.MouseUp)
                LastMousePosition = Event.current.mousePosition;
            if (type == EventType.Layout) Layouts++;
            if (type == EventType.Repaint) Repaints++;
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            try
            {
                EditorGUIUtility.labelWidth = LabelWidth;
                Inspector.serializedObject.Update();
                Call(Inspector, "DrawKeepAcrossScenesToggle");
                if (type == EventType.Repaint) KeepToggleRect = GUILayoutUtility.GetLastRect();
                Call(Inspector, "DrawCrossSceneStateHandoffSettings");
                if (type == EventType.Repaint && !Inspector.serializedObject.FindProperty("keepAcrossScenes").boolValue &&
                    !Inspector.serializedObject.FindProperty("enableCrossSceneStateHandoff").boolValue)
                    HandoffToggleRect = GUILayoutUtility.GetLastRect();
                Inspector.serializedObject.ApplyModifiedProperties();
            }
            catch (TargetInvocationException error) when (error.InnerException is ExitGUIException)
            {
                throw error.InnerException;
            }
            catch (ExitGUIException) { throw; }
            catch (Exception error) { Failure = error; }
            finally { EditorGUIUtility.labelWidth = previousLabelWidth; }
        }

        private void OnDisable()
        {
            if (Inspector) Object.DestroyImmediate(Inspector);
        }
    }
}
#endif
