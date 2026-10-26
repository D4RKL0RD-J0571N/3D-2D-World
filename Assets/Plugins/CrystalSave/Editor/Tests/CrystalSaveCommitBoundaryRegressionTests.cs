#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Collections;
using System.Linq;
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
        [UnityTest] public IEnumerator SaveCommit_QuickFailurePreservesHistory() => IndependentRotation(false,true);
        [UnityTest] public IEnumerator SaveCommit_AutoFailurePreservesHistory() => IndependentRotation(true,true);
        [UnityTest] public IEnumerator SaveCommit_QuickHealthyControl() => IndependentRotation(false,false);
        [UnityTest] public IEnumerator SaveCommit_AutoHealthyControl() => IndependentRotation(true,false);
        IEnumerator IndependentRotation(bool automatic,bool fail)
        {
            yield return LoadScene("A");
            var manager=SaveManager.Instance;
            var go=new GameObject("RotationCapture"); Composite(go,typeof(FeatureProbe));
            var probe=go.GetComponent<FeatureProbe>(); probe.Value=17;
            yield return null;
            Func<Task> save=()=>automatic ? manager.AutoSaveAsync() : manager.QuickSaveAsync();
            yield return Await(save());
            probe.Value=29;
            yield return Await(save());
            int first=(automatic ? manager.SaveSettings.autoSaveSlotOffset : manager.SaveSettings.quickSaveSlotOffset)+1;
            var history=manager.GetSlotMetadata(first+1);
            var oldHistory=manager.SaveSystem.Load(history);
            var oldPrimary=manager.SaveSystem.Load(manager.GetSlotMetadata(first));
            Assert.That(oldHistory.SequenceEqual(oldPrimary),Is.False,"distinct history fixture");
            Set(probe,"enableEventBridge",true);
            var saving=Get<UnityEngine.Events.UnityEvent>(probe,"onSaving");
            if(fail) saving.AddListener(()=>throw new InvalidOperationException("Required rotation capture rejected"));
            probe.Value=41;
            LogAssert.ignoreFailingMessages=true;
            try
            {
                var request=save();
                yield return Until(()=>request.IsCompleted,"rotation completion",15);
                var currentHistory=manager.SaveSystem.Load(manager.GetSlotMetadata(first+1));
                TestContext.WriteLine($"automatic={automatic}, failure={fail}, task={request.Status}, oldHistoryPreserved={currentHistory.SequenceEqual(oldHistory)}, copiedPrimary={currentHistory.SequenceEqual(oldPrimary)}");
                Assert.That(request.IsFaulted,Is.EqualTo(fail));
                Assert.That(currentHistory,Is.EqualTo(fail ? oldHistory : oldPrimary),"Rejected required capture must not overwrite a prior history generation.");
            }
            finally { saving.RemoveAllListeners(); UnityEngine.Object.Destroy(go); LogAssert.ignoreFailingMessages=false; }
        }
        [UnityTest] public IEnumerator SaveCommit_RejectedCaptureDoesNotUploadScreenshot() => FinalReviewScreenshotCapture(1);
        [UnityTest] public IEnumerator SaveCommit_HealthyCaptureScreenshotControl() => FinalReviewScreenshotCapture(0);
        [UnityTest] public IEnumerator SaveCommit_CancelledScreenshotDoesNotUpload() => FinalReviewScreenshotCapture(2);
        [UnityTest] public IEnumerator SaveCommit_FailedEncryptionDoesNotUpload() => FinalReviewScreenshotCapture(3);
        IEnumerator FinalReviewScreenshotCapture(int failureMode)
        {
            var manager = SaveManager.Instance; var original = manager.SaveSystem;
            var settings = manager.SaveSettings;
            string originalApi = settings.mySqlApiUrl;
            settings.enableCloudSave = true; settings.backend = SaveBackend.MySQL; settings.keepLocalMirror = false;
            settings.enableScreenshots = true;
            var screenshot = Get<ScreenshotManager>(manager, "screenshotManager");
            screenshot.CustomCapture = () => new Texture2D(4, 4);
            bool fail = failureMode != 0;
            using var cancellation = new CancellationTokenSource();
            if (failureMode == 2) screenshot.CustomCapture = () => { cancellation.Cancel(); return new Texture2D(4, 4); };
            settings.enableSaveFileVerification = failureMode == 3;
            var failure = new PostReviewSaveable { ThrowSave = failureMode == 1 };
            manager.ComponentManager.RegisterSaveableComponent(failure);
            using var listener = new HttpListener();
            var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
            int port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
            settings.mySqlApiUrl = $"http://127.0.0.1:{port}";
            listener.Prefixes.Add(settings.mySqlApiUrl + "/"); listener.Start();
            int uploads = 0, saves = 0;
            var serving = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext request;
                    try { request = await listener.GetContextAsync(); }
                    catch (Exception) when (!listener.IsListening) { break; }
                    using var reader = new StreamReader(request.Request.InputStream);
                    await reader.ReadToEndAsync();
                    string path = request.Request.Url.AbsolutePath;
                    if (path == "/uploadImage") System.Threading.Interlocked.Increment(ref uploads);
                    if (path == "/save") System.Threading.Interlocked.Increment(ref saves);
                    request.Response.StatusCode = path == "/load" ? 404 : 200;
                    request.Response.ContentLength64 = 0; request.Response.Close();
                }
            });
            Set(manager, "saveSystem", new MySqlSaveSystem(settings, original.GetSaveGamesPath()));
            LogAssert.ignoreFailingMessages = true;
            try
            {
                if (failureMode == 3)
                {
                    // Missing required crypto rejects the prepared image before publication.
                    settings.enableEncryption = true;
                    settings.cloudCryptoMode = CloudCryptoMode.ServerSide;
                    settings.cloudCryptoProvider = null;
                }
                var save = manager.SaveAsync(1, null, cancellation.Token);
                yield return Until(() => save.IsCompleted, "screenshot capture outcome");
                TestContext.WriteLine($"captureFail={fail}, faulted={save.IsFaulted}, uploads={uploads}, payloadSaves={saves}, error={save.Exception}");
                Assert.That(save.IsFaulted || save.IsCanceled, Is.EqualTo(fail));
                Assert.That(saves, Is.EqualTo(fail ? 0 : 1));
                Assert.That(uploads, Is.EqualTo(fail ? 0 : 1), "Rejected capture must not upload or change remote screenshot metadata");
            }
            finally
            {
                listener.Stop();
                manager.ComponentManager.UnregisterSaveableComponent(failure);
                screenshot.CustomCapture = null; settings.enableCloudSave = false; settings.enableScreenshots = false;
                settings.mySqlApiUrl = originalApi;
                Set(manager, "saveSystem", original); LogAssert.ignoreFailingMessages = false;
            }
        }
    
        [UnityTest] public IEnumerator SaveCommit_QuickWriteFailureRollsBackWholeHistory() => RotationWriteFailure(false);
        [UnityTest] public IEnumerator SaveCommit_AutoWriteFailureRollsBackWholeHistory() => RotationWriteFailure(true);
        IEnumerator RotationWriteFailure(bool automatic)
        {
            yield return LoadScene("A");
            var manager = SaveManager.Instance;
            var go = new GameObject("RotationFailure"); Composite(go, typeof(FeatureProbe));
            var probe = go.GetComponent<FeatureProbe>();
            Func<Task> save = () => automatic ? manager.AutoSaveAsync() : manager.QuickSaveAsync();
            probe.Value = 11; yield return Await(save());
            probe.Value = 22; yield return Await(save());
            int first = (automatic ? manager.SaveSettings.autoSaveSlotOffset : manager.SaveSettings.quickSaveSlotOffset) + 1;
            var headers = new[] { manager.GetSlotMetadata(first), manager.GetSlotMetadata(first + 1) };
            var bytes = headers.Select(slot => manager.SaveSystem.Load(slot)).ToArray();
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction")
                .GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
            int writes = 0;
            hook.SetValue(null, (Action<string>)(stage =>
            {
                if (stage == "payload" && ++writes == 2) throw new IOException("Rejected primary write after history write");
            }));
            LogAssert.ignoreFailingMessages = true;
            try
            {
                probe.Value = 33;
                var failed = save(); yield return Until(() => failed.IsCompleted, "rotation failure");
                Assert.That(failed.IsFaulted, Is.True);
                Assert.That(writes, Is.EqualTo(2));
                hook.SetValue(null, null);
                var restarted = new SaveSystem(manager.SaveSettings, manager.SaveSystem.GetSaveGamesPath());
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(restarted.Load(headers[i]), Is.EqualTo(bytes[i]));
                    Assert.That(manager.GetSlotMetadata(first + i), Is.SameAs(headers[i]));
                }
                yield return Await(save());
                Assert.That(restarted.Load(manager.GetSlotMetadata(first + 1)), Is.EqualTo(bytes[0]));
            }
            finally { hook.SetValue(null, null); UnityEngine.Object.Destroy(go); LogAssert.ignoreFailingMessages = false; }
        }

        [UnityTest]
        public IEnumerator SaveCommit_DurableRotationSurvivesInterruptedCleanup()
        {
            yield return LoadScene("A");
            var manager = SaveManager.Instance;
            var go = new GameObject("RotationCleanup"); Composite(go, typeof(FeatureProbe));
            var probe = go.GetComponent<FeatureProbe>();
            probe.Value = 1; yield return Await(manager.QuickSaveAsync());
            probe.Value = 2; yield return Await(manager.QuickSaveAsync());
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveVerificationTransaction")
                .GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
            bool injected = false;
            hook.SetValue(null, (Action<string>)(stage =>
            {
                if (stage == "batch-verified") { injected = true; throw new IOException("Interrupted batch cleanup"); }
            }));
            try
            {
                probe.Value = 3; yield return Await(manager.QuickSaveAsync());
                Assert.That(injected, Is.True);
                hook.SetValue(null, null);
                string root = manager.SaveSystem.GetSaveGamesPath();
                Assert.That(Directory.GetFiles(root, "*.csav-verified-batch"), Has.Length.EqualTo(1));
                var restart = new SaveSystem(manager.SaveSettings, root);
                var newest = restart.Load(manager.GetSlotMetadata(11));
                var older = restart.Load(manager.GetSlotMetadata(12));
                Assert.That(newest, Is.Not.EqualTo(older));
                Assert.That(Directory.GetFiles(root, "*.csav-verified-batch"), Is.Empty);
                Assert.That(Directory.GetFiles(root, "*.csav-verify"), Is.Empty);
            }
            finally { hook.SetValue(null, null); UnityEngine.Object.Destroy(go); }
        }

        [UnityTest]
        public IEnumerator SaveCommit_RotatingScreenshotsStayBoundedAndFailedSaveCleansStaging()
        {
            yield return LoadScene("A");
            var manager = SaveManager.Instance;
            manager.SaveSettings.enableScreenshots = true;
            var screenshots = Get<ScreenshotManager>(manager, "screenshotManager");
            screenshots.CustomCapture = () => new Texture2D(4, 4);
            string images = Path.Combine(manager.SaveSystem.GetSaveGamesPath(), manager.SaveSettings.screenshotFolderName);
            var failure = new PostReviewSaveable();
            manager.ComponentManager.RegisterSaveableComponent(failure);
            try
            {
                for (int i = 0; i < 5; i++) yield return Await(manager.QuickSaveAsync());
                string[] referenced = manager.GetSaveSlots().Select(slot => slot.ScreenshotFileName)
                    .Where(name => !string.IsNullOrEmpty(name)).Distinct().OrderBy(name => name).ToArray();
                Assert.That(referenced, Has.Length.EqualTo(2));
                Assert.That(Directory.GetFiles(images).Select(Path.GetFileName).OrderBy(name => name), Is.EqualTo(referenced));
                var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction")
                    .GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
                hook.SetValue(null, (Action<string>)(stage => { if (stage == "payload") throw new IOException("Failed image generation commit"); }));
                LogAssert.ignoreFailingMessages = true;
                try
                {
                    var save = manager.QuickSaveAsync(); yield return Until(() => save.IsCompleted, "screenshot rollback");
                    Assert.That(save.IsFaulted, Is.True);
                    Assert.That(Directory.GetFiles(images).Select(Path.GetFileName).OrderBy(name => name), Is.EqualTo(referenced));
                }
                finally { hook.SetValue(null, null); LogAssert.ignoreFailingMessages = false; }
            }
            finally { screenshots.CustomCapture = null; manager.ComponentManager.UnregisterSaveableComponent(failure); }
        }

    }
}
#endif
