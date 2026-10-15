namespace Domain.Interaction
{
    using Core;

    public class GridBlockClickedEvent : GameEvent
    {
        public GridHitInfo HitInfo;
        public int MouseButton; // 0 for left, 1 for right
    }
}
