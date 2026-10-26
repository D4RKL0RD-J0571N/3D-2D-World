#if UNITY_EDITOR && !UNITY_6000_6_OR_NEWER && MEMORYPACK && ARAWN_REMEMBERME
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
#if UNITY_6000_4_OR_NEWER
using RowID = UnityEngine.EntityId;
#else
using RowID = System.Int32;
#endif

namespace Arawn.CrystalSave.EditorTests
{
    // These cases run in the required older-editor graphics matrix. The current
    // UI Toolkit hierarchy has its own fixture and cannot qualify these branches.
    public sealed class CrystalSaveLegacyHierarchyTests
    {
        private const string RegistryDirectory = "Assets/Plugins/CrystalSave/Resources";
        private const string RegistryPath = RegistryDirectory + "/SceneObjectRegistry.asset";
        private const string AssetDirectory = "Assets/__CrystalSaveLegacyHierarchyTests";
        private static readonly Type Linked = typeof(LinkedRememberComponentHierarchyIcon);
        private static readonly Type[] Drawers = { typeof(SaveablePrefabHierarchyIcon),
            typeof(RememberCompositeHierarchyIcon), Linked };
        private FeatureSettingsScope settings;
        private GameObject root;
        private GameObject empty;
        private GameObject linkedObject;
        private Object[] selection;
        private SceneObjectRegistry registry;
        private LegacyHierarchyTestWindow host;
        private EditorWindow hierarchy;
        private bool ownsRegistryDirectory;
        private bool ownsRegistry;
        private bool ownsAssets;

        [SetUp]
        public void SetUp()
        {
            selection = Selection.objects;
            settings = new FeatureSettingsScope();
            SetVisibility(false);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(RegistryPath), Is.Null,
                "The isolated runner must not copy an installed scene registry into this fixture.");
            Assert.That(AssetDatabase.IsValidFolder(AssetDirectory), Is.False);
            AssetDatabase.CreateFolder("Assets", "__CrystalSaveLegacyHierarchyTests");
            ownsAssets = true;
            if (!AssetDatabase.IsValidFolder(RegistryDirectory))
            {
                AssetDatabase.CreateFolder("Assets/Plugins/CrystalSave", "Resources");
                ownsRegistryDirectory = true;
            }
            root = new GameObject("CrystalSaveLegacyHierarchyProbe");
            root.AddComponent<SaveablePrefab>();
            linkedObject = new GameObject("CrystalSaveLegacyLinkedProbe");
            Composite(linkedObject, typeof(RememberGameObject));
            string id = linkedObject.GetComponent<UniqueID>().ID;
            empty = new GameObject("CrystalSaveLegacyHierarchyEmpty");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(empty, AssetDirectory + "/Linked.prefab");
            registry = ScriptableObject.CreateInstance<SceneObjectRegistry>();
            registry.Entries.Add(new SceneObjectRegistry.SceneObjectEntry { UniqueID = id, PrefabAsset = prefab });
            AssetDatabase.CreateAsset(registry, RegistryPath);
            ownsRegistry = true;
            AssetDatabase.SaveAssets();
            Call(Linked, "ClearCache");
        }

        [TearDown]
        public void TearDown()
        {
            if (host) host.Close();
            if (hierarchy) hierarchy.Close();
            Selection.objects = selection;
            if (root) Object.DestroyImmediate(root);
            if (empty) Object.DestroyImmediate(empty);
            if (linkedObject) Object.DestroyImmediate(linkedObject);
            if (ownsRegistry) AssetDatabase.DeleteAsset(RegistryPath);
            if (ownsRegistryDirectory) AssetDatabase.DeleteAsset(RegistryDirectory);
            if (ownsAssets) AssetDatabase.DeleteAsset(AssetDirectory);
            Call(Linked, "ClearCache");
            Call(typeof(SaveablePrefabEditor), "CancelSceneIdentityScan");
            if (settings != null) settings.Dispose();
        }

        [UnityTest, Category("CrystalSaveLegacyGraphics")]
        public IEnumerator ActualLegacyHierarchyRepaintsWithAllThreeRegisteredDrawers()
        {
            SetVisibility(true);
            Delegate callbacks =
#if UNITY_6000_4_OR_NEWER
                EditorApplication.hierarchyWindowItemByEntityIdOnGUI;
#else
                EditorApplication.hierarchyWindowItemOnGUI;
#endif
            foreach (Type type in Drawers)
                Assert.That(callbacks.GetInvocationList().Count(item => item.Method.DeclaringType == type),
                    Is.EqualTo(1), "Exactly one live hierarchy subscription is required for " + type.Name);
            int repaints = 0;
            RowID id = ID(root);
#if UNITY_6000_4_OR_NEWER
            EditorApplication.HierarchyWindowItemByEntityIdCallback observer = (row, rect) =>
#else
            EditorApplication.HierarchyWindowItemCallback observer = (row, rect) =>
#endif
            {
                if (row.Equals(id) && Event.current.type == EventType.Repaint) repaints++;
            };
#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += observer;
#else
            EditorApplication.hierarchyWindowItemOnGUI += observer;
#endif
            try
            {
                Type windowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow", true);
                hierarchy = (EditorWindow)ScriptableObject.CreateInstance(windowType);
                hierarchy.position = new Rect(20, 30, 450, 400);
                hierarchy.Show();
                Selection.activeGameObject = root;
                yield return Until(() => repaints > 1, () => hierarchy.Repaint());
                Assert.That(CacheLoaded, Is.True);
                Assert.That(LinkedIDs, Does.Contain(linkedObject.GetComponent<UniqueID>().ID));
                Debug.Log("Legacy hierarchy qualified on " + Application.unityVersion + ": three registered drawers and " + repaints + " object repaints.");
            }
            finally
            {
#if UNITY_6000_4_OR_NEWER
                EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= observer;
#else
                EditorApplication.hierarchyWindowItemOnGUI -= observer;
#endif
            }
        }

        [UnityTest, Category("CrystalSaveLegacyGraphics")]
        public IEnumerator LegacyDrawersRenderMatchingRowsAndRespectVisibility()
        {
            OpenHost();
            SetVisibility(false);
            yield return RepaintHost();
            Color[][] disabled = CaptureRows();
            SetVisibility(true);
            yield return RepaintHost();
            Color[][] enabled = CaptureRows();
            for (int i = 0; i < Drawers.Length; i++)
                Assert.That(DifferentPixels(disabled[i], enabled[i]), Is.GreaterThan(12),
                    Drawers[i].Name + " must draw visible texture pixels for its matching object.");
            host.RowIDs = new[] { ID(empty), ID(empty), ID(empty) };
            yield return RepaintHost();
            Color[][] unmatched = CaptureRows();
            for (int i = 0; i < Drawers.Length; i++)
                Assert.That(DifferentPixels(disabled[i], unmatched[i]), Is.Zero,
                    Drawers[i].Name + " must leave a nonmatching row unchanged.");
            host.RowIDs = new[] { ID(root), ID(linkedObject), ID(linkedObject) };
            SetVisibility(false);
            yield return RepaintHost();
            Color[][] disabledAgain = CaptureRows();
            for (int i = 0; i < Drawers.Length; i++)
                Assert.That(DifferentPixels(disabled[i], disabledAgain[i]), Is.Zero);
        }

        [UnityTest, Category("CrystalSaveLegacyGraphics")]
        public IEnumerator LegacyRowsSkipColdCacheDuringNonRepaintAndRepeatedDisabledEvents()
        {
            OpenHost();
            SetVisibility(false);
            yield return RepaintHost();
            host.CheckOnRepaint = () =>
            {
                SetVisibility(true);
                Call(Linked, "ClearCache");
                EventType previous = Event.current.type;
                try
                {
                    foreach (EventType type in new[] { EventType.Layout, EventType.MouseMove, EventType.MouseDown, EventType.KeyDown })
                    {
                        Event.current.type = type;
                        host.DrawRows();
                        Assert.That(CacheLoaded, Is.False, type + " must not initialize the registry.");
                    }
                }
                finally { Event.current.type = previous; }
                host.DrawRows();
                Assert.That(CacheLoaded, Is.True, "Repaint must initialize the enabled registry.");
                Assert.That(LinkedIDs, Does.Contain(linkedObject.GetComponent<UniqueID>().ID));
                SetVisibility(false);
                host.DrawRows();
                Assert.That(CacheLoaded, Is.False);
                Assert.That(LinkedIDs, Is.Empty);
                // A sentinel in a cold cache makes repeated ClearCache work observable.
                LinkedIDs.Add("disabled-cache-sentinel");
                HashSet<string> cache = LinkedIDs;
                for (int i = 0; i < 1024; i++) host.DrawRows();
                Assert.That(LinkedIDs, Is.SameAs(cache));
                Assert.That(LinkedIDs, Does.Contain("disabled-cache-sentinel"));
                Assert.That(CacheLoaded, Is.False);
            };
            yield return RepaintHost();
            Assert.That(host.CheckOnRepaint, Is.Null, "The real GUI callback did not execute the assertions.");
        }

        [UnityTest, Category("CrystalSaveLegacyGraphics")]
        public IEnumerator WarmLegacyRowsReuseTheirCacheWithoutManagedAllocations()
        {
            OpenHost();
            SetVisibility(true);
            yield return RepaintHost();
            host.CheckOnRepaint = () =>
            {
                HashSet<string> cache = LinkedIDs;
                foreach (bool enabled in new[] { true, false })
                {
                    SetVisibility(enabled);
                    for (int i = 0; i < 32; i++) host.DrawRows();
                    for (int drawer = 0; drawer < Drawers.Length; drawer++)
                    {
                        Action<RowID, Rect> callback = host.Drawers[drawer];
                        Rect rect = host.Rows[drawer];
                        long allocated = CrystalSaveAllocationMeasurement.Count(() =>
                        {
                            for (int i = 0; i < 1024; i++) callback(host.RowIDs[drawer], rect);
                        });
                        Assert.That(allocated, Is.Zero, Drawers[drawer].Name + " enabled=" + enabled + " allocated across 1,024 warmed rows.");
                    }
                    Assert.That(LinkedIDs, Is.SameAs(cache), "Row drawing must reuse the registry cache.");
                }
            };
            yield return RepaintHost();
            Assert.That(host.CheckOnRepaint, Is.Null);
        }

        [UnityTest, Category("CrystalSaveLegacyGraphics")]
        public IEnumerator ActualProjectChangeRebuildsLinkedRegistryCache()
        {
            OpenHost();
            SetVisibility(true);
            yield return RepaintHost();
            string previous = linkedObject.GetComponent<UniqueID>().ID;
            Assert.That(LinkedIDs, Does.Contain(previous));
            registry.Entries[0].UniqueID = "replacement-linked-id";
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RegistryPath, ImportAssetOptions.ForceUpdate);
            // Unity's projectChanged notification covers structural asset changes;
            // saving an existing asset's contents alone does not promise that event.
            Assert.That(AssetDatabase.RenameAsset(AssetDirectory + "/Linked.prefab", "LinkedRenamed"), Is.Empty);
            yield return Until(() => LinkedIDs.Contains("replacement-linked-id"), () => host.Repaint());
            Assert.That(LinkedIDs, Does.Not.Contain(previous));
            Assert.That(CacheLoaded, Is.True);
        }

        private void OpenHost()
        {
            host = ScriptableObject.CreateInstance<LegacyHierarchyTestWindow>();
            host.RowIDs = new[] { ID(root), ID(linkedObject), ID(linkedObject) };
#if UNITY_6000_4_OR_NEWER
            const string method = "OnHierarchyGUIByEntityId";
#else
            const string method = "OnHierarchyGUI";
#endif
            host.Drawers = Drawers.Select(type => (Action<RowID, Rect>)Delegate.CreateDelegate(
                typeof(Action<RowID, Rect>), type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static))).ToArray();
            host.position = new Rect(30, 40, 430, 190);
            host.ShowUtility();
            host.Focus();
        }

        private IEnumerator RepaintHost()
        {
            int before = host.Repaints;
            yield return Until(() => host.Failure != null || host.Repaints >= before + 2, () => host.Repaint());
            if (host.Failure != null) throw new AssertionException(host.Failure.ToString());
        }

        private static IEnumerator Until(Func<bool> condition, Action tick)
        {
            double deadline = EditorApplication.timeSinceStartup + 10;
            while (!condition() && EditorApplication.timeSinceStartup < deadline) { tick(); yield return null; }
            Assert.That(condition(), Is.True, "Timed out waiting for the editor's actual GUI/project callback.");
        }

        private Color[][] CaptureRows()
        {
            var rows = new Color[3][];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(host.ScreenRows[i], 100, 20);
            return rows;
        }

        private static int DifferentPixels(Color[] first, Color[] second)
        {
            Assert.That(second.Length, Is.EqualTo(first.Length));
            int count = 0;
            for (int i = 0; i < first.Length; i++)
                if (Mathf.Abs(first[i].r - second[i].r) + Mathf.Abs(first[i].g - second[i].g) +
                    Mathf.Abs(first[i].b - second[i].b) > 0.03f) count++;
            return count;
        }

        private void SetVisibility(bool value)
        {
            settings.Settings.showEditorHierarchyIcons = value;
            CrystalSaveEditorAutomationSettings.ClearCache();
        }

        private static bool CacheLoaded => (bool)Field(Linked, "s_CacheLoaded").GetValue(null);
        private static HashSet<string> LinkedIDs => (HashSet<string>)Field(Linked, "s_LinkedIDs").GetValue(null);
        private static RowID ID(GameObject go)
        {
#if UNITY_6000_4_OR_NEWER
            return go.GetEntityId();
#else
#pragma warning disable CS0618
            return go.GetInstanceID();
#pragma warning restore CS0618
#endif
        }
    }

    internal sealed class LegacyHierarchyTestWindow : EditorWindow
    {
        internal RowID[] RowIDs;
        internal Action<RowID, Rect>[] Drawers;
        internal readonly Rect[] Rows = { new Rect(10, 20, 380, 20), new Rect(10, 65, 380, 20), new Rect(10, 110, 380, 20) };
        internal readonly Vector2[] ScreenRows = new Vector2[3];
        internal int Repaints;
        internal Action CheckOnRepaint;
        internal Exception Failure;

        internal void DrawRows()
        {
            for (int i = 0; i < Drawers.Length; i++) Drawers[i](RowIDs[i], Rows[i]);
        }

        private void OnGUI()
        {
            if (Drawers == null || Failure != null) return;
            try
            {
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.19f, 0.23f, 0.27f));
                    for (int i = 0; i < Rows.Length; i++)
                        ScreenRows[i] = GUIUtility.GUIToScreenPoint(new Vector2(Rows[i].xMax - 100, Rows[i].y));
                    Action check = CheckOnRepaint;
                    CheckOnRepaint = null;
                    if (check != null) check();
                    Repaints++;
                }
                DrawRows();
            }
            catch (ExitGUIException) { throw; }
            catch (Exception error) { Failure = error; Repaints++; }
        }
    }
}
#endif
