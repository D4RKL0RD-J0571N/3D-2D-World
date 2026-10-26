#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System.Collections;
using System.Linq;
using Arawn.CrystalSave.Runtime;
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
        [UnityTest]
        public IEnumerator TransformRestore_PhysicsRotationsCompleteWithoutRetries()
        {
            var mode = Physics.simulationMode;
            var helper = SceneComponent("Physics rotation roundoff");
            var body = helper.gameObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                for (int i = 0; i < 64; i++)
                {
                    body.angularVelocity = new Vector3(1.7f, 2.3f, -0.9f);
                    Physics.Simulate(0.02f);
                    byte[] bytes = helper.SaveData();
                    var expected = SaveManager.Instance.Serializer.Deserialize<TransformData>(bytes);
                    helper.transform.rotation = Quaternion.identity;
                    helper.LoadData(bytes);
                    yield return null;
                    yield return null;
                    Assert.That(Get<int>(helper, "retryCount"), Is.Zero, "Valid physics rotation at sample " + i);
                    Assert.That(Get<bool>(helper, "shouldApplyTransform"), Is.False);
                    Assert.That(Get<object>(helper, "applicationError"), Is.Null);
                    Assert.That(Quaternion.Angle(helper.transform.rotation, expected.Rotation.Value), Is.LessThan(0.05f));
                    Assert.That(body.isKinematic, Is.False);
                }
            }
            finally { Physics.simulationMode = mode; Object.Destroy(helper.gameObject); }
        }

        [UnityTest]
        public IEnumerator TransformRestore_ParentSpaceRoundoffCompletesWithoutRetries()
        {
            var helper = SceneComponent("Parent space roundoff");
            var parent = new GameObject("Rotated scaled parent");
            helper.transform.SetParent(parent.transform, false);
            parent.transform.SetPositionAndRotation(new Vector3(19.05f, 3.71f, -7.39f), Quaternion.Euler(37.1f, 91.7f, -63.9f));
            parent.transform.localScale = Vector3.one * 1.7f;
            try
            {
                foreach (SpaceMode space in new[] { SpaceMode.Local, SpaceMode.World })
                {
                    ConfigureTransform(helper.gameObject, space);
                    for (int i = 0; i < 8; i++)
                    {
                        helper.transform.localPosition = new Vector3(1.17f + i, -3.1f, 8.23f);
                        helper.transform.localRotation = Quaternion.Euler(17.3f * i, -41.7f, 113.1f);
                        byte[] bytes = helper.SaveData();
                        var expected = SaveManager.Instance.Serializer.Deserialize<TransformData>(bytes);
                        helper.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        helper.LoadData(bytes);
                        yield return null;
                        yield return null;
                        Assert.That(Get<int>(helper, "retryCount"), Is.Zero, space + " pose " + i);
                        Assert.That(Get<object>(helper, "applicationError"), Is.Null);
                        Vector3 position = space == SpaceMode.Local ? helper.transform.localPosition : helper.transform.position;
                        Quaternion rotation = space == SpaceMode.Local ? helper.transform.localRotation : helper.transform.rotation;
                        Assert.That(Vector3.Distance(position, expected.Position.Value), Is.LessThan(0.0001f));
                        Assert.That(Quaternion.Angle(rotation, expected.Rotation.Value), Is.LessThan(0.05f));
                    }
                }
            }
            finally { Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator TransformRestore_PreservesDynamicBodyMotion()
        {
            var mode = Physics.simulationMode;
            var helper = SceneComponent("Moving transform restore");
            var body = helper.gameObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                var velocity = new Vector3(2, 3, 4);
                var spin = new Vector3(0.5f, -1.2f, 2.1f);
                var physicsHelper = helper.gameObject.AddComponent<RememberRigidbody>();
                body.linearVelocity = velocity;
                body.angularVelocity = spin;
                byte[] physicsBytes = physicsHelper.SaveData();
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                physicsHelper.LoadData(physicsBytes);
                helper.ApplyTransformData(new TransformData { Position = new Vector3(5, 6, 7), Rotation = Quaternion.identity, Scale = Vector3.one });
                Assert.That(body.linearVelocity, Is.EqualTo(velocity), "Transform application must preserve existing or RememberRigidbody-restored motion.");
                Assert.That(body.angularVelocity, Is.EqualTo(spin));
                Assert.That(body.isKinematic, Is.False);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = true;
                body.Sleep();
                helper.ApplyTransformData(new TransformData { Position = Vector3.up * 10 });
                Physics.Simulate(0.02f);
                Assert.That(body.position.y, Is.LessThan(10f), "A sleeping body teleported into the air must resume physics.");
                body.isKinematic = true;
                helper.ApplyTransformData(new TransformData { Position = Vector3.zero });
                Assert.That(body.isKinematic, Is.True);
                yield return null;
            }
            finally { Physics.simulationMode = mode; Object.Destroy(helper.gameObject); }
        }

        [UnityTest]
        public IEnumerator TransformRestore_InvalidRotationFailsAndRetainsPendingBytes()
        {
            var mode = Physics.simulationMode;
            var helper = SceneComponent("Invalid rotation restore");
            helper.gameObject.AddComponent<Rigidbody>().useGravity = false;
            Physics.simulationMode = SimulationMode.Script;
            bool ignored = LogAssert.ignoreFailingMessages;
            try
            {
                var manager = SaveManager.Instance;
                var candidate = new SaveData(manager.SaveSettings.version);
                byte[] bytes = manager.Serializer.Serialize(new TransformData
                {
                    Position = Vector3.one, Rotation = new Quaternion(0, 0, 0, 2), Scale = Vector3.one
                });
                candidate.ComponentsData[helper.UniqueIdentifier] = bytes;
                manager.SaveSystem.Save(manager.Serializer.Serialize(candidate), manager.GetSlotMetadata(1));
                LogAssert.ignoreFailingMessages = true;
                var load = manager.LoadSaveSlotAsync(1, restoreLastActiveScene: false);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.False, "Tolerance must not hide a materially invalid rotation.");
                Assert.That(load.Result.ErrorMessage, Does.Contain(helper.UniqueIdentifier));
                Assert.That(PendingBytes(manager.ComponentManager)[helper.UniqueIdentifier], Is.EqualTo(bytes));
                Assert.That(manager.ComponentManager.HasComponentDeserialized(helper.UniqueIdentifier), Is.False);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignored;
                Physics.simulationMode = mode;
                Object.Destroy(helper.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TransformRestore_DemoCylindersStreamAcrossSavesAndPlayModeRestarts()
        {
            yield return new ExitPlayMode();
            yield return new RecompileScripts(expectScriptCompilation: false);
            // Copy the shipped cylinder configuration into the fixture's scene. Never
            // run the demo's UI/settings against a developer's persistent save folder.
            var scene = EditorSceneManager.OpenScene(AssetsRoot + "/B.unity", OpenSceneMode.Single);
            var preview = EditorSceneManager.OpenPreviewScene(DemoRoot + "DeferredComponentStreaming.unity");
            try
            {
                foreach (string name in new[] { "Cylinder (17)", "Cylinder (18)" })
                {
                    var source = preview.GetRootGameObjects().Single(g => g.name == name);
                    var copy = Object.Instantiate(source);
                    copy.name = name;
                    SceneManager.MoveGameObjectToScene(copy, scene);
                    Assert.That(copy.GetComponent<RememberTransform>().DeferLowPriorityUntilRequested, Is.True);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(18, -0.5f, 0);
            floor.transform.localScale = new Vector3(50, 1, 50);
            var player = new GameObject("Transform restore streaming origin");
            player.transform.position = Vector3.one * 1000;
            var streamer = player.AddComponent<DeferredPrefabRadiusStreamer>();
            Set(streamer, "playerTransform", player.transform);
            Set(streamer, "activationRadius", 2f);
            Set(streamer, "refreshInterval", 0f);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            var mode = Physics.simulationMode;
            try
            {
                yield return new RecompileScripts(expectScriptCompilation: false);
                yield return new EnterPlayMode();
                Assert.That(Application.isPlaying, Is.True);
                Physics.simulationMode = SimulationMode.Script;
                yield return Until(() => SaveManager.IsInitialized && SaveManager.AreSaveSlotsReady, "transform demo initialization");
                PushDemoCylinder("Cylinder (17)");
                PushDemoCylinder("Cylinder (18)");
                yield return Await(SaveManager.Instance.SaveImmediateAsync(1));

                yield return new ExitPlayMode();
                yield return new RecompileScripts(expectScriptCompilation: false);
                yield return new EnterPlayMode();
                Assert.That(Application.isPlaying, Is.True);
                Physics.simulationMode = SimulationMode.Script;
                yield return Until(() => SaveManager.IsInitialized && SaveManager.AreSaveSlotsReady, "transform demo initialization");
                var manager = SaveManager.Instance;
                var first = manager.LoadSaveSlotAsync(1, restoreLastActiveScene: false);
                yield return Await(first);
                Assert.That(first.Result.Success, Is.True, first.Result.ErrorMessage);
                Assert.That(manager.ComponentManager.HasDeferredComponents, Is.True);
                yield return StreamAndCheckDemoCylinder("Cylinder (17)", 1);
                Assert.That(manager.ComponentManager.HasComponentDeserialized(
                    GameObject.Find("Cylinder (18)").GetComponent<RememberTransform>().UniqueIdentifier), Is.False);
                PushDemoCylinder("Cylinder (17)");
                yield return Await(manager.SaveImmediateAsync(2));

                GameObject.Find("Transform restore streaming origin").transform.position = Vector3.one * 1000;
                var second = manager.LoadSaveSlotAsync(2, restoreLastActiveScene: false);
                yield return Await(second);
                Assert.That(second.Result.Success, Is.True, second.Result.ErrorMessage);
                yield return StreamAndCheckDemoCylinder("Cylinder (17)", 2);
                // The untouched cylinder's still-deferred first-save pose must survive resaving.
                yield return StreamAndCheckDemoCylinder("Cylinder (18)", 1);

                yield return new ExitPlayMode();
                yield return new RecompileScripts(expectScriptCompilation: false);
                yield return new EnterPlayMode();
                Assert.That(Application.isPlaying, Is.True);
                Physics.simulationMode = SimulationMode.Script;
                yield return Until(() => SaveManager.IsInitialized && SaveManager.AreSaveSlotsReady, "transform demo initialization");
                manager = SaveManager.Instance;
                var restarted = manager.LoadSaveSlotAsync(2, restoreLastActiveScene: false);
                yield return Await(restarted);
                Assert.That(restarted.Result.Success, Is.True, restarted.Result.ErrorMessage);
                yield return StreamAndCheckDemoCylinder("Cylinder (17)", 2);
                yield return StreamAndCheckDemoCylinder("Cylinder (18)", 1);
                Assert.That(manager.ComponentManager.HasDeferredComponents, Is.False);
                yield return new ExitPlayMode();
                LogAssert.NoUnexpectedReceived();
            }
            finally { Physics.simulationMode = mode; }
        }

        static void PushDemoCylinder(string name)
        {
            var body = GameObject.Find(name).GetComponent<Rigidbody>();
            body.AddForceAtPosition(new Vector3(5.1f, 0, 1.3f), body.position + Vector3.up * 0.8f, ForceMode.Impulse);
            for (int i = 0; i < 35; i++) Physics.Simulate(0.02f);
            Assert.That(Quaternion.Angle(body.rotation, Quaternion.identity), Is.GreaterThan(1f));
        }

        static IEnumerator StreamAndCheckDemoCylinder(string name, int slot)
        {
            var manager = SaveManager.Instance;
            var helper = GameObject.Find(name).GetComponent<RememberTransform>();
            var saved = manager.LoadSaveDataForSlotAsync(slot);
            yield return Await(saved);
            var expected = manager.Serializer.Deserialize<TransformData>(saved.Result.ComponentsData[helper.UniqueIdentifier]);
            GameObject.Find("Transform restore streaming origin").transform.position = helper.transform.position;
            yield return Until(() => manager.ComponentManager.HasComponentDeserialized(helper.UniqueIdentifier), "streamed pose for " + name);
            Assert.That(Vector3.Distance(helper.transform.localPosition, expected.Position.Value), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(helper.transform.localRotation, expected.Rotation.Value), Is.LessThan(0.05f));
            Assert.That(helper.transform.localScale, Is.EqualTo(expected.Scale.Value));
            Assert.That(helper.GetComponent<Rigidbody>().isKinematic, Is.False);
            Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(helper.UniqueIdentifier), Is.False);
            Assert.That(Get<int>(helper, "retryCount"), Is.Zero);
        }
    }
}
#endif
