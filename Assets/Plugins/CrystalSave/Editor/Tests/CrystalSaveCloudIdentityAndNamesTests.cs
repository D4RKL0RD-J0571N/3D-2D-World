#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
        [UnityTest] public IEnumerator CloudNames_ConflictRetainsEmptySelectedName() => CloudNamesConflict("");
        [UnityTest] public IEnumerator CloudNames_ConflictRetainsNamedControl() => CloudNamesConflict("Remote selected");
        [UnityTest] public IEnumerator CloudNames_ConflictRetainsMissingNameDefault() => CloudNamesConflict(null);

        IEnumerator CloudNamesConflict(string name)
        {
            var manager = SaveManager.Instance;
            var original = manager.SaveSystem;
            var settings = manager.SaveSettings;
            bool cloud = settings.enableCloudSave, automatic = settings.autoResolveConflicts;
            var backend = settings.backend;
            var policy = settings.autoConflictPolicy;
            var local = new SaveData(settings.version) { LastSaved = new DateTime(2024, 1, 1) };
            var remote = new SaveData(settings.version) { LastSaved = new DateTime(2025, 1, 1) };
            local.ComponentsData["local_id"] = new byte[] { 1 };
            remote.ComponentsData["remote_id"] = new byte[] { 2 };
            var storage = new PostReviewStorage(original)
            {
                CandidateGate = Task.FromResult(new SaveConflictCandidates(
                    new SaveConflictCandidate(manager.Serializer.Serialize(local), new SaveSlot(1, "Local", local.LastSaved, "", "")),
                    new SaveConflictCandidate(manager.Serializer.Serialize(remote), new SaveSlot(1, name, remote.LastSaved, "", ""))))
            };
            manager.GetSlotMetadata(1).SlotName = "Old destination";
            settings.enableCloudSave = true;
            settings.backend = SaveBackend.MySQL;
            settings.autoResolveConflicts = true;
            settings.autoConflictPolicy = AutoConflictPolicy.CloudWins;
            Set(manager, "saveSystem", storage);
            try
            {
                yield return Await(manager.SaveImmediateAsync(1));
                Assert.That(manager.Serializer.Deserialize<SaveData>(storage.Written).ComponentsData.ContainsKey("remote_id"), Is.True);
                Assert.That(manager.GetSlotMetadata(1).SlotName, Is.EqualTo(name ?? "Old destination"));
            }
            finally
            {
                settings.enableCloudSave = cloud;
                settings.backend = backend;
                settings.autoResolveConflicts = automatic;
                settings.autoConflictPolicy = policy;
                Set(manager, "saveSystem", original);
            }
        }

        [UnityTest] public IEnumerator CloudNames_MySqlWritesPreserveEmptyNames() => CloudNamesBackend(SaveBackend.MySQL);
        [UnityTest] public IEnumerator CloudNames_FirebaseWritesPreserveEmptyNames() => CloudNamesBackend(SaveBackend.Firebase);
        [UnityTest] public IEnumerator CloudNames_SupabaseWritesPreserveEmptyNames() => CloudNamesBackend(SaveBackend.Supabase);

        IEnumerator CloudNamesBackend(SaveBackend kind)
        {
            using var mysql = new CloudNameMySqlServer("alice");
            using var firebase = new CloudRefreshServer();
            using var supabase = new RestorationCloudServer();
            var cfg = RestorationCloudSettings(supabase.Url);
            cfg.backend = kind;
            cfg.userFolderStrategy = UserFolderStrategy.Shared;
            cfg.mySqlApiUrl = mysql.Url;
            cfg.firebaseBucket = "test-bucket";
            string root = CloudRefreshRoot("names-" + kind);
            ISaveSystem storage;
            if (kind == SaveBackend.MySQL) storage = new MySqlSaveSystem(cfg, root);
            else if (kind == SaveBackend.Firebase)
            {
                storage = new FirebaseSaveSystem(cfg, root);
                Set(storage, "url", firebase.Url);
            }
            else storage = new SupabaseSaveSystem(cfg, root);
            try
            {
                foreach (string name in new[] { "", "Named control", null })
                {
                    var slot = new SaveSlot(1, name, new DateTime(2025, 1, 1), "", "SceneA");
                    yield return Await(storage.SaveAsync(new byte[] { 7, 8 }, slot));
                    var saved = storage.LoadSlotMetadataAsync(1);
                    yield return Await(saved);
                    Assert.That(saved.Result.SlotName, Is.EqualTo(name ?? "Slot 1"), kind + " full save");
                    slot.SlotName = "";
                    yield return Await(storage.SaveSlotMetadataAsync(slot));
                    var edited = storage.LoadSlotMetadataAsync(1);
                    yield return Await(edited);
                    Assert.That(edited.Result.SlotName, Is.Empty, kind + " metadata update");
                    var payload = storage.LoadAsync(slot);
                    yield return Await(payload);
                    Assert.That(payload.Result, Is.EqualTo(new byte[] { 7, 8 }), "Metadata update preserves payload");
                }
                // Legacy headers omit names, images, scenes and custom metadata. Keep
                // each existing decoder's missing-field defaults distinct from an empty name.
                if (kind == SaveBackend.MySQL) mysql.Metadata = new JObject { ["slot"] = 1, ["ticks"] = 0 };
                else
                {
                    byte[] legacy = Encoding.UTF8.GetBytes("{\"SlotNumber\":1,\"LastSavedTicks\":0}");
                    if (kind == SaveBackend.Firebase) firebase.Objects["users/guest/Slot1.meta.json"] = legacy;
                    else supabase.Objects["/storage/v1/object/test-bucket/users/guest/Slot1.meta.json"] = legacy;
                }
                var missing = storage.LoadSlotMetadataAsync(1);
                yield return Await(missing);
                Assert.That(missing.Result.SlotName, Is.EqualTo(kind == SaveBackend.MySQL ? "Slot 1" : null));
                Assert.That(missing.Result.CustomMetadata, Is.Empty);
                Assert.That(missing.Result.LastSaved.Ticks, Is.Zero);
            }
            finally { Object.Destroy(cfg); }
        }

        [UnityTest] public IEnumerator CloudImages_MySqlRawLoginUid() => CloudImageUid("alice");
        [UnityTest] public IEnumerator CloudImages_MySqlPrefixedUidControl() => CloudImageUid("users/alice");
        [UnityTest] public IEnumerator CloudImages_MySqlBuildScopeUidControl() => CloudImageUid("build/test-build");
        [UnityTest] public IEnumerator CloudImages_MySqlLegacyBuildUid() => CloudImageUid("build-test-build");
        [UnityTest] public IEnumerator CloudImages_MySqlEscapedUidAndFilename() => CloudImageUid("users/雪 #+% user");

        IEnumerator CloudImageUid(string uid)
        {
            using var server = new CloudNameMySqlServer(uid);
            var cfg = RestorationCloudSettings(server.Url);
            cfg.backend = SaveBackend.MySQL;
            cfg.userFolderStrategy = UserFolderStrategy.Shared;
            cfg.mySqlApiUrl = server.Url + "/saves";
            var storage = new MySqlSaveSystem(cfg, CloudRefreshRoot("image-uid"));
            const string file = "candidate #+% 雪.png";
            var bytes = new byte[] { 1, 2, 3 };
            try
            {
                var login = storage.LoginAsync("alice", "disposable");
                yield return Await(login);
                Assert.That(login.Result, Is.True);
                yield return Await(storage.UploadScreenshotAsync(1, file, bytes, ScreenshotFormat.PNG));
                Assert.That(server.UploadUid, Is.EqualTo(uid));
                Assert.That(server.UploadName, Is.EqualTo(file));
                var read = storage.DownloadScreenshotAsync(file);
                yield return Await(read);
                Assert.That(read.Result, Is.EqualTo(bytes), "raw=" + server.ImageRaw + "; decoded=" + server.ImagePath + "; upload=" + server.UploadUid + "/" + server.UploadName);
                Assert.That(server.ImagePath, Is.EqualTo("/saves/img/" + uid + "/" + file));
            }
            finally { Object.Destroy(cfg); }
        }

        sealed class CloudNameMySqlServer : IDisposable
        {
            readonly HttpListener listener = new();
            readonly Task loop;
            internal readonly string Url;
            internal JObject Metadata;
            internal string UploadUid, UploadName, ImagePath, ImageRaw;
            byte[] payload;

            internal CloudNameMySqlServer(string uid)
            {
                var reservation = new TcpListener(IPAddress.Loopback, 0);
                reservation.Start();
                int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
                reservation.Stop();
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
                            // Mono's HttpListener.Url reparses an escaped # as a URI
                            // fragment; RawUrl retains the actual request target.
                            string fullPath = Uri.UnescapeDataString(request.RawUrl.Split('?')[0]);
                            string path = fullPath.StartsWith("/saves/", StringComparison.Ordinal) ? fullPath.Substring(6) : fullPath;
                            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                            string body = await reader.ReadToEndAsync();
                            byte[] response = Array.Empty<byte>();
                            if (path == "/login") response = Encoding.UTF8.GetBytes(new JObject { ["uid"] = uid }.ToString());
                            else if (path == "/save" && request.HttpMethod == "POST") payload = Convert.FromBase64String((string)JObject.Parse(body)["data"]);
                            else if (path == "/load")
                            {
                                if (payload == null) context.Response.StatusCode = 404;
                                else response = Encoding.UTF8.GetBytes(Convert.ToBase64String(payload));
                            }
                            else if (path == "/metadata" && request.HttpMethod == "POST")
                            {
                                Metadata = JObject.Parse(body);
                                Metadata["meta"] = new JArray(((JObject)Metadata["meta"]).Properties().Select(pair => new JObject { ["key"] = pair.Name, ["value"] = pair.Value }));
                            }
                            else if (path == "/metadata")
                            {
                                if (Metadata == null) context.Response.StatusCode = 404;
                                else response = Encoding.UTF8.GetBytes(Metadata.ToString());
                            }
                            else if (path == "/uploadImage")
                            {
                                UploadUid = MultipartField(body, "uid");
                                UploadName = MultipartField(body, "name");
                            }
                            else if (path.StartsWith("/img/", StringComparison.Ordinal))
                            {
                                ImageRaw = request.RawUrl;
                                ImagePath = fullPath;
                                if (path == "/img/" + UploadUid + "/" + UploadName) response = new byte[] { 1, 2, 3 };
                                else context.Response.StatusCode = 404;
                            }
                            else context.Response.StatusCode = 404;
                            context.Response.ContentLength64 = response.Length;
                            await context.Response.OutputStream.WriteAsync(response, 0, response.Length);
                            context.Response.Close();
                        }
                    }
                    catch (HttpListenerException) when (!listener.IsListening) { }
                    catch (ObjectDisposedException) when (!listener.IsListening) { }
                });
            }

            static string MultipartField(string body, string name)
            {
                var match = Regex.Match(body, "name=\"" + name + "\"[^\\r\\n]*\\r\\n(?:[^\\r\\n]+\\r\\n)*\\r\\n([^\\r\\n]*)");
                if (!match.Success) throw new InvalidDataException("Missing multipart field: " + name);
                return match.Groups[1].Value;
            }

            public void Dispose()
            {
                listener.Stop();
                listener.Close();
                if (!loop.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("MySQL loopback fixture did not stop.");
            }
        }
    }
}
#endif
