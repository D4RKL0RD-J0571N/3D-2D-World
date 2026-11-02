namespace Core
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Global service registry supporting both Two-Phase module initialization
    /// (<see cref="BeginRegistrationPhase"/> / <see cref="CompleteRegistrationAndInitialize"/>)
    /// and immediate single-phase registration for backward compatibility.
    /// </summary>
    public static class ServiceLocator
    {
        #region Fields

        private static readonly Dictionary<Type, IGameService> _services = new();
        private static readonly List<IGameService> _pendingInitialization = new();
        private static readonly HashSet<IGameService> _initializedInstances = new();
        private static bool _isRegistrationPhase;

        #endregion

        #region Properties

        /// <summary>Returns true if the locator is currently in Phase 1 (Service Registration) prior to Phase 2 initialization.</summary>
        public static bool IsRegistrationPhase => _isRegistrationPhase;

        #endregion

        #region Two-Phase Lifecycle Control

        /// <summary>
        /// Enters Phase 1 (Registration Phase). Services registered while this phase is active
        /// are added to the lookup table immediately, but their <see cref="IGameService.Initialize"/>
        /// calls are deferred until <see cref="CompleteRegistrationAndInitialize"/> is invoked.
        /// </summary>
        public static void BeginRegistrationPhase()
        {
            _isRegistrationPhase = true;
            _pendingInitialization.Clear();
        }

        /// <summary>
        /// Completes Phase 1 and executes Phase 2: invokes <see cref="IGameService.Initialize"/>
        /// on all newly registered services in deterministic registration order.
        /// Guarantees that every service can safely resolve any other registered service inside <c>Initialize()</c>.
        /// </summary>
        public static void CompleteRegistrationAndInitialize()
        {
            _isRegistrationPhase = false;

            var toInitialize = new List<IGameService>(_pendingInitialization);
            _pendingInitialization.Clear();

            for (int i = 0; i < toInitialize.Count; i++)
            {
                IGameService service = toInitialize[i];
                if (service != null && _initializedInstances.Add(service))
                {
                    service.Initialize();
                }
            }
        }

        #endregion

        #region Registration & Resolution

        /// <summary>
        /// Registers a service under its type <typeparamref name="T"/>.
        /// In Two-Phase mode, defers <see cref="IGameService.Initialize"/> to Phase 2;
        /// in immediate mode, invokes <see cref="IGameService.Initialize"/> immediately (once per unique instance).
        /// </summary>
        public static void Register<T>(T service) where T : class, IGameService
        {
            RegisterInternal(typeof(T), service);
        }

        /// <summary>
        /// Registers a concrete service implementation under both <typeparamref name="TInterface"/> and <typeparamref name="TImpl"/>,
        /// ensuring <see cref="IGameService.Initialize"/> is invoked only once for the shared instance.
        /// </summary>
        public static void Register<TInterface, TImpl>(TImpl service)
            where TInterface : class, IGameService
            where TImpl : class, TInterface
        {
            RegisterInternal(typeof(TInterface), service);
            if (typeof(TInterface) != typeof(TImpl))
            {
                RegisterInternal(typeof(TImpl), service);
            }
        }

        /// <summary>
        /// Returns the registered service of type <typeparamref name="T"/>.
        /// Logs an error and returns <c>null</c> if the service is not registered.
        /// </summary>
        public static T Get<T>() where T : class, IGameService
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }

            Debug.LogError($"[ServiceLocator] Service {typeof(T).Name} not found.");
            return null;
        }

        /// <summary>
        /// Attempts to retrieve the registered service of type <typeparamref name="T"/>
        /// without logging an error on miss.
        /// </summary>
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
        /// <see cref="IGameService.Shutdown"/> method if no other key references the same instance.
        /// </summary>
        public static void Unregister<T>() where T : class, IGameService
        {
            var type = typeof(T);

            if (!_services.TryGetValue(type, out var service))
            {
                Debug.LogWarning($"[ServiceLocator] Cannot unregister {type.Name} — not registered.");
                return;
            }

            _services.Remove(type);
            _pendingInitialization.Remove(service);

            if (!ContainsInstance(service))
            {
                _initializedInstances.Remove(service);
                service.Shutdown();
            }
        }

        /// <summary>
        /// Shuts down and removes all registered services (invoking <see cref="IGameService.Shutdown"/> once per unique instance in reverse order).
        /// </summary>
        public static void Clear()
        {
            _isRegistrationPhase = false;
            _pendingInitialization.Clear();

            var uniqueServices = new List<IGameService>();
            var visited = new HashSet<IGameService>();

            foreach (var service in _services.Values)
            {
                if (service != null && visited.Add(service))
                {
                    uniqueServices.Add(service);
                }
            }

            for (int i = uniqueServices.Count - 1; i >= 0; i--)
            {
                uniqueServices[i].Shutdown();
            }

            _services.Clear();
            _initializedInstances.Clear();
        }

        #endregion

        #region Internal Helpers

        private static void RegisterInternal(Type type, IGameService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (_services.TryGetValue(type, out var existing) && !ReferenceEquals(existing, service))
            {
                Debug.LogWarning($"[ServiceLocator] {type.Name} already registered. Overwriting.");
                _services.Remove(type);
                if (!ContainsInstance(existing))
                {
                    _initializedInstances.Remove(existing);
                    existing.Shutdown();
                }
            }

            _services[type] = service;

            if (_isRegistrationPhase)
            {
                if (!_pendingInitialization.Contains(service) && !_initializedInstances.Contains(service))
                {
                    _pendingInitialization.Add(service);
                }
            }
            else
            {
                if (_initializedInstances.Add(service))
                {
                    service.Initialize();
                }
            }
        }

        private static bool ContainsInstance(IGameService instance)
        {
            foreach (var registered in _services.Values)
            {
                if (ReferenceEquals(registered, instance))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
