using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/*
 * Northstar Overlay is an icon that is displayed on the screen (over the target), at the edge of the screen (pointing
 * toward a target), or on the horizontal compass bar (indicating the direction to a target).
 *
 * You can override this to create custom functionality.
 */

namespace MagicPigGames.Northstar
{
    [System.Serializable]
    public class NorthstarOverlay : MonoBehaviour
    {
        public static NorthstarOverlay Instance { get; private set; }
        
        public Dictionary<TrackedTargetOverlay, OverlayIcon> screenOverlayIcons = new Dictionary<TrackedTargetOverlay, OverlayIcon>();
        public Dictionary<string, Dictionary<TrackedTargetOverlay, OverlayIcon>> screenOverlayIconsByType = new Dictionary<string, Dictionary<TrackedTargetOverlay, OverlayIcon>>();

        public NorthstarOverlaySettings northstarOverlaySettings;
        public Camera targetCamera;
        public Vector3 TargetCameraPosition => targetCamera.transform.position;
        public GameObject overlayIconPrefab;
        public GameObject edgeOverlayPrefab;
        public NavigationBar navigationBar;
        
        // These fields are not read at runtime — OverlayIcon reads everything from NorthstarOverlaySettings.
        // Kept for serialization compatibility; hidden from the inspector to prevent silent no-op edits.
        [HideInInspector] public Sprite defaultArrowSprite;
        [HideInInspector] public int defaultOffset = 5;
        [HideInInspector] public int defaultArrowSize = 30;
        [HideInInspector] public int defaultArrowSizeInitial = 10;
        [HideInInspector] public int defaultIconOffsetFromArrow = 10;
        [HideInInspector] public bool rotateArrow = true;

        // Enablement — these ARE read at runtime
        public bool enableScreenOverlay = true;
        public bool enableEdgeOverlay = false;
        [FormerlySerializedAs("enableCompassBar")] public bool enableNavigationBar = false;

        // Dead fields below — kept for serialization compatibility only
        [HideInInspector] public Sprite overlaySprite;
        [HideInInspector] public Color screenSpriteColor = Color.white;
        [HideInInspector] public float screenDistanceMin = 0f;
        [HideInInspector] public float screenDistanceMax = 200f;
        [HideInInspector] public int screenSpriteSize = 100;
        [HideInInspector] public bool overlaySpriteSizeUseCurve = true;
        [HideInInspector] public AnimationCurve overlaySpriteSizeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [HideInInspector] public float screenSpriteOpacity = 1f;
        [HideInInspector] public bool overlaySpriteOpacityUseCurve = true;
        [HideInInspector] public AnimationCurve overlaySpriteOpacityCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [HideInInspector] public Sprite edgeSprite;
        [HideInInspector] public Color edgeSpriteColor = Color.white;
        [HideInInspector] public Color arrowColor = Color.white;
        [HideInInspector] public Color arrowColorInitial = Color.white;
        [HideInInspector] public float edgeDistanceMin = 0f;
        [HideInInspector] public float edgeDistanceMax = 200f;
        [HideInInspector] public int edgeSpriteSize = 100;
        [HideInInspector] public bool edgeSpriteSizeUseCurve = true;
        [HideInInspector] public AnimationCurve edgeSpriteSizeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [HideInInspector] public float edgeSpriteOpacity = 1f;
        [HideInInspector] public bool edgeSpriteOpacityUseCurve = true;
        [HideInInspector] public AnimationCurve edgeSpriteOpacityCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [HideInInspector] public bool arrowSizeUseCurve = false;
        [HideInInspector] public AnimationCurve arrowSizeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [HideInInspector] public float arrowOpacity = 1f;
        [HideInInspector] public float arrowOpacityInitial = 0f;
        [HideInInspector] public bool arrowOpacityUseCurve = true;
        [HideInInspector] public AnimationCurve arrowOpacityCurve = AnimationCurve.Linear(0, 1, 1, 0);
        
        /*
         * OVERRIDE METHODS
         * These methods can be used to override all of the icons, all of a
         * specific type, or a a single icon. Don't forget you can also reset the
         * overrides if you'd like!
         */
        
        // Individual
        public virtual void SetOverrideMaxOpacity(OverlayIcon overlayIcon, float value) 
            => overlayIcon.SetOverrideMaxOpacity(value);
        public virtual void SetOverrideMaxSize(OverlayIcon overlayIcon, float value) 
            => overlayIcon.SetOverrideMaxSize(value);

        public virtual void SetOverrideColor(OverlayIcon overlayIcon, Color value)
            => overlayIcon.SetOverrideColor(value);
        public virtual void SetOverrideEdgeColor(OverlayIcon overlayIcon, Color value) 
            => overlayIcon.SetOverrideEdgeColor(value);
        public virtual void SetOverrideScreenColor(OverlayIcon overlayIcon, Color value) 
            => overlayIcon.SetOverrideScreenColor(value);
        
        public virtual void SetOverrideArrowColor(OverlayIcon overlayIcon, Color value) 
            => overlayIcon.SetOverrideArrowColor(value);
        public virtual void SetOverrideArrowColorInitial(OverlayIcon overlayIcon, Color value) 
            => overlayIcon.SetOverrideArrowColorInitial(value);
        
        public virtual void SetOverrideSprite(OverlayIcon overlayIcon, Sprite value)
            => overlayIcon.SetOverrideSprite(value);
        
        public virtual void SetOverrideDistanceMin(OverlayIcon overlayIcon, float value) 
            => overlayIcon.SetOverrideDistanceMin(value);
        public virtual void SetOverrideDistanceMax(OverlayIcon overlayIcon, float value) 
            => overlayIcon.SetOverrideDistanceMax(value);
        public virtual void SetOverrideScreenDistanceMin(OverlayIcon overlayIcon, float value) 
            => overlayIcon.SetOverrideScreenDistanceMin(value);
        public virtual void SetOverrideScreenDistanceMax(OverlayIcon overlayIcon, float value) 
            => overlayIcon.SetOverrideScreenDistanceMax(value);
        public virtual void SetOverrideEdgeDistanceMin(OverlayIcon overlayIcon, float value) 
            => overlayIcon.SetOverrideEdgeDistanceMin(value);
        public virtual void SetOverrideEdgeDistanceMax(OverlayIcon overlayIcon, float value) 
            => overlayIcon.SetOverrideEdgeDistanceMax(value);
        
        public virtual void ResetOverrideMaxOpacity(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideMaxOpacity();
        public virtual void ResetOverrideMaxSize(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideMaxSize();
        
        public virtual void ResetOverrideColor(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideColor();
        public virtual void ResetOverrideScreenColor(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideScreenColor();
        public virtual void ResetOverrideEdgeColor(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideEdgeColor();
        
        public virtual void ResetOverrideArrowColor(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideArrowColor();
        public virtual void ResetOverrideArrowColorInitial(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideArrowColorInitial();
        
        public virtual void ResetOverrideSprite(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideSprite();
        
        public virtual void ResetOverrideDistanceMin(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideDistanceMin();
        public virtual void ResetOverrideDistanceMax(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideDistanceMax();
        public virtual void ResetOverrideScreenDistanceMin(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideScreenDistanceMin();
        public virtual void ResetOverrideScreenDistanceMax(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideScreenDistanceMax();
        public virtual void ResetOverrideEdgeDistanceMin(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideEdgeDistanceMin();
        public virtual void ResetOverrideEdgeDistanceMax(OverlayIcon overlayIcon) 
            => overlayIcon.ResetOverrideEdgeDistanceMax();
        
        /// <summary>
        /// Override the maximum opacity for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideMaxOpacity(string type, float value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideMaxOpacity(value);
        }

        /// <summary>
        /// Override the maximum size for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideMaxSize(string type, float value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideMaxSize(value);
        }

        /// <summary>
        /// Override the color for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideColor(string type, Color value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideColor(value);
        }
        
        /// <summary>
        /// Override the color for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideScreenColor(string type, Color value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideScreenColor(value);
        }
        
        /// <summary>
        /// Override the Edge color for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideEdgeColor(string type, Color value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideEdgeColor(value);
        }
        
        /// <summary>
        /// Override the color for all arrows (Edge icon) of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideArrowColor(string type, Color value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideArrowColor(value);
        }
        
        /// <summary>
        /// Override the initial color for all arrows (Edge icon) of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideArrowColorInitial(string type, Color value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideArrowColorInitial(value);
        }

        /// <summary>
        /// Override the sprite for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideSprite(string type, Sprite value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideSprite(value);
        }

        /// <summary>
        /// Override the minimum distance for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideScreenDistanceMin(string type, float value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideScreenDistanceMin(value);
        }

        /// <summary>
        /// Override the maximum distance for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void SetOverrideScreenDistanceMax(string type, float value)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.SetOverrideScreenDistanceMax(value);
        }

        /// <summary>
        /// Reset (remove the override) the maximum opacity values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideMaxOpacity(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideMaxOpacity();
        }

        /// <summary>
        /// Reset (remove the override) the maximum size values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideMaxSize(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideMaxSize();
        }

        /// <summary>
        /// Reset (remove the override) the color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideColor(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideColor();
        }
        
        /// <summary>
        /// Reset (remove the override) the color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideScreenColor(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideScreenColor();
        }
        
        /// <summary>
        /// Reset (remove the override) the edge color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideEdgeColor(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideEdgeColor();
        }
        
        /// <summary>
        /// Reset (remove the override) the edge color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideArrowColor(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideArrowColor();
        }
        
        /// <summary>
        /// Reset (remove the override) the edge color values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideArrowColorInitial(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideArrowColorInitial();
        }

        /// <summary>
        /// Reset (remove the override) the sprite values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideSprite(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideSprite();
        }

        /// <summary>
        /// Reset (remove the override) the minimum distance values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideScreenDistanceMin(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideScreenDistanceMin();
        }

        /// <summary>
        /// Reset (remove the override) the maximum distance values for all icons of a specific type.
        /// </summary>
        /// <param name="type"></param>
        public virtual void ResetOverrideScreenDistanceMax(string type)
        {
            if (!screenOverlayIconsByType.TryGetValue(type, out var iconsForType)) return;
            foreach (var screenOverlayIcon in iconsForType.Values)
                screenOverlayIcon.ResetOverrideScreenDistanceMax();
        }
        
        protected virtual void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("Only one NorthstarOverlay instance can be active at a time!");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (enableNavigationBar && navigationBar == null)
            {
                Debug.LogError("NorthstarCompassBar is enabled but the component is missing!");
                enableNavigationBar = false;
            }

            if (enableNavigationBar && navigationBar != null)
                navigationBar.gameObject.SetActive(enableNavigationBar && navigationBar != null);
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Remove a TrackedTargetOverlay from the Northstar Overlay.
        /// </summary>
        /// <param name="trackedTargetOverlay"></param>
        public virtual void RemoveTarget(TrackedTargetOverlay trackedTargetOverlay)
        {
            if (!screenOverlayIcons.ContainsKey(trackedTargetOverlay))
            {
                Debug.Log($"Tried to remove a TrackedTargetOverlay that is not registered with the Northstar Overlay!");
                return;
            }
            
            var screenOverlayIcon = screenOverlayIcons[trackedTargetOverlay];
            screenOverlayIcons.Remove(trackedTargetOverlay);

            // targetType is a public string that may have changed since registration -- if it's not
            // under the current type, scan all types so the entry can't leak.
            if (!screenOverlayIconsByType.TryGetValue(trackedTargetOverlay.targetType, out var iconsOfType)
                || !iconsOfType.Remove(trackedTargetOverlay))
            {
                foreach (var icons in screenOverlayIconsByType.Values)
                {
                    if (icons.Remove(trackedTargetOverlay))
                        break;
                }
            }
            
            // Remove registration with Compass Bar if it's populated and we're enabling it
            if (navigationBar != null && trackedTargetOverlay.EnableNavigationBar(enableNavigationBar))
                navigationBar.RemoveOverlay(trackedTargetOverlay);
            
            if (screenOverlayIcon != null)
                Destroy(screenOverlayIcon.gameObject);
        }
        
        // This should only be called if both the Northstar Overlay and the TrackedTargetOverlay have
        // Screen Overlay enabled.
        public virtual OverlayIcon AddScreenOverlay(TrackedTargetOverlay trackedTargetOverlay)
        {
            if (screenOverlayIcons.TryGetValue(trackedTargetOverlay, out var overlay))
            {
                Debug.Log($"This TrackedTargetOverlay is already registered with the Northstar Overlay!");
                return overlay;
            }

            if (overlayIconPrefab == null)
            {
                Debug.LogError(
                    $"Screen Overlay Prefab is null! Please assign a prefab to the Northstar Overlay component.");
                return default;
            }
            
            var prefabToUse = trackedTargetOverlay.customScreenIcon == null 
                ? overlayIconPrefab 
                : trackedTargetOverlay.customScreenIcon;
            var screenOverlay = Instantiate(prefabToUse, transform);

            _sortLayers = true;
            var screenOverlayIcon = screenOverlay.GetComponent<OverlayIcon>();
            if (screenOverlayIcon == null)
            {
                Debug.LogError(
                    $"The instantiated object is missing a ScreenOverlayIcon component! Please add one to the prefab.");
                Destroy(screenOverlay);
                return null;
            }

            // Setup the icon with either the default or override values
            screenOverlayIcon.Setup(this, trackedTargetOverlay);

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
            
            // Register with Compass Bar if it's populated and we're enabling it
            if (navigationBar != null && trackedTargetOverlay.EnableNavigationBar(enableNavigationBar))
                navigationBar.AddOverlay(trackedTargetOverlay);

            return screenOverlayIcon;
        }

        protected virtual void LateUpdate() => SortLayers();

        private bool _sortLayers = false;
        protected virtual void SortLayers()
        {
            if (!_sortLayers) return;
            NorthstarUtilities.SortLayers(screenOverlayIcons);
            _sortLayers = false;
        }

        public OverlayIcon GetIcon(TrackedTargetOverlay trackedTargetOverlay) 
            => screenOverlayIcons.TryGetValue(trackedTargetOverlay, out var icon) ? icon : null;

        public void SetNavigationBar(NavigationBar newNavigationBar)
        {
            navigationBar = newNavigationBar;
        }

        public void SetNorthstarOverlaySettings(NorthstarOverlaySettings newSettings) => northstarOverlaySettings = newSettings;
    }
}