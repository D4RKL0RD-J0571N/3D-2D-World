namespace Core.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Core.Assets;
    using Core.Content;
    using Core.Diagnostics;
    using Core.Scheduling;

    /// <summary>
    /// Lifecycle state of the <see cref="ModuleHost"/>.
    /// </summary>
    public enum ModuleHostState
    {
        Uninitialised = 0,
        Registering = 1,
        Initializing = 2,
        Active = 3,
        ShuttingDown = 4,
        Stopped = 5,
        Faulted = 6
    }

    /// <summary>
    /// Validates module dependencies, computes a deterministic topological boot order using Kahn's algorithm,
    /// orchestrates Two-Phase <see cref="ServiceLocator"/> initialization, drives the <see cref="TickScheduler"/>,
    /// and executes reverse-topological teardown.
    /// </summary>
    public sealed class ModuleHost
    {
        #region Fields

        private readonly List<IEngineModule> _registeredModules = new();
        private readonly List<IEngineModule> _sortedModules = new();
        private readonly Dictionary<string, IModuleContext> _contexts = new(StringComparer.Ordinal);

        private DiagnosticService _diagnostics;
        private ContentService _contentService;
        private AssetProviderService _assetProvider;
        private TickScheduler _tickScheduler;

        #endregion

        #region Properties

        public ModuleHostState State { get; private set; } = ModuleHostState.Uninitialised;

        /// <summary>Returns the modules in their resolved topological boot order.</summary>
        public IReadOnlyList<IEngineModule> SortedModules => _sortedModules;

        public IDiagnosticService Diagnostics => _diagnostics;
        public IContentService Content => _contentService;
        public IAssetProvider Assets => _assetProvider;
        public TickScheduler Scheduler => _tickScheduler;

        #endregion

        #region Module Registration & Discovery

        /// <summary>Adds an <see cref="IEngineModule"/> instance to the host prior to calling <see cref="Boot"/>.</summary>
        public void AddModule(IEngineModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (State != ModuleHostState.Uninitialised && State != ModuleHostState.Stopped)
            {
                throw new InvalidOperationException($"Cannot add module '{module.Descriptor.ModuleId}' while ModuleHost is in state {State}.");
            }

            _registeredModules.Add(module);
        }

        /// <summary>
        /// Discovers and instantiates built-in <see cref="IEngineModule"/> classes tagged with <see cref="BuiltInEngineModuleAttribute"/>
        /// across loaded assemblies, filtered by <paramref name="profile"/>.
        /// </summary>
        public void DiscoverAndAddModules(EngineBootstrapProfile profile = null)
        {
            bool coreOnly = profile != null && profile.CoreOnlyMode;
            HashSet<string> explicitFilter = (profile != null && profile.EnabledModuleIds != null && profile.EnabledModuleIds.Count > 0)
                ? new HashSet<string>(profile.EnabledModuleIds, StringComparer.Ordinal)
                : null;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Assembly assembly = assemblies[a];
                if (assembly.IsDynamic) continue;

                string asmName = assembly.GetName().Name;
                if (asmName.StartsWith("System", StringComparison.Ordinal) ||
                    asmName.StartsWith("Unity", StringComparison.Ordinal) ||
                    asmName.StartsWith("mscorlib", StringComparison.Ordinal) ||
                    asmName.StartsWith("Mono", StringComparison.Ordinal) ||
                    asmName.StartsWith("nunit", StringComparison.OrdinalIgnoreCase) ||
                    asmName.StartsWith("Sirenix", StringComparison.Ordinal))
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null) continue;

                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];
                    if (type == null || type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IEngineModule).IsAssignableFrom(type)) continue;

                    var attr = type.GetCustomAttribute<BuiltInEngineModuleAttribute>( inherit: false );
                    if (attr == null) continue;

                    if (coreOnly && !attr.IsCoreModule) continue;

                    if (Activator.CreateInstance(type) is IEngineModule instance)
                    {
                        if (explicitFilter != null && !explicitFilter.Contains(instance.Descriptor.ModuleId))
                        {
                            continue;
                        }

                        AddModule(instance);
                    }
                }
            }
        }

        #endregion

        #region Boot, Tick & Shutdown Lifecycle

        /// <summary>
        /// Validates dependencies, topologically sorts all added modules, and executes Phase 1 (RegisterServices),
        /// Phase 2 (Initialize), and Phase 3 (OnActivate) synchronously so Unity <c>Start()</c> methods never run against uninitialized services.
        /// </summary>
        public void Boot(bool hydrateProjectAssets = true)
        {
            try
            {
                _sortedModules.Clear();
                _contexts.Clear();

                // 1. Validate dependencies & compute topological order (throws before any service is initialized on error)
                List<IEngineModule> sorted = ComputeTopologicalOrder(_registeredModules);
                _sortedModules.AddRange(sorted);

                // 2. Phase 1 — Service Registration
                State = ModuleHostState.Registering;
                ServiceLocator.BeginRegistrationPhase();

                _diagnostics = new DiagnosticService();
                ServiceLocator.Register<IDiagnosticService, DiagnosticService>(_diagnostics);

                _contentService = new ContentService();
                ServiceLocator.Register<IContentService, ContentService>(_contentService);

                _assetProvider = new AssetProviderService();
                ServiceLocator.Register<IAssetProvider, AssetProviderService>(_assetProvider);

                _tickScheduler = new TickScheduler();
                ServiceLocator.Register(_tickScheduler);

                for (int i = 0; i < _sortedModules.Count; i++)
                {
                    IEngineModule module = _sortedModules[i];
                    var context = new ModuleContext(module.Descriptor.ModuleId, _contentService, _assetProvider, _diagnostics, _tickScheduler);
                    _contexts[module.Descriptor.ModuleId] = context;
                    module.RegisterServices(context);
                }

                // 3. Phase 2 — Service Initialization & Module Initialization
                State = ModuleHostState.Initializing;
                if (hydrateProjectAssets)
                {
                    LegacyDatabaseBridge.PopulateFromProjectAssets(_contentService);
                }

                ServiceLocator.CompleteRegistrationAndInitialize();

                for (int i = 0; i < _sortedModules.Count; i++)
                {
                    IEngineModule module = _sortedModules[i];
                    module.Initialize(_contexts[module.Descriptor.ModuleId]);
                }

                // 4. Phase 3 — Activation
                for (int i = 0; i < _sortedModules.Count; i++)
                {
                    IEngineModule module = _sortedModules[i];
                    module.OnActivate(_contexts[module.Descriptor.ModuleId]);
                }

                State = ModuleHostState.Active;
            }
            catch
            {
                State = ModuleHostState.Faulted;
                throw;
            }
        }

        /// <summary>Drives per-frame updates across all <see cref="ITickable"/> systems in <see cref="TickGroup"/> order.</summary>
        public void Tick(float deltaTime)
        {
            if (State != ModuleHostState.Active || _tickScheduler == null)
            {
                return;
            }

            _tickScheduler.TickAll(deltaTime);
        }

        /// <summary>
        /// Shuts down all active modules in strict reverse topological order, disposes module event scopes,
        /// and clears <see cref="ServiceLocator"/> and <see cref="GameEventManager"/>.
        /// </summary>
        public void Shutdown()
        {
            if (State == ModuleHostState.Stopped || State == ModuleHostState.Uninitialised)
            {
                return;
            }

            State = ModuleHostState.ShuttingDown;

            for (int i = _sortedModules.Count - 1; i >= 0; i--)
            {
                IEngineModule module = _sortedModules[i];
                if (_contexts.TryGetValue(module.Descriptor.ModuleId, out IModuleContext context))
                {
                    try
                    {
                        module.OnShutdown(context);
                    }
                    finally
                    {
                        context.Events?.Dispose();
                    }
                }
            }

            _contexts.Clear();
            _sortedModules.Clear();
            _registeredModules.Clear();

            ServiceLocator.Clear();
            GameEventManager.Clear();

            State = ModuleHostState.Stopped;
        }

        #endregion

        #region Kahn's Topological Sort & Dependency Validation

        public static List<IEngineModule> ComputeTopologicalOrder(IReadOnlyList<IEngineModule> modules)
        {
            var byId = new Dictionary<string, IEngineModule>(StringComparer.Ordinal);
            var originalIndex = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < modules.Count; i++)
            {
                IEngineModule mod = modules[i];
                if (mod?.Descriptor == null) continue;

                string id = mod.Descriptor.ModuleId;
                if (byId.ContainsKey(id))
                {
                    throw new InvalidOperationException($"Duplicate module ID '{id}' registered in ModuleHost.");
                }

                byId[id] = mod;
                originalIndex[id] = i;
            }

            // Build adjacency list (dependency -> dependent) and in-degree counts
            var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (string id in byId.Keys)
            {
                dependents[id] = new List<string>();
                inDegree[id] = 0;
            }

            foreach (var kvp in byId)
            {
                string modId = kvp.Key;
                ModuleDescriptor desc = kvp.Value.Descriptor;

                // Required dependencies
                if (desc.Dependencies != null)
                {
                    for (int i = 0; i < desc.Dependencies.Count; i++)
                    {
                        string depId = desc.Dependencies[i];
                        if (string.IsNullOrWhiteSpace(depId)) continue;

                        if (!byId.ContainsKey(depId))
                        {
                            throw new MissingModuleDependencyException(modId, depId);
                        }

                        dependents[depId].Add(modId);
                        inDegree[modId]++;
                    }
                }

                // Optional dependencies (order after them if present, ignore if absent)
                if (desc.OptionalDependencies != null)
                {
                    for (int i = 0; i < desc.OptionalDependencies.Count; i++)
                    {
                        string optId = desc.OptionalDependencies[i];
                        if (string.IsNullOrWhiteSpace(optId) || !byId.ContainsKey(optId)) continue;

                        dependents[optId].Add(modId);
                        inDegree[modId]++;
                    }
                }
            }

            // Ready queue ordered deterministically by original registration index then ModuleId
            var ready = new List<string>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                {
                    ready.Add(kvp.Key);
                }
            }

            SortReadyQueue(ready, originalIndex);

            var sortedResult = new List<IEngineModule>(byId.Count);
            while (ready.Count > 0)
            {
                string currentId = ready[0];
                ready.RemoveAt(0);
                sortedResult.Add(byId[currentId]);

                List<string> nextList = dependents[currentId];
                for (int i = 0; i < nextList.Count; i++)
                {
                    string dependentId = nextList[i];
                    inDegree[dependentId]--;
                    if (inDegree[dependentId] == 0)
                    {
                        ready.Add(dependentId);
                        SortReadyQueue(ready, originalIndex);
                    }
                }
            }

            if (sortedResult.Count != byId.Count)
            {
                var cycleMembers = new List<string>();
                foreach (var kvp in inDegree)
                {
                    if (kvp.Value > 0)
                    {
                        cycleMembers.Add(kvp.Key);
                    }
                }

                throw new ModuleDependencyCycleException(cycleMembers);
            }

            return sortedResult;
        }

        private static void SortReadyQueue(List<string> queue, Dictionary<string, int> originalIndex)
        {
            queue.Sort((a, b) =>
            {
                int cmp = originalIndex[a].CompareTo(originalIndex[b]);
                return cmp != 0 ? cmp : string.Compare(a, b, StringComparison.Ordinal);
            });
        }

        #endregion

        #region Internal ModuleContext Implementation

        private sealed class ModuleContext : IModuleContext
        {
            public string ModuleId { get; }
            public IContentService Content { get; }
            public IAssetProvider Assets { get; }
            public IDiagnosticService Diagnostics { get; }
            public TickScheduler Scheduler { get; }
            public IEventScope Events { get; }

            public ModuleContext(
                string moduleId,
                IContentService content,
                IAssetProvider assets,
                IDiagnosticService diagnostics,
                TickScheduler scheduler)
            {
                ModuleId = moduleId;
                Content = content;
                Assets = assets;
                Diagnostics = diagnostics;
                Scheduler = scheduler;
                Events = GameEventManager.CreateScope();
            }

            public void RegisterService<T>(T service) where T : class, IGameService
            {
                ServiceLocator.Register(service);
            }

            public void RegisterService<TInterface, TImpl>(TImpl service)
                where TInterface : class, IGameService
                where TImpl : class, TInterface
            {
                ServiceLocator.Register<TInterface, TImpl>(service);
            }

            public void RegisterTickable(ITickable tickable, TickGroup group = TickGroup.Simulation, float intervalSeconds = 0f)
            {
                Scheduler.Register(tickable, group, intervalSeconds);
            }

            public T ResolveService<T>() where T : class, IGameService
            {
                return ServiceLocator.Get<T>();
            }

            public bool TryResolveService<T>(out T service) where T : class, IGameService
            {
                return ServiceLocator.TryGet(out service);
            }
        }

        #endregion
    }
}
