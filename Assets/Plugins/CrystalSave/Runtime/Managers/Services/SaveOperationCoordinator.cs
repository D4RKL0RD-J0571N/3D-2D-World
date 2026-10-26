#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace Arawn.CrystalSave.Runtime
{
    internal sealed class SaveOperationContext : IDisposable
    {
        readonly CancellationTokenSource cancellation;
        readonly CancellationToken token;
        internal CancellationToken Token => token;
        // Once commit/application starts, follow-up persistence belongs to its actual outcome.
        internal CancellationToken PreparationToken => Committed ? CancellationToken.None : Token;
        internal SaveSlot WorkingSlot;
        internal bool Committed { get; private set; }
        // All payload/header writes and configured verification completed. Later cache
        // cleanup failures must not undo publication or delete the committed image.
        internal bool PayloadCommitted;
        bool ended;
        internal bool Ended => ended;
        internal bool PreserveLiveState;
        internal readonly Dictionary<string, string> MetadataOverrides = new();
        internal string RequestedSlotName;
        internal SaveSlotMetadataUpdate MetadataUpdate;
        internal readonly List<Exception> RestoreErrors = new();
        internal readonly List<ComponentOperationScope> PendingRestores = new();
        internal readonly List<Func<Task>> PreparedCacheWrites = new List<Func<Task>>();
        internal readonly List<Func<Task>> CommitSideEffects = new();
        internal readonly Dictionary<object, CloudRequestIdentity> CloudIdentities = new();
        internal readonly Dictionary<object, SaveWritePlan> WritePlans = new();
        internal Func<Task<List<PreparedSaveWrite>>> PrepareHistory;
        internal readonly List<Action> Rollback = new List<Action>();
        internal readonly List<Func<Task>> AsyncRollback = new();
        internal string SaveError, LoadError;
        internal bool SaveReported, LoadReported;
        internal readonly List<Action> Notifications = new List<Action>();
        internal SaveOperationContext(CancellationToken token, CancellationToken lifetime)
        { cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime); this.token = cancellation.Token; }
        internal void CheckCancellation()
        {
            if (Committed) return;
            if (ended) throw new OperationCanceledException("Operation has ended.");
            Token.ThrowIfCancellationRequested();
        }
        internal void BeginCommit()
        {
            CheckCancellation();
            Committed = true;
        }
        internal async Task WaitAsync(Task task)
        {
            CheckCancellation();
            if (Committed || !Token.CanBeCanceled) { await task; return; }
#if UNITY_WEBGL && !UNITY_EDITOR
            // Task.WhenAny may schedule completion actions through the unavailable Web thread pool.
            while (!task.IsCompleted)
            {
                if (Token.IsCancellationRequested) { ObserveLateFailure(task); Token.ThrowIfCancellationRequested(); }
                await Task.Yield();
            }
            await task;
#else
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (Token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task) != task)
                {
                    ObserveLateFailure(task);
                    throw new OperationCanceledException(Token);
                }
                await task;
            }
#endif
            CheckCancellation();
        }
        internal static async void ObserveLateFailure(Task task)
        { try { await task; } catch (Exception) { /* The request has already reported cancellation. */ } }
        internal async Task<T> WaitAsync<T>(Task<T> task) { await WaitAsync((Task)task); return task.GetAwaiter().GetResult(); }
        public void Dispose()
        {
            ended = true; cancellation.Dispose();
            WorkingSlot = null; Rollback.Clear(); Notifications.Clear(); PreparedCacheWrites.Clear();
            RestoreErrors.Clear(); PendingRestores.Clear();
            MetadataOverrides.Clear();
            MetadataUpdate = null;
            CommitSideEffects.Clear(); PrepareHistory = null;
            AsyncRollback.Clear();
            lock (CloudIdentities) CloudIdentities.Clear();
            lock (WritePlans) WritePlans.Clear();
        }
    }

    // Flows with backend Tasks, including continuations arriving after a cancelled load.
    internal static class SaveIoScope
    {
        // Native transports may continue off-thread; WebGL has no managed thread pool.
#if UNITY_WEBGL && !UNITY_EDITOR
        internal const bool PreserveUnityContext = true;
#else
        internal const bool PreserveUnityContext = false;
#endif
        internal static readonly AsyncLocal<SaveOperationContext> Current = new AsyncLocal<SaveOperationContext>();
        static readonly AsyncLocal<CancellationToken?> readToken = new();
        internal static void CheckCancellation()
        {
            readToken.Value?.ThrowIfCancellationRequested();
            Current.Value?.CheckCancellation();
        }
        internal static CancellationToken Token => readToken.Value ?? (Current.Value?.PreparationToken ?? CancellationToken.None);
        internal static IDisposable WithReadCancellation(CancellationToken token) => new ReadCancellation(token);
        sealed class ReadCancellation : IDisposable
        {
            readonly CancellationToken? previous;
            internal ReadCancellation(CancellationToken token) { previous = readToken.Value; readToken.Value = token; }
            public void Dispose() { readToken.Value = previous; }
        }
        internal static CancellationTokenRegistration AbortOnCancellation(UnityEngine.Networking.UnityWebRequest request)
        {
            var dispatcher = UnityMainThreadDispatcher.Instance();
            var token = Token;
            return token.Register(() => dispatcher.Enqueue(() => { try { request.Abort(); } catch (ObjectDisposedException) { } }));
        }
    }

    internal sealed class SaveOperationCoordinator : IDisposable
    {
        readonly HashSet<IEnumerator> coroutines = new HashSet<IEnumerator>();
        readonly Queue<Action> pending = new Queue<Action>();
        readonly object gate = new object();
        readonly UnityMainThreadDispatcher dispatcher;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        bool running, disposed;
        internal SaveOperationContext Active { get; private set; }
        internal bool IsBusy { get { lock (gate) return running || pending.Count != 0; } }
        internal int PendingCount { get { lock (gate) return pending.Count; } }
        internal SaveOperationCoordinator(MonoBehaviour host)
        { dispatcher = UnityMainThreadDispatcher.InstanceIndependentOf(host.gameObject); }

        internal Task RunAsync(Func<SaveOperationContext, Task> work, CancellationToken token = default)
            => RunAsync(async context => { await work(context); return true; }, token);

        internal Task<T> RunAsync<T>(Func<SaveOperationContext, Task<T>> work, CancellationToken token = default)
        {
            if (token.IsCancellationRequested) return Task.FromCanceled<T>(token);
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var context = new SaveOperationContext(token, lifetime.Token);
            CancellationTokenRegistration registration = default;
            // Cancellation completes a request still in the queue without accessing Unity.
            registration = context.Token.Register(() => { lock (gate) { if (Active != context && !context.Committed) tcs.TrySetCanceled(context.Token); } });
            lock (gate)
            {
                if (disposed) { registration.Dispose(); context.Dispose(); return Task.FromException<T>(new ObjectDisposedException(nameof(SaveManager))); }
                pending.Enqueue(async () =>
                {
                    if (tcs.Task.IsCompleted) { registration.Dispose(); context.Dispose(); Finish(); return; }
                    Active = context;
                    var previous = SaveIoScope.Current.Value;
                    SaveIoScope.Current.Value = context;
                    T result = default; Exception failure = null;
                    try
                    {
                        try { context.CheckCancellation(); result = await work(context); }
                        catch (Exception ex) { failure = ex; }
                        if (failure != null || context.SaveError != null)
                        {
                            // Keep queue ownership until cleanup is durable. Cancellation
                            // cannot interrupt removal of a rejected operation's staged files.
                            SaveIoScope.Current.Value = null;
                            var cleanupErrors = new List<Exception>();
                            foreach (var rollback in context.Rollback)
                                try { rollback(); }
                                catch (Exception ex) { cleanupErrors.Add(ex); }
                            foreach (var rollback in context.AsyncRollback)
                                try { await rollback(); }
                                catch (Exception ex) { cleanupErrors.Add(ex); }
                            if (cleanupErrors.Count != 0)
                            {
                                cleanupErrors.Insert(0, failure ?? new InvalidOperationException(context.SaveError));
                                failure = new AggregateException("Operation failed and staged-file cleanup could not be acknowledged.", cleanupErrors);
                                Debug.LogWarning(failure.Message);
                            }
                        }
                    }
                    finally
                    {
                        SaveIoScope.Current.Value = previous;
                        Active = null;
                        registration.Dispose();
                        lock (gate) running = false;
                    }
                    // Ownership is released before notifying users. A failing listener cannot strand a Task.
                    foreach (var notify in context.Notifications)
                        try { notify(); } catch (Exception ex) { Debug.LogException(ex); }
                    if (failure is OperationCanceledException) tcs.TrySetCanceled(context.Token);
                    else if (failure != null) tcs.TrySetException(failure);
                    else tcs.TrySetResult(result);
                    context.Dispose();
                    Schedule();
                });
            }
            Schedule();
            return tcs.Task;
        }
        void Schedule()
        {
            // Always post, so completion callbacks cannot recursively enter a new operation.
            dispatcher.Enqueue(() =>
            {
                Action next;
                lock (gate)
                {
                    if (running || pending.Count == 0) return;
                    running = true; next = pending.Dequeue();
                }
                next();
            });
        }
        void Finish() { lock (gate) running = false; Schedule(); }
        internal void RunSync(Action<SaveOperationContext> work)
        {
            if (!UnityMainThreadDispatcher.IsMainThread) throw new InvalidOperationException("Synchronous saves require Unity's main thread.");
            lock (gate)
            {
                if (disposed || running || pending.Count != 0) throw new InvalidOperationException("Crystal Save is busy. Use the asynchronous API to queue this operation.");
                running = true;
            }
            using var context = new SaveOperationContext(default, lifetime.Token);
            Active = context; var previous = SaveIoScope.Current.Value; SaveIoScope.Current.Value = context;
            try { work(context); }
            catch { foreach (var rollback in context.Rollback) try { rollback(); } catch (Exception ex) { Debug.LogWarning(ex.Message); } throw; }
            finally
            {
                Active = null; SaveIoScope.Current.Value = previous;
                lock (gate) running = false;
                foreach (var notification in context.Notifications)
                    try { notification(); } catch (Exception ex) { Debug.LogException(ex); }
                Schedule();
            }
        }
        internal Task RunCoroutine(IEnumerator routine, SaveOperationContext context) => RunCoroutine(routine, context, out _);
        internal Task RunCoroutine(IEnumerator routine, SaveOperationContext context, out IEnumerator pump)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            IEnumerator owned = null;
            owned = Pump(routine, context, tcs, () => coroutines.Remove(owned));
            pump = owned;
            coroutines.Add(pump);
            // The manager remains the operation owner, but deactivating its GameObject
            // must not stop a committed scene transition or its finally blocks.
            dispatcher.StartCoroutine(pump);
            return tcs.Task;
        }
        internal void StopRoutine(IEnumerator routine)
        {
            if (dispatcher) dispatcher.StopCoroutine(routine);
            (routine as IDisposable)?.Dispose();
            coroutines.Remove(routine);
        }
        static IEnumerator Pump(IEnumerator routine, SaveOperationContext context, TaskCompletionSource<bool> tcs, Action release)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    object yielded = null; Exception error = null;
                    var previous = SaveIoScope.Current.Value; SaveIoScope.Current.Value = context;
                    try
                    {
                        context.CheckCancellation();
                        var top = stack.Peek();
                        if (!top.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                        yielded = top.Current;
                        if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
                    }
                    catch (Exception ex) { error = ex; }
                    finally { SaveIoScope.Current.Value = previous; }
                    if (error != null) { tcs.TrySetException(error); yield break; }
                    yield return yielded;
                }
                tcs.TrySetResult(true);
            }
            finally
            {
                Exception disposalError = null;
                while (stack.Count > 0)
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception ex) { disposalError = ex; }
                if (disposalError != null) tcs.TrySetException(disposalError);
                else if (!tcs.Task.IsCompleted) tcs.TrySetCanceled();
                // Coroutine disposal runs on Unity's thread, including threadless WebGL players.
                release();
            }
        }
        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed = true; }
            lifetime.Cancel();
            // Unity does not promise to dispose nested iterators when a host is destroyed.
            foreach (var routine in new List<IEnumerator>(coroutines))
            {
                if (dispatcher) dispatcher.StopCoroutine(routine);
                (routine as IDisposable)?.Dispose();
            }
            coroutines.Clear();
            // Queued callbacks dispose their registrations when the dispatcher drains.
        }
    }
    internal static class SaveEventDispatch
    {
        internal static void Raise<T>(EventHandler<T> handlers, object sender, T args)
        { if (handlers == null) return; foreach (EventHandler<T> handler in handlers.GetInvocationList()) try { handler(sender, args); } catch (Exception ex) { Debug.LogException(ex); } }
        internal static void Raise(Action handlers)
        { if (handlers == null) return; foreach (Action handler in handlers.GetInvocationList()) try { handler(); } catch (Exception ex) { Debug.LogException(ex); } }
        internal static void Raise<T>(Action<T> handlers, T value)
        { if (handlers == null) return; foreach (Action<T> handler in handlers.GetInvocationList()) try { handler(value); } catch (Exception ex) { Debug.LogException(ex); } }
        internal static void Raise<T1, T2>(Action<T1, T2> handlers, T1 first, T2 second)
        { if (handlers == null) return; foreach (Action<T1, T2> handler in handlers.GetInvocationList()) try { handler(first, second); } catch (Exception ex) { Debug.LogException(ex); } }
    }
}
#endif
