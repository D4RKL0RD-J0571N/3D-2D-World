#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object=UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveVisibilityAndObjectTests
    {
        private GameObject root;
        private FeatureSettingsScope settings;
        [SetUp] public void SetUp()
        {
            settings=new FeatureSettingsScope(); root=new GameObject("ObjectFeatures"); root.SetActive(false);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); settings.Dispose(); }

        [TestCase(false,false)] [TestCase(false,true)] [TestCase(true,false)] [TestCase(true,true)]
        public void OffSceneOptionsApplyForBothPrefabAndComponent(bool prefabOwner,bool enabled)
        {
            Component owner=prefabOwner ? (Component)root.AddComponent<SaveablePrefab>() : root.AddComponent<FeatureProbe>();
            foreach(string property in new[]{"DisableAllColliders","DisableRenderers","DisableCharacterController","SetRigidbodyKinematic"})
                owner.GetType().GetProperty(property).SetValue(owner,enabled);
            Set(owner,"VisibleInScenes",new List<string>{"Home"});
            var box=root.AddComponent<BoxCollider>(); var renderer=root.AddComponent<MeshRenderer>();
            var canvas=root.AddComponent<Canvas>(); var controller=root.AddComponent<CharacterController>();
            var body=root.AddComponent<Rigidbody>(); body.useGravity=false;
            var visibility=root.AddComponent<PersistentVisibilityController>();
            Call(visibility,"InitializeReferences"); Call(visibility,"InitializeComponentStates");
            visibility.ApplyVisibilityBasedOnScene("Away");
            Assert.That(box.enabled,Is.EqualTo(!enabled)); Assert.That(renderer.enabled,Is.EqualTo(!enabled));
            Assert.That(canvas.enabled,Is.EqualTo(!enabled)); Assert.That(controller.enabled,Is.EqualTo(!enabled));
            Assert.That(body.isKinematic,Is.EqualTo(enabled));
            visibility.ApplyVisibilityBasedOnScene("HOME"); // Scene filter is intentionally case insensitive.
            Assert.That(box.enabled && renderer.enabled && canvas.enabled && controller.enabled,Is.True);
            Assert.That(body.isKinematic,Is.False);
            var restored=SaveDataSerializer.Instance.Deserialize<VisibilitySettingsData>(visibility.CaptureAndSerializeSettings());
            Assert.That(restored.VisibleInScenes,Is.EqualTo(new[]{"Home"}));
            Assert.That(restored.DisableAllColliders,Is.EqualTo(enabled));
        }

        [TestCase("RememberName",false)] [TestCase("RememberName",true)]
        [TestCase("RememberLayer",false)] [TestCase("RememberLayer",true)]
        [TestCase("RememberTag",false)] [TestCase("RememberTag",true)]
        [TestCase("RememberActive",false)] [TestCase("RememberActive",true)]
        public void RememberGameObjectRestoresOnlySelectedProperties(string option,bool enabled)
        {
            Composite(root,typeof(RememberGameObject)); var component=root.GetComponent<RememberGameObject>();
            var options=new GameObjectPropertySettings(); typeof(GameObjectPropertySettings).GetField(option).SetValue(options,enabled);
            Set(component,"propertySettings",options);
            root.name="Saved"; root.layer=2; root.tag="Player"; root.SetActive(false);
            var bytes=component.SaveData();
            root.name="Changed"; root.layer=0; root.tag="Untagged"; root.SetActive(true);
            component.LoadData(bytes);
            Assert.That(root.name,Is.EqualTo(enabled && option=="RememberName" ? "Saved" : "Changed"));
            Assert.That(root.layer,Is.EqualTo(enabled && option=="RememberLayer" ? 2 : 0));
            Assert.That(root.tag,Is.EqualTo(enabled && option=="RememberTag" ? "Player" : "Untagged"));
            Assert.That(root.activeSelf,Is.EqualTo(!(enabled && option=="RememberActive")));
        }
    }
}
#endif
