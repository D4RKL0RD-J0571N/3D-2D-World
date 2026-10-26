#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    internal sealed class CloudRequestIdentity
    {
        internal readonly string UserFolder;
        internal readonly string AccessKey;
        internal CloudRequestIdentity(string userFolder, string accessKey)
        { UserFolder = userFolder; AccessKey = accessKey; }
    }

    internal interface ICloudRequestIdentitySource
    {
        Task<CloudRequestIdentity> CaptureRequestIdentityAsync();
    }

    // One account per coordinated operation, or per direct composite provider call.
    // A login or resolver update affects subsequent operations without redirecting an
    // in-flight payload's metadata, image, deletion or conflict read to another account.
    internal sealed class CloudRequestScope : IDisposable
    {
        static readonly AsyncLocal<CloudRequestScope> current = new();
        readonly CloudRequestScope previous;
        readonly object provider;
        readonly CloudRequestIdentity identity;

        internal CloudRequestScope(object provider, CloudRequestIdentity identity)
        {
            this.provider = provider;
            this.identity = identity;
            previous = current.Value;
            current.Value = this;
        }

        internal static CloudRequestIdentity Find(object provider)
        {
            var context = SaveIoScope.Current.Value;
            if (context != null)
                lock (context.CloudIdentities)
                    if (context.CloudIdentities.TryGetValue(provider, out var identity)) return identity;
            for (var scope = current.Value; scope != null; scope = scope.previous)
                if (ReferenceEquals(scope.provider, provider)) return scope.identity;
            return null;
        }

        internal static Task<CloudRequestIdentity> CaptureAsync(object provider, Func<CloudRequestIdentity> capture,
            bool requiresUnityThread = true)
        {
            SaveIoScope.CheckCancellation();
            var existing = Find(provider);
            if (existing != null) return Task.FromResult(existing);
            var context = SaveIoScope.Current.Value;
            CloudRequestIdentity Capture()
            {
                if (context == null) return capture();
                lock (context.CloudIdentities)
                {
                    if (context.Ended) throw new OperationCanceledException("The cloud operation has ended.");
                    if (!context.CloudIdentities.TryGetValue(provider, out var identity))
                        context.CloudIdentities.Add(provider, identity = capture());
                    return identity;
                }
            }
            return !requiresUnityThread || UnityMainThreadDispatcher.IsMainThread ? Task.FromResult(Capture())
                : UnityMainThreadDispatcher.Instance().EnqueueAsync(Capture);
        }

        public void Dispose() => current.Value = previous;
    }
}
#endif
