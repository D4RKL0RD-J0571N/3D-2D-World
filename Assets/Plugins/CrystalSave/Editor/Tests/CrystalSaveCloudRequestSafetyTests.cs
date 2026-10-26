#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
        const string RequestSafetyEmail = "\"quoted\\mail\"+雪@example.test";
        const string RequestSafetyPassword = "quo\"te\\slash\nline\t\0雪";

        [UnityTest] public IEnumerator CloudAuth_SupabaseSignupPreservesCredentials() => RequestSafetyPasswordAuth(true);
        [UnityTest] public IEnumerator CloudAuth_SupabaseSigninPreservesCredentials() => RequestSafetyPasswordAuth(false);

        IEnumerator RequestSafetyPasswordAuth(bool signup)
        {
            using var server = new RequestSafetyServer();
            int success = 0, errors = 0;
            string actualToken = null, actualUid = null;
            Action<string, string> accepted = (token, uid) => { success++; actualToken = token; actualUid = uid; };
            IEnumerator request = signup
                ? SupabaseRestAuth.SignUpWithEmailPassword(server.Url + "/", "fixture-key", RequestSafetyEmail, RequestSafetyPassword,
                    (token, uid, confirmation) => { Assert.That(confirmation, Is.False); accepted(token, uid); }, _ => errors++)
                : SupabaseRestAuth.SignInWithPassword(server.Url + "/", "fixture-key", RequestSafetyEmail, RequestSafetyPassword,
                    accepted, _ => errors++);
            yield return RunRequestSafetyAuth(request);
            Assert.That(success, Is.EqualTo(1));
            Assert.That(errors, Is.Zero);
            Assert.That(actualToken, Is.EqualTo("fixture-token"));
            Assert.That(actualUid, Is.EqualTo("fixture-user"));
            Assert.That(server.RequestPath, Is.EqualTo(signup ? "/auth/v1/signup" : "/auth/v1/token"));
            Assert.That(server.RequestJson["email"].Value<string>(), Is.EqualTo(RequestSafetyEmail));
            Assert.That(server.RequestJson["password"].Value<string>(), Is.EqualTo(RequestSafetyPassword));
            Assert.That(server.RequestJson.Count, Is.EqualTo(2));
            Assert.That(server.ApiKey, Is.EqualTo("fixture-key"));
            if (!signup) Assert.That(server.Grant, Is.EqualTo("password"));
        }

        [UnityTest]
        public IEnumerator CloudAuth_SupabaseMagicLinkPreservesEmailAndRedirect()
        {
            using var server = new RequestSafetyServer();
            const string redirect = "game-save://callback/path?query=a&value=\"雪\"#resume";
            int success = 0, errors = 0;
            yield return RunRequestSafetyAuth(SupabaseRestAuth.SendMagicLink(server.Url, "fixture-key", RequestSafetyEmail,
                () => success++, _ => errors++, redirect));
            Assert.That(success, Is.EqualTo(1));
            Assert.That(errors, Is.Zero);
            Assert.That(server.RequestJson["email"].Value<string>(), Is.EqualTo(RequestSafetyEmail));
            Assert.That(server.RequestJson["create_user"].Value<bool>(), Is.True);
            Assert.That(server.Redirect, Is.EqualTo(redirect), "GoTrue reads redirect_to from the query rather than JSON.");
            yield return RunRequestSafetyAuth(SupabaseRestAuth.SendMagicLink(server.Url, "fixture-key", "plain@example.test",
                () => success++, _ => errors++));
            Assert.That(success, Is.EqualTo(2));
            Assert.That(errors, Is.Zero);
            Assert.That(server.Redirect, Is.Null, "The default redirect remains server-owned.");
        }

        [UnityTest]
        public IEnumerator CloudAuth_SupabaseConfirmationAndJwtFallbackRetainIdentity()
        {
            using var server = new RequestSafetyServer { Response = "{\"id\":\"confirmation-user\"}" };
            int success = 0, errors = 0;
            yield return RunRequestSafetyAuth(SupabaseRestAuth.SignUpWithEmailPassword(server.Url, "fixture-key", "plain@example.test", "password",
                (token, uid, confirmation) =>
                {
                    success++;
                    Assert.That(token, Is.Null.Or.Empty);
                    Assert.That(uid, Is.EqualTo("confirmation-user"));
                    Assert.That(confirmation, Is.True);
                }, _ => errors++));
            string claims = Convert.ToBase64String(Encoding.UTF8.GetBytes("{ \"sub\" : \"escaped\\\"user雪\" }"))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            string jwt = "header." + claims + ".signature";
            server.Response = new JObject { ["access_token"] = jwt }.ToString();
            yield return RunRequestSafetyAuth(SupabaseRestAuth.SignInWithPassword(server.Url, "fixture-key", "plain@example.test", "password",
                (token, uid) => { success++; Assert.That(token, Is.EqualTo(jwt)); Assert.That(uid, Is.EqualTo("escaped\"user雪")); }, _ => errors++));
            Assert.That(success, Is.EqualTo(2));
            Assert.That(errors, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CloudAuth_SupabaseInvalidSuccessBodiesUseErrorCallback()
        {
            using var server = new RequestSafetyServer();
            int success = 0, errors = 0;
            foreach (string response in new[] { "{}", "null", "{broken", "{\"access_token\":\"invalid\"}" })
            {
                server.Response = response;
                yield return RunRequestSafetyAuth(SupabaseRestAuth.SignUpWithEmailPassword(server.Url, "fixture-key", "plain@example.test", "password",
                    (_, __, ___) => success++, _ => errors++));
                yield return RunRequestSafetyAuth(SupabaseRestAuth.SignInWithPassword(server.Url, "fixture-key", "plain@example.test", "password",
                    (_, __) => success++, _ => errors++));
            }
            Assert.That(success, Is.Zero);
            Assert.That(errors, Is.EqualTo(8));
        }

        [UnityTest]
        public IEnumerator CloudAuth_SupabaseStalledMagicLinkHasFiniteDeadline()
        {
            using var server = new RequestSafetyServer { HangAuth = true };
            int success = 0, errors = 0;
            var request = SupabaseRestAuth.SendMagicLink(server.Url, "fixture-key", "plain@example.test",
                () => success++, _ => errors++);
            var runner = SaveManager.Instance.StartCoroutine(request);
            try
            {
                yield return Until(() => errors + success != 0, "Supabase auth request deadline", 22);
                Assert.That(server.AuthRequests, Is.EqualTo(1));
                Assert.That(success, Is.Zero);
                Assert.That(errors, Is.EqualTo(1));
            }
            finally { SaveManager.Instance.StopCoroutine(runner); (request as IDisposable)?.Dispose(); }
        }

        [UnityTest]
        public IEnumerator CloudAuth_MySqlPreservesCredentialsAndLoginIdentity()
        {
            const string user = "quoted\"user\\雪\0tail";
            using var server = new RequestSafetyServer { Response = new JObject { ["uid"] = user }.ToString() };
            var settings = RequestSafetySettings(SaveManager.Instance.SaveSettings, server.Url);
            settings.mySqlAuthApiUrl = server.Url;
            var storage = new MySqlSaveSystem(settings, Path.Combine(SaveManager.Instance.SaveSystem.GetSaveGamesPath(), "auth-encoding"));
            try
            {
                var signup = storage.SignUpAsync(user, RequestSafetyPassword);
                yield return Await(signup);
                Assert.That(signup.Result, Is.True);
                Assert.That(server.RequestJson["username"].Value<string>(), Is.EqualTo(user));
                Assert.That(server.RequestJson["password"].Value<string>(), Is.EqualTo(RequestSafetyPassword));
                var login = storage.LoginAsync(user, RequestSafetyPassword);
                yield return Await(login);
                Assert.That(login.Result, Is.True);
                Assert.That(server.RequestJson["username"].Value<string>(), Is.EqualTo(user));
                Assert.That(server.RequestJson["password"].Value<string>(), Is.EqualTo(RequestSafetyPassword));
                Assert.That(Get<string>(storage, "userIdOverride"), Is.EqualTo(user));
                Assert.That(server.AuthRequests, Is.EqualTo(2));
                var slot = new SaveSlot(1, "Identity", DateTime.UtcNow, "", "");
                yield return Await(storage.SaveAsync(new byte[] { 1, 2 }, slot));
                Assert.That(server.PayloadUid, Is.EqualTo(user));
                yield return Await(storage.DeleteAsync(slot));
                Assert.That(server.DeleteUid, Is.EqualTo(user));
            }
            finally { Object.Destroy(settings); }
        }

        [UnityTest]
        public IEnumerator CloudImages_MySqlWorkerUploadDispatchesAndReportsHttpFailures()
        {
            using var server = new RequestSafetyServer();
            var settings = Object.Instantiate(SaveManager.Instance.SaveSettings);
            settings.mySqlApiUrl = server.Url;
            settings.userFolderStrategy = UserFolderStrategy.Shared;
            var storage = new MySqlSaveSystem(settings, Path.Combine(SaveManager.Instance.SaveSystem.GetSaveGamesPath(), "image-thread"));
            try
            {
                yield return Await(Task.Run(() => storage.UploadScreenshotAsync(1, "worker.png", new byte[] { 1, 2 }, ScreenshotFormat.PNG)));
                Assert.That(server.ImageRequests, Is.EqualTo(1));
                server.ImageStatus = 503;
                var rejected = Task.Run(() => storage.UploadScreenshotAsync(1, "worker.png", new byte[] { 1, 2 }, ScreenshotFormat.PNG));
                yield return Until(() => rejected.IsCompleted, "failed worker image upload", 10);
                Assert.That(rejected.IsFaulted, Is.True);
                Assert.That(rejected.Exception.GetBaseException(), Is.TypeOf<IOException>());
                Assert.That(server.ImageRequests, Is.EqualTo(2));
            }
            finally { Object.Destroy(settings); }
        }

        [UnityTest]
        public IEnumerator CloudImages_MySqlStalledCommitReleasesQueuedSave()
        {
            using var server = new RequestSafetyServer { HangImage = true };
            var manager = SaveManager.Instance;
            var original = manager.SaveSystem;
            var settings = manager.SaveSettings;
            var screenshot = Get<ScreenshotManager>(manager, "screenshotManager");
            string oldUrl = settings.mySqlApiUrl;
            var oldBackend = settings.backend;
            bool oldCloud = settings.enableCloudSave, oldMirror = settings.keepLocalMirror, oldImages = settings.enableScreenshots;
            bool oldIgnore = LogAssert.ignoreFailingMessages;
            var oldCapture = screenshot.CustomCapture;
            var storage = new MySqlSaveSystem(RequestSafetySettings(settings, server.Url),
                Path.Combine(original.GetSaveGamesPath(), "image-timeout"));
            var storageSettings = Get<SaveSettings>(storage, "_cfg");
            settings.enableCloudSave = true;
            settings.backend = SaveBackend.MySQL;
            settings.keepLocalMirror = false;
            settings.enableScreenshots = true;
            screenshot.CustomCapture = () => new Texture2D(2, 2);
            Set(manager, "saveSystem", storage);
            LogAssert.ignoreFailingMessages = true;
            using var cancellation = new CancellationTokenSource();
            try
            {
                var stalled = manager.SaveAsync(1, null, cancellation.Token);
                yield return Until(() => server.ImageRequests == 1, "image upload entered commit", 10);
                cancellation.Cancel(); // Commit owns its real transport result after this point.
                server.HangImage = false;
                var following = manager.SaveAsync(2);
                yield return null;
                Assert.That(following.IsCompleted, Is.False, "The next save must wait for the in-flight request.");
                yield return Until(() => stalled.IsCompleted && following.IsCompleted, "image timeout releases FIFO", 25);
                Assert.That(stalled.IsFaulted, Is.True);
                Assert.That(stalled.Exception.ToString(), Does.Contain("uploadImage"));
                Assert.That(following.IsFaulted || following.IsCanceled, Is.False, following.Exception?.ToString());
                Assert.That(server.ImageRequests, Is.EqualTo(2));
                Assert.That(server.PayloadRequests, Is.EqualTo(1), "Timed-out screenshot publication must not write its payload.");
            }
            finally
            {
                Set(manager, "saveSystem", original);
                screenshot.CustomCapture = oldCapture;
                settings.mySqlApiUrl = oldUrl;
                settings.backend = oldBackend;
                settings.enableCloudSave = oldCloud;
                settings.keepLocalMirror = oldMirror;
                settings.enableScreenshots = oldImages;
                LogAssert.ignoreFailingMessages = oldIgnore;
                Object.Destroy(storageSettings);
            }
        }

        static SaveSettings RequestSafetySettings(SaveSettings original, string url)
        {
            var settings = Object.Instantiate(original);
            settings.mySqlApiUrl = url;
            settings.enableCloudSave = true;
            settings.keepLocalMirror = false;
            settings.backend = SaveBackend.MySQL;
            settings.userFolderStrategy = UserFolderStrategy.Shared;
            return settings;
        }

        static IEnumerator RunRequestSafetyAuth(IEnumerator request)
        {
            // This assembly's UnityTest enumerator is an EditMode runner, even while
            // the fixture is in Play Mode. Let a real Unity coroutine own AsyncOperation.
            bool complete = false;
            IEnumerator Run()
            {
                yield return request;
                complete = true;
            }
            var runner = SaveManager.Instance.StartCoroutine(Run());
            try { yield return Until(() => complete, "auth coroutine response", 22); }
            finally { SaveManager.Instance.StopCoroutine(runner); (request as IDisposable)?.Dispose(); }
        }

        sealed class RequestSafetyServer : IDisposable
        {
            readonly HttpListener listener = new();
            readonly List<HttpListenerContext> pending = new();
            readonly Task loop;
            internal readonly string Url;
            internal volatile bool HangImage, HangAuth;
            internal volatile int ImageStatus = 200;
            internal int ImageRequests, AuthRequests, PayloadRequests;
            internal string RequestPath, ApiKey, Grant, Redirect, PayloadUid, DeleteUid;
            internal JObject RequestJson;
            internal string Response = "{\"access_token\":\"fixture-token\",\"user\":{\"id\":\"fixture-user\"}}";

            internal RequestSafetyServer()
            {
                var port = new TcpListener(IPAddress.Loopback, 0);
                port.Start();
                Url = "http://127.0.0.1:" + ((IPEndPoint)port.LocalEndpoint).Port;
                port.Stop();
                listener.Prefixes.Add(Url + "/");
                listener.Start();
                loop = Task.Run(async () =>
                {
                    while (listener.IsListening)
                    {
                        HttpListenerContext context;
                        try { context = await listener.GetContextAsync(); }
                        catch (Exception) when (!listener.IsListening) { break; }
                        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                        string body = await reader.ReadToEndAsync();
                        string path = context.Request.Url.AbsolutePath;
                        byte[] response = Array.Empty<byte>();
                        if (path.StartsWith("/auth/v1/", StringComparison.Ordinal) || path == "/login" || path == "/signup")
                        {
                            RequestPath = path;
                            ApiKey = context.Request.Headers["apikey"];
                            Grant = context.Request.QueryString["grant_type"];
                            Redirect = context.Request.QueryString["redirect_to"];
                            try { RequestJson = JObject.Parse(body); }
                            catch { context.Response.StatusCode = 400; }
                            Interlocked.Increment(ref AuthRequests);
                            if (HangAuth) { lock (pending) pending.Add(context); continue; }
                            response = Encoding.UTF8.GetBytes(Response);
                        }
                        else if (path == "/uploadImage")
                        {
                            bool hang = HangImage;
                            Interlocked.Increment(ref ImageRequests);
                            if (hang) { lock (pending) pending.Add(context); continue; }
                            context.Response.StatusCode = ImageStatus;
                        }
                        else if (path == "/save")
                        {
                            PayloadUid = JObject.Parse(body)["uid"].Value<string>();
                            Interlocked.Increment(ref PayloadRequests);
                        }
                        else if (path == "/delete") DeleteUid = JObject.Parse(body)["uid"].Value<string>();
                        else if (path == "/load" || (path == "/metadata" && context.Request.HttpMethod == "GET"))
                            context.Response.StatusCode = 404;
                        context.Response.ContentLength64 = response.Length;
                        if (response.Length != 0) await context.Response.OutputStream.WriteAsync(response, 0, response.Length);
                        context.Response.Close();
                    }
                });
            }

            public void Dispose()
            {
                listener.Stop();
                lock (pending) foreach (var request in pending) request.Response.Abort();
                listener.Close();
                if (!loop.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Loopback request fixture did not stop.");
                loop.GetAwaiter().GetResult();
            }
        }
    }
}
#endif
