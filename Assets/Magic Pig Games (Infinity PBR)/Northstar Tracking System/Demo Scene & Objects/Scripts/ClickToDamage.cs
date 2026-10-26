using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicPigGames.Northstar
{
    public class ClickToDamage : MonoBehaviour
    {
        [Header("Plumbing")] 
        public TrackedTargetOverlay trackedTargetOverlay;
        
        private SimpleHealthBar _simpleHealthBar;
        private OverlayIcon _screenIcon;

        private bool HasHealthBar => _simpleHealthBar != null;
        
        // When the player clicks on this object, damage it
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

            _simpleHealthBar.TakeDamage();
        }

        private void Awake() => StartCoroutine(FindHealthBar());
        
        private IEnumerator FindHealthBar()
        {
            while (!HasHealthBar)
            {
                _screenIcon = trackedTargetOverlay.ScreenIcon;
                if (_screenIcon != null)
                {
                    _simpleHealthBar = _screenIcon.GetComponentInChildren<SimpleHealthBar>();
                    _simpleHealthBar.SetCharacter(gameObject);
                    _simpleHealthBar.SetOverlayIcon(_screenIcon);
                    yield break;
                }

                yield return null;
            }
            
            yield break;
        }
    }

}
