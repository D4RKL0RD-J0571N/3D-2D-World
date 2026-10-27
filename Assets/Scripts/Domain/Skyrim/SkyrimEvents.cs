namespace Domain.Skyrim
{
    using Core;

    /// <summary>
    /// Fired when a new POI marker is registered on the Skyrim compass.
    /// </summary>
    public class CompassMarkerAddedEvent : GameEvent
    {
        public CompassMarkerData Marker;

        public CompassMarkerAddedEvent(CompassMarkerData marker)
        {
            Marker = marker;
        }
    }

    /// <summary>
    /// Fired when a POI marker is removed or unregistered.
    /// </summary>
    public class CompassMarkerRemovedEvent : GameEvent
    {
        public string MarkerId;

        public CompassMarkerRemovedEvent(string markerId)
        {
            MarkerId = markerId;
        }
    }

    /// <summary>
    /// Fired when the player's stealth / detection level updates.
    /// </summary>
    public class StealthStateChangedEvent : GameEvent
    {
        public StealthState State;
        public float DetectionRatio;
        public bool IsCrouching;

        public StealthStateChangedEvent(StealthState state, float detectionRatio, bool isCrouching)
        {
            State = state;
            DetectionRatio = detectionRatio;
            IsCrouching = isCrouching;
        }
    }

    /// <summary>
    /// Fired when an objective or quest location is discovered in the world.
    /// </summary>
    public class LocationDiscoveredEvent : GameEvent
    {
        public string LocationName;
        public CompassPOIType LocationType;

        public LocationDiscoveredEvent(string locationName, CompassPOIType locationType)
        {
            LocationName = locationName;
            LocationType = locationType;
        }
    }
}
