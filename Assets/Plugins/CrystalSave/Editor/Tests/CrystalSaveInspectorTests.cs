#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using Arawn.CrystalSave.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_6000_6_OR_NEWER
using UnityEngine.UIElements;
#endif
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveInspectorTests
    {
        private GameObject root;
        private FeatureSettingsScope settings;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Optional SDK detection may queue a define-driven reload. Inspector cleanup
            // deliberately ignores compilation teardown, so create objects only after it drains.
            yield return new RecompileScripts(expectScriptCompilation: false);
            settings = new FeatureSettingsScope();
            root = new GameObject("InspectorTest");
            root.SetActive(true);
        }
        [TearDown] public void TearDown() { if(root) Object.DestroyImmediate(root); settings?.Dispose(); }

        [Test]
        public void CompositeCreatesOneHiddenIdAndReusesSelectedHelperAndIdentity()
        {
            var composite = Composite(root,typeof(RememberTransform));
            var helper = root.GetComponent<RememberTransform>(); var uid = root.GetComponent<UniqueID>();
            Assert.That(helper, Is.Not.Null); Assert.That(uid, Is.Not.Null); Assert.That(uid.ID, Is.Not.Empty);
            string componentId = helper.ComponentID; string id = uid.ID;
            composite.RefreshComponents();
            Assert.That(root.GetComponents<UniqueID>(), Has.Length.EqualTo(1));
            Assert.That(root.GetComponents<RememberTransform>(), Has.Length.EqualTo(1));
            Assert.That(composite.GetInnerComponent(typeof(RememberTransform).AssemblyQualifiedName), Is.SameAs(helper));
            Assert.That(helper.ComponentID, Is.EqualTo(componentId)); Assert.That(uid.ID, Is.EqualTo(id));
            Assert.That((uid.hideFlags & HideFlags.HideInInspector) != 0, Is.True);
            Assert.That((helper.hideFlags & HideFlags.HideInInspector) != 0, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void PrefabRootAndChildCompositesDoNotAddUniqueId(bool child)
        {
            root.AddComponent<SaveablePrefab>();
            GameObject owner=root;
            if(child) { owner=new GameObject("Child"); owner.transform.SetParent(root.transform); }
            Composite(owner,typeof(RememberTransform));
            Assert.That(owner.GetComponent<UniqueID>(), Is.Null);
            Assert.That(Call(typeof(RememberCompositeEditor),"NeedsUidFix",owner), Is.False);
        }

        [Test]
        public void ValidSceneCompositeDoesNotShowMissingIdentityRepair()
        {
            Composite(root,typeof(RememberTransform));
            Assert.That(Call(typeof(RememberCompositeEditor),"NeedsUidFix",root), Is.False);
        }

        [Test]
        public void InspectorIdentityRepairAndRemovalSupportUndo()
        {
            Call(typeof(RememberCompositeEditor),"AddUniqueId",root);
            Assert.That(root.GetComponent<UniqueID>(), Is.Not.Null);
            var parent = new GameObject("PrefabParent"); parent.SetActive(false); parent.AddComponent<SaveablePrefab>();
            try
            {
                root.transform.SetParent(parent.transform);
                Assert.That(Call(typeof(RememberCompositeEditor),"NeedsUidRemoval",root), Is.True);
                Undo.IncrementCurrentGroup();
                Call(typeof(RememberCompositeEditor),"RemoveUniqueId",root);
                Assert.That(root.GetComponent<UniqueID>(), Is.Null);
                Undo.PerformUndo();
                Assert.That(root.GetComponent<UniqueID>(), Is.Not.Null);
            }
            finally { root.transform.SetParent(null); Object.DestroyImmediate(parent); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ComponentInspectorDesignatesExactlyOnePersistenceOwner(bool firstEnabled)
        {
            Composite(root,typeof(RememberTransform),typeof(RememberGameObject));
            var first=root.GetComponent<RememberTransform>(); var second=root.GetComponent<RememberGameObject>();
            first.enabled=firstEnabled; second.enabled=true;
            var editor=UnityEditor.Editor.CreateEditor(first,typeof(SaveableComponentEditor));
            try
            {
                Call(editor,"DetermineEditableComponents");
                var map=Get<Dictionary<GameObject,SaveableComponent>>(editor,"designatedComponentsMap");
                Assert.That(map,Has.Count.EqualTo(1));
                Assert.That(map[root],Is.SameAs(firstEnabled ? (SaveableComponent)first : second));
                first.enabled=false; second.enabled=false;
                Call(editor,"DetermineEditableComponents");
                Assert.That(map[root],Is.SameAs(first),"Disabled objects keep a deterministic inspector owner.");
            }
            finally { Object.DestroyImmediate(editor); }
        }

        [UnityTest]
        public IEnumerator RemovingCompositeCleansUpOwnedHelperAndIdentityOnEditorTick()
        {
            var composite=Composite(root,typeof(RememberTransform));
            Object.DestroyImmediate(composite);
            double deadline=EditorApplication.timeSinceStartup+3;
            while(root.GetComponent<RememberTransform>() && EditorApplication.timeSinceStartup<deadline) yield return null;
            Assert.That(root.GetComponent<RememberTransform>(), Is.Null);
            Assert.That(root.GetComponent<UniqueID>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator RemovingTypeFromCompositeRemovesHelperButKeepsSceneIdentity()
        {
            var composite=Composite(root,typeof(RememberTransform),typeof(RememberGameObject));
            var so=new SerializedObject(composite); var types=so.FindProperty("_rememberTypes");
            types.arraySize=1; types.GetArrayElementAtIndex(0).stringValue=typeof(RememberGameObject).AssemblyQualifiedName;
            so.ApplyModifiedPropertiesWithoutUndo(); composite.RefreshComponents(); double deadline=EditorApplication.timeSinceStartup+3;
            while(root.GetComponent<RememberTransform>() && EditorApplication.timeSinceStartup<deadline) yield return null;
            Assert.That(root.GetComponent<RememberTransform>(), Is.Null);
            Assert.That(root.GetComponent<RememberGameObject>(), Is.Not.Null);
            Assert.That(root.GetComponent<UniqueID>(), Is.Not.Null);
        }

#if UNITY_6000_6_OR_NEWER
        [UnityTest, Category("CrystalSaveGraphics")]
        public IEnumerator ActualHierarchyWindowBindsIconsToMatchingObject()
        {
            settings.Settings.showEditorHierarchyIcons=true;
            root.name="CrystalSaveHierarchyBindingTest";
            root.AddComponent<SaveablePrefab>(); Composite(root,typeof(RememberTransform));
            var window=ScriptableObject.CreateInstance<Unity.Hierarchy.Editor.HierarchyWindow>();
            try
            {
                window.Show(); window.SetSearchText(root.name);
                double deadline=EditorApplication.timeSinceStartup+10;
                while(window.rootVisualElement.Q(CrystalSaveHierarchyIcons.ContainerName)==null &&
                    EditorApplication.timeSinceStartup<deadline)
                {
                    window.Repaint(); yield return null;
                }
                var icons=window.rootVisualElement.Q(CrystalSaveHierarchyIcons.ContainerName);
                Assert.That(icons,Is.Not.Null,"Real HierarchyWindow.BindViewItem must reach the Crystal Save drawer.");
                Assert.That(icons.Q<Image>("SaveablePrefab"),Is.Not.Null);
                Assert.That(icons.Q<Image>("RememberComposite"),Is.Not.Null);
                settings.Settings.showEditorHierarchyIcons=false;
                CrystalSaveEditorAutomationSettings.ClearCache();
                deadline=EditorApplication.timeSinceStartup+3;
                while(window.rootVisualElement.Q(CrystalSaveHierarchyIcons.ContainerName)!=null &&
                    EditorApplication.timeSinceStartup<deadline) yield return null;
                Assert.That(window.rootVisualElement.Q(CrystalSaveHierarchyIcons.ContainerName),Is.Null);
            }
            finally { window.Close(); }
        }

        [Test]
        public void HierarchyRowShowsDistinctIconsAndRebindingClearsOnlyCrystalSaveElements()
        {
            root.AddComponent<SaveablePrefab>(); Composite(root,typeof(RememberGameObject));
            var uid=root.AddComponent<UniqueID>(); uid.ID="linked";
            var container=new VisualElement(); var foreign=new Label("other extension"); container.Add(foreign);
            var linked=new HashSet<string> { "linked" };
            CrystalSaveHierarchyIcons.UpdateIcons(container,root,true,linked);
            var icons=container.Q(CrystalSaveHierarchyIcons.ContainerName);
            Assert.That(icons, Is.Not.Null); Assert.That(icons.childCount, Is.EqualTo(3));
            foreach(string name in new[] { "SaveablePrefab","RememberComposite","LinkedUniqueID" })
                Assert.That(icons.Q<Image>(name)?.image, Is.Not.Null,name);
            CrystalSaveHierarchyIcons.UpdateIcons(container,root,true,linked);
            Assert.That(container.childCount, Is.EqualTo(2),"Repeated binding must not duplicate icons.");
            CrystalSaveHierarchyIcons.UpdateIcons(container,null,true,linked);
            Assert.That(container.childCount, Is.EqualTo(1)); Assert.That(container[0], Is.SameAs(foreign));
            CrystalSaveHierarchyIcons.UpdateIcons(container,root,false,linked);
            Assert.That(container.childCount, Is.EqualTo(1));
        }
#endif
    }
}
#endif
