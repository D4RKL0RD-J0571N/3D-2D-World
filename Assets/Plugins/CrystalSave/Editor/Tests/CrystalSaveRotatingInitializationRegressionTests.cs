#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator LifecycleOwnership_QuickInitializationCannotDropHistory() => RotatingInitializationScenario(false, true);
        [UnityTest] public IEnumerator LifecycleOwnership_AutoInitializationCannotDropHistory() => RotatingInitializationScenario(true, true);
        [UnityTest] public IEnumerator LifecycleOwnership_QuickHistory_Control() => RotatingInitializationScenario(false, false);
        [UnityTest] public IEnumerator LifecycleOwnership_AutoHistory_Control() => RotatingInitializationScenario(true, false);
        [UnityTest] public IEnumerator LifecycleOwnership_QuickInitializationFailurePreservesPublication() => RotatingInitializationFailureScenario(false);
        [UnityTest] public IEnumerator LifecycleOwnership_AutoInitializationFailurePreservesPublication() => RotatingInitializationFailureScenario(true);
        [UnityTest] public IEnumerator LifecycleOwnership_QuickInitializationListenersAreNotifications() => RotatingInitializationListenerScenario(false);
        [UnityTest] public IEnumerator LifecycleOwnership_AutoInitializationListenersAreNotifications() => RotatingInitializationListenerScenario(true);
        [UnityTest] public IEnumerator LifecycleOwnership_QuickInitializationsPublishInOrder() => RotatingInitializationOrderScenario(false);
        [UnityTest] public IEnumerator LifecycleOwnership_AutoInitializationsPublishInOrder() => RotatingInitializationOrderScenario(true);

        static SaveSlotManager PublishedRotatingManager(SaveManager manager, bool automatic)
            => (SaveSlotManager)typeof(SaveManager).GetProperty(automatic ? "AutoSlotManager" : "QuickSlotManager",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

        IEnumerator RotatingInitializationFailureScenario(bool automatic)
        {
            var manager = SaveManager.Instance;
            Task Initialize(int count) => automatic ? manager.InitializeAutoSaveSlotsAsync(count) : manager.InitializeQuickSaveSlotsAsync(count);
            yield return Await(Initialize(2));
            var oldManager = PublishedRotatingManager(manager, automatic);
            var oldSlots = oldManager.GetAll();
            var original = manager.SaveSystem;
            var backend = new RotatingInitializationStorage(original) { FailMetadata = true };
            int notifications = 0;
            Action updated = () => notifications++;
            if (automatic) manager.OnAutoSlotsUpdated += updated; else manager.OnQuickSlotsUpdated += updated;
            Set(manager, "saveSystem", backend);
            try
            {
                foreach (int count in new[] { 3, -1 })
                {
                    var request = Initialize(count);
                    yield return Until(() => request.IsCompleted, "rejected rotating initialization");
                    Assert.That(request.IsFaulted, Is.True);
                    _ = request.Exception;
                    Assert.That(PublishedRotatingManager(manager, automatic), Is.SameAs(oldManager));
                    Assert.That(PublishedRotatingManager(manager, automatic).Slots, Is.EqualTo(oldSlots));
                    Assert.That(automatic ? SaveManager.AreAutoSlotsReady : SaveManager.AreQuickSlotsReady, Is.True);
                    Assert.That(notifications, Is.Zero);
                }
                backend.FailMetadata = false;
                yield return Await(Initialize(3));
                Assert.That(PublishedRotatingManager(manager, automatic).Slots.Count, Is.EqualTo(3));
                Assert.That(notifications, Is.EqualTo(1));
                yield return Await(manager.SaveAsync(1));
            }
            finally
            {
                if (automatic) manager.OnAutoSlotsUpdated -= updated; else manager.OnQuickSlotsUpdated -= updated;
                Set(manager, "saveSystem", original);
            }
        }

        IEnumerator RotatingInitializationListenerScenario(bool automatic)
        {
            var manager = SaveManager.Instance;
            yield return Await(automatic ? manager.InitializeAutoSaveSlotsAsync(2) : manager.InitializeQuickSaveSlotsAsync(2));
            int updatedCount = 0, initializedCount = 0;
            Task following = null;
            Action badUpdate = () => throw new InvalidOperationException("ROTATING_UPDATE_LISTENER");
            Action badInitialized = () => throw new InvalidOperationException("ROTATING_INITIALIZED_LISTENER");
            Action update = () =>
            {
                updatedCount++;
                Assert.That(automatic ? SaveManager.AreAutoSlotsReady : SaveManager.AreQuickSlotsReady, Is.True);
                Assert.That(PublishedRotatingManager(manager, automatic).Slots.Count, Is.EqualTo(2));
            };
            Action initialized = () => { initializedCount++; following = manager.SaveAsync(1); };
            if (automatic)
            {
                manager.OnAutoSlotsUpdated += badUpdate; manager.OnAutoSlotsUpdated += update;
                SaveManager.AutoSlotsInitialized += badInitialized; SaveManager.AutoSlotsInitialized += initialized;
            }
            else
            {
                manager.OnQuickSlotsUpdated += badUpdate; manager.OnQuickSlotsUpdated += update;
                SaveManager.QuickSlotsInitialized += badInitialized; SaveManager.QuickSlotsInitialized += initialized;
            }
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("ROTATING_UPDATE_LISTENER"));
                LogAssert.Expect(LogType.Exception, new Regex("ROTATING_INITIALIZED_LISTENER"));
                yield return Await(automatic ? manager.InitializeAutoSaveSlotsAsync(2) : manager.InitializeQuickSaveSlotsAsync(2));
                Assert.That(updatedCount, Is.EqualTo(1));
                Assert.That(initializedCount, Is.EqualTo(1));
                Assert.That(following, Is.Not.Null);
                yield return Await(following);
            }
            finally
            {
                if (automatic)
                {
                    manager.OnAutoSlotsUpdated -= badUpdate; manager.OnAutoSlotsUpdated -= update;
                    SaveManager.AutoSlotsInitialized -= badInitialized; SaveManager.AutoSlotsInitialized -= initialized;
                }
                else
                {
                    manager.OnQuickSlotsUpdated -= badUpdate; manager.OnQuickSlotsUpdated -= update;
                    SaveManager.QuickSlotsInitialized -= badInitialized; SaveManager.QuickSlotsInitialized -= initialized;
                }
            }
        }

        IEnumerator RotatingInitializationOrderScenario(bool automatic)
        {
            var manager = SaveManager.Instance;
            Task Initialize(int count) => automatic ? manager.InitializeAutoSaveSlotsAsync(count) : manager.InitializeQuickSaveSlotsAsync(count);
            yield return Await(Initialize(2));
            var before = PublishedRotatingManager(manager, automatic);
            var original = manager.SaveSystem;
            var backend = new RotatingInitializationStorage(original) { GateMetadata = true };
            var counts = new List<int>();
            Action update = () => counts.Add(PublishedRotatingManager(manager, automatic).Slots.Count);
            if (automatic) manager.OnAutoSlotsUpdated += update; else manager.OnQuickSlotsUpdated += update;
            Set(manager, "saveSystem", backend);
            try
            {
                var first = Initialize(3);
                yield return Until(() => backend.MetadataReads != 0, "detached rotating candidate");
                var second = Initialize(4);
                yield return null;
                Assert.That(PublishedRotatingManager(manager, automatic), Is.SameAs(before));
                Assert.That(automatic ? SaveManager.AreAutoSlotsReady : SaveManager.AreQuickSlotsReady, Is.True);
                Assert.That(second.IsCompleted, Is.False);
                Assert.That(counts, Is.Empty);
                backend.MetadataGate.TrySetResult(true);
                yield return Await(first); yield return Await(second);
                Assert.That(counts, Is.EqualTo(new[] { 3, 4 }));
                Assert.That(PublishedRotatingManager(manager, automatic).Slots.Count, Is.EqualTo(4));
                var published = Get<List<SaveSlot>>(manager, automatic ? "autoSaveSlots" : "quickSaveSlots");
                Assert.That(published.Count, Is.EqualTo(4));
                Assert.That(published.Select(slot => slot.SlotNumber).Distinct().Count(), Is.EqualTo(4));
            }
            finally
            {
                backend.MetadataGate.TrySetResult(true);
                Set(manager, "saveSystem", original);
                if (automatic) manager.OnAutoSlotsUpdated -= update; else manager.OnQuickSlotsUpdated -= update;
            }
        }

        IEnumerator RotatingInitializationScenario(bool automatic, bool overlap)
        {
            var manager = SaveManager.Instance;
            var original = manager.SaveSystem;
            var backend = new RotatingInitializationStorage(original);
            Set(manager, "saveSystem", backend);
            var probe = new RotatingInitializationComponent();
            manager.ComponentManager.RegisterSaveableComponent(probe);
            Task initialization = null;
            Task Save() => automatic ? manager.AutoSaveAsync() : manager.QuickSaveAsync();
            Task Initialize() => automatic ? manager.InitializeAutoSaveSlotsAsync(2) : manager.InitializeQuickSaveSlotsAsync(2);
            try
            {
                yield return Await(Initialize());
                probe.Value = 11;
                yield return Await(Save());
                probe.Value = 22;
                yield return Await(Save());
                probe.Value = 33;
                if (overlap)
                {
                    probe.OnCapture = () =>
                    {
                        probe.OnCapture = null;
                        backend.GateMetadata = true;
                        // A user capture callback starts the public initialization API while
                        // the rotating save still owns capture and has not prepared history.
                        initialization = Initialize();
                    };
                }
                yield return Await(Save());
                backend.MetadataGate.TrySetResult(true);
                if (initialization != null) yield return Await(initialization);
                int historyNumber = (automatic ? manager.SaveSettings.autoSaveSlotOffset : manager.SaveSettings.quickSaveSlotOffset) + 2;
                var restore = manager.LoadSaveSlotAsync(historyNumber);
                yield return Await(restore);
                Assert.That(restore.Result.Success, Is.True, restore.Result.ErrorMessage);
                TestContext.WriteLine($"{(automatic ? "Auto" : "Quick")} overlap={overlap}: history restored value={probe.Value}; expected previous save=22");
                Assert.That(probe.Value, Is.EqualTo(22), "Reinitializing slot metadata during a save must not discard the most recent history generation.");
            }
            finally
            {
                backend.MetadataGate.TrySetResult(true);
                probe.OnCapture = null;
                manager.ComponentManager.UnregisterSaveableComponent(probe);
                Set(manager, "saveSystem", original);
            }
        }
    }

    sealed class RotatingInitializationComponent : ISaveable
    {
        internal int Value;
        internal Action OnCapture;
        public string UniqueIdentifier => "review-init-object_review-init-component";
        public byte[] SaveData() { OnCapture?.Invoke(); return BitConverter.GetBytes(Value); }
        public void LoadData(byte[] bytes) { Value = BitConverter.ToInt32(bytes, 0); }
    }

    sealed class RotatingInitializationStorage : ISaveSystem
    {
        readonly ISaveSystem inner;
        internal bool GateMetadata;
        internal bool FailMetadata;
        internal int MetadataReads;
        internal readonly TaskCompletionSource<bool> MetadataGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal RotatingInitializationStorage(ISaveSystem inner) { this.inner = inner; }
        public async Task<SaveSlot> LoadSlotMetadataAsync(int number)
        {
            MetadataReads++;
            if (GateMetadata) await MetadataGate.Task;
            if (FailMetadata) throw new IOException("Injected rotating metadata failure");
            return await inner.LoadSlotMetadataAsync(number);
        }
        public Task SaveAsync(byte[] data, SaveSlot slot) => inner.SaveAsync(data, slot);
        public Task<byte[]> LoadAsync(SaveSlot slot) => inner.LoadAsync(slot);
        public Task DeleteAsync(SaveSlot slot) => inner.DeleteAsync(slot);
        public Task SaveSlotMetadataAsync(SaveSlot slot) => inner.SaveSlotMetadataAsync(slot);
        public void Save(byte[] data, SaveSlot slot) => inner.Save(data, slot);
        public byte[] Load(SaveSlot slot) => inner.Load(slot);
        public void Delete(SaveSlot slot) => inner.Delete(slot);
        public string GetSaveGamesPath() => inner.GetSaveGamesPath();
        public void BackupSlot(SaveSlot slot) => inner.BackupSlot(slot);
        public void RestoreBackup(SaveSlot slot) => inner.RestoreBackup(slot);
        public void DeleteBackup(SaveSlot slot) => inner.DeleteBackup(slot);
        public Task WriteTempAsync(byte[] data, SaveSlot slot) => inner.WriteTempAsync(data, slot);
        public Task<byte[]> LoadTempAsync(SaveSlot slot) => inner.LoadTempAsync(slot);
        public void DeleteTemp(SaveSlot slot) => inner.DeleteTemp(slot);
    }
}
#endif
