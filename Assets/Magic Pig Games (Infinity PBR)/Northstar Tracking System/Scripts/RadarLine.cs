using System.Collections;
using UnityEngine;
using static MagicPigGames.Northstar.GlobalNorthstarSettings;

/*
 * This should be attached to the "line" UI element which rotates around the radar.
 */

namespace MagicPigGames.Northstar
{
    [System.Serializable]
    public class RadarLine : MonoBehaviour
    {
        [Tooltip("Determines whether the line rotates clockwise or counter-clockwise.")]
        public bool clockwise = true;

        public float Angle => transform.rotation.eulerAngles.z;
        public float LastAngle { get; private set; }

        private float _additionalRotation = 0;
        public void SetAdditionalRotation(float angle) => _additionalRotation = angle;
        public GlobalNorthstarSettings NorthstarSettings => Radar.RadarInstance.NorthstarSettings;
        
        private void OnEnable() => StartCoroutine(StartActions());
        
        private void LateUpdate()
        {
            if (Radar.RadarInstance.PauseLevel == NorthstarPauseLevel.PauseMovement ||
                Radar.RadarInstance.PauseLevel == NorthstarPauseLevel.FullPause)
                return;
            
            LastAngle = Angle;
            var newAngle = transform.rotation.eulerAngles.z
                           + ((clockwise
                               ? -Radar.RadarInstance.radarRotationSpeed
                               : Radar.RadarInstance.radarRotationSpeed)
                           * Time.deltaTime)
                           + _additionalRotation;
            _additionalRotation = 0f; // one-shot offset; callers re-set each frame if needed
            transform.rotation = Quaternion.Euler(0, 0, newAngle);
        }
        
        private IEnumerator StartActions()
        {
            yield return new WaitUntil(() => Radar.RadarInstance != null);
            LastAngle = transform.rotation.eulerAngles.z;
        }
    }
}