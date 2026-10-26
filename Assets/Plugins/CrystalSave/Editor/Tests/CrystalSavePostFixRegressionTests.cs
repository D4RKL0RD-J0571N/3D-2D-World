#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;
namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator PostFixRegression_DestroyedSceneFailureIsReported() => PostFixRegression_DestroyedScene(true);
        [UnityTest] public IEnumerator PostFixRegression_DestroyedSceneHealthyControl() => PostFixRegression_DestroyedScene(false);
        IEnumerator PostFixRegression_DestroyedScene(bool fail)
        {
            var manager = SaveManager.Instance;
            var go = new GameObject("DestroyedSceneProbe"); Composite(go, typeof(FeatureProbe), typeof(RememberGameObject));
            var probe = go.GetComponent<FeatureProbe>(); probe.Value = 7;
            string id = go.GetComponent<UniqueID>().ID;
            int notifications = 0; Action<string> listener = _ => notifications++;
            manager.OnGameObjectRestored += listener;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return null; yield return Await(manager.SaveImmediateAsync(1));
                manager.RegisterGameObject(go, go.GetComponent<RememberGameObject>().PropertySettings);
                manager.DestroyWithSnapshot(go, destroy: false);
                Assert.That(go.activeSelf, Is.False);
                Assert.That(manager.CurrentSaveData.DestroyedObjectData.ContainsKey(id), Is.True, "precondition: snapshot");
                probe.Value = 99;
                if (fail) probe.Loaded = () => throw new InvalidOperationException("REVIEW_DESTROYED_RESTORE_FAILURE");
                var task = manager.RestoreDestroyedGameObjectFromCurrentDataAsync(id, 1, 0);
                yield return Await(task);
                TestContext.WriteLine($"result={task.Result}, notifications={notifications}, retainedSnapshot={manager.CurrentSaveData.DestroyedObjectData.ContainsKey(id)}, value={probe.Value}");
                Assert.That(probe.Loads, Is.GreaterThan(0), "precondition: component restoration attempted");
                Assert.That(task.Result, Is.EqualTo(!fail), "Component outcome must determine the public result");
                Assert.That(notifications, Is.EqualTo(fail ? 0 : 1));
                if (fail) Assert.That(manager.CurrentSaveData.DestroyedObjectData.ContainsKey(id), Is.True);
                else Assert.That(probe.Value, Is.EqualTo(7));
            }
            finally { manager.OnGameObjectRestored -= listener; Object.Destroy(go); LogAssert.ignoreFailingMessages = false; }
        }
        [UnityTest]
        public IEnumerator PostFixRegression_UnknownDestroyedPrefabDoesNotReportSuccess()
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveImmediateAsync(1));
            var task = manager.RestoreDestroyedPrefabWithRetryAsync("not-a-saved-prefab", 1, 1, 0);
            yield return Await(task);
            Assert.That(task.Result, Is.False, "A valid slot is not evidence that the requested prefab was restored");
        }
        [UnityTest]
        public IEnumerator PostFixRegression_NeverActivatedChildDeletionIsSaved()
        {
            var manager = SaveManager.Instance;
            var asset = Resources.Load<GameObject>("CrystalSaveLifecyclePrefab");
            var go = Object.Instantiate(asset); // authored fixture root is inactive
            var prefab = go.GetComponent<SaveablePrefab>();
            var child = go.transform.Find("State"); child.gameObject.SetActive(false);
            prefab.TrackChildStateOverrides = true; prefab.SetOriginalPrefabAsset(asset);
            go.SetActive(true);
            SaveablePrefab fresh = null;
            try
            {
                yield return null;
                Assert.That(child.GetComponent("DestroyObserver"), Is.Not.Null, "precondition: tracking observer attached");
                Object.Destroy(child.gameObject); yield return null;
                yield return Await(manager.SaveImmediateAsync(1));
                var data = manager.Serializer.Deserialize<SaveData>(manager.SaveSystem.Load(manager.GetSlotMetadata(1)));
                var bytes = data.Prefabs.Single(p => p.InstanceID == prefab.UniqueID).RuntimeModificationData;
                var mods = bytes == null ? null : manager.Serializer.Deserialize<RuntimeModificationData>(bytes);
                fresh = Spawn(); fresh.TrackChildStateOverrides = true;
                if (bytes != null) fresh.ApplyRuntimeModifications(bytes);
                yield return null;
                TestContext.WriteLine($"savedDeletion={mods?.ChildStates.Any(c => c.Path == "State" && !c.Exists) ?? false}, childExistsAfterRestore={fresh.transform.Find("State") != null}");
                Assert.That(fresh.transform.Find("State"), Is.Null, "A fresh instance must not resurrect the deleted child");
                Assert.That(mods?.ChildStates.Any(c => c.Path == "State" && !c.Exists) ?? false, Is.True,
                    "Deleting a never-activated authored child must persist its absence");
            }
            finally { Object.Destroy(go); if (fresh) Object.Destroy(fresh.gameObject); }
        }
        [UnityTest]
        public IEnumerator PostFixRegression_RepeatedTargetedPrefabControl() => PostFixRegression_TargetedPrefab(true);
        [UnityTest]
        public IEnumerator PostFixRegression_TargetedPrefabWithoutBlobsRestoresComponents() => PostFixRegression_TargetedPrefab(false);
        IEnumerator PostFixRegression_TargetedPrefab(bool blobs)
        {
            var manager = SaveManager.Instance; var prefab = Spawn(); prefab.TrackComponentBlobs = blobs;
            var child = prefab.transform.Find("State"); var saved = new Vector3(3,4,5); child.localPosition = saved;
            try
            {
                yield return null; yield return Await(manager.SaveImmediateAsync(1));
                var helper = child.GetComponent<RememberTransform>();
                if (!blobs) Assert.That(manager.CurrentSaveData.ComponentsData.ContainsKey(helper.UniqueIdentifier), Is.True,
                    "precondition: helper was captured globally under its instance identity");
                for (int i = 0; i < 2; i++)
                {
                    child.localPosition = Vector3.one * 99;
                    var task = manager.RestoreSingleGameObjectFromCurrentDataAsync(prefab.gameObject, 1, 0);
                    yield return Await(task); Assert.That(task.Result, Is.True);
                    TestContext.WriteLine($"blobs={blobs}, helper={helper.UniqueIdentifier}, position={child.localPosition}, success={task.Result}");
                    Assert.That(Vector3.Distance(child.localPosition, saved), Is.LessThan(0.001f));
                }
            }
            finally { Object.Destroy(prefab.gameObject); }
        }
        [UnityTest]
        public IEnumerator PostFixRegression_MissingReferenceFailureMustFailLoad()
        {
            var manager = SaveManager.Instance;
            var go = new GameObject("MissingReferenceReceiver"); Composite(go, typeof(FeatureProbe));
            var probe = go.GetComponent<FeatureProbe>(); probe.Value = 7;
            var dependency = new GameObject("RequiredDependency");
            try
            {
                yield return null; yield return Await(manager.SaveImmediateAsync(1));
                Object.Destroy(dependency); yield return null;
                bool callbackReached = false;
                probe.Loaded = () => { callbackReached = true; string name = dependency.name; };
                LogAssert.Expect(LogType.Error, "LoadStateMachine: Load process for slot 1 failed.");
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("HandleLoadCompletion: Load operation for slot 1 failed with error: Restoration failed"));
                var task = manager.LoadSaveSlotAsync(1); yield return Await(task);
                Assert.That(callbackReached, Is.True, "precondition: required deserializer ran");
                Assert.That(probe, Is.Not.Null, "receiver still exists; its dependency was destroyed");
                TestContext.WriteLine($"success={task.Result.Success}, loads={probe.Loads}, error={task.Result.ErrorMessage}, pending={PendingBytes(manager.ComponentManager).ContainsKey(probe.UniqueIdentifier)}");
                Assert.That(task.Result.Success, Is.False, "A MissingReferenceException from required restore is still a failure");
            }
            finally { Object.Destroy(go); if (dependency) Object.Destroy(dependency); }
        }
        [UnityTest]
        public IEnumerator PostFixRegression_DestroyedRestoreWaitsForCoordinator()
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var go = new GameObject("QueuedDestroyedScene"); Composite(go, typeof(FeatureProbe), typeof(RememberGameObject));
            yield return null; yield return Await(manager.SaveImmediateAsync(1));
            string id = go.GetComponent<UniqueID>().ID;
            manager.RegisterGameObject(go, go.GetComponent<RememberGameObject>().PropertySettings);
                manager.DestroyWithSnapshot(go, destroy: false);
            Assert.That(manager.CurrentSaveData.DestroyedObjectData.ContainsKey(id), Is.True);
            var gate = new TaskCompletionSource<SaveConflictCandidates>();
            var storage = new PostReviewStorage(original) { CandidateGate = gate.Task };
            manager.SaveSettings.enableCloudSave = true; manager.SaveSettings.backend = SaveBackend.MySQL;
            Set(manager, "saveSystem", storage);
            using var cancellation = new CancellationTokenSource();
            try
            {
                var save = manager.SaveAsync(1, null, cancellation.Token);
                yield return Until(() => storage.LocalReads > 0, "save holds coordinator");
                var restore = manager.RestoreDestroyedGameObjectFromCurrentDataAsync(id, 1, 0);
                yield return null;
                bool completedWhileSaving = restore.IsCompleted;
                bool activeWhileSaving = go.activeSelf;
                cancellation.Cancel(); yield return Until(() => save.IsCompleted, "cancel held save");
                gate.SetResult(new SaveConflictCandidates(null, null));
                yield return Await(restore);
                TestContext.WriteLine($"completedWhileSaving={completedWhileSaving}, activeWhileSaving={activeWhileSaving}");
                Assert.That(completedWhileSaving, Is.False, "Destroyed restoration must wait for its turn");
                Assert.That(activeWhileSaving, Is.False, "Queued work cannot mutate the scene before it starts");
            }
            finally
            {
                cancellation.Cancel(); gate.TrySetResult(new SaveConflictCandidates(null, null));
                manager.SaveSettings.enableCloudSave = false; Set(manager, "saveSystem", original); Object.Destroy(go);
            }
        }
        [UnityTest] public IEnumerator PostFixRegression_SupabaseMetadataRoundTrip() => PostFixRegression_MetadataRoundTrip(false);
        [UnityTest] public IEnumerator PostFixRegression_FirebaseMetadataRoundTrip() => PostFixRegression_MetadataRoundTrip(true);
        IEnumerator PostFixRegression_MetadataRoundTrip(bool firebase)
        {
            using var server = new PostFixRegressionServer();
            var settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = true; settings.keepLocalMirror = false;
            settings.supabaseUrl = server.Url; settings.supabaseAnonKey = "test-key"; settings.bucket = "test-bucket";
            settings.firebaseBucket = "test-bucket"; settings.firebaseIdToken = "test-token";
            settings.saveFileName = "Slot"; settings.userFolderStrategy = UserFolderStrategy.Custom;
            string root = Path.Combine(SessionState.GetString(SessionKey + ".root", ""), firebase ? "firebase" : "supabase");
            ISaveSystem storage;
            if (firebase) { storage = new FirebaseSaveSystem(settings, root); Set(storage, "url", server.Url); }
            else storage = new SupabaseSaveSystem(settings, root);
            var slot = new SaveSlot(1, "Cloud generation", new DateTime(2025,1,1), "", "SceneA");
            slot.CustomMetadata["score"] = "100"; slot.CustomMetadata["chapter"] = "山 and coast";
            byte[] payload = SaveManager.Instance.Serializer.Serialize(new SaveData(settings.version) { LastSaved = slot.LastSaved });
            try
            {
                yield return Await(storage.SaveAsync(payload, slot));
                var read = ((ISaveConflictSource)storage).ReadCandidatesAsync(slot, CancellationToken.None);
                yield return Await(read);
                TestContext.WriteLine("Uploaded metadata: " + Encoding.UTF8.GetString(server.Metadata ?? Array.Empty<byte>()));
                Assert.That(read.Result.Remote, Is.Not.Null);
                Assert.That(read.Result.Remote.Metadata, Is.Not.Null);
                Assert.That(read.Result.Remote.Metadata.SlotName, Is.EqualTo(slot.SlotName), "ordinary metadata control");
                Assert.That(read.Result.Remote.Payload, Is.EqualTo(payload), "payload control");
                Assert.That(read.Result.Remote.Metadata.CustomMetadata.ContainsKey("score"), Is.True,
                    "Custom metadata must survive remote persistence, not only an in-memory fake backend");
                Assert.That(read.Result.Remote.Metadata.CustomMetadata["score"], Is.EqualTo("100"));
            }
            finally { Object.Destroy(settings); }
        }
        sealed class PostFixRegressionServer : IDisposable
        {
            readonly HttpListener listener = new HttpListener();
            readonly Task loop;
            internal readonly string Url;
            internal byte[] Metadata, Payload;
            internal PostFixRegressionServer()
            {
                var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
                int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
                Url = "http://127.0.0.1:" + port;
                listener.Prefixes.Add(Url + "/"); listener.Start();
                loop = Task.Run(async () =>
                {
                    try { while (listener.IsListening)
                    {
                        var context = await listener.GetContextAsync();
                        bool metadata = Uri.UnescapeDataString(context.Request.RawUrl).Contains(".meta.json");
                        byte[] response;
                        if (context.Request.HttpMethod == "POST" || context.Request.HttpMethod == "PUT")
                        {
                            using var body = new MemoryStream(); await context.Request.InputStream.CopyToAsync(body);
                            if (metadata) Metadata = body.ToArray(); else Payload = body.ToArray();
                            response = Encoding.UTF8.GetBytes("{}");
                        }
                        else response = metadata ? Metadata : Payload;
                        if (response == null) { context.Response.StatusCode = 404; response = Array.Empty<byte>(); }
                        context.Response.ContentLength64 = response.Length;
                        await context.Response.OutputStream.WriteAsync(response, 0, response.Length);
                        context.Response.Close();
                    }} catch (HttpListenerException) {} catch (ObjectDisposedException) {}
                });
            }
            public void Dispose() { listener.Stop(); listener.Close(); }
        }
    }
}
#endif
