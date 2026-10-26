#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator AsyncRollback_HoldsQueueUntilCleanupCompletes()
            => AsyncRollbackOwnershipScenario(false);
        [UnityTest] public IEnumerator AsyncRollback_ReportsCleanupFailureAndReleasesQueue()
            => AsyncRollbackOwnershipScenario(true);

        IEnumerator AsyncRollbackOwnershipScenario(bool failCleanup)
        {
            var manager = SaveManager.Instance;
            int unityThread = Thread.CurrentThread.ManagedThreadId;
            int captures = 0, cleanupStartThread = 0, cleanupEndThread = 0, failures = 0;
            bool cleanupStarted = false;
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var probe = new AsyncRollbackSaveable();
            probe.Capture = () =>
            {
                captures++;
                if (captures != 1) return;
                Type ioScope = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveIoScope");
                object current = ioScope.GetField("Current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                object context = current.GetType().GetProperty("Value").GetValue(current);
                Get<List<Func<Task>>>(context, "AsyncRollback").Add(async () =>
                {
                    cleanupStartThread = Thread.CurrentThread.ManagedThreadId;
                    cleanupStarted = true;
                    await gate.Task;
                    cleanupEndThread = Thread.CurrentThread.ManagedThreadId;
                    if (failCleanup) throw new IOException("ASYNC_ROLLBACK_CLEANUP_FAILURE");
                });
                throw new InvalidOperationException("ASYNC_ROLLBACK_CAPTURE_FAILURE");
            };
            EventHandler<OperationFailedEventArgs> onFailure = (_, __) => failures++;
            manager.OnSaveFailed += onFailure;
            manager.ComponentManager.RegisterSaveableComponent(probe);
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var rejected = manager.SaveImmediateAsync(1);
                var following = manager.SaveImmediateAsync(2);
                yield return Until(() => cleanupStarted, "asynchronous rollback started");
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That(rejected.IsCompleted, Is.False, "Failure completion waits for cleanup acknowledgement.");
                Assert.That(following.IsCompleted, Is.False);
                Assert.That(captures, Is.EqualTo(1), "A later capture cannot enter while rollback still owns the queue.");
                Assert.That(failures, Is.Zero, "Completion notifications follow cleanup and ownership release.");
                gate.TrySetResult(true);
                yield return Until(() => rejected.IsCompleted && following.IsCompleted, "rollback and queued operation completion");
                Assert.That(rejected.IsFaulted, Is.True);
                string errors = string.Join("; ", rejected.Exception.Flatten().InnerExceptions.Select(error => error.Message));
                Assert.That(errors, Does.Contain("ASYNC_ROLLBACK_CAPTURE_FAILURE"));
                Assert.That(errors.Contains("ASYNC_ROLLBACK_CLEANUP_FAILURE"), Is.EqualTo(failCleanup));
                Assert.That(following.Status, Is.EqualTo(TaskStatus.RanToCompletion), following.Exception?.ToString());
                Assert.That(captures, Is.EqualTo(2));
                Assert.That(failures, Is.EqualTo(1));
                Assert.That(cleanupStartThread, Is.EqualTo(unityThread));
                Assert.That(cleanupEndThread, Is.EqualTo(unityThread), "Cleanup must retain Unity context after awaiting.");
                yield return Await(manager.SaveImmediateAsync(3));
            }
            finally
            {
                gate.TrySetResult(true);
                manager.OnSaveFailed -= onFailure;
                manager.ComponentManager.UnregisterSaveableComponent(probe);
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }

    internal sealed class AsyncRollbackSaveable : ISaveable
    {
        internal Action Capture;
        public string UniqueIdentifier => "async-rollback-object_async-rollback-component";
        public byte[] SaveData() { Capture?.Invoke(); return new byte[] { 7 }; }
        public void LoadData(byte[] bytes) { }
    }
}
#endif
