namespace Gameplay.Interactables
{
    using System.Collections.Generic;
    using Core;
    using Domain.Combat;
    using Domain.Dialogue;
    using Gameplay.Entities;
    using Gameplay.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Interactable shrine representing the Statue of Mara in the Alternate Start prison cell.
    /// Allows the player to converse with Mara to choose their starting life scenario.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Interactables/Statue Of Mara Interactable")]
    public class StatueOfMaraInteractable : MonoBehaviour, IInteractable
    {
        #region IInteractable

        public string InteractionPrompt => "Pray to Statue of Mara";
        public bool CanInteract => true;

        public void OnInteract(BaseActor interactor)
        {
            if (interactor == null) return;

            var altService = ServiceLocator.Get<AlternateStartService>();
            if (altService == null) return;

            var topics = new List<DialogueTopic>();
            for (int i = 0; i < altService.Scenarios.Count; i++)
            {
                var scenario = altService.Scenarios[i];
                string scId = scenario.ScenarioId;
                topics.Add(new DialogueTopic(scenario.MaraPrompt, scenario.Description, scId));
            }

            string greeting = "Speak, mortal. The Goddess Mara listens. Who were you before you arrived in this place?";
            GameEventManager.Publish(new NPCDialogueOpenedEvent("Statue of Mara", "Divine Mother", greeting, topics));
        }

        #endregion
    }
}
