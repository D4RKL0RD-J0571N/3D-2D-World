namespace Gameplay.Interactables
{
    /// <summary>
    /// Contract for any world object that the player can interact with via key press or proximity prompt.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Text shown in the interaction prompt HUD.</summary>
        string InteractionPrompt { get; }

        /// <summary>Whether interaction is currently possible (not locked, not used up, etc.).</summary>
        bool CanInteract { get; }

        /// <summary>Called when the player confirms interaction with this object.</summary>
        void OnInteract(Gameplay.Entities.BaseActor interactor);
    }
}
