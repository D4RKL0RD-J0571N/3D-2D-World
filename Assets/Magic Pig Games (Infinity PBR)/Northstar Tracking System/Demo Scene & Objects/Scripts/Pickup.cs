using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using MagicPigGames.Northstar.Demo;

namespace MagicPigGames.Northstar
{
    public class Pickup : MonoBehaviour, IInteractable
    {
        [Header("Plumbing")]
        public TrackedTargetOverlay trackedTargetOverlay;

        private OverlayIcon _screenIcon;
        private PickupsIndicator _pickupsIndicator;
        private Key _interactionKey;

        private void Awake() => StartCoroutine(FindPickupsIndicator());

        private IEnumerator FindPickupsIndicator()
        {
            while (!HasIndicator)
            {
                _screenIcon = trackedTargetOverlay.ScreenIcon;
                if (_screenIcon != null)
                {
                    _pickupsIndicator = _screenIcon.GetComponentInChildren<PickupsIndicator>();
                    yield break;
                }
            
                yield return null;
            }

            yield break;
        }

        private bool HasIndicator => _pickupsIndicator != null;

        public void Interact(Key interactionKey)
        {
            if (_pickupsIndicator == null)
            {
                Debug.Log("PickupsIndicator not found!");
                return;
            }
            _interactionKey = interactionKey;
            _pickupsIndicator.StartProgress(_interactionKey);
        }
    }

}
