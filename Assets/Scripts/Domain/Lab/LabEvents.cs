namespace Domain.Lab
{
    using Core;

    /// <summary>Published when a lab parameter value changes at runtime.</summary>
    public sealed class LabParameterChangedEvent : GameEvent
    {
        public string GroupId;
        public string ParameterId;
        public float OldValue;
        public float NewValue;
    }

    /// <summary>Published to request toggling the Lab HUD panel visibility.</summary>
    public sealed class ToggleLabHudEvent : GameEvent { }
}
