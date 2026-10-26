#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator StorageBoundary_InvalidPathsPreventCommitSideEffects() => StoragePathCommitEffects(true);
        [UnityTest] public IEnumerator StorageBoundary_ValidPathsPublishCommitSideEffectsControl() => StoragePathCommitEffects(false);

        IEnumerator StoragePathCommitEffects(bool invalid)
        {
            var manager = SaveManager.Instance;
            var settings = manager.SaveSettings;
            string previousPayload = settings.saveFileName, previousMetadata = settings.metadataFileNamePattern;
            bool verification = settings.enableSaveFileVerification;
            bool ignored = LogAssert.ignoreFailingMessages;
            int published = 0;
            var saveable = new StoragePreflightSaveable(() =>
            {
                var coordinator = typeof(SaveManager).GetProperty("Operations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                var context = coordinator.GetType().GetProperty("Active", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator);
                var effects = (List<Func<Task>>)context.GetType().GetField("CommitSideEffects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);
                effects.Add(() => { published++; return Task.CompletedTask; });
            });
            try
            {
                settings.saveFileName = "Preflight{n}";
                settings.metadataFileNamePattern = invalid ? "Preflight{n}.sav.bak" : "Preflight{n}_Meta.bin";
                settings.enableSaveFileVerification = false;
                manager.ComponentManager.RegisterSaveableComponent(saveable);
                LogAssert.ignoreFailingMessages = invalid || ignored;
                var request = manager.SaveAsync(1);
                yield return Until(() => request.IsCompleted, "storage path preflight completion");
                Assert.That(request.IsFaulted, Is.EqualTo(invalid), request.Exception?.ToString());
                if (invalid) Assert.That(published, Is.Zero, "An invalid local filename must not publish a queued screenshot upload.");
                else Assert.That(published, Is.GreaterThan(0), "Healthy control must exercise the same commit side effects.");
            }
            finally
            {
                manager.ComponentManager.UnregisterSaveableComponent(saveable);
                settings.saveFileName = previousPayload;
                settings.metadataFileNamePattern = previousMetadata;
                settings.enableSaveFileVerification = verification;
                LogAssert.ignoreFailingMessages = ignored;
            }
        }

        [UnityTest] public IEnumerator StorageBoundary_InvalidPathsPreventImportImageStaging() => StoragePathImportStaging(true);
        [UnityTest] public IEnumerator StorageBoundary_ValidPathsStageImportImageControl() => StoragePathImportStaging(false);

        IEnumerator StoragePathImportStaging(bool invalid)
        {
            var manager = SaveManager.Instance;
            var settings = manager.SaveSettings;
            string previousPayload = settings.saveFileName, previousMetadata = settings.metadataFileNamePattern;
            bool verification = settings.enableSaveFileVerification;
            string image = Path.Combine(manager.SaveSystem.GetSaveGamesPath(), "preflight-import.png");
            int staged = 0;
            try
            {
                settings.saveFileName = "Preflight{n}";
                settings.metadataFileNamePattern = invalid ? "Preflight{n}.sav.bak" : "Preflight{n}_Meta.bin";
                settings.enableSaveFileVerification = false;
                var service = Get<object>(manager, "saveOperationService");
                // QueueImport supplies this exact beforeWrite hook to the shared commit
                // core. Observe it directly so rollback cannot hide an earlier image write.
                Action beforeWrite = () => { staged++; File.WriteAllBytes(image, new byte[] { 4, 5 }); };
                var slot = new SaveSlot(1, "Preflight import", DateTime.UtcNow, "preflight-import.png", "A");
                using var context = CreateOperationContext();
                var request = (Task)service.GetType().GetMethod("CommitPayloadAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(service, new object[] { context, new byte[] { 1, 2 }, slot, beforeWrite });
                yield return Until(() => request.IsCompleted, "import staging preflight completion");
                Assert.That(request.IsFaulted, Is.EqualTo(invalid), request.Exception?.ToString());
                Assert.That(staged, Is.EqualTo(invalid ? 0 : 1));
                Assert.That(File.Exists(image), Is.EqualTo(!invalid));
            }
            finally
            {
                settings.saveFileName = previousPayload;
                settings.metadataFileNamePattern = previousMetadata;
                settings.enableSaveFileVerification = verification;
                if (File.Exists(image)) File.Delete(image);
            }
        }

        sealed class StoragePreflightSaveable : ISaveable
        {
            readonly Action capture;
            internal StoragePreflightSaveable(Action capture) { this.capture = capture; }
            public string UniqueIdentifier => "storage-preflight_component";
            public byte[] SaveData() { capture(); return new byte[] { 1, 2 }; }
            public void LoadData(byte[] bytes) { }
        }
    }
}
#endif
