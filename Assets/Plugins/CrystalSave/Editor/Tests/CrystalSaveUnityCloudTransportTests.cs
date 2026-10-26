#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveUnityCloudTransportTests
    {
        [TestCase(CloudSaveTransport.Binary, true, CloudSaveTransport.JSON)]
        [TestCase(CloudSaveTransport.Binary, false, CloudSaveTransport.Binary)]
        [TestCase(CloudSaveTransport.JSON, true, CloudSaveTransport.JSON)]
        [TestCase(CloudSaveTransport.JSON, false, CloudSaveTransport.JSON)]
        public void PayloadWriteReadAndDeleteUseTheSameService(CloudSaveTransport configured, bool browser, CloudSaveTransport expected)
        {
            var fixture = new TransportFixture(configured, browser);
            var other = expected == CloudSaveTransport.JSON ? CloudSaveTransport.Binary : CloudSaveTransport.JSON;
            fixture.Stores[other]["slot-1"] = new byte[] { 99 };
            fixture.Stores[expected]["slot-2"] = new byte[] { 88 };

            Complete(fixture.Save("slot-1", new byte[] { 1, 2, 3 }));
            Assert.That(fixture.Stores[expected]["slot-1"], Is.EqualTo(new byte[] { 1, 2, 3 }));
            var read = fixture.Load("slot-1"); Complete(read);
            Assert.That(read.Result, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Complete(fixture.Delete("slot-1"));
            var missing = fixture.Load("slot-1"); Complete(missing);
            Assert.That(missing.Result, Is.Null, "Existence reads must not rediscover a deleted payload.");
            Assert.That(fixture.Stores[other]["slot-1"], Is.EqualTo(new byte[] { 99 }), "Do not delete from another API.");
            Assert.That(fixture.Stores[expected]["slot-2"], Is.EqualTo(new byte[] { 88 }), "Do not delete another key.");
            Assert.That(fixture.Routes, Is.All.EqualTo(expected));
            Assert.That(fixture.Routes.Count, Is.EqualTo(4));
        }

        [TestCase(CloudSaveTransport.Binary, true)]
        [TestCase(CloudSaveTransport.Binary, false)]
        [TestCase(CloudSaveTransport.JSON, true)]
        [TestCase(CloudSaveTransport.JSON, false)]
        public void MissingPayloadReadAndDeleteSucceedWithoutFallback(CloudSaveTransport configured, bool browser)
        {
            var fixture = new TransportFixture(configured, browser);
            var read = fixture.Load("missing"); Complete(read);
            Assert.That(read.Result, Is.Null);
            Complete(fixture.Delete("missing"));
            Assert.That(fixture.Routes.Count, Is.EqualTo(2), "Not-found must not probe or delete a second API.");
        }

        [TestCase(CloudSaveTransport.Binary, true)]
        [TestCase(CloudSaveTransport.Binary, false)]
        [TestCase(CloudSaveTransport.JSON, true)]
        [TestCase(CloudSaveTransport.JSON, false)]
        public void RequiredProviderFailuresRemainFailures(CloudSaveTransport configured, bool browser)
        {
            var fixture = new TransportFixture(configured, browser);
            Complete(fixture.Save("slot", new byte[] { 7 }));
            foreach (var failure in new Exception[] { new IOException("HTTP 500"), new UnauthorizedAccessException("HTTP 403") })
            {
                fixture.Failure = failure;
                Assert.That(Assert.Catch(() => Complete(fixture.Save("slot", new byte[] { 9 }))), Is.SameAs(failure));
                Assert.That(Assert.Catch(() => Complete(fixture.Load("slot"))), Is.SameAs(failure));
                Assert.That(Assert.Catch(() => Complete(fixture.Delete("slot"))), Is.SameAs(failure));
            }
            fixture.Failure = null;
            var read = fixture.Load("slot"); Complete(read);
            Assert.That(read.Result, Is.EqualTo(new byte[] { 7 }));
            Assert.That(fixture.Routes.Count, Is.EqualTo(8), "Failures must not trigger writes or deletes through the other API.");
        }

        [UnityTest]
        public IEnumerator BrowserBinaryDeleteWaitsForAcknowledgementAndPropagatesFailure()
        {
            var fixture = new TransportFixture(CloudSaveTransport.Binary, true);
            Complete(fixture.Save("slot", new byte[] { 7 }));
            fixture.DeleteGate = new TaskCompletionSource<bool>();
            var failedDelete = fixture.Delete("slot");
            try
            {
                Assert.That(failedDelete.IsCompleted, Is.False);
                fixture.DeleteGate.SetException(new IOException("Rejected delete"));
                yield return UntilCompleted(failedDelete);
                Assert.Throws<IOException>(() => failedDelete.GetAwaiter().GetResult());
                Assert.That(fixture.Stores[CloudSaveTransport.JSON].ContainsKey("slot"), Is.True);

                fixture.DeleteGate = new TaskCompletionSource<bool>();
                var acceptedDelete = fixture.Delete("slot");
                Assert.That(acceptedDelete.IsCompleted, Is.False);
                fixture.DeleteGate.SetResult(true);
                yield return UntilCompleted(acceptedDelete);
                acceptedDelete.GetAwaiter().GetResult();
                Assert.That(fixture.Stores[CloudSaveTransport.JSON].ContainsKey("slot"), Is.False);
                Assert.That(fixture.Routes, Is.All.EqualTo(CloudSaveTransport.JSON));
            }
            finally { fixture.DeleteGate.TrySetResult(true); }
        }

        static void Complete(Task task)
        {
            Assert.That(task.IsCompleted, Is.True, "This test uses immediate provider results and must not block Unity.");
            task.GetAwaiter().GetResult();
        }

        static IEnumerator UntilCompleted(Task task)
        {
            var timer = Stopwatch.StartNew();
            while (!task.IsCompleted && timer.Elapsed.TotalSeconds < 5) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Provider acknowledgement timed out.");
        }

        sealed class TransportFixture
        {
            internal readonly Dictionary<CloudSaveTransport, Dictionary<string, byte[]>> Stores = new()
            {
                [CloudSaveTransport.Binary] = new Dictionary<string, byte[]>(),
                [CloudSaveTransport.JSON] = new Dictionary<string, byte[]>()
            };
            internal readonly List<CloudSaveTransport> Routes = new();
            internal Exception Failure;
            internal TaskCompletionSource<bool> DeleteGate;
            internal readonly Func<string, byte[], Task> Save;
            internal readonly Func<string, Task<byte[]>> Load;
            internal readonly Func<string, Task> Delete;

            internal TransportFixture(CloudSaveTransport configured, bool browser)
            {
                var type = typeof(SaveSystem).Assembly.GetType("Arawn.CrystalSave.Runtime.UnityCloudPayloadTransport", true);
                object transport = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { configured, browser,
                        (Func<CloudSaveTransport, string, byte[], Task>)Write,
                        (Func<CloudSaveTransport, string, Task<byte[]>>)Read,
                        (Func<CloudSaveTransport, string, Task>)Remove,
                        (Func<Exception, bool>)(exception => exception is KeyNotFoundException) }, null);
                Save = (Func<string, byte[], Task>)Delegate.CreateDelegate(typeof(Func<string, byte[], Task>), transport,
                    type.GetMethod("SaveAsync", BindingFlags.Instance | BindingFlags.NonPublic));
                Load = (Func<string, Task<byte[]>>)Delegate.CreateDelegate(typeof(Func<string, Task<byte[]>>), transport,
                    type.GetMethod("LoadAsync", BindingFlags.Instance | BindingFlags.NonPublic));
                Delete = (Func<string, Task>)Delegate.CreateDelegate(typeof(Func<string, Task>), transport,
                    type.GetMethod("DeleteAsync", BindingFlags.Instance | BindingFlags.NonPublic));
            }

            Task Write(CloudSaveTransport route, string key, byte[] payload)
            {
                Routes.Add(route);
                if (Failure != null) return Task.FromException(Failure);
                Stores[route][key] = (byte[])payload.Clone();
                return Task.CompletedTask;
            }

            Task<byte[]> Read(CloudSaveTransport route, string key)
            {
                Routes.Add(route);
                if (Failure != null) return Task.FromException<byte[]>(Failure);
                return Stores[route].TryGetValue(key, out var payload) ? Task.FromResult((byte[])payload.Clone())
                    : Task.FromException<byte[]>(new KeyNotFoundException(key));
            }

            async Task Remove(CloudSaveTransport route, string key)
            {
                Routes.Add(route);
                if (Failure != null) throw Failure;
                if (DeleteGate != null) await DeleteGate.Task;
                if (!Stores[route].Remove(key)) throw new KeyNotFoundException(key);
            }
        }
    }
}
#endif
