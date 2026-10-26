#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveSlotPublicationBoundaryTests
    {
        private FeatureSettingsScope settings;
        private string root;
        private SaveManager previous;
        private readonly List<GameObject> objects = new();
        private static PropertyInfo Singleton => typeof(SaveManager).GetProperty(nameof(SaveManager.Instance));

        [SetUp]
        public void SetUp()
        {
            settings = new FeatureSettingsScope();
            root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            previous = SaveManager.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            Singleton.SetValue(null, previous);
            foreach (var instance in objects) if (instance != null) Object.DestroyImmediate(instance);
            objects.Clear();
            settings.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static SaveSlot Slot(string name = "Published") => new(3, name, DateTime.UtcNow, "", "A");

        [TestCase("PublishSlot")]
        [TestCase("AppendHistorySlot")]
        [TestCase("SynchronizeSlot")]
        [TestCase("ReplaceSlots")]
        public async Task PublicationRetiresAnOlderExistenceProbe(string publication)
        {
            settings.Settings.enableCloudSave = true;
            var storage = new HardeningStorage(new SaveSystem(settings.Settings, root)) { GateReads = true };
            var slots = new SaveSlotManager(storage, settings.Settings, null, root);
            var old = Slot("Old");
            slots.Slots.Add(old);
            var query = slots.HasSaveAtAsync(3);
            try
            {
                Assert.That(query.IsCompleted, Is.False);
                var replacement = Slot("");
                replacement.LastActiveScene = "";
                replacement.CustomMetadata["request"] = "new";
                if (publication == "ReplaceSlots") Call(slots, publication, (object)new[] { replacement });
                else if (publication == "SynchronizeSlot") Call(slots, publication, replacement, false);
                else Call(slots, publication, replacement);
                storage.GateReads = false;
                Assert.That(await slots.HasSaveAtAsync(3), Is.False);
                storage.Read.TrySetResult(new byte[] { 1 });
                Assert.That(await query, Is.False);
                Assert.That(await slots.HasSaveAtAsync(3), Is.False,
                    "A retired positive probe cannot overwrite the new generation's absence.");
                var published = slots.GetByNumber(3);
                Assert.That(published.SlotName, Is.Empty);
                Assert.That(published.LastActiveScene, Is.Empty);
                Assert.That(published.CustomMetadata["request"], Is.EqualTo("new"));
                if (publication == "SynchronizeSlot")
                    Assert.That(published, Is.SameAs(old), "Metadata synchronization preserves published object identity.");
                else Assert.That(published, Is.SameAs(replacement));
            }
            finally { storage.Release(); await query; }
        }

        [Test]
        public void ReplacementAcceptsItsLiveViewAndRejectedEnumerationPreservesPublication()
        {
            var slots = new SaveSlotManager(new SaveSystem(settings.Settings, root), settings.Settings, null, root);
            var slot = Slot();
            Call(slots, "PublishSlot", slot);
            var view = slots.ManagedSlots;
            Assert.Throws<NotSupportedException>(() => ((ICollection<SaveSlot>)view).Clear());
            Call(slots, "ReplaceSlots", view);
            Assert.That(view.Single(), Is.SameAs(slot));
            Assert.Throws<TargetInvocationException>(() => Call(slots, "ReplaceSlots", BrokenCandidates()));
            Assert.That(view.Single(), Is.SameAs(slot));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task DeletionUsesItsBoundHostWhileLegacyConstructionKeepsSingletonFallback(bool bound)
        {
            var first = CreateManager("First host");
            var second = CreateManager("Second host");
            SaveSlot deleted = null;
            // This is a publication/owner test. Actual deletion, including the
            // dispatcher-driven preference cleanup, is covered in Play Mode.
            var backend = new HardeningStorage(new SaveSystem(settings.Settings, root))
            {
                DeleteOverride = slot => { deleted = slot; return Task.CompletedTask; }
            };
            var slots = bound
                ? (SaveSlotManager)Activator.CreateInstance(typeof(SaveSlotManager),
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { backend, settings.Settings, null, root, first }, null)
                : new SaveSlotManager(backend, settings.Settings, null, root);
            slots.Slots.Add(Slot("First"));
            var other = new SaveSlotManager(backend, settings.Settings, null, root);
            other.Slots.Add(Slot("Second"));
            var publication = typeof(SaveManager).GetProperty("SlotManager", BindingFlags.Instance | BindingFlags.NonPublic);
            publication.SetValue(first, slots);
            publication.SetValue(second, other);
            Singleton.SetValue(null, second);

            Assert.That(await slots.DeleteAsync(3), Is.True);

            Assert.That(deleted, Is.SameAs(slots.GetByNumber(3)));
            Assert.That(slots.GetByNumber(3).LastSaved, Is.EqualTo(DateTime.MinValue));
            Assert.That(other.GetByNumber(3).SlotName, Is.EqualTo(bound ? "Second" : "Slot 3"));
            Assert.That(other.GetByNumber(3).LastSaved == DateTime.MinValue, Is.EqualTo(!bound));
        }

        private SaveManager CreateManager(string name)
        {
            var instance = new GameObject(name);
            instance.SetActive(false);
            objects.Add(instance);
            var manager = instance.AddComponent<SaveManager>();
            Set(manager, "saveSettings", settings.Settings);
            return manager;
        }

        private static IEnumerable<SaveSlot> BrokenCandidates()
        {
            yield return Slot("Unpublished");
            throw new InvalidOperationException("Candidate enumeration failed");
        }
    }
}
#endif
