#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember Transform")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(Transform))]
	public class RememberTransform : SaveableComponent, ITransformAppliable
	{
		[StyledHeader("Transform Properties to Save")]
		[SerializeField]
		private TransformPropertySettings rememberPositionSettings = new TransformPropertySettings { Save = false, SpaceMode = SpaceMode.Local };

		[SerializeField]
		private TransformPropertySettings rememberRotationSettings = new TransformPropertySettings { Save = false, SpaceMode = SpaceMode.Local };

		[SerializeField]
		private TransformPropertySettings rememberScaleSettings = new TransformPropertySettings { Save = false, SpaceMode = SpaceMode.Local };

		[StyledHeader("CharacterController Handling")]
		[Tooltip("If true, temporarily disable any attached CharacterController when restoring transform.")]
		[SerializeField]
		private bool disableControllerOnApply = false;

		[Tooltip("Enable lightweight caching of frequently-used components to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;

		[Tooltip("Batch registration: when enabled, SaveManager will optionally batch-register SaveableComponents in this subtree using a single GetComponentsInChildren sweep and spread work across frames. Use with care if you depend on strict parent-before-child registration ordering.")]
		[SerializeField] private bool enableBatchRegistration = false;

		[Tooltip("Skip serialization when the captured transform matches the last saved state.")]
		[SerializeField] private bool skipSavingWhenUnchanged = false;

		public bool EnablePerformanceCaching => enablePerformanceCaching;
		public bool EnableBatchRegistration => enableBatchRegistration;

		// Queued TransformData to be applied in LateUpdate
		private bool shouldApplyTransform = false;
		private TransformData pendingTransformData;
        private ComponentOperationScope.ApplicationTicket applicationTicket;
        private Exception applicationError;
		private int retryCount = 0;
		private const int maxRetries = 3;

		// Cache a reference to any CharacterController on this GameObject
		private CharacterController cachedController;

                private const float PositionTolerance = 0.0001f;
                private const float RotationTolerance = 0.0001f;

                private TransformData initialState;
                private bool hasInitialState;

                protected override void Awake()
                {
                        base.Awake();
                        Logger.Log($"RememberTransform Awake called for '{gameObject.name}'", LogCategory.RememberTransform, LogLevel.Off);

                        // Always detect CharacterController if present, regardless of caching setting
                        cachedController = GetComponent<CharacterController>();

                        if (skipSavingWhenUnchanged)
                        {
                                hasInitialState = TryCaptureCurrentState(out initialState, logSerialization: false);
                        }
                        else
                        {
                                hasInitialState = false;
                                initialState = null;
                        }
                }

		void LateUpdate()
		{
            if (applicationTicket != null && !applicationTicket.IsCurrent)
            {
                applicationTicket.Complete();
                applicationTicket = null;
                shouldApplyTransform = false;
                pendingTransformData = null;
                InvalidateSaveCache();
                return;
            }
            if (!shouldApplyTransform || pendingTransformData == null) return;
            var data = pendingTransformData;
            // Clear before application so a retry scheduled by ApplyTransformData survives.
            shouldApplyTransform = false;
            pendingTransformData = null;
            applicationError = null;
            ApplyTransformData(data);
                InvalidateSaveCache();
            if (!shouldApplyTransform)
            {
                applicationTicket?.Complete(applicationError);
                applicationTicket = null;
            }
        }

        protected override void OnDestroy()
        {
            applicationTicket?.Complete(new InvalidOperationException("Transform was destroyed before application completed."));
            applicationTicket = null;
            base.OnDestroy();
        }

                protected override byte[] SerializeComponentData()
                {
                        if (!TryCaptureCurrentState(out var transformData, logSerialization: true))
                                return null;

                        // During snapshot capture, we still compare against cached state to avoid
                        // unnecessary serialization, but we DON'T return null for unchanged data.
                        // Instead, we serialize the cached state to ensure snapshot completeness.
                        bool isSnapshot = IsCapturingSnapshot;
                        
                        if (skipSavingWhenUnchanged && hasInitialState && AreEquivalent(transformData, initialState))
                        {
                                if (isSnapshot)
                                {
                                        // Data unchanged: for snapshot, serialize the existing cached state
                                        Logger.Log($"RememberTransform: Snapshot capture for '{gameObject.name}' using cached state (unchanged).", LogCategory.RememberTransform, LogLevel.Off);
                                        return Serializer.Serialize(initialState);
                                }
                                else
                                {
                                        // Data unchanged: return cached serialized data instead of null to preserve save integrity
                                        if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                        {
                                                Logger.Log($"RememberTransform: Returning cached serialized data for '{gameObject.name}' (unchanged).", LogCategory.RememberTransform, LogLevel.Off);
                                                return cachedSerializedData;
                                        }
                                        
                                        Logger.Log($"RememberTransform: Data unchanged but no cached serialized data for '{gameObject.name}' - will serialize fresh.", LogCategory.RememberTransform, LogLevel.Off);
                                }
                        }

                        byte[] serialized = Serializer.Serialize(transformData);
                        // Note: Removed frequent logging to improve performance during TimeMachine recording
                        // Logger.Log($"TransformData serialized for '{gameObject.name}'. Byte length: {serialized.Length}", LogCategory.RememberTransform, LogLevel.Info);
                        if (skipSavingWhenUnchanged)
                        {
                                initialState = transformData;
                                hasInitialState = true;
                                cachedSerializedData = serialized;
                        }
                        return serialized;
                }

		protected override void DeserializeComponentData(byte[] data)
		{
			if (data == null || data.Length == 0)
			{
				Logger.Log("DeserializeComponentData failed: data is null or empty.", gameObject, LogCategory.RememberTransform, LogLevel.Warning);
				return;
			}

			try
			{
				TransformData deserializedData = Serializer.Deserialize<TransformData>(data);

				if (deserializedData == null)
				{
					Logger.Log("DeserializeComponentData failed: deserialized data is null.", gameObject, LogCategory.RememberTransform, LogLevel.Warning);
					return;
				}

				Logger.Log($"Deserialized TransformData for '{gameObject.name}': Position={deserializedData.Position}, Rotation={deserializedData.Rotation}, Scale={deserializedData.Scale}", LogCategory.RememberTransform, LogLevel.Info);

				applicationTicket?.Complete(new InvalidOperationException("Transform application was superseded before completion."));
                                applicationTicket = ComponentOperationScope.DeferApplication();
                                retryCount = 0;
                                // Queue the Transform data for application in LateUpdate
				pendingTransformData = deserializedData;
				shouldApplyTransform = true;
                                Logger.Log($"Queued Transform data for LateUpdate application on '{gameObject.name}'.", LogCategory.RememberTransform, LogLevel.Info);

                                if (skipSavingWhenUnchanged)
                                {
                                        initialState = deserializedData;
                                        hasInitialState = true;
                                        cachedSerializedData = (byte[])data.Clone();
                                }
                        }
				catch (Exception ex)
				{
					ComponentOperationScope.Record(ex);
                                        Logger.Log($"DeserializeComponentData encountered an error for '{Logger.GetUnityObjectName(gameObject)}': {ex.Message}\nStack Trace: {ex.StackTrace}", gameObject, LogCategory.RememberTransform, LogLevel.Error);
				}
			}

                public void ApplyTransformData(TransformData data)
                {
	                        if (data == null)
	                        {
	                                Logger.Log($"RememberTransform: Received null TransformData for '{Logger.GetUnityObjectName(gameObject)}'.", gameObject, LogCategory.RememberTransform, LogLevel.Warning);
	                                return;
	                        }

                        // Variables for Rigidbody state management (need to be outside try block for finally access)
                        Rigidbody rb = null;
                        bool hadRigidbody = false;
                        bool wasKinematic = false;
                        Vector3 velocity = Vector3.zero;
                        Vector3 angularVelocity = Vector3.zero;
                        CharacterController controller = enablePerformanceCaching ? cachedController : GetComponent<CharacterController>();
                        bool controllerWasEnabled = controller != null && controller.enabled;

                        try
                        {
                                // Temporarily make Rigidbody kinematic to prevent physics interference during transform application
                                // This is especially important for RememberHomeScene objects that load with physics enabled
                                rb = GetComponent<Rigidbody>();
                                hadRigidbody = rb != null;
                                wasKinematic = hadRigidbody && rb.isKinematic;
                                
                                if (hadRigidbody && !wasKinematic)
                                {
                                        // Switching to kinematic clears motion, including values just
                                        // restored by RememberRigidbody before this LateUpdate.
#if UNITY_6000_0_OR_NEWER
                                        velocity = rb.linearVelocity;
#else
                                        velocity = rb.velocity;
#endif
                                        angularVelocity = rb.angularVelocity;
                                        rb.isKinematic = true;
                                }

                                // If configured, disable CharacterController to prevent it from overriding the transform
                                // Use cached reference if caching enabled, otherwise get component each time
                                if (disableControllerOnApply && controller != null)
                                {
                                        controller.enabled = false;
                                }

                                // Restore Position
                                if (rememberPositionSettings.Save && data.Position.HasValue)
                                {
                                        if (rememberPositionSettings.SpaceMode == SpaceMode.Local)
                                                transform.localPosition = data.Position.Value;
                                        else
                                                transform.position = data.Position.Value;

	                                        if (Logger.ShouldLog(LogCategory.RememberTransform))
	                                        {
	                                                Logger.Log(BuildVectorLog("Applied Position", Logger.GetUnityObjectName(gameObject), transform.position), LogCategory.RememberTransform, LogLevel.Info);
	                                        }
                                }

                                // Restore Rotation
                                if (rememberRotationSettings.Save && data.Rotation.HasValue)
                                {
                                        if (rememberRotationSettings.SpaceMode == SpaceMode.Local)
                                                transform.localRotation = data.Rotation.Value;
                                        else
                                                transform.rotation = data.Rotation.Value;

	                                        if (Logger.ShouldLog(LogCategory.RememberTransform))
	                                        {
	                                                Logger.Log(BuildQuaternionLog("Applied Rotation", Logger.GetUnityObjectName(gameObject), transform.rotation), LogCategory.RememberTransform, LogLevel.Info);
	                                        }
                                }

                                // Restore Scale
                                if (rememberScaleSettings.Save && data.Scale.HasValue)
                                {
                                        if (rememberScaleSettings.SpaceMode == SpaceMode.Local)
                                        {
                                                transform.localScale = data.Scale.Value;
                                        }
                                        else
                                        {
                                                if (transform.parent != null)
                                                {
                                                        Vector3 parentLossyScale = transform.parent.lossyScale;
                                                        if (parentLossyScale.x == 0 || parentLossyScale.y == 0 || parentLossyScale.z == 0)
                                                        {
	                                                                applicationError = new InvalidOperationException("Cannot apply world scale under a zero-scale parent.");
                                                                Logger.Log($"Cannot apply world scale for '{Logger.GetUnityObjectName(gameObject)}' because parent has a scale of zero.", gameObject, LogCategory.RememberTransform, LogLevel.Error);
	                                                                return;
	                                                        }

                                                        Vector3 desiredLocalScale = new Vector3(
                                                                data.Scale.Value.x / parentLossyScale.x,
                                                                data.Scale.Value.y / parentLossyScale.y,
                                                                data.Scale.Value.z / parentLossyScale.z
                                                        );

                                                        transform.localScale = desiredLocalScale;
                                                }
                                                else
                                                {
                                                        transform.localScale = data.Scale.Value;
                                                }
                                        }

	                                        if (Logger.ShouldLog(LogCategory.RememberTransform))
	                                        {
	                                                Logger.Log(BuildVectorLog("Applied Scale", Logger.GetUnityObjectName(gameObject), transform.localScale), LogCategory.RememberTransform, LogLevel.Info);
	                                        }
                                }

                                // Sync physics transforms to prevent physics interference with validation
                                // This is especially important for RememberHomeScene objects with Rigidbodies
                                if (hadRigidbody)
                                {
                                        Physics.SyncTransforms();
                                }

                                // Transform/PhysX can normalize rotations and round parent-space
                                // conversions. Bitwise equality rejects otherwise successful restores.
                                bool positionCorrect = !rememberPositionSettings.Save || !data.Position.HasValue ||
                                        AreApproximatelyEqual(rememberPositionSettings.SpaceMode == SpaceMode.Local ?
                                                transform.localPosition : transform.position, data.Position.Value);

                                bool rotationCorrect = !rememberRotationSettings.Save || !data.Rotation.HasValue ||
                                        RotationMatches(rememberRotationSettings.SpaceMode == SpaceMode.Local ?
                                                transform.localRotation : transform.rotation, data.Rotation.Value);

                                bool scaleCorrect = !rememberScaleSettings.Save || !data.Scale.HasValue ||
                                        (rememberScaleSettings.SpaceMode == SpaceMode.Local ?
                                                AreApproximatelyEqual(transform.localScale, data.Scale.Value) :
                                                AreApproximatelyEqual(transform.lossyScale, data.Scale.Value) ||
                                                Mathf.Approximately(transform.lossyScale.x, data.Scale.Value.x) &&
                                                Mathf.Approximately(transform.lossyScale.y, data.Scale.Value.y) &&
                                                Mathf.Approximately(transform.lossyScale.z, data.Scale.Value.z));

                                if (!positionCorrect || !rotationCorrect || !scaleCorrect)
                                {
	                                        if (Logger.ShouldLog(LogCategory.RememberTransform))
	                                        {
	                                                Logger.Log(BuildRetryLog(Logger.GetUnityObjectName(gameObject), retryCount + 1, maxRetries), gameObject, LogCategory.RememberTransform, LogLevel.Info);
	                                        }
                                        retryCount++;
                                        if (retryCount < maxRetries)
                                        {
                                                shouldApplyTransform = true; // Reapply in the next LateUpdate
                                                pendingTransformData = data; // Reassign the data
                                        }
                                        else
                                        {
	                                                Logger.Log($"Failed to apply Transform correctly for '{Logger.GetUnityObjectName(gameObject)}' after {maxRetries} retries. Matches: position={positionCorrect}, rotation={rotationCorrect}, scale={scaleCorrect}.", gameObject, LogCategory.RememberTransform, LogLevel.Error);
                                                applicationError = new InvalidOperationException($"Transform validation failed after {maxRetries} attempts.");
                                        }
                                }
                                else
                                {
	                                        if (Logger.ShouldLog(LogCategory.RememberTransform))
	                                        {
	                                                Logger.Log($"Successfully applied and validated Transform for '{Logger.GetUnityObjectName(gameObject)}'.", LogCategory.RememberTransform, LogLevel.Info);
	                                        }
                                        retryCount = 0; // Reset retry count on success
                                }
                        }
                        catch (Exception ex)
                        {
	                                applicationError = ex;
                                ComponentOperationScope.Record(ex);
                                Logger.Log($"Exception while applying Transform for '{Logger.GetUnityObjectName(gameObject)}': {ex.Message}\nStack Trace: {ex.StackTrace}", gameObject, LogCategory.RememberTransform, LogLevel.Error);
                        }
                        finally
                        {
                                // Restore Rigidbody kinematic state if we changed it
                                // We always restore to the original state since RememberRigidbody (if present)
                                // has already set the correct state during its earlier deserialization
                                if (hadRigidbody && !wasKinematic && rb != null)
                                {
                                        rb.isKinematic = false;
#if UNITY_6000_0_OR_NEWER
                                        rb.linearVelocity = velocity;
#else
                                        rb.velocity = velocity;
#endif
                                        rb.angularVelocity = angularVelocity;
                                }

                                // Re-enable the CharacterController if it was disabled
                                // Use cached reference if caching enabled, otherwise get component each time
                                if (disableControllerOnApply && controller != null)
                                {
                                        controller.enabled = controllerWasEnabled;
                                }
                        }
                }

                private static bool RotationMatches(Quaternion actual, Quaternion expected)
                {
                        // q and -q encode the same rotation. Compare their components with
                        // a tight tolerance; a dot/angle test loses precision near identity.
                        float sign = Quaternion.Dot(actual, expected) < 0f ? -1f : 1f;
                        float x = actual.x - sign * expected.x;
                        float y = actual.y - sign * expected.y;
                        float z = actual.z - sign * expected.z;
                        float w = actual.w - sign * expected.w;
                        return x * x + y * y + z * z + w * w <= RotationTolerance * RotationTolerance;
                }

                private bool TryCaptureCurrentState(out TransformData data, bool logSerialization)
                {
                        bool hasAnyData = false;
                        data = new TransformData();

                        if (rememberPositionSettings.Save)
                        {
                                data.Position = rememberPositionSettings.SpaceMode == SpaceMode.Local ? transform.localPosition : transform.position;
                                hasAnyData = true;
                                if (logSerialization)
                                        Logger.Log($"Serialized Position for '{gameObject.name}': {data.Position}", LogCategory.RememberTransform, LogLevel.Off); // Changed to LogLevel.Off for performance
                        }

                        if (rememberRotationSettings.Save)
                        {
                                data.Rotation = rememberRotationSettings.SpaceMode == SpaceMode.Local ? transform.localRotation : transform.rotation;
                                hasAnyData = true;
                                if (logSerialization)
                                        Logger.Log($"Serialized Rotation for '{gameObject.name}': {data.Rotation}", LogCategory.RememberTransform, LogLevel.Info);
                        }

                        if (rememberScaleSettings.Save)
                        {
                                if (rememberScaleSettings.SpaceMode == SpaceMode.Local)
                                {
                                        data.Scale = transform.localScale;
                                }
                                else
                                {
                                        data.Scale = transform.lossyScale;
                                }

                                hasAnyData = true;
                                if (logSerialization)
                                        Logger.Log($"Serialized Scale for '{gameObject.name}': {data.Scale}", LogCategory.RememberTransform, LogLevel.Info);
                        }

                        if (!hasAnyData)
                                data = null;

                        return hasAnyData;
                }

                private static bool AreEquivalent(TransformData a, TransformData b)
                {
                        return AreApproximatelyEqual(a?.Position, b?.Position) &&
                               AreApproximatelyEqual(a?.Rotation, b?.Rotation) &&
                               AreApproximatelyEqual(a?.Scale, b?.Scale);
                }

                private static bool AreApproximatelyEqual(Vector3? a, Vector3? b)
                {
                        if (!a.HasValue && !b.HasValue)
                                return true;

                        if (a.HasValue != b.HasValue)
                                return false;

                        return (a.Value - b.Value).sqrMagnitude <= PositionTolerance * PositionTolerance;
                }

                private static bool AreApproximatelyEqual(Quaternion? a, Quaternion? b)
                {
                        if (!a.HasValue && !b.HasValue)
                                return true;

                        if (a.HasValue != b.HasValue)
                                return false;

                        float dot = Mathf.Abs(Quaternion.Dot(a.Value, b.Value));
                        return 1f - dot <= RotationTolerance;
                }

                private static string BuildVectorLog(string prefix, string objectName, Vector3 value)
                {
                        var builder = new StringBuilder(128);
                        builder.Append(prefix).Append(" for '").Append(objectName).Append("': ")
                                .Append('(')
                                .Append(value.x.ToString("F2")).Append(", ")
                                .Append(value.y.ToString("F2")).Append(", ")
                                .Append(value.z.ToString("F2")).Append(')');
                        return builder.ToString();
                }

                private static string BuildQuaternionLog(string prefix, string objectName, Quaternion value)
                {
                        var builder = new StringBuilder(144);
                        builder.Append(prefix).Append(" for '").Append(objectName).Append("': ")
                                .Append('(')
                                .Append(value.x.ToString("F2")).Append(", ")
                                .Append(value.y.ToString("F2")).Append(", ")
                                .Append(value.z.ToString("F2")).Append(", ")
                                .Append(value.w.ToString("F2")).Append(')');
                        return builder.ToString();
                }

                private static string BuildRetryLog(string objectName, int attempt, int maxRetries)
                {
                        var builder = new StringBuilder(144);
                        builder.Append("Validation failed after applying Transform for '")
                                .Append(objectName)
                                .Append("'. Retrying... (")
                                .Append(attempt)
                                .Append('/')
                                .Append(maxRetries)
                                .Append(')');
                        return builder.ToString();
                }
        }

	// Define the ITransformAppliable interface if needed
	public interface ITransformAppliable
	{
		void ApplyTransformData(TransformData data);
	}

	[MemoryPackable]
	public partial class TransformData
	{
		//[MemoryPackOrder(0)]
		public Vector3? Position { get; set; }
		//[MemoryPackOrder(1)]
		public Quaternion? Rotation { get; set; }
		//[MemoryPackOrder(2)]
		public Vector3? Scale { get; set; }

		// Default constructor for MemoryPack
		public TransformData()
		{
			Position = null;
			Rotation = null;
			Scale = null;
		}

		// MemoryPack constructor
		[MemoryPackConstructor]
		public TransformData(Vector3? position, Quaternion? rotation, Vector3? scale)
		{
			Position = position;
			Rotation = rotation;
			Scale = scale;
		}
	}

	[Serializable]
	public class TransformPropertySettings
	{
		[Tooltip("Enable saving this Transform property.")]
		public bool Save = false;

		[Tooltip("Select the coordinate space for this property.")]
		public SpaceMode SpaceMode = SpaceMode.Local;
	}

	public enum SpaceMode
	{
		Local,
		World
	}
}
#endif
