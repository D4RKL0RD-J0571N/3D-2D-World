#if UNITY_EDITOR && REMEMBERME_DISABLED
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Editor
{
	/// <summary>
	/// Automatically adds a <see cref="UniqueID"/> component to every GameObject
	/// that appears in the hierarchy *while the Editor is in Edit-Mode*.
	/// </summary>
	[InitializeOnLoad]
	public static class AutoAddUniqueID
	{
		// Track which objects we already touched this session
		private static readonly System.Collections.Generic.HashSet<GameObject> processedObjects
			= new System.Collections.Generic.HashSet<GameObject>();

		static AutoAddUniqueID()
		{
			// react to hierarchy changes
			EditorApplication.hierarchyChanged += OnHierarchyChanged;

			// initial pass (next frame – after domain reload)
			EditorApplication.delayCall += OnHierarchyChanged;
		}

		private static void OnHierarchyChanged()
		{
			/* ── 1. Bail out during Play-mode ──────────────────── */
			if (EditorApplication.isPlayingOrWillChangePlaymode) return;

			/* ── 2. Feature toggle check ───────────────────────── */
			if (!AutoAddUniqueIDSettings.IsEnabled) return;

			/* ── 3. Scan all roots of the active scene ─────────── */
			foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
				ProcessRecursively(root);
		}

		private static void ProcessRecursively(GameObject obj)
		{
			if (obj == null || processedObjects.Contains(obj)) return;

			if (obj.GetComponent<UniqueID>() == null)
			{
				Undo.AddComponent<UniqueID>(obj);
				Debug.Log($"[Crystal Save] UniqueID: Automatically added to '{obj.name}'.");
			}

			processedObjects.Add(obj);

			foreach (Transform child in obj.transform)
				ProcessRecursively(child.gameObject);
		}
	}
}
#endif
