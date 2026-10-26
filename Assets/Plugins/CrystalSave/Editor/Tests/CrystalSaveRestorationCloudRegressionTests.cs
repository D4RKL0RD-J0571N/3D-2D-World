#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
  [UnityTest]
  public IEnumerator RestorationCloud_SupabaseOutagePreservesLocalMetadata()
  {
   using var server=new RestorationCloudServer();
   var cfg=RestorationCloudSettings(server.Url); cfg.keepLocalMirror=true;
   string root=Path.Combine(SessionState.GetString(SessionKey+".root",""),"supabase-outage");
   var storage=new SupabaseSaveSystem(cfg,root);
   var slot=new SaveSlot(1,"Existing generation",new DateTime(2025,1,1),"","SceneA");
   slot.CustomMetadata["score"]="100";
   var payload=SaveManager.Instance.Serializer.Serialize(new SaveData(cfg.version));
   LogAssert.ignoreFailingMessages=true;
   try
   {
    yield return Await(storage.SaveAsync(payload,slot));
    var files=Directory.GetFiles(root,"*.bin",SearchOption.AllDirectories);
    Assert.That(files.Length,Is.EqualTo(1),"precondition: local metadata committed");
    string metadata=files[0]; byte[] before=File.ReadAllBytes(metadata);
    server.FailReads=true;
    var read=storage.LoadSlotMetadataAsync(1); yield return Until(()=>read.IsCompleted,"metadata 503",10);
    TestContext.WriteLine($"taskFaulted={read.IsFaulted}, returnedSlot={(!read.IsFaulted&&read.Result!=null)}, metadataExists={File.Exists(metadata)}, payloadExists={File.Exists(Path.Combine(root,"Slot1.sav"))}");
    Assert.That(File.Exists(metadata),Is.True,"Transport failure cannot delete a committed local header");
    Assert.That(File.ReadAllBytes(metadata),Is.EqualTo(before));
    Assert.That(read.IsFaulted || read.Result!=null,Is.True,"An outage cannot be reported as an absent save");
   }
   finally { LogAssert.ignoreFailingMessages=false; Object.Destroy(cfg); }
  }
  [UnityTest]
  public IEnumerator RestorationCloud_SupabaseListingUsesProviderProtocol()
  {
   using var server=new RestorationCloudServer();
   var cfg=RestorationCloudSettings(server.Url);
   var storage=new SupabaseSaveSystem(cfg,Path.Combine(SessionState.GetString(SessionKey+".root",""),"supabase-list"));
   var slot=new SaveSlot(1,"Existing generation",new DateTime(2025,1,1),"","SceneA");
   try
   {
    yield return Await(storage.SaveAsync(new byte[]{1,2,3},slot));
    var read=storage.ListRemoteSlotsAsync(); yield return Until(()=>read.IsCompleted,"listing",10);
    TestContext.WriteLine($"method={server.ListMethod}, taskFaulted={read.IsFaulted}, slots={(!read.IsFaulted?read.Result.Count:-1)}");
    Assert.That(server.ListMethod,Is.EqualTo("POST"),"Supabase object/list takes a JSON POST, not a GET query");
    Assert.That(read.IsFaulted,Is.False,read.Exception?.ToString());
    Assert.That(read.Result.Select(s=>s.SlotNumber),Is.EquivalentTo(new[]{1}));
   }
   finally { Object.Destroy(cfg); }
  }
  [UnityTest]
  public IEnumerator RestorationCloud_SupabaseHealthyMetadataControl()
  {
   using var server=new RestorationCloudServer();
   var cfg=RestorationCloudSettings(server.Url);
   var storage=new SupabaseSaveSystem(cfg,Path.Combine(SessionState.GetString(SessionKey+".root",""),"supabase-control"));
   var slot=new SaveSlot(1,"雲 \"one\"",new DateTime(2025,1,1),"","SceneA");
   slot.CustomMetadata["chapter"]="山\\coast\nnext";slot.CustomMetadata["null"]=null;slot.CustomMetadata[""]="";
   try
   {
    yield return Await(storage.SaveAsync(new byte[]{1,2,3},slot));
    var read=storage.LoadSlotMetadataAsync(1); yield return Await(read);
    Assert.That(read.Result.SlotName,Is.EqualTo(slot.SlotName));
    Assert.That(read.Result.CustomMetadata,Is.EquivalentTo(slot.CustomMetadata));
   }
   finally { Object.Destroy(cfg); }
  }
  [UnityTest]
  public IEnumerator RestorationCloud_OutgoingDeferredHelperCannotOverwriteNewPendingState()
  {
   var manager=SaveManager.Instance;
   var outgoing=new GameObject("OutgoingDeferred");Composite(outgoing,typeof(RememberTransform));var oldHelper=ConfigureTransform(outgoing);
   GameObject incoming=null;
   try
   {
    outgoing.transform.localPosition=new Vector3(3,4,5);
    yield return null;yield return Await(manager.SaveImmediateAsync(1));
    var oldData=manager.Serializer.Deserialize<SaveData>(manager.Serializer.Serialize(manager.CurrentSaveData));
    string id=oldHelper.UniqueIdentifier, goId=oldHelper.GameObjectUniqueID;
    outgoing.SetActive(false);
    var deferred=manager.RestoreSingleGameObjectAsync(outgoing,oldData);yield return Await(deferred);
    Assert.That(deferred.Result,Is.True);
    Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(id),Is.True,"precondition: outgoing application is pending");
    incoming=new GameObject("IncomingReplacement");incoming.SetActive(false);Composite(incoming,typeof(RememberTransform));var newHelper=ConfigureTransform(incoming);
    incoming.GetComponent<UniqueID>().ID=goId;newHelper.ComponentID=oldHelper.ComponentID;Call(newHelper,"OverrideGameObjectID",goId);
    incoming.SetActive(true);yield return null;yield return null;
    Assert.That(newHelper.UniqueIdentifier,Is.EqualTo(id));
    incoming.transform.localPosition=new Vector3(9,8,7);
    var newData=new SaveData(manager.SaveSettings.version);newData.ComponentsData[id]=newHelper.SaveData();
    var restored=manager.RestoreSingleGameObjectAsync(incoming,newData);yield return Await(restored);
    Assert.That(restored.Result,Is.True);
    Assert.That(incoming.transform.localPosition,Is.EqualTo(new Vector3(9,8,7)));
    Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(id),Is.False,"precondition: new generation successfully applied");
    Object.Destroy(outgoing);yield return null;
    bool stalePending=PendingBytes(manager.ComponentManager).ContainsKey(id);
    incoming.SetActive(false);incoming.SetActive(true);yield return null;yield return null;
    TestContext.WriteLine($"stalePending={stalePending}, replacementPosition={incoming.transform.localPosition}");
    Assert.That(stalePending,Is.False,"A superseded outgoing instance cannot reinsert older pending bytes");
    Assert.That(incoming.transform.localPosition,Is.EqualTo(new Vector3(9,8,7)));
   }
   finally {if(outgoing)Object.Destroy(outgoing);if(incoming)Object.Destroy(incoming);}
  }
        [UnityTest]
        public IEnumerator RestorationCloud_ErrorStatusesAndRefreshPreserveMirrorGeneration()
        {
            using var server = new RestorationCloudServer();
            var cfg = RestorationCloudSettings(server.Url);
            cfg.keepLocalMirror = true;
            string root = Path.Combine(SessionState.GetString(SessionKey + ".root", ""), "mirror-errors");
            var storage = new SupabaseSaveSystem(cfg, root);
            var before = new SaveSlot(1, "Local A", new DateTime(2024, 1, 1), "", "A");
            before.CustomMetadata["generation"] = "A";
            try
            {
                yield return Await(storage.SaveAsync(new byte[] { 1 }, before));
                string metadata = Directory.GetFiles(root, "*.bin", SearchOption.AllDirectories).Single();
                byte[] original = File.ReadAllBytes(metadata);
                foreach (int status in new[] { 400, 401, 403, 429, 500, 503, 404 })
                {
                    server.ErrorStatus = status;
                    var read = storage.LoadSlotMetadataAsync(1);
                    yield return Until(() => read.IsCompleted, "metadata error " + status, 10);
                    Assert.That(read.IsFaulted, Is.EqualTo(status != 404), "HTTP " + status);
                    if (read.IsFaulted) Assert.That(read.Exception.GetBaseException(), Is.InstanceOf<IOException>());
                    else Assert.That(read.Result, Is.Null);
                    Assert.That(File.ReadAllBytes(metadata), Is.EqualTo(original), "HTTP " + status);
                }
                server.ErrorStatus = 0;
                cfg.keepLocalMirror = false;
                var remoteWriter = new SupabaseSaveSystem(cfg, root);
                var after = new SaveSlot(1, "Remote B", new DateTime(2025, 1, 1), "", "B");
                after.CustomMetadata["generation"] = "B";
                yield return Await(remoteWriter.SaveAsync(new byte[] { 2 }, after));
                cfg.keepLocalMirror = true;
                var refreshed = storage.LoadSlotMetadataAsync(1);
                yield return Await(refreshed);
                Assert.That(refreshed.Result.CustomMetadata["generation"], Is.EqualTo("B"));
                Assert.That(File.ReadAllBytes(metadata), Is.EqualTo(original));
                var candidates = storage.ReadCandidatesAsync(before, CancellationToken.None);
                yield return Await(candidates);
                Assert.That(candidates.Result.Local.Payload, Is.EqualTo(new byte[] { 1 }));
                Assert.That(candidates.Result.Local.Metadata.CustomMetadata["generation"], Is.EqualTo("A"));
                Assert.That(candidates.Result.Remote.Metadata.CustomMetadata["generation"], Is.EqualTo("B"));
            }
            finally { Object.Destroy(cfg); }
        }

        [UnityTest]
        public IEnumerator RestorationCloud_ListingPagesRelativePathsAndRefreshFailure()
        {
            using var server = new RestorationCloudServer();
            var cfg = RestorationCloudSettings(server.Url);
            var manager = SaveManager.Instance;
            var previousStorage = manager.SaveSystem;
            var previousService = Get<CloudSaveService>(manager, "cloudSaveService");
            var previousBackend = manager.SaveSettings.backend;
            bool previousCloud = manager.SaveSettings.enableCloudSave;
            var storage = new SupabaseSaveSystem(cfg, Path.Combine(SessionState.GetString(SessionKey + ".root", ""), "listing-pages"));
            var slot = new SaveSlot(1, "Remote page", DateTime.UtcNow.AddMinutes(1), "", "A");
            slot.CustomMetadata["score"] = "42";
            try
            {
                yield return Await(storage.SaveAsync(new byte[] { 3 }, slot));
                server.ListingExtraNames = Enumerable.Range(0, 1002).Select(i => "filler-" + i.ToString("D4")).ToArray();
                // An orphan header must not appear as a valid save.
                server.Objects["/storage/v1/object/test-bucket/users/guest/Slot2.meta.json"] =
                    Encoding.UTF8.GetBytes("{\"SlotNumber\":2,\"SlotName\":\"Orphan\",\"LastSavedTicks\":0}");
                var listing = storage.ListRemoteSlotsAsync();
                yield return Await(listing);
                Assert.That(server.Offsets.ToArray(), Is.EqualTo(new[] { 0, 1000 }));
                Assert.That(listing.Result.Select(value => value.SlotNumber), Is.EqualTo(new[] { 1 }));
                Assert.That(listing.Result[0].CustomMetadata["score"], Is.EqualTo("42"));
                Set(manager, "saveSystem", storage);
                Set(manager, "cloudSaveService", new CloudSaveService(manager));
                manager.SaveSettings.enableCloudSave = true;
                manager.SaveSettings.backend = SaveBackend.Supabase;
                yield return Await(manager.RefreshRemoteSlotsAsync());
                Assert.That(manager.GetSlotMetadata(1).SlotName, Is.EqualTo("Remote page"));
                server.ErrorStatus = 403;
                var failed = manager.RefreshRemoteSlotsAsync();
                yield return Until(() => failed.IsCompleted, "failed remote refresh", 10);
                Assert.That(failed.IsFaulted, Is.True);
                Assert.That(failed.Exception.GetBaseException(), Is.InstanceOf<IOException>());
                Assert.That(manager.GetSlotMetadata(1).SlotName, Is.EqualTo("Remote page"));
            }
            finally
            {
                Set(manager, "saveSystem", previousStorage);
                Set(manager, "cloudSaveService", previousService);
                manager.SaveSettings.enableCloudSave = previousCloud;
                manager.SaveSettings.backend = previousBackend;
                Object.Destroy(cfg);
            }
        }

        [UnityTest]
        public IEnumerator RestorationCloud_CancelledMetadataReadPreservesMirror()
        {
            using var server = new RestorationCloudServer();
            var cfg = RestorationCloudSettings(server.Url);
            cfg.keepLocalMirror = true;
            string root = Path.Combine(SessionState.GetString(SessionKey + ".root", ""), "cancelled-metadata");
            var storage = new SupabaseSaveSystem(cfg, root);
            using var cancellation = new CancellationTokenSource();
            try
            {
                yield return Await(storage.SaveAsync(new byte[] { 1 }, new SaveSlot(1, "Before", DateTime.UtcNow, "", "A")));
                string metadata = Directory.GetFiles(root, "*.bin", SearchOption.AllDirectories).Single();
                byte[] original = File.ReadAllBytes(metadata);
                server.ReadGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var scope = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveIoScope");
                Task<SaveSlot> read;
                using ((IDisposable)Call(scope, "WithReadCancellation", cancellation.Token))
                    read = storage.LoadSlotMetadataAsync(1);
                yield return Until(() => server.Reads > 0, "held metadata read", 10);
                cancellation.Cancel();
                yield return Until(() => read.IsCompleted, "cancelled metadata read", 10);
                Assert.That(read.IsCanceled, Is.True, read.Exception?.ToString());
                Assert.That(File.ReadAllBytes(metadata), Is.EqualTo(original));
                server.ReadGate.TrySetResult(true);
                yield return null;
                Assert.That(File.ReadAllBytes(metadata), Is.EqualTo(original));
            }
            finally { cancellation.Cancel(); server.ReadGate?.TrySetResult(true); Object.Destroy(cfg); }
        }

        [UnityTest]
        public IEnumerator RestorationCloud_LateSuccessCannotConsumeReplacementPendingBytes()
        {
            var manager = SaveManager.Instance;
            var outgoing = new GameObject("OutgoingLateSuccess");
            Composite(outgoing, typeof(RememberTransform));
            var oldHelper = ConfigureTransform(outgoing);
            GameObject incoming = null;
            try
            {
                outgoing.transform.localPosition = new Vector3(3, 4, 5);
                yield return null;
                yield return Await(manager.SaveImmediateAsync(1));
                var oldData = manager.CurrentSaveData;
                string id = oldHelper.UniqueIdentifier;
                outgoing.SetActive(false);
                yield return Await(manager.RestoreSingleGameObjectAsync(outgoing, oldData));
                incoming = new GameObject("IncomingPending");
                incoming.SetActive(false);
                Composite(incoming, typeof(RememberTransform));
                var newHelper = ConfigureTransform(incoming);
                incoming.GetComponent<UniqueID>().ID = oldHelper.GameObjectUniqueID;
                newHelper.ComponentID = oldHelper.ComponentID;
                Call(newHelper, "OverrideGameObjectID", oldHelper.GameObjectUniqueID);
                incoming.transform.localPosition = new Vector3(9, 8, 7);
                byte[] newer = newHelper.SaveData();
                var newData = new SaveData(manager.SaveSettings.version);
                newData.ComponentsData[id] = newer;
                yield return Await(manager.RestoreSingleGameObjectAsync(incoming, newData));
                // Drive the old application phase without registering it again through OnEnable.
                Call(oldHelper, "LateUpdate");
                var pending = PendingBytes(manager.ComponentManager);
                Assert.That(pending.ContainsKey(id), Is.True);
                Assert.That(pending[id], Is.SameAs(newer));
                incoming.transform.localPosition = Vector3.zero;
                incoming.SetActive(true);
                yield return null; yield return null;
                Assert.That(incoming.transform.localPosition, Is.EqualTo(new Vector3(9, 8, 7)));
            }
            finally { Object.Destroy(outgoing); if (incoming) Object.Destroy(incoming); }
        }

        [UnityTest]
        public IEnumerator RestorationCloud_DispatcherDoesNotHoldProducerLockOrDrainNewWork()
        {
            var dispatcher = UnityMainThreadDispatcher.Instance();
            bool producerCompleted = false;
            int firstFrame = -1, nextFrame = -1;
            Task producer = null;
            dispatcher.Enqueue(() =>
            {
                firstFrame = Time.frameCount;
                producer = Task.Run(() => dispatcher.Enqueue(() => nextFrame = Time.frameCount));
                // A bounded wait proves that enqueuing from another thread does not
                // require the callback's own execution lock. Never wait for execution.
                producerCompleted = producer.Wait(TimeSpan.FromSeconds(1));
            });
            yield return Until(() => nextFrame >= 0, "dispatcher callback", 5);
            Assert.That(producerCompleted, Is.True);
            Assert.That(producer.IsCompleted, Is.True);
            Assert.That(nextFrame, Is.GreaterThan(firstFrame));
        }

        [UnityTest]
        public IEnumerator RestorationCloud_SynchronousCloudCallsRejectBeforeNetworkWork()
        {
            using var server = new RestorationCloudServer();
            var cfg = RestorationCloudSettings(server.Url);
            cfg.firebaseBucket = "test-bucket";
            cfg.mySqlApiUrl = server.Url;
            string root = Path.Combine(SessionState.GetString(SessionKey + ".root", ""), "sync-cloud");
            var slot = new SaveSlot(1, "Test", DateTime.UtcNow, "", "A");
            try
            {
                var supabase = new SupabaseSaveSystem(cfg, root);
                var firebase = new FirebaseSaveSystem(cfg, root);
                Set(firebase, "url", server.Url);
                var mysql = new MySqlSaveSystem(cfg, root);
                foreach (var storage in new ISaveSystem[] { supabase, firebase, mysql })
                    Assert.Throws<NotSupportedException>(() => storage.Load(slot));
                foreach (var storage in new ISaveSystem[] { firebase, mysql })
                {
                    Assert.Throws<NotSupportedException>(() => storage.Save(new byte[] { 1 }, slot));
                    Assert.Throws<NotSupportedException>(() => storage.Delete(slot));
                }
                yield return null;
                Assert.That(server.Reads, Is.Zero);
                Assert.That(server.Objects, Is.Empty);
            }
            finally { Object.Destroy(cfg); }
        }

  static SaveSettings RestorationCloudSettings(string url)
  {
   var cfg=ScriptableObject.CreateInstance<SaveSettings>();cfg.enableCloudSave=true;cfg.keepLocalMirror=false;
   cfg.supabaseUrl=url;cfg.supabaseAnonKey="test-key";cfg.bucket="test-bucket";cfg.saveFileName="Slot";
   cfg.saveMethod=SaveMethod.BinaryFileFormat;cfg.userFolderStrategy=UserFolderStrategy.Custom;return cfg;
  }
        sealed class RestorationCloudServer : IDisposable
        {
            readonly HttpListener listener = new();
            readonly Task loop;
            internal readonly string Url;
            internal readonly ConcurrentDictionary<string, byte[]> Objects = new();
            internal readonly ConcurrentQueue<int> Offsets = new();
            internal volatile int ErrorStatus;
            internal volatile int Reads;
            internal string ListMethod;
            internal string[] ListingExtraNames = Array.Empty<string>();
            internal TaskCompletionSource<bool> ReadGate;
            internal bool FailReads { set => ErrorStatus = value ? 503 : 0; }

            internal RestorationCloudServer()
            {
                var ports = new TcpListener(IPAddress.Loopback, 0);
                ports.Start();
                int port = ((IPEndPoint)ports.LocalEndpoint).Port;
                ports.Stop();
                Url = "http://127.0.0.1:" + port;
                listener.Prefixes.Add(Url + "/");
                listener.Start();
                loop = Task.Run(async () =>
                {
                    try
                    {
                        while (listener.IsListening)
                        {
                            var context = await listener.GetContextAsync();
                            string path = Uri.UnescapeDataString(context.Request.Url.AbsolutePath);
                            bool listing = path.Contains("/object/list/");
                            bool read = listing || context.Request.HttpMethod == "GET";
                            byte[] response;
                            if (read)
                            {
                                Interlocked.Increment(ref Reads);
                                if (ReadGate != null) await Task.WhenAny(ReadGate.Task, Task.Delay(TimeSpan.FromSeconds(5)));
                            }
                            if (read && ErrorStatus != 0)
                            {
                                context.Response.StatusCode = ErrorStatus;
                                response = Encoding.UTF8.GetBytes("{\"error\":\"Injected provider failure\"}");
                            }
                            else if (listing)
                            {
                                ListMethod = context.Request.HttpMethod;
                                if (ListMethod != "POST")
                                {
                                    context.Response.StatusCode = 404;
                                    response = Encoding.UTF8.GetBytes("{\"error\":\"Route not found\"}");
                                }
                                else
                                {
                                    using var reader = new StreamReader(context.Request.InputStream);
                                    var request = JObject.Parse(await reader.ReadToEndAsync());
                                    string prefix = (string)request["prefix"];
                                    int offset = (int)request["offset"], limit = (int)request["limit"];
                                    if (prefix != "users/guest/" || limit < 1 || offset < 0 ||
                                        (string)request["sortBy"]?["column"] != "name" ||
                                        (string)request["sortBy"]?["order"] != "asc")
                                        throw new InvalidDataException("Invalid listing request body");
                                    Offsets.Enqueue(offset);
                                    string objectPrefix = "/storage/v1/object/test-bucket/" + prefix;
                                    var names = Objects.Keys.Where(key => key.StartsWith(objectPrefix, StringComparison.Ordinal))
                                        .Select(key => key.Substring(objectPrefix.Length))
                                        .Concat(ListingExtraNames).Distinct().OrderBy(name => name, StringComparer.Ordinal)
                                        .Skip(offset).Take(limit);
                                    response = Encoding.UTF8.GetBytes(new JArray(names.Select(name => new JObject { ["name"] = name })).ToString());
                                }
                            }
                            else if (context.Request.HttpMethod == "POST" || context.Request.HttpMethod == "PUT")
                            {
                                using var body = new MemoryStream();
                                await context.Request.InputStream.CopyToAsync(body);
                                Objects[path] = body.ToArray();
                                response = Encoding.UTF8.GetBytes("{}");
                            }
                            else if (!Objects.TryGetValue(path, out response))
                            {
                                context.Response.StatusCode = 404;
                                response = Array.Empty<byte>();
                            }
                            context.Response.ContentType = "application/json";
                            context.Response.ContentLength64 = response.Length;
                            await context.Response.OutputStream.WriteAsync(response, 0, response.Length);
                            context.Response.Close();
                        }
                    }
                    catch (HttpListenerException) { }
                    catch (ObjectDisposedException) { }
                });
            }
            public void Dispose()
            {
                ReadGate?.TrySetResult(true);
                listener.Stop();
                listener.Close();
                if (loop.IsFaulted) throw new InvalidOperationException("Loopback fixture failed", loop.Exception);
            }
        }

 }
}
#endif
