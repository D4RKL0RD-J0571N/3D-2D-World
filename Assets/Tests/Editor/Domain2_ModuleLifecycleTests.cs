namespace Tests.Editor
{
    using System.Collections.Generic;
    using Core;
    using Core.Modules;
    using Core.Scheduling;
    using NUnit.Framework;
    using Systems.Interaction;
    using Systems.Meshing;

    /// <summary>
    /// Milestone 2 verification suite for Domain 2 (Module Lifecycle, Topological Sort & Two-Phase ServiceLocator):
    /// Verifies deterministic Kahn's topological sorting (<c>MOD-01</c>), reverse-order shutdown (<c>MOD-02</c>),
    /// cycle detection (<c>MOD-03</c>), missing dependency detection (<c>MOD-04</c>), and Two-Phase service resolution (<c>MOD-05</c>).
    /// </summary>
    [TestFixture]
    public class Domain2_ModuleLifecycleTests
    {
        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            GameEventManager.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            GameEventManager.Clear();
        }

        [Test]
        public void MOD01_And_MOD02_ModuleHost_TopologicalSortAndReverseShutdown_AreDeterministic()
        {
            var executionLog = new List<string>();
            var host = new ModuleHost();

            // Register in scrambled order: C (depends on B), A (no deps), B (depends on A)
            var modC = new MockLifecycleModule("test:mod_c", new[] { "test:mod_b" }, executionLog);
            var modA = new MockLifecycleModule("test:mod_a", null, executionLog);
            var modB = new MockLifecycleModule("test:mod_b", new[] { "test:mod_a" }, executionLog);

            host.AddModule(modC);
            host.AddModule(modA);
            host.AddModule(modB);

            host.Boot(hydrateProjectAssets: false);
            Assert.AreEqual(ModuleHostState.Active, host.State);

            var expectedBootOrder = new[]
            {
                "test:mod_a.Register",
                "test:mod_b.Register",
                "test:mod_c.Register",
                "test:mod_a.Init",
                "test:mod_b.Init",
                "test:mod_c.Init",
                "test:mod_a.Activate",
                "test:mod_b.Activate",
                "test:mod_c.Activate"
            };
            CollectionAssert.AreEqual(expectedBootOrder, executionLog);

            executionLog.Clear();
            host.Shutdown();
            Assert.AreEqual(ModuleHostState.Stopped, host.State);

            var expectedShutdownOrder = new[]
            {
                "test:mod_c.Shutdown",
                "test:mod_b.Shutdown",
                "test:mod_a.Shutdown"
            };
            CollectionAssert.AreEqual(expectedShutdownOrder, executionLog);
        }

        [Test]
        public void MOD03_ModuleHost_CircularDependency_ThrowsDiagnosticException()
        {
            var log = new List<string>();
            var host = new ModuleHost();

            host.AddModule(new MockLifecycleModule("test:cycle_x", new[] { "test:cycle_y" }, log));
            host.AddModule(new MockLifecycleModule("test:cycle_y", new[] { "test:cycle_x" }, log));

            var ex = Assert.Throws<ModuleDependencyCycleException>(() => host.Boot(hydrateProjectAssets: false));
            Assert.IsNotNull(ex);
            Assert.AreEqual(ModuleHostState.Faulted, host.State);
            Assert.IsEmpty(log, "No module lifecycle methods should execute if a dependency cycle exists.");
        }

        [Test]
        public void MOD04_ModuleHost_MissingRequiredDependency_FailsCleanly()
        {
            var log = new List<string>();
            var host = new ModuleHost();

            host.AddModule(new MockLifecycleModule("framework:quests_nav", new[] { "framework:factions" }, log));

            var ex = Assert.Throws<MissingModuleDependencyException>(() => host.Boot(hydrateProjectAssets: false));
            Assert.AreEqual("framework:quests_nav", ex.ModuleId);
            Assert.AreEqual("framework:factions", ex.MissingDependencyId);
            Assert.AreEqual(ModuleHostState.Faulted, host.State);
            Assert.IsEmpty(log);
        }

        [Test]
        public void MOD05_TwoPhaseServiceLocator_ResolvesCrossServiceInPhase2RegardlessOfRegistrationOrder()
        {
            // Register ConsumerService BEFORE ProviderService during Phase 1
            ServiceLocator.BeginRegistrationPhase();

            var consumer = new ConsumerTestService();
            var provider = new ProviderTestService();

            ServiceLocator.Register(consumer);
            ServiceLocator.Register(provider);

            // Neither should be initialized during Phase 1
            Assert.IsNull(consumer.ResolvedProvider);

            // Phase 2 initializes all services now that all registrations are present
            ServiceLocator.CompleteRegistrationAndInitialize();

            Assert.IsNotNull(consumer.ResolvedProvider, "ConsumerService must resolve ProviderService in Phase 2 even when registered first.");
            Assert.AreSame(provider, consumer.ResolvedProvider);

            // Also verify BlockPlacementService + GreedyMeshingService boot order fix
            ServiceLocator.Clear();
            ServiceLocator.BeginRegistrationPhase();

            var blockPlacement = new BlockPlacementService();
            var greedyMeshing = new GreedyMeshingService();

            ServiceLocator.Register(blockPlacement);
            ServiceLocator.Register(greedyMeshing);

            ServiceLocator.CompleteRegistrationAndInitialize();
            Assert.IsNotNull(greedyMeshing.Registry, "GreedyMeshingService must auto-resolve DefaultBlockRegistry on Initialize().");
        }

        [Test]
        public void MOD05_TickScheduler_ExecutesInTickGroupOrderAndRespectsThrottleInterval()
        {
            var orderLog = new List<string>();
            var scheduler = new TickScheduler();
            scheduler.Initialize();

            // Register in reverse group order
            scheduler.Register(new MockTickable("UI", TickGroup.UI, 0f, orderLog));
            scheduler.Register(new MockTickable("AI_10Hz", TickGroup.AI, 0.1f, orderLog));
            scheduler.Register(new MockTickable("Input", TickGroup.Input, 0f, orderLog));

            // Frame 1: deltaTime = 0.05s (AI_10Hz should not fire yet because 0.05 < 0.10)
            scheduler.TickAll(0.05f);
            CollectionAssert.AreEqual(new[] { "Input", "UI" }, orderLog);

            // Frame 2: deltaTime = 0.06s (Accumulated = 0.11s >= 0.10s -> AI_10Hz fires between Input and UI!)
            orderLog.Clear();
            scheduler.TickAll(0.06f);
            CollectionAssert.AreEqual(new[] { "Input", "AI_10Hz", "UI" }, orderLog);

            scheduler.Shutdown();
        }

        #region Test Helpers

        private sealed class MockLifecycleModule : EngineModuleBase
        {
            private readonly List<string> _log;

            public override ModuleDescriptor Descriptor { get; }

            public MockLifecycleModule(string id, string[] dependencies, List<string> log)
            {
                Descriptor = new ModuleDescriptor(id, id, "1.0.0", dependencies);
                _log = log;
            }

            public override void RegisterServices(IModuleContext context) => _log.Add($"{Descriptor.ModuleId}.Register");
            public override void Initialize(IModuleContext context) => _log.Add($"{Descriptor.ModuleId}.Init");
            public override void OnActivate(IModuleContext context) => _log.Add($"{Descriptor.ModuleId}.Activate");
            public override void OnShutdown(IModuleContext context) => _log.Add($"{Descriptor.ModuleId}.Shutdown");
        }

        private sealed class ProviderTestService : IGameService
        {
            public void Initialize() { }
            public void Shutdown() { }
        }

        private sealed class ConsumerTestService : IGameService
        {
            public ProviderTestService ResolvedProvider { get; private set; }

            public void Initialize()
            {
                ServiceLocator.TryGet(out ProviderTestService provider);
                ResolvedProvider = provider;
            }

            public void Shutdown()
            {
                ResolvedProvider = null;
            }
        }

        private sealed class MockTickable : IScheduledTickable
        {
            private readonly string _name;
            private readonly List<string> _log;

            public TickGroup Group { get; }
            public float TickIntervalSeconds { get; }

            public MockTickable(string name, TickGroup group, float interval, List<string> log)
            {
                _name = name;
                Group = group;
                TickIntervalSeconds = interval;
                _log = log;
            }

            public void Tick(float deltaTime) => _log.Add(_name);
        }

        #endregion
    }
}
