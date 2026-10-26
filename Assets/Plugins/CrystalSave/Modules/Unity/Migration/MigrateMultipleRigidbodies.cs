#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;
using MemoryPack;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateMultipleRigidbodies", menuName = "Crystal Save/Create Migration Actions/Migrate Multiple Rigidbodies")]
	public class MigrateMultipleRigidbodies : MigrationAction
	{
		[System.Serializable]
		public class RigidbodyMigrationEntry
		{
			[Header("Target Identification")]
			[Tooltip("Unique Identifier of the Rigidbody to migrate.")]
			public string targetUniqueID;

			[Tooltip("Human-friendly name of the target. (Auto-populated from the Source GameObject)")]
			public string targetName;

			[Header("Rigidbody Properties Updates")]
			[Tooltip("If true, update the kinematic state.")]
			public bool updateIsKinematic = false;
			public bool newIsKinematic;

			[Tooltip("If true, update the useGravity setting.")]
			public bool updateUseGravity = false;
			public bool newUseGravity;

			[Tooltip("If true, update the mass.")]
			public bool updateMass = false;
			public float newMass;

			[Tooltip("If true, update the linear drag (damping).")]
			public bool updateDrag = false;
			public float newDrag;

			[Tooltip("If true, update the angular drag (damping).")]
			public bool updateAngularDrag = false;
			public float newAngularDrag;

			[Tooltip("If true, update the Rigidbody's constraints.")]
			public bool updateConstraints = false;
			public RigidbodyConstraints newConstraints;

			[Tooltip("If true, update the linear velocity.")]
			public bool updateVelocity = false;
			public Vector3 newVelocity;

			[Tooltip("If true, update the angular velocity.")]
			public bool updateAngularVelocity = false;
			public Vector3 newAngularVelocity;

			[Tooltip("If true, update the detectCollisions setting.")]
			public bool updateDetectCollisions = false;
			public bool newDetectCollisions;
		}

		[Tooltip("List of Rigidbody migration entries.")]
		public List<RigidbodyMigrationEntry> migrationEntries = new List<RigidbodyMigrationEntry>();

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateMultipleRigidbodies: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}
			if (migrationEntries == null || migrationEntries.Count == 0)
			{
				Logger.Log("MigrateMultipleRigidbodies: No migration entries provided. Nothing to migrate.", LogLevel.Warning);
				return;
			}

			foreach (var entry in migrationEntries)
			{
				if (entry == null)
				{
					Logger.Log("MigrateMultipleRigidbodies: Encountered a null migration entry. Skipping.", LogLevel.Warning);
					continue;
				}
				if (string.IsNullOrEmpty(entry.targetUniqueID))
				{
					Logger.Log("MigrateMultipleRigidbodies: targetUniqueID is not set. Skipping entry.", LogLevel.Warning);
					continue;
				}
				int matches = ComponentSaveBlobMigrationUtility.RewriteMatchingComponentData(data, entry.targetUniqueID, componentData =>
				{
					// Deserialize the stored RememberRigidbodyData.
					RememberRigidbodyData rigidbodyData = SaveDataSerializer.Instance.Deserialize<RememberRigidbodyData>(componentData);
					if (rigidbodyData == null)
					{
						Logger.Log($"MigrateMultipleRigidbodies: Failed to deserialize RememberRigidbodyData for '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
						return null;
					}

					bool dataChanged = false;

					if (entry.updateIsKinematic)
					{
						rigidbodyData.IsKinematic = entry.newIsKinematic;
						dataChanged = true;
						Logger.Log($"MigrateMultipleRigidbodies: Updated IsKinematic for '{entry.targetUniqueID}' to {entry.newIsKinematic}.", LogLevel.Info);
					}
					if (entry.updateUseGravity)
					{
						rigidbodyData.UseGravity = entry.newUseGravity;
						dataChanged = true;
						Logger.Log($"MigrateMultipleRigidbodies: Updated UseGravity for '{entry.targetUniqueID}' to {entry.newUseGravity}.", LogLevel.Info);
					}
					if (entry.updateMass)
					{
						rigidbodyData.Mass = entry.newMass;
						dataChanged = true;
						Logger.Log($"MigrateMultipleRigidbodies: Updated Mass for '{entry.targetUniqueID}' to {entry.newMass}.", LogLevel.Info);
					}
					if (entry.updateDrag)
					{
						rigidbodyData.Drag = entry.newDrag;
						dataChanged = true;
						Logger.Log($"MigrateMultipleRigidbodies: Updated Drag for '{entry.targetUniqueID}' to {entry.newDrag}.", LogLevel.Info);
					}
					if (entry.updateAngularDrag)
					{
						rigidbodyData.AngularDrag = entry.newAngularDrag;
						dataChanged = true;
						Logger.Log($"MigrateMultipleRigidbodies: Updated AngularDrag for '{entry.targetUniqueID}' to {entry.newAngularDrag}.", LogLevel.Info);
					}
					if (entry.updateConstraints)
					{
						rigidbodyData.Constraints = entry.newConstraints;
						dataChanged = true;
						Logger.Log($"MigrateMultipleRigidbodies: Updated Constraints for '{entry.targetUniqueID}' to {entry.newConstraints}.", LogLevel.Info);
					}
					if (entry.updateVelocity)
					{
						rigidbodyData.Velocity = entry.newVelocity;
						dataChanged = true;
						Logger.Log($"MigrateMultipleRigidbodies: Updated Velocity for '{entry.targetUniqueID}' to {entry.newVelocity}.", LogLevel.Info);
					}
					if (entry.updateAngularVelocity)
					{
						rigidbodyData.AngularVelocity = entry.newAngularVelocity;
						dataChanged = true;
						Logger.Log($"MigrateMultipleRigidbodies: Updated AngularVelocity for '{entry.targetUniqueID}' to {entry.newAngularVelocity}.", LogLevel.Info);
					}
					if (entry.updateDetectCollisions)
					{
						rigidbodyData.DetectCollisions = entry.newDetectCollisions;
						dataChanged = true;
						Logger.Log($"MigrateMultipleRigidbodies: Updated DetectCollisions for '{entry.targetUniqueID}' to {entry.newDetectCollisions}.", LogLevel.Info);
					}

					if (dataChanged)
					{
						try
						{
							byte[] updatedData = SaveDataSerializer.Instance.Serialize(rigidbodyData);
							if (updatedData != null)
							{
								Logger.Log($"MigrateMultipleRigidbodies: Successfully updated RememberRigidbodyData for '{entry.targetUniqueID}'.", LogLevel.Info);
								return updatedData;
							}

							Logger.Log($"MigrateMultipleRigidbodies: Serialization returned null for '{entry.targetUniqueID}'.", LogLevel.Error);
						}
						catch (Exception ex)
						{
							Logger.Log($"MigrateMultipleRigidbodies: Exception during serialization for '{entry.targetUniqueID}': {ex.Message}\nStack Trace: {ex.StackTrace}", LogLevel.Error);
						}

						return null;
					}

					Logger.Log($"MigrateMultipleRigidbodies: No changes applied for '{entry.targetUniqueID}'.", LogLevel.Info);
					return null;
				});

				if (matches == 0)
					Logger.Log($"MigrateMultipleRigidbodies: No data found for UniqueIdentifier '{entry.targetUniqueID}'. Skipping entry.", LogLevel.Warning);
			}
		}
	}
}
#endif
