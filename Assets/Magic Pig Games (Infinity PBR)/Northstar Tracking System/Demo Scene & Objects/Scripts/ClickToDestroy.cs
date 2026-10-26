using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicPigGames.Northstar
{
    public class ClickToDestroy : MonoBehaviour
    {
        // When the player clicks on this object, destroy it
        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            // Check to see if we are over this object
            var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit))
                return;

            // If the hit is not this object, return
            if (hit.transform.gameObject != gameObject)
                return;

            Destroy(gameObject);
        }
    }

}
