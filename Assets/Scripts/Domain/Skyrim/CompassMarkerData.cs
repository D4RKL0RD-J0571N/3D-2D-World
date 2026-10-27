namespace Domain.Skyrim
{
    using UnityEngine;

    /// <summary>
    /// Types of Points of Interest (POIs) that can be tracked on the Skyrim-style compass.
    /// </summary>
    public enum CompassPOIType
    {
        Dungeon = 0,
        Crypt = 1,
        BanditCamp = 2,
        Shrine = 3,
        Town = 4,
        QuestMarker = 5,
        Enemy = 6,
        Custom = 7
    }

    /// <summary>
    /// Runtime descriptor for a Point of Interest (POI) displayed on the compass bar.
    /// </summary>
    public class CompassMarkerData
    {
        #region Properties

        public string Id { get; set; }
        public string Title { get; set; }
        public CompassPOIType Type { get; set; }
        public Vector3 WorldPosition { get; set; }
        public float MaxVisibleDistance { get; set; } = 250f;
        public bool IsDiscovered { get; set; }
        public Transform TargetTransform { get; set; }

        #endregion

        #region Constructors

        public CompassMarkerData() { }

        public CompassMarkerData(string id, string title, CompassPOIType type, Vector3 worldPosition, float maxDistance = 250f)
        {
            Id = id;
            Title = title;
            Type = type;
            WorldPosition = worldPosition;
            MaxVisibleDistance = maxDistance;
            IsDiscovered = false;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns the live world position, prioritizing attached transform if present.
        /// </summary>
        public Vector3 GetPosition()
        {
            return TargetTransform != null ? TargetTransform.position : WorldPosition;
        }

        #endregion
    }
}
