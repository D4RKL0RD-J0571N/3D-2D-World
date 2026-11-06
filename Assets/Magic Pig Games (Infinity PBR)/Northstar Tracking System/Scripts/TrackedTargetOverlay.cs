using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/*
 * This is the "Tracked Target" class for the overlay system (screen & edge overlay, plus horizontal compass bar). It
 * is different from the Tracked Target class for the compass / radar.
 *
 * This goes on any object that you want to track with the overlay system. The object can have both this and the
 * compass / radar Tracked Target class on it.
 */

namespace MagicPigGames.Northstar
{
    [System.Serializable]
    public class TrackedTargetOverlay : MonoBehaviour
    {
        public NorthstarOverlaySettings overlaySettings;
        
        public Vector3 iconOffset = Vector3.zero;
        public string targetType = "Default";
        public int layerOrder = 0;

        public GameObject customScreenIcon;
        public GameObject customNavigationBarIcon;
        
        // Override Main
        public bool overrideScreenOverlay = false;
        public bool overrideEdgeOverlay = false;
        public bool overrideNavigationBar = false;

        public bool ignoreRadar = false;
        
        // Overrides
        public bool overrideClampPositionAtEdges = false;
        public bool overrideNavigationBarYPosition = false;
        public bool overrideFadeAtEdges = false;
        public bool overrideArrowRotation = false;
        public bool overrideScreenSprite = false;
        public bool overrideScreenSpriteSize = false;
        public bool overrideScreenSizeCurve = false;
        public bool overrideScreenUseSizeCurve = false;
        public bool overrideScreenOpacityCurve = false;
        public bool overrideScreenUseOpacityCurve = false;
        public bool overrideScreenSpriteOpacity = false;
        public bool overrideScreenSpriteColor = false;
        public bool overrideScreenDistance = false;
        
        public bool overrideNavigationBarSprite = false;
        public bool overrideNavigationBarSpriteSize = false;
        public bool overrideNavigationBarSizeCurve = false;
        public bool overrideNavigationBarUseSizeCurve = false;
        public bool overrideNavigationBarOpacityCurve = false;
        public bool overrideNavigationBarUseOpacityCurve = false;
        public bool overrideNavigationBarSpriteOpacity = false;
        public bool overrideNavigationBarSpriteColor = false;
        public bool overrideNavigationBarDistance = false;
        public bool overrideNavigationBarMoveWithRotation = false;
        public float overrideNavigationBarXPositionValue = 0f;
        public bool overrideNavigationBarXPosition = false;
        
        public bool overrideEdgeSizeCurve = false;
        public bool overrideEdgeUseSizeCurve = false;
        public bool overrideEdgeOpacityCurve = false;
        public bool overrideEdgeUseOpacityCurve = false;
        
        public bool overrideArrowSizeCurve = false;
        public bool overrideArrowUseSizeCurve = false;
        public bool overrideArrowOpacityCurve = false;
        public bool overrideArrowUseOpacityCurve = false;
        
        public bool overrideArrowSprite = false;
        
        // Edge Overrides
        public bool overrideArrowOpacity = false;
        public bool overrideArrowOpacityInitial = false;
        public bool overrideEdgeSprite = false;
        public bool overrideEdgeSpriteSize = false;
        public bool overrideEdgeSpriteOpacity = false;
        public bool overrideEdgeSpriteColor = false;
        public bool overrideEdgeDistance = false;
        public bool overrideArrowSizeInitial = false;
        public bool overrideOffset = false;
        public bool overrideEdgeArrow = false;
        public bool overrideIconOffsetFromArrow = false;
        public bool overrideArrowSize = false;
        public bool overrideArrowColor = false;
        public bool overrideArrowColorInitial = false;
        
        // Navigation bar
        public bool fixedNavigationBarAngle = false;
        public float fixedAngle = 0f;

        public bool EnableNavigationBar(bool currentOption) 
            => overrideNavigationBar ? overlaySettings.enableNavigationBar : currentOption;
        
        // Runtime only
        [HideInInspector] public OverlayIcon overlayScreenIcon;
        private OverlayIcon _navigationBarIcon;
        private OverlayIcon _screenIcon;
        public OverlayIcon NavigationBarIcon => _navigationBarIcon == null && NavigationBar.Instance != null
            ? _navigationBarIcon = NavigationBar.Instance.GetIcon(this)
            : _navigationBarIcon;
        
        public OverlayIcon ScreenIcon => _screenIcon == null && NorthstarOverlay.Instance != null
            ? _screenIcon = NorthstarOverlay.Instance.GetIcon(this)
            : _screenIcon;
        
        public event Action<OverlayIcon> OnIconCreated;

        private Coroutine _registerCoroutine;

        protected virtual void OnEnable() => _registerCoroutine = StartCoroutine(RegisterWithNorthstarOverlay());

        protected virtual void OnDisable()
        {
            // Disabling a component does NOT stop its coroutines -- stop the pending registration
            // so we don't register after being disabled (or stack coroutines on re-enable).
            if (_registerCoroutine != null)
            {
                StopCoroutine(_registerCoroutine);
                _registerCoroutine = null;
            }
            RemoveRegistration();
        }

        public virtual void RemoveRegistration()
        {
            if (NorthstarOverlay.Instance == null) return;

            NorthstarOverlay.Instance.RemoveTarget(this);
        }


        // Use a coroutine in case the Compass system is not yet active.
        protected virtual IEnumerator RegisterWithNorthstarOverlay()
        {
            yield return new WaitUntil(() => NorthstarOverlay.Instance != null);

            overlayScreenIcon = NorthstarOverlay.Instance.AddScreenOverlay(this);
            _registerCoroutine = null;
            if (overlayScreenIcon == null)
            {
                Debug.LogWarning("Unable to register screen overlay with Northstar Overlay.");
                yield break;
            }
            OnIconCreated?.Invoke(overlayScreenIcon);
        }
    }
}