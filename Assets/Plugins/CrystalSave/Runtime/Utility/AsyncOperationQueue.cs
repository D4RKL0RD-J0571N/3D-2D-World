using System;
using System.Threading;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    // Unlike Mono's SemaphoreSlim, releasing this queue does not schedule a worker
    // to complete its waiter. Awaiters retain their caller's synchronization context.
    internal sealed class AsyncOperationQueue
    {
        readonly object gate = new();
        Task tail = Task.CompletedTask;

        internal async Task<IDisposable> EnterAsync()
        {
            Task previous;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                previous = tail;
                tail = completion.Task;
            }
            await previous;
            return new Lease(completion);
        }

        sealed class Lease : IDisposable
        {
            TaskCompletionSource<bool> completion;
            internal Lease(TaskCompletionSource<bool> completion) { this.completion = completion; }
            public void Dispose() => Interlocked.Exchange(ref completion, null)?.TrySetResult(true);
        }
    }
}
