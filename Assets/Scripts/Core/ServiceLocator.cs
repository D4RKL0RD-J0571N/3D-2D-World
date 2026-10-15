namespace Core
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Global service registry. Provides a single resolution point for shared runtime systems.
    /// </summary>
    /// <remarks>
    /// <para>Use the ServiceLocator for systems consumed by two or more other systems,
    /// or for globally shared runtime services such as audio, input, save, UI, and state machines.
    /// For dependencies internal to a single feature, prefer direct references.</para>
    /// <para>All services are registered and initialized in <see cref="GameBootstrapper"/>.
    /// Registering a duplicate type logs a warning and overwrites the existing entry after
    /// shutting it down — this supports scene reloads and editor Play Mode restarts.</para>
    /// </remarks>
    /// <example>
    /// Registering a service:
    /// <code>
    /// ServiceLocator.Register(new AudioManager());
    /// </code>
    /// Retrieving a service:
    /// <code>
    /// var audio = ServiceLocator.Get&lt;AudioManager&gt;();
    /// audio?.PlayClip(clip);
    /// </code>
    /// Optional retrieval without logging an error on miss:
    /// <code>
    /// if (ServiceLocator.TryGet&lt;AudioManager&gt;(out var audio))
    ///     audio.PlayClip(clip);
    /// </code>
    /// </example>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, IGameService> _services = new();

        /// <summary>
        /// Registers a service of type <typeparamref name="T"/> and calls its
        /// <see cref="IGameService.Initialize"/> method.
        /// If a service of the same type is already registered, it is shut down and replaced.
        /// </summary>
        /// <typeparam name="T">The service type used as the lookup key. Must implement <see cref="IGameService"/>.</typeparam>
        /// <param name="service">The service instance to register.</param>
        public static void Register<T>(T service) where T : class, IGameService
        {
            var type = typeof(T);

            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] {type.Name} already registered. Overwriting.");
                _services[type].Shutdown();
            }

            _services[type] = service;
            service.Initialize();
        }

        /// <summary>
        /// Returns the registered service of type <typeparamref name="T"/>.
        /// Logs an error and returns <c>null</c> if the service is not registered.
        /// </summary>
        /// <typeparam name="T">The service type to retrieve.</typeparam>
        /// <returns>The registered service instance, or <c>null</c> if not found.</returns>
        public static T Get<T>() where T : class, IGameService
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;

            Debug.LogError($"[ServiceLocator] Service {typeof(T).Name} not found.");
            return null;
        }

        /// <summary>
        /// Attempts to retrieve the registered service of type <typeparamref name="T"/>
        /// without logging an error on miss. Use this for optional services.
        /// </summary>
        /// <typeparam name="T">The service type to retrieve.</typeparam>
        /// <param name="service">
        /// When this method returns <c>true</c>, contains the registered service instance.
        /// When <c>false</c>, contains <c>null</c>.
        /// </param>
        /// <returns><c>true</c> if the service is registered; otherwise <c>false</c>.</returns>
        public static bool TryGet<T>(out T service) where T : class, IGameService
        {
            if (_services.TryGetValue(typeof(T), out var raw))
            {
                service = (T)raw;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Unregisters the service of type <typeparamref name="T"/> and calls its
        /// <see cref="IGameService.Shutdown"/> method.
        /// Logs a warning and returns silently if the service is not registered.
        /// </summary>
        /// <typeparam name="T">The service type to unregister.</typeparam>
        public static void Unregister<T>() where T : class, IGameService
        {
            var type = typeof(T);

            if (!_services.TryGetValue(type, out var service))
            {
                Debug.LogWarning($"[ServiceLocator] Cannot unregister {type.Name} — not registered.");
                return;
            }

            _services.Remove(type);
            service.Shutdown();
        }

        /// <summary>
        /// Shuts down and removes all registered services.
        /// Called automatically by <see cref="GameBootstrapper"/> on destroy.
        /// </summary>
        public static void Clear()
        {
            foreach (var service in _services.Values)
                service.Shutdown();

            _services.Clear();
        }
    }
}