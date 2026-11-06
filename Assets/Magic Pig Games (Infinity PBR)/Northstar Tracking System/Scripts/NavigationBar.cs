using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/*
 * This is the "Horizontal Compass Bar", and handles all the logic therein. You can override this class to create your
 * own custom Compass Bar with unique logic and methods.
 */

namespace MagicPigGames.Northstar
{
    [System.Serializable]
    public class NavigationBar : MonoBehaviour
    {
        public static NavigationBar Instance { get; private set; }
        
        [Header("Required")]
        public NorthstarOverlay northstarOverlay;

        [Header("Options")] 
        //public float yPosition = 22f;
        public float visibleAngle = 180f; // Total field of view
        public float fadeAngle = 85f;
        public float fadeAngleEnd = 120f;
        public NorthstarOverlaySettings northstarOverlaySettings;
        
        [Header("Evergreen Icons")]
        public List<GameObject> evergreenObjects = new List<GameObject>();
        public Dictionary<GameObject, NavigationBarEvergreenObject> evergreenIcons = new Dictionary<GameObject, NavigationBarEvergreenObject>();

        public Dictionary<TrackedTargetOverlay, OverlayIcon> screenOverlayIcons = new Dictionary<TrackedTargetOverlay, OverlayIcon>();
        public Dictionary<string, Dictionary<TrackedTargetOverlay, OverlayIcon>> screenOverlayIconsByType = new Dictionary<string, Dictionary<TrackedTargetOverlay, OverlayIcon>>();

        private RectTransform _barTransform;

        public float NorthAngle => GlobalNorthstarSettings.Instance.NorthAngle;
        public float WorldNorth => GlobalNorthstarSettings.Instance.WorldNorth;
        public GameObject OverlayIconPrefab => northstarOverlay.overlayIconPrefab;
        public Camera Camera => northstarOverlay.targetCamera;
        public Transform PlayerTransform => Camera.transform;
        public virtual float PlayerWorldAngle 
        {
            get
            {
                Vector3 direction = PlayerTransform.forward;
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                return (angle + 360) % 360;
            }
        }
        
        public virtual void SetOverrideColor(OverlayIcon overlayIcon, Color value) 
            => overlayIcon.SetOverrideNavigationBarColor(value);
        
        public virtual void ResetOverrideColor(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideColor();
        
        public virtual void SetOverrideCompassBarMaxOpacity(OverlayIcon overlayIcon, float value) 
            => overlayIcon.SetOverrideNavigationBarMaxOpacity(value);
        
        public virtual void ResetOverrideCompassBarMaxOpacity(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideNavigationBarMaxOpacity();
        
        public virtual void SetOverrideCompassBarMaxSize(OverlayIcon overlayIcon, float value) 
            => overlayIcon.SetOverrideNavigationBarMaxSize(value);
        
        public virtual void ResetOverrideCompassBarMaxSize(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideNavigationBarMaxSize();
        
        public virtual void SetOverrideCompassBarSprite(OverlayIcon overlayIcon, Sprite value)
            => overlayIcon.SetOverrideNavigationBarSprite(value);
        
        public virtual void ResetOverrideCompassBarSprite(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideNavigationBarSprite();
        
        public virtual void SetOverrideCompassBarYPosition(OverlayIcon overlayIcon, float value)
            => overlayIcon.SetOverrideNavigationBarYPosition(value);
        
        public virtual void ResetOverrideCompassBarYPosition(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideNavigationBarYPosition();
        
        public virtual void SetOverrideCompassBarXPosition(OverlayIcon overlayIcon, float value)
            => overlayIcon.SetOverrideNavigationBarXPosition(value);
        
        public virtual void ResetOverrideCompassBarXPosition(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideNavigationBarXPosition();
        
        /// <summary>
        /// Override the sprite for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideCompassBarYPosition(string type, float value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideNavigationBarYPosition(value);
        }
        
        /// <summary>
        /// Reset (remove the override) the sprite values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideCompassBarYPosition(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideNavigationBarYPosition();
        }
        
        /// <summary>
        /// Override the sprite for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideCompassBarXPosition(string type, float value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideNavigationBarXPosition(value);
        }
        
        /// <summary>
        /// Reset (remove the override) the sprite values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideCompassBarXPosition(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideNavigationBarXPosition();
        }
        
        /// <summary>
        /// Override the sprite for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideCompassBarSprite(string type, Sprite value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideNavigationBarSprite(value);
        }
        
        /// <summary>
        /// Reset (remove the override) the sprite values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideCompassBarSprite(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideNavigationBarSprite();
        }
        
        /// <summary>
        /// Override the Edge color for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideColor(string type, Color value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideNavigationBarColor(value);
        }
        
        /// <summary>
        /// Reset (remove the override) the edge color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideColor(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideNavigationBarColor();
        }
        
        /// <summary>
        /// Override the Edge color for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideCompassBarMaxOpacity(string type, float value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideNavigationBarMaxOpacity(value);
        }
        
        /// <summary>
        /// Reset (remove the override) the edge color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideCompassBarMaxOpacity(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideNavigationBarMaxOpacity();
        }
        
        /// <summary>
        /// Override the Edge color for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideCompassBarMaxSize(string type, float value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideNavigationBarMaxSize(value);
        }
        
        /// <summary>
        /// Reset (remove the override) the edge color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideCompassBarMaxSize(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideNavigationBarMaxSize();
        }
        
        // This method gets the delta angle based on the PlayerWorldAngle, and a given base angle.
        protected virtual float GetDeltaFromWorldAngle(float worldAngleDiff = 0)
        {
            var delta = PlayerWorldAngle - WorldNorth + worldAngleDiff;
            delta = (delta + 360) % 360;
            if (delta > 180) delta -= 360;
            return -delta;
        }
        
        // The angle at which the player is, compared to world north.
        public virtual float PlayerAngle => NorthstarUtilities.GetAngleBetweenPoints(PlayerTransform.forward, Vector3.forward, WorldNorth);
        
        protected virtual void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("Only one NavigationBar instance can be active at a time!");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _barTransform = GetComponent<RectTransform>();
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        protected virtual void Start() => PopulateEvergreenObjects();
        
        /*
        // Evergreen objects are those that are always on the Navigation Bar -- cardinal directions, tick lines, etc.
        protected virtual void PlaceEvergreenObjects()
        {
            Debug.Log($"Placing evergreen objects {evergreenIcons.Count}");
            foreach (var obj in evergreenIcons)
            {
                var angle = obj.Value.moveWithRotation
                    ? GetDeltaFromWorldAngle(obj.Value.angle)
                    : obj.Value.angle;
                
                PlaceEvergreenObject(obj.Key, angle, obj.Value.clampPositionAtEdges, obj.Value.overrideYPosition, obj.Value.yPos);
                
                if (obj.Value.fadeAtEdges)
                    FadeAtEdges(obj.Key, angle);
            }
        }
        */
        
        /// <summary>
        /// Adds a NorthstarIcon to the compass bar.
        /// </summary>
        /// <param name="overlayIcon"></param>
        public virtual void PlaceObject(OverlayIcon overlayIcon)
        {
            overlayIcon.transform.localPosition = GetPositionOnBar(overlayIcon);
            FadeAtEdges(overlayIcon.Image, overlayIcon.AngleDelta);
        }

        /// <summary>
        /// Add an evergreen object onto the compass bar.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="angle"></param>
        /// <param name="clamp"></param>
        /// <param name="overrideYPos"></param>
        /// <param name="customYPos"></param>
        /// <param name="maxAlpha"></param>
        public virtual void PlaceEvergreenObject(GameObject obj, float angle, bool clamp = true, bool overrideYPos = false,
            float customYPos = -1, float maxAlpha = 1)
        {
            if (obj == null)
                return;
            //Debug.Log($"Placing {obj.name}, overrideYPos: {overrideYPos}, customYPos: {customYPos}");
            obj.transform.localPosition = GetPositionOnBar(angle, clamp, overrideYPos, customYPos);
            FadeAtEdges(obj, angle);
        }

        /// <summary>
        /// Get the angle delta between the player and a given object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public virtual float GetAngleDelta(GameObject obj)
        {
            var direction = obj.transform.position - PlayerTransform.position;
            var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            return (angle + 360) % 360;
        }
        
        /// <summary>
        /// Get the position on the compass bar for a given NorthstarIcon.
        /// </summary>
        /// <param name="overlayIcon"></param>
        /// <returns></returns>
        public virtual Vector2 GetPositionOnBar(OverlayIcon overlayIcon)
        {
            // Normalize the angle delta to a value between -0.5 and 0.5
            var normalizedPosition = overlayIcon.AngleDelta / visibleAngle;

            // Clamp the normalized position to the range [-0.5, 0.5] so objects are not placed beyond the edges of the bar
            // You might want to adjust the range if you don't want objects to appear exactly on the edge
            if (overlayIcon.ClampPositionAtEdges)
                normalizedPosition = Mathf.Clamp(normalizedPosition, -0.5f, 0.5f);

            // Convert normalized position to bar position
            var barPosition = normalizedPosition * _barTransform.rect.width;

            return new Vector2(barPosition, overlayIcon.NavigationBarYPosition);
        }

        /// <summary>
        /// Get the position on the compass bar for a given angle delta, where 0 = straight ahead
        /// </summary>
        /// <param name="angleDelta"></param>
        /// <param name="clamp"></param>
        /// <param name="overrideYPos"></param>
        /// <param name="customYPos"></param>
        /// <returns></returns>
        public virtual Vector2 GetPositionOnBar(float angleDelta, bool clamp = true, bool overrideYPos = false, float customYPos = -1)
        {
            // Normalize the angle delta to a value between -0.5 and 0.5
            var normalizedPosition = angleDelta / visibleAngle;

            // Clamp the normalized position to the range [-0.5, 0.5] so objects are not placed beyond the edges of the bar
            // You might want to adjust the range if you don't want objects to appear exactly on the edge
            if (clamp)
                normalizedPosition = Mathf.Clamp(normalizedPosition, -0.5f, 0.5f);

            // Convert normalized position to bar position
            var barPosition = normalizedPosition * _barTransform.rect.width;

            return new Vector2(barPosition, overrideYPos ? customYPos : northstarOverlaySettings.yPosition);
        }
        
        /// <summary>
        /// Fade an object at the edges of the compass bar.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="angle"></param>
        public virtual void FadeAtEdges(GameObject obj, float angle) => FadeAtEdges(obj.GetComponent<Image>(), angle);

        /// <summary>
        /// Fade an image at the edges of the compass bar.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="angle"></param>
        public virtual void FadeAtEdges(Image image, float angle)
        {
            if (image == null)
                return;
            
            var color = image.color;
            image.color = new Color(color.r, color.g, color.b, ComputeEdgeFade(angle, color.a));
        }
        
        /// <summary>
        /// Determine the fade value for an object at the edges of the compass bar.
        /// </summary>
        /// <param name="angle"></param>
        /// <param name="currentAlpha"></param>
        /// <returns></returns>
        public virtual float ComputeEdgeFade(float angle, float currentAlpha = 1)
        {
            // If the angle is within the complete visibility range, the object is fully visible.
            if (Mathf.Abs(angle) <= fadeAngle)
                return 1f;

            // If the angle is beyond the fading range, the object is fully faded.
            if (Mathf.Abs(angle) >= fadeAngleEnd)
                return 0f;

            // If the angle is inside the fading range, we interpolate the fade value.
            var fadeRangeSize = fadeAngleEnd - fadeAngle;
            var distanceFromStartOfFadeRange = Mathf.Abs(angle) - fadeAngle;
            
            return Mathf.Min(1f - (distanceFromStartOfFadeRange / fadeRangeSize), Mathf.Min(currentAlpha, northstarOverlay.northstarOverlaySettings.navigationBarSpriteOpacity));
        }

        /// <summary>
        /// Populate the evergreen objects on the compass bar.
        /// </summary>
        protected virtual void PopulateEvergreenObjects()
        {
            foreach (var obj in evergreenObjects)
            {
                var newObj = AddOverlay(obj.GetComponent<TrackedTargetOverlay>());
                var image = obj.GetComponent<Image>();
                if (image == null) continue;
                
                var screenOverlayIcon = newObj.GetComponent<OverlayIcon>();
                screenOverlayIcon.overrideNavigationBarSprite = true;
                screenOverlayIcon.overrideNavigationBarSpriteValue = image.sprite;
            }
        }
        
        /// <summary>
        /// Remove a TrackedTargetOverlay from the compass bar if it is registered.
        /// </summary>
        /// <param name="trackedTargetOverlay"></param>
        /// <returns></returns>
        public virtual void RemoveOverlay(TrackedTargetOverlay trackedTargetOverlay)
        {
            if (!screenOverlayIcons.TryGetValue(trackedTargetOverlay, out var overlay))
            {
                Debug.Log($"This TrackedTargetOverlay is not registered with the Northstar Overlay!");
                return;
            }
            
            // If the overlay exists, remove it
            screenOverlayIcons.Remove(trackedTargetOverlay);

            // Also remove it from 'screenOverlayIconsByType' dictionary
            if (screenOverlayIconsByType.ContainsKey(trackedTargetOverlay.targetType))
            {
                screenOverlayIconsByType[trackedTargetOverlay.targetType].Remove(trackedTargetOverlay);

                // If the inner dictionary has become empty, remove it too
                if (!screenOverlayIconsByType[trackedTargetOverlay.targetType].Any())
                    screenOverlayIconsByType.Remove(trackedTargetOverlay.targetType);
            }

            // Finally, destroy the game object
            Destroy(overlay.gameObject);
        }

        /// <summary>
        /// Add a TrackedTargetOverlay to the compass bar. Each one can only be registered once.
        /// </summary>
        /// <param name="trackedTargetOverlay"></param>
        /// <returns></returns>
        public virtual GameObject AddOverlay(TrackedTargetOverlay trackedTargetOverlay)
        {
            if (screenOverlayIcons.TryGetValue(trackedTargetOverlay, out var overlay))
            {
                Debug.Log($"This TrackedTargetOverlay is already registered with the Northstar Overlay!");
                return overlay.gameObject;
            }

            if (OverlayIconPrefab == null)
            {
                Debug.LogError($"Screen Overlay Prefab is null! Please assign a prefab to the Northstar Overlay component.");
                return default;
            }
            
            var prefabToUse = trackedTargetOverlay.customNavigationBarIcon == null 
                ? OverlayIconPrefab 
                : trackedTargetOverlay.customNavigationBarIcon;
            var screenOverlay = Instantiate(prefabToUse, transform);
            
            _sortLayers = true;
            var screenOverlayIcon = screenOverlay.GetComponent<OverlayIcon>();
            if (screenOverlayIcon == null)
            {
                Debug.LogError(
                    $"The instantiated object is missing a ScreenOverlayIcon component! Please add one to the prefab.");
                return default;
            }

            // Setup the icon with either the default or override values
            screenOverlayIcon.SetupNavigationBar(northstarOverlay, trackedTargetOverlay, this);

            // Populate dictionaries -- both full list and by type.
            screenOverlayIcons.Add(trackedTargetOverlay, screenOverlayIcon);

            // Check if the type already exists in the dictionary
            if (screenOverlayIconsByType.ContainsKey(trackedTargetOverlay.targetType))
            {
                // If the type already exists, just add the new TrackedTargetOverlay to its inner dictionary
                screenOverlayIconsByType[trackedTargetOverlay.targetType].Add(trackedTargetOverlay, screenOverlayIcon);
            }
            else
            {
                // If the type does not exist in the dictionary, create and add a new dictionary for the type
                screenOverlayIconsByType.Add(trackedTargetOverlay.targetType,
                    new Dictionary<TrackedTargetOverlay, OverlayIcon>
                        { { trackedTargetOverlay, screenOverlayIcon } });
            }

            if (screenOverlayIcon.arrowImage != null)
                screenOverlayIcon.arrowImage.enabled = false;
            
            return screenOverlay;
        }
        
        protected virtual void LateUpdate() => SortLayers();

        private bool _sortLayers = false;
        protected virtual void SortLayers()
        {
            if (!_sortLayers) return;
            NorthstarUtilities.SortLayers(screenOverlayIcons);
            _sortLayers = false;
        }
        
        /// <summary>
        /// Get the NorthstarIcon for a given TrackedTargetOverlay.
        /// </summary>
        /// <param name="trackedTargetOverlay"></param>
        /// <returns></returns>
        public OverlayIcon GetIcon(TrackedTargetOverlay trackedTargetOverlay) 
            => screenOverlayIcons.TryGetValue(trackedTargetOverlay, out var icon) ? icon : null;
    }
}