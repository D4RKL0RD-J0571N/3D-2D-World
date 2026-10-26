#if UNITY_EDITOR && REMEMBERME_DISABLED
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	public static class AutoAddUniqueIDSettings
	{
		private const string MenuPath = "Tools/Crystal Save/Scene/Auto Add UniqueID";
		private const string EditorPrefKey = "Tools/RememberMe_AutoAddUniqueID";

		// Property to get/set the toggle state
		public static bool IsEnabled
		{
			get => EditorPrefs.GetBool(EditorPrefKey, false); // Default is false
			set => EditorPrefs.SetBool(EditorPrefKey, value);
		}

		// Initialize the menu item
		[MenuItem(MenuPath, true)]
		public static bool ToggleAutoAddValidator()
		{
			// This method is used to set the checked state of the menu item
			Menu.SetChecked(MenuPath, IsEnabled);
			return true;
		}

		// The method that gets called when the menu item is clicked
		[MenuItem(MenuPath)]
		public static void ToggleAutoAdd()
		{
			IsEnabled = !IsEnabled;
			Menu.SetChecked(MenuPath, IsEnabled);
			Debug.Log($"[Crystal Save] Auto Add UniqueID is now {(IsEnabled ? "Enabled" : "Disabled")}.");
		}
	}
}
#endif