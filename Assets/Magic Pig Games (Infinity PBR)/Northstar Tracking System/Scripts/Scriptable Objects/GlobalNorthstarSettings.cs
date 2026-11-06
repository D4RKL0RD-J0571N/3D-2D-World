using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/*
 * This is a static Scriptable Object which holds the global settings that aren't intended to change for your entire
 * project.
 */

namespace MagicPigGames.Northstar
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "GlobalNorthstarSettings", menuName = "Northstar/Global Settings")]
    public class GlobalNorthstarSettings : ScriptableObject
    {
        public float northAngle;
        public float worldNorth;
        
        public List<TrackedTargetOverlay> trackedTargetOverlayPrefabs = new List<TrackedTargetOverlay>();

        public float NorthAngle { 
            get => northAngle;
            set => northAngle = Mathf.Clamp(value, -180f, 180f);
        }

        public float WorldNorth {
            get => worldNorth;
            set => worldNorth = Mathf.Clamp(value, 0f, 359.9f);
        }

        public enum NorthstarPauseLevel
        {
            FullPause,
            PauseRotation,
            PauseMovement,
            Unpaused
        }

        public enum TrackingType
        {
            PointNorth,
            PointAtTarget,
            RelativePoint,
            DoNotMove,
            PlayerForward
        }

        // Singleton instance property
        public static GlobalNorthstarSettings Instance { get; private set; }

        // Called from NorthstarSystem.Awake() to guarantee the instance is set in builds
        // even if the SO's OnEnable has not yet fired (e.g. no direct scene reference).
        public static void Initialize(GlobalNorthstarSettings settings)
        {
            if (settings != null) Instance = settings;
        }

        private void OnEnable() => Instance = this;
        
        public void AddTrackedTargetOverlayPrefab(TrackedTargetOverlay prefab)
        {
            trackedTargetOverlayPrefabs.RemoveAll(x => x == null);
            
            if (trackedTargetOverlayPrefabs.Contains(prefab))return;
            trackedTargetOverlayPrefabs.Add(prefab);
        }
        
        public void RemoveTrackedTargetOverlayPrefab(TrackedTargetOverlay prefab) 
            => trackedTargetOverlayPrefabs.Remove(prefab);

        public List<TrackedTargetOverlay> TrackedTargetOverlayPrefabsByType(string targetType) 
            => trackedTargetOverlayPrefabs
                .Where(x => x.targetType == targetType)
                .ToList();
    }
}