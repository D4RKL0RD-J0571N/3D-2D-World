#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    // Component callbacks often deliberately catch exceptions. This scope carries their
    // final outcome independently of exceptions, including a helper's delayed application.
    internal sealed class ComponentOperationScope : IDisposable
    {
        static readonly AsyncLocal<ComponentOperationScope> current = new();
        readonly ComponentOperationScope previous;
        readonly List<Exception> errors;
        readonly SaveOperationContext operation;
        readonly ComponentRestoreState restoration;
        readonly object restoreGeneration;
        readonly string identity, route;
        readonly bool capture;
        Action succeeded, retained, completed;
        int pending;
        bool invocationEnded;
        internal readonly MonoBehaviour Component;
        internal bool Failed => errors.Count != 0;
        bool IsCurrentGeneration => capture || restoration == null ||
            ReferenceEquals(restoreGeneration, restoration.Generation);
        internal bool Waiting => IsCurrentGeneration && pending != 0 && Component && Component.isActiveAndEnabled;

        internal ComponentOperationScope(bool capture, string identity, string route, ISaveable component = null, ComponentRestoreState restoration = null)
        {
            previous = current.Value;
            this.capture = capture;
            this.identity = identity;
            this.route = route;
            Component = component as MonoBehaviour;
            operation = SaveIoScope.Current.Value;
            this.restoration = restoration;
            restoreGeneration = restoration?.Generation;
            errors = capture && previous?.capture == true ? previous.errors : new List<Exception>();
            current.Value = this;
        }

        internal static ComponentOperationScope Capture(string identity = null, string route = "Collection")
            => new(true, identity, route);

        internal static void Record(Exception error)
        {
            var scope = current.Value;
            if (scope == null) return;
            scope.RecordError(error);
        }

        void RecordError(Exception error)
        {
            if (!IsCurrentGeneration) return;
            var issue = new InvalidOperationException(
                $"{(capture ? "Capture" : "Restore")} failed for '{identity ?? "save data"}' ({route}): {error.Message}", error);
            errors.Add(issue);
            if (!capture && operation != null && !operation.Ended)
                operation.RestoreErrors.Add(issue);
        }

        internal void ThrowIfFailed()
        {
            if (Failed) throw new AggregateException("Required save data could not be captured.", errors);
        }

        internal static bool Restore(ISaveable component, byte[] data, Action onSuccess, Action retain, ComponentRestoreState restoration, Action onCompleted = null)
        {
            string route = component is SaveableComponent helper && helper.PrefabHandlesSerialization
                ? "Prefab-owned component blob" : "Global component";
            using var scope = new ComponentOperationScope(false, component.UniqueIdentifier, route, component, restoration);
            scope.succeeded = onSuccess;
            scope.retained = retain;
            scope.completed = onCompleted;
            try { component.LoadData(data); }
            catch (Exception ex)
            {
                scope.RecordError(ex);
                Logger.Log($"Component restore failed for '{component.UniqueIdentifier}': {ex.Message}", LogLevel.Error);
            }
            scope.invocationEnded = true;
            if (scope.pending != 0)
            {
                retain?.Invoke();
                scope.operation?.PendingRestores.Add(scope);
            }
            else scope.PublishOutcome();
            return !scope.Failed && scope.pending == 0;
        }

        void PublishOutcome()
        {
            try
            {
                if (IsCurrentGeneration)
                {
                    if (Failed) retained?.Invoke();
                    else succeeded?.Invoke();
                }
            }
            finally
            {
                completed?.Invoke();
                succeeded = null;
                retained = null;
                completed = null;
            }
        }

        internal static ApplicationTicket DeferApplication()
        {
            var scope = current.Value;
            if (scope == null || scope.capture) return null;
            scope.pending++;
            return new ApplicationTicket(scope);
        }

        internal sealed class ApplicationTicket
        {
            ComponentOperationScope scope;
            internal ApplicationTicket(ComponentOperationScope scope) { this.scope = scope; }
            internal bool IsCurrent => scope != null && scope.IsCurrentGeneration;
            internal IDisposable Resume() => new ResumedScope(scope);
            internal void Complete(Exception error = null)
            {
                var owner = scope;
                if (owner == null) return;
                scope = null;
                if (error != null) owner.RecordError(error);
                owner.pending--;
                if (owner.invocationEnded && owner.pending == 0) owner.PublishOutcome();
            }
        }

        sealed class ResumedScope : IDisposable
        {
            readonly ComponentOperationScope previous;
            internal ResumedScope(ComponentOperationScope scope)
            {
                previous = current.Value;
                current.Value = scope;
            }
            public void Dispose() { current.Value = previous; }
        }

        internal static string DescribeErrors(IEnumerable<Exception> errors)
        {
            var all = errors.ToList();
            return $"Restoration failed for {all.Count} component operation(s): " +
                string.Join("; ", all.Take(10).Select(error => error.Message));
        }

        public void Dispose() { current.Value = previous; }
    }
}
#endif
