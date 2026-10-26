#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator RestorePrecedence_ActiveDiskBeatsHomeAndShared() => RestorePrecedenceScenario(true, false);
        [UnityTest] public IEnumerator RestorePrecedence_InactiveDiskBeatsHomeAndShared() => RestorePrecedenceScenario(true, true);
        [UnityTest] public IEnumerator RestorePrecedence_ActiveHomeBeatsShared() => RestorePrecedenceScenario(false, false);
        [UnityTest] public IEnumerator RestorePrecedence_InactiveHomeBeatsShared() => RestorePrecedenceScenario(false, true);

        [UnityTest]
        public IEnumerator RestorePrecedence_DiskLoadRestoresLateReceiverInsteadOfOlderHomeSnapshot()
        {
            var manager = SaveManager.Instance;
            var helper = CreatePrecedenceHelper("Precedence loaded receiver");
            try
            {
                helper.transform.localPosition = Vector3.right * 11;
                byte[] home = helper.SaveData();
                helper.transform.localPosition = Vector3.right * 33;
                byte[] disk = helper.SaveData();
                var data = new SaveData(manager.SaveSettings.version);
                data.ComponentsData[helper.UniqueIdentifier] = disk;
                data.HomeSceneComponentSnapshots = new Dictionary<string, Dictionary<string, Dictionary<string, byte[]>>>
                {
                    [helper.HomeScene] = new Dictionary<string, Dictionary<string, byte[]>>
                    {
                        [helper.GameObjectUniqueID] = new Dictionary<string, byte[]>
                        { [helper.UniqueIdentifier] = home }
                    }
                };
                manager.SaveSystem.Save(manager.Serializer.Serialize(data), manager.GetSlotMetadata(1));
                helper.gameObject.SetActive(false);
                helper.transform.localPosition = Vector3.right * 99;
                var load = manager.LoadSaveSlotAsync(1);
                yield return Await(load);
                Assert.That(load.Result.Success, Is.True, load.Result.ErrorMessage);
                Assert.That(PendingBytes(manager.ComponentManager)[helper.UniqueIdentifier], Is.EqualTo(disk));
                helper.gameObject.SetActive(true);
                yield return null;
                yield return null;
                Assert.That(helper.transform.localPosition, Is.EqualTo(Vector3.right * 33));
                Assert.That(PendingBytes(manager.ComponentManager).ContainsKey(helper.UniqueIdentifier), Is.False);
            }
            finally { Object.Destroy(helper.gameObject); }
        }

        IEnumerator RestorePrecedenceScenario(bool disk, bool inactive)
        {
            var components = SaveManager.Instance.ComponentManager;
            var helper = CreatePrecedenceHelper("Precedence receiver");
            try
            {
                byte[] diskBytes = SeedPrecedenceSources(components, helper);
                if (disk) Call(components, "QueuePendingComponentData", helper.UniqueIdentifier, diskBytes);
                if (inactive) helper.gameObject.SetActive(false);
                components.RegisterSaveableComponent(helper);
                var ticket = Get<object>(helper, "applicationTicket");
                Assert.That(ticket, Is.Not.Null, "The real helper must still await LateUpdate.");
                Assert.That(components.HasComponentDeserialized(helper.UniqueIdentifier), Is.False);
                Assert.That(PendingBytes(components)[helper.UniqueIdentifier],
                    Is.EqualTo(disk ? diskBytes : HomeSnapshots(components)[helper.HomeScene][helper.GameObjectUniqueID][helper.UniqueIdentifier]));

                // Repeated registration must not cancel a still-running application,
                // including an inactive receiver whose delayed work has not run yet.
                helper.gameObject.SetActive(true);
                helper.enabled = false;
                helper.enabled = true;
                Assert.That(Get<object>(helper, "applicationTicket"), Is.SameAs(ticket));
                yield return null;
                yield return null;
                Assert.That(helper.transform.localPosition, Is.EqualTo(Vector3.right * (disk ? 33 : 11)));
                Assert.That(PendingBytes(components).ContainsKey(helper.UniqueIdentifier), Is.False);
                Assert.That(components.HasComponentDeserialized(helper.UniqueIdentifier), Is.True);
            }
            finally { Object.Destroy(helper.gameObject); }
        }

        [UnityTest]
        public IEnumerator RestorePrecedence_FailedDiskApplicationRetainsBytesAndRetries()
        {
            var components = SaveManager.Instance.ComponentManager;
            var helper = CreatePrecedenceHelper("Precedence retry", SpaceMode.World);
            var parent = new GameObject("Temporarily invalid scale");
            bool ignored = LogAssert.ignoreFailingMessages;
            try
            {
                byte[] diskBytes = SeedPrecedenceSources(components, helper);
                helper.transform.SetParent(parent.transform);
                parent.transform.localScale = Vector3.zero;
                Call(components, "QueuePendingComponentData", helper.UniqueIdentifier, diskBytes);
                LogAssert.ignoreFailingMessages = true;
                components.RegisterSaveableComponent(helper);
                yield return null;
                yield return null;
                Assert.That(Get<object>(helper, "applicationTicket"), Is.Null, "The first application must have failed and finished.");
                Assert.That(PendingBytes(components)[helper.UniqueIdentifier], Is.EqualTo(diskBytes));
                Assert.That(components.HasComponentDeserialized(helper.UniqueIdentifier), Is.False);
                LogAssert.ignoreFailingMessages = ignored;

                parent.transform.localScale = Vector3.one;
                helper.enabled = false;
                helper.enabled = true;
                Assert.That(Get<object>(helper, "applicationTicket"), Is.Not.Null, "Failed applications must remain retryable.");
                yield return null;
                yield return null;
                Assert.That(helper.transform.position, Is.EqualTo(Vector3.right * 33));
                Assert.That(helper.transform.lossyScale, Is.EqualTo(Vector3.one));
                Assert.That(PendingBytes(components).ContainsKey(helper.UniqueIdentifier), Is.False);
                Assert.That(components.HasComponentDeserialized(helper.UniqueIdentifier), Is.True);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignored;
                Object.Destroy(helper.gameObject);
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator RestorePrecedence_OutgoingSharedStateCannotReplaceDelayedDestination()
        {
            var components = SaveManager.Instance.ComponentManager;
            var source = CreatePrecedenceHelper("Precedence outgoing");
            RememberTransform destination = null;
            try
            {
                source.RememberHomeScene = false;
                source.transform.localPosition = Vector3.right * 77;
                destination = CreatePrecedenceHelper("Precedence destination", source: source);
                destination.RememberHomeScene = false;
                destination.transform.localPosition = Vector3.right * 33;
                byte[] diskBytes = destination.SaveData();
                destination.transform.localPosition = Vector3.zero;
                Call(components, "RestoreComponent", destination, diskBytes, destination.UniqueIdentifier);
                object ticket = Get<object>(destination, "applicationTicket");
                components.UnregisterSaveableComponent(source);
                Assert.That(Get<object>(destination, "applicationTicket"), Is.SameAs(ticket));
                Assert.That(PendingBytes(components)[destination.UniqueIdentifier], Is.EqualTo(diskBytes));
                yield return null;
                yield return null;
                Assert.That(destination.transform.localPosition, Is.EqualTo(Vector3.right * 33));
            }
            finally
            {
                if (destination) Object.Destroy(destination.gameObject);
                Object.Destroy(source.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator RestorePrecedence_ReplacementClaimsInactiveApplicationWithoutLosingPendingBytes()
        {
            var components = SaveManager.Instance.ComponentManager;
            var outgoing = CreatePrecedenceHelper("Precedence old instance");
            RememberTransform replacement = null;
            try
            {
                byte[] diskBytes = SeedPrecedenceSources(components, outgoing);
                outgoing.gameObject.SetActive(false);
                Call(components, "QueuePendingComponentData", outgoing.UniqueIdentifier, diskBytes);
                components.RegisterSaveableComponent(outgoing);
                object oldTicket = Get<object>(outgoing, "applicationTicket");
                replacement = CreatePrecedenceHelper("Precedence replacement", source: outgoing, active: false);
                components.RegisterSaveableComponent(replacement);
                Assert.That(Get<object>(replacement, "applicationTicket"), Is.Not.SameAs(oldTicket));
                Object.Destroy(outgoing.gameObject);
                yield return null;
                Assert.That(PendingBytes(components)[replacement.UniqueIdentifier], Is.EqualTo(diskBytes),
                    "A retired receiver must not consume or replace the new receiver's bytes.");
                replacement.gameObject.SetActive(true);
                yield return null;
                yield return null;
                Assert.That(replacement.transform.localPosition, Is.EqualTo(Vector3.right * 33));
                Assert.That(PendingBytes(components).ContainsKey(replacement.UniqueIdentifier), Is.False);
            }
            finally
            {
                if (outgoing) Object.Destroy(outgoing.gameObject);
                if (replacement) Object.Destroy(replacement.gameObject);
            }
        }

        static RememberTransform CreatePrecedenceHelper(string name, SpaceMode space = SpaceMode.Local,
            RememberTransform source = null, bool active = true)
        {
            var target = new GameObject(name);
            target.SetActive(false);
            target.AddComponent<UniqueID>().ID = source ? source.GameObjectUniqueID : Guid.NewGuid().ToString("N");
            var helper = ConfigureTransform(target, space);
            if (source) helper.ComponentID = source.ComponentID;
            // Inactive explicit registration precedes Awake, so set the same identity
            // that Awake will take from the sibling UniqueID when it first activates.
            Set(helper, "uniqueID", target.GetComponent<UniqueID>().ID);
            helper.RememberHomeScene = true;
            helper.HomeScene = SceneManager.GetActiveScene().name;
            helper.ShareStateAcrossScenes = true;
            Set(helper, "unregisterOnDisable", true);
            target.SetActive(active);
            return helper;
        }

        static byte[] SeedPrecedenceSources(ComponentManager components, RememberTransform helper)
        {
            helper.transform.localPosition = Vector3.right * 11;
            components.SnapshotObjectToSceneCache(helper.gameObject);
            helper.transform.localPosition = Vector3.right * 33;
            byte[] disk = helper.SaveData();
            helper.transform.localPosition = Vector3.right * 22;
            components.UnregisterSaveableComponent(helper);
            helper.transform.localPosition = Vector3.right * 99;
            return disk;
        }
    }
}
#endif
