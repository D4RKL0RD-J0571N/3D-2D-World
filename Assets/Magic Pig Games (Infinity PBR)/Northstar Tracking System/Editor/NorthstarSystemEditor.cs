using System.IO;
using System.Linq;
using MagicPigGames.Shared;
using UnityEditor;
using UnityEngine;
using static MagicPigGames.Shared.MagicPigEditor;
using static MagicPigGames.Northstar.GlobalNorthstarSettings;

namespace MagicPigGames.Northstar
{
    public static class NorthstarSystemEditor
    {
        public static bool ShowRequired
        {
            get => EditorPrefs.GetBool("Compass Show Required", true);
            set => EditorPrefs.SetBool("Compass Show Required", value);
        }
        
        public static bool ShowRadar
        {
            get => EditorPrefs.GetBool("Compass Show Radar", true);
            set => EditorPrefs.SetBool("Compass Show Radar", value);
        }
        
        public static bool ShowPresetObjects
        {
            get => EditorPrefs.GetBool("Compass Show Preset Objects", true);
            set => EditorPrefs.SetBool("Compass Show Preset Objects", value);
        }

        public static void RequiredFields(NorthstarSystem compass)
        {
            StartRow();
            ShowRequired = ButtonOpenClose(ShowRequired);
            Header2("Required");
            EndRow();
            if (!ShowRequired) return;
            PlayerSelection(compass);
            DefaultTargetPrefab(compass);
            GlobalNorthstarSettings(compass);

            RangeSettings(compass);
        }

        public static void RangeSettings(NorthstarSystem compass)
        {
            Space();
            Header3("Tracked Target Range Settings");
            Label($"{textNormal}<i>These settings only affect {textHightlight}Tracked Icons{textColorEnd} set to {textHightlight}Relative Point{textColorEnd}.</i>{textColorEnd}", false, true, true);
            
            StartRow();
            Label($"Detection Range {symbolInfo}", "The maximum range for an object to the detected. NOTE: This is based on direct distance to the " +
                                                   "object, while the \"Fade\" values below are 2D linear distances.", 172);
            compass.detectionRange = Float(compass.detectionRange, 50);
            var fadeRangeY = compass.fadeOnDistance ? compass.maxDistanceFadeRange.y : compass.objectDistanceRange.y;
            if (compass.detectionRange < fadeRangeY)
            {
                Label($"{textError}<b>Suggested value >= {Mathf.Max(compass.objectDistanceRange.y, fadeRangeY)}</b>{textColorEnd}", false,
                    false, true);
            }
            else if (compass.fadeOnDistance && compass.detectionRange > compass.maxDistanceFadeRange.y)
            {
                Label($"{textWarning}<i>Icon will not be visible past range {compass.maxDistanceFadeRange.y}</i>{textColorEnd}", false,
                    false, true);
            }
            EndRow();
            
            StartRow();
            Label($"Object Range {symbolInfo}", "The min and max distance for objects to be, mapped to the Range from Center. This means objects " +
                                                "<= value X will be at the closest allowed point to the center of the UI element, while those >= the " +
                                                "value Y will be at the furthest point.", 172);
            compass.objectDistanceRange = Vector2Field(compass.objectDistanceRange, 120);
            if (compass.objectDistanceRange.x > compass.objectDistanceRange.y)
            {
                Label($"{textError}<b>X = Min, Y = Max</b>{textColorEnd}", false,
                    false, true);
            }
            EndRow();
            
            StartRow();
            Label($"UI Range from Center {symbolInfo}","The min and max range from the center for the target sprite, based on the " +
                                                   "min/max of the Object Distance Range.", 172);
            compass.rangeFromCenter = Vector2Field(compass.rangeFromCenter, 120);
            if (compass.rangeFromCenter.x > compass.rangeFromCenter.y)
            {
                Label($"{textError}<b>X = Min, Y = Max</b>{textColorEnd}", false,
                    false, true);
            }
            EndRow();
            
            StartRow();
            compass.fadeOnDistance = EnabledButton(compass.fadeOnDistance);
            Label($"Fade on Distance {symbolInfo}", "When true, the target can fade out when the distance is too far.", 150, true);
            EndRow();
            
            if (compass.fadeOnDistance)
            {
                StartRow();
                Gap();
                Label($"Max Distance Fade {symbolInfo}",
                    "The distance at which the object will start and finish fading out, for objects far away.", 150);
                compass.maxDistanceFadeRange = Vector2Field(compass.maxDistanceFadeRange, 120);
                if (compass.maxDistanceFadeRange.x > compass.maxDistanceFadeRange.y)
                {
                    Label($"{textError}<b>X = Min, Y = Max</b>{textColorEnd}", false,
                        false, true);
                }

                EndRow();
            }

            StartRow();
            compass.fadeOnYDelta = EnabledButton(compass.fadeOnYDelta);
            Label($"Fade on Y Delta {symbolInfo}", "When true, the target can fade out when the Y delta is too high.", 150, true);
            EndRow();

            if (!compass.fadeOnYDelta) return;
            
            StartRow();
            Gap();
            Label($"Y Delta Below {symbolInfo}",
                "The min and max Y delta BELOW the player for fading out. Keep the values the same to \"turn off\" instead of fade. Must be below 0 and X <= Y.",
                150);
            compass.maxYDeltaFadeRangeBelow = Vector2Field(compass.maxYDeltaFadeRangeBelow, 120);
            if (compass.maxYDeltaFadeRangeBelow.x > compass.maxYDeltaFadeRangeBelow.y
                || compass.maxYDeltaFadeRangeBelow.x > 0 || compass.maxYDeltaFadeRangeBelow.y > 0)
            {
                Label($"{textError}<b>X <= Y, Y <= 0</b>{textColorEnd}", false,
                    false, true);
            }

            EndRow();

            StartRow();
            Gap();
            Label($"Y Delta Above {symbolInfo}",
                "The min and max Y delta ABOVE the player for fading out. Keep the values the same to \"turn off\" instead of fade. Must be above 0 and X <= Y.",
                150);
            compass.maxYDeltaFadeRangeAbove = Vector2Field(compass.maxYDeltaFadeRangeAbove, 120);
            if (compass.maxYDeltaFadeRangeAbove.x > compass.maxYDeltaFadeRangeAbove.y
                || compass.maxYDeltaFadeRangeAbove.x < 0 || compass.maxYDeltaFadeRangeAbove.y < 0)
            {
                Label($"{textError}<b>X <= Y, X >= 0</b>{textColorEnd}", false,
                    false, true);
            }

            EndRow();
        }

        
        public static void GlobalNorthstarSettings(NorthstarSystem compass)
        {
            StartRow();
            Label($"Northstar Settings {symbolInfo}", "This is created automatically. Holds global settings for all Northstar systems in your project.", 180);
            PingButton(compass.NorthstarSettings);
            Label($"{compass.NorthstarSettings.name}");
            EndRow();
        }

        public static void PlayerSelection(NorthstarSystem compass)
        {
            StartRow();
            Label($"Player {symbolInfo}", "This is the center point of the compass, generally the player object.", 180);
            compass.player = (Transform)Object(compass.player, typeof(Transform), 200, true);
            EndRow();
        }

        public static void CompassObjects(NorthstarSystem compass)
        {
            StartRow();
            ShowPresetObjects = ButtonOpenClose(ShowPresetObjects);
            Header2("Preset Objects");
            EndRow();
            if (!ShowPresetObjects) return;
            PresetObjects(compass);
            RuntimeObjects(compass);
        }

        public static void RuntimeObjects(NorthstarSystem compass)
        {
            if (compass.CompassIcons.Count <= 0) return;
            Header2("Runtime Tracked Objects");
            Label($"{textNormal}<i>There are currently " +
                  $"<b>{compass.CompassIcons.Count}</b> objects being tracked, including the Preset Objects above. " +
                  $"Some may not be visible due to alpha settings.</i>{textColorEnd}", false, true, true);
            BlackLine();
            foreach(var (key, trackedIcon) in compass.CompassIcons)
            {
                if (compass.presetTrackedObjects.Contains(trackedIcon)) continue;
                RuntimeTrackedObject(trackedIcon);
            }
        }

        public static void PresetObjects(NorthstarSystem compass)
        {
            Label($"{textNormal}<i>These are children of " +
                  $"<b>{compass.gameObject.name}</b> which have a {textHightlight}CompassIcon{textColorEnd} component on " +
                  $"them.</i>{textColorEnd}", false, true, true);
            BlackLine();
            for (var index = 0; index < compass.presetTrackedObjects.Count; index++)
            {
                var compassObject = compass.presetTrackedObjects[index];
                Undo.RecordObject(compassObject, "Compass Object Change");
                TrackedTarget(compass, compassObject);
                EditorUtility.SetDirty(compassObject);
                if (index < compass.presetTrackedObjects.Count - 1) 
                    BlackLine();
                else 
                    GreyLine();
            }
        }

        public static void RuntimeTrackedObject(CompassIcon compassIcon)
        {
            StartRow();
            PingButton(compassIcon.gameObject);
            Label($"{compassIcon.name}", 150);
            PingButton(compassIcon.target.gameObject);
            Label($"{compassIcon.target.name}", 150);
            Label($"{compassIcon.trackingType.ToString()}", 120);
            if (compassIcon.trackingType == TrackingType.RelativePoint)
                Label($"Distance: {compassIcon.DistanceToTarget().RoundToDecimal(2)}", 100);
            EndRow();
        }
        
        public static void DefaultTargetPrefab(NorthstarSystem compass)
        {
            StartRow();
            Label($"Default Tracked Icon Prefab {symbolInfo}", "When calling the AddTarget() method, this prefab will be used " +
                                                         "unless another one is provided.", 180);
            compass.defaultTrackedIcon = (GameObject)Object(compass.defaultTrackedIcon, typeof(GameObject), 200, false);
            if (compass.defaultTrackedIcon != null && compass.defaultTrackedIcon.GetComponent<CompassIcon>() == null)
            {
                compass.defaultTrackedIcon = null;
                Debug.LogError($"There was no TrackedIcon component found on the default target prefab.");
            }
            EndRow();
            if (compass.defaultTrackedIcon == null)
            {
                Label($"{textError}<b>Default Tracked Icon Prefab is required!</b>{textColorEnd}", 250, false, false, true);
            }
        }

        public static void TrackedTarget(NorthstarSystem compass, CompassIcon compassIcon)
        {
            StartRow();
            PingButton(compassIcon.gameObject);
            compassIcon.gameObject.SetActive(OnOffButton(compassIcon.gameObject.activeSelf));
            Label($"{compassIcon.name}", 100);
            compassIcon.trackingType = (TrackingType)EnumPopup(compassIcon.trackingType, 125);
            if (!compassIcon.enabled)
            {
                Gap();
                Label($"{textError}<b>Component disabled</b>{textColorEnd}", 150, false, false, true);
                if (Button("Enable", 80))
                    compassIcon.enabled = true;
                EndRow();
                return;
            }
            PointNorth(compass, compassIcon);
            DoNothing(compass, compassIcon);
            PointAtTarget(compass, compassIcon);
            DistanceToTarget(compass, compassIcon);
            EndRow();
        }

        public static void DistanceToTarget(NorthstarSystem compass, CompassIcon compassIcon)
        {
            if (!compassIcon.trackingType.Equals(TrackingType.RelativePoint)) return;
            StartRow();
            TargetInfo(compass, compassIcon);
            EndRow();
        }

        public static void PointAtTarget(NorthstarSystem compass, CompassIcon compassIcon)
        {
            if (!compassIcon.trackingType.Equals(TrackingType.PointAtTarget)) return;
            TargetInfo(compass, compassIcon);
        }

        public static void TargetInfo(NorthstarSystem compass, CompassIcon compassIcon)
        {
            Gap();
            Label($"Target {symbolInfo}", "Optional. Can be set at runtime.", 70);
            compassIcon.target = (Transform)Object(compassIcon.target, typeof(Transform), 200, true);
            Label($"{textMuted}<i>Optional</i>{textColorEnd}", 70,false, false, true);
        }

        public static void DoNothing(NorthstarSystem compass, CompassIcon compassIcon)
        {
            if (!compassIcon.trackingType.Equals(TrackingType.DoNotMove)) return;
            Gap();
            Label($"{textMuted}<i>This object will not move.</i>{textColorEnd}", 200, false, true, true);
        }

        public static void PointNorth(NorthstarSystem compass, CompassIcon compassIcon)
        {
            if (!compassIcon.trackingType.Equals(TrackingType.PointNorth)) return;
            Gap();
            compassIcon.invertRotation = LeftCheck($"Invert Rotation {symbolInfo}", "When true, the rotation will go the opposite " +
                "direction. Often used for background rotation, rather than \"needle\" rotation, where the needle continues to " +
                "point north while the background rotates.", compassIcon.invertRotation);
        }
        
        public static void LinkGlobalNorthstarSettings(NorthstarSystem northstarObject)
        {
            // Find an existing GlobalNorthstarSettings asset in the project and link it
            if (northstarObject.northstarSettings != null) return;
            northstarObject.northstarSettings = AssetDatabase.FindAssets("t:GlobalNorthstarSettings")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GlobalNorthstarSettings>)
                .FirstOrDefault();
        }

        // Separate method called only from an explicit user button press — never from OnEnable.
        public static void CreateGlobalNorthstarSettings(NorthstarSystem northstarObject)
        {
            if (northstarObject.northstarSettings != null) return;

            var folderPath = "Assets/Northstar";
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            northstarObject.northstarSettings = ScriptableObject.CreateInstance<GlobalNorthstarSettings>();
            var assetPath = folderPath + "/GlobalNorthstarSettings.asset";
            AssetDatabase.CreateAsset(northstarObject.northstarSettings, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}