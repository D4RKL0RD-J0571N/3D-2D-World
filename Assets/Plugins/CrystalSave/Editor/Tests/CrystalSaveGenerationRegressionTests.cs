#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator Generation_FullLoadDiscardsEarlierPendingComponents()
            => PendingGenerationScenario("replace");
        [UnityTest] public IEnumerator Generation_CurrentPendingComponentsStillApply()
            => PendingGenerationScenario("current");
        [UnityTest] public IEnumerator Generation_RejectedPreparationKeepsCurrentPendingComponents()
            => PendingGenerationScenario("invalid");
        [UnityTest] public IEnumerator Generation_CancelledLoadKeepsCurrentPendingComponents()
            => PendingGenerationScenario("cancel");

        IEnumerator PendingGenerationScenario(string next)
        {
            var manager = SaveManager.Instance;
            var probe = new GenerationSaveable();
            var first = new SaveData(manager.SaveSettings.version);
            first.ComponentsData[probe.UniqueIdentifier] = new byte[] { 73 };
            manager.SaveSystem.Save(manager.Serializer.Serialize(first), manager.GetSlotMetadata(1));
            var second = new SaveData(manager.SaveSettings.version);
            if (next == "invalid") second.Version = null;
            manager.SaveSystem.Save(manager.Serializer.Serialize(second), manager.GetSlotMetadata(2));
            var loadA = manager.LoadSaveSlotAsync(1);
            yield return Await(loadA);
            Assert.That(loadA.Result.Success, Is.True, loadA.Result.ErrorMessage);
            var pending = PendingBytes(manager.ComponentManager);
            Assert.That(pending.ContainsKey(probe.UniqueIdentifier), Is.True);
            var published = manager.CurrentSaveData;
            if (next != "current")
            {
                LogAssert.ignoreFailingMessages = next == "invalid";
                try
                {
                    var loadB = manager.LoadSaveSlotAsync(2, cancellationToken: new CancellationToken(next == "cancel"));
                    yield return Await(loadB);
                    Assert.That(loadB.Result.Success, Is.EqualTo(next == "replace"), loadB.Result.ErrorMessage);
                    if (next != "replace") Assert.That(manager.CurrentSaveData, Is.SameAs(published));
                }
                finally { LogAssert.ignoreFailingMessages = false; }
            }
            try
            {
                manager.ComponentManager.RegisterSaveableComponent(probe);
                Assert.That(probe.Loads, Is.EqualTo(next == "replace" ? 0 : 1));
                Assert.That(probe.Value, Is.EqualTo(next == "replace" ? 0 : 73));
            }
            finally { manager.ComponentManager.UnregisterSaveableComponent(probe); }
        }

        [UnityTest]
        public IEnumerator Generation_RetiresPendingBeforeSceneTransitionCallbacks()
        {
            var manager = SaveManager.Instance;
            var probe = new GenerationSaveable();
            var first = new SaveData(manager.SaveSettings.version);
            first.ComponentsData[probe.UniqueIdentifier] = new byte[] { 73 };
            manager.SaveSystem.Save(manager.Serializer.Serialize(first), manager.GetSlotMetadata(1));
            manager.SaveSystem.Save(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)
                { LastActiveScene = "A" }), manager.GetSlotMetadata(2));
            yield return Await(manager.LoadSaveSlotAsync(1));
            bool observed = false;
            Action<string> onTransition = _ =>
            {
                observed = true;
                manager.ComponentManager.RegisterSaveableComponent(probe);
            };
            manager.OnSceneLoadStarted += onTransition;
            try
            {
                var load = manager.LoadSaveSlotAsync(2, true, true);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                Assert.That(observed, Is.True);
                Assert.That(probe.Loads, Is.Zero, "Retirement must precede scene activation and registration callbacks.");
            }
            finally
            {
                manager.OnSceneLoadStarted -= onTransition;
                manager.ComponentManager.UnregisterSaveableComponent(probe);
            }
        }

        [UnityTest]
        public IEnumerator Generation_NewDeferredBytesReplaceOldPendingAndRetainFailedRetry()
        {
            var manager = SaveManager.Instance;
            var probe = new GenerationSaveable();
            var first = new SaveData(manager.SaveSettings.version);
            first.ComponentsData[probe.UniqueIdentifier] = new byte[] { 21 };
            manager.SaveSystem.Save(manager.Serializer.Serialize(first), manager.GetSlotMetadata(1));
            var second = new SaveData(manager.SaveSettings.version);
            second.ComponentsData[probe.UniqueIdentifier] = new byte[] { 42 };
            second.ComponentMetadata[probe.UniqueIdentifier] = new ComponentDataMetadata(50, true, null);
            manager.SaveSystem.Save(manager.Serializer.Serialize(second), manager.GetSlotMetadata(2));
            yield return Await(manager.LoadSaveSlotAsync(1));
            var load = manager.LoadSaveSlotAsync(2);
            yield return Await(load);
            Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
            try
            {
                manager.ComponentManager.RegisterSaveableComponent(probe);
                Assert.That(probe.Loads, Is.Zero, "Old pending data cannot bypass the new generation's deferral.");
                probe.Fail = true;
                LogAssert.ignoreFailingMessages = true;
                manager.ComponentManager.ProcessDeferredComponents();
                var pending = PendingBytes(manager.ComponentManager);
                Assert.That(pending[probe.UniqueIdentifier], Is.EqualTo(new byte[] { 42 }));
                manager.ComponentManager.UnregisterSaveableComponent(probe);
                probe.Fail = false;
                manager.ComponentManager.RegisterSaveableComponent(probe);
                Assert.That(probe.Value, Is.EqualTo(42));
                Assert.That(probe.Loads, Is.EqualTo(2));
                Assert.That(pending.ContainsKey(probe.UniqueIdentifier), Is.False);
            }
            finally
            {
                manager.ComponentManager.UnregisterSaveableComponent(probe);
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator Generation_FullLoadRetiresInactiveHelperApplications()
        {
            var manager = SaveManager.Instance;
            foreach (Type helperType in new[] { typeof(RememberTransform), typeof(RememberCollider), typeof(RememberParent) })
            {
                var target = new GameObject("Pending " + helperType.Name);
                var firstParent = new GameObject("First parent");
                var currentParent = new GameObject("Current parent");
                firstParent.AddComponent<UniqueID>();
                currentParent.AddComponent<UniqueID>();
                try
                {
                    var collider = target.AddComponent<BoxCollider>();
                    Composite(target, helperType);
                    if (helperType == typeof(RememberTransform)) ConfigureTransform(target);
                    var helper = (SaveableComponent)target.GetComponent(helperType);
                    yield return null;
                    target.transform.localPosition = new Vector3(3, 4, 5);
                    collider.enabled = false;
                    if (helperType == typeof(RememberParent)) target.transform.SetParent(firstParent.transform);
                    byte[] bytes = helper.SaveData();
                    string id = helper.UniqueIdentifier;
                    target.SetActive(false);
                    target.transform.localPosition = new Vector3(8, 9, 10);
                    collider.enabled = true;
                    if (helperType == typeof(RememberParent)) target.transform.SetParent(currentParent.transform);
                    var snapshot = new SaveData(manager.SaveSettings.version);
                    snapshot.ComponentsData[id] = bytes;
                    var restore = manager.RestoreSingleGameObjectAsync(target, snapshot);
                    yield return Await(restore);
                    Assert.That(restore.Result, Is.True);
                    Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(id), Is.True);
                    manager.SaveSystem.Save(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)), manager.GetSlotMetadata(2));
                    var load = manager.LoadSaveSlotAsync(2);
                    yield return Await(load);
                    Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                    target.SetActive(true);
                    yield return null; yield return null;
                    if (helperType == typeof(RememberTransform))
                        Assert.That(target.transform.localPosition, Is.EqualTo(new Vector3(8, 9, 10)));
                    if (helperType == typeof(RememberCollider)) Assert.That(collider.enabled, Is.True);
                    if (helperType == typeof(RememberParent)) Assert.That(target.transform.parent, Is.SameAs(currentParent.transform));
                    Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(id), Is.False);
                }
                finally
                {
                    if (target) Object.Destroy(target);
                    Object.Destroy(firstParent); Object.Destroy(currentParent);
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Generation_CompletedParentApplicationRetiresOnlyAfterNewFullLoad()
        {
            var manager = SaveManager.Instance;
            var target = new GameObject("Completed parent application");
            var savedParent = new GameObject("Saved parent");
            var currentParent = new GameObject("Current parent");
            savedParent.AddComponent<UniqueID>();
            currentParent.AddComponent<UniqueID>();
            try
            {
                Composite(target, typeof(RememberParent));
                var helper = target.GetComponent<RememberParent>();
                yield return null;
                target.transform.SetParent(savedParent.transform);
                var snapshot = new SaveData(manager.SaveSettings.version);
                snapshot.ComponentsData[helper.UniqueIdentifier] = helper.SaveData();
                var restored = manager.RestoreSingleGameObjectAsync(target, snapshot);
                yield return Await(restored);
                Assert.That(restored.Result, Is.True);
                Assert.That(Get<object>(helper, "applicationTicket"), Is.Null, "The completed-ticket path must be exercised.");
                target.transform.SetParent(currentParent.transform);
                target.SetActive(false); target.SetActive(true);
                Assert.That(target.transform.parent, Is.SameAs(savedParent.transform), "Within-generation pooling retains parent restoration.");
                target.transform.SetParent(currentParent.transform);
                manager.SaveSystem.Save(manager.Serializer.Serialize(new SaveData(manager.SaveSettings.version)), manager.GetSlotMetadata(2));
                var load = manager.LoadSaveSlotAsync(2);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                target.SetActive(false); target.SetActive(true);
                yield return null;
                Assert.That(target.transform.parent, Is.SameAs(currentParent.transform), "An earlier generation cannot reclaim the parent on pooling activation.");
            }
            finally
            {
                if (target) Object.Destroy(target);
                Object.Destroy(savedParent); Object.Destroy(currentParent);
            }
        }
    }

    internal sealed class GenerationSaveable : ISaveable
    {
        public string UniqueIdentifier => "generation-object_generation-component";
        internal int Loads, Value;
        internal bool Fail;
        public byte[] SaveData() => new byte[] { (byte)Value };
        public void LoadData(byte[] bytes)
        {
            Loads++;
            if (Fail) throw new InvalidOperationException("Current generation retry probe.");
            Value = bytes[0];
        }
    }
}
#endif
