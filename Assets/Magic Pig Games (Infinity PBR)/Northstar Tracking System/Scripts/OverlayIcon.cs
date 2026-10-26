using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static UnityEngine.Screen;

/*
 * The NorthstarIcon is the base class for all icons which are displayed on the compass bar and screen overlay. You
 * can override this and create your own custom icons.
 */

namespace MagicPigGames.Northstar
{
    [System.Serializable]
    public class OverlayIcon : MonoBehaviour
    {
        public bool showOnEdge = true; // show the icon on the edge of the screen if it's off screen

        public float PercentToEdge => _offsetEffect; // 0 = fully on screen, 1 = fully on edge. Otherwise, transitioning.
        
        public List<DistanceCheck> distanceChecks = new List<DistanceCheck>();
        protected RectTransform _rectTransform;
        public RectTransform RectTransform => _rectTransform;
        protected Image _image;
        public Image Image => _image;
        protected NorthstarOverlay _northstarOverlay;
        public NorthstarOverlay NorthstarOverlay => _northstarOverlay;
        protected TrackedTargetOverlay _trackedTargetOverlay;
        public TrackedTargetOverlay TrackedTargetOverlay => _trackedTargetOverlay;
        protected NavigationBar navigationBar;
        public NavigationBar NavigationBar => navigationBar;

        private bool _isNavigationBarIcon = false;
        public bool IsNavigationBarIcon => _isNavigationBarIcon;
        
        private NorthstarOverlaySettings DefaultSettings => _northstarOverlay.northstarOverlaySettings;
        private NorthstarOverlaySettings TargetSettings 
            => _trackedTargetOverlay.overlaySettings == null 
            ? _northstarOverlay.northstarOverlaySettings 
            : _trackedTargetOverlay.overlaySettings;
       
        public bool ShowScreenOverlay => _trackedTargetOverlay.overrideScreenOverlay 
            ? TargetSettings.enableScreenOverlay 
            : _northstarOverlay.enableScreenOverlay;
        
        public bool ShowEdgeOverlay => _trackedTargetOverlay.overrideEdgeOverlay 
            ? TargetSettings.enableEdgeOverlay 
            : _northstarOverlay.enableEdgeOverlay;
        
        public bool ShowNavigationBarOverlay => _trackedTargetOverlay.overrideNavigationBar 
            ? TargetSettings.enableNavigationBar 
            : _northstarOverlay.enableNavigationBar;

        // Overlay Main
        public Sprite overlaySprite;
        public Color overlaySpriteColor = Color.white;
        public float overlayDistanceMin = 0f;
        public float overlayDistanceMax = 200f;

        // Overlay Sprite Size
        public int overlaySpriteSize = 100;
        public bool overlaySpriteSizeUseCurve = true;
        public AnimationCurve overlaySpriteSizeCurve = AnimationCurve.Linear(0, 1, 1, 0);

        // Overlay Sprite Opacity
        public float overlaySpriteOpacity = 1f;
        public bool overlaySpriteOpacityUseCurve = true;
        public AnimationCurve overlaySpriteOpacityCurve = AnimationCurve.Linear(0, 1, 1, 0);
        
        // Edge Specific
        public Image arrowImage;
        public bool UseArrow => ArrowSprite != null;
        public virtual Sprite ArrowSprite => TargetSettings.arrowSprite ? TargetSettings.arrowSprite : DefaultSettings.arrowSprite;

        public virtual int EdgeArrowSize => _trackedTargetOverlay.overrideArrowSize ? TargetSettings.arrowSize : DefaultSettings.arrowSize;
        public virtual int EdgeArrowSizeInitial => _trackedTargetOverlay.overrideArrowSizeInitial ? TargetSettings.arrowSizeInitial : DefaultSettings.arrowSizeInitial;
        public virtual int Offset => _trackedTargetOverlay.overrideOffset ? TargetSettings.offset : DefaultSettings.offset;
        public virtual int IconOffsetFromArrow => _trackedTargetOverlay.overrideIconOffsetFromArrow ? TargetSettings.iconOffsetFromArrow : DefaultSettings.iconOffsetFromArrow;

        public virtual int FullOffset 
            => DefaultSettings.enableEdgeOverlay 
               || (_trackedTargetOverlay.overrideEdgeOverlay && TargetSettings.enableEdgeOverlay)
            ? Offset + EdgeArrowSize + IconOffsetFromArrow
            : Offset;
        
        public float AngleDelta => _trackedTargetOverlay.fixedNavigationBarAngle 
            ? NorthstarUtilities.GetAngleBetweenPointsSourceForwardIgnoringRotation(CameraTransform, _trackedTargetOverlay.fixedAngle, GlobalNorthstarSettings.Instance.northAngle) 
            : NorthstarUtilities.GetAngleBetweenPointsSourceForwardIgnoringRotation(CameraTransform, TrackedPosition);
        
        // Additional max values [Set manually to cap the values
        // based on variables 100% up to YOU the developer of YOUR
        // project]
        
        // NAVIGATION BAR RUNTIME OVERRIDES
        public float overrideNavigationBarMaxOpacity = -1f;
        public float overrideNavigationBarMaxSize = -1f;
        public bool overrideNavigationBarColor = false;
        public Color overrideNavigationBarColorValue = Color.black;
        public bool overrideNavigationBarSprite = false;
        public Sprite overrideNavigationBarSpriteValue;
        public bool overrideNavigationBarYPosition = false;
        public float overrideNavigationBarYPositionValue;
        public bool overrideNavigationBarXPosition = false;
        public float overrideNavigationBarXPositionValue;
        public float overrideNavigationBarDistanceMin = -1f;
        public float overrideNavigationBarDistanceMax = -1f;
        
        // SCREEN / EDGE OVERLAY RUNTIME OVERRIDES
        public float overrideMaxOpacity = -1f;
        public float overrideMaxSize = -1f;
        public bool overrideColor = false;
        public bool overrideEdgeColor = false;
        public Color overrideColorValue = Color.black;
        public Color overrideEdgeColorValue = Color.black;
        public bool overrideSprite = false;
        public Sprite overrideSpriteValue;
        public float overrideScreenDistanceMin = -1f;
        public float overrideScreenDistanceMax = -1f;
        public float overrideEdgeDistanceMin = -1f;
        public float overrideEdgeDistanceMax = -1f;
        public bool overrideArrowColor = false;
        public Color overrideArrowColorValue = Color.white;
        public bool overrideArrowColorInitial = false;
        public Color overrideArrowColorInitialValue = Color.white;

        // Methods to call to override the values
        public virtual void SetOverrideNavigationBarYPosition(float value)
        {
            overrideNavigationBarYPosition = true;
            overrideNavigationBarYPositionValue = value;
        }
        public virtual void ResetOverrideNavigationBarYPosition() => overrideNavigationBarYPosition = false;
        public virtual void SetOverrideNavigationBarXPosition(float value)
        {
            overrideNavigationBarXPosition = true;
            overrideNavigationBarXPositionValue = value;
        }
        public virtual void ResetOverrideNavigationBarXPosition() => overrideNavigationBarXPosition = false;
        public virtual void SetOverrideArrowColor(Color value)
        {
            overrideArrowColor = true;
            overrideArrowColorValue = value;
        }
        public virtual void ResetOverrideArrowColor() => overrideArrowColor = false;
        public virtual void SetOverrideArrowColorInitial(Color value)
        {
            overrideArrowColorInitial = true;
            overrideArrowColorInitialValue = value;
        }
        public virtual void ResetOverrideArrowColorInitial() => overrideArrowColorInitial = false;
        
        public virtual void SetOverrideMaxOpacity(float value) => overrideMaxOpacity = value;
        public virtual void ResetOverrideMaxOpacity() => overrideMaxOpacity = -1f;
        public virtual void SetOverrideMaxSize(float value) => overrideMaxSize = value;
        public virtual void ResetOverrideMaxSize() => overrideMaxSize = -1f;
        
        public virtual void SetOverrideNavigationBarMaxOpacity(float value) => overrideNavigationBarMaxOpacity = value;
        public virtual void ResetOverrideNavigationBarMaxOpacity() => overrideNavigationBarMaxOpacity = -1f;
        public virtual void SetOverrideNavigationBarMaxSize(float value) => overrideNavigationBarMaxSize = value;
        public virtual void ResetOverrideNavigationBarMaxSize() => overrideNavigationBarMaxSize = -1f;

        public virtual void SetOverrideNavigationBarColor(Color value)
        {
            overrideNavigationBarColor = true;
            overrideNavigationBarColorValue = value;
        }
        
        public virtual void SetOverrideScreenColor(Color value)
        {
            overrideColor = true;
            overrideColorValue = value;
        }
        
        public virtual void SetOverrideEdgeColor(Color value)
        {
            overrideEdgeColor = true;
            overrideEdgeColorValue = value;
        }

        public virtual void SetOverrideColor(Color value)
        {
            SetOverrideEdgeColor(value);
            SetOverrideScreenColor(value);
        }

        public virtual void ResetOverrideScreenColor() => overrideColor = false;
        public virtual void ResetOverrideNavigationBarColor() => overrideNavigationBarColor = false;
        public virtual void ResetOverrideEdgeColor() => overrideEdgeColor = false;
        
        public virtual void ResetOverrideColor()
        {
            ResetOverrideScreenColor();
            ResetOverrideNavigationBarColor();
            ResetOverrideEdgeColor();
        }

        public virtual void SetOverrideSprite(Sprite value)
        {
            overrideSprite = true;
            overrideSpriteValue = value;
        }

        public virtual void ResetOverrideSprite() => overrideSprite = false;
        
        public virtual void SetOverrideNavigationBarSprite(Sprite value)
        {
            overrideNavigationBarSprite = true;
            overrideNavigationBarSpriteValue = value;
        }

        public virtual void ResetOverrideNavigationBarSprite() => overrideNavigationBarSprite = false;
        public virtual void SetOverrideScreenDistanceMin(float value) => overrideScreenDistanceMin = value;
        public virtual void ResetOverrideScreenDistanceMin() => overrideScreenDistanceMin = -1f;
        public virtual void SetOverrideScreenDistanceMax(float value) => overrideScreenDistanceMax = value;
        public virtual void ResetOverrideScreenDistanceMax() => overrideScreenDistanceMax = -1f;
        public virtual void SetOverrideNavigationBarDistanceMin(float value) => overrideNavigationBarDistanceMin = value;
        public virtual void ResetOverrideNavigationBarDistanceMin() => overrideNavigationBarDistanceMin = -1f;
        public virtual void SetOverrideNavigationBarDistanceMax(float value) => overrideNavigationBarDistanceMax = value;
        public virtual void ResetOverrideNavigationBarDistanceMax() => overrideNavigationBarDistanceMax = -1f;
        public virtual void SetOverrideEdgeDistanceMin(float value) => overrideEdgeDistanceMin = value;
        public virtual void ResetOverrideEdgeDistanceMin() => overrideEdgeDistanceMin = -1f;
        public virtual void SetOverrideEdgeDistanceMax(float value) => overrideEdgeDistanceMax = value;
        public virtual void ResetOverrideEdgeDistanceMax() => overrideEdgeDistanceMax = -1f;
        
        public virtual void SetOverrideDistanceMin(float value)
        {
            overrideScreenDistanceMin = value;
            overrideEdgeDistanceMin = value;
        }

        public virtual void ResetOverrideDistanceMin()
        {
            overrideScreenDistanceMin = -1f;
            overrideEdgeDistanceMin = -1f;
        }

        public virtual void SetOverrideDistanceMax(float value)
        {
            overrideScreenDistanceMax = value;
            overrideEdgeDistanceMax = value;
        }

        public virtual void ResetOverrideDistanceMax()
        {
            overrideScreenDistanceMax = -1f;
            overrideEdgeDistanceMax = -1f;
        }

        public virtual Camera TargetCamera => _northstarOverlay.targetCamera;
        public virtual Transform CameraTransform => TargetCamera.transform;
        public virtual GameObject TrackedObject => _trackedTargetOverlay.gameObject;
        public virtual Transform TrackedTransform => _trackedTargetOverlay.transform;

        public virtual Vector3 TrackedPosition => _lastTrackedPosition;

        private Vector3 _lastTrackedPosition;
        
        private float _distanceCheckCounter;
        private float _currentCheckFrequency = -1f;
        public float DistanceToTarget { get; private set; }
        //public virtual Vector3 ScreenPosition => TargetCamera.WorldToScreenPoint(TrackedPosition);
        
        public virtual bool WithinRange
        {
            get
            {
                // Nav bar icons only check their own range; screen/edge icons check screen/edge only.
                // This prevents a nav-bar range limit from hiding an on-screen icon (and vice versa).
                if (_isNavigationBarIcon)
                {
                    if (ShowNavigationBarOverlay)
                    {
                        if (DistanceToTarget < NavigationBarDistanceMin || DistanceToTarget > NavigationBarDistanceMax)
                            return false;
                    }
                    return true;
                }

                // SCREEN
                if (ShowScreenOverlay)
                {
                    if (DistanceToTarget < ScreenDistanceMin || DistanceToTarget > ScreenDistanceMax)
                        return false;
                }

                // EDGE
                if (ShowEdgeOverlay)
                {
                    if (DistanceToTarget < EdgeDistanceMin || DistanceToTarget > EdgeDistanceMax)
                        return false;
                }

                return true;
            }
        }

        public virtual float ScreenDistancePercent
        {
            get
            {
                var range = ScreenDistanceMax - ScreenDistanceMin;
                return range <= 0f ? 0f : Mathf.Clamp01((DistanceToTarget - ScreenDistanceMin) / range);
            }
        }

        public virtual float NavigationBarDistancePercent
        {
            get
            {
                var range = NavigationBarDistanceMax - NavigationBarDistanceMin;
                return range <= 0f ? 0f : Mathf.Clamp01((DistanceToTarget - NavigationBarDistanceMin) / range);
            }
        }

        public virtual float EdgeDistancePercent
        {
            get
            {
                var range = EdgeDistanceMax - EdgeDistanceMin;
                return range <= 0f ? 0f : Mathf.Clamp01((DistanceToTarget - EdgeDistanceMin) / range);
            }
        }

        public virtual float ScreenDistancePercentCurve => ScreenSpriteSizeUseCurve
            ? ScreenSpriteSizeCurve.Evaluate(ScreenDistancePercent)
            : 1;

        public virtual float ScreenDistancePercentOpacity => ScreenOpacityUseCurve
            ? ScreenOpacityCurve.Evaluate(ScreenDistancePercent)
            : 1;
        
        public virtual float NavigationBarDistancePercentCurve => NavigationBarSpriteSizeUseCurve
            ? NavigationBarSpriteSizeCurve.Evaluate(NavigationBarDistancePercent)
            : 1;

        public virtual float NavigationBarDistancePercentOpacity => NavigationBarOpacityUseCurve
            ? NavigationBarOpacityCurve.Evaluate(NavigationBarDistancePercent)
            : 1;
        
        public virtual float EdgeDistancePercentCurve => EdgeSpriteSizeUseCurve
            ? EdgeSpriteSizeCurve.Evaluate(EdgeDistancePercent)
            : 1; // Nov 12 2025 -- switched to "1" from previous: EdgeDistancePercent;

        public virtual float EdgeDistancePercentOpacity => EdgeOpacityUseCurve
            ? EdgeOpacityCurve.Evaluate(EdgeDistancePercent)
            : 1; // Nov 12 2025 -- switched to "1" from previous: EdgeDistancePercent;

        public virtual float NavigationBarYPosition =>
            overrideNavigationBarYPosition
                ? overrideNavigationBarYPositionValue
                : (_trackedTargetOverlay.overrideNavigationBarYPosition
                    ? TargetSettings.yPosition
                    : DefaultSettings.yPosition);
        
        public virtual float NavigationBarXPosition =>
            overrideNavigationBarXPosition
                ? overrideNavigationBarXPositionValue
                : (_trackedTargetOverlay.overrideNavigationBarXPosition
                    ? TargetSettings.xPosition
                    : DefaultSettings.xPosition);

        public virtual Sprite NavigationBarSprite =>
            overrideNavigationBarSprite
                ? overrideNavigationBarSpriteValue
                : (_trackedTargetOverlay.overrideNavigationBarSprite
                    ? TargetSettings.navigationBarSprite
                    : DefaultSettings.navigationBarSprite);
        
        public virtual Sprite ScreenSprite =>
            overrideSprite
                ? overrideSpriteValue
                : (_trackedTargetOverlay.overrideScreenSprite
                    ? TargetSettings.screenSprite
                    : DefaultSettings.screenSprite);

        public virtual Sprite EdgeSprite =>
            overrideSprite
                ? overrideSpriteValue
                : (_trackedTargetOverlay.overrideEdgeSprite
                    ? TargetSettings.edgeSprite
                    : DefaultSettings.edgeSprite);

        public virtual Color ScreenSpriteColor =>
            overrideColor
                ? overrideColorValue
                : (_trackedTargetOverlay.overrideScreenSpriteColor
                    ? TargetSettings.screenSpriteColor
                    : DefaultSettings.screenSpriteColor);
        
        public virtual Color NavigationBarSpriteColor =>
            overrideNavigationBarColor
                ? overrideNavigationBarColorValue
                : (_trackedTargetOverlay.overrideNavigationBarSpriteColor
                    ? TargetSettings.navigationBarSpriteColor
                    : DefaultSettings.navigationBarSpriteColor);
        
        public virtual Color EdgeSpriteColor =>
            overrideEdgeColor
                ? overrideEdgeColorValue
                : (_trackedTargetOverlay.overrideEdgeSpriteColor
                    ? TargetSettings.edgeSpriteColor
                    : DefaultSettings.edgeSpriteColor);
        
        public virtual Color ArrowColor =>
            overrideArrowColor
                ? overrideArrowColorValue
                : (_trackedTargetOverlay.overrideArrowColor
                    ? TargetSettings.arrowColor
                    : DefaultSettings.arrowColor);

        
        public virtual Color ArrowColorInitial =>
            overrideArrowColorInitial
                ? overrideArrowColorInitialValue
                : (_trackedTargetOverlay.overrideArrowColorInitial
                    ? TargetSettings.arrowColorInitial
                    : DefaultSettings.arrowColorInitial);

       
        public virtual float ScreenDistanceMin =>
            (overrideScreenDistanceMin > -1)
                ? overrideScreenDistanceMin
                : (_trackedTargetOverlay.overrideScreenDistance
                    ? TargetSettings.screenDistanceMin
                    : DefaultSettings.screenDistanceMin);

        public virtual float ScreenDistanceMax =>
            (overrideScreenDistanceMax > -1)
                ? overrideScreenDistanceMax
                : (_trackedTargetOverlay.overrideScreenDistance
                    ? TargetSettings.screenDistanceMax
                    : DefaultSettings.screenDistanceMax);
        
        public virtual float NavigationBarDistanceMin =>
            (overrideNavigationBarDistanceMin > -1)
                ? overrideNavigationBarDistanceMin
                : (_trackedTargetOverlay.overrideNavigationBarDistance
                    ? TargetSettings.navigationBarDistanceMin
                    : DefaultSettings.navigationBarDistanceMin);

        public virtual float NavigationBarDistanceMax =>
            (overrideNavigationBarDistanceMax > -1)
                ? overrideNavigationBarDistanceMax
                : (_trackedTargetOverlay.overrideNavigationBarDistance
                    ? TargetSettings.navigationBarDistanceMax
                    : DefaultSettings.navigationBarDistanceMax);
        
        public virtual float EdgeDistanceMin =>
            (overrideEdgeDistanceMin > -1)
                ? overrideEdgeDistanceMin
                : (_trackedTargetOverlay.overrideEdgeDistance
                    ? TargetSettings.edgeDistanceMin
                    : DefaultSettings.edgeDistanceMin);

        public virtual float EdgeDistanceMax =>
            (overrideEdgeDistanceMax > -1)
                ? overrideEdgeDistanceMax
                : (_trackedTargetOverlay.overrideEdgeDistance
                    ? TargetSettings.edgeDistanceMax
                    : DefaultSettings.edgeDistanceMax);
        
        public virtual bool MoveWithRotation =>_trackedTargetOverlay.overrideNavigationBarMoveWithRotation
                ? TargetSettings.moveWithRotation
                : DefaultSettings.moveWithRotation;

        public virtual int ScreenSpriteSize
        {
            get
            {
                var originalSize = _trackedTargetOverlay.overrideScreenSpriteSize
                    ? TargetSettings.screenSpriteSize
                    : DefaultSettings.screenSpriteSize;

                var spriteSize = ScreenSpriteSizeUseCurve
                    ? originalSize * ScreenSpriteSizeCurve.Evaluate(ScreenDistancePercent)
                    : originalSize;

                return overrideMaxSize > -1
                    ? (int)Mathf.Min(spriteSize, (int)overrideMaxSize)
                    : (int)spriteSize;
            }
        }
        
        public virtual int NavigationBarSpriteSize
        {
            get
            {
                var originalSize = _trackedTargetOverlay.overrideNavigationBarSpriteSize
                    ? TargetSettings.navigationBarSize
                    : DefaultSettings.navigationBarSize;

                var spriteSize = NavigationBarSpriteSizeUseCurve
                    ? originalSize * NavigationBarSpriteSizeCurve.Evaluate(NavigationBarDistancePercent)
                    : originalSize;

                return overrideNavigationBarMaxSize > -1
                    ? (int)Mathf.Min(spriteSize, (int)overrideNavigationBarMaxSize)
                    : (int)spriteSize;
            }
        }
        
        public virtual int EdgeSpriteSize
        {
            get
            {
                var originalSize = _trackedTargetOverlay.overrideEdgeSpriteSize
                    ? TargetSettings.edgeSpriteSize
                    : DefaultSettings.edgeSpriteSize;

                var spriteSize = EdgeSpriteSizeUseCurve
                    ? originalSize * EdgeSpriteSizeCurve.Evaluate(ScreenDistancePercent)
                    : originalSize;

                return overrideMaxSize > -1
                    ? (int)Mathf.Min(spriteSize, (int)overrideMaxSize)
                    : (int)spriteSize;
            }
        }

        public virtual bool FadeAtEdges => _trackedTargetOverlay.overrideFadeAtEdges
            ? TargetSettings.fadeAtEdges
            : DefaultSettings.fadeAtEdges;
        
        public virtual bool ClampPositionAtEdges => _trackedTargetOverlay.overrideClampPositionAtEdges
            ? TargetSettings.clampPositionAtEdges
            : DefaultSettings.clampPositionAtEdges;
        
        public virtual bool ScreenSpriteSizeUseCurve => _trackedTargetOverlay.overrideScreenSpriteSize
            ? TargetSettings.screenSpriteSizeUseCurve
            : DefaultSettings.screenSpriteSizeUseCurve;
        
        public virtual bool NavigationBarSpriteSizeUseCurve => _trackedTargetOverlay.overrideNavigationBarSpriteSize
            ? TargetSettings.navigationBarSizeUseCurve
            : DefaultSettings.navigationBarSizeUseCurve;
        
        public virtual bool EdgeSpriteSizeUseCurve => _trackedTargetOverlay.overrideEdgeSpriteSize
            ? TargetSettings.edgeSpriteSizeUseCurve
            : DefaultSettings.edgeSpriteSizeUseCurve;

        public virtual AnimationCurve ScreenSpriteSizeCurve => _trackedTargetOverlay.overrideScreenUseSizeCurve
            ? TargetSettings.screenSpriteSizeCurve
            : DefaultSettings.screenSpriteSizeCurve;
        
        public virtual AnimationCurve NavigationBarSpriteSizeCurve => _trackedTargetOverlay.overrideNavigationBarUseSizeCurve
            ? TargetSettings.navigationBarSizeCurve
            : DefaultSettings.navigationBarSizeCurve;
        
        public virtual AnimationCurve EdgeSpriteSizeCurve => _trackedTargetOverlay.overrideEdgeSpriteSize
            ? TargetSettings.edgeSpriteSizeCurve
            : DefaultSettings.edgeSpriteSizeCurve;

        public virtual float ScreenSpriteOpacity
        {
            get
            {
                var opacity = _trackedTargetOverlay.overrideScreenSpriteOpacity
                    ? TargetSettings.screenSpriteOpacity
                    : DefaultSettings.screenSpriteOpacity;

                return overrideMaxOpacity > -1
                    ? Mathf.Min(opacity, overrideMaxOpacity)
                    : opacity;
            }
        }
        
        public virtual float NavigationBarSpriteOpacity
        {
            get
            {
                var opacity = _trackedTargetOverlay.overrideNavigationBarSpriteOpacity
                    ? TargetSettings.navigationBarSpriteOpacity
                    : DefaultSettings.navigationBarSpriteOpacity;

                return overrideNavigationBarMaxOpacity > -1
                    ? Mathf.Min(opacity, overrideNavigationBarMaxOpacity)
                    : opacity;
            }
        }
        
        public virtual float EdgeSpriteOpacity
        {
            get
            {
                var opacity = _trackedTargetOverlay.overrideEdgeSpriteOpacity
                    ? TargetSettings.edgeSpriteOpacity
                    : DefaultSettings.edgeSpriteOpacity;

                return overrideMaxOpacity > -1
                    ? Mathf.Min(opacity, overrideMaxOpacity)
                    : opacity;
            }
        }
        
        public virtual float ArrowOpacity
        {
            get
            {
                var opacity = _trackedTargetOverlay.overrideArrowOpacity
                    ? TargetSettings.arrowOpacity
                    : DefaultSettings.arrowOpacity;

                return overrideMaxOpacity > -1
                    ? Mathf.Min(opacity, overrideMaxOpacity)
                    : opacity;
            }
        }
        
        public virtual float ArrowOpacityInitial
        {
            get
            {
                var opacity = _trackedTargetOverlay.overrideArrowOpacityInitial
                    ? TargetSettings.arrowOpacityInitial
                    : DefaultSettings.arrowOpacityInitial;

                return overrideMaxOpacity > -1
                    ? Mathf.Min(opacity, overrideMaxOpacity)
                    : opacity;
            }
        }

        public virtual bool ScreenOpacityUseCurve => _trackedTargetOverlay.overrideScreenSpriteOpacity
            ? TargetSettings.screenSpriteOpacityUseCurve
            : DefaultSettings.screenSpriteOpacityUseCurve;

        public virtual AnimationCurve ScreenOpacityCurve => _trackedTargetOverlay.overrideScreenSpriteOpacity
            ? TargetSettings.screenSpriteOpacityCurve
            : DefaultSettings.screenSpriteOpacityCurve;
        
        public virtual bool NavigationBarOpacityUseCurve => _trackedTargetOverlay.overrideNavigationBarSpriteOpacity
            ? TargetSettings.navigationBarSpriteOpacityUseCurve
            : DefaultSettings.navigationBarSpriteOpacityUseCurve;

        public virtual AnimationCurve NavigationBarOpacityCurve => _trackedTargetOverlay.overrideNavigationBarSpriteOpacity
            ? TargetSettings.navigationBarSpriteOpacityCurve
            : DefaultSettings.navigationBarSpriteOpacityCurve;
        
        public virtual bool EdgeOpacityUseCurve => _trackedTargetOverlay.overrideEdgeSpriteOpacity
            ? TargetSettings.edgeSpriteOpacityUseCurve
            : DefaultSettings.edgeSpriteOpacityUseCurve;

        public virtual AnimationCurve EdgeOpacityCurve => _trackedTargetOverlay.overrideEdgeSpriteOpacity
                ? TargetSettings.edgeSpriteOpacityCurve
                : DefaultSettings.edgeSpriteOpacityCurve;

        public virtual bool RotateArrow => _trackedTargetOverlay.overrideArrowRotation
            ? TargetSettings.rotateArrow
            : DefaultSettings.rotateArrow;

        private bool _cachedUseRadar = false;
        private bool _useRadar;
        private bool UseRadar => GetUseRadar();
        private CompassIcon _compassIcon;
        private TrackedTargetCompassRadar _compassRadar;
        private float RadarAlpha => !UseRadar || _compassIcon == null ? 1 : _compassIcon.PingedAlphaValue;
        
        private bool RadarMovement() 
        {
            if (!UseRadar)
                return true; // No radar, so we can show movement
            if (Radar.RadarInstance == null)
                return true; // No radar, so we can show movement
            if (Radar.RadarInstance.showPingMovement)
                return true; // Radar is showing movement, so we can show movement
            if (CompassIcon == null)
                return true; // No tracked icon, so we can show movement
            if (CompassIcon.pinged)
                return true; // Radar is not showing movement, but the icon is pinged, so we can show movement
                
            return false;
        }

        private CompassIcon CompassIcon => _compassIcon == null 
            ? _compassIcon = _compassRadar.compassIcon 
            : _compassIcon;
        
        private bool GetUseRadar()
        {
            if (TrackedTargetOverlay == null) return false; // Just in case it hasn't been set up yet
            if (TrackedTargetOverlay.ignoreRadar)
                return false;

            if (_cachedUseRadar) return _useRadar;

            // Don't cache the negative when the radar simply hasn't initialized yet —
            // it may start after this icon does. Only commit to false once we've
            // confirmed this target has no TrackedTargetCompassRadar component.
            _compassRadar = _trackedTargetOverlay.gameObject.GetComponent<TrackedTargetCompassRadar>();
            if (_compassRadar == null)
            {
                _useRadar = false;
                _cachedUseRadar = true;
                return false;
            }

            if (Radar.RadarInstance == null)
                return false; // Radar not yet up; don't cache — re-check next frame

            _compassIcon = _compassRadar.compassIcon;
            _useRadar = true;
            _cachedUseRadar = true;
            return true;
        }

        protected virtual void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
        }

        public virtual void Setup(NorthstarOverlay northstarOverlayValue,
            TrackedTargetOverlay trackedTargetOverlayValue)
        {
            _northstarOverlay = northstarOverlayValue;
            _trackedTargetOverlay = trackedTargetOverlayValue;

            PreComputeScreenValues();
            
            // Initial values
            _isNavigationBarIcon = false;
            SetValues();
        }
        
        public virtual void SetupNavigationBar(NorthstarOverlay northstarOverlayValue,
            TrackedTargetOverlay trackedTargetOverlayValue, NavigationBar navigationBarValue)
        {
            _northstarOverlay = northstarOverlayValue;
            _trackedTargetOverlay = trackedTargetOverlayValue;
            navigationBar = navigationBarValue;
            
            // Initial values
            _isNavigationBarIcon = true;
            SetNavigationBarValues();
        }

        protected virtual void LateUpdate()
        {
            if (DestroyIfNoTarget()) return;
            
            if (RadarMovement())
                _lastTrackedPosition = TrackedTransform.position + _trackedTargetOverlay.iconOffset;
            
            UpdateDistanceToTarget();
            SetNavigationBarValues();
            SetValues();
        }

        private bool DestroyIfNoTarget()
        {
            if (_trackedTargetOverlay != null) return false;
            
            Destroy(gameObject);
            return true;
        }

        /// <summary>
        /// This will instantly check the distance, skipping the frequency check
        /// </summary>
        public virtual void ForceDistanceUpdate()
        {
            _distanceCheckCounter = _currentCheckFrequency;
            UpdateDistanceToTarget();
        }
        
        /// <summary>
        /// Updates the distance to the target periodically
        /// </summary>
        protected virtual void UpdateDistanceToTarget()
        {
            if (_currentCheckFrequency <= 0) // Every frame
            {
                DistanceToTarget = Vector3.Distance(TargetCamera.transform.position, TrackedPosition);
                UpdateDistanceCheckFrequency();
                return;
            }
            
            _distanceCheckCounter += Time.deltaTime;
            if (!(_distanceCheckCounter >= _currentCheckFrequency)) return;
            
            DistanceToTarget = Vector3.Distance(TargetCamera.transform.position, TrackedPosition);
            _distanceCheckCounter = 0;

            UpdateDistanceCheckFrequency();
        }

        /// <summary>
        /// Updates the distance check frequency based on the distance to the target, using the lookup table
        /// on the Northstar Icon.
        /// </summary>
        protected virtual void UpdateDistanceCheckFrequency()
        {
            // With no lookup table, keep checking every frame
            if (distanceChecks.Count == 0)
            {
                _currentCheckFrequency = -1f;
                return;
            }

            // Update the frequency of distance checks based on the distance to the target
            // Will save the first value that is greater than the current distance
            foreach (var check in distanceChecks)
            {
                if (DistanceToTarget > check.distance) continue;
                
                _currentCheckFrequency = check.frequency;
                return;
            }
            
            // If no value is greater than the current distance, use the last value
            _currentCheckFrequency = distanceChecks[^1].frequency;
        }

        protected virtual bool ShouldShow()
        {
            if (TrackedObject == null)
                return false;
            if (TrackedObject.activeSelf == false)
                return false;
            
            if (!ShowScreenOverlay && !ShowEdgeOverlay && !ShowNavigationBarOverlay)
                return false;

            if (!WithinRange)
                return false;

            return true;
        }
        
        protected virtual void ToggleAll(bool value)
        {
            _image.enabled = value;
            arrowImage.enabled = value;
        }
        
        protected virtual void SetNavigationBarValues()
        {
            if (!_isNavigationBarIcon) return;
            
            if (_northstarOverlay == null)
                return;

            // Don't show if the target is disabled or destroyed
            if (!ShouldShow())
            {
                _image.enabled = false;
                return;
            }
            
            // Main Sprite
            _image.enabled = true;
            _image.sprite = NavigationBarSprite;
            
            // Main sprite site
            _rectTransform.sizeDelta = Vector2.one * (NavigationBarSpriteSize * NavigationBarDistancePercentCurve);
            
            // Main sprite Opacity & Color
            var mainSpriteOpacity = NavigationBarSpriteOpacity * NavigationBarDistancePercentOpacity;
            var mainSpriteColor = NavigationBarSpriteColor;
            _image.color = new Color(mainSpriteColor.r, mainSpriteColor.g, mainSpriteColor.b, Mathf.Min(RadarAlpha, mainSpriteOpacity));
            PlaceObjectOnNavigationBar();
            FadeAtEdgesOfNavigationBar(AngleDelta);
        }

       
        
        protected virtual void PlaceObjectOnNavigationBar()
        {
            if (MoveWithRotation)
            {
                _rectTransform.localPosition = navigationBar.GetPositionOnBar(this);
                return;
            }
            
            _rectTransform.localPosition = new Vector2(NavigationBarXPosition, NavigationBarYPosition);
        }
        
        protected virtual  void FadeAtEdgesOfNavigationBar(float angle)
        {
            if (FadeAtEdges == false || !MoveWithRotation)
                return;
            var color = _image.color;
            _image.color = new Color(color.r, color.g, color.b, Mathf.Min(RadarAlpha, ComputeNavigationBarEdgeFade(angle, color.a)));
        }
        
        /// <summary>
        /// Computes the amount of edge fading based on the position of the object on the compass bar.
        /// </summary>
        /// <param name="angle"></param>
        /// <param name="currentAlpha"></param>
        /// <returns></returns>
        protected virtual  float ComputeNavigationBarEdgeFade(float angle, float currentAlpha = 1)
        {
            // If the angle is within the complete visibility range, the object is fully visible.
            if (Mathf.Abs(angle) <= navigationBar.fadeAngle)
                return currentAlpha;

            // If the angle is beyond the fading range, the object is fully faded.
            if (Mathf.Abs(angle) >= navigationBar.fadeAngleEnd)
                return 0f;

            // If the angle is inside the fading range, we interpolate the fade value.
            var fadeRangeSize = navigationBar.fadeAngleEnd - navigationBar.fadeAngle;
            var distanceFromStartOfFadeRange = Mathf.Abs(angle) - navigationBar.fadeAngle;
            
            return Mathf.Min(1f - (distanceFromStartOfFadeRange / fadeRangeSize), currentAlpha);
        }

        private Vector3 _lastScreenPosition;
        private Vector3 _lastNavigationBarPosition;
        
        protected virtual void SetValues()
        {
            if (_isNavigationBarIcon) return;

            if (_northstarOverlay == null)
                return;

            // Refresh screen bounds if the display resolution changed (e.g. mobile rotation, window resize)
            if (width != _cachedScreenWidth || height != _cachedScreenHeight)
                PreComputeScreenValues();

            // Don't show if the target is disabled or destroyed
            if (!ShouldShow())
            {
                ToggleAll(false);
                return;
            }
            
            // Compute how much we need to blend between the screen and edge overlay settings
            var position = CalculateScreenPosition(out var offsetEffect);             // <-------------------------------------------------
            _offsetEffect = offsetEffect;
            
            // If we're in the middle of the screen and not using the screen overlay, don't show
            // If we're outside of the edge and not using the edge overlay, don't show
            if ((_offsetEffect == 0 && !ShowScreenOverlay)
                || (_offsetEffect == 1 && !ShowEdgeOverlay))
            {
                _image.enabled = false;
                arrowImage.enabled = false;
                return;
            }
            
            // Only show the arrow if we are close to the edge or at the edge
            arrowImage.enabled = _offsetEffect != 0 && ShowEdgeOverlay;
            
            // Main Sprite — swap to edge sprite when icon is more than halfway to the edge
            _image.enabled = true;
            var resolvedEdgeSprite = EdgeSprite;
            _image.sprite = (_offsetEffect >= 0.5f && resolvedEdgeSprite != null)
                ? resolvedEdgeSprite
                : ScreenSprite;
            _rectTransform.position = position;

            // Main sprite size
            var screenToEdgeSize = ShowEdgeOverlay 
                ? Mathf.Lerp(ScreenSpriteSize, EdgeSpriteSize, _offsetEffect) 
                : ScreenSpriteSize;
            var distanceSize = ShowEdgeOverlay 
                ? Mathf.Lerp(ScreenDistancePercentCurve, EdgeDistancePercentCurve, _offsetEffect)
                : ScreenDistancePercentCurve;
            _rectTransform.sizeDelta = Vector2.one * (screenToEdgeSize * distanceSize);
            
            // This will fade it out near the edge when we aren't using the edge overlay
            var edgeOfScreenNoEdgeEffectOpacity = _offsetEffect > 0
                ? Mathf.Lerp(ScreenDistancePercentOpacity, 0, _offsetEffect)
                : ScreenDistancePercentOpacity;
            
            //Debug.Log($"ShowEdgeOverlay is {ShowEdgeOverlay} - ScreenDistancePercentOpacity {ScreenDistancePercentOpacity} - EdgeDistancePercentOpacity {EdgeDistancePercentOpacity}");
            // Main sprite Opacity & Color
            var screenToEdgeOpacity = ShowEdgeOverlay 
                ? Mathf.Lerp(ScreenSpriteOpacity, EdgeSpriteOpacity, _offsetEffect)
                : ScreenSpriteOpacity;
            var distanceOpacity = ShowEdgeOverlay
                ? Mathf.Lerp(ScreenDistancePercentOpacity, EdgeDistancePercentOpacity, _offsetEffect)
                : edgeOfScreenNoEdgeEffectOpacity; //ScreenDistancePercentOpacity;
            
            //Debug.Log($"screenToEdgeOpacity is {screenToEdgeOpacity}, distanceOpacity is {distanceOpacity}");
            
            var mainSpriteOpacity = screenToEdgeOpacity * distanceOpacity;
            var mainSpriteColor = ShowEdgeOverlay 
                ? Color.Lerp(ScreenSpriteColor, EdgeSpriteColor, _offsetEffect)
                : ScreenSpriteColor;
            _image.color = new Color(mainSpriteColor.r, mainSpriteColor.g, mainSpriteColor.b, Mathf.Min(RadarAlpha, mainSpriteOpacity));
            
            if (UseArrow && ShowEdgeOverlay)
            {
                arrowImage.enabled = _offsetEffect > 0; // Turn on if the offset effect is positive
                arrowImage.sprite = ArrowSprite;
                arrowImage.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(EdgeArrowSizeInitial, EdgeArrowSize, _offsetEffect);
                arrowImage.rectTransform.position = _arrowPosition;
                
                // Color & Opacity
                var arrowOpacity = Mathf.Lerp(ArrowOpacityInitial, ArrowOpacity, _offsetEffect) * EdgeDistancePercentOpacity;
                var arrowColor = Color.Lerp(ArrowColorInitial, ArrowColor, _offsetEffect);
                arrowImage.color = new Color(arrowColor.r, arrowColor.g, arrowColor.b, Mathf.Min(RadarAlpha, arrowOpacity));
            }
            else
                arrowImage.enabled = false;
        }

        private float _offsetEffect;
        private Vector3 _arrowPosition;
        private Vector3 _screenCenter;
        private float _screenBoundX, _screenBoundY,_screenBoundArrowX, _screenBoundArrowY;
        private Vector3 _clampedPosFromCenter, _clampedPosFromCenterArrow;
        private int _cachedScreenWidth, _cachedScreenHeight;

        protected virtual void PreComputeScreenValues()
        {
            _cachedScreenWidth = width;
            _cachedScreenHeight = height;
            _screenCenter = new Vector3(width, height, 0) / 2;
            _screenBoundX = _screenCenter.x - FullOffset;
            _screenBoundY = _screenCenter.y - FullOffset;
            _screenBoundArrowX = _screenCenter.x - Offset;
            _screenBoundArrowY = _screenCenter.y - Offset;
        }
        
        private Vector3 CalculateScreenPosition(out float offsetEffect)
        {
            var desiredScreenPos = TargetCamera.WorldToScreenPoint(TrackedPosition);
            
            var lookingAway = desiredScreenPos.z < 0;
            if (lookingAway) // Position is behind the camera, so flip it
                desiredScreenPos = PushToScreenEdge(desiredScreenPos, isBehindScreen: true);
            
            // Translate position from the world space to space having center at (0, 0)
            var screenPosFromCenter = desiredScreenPos - _screenCenter;
            
            var pixelsFromEdge = CalculatePixelsFromEdge(screenPosFromCenter);
            offsetEffect = CalculateOffsetEffect(pixelsFromEdge, lookingAway);
            
            // Convert screen position back to world position
           // var worldPos = TargetCamera.ScreenToWorldPoint(new Vector3(desiredScreenPos.x, desiredScreenPos.y, TargetCamera.nearClipPlane));
            
            // If we are not offsetting the icon, return the desired position
            // No need to set arrow position: It will not display
            if (!ShowEdgeOverlay || (pixelsFromEdge > FullOffset && !lookingAway))
                return desiredScreenPos;
            
            return ComputeFinalPosition(screenPosFromCenter);
        }

        private Vector3 ComputeFinalPosition(Vector3 screenPosFromCenter)
        {
            // Compute angle from center of screen to desired position
            var angle = Mathf.Atan2(screenPosFromCenter.y, screenPosFromCenter.x);
            angle -= 90 * Mathf.Deg2Rad;

            // Compute (cos, sin) for the calculated angle
            var cos = Mathf.Cos(angle);
            var sin = -Mathf.Sin(angle);

            // Approximate the screen bound
            var cosAbs = Mathf.Abs(cos);
            var sinAbs = Mathf.Abs(sin);
            
            var screenBound = Mathf.Min(_screenBoundY / cosAbs, _screenBoundX / sinAbs);
            var screenBoundArrow = Mathf.Min(_screenBoundArrowY / cosAbs, _screenBoundArrowX / sinAbs);

            // Clamp desired position not to go outside screen bounds
            var magnitude = Mathf.Clamp(screenPosFromCenter.magnitude, 0, screenBound);
            var magnitudeArrow = Mathf.Clamp(screenPosFromCenter.magnitude, 0, screenBoundArrow);
            _clampedPosFromCenter = new Vector3(sin * magnitude, cos * magnitude, 0);
            _clampedPosFromCenterArrow = new Vector3(sin * magnitudeArrow, cos * magnitudeArrow, 0);

            // Translate position back to the original space
            //_arrowPosition = _clampedPosFromCenterArrow + _screenCenter;
            
            // Translate position back to the original space
            var directionTowardsEdge = (_clampedPosFromCenterArrow - _clampedPosFromCenter).normalized;
            _arrowPosition = _clampedPosFromCenter + _screenCenter + directionTowardsEdge * IconOffsetFromArrow;

            DoArrowRotation();
            
            return _clampedPosFromCenter + _screenCenter;
        }

        private void DoArrowRotation()
        {
            if (!RotateArrow)
                return;
            
            // Compute direction vector from center to arrow position
            Vector3 directionToEdge = _arrowPosition - _screenCenter;

            // Compute rotation angle (in degrees)
            float arrowAngle = Mathf.Atan2(directionToEdge.y, directionToEdge.x) * Mathf.Rad2Deg;

            // Since the arrow should point outwards, we'll add 90 degrees to the calculated angle
            arrowAngle -= 90;

            // To flip the arrow and make it point outwards, subtract 180 degrees
           // arrowAngle -= 180;

            // Set the Quaternion rotation of the RectTransform
            arrowImage.rectTransform.rotation = Quaternion.Euler(0f, 0f, arrowAngle);
        }





        private float AspectRatio => width / height;
        
        private float CalculatePixelsFromEdge(Vector3 screenPosFromCenter) 
            => Mathf.Min(
                width / 2 - Mathf.Abs(screenPosFromCenter.x), 
                height / 2 - Mathf.Abs(screenPosFromCenter.y));

        private float CalculateOffsetEffect(float pixelsFromEdge, bool lookingAway) 
            => lookingAway 
                ? 1 
                : 1 - Mathf.Clamp(pixelsFromEdge / Offset, 0, 1);

        
        private Vector3 PushToScreenEdge(Vector3 pointOnScreen, bool isBehindScreen)
        {
            var direction = pointOnScreen - _screenCenter;
            var scaleX = Mathf.Abs(direction.x / (width / 2));
            var scaleY = Mathf.Abs(direction.y / (height / 2));
            var maxScale = Mathf.Max(scaleX, scaleY);
            if (maxScale < 1e-6f) // target exactly behind camera center — push to a stable fallback
                direction = new Vector3(0, -1, 0);
            else
                direction /= maxScale;

            if (isBehindScreen)
                direction *= -1;

            return _screenCenter + direction;
        }
        
        
        
    }
}


