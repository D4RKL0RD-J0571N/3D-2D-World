#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class SaveableComponentFeatureTests
    {
        private readonly List<GameObject> roots = new();
        private FeatureSettingsScope settings;
        [SetUp] public void SetUp() => settings = new FeatureSettingsScope();
        [TearDown] public void TearDown()
        {
            foreach (var root in roots) if (root) Object.DestroyImmediate(root);
            roots.Clear();
            settings.Dispose();
        }
        private GameObject Root(string name = "FeatureTest")
        {
            var go = new GameObject(name); go.SetActive(false); roots.Add(go); return go;
        }
        private FeatureProbe Probe(string id)
        {
            var c = Root(id).AddComponent<FeatureProbe>(); Set(c, "uniqueID", id); c.ComponentID = "value"; return c;
        }

        [TestCase(SaveableComponent.EventValueType.Bool)]
        [TestCase(SaveableComponent.EventValueType.Int)]
        [TestCase(SaveableComponent.EventValueType.Float)]
        [TestCase(SaveableComponent.EventValueType.String)]
        [TestCase(SaveableComponent.EventValueType.Vector2)]
        [TestCase(SaveableComponent.EventValueType.Vector3)]
        [TestCase(SaveableComponent.EventValueType.Vector4)]
        [TestCase(SaveableComponent.EventValueType.Color)]
        [TestCase(SaveableComponent.EventValueType.Quaternion)]
        public void EventBridgeRestoresSavedTypeAndValueOnFreshReceiver(SaveableComponent.EventValueType type)
        {
            var source = Probe("source"); var target = Probe("target");
            source.Value = 73;
            Set(source, "enableEventBridge", true); Set(target, "enableEventBridge", true);
            object value = type switch {
                SaveableComponent.EventValueType.Bool => true,
                SaveableComponent.EventValueType.Int => 42,
                SaveableComponent.EventValueType.Float => 0.75f,
                SaveableComponent.EventValueType.String => "Crystal 🌟 Save",
                SaveableComponent.EventValueType.Vector2 => new Vector2(2, 3),
                SaveableComponent.EventValueType.Vector3 => new Vector3(2, 3, 4),
                SaveableComponent.EventValueType.Vector4 => new Vector4(2, 3, 4, 5),
                SaveableComponent.EventValueType.Color => new Color(.1f, .2f, .3f, .4f),
                _ => Quaternion.Euler(20, 30, 40)
            };
            var order = new List<string>();
            Get<UnityEvent>(source, "onSaving").AddListener(() => Call(source, "SetEvent" + type, value));
            target.Loaded = () => order.Add("component");
            object observed = null;
            switch (type)
            {
                case SaveableComponent.EventValueType.Bool: Get<SaveableComponent.BoolEvent>(target,"onLoadedBool").AddListener(v => observed=v); break;
                case SaveableComponent.EventValueType.Int: Get<SaveableComponent.IntEvent>(target,"onLoadedInt").AddListener(v => observed=v); break;
                case SaveableComponent.EventValueType.Float: Get<SaveableComponent.FloatEvent>(target,"onLoadedFloat").AddListener(v => observed=v); break;
                case SaveableComponent.EventValueType.String: Get<SaveableComponent.StringEvent>(target,"onLoadedString").AddListener(v => observed=v); break;
                case SaveableComponent.EventValueType.Vector2: Get<SaveableComponent.Vector2Event>(target,"onLoadedVector2").AddListener(v => observed=v); break;
                case SaveableComponent.EventValueType.Vector3: Get<SaveableComponent.Vector3Event>(target,"onLoadedVector3").AddListener(v => observed=v); break;
                case SaveableComponent.EventValueType.Vector4: Get<SaveableComponent.Vector4Event>(target,"onLoadedVector4").AddListener(v => observed=v); break;
                case SaveableComponent.EventValueType.Color: Get<SaveableComponent.ColorEvent>(target,"onLoadedColor").AddListener(v => observed=v); break;
                case SaveableComponent.EventValueType.Quaternion: Get<SaveableComponent.QuaternionEvent>(target,"onLoadedQuaternion").AddListener(v => observed=v); break;
            }
            Get<UnityEvent>(target,"onLoaded").AddListener(() => {
                Assert.That(observed, Is.EqualTo(value)); order.Add("loaded");
            });
            target.LoadData(source.SaveData());
            Assert.That(target.Value, Is.EqualTo(73));
            Assert.That(observed, Is.EqualTo(value));
            Assert.That(order, Is.EqualTo(new[] { "component", "loaded" }));
        }

        [Test]
        public void BridgeResetsToDefaultBeforeEverySaveAndDisabledBridgeKeepsLegacyBytes()
        {
            var c = Probe("default"); c.Value = 27;
            var legacy = c.SaveData();
            Set(c,"enableEventBridge",true); Set(c,"eventDefaultFloat",.9f);
            var saving = Get<UnityEvent>(c,"onSaving");
            UnityAction push = () => c.SetEventFloat(.2f);
            saving.AddListener(push); var first = c.SaveData(); saving.RemoveListener(push);
            c.LoadData(first); Assert.That(c.EventCurrentFloat, Is.EqualTo(.2f));
            c.LoadData(c.SaveData()); Assert.That(c.EventCurrentFloat, Is.EqualTo(.9f));
            c.Value = 0; c.LoadData(legacy); Assert.That(c.Value, Is.EqualTo(27));
            Set(c,"enableEventBridge",false); Assert.That(c.SaveData(), Is.EqualTo(legacy));
        }

        [TestCase(SpaceMode.Local, false)] [TestCase(SpaceMode.Local, true)]
        [TestCase(SpaceMode.World, false)] [TestCase(SpaceMode.World, true)]
        public void TransformRoundTripsThroughCompositeAndLateUpdate(SpaceMode space, bool caching)
        {
            var parent = Root("parent"); parent.transform.position = new Vector3(12,3,4); parent.transform.localScale = Vector3.one * 2;
            var go = Root(); go.transform.SetParent(parent.transform);
            Composite(go, typeof(RememberTransform));
            var c = ConfigureTransform(go,space); Set(c,"enablePerformanceCaching",caching);
            go.transform.localPosition = new Vector3(4,5,6); go.transform.localRotation = Quaternion.Euler(10,20,30); go.transform.localScale = Vector3.one * 3;
            var position = go.transform.position; var rotation = go.transform.rotation; var scale = go.transform.lossyScale;
            var data = c.SaveData();
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            c.LoadData(data);
            Assert.That(go.transform.position, Is.Not.EqualTo(position), "Transform application must remain deferred.");
            Call(c,"LateUpdate");
            Assert.That(Vector3.Distance(go.transform.position,position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(go.transform.rotation,rotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(go.transform.lossyScale,scale), Is.LessThan(.0001f));
        }

        [Test]
        public void SkipUnchangedAfterLoadReturnsRestoredTransformRatherThanPreviousSave()
        {
            var c = ConfigureTransform(Root()); Set(c,"skipSavingWhenUnchanged",true);
            c.transform.position = Vector3.one; var original = c.SaveData();
            c.transform.position = Vector3.one * 9; var later = c.SaveData();
            c.LoadData(original); Call(c,"LateUpdate");
            Assert.That(c.SaveData(), Is.EqualTo(original));
            Assert.That(c.SaveData(), Is.Not.EqualTo(later));
        }

        [TestCase(true)] [TestCase(false)]
        public void TransformRestorePreservesCharacterControllerEnabledState(bool enabled)
        {
            var go = Root(); var controller = go.AddComponent<CharacterController>(); controller.enabled = enabled;
            var c = ConfigureTransform(go); Set(c,"disableControllerOnApply",true);
            var data = c.SaveData(); c.transform.position = Vector3.one * 10; c.LoadData(data); Call(c,"LateUpdate");
            Assert.That(controller.enabled, Is.EqualTo(enabled));
        }

        [Test]
        public void ComponentSchedulingSortsPrioritiesAndStreamsOnlyRequestedObject()
        {
            var manager = new ComponentManager(); var order = new List<string>(); var data = new SaveData();
            foreach (var spec in new[] { ("low",10,false), ("high",90,false), ("stream-a",100,true), ("stream-b",20,true) })
            {
                var c = Probe(spec.Item1); c.Value = spec.Item2; c.LoadPriority = spec.Item2; c.DeferLowPriorityUntilRequested=spec.Item3;
                c.HomeScene="Area"; c.RememberHomeScene=true; c.Loaded=() => order.Add(c.gameObject.name);
                manager.RegisterSaveableComponent(c);
            }
            manager.CollectComponentData(data);
            Assert.That(data.ComponentMetadata["stream-a_value"].DeferLowPriorityUntilRequested, Is.True);
            manager.ApplyComponentData(data); Assert.That(order, Is.EqualTo(new[] { "high", "low" }));
            manager.ProcessDeferredComponentsForGameObject("stream-a"); Assert.That(order, Is.EqualTo(new[] { "high", "low", "stream-a" }));
            manager.ProcessDeferredComponentsForScene("Area"); Assert.That(order, Is.EqualTo(new[] { "high", "low", "stream-a", "stream-b" }));
            manager.ProcessDeferredComponents(); Assert.That(order.Count, Is.EqualTo(4));
        }

        [Test]
        public void DataArrivingBeforeRegistrationIsAppliedExactlyOnce()
        {
            var manager = new ComponentManager(); var c = Probe("late"); c.Value=52;
            var data = new SaveData(); data.ComponentsData[c.UniqueIdentifier]=c.SaveData(); c.Value=0;
            manager.ApplyComponentData(data); Assert.That(c.Loads, Is.Zero);
            manager.RegisterSaveableComponent(c); manager.RegisterSaveableComponent(c);
            Assert.That(c.Value, Is.EqualTo(52)); Assert.That(c.Loads, Is.EqualTo(1));
        }

        [TestCase(-10,0)] [TestCase(50,50)] [TestCase(120,100)]
        public void PriorityClampsForBothSaveableKinds(int input,int expected)
        {
            var c = Probe("priority"); var p = Root().AddComponent<SaveablePrefab>();
            c.LoadPriority=input; p.LoadPriority=input;
            Assert.That(c.LoadPriority, Is.EqualTo(expected)); Assert.That(p.LoadPriority, Is.EqualTo(expected));
        }
    }
}
#endif
