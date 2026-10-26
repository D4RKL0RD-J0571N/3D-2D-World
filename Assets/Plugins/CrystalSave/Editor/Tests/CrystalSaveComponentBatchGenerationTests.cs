#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator ComponentBatchGeneration_DeferredYieldedRetires() => ComponentBatchGenerationScenario("deferred", "replace");
        [UnityTest] public IEnumerator ComponentBatchGeneration_SceneDeferredYieldedRetires() => ComponentBatchGenerationScenario("scene", "replace");
        [UnityTest] public IEnumerator ComponentBatchGeneration_ImmediateYieldedRetires() => ComponentBatchGenerationScenario("immediate", "replace");
        [UnityTest] public IEnumerator ComponentBatchGeneration_DeferredUnstartedRetires() => ComponentBatchGenerationScenario("deferred", "unstarted");
        [UnityTest] public IEnumerator ComponentBatchGeneration_SceneDeferredUnstartedRetires() => ComponentBatchGenerationScenario("scene", "unstarted");
        [UnityTest] public IEnumerator ComponentBatchGeneration_ImmediateUnstartedRetires() => ComponentBatchGenerationScenario("immediate", "unstarted");
        [UnityTest] public IEnumerator ComponentBatchGeneration_CurrentBatchControl() => ComponentBatchGenerationScenario("deferred", "current");
        [UnityTest] public IEnumerator ComponentBatchGeneration_ClearQueueKeepsActiveBatch() => ComponentBatchGenerationScenario("deferred", "clear");
        [UnityTest] public IEnumerator ComponentBatchGeneration_RejectedLoadKeepsActiveBatch() => ComponentBatchGenerationScenario("deferred", "invalid");
        [UnityTest] public IEnumerator ComponentBatchGeneration_CancelledLoadKeepsActiveBatch() => ComponentBatchGenerationScenario("deferred", "cancel");

        IEnumerator ComponentBatchGenerationScenario(string route, string outcome)
        {
            var manager = SaveManager.Instance;
            var components = manager.ComponentManager;
            var first = new BatchGenerationReceiver("batch-generation-first_component");
            var second = new BatchGenerationReceiver("batch-generation-second_component");
            var old = new SaveData(manager.SaveSettings.version);
            old.ComponentsData[first.UniqueIdentifier] = new byte[] { 21 };
            old.ComponentsData[second.UniqueIdentifier] = new byte[] { 73 };
            old.ComponentMetadata[first.UniqueIdentifier] = new ComponentDataMetadata(100, route != "immediate", "BatchArea");
            old.ComponentMetadata[second.UniqueIdentifier] = new ComponentDataMetadata(50, route != "immediate", "BatchArea");
            manager.SaveSystem.Save(manager.Serializer.Serialize(old), manager.GetSlotMetadata(1));
            if (route != "immediate")
            {
                var loadA = manager.LoadSaveSlotAsync(1);
                yield return Await(loadA);
                Assert.That(loadA.Result.Success, Is.True, loadA.Result.ErrorMessage);
            }
            components.RegisterSaveableComponent(first);
            components.RegisterSaveableComponent(second);
            IEnumerator routine = route == "immediate" ? components.ApplyComponentDataCoroutine(old, 1)
                : route == "scene" ? components.ProcessDeferredComponentsForSceneCoroutine("BatchArea", 1)
                : components.ProcessDeferredComponentsCoroutine(1);
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            bool ignored = LogAssert.ignoreFailingMessages;
            try
            {
                if (outcome != "unstarted")
                {
                    Assert.That(AdvanceComponentBatch(stack), Is.True);
                    Assert.That(first.Value, Is.EqualTo(21), "The first entry must apply before the batch yields.");
                    Assert.That(second.Loads, Is.Zero);
                }
                if (outcome == "clear") components.ClearDeferredComponentData();
                if (outcome != "current" && outcome != "clear")
                {
                    var next = new SaveData(manager.SaveSettings.version);
                    if (outcome == "invalid") next.Version = null;
                    if (outcome == "unstarted" && route != "immediate")
                    {
                        next.ComponentsData[second.UniqueIdentifier] = new byte[] { 99 };
                        next.ComponentMetadata[second.UniqueIdentifier] = new ComponentDataMetadata(50, true, "BatchArea");
                    }
                    manager.SaveSystem.Save(manager.Serializer.Serialize(next), manager.GetSlotMetadata(2));
                    LogAssert.ignoreFailingMessages = ignored || outcome == "invalid";
                    var replacement = manager.LoadSaveSlotAsync(2, cancellationToken: new CancellationToken(outcome == "cancel"));
                    yield return Await(replacement);
                    Assert.That(replacement.Result.Success, Is.EqualTo(outcome != "invalid" && outcome != "cancel"), replacement.Result.ErrorMessage);
                }
                int moves = 0;
                while (AdvanceComponentBatch(stack))
                {
                    Assert.That(++moves, Is.LessThan(10));
                    yield return null;
                }
                bool retired = outcome == "replace" || outcome == "unstarted";
                Assert.That(second.Loads, Is.EqualTo(retired ? 0 : 1),
                    "A component iterator belongs to the full snapshot current when it was created.");
                if (retired)
                    Assert.That(components.HasComponentDeserialized(second.UniqueIdentifier), Is.False);
                else Assert.That(second.Value, Is.EqualTo(73));
                if (outcome == "unstarted" && route != "immediate")
                {
                    Assert.That(components.HasDeferredComponents, Is.True, "An old iterator cannot dequeue a replacement snapshot's entries.");
                    components.ProcessDeferredComponents();
                    Assert.That(second.Value, Is.EqualTo(99));
                    Assert.That(second.Loads, Is.EqualTo(1));
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                components.UnregisterSaveableComponent(first);
                components.UnregisterSaveableComponent(second);
                LogAssert.ignoreFailingMessages = ignored;
            }
        }

        static bool AdvanceComponentBatch(Stack<IEnumerator> stack)
        {
            int steps = 0;
            while (stack.Count > 0)
            {
                Assert.That(++steps, Is.LessThan(30), "Nested component iterator must remain bounded.");
                var current = stack.Peek();
                if (!current.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
                return true;
            }
            return false;
        }
    }

    internal sealed class BatchGenerationReceiver : ISaveable
    {
        public string UniqueIdentifier { get; }
        internal int Loads, Value;
        internal BatchGenerationReceiver(string id) { UniqueIdentifier = id; }
        public byte[] SaveData() => new byte[] { (byte)Value };
        public void LoadData(byte[] bytes) { Loads++; Value = bytes[0]; }
    }
}
#endif
