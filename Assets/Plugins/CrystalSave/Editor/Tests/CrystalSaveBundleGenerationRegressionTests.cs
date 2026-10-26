#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator BundleGeneration_LegacyMissingPayloadPreviewUsesMatchingHeader()
        {
            var manager = SaveManager.Instance;
            var data = new SaveData(manager.SaveSettings.version) { LastActiveScene = "A" };
            var header = new SaveSlot(1, "Legacy preview", data.LastSaved, "legacy.png", data.LastActiveScene);
            yield return Await(manager.SaveSystem.SaveAsync(manager.Serializer.Serialize(data), header));
            string images = Path.Combine(manager.SaveSystem.GetSaveGamesPath(), manager.SaveSettings.screenshotFolderName);
            Directory.CreateDirectory(images);
            File.WriteAllBytes(Path.Combine(images, "legacy.png"), new byte[] { 2, 3 });
            var export = manager.ExportSaveBundleBytesAsync(1); yield return Await(export);
            var decoded = DecodeReviewBundle(export.Result);
            Assert.That(decoded.GetType().GetField("Screenshot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(decoded), Is.EqualTo(new byte[] { 2, 3 }));
            var bytes = (byte[])decoded.GetType().GetField("SaveBytes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(decoded);
            Assert.That(manager.Serializer.Deserialize<SaveData>(bytes).ScreenshotFileName, Is.EqualTo(header.ScreenshotFileName));
        }

        [UnityTest]
        public IEnumerator BundleGeneration_FreshHeaderScreenshotClearDoesNotReintroduceCachedImage()
        {
            var manager = SaveManager.Instance;
            var data = new SaveData(manager.SaveSettings.version) { LastActiveScene = "A", ScreenshotFileName = "obsolete.png" };
            var header = new SaveSlot(1, "Cleared screenshot", data.LastSaved, "", data.LastActiveScene);
            yield return Await(manager.SaveSystem.SaveAsync(manager.Serializer.Serialize(data), header));
            string images = Path.Combine(manager.SaveSystem.GetSaveGamesPath(), manager.SaveSettings.screenshotFolderName);
            Directory.CreateDirectory(images);
            File.WriteAllBytes(Path.Combine(images, "obsolete.png"), new byte[] { 1, 2 });
            var export = manager.ExportSaveBundleBytesAsync(1); yield return Await(export);
            var decoded = DecodeReviewBundle(export.Result);
            Assert.That(decoded.GetType().GetField("Screenshot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(decoded), Is.Null);
            var bytes = (byte[])decoded.GetType().GetField("SaveBytes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(decoded);
            Assert.That(manager.Serializer.Deserialize<SaveData>(bytes).ScreenshotFileName, Is.Null.Or.Empty);
        }

        [UnityTest]
        public IEnumerator BundleGeneration_FreshLocalHeaderAndScreenshotReplaceCachedGeneration()
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveAsync(1));
            var published = manager.GetSlotMetadata(1);
            published.CustomMetadata["old"] = "cached";
            published.ScreenshotFileName = "old.png";
            var data = new SaveData(manager.SaveSettings.version)
            {
                LastSaved = published.LastSaved.AddHours(1), LastActiveScene = "A", ScreenshotFileName = "new.png"
            };
            var header = new SaveSlot(1, "Remote-style replacement", data.LastSaved, data.ScreenshotFileName, data.LastActiveScene);
            header.CustomMetadata["new"] = "fresh";
            yield return Await(manager.SaveSystem.SaveAsync(manager.Serializer.Serialize(data), header));
            string images = Path.Combine(manager.SaveSystem.GetSaveGamesPath(), manager.SaveSettings.screenshotFolderName);
            Directory.CreateDirectory(images);
            File.WriteAllBytes(Path.Combine(images, "old.png"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(images, "new.png"), new byte[] { 2, 3 });
            var request = manager.ExportSaveBundleBytesAsync(1);
            yield return Await(request);
            var decoded = DecodeReviewBundle(request.Result);
            var manifest = (SaveBundleManifest)decoded.GetType().GetField("Manifest", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(decoded);
            var screenshot = (byte[])decoded.GetType().GetField("Screenshot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(decoded);
            Assert.That(manifest.SlotName, Is.EqualTo(header.SlotName));
            Assert.That(manifest.CustomMetadata, Is.EqualTo(header.CustomMetadata));
            Assert.That(screenshot, Is.EqualTo(new byte[] { 2, 3 }));
            Assert.That(manager.GetSlotMetadata(1), Is.SameAs(published));
            Assert.That(published.CustomMetadata.ContainsKey("old"), Is.True);
        }

        [UnityTest] public IEnumerator BundleGeneration_RejectsConflictingTimestampWithoutWriting() => RejectConflictingBundle(true);
        [UnityTest] public IEnumerator BundleGeneration_RejectsConflictingSceneWithoutWriting() => RejectConflictingBundle(false);
        IEnumerator RejectConflictingBundle(bool timestamp)
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveAsync(1));
            var before = manager.SaveSystem.Load(manager.GetSlotMetadata(1));
            var priorHeader = manager.GetSlotMetadata(1);
            var payload = manager.Serializer.Deserialize<SaveData>(before);
            var manifest = new SaveBundleManifest
            {
                LastSaved = timestamp ? payload.LastSaved.AddHours(1) : payload.LastSaved,
                LastActiveScene = timestamp ? payload.LastActiveScene : "ConflictingScene",
                CustomMetadata = new Dictionary<string, string> { ["foreign"] = "value" }
            };
            var encoded = EncodeReviewBundle(manifest, before);
            var request = manager.ImportSaveBundleAsync(encoded, 1);
            yield return Await(request);
            Assert.That(request.Result, Is.False);
            Assert.That(manager.SaveSystem.Load(priorHeader), Is.EqualTo(before));
            Assert.That(manager.GetSlotMetadata(1), Is.SameAs(priorHeader));
            yield return Await(manager.SaveAsync(2));
        }

        [UnityTest]
        public IEnumerator BundleGeneration_MissingManifestFieldsUsePayloadAndKeepDisplayMetadata()
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveAsync(1));
            var plain = manager.ExportSaveAsync(1); yield return Await(plain);
            var manifest = new SaveBundleManifest { SlotName = "Edited display name", CustomMetadata = new Dictionary<string, string> { ["label"] = "value" } };
            var import = manager.ImportSaveBundleAsync(EncodeReviewBundle(manifest, plain.Result), 2);
            yield return Await(import);
            Assert.That(import.Result, Is.True);
            var header = manager.GetSlotMetadata(2);
            var payload = manager.Serializer.Deserialize<SaveData>(plain.Result);
            Assert.That(header.LastSaved, Is.EqualTo(payload.LastSaved));
            Assert.That(header.LastActiveScene, Is.EqualTo(payload.LastActiveScene));
            Assert.That(header.SlotName, Is.EqualTo(manifest.SlotName));
            Assert.That(header.CustomMetadata, Is.EqualTo(manifest.CustomMetadata));
        }

        static byte[] EncodeReviewBundle(SaveBundleManifest manifest, byte[] payload) => (byte[])typeof(SaveManager).Assembly
            .GetType("Arawn.CrystalSave.Runtime.SaveBundleCodec").GetMethod("Encode", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { manifest, payload, null, null, null });
        static object DecodeReviewBundle(byte[] bytes) => typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveBundleCodec")
            .GetMethod("Decode", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(byte[]), typeof(SaveBundleOptions) }, null)
            .Invoke(null, new object[] { bytes, null });

        [UnityTest]
        public IEnumerator BundleGeneration_BundleCachedHeaderMatchesPayload_Control()
            => ReviewBundleGeneration(false);

        [UnityTest]
        public IEnumerator BundleGeneration_BundleUsesHeaderFromFetchedGeneration()
            => ReviewBundleGeneration(true);

        IEnumerator ReviewBundleGeneration(bool newerStoredGeneration)
        {
            var manager = SaveManager.Instance;
            yield return Await(manager.SaveAsync(1));
            var original = manager.SaveSystem;
            var initialExport = manager.ExportSaveAsync(1);
            yield return Await(initialExport);
            var fetched = manager.Serializer.Deserialize<SaveData>(initialExport.Result);
            var published = manager.GetSlotMetadata(1);
            if (newerStoredGeneration)
            {
                fetched.LastSaved = fetched.LastSaved.AddHours(1);
                fetched.LastActiveScene = "A";
            }
            // Model the shipped REST LoadAsync contract: return the current payload,
            // leaving the SaveManager's previously published slot header untouched.
            var backend = new PostReviewStorage(original) { CloudRead = manager.Serializer.Serialize(fetched) };
            Set(manager, "saveSystem", backend);
            byte[] bundle;
            try
            {
                var request = manager.ExportSaveBundleBytesAsync(1);
                yield return Await(request);
                bundle = request.Result;
            }
            finally { Set(manager, "saveSystem", original); }
            var imported = manager.ImportSaveBundleAsync(bundle, 2);
            yield return Await(imported);
            Assert.That(imported.Result, Is.True);
            var header = manager.GetSlotMetadata(2);
            var importedBytes = manager.ExportSaveAsync(2);
            yield return Await(importedBytes);
            var data = manager.Serializer.Deserialize<SaveData>(importedBytes.Result);
            TestContext.WriteLine($"newer={newerStoredGeneration}, cached={published.LastSaved:O}/{published.LastActiveScene}, fetched={fetched.LastSaved:O}/{fetched.LastActiveScene}, importedHeader={header.LastSaved:O}/{header.LastActiveScene}, importedData={data.LastSaved:O}/{data.LastActiveScene}");
            Assert.That(data.LastSaved, Is.EqualTo(fetched.LastSaved));
            Assert.That(header.LastSaved, Is.EqualTo(data.LastSaved), "A bundle cannot combine the previous published header with a freshly fetched save generation.");
            Assert.That(header.LastActiveScene, Is.EqualTo(data.LastActiveScene));
        }
    }
}
#endif
