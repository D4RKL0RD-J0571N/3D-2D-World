#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class SaveablePrefabFeatureTests
    {
        private GameObject root;
        private SaveablePrefab prefab;
        private PrefabManager manager;
        private FeatureSettingsScope settings;
        [SetUp] public void SetUp()
        {
            settings=new FeatureSettingsScope();
            root=new GameObject("PrefabFeature"); root.SetActive(false);
            prefab=root.AddComponent<SaveablePrefab>();
            Set(prefab,"uniqueID","instance-id"); Set(prefab,"prefabAssetID","asset-id");
            prefab.TrackChildStateOverrides=false;
            manager=root.AddComponent<PrefabManager>();
        }
        [TearDown] public void TearDown() { if(root) Object.DestroyImmediate(root); settings.Dispose(); }
        private GameObject Child()
        {
            var go=new GameObject("Child"); go.transform.SetParent(root.transform); return go;
        }

        [TestCase(false)] [TestCase(true)]
        public void ChildStateAndTransformOverridesRoundTripOnlyWhenEnabled(bool enabled)
        {
            var child=Child(); child.SetActive(false); child.layer=2; child.transform.localPosition=new Vector3(2,3,4);
            prefab.TrackChildStateOverrides=enabled; prefab.TrackChildTransformOverrides=enabled;
            var bytes=prefab.CaptureRuntimeModifications();
            child.SetActive(true); child.layer=0; child.transform.localPosition=Vector3.zero;
            prefab.ApplyRuntimeModifications(bytes);
            Assert.That(child.activeSelf, Is.EqualTo(!enabled));
            Assert.That(child.layer, Is.EqualTo(enabled ? 2 : 0));
            Assert.That(child.transform.localPosition, Is.EqualTo(enabled ? new Vector3(2,3,4) : Vector3.zero));
        }

        [TestCase(false)] [TestCase(true)]
        public void ComponentBlobsRestoreHelperPayloadOnlyWhenEnabled(bool enabled)
        {
            var probe=Child().AddComponent<FeatureProbe>(); Set(probe,"uniqueID","instance-id"); probe.ComponentID="health"; probe.Value=87;
            prefab.TrackComponentBlobs=enabled;
            var bytes=prefab.CaptureRuntimeModifications(); probe.Value=0;
            prefab.ApplyRuntimeModifications(bytes);
            Assert.That(probe.Value, Is.EqualTo(enabled ? 87 : 0));
            Assert.That(probe.Loads, Is.EqualTo(enabled ? 1 : 0));
        }

        [TestCase(false)] [TestCase(true)]
        public void OuterBlobRestoreCannotCrossANestedPrefabBoundary(bool innerTracksBlobs)
        {
            prefab.TrackComponentBlobs = true;
            var outerProbe = Child().AddComponent<FeatureProbe>();
            Set(outerProbe, "uniqueID", "instance-id"); outerProbe.ComponentID = "outer-state";
            var inner = Child().AddComponent<SaveablePrefab>();
            Set(inner, "uniqueID", "inner-id"); inner.TrackComponentBlobs = innerTracksBlobs;
            var innerChild = new GameObject("NestedState"); innerChild.transform.SetParent(inner.transform);
            var innerProbe = innerChild.AddComponent<FeatureProbe>();
            Set(innerProbe, "uniqueID", "inner-id"); innerProbe.ComponentID = "inner-state";
            outerProbe.Value = 17; innerProbe.Value = 42;
            var outerBytes = outerProbe.SaveData();
            var innerBytes = innerProbe.SaveData();
            outerProbe.Value = 0; innerProbe.Value = 0;
            // Deliberately include bytes for a nested helper in the outer payload.
            // A valid outer capture alone would never exercise restore's ownership check.
            var mods = new RuntimeModificationData
            {
                ComponentBlobs = new List<ComponentBlob>
                {
                    new ComponentBlob("outer-state", outerBytes),
                    new ComponentBlob("inner-state", innerBytes)
                }
            };
            prefab.ApplyRuntimeModifications(SaveDataSerializer.Instance.Serialize(mods));
            Assert.That(outerProbe.Value, Is.EqualTo(17));
            Assert.That(outerProbe.Loads, Is.EqualTo(1));
            Assert.That(innerProbe.Value, Is.Zero);
            Assert.That(innerProbe.Loads, Is.Zero);
            inner.ApplyRuntimeModifications(SaveDataSerializer.Instance.Serialize(mods));
            Assert.That(innerProbe.Value, Is.EqualTo(innerTracksBlobs ? 42 : 0));
            Assert.That(innerProbe.Loads, Is.EqualTo(innerTracksBlobs ? 1 : 0));
            Assert.That(outerProbe.Loads, Is.EqualTo(1));
        }

        [Test]
        public void PersistentPrefabSuppressesHandoffFromConflictingSerializedFlags()
        {
            Set(prefab, "keepAcrossScenes", true);
            Set(prefab, "enableCrossSceneStateHandoff", true);
            Assert.That(Get<bool>(prefab, "enableCrossSceneStateHandoff"), Is.True,
                "Fixture must bypass setters to represent conflicting legacy serialized input.");
            var data = prefab.TryBuildSaveData();
            Assert.That(data, Is.Not.Null);
            Assert.That(data.EnableCrossSceneStateHandoff, Is.False);
        }

        [Test]
        public void HandoffEmitsDataWhenAnUnchangedScenePrefabWouldOtherwiseBeSkipped()
        {
            Set(prefab, "skipSavingWhenUnchanged", true);
            Set(prefab, "initialName", root.name);
            Set(prefab, "initialTag", root.tag);
            Set(prefab, "initialLayer", root.layer);
            Set(prefab, "initialActiveState", root.activeSelf);
            Assert.That(prefab.IsAddedAtRuntime, Is.False);
            Assert.That(prefab.TryBuildSaveData(), Is.Not.Null, "First capture establishes the comparison state.");
            Assert.That(prefab.TryBuildSaveData(), Is.Null, "Control must actually exercise skip-unchanged.");
            prefab.EnableCrossSceneStateHandoff = true;
            Assert.That(prefab.TryBuildSaveData().EnableCrossSceneStateHandoff, Is.True);
            Assert.That(prefab.TryBuildSaveData().EnableCrossSceneStateHandoff, Is.True,
                "Repeated captures must keep the source identity available for scene handoff.");
            prefab.EnableCrossSceneStateHandoff = false;
            Assert.That(prefab.TryBuildSaveData(), Is.Null);
        }

        [TestCase(false)] [TestCase(true)]
        public void ColliderSettingsRoundTripRootAndChildThroughPrefabManager(bool onChild)
        {
            var go=onChild ? Child() : root;
            var box=go.AddComponent<BoxCollider>(); box.center=Vector3.one; box.size=new Vector3(2,3,4); box.isTrigger=true; box.enabled=false;
            prefab.TrackColliderSettings=true;
            var data=manager.BuildPrefabData(prefab);
            Assert.That(data.Colliders, Has.Count.EqualTo(1));
            box.center=Vector3.zero; box.size=Vector3.one; box.isTrigger=false; box.enabled=true;
            Call(typeof(PrefabManager),"ApplyColliderSettings",root,data.Colliders);
            Assert.That(box.center, Is.EqualTo(Vector3.one)); Assert.That(box.size, Is.EqualTo(new Vector3(2,3,4)));
            Assert.That(box.isTrigger, Is.True); Assert.That(box.enabled, Is.False);
            prefab.TrackColliderSettings=false;
            Assert.That(manager.BuildPrefabData(prefab).Colliders, Is.Null.Or.Empty);
        }

        [Test]
        public void SchedulingHomeScenePoolingAndTrackingOptionsSurviveSerialization()
        {
            prefab.LoadPriority=87; prefab.DeferLowPriorityUntilRequested=true; prefab.DisablePooling=true;
            prefab.RememberHomeScene=true; prefab.SetHomeScene("Home");
            prefab.EnableCrossSceneStateHandoff=true; prefab.HandoffTransform=false;
            foreach (string property in new[] { "TrackAddedComponents","TrackComponentBlobs","TrackMaterialOverrides",
                "TrackChildStateOverrides","TrackChildTransformOverrides","TrackSkinnedMeshOverrides",
                "TrackBlendshapeOverrides","TrackTextureOverrides","TrackParticleSnapshots","TrackColliderSettings" })
                typeof(SaveablePrefab).GetProperty(property).SetValue(prefab,true);
            var data=SaveDataSerializer.Instance.Deserialize<SaveablePrefabData>(SaveDataSerializer.Instance.Serialize(manager.BuildPrefabData(prefab)));
            Assert.That(data.InstanceID, Is.EqualTo(prefab.UniqueID)); Assert.That(data.PrefabID, Is.EqualTo(prefab.PrefabAssetID));
            Assert.That(data.LoadPriority, Is.EqualTo(87)); Assert.That(data.DeferLowPriorityUntilRequested, Is.True);
            Assert.That(data.HomeScene, Is.EqualTo("Home")); Assert.That(data.DisablePooling, Is.True);
            Assert.That(data.EnableCrossSceneStateHandoff, Is.True); Assert.That(data.HandoffTransform, Is.False);
            foreach (string property in new[] { "TrackAddedComponents","TrackComponentBlobs","TrackMaterialOverrides",
                "TrackChildStateOverrides","TrackChildTransformOverrides","TrackSkinnedMeshOverrides",
                "TrackBlendshapeOverrides","TrackTextureOverrides","TrackParticleSnapshots","TrackColliderSettings" })
                Assert.That(typeof(SaveablePrefabData).GetProperty(property).GetValue(data), Is.True,property);
        }

        [Test]
        public void PrefabSchedulerOrdersImmediateAndDefersEvenHighestPriorityByScene()
        {
            var entries=new List<SaveablePrefabData>();
            foreach(var spec in new[] { ("low",10,false,"A"), ("high",90,false,"A"), ("defer",100,true,"B") })
            {
                var d=manager.BuildPrefabData(prefab); d.InstanceID=spec.Item1; d.LoadPriority=spec.Item2;
                d.DeferLowPriorityUntilRequested=spec.Item3; d.HomeScene=spec.Item4; entries.Add(d);
            }
            var immediate=manager.PrepareImmediatePrefabs(entries);
            Assert.That(immediate.Select(d=>d.InstanceID), Is.EqualTo(new[] { "high","low" }));
            Assert.That(manager.GetDeferredSceneKeys(), Does.Contain("B"));
            Assert.That(manager.PeekDeferredPrefabsForScene("B").Select(d=>d.InstanceID), Is.EqualTo(new[] { "defer" }));
        }

        [TestCase(false)] [TestCase(true)]
        public void AddedComponentPrimitiveFieldsRestoreOnlyWhenEnabled(bool enabled)
        {
            // Capture a procedural root: its attached helper was added at runtime.
            var probe=root.AddComponent<FeatureProbe>(); probe.Value=123;
            prefab.TrackAddedComponents=enabled;
            var data=prefab.CaptureRuntimeModifications(); Object.DestroyImmediate(probe);
            prefab.ApplyRuntimeModifications(data);
            var restored=root.GetComponent<FeatureProbe>();
            if(enabled) { Assert.That(restored,Is.Not.Null); Assert.That(restored.Value,Is.EqualTo(123)); }
            else Assert.That(restored,Is.Null);
        }

        [TestCase(false)] [TestCase(true)]
        public void MeshAndMaterialAssetOverridesRestoreThroughSavedReferences(bool enabled)
        {
            string folder="Assets/CrystalSaveAssetTest_"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",System.IO.Path.GetFileName(folder));
            var mesh=new Mesh { name="TestMesh", vertices=new[] { Vector3.zero,Vector3.right,Vector3.up }, triangles=new[] {0,1,2} };
            var material=new Material(Shader.Find("Hidden/InternalErrorShader")) { name="TestMaterial" };
            AssetDatabase.CreateAsset(mesh,folder+"/Mesh.asset"); AssetDatabase.CreateAsset(material,folder+"/Material.mat");
            try
            {
                var go=Child(); var filter=go.AddComponent<MeshFilter>(); filter.sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
                prefab.TrackSkinnedMeshOverrides=enabled; prefab.TrackMaterialOverrides=enabled;
                var bytes=prefab.CaptureRuntimeModifications(); filter.sharedMesh=null; renderer.sharedMaterial=null;
                prefab.ApplyRuntimeModifications(bytes);
                Assert.That(filter.sharedMesh,Is.EqualTo(enabled ? mesh : null));
                Assert.That(renderer.sharedMaterial,Is.EqualTo(enabled ? material : null));
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [TestCase(false)] [TestCase(true)]
        public void BlendshapeOverridesRestoreWeightOnlyWhenEnabled(bool enabled)
        {
            var mesh=new Mesh { vertices=new[] {Vector3.zero,Vector3.right,Vector3.up}, triangles=new[]{0,1,2} };
            var delta=new[] {Vector3.forward,Vector3.forward,Vector3.forward};
            mesh.AddBlendShapeFrame("Shape",100,delta,new Vector3[3],new Vector3[3]);
            try
            {
                var renderer=Child().AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh=mesh; renderer.SetBlendShapeWeight(0,67);
                prefab.TrackBlendshapeOverrides=enabled;
                var bytes=prefab.CaptureRuntimeModifications(); renderer.SetBlendShapeWeight(0,0); prefab.ApplyRuntimeModifications(bytes);
                Assert.That(renderer.GetBlendShapeWeight(0),Is.EqualTo(enabled ? 67 : 0));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ParticleSnapshotsRestoreSimulationTimeOnlyWhenEnabled(bool enabled)
        {
            root.SetActive(true);
            var particles=Child().AddComponent<ParticleSystem>();
            var main=particles.main; main.playOnAwake=false;
            particles.Simulate(.8f,true,true,true);
            Assert.That(particles.time,Is.EqualTo(.8f).Within(.03f),"Fixture must have an advanced simulation.");
            prefab.TrackParticleSnapshots=enabled;
            var bytes=prefab.CaptureRuntimeModifications(); particles.time=0; prefab.ApplyRuntimeModifications(bytes);
            Assert.That(particles.time,Is.EqualTo(enabled ? .8f : 0).Within(.03f));
        }

        [Test]
        public void UnregisteredPrefabEmitsNothingAndPersistentPrefabSuppressesHandoff()
        {
            prefab.RegisterWithSaveSystem=false;
            Assert.That(prefab.TryBuildSaveData(), Is.Null);
            prefab.RegisterWithSaveSystem=true; prefab.KeepAcrossScenes=true; prefab.EnableCrossSceneStateHandoff=true;
            Assert.That(prefab.TryBuildSaveData().EnableCrossSceneStateHandoff, Is.False);
        }
    }
}
#endif
