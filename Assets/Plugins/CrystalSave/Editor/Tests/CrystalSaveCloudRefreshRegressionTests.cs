#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
        [UnityTest]
        public IEnumerator CloudRefresh_FirebaseObjectNameIsOneEncodedSegment()
        {
            using var server = new CloudRefreshServer("{}");
            var cfg = RestorationCloudSettings(server.Url);
            cfg.firebaseBucket = "test-bucket";
            var backend = new FirebaseSaveSystem(cfg, CloudRefreshRoot("encoded"));
            Set(backend, "url", server.Url);
            var slot = new SaveSlot(1, "Firebase", DateTime.UtcNow, "", "A");
            try
            {
                yield return Await(backend.SaveAsync(new byte[] { 7, 8 }, slot));
                Assert.That(server.Payload, Is.EqualTo(new byte[] { 7, 8 }), "upload control");
                var read = backend.LoadAsync(slot);
                yield return Await(read);
                TestContext.WriteLine("GET: " + server.ObjectGet);
                Assert.That(read.Result, Is.EqualTo(new byte[] { 7, 8 }), "Provider object URLs require slashes inside the name to be percent-encoded");
            }
            finally { Object.Destroy(cfg); }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_FirebaseEmptyListingIsValid()
        {
            using var server = new CloudRefreshServer("{}");
            var cfg = RestorationCloudSettings(server.Url);
            var backend = new FirebaseSaveSystem(cfg, CloudRefreshRoot("empty"));
            Set(backend, "url", server.Url);
            try
            {
                var read = backend.ListRemoteSlotsAsync();
                yield return Until(() => read.IsCompleted, "empty list", 10);
                Assert.That(read.IsFaulted, Is.False, read.Exception?.ToString());
                Assert.That(read.Result, Is.Empty);
            }
            finally { Object.Destroy(cfg); }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_FirebaseListingFollowsNextPageToken()
        {
            using var server = new CloudRefreshServer("{\"items\":[],\"nextPageToken\":\"page-two\"}");
            var cfg = RestorationCloudSettings(server.Url);
            var backend = new FirebaseSaveSystem(cfg, CloudRefreshRoot("pages"));
            Set(backend, "url", server.Url);
            try
            {
                var read = backend.ListRemoteSlotsAsync();
                yield return Await(read);
                TestContext.WriteLine("Listing requests: " + server.ListRequests);
                Assert.That(server.ListRequests, Is.EqualTo(2), "An empty first page can still have a next page");
            }
            finally { Object.Destroy(cfg); }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_CloudRefreshDoesNotResurrectClearedMetadata()
        {
            using var server = new RestorationCloudServer();
            var cfg = RestorationCloudSettings(server.Url);
            var manager = SaveManager.Instance;
            var oldStorage = manager.SaveSystem;
            var oldService = Get<CloudSaveService>(manager, "cloudSaveService");
            var oldBackend = manager.SaveSettings.backend;
            bool oldCloud = manager.SaveSettings.enableCloudSave;
            var backend = new SupabaseSaveSystem(cfg, CloudRefreshRoot("merge"));
            var current = manager.GetSlotMetadata(1);
            current.LastSaved = DateTime.UtcNow.AddHours(-1);
            current.CustomMetadata["deleted-key"] = "old-value";
            current.ScreenshotFileName = "old-shot.png";
            // Manager maintains a separate published list; synchronize this fixture explicitly.
            Call(manager, "SyncSlotToInternalList", current);
            var remote = new SaveSlot(1, "New generation", DateTime.UtcNow, "", "A");
            try
            {
                yield return Await(backend.SaveAsync(new byte[] { 1 }, remote));
                Set(manager, "saveSystem", backend);
                Set(manager, "cloudSaveService", new CloudSaveService(manager));
                manager.SaveSettings.enableCloudSave = true;
                manager.SaveSettings.backend = SaveBackend.Supabase;
                yield return Await(manager.RefreshRemoteSlotsAsync());
                var actual = manager.GetSlotMetadata(1);
                TestContext.WriteLine("name=" + actual.SlotName + ", metadata=" + actual.CustomMetadata.Count + ", screenshot=" + actual.ScreenshotFileName);
                Assert.That(actual.SlotName, Is.EqualTo("New generation"), "newer remote generation selected");
                Assert.That(actual.CustomMetadata, Is.Empty, "Cleared metadata cannot come back from the previous generation");
                Assert.That(actual.ScreenshotFileName, Is.Empty);
            }
            finally
            {
                Set(manager, "saveSystem", oldStorage);
                Set(manager, "cloudSaveService", oldService);
                manager.SaveSettings.backend = oldBackend;
                manager.SaveSettings.enableCloudSave = oldCloud;
                Object.Destroy(cfg);
            }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_FailedForceRefreshPreservesPublishedSlots()
        {
            var manager = SaveManager.Instance;
            var oldStorage = manager.SaveSystem;
            var original = manager.GetSlotMetadata(1);
            original.SlotName = "Published original";
            original.LastSaved = DateTime.UtcNow.AddHours(-1);
            var next = new SaveSlot(1, "Uncommitted refresh", DateTime.UtcNow, "", "A");
            Set(manager, "saveSystem", new CloudRefreshMetadataBackend(oldStorage, next));
            try
            {
                var refresh = manager.ForceRefreshSlotsAsync();
                yield return Until(() => refresh.IsCompleted, "failed refresh", 10);
                Assert.That(refresh.IsFaulted, Is.True, "second slot's read fails");
                TestContext.WriteLine(refresh.Exception.GetBaseException().Message);
                Assert.That(manager.GetSlotMetadata(1), Is.SameAs(original), "A failed refresh must not publish a partial candidate");
            }
            finally { Set(manager, "saveSystem", oldStorage); }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_ExistingSupabaseRefreshWithInitializedServiceControl()
        {
            var manager = SaveManager.Instance;
            var previous = Get<CloudSaveService>(manager, "cloudSaveService");
            try
            {
                Set(manager, "cloudSaveService", new CloudSaveService(manager));
                yield return RestorationCloud_ListingPagesRelativePathsAndRefreshFailure();
            }
            finally { Set(manager, "cloudSaveService", previous); }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_FirebaseMetadataScreenshotDeleteAndInitialization()
        {
            using var server = new CloudRefreshServer();
            var cfg = RestorationCloudSettings(server.Url);
            cfg.backend = SaveBackend.Firebase;
            cfg.saveFileName = "雲 #+%_";
            var storage = new FirebaseSaveSystem(cfg, CloudRefreshRoot("firebase-roundtrip"));
            Set(storage, "url", server.Url);
            var slot = new SaveSlot(1, "Unicode generation", DateTime.UtcNow, "山 #+%.png", "A");
            slot.CustomMetadata["chapter"] = "山\n\"one\"";
            try
            {
                yield return Await(storage.SaveAsync(new byte[] { 7, 8 }, slot));
                var read = storage.ReadCandidatesAsync(slot, CancellationToken.None);
                yield return Await(read);
                Assert.That(read.Result.Remote.Metadata.CustomMetadata, Is.EquivalentTo(slot.CustomMetadata));
                yield return Await(storage.UploadScreenshotAsync(slot.ScreenshotFileName, new byte[] { 3, 4, 5 }, ScreenshotFormat.PNG));
                var shot = storage.DownloadScreenshotAsync(slot.ScreenshotFileName);
                yield return Await(shot);
                Assert.That(shot.Result, Is.EqualTo(new byte[] { 3, 4, 5 }));

                // A slot header owns one safe local filename. The lower-level provider
                // API also supports nested remote keys and must retain their raw identity.
                const string nestedKey = "shots/山 #+%.png";
                Assert.That(ScreenshotManager.IsValidScreenshotFileName(slot.ScreenshotFileName), Is.True);
                Assert.That(ScreenshotManager.IsValidScreenshotFileName(nestedKey), Is.False);
                yield return Await(storage.UploadScreenshotAsync(nestedKey, new byte[] { 9, 8, 7 }, ScreenshotFormat.PNG));
                var nested = storage.DownloadScreenshotAsync(nestedKey);
                yield return Await(nested);
                Assert.That(nested.Result, Is.EqualTo(new byte[] { 9, 8, 7 }));
                Assert.That(server.Objects.ContainsKey("users/guest/" + nestedKey), Is.True);
                yield return Await(storage.DeleteScreenshotAsync(nestedKey));
                Assert.That(server.Objects.ContainsKey("users/guest/" + nestedKey), Is.False);
                Assert.That(server.Objects.ContainsKey("users/guest/" + slot.ScreenshotFileName), Is.True,
                    "Deleting an explicit remote key cannot normalize it to a different screenshot filename.");

                var slots = new SaveSlotManager(storage, cfg, null, CloudRefreshRoot("firebase-roundtrip"));
                yield return Await(slots.InitializeAsync(1));
                Assert.That(slots.GetByNumber(1).SlotName, Is.EqualTo(slot.SlotName), "Firebase does not require a Unity Authentication session");
                yield return Await(storage.DeleteAsync(slot));
                Assert.That(server.Objects, Is.Empty, "Remote payload, metadata and screenshot must all be deleted");
                var missing = storage.LoadAsync(slot); yield return Await(missing);
                Assert.That(missing.Result, Is.Null);
            }
            finally { Object.Destroy(cfg); }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_FirebasePaginatedDiscoveryAndFailures()
        {
            using var server = new CloudRefreshServer();
            var cfg = RestorationCloudSettings(server.Url);
            var storage = new FirebaseSaveSystem(cfg, CloudRefreshRoot("firebase-pages"));
            Set(storage, "url", server.Url);
            try
            {
                for (int number = 1; number <= 3; number++)
                    yield return Await(storage.SaveAsync(new byte[] { (byte)number }, new SaveSlot(number, "Remote " + number, DateTime.UtcNow, "", "A")));
                server.Objects["users/guest/Slot4.meta.json"] = Encoding.UTF8.GetBytes("{\"SlotNumber\":4}");
                var list = storage.ListRemoteSlotsAsync(); yield return Await(list);
                Assert.That(list.Result.Select(slot => slot.SlotNumber), Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(server.ListRequests, Is.EqualTo(4));
                foreach (int code in new[] { 400, 401, 403, 404, 429, 503 })
                {
                    server.ListStatus = code;
                    var failed = storage.ListRemoteSlotsAsync();
                    yield return Until(() => failed.IsCompleted, "Firebase list failure", 10);
                    Assert.That(failed.IsFaulted, Is.True, "HTTP " + code);
                    Assert.That(failed.Exception.GetBaseException(), Is.InstanceOf<IOException>());
                }
                server.ListStatus = 0;
                var retry = storage.ListRemoteSlotsAsync(); yield return Await(retry);
                Assert.That(retry.Result.Count, Is.EqualTo(3));
            }
            finally { Object.Destroy(cfg); }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_FirebaseRejectsMalformedMetadataAndRetainsLegacyDefaults()
        {
            using var server = new CloudRefreshServer();
            var cfg = RestorationCloudSettings(server.Url);
            var storage = new FirebaseSaveSystem(cfg, CloudRefreshRoot("invalid-meta"));
            Set(storage, "url", server.Url);
            var slot = new SaveSlot(1, "Control", DateTime.UtcNow, "", "A");
            try
            {
                yield return Await(storage.SaveAsync(new byte[] { 7 }, slot));
                foreach (string json in new[] { "", "{}", "{\"SlotNumber\":2}", "{\"SlotNumber\":1,\"LastSavedTicks\":-1}",
                    "{\"SlotNumber\":1,\"CustomMetadata\":[{\"Key\":\"a\",\"Value\":\"1\"},{\"Key\":\"a\",\"Value\":\"2\"}]}" })
                {
                    server.Objects["users/guest/Slot1.meta.json"] = Encoding.UTF8.GetBytes(json);
                    var metadata = storage.LoadSlotMetadataAsync(1);
                    yield return Until(() => metadata.IsCompleted, "invalid header", 10);
                    Assert.That(metadata.IsFaulted, Is.True, json);
                    Assert.That(metadata.Exception.GetBaseException(), Is.InstanceOf<InvalidDataException>());
                    var conflict = storage.ReadCandidatesAsync(slot, CancellationToken.None);
                    yield return Until(() => conflict.IsCompleted, "invalid conflict header", 10);
                    Assert.That(conflict.IsFaulted, Is.True, json);
                    _ = conflict.Exception;
                }
                server.Objects["users/guest/Slot1.meta.json"] = Encoding.UTF8.GetBytes("{\"SlotNumber\":1,\"SlotName\":\"Legacy\"}");
                var legacy = storage.LoadSlotMetadataAsync(1); yield return Await(legacy);
                Assert.That(legacy.Result.CustomMetadata, Is.Empty);
                Assert.That(legacy.Result.LastSaved, Is.EqualTo(DateTime.MinValue));
            }
            finally { Object.Destroy(cfg); }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_RemoteFailureCannotPublishPreparedHeaders()
        {
            using var server = new CloudRefreshServer();
            var cfg = RestorationCloudSettings(server.Url);
            var manager = SaveManager.Instance;
            var previousStorage = manager.SaveSystem;
            var previousService = Get<CloudSaveService>(manager, "cloudSaveService");
            bool cloud = manager.SaveSettings.enableCloudSave;
            var backend = new FirebaseSaveSystem(cfg, CloudRefreshRoot("failed-list"));
            Set(backend, "url", server.Url);
            int notifications = 0;
            Action updated = () => notifications++;
            var original = manager.GetSlotMetadata(1);
            try
            {
                yield return Await(backend.SaveAsync(new byte[] { 1 }, new SaveSlot(1, "Prepared header", DateTime.UtcNow, "", "A")));
                Set(manager, "saveSystem", backend);
                Set(manager, "cloudSaveService", new CloudSaveService(manager));
                manager.SaveSettings.enableCloudSave = true;
                manager.OnSaveSlotsUpdated += updated;
                server.ListStatus = 503;
                var failed = manager.ForceRefreshSlotsAsync();
                yield return Until(() => failed.IsCompleted, "prepared refresh failure", 10);
                Assert.That(failed.IsFaulted, Is.True); _ = failed.Exception;
                Assert.That(manager.GetSlotMetadata(1), Is.SameAs(original));
                Assert.That(notifications, Is.Zero);
                server.ListStatus = 0;
                yield return Await(manager.ForceRefreshSlotsAsync());
                Assert.That(manager.GetSlotMetadata(1).SlotName, Is.EqualTo("Prepared header"));
                Assert.That(notifications, Is.EqualTo(1));
            }
            finally
            {
                manager.OnSaveSlotsUpdated -= updated;
                manager.SaveSettings.enableCloudSave = cloud;
                Set(manager, "saveSystem", previousStorage);
                Set(manager, "cloudSaveService", previousService);
                Object.Destroy(cfg);
            }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_CancellationAndTimeoutIgnoreLateHeaders()
        {
            var manager = SaveManager.Instance;
            var previous = manager.SaveSystem;
            var original = manager.GetSlotMetadata(1);
            int notifications = 0;
            Action updated = () => notifications++;
            manager.OnSaveSlotsUpdated += updated;
            try
            {
                foreach (bool timeout in new[] { false, true })
                {
                    var gate = new TaskCompletionSource<SaveSlot>();
                    var backend = new CloudRefreshMetadataBackend(previous, null) { ReadMetadata = number => gate.Task };
                    Set(manager, "saveSystem", backend);
                    using var cancellation = new CancellationTokenSource();
                    var refresh = manager.ForceRefreshSlotsAsync(cancellation.Token, timeout ? TimeSpan.FromMilliseconds(150) : (TimeSpan?)null);
                    yield return Until(() => backend.MetadataReads > 0, "held refresh");
                    if (!timeout) cancellation.Cancel();
                    yield return Until(() => refresh.IsCompleted, "cancelled refresh", 5);
                    Assert.That(refresh.IsCanceled, Is.True);
                    gate.SetResult(new SaveSlot(1, "Late result", DateTime.UtcNow, "", "A"));
                    yield return null; yield return null;
                    Assert.That(manager.GetSlotMetadata(1), Is.SameAs(original));
                    Assert.That(notifications, Is.Zero);
                }
                Set(manager, "saveSystem", previous);
                yield return Await(manager.SaveImmediateAsync(1));
            }
            finally { manager.OnSaveSlotsUpdated -= updated; Set(manager, "saveSystem", previous); }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_PublicRefreshWaitsForOwnedRefresh()
        {
            var manager = SaveManager.Instance;
            var previous = manager.SaveSystem;
            var gate = new TaskCompletionSource<SaveSlot>();
            var backend = new CloudRefreshMetadataBackend(previous, null)
            {
                ReadMetadata = number => number == 1 ? gate.Task : Task.FromResult<SaveSlot>(null)
            };
            Set(manager, "saveSystem", backend);
            try
            {
                var first = manager.ForceRefreshSlotsAsync();
                yield return Until(() => backend.MetadataReads > 0, "first refresh");
                var second = manager.RefreshRemoteSlotsAsync();
                using var cancel = new CancellationTokenSource();
                var queued = manager.RefreshRemoteSlotsAsync(cancel.Token);
                cancel.Cancel();
                yield return Until(() => queued.IsCompleted, "queued refresh cancellation");
                Assert.That(queued.IsCanceled, Is.True);
                Assert.That(second.IsCompleted, Is.False);
                gate.SetResult(new SaveSlot(1, "First generation", DateTime.UtcNow, "", "A"));
                yield return Await(first); yield return Await(second);
                Assert.That(manager.GetSlotMetadata(1).SlotName, Is.EqualTo("First generation"));
            }
            finally { gate.TrySetResult(null); Set(manager, "saveSystem", previous); }
        }

        [UnityTest]
        public IEnumerator CloudRefresh_InvalidatedExistenceProbeCannotOverwriteNewResult()
        {
            var cfg = RestorationCloudSettings("http://127.0.0.1");
            var oldRead = new TaskCompletionSource<byte[]>();
            var backend = new CloudRefreshMetadataBackend(SaveManager.Instance.SaveSystem, null) { ReadPayload = () => oldRead.Task };
            var slots = new SaveSlotManager(backend, cfg, null, CloudRefreshRoot("probe-ownership"));
            slots.Slots.Add(new SaveSlot(1, "Probe", DateTime.UtcNow, "", "A"));
            try
            {
                var oldQuery = slots.HasSaveAtAsync(1);
                yield return Until(() => backend.PayloadReads == 1, "old existence probe");
                slots.InvalidateCloudCache(1);
                backend.ReadPayload = () => Task.FromResult(new byte[] { 1 });
                var fresh = slots.HasSaveAtAsync(1); yield return Await(fresh);
                Assert.That(fresh.Result, Is.True);
                oldRead.SetResult(null); yield return Await(oldQuery);
                var cached = slots.HasSaveAtAsync(1); yield return Await(cached);
                Assert.That(cached.Result, Is.True, "Old absence cannot replace the new generation's existence");
                Assert.That(backend.PayloadReads, Is.EqualTo(2));
                slots.InvalidateCloudCache(1);
                backend.ReadPayload = () => Task.FromException<byte[]>(new IOException("Temporary probe outage"));
                var outage = slots.HasSaveAtAsync(1); yield return Await(outage);
                Assert.That(outage.Result, Is.False);
                backend.ReadPayload = () => Task.FromResult(new byte[] { 1 });
                var recovered = slots.HasSaveAtAsync(1); yield return Await(recovered);
                Assert.That(recovered.Result, Is.True, "An outage must not be cached as permanent absence");
                Assert.That(backend.PayloadReads, Is.EqualTo(4));
                Assert.That(Get<Dictionary<int, object>>(slots, "cloudProbeOwners"), Is.Empty);
            }
            finally { oldRead.TrySetResult(null); Object.Destroy(cfg); }
        }

        static string CloudRefreshRoot(string suffix) => Path.Combine(SessionState.GetString(SessionKey + ".root", ""), "cloud-refresh-" + suffix);

        sealed class CloudRefreshMetadataBackend : ISaveSystem
        {
            readonly ISaveSystem inner;
            readonly SaveSlot next;
            internal Func<int, Task<SaveSlot>> ReadMetadata;
            internal Func<Task<byte[]>> ReadPayload;
            internal int MetadataReads, PayloadReads;
            internal CloudRefreshMetadataBackend(ISaveSystem inner, SaveSlot next) { this.inner = inner; this.next = next; }
            public Task<SaveSlot> LoadSlotMetadataAsync(int number)
            {
                MetadataReads++;
                return ReadMetadata != null ? ReadMetadata(number) : number == 1 ? Task.FromResult(next) : Task.FromException<SaveSlot>(new IOException("Injected second-slot read failure"));
            }
            public Task SaveAsync(byte[] data, SaveSlot slot) => inner.SaveAsync(data, slot);
            public Task<byte[]> LoadAsync(SaveSlot slot) { PayloadReads++; return ReadPayload != null ? ReadPayload() : inner.LoadAsync(slot); }
            public Task DeleteAsync(SaveSlot slot) => inner.DeleteAsync(slot);
            public Task SaveSlotMetadataAsync(SaveSlot slot) => inner.SaveSlotMetadataAsync(slot);
            public void Save(byte[] data, SaveSlot slot) => inner.Save(data, slot);
            public byte[] Load(SaveSlot slot) => inner.Load(slot);
            public void Delete(SaveSlot slot) => inner.Delete(slot);
            public string GetSaveGamesPath() => inner.GetSaveGamesPath();
            public void BackupSlot(SaveSlot slot) => inner.BackupSlot(slot);
            public void RestoreBackup(SaveSlot slot) => inner.RestoreBackup(slot);
            public void DeleteBackup(SaveSlot slot) => inner.DeleteBackup(slot);
            public Task WriteTempAsync(byte[] data, SaveSlot slot) => inner.WriteTempAsync(data, slot);
            public Task<byte[]> LoadTempAsync(SaveSlot slot) => inner.LoadTempAsync(slot);
            public void DeleteTemp(SaveSlot slot) => inner.DeleteTemp(slot);
        }

        sealed class CloudRefreshServer : IDisposable
        {
            readonly HttpListener listener = new();
            readonly Task loop;
            internal readonly string Url;
            internal readonly ConcurrentDictionary<string, byte[]> Objects = new();
            internal byte[] Payload;
            internal int ListStatus;
            internal int ObjectStatus;
            internal int PageSize = 2;
            internal string ObjectGet;
            internal int ListRequests;
            internal CloudRefreshServer(string listing = null)
            {
                var portProbe = new TcpListener(IPAddress.Loopback, 0);
                portProbe.Start();
                int port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
                portProbe.Stop();
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
                            var request = context.Request;
                            byte[] response;
                            if (request.HttpMethod == "POST")
                            {
                                using var body = new MemoryStream();
                                await request.InputStream.CopyToAsync(body);
                                string name = request.QueryString["name"];
                                if (string.IsNullOrEmpty(name)) throw new InvalidDataException("Missing upload object name.");
                                Objects[name] = body.ToArray();
                                if (!name.EndsWith(".meta.json")) Payload = body.ToArray();
                                response = Encoding.UTF8.GetBytes("{}");
                            }
                            else if (request.Url.AbsolutePath == "/o")
                            {
                                ListRequests++;
                                if (ListStatus != 0)
                                {
                                    context.Response.StatusCode = ListStatus;
                                    response = Encoding.UTF8.GetBytes("{\"error\":\"Injected list failure\"}");
                                }
                                else if (listing != null)
                                    response = Encoding.UTF8.GetBytes(request.QueryString["pageToken"] == "page-two" ? "{\"items\":[]}" : listing);
                                else
                                {
                                    AssertRequest(request.QueryString["maxResults"] == "1000", "Missing page limit");
                                    string prefix = request.QueryString["prefix"];
                                    AssertRequest(!string.IsNullOrEmpty(prefix), "Missing prefix");
                                    string token = request.QueryString["pageToken"];
                                    int offset = token == null ? 0 : int.Parse(token.Split('/')[1].Split(' ')[0]);
                                    string[] names = Objects.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(key => key, StringComparer.Ordinal).ToArray();
                                    var page = new Newtonsoft.Json.Linq.JObject
                                    {
                                        ["items"] = new Newtonsoft.Json.Linq.JArray(names.Skip(offset).Take(PageSize).Select(name => new Newtonsoft.Json.Linq.JObject { ["name"] = name }))
                                    };
                                    if (offset + PageSize < names.Length) page["nextPageToken"] = "page/" + (offset + PageSize) + " #+%";
                                    response = Encoding.UTF8.GetBytes(page.ToString());
                                }
                            }
                            else
                            {
                                ObjectGet = request.RawUrl;
                                string rawPath = request.RawUrl.Split('?')[0];
                                string encodedName = rawPath.StartsWith("/o/") ? rawPath.Substring(3) : "";
                                string name = Uri.UnescapeDataString(encodedName);
                                if (ObjectStatus != 0)
                                {
                                    context.Response.StatusCode = ObjectStatus;
                                    response = Encoding.UTF8.GetBytes("{\"error\":\"Injected object failure\"}");
                                }
                                else if (encodedName.Length == 0 || encodedName.Contains("/") || !Objects.TryGetValue(name, out response))
                                {
                                    context.Response.StatusCode = 404;
                                    response = Encoding.UTF8.GetBytes("{\"error\":\"Unknown or incorrectly encoded object name\"}");
                                }
                                else if (request.HttpMethod == "DELETE")
                                {
                                    Objects.TryRemove(name, out _);
                                    response = Array.Empty<byte>();
                                }
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
            static void AssertRequest(bool condition, string message)
            {
                if (!condition) throw new InvalidDataException(message);
            }

            public void Dispose()
            {
                listener.Stop(); listener.Close();
                if (loop.IsFaulted) throw new InvalidOperationException("Review endpoint failed", loop.Exception);
            }
        }
    }
}
#endif
