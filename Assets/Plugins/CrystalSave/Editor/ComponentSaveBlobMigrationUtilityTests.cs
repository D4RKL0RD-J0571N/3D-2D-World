#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using MemoryPack;
using NUnit.Framework;
using UnityEngine;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.EditorTests
{
	public class ComponentSaveBlobMigrationUtilityTests
	{
		private const string ComponentKey = "object-1_transform";

		[Test]
		public void RawPayloadRoundTrips()
		{
			var original = new TransformData(Vector3.one, Quaternion.identity, Vector3.one);
			byte[] raw = SaveDataSerializer.Instance.Serialize(original);

			bool unpacked = ComponentSaveBlobMigrationUtility.TryUnpack(
				raw,
				out var payload,
				out var context);

			Assert.IsTrue(unpacked);
			Assert.IsNotNull(context);
			Assert.IsFalse(context.ContainsEventBridgeEnvelope);
			Assert.IsFalse(context.ContainsTimeMachineWrapper);
			CollectionAssert.AreEqual(raw, payload);

			bool repacked = ComponentSaveBlobMigrationUtility.TryRepack(payload, context, out var savedBlob);

			Assert.IsTrue(repacked);
			CollectionAssert.AreEqual(raw, savedBlob);
		}

		[Test]
		public void EventBridgePayloadIsPreservedWhenInnerDataChanges()
		{
			byte[] bridgeData = MemoryPackSerializer.Serialize(
				new EventBridgePayload { type = 1, intValue = 42 },
				SaveDataSerializer.Instance.Options);

			byte[] wrapped = WrapEventBridge(
				new TransformData(Vector3.zero, Quaternion.identity, Vector3.one),
				bridgeData);

			bool deserialized = ComponentSaveBlobMigrationUtility.TryDeserialize<TransformData>(
				wrapped,
				out var transformData,
				out var context);

			Assert.IsTrue(deserialized);
			Assert.IsTrue(context.ContainsEventBridgeEnvelope);

			transformData.Position = new Vector3(2f, 3f, 4f);

			bool serialized = ComponentSaveBlobMigrationUtility.TrySerialize(
				transformData,
				context,
				out var updatedBlob);

			Assert.IsTrue(serialized);

			var updatedEnvelope = MemoryPackSerializer.Deserialize<EventBridgeEnvelope>(
				updatedBlob,
				SaveDataSerializer.Instance.Options);
			var updatedTransform = SaveDataSerializer.Instance.Deserialize<TransformData>(updatedEnvelope.componentData);

			CollectionAssert.AreEqual(bridgeData, updatedEnvelope.bridgeData);
			Assert.AreEqual(new Vector3(2f, 3f, 4f), updatedTransform.Position.Value);
		}

		[Test]
		public void RewriteMatchingComponentDataUpdatesEverySaveDataStore()
		{
			var data = new SaveData(new VersionData(1, 0, 0));
			byte[] raw = SaveDataSerializer.Instance.Serialize(
				new TransformData(Vector3.zero, Quaternion.identity, Vector3.one));

			data.ComponentsData[ComponentKey] = raw;
			data.DestroyedObjectData["object-1"] = new Dictionary<string, byte[]> { { ComponentKey, raw } };
			data.HomeSceneComponentSnapshots["SceneA"] = new Dictionary<string, Dictionary<string, byte[]>>
			{
				["object-1"] = new Dictionary<string, byte[]> { { ComponentKey, raw } }
			};
			data.SaveableComponents.Add(new SaveDataEntry(
				"object-1",
				new List<SerializedSaveableData>
				{
					new SerializedSaveableData(ComponentKey, raw)
				},
				null,
				false));

			int matches = ComponentSaveBlobMigrationUtility.RewriteMatchingComponentData(
				data,
				ComponentKey,
				payload =>
				{
					var transform = SaveDataSerializer.Instance.Deserialize<TransformData>(payload);
					transform.Position = new Vector3(9f, 8f, 7f);
					return SaveDataSerializer.Instance.Serialize(transform);
				});

			Assert.AreEqual(4, matches);
			AssertPosition(data.ComponentsData[ComponentKey], new Vector3(9f, 8f, 7f));
			AssertPosition(data.DestroyedObjectData["object-1"][ComponentKey], new Vector3(9f, 8f, 7f));
			AssertPosition(data.HomeSceneComponentSnapshots["SceneA"]["object-1"][ComponentKey], new Vector3(9f, 8f, 7f));
			AssertPosition(data.SaveableComponents[0].DataEntries[0].BinaryData, new Vector3(9f, 8f, 7f));
		}

		[Test]
		public void TransformMigrationActionUpdatesEventBridgeWrappedData()
		{
			byte[] bridgeData = MemoryPackSerializer.Serialize(
				new EventBridgePayload { type = 2, floatValue = 12.5f },
				SaveDataSerializer.Instance.Options);

			var data = new SaveData(new VersionData(1, 0, 0));
			data.ComponentsData[ComponentKey] = WrapEventBridge(
				new TransformData(Vector3.zero, Quaternion.identity, Vector3.one),
				bridgeData);

			var action = ScriptableObject.CreateInstance<MigrateMultipleTransforms>();
			action.migrationEntries = new List<MigrateMultipleTransforms.TransformMigrationEntry>
			{
				new MigrateMultipleTransforms.TransformMigrationEntry
				{
					targetUniqueID = ComponentKey,
					newPosition = new Vector3(4f, 5f, 6f),
					newEulerRotation = new Vector3(0f, 90f, 0f),
					newScale = new Vector3(2f, 2f, 2f)
				}
			};

			try
			{
				action.ApplyMigration(data);

				var envelope = MemoryPackSerializer.Deserialize<EventBridgeEnvelope>(
					data.ComponentsData[ComponentKey],
					SaveDataSerializer.Instance.Options);
				var transform = SaveDataSerializer.Instance.Deserialize<TransformData>(envelope.componentData);

				CollectionAssert.AreEqual(bridgeData, envelope.bridgeData);
				Assert.AreEqual(new Vector3(4f, 5f, 6f), transform.Position.Value);
				Assert.AreEqual(new Vector3(2f, 2f, 2f), transform.Scale.Value);
			}
			finally
			{
				Object.DestroyImmediate(action);
			}
		}

#if CRYSTALSAVE_TIMEMACHINE
		[Test]
		public void TimeMachineWrapperIsPreservedWhenEventBridgeInnerDataChanges()
		{
			byte[] bridgeData = MemoryPackSerializer.Serialize(
				new EventBridgePayload { type = 3, stringValue = "bridge" },
				SaveDataSerializer.Instance.Options);
			byte[] eventBridgeBlob = WrapEventBridge(
				new TransformData(Vector3.zero, Quaternion.identity, Vector3.one),
				bridgeData);

			var timelineData = new TimelineSaveData
			{
				activeBranchName = "BranchA",
				recordingSpeed = 2f,
				isPlaying = true
			};
			var wrapper = new ComponentSaveDataWrapper
			{
				componentData = eventBridgeBlob,
				timeMachineData = timelineData
			};
			byte[] wrapped = MemoryPackSerializer.Serialize(wrapper, SaveDataSerializer.Instance.Options);

			bool deserialized = ComponentSaveBlobMigrationUtility.TryDeserialize<TransformData>(
				wrapped,
				out var transformData,
				out var context);

			Assert.IsTrue(deserialized);
			Assert.IsTrue(context.ContainsEventBridgeEnvelope);
			Assert.IsTrue(context.ContainsTimeMachineWrapper);

			transformData.Position = Vector3.up;

			bool serialized = ComponentSaveBlobMigrationUtility.TrySerialize(
				transformData,
				context,
				out var updatedBlob);

			Assert.IsTrue(serialized);

			var updatedWrapper = MemoryPackSerializer.Deserialize<ComponentSaveDataWrapper>(
				updatedBlob,
				SaveDataSerializer.Instance.Options);
			var updatedEnvelope = MemoryPackSerializer.Deserialize<EventBridgeEnvelope>(
				updatedWrapper.componentData,
				SaveDataSerializer.Instance.Options);
			var updatedTransform = SaveDataSerializer.Instance.Deserialize<TransformData>(updatedEnvelope.componentData);

			Assert.AreEqual("BranchA", updatedWrapper.timeMachineData.activeBranchName);
			Assert.AreEqual(2f, updatedWrapper.timeMachineData.recordingSpeed);
			Assert.IsTrue(updatedWrapper.timeMachineData.isPlaying);
			CollectionAssert.AreEqual(bridgeData, updatedEnvelope.bridgeData);
			Assert.AreEqual(Vector3.up, updatedTransform.Position.Value);
		}
#endif

		private static byte[] WrapEventBridge(TransformData data, byte[] bridgeData)
		{
			byte[] componentData = SaveDataSerializer.Instance.Serialize(data);
			return MemoryPackSerializer.Serialize(
				new EventBridgeEnvelope
				{
					componentData = componentData,
					bridgeData = bridgeData
				},
				SaveDataSerializer.Instance.Options);
		}

		private static void AssertPosition(byte[] savedBlob, Vector3 expected)
		{
			Assert.IsTrue(ComponentSaveBlobMigrationUtility.TryDeserialize<TransformData>(
				savedBlob,
				out var transform,
				out _));
			Assert.AreEqual(expected, transform.Position.Value);
		}
	}
}
#endif
