#if MEMORYPACK && ARAWN_REMEMBERME
using MemoryPack;
using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[AddComponentMenu("Crystal Save/Remember Components/Remember Collider")]
	[DisallowMultipleComponent]
	[RememberTarget(typeof(Collider))]
	[RememberIcon("SphereCollider Icon")]
	public sealed class RememberCollider : SaveableComponent
	{
		[Tooltip("Enable lightweight caching of the Collider reference to avoid repeated GetComponent calls.")]
		[SerializeField] private bool enablePerformanceCaching = false;
		[StyledHeader("Collider properties to save")]

		[Tooltip("Save the Collider’s enabled state.")]
		[SerializeField] private bool rememberEnabled = true;

		[Tooltip("Save whether the Collider is marked as Trigger.")]
		[SerializeField] private bool rememberIsTrigger = true;

		[Tooltip("Save the Collider’s PhysicMaterial (stored by name).")]
		[SerializeField] private bool rememberMaterial = true;

		[Tooltip("Skip serializing when the collider state hasn't changed since the previous save.")]
		[SerializeField] private bool skipSavingWhenUnchanged = false;

		private Collider targetCollider;
		private ColliderData cachedSnapshot;
		private bool hasCachedSnapshot;
		private const float FloatTolerance = 0.0001f;

		// Queue collider data for LateUpdate application (like RememberTransform)
		private ColliderData pendingColliderData;
		private bool shouldApplyCollider = false;
        private ComponentOperationScope.ApplicationTicket applicationTicket;

		/*─────────────────────────── LIFECYCLE ───────────────────────────*/
		protected override void Awake()
		{
			base.Awake();

			// Always get the Collider component, regardless of caching setting
                        targetCollider = GetComponent<Collider>();
                        if (targetCollider == null)
                        {
                                Logger.Log($"{nameof(RememberCollider)} requires a Collider on '{gameObject.name}'.", LogCategory.RememberCollider, LogLevel.Error);
                                enabled = false;
                                return;
                        }

                        if (skipSavingWhenUnchanged)
                        {
                                if (TryCaptureCurrentState(out var snapshot))
                                {
                                        cachedSnapshot = snapshot;
                                        hasCachedSnapshot = true;
                                }
                                else
                                {
                                        cachedSnapshot = null;
                                        hasCachedSnapshot = false;
                                }
                        }
                        else
                        {
                                cachedSnapshot = null;
                                hasCachedSnapshot = false;
                        }
                }

                /*────────────────────── SERIALISE OUT ───────────────────────*/
                protected override byte[] SerializeComponentData()
                {
                        if (!TryCaptureCurrentState(out var snapshot) || snapshot == null)
                                return null;

                        if (skipSavingWhenUnchanged && hasCachedSnapshot && AreEquivalent(cachedSnapshot, snapshot))
                        {
                                if (cachedSerializedData != null && cachedSerializedData.Length > 0)
                                {
                                        Logger.Log($"RememberCollider: Returning cached serialized data for '{gameObject.name}' (unchanged).", LogCategory.RememberCollider, LogLevel.Off);
                                        return cachedSerializedData;
                                }

                                Logger.Log($"RememberCollider: Data unchanged but no cached serialized data for '{gameObject.name}' - will serialize fresh.", LogCategory.RememberCollider, LogLevel.Off);
                        }

                        var serialized = Serializer.Serialize(snapshot);

                        if (skipSavingWhenUnchanged)
                        {
                                cachedSnapshot = snapshot;
                                hasCachedSnapshot = true;
                                cachedSerializedData = serialized;
                        }

                        return serialized;
                }

	/*────────────────────── SERIALISE  IN ───────────────────────*/
	protected override void DeserializeComponentData(byte[] bytes)
	{
		if (bytes == null || bytes.Length == 0)
		{
			Logger.Log("DeserializeComponentData failed: data is null or empty.", LogCategory.RememberCollider, LogLevel.Warning);
			return;
		}

		try
		{
			var data = Serializer.Deserialize<ColliderData>(bytes);
			if (data == null)
			{
				Logger.Log("DeserializeComponentData failed: deserialized data is null.", LogCategory.RememberCollider, LogLevel.Warning);
                return;
                        }

                        // Queue the Collider data for application in LateUpdate
                        applicationTicket?.Complete(new InvalidOperationException("Pending application was superseded."));
                applicationTicket = ComponentOperationScope.DeferApplication();
                pendingColliderData = data;
                        shouldApplyCollider = true;			if (skipSavingWhenUnchanged)
			{
				cachedSnapshot = data;
				hasCachedSnapshot = true;
			}
		}
		catch (Exception ex)
		{
                ComponentOperationScope.Record(ex);
			Logger.Log($"DeserializeComponentData encountered an error for '{gameObject.name}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.RememberCollider, LogLevel.Error);
		}
	}

	protected override void OnDestroy()
        {
            applicationTicket?.Complete(new InvalidOperationException("Helper was destroyed before pending application finished."));
            base.OnDestroy();
        }

        void LateUpdate()
	{
            if (applicationTicket != null && !applicationTicket.IsCurrent)
            {
                applicationTicket.Complete();
                applicationTicket = null;
                shouldApplyCollider = false;
                pendingColliderData = null;
                InvalidateSaveCache();
                return;
            }
            if (shouldApplyCollider && pendingColliderData != null)
            {
                using var application = applicationTicket?.Resume();
                var data = pendingColliderData;
                shouldApplyCollider = false;
                pendingColliderData = null;
                ApplyColliderData(data);
                InvalidateSaveCache();
                if (!shouldApplyCollider) { applicationTicket?.Complete(); applicationTicket = null; }
            }

        }

	/*────────────────────── DESERIALISE IN ───────────────────────*/

	private void ApplyColliderData(ColliderData data)
	{
			if (data == null)
			{
				Logger.Log($"RememberCollider: Received null ColliderData for '{Logger.GetUnityObjectName(gameObject)}'.", LogCategory.RememberCollider, LogLevel.Warning);
				return;
			}

		try
		{
			// Use cached reference if caching enabled, otherwise get component each time
			Collider collider = enablePerformanceCaching ? targetCollider : GetComponent<Collider>();

				if (collider == null)
				{
					ComponentOperationScope.Record(new InvalidOperationException("Collider component is missing."));
                    Logger.Log($"RememberCollider: Collider component not found on '{Logger.GetUnityObjectName(gameObject)}'.", LogCategory.RememberCollider, LogLevel.Error);
					return;
				}

			// Apply collider state
			if (rememberEnabled) collider.enabled = data.Enabled;
			if (rememberIsTrigger) collider.isTrigger = data.IsTrigger;

				if (Logger.ShouldLog(LogCategory.RememberCollider))
				{
					Logger.Log(BuildAppliedColliderLog(Logger.GetUnityObjectName(gameObject), data.Enabled, data.IsTrigger), LogCategory.RememberCollider, LogLevel.Info);
				}

			// Apply material if configured
			if (rememberMaterial && !string.IsNullOrEmpty(data.MaterialName))
			{
				#if UNITY_6000_0_OR_NEWER
					var mat = SavedAssetReferenceResolver.Load<PhysicsMaterial>(data.MaterialName, "RememberCollider material", LogLevel.Off);
				#else
					var mat = SavedAssetReferenceResolver.Load<PhysicMaterial>(data.MaterialName, "RememberCollider material", LogLevel.Off);
				#endif

				if (mat != null)
				{
					collider.sharedMaterial = mat;
				}
				else
				{
						Logger.Log($"Material '{data.MaterialName}' not found for '{Logger.GetUnityObjectName(gameObject)}'.", LogCategory.RememberCollider, LogLevel.Warning);
				}
			}
		}
		catch (Exception ex)
		{
                ComponentOperationScope.Record(ex);
				Logger.Log($"ApplyColliderData encountered an error for '{Logger.GetUnityObjectName(gameObject)}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.RememberCollider, LogLevel.Error);
			}
		}

		private static string BuildAppliedColliderLog(string objectName, bool enabled, bool isTrigger)
		{
			var builder = new StringBuilder(128);
			builder.Append("Applied Collider data for '").Append(objectName)
				.Append("': Enabled=").Append(enabled)
				.Append(", IsTrigger=").Append(isTrigger);
			return builder.ToString();
		}

		private bool TryCaptureCurrentState(out ColliderData snapshot)
                {
                        snapshot = null;

                        if (!rememberEnabled && !rememberIsTrigger && !rememberMaterial)
                        {
                                return false;
                        }

                        Collider collider = enablePerformanceCaching ? targetCollider : GetComponent<Collider>();
                        if (collider == null)
                        {
                                return false;
                        }

                        var data = new ColliderData();
                        if (rememberEnabled)
                        {
                                data.Enabled = collider.enabled;
                        }

                        if (rememberIsTrigger)
                        {
                                data.IsTrigger = collider.isTrigger;
                        }

                        if (rememberMaterial)
                        {
                                data.MaterialName = collider.sharedMaterial != null
                                        ? SavedAssetReferenceResolver.GetSaveKey(collider.sharedMaterial)
                                        : null;
                        }

                        snapshot = data;
                        return true;
                }

                private bool AreEquivalent(ColliderData a, ColliderData b)
                {
                        if (a == null || b == null) return false;

                        if (rememberEnabled && a.Enabled != b.Enabled) return false;
                        if (rememberIsTrigger && a.IsTrigger != b.IsTrigger) return false;

                        if (rememberMaterial)
                        {
                                if (!string.Equals(a.MaterialName, b.MaterialName, StringComparison.Ordinal))
                                {
                                        return false;
                                }
                        }

                        return true;
                }
	}

	/*─────────────────────────── DATA POCO ──────────────────────────────*/
	[MemoryPackable]
	public partial class ColliderData
	{
		public bool Enabled;
		public bool IsTrigger;
		public string MaterialName;
	}
}
#endif
