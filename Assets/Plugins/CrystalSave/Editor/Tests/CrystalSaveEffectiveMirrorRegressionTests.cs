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
        [UnityTest] public IEnumerator EffectiveMirror_MySqlPlayerPrefsVerifiesAndRotates() => EffectiveMirrorCase(SaveBackend.MySQL);
        [UnityTest] public IEnumerator EffectiveMirror_FirebasePlayerPrefsVerifiesAndRotates() => EffectiveMirrorCase(SaveBackend.Firebase);
        [UnityTest] public IEnumerator EffectiveMirror_SupabasePlayerPrefsVerifiesAndRotates() => EffectiveMirrorCase(SaveBackend.Supabase);

        IEnumerator EffectiveMirrorCase(SaveBackend backend)
        {
            using var server = new EffectiveMirrorServer();
            var manager = SaveManager.Instance;
            var previousSettings = manager.SaveSettings;
            var settings = Object.Instantiate(previousSettings);
            var previousStorage = manager.SaveSystem;
            var receiver = new EffectiveMirrorReceiver();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var quickProperty = typeof(SaveManager).GetProperty("QuickSlotManager", flags);
            var previousQuick = quickProperty.GetValue(manager);
            var previousQuickSlots = new List<SaveSlot>(Get<List<SaveSlot>>(manager, "quickSaveSlots"));
            bool ready = SaveManager.AreQuickSlotsReady;
            string root = Path.Combine(previousStorage.GetSaveGamesPath(), "effective-mirror-" + backend);
            settings.backend = backend;
            settings.enableCloudSave = settings.keepLocalMirror = settings.enableSaveFileVerification = true;
            settings.enableEncryption = settings.enableCompression = settings.enableScreenshots = false;
            settings.cloudCryptoMode = CloudCryptoMode.ClientSide;
            settings.cloudSaveTransport = CloudSaveTransport.Binary;
            settings.saveMethod = SaveMethod.PlayerPrefs;
            settings.userFolderStrategy = UserFolderStrategy.Shared;
            settings.saveFileName = "Slot";
            settings.mySqlApiUrl = settings.supabaseUrl = server.Url;
            settings.bucket = settings.firebaseBucket = "test-bucket";
            settings.firebaseIdToken = settings.supabaseAnonKey = "loopback-test";
            ISaveSystem storage = backend == SaveBackend.MySQL ? new MySqlSaveSystem(settings, root) :
                backend == SaveBackend.Firebase ? new FirebaseSaveSystem(settings, root) : new SupabaseSaveSystem(settings, root);
            if (storage is FirebaseSaveSystem) Set(storage, "url", server.Url);
            Set(manager, "saveSettings", settings);
            Set(manager, "saveSystem", storage);
            manager.ComponentManager.RegisterSaveableComponent(receiver);
            try
            {
                receiver.Value = 9;
                yield return Await(manager.SaveImmediateAsync(1));
                yield return AssertRemoteValue(storage, manager.GetSlotMetadata(1), receiver, 9);
                yield return Await(manager.InitializeQuickSaveSlotsAsync(2));
                foreach (bool verify in new[] { true, false })
                {
                    settings.enableSaveFileVerification = verify;
                    receiver.Value = verify ? (byte)11 : (byte)31;
                    yield return Await(manager.QuickSaveAsync());
                    receiver.Value++;
                    yield return Await(manager.QuickSaveAsync());
                    yield return AssertRemoteValue(storage, manager.GetSlotMetadata(settings.quickSaveSlotOffset + 1), receiver, receiver.Value);
                    yield return AssertRemoteValue(storage, manager.GetSlotMetadata(settings.quickSaveSlotOffset + 2), receiver, (byte)(receiver.Value - 1));
                    Assert.That(Directory.GetFiles(root, "*", SearchOption.AllDirectories), Is.Empty,
                        "Cloud-only verification must retire staging files and must not invent a mirror.");
                }
            }
            finally
            {
                manager.ComponentManager.UnregisterSaveableComponent(receiver);
                Set(manager, "saveSettings", previousSettings);
                Set(manager, "saveSystem", previousStorage);
                quickProperty.SetValue(manager, previousQuick);
                var quickSlots = Get<List<SaveSlot>>(manager, "quickSaveSlots");
                quickSlots.Clear(); quickSlots.AddRange(previousQuickSlots);
                typeof(SaveManager).GetProperty("AreQuickSlotsReady").SetValue(null, ready);
                Object.Destroy(settings);
            }
        }

        static IEnumerator AssertRemoteValue(ISaveSystem storage, SaveSlot slot, EffectiveMirrorReceiver receiver, byte expected)
        {
            var read = storage.LoadAsync(slot);
            yield return Await(read);
            var data = SaveDataSerializer.Instance.Deserialize<SaveData>(read.Result);
            Assert.That(data.ComponentsData[receiver.UniqueIdentifier], Is.EqualTo(new[] { expected }));
            var header = storage.LoadSlotMetadataAsync(slot.SlotNumber);
            yield return Await(header);
            Assert.That(header.Result.LastSaved, Is.EqualTo(data.LastSaved));
        }

        sealed class EffectiveMirrorReceiver : ISaveable
        {
            internal byte Value;
            public string UniqueIdentifier => "effective-mirror_component";
            public byte[] SaveData() => new[] { Value };
            public void LoadData(byte[] bytes) => Value = bytes[0];
        }

        sealed class EffectiveMirrorServer : IDisposable
        {
            readonly HttpListener listener = new();
            readonly Task loop;
            readonly Dictionary<string, byte[]> objects = new();
            internal readonly string Url;
            internal EffectiveMirrorServer()
            {
                var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
                int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
                Url = "http://127.0.0.1:" + port;
                listener.Prefixes.Add(Url + "/"); listener.Start();
                loop = Task.Run(async () =>
                {
                    try
                    {
                        while (listener.IsListening)
                        {
                            var context = await listener.GetContextAsync();
                            var request = context.Request;
                            string path = Uri.UnescapeDataString(request.Url.AbsolutePath);
                            byte[] response;
                            if (request.HttpMethod == "POST" || request.HttpMethod == "PUT")
                            {
                                using var body = new MemoryStream(); await request.InputStream.CopyToAsync(body);
                                byte[] bytes = body.ToArray();
                                if (path == "/save" || path == "/metadata")
                                {
                                    var json = JObject.Parse(Encoding.UTF8.GetString(bytes));
                                    string key = path + "/" + (int)json["slot"];
                                    if (path == "/save") bytes = Encoding.UTF8.GetBytes((string)json["data"]);
                                    else
                                    {
                                        json["meta"] = new JArray(((JObject)json["meta"]).Properties().Select(pair =>
                                            new JObject { ["key"] = pair.Name, ["value"] = pair.Value }));
                                        bytes = Encoding.UTF8.GetBytes(json.ToString());
                                    }
                                    objects[key] = bytes;
                                }
                                else objects[path == "/o" ? "/o/" + request.QueryString["name"] : path] = bytes;
                                response = Encoding.UTF8.GetBytes("{}");
                            }
                            else
                            {
                                string key = path == "/load" ? "/save/" + request.QueryString["slot"] :
                                    path == "/metadata" ? path + "/" + request.QueryString["slot"] : path;
                                if (!objects.TryGetValue(key, out response))
                                { context.Response.StatusCode = 404; response = Array.Empty<byte>(); }
                            }
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
                listener.Stop(); listener.Close();
                if (loop.IsFaulted) throw new InvalidOperationException("Mirror routing fixture failed", loop.Exception);
            }
        }
    }
}
#endif
