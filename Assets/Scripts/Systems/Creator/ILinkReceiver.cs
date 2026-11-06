namespace Systems.Creator
{
    using UnityEngine;

    /// <summary>
    /// Contract implemented by interactive props, doors, traps, and spawners that can receive
    /// directed <see cref="Domain.Creator.CrossObjectLink"/> triggers authored in the In-Game Editor (<c>ARCH-05</c>).
    /// </summary>
    public interface ILinkReceiver
    {
        /// <summary>
        /// Executes a named action (e.g., <c>"Activate"</c>, <c>"Deactivate"</c>, <c>"Toggle"</c>, <c>"Unlock"</c>, <c>"Open"</c>)
        /// triggered by a linked source object.
        /// </summary>
        void ExecuteLinkAction(string actionName, GameObject sourceObject = null);
    }
}
