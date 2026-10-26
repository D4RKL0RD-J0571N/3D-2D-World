#if UNITY_EDITOR && REMEMBERME_DISABLED
using Arawn.CrystalSave.Runtime;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Editor
{
	/// <summary>
	/// Provides a menu item to disable the auto-adding of UniqueID components and remove all existing UniqueID components from all loaded scenes.
	/// </summary>
	public static class DisableAutoAddAndRemoveUniqueIDs
	{
		private const string MenuPath = "Tools/Crystal Save/Scene/Remove UniqueIDs from all GameObjects";

		/// <summary>
		/// Adds a menu item that, when clicked, disables the auto-add feature and removes all UniqueID components from all loaded scenes.
		/// </summary>
		[MenuItem(MenuPath, priority = 1001)] // Priority set to 1001 to place it after the existing Auto Add menu item
		public static void DisableAndRemoveUniqueIDs()
		{
			// Confirm the action with the user
			if (!EditorUtility.DisplayDialog(
				"Disable Auto Add and Remove UniqueIDs",
				"Are you sure you want to disable the auto-adding of UniqueID components and remove all existing UniqueID components from all loaded scenes? This action cannot be undone.",
				"Yes",
				"No"))
			{
				return;
			}

			// Disable the auto-add feature
			AutoAddUniqueIDSettings.IsEnabled = false;
			Debug.Log("[Crystal Save] Auto Add UniqueID has been disabled.");

			// Uncheck the "Auto Add UniqueID" menu item to reflect the disabled state
			Menu.SetChecked("Crystal Save/Scene/Auto Add UniqueID", false);
			Debug.Log("[Crystal Save] 'Auto Add UniqueID' menu item has been unchecked.");

			// Collect all UniqueID components in all loaded scenes
			List<UniqueID> allUniqueIDs = new List<UniqueID>();

			int loadedSceneCount = SceneManager.sceneCount;

			for (int i = 0; i < loadedSceneCount; i++)
			{
				Scene scene = SceneManager.GetSceneAt(i);

				if (scene.isLoaded)
				{
					Debug.Log($"[Crystal Save] Processing Scene: {scene.name}");
					GameObject[] rootGameObjects = scene.GetRootGameObjects();

					foreach (GameObject root in rootGameObjects)
					{
						// Get all UniqueID components in the hierarchy
						UniqueID[] uniqueIDs = root.GetComponentsInChildren<UniqueID>(true);
						Debug.Log($"[Crystal Save] Found {uniqueIDs.Length} UniqueID components in '{root.name}'.");
						allUniqueIDs.AddRange(uniqueIDs);
					}
				}
			}

			Debug.Log($"[Crystal Save] Found {allUniqueIDs.Count} UniqueID components to remove.");

			if (allUniqueIDs.Count == 0)
			{
				EditorUtility.DisplayDialog(
					"No UniqueIDs Found",
					"There are no UniqueID components to remove in the currently loaded scenes.",
					"OK");
				return;
			}

			// Begin an Undo group for all removal operations
			int undoGroup = Undo.GetCurrentGroup();
			Undo.IncrementCurrentGroup();
			Undo.SetCurrentGroupName("Disable Auto Add and Remove UniqueIDs");

			// Remove all UniqueID components with Undo support
			foreach (UniqueID uniqueID in allUniqueIDs)
			{
				if (uniqueID != null)
				{
					string objName = uniqueID.gameObject.name; // Retrieve name before destruction
					Undo.DestroyObjectImmediate(uniqueID);
					Debug.Log($"[Crystal Save] UniqueID: Removed from '{objName}'.");
				}
			}

			// Collapse all Undo operations into a single step
			Undo.CollapseUndoOperations(undoGroup + 1);

			// Optionally, refresh the hierarchy to reflect changes
			EditorApplication.RepaintHierarchyWindow();

			// Notify the user of completion
			EditorUtility.DisplayDialog(
				"Operation Completed",
				$"Auto Add UniqueID has been disabled and all UniqueID components have been removed from all loaded scenes.",
				"OK");
		}
	}
}
#endif
