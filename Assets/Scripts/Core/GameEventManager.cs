namespace Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Global typed event bus. Decouples systems by letting publishers and subscribers
    /// communicate through strongly-typed <see cref="GameEvent"/> instances without
    /// holding direct references to each other.
    /// </summary>
    /// <remarks>
    /// <para>All listeners are cleared when <see cref="Clear"/> is called — typically
    /// on scene unload or application shutdown via <see cref="GameBootstrapper"/>.</para>
    /// <para>Listeners added during event dispatch are not invoked for the current dispatch
    /// cycle. Listeners removed during dispatch are safe — the internal list is copied
    /// before iteration.</para>
    /// </remarks>
    /// <example>
    /// Publishing an event:
    /// <code>
    /// GameEventManager.Publish(new PlayerDiedEvent { FinalScore = 100 });
    /// </code>
    /// Subscribing and unsubscribing:
    /// <code>
    /// GameEventManager.AddListener&lt;PlayerDiedEvent&gt;(OnPlayerDied);
    /// GameEventManager.RemoveListener&lt;PlayerDiedEvent&gt;(OnPlayerDied);
    ///
    /// private void OnPlayerDied(PlayerDiedEvent e) { ... }
    /// </code>
    /// </example>
    public static class GameEventManager
    {
        private static readonly Dictionary<Type, List<Delegate>> _eventListeners = new();

        /// <summary>
        /// Registers a listener for events of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The <see cref="GameEvent"/> type to listen for.</typeparam>
        /// <param name="listener">The callback to invoke when an event of type <typeparamref name="T"/> is published.</param>
        public static void AddListener<T>(Action<T> listener) where T : GameEvent
        {
            var type = typeof(T);
            if (!_eventListeners.ContainsKey(type))
                _eventListeners[type] = new List<Delegate>();

            _eventListeners[type].Add(listener);
        }

        /// <summary>
        /// Unregisters a previously added listener for events of type <typeparamref name="T"/>.
        /// Safe to call if the listener is not registered — no error is thrown.
        /// </summary>
        /// <typeparam name="T">The <see cref="GameEvent"/> type to stop listening for.</typeparam>
        /// <param name="listener">The callback to remove.</param>
        public static void RemoveListener<T>(Action<T> listener) where T : GameEvent
        {
            var type = typeof(T);
            if (!_eventListeners.TryGetValue(type, out var listeners))
                return;

            listeners.Remove(listener);

            if (listeners.Count == 0)
                _eventListeners.Remove(type);
        }

        /// <summary>
        /// Publishes an event to all registered listeners of type <typeparamref name="T"/>.
        /// Listeners are invoked synchronously in the order they were registered.
        /// </summary>
        /// <typeparam name="T">The <see cref="GameEvent"/> type to publish.</typeparam>
        /// <param name="gameEvent">The event instance to deliver to listeners.</param>
        public static void Publish<T>(T gameEvent) where T : GameEvent
        {
            var type = typeof(T);
            if (!_eventListeners.TryGetValue(type, out var listeners))
                return;

            foreach (var listener in listeners.ToArray())
                ((Action<T>)listener)?.Invoke(gameEvent);
        }

        /// <summary>
        /// Removes all registered listeners for all event types.
        /// Call this on scene unload or application shutdown to prevent stale references.
        /// </summary>
        public static void Clear()
        {
            _eventListeners.Clear();
        }
    }
}