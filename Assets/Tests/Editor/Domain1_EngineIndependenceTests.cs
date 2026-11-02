namespace Tests.Editor
{
    using System;
    using System.IO;
    using Core;
    using Core.Content;
    using Core.Diagnostics;
    using Core.Modules;
    using NUnit.Framework;
    using Systems.Creator;
    using Systems.Pooling;
    using Systems.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Milestone 2 verification suite for Domain 1 (Engine Independence & Fault Isolation):
    /// Verifies that Engine Core boots and operates without any Fantasy Framework modules (<c>ENG-01</c>),
    /// that <c>Assets/Scripts/Core/</c> has zero imports of Skyrim/Gameplay namespaces (<c>ENG-02</c>),
    /// and that <see cref="GameEventManager"/> isolates throwing subscribers (<c>ENG-03</c>).
    /// </summary>
    [TestFixture]
    public class Domain1_EngineIndependenceTests
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
        public void ENG01_BootEngine_WithoutFantasyFramework_Succeeds()
        {
            var profile = EngineBootstrapProfile.CreateCoreOnlyProfile();
            var host = new ModuleHost();

            try
            {
                host.DiscoverAndAddModules(profile);
                host.Boot(hydrateProjectAssets: false);

                Assert.AreEqual(ModuleHostState.Active, host.State);

                // Core services must be registered and initialized
                Assert.IsTrue(ServiceLocator.TryGet<IContentService>(out _), "IContentService must be active in Core-Only mode.");
                Assert.IsTrue(ServiceLocator.TryGet<IDiagnosticService>(out _), "IDiagnosticService must be active in Core-Only mode.");
                Assert.IsTrue(ServiceLocator.TryGet<PoolingManager>(out _), "PoolingManager must be active in Core-Only mode.");
                Assert.IsTrue(ServiceLocator.TryGet<CreatorStudioService>(out _), "CreatorStudioService must be active in Core-Only mode.");

                // Fantasy Framework / Skyrim services must NOT be loaded in Core-Only mode
                Assert.IsFalse(ServiceLocator.TryGet<SkillProgressionService>(out _), "SkillProgressionService must NOT be loaded in Core-Only mode.");
                Assert.IsFalse(ServiceLocator.TryGet<FactionService>(out _), "FactionService must NOT be loaded in Core-Only mode.");
                Assert.IsFalse(ServiceLocator.TryGet<RadiantQuestService>(out _), "RadiantQuestService must NOT be loaded in Core-Only mode.");
                Assert.IsFalse(ServiceLocator.TryGet<StealthDetectionService>(out _), "StealthDetectionService must NOT be loaded in Core-Only mode.");

                Assert.AreEqual(0, host.Diagnostics.ErrorCount);
            }
            finally
            {
                host.Shutdown();
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ENG02_CoreNamespaceScripts_HaveZeroImportsOfSkyrimOrGameplay()
        {
            string coreDir = Path.Combine(Application.dataPath, "Scripts", "Core");
            Assert.IsTrue(Directory.Exists(coreDir), $"Core directory not found at {coreDir}");

            string[] csFiles = Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(csFiles);

            foreach (string file in csFiles)
            {
                string content = File.ReadAllText(file);
                string fileName = Path.GetFileName(file);

                Assert.IsFalse(content.Contains("using Systems.Skyrim"), $"{fileName} in Core must not import Systems.Skyrim.");
                Assert.IsFalse(content.Contains("using Domain.Skyrim"), $"{fileName} in Core must not import Domain.Skyrim.");
                Assert.IsFalse(content.Contains("using Gameplay."), $"{fileName} in Core must not import Gameplay namespaces.");
            }
        }

        [Test]
        public void ENG03_GameEventManager_IsolatesThrowingSubscriber()
        {
            var diagnostics = new DiagnosticService();
            ServiceLocator.Register<IDiagnosticService, DiagnosticService>(diagnostics);

            int healthySubscriberCalls = 0;

            GameEventManager.AddListener<TestIsolationEvent>(_ =>
            {
                throw new InvalidOperationException("Simulated broken mod subscriber exception");
            });

            GameEventManager.AddListener<TestIsolationEvent>(evt =>
            {
                healthySubscriberCalls += evt.PayloadValue;
            });

            // Publish with logExceptionsToUnityConsole: false so NUnit / MCP runner doesn't fail on Debug.LogError
            GameEventManager.Publish(new TestIsolationEvent { PayloadValue = 42 }, logExceptionsToUnityConsole: false);

            Assert.AreEqual(42, healthySubscriberCalls, "Healthy subscriber must still execute even after an earlier subscriber throws.");
            Assert.AreEqual(1, diagnostics.ErrorCount, "DiagnosticService must record the isolated subscriber exception.");
        }

        private sealed class TestIsolationEvent : GameEvent
        {
            public int PayloadValue;
        }
    }
}
