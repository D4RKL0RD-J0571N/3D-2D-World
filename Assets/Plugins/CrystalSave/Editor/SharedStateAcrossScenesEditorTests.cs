#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class SharedStateAcrossScenesEditorTests
    {
        private GameObject sourceObject;
        private GameObject destinationObject;

        [TearDown]
        public void TearDown()
        {
            if (sourceObject != null)
                UnityEngine.Object.DestroyImmediate(sourceObject);
            if (destinationObject != null)
                UnityEngine.Object.DestroyImmediate(destinationObject);
        }

        [Test]
        public void UnregisterAppliesSnapshotWhenMatchingDestinationRegisteredFirst()
        {
            var manager = new ComponentManager();
            SharedStateTestComponent source = CreateComponent(
                ref sourceObject,
                "SharedStateSource",
                "shared-character",
                "remember-preset");
            SharedStateTestComponent destination = CreateComponent(
                ref destinationObject,
                "SharedStateDestination",
                "shared-character",
                "remember-preset");
            source.SerializedPayload = new byte[] { 4, 8, 15, 16, 23, 42 };

            manager.RegisterSaveableComponent(source);
            manager.RegisterSaveableComponent(destination);

            manager.UnregisterSaveableComponent(source);

            Assert.That(destination.DeserializeInvocationCount, Is.EqualTo(1));
            Assert.That(destination.LastDeserializedData, Is.EqualTo(source.SerializedPayload));
            Assert.That(
                manager.HasComponentDeserialized(destination.UniqueIdentifier),
                Is.True,
                "Removing an overlapping source must not clear the destination's applied-state marker.");
        }

        [Test]
        public void UnregisterDoesNotReplaceAlreadyDeserializedDestination()
        {
            var manager = new ComponentManager();
            SharedStateTestComponent source = CreateComponent(
                ref sourceObject,
                "SharedStateDiskSource",
                "shared-character-disk",
                "remember-preset");
            SharedStateTestComponent destination = CreateComponent(
                ref destinationObject,
                "SharedStateDiskDestination",
                "shared-character-disk",
                "remember-preset");
            source.SerializedPayload = new byte[] { 1, 2, 3 };

            manager.RegisterSaveableComponent(source);
            manager.RegisterSaveableComponent(destination);
            manager.MarkComponentDeserialized(destination.UniqueIdentifier);

            manager.UnregisterSaveableComponent(source);

            Assert.That(destination.DeserializeInvocationCount, Is.Zero);
            Assert.That(manager.HasComponentDeserialized(destination.UniqueIdentifier), Is.True);
        }

        [Test]
        public void DiskLoadedSourceDoesNotBlockRuntimeSnapshotForMatchingDestination()
        {
            var manager = new ComponentManager();
            SharedStateTestComponent source = CreateComponent(
                ref sourceObject,
                "SharedStateLoadedSource",
                "shared-character-loaded-source",
                "remember-preset");
            SharedStateTestComponent destination = CreateComponent(
                ref destinationObject,
                "SharedStateDestinationAfterLoadedSource",
                "shared-character-loaded-source",
                "remember-preset");
            byte[] diskPayload = { 1, 1, 2, 3, 5 };
            byte[] runtimePayload = { 8, 13, 21, 34 };

            manager.RegisterSaveableComponent(source);
            source.LoadData(diskPayload);
            MarkComponentInstanceDeserialized(manager, source);
            source.SerializedPayload = runtimePayload;
            manager.RegisterSaveableComponent(destination);

            manager.UnregisterSaveableComponent(source);

            Assert.That(destination.DeserializeInvocationCount, Is.EqualTo(1));
            Assert.That(destination.LastDeserializedData, Is.EqualTo(runtimePayload));
        }

        [Test]
        public void DiskLoadedDestinationKeepsPriorityOverRuntimeSnapshot()
        {
            var manager = new ComponentManager();
            SharedStateTestComponent source = CreateComponent(
                ref sourceObject,
                "SharedStateSourceBeforeLoadedDestination",
                "shared-character-loaded-destination",
                "remember-preset");
            SharedStateTestComponent destination = CreateComponent(
                ref destinationObject,
                "SharedStateLoadedDestination",
                "shared-character-loaded-destination",
                "remember-preset");
            byte[] diskPayload = { 55, 89 };
            source.SerializedPayload = new byte[] { 144, 233 };

            manager.RegisterSaveableComponent(source);
            manager.RegisterSaveableComponent(destination);
            destination.LoadData(diskPayload);
            MarkComponentInstanceDeserialized(manager, destination);

            manager.UnregisterSaveableComponent(source);

            Assert.That(destination.DeserializeInvocationCount, Is.EqualTo(1));
            Assert.That(destination.LastDeserializedData, Is.EqualTo(diskPayload));
        }

        [Test]
        public void DefensiveSecondUnregisterDoesNotOverwriteLiveTeardownSnapshot()
        {
            var manager = new ComponentManager();
            SharedStateTestComponent source = CreateComponent(
                ref sourceObject,
                "SharedStateDoubleUnregisterSource",
                "shared-character-teardown",
                "remember-preset");
            SharedStateTestComponent destination = CreateComponent(
                ref destinationObject,
                "SharedStateDoubleUnregisterDestination",
                "shared-character-teardown",
                "remember-preset");
            source.SerializedPayload = new byte[] { 10, 20, 30 };
            manager.RegisterSaveableComponent(source);

            manager.UnregisterSaveableComponent(source);
            source.SerializedPayload = new byte[] { 0 };
            manager.UnregisterSaveableComponent(source);
            manager.RegisterSaveableComponent(destination);

            Assert.That(destination.DeserializeInvocationCount, Is.EqualTo(1));
            Assert.That(destination.LastDeserializedData, Is.EqualTo(new byte[] { 10, 20, 30 }));
        }

        [Test]
        public void ValidationUsesSiblingUniqueIdAsAuthoritativeIdentity()
        {
            sourceObject = new GameObject("SharedStateIdentityValidation");
            sourceObject.SetActive(false);
            UniqueID siblingIdentity = sourceObject.AddComponent<UniqueID>();
            siblingIdentity.ID = "matched-scene-character";
            SharedStateTestComponent component =
                sourceObject.AddComponent<SharedStateTestComponent>();
            component.SetIdentity("stale-hidden-helper-id", "remember-preset");

            MethodInfo onValidate = typeof(SaveableComponent).GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onValidate, Is.Not.Null);
            onValidate.Invoke(component, null);

            Assert.That(component.GameObjectUniqueID, Is.EqualTo(siblingIdentity.ID));
            Assert.That(siblingIdentity.ID, Is.EqualTo("matched-scene-character"));
        }

        private static SharedStateTestComponent CreateComponent(
            ref GameObject owner,
            string name,
            string gameObjectId,
            string componentId)
        {
            owner = new GameObject(name);
            owner.SetActive(false);
            SharedStateTestComponent component = owner.AddComponent<SharedStateTestComponent>();
            component.SetIdentity(gameObjectId, componentId);
            component.ShareStateAcrossScenes = true;
            return component;
        }

        private static void MarkComponentInstanceDeserialized(
            ComponentManager manager,
            ISaveable component)
        {
            MethodInfo markInstance = typeof(ComponentManager).GetMethod(
                "MarkComponentDeserialized",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(ISaveable), typeof(string) },
                modifiers: null);
            Assert.That(markInstance, Is.Not.Null);
            markInstance.Invoke(
                manager,
                new object[] { component, component.UniqueIdentifier });
        }
    }

    internal sealed class SharedStateTestComponent : SaveableComponent
    {
        internal byte[] SerializedPayload { get; set; } = Array.Empty<byte>();
        internal int DeserializeInvocationCount { get; private set; }
        internal byte[] LastDeserializedData { get; private set; }

        internal void SetIdentity(string gameObjectId, string componentId)
        {
            uniqueID = gameObjectId;
            ComponentID = componentId;
        }

        protected override byte[] SerializeComponentData()
        {
            return SerializedPayload;
        }

        protected override void DeserializeComponentData(byte[] data)
        {
            DeserializeInvocationCount++;
            LastDeserializedData = data == null ? null : (byte[])data.Clone();
        }
    }
}
#endif
