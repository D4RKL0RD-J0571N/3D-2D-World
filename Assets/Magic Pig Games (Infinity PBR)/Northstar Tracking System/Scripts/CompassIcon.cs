using System;
using UnityEngine;
using UnityEngine.UI;
using static MagicPigGames.Northstar.GlobalNorthstarSettings;
using static MagicPigGames.Northstar.NorthstarUtilities;

/*
 * This component should be attached to any object which should be tracked on the Compass system. It will automatically
 * register itself with the Compass system on Start(), and unregister itself on OnDestroy().
 */

namespace MagicPigGames.Northstar
{
    [System.Serializable]
    public class CompassIcon : MonoBehaviour
    {
        [Header("Required")]
        [Tooltip("Determines how the compass will rotate.")]
        public TrackingType trackingType = TrackingType.PointNorth;
        public bool adjustBasedOnPlayerIcon = false;
        
        [Tooltip("When moving the background, the compass will rotate in the opposite direction.")]
        public bool invertRotation = false;
        
        [Header("Point at Target")]
        [Tooltip("When true, a compass marked followTarget true will point to that target.")]
        public Transform target;

        [Header("Point at Relative Point")] 
        public GameObject trackerIconPrefab;

        [Header("Radar - Only active w/ Radar")]
        public bool ignoreRadar = false;
        public float alphaValue = 1f;
        public bool pinged = false;
        public float timer = 0f;
        public Image image;
        public RectTransform rectTransform;
        
        private Vector3 _compassDirection;

        private Transform PlayerTransform => NorthstarSystem.Instance.player;
        private Vector3 PlayerForward => PlayerTransform.forward;
        private Vector3 PlayerPosition => PlayerTransform.position;
        private float NorthAngle => GlobalNorthstarSettings.Instance.NorthAngle;
        private float WorldNorth => GlobalNorthstarSettings.Instance.WorldNorth;
        
        public float Angle => transform.rotation.eulerAngles.z;
        
        private Radar Radar => NorthstarSystem.Instance as Radar;
        private bool _usingRadar;
        public bool RespondToRadar => _usingRadar && !ignoreRadar;
        public bool CanUpdateRadarPosition => RespondToRadar && (pinged || Radar.RadarInstance.showPingMovement);
        public float PingedAlphaValue = 1f;
        
        [HideInInspector] public bool preCreated = false;

        private Color _originalColor;
        private float _originalOpacity;
        private Vector3 _originalLocalScale;
        
        private void Awake()
        {
            if (image == null) image = GetComponent<Image>();
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            _originalColor = image.color;
            _originalOpacity = _originalColor.a;
            _originalLocalScale = transform.localScale;
        }
        
        protected virtual void LateUpdate()
        {
            if (NorthstarSystem.Instance == null) return;
            if (NorthstarSystem.Instance.PauseLevel == NorthstarPauseLevel.FullPause)
                return;
            
            SetColor();
            SetScale();
            
            switch (trackingType)
            {
                case TrackingType.PointNorth:
                    SetCompass();
                    break;
                case TrackingType.PointAtTarget:
                    if (adjustBasedOnPlayerIcon)
                        PointAtTargetAdjusted();
                    else
                        PointAtTarget();
                    break;
                case TrackingType.RelativePoint:
                    UpdatePosition();
                    HandleRadarPingAndFade();
                    break;
                case TrackingType.DoNotMove:
                    break;
                case TrackingType.PlayerForward:
                    PointAtPlayerForward();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (RespondToRadar)
                pinged = false; // Reset for next frame
            
            
        }

        protected virtual void SetScale()
        {
            var targetScale = overrideScale ? overrideScaleValue : _originalLocalScale;
            if (transform.localScale != targetScale)
                transform.localScale = targetScale;
        }

        protected virtual void SetColor()
        {
            image.color = overrideColor ? overrideColorValue : _originalColor;
        }

        private void Start() => _usingRadar = NorthstarSystem.Instance is Radar;

        protected virtual void SetCompass()
        {
            if (NorthstarSystem.Instance.PauseLevel is NorthstarPauseLevel.PauseRotation 
                or NorthstarPauseLevel.FullPause)
                return;
            
            // Calculate the angle between the target's forward vector and the world north.
            var targetAngle = Vector3.SignedAngle(Vector3.forward, PlayerForward, Vector3.up);

            // Adjusting for northAngle and worldNorth
            var compassRotation = targetAngle + NorthAngle - WorldNorth;
    
            // Invert the compass rotation direction
            if (!invertRotation)
                compassRotation = 360 - compassRotation;
            
            // Clamp the rotation between 0 and 360 degrees
            compassRotation = compassRotation % 360;
            if (compassRotation < 0)
                compassRotation += 360;

            // Set the compass direction
            _compassDirection = new Vector3(0, 0, compassRotation);
            transform.localEulerAngles = _compassDirection;
        }
        
        private float MapValue(float value, float inMin, float inMax, float outMin, float outMax) 
            => (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
        
        protected virtual void PointAtTarget()
        {
            if (NorthstarSystem.Instance.PauseLevel is NorthstarPauseLevel.PauseRotation 
                or NorthstarPauseLevel.FullPause)
                return;
            
            if (target == null)
                return;

            transform.localEulerAngles = new Vector3(0, 0, GetAngle());
        }
        
        protected virtual void PointAtTargetAdjusted()
        {
            
            if (NorthstarSystem.Instance.PauseLevel is NorthstarPauseLevel.PauseRotation 
                or NorthstarPauseLevel.FullPause)
                return;
            
            if (target == null)
                return;

            if (NorthstarSystem.Instance.PlayerIcon == null)
            {
                PointAtTarget();
                return;
            }

            var playerAngle = NorthstarSystem.Instance.PlayerIcon.Angle;
    
            var value = 360 - playerAngle;
            var newAngle = (GetAngle() - value + 360) % 360;
    
            transform.localEulerAngles = new Vector3(0, 0, newAngle);
        }

        protected virtual void PointAtPlayerForward()
        {
            if (NorthstarSystem.Instance.PauseLevel is NorthstarPauseLevel.PauseRotation 
                or NorthstarPauseLevel.FullPause)
                return;

            // Calculate the angle between the player's forward vector and the world north direction
            var angleBetweenPlayerAndWorldNorth = Vector3.SignedAngle(Vector3.forward, PlayerForward, Vector3.up);

            // Calculate the angle between the player's forward vector and the compass north (up)
            //float compassRotation = angleBetweenPlayerAndWorldNorth + WorldNorth - NorthAngle;
            var compassRotation = 360 - (angleBetweenPlayerAndWorldNorth + WorldNorth - NorthAngle);

    
            // Normalize the angle to the [0, 360) range
            compassRotation = (compassRotation + 360) % 360;

            //Debug.Log($"angleBetweenPlayerAndWorldNorth: {angleBetweenPlayerAndWorldNorth}; compassRotation: {compassRotation}; WorldNorth: {WorldNorth}; NorthAngle: {NorthAngle}");
            // Set the z rotation of this object to the calculated angle
            transform.localEulerAngles = new Vector3(0, 0, compassRotation);
        }
        
        public virtual float GetAngle()
        {
            var toTargetDirection = (target.position - PlayerPosition).normalized;
            toTargetDirection.y = 0; // here, we're ignoring difference in height
            var adjustedPlayerForward = PlayerForward;
            adjustedPlayerForward.y = 0; // again, ignoring the tilt of player's forward vector
            
            var angleBetweenPlayerAndTarget = Vector3.SignedAngle(adjustedPlayerForward, toTargetDirection, Vector3.up);
            angleBetweenPlayerAndTarget = -angleBetweenPlayerAndTarget;
            var compassRotation = (angleBetweenPlayerAndTarget + 360) % 360;
            return compassRotation;
        }
        
        private float FadeTime => Radar.RadarInstance.radarPingFadeTime;
        
        public virtual void Ping()
        {
            pinged = true;
            alphaValue = 1;
            timer = 0;
            SetAlpha();
        }
        
        private bool OutsideDetectionRange => DistanceToTarget() > NorthstarSystem.Instance.detectionRange;
        
        private void UpdatePosition()
        {
            if (NorthstarSystem.Instance.PauseLevel is NorthstarPauseLevel.PauseMovement
                or NorthstarPauseLevel.FullPause)
                return;

            // Cache values -- Direction first, then distance, required to compute ping.
            DirectionToTarget(true);
            DistanceToTarget(true);
            if (OutsideDetectionRange)
                return;
            YDelta(true);
            
            if (NorthstarSystem.Instance.fadeOnYDelta 
                && (YDelta() > NorthstarSystem.Instance.maxYDeltaFadeRangeAbove.y 
                    || YDelta() < NorthstarSystem.Instance.maxYDeltaFadeRangeBelow.x))
            {
                //Debug.Log($"YDelta: {YDelta()}; Min: {NorthstarSystem.Instance.maxYDeltaFadeRangeBelow.x}; Max: {NorthstarSystem.Instance.maxYDeltaFadeRangeAbove.y}");
                return;
            }
            
            if (CanUpdateRadarPosition)
                UpdatePositionRadar();
            if (RespondToRadar)
                return;
            
            UpdatePositionCompass();
            return;
           
        }

        private void UpdatePositionRadar()
        {
            var playerPosition2D = new Vector3(PlayerPosition.x, 0, PlayerPosition.z);
            var targetPosition2D = new Vector3(target.position.x, 0, target.position.z);

            var directionToTarget = targetPosition2D - playerPosition2D;
            _directionToTarget = directionToTarget;

            var angleFromNorth = Vector3.SignedAngle(Vector3.right,
                new Vector3(directionToTarget.x, 0, directionToTarget.z), -Vector3.up);

            var adjustedAngleFromNorth = angleFromNorth + WorldNorth - NorthAngle;

            if (adjustedAngleFromNorth < 0)
                adjustedAngleFromNorth += 360;

            var distanceToTarget = new Vector3(directionToTarget.x, 0, directionToTarget.z).magnitude;
            _distanceToTarget = distanceToTarget;

            // Interpolate the radar distance based on where the target distance lies within distance range
            var distanceNormalized = Mathf.InverseLerp(NorthstarSystem.Instance.objectDistanceRange.x,
                NorthstarSystem.Instance.objectDistanceRange.y, distanceToTarget);
            var radarDistance = Mathf.Lerp(NorthstarSystem.Instance.rangeFromCenter.x,
                NorthstarSystem.Instance.rangeFromCenter.y, distanceNormalized);

            var radarX = radarDistance * Mathf.Cos(adjustedAngleFromNorth * Mathf.Deg2Rad);
            var radarY = radarDistance * Mathf.Sin(adjustedAngleFromNorth * Mathf.Deg2Rad);

            rectTransform.anchoredPosition = new Vector2(radarX, radarY);
        }

        private void UpdatePositionCompass()
        {
            if (OutsideDistanceFadeRange())
                return;

            var radarDistance = Mathf.Lerp(
                NorthstarSystem.Instance.rangeFromCenter.x,
                NorthstarSystem.Instance.rangeFromCenter.y,
                Mathf.InverseLerp(NorthstarSystem.Instance.objectDistanceRange.x
                    , NorthstarSystem.Instance.objectDistanceRange.y
                    , DistanceToTarget()));

            Vector2 radarDirection;
            // Calculate 2D direction on radar
            radarDirection = new Vector2(
                Vector3.Dot(DirectionToTarget(), transform.right),
                Vector3.Dot(DirectionToTarget(), transform.forward));

            radarDirection.Normalize();
            radarDirection *= radarDistance;

            if (rectTransform == null) return;
            rectTransform.anchoredPosition = radarDirection;
        }

        private Vector3 _directionToTarget;
        public Vector3 DirectionToTarget(bool cache = false)
        {
            if (!cache)
                return _directionToTarget;
            
            _directionToTarget =  target.position - PlayerPosition;
            _directionToTarget.y = 0;
            _directionToTarget = PlayerTransform.InverseTransformDirection(_directionToTarget);
            return _directionToTarget;
        }

        private float _yDelta;
        public float YDelta(bool cache = false)
        {
            if (!cache)
                return _yDelta;
            
            // We want value to be < 0 if target is below player
            _yDelta = PlayerPosition.y -target.position.y;
            return _yDelta;
        }
        
        private float _distanceToTarget;
        public float DistanceToTarget(bool cache = false)
        {
            if (!cache) return _distanceToTarget;
            
            _distanceToTarget = DirectionToTarget().magnitude;
            return _distanceToTarget;
        }

        private bool WithinDistanceFadeRange(bool cache = false) => DistanceToTarget(cache) <= NorthstarSystem.Instance.maxDistanceFadeRange.x;
        private bool OutsideDistanceFadeRange(bool cache = false) => DistanceToTarget(cache) >= NorthstarSystem.Instance.maxDistanceFadeRange.y;

        // Returns the maximum allowed alpha based on distance settings
        private float MaxAlphaFromDistance()
        {
            if (WithinDistanceFadeRange()) return 1f;
            if (OutsideDistanceFadeRange()) return 0f;
            return Mathf.InverseLerp(NorthstarSystem.Instance.maxDistanceFadeRange.y
                , NorthstarSystem.Instance.maxDistanceFadeRange.x
                , DistanceToTarget());
        }

        
        // Returns the maximum allowed alpha based on height settings
        private float MaxAlphaFromHeight()
        {
            if (YDelta() > NorthstarSystem.Instance.maxYDeltaFadeRangeAbove.y // Target is above max height
                || YDelta() < NorthstarSystem.Instance.maxYDeltaFadeRangeBelow.x) // Target is below min height
                return 0f;
            
            // Target is within height range
            if (YDelta() <= NorthstarSystem.Instance.maxYDeltaFadeRangeAbove.x
                && YDelta() >= NorthstarSystem.Instance.maxYDeltaFadeRangeBelow.y)
                return 1f;
            
            if (YDelta() < 0) // Target is below player
            {
                var alphaByHeight = Mathf.InverseLerp(NorthstarSystem.Instance.maxYDeltaFadeRangeBelow.x
                    , NorthstarSystem.Instance.maxYDeltaFadeRangeBelow.y
                    , YDelta());
                return Mathf.Clamp(alphaByHeight, 0f, 1f);
            }
            
            // Target is above player
            var alphaByHeightAbove = Mathf.InverseLerp(NorthstarSystem.Instance.maxYDeltaFadeRangeAbove.y
                , NorthstarSystem.Instance.maxYDeltaFadeRangeAbove.x
                , YDelta());
            return Mathf.Clamp(alphaByHeightAbove, 0f, 1f);
        }
        
        private float MaxAlpha()
        {
            // If we are outside the detection range, return 0
            if (OutsideDetectionRange)
                return 0;
            
            // if we are using the radar, get the ping max alpha
            var radarMax = RespondToRadar ? GetPingedAlphaValue() : 1f;
            if (radarMax == 0)
                return 0;
            
            // If we are fading on distance, get the distance max alpha
            var distanceMax = NorthstarSystem.Instance.fadeOnDistance ? MaxAlphaFromDistance() : 1f;
            if (distanceMax == 0)
                return 0;
            
            // If we are fading on y delta, get the height max alpha
            var heightMax = NorthstarSystem.Instance.fadeOnYDelta ? MaxAlphaFromHeight() : 1f;
            if (heightMax == 0)
                return 0;

            // Check for runtime override
            var minValue = Mathf.Min(radarMax, distanceMax, heightMax);
            minValue = overrideOpacity ? Mathf.Max(minValue, overrideOpacityValue) : minValue;
            
            // We return the minimum allowed of the three values
            return minValue;
        }

        private void HandleRadarPingAndFade() => SetAlpha(MaxAlpha());
        
        private float GetPingedAlphaValue()
        {
            if (NorthstarSystem.Instance.PauseLevel is NorthstarPauseLevel.FullPause 
                or NorthstarPauseLevel.PauseMovement) return 1f;
            if (!Radar.RadarInstance.fadeRadarPings) return 1f;
            
            if (pinged) return 1f; // We are currently pinged, so stay at full alpha.
            if (timer >= FadeTime) return 0f; // We have faded out, so stay at 0 alpha.
            
            timer += Time.deltaTime;
            alphaValue = Radar.RadarInstance.radarPingFadeCurve.Evaluate(timer / FadeTime);
            PingedAlphaValue = alphaValue;
            return PingedAlphaValue;
        }

        private void SetAlpha(float value = 1f) 
            => SetImageAlpha(image, overrideOpacity 
                ? Mathf.Max(value, overrideOpacityValue) 
                : value);

        // Runtime Overrides
        
        public bool overrideColor = false;
        public Color overrideColorValue = Color.black;
        
        public virtual void SetOverrideColor(Color value)
        {
            overrideColor = true;
            overrideColorValue = value;
        }
        
        public virtual void ResetOverrideColor() => overrideColor = false;
        
        public bool overrideOpacity = false;
        public float overrideOpacityValue = 1f;
        
        public virtual void SetOverrideOpacity(float value)
        {
            overrideOpacity = true;
            overrideOpacityValue = value;
        }
        
        public virtual void ResetOverrideOpacity() => overrideOpacity = false;
        
        public bool overrideScale = false;
        public Vector3 overrideScaleValue = Vector3.one;
        
        public virtual void SetOverrideScale(Vector3 value)
        {
            overrideScale = true;
            overrideScaleValue = value;
        }
        
        public virtual void ResetOverrideScale() => overrideScale = false;
    }

}
