#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateMultipleParents", menuName = "Crystal Save/Create Migration Actions/Migrate Multiple Parents")]
	public class MigrateMultipleParents : MigrationAction
	{
		[System.Serializable]
		public class ParentMigrationEntry
		{
			[Tooltip("Human-friendly name for the target (auto-populated).")]
			public string targetName;

			[Header("Target Identification")]
			[Tooltip("Unique Identifier of the RememberParent component to migrate.")]
			public string targetComponentUniqueID;

			[Header("New Parent Information")]
			[Tooltip("Unique Identifier of the new parent GameObject. Leave empty to remove the parent.")]
			public string newParentUniqueID;
		}

		[Tooltip("List of parent migration entries for all RememberParent components that need updating.")]
		public List<ParentMigrationEntry> migrationEntries = new List<ParentMigrationEntry>();

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateMultipleParents: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}

			if (migrationEntries == null || migrationEntries.Count == 0)
			{
				Logger.Log("MigrateMultipleParents: No migration entries provided. Nothing to migrate.", LogLevel.Warning);
				return;
			}

			foreach (var entry in migrationEntries)
			{
				if (entry == null)
				{
					Logger.Log("MigrateMultipleParents: Encountered a null migration entry. Skipping.", LogLevel.Warning);
					continue;
				}
				if (string.IsNullOrEmpty(entry.targetComponentUniqueID))
				{
					Logger.Log("MigrateMultipleParents: targetComponentUniqueID is not set for one of the entries. Skipping entry.", LogLevel.Warning);
					continue;
				}

				int matches = ComponentSaveBlobMigrationUtility.RewriteMatchingComponentData(data, entry.targetComponentUniqueID, componentData =>
				{
					// Attempt to deserialize ParentData.
					ParentData parentData = SaveDataSerializer.Instance.Deserialize<ParentData>(componentData);
					if (parentData != null)
					{
						string oldParentID = parentData.ParentUniqueID;
						parentData.ParentUniqueID = string.IsNullOrEmpty(entry.newParentUniqueID) ? null : entry.newParentUniqueID;

						Logger.Log($"MigrationAction: Updated ParentUniqueID for '{entry.targetComponentUniqueID}' from '{oldParentID}' to '{parentData.ParentUniqueID ?? "null"}'.", LogLevel.Info);

						// Serialize and save back.
						byte[] updatedComponentData = SaveDataSerializer.Instance.Serialize(parentData);
						if (updatedComponentData != null)
						{
							Logger.Log($"MigrationAction: Successfully updated ParentData for UniqueIdentifier '{entry.targetComponentUniqueID}'.", LogLevel.Info);
							return updatedComponentData;
						}

						Logger.Log($"MigrationAction: Failed to serialize updated ParentData for UniqueIdentifier '{entry.targetComponentUniqueID}'.", LogLevel.Error);
						return null;
					}

					Logger.Log($"MigrateMultipleParents: Failed to deserialize ParentData for UniqueIdentifier '{entry.targetComponentUniqueID}'. Attempting default assignment.", LogLevel.Warning);
					// Create default ParentData in case of deserialization failure.
					ParentData defaultParentData = new ParentData
					{
						ParentUniqueID = string.IsNullOrEmpty(entry.newParentUniqueID) ? null : entry.newParentUniqueID
					};

					byte[] defaultComponentData = SaveDataSerializer.Instance.Serialize(defaultParentData);
					if (defaultComponentData != null)
					{
						Logger.Log($"MigrationAction: Assigned default ParentData for UniqueIdentifier '{entry.targetComponentUniqueID}'.", LogLevel.Info);
						return defaultComponentData;
					}

					Logger.Log($"MigrationAction: Failed to serialize default ParentData for UniqueIdentifier '{entry.targetComponentUniqueID}'.", LogLevel.Error);
					return null;
				});

				if (matches == 0)
					Logger.Log($"MigrateMultipleParents: No data found for UniqueIdentifier '{entry.targetComponentUniqueID}'. Skipping entry.", LogLevel.Warning);
			}
		}
	}
}
#endif
