using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicPigGames.Northstar.Demo
{
    public interface IInteractable
    {
        public void Interact(Key interactionKey);
    }
}
