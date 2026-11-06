namespace Core.Modules
{
    using System;
    using System.Collections.Generic;
    using Core.Assets;
    using Core.Content;
    using Core.Diagnostics;
    using Core.Scheduling;

    /// <summary>
    /// Marks a concrete <see cref="IEngineModule"/> class for automatic discovery by <see cref="ModuleHost"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class BuiltInEngineModuleAttribute : Attribute
    {
        public bool IsCoreModule { get; }

        public BuiltInEngineModuleAttribute(bool isCoreModule = false)
        {
            IsCoreModule = isCoreModule;
        }
    }

    /// <summary>
    /// Exception thrown when a module declares a required dependency that is not present in the active module set.
    /// </summary>
    public sealed class MissingModuleDependencyException : InvalidOperationException
    {
        public string ModuleId { get; }
        public string MissingDependencyId { get; }

        public MissingModuleDependencyException(string moduleId, string missingDependencyId)
            : base($"Module '{moduleId}' requires missing dependency module '{missingDependencyId}'.")
        {
            ModuleId = moduleId;
            MissingDependencyId = missingDependencyId;
        }
    }

    /// <summary>
    /// Exception thrown when a circular dependency is detected in the module dependency graph during topological sort.
    /// </summary>
    public sealed class ModuleDependencyCycleException : InvalidOperationException
    {
        public IReadOnlyList<string> InvolvedModuleIds { get; }

        public ModuleDependencyCycleException(IReadOnlyList<string> involvedModuleIds)
            : base($"Circular dependency detected among engine modules: {string.Join(" -> ", involvedModuleIds)}")
        {
            InvolvedModuleIds = involvedModuleIds;
        }
    }

    /// <summary>
    /// Immutable metadata and dependency manifest for an <see cref="IEngineModule"/>.
    /// </summary>
    public sealed class ModuleDescriptor
    {
        public string ModuleId { get; }
        public string DisplayName { get; }
        public string Version { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public IReadOnlyList<string> OptionalDependencies { get; }

        public ModuleDescriptor(
            string moduleId,
            string displayName,
            string version = "1.0.0",
            string[] dependencies = null,
            string[] optionalDependencies = null)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                throw new ArgumentException("ModuleId cannot be null or empty.", nameof(moduleId));
            }

            ModuleId = moduleId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ModuleId : displayName.Trim();
            Version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Trim();
            Dependencies = dependencies ?? Array.Empty<string>();
            OptionalDependencies = optionalDependencies ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Scoped context passed to an <see cref="IEngineModule"/> during registration, initialization, activation, and shutdown.
    /// </summary>
    public interface IModuleContext
    {
        string ModuleId { get; }
        IContentService Content { get; }
        IAssetProvider Assets { get; }
        IDiagnosticService Diagnostics { get; }
        TickScheduler Scheduler { get; }
        IEventScope Events { get; }

        void RegisterService<T>(T service) where T : class, IGameService;
        void RegisterService<TInterface, TImpl>(TImpl service)
            where TInterface : class, IGameService
            where TImpl : class, TInterface;

        void RegisterTickable(ITickable tickable, TickGroup group = TickGroup.Simulation, float intervalSeconds = 0f);
        T ResolveService<T>() where T : class, IGameService;
        bool TryResolveService<T>(out T service) where T : class, IGameService;
    }

    /// <summary>
    /// Contract for pluggable Engine Core, Fantasy Framework, and Gameplay Feature modules.
    /// Executed deterministically by <see cref="ModuleHost"/> in topological dependency order.
    /// </summary>
    public interface IEngineModule
    {
        ModuleDescriptor Descriptor { get; }

        /// <summary>Phase 1: Instantiate and register services with <paramref name="context"/>. Do not resolve other modules' services here.</summary>
        void RegisterServices(IModuleContext context);

        /// <summary>Phase 2: Resolve cross-module services, register content definitions, and subscribe to events.</summary>
        void Initialize(IModuleContext context);

        /// <summary>Phase 3: Called after all modules and services are initialized and content registries are ready.</summary>
        void OnActivate(IModuleContext context);

        /// <summary>Teardown: Called in reverse topological order during engine shutdown.</summary>
        void OnShutdown(IModuleContext context);
    }

    /// <summary>
    /// Convenience base class for <see cref="IEngineModule"/> implementations with virtual no-op lifecycle hooks.
    /// </summary>
    public abstract class EngineModuleBase : IEngineModule
    {
        public abstract ModuleDescriptor Descriptor { get; }

        public virtual void RegisterServices(IModuleContext context) { }
        public virtual void Initialize(IModuleContext context) { }
        public virtual void OnActivate(IModuleContext context) { }
        public virtual void OnShutdown(IModuleContext context) { }
    }
}
