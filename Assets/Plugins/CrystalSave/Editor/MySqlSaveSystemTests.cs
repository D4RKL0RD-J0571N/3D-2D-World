#if ARAWN_REMEMBERME && MEMORYPACK && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System.Linq;
using UnityEngine;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.EditorTests
{
    public class MySqlSaveSystemTests
    {
        [TestCase(false, null)]
        [TestCase(true, null)]
        [TestCase(false, "save")]
        [TestCase(true, "save")]
        [TestCase(true, "metadata")]
        public async Task SaveLoadDeleteCycle(bool keepMirror, string failEndpoint = null)
        {
            using var listener = new HttpListener();
            var portProbe = new TcpListener(IPAddress.Loopback, 0);
            portProbe.Start();
            int port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
            portProbe.Stop();
            string endpoint = $"http://localhost:{port}";
            listener.Prefixes.Add(endpoint + "/");
            listener.Start();

            bool rejectWrites = false;
            var serverTask = Task.Run(async () =>
            {
                var data = new Dictionary<int, byte[]>();
                var meta = new Dictionary<int, SlotMeta>();
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync(); }
                    catch (Exception) when (!listener.IsListening) { break; }
                    string action = ctx.Request.Url.AbsolutePath.Trim('/');
                    using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                    string body = await reader.ReadToEndAsync();
                    if (rejectWrites && action == failEndpoint && ctx.Request.HttpMethod == "POST")
                    {
                        ctx.Response.StatusCode = 500;
                        ctx.Response.OutputStream.Close();
                        continue;
                    }
                    switch (action)
                    {
                        case "save":
                            var saveReq = JsonUtility.FromJson<SaveReq>(body);
                            data[saveReq.slot] = Convert.FromBase64String(saveReq.data);
                            break;
                        case "load":
                            int lslot = int.Parse(ctx.Request.QueryString["slot"]);
                            if (data.TryGetValue(lslot, out var d))
                            {
                                byte[] buf = Encoding.UTF8.GetBytes(Convert.ToBase64String(d));
                                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                            }
                            break;
                        case "delete":
                            var delReq = JsonUtility.FromJson<DeleteReq>(body);
                            data.Remove(delReq.slot);
                            meta.Remove(delReq.slot);
                            break;
                        case "metadata":
                            if (ctx.Request.HttpMethod == "GET")
                            {
                                int metadataSlot = int.Parse(ctx.Request.QueryString["slot"]);
                                if (meta.TryGetValue(metadataSlot, out var entry))
                                {
                                    byte[] response = Encoding.UTF8.GetBytes(JsonUtility.ToJson(entry));
                                    ctx.Response.OutputStream.Write(response, 0, response.Length);
                                }
                                else ctx.Response.StatusCode = 404;
                                break;
                            }
                            var mReq = JsonUtility.FromJson<MetaReq>(body);
                            meta[mReq.slot] = new SlotMeta
                            {
                                slot = mReq.slot,
                                name = mReq.name,
                                ticks = mReq.ticks,
                                scene = mReq.scene,
                                shot = mReq.shot,
                                meta = ((JObject)JObject.Parse(body)["meta"]).Properties()
                                    .Select(p => new MetaPair { key = p.Name, value = (string)p.Value }).ToList()
                            };
                            break;
                        case "list":
                            var sb = new StringBuilder();
                            sb.Append('[');
                            bool first = true;
                            foreach (var m in meta.Values)
                            {
                                if (!first) sb.Append(',');
                                sb.Append(JsonUtility.ToJson(m));
                                first = false;
                            }
                            sb.Append(']');
                            byte[] outBuf = Encoding.UTF8.GetBytes(sb.ToString());
                            ctx.Response.OutputStream.Write(outBuf, 0, outBuf.Length);
                            break;
                    }
                    ctx.Response.OutputStream.Close();
                }
            });

            var settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.backend        = SaveBackend.MySQL;
            settings.mySqlApiUrl    = endpoint;
            settings.enableCloudSave = true;
            settings.tableName      = "CrystalSaveData";
            settings.keepLocalMirror = keepMirror;
            string root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            var system = new MySqlSaveSystem(settings, root);
            try
            {

                var slot = new SaveSlot(1, "Test", DateTime.UtcNow, string.Empty, "Scene");
                slot.CustomMetadata["Player"] = "Alice";
                byte[] bytes = new byte[] {1,2,3};
                await system.SaveAsync(bytes, slot);
                Assert.That(Directory.Exists(root), Is.EqualTo(keepMirror));
                var metaSlot = await system.LoadSlotMetadataAsync(slot.SlotNumber);
                Assert.AreEqual("Alice", metaSlot.CustomMetadata["Player"]);
                var list = await system.ListRemoteSlotsAsync();
                Assert.AreEqual("Alice", list[0].CustomMetadata["Player"]);
                var loaded = await system.LoadAsync(slot);
                Assert.AreEqual(bytes, loaded);
                if (failEndpoint != null)
                {
                    var before = Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes) : new Dictionary<string, byte[]>();
                    rejectWrites = true;
                    Exception failure = null;
                    try { await system.SaveAsync(new byte[] {9,8,7}, new SaveSlot(1, "Rejected", DateTime.UtcNow, "", "Scene")); }
                    catch (Exception ex) { failure = ex; }
                    Assert.That(failure, Is.Not.Null, "HTTP failure must reach the caller.");
                    if (failEndpoint == "metadata") Assert.That(failure.Message, Does.Contain("partially updated"));
                    foreach (var entry in before) Assert.That(File.ReadAllBytes(entry.Key), Is.EqualTo(entry.Value));
                    Assert.That(Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length : 0, Is.EqualTo(before.Count));
                    return;
                }
                await system.DeleteAsync(slot);
                var afterDelete = await system.LoadAsync(slot);
                Assert.IsNull(afterDelete);

            }
            finally
            {
                listener.Stop();
                try { await serverTask; }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(settings);
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
            }
        }

        [Serializable] class MetaPair { public string key; public string value; }
        [Serializable] class SaveReq { public string uid; public int slot; public string table; public string data; }
        [Serializable] class DeleteReq { public string uid; public int slot; public string table; }
        [Serializable] class MetaReq { public string uid; public int slot; public string table; public string name; public long ticks; public string scene; public string shot; }
        [Serializable] class SlotMeta { public int slot; public string name; public long ticks; public string scene; public string shot; public List<MetaPair> meta; }
        [Serializable] class SlotMetaList { public SlotMeta[] items; }
    }
}
#endif
