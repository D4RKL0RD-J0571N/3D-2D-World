using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*
 * Utility methods for the Northstar system editor scripts.
 */

namespace MagicPigGames.Northstar
{
    public static class NorthstarEditorUtilities
    {
        public static GameObject FindDefaultScreenOverlayPrefab()
        {
            var foundObject = FindGameObjectByName("Screen Overlay Prefab");
            if (foundObject != null)
                return foundObject;

            Debug.LogWarning("Was not able to find the default Screen Overlay Prefab, called \"Screen " +
                             "Overlay Prefab\". Did it get removed or renamed?");
            return default;
        }
        
        public static GameObject FindGameObjectByName(string objectName)
        {
            // Use t:Prefab + name filter, then verify exact file name match to avoid fuzzy collisions.
            return UnityEditor.AssetDatabase.FindAssets($"t:Prefab {objectName}")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Where(path => System.IO.Path.GetFileNameWithoutExtension(path) == objectName)
                .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>)
                .FirstOrDefault(prefab => prefab != null);
        }

        public static Camera FindFirstEnabledCamera()
        {
            // Search all enabled objects in scene for a Camera component that is enabled. Return the first found.
            // FindObjectsSortMode is deprecated in Unity 6.1+, but the sort-mode overload is the only one that
            // exists on the Unity 6.0 floor we support, so suppress the deprecation rather than break 6.0.
#pragma warning disable 618
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.enabled);
#pragma warning restore 618
            foreach (var camera in cameras)
                return camera;

            Debug.LogWarning("Was not able to find the an enabled Camera in the scene!");
            return default;
        }

        public static List<TrackedTargetOverlay> FindAllTrackedTargetOverlays(string targetType = "", bool inProject = true, bool inScene = true)
        {
            var foundList = new List<TrackedTargetOverlay>();
            // Find all in scene
            if (inScene)
            {
#pragma warning disable 618 // FindObjectsSortMode deprecated in Unity 6.1+; required overload on the Unity 6.0 floor
                var foundObjects = Object.FindObjectsByType(typeof(TrackedTargetOverlay), FindObjectsInactive.Include, FindObjectsSortMode.None);
#pragma warning restore 618
                foundList.AddRange(foundObjects.Cast<TrackedTargetOverlay>()
                    .Where(foundObject => targetType == "" || foundObject.targetType == targetType));
            }
            
            // Find all in project
            if (!inProject) return foundList;
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var components = prefab.GetComponentsInChildren<TrackedTargetOverlay>(true);
                foundList.AddRange(components.Where(component => targetType == "" || component.targetType == targetType));
            }

            return foundList;
        }
    }
}