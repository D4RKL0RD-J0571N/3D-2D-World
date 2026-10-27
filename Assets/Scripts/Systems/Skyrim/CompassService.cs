namespace Systems.Skyrim
{
    using System.Collections.Generic;
    using Core;
    using Domain.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Service managing the Skyrim-style compass. Calculates player heading,
    /// tracks active world markers (caves, ruins, camps, quest markers, hostiles),
    /// and projects them into normalized coordinates for the compass HUD strip.
    /// </summary>
    public class CompassService : IGameService, ITickable
    {
        #region Fields

        private readonly Dictionary<string, CompassMarkerData> _markers = new();
        private Camera _mainCamera;
        private Transform _cameraTransform;

        private const float FieldOfViewAngle = 140f; // Compass horizontal FOV
        private const float DiscoveryRadius = 16f;

        #endregion

        #region Properties

        /// <summary>Current compass heading in degrees (0 = North, 90 = East, 180 = South, 270 = West).</summary>
        public float CurrentHeading { get; private set; }

        /// <summary>All active registered compass markers.</summary>
        public IReadOnlyCollection<CompassMarkerData> Markers => _markers.Values;

        #endregion

        #region IGameService & ITickable Lifecycle

        /// <inheritdoc />
        public void Initialize()
        {
            ResolveCamera();
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _markers.Clear();
            _mainCamera = null;
            _cameraTransform = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_cameraTransform == null)
            {
                ResolveCamera();
                if (_cameraTransform == null) return;
            }

            UpdateHeading();
            CheckDiscovery();
        }

        #endregion

        #region Setup & Helpers

        private void ResolveCamera()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_mainCamera != null)
            {
                _cameraTransform = _mainCamera.transform;
            }
        }

        private void UpdateHeading()
        {
            Vector3 forward = _cameraTransform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                CurrentHeading = (angle + 360f) % 360f;
            }
        }

        private void CheckDiscovery()
        {
            Vector3 playerPos = _cameraTransform.position;

            foreach (var marker in _markers.Values)
            {
                if (marker.IsDiscovered) continue;

                float dist = Vector3.Distance(playerPos, marker.GetPosition());
                if (dist <= DiscoveryRadius)
                {
                    marker.IsDiscovered = true;
                    GameEventManager.Publish(new LocationDiscoveredEvent(marker.Title, marker.Type));
                }
            }
        }

        #endregion

        #region Public Marker Management

        /// <summary>
        /// Registers a marker on the compass.
        /// </summary>
        public void RegisterMarker(CompassMarkerData marker)
        {
            if (marker == null || string.IsNullOrEmpty(marker.Id)) return;

            _markers[marker.Id] = marker;
            GameEventManager.Publish(new CompassMarkerAddedEvent(marker));
        }

        /// <summary>
        /// Unregisters a marker by ID.
        /// </summary>
        public void UnregisterMarker(string markerId)
        {
            if (string.IsNullOrEmpty(markerId)) return;

            if (_markers.Remove(markerId))
            {
                GameEventManager.Publish(new CompassMarkerRemovedEvent(markerId));
            }
        }

        /// <summary>
        /// Projects a world position onto the compass strip.
        /// Returns true if within view angle (-0.5 to +0.5 normalized range).
        /// </summary>
        public bool TryGetNormalizedCompassOffset(Vector3 targetWorldPos, out float normalizedOffset, out float distance)
        {
            normalizedOffset = 0f;
            distance = 0f;

            if (_cameraTransform == null) return false;

            Vector3 toTarget = targetWorldPos - _cameraTransform.position;
            distance = toTarget.magnitude;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.01f) return false;

            Vector3 cameraForward = _cameraTransform.forward;
            cameraForward.y = 0f;

            // Signed angle from camera forward to target (-180 to +180)
            float signedAngle = Vector3.SignedAngle(cameraForward, toTarget, Vector3.up);

            // Check if within compass visibility cone
            float halfFov = FieldOfViewAngle * 0.5f;
            if (Mathf.Abs(signedAngle) > halfFov)
            {
                return false;
            }

            // Map angle to -0.5 to +0.5 where 0 is center of compass
            normalizedOffset = signedAngle / FieldOfViewAngle;
            return true;
        }

        #endregion
    }
}
