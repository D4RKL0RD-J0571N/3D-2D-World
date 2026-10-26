#if UNITY_EDITOR && UNITY_6000_6_OR_NEWER && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Arawn.CrystalSave.Editor;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveHierarchyCacheTests
    {
        [UnityTest, Category("CrystalSaveGraphics")]
        public IEnumerator DisabledHierarchyDoesNotInitializeTexturesOrRegistryAndReenableStillWorks()
        {
            using (var settings = new FeatureSettingsScope())
            {
                var root = new GameObject("CrystalSaveDisabledHierarchyCacheProbe");
                root.AddComponent<SaveablePrefab>();
                var window = ScriptableObject.CreateInstance<Unity.Hierarchy.Editor.HierarchyWindow>();
                var textures = Static<Dictionary<string, Texture2D>>("Textures");
                var savedTextures = new Dictionary<string, Texture2D>(textures);
                var entries = settings.Overrides.sceneObjectRegistry.Entries;
                try
                {
                    settings.Settings.showEditorHierarchyIcons = false;
                    CrystalSaveEditorAutomationSettings.ClearCache();
                    textures.Clear();
                    // Enumerating this sentinel would fail: a disabled refresh must
                    // not initialize the scene-registry cache, even after invalidation.
                    settings.Overrides.sceneObjectRegistry.Entries = null;
                    Call(typeof(CrystalSaveHierarchyIcons), "Invalidate");
                    window.Show();
                    window.SetSearchText(root.name);
                    double deadline = EditorApplication.timeSinceStartup + 10;
                    var rows = Static<Dictionary<VisualElement, EntityId>>("Rows");
                    while (!rows.ContainsValue(root.GetEntityId()) && EditorApplication.timeSinceStartup < deadline)
                    { window.Repaint(); yield return null; }
                    Assert.That(rows.ContainsValue(root.GetEntityId()), Is.True, "The actual hierarchy must bind the disabled row.");
                    for (int i = 0; i < 10; i++)
                    {
                        Call(typeof(CrystalSaveHierarchyIcons), "Invalidate");
                        window.Repaint();
                        yield return null;
                        Assert.That(textures, Is.Empty, "Disabled binding must not load icon textures.");
                        Assert.That(Static<HashSet<string>>("LinkedIDs"), Is.Empty);
                        Assert.That(window.rootVisualElement.Q(CrystalSaveHierarchyIcons.ContainerName), Is.Null);
                    }
                    settings.Overrides.sceneObjectRegistry.Entries = entries;
                    settings.Settings.showEditorHierarchyIcons = true;
                    CrystalSaveEditorAutomationSettings.ClearCache();
                    deadline = EditorApplication.timeSinceStartup + 5;
                    while (window.rootVisualElement.Q(CrystalSaveHierarchyIcons.ContainerName) == null &&
                        EditorApplication.timeSinceStartup < deadline) { window.Repaint(); yield return null; }
                    Assert.That(window.rootVisualElement.Q<Image>("SaveablePrefab"), Is.Not.Null,
                        "Disabled cache avoidance must preserve later enabling of the same bound row.");
                }
                finally
                {
                    settings.Overrides.sceneObjectRegistry.Entries = entries;
                    window.Close();
                    Object.DestroyImmediate(root);
                    textures.Clear();
                    foreach (var pair in savedTextures) textures.Add(pair.Key, pair.Value);
                    Call(typeof(CrystalSaveHierarchyIcons), "Invalidate");
                }
            }
        }

        [Test]
        public void RepeatedDisabledRowsAndIdleRefreshDoNotAllocateOrRebuildCaches()
        {
            using (var settings = new FeatureSettingsScope())
            {
                settings.Settings.showEditorHierarchyIcons = false;
                CrystalSaveEditorAutomationSettings.ClearCache();
                Type type = typeof(CrystalSaveHierarchyIcons);
                var refresh = (Action)Delegate.CreateDelegate(typeof(Action), type.GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Static));
                var container = new VisualElement();
                var foreign = new Label("unrelated extension");
                container.Add(foreign);
                var linked = Static<HashSet<string>>("LinkedIDs");
                Call(type, "Invalidate");
                refresh();
                linked.Add("cold-disabled-cache-sentinel");
                try
                {
                    for (int i = 0; i < 32; i++)
                    { refresh(); CrystalSaveHierarchyIcons.UpdateIcons(container, null, false, linked); }
                    long allocated = CrystalSaveAllocationMeasurement.Count(() =>
                    {
                        for (int i = 0; i < 1024; i++)
                        { refresh(); CrystalSaveHierarchyIcons.UpdateIcons(container, null, false, linked); }
                    });
                    Assert.That(allocated, Is.Zero, "Warmed disabled rows and unchanged refreshes must not allocate.");
                    Assert.That(linked, Does.Contain("cold-disabled-cache-sentinel"), "Idle refresh must not rebuild the registry set.");
                    Assert.That(container.childCount, Is.EqualTo(1));
                    Assert.That(container[0], Is.SameAs(foreign));
                }
                finally { linked.Clear(); Call(type, "Invalidate"); }
            }
        }

        private static T Static<T>(string name) => (T)Field(typeof(CrystalSaveHierarchyIcons), name).GetValue(null);
    }
}
#endif
