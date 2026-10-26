#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	[CreateAssetMenu(fileName = "MigrateSpecificParent_ToNewVersion", menuName = "Crystal Save/Create Migration Actions/Legacy/Migrate Specific Parent")]
	public class MigrateSpecificParent : MigrationAction
	{
		[Header("Target Identification")]
		[Tooltip("Unique Identifier of the RememberParent component to migrate.")]
		public string targetComponentUniqueID;

		[Header("New Parent Information")]
		[Tooltip("Unique Identifier of the new parent GameObject. Leave empty to remove the parent.")]
		public string newParentUniqueID;

		public override void ApplyMigration(SaveData data)
		{
			if (data == null)
			{
				Logger.Log("MigrateSpecificParent: SaveData is null. Migration aborted.", LogLevel.Warning);
				return;
			}

			if (string.IsNullOrEmpty(targetComponentUniqueID))
			{
				Logger.Log("MigrateSpecificParent: targetComponentUniqueID is not set. Migration aborted.", LogLevel.Warning);
				return;
			}

			int matches = ComponentSaveBlobMigrationUtility.RewriteMatchingComponentData(data, targetComponentUniqueID, componentData =>
			{
				// Deserialize the existing ParentData
				ParentData parentData = SaveDataSerializer.Instance.Deserialize<ParentData>(componentData);
				if (parentData != null)
				{
					// Update ParentUniqueID
					string oldParentID = parentData.ParentUniqueID;
					parentData.ParentUniqueID = string.IsNullOrEmpty(newParentUniqueID) ? null : newParentUniqueID;

					Logger.Log($"MigrationAction: Updated ParentUniqueID for '{targetComponentUniqueID}' from '{oldParentID}' to '{parentData.ParentUniqueID ?? "null"}'.", LogLevel.Info);

					// Serialize and save back
					byte[] updatedComponentData = SaveDataSerializer.Instance.Serialize(parentData);
					if (updatedComponentData != null)
					{
						Logger.Log($"MigrationAction: Successfully updated ParentData for UniqueIdentifier '{targetComponentUniqueID}'.", LogLevel.Info);
						return updatedComponentData;
					}

					Logger.Log($"MigrationAction: Failed to serialize updated ParentData for UniqueIdentifier '{targetComponentUniqueID}'.", LogLevel.Error);
					return null;
				}

				Logger.Log($"MigrateSpecificParent: Failed to deserialize ParentData for UniqueIdentifier '{targetComponentUniqueID}'. Migration aborted.", LogLevel.Warning);
				// Optionally, assign default ParentData or handle accordingly
				ParentData defaultParentData = new ParentData
				{
					ParentUniqueID = string.IsNullOrEmpty(newParentUniqueID) ? null : newParentUniqueID
				};

				byte[] defaultComponentData = SaveDataSerializer.Instance.Serialize(defaultParentData);
				if (defaultComponentData != null)
				{
					Logger.Log($"MigrateSpecificParent: Assigned default ParentData for UniqueIdentifier '{targetComponentUniqueID}'.", LogLevel.Info);
					return defaultComponentData;
				}

				Logger.Log($"MigrateSpecificParent: Failed to serialize default ParentData for UniqueIdentifier '{targetComponentUniqueID}'.", LogLevel.Error);
				return null;
			});

			if (matches == 0)
				Logger.Log($"MigrateSpecificParent: No data found for UniqueIdentifier '{targetComponentUniqueID}'. Migration aborted.", LogLevel.Warning);
		}
	}
}
#endif
