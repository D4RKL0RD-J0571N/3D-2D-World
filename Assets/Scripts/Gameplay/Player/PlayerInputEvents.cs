namespace Gameplay.Player
{
    using Core;
    using UnityEngine;

    public sealed class PlayerMoveInputEvent : GameEvent
    {
        public Vector2 Move;
    }

    public sealed class PlayerLookInputEvent : GameEvent
    {
        public Vector2 LookDelta;
    }

    public sealed class PlayerRunInputEvent : GameEvent
    {
        public bool Held;
    }

    public sealed class PlayerJumpInputEvent : GameEvent
    {
    }

    public sealed class PlayerCrouchInputEvent : GameEvent
    {
        public bool Held;
    }

    public sealed class PlayerInteractPrimaryInputEvent : GameEvent
    {
    }

    public sealed class PlayerInteractSecondaryInputEvent : GameEvent
    {
    }

    public sealed class ToggleWorldMapInputEvent : GameEvent
    {
    }

    public sealed class ToggleMinimapInputEvent : GameEvent
    {
    }
}
