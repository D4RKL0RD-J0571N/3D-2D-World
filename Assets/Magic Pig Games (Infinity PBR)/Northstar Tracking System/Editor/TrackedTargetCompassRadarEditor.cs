using UnityEditor;
using UnityEngine;
using static MagicPigGames.Shared.MagicPigEditor;
using static MagicPigGames.Northstar.GlobalNorthstarSettings;

namespace MagicPigGames.Northstar
{
    [CustomEditor(typeof(TrackedTargetCompassRadar))]
    public class TrackedTargetCompassRadarEditor : Editor
    {
        TrackedTargetCompassRadar TargetCompassRadar => (TrackedTargetCompassRadar)target;
        
        public override void OnInspectorGUI()
        {
            LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/northstar-tracking-system/northstar-documentation/tracked-target-compass-radar");
            
            Header2($"Tracked Target Compass/Radar");
            Label($"{textNormal}This is an object which will be tracked by the <b>{textHightlight}Northstar Compass or Radar{textColorEnd}</b>, based " +
                  $"on the options you set.{textColorEnd}", false, true, true);
            Space();

            TrackingType();
            Options();
            Runtime();
            
            EditorUtility.SetDirty(TargetCompassRadar);
        }

        private void TrackingType()
        {
            StartRow();
            Label($"Tracking Type {symbolInfo}", "This determines how the tracked object is handled. Check the " +
                                                 "docs for more details of each option.", 150);
            TargetCompassRadar.trackingType = (TrackingType) EnumPopup(TargetCompassRadar.trackingType, 200);
            EndRow();

            if (TargetCompassRadar.trackingType == GlobalNorthstarSettings.TrackingType.PointAtTarget)
            {
                TargetCompassRadar.adjustBasedOnPlayerIcon = LeftCheck($"Adjust based on Player Icon {symbolInfo}", "When true, the system will search for an icon that " +
                    "has the PlayerForward tracking type, and will adjust the" +
                    "angle based on that.\n\n Example: When false, the player " +
                    "forward should be \"up\" on the compass/radar. When true, " +
                    "the expectation is that the player icon in the center will " +
                    "be rotating, so the direction to the target will be " +
                    "relative to the player icon, rather than the player forward " +
                    "direction only.", TargetCompassRadar.adjustBasedOnPlayerIcon, 250);
            }
            
            if (TargetCompassRadar.trackingType != GlobalNorthstarSettings.TrackingType.RelativePoint)
            {
                BlackLine();
                Label($"{textWarning}<b>Relative Point Tracking Not Selected</b>{textColorEnd}", false, false, true);
                Label($"{textNormal}You currently have {textWarning}{TargetCompassRadar.trackingType.ToString()}{textColorEnd} selected. Please " +
                      $"ensure you have the {textHightlight}<i>Tracked Object Prefab</i>{textColorEnd} that you'd like to use. Generally the default prefab will " +
                      $"be a UI icon that is a single point, which can travel around the Compass/Radar area.\n\n" +
                      $"When {textWarning}{TargetCompassRadar.trackingType.ToString()}{textColorEnd} is selected, it implies that a " +
                      $"different UI element should be used, specific to the tracking purpose. " +
                      $"If you'd like to track this as a UI icon that travels around the Compass/Radar area, please select " +
                      $"the {textWarning}Relative Point{textColorEnd} option.{textColorEnd}", false, true, true);
                BlackLine();
            }
            else
                Space();
        }
        
        private void Options()
        {
            Header3("Options");
            StartRow();
            Label($"Tracked Object Icon {symbolInfo}", "This is the icon prefab that will be used to display the tracked object. " +
                                                         "If you leave this blank, the default prefab will be used.", 150);
            TargetCompassRadar.trackedObjectPrefab = Object(TargetCompassRadar.trackedObjectPrefab, typeof(GameObject), 200, false) as GameObject;
            if (TargetCompassRadar.trackedObjectPrefab == null)
                Label($"{textMuted}Default prefab will be used.{textColorEnd}", false, false, true);
            EndRow();
        }
        
        private void Runtime()
        {
            if (TargetCompassRadar.compassIcon == null) return;
            GreyLine();
            Label($"{textWarning}<i><b>Runtime Only</b></i>{textColorEnd}", false, false, true);
            StartRow();
            Label($"Tracked Icon {symbolInfo}", "This is the runtime icon being tracked on the Compass/Radar.", 150);
            PingButton(TargetCompassRadar.compassIcon);
            Label($"{TargetCompassRadar.compassIcon.name}", 150);
            EndRow();
        }
    }
}