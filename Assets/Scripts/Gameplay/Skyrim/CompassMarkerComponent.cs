namespace Gameplay.Skyrim
{
    using Core;
    using Domain.Skyrim;
    using Systems.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Attach to any world GameObject (dungeon entrance, chest, bandit camp,
    /// pedestal, quest objective) to display it on the Skyrim-style compass.
    /// </summary>
    [AddComponentMenu("3D 2D World/Skyrim/Compass Marker Component")]
    public class CompassMarkerComponent : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Marker Settings")]
        [SerializeField] private string _markerId;
        [SerializeField] private string _markerTitle = "Point of Interest";
        [SerializeField] private CompassPOIType _markerType = CompassPOIType.Dungeon;
        [SerializeField] private float _visibleDistance = 250f;

        #endregion

        #region Private Fields

        private CompassMarkerData _markerData;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (string.IsNullOrEmpty(_markerId))
            {
                _markerId = $"{name}_{GetInstanceID()}";
            }

            _markerData = new CompassMarkerData(_markerId, _markerTitle, _markerType, transform.position, _visibleDistance)
            {
                TargetTransform = transform
            };
        }

        private void OnEnable()
        {
            RegisterMarker();
        }

        private void OnDisable()
        {
            UnregisterMarker();
        }

        #endregion

        #region Registration

        private void RegisterMarker()
        {
            if (ServiceLocator.TryGet<CompassService>(out var compass))
            {
                compass.RegisterMarker(_markerData);
            }
        }

        private void UnregisterMarker()
        {
            if (ServiceLocator.TryGet<CompassService>(out var compass))
            {
                compass.UnregisterMarker(_markerId);
            }
        }

        #endregion
    }
}
