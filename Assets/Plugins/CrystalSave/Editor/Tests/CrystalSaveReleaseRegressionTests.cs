#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator ReleaseReview_ImportRollbackPreservesMetadataScreenshotsAndEmptySlots()
        {
            var manager = SaveManager.Instance;
            var settings = manager.SaveSettings;
            settings.saveFileName = "Slot{n}_{meta:profile}";
            settings.metadataFileNamePattern = "Slot{n}_{meta:profile}_Meta.bin";
            settings.enableSaveFileVerification = true;
            string root = manager.SaveSystem.GetSaveGamesPath();
            string images = Path.Combine(root, settings.screenshotFolderName);
            Directory.CreateDirectory(images);
            string sourceImage = Path.Combine(images, "source.png"), previousImage = Path.Combine(images, "previous.png");
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/a9sAAAAASUVORK5CYII=");
            var source = manager.GetSlotMetadata(2);
            source.CustomMetadata["profile"] = "after";
            source.ScreenshotFileName = "source.png";
            File.WriteAllBytes(sourceImage, png);
            yield return Await(manager.SaveImmediateAsync(2));
            var portable = manager.ExportSaveAsync(2);
            var bundle = manager.ExportSaveBundleBytesAsync(2);
            yield return Await(portable); yield return Await(bundle);
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction")
                .GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
            LogAssert.ignoreFailingMessages = true;
            try
            {
                foreach (bool useBundle in new[] { false, true })
                foreach (bool hadPrevious in new[] { false, true })
                {
                    yield return Await(manager.DeleteAsync(1));
                    var destination = manager.GetSlotMetadata(1);
                    destination.CustomMetadata["profile"] = "before";
                    destination.ScreenshotFileName = hadPrevious ? "previous.png" : null;
                    byte[] previous = null;
                    SaveSlot previousHeader = null;
                    if (hadPrevious)
                    {
                        File.WriteAllBytes(previousImage, png);
                        yield return Await(manager.SaveImmediateAsync(1));
                        previous = manager.SaveSystem.Load(destination);
                        var header = manager.SaveSystem.LoadSlotMetadataAsync(1);
                        yield return Await(header); previousHeader = header.Result;
                    }
                    var imagesBefore = Directory.GetFiles(images).OrderBy(path => path).ToArray();
                    var live = manager.CurrentSaveData;
                    bool injected = false;
                    hook.SetValue(null, (Action<string>)(stage =>
                    {
                        if (stage != "committed") return;
                        injected = true;
                        string incoming = Directory.GetFiles(root, "Slot1*.sav", SearchOption.AllDirectories)
                            .Single(path => Path.GetFileName(path) != "Slot1_before.sav");
                        File.WriteAllBytes(incoming, new byte[] { 0x7f });
                    }));
                    var request = useBundle ? manager.ImportSaveBundleAsync(bundle.Result, 1) : manager.ImportSaveAsync(portable.Result, 1);
                    yield return Await(request);
                    hook.SetValue(null, null);
                    Assert.That(injected, Is.True);
                    Assert.That(request.Result, Is.False);
                    Assert.That(manager.SaveSystem.Load(destination), Is.EqualTo(previous));
                    var retained = manager.SaveSystem.LoadSlotMetadataAsync(1);
                    yield return Await(retained);
                    Assert.That(retained.Result?.SlotName, Is.EqualTo(previousHeader?.SlotName));
                    Assert.That(retained.Result?.ScreenshotFileName, Is.EqualTo(previousHeader?.ScreenshotFileName));
                    if (hadPrevious) Assert.That(retained.Result.CustomMetadata["profile"], Is.EqualTo("before"));
                    Assert.That(Directory.GetFiles(images).OrderBy(path => path), Is.EqualTo(imagesBefore));
                    Assert.That(manager.CurrentSaveData, Is.SameAs(live));
                    Assert.That(Directory.GetFiles(root, "*.csav-verify"), Is.Empty);
                }
                yield return Await(manager.SaveImmediateAsync(1));
            }
            finally { hook.SetValue(null, null); LogAssert.ignoreFailingMessages = false; }
        }

        [UnityTest] public IEnumerator ReleaseReview_RawImportHonorsVerification()
            => ReleaseReviewImport(false, true);
        [UnityTest] public IEnumerator ReleaseReview_BundleImportHonorsVerification()
            => ReleaseReviewImport(true, true);
        [UnityTest] public IEnumerator ReleaseReview_HealthyImportControl()
            => ReleaseReviewImport(false, false);

        IEnumerator ReleaseReviewImport(bool bundle, bool corrupt)
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveImmediateAsync(1));
            var bytesBefore = manager.SaveSystem.Load(manager.GetSlotMetadata(1));
            var exported = bundle ? manager.ExportSaveBundleBytesAsync(1) : manager.ExportSaveAsync(1);
            yield return Await(exported);
            manager.SaveSettings.enableSaveFileVerification = true;
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction")
                .GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
            bool injected = false;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                if (corrupt) hook.SetValue(null, (Action<string>)(stage =>
                {
                    if (stage != "committed") return;
                    injected = true;
                    File.WriteAllBytes(Directory.GetFiles(manager.SaveSystem.GetSaveGamesPath(), "*.sav", SearchOption.AllDirectories).Single(), new byte[] { 0x7f });
                }));
                var import = bundle ? manager.ImportSaveBundleAsync(exported.Result, 1) : manager.ImportSaveAsync(exported.Result, 1);
                yield return Await(import);
                var actual = manager.SaveSystem.Load(manager.GetSlotMetadata(1));
                TestContext.WriteLine($"bundle={bundle}, injected={injected}, success={import.Result}, previousBytes={bytesBefore.Length}, actualBytes={actual?.Length}, preserved={bytesBefore.SequenceEqual(actual ?? Array.Empty<byte>())}");
                Assert.That(injected, Is.EqualTo(corrupt));
                Assert.That(import.Result, Is.EqualTo(!corrupt), "Imports must honor the configured verification outcome.");
                if (corrupt)
                    Assert.That(actual, Is.EqualTo(bytesBefore), "A rejected import must retain the previous generation.");
                else
                {
                    // Portable import intentionally normalizes the screenshot reference.
                    // The healthy control compares decoded gameplay state, not wire identity.
                    var restored = manager.Serializer.Deserialize<SaveData>(actual);
                    var prior = manager.Serializer.Deserialize<SaveData>(bytesBefore);
                    Assert.That(restored, Is.Not.Null);
                    Assert.That(restored.LastSaved, Is.EqualTo(prior.LastSaved));
                    Assert.That(restored.LastActiveScene, Is.EqualTo(prior.LastActiveScene));
                    Assert.That(restored.Version, Is.EqualTo(prior.Version));
                }
            }
            finally { hook.SetValue(null, null); LogAssert.ignoreFailingMessages = false; }
        }
    }

    public sealed class CrystalSaveReleaseRegressionTests
    {
        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        public async Task ReleaseReview_LegacySaveDiscoveryMatchesRead(bool legacyRoot, bool changedName, bool recoveryOnly)
        {
            var settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = false;
            settings.enableEncryption = false;
            settings.saveMethod = SaveMethod.BinaryFileFormat;
            settings.saveFileName = "Slot{n}_{meta:profile}";
            settings.metadataFileNamePattern = "Slot{n}_Meta.bin";
            string root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new SaveSystem(settings, root);
                var slot = new SaveSlot(1, "Existing save", DateTime.UtcNow, "", "A");
                slot.CustomMetadata["profile"] = "before";
                byte[] payload = SaveDataSerializer.Instance.Serialize(new SaveData(new VersionData(2, 0, 0)));
                storage.Save(payload, slot);
                if (legacyRoot)
                    foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".sav") || f.EndsWith(".bin")))
                        File.Move(file, Path.Combine(root, Path.GetFileName(file)));
                if (recoveryOnly)
                {
                    string path = Directory.GetFiles(root, "*.sav", SearchOption.AllDirectories).Single();
                    File.Move(path, path + ".previous");
                }
                var slots = new SaveSlotManager(storage, settings, null, root);
                await slots.InitializeAsync(1);
                Assert.That(slots.GetByNumber(1).SlotName, Is.EqualTo("Existing save"), "metadata discovery control");
                if (changedName) slots.GetByNumber(1).CustomMetadata["profile"] = "different";
                bool exists = await slots.HasSaveAtAsync(1);
                Assert.That(storage.Load(slots.GetByNumber(1)), Is.EqualTo(payload), "payload read control");
                TestContext.WriteLine($"legacyRoot={legacyRoot}, storageRead=True, metadataRead=True, hasSave={exists}");
                Assert.That(exists, Is.True, "A supported loadable save must remain visible to the public existence queries.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestCase(typeof(MySqlSaveSystem))]
        [TestCase(typeof(FirebaseSaveSystem))]
        [TestCase(typeof(SupabaseSaveSystem))]
        public async Task ReleaseReview_MirrorPresenceDoesNotContactCloud(Type type)
        {
            var settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = true;
            settings.keepLocalMirror = true;
            settings.enableEncryption = false;
            settings.saveMethod = SaveMethod.BinaryFileFormat;
            settings.saveFileName = "Mirror";
            settings.userFolderStrategy = UserFolderStrategy.Shared;
            settings.mySqlApiUrl = settings.supabaseUrl = "http://127.0.0.1:1";
            string root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            try
            {
                var storage = (ISaveSystem)Activator.CreateInstance(type, settings, root);
                var slot = new SaveSlot(1, "Mirror", DateTime.UtcNow, "", "A");
                var write = type.GetMethod("WriteLocalMirrorAsync", BindingFlags.Instance | BindingFlags.NonPublic);
                await (Task)write.Invoke(storage, new object[] { new byte[] { 1, 2, 3 }, slot });
                var slots = new SaveSlotManager(storage, settings, null, root);
                slots.Slots.Add(slot);
                Assert.That(await slots.HasSaveAtAsync(1), Is.True);
                Assert.That(Get<System.Collections.IDictionary>(slots, "cloudProbes"), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestCase(false, 99, "Foreign profile")]
        [TestCase(true, 99, "Foreign profile")]
        [TestCase(false, 0, "Foreign profile")]
        [TestCase(true, -1, "Foreign profile")]
        [TestCase(false, 1, "Foreign profile")]
        [TestCase(false, 1, "")]
        [TestCase(true, 1, "")]
        [TestCase(false, 1, null)]
        public async Task ReleaseReview_MySqlMetadataIdentity(bool conflictRead, int returnedSlot, string name)
        {
            using var listener = new HttpListener();
            var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            string url = $"http://127.0.0.1:{port}";
            listener.Prefixes.Add(url + "/");
            listener.Start();
            var serving = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext request;
                    try { request = await listener.GetContextAsync(); }
                    catch (Exception) when (!listener.IsListening) { break; }
                    string nameField = name == null ? "" : "\"name\":\"" + name + "\",";
                    string body = request.Request.Url.AbsolutePath == "/load" ? "AQID" :
                        "{" + nameField + "\"slot\":" + returnedSlot + ",\"ticks\":638712864000000000,\"scene\":\"A\",\"shot\":\"\",\"meta\":[{\"key\":\"profile\",\"value\":\"foreign\"}]}";
                    if (request.Request.Url.AbsolutePath == "/list") body = "[" + body + "]";
                    byte[] response = Encoding.UTF8.GetBytes(body);
                    request.Response.ContentLength64 = response.Length;
                    await request.Response.OutputStream.WriteAsync(response, 0, response.Length);
                    request.Response.Close();
                }
            });
            var settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.backend = SaveBackend.MySQL;
            settings.enableCloudSave = true;
            settings.keepLocalMirror = false;
            settings.mySqlApiUrl = url;
            settings.userFolderStrategy = UserFolderStrategy.Shared;
            string root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new MySqlSaveSystem(settings, root);
                SaveSlot result = null;
                Exception error = null;
                try
                {
                    if (conflictRead)
                        result = (await storage.ReadCandidatesAsync(new SaveSlot(1, "Requested", DateTime.MinValue, "", ""), CancellationToken.None)).Remote.Metadata;
                    else result = await storage.LoadSlotMetadataAsync(1);
                }
                catch (Exception ex) { error = ex; }
                TestContext.WriteLine($"requested=1, returned={returnedSlot}, conflict={conflictRead}, acceptedAs={result?.SlotNumber}, metadata={result?.SlotName}, error={error}");
                if (returnedSlot == 1)
                {
                    Assert.That(error, Is.Null);
                    Assert.That(result.SlotNumber, Is.EqualTo(1));
                    Assert.That(result.SlotName, Is.EqualTo(name ?? "Slot 1"));
                    var listed = await storage.ListRemoteSlotsAsync();
                    Assert.That(listed.Single().SlotName, Is.EqualTo(name ?? "Slot 1"));
                }
                else Assert.That(error, Is.InstanceOf<InvalidDataException>(), "Metadata from another slot must be rejected before publishing or selecting it.");
            }
            finally
            {
                listener.Stop();
                await serving;
                UnityEngine.Object.DestroyImmediate(settings);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
#endif
