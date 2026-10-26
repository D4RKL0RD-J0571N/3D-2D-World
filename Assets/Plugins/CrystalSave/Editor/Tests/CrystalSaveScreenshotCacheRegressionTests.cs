#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator ScreenshotCache_EncryptedDownloadRetainsEnvelope() => ScreenshotCacheRoundTrip(true, true);
        [UnityTest] public IEnumerator ScreenshotCache_PlainDownloadControl() => ScreenshotCacheRoundTrip(false, false);
        [UnityTest] public IEnumerator ScreenshotCache_LegacyPlainDownloadUpgradesEncryption() => ScreenshotCacheRoundTrip(true, false);

        IEnumerator ScreenshotCacheRoundTrip(bool encrypt, bool encryptedUpload)
        {
            using var fixture = new ScreenshotCacheFixture(encrypt);
            yield return Await(fixture.Initialize());
            byte[] wire = encryptedUpload ? fixture.Encryption.MaybeEncrypt(fixture.Plain) : fixture.Plain;
            const string name = "download.png";
            yield return Await(fixture.Storage.UploadScreenshotAsync(name, wire, ScreenshotFormat.PNG));
            var task = fixture.Images.DownloadScreenshotAsync(ScreenshotCacheSlot(name));
            yield return Await(task);
            Assert.That(task.Result, Is.Not.Null);
            Object.Destroy(task.Result);
            byte[] cached = File.ReadAllBytes(fixture.PathFor(name));
            Assert.That(SaveCrypto.HasEnvelope(cached), Is.EqualTo(encrypt));
            Assert.That(fixture.Encryption.MaybeDecrypt(cached), Is.EqualTo(fixture.Plain));
            if (encryptedUpload) Assert.That(cached, Is.EqualTo(wire), "Retain the authenticated downloaded envelope");
            var local = fixture.Images.LoadScreenshot(ScreenshotCacheSlot(name));
            Assert.That(local, Is.Not.Null);
            Object.Destroy(local);
        }

        [UnityTest]
        public IEnumerator ScreenshotCache_TamperedDownloadPreservesExistingCache()
        {
            using var fixture = new ScreenshotCacheFixture(true);
            yield return Await(fixture.Initialize());
            byte[] prior = fixture.Encryption.MaybeEncrypt(fixture.Plain);
            const string name = "download.png";
            File.WriteAllBytes(fixture.PathFor(name), prior);
            byte[] tampered = (byte[])prior.Clone(); tampered[tampered.Length - 1] ^= 1;
            yield return Await(fixture.Storage.UploadScreenshotAsync(name, tampered, ScreenshotFormat.PNG));
            LogAssert.Expect(LogType.Error, new Regex("Load failed: save file was encrypted with a different master secret"));
            var download = fixture.Images.DownloadScreenshotAsync(ScreenshotCacheSlot(name));
            yield return Await(download);
            Assert.That(download.Result, Is.Null);
            Assert.That(File.ReadAllBytes(fixture.PathFor(name)), Is.EqualTo(prior));
        }

        [UnityTest]
        public IEnumerator ScreenshotCache_CopyUpgradesLegacyPlaintextBeforeUpload()
        {
            using var fixture = new ScreenshotCacheFixture(true);
            yield return Await(fixture.Initialize());
            const string name = "legacy.png";
            File.WriteAllBytes(fixture.PathFor(name), fixture.Plain);
            string synchronous = fixture.Images.DuplicateScreenshot(name, 8);
            Assert.That(SaveCrypto.HasEnvelope(File.ReadAllBytes(fixture.PathFor(synchronous))), Is.True);
            // Exercise the asynchronous rotation staging helper before its operation-owned
            // publish callback, avoiding a second queue or unrelated gameplay capture.
            fixture.Settings.enableCloudSave = false;
            var prepare = typeof(ScreenshotManager).GetMethod("PrepareScreenshotCopyAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            var copy = (Task<string>)prepare.Invoke(fixture.Images, new object[] { name, 9 });
            yield return Await(copy);
            byte[] encoded = File.ReadAllBytes(fixture.PathFor(copy.Result));
            Assert.That(SaveCrypto.HasEnvelope(encoded), Is.True);
            Assert.That(fixture.Encryption.MaybeDecrypt(encoded), Is.EqualTo(fixture.Plain));
            fixture.Settings.enableCloudSave = true;
            var upload = (Func<Task>)typeof(ScreenshotManager).GetMethod("PrepareScreenshotUpload", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture.Images, new object[] { 9, copy.Result, encoded });
            yield return Await(upload());
            var remote = fixture.Storage.DownloadScreenshotAsync(copy.Result);
            yield return Await(remote);
            Assert.That(remote.Result, Is.EqualTo(encoded));
            Assert.That(SaveCrypto.HasEnvelope(remote.Result), Is.True);
            Assert.That(File.ReadAllBytes(fixture.PathFor(name)), Is.EqualTo(fixture.Plain), "Reading a legacy source does not rewrite its generation");
        }

        [UnityTest]
        public IEnumerator ScreenshotCache_PathNamesCannotReadWriteDeleteOrCopyOutsideFolder()
        {
            using var fixture = new ScreenshotCacheFixture(false);
            yield return Await(fixture.Initialize());
            string outside = Path.Combine(fixture.Root, "escaped.png");
            File.WriteAllBytes(outside, fixture.Plain);
            yield return Await(fixture.Storage.UploadScreenshotAsync("../escaped.png", fixture.Plain, ScreenshotFormat.PNG));
            int reads = fixture.Server.Reads;
            string[] invalid = { "../escaped.png", "..\\escaped.png", "folder/image.png", "folder\\image.png", outside,
                "C:\\escaped.png", "C:escaped.png", "image\0.png", ".", "..", new string('x', 256) + ".png" };
            var prepare = typeof(ScreenshotManager).GetMethod("PrepareScreenshotCopyAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (string name in invalid)
            {
                var slot = ScreenshotCacheSlot(name);
                Assert.That(fixture.Images.LoadScreenshot(slot), Is.Null, name);
                var download = fixture.Images.DownloadScreenshotAsync(slot);
                yield return Await(download);
                Assert.That(download.Result, Is.Null, name);
                fixture.Images.DeleteScreenshot(name);
                Assert.That(fixture.Images.DuplicateScreenshot(name, 8), Is.Null, name);
                var copy = (Task<string>)prepare.Invoke(fixture.Images, new object[] { name, 9 });
                yield return Await(copy);
                Assert.That(copy.Result, Is.Null, name);
                Assert.That(File.ReadAllBytes(outside), Is.EqualTo(fixture.Plain), name);
            }
            Assert.That(fixture.Server.Reads, Is.EqualTo(reads), "Reject path-bearing headers before provider reads");
        }

        [UnityTest]
        public IEnumerator ScreenshotCache_SafeUnicodeFilenameControl()
        {
            using var fixture = new ScreenshotCacheFixture(true);
            yield return Await(fixture.Initialize());
            const string name = "saved image 雪 #+%.png";
            // Local path/cache behavior is the subject here; REST URL escaping has its
            // own strict request-target fixture in CrystalSaveCloudIdentityAndNamesTests.
            File.WriteAllBytes(fixture.PathFor(name), fixture.Encryption.MaybeEncrypt(fixture.Plain));
            var image = fixture.Images.LoadScreenshot(ScreenshotCacheSlot(name));
            Assert.That(image, Is.Not.Null);
            Object.Destroy(image);
            Assert.That(fixture.Images.DuplicateScreenshot(name, 3), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ScreenshotCache_DownloadTimeoutIgnoresLateResult()
        {
            using var fixture = new ScreenshotCacheFixture(true);
            yield return Await(fixture.Initialize());
            const string name = "late.png";
            yield return Await(fixture.Storage.UploadScreenshotAsync(name, fixture.Encryption.MaybeEncrypt(fixture.Plain), ScreenshotFormat.PNG));
            fixture.Server.ReadGate = new TaskCompletionSource<bool>();
            var download = fixture.Images.DownloadScreenshotAsync(ScreenshotCacheSlot(name), 1);
            yield return Until(() => download.IsCompleted, "Screenshot deadline", 3);
            Assert.That(download.Result, Is.Null);
            Assert.That(File.Exists(fixture.PathFor(name)), Is.False);
            fixture.Server.ReadGate.TrySetResult(true);
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(File.Exists(fixture.PathFor(name)), Is.False, "An aborted/late provider response cannot write the cache");
        }

        [UnityTest]
        public IEnumerator ScreenshotCache_EditorPreviewDecodesEncryptedDownload()
        {
            using var fixture = new ScreenshotCacheFixture(true);
            yield return Await(fixture.Initialize());
            const string name = "editor.png";
            yield return Await(fixture.Storage.UploadScreenshotAsync(name, fixture.Encryption.MaybeEncrypt(fixture.Plain), ScreenshotFormat.PNG));
            var window = ScriptableObject.CreateInstance<Arawn.CrystalSave.Editor.SaveSlotManagerWindow>();
            try
            {
                Set(window, "supabase", fixture.Storage);
                var task = (Task<Texture2D>)typeof(Arawn.CrystalSave.Editor.SaveSlotManagerWindow)
                    .GetMethod("DownloadScreenshotAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(window, new object[] { name });
                yield return Await(task);
                Assert.That(task.Result, Is.Not.Null);
                Object.Destroy(task.Result);
            }
            finally { Object.DestroyImmediate(window); }
        }

        static SaveSlot ScreenshotCacheSlot(string file) => new SaveSlot(1, "Image", default, file, "");

        [UnityTest]
        public IEnumerator ScreenshotCache_BundleExportRejectsPathBearingImageReferences()
        {
            var manager = SaveManager.Instance;
            string folder = Path.Combine(manager.RootPath, manager.SaveSettings.screenshotFolderName);
            Directory.CreateDirectory(folder);
            var texture = new Texture2D(16, 9);
            byte[] png = texture.EncodeToPNG();
            Object.Destroy(texture);
            string owned = Path.Combine(folder, "owned.png");
            File.WriteAllBytes(owned, png);
            foreach (string name in new[] { "owned.png", "../owned.png", "nested/owned.png" })
            {
                manager.GetSlotMetadata(1).ScreenshotFileName = name;
                yield return Await(manager.SaveImmediateAsync(1));
                File.WriteAllBytes(owned, png);
                var export = manager.ExportSaveBundleBytesAsync(1);
                yield return Await(export);
                var import = manager.ImportSaveBundleAsync(export.Result, 2);
                yield return Await(import);
                Assert.That(import.Result, Is.True);
                Assert.That(!string.IsNullOrEmpty(manager.GetSlotMetadata(2).ScreenshotFileName),
                    Is.EqualTo(name == "owned.png"), "Do not turn an invalid remote path into a different cache filename.");
                Assert.That(File.ReadAllBytes(owned), Is.EqualTo(png));
            }
        }

        sealed class ScreenshotCacheFixture : IDisposable
        {
            internal readonly RestorationCloudServer Server = new();
            internal readonly SaveSettings Settings;
            internal readonly SupabaseSaveSystem Storage;
            internal readonly ScreenshotManager Images;
            internal readonly EncryptionService Encryption;
            internal readonly string Root;
            internal readonly byte[] Plain;
            readonly TestSecret secret;
            readonly SaveManager manager;
            readonly ISaveSystem previousStorage;
            readonly object previousEncryption;
            readonly PropertyInfo encryptionProperty = typeof(SaveManager).GetProperty("EncryptionService", BindingFlags.Instance | BindingFlags.NonPublic);

            internal ScreenshotCacheFixture(bool encrypt)
            {
                Settings = RestorationCloudSettings(Server.Url);
                Settings.backend = SaveBackend.Supabase; Settings.enableScreenshots = true; Settings.keepLocalMirror = true;
                Settings.enableEncryption = Settings.encryptScreenshots = encrypt;
                Settings.cloudCryptoMode = CloudCryptoMode.ClientSide;
                secret = ScriptableObject.CreateInstance<TestSecret>(); Settings.masterSecretProvider = secret;
                Root = CloudRefreshRoot("screenshot-cache");
                Storage = new SupabaseSaveSystem(Settings, Root);
                Images = new ScreenshotManager(Settings, Root);
                Encryption = new EncryptionService(Settings);
                manager = SaveManager.Instance; previousStorage = manager.SaveSystem;
                previousEncryption = encryptionProperty.GetValue(manager);
                encryptionProperty.SetValue(manager, Encryption); Set(manager, "saveSystem", Storage);
                var texture = new Texture2D(16, 9); Plain = texture.EncodeToPNG(); Object.Destroy(texture);
            }
            internal Task Initialize() => Encryption.InitializeAsync("download-image-user");
            internal string PathFor(string name) => Path.Combine(Root, Settings.screenshotFolderName, name);
            public void Dispose()
            {
                Set(manager, "saveSystem", previousStorage); encryptionProperty.SetValue(manager, previousEncryption);
                Object.Destroy(Settings); Object.Destroy(secret); Server.Dispose();
            }
        }
    }
}
#endif
