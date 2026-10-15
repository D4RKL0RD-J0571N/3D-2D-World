namespace Domain.Entities
{
    using Core;

    public class RoomEnteredEvent : GameEvent
    {
        public string RoomId;
        public UnityEngine.Vector2Int RoomGridPosition;
    }

    public class RoomExitedEvent : GameEvent
    {
        public string RoomId;
    }

    public class AreaTierChangedEvent : GameEvent
    {
        public string RoomId;
        public AreaSimulationTier PreviousTier;
        public AreaSimulationTier NewTier;
    }
}
