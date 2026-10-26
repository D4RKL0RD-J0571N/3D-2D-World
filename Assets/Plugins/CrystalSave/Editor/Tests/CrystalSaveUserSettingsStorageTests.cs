#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveUserSettingsStorageTests
    {
        static readonly Type Storage = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.UserSettingsStorage", true);
        static readonly FieldInfo Flush = Storage.GetField("FlushOverride", BindingFlags.Static | BindingFlags.NonPublic);
        FeatureSettingsScope scope;
        string root, path;
        object previousFlush;

        [SetUp]
        public void SetUp()
        {
            scope = new FeatureSettingsScope();
            root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            path = Path.Combine(root, "nested", "UserSettings.json");
            scope.Settings.persistentPath.mode = PersistentPathMode.Custom;
            scope.Settings.persistentPath.nonWebGLOutputRoot = root;
            previousFlush = Flush.GetValue(null);
            Flush.SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            Flush.SetValue(null, previousFlush);
            scope.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        static string Json(float volume) => JsonUtility.ToJson(new UserSettingsData { MasterVolume = volume, LocaleCode = "日本語\n\"user\"" });
        static Task Write(string path, string json) => (Task)Call(Storage, "WriteAsync", path, json);
        static Task<UserSettingsData> Read(string path) => (Task<UserSettingsData>)Call(Storage, "ReadAsync", path);

        [Test]
        public async Task CompletedQueueLeaseCannotReleaseTheFollowingOperationTwice()
        {
            var type = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.AsyncOperationQueue", true);
            var queue = Activator.CreateInstance(type, true);
            Task<IDisposable> Enter() => (Task<IDisposable>)Call(queue, "EnterAsync");
            IDisposable first = await Enter();
            var second = Enter();
            var third = Enter();
            first.Dispose();
            IDisposable secondOwner = await second;
            try
            {
                first.Dispose();
                Assert.That(third.IsCompleted, Is.False, "Only the active operation's lease can advance the queue.");
            }
            finally { secondOwner.Dispose(); }
            (await third).Dispose();
        }

        [Test]
        public async Task MissingDirectoryIsCreatedAndLegacyJsonRoundTrips()
        {
            await Write(path, Json(0.25f));
            var loaded = await Read(path);
            Assert.That(loaded.MasterVolume, Is.EqualTo(0.25f));
            Assert.That(loaded.LocaleCode, Is.EqualTo("日本語\n\"user\""));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.writing-*"), Is.Empty);
        }

        [Test]
        public async Task InvalidIncomingJsonDoesNotReplaceExistingSettings()
        {
            await Write(path, Json(0.25f));
            byte[] before = File.ReadAllBytes(path);
            foreach (string invalid in new[] { "", "null", "[]", "true", "42", "\"text\"", "{\"MasterVolume\":", "{\"MasterVolume\":1} trailing" })
            {
                var write = Write(path, invalid);
                try { await write; } catch { }
                Assert.That(write.IsFaulted, Is.True, invalid);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            }
        }

        [Test]
        public async Task CorruptPrimaryUsesPreviousGenerationWithoutRewritingEitherFile()
        {
            await Write(path, Json(0.25f));
            await Write(path, Json(0.75f));
            string previous = File.ReadAllText(path + ".previous");
            File.WriteAllText(path, "{broken");
            Assert.That((await Read(path)).MasterVolume, Is.EqualTo(0.25f));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{broken"));
            Assert.That(File.ReadAllText(path + ".previous"), Is.EqualTo(previous));
        }

        [Test]
        public async Task FailedAtomicReplacementKeepsPriorSettingsAndRemovesStagingFile()
        {
            await Write(path, Json(0.25f));
            byte[] before = File.ReadAllBytes(path);
            Directory.CreateDirectory(path + ".previous");
            var write = Write(path, Json(0.75f));
            try { await write; } catch { }
            Assert.That(write.IsFaulted, Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.writing-*"), Is.Empty);
        }

        [Test]
        public async Task FailedBrowserWriteAwaitsRollbackBeforeTheNextWrite()
        {
            await Write(path, Json(0.25f));
            byte[] before = File.ReadAllBytes(path);
            var flushStarted = new TaskCompletionSource<bool>();
            var rollbackStarted = new TaskCompletionSource<bool>();
            var rollback = new TaskCompletionSource<bool>();
            int flushes = 0;
            Flush.SetValue(null, (Func<Task>)(() =>
            {
                flushes++;
                if (flushes == 1) return flushStarted.Task;
                if (flushes == 2) { rollbackStarted.TrySetResult(true); return rollback.Task; }
                return Task.CompletedTask;
            }));
            var failed = Write(path, Json(0.5f));
            var next = Write(path, Json(0.75f));
            try
            {
                Assert.That(failed.IsCompleted || next.IsCompleted, Is.False);
                flushStarted.SetException(new IOException("rejected browser flush"));
                await Task.WhenAny(rollbackStarted.Task, Task.Delay(5000));
                Assert.That(rollbackStarted.Task.IsCompleted, Is.True, "Rollback must begin after the rejected flush.");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), "Rollback restores exact local bytes before its persistence acknowledgement.");
                Assert.That(failed.IsCompleted || next.IsCompleted, Is.False);
                rollback.SetResult(true);
                try { await failed; } catch { }
                await next;
                Assert.That(failed.IsFaulted, Is.True);
                Assert.That((await Read(path)).MasterVolume, Is.EqualTo(0.75f));
                Assert.That(flushes, Is.EqualTo(3));
            }
            finally
            {
                flushStarted.TrySetResult(true);
                rollback.TrySetResult(true);
                try { await failed; } catch { }
                try { await next; } catch { }
            }
        }

        [Test]
        public async Task FailedFirstBrowserWriteLeavesNoSettingsGeneration()
        {
            int flushes = 0;
            Flush.SetValue(null, (Func<Task>)(() => ++flushes == 1
                ? Task.FromException(new IOException("first browser write rejected")) : Task.CompletedTask));
            var write = Write(path, Json(0.75f));
            try { await write; } catch { }
            Assert.That(write.IsFaulted, Is.True);
            Assert.That(File.Exists(path) || File.Exists(path + ".previous"), Is.False);
            Assert.That(flushes, Is.EqualTo(2));
        }

        [Test]
        public void EarlySettingsPathUsesConfiguredRootWithoutASaveManager()
        {
            string resolved = (string)Call(Storage, "ResolvePath", new object[] { null });
            Assert.That(Path.GetFullPath(resolved), Does.StartWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar));
            Assert.That(Path.GetFileName(resolved), Is.EqualTo("UserSettings.json"));
        }

        [Test]
        public async Task LegacyVoidSaveAndAwaitableSaveShareTheAcknowledgedStorageQueue()
        {
            var go = new GameObject("Inactive user settings save test");
            go.SetActive(false);
            var settings = go.AddComponent<UserSettingsManager>();
            var gate = new TaskCompletionSource<bool>();
            int flushes = 0;
            Flush.SetValue(null, (Func<Task>)(() => ++flushes == 1 ? gate.Task : Task.CompletedTask));
            Task second = null;
            try
            {
                Set(settings, "currentSettings", new UserSettingsData { LocaleCode = "queue-fixture" });
                settings.SaveSettings();
                second = settings.SaveSettingsAsync();
                Assert.That(flushes, Is.EqualTo(1));
                Assert.That(second.IsCompleted, Is.False);
                gate.SetResult(true);
                await second;
                Assert.That(flushes, Is.EqualTo(2));
                string resolved = (string)Call(Storage, "ResolvePath", new object[] { null });
                Assert.That((await Read(resolved)).LocaleCode, Is.EqualTo("queue-fixture"));
            }
            finally
            {
                gate.TrySetResult(true);
                if (second != null) { try { await second; } catch { } }
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CameraSettingsApplicationToleratesMissingCameraAndUnloadedSettings()
        {
            var go = new GameObject("Inactive user settings camera test");
            go.SetActive(false);
            var settings = go.AddComponent<UserSettingsManager>();
            Set(settings, "autoReferenceMainCamera", false);
            try
            {
                // Also exercises the optional HDRP branch when that integration is present.
                Assert.DoesNotThrow(settings.ApplyHDRPDynamicResolutionSettings);
                Set(settings, "currentSettings", new UserSettingsData());
                Assert.DoesNotThrow(settings.ApplyHDRPDynamicResolutionSettings);
                Set(settings, "mainCamera", go.AddComponent<Camera>());
                Set(settings, "currentSettings", null);
                Assert.DoesNotThrow(settings.ApplyHDRPDynamicResolutionSettings);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public async Task CorruptExplicitLoadPreservesExistingDataWithoutApplyingGraphics()
        {
            string resolved = (string)Call(Storage, "ResolvePath", new object[] { null });
            Directory.CreateDirectory(Path.GetDirectoryName(resolved));
            File.WriteAllText(resolved, "{broken");
            var go = new GameObject("Inactive user settings storage test");
            go.SetActive(false);
            var settings = go.AddComponent<UserSettingsManager>();
            var previous = new UserSettingsData { MasterVolume = 0.4f };
            Set(settings, "currentSettings", previous);
            try
            {
                var load = settings.LoadSettingsAsync();
                try { await load; } catch { }
                Assert.That(load.IsFaulted, Is.True);
                Assert.That(settings.CurrentSettings, Is.SameAs(previous));
                Assert.That(settings.MasterVolume, Is.EqualTo(0.4f));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
#endif
