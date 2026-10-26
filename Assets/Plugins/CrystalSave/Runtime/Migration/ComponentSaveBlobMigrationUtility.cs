#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using MemoryPack;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Describes the Crystal Save wrapper layers found around a component payload.
	/// </summary>
	public sealed class ComponentSaveBlobContext
	{
		internal ComponentSaveBlobContext(byte[] originalBlob)
		{
			OriginalBlob = originalBlob;
		}

		internal byte[] OriginalBlob { get; }

		public bool ContainsEventBridgeEnvelope { get; internal set; }
		public bool ContainsTimeMachineWrapper { get; internal set; }

		internal byte[] BridgeData { get; set; }

#if CRYSTALSAVE_TIMEMACHINE
		internal TimelineSaveData TimeMachineData { get; set; }
#endif
	}

	/// <summary>
	/// Helpers for migration actions that need to edit serialized SaveableComponent payloads
	/// without losing Crystal Save wrapper data such as Event Bridge or TimeMachine state.
	/// </summary>
	public static class ComponentSaveBlobMigrationUtility
	{
		/// <summary>
		/// Unwraps a saved component blob and returns the innermost component payload.
		/// </summary>
		public static bool TryUnpack(
			byte[] savedBlob,
			out byte[] componentPayload,
			out ComponentSaveBlobContext context)
		{
			componentPayload = null;
			context = null;

			if (savedBlob == null || savedBlob.Length == 0)
				return false;

			context = new ComponentSaveBlobContext(savedBlob);
			byte[] current = savedBlob;

#if CRYSTALSAVE_TIMEMACHINE
			try
			{
				var wrapper = MemoryPackSerializer.Deserialize<ComponentSaveDataWrapper>(
					current,
					SaveDataSerializer.Instance.Options);

				if (wrapper != null && wrapper.componentData != null)
				{
					context.ContainsTimeMachineWrapper = true;
					context.TimeMachineData = wrapper.timeMachineData;
					current = wrapper.componentData;
				}
			}
			catch
			{
				// Not a TimeMachine wrapper. Treat the blob as the next inner layer.
			}
#endif

			try
			{
				var envelope = MemoryPackSerializer.Deserialize<EventBridgeEnvelope>(
					current,
					SaveDataSerializer.Instance.Options);

				if (envelope != null && envelope.componentData != null)
				{
					context.ContainsEventBridgeEnvelope = true;
					context.BridgeData = envelope.bridgeData;
					current = envelope.componentData;
				}
			}
			catch
			{
				// Not an EventBridge envelope. Treat the blob as raw component data.
			}

			componentPayload = current;
			return componentPayload != null && componentPayload.Length > 0;
		}

		/// <summary>
		/// Rebuilds a saved component blob from an edited component payload and a previous unwrap context.
		/// </summary>
		public static bool TryRepack(
			byte[] componentPayload,
			ComponentSaveBlobContext context,
			out byte[] savedBlob)
		{
			savedBlob = null;

			if (componentPayload == null || componentPayload.Length == 0)
				return false;

			byte[] current = componentPayload;

			if (context != null && context.ContainsEventBridgeEnvelope)
			{
				var envelope = new EventBridgeEnvelope
				{
					componentData = current,
					bridgeData = context.BridgeData
				};

				current = MemoryPackSerializer.Serialize(envelope, SaveDataSerializer.Instance.Options);
				if (current == null || current.Length == 0)
					return false;
			}

#if CRYSTALSAVE_TIMEMACHINE
			if (context != null && context.ContainsTimeMachineWrapper)
			{
				var wrapper = new ComponentSaveDataWrapper
				{
					componentData = current,
					timeMachineData = context.TimeMachineData
				};

				current = MemoryPackSerializer.Serialize(wrapper, SaveDataSerializer.Instance.Options);
				if (current == null || current.Length == 0)
					return false;
			}
#endif

			savedBlob = current;
			return true;
		}

		/// <summary>
		/// Unwraps a saved component blob and deserializes its innermost component payload.
		/// </summary>
		public static bool TryDeserialize<T>(
			byte[] savedBlob,
			out T value,
			out ComponentSaveBlobContext context)
			where T : class
		{
			value = null;

			if (!TryUnpack(savedBlob, out var componentPayload, out context))
				return false;

			value = SaveDataSerializer.Instance.Deserialize<T>(componentPayload);
			return value != null;
		}

		/// <summary>
		/// Serializes a component payload and restores the wrapper layers captured in the supplied context.
		/// </summary>
		public static bool TrySerialize<T>(
			T value,
			ComponentSaveBlobContext context,
			out byte[] savedBlob)
			where T : class
		{
			savedBlob = null;

			if (value == null)
				return false;

			byte[] componentPayload = SaveDataSerializer.Instance.Serialize(value);
			if (componentPayload == null || componentPayload.Length == 0)
				return false;

			return TryRepack(componentPayload, context, out savedBlob);
		}

		/// <summary>
		/// Rewrites exact component-key matches in all SaveData stores that can contain component blobs.
		/// The callback receives the unwrapped component payload and should return the edited payload.
		/// Returning null leaves that matched blob unchanged.
		/// </summary>
		/// <returns>The number of exact component-key matches found.</returns>
		public static int RewriteMatchingComponentData(
			SaveData data,
			string componentKey,
			Func<byte[], byte[]> rewritePayload)
		{
			if (data == null || string.IsNullOrEmpty(componentKey) || rewritePayload == null)
				return 0;

			int matches = 0;

			if (data.ComponentsData != null &&
			    data.ComponentsData.TryGetValue(componentKey, out var componentBlob))
			{
				matches++;
				if (TryRewriteSavedBlob(componentBlob, rewritePayload, out var updatedBlob))
					data.ComponentsData[componentKey] = updatedBlob;
			}

			if (data.DestroyedObjectData != null)
			{
				foreach (var objectEntry in data.DestroyedObjectData)
				{
					var componentData = objectEntry.Value;
					if (componentData == null ||
					    !componentData.TryGetValue(componentKey, out var destroyedBlob))
						continue;

					matches++;
					if (TryRewriteSavedBlob(destroyedBlob, rewritePayload, out var updatedBlob))
						componentData[componentKey] = updatedBlob;
				}
			}

			if (data.HomeSceneComponentSnapshots != null)
			{
				foreach (var sceneEntry in data.HomeSceneComponentSnapshots)
				{
					var perObject = sceneEntry.Value;
					if (perObject == null)
						continue;

					foreach (var objectEntry in perObject)
					{
						var componentData = objectEntry.Value;
						if (componentData == null ||
						    !componentData.TryGetValue(componentKey, out var snapshotBlob))
							continue;

						matches++;
						if (TryRewriteSavedBlob(snapshotBlob, rewritePayload, out var updatedBlob))
							componentData[componentKey] = updatedBlob;
					}
				}
			}

			if (data.SaveableComponents != null)
			{
				foreach (var entry in data.SaveableComponents)
				{
					if (entry?.DataEntries == null)
						continue;

					foreach (var dataEntry in entry.DataEntries)
					{
						if (dataEntry == null ||
						    !string.Equals(dataEntry.UniqueIdentifier, componentKey, StringComparison.Ordinal))
							continue;

						matches++;
						if (TryRewriteSavedBlob(dataEntry.BinaryData, rewritePayload, out var updatedBlob))
							dataEntry.BinaryData = updatedBlob;
					}
				}
			}

			return matches;
		}

		private static bool TryRewriteSavedBlob(
			byte[] savedBlob,
			Func<byte[], byte[]> rewritePayload,
			out byte[] updatedBlob)
		{
			updatedBlob = null;

			if (!TryUnpack(savedBlob, out var componentPayload, out var context))
				return false;

			byte[] updatedPayload = rewritePayload(componentPayload);
			if (updatedPayload == null || updatedPayload.Length == 0)
				return false;

			return TryRepack(updatedPayload, context, out updatedBlob);
		}
	}
}
#endif
