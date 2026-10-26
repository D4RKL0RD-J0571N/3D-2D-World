using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using static MagicPigGames.Northstar.GlobalNorthstarSettings;

/*
 * This is the parent class for the Radar and Compass. You can inherit from it and create your own objects with
 * similar logic, but customized.
 */

namespace MagicPigGames.Northstar
{
    public abstract class NorthstarSystem : MonoBehaviour
    {
        [Header("Required")] 
        [Tooltip("This is the center point of the compass, generally the player object.")]
        public Transform player;
        [Tooltip("The default tracker object for compass and radar")]
        public GameObject defaultTrackedIcon;

        [Header("Distance to Target")] 
        public float detectionRange = 200;
        public Vector2 rangeFromCenter = new Vector2(10, 100);
        [FormerlySerializedAs("compassDistanceRange")] public Vector2 objectDistanceRange = new Vector2(0, 50);
        public bool fadeOnDistance = true;
        public Vector2 maxDistanceFadeRange = new Vector2(50, 60);
        public bool fadeOnYDelta = true;
        public Vector2 maxYDeltaFadeRangeAbove = new Vector2(10, 50);
        public Vector2 maxYDeltaFadeRangeBelow = new Vector2(-50, -10);
        
        
       [Header("Active Objects - Set at Edit Time")]
        public List<CompassIcon> presetTrackedObjects = new List<CompassIcon>();
        protected readonly HashSet<CompassIcon> _presetTrackedObjectsSet = new HashSet<CompassIcon>();

        // This holds all the active objects
        public Dictionary<Transform, CompassIcon> CompassIcons { get; } = new Dictionary<Transform, CompassIcon>();
        public Dictionary<string, Dictionary<TrackedTargetCompassRadar, CompassIcon>> trackedIconsByType = new Dictionary<string, Dictionary<TrackedTargetCompassRadar, CompassIcon>>();

        public GlobalNorthstarSettings northstarSettings;
        public GlobalNorthstarSettings NorthstarSettings => northstarSettings;
        
        public NorthstarPauseLevel PauseLevel = NorthstarPauseLevel.Unpaused;

        public CompassIcon PlayerIcon => GetPlayerIcon();
        private CompassIcon _playerIcon;

        private CompassIcon GetPlayerIcon()
        {
            if (_playerIcon != null)
                return _playerIcon;

            foreach (var pair in CompassIcons)
            {
                if (pair.Value.trackingType != TrackingType.PlayerForward) continue;
                _playerIcon = pair.Value;
                return _playerIcon;
            }

            return default;
        }

        public static NorthstarSystem Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("Only one Northstar System instance can be active at a time!");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            GlobalNorthstarSettings.Initialize(northstarSettings);
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected virtual void SetDictionary()
        {
            foreach (var trackedObject in presetTrackedObjects)
            {
                TrackObject(trackedObject);
                _presetTrackedObjectsSet.Add(trackedObject);
            }
        }
        
        protected virtual bool TrackObject(CompassIcon compassIcon)
        {
            if (CompassIcons.ContainsKey(compassIcon.target))
                return false;
            CompassIcons.Add(compassIcon.target, compassIcon);
            return true;
        }

        /// <summary>
        /// Add a target to the compass panel. Use the targetObject on the compass, or pass in a custom object. Will set
        /// the target object to follow the target transform provided. Will return the GameObject created.
        /// </summary>
        /// <param name="target">The target we are following</param>
        /// <param name="trackerPrefab">The compass target object (prefab) to use. Optional.</param>
        /// <param name="trackingType"></param>
        public CompassIcon AddTarget(TrackedTargetCompassRadar trackedTargetCompassRadar)  //Transform target, GameObject trackerPrefab = null, TrackingType trackingType = TrackingType.RelativePoint)
        {
            // Already tracked -- don't instantiate a second icon for the same target.
            if (CompassIcons.ContainsKey(trackedTargetCompassRadar.transform))
                return null;

            var newObj = Instantiate(trackedTargetCompassRadar.trackedObjectPrefab == null ? defaultTrackedIcon : trackedTargetCompassRadar.trackedObjectPrefab, transform);
            var trackedIcon = newObj.GetComponent<CompassIcon>();
            if (trackedIcon == null)
            {
                Debug.LogError($"The tracked icon prefab used for {trackedTargetCompassRadar.name} has no CompassIcon component!");
                Destroy(newObj);
                return null;
            }
            trackedIcon.target = trackedTargetCompassRadar.transform;
            trackedIcon.trackingType = trackedTargetCompassRadar.trackingType;
            trackedIcon.adjustBasedOnPlayerIcon = trackedTargetCompassRadar.adjustBasedOnPlayerIcon;
            
            // Check if the type already exists in the dictionary
            if (trackedIconsByType.ContainsKey(trackedTargetCompassRadar.targetType))
            {
                // If the type already exists, just add the new TrackedTargetOverlay to its inner dictionary, but first
                // make sure the tracked object is not already there.
                if (!trackedIconsByType[trackedTargetCompassRadar.targetType].ContainsKey(trackedTargetCompassRadar))
                    trackedIconsByType[trackedTargetCompassRadar.targetType].Add(trackedTargetCompassRadar, trackedIcon);
            }
            else
            {
                // If the type does not exist in the dictionary, create and add a new dictionary for the type
                trackedIconsByType.Add(trackedTargetCompassRadar.targetType,
                    new Dictionary<TrackedTargetCompassRadar, CompassIcon>
                        { { trackedTargetCompassRadar, trackedIcon } });
            }
            
            if (!TrackObject(trackedIcon))
            {
                RemoveIconFromTypeDictionary(trackedIcon);
                Destroy(newObj);
                return null;
            }

            return trackedIcon;
        }

        /// <summary>
        /// Removes a target transform from the list. Will also destroy the tracking UI element.
        /// </summary>
        /// <param name="target"></param>
        public virtual void RemoveTarget(Transform target){
            if (!CompassIcons.TryGetValue(target, out var trackedObject))
                return;
            presetTrackedObjects.Remove(trackedObject);
            CompassIcons.Remove(target);
            RemoveIconFromTypeDictionary(trackedObject);
            Destroy(trackedObject.gameObject);
        }

        // Removes the icon from trackedIconsByType regardless of which type it was registered under,
        // so the by-type override APIs never iterate destroyed icons.
        protected virtual void RemoveIconFromTypeDictionary(CompassIcon icon)
        {
            foreach (var iconsOfType in trackedIconsByType.Values)
            {
                TrackedTargetCompassRadar foundKey = null;
                foreach (var pair in iconsOfType)
                {
                    if (pair.Value != icon) continue;
                    foundKey = pair.Key;
                    break;
                }

                if (foundKey != null)
                    iconsOfType.Remove(foundKey);
            }
        }
        
        public virtual void SetOverrideColor(CompassIcon compassIcon, Color value) 
            => compassIcon.SetOverrideColor(value);
        public virtual void ResetOverrideColor(CompassIcon compassIcon) 
            => compassIcon.ResetOverrideColor();
        
        /// <summary>
        /// Override the Edge color for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideColor(string type, Color value)
        {
            if (!trackedIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideColor(value);
        }
        
        /// <summary>
        /// Reset (remove the override) the edge color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideColor(string type)
        {
            if (!trackedIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideColor();
        }
        
        public virtual void SetOverrideOpacity(CompassIcon compassIcon, float value) 
            => compassIcon.SetOverrideOpacity(value);
        public virtual void ResetOverrideOpacity(CompassIcon compassIcon) 
            => compassIcon.ResetOverrideOpacity();
        
        /// <summary>
        /// Override the Edge color for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideOpacity(string type, float value)
        {
            if (!trackedIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideOpacity(value);
        }
        
        /// <summary>
        /// Reset (remove the override) the edge color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideOpacity(string type)
        {
            if (!trackedIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideOpacity();
        }
        
        public virtual void SetOverrideScale(CompassIcon compassIcon, Vector3 value) 
            => compassIcon.SetOverrideScale(value);
        public virtual void ResetOverrideScale(CompassIcon compassIcon) 
            => compassIcon.ResetOverrideScale();
        
        /// <summary>
        /// Override the Edge color for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideScale(string type, Vector3 value)
        {
            if (!trackedIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideScale(value);
        }
        
        /// <summary>
        /// Reset (remove the override) the edge color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideScale(string type)
        {
            if (!trackedIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideScale();
        }
    }
}