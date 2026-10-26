#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveScreenshotDeletionRegressionTests
    {
        string root;
        SaveSettings settings;
        const string Image = "sentinel.png";
        static readonly byte[] Sentinel = { 8, 3, 5 };
        static readonly byte[] Payload = { 1, 2, 3 };

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = false;
            settings.enableEncryption = false;
            settings.enableScreenshots = true;
            settings.enableSaveFileVerification = false;
            settings.keepLocalMirror = true;
            settings.saveMethod = SaveMethod.BinaryFileFormat;
            settings.saveKey = "ScreenshotDeletion_" + Guid.NewGuid().ToString("N");
            settings.saveFileName = "Mirror";
            settings.metadataFileNamePattern = "Slot{n}_Meta.bin";
            settings.userFolderStrategy = UserFolderStrategy.Shared;
            settings.mySqlApiUrl = settings.supabaseUrl = "http://127.0.0.1:1";
            Directory.CreateDirectory(Path.Combine(root, settings.screenshotFolderName));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(settings);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        static SaveSlot Slot(string image) => new SaveSlot(1, "Delete image", new DateTime(2025, 1, 1), image, "Scene");

        IEnumerable<string> InvalidReferences()
        {
            yield return "../" + Image;
            yield return "..\\" + Image;
            yield return "folder/" + Image;
            yield return "folder\\" + Image;
            yield return Path.Combine(root, Image);
        }

        string[] WriteSentinels(string image = Image)
        {
            string[] paths =
            {
                Path.Combine(root, image),
                Path.Combine(root, image + ".previous"),
                Path.Combine(root, settings.screenshotFolderName, image),
                Path.Combine(root, settings.screenshotFolderName, image + ".previous")
            };
            foreach (string path in paths) File.WriteAllBytes(path, Sentinel);
            return paths;
        }

        static void AssertSentinels(string[] paths, string reference)
        {
            foreach (string path in paths)
            {
                Assert.That(File.Exists(path), Is.True, reference + " removed " + path);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(Sentinel), reference + " changed " + path);
            }
        }

        static Task Delete(SaveSystem storage, SaveSlot slot, string route)
        {
            if (route == "sync") { storage.Delete(slot); return Task.CompletedTask; }
            return route == "local" ? storage.DeleteLocalSlotAsync(slot) : storage.DeleteAsync(slot);
        }

        internal async Task AssertLocalSlotDeletionRejectsPathBearingImageReferences(string route)
        {
            var storage = new SaveSystem(settings, root);
            foreach (string reference in InvalidReferences())
            {
                var slot = Slot(reference);
                storage.Save(Payload, slot);
                string[] sentinels = WriteSentinels();
                await Delete(storage, slot, route);
                AssertSentinels(sentinels, reference);
                var restarted = new SaveSystem(settings, root);
                Assert.That(restarted.Load(slot), Is.Null, "An invalid image must not prevent payload deletion.");
                Assert.That(await restarted.LoadSlotMetadataAsync(1), Is.Null);
            }
        }

        internal async Task AssertLocalSlotDeletionRemovesSafeImageAndItsRecoveryCopy(string route)
        {
            var storage = new SaveSystem(settings, root);
            var slot = Slot(Image);
            storage.Save(Payload, slot);
            string[] sentinels = WriteSentinels();
            await Delete(storage, slot, route);
            Assert.That(File.ReadAllBytes(sentinels[0]), Is.EqualTo(Sentinel), "Local screenshots do not own root files.");
            Assert.That(File.Exists(sentinels[1]), Is.True);
            Assert.That(File.Exists(sentinels[2]), Is.False);
            Assert.That(File.Exists(sentinels[3]), Is.False);
        }

        ISaveSystem CreateMirror(Type backend) => (ISaveSystem)Activator.CreateInstance(backend, settings, root);

        static Task WriteMirror(ISaveSystem storage, SaveSlot slot) => (Task)storage.GetType()
            .GetMethod("WriteLocalMirrorAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(storage, new object[] { Payload, slot });

        static Task DeleteMirror(ISaveSystem storage, SaveSlot slot) => (Task)storage.GetType()
            .GetMethod("DeleteLocalSlotAsync", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(storage, new object[] { slot });

        [TestCase(typeof(MySqlSaveSystem))] [TestCase(typeof(FirebaseSaveSystem))] [TestCase(typeof(SupabaseSaveSystem))]
        public async Task ScreenshotDeletion_CloudMirrorDeletionRejectsPathBearingImageReferences(Type backend)
        {
            var storage = CreateMirror(backend);
            foreach (string reference in InvalidReferences())
            {
                var slot = Slot(reference);
                await WriteMirror(storage, slot);
                string[] sentinels = WriteSentinels();
                await DeleteMirror(storage, slot);
                AssertSentinels(sentinels, reference);
                CreateMirror(backend);
                Assert.That(File.Exists(Path.Combine(root, "Mirror1.sav")), Is.False);
                Assert.That(File.Exists(Path.Combine(root, "Slot1_Meta.bin")), Is.False);
            }
        }

        [TestCase(typeof(MySqlSaveSystem))] [TestCase(typeof(FirebaseSaveSystem))] [TestCase(typeof(SupabaseSaveSystem))]
        public async Task ScreenshotDeletion_CloudMirrorDeletionRemovesLegacyAndCurrentSafeImages(Type backend)
        {
            var storage = CreateMirror(backend);
            foreach (string extension in new[] { ".png", ".jpg", ".jpeg", ".PNG" })
            {
                string image = "safe image 雪" + extension;
                var slot = Slot(image);
                await WriteMirror(storage, slot);
                string[] sentinels = WriteSentinels(image);
                await DeleteMirror(storage, slot);
                foreach (string path in sentinels) Assert.That(File.Exists(path), Is.False, path);
            }
        }

        [TestCase(typeof(MySqlSaveSystem))] [TestCase(typeof(FirebaseSaveSystem))] [TestCase(typeof(SupabaseSaveSystem))]
        public async Task ScreenshotDeletion_CloudMirrorImageReferenceCannotDeleteAnotherSlotPayload(Type backend)
        {
            var storage = CreateMirror(backend);
            var other = new SaveSlot(11, "Preserve", new DateTime(2025, 1, 1), null, "Scene");
            await WriteMirror(storage, other);
            const string image = "Mirror11.sav";
            string otherPayload = Path.Combine(root, image);
            string otherHeader = Path.Combine(root, settings.ResolveSlotFolderName(11), "Slot11_Meta.bin");
            byte[] header = File.ReadAllBytes(otherHeader);
            var slot = Slot(image);
            await WriteMirror(storage, slot);
            string cached = Path.Combine(root, settings.screenshotFolderName, image);
            File.WriteAllBytes(cached, Sentinel);
            await DeleteMirror(storage, slot);
            CreateMirror(backend);
            Assert.That(File.ReadAllBytes(otherPayload), Is.EqualTo(Payload));
            Assert.That(File.ReadAllBytes(otherHeader), Is.EqualTo(header));
            Assert.That(File.Exists(Path.Combine(root, "Mirror1.sav")), Is.False);
            Assert.That(File.Exists(cached), Is.False, "Generic safe names remain valid within the screenshot directory.");
        }
    }

    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator ScreenshotDeletion_LocalSlotDeletionRejectsPathBearingImageReferences()
            => LocalScreenshotDeletionScenario(false);

        [UnityTest]
        public IEnumerator ScreenshotDeletion_LocalSlotDeletionRemovesSafeImageAndItsRecoveryCopy()
            => LocalScreenshotDeletionScenario(true);

        IEnumerator LocalScreenshotDeletionScenario(bool safeImage)
        {
            // Async binary deletion also purges legacy PlayerPrefs on the dispatcher.
            // Run real frames rather than blocking an EditMode Task on its Update.
            foreach (string route in new[] { "sync", "async", "local" })
            {
                var fixture = new CrystalSaveScreenshotDeletionRegressionTests();
                fixture.SetUp();
                try
                {
                    Task deletion = safeImage
                        ? fixture.AssertLocalSlotDeletionRemovesSafeImageAndItsRecoveryCopy(route)
                        : fixture.AssertLocalSlotDeletionRejectsPathBearingImageReferences(route);
                    yield return Await(deletion);
                }
                finally { fixture.TearDown(); }
            }
        }
    }
}
#endif
