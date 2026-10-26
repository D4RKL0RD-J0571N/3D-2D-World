#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator CloudAccounts_MySqlConcurrentDirectSavesKeepTheirIdentity() => RequestSnapshotDirect(SaveBackend.MySQL);
        [UnityTest] public IEnumerator CloudAccounts_FirebaseConcurrentDirectSavesKeepTheirIdentity() => RequestSnapshotDirect(SaveBackend.Firebase);
        [UnityTest] public IEnumerator CloudAccounts_SupabaseConcurrentDirectSavesKeepTheirIdentity() => RequestSnapshotDirect(SaveBackend.Supabase);
        [UnityTest] public IEnumerator CloudAccounts_MySqlCoordinatedImagesAndPayloadKeepTheirIdentity() => RequestSnapshotCoordinated(SaveBackend.MySQL);
        [UnityTest] public IEnumerator CloudAccounts_FirebaseCoordinatedImagesAndPayloadKeepTheirIdentity() => RequestSnapshotCoordinated(SaveBackend.Firebase);
        [UnityTest] public IEnumerator CloudAccounts_SupabaseCoordinatedImagesAndPayloadKeepTheirIdentity() => RequestSnapshotCoordinated(SaveBackend.Supabase);
        [UnityTest] public IEnumerator CloudAccounts_SupabasePendingLoginCapturesAuthenticatedIdentity() => RequestSnapshotCoordinated(SaveBackend.Supabase, true);

        IEnumerator RequestSnapshotDirect(SaveBackend kind)
        {
            using var server = new RequestSnapshotServer("payload");
            var resolver = ScriptableObject.CreateInstance<RequestSnapshotResolver>();
            var settings = Object.Instantiate(SaveManager.Instance.SaveSettings);
            ConfigureRequestSnapshotSettings(settings, resolver, server.Url, kind);
            var storage = CreateRequestSnapshotStorage(settings,
                Path.Combine(SaveManager.Instance.SaveSystem.GetSaveGamesPath(), "account-direct-" + kind), server.Url);
            try
            {
                var first = storage.SaveAsync(new byte[] { 1 }, new SaveSlot(1, "A", DateTime.UtcNow, "", ""));
                yield return Until(() => server.Gated, "first account payload held", 5);
                resolver.Account = "account-b";
                if (storage is MySqlSaveSystem mysql)
                {
                    var login = mysql.LoginAsync("users/account-b", "fixture-password");
                    yield return Await(login);
                    Assert.That(login.Result, Is.True);
                }
                var second = storage.SaveAsync(new byte[] { 2 }, new SaveSlot(2, "B", DateTime.UtcNow, "", ""));
                yield return Await(second);
                Assert.That(first.IsCompleted, Is.False, "The second request must not overwrite the first request's captured account.");
                server.Release();
                yield return Await(first);
                var writes = server.Writes;
                Assert.That(writes.Count, Is.EqualTo(4));
                AssertSnapshotWrites(writes.Where(write => write.Slot == 1), "account-a", kind, 2);
                AssertSnapshotWrites(writes.Where(write => write.Slot == 2), "account-b", kind, 2);
            }
            finally { server.Release(); Object.Destroy(settings); Object.Destroy(resolver); }
        }

        IEnumerator RequestSnapshotCoordinated(SaveBackend kind, bool pendingLogin = false)
        {
            using var server = new RequestSnapshotServer("image");
            var manager = SaveManager.Instance;
            var originalStorage = manager.SaveSystem;
            var settings = manager.SaveSettings;
            var previous = Object.Instantiate(settings);
            var resolver = ScriptableObject.CreateInstance<RequestSnapshotResolver>();
            var screenshot = Get<ScreenshotManager>(manager, "screenshotManager");
            var previousCapture = screenshot.CustomCapture;
            bool previousLogin = manager.IsSupabaseCustomLoggedIn;
            ConfigureRequestSnapshotSettings(settings, resolver, server.Url, kind);
            settings.enableScreenshots = true;
            screenshot.CustomCapture = () => new Texture2D(2, 2);
            Set(manager, "isSupabaseCustomLoggedIn", !pendingLogin);
            if (pendingLogin) resolver.Account = "guest";
            var storage = CreateRequestSnapshotStorage(settings,
                Path.Combine(originalStorage.GetSaveGamesPath(), "account-manager-" + kind), server.Url);
            Set(manager, "saveSystem", storage);
            try
            {
                var first = manager.SaveAsync(1);
                if (pendingLogin)
                {
                    var coordinator = Get<object>(manager, "operationCoordinator");
                    var active = coordinator.GetType().GetProperty("Active", BindingFlags.Instance | BindingFlags.NonPublic);
                    yield return Until(() => active.GetValue(coordinator) != null, "save entered login wait", 5);
                    yield return null;
                    Assert.That(server.Writes, Is.Empty);
                    resolver.Account = "account-a";
                    Set(manager, "isSupabaseCustomLoggedIn", true);
                }
                yield return Until(() => server.Gated, "first account image held", 5);
                resolver.Account = "account-b";
                if (storage is MySqlSaveSystem mysql)
                {
                    var login = mysql.LoginAsync("users/account-b", "fixture-password");
                    yield return Await(login);
                    Assert.That(login.Result, Is.True);
                }
                var second = manager.SaveAsync(2);
                server.Release();
                yield return Await(first);
                yield return Await(second);
                var writes = server.Writes;
                Assert.That(writes.Count, Is.EqualTo(6));
                AssertSnapshotWrites(writes.Take(3), "account-a", kind, 3);
                AssertSnapshotWrites(writes.Skip(3), "account-b", kind, 3);
            }
            finally
            {
                server.Release();
                Set(manager, "saveSystem", originalStorage);
                screenshot.CustomCapture = previousCapture;
                Set(manager, "isSupabaseCustomLoggedIn", previousLogin);
                // Restore only fields the fixture changes; runtime state/assets remain owned by teardown.
                settings.backend = previous.backend;
                settings.enableCloudSave = previous.enableCloudSave;
                settings.keepLocalMirror = previous.keepLocalMirror;
                settings.enableScreenshots = previous.enableScreenshots;
                settings.userFolderStrategy = previous.userFolderStrategy;
                settings.customUserFolderResolver = previous.customUserFolderResolver;
                settings.mySqlApiUrl = previous.mySqlApiUrl;
                settings.mySqlAuthApiUrl = previous.mySqlAuthApiUrl;
                settings.supabaseUrl = previous.supabaseUrl;
                settings.supabaseAnonKey = previous.supabaseAnonKey;
                settings.bucket = previous.bucket;
                settings.firebaseBucket = previous.firebaseBucket;
                settings.firebaseIdToken = previous.firebaseIdToken;
                settings.saveFileName = previous.saveFileName;
                settings.cloudSaveTransport = previous.cloudSaveTransport;
                Object.Destroy(previous);
                Object.Destroy(resolver);
            }
        }

        static void ConfigureRequestSnapshotSettings(SaveSettings settings, RequestSnapshotResolver resolver, string url, SaveBackend kind)
        {
            settings.backend = kind;
            settings.enableCloudSave = true;
            settings.keepLocalMirror = false;
            settings.userFolderStrategy = UserFolderStrategy.Custom;
            settings.customUserFolderResolver = resolver;
            settings.mySqlApiUrl = settings.mySqlAuthApiUrl = url;
            settings.supabaseUrl = url;
            settings.supabaseAnonKey = "fixture-key";
            settings.bucket = settings.firebaseBucket = "test-bucket";
            settings.firebaseIdToken = "fixture-token";
            settings.saveFileName = "Slot";
            settings.cloudSaveTransport = CloudSaveTransport.Binary;
        }

        static ISaveSystem CreateRequestSnapshotStorage(SaveSettings settings, string root, string url)
        {
            if (settings.backend == SaveBackend.MySQL) return new MySqlSaveSystem(settings, root);
            if (settings.backend == SaveBackend.Supabase) return new SupabaseSaveSystem(settings, root);
            var firebase = new FirebaseSaveSystem(settings, root);
            Set(firebase, "url", url);
            return firebase;
        }

        static void AssertSnapshotWrites(IEnumerable<RequestSnapshotWrite> sequence, string account, SaveBackend backend, int count)
        {
            var writes = sequence.ToArray();
            Assert.That(writes.Length, Is.EqualTo(count));
            foreach (var write in writes)
            {
                Assert.That(write.User, Is.EqualTo("users/" + account), write.Kind + " crossed accounts");
                if (backend != SaveBackend.MySQL)
                    Assert.That(write.Authorization, Is.EqualTo("Bearer token-" + account), write.Kind + " used a different account token");
            }
        }

        sealed class RequestSnapshotResolver : ScriptableObject, IUserAuthorizationResolver
        {
            internal string Account = "account-a";
            public string ResolveUserFolder()
            {
                if (!UnityMainThreadDispatcher.IsMainThread) throw new InvalidOperationException("Resolve user folder on Unity's main thread.");
                return "users/" + Account;
            }
            public string ResolveAccessKey()
            {
                if (!UnityMainThreadDispatcher.IsMainThread) throw new InvalidOperationException("Resolve bearer on Unity's main thread.");
                return "token-" + Account;
            }
        }

        sealed class RequestSnapshotWrite
        {
            internal string User, Authorization, Kind;
            internal int Slot;
        }

        sealed class RequestSnapshotServer : IDisposable
        {
            readonly HttpListener listener = new();
            readonly Task loop;
            readonly List<Task> handlers = new();
            readonly List<RequestSnapshotWrite> writes = new();
            readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            readonly string gateKind;
            internal readonly string Url;
            internal volatile bool Gated;
            internal List<RequestSnapshotWrite> Writes { get { lock (writes) return new List<RequestSnapshotWrite>(writes); } }
            internal void Release() => release.TrySetResult(true);

            internal RequestSnapshotServer(string gateKind)
            {
                this.gateKind = gateKind;
                var port = new TcpListener(IPAddress.Loopback, 0);
                port.Start(); Url = "http://127.0.0.1:" + ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
                listener.Prefixes.Add(Url + "/"); listener.Start();
                loop = Task.Run(async () =>
                {
                    while (listener.IsListening)
                    {
                        HttpListenerContext context;
                        try { context = await listener.GetContextAsync(); }
                        catch (Exception) when (!listener.IsListening) { break; }
                        lock (handlers) handlers.Add(Handle(context));
                    }
                });
            }

            async Task Handle(HttpListenerContext context)
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                string body = await reader.ReadToEndAsync();
                var request = context.Request;
                string path = request.Url.AbsolutePath;
                byte[] response = Array.Empty<byte>();
                RequestSnapshotWrite write = null;
                if (path == "/login") response = Encoding.UTF8.GetBytes(new JObject { ["uid"] = JObject.Parse(body)["username"] }.ToString());
                else if (request.HttpMethod == "GET") context.Response.StatusCode = 404;
                else if (path == "/save" || path == "/metadata")
                {
                    var json = JObject.Parse(body);
                    write = new RequestSnapshotWrite { User = (string)json["uid"], Slot = (int)json["slot"], Kind = path == "/save" ? "payload" : "metadata" };
                }
                else if (path == "/uploadImage")
                {
                    var match = Regex.Match(body, "name=\"uid\"[^\\r\\n]*\\r\\n(?:[^\\r\\n]+\\r\\n)*\\r\\n([^\\r\\n]*)");
                    if (!match.Success) throw new InvalidDataException("Missing image UID.");
                    write = new RequestSnapshotWrite { User = match.Groups[1].Value, Kind = "image" };
                }
                else
                {
                    string key = path.StartsWith("/storage/v1/object/test-bucket/", StringComparison.Ordinal)
                        ? Uri.UnescapeDataString(path.Substring("/storage/v1/object/test-bucket/".Length))
                        : request.QueryString["name"];
                    if (key == null) throw new InvalidDataException("Unexpected cloud request " + request.RawUrl);
                    string name = key.Substring(key.LastIndexOf('/') + 1);
                    string kind = name.EndsWith(".meta.json", StringComparison.Ordinal) ? "metadata" : name.EndsWith(".sav", StringComparison.Ordinal) ? "payload" : "image";
                    int slot = kind == "image" ? 0 : int.Parse(name.Substring(4, name.IndexOf('.') - 4));
                    write = new RequestSnapshotWrite { User = key.Substring(0, key.LastIndexOf('/')), Kind = kind, Slot = slot };
                }
                if (write != null)
                {
                    write.Authorization = request.Headers["Authorization"];
                    bool gate;
                    lock (writes)
                    {
                        writes.Add(write);
                        gate = !Gated && write.Kind == gateKind;
                        if (gate) Gated = true;
                    }
                    if (gate) await release.Task;
                }
                context.Response.ContentLength64 = response.Length;
                if (response.Length != 0) await context.Response.OutputStream.WriteAsync(response, 0, response.Length);
                context.Response.Close();
            }

            public void Dispose()
            {
                Release(); listener.Stop(); listener.Close();
                if (!loop.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Account fixture did not stop.");
                Task[] active;
                lock (handlers) active = handlers.ToArray();
                if (!Task.WaitAll(active, TimeSpan.FromSeconds(3))) throw new TimeoutException("Account requests did not finish.");
            }
        }
    }
}
#endif
