using UnityEditor;
using UnityEngine;
using static MagicPigGames.Shared.MagicPigEditor;
using static MagicPigGames.Northstar.NorthstarSystemEditor;

namespace MagicPigGames.Northstar
{
    [CustomEditor(typeof(Radar))]
    public class RadarEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var radar = (Radar)target;

            LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/northstar-tracking-system/northstar-documentation/compass-and-radar/radar");
            
            Header1($"Radar");
            Label($"<color=#999999>There are <color=#99ffff><b>{radar.presetTrackedObjects.Count}</b> Compass Objects</color> in this Radar.</color>", false, true, true);
            GreyLine();
            RequiredFields(radar);
            GreyLine();
            RadarOptions(radar);
            GreyLine();
            CompassObjects(radar);
            GreyLine();
            EditorUtility.SetDirty(radar);
        }
        
        // When the Inspector is viewed, this will populate all the Compass Objects automatically.
        private void OnEnable()
        {
            var compass = (Radar)target;

            if (!Application.isPlaying)
            {
                foreach (Transform child in compass.transform)
                {
                    var trackedObject = child.GetComponent<CompassIcon>();
                    if (trackedObject == null || compass.presetTrackedObjects.Contains(trackedObject)) continue;
                    
                    compass.presetTrackedObjects.Add(trackedObject);
                    trackedObject.preCreated = true;
                }
                compass.presetTrackedObjects.RemoveAll(item => item == null || item.transform.parent != compass.transform);
            }

            LinkGlobalNorthstarSettings(compass);
        }
        
        private void RadarOptions(Radar compass)
        {
            StartRow();
            ShowRadar = ButtonOpenClose(ShowRadar);
            Header2($"Radar {symbolInfo}", "When true, a rotating radar line will be displayed, and objects will appear as radar pings.", 150);
            EndRow();
            
            Space();
            
            StartRow();
            Label($"Rotate With Player {symbolInfo}", "When true, the entire Radar object will rotate with the player.", 150);
            compass.rotateWithPlayer = Check(compass.rotateWithPlayer, 25);
            if (compass.rotateWithPlayer && compass.radarLineObject != null)
            {
                IconWarning();
                Label($"{textWarning}Radar Line will not rotate with the player.{textColorEnd}", 250, false, false, true);
            }
            EndRow();

            StartRow();
            Label($"Radar Line Object {symbolInfo}", "This is the line which rotates around the compass. When null, the entire " +
                                                     "map will be pinged at once based on the rotation speed, effectively providing " +
                                                     "a \"flash\" look at all objects at once.", 150);
            compass.radarLineObject = Object(compass.radarLineObject, typeof(GameObject), 200, true) as GameObject;
            if (compass.radarLineObject == null)
                Label("<color=#777777><i>Full map will ping based on rotation speed.</i></color>", false, false, true);
            EndRow();
            
            StartRow();
            Label($"Radar Rotation Speed {symbolInfo}", "Speed at which the line rotates. 360 = 1 rotation per second.", 150);
            compass.radarRotationSpeed = Float(compass.radarRotationSpeed, 80);
            EndRow();
            
            StartRow();
            Label($"Fade Radar Pings {symbolInfo}", "When true, pings will fade over time.", 150);
            compass.fadeRadarPings = Check(compass.fadeRadarPings, 25);
            EndRow();

            if (compass.fadeRadarPings)
            {
                StartRow();
                Label($"Fade Time {symbolInfo}", "How long it takes for the ping to fade to 0.", 150);
                compass.radarPingFadeTime = Float(compass.radarPingFadeTime, 80);
                EndRow();
                
                StartRow();
                Label($"Fade Curve {symbolInfo}", "Optional curve to use for fading the radar pings.", 150);
                compass.radarPingFadeCurve = Curve(compass.radarPingFadeCurve, -1, 200, 50);
                EndRow();
            }
            
            StartRow();
            Label($"Show Ping Movement {symbolInfo}", "When true, pings will continue to move. Otherwise they will remain stationary until painted again.", 150);
            compass.showPingMovement = Check(compass.showPingMovement, 25);
            EndRow();
        }
    }
}