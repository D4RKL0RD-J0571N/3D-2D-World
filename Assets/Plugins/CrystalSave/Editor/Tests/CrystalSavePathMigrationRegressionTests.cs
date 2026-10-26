#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSavePathMigrationRegressionTests
    {
        string root;
        FieldInfo fault;
        object previousFault;
        FieldInfo flush;
        object previousFlush;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "crystal-save-path-migration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            fault = typeof(PersistentPathMigration).GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
            previousFault = fault.GetValue(null);
            flush = typeof(PersistentPathMigration).GetField("FlushOverride", BindingFlags.Static | BindingFlags.NonPublic);
            previousFlush = flush.GetValue(null);
            flush.SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            fault.SetValue(null, previousFault);
            flush.SetValue(null, previousFlush);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [TestCase("child")]
        [TestCase("sibling")]
        [TestCase("parent")]
        public void MigrationPreservesFilesInOverlappingAndSeparateRoots(string relation)
        {
            string source = Path.Combine(root, "original");
            string target = relation == "child" ? Path.Combine(source, "CrystalSave")
                : relation == "parent" ? root : Path.Combine(root, "new");
            Write(Path.Combine(source, "slot", "save.sav"), "payload");
            Write(Path.Combine(source, "slot", "header.json"), "metadata");
            Directory.CreateDirectory(Path.Combine(source, "empty"));
            Directory.CreateDirectory(target);
            PersistentPathMigration.TryMigrate(source, target);
            Assert.That(File.ReadAllText(Path.Combine(target, "slot", "save.sav")), Is.EqualTo("payload"));
            Assert.That(File.ReadAllText(Path.Combine(target, "slot", "header.json")), Is.EqualTo("metadata"));
            Assert.That(Directory.Exists(Path.Combine(target, "empty")), Is.True);
            Assert.That(File.Exists(Path.Combine(target, ".migrated")), Is.True);
            Assert.That(File.Exists(Path.Combine(source, "slot", "save.sav")), Is.False);
        }

        [Test]
        public void ExistingDestinationAndUnrelatedTemporaryDirectoryArePreserved()
        {
            string source = Path.Combine(root, "old"), target = Path.Combine(root, "new");
            Write(Path.Combine(source, "slot1.sav"), "first");
            Write(Path.Combine(target, "slot2.sav"), "second");
            Write(Path.Combine(target + "_tmp", "unrelated.txt"), "keep");
            PersistentPathMigration.TryMigrate(source, target);
            Assert.That(File.ReadAllText(Path.Combine(target, "slot1.sav")), Is.EqualTo("first"));
            Assert.That(File.ReadAllText(Path.Combine(target, "slot2.sav")), Is.EqualTo("second"));
            Assert.That(File.ReadAllText(Path.Combine(target + "_tmp", "unrelated.txt")), Is.EqualTo("keep"));
        }

        [Test]
        public void ConflictingDestinationRejectsBeforeMovingAnySave()
        {
            string source = Path.Combine(root, "old"), target = Path.Combine(root, "new");
            Write(Path.Combine(source, "slot1.sav"), "old");
            Write(Path.Combine(source, "slot2.sav"), "second");
            Write(Path.Combine(target, "slot1.sav"), "destination");
            PersistentPathMigration.TryMigrate(source, target);
            Assert.That(File.ReadAllText(Path.Combine(source, "slot1.sav")), Is.EqualTo("old"));
            Assert.That(File.ReadAllText(Path.Combine(source, "slot2.sav")), Is.EqualTo("second"));
            Assert.That(File.ReadAllText(Path.Combine(target, "slot1.sav")), Is.EqualTo("destination"));
            Assert.That(File.Exists(Path.Combine(target, "slot2.sav")), Is.False);
            Assert.That(File.Exists(Path.Combine(target, ".migrated")), Is.False);
        }

        [TestCase("copied")]
        [TestCase("published")]
        public void InterruptedMigrationPreservesCompleteCopyAndRetryIsSafe(string stage)
        {
            string source = Path.Combine(root, "old"), target = Path.Combine(source, "custom");
            Write(Path.Combine(source, "slot1.sav"), "one");
            Write(Path.Combine(source, "slot2.sav"), "two");
            fault.SetValue(null, (Action<string>)(current => { if (current == stage) throw new IOException("Injected migration interruption"); }));
            PersistentPathMigration.TryMigrate(source, target);
            Assert.That(File.ReadAllText(Path.Combine(source, "slot1.sav")), Is.EqualTo("one"));
            Assert.That(File.ReadAllText(Path.Combine(source, "slot2.sav")), Is.EqualTo("two"));
            fault.SetValue(null, null);
            PersistentPathMigration.TryMigrate(source, target);
            Assert.That(File.ReadAllText(Path.Combine(target, "slot1.sav")), Is.EqualTo("one"));
            Assert.That(File.ReadAllText(Path.Combine(target, "slot2.sav")), Is.EqualTo("two"));
            Assert.That(File.Exists(Path.Combine(target, ".migrated")), Is.True);
            Assert.That(Directory.GetFiles(root, "*.migrating-*", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void EquivalentNormalizedPathsLeaveExistingSaveUntouched()
        {
            string source = Path.Combine(root, "old");
            Write(Path.Combine(source, "save.sav"), "payload");
            PersistentPathMigration.TryMigrate(source + Path.DirectorySeparatorChar, Path.Combine(source, "."));
            Assert.That(File.ReadAllText(Path.Combine(source, "save.sav")), Is.EqualTo("payload"));
            Assert.That(File.Exists(Path.Combine(source, ".migrated")), Is.False);
        }

        [Test]
        public async Task RejectedCopyAcknowledgementRetainsOriginalsAndDoesNotPublishMarker()
        {
            string source = Path.Combine(root, "old"), target = Path.Combine(source, "custom");
            Write(Path.Combine(source, "save.sav"), "payload");
            flush.SetValue(null, (Func<Task>)(() => Task.FromException(new IOException("Rejected migration flush"))));
            Assert.That(await PersistentPathMigration.TryMigrateAsync(source, target), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(source, "save.sav")), Is.EqualTo("payload"));
            Assert.That(File.Exists(Path.Combine(target, ".migrated")), Is.False);
            flush.SetValue(null, null);
            Assert.That(await PersistentPathMigration.TryMigrateAsync(source, target), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(target, "save.sav")), Is.EqualTo("payload"));
        }

        [Test]
        public async Task QueuedMigrationCannotDeleteOriginalsBeforeCopyAcknowledgement()
        {
            string source = Path.Combine(root, "old"), target = Path.Combine(source, "custom");
            Write(Path.Combine(source, "save.sav"), "payload");
            var acknowledgement = new TaskCompletionSource<bool>();
            int flushes = 0;
            flush.SetValue(null, (Func<Task>)(() => ++flushes == 1 ? acknowledgement.Task : Task.CompletedTask));
            var first = PersistentPathMigration.TryMigrateAsync(source, target);
            var second = PersistentPathMigration.TryMigrateAsync(source, target);
            Assert.That(first.IsCompleted, Is.False);
            Assert.That(second.IsCompleted, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(source, "save.sav")), Is.EqualTo("payload"));
            Assert.That(File.Exists(Path.Combine(target, ".migrated")), Is.False);
            acknowledgement.SetResult(true);
            Assert.That(await first, Is.True);
            Assert.That(await second, Is.True);
            Assert.That(File.ReadAllText(Path.Combine(target, "save.sav")), Is.EqualTo("payload"));
            Assert.That(File.Exists(Path.Combine(source, "save.sav")), Is.False);
        }

#if UNITY_EDITOR_LINUX
        [DllImport("libc", EntryPoint = "symlink", SetLastError = true)]
        static extern int CreateSymbolicLink(string target, string link);

        [Test]
        public void SymlinkAncestorCannotAliasAndDeleteTheOriginalGeneration()
        {
            string physical = Path.Combine(root, "physical"), alias = Path.Combine(root, "alias");
            string source = Path.Combine(physical, "saves");
            Write(Path.Combine(source, "save.sav"), "payload");
            Assert.That(CreateSymbolicLink(physical, alias), Is.Zero);
            try
            {
                PersistentPathMigration.TryMigrate(source, Path.Combine(alias, "saves"));
                Assert.That(File.ReadAllText(Path.Combine(source, "save.sav")), Is.EqualTo("payload"));
                Assert.That(File.Exists(Path.Combine(source, ".migrated")), Is.False);
                PersistentPathMigration.TryMigrate(Path.Combine(alias, "saves"), Path.Combine(root, "new"));
                Assert.That(File.ReadAllText(Path.Combine(source, "save.sav")), Is.EqualTo("payload"));
                Assert.That(Directory.Exists(Path.Combine(root, "new")), Is.False);
            }
            finally { Directory.Delete(alias); }
        }
#endif

        static void Write(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }
    }
}
#endif
