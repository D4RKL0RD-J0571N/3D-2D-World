using UnityEngine;

/*
 * Radar inherits from NorthstarSystem, and adds additional functionality, beyond what the Compass does. Specifically
 * a radar has the ability to "ping" tracked objects.
 *
 * You can override this class to create your own custom Radar.
 */

namespace MagicPigGames.Northstar
{
    public class Radar : NorthstarSystem
    {
        public static Radar RadarInstance { get; private set; }

        [Header("Radar Options")] 
        public bool rotateWithPlayer = true;
        [Tooltip("This is the line which rotates around the compass.")]
        public GameObject radarLineObject;
        [Tooltip("Speed at which the line rotates. 360 = 1 rotation per second. Set to 0 to disable the rotating line (uses pingInterval instead).")]
        public float radarRotationSpeed = 180;
        [Tooltip("Ping interval in seconds when radarRotationSpeed is 0 (no rotating line).")]
        public float pingInterval = 2f;
        [Tooltip("When true, radar pings will fade out over time.")]
        public bool fadeRadarPings = true;
        [Tooltip("How long it takes for the ping to fade to 0.")]
        public float radarPingFadeTime = 1f;
        [Tooltip("Optional curve to use for fading the radar pings.")]
        public AnimationCurve radarPingFadeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [Tooltip("When true, pings will continue to move, otherwise, they will remain stationary until the next time they are painted.")]
        public bool showPingMovement = false;
        
        private RadarLine _radarLine;
        private float _globalPaintTimer = 0;
        private float NorthAngle => GlobalNorthstarSettings.Instance.NorthAngle;
        private float WorldNorth => GlobalNorthstarSettings.Instance.WorldNorth;
        
        private Transform Player => NorthstarSystem.Instance.player;
        private Vector3 PlayerForward => Player.forward;
        private Vector3 PlayerPosition => Player.position;

        protected override void Awake()
        {
            if (RadarInstance != null)
            {
                Debug.LogError("There can only be one Compass / Radar in the scene.");
                Destroy(this);
                return;
            }

            RadarInstance = this;

            base.Awake(); // This calls the Awake method in NorthstarSystem

            SetDictionary();
            ResetTimer();
        }

        protected override void OnDestroy()
        {
            if (RadarInstance == this) RadarInstance = null;
            base.OnDestroy();
        }

        protected virtual void ResetTimer()
            => _globalPaintTimer = radarRotationSpeed > 0 ? 360f / radarRotationSpeed : pingInterval;

        protected virtual void Start() => CheckRadarLineObjectForRadarLineComponent();

        protected virtual void CheckRadarLineObjectForRadarLineComponent()
        {
            if (radarLineObject == null) return;
            
            _radarLine = radarLineObject.GetComponent<RadarLine>();
            
            if (_radarLine == null)
                Debug.LogError("Radar Line Object must have a RadarLine component!");
        }

        protected virtual void LateUpdate()
        {
            PaintTrackedObjects();
            RotateWithPlayer();
        }

        protected virtual void RotateWithPlayer()
        {
            if (!rotateWithPlayer) return;
            
            // Calculate the angle between the player's forward vector and the world north direction
            var angleBetweenPlayerAndWorldNorth = Vector3.SignedAngle(Vector3.forward, PlayerForward, Vector3.up);

            // Calculate the angle between the player's forward vector and the compass north (up)
            //float compassRotation = angleBetweenPlayerAndWorldNorth + WorldNorth - NorthAngle;
            var compassRotation = 360 - (angleBetweenPlayerAndWorldNorth + WorldNorth - NorthAngle);

    
            // Normalize the angle to the [0, 360) range
            compassRotation = (compassRotation + 360) % 360;

            //Debug.Log($"angleBetweenPlayerAndWorldNorth: {angleBetweenPlayerAndWorldNorth}; compassRotation: {compassRotation}; WorldNorth: {WorldNorth}; NorthAngle: {NorthAngle}");
            // Set the z rotation of this object to the calculated angle
            transform.localEulerAngles = new Vector3(0, 0, -compassRotation);
        }
        
        // This method will determine whether we paint all objects periodically, or via the radar line.
        protected virtual void PaintTrackedObjects()
        {
            if (_radarLine == null)
                PaintAllTimed();
            else
                PaintWithRadarLine();
        }

        // Periodically paint ALL objects, since we don't have the line.
        protected virtual void PaintAllTimed()
        {
            if (_globalPaintTimer > 0)
            {
                _globalPaintTimer -= Time.deltaTime;
                return;
            }

            foreach (var compassIcon in CompassIcons)
            {
                if (compassIcon.Value.DistanceToTarget() > objectDistanceRange.y) continue;
                compassIcon.Value.Ping();
            }
            
            ResetTimer();
        }

        // Iterate through the tracked objects and compare their angles to the radar line
        private void PaintWithRadarLine()
        {
            if (CompassIcons.Count <= 0) return;

            // Get the angle range of the radar line
            var min = _radarLine.LastAngle;
            var max = _radarLine.Angle;
            if (_radarLine.clockwise)
                (min, max) = (max, min);

            foreach (var (key, trackedIcon) in CompassIcons)
            {
                if (trackedIcon.ignoreRadar) continue;
                if (_presetTrackedObjectsSet.Contains(trackedIcon)) continue;
                if (trackedIcon.DistanceToTarget() > objectDistanceRange.y) continue;

                // Get the angle between the player and the tracked object
                var angle = (NorthstarUtilities
                    .GetAngleBetweenPoints(
                        Player.position, 
                        key.position, 
                        NorthAngle) + 360) % 360;
                angle = angle < 0 ? Mathf.Abs(angle) : 360 - angle;
                
                // If the angle is not between the radar line angles, then skip
                if (!angle.BetweenAngle(min, max))
                    continue;
                
                trackedIcon.Ping();
            }
        }
    }
}
