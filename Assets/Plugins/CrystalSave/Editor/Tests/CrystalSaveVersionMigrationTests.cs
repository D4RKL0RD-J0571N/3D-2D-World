#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveVersionMigrationTests
    {
        MigrationManager migrations;
        readonly List<MigrationProbeAction> actions = new();

        [SetUp] public void SetUp() => migrations = ScriptableObject.CreateInstance<MigrationManager>();
        [TearDown] public void TearDown()
        {
            foreach (var action in actions) Object.DestroyImmediate(action);
            actions.Clear();
            Object.DestroyImmediate(migrations);
        }

        MigrationStep Step(int version, Action<SaveData> callback)
        {
            var action = ScriptableObject.CreateInstance<MigrationProbeAction>();
            action.Apply = callback;
            actions.Add(action);
            return new MigrationStep { targetVersion = new VersionData(version, 0, 0), migrationActions = new List<MigrationAction> { action } };
        }

        [Test]
        public void DuplicateVersionStepsRunInSerializedOrderBeforeAdvancingVersion()
        {
            var order = new List<string>();
            var observedVersions = new List<int>();
            var later = Step(3, _ => order.Add("3"));
            var first = Step(2, data => { order.Add("2a"); observedVersions.Add(data.Version.Major); });
            var second = Step(2, data => { order.Add("2b"); observedVersions.Add(data.Version.Major); });
            Set(migrations, "migrationSteps", new List<MigrationStep> { later, first, second });
            var save = new SaveData(new VersionData(1, 0, 0));
            migrations.Migrate(save);
            Assert.That(order, Is.EqualTo(new[] { "2a", "2b", "3" }));
            Assert.That(observedVersions, Is.EqualTo(new[] { 1, 1 }));
            Assert.That(save.Version, Is.EqualTo(new VersionData(3, 0, 0)));
        }

        [Test]
        public void ExistingVersionDoesNotRepeatItsMigrationGroup()
        {
            int olderCalls = 0, newCalls = 0;
            Set(migrations, "migrationSteps", new List<MigrationStep>
            { Step(2, _ => olderCalls++), Step(2, _ => olderCalls++), Step(3, _ => newCalls++) });
            migrations.Migrate(new SaveData(new VersionData(2, 0, 0)));
            Assert.That(olderCalls, Is.Zero);
            Assert.That(newCalls, Is.EqualTo(1));
        }

        [Test]
        public void FailedDuplicateVersionGroupDoesNotClaimCompletionOrRunLaterSteps()
        {
            bool laterRan = false;
            Set(migrations, "migrationSteps", new List<MigrationStep>
            {
                Step(2, data => data.ComponentsData["partial"] = new byte[] { 1 }),
                Step(2, _ => throw new InvalidOperationException("migration fixture failure")),
                Step(3, _ => laterRan = true)
            });
            var save = new SaveData(new VersionData(1, 0, 0));
            var failure = Assert.Throws<InvalidOperationException>(() => migrations.Migrate(save));
            Assert.That(failure.InnerException.Message, Is.EqualTo("migration fixture failure"));
            Assert.That(save.Version, Is.EqualTo(new VersionData(1, 0, 0)));
            Assert.That(laterRan, Is.False);
        }

        [Test]
        public void CallbacksCannotSkipPlannedStepsByEditingVersionsOrActionLists()
        {
            int remainingCalls = 0;
            var later = Step(3, _ => remainingCalls++);
            var sameVersion = Step(2, _ => remainingCalls++);
            var first = Step(2, data =>
            {
                data.Version = new VersionData(99, 0, 0);
                later.targetVersion.Major = 100;
                sameVersion.migrationActions.Clear();
            });
            Set(migrations, "migrationSteps", new List<MigrationStep> { first, sameVersion, later });
            var save = new SaveData(new VersionData(1, 0, 0));
            migrations.Migrate(save);
            Assert.That(remainingCalls, Is.EqualTo(2));
            Assert.That(save.Version, Is.EqualTo(new VersionData(3, 0, 0)));
        }

        [Test]
        public void AutomaticMigrationBoundExcludesFutureSteps()
        {
            var visited = new List<int>();
            Set(migrations, "migrationSteps", new List<MigrationStep>
            { Step(3, _ => visited.Add(3)), Step(2, _ => visited.Add(2)), Step(2, _ => visited.Add(2)) });
            var save = new SaveData(new VersionData(1, 0, 0));
            Call(migrations, "MigrateThrough", save, new VersionData(2, 0, 0));
            Assert.That(visited, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(save.Version, Is.EqualTo(new VersionData(2, 0, 0)));
        }
    }

    public sealed class MigrationProbeAction : MigrationAction
    {
        [NonSerialized] public Action<SaveData> Apply;
        public override void ApplyMigration(SaveData data) => Apply?.Invoke(data);
    }

    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator VersionMigration_ServiceStopsAtRunningVersion()
        {
            var manager = SaveManager.Instance;
            var migrations = manager.MigrationManager;
            var previous = Get<List<MigrationStep>>(migrations, "migrationSteps");
            var current = manager.VersionManager.CurrentVersion;
            var future = new VersionData(current.Major + 1, 0, 0);
            var action = ScriptableObject.CreateInstance<MigrationProbeAction>();
            int calls = 0;
            action.Apply = _ => calls++;
            try
            {
                Set(migrations, "migrationSteps", new List<MigrationStep>
                {
                    new MigrationStep { targetVersion = future, migrationActions = new List<MigrationAction> { action } },
                    new MigrationStep { targetVersion = (VersionData)current.Clone(), migrationActions = new List<MigrationAction> { action } },
                    new MigrationStep { targetVersion = (VersionData)current.Clone(), migrationActions = new List<MigrationAction> { action } }
                });
                var data = new SaveData(new VersionData(current.Major - 1, current.Minor, current.Patch));
                manager.MigrateSaveData(data);
                Assert.That(calls, Is.EqualTo(2));
                Assert.That(data.Version, Is.EqualTo(current));
                yield return null;
            }
            finally { Set(migrations, "migrationSteps", previous); Object.Destroy(action); }
        }

        [UnityTest]
        public IEnumerator VersionMigration_FailedLoadAndImportKeepPublishedStateAndFiles()
        {
            var manager = SaveManager.Instance;
            var migrations = manager.MigrationManager;
            var previous = Get<List<MigrationStep>>(migrations, "migrationSteps");
            var action = ScriptableObject.CreateInstance<MigrationProbeAction>();
            bool ignored = LogAssert.ignoreFailingMessages;
            try
            {
                yield return Await(manager.SaveImmediateAsync(1));
                var published = manager.CurrentSaveData;
                var generation = typeof(ComponentManager).GetProperty("RestoreGeneration", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(manager.ComponentManager);
                string scene = SceneManager.GetActiveScene().name;
                byte[] publishedBytes = manager.SaveSystem.Load(manager.GetSlotMetadata(1));
                var current = manager.VersionManager.CurrentVersion;
                var old = new SaveData(new VersionData(current.Major - 1, current.Minor, current.Patch));
                old.ComponentsData["migration-fixture_component"] = new byte[] { 17 };
                byte[] incoming = manager.Serializer.Serialize(old);
                manager.SaveSystem.Save(incoming, manager.GetSlotMetadata(2));
                action.Apply = candidate =>
                {
                    candidate.ComponentsData.Clear();
                    candidate.LastActiveScene = "A";
                    throw new InvalidOperationException("detached migration failure");
                };
                Set(migrations, "migrationSteps", new List<MigrationStep>
                {
                    new MigrationStep { targetVersion = (VersionData)current.Clone(), migrationActions = new List<MigrationAction> { action } }
                });
                LogAssert.ignoreFailingMessages = true;
                var load = manager.LoadSaveSlotAsync(2, restoreLastActiveScene: true);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.False);
                var import = manager.ImportSaveAsync(incoming, 1);
                yield return Await(import);
                Assert.That(import.Result, Is.False);
                Assert.That(manager.CurrentSaveData, Is.SameAs(published));
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(scene));
                Assert.That(typeof(ComponentManager).GetProperty("RestoreGeneration", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(manager.ComponentManager), Is.SameAs(generation));
                Assert.That(manager.SaveSystem.Load(manager.GetSlotMetadata(1)), Is.EqualTo(publishedBytes));
                Assert.That(manager.SaveSystem.Load(manager.GetSlotMetadata(2)), Is.EqualTo(incoming));
                Assert.That(manager.Serializer.Deserialize<SaveData>(incoming).ComponentsData["migration-fixture_component"], Is.EqualTo(new byte[] { 17 }));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignored;
                Set(migrations, "migrationSteps", previous);
                Object.Destroy(action);
            }
        }
    }
}
#endif
