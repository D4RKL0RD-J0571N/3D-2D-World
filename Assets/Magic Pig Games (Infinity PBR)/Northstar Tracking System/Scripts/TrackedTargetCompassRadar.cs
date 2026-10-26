using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using static MagicPigGames.Northstar.GlobalNorthstarSettings;

/*
 * This is the "Tracked Target" class for the compass and radar system. It is different from the Tracked Target
 * class for the overlay, as this only handles the compass and radar.
 *
 * This goes on any object that you want to track with the compass and radar. The object can have both this and
 * the overlay Tracked Target class on it.
 */

namespace MagicPigGames.Northstar
{
    [System.Serializable]
    public class TrackedTargetCompassRadar : MonoBehaviour
    {
        public TrackingType trackingType = TrackingType.RelativePoint;
        public GameObject trackedObjectPrefab;
        public bool adjustBasedOnPlayerIcon = false;

        public string targetType = "Default";
        
        // Runtime only
        [FormerlySerializedAs("trackedIcon")] [HideInInspector] public CompassIcon compassIcon;

        private Coroutine _registerCoroutine;

        protected virtual void OnEnable() => _registerCoroutine = StartCoroutine(RegisterWithNorthstarSystem());

        protected virtual void OnDisable()
        {
            // Disabling a component does NOT stop its coroutines -- stop the pending registration
            // so we don't register after being disabled (or stack coroutines on re-enable).
            if (_registerCoroutine != null)
            {
                StopCoroutine(_registerCoroutine);
                _registerCoroutine = null;
            }
            RemoveRegistration();
        }

        // Removes the registration from the Northstar system.
        public virtual void RemoveRegistration()
        {
            if (NorthstarSystem.Instance == null) return;

            NorthstarSystem.Instance.RemoveTarget(transform);
        }

        // Use a coroutine in case the Compass system is not yet active.
        protected virtual IEnumerator RegisterWithNorthstarSystem()
        {
            yield return new WaitUntil(() => NorthstarSystem.Instance != null);
            compassIcon = NorthstarSystem.Instance.AddTarget(this);
            _registerCoroutine = null;
            if (compassIcon == null)
                Debug.LogWarning("Unable to register with compass.");
        }
        
        public virtual void SetOverrideColor(CompassIcon icon, Color value) 
            => icon.SetOverrideColor(value);
        public virtual void ResetOverrideColor(CompassIcon icon) 
            => icon.ResetOverrideColor();
    }
}
