namespace Core
{
    using System;
    using System.Collections.Generic;
    using Core.Diagnostics;
    using UnityEngine;

    /// <summary>
    /// Scoped event subscription token container. When disposed (e.g., on module or mod shutdown),
    /// automatically unsubscribes all listeners registered through this scope.
    /// </summary>
    public interface IEventScope : IDisposable
    {
        void Subscribe<T>(Action<T> listener) where T : GameEvent;
    }

    /// <summary>
    /// Global typed event bus. Decouples systems by letting publishers and subscribers
    /// communicate through strongly-typed <see cref="GameEvent"/> instances without
    /// holding direct references to each other.
    /// Isolates exceptions per listener so a fault in one handler never aborts event delivery to remaining subscribers.
    /// </summary>
    public static class GameEventManager
    {
        private static readonly Dictionary<Type, List<Delegate>> _eventListeners = new();

        /// <summary>
        /// Registers a listener for events of type <typeparamref name="T"/>.
        /// </summary>
        public static void AddListener<T>(Action<T> listener) where T : GameEvent
        {
            if (listener == null) return;

            var type = typeof(T);
            if (!_eventListeners.TryGetValue(type, out var listeners))
            {
                listeners = new List<Delegate>();
                _eventListeners[type] = listeners;
            }

            if (!listeners.Contains(listener))
            {
                listeners.Add(listener);
            }
        }

        /// <summary>Alias for <see cref="AddListener{T}"/>.</summary>
        public static void Subscribe<T>(Action<T> listener) where T : GameEvent => AddListener(listener);

        /// <summary>
        /// Unregisters a previously added listener for events of type <typeparamref name="T"/>.
        /// </summary>
        public static void RemoveListener<T>(Action<T> listener) where T : GameEvent
        {
            if (listener == null) return;

            var type = typeof(T);
            if (!_eventListeners.TryGetValue(type, out var listeners))
                return;

            listeners.Remove(listener);

            if (listeners.Count == 0)
                _eventListeners.Remove(type);
        }

        /// <summary>Alias for <see cref="RemoveListener{T}"/>.</summary>
        public static void Unsubscribe<T>(Action<T> listener) where T : GameEvent => RemoveListener(listener);

        /// <summary>
        /// Creates a disposable <see cref="IEventScope"/> that tracks subscriptions and automatically unsubscribes them on <c>Dispose()</c>.
        /// </summary>
        public static IEventScope CreateScope()
        {
            return new EventScope();
        }

        /// <summary>
        /// Publishes an event to all registered listeners of type <typeparamref name="T"/>.
        /// Listeners are invoked synchronously in registration order with per-listener exception isolation.
        /// </summary>
        /// <param name="gameEvent">The event instance to deliver to listeners.</param>
        /// <param name="logExceptionsToUnityConsole">If false, records caught exceptions to <see cref="IDiagnosticService"/> without calling <c>Debug.LogError</c>.</param>
        public static void Publish<T>(T gameEvent, bool logExceptionsToUnityConsole = true) where T : GameEvent
        {
            if (gameEvent == null) return;

            var type = typeof(T);
            if (!_eventListeners.TryGetValue(type, out var listeners) || listeners.Count == 0)
                return;

            var snapshot = listeners.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] is not Action<T> typedListener)
                    continue;

                try
                {
                    typedListener.Invoke(gameEvent);
                }
                catch (Exception ex)
                {
                    if (ServiceLocator.TryGet<IDiagnosticService>(out var diagnostics))
                    {
                        diagnostics.RecordException($"GameEventManager.Publish<{type.Name}>", ex, logExceptionsToUnityConsole);
                    }
                    else if (logExceptionsToUnityConsole)
                    {
                        Debug.LogError($"[GameEventManager] Exception in listener for {type.Name}: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Removes all registered listeners for all event types.
        /// </summary>
        public static void Clear()
        {
            _eventListeners.Clear();
        }

        private sealed class EventScope : IEventScope
        {
            private readonly List<Action> _unsubscribeActions = new();
            private bool _isDisposed;

            public void Subscribe<T>(Action<T> listener) where T : GameEvent
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(nameof(EventScope));
                }

                AddListener(listener);
                _unsubscribeActions.Add(() => RemoveListener(listener));
            }

            public void Dispose()
            {
                if (_isDisposed) return;
                _isDisposed = true;

                for (int i = _unsubscribeActions.Count - 1; i >= 0; i--)
                {
                    _unsubscribeActions[i]?.Invoke();
                }

                _unsubscribeActions.Clear();
            }
        }
    }
}
