namespace Gameplay.Interactables
{
    using Core;
    using Domain.Combat;
    using Gameplay.Entities;
    using Gameplay.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Bed in the Alternate Start prison cell. Sleeping in this bed finalizes the player's
    /// choice of life made with the Statue of Mara and teleports them into the world.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Interactables/Prison Bed Interactable")]
    public class PrisonBedInteractable : MonoBehaviour, IInteractable
    {
        #region IInteractable

        public string InteractionPrompt => "Sleep in bed to begin your new life";
        public bool CanInteract => true;

        public void OnInteract(BaseActor interactor)
        {
            if (interactor == null) return;

            var altService = ServiceLocator.Get<AlternateStartService>();
            if (altService != null)
            {
                altService.CommitLifeChoice(interactor.gameObject);
                GameEventManager.Publish(new NPCDialogueClosedEvent());
            }
        }

        #endregion
    }
}
