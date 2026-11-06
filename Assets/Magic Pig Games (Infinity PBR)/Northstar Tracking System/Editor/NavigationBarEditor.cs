using UnityEditor;
using UnityEngine;
using static MagicPigGames.Shared.MagicPigEditor;

namespace MagicPigGames.Northstar
{
    [CustomEditor(typeof(NavigationBar))]
    public class NavigationBarEditor : Editor
    {
        NavigationBar Target => (NavigationBar)target;

        private GameObject _newObj;
        
        public override void OnInspectorGUI()
        {
            HeaderAndIntro();
            GreyLine();

            Undo.RecordObject(Target, "Navigation Bar Required Settings");
            RequiredSettings();
            //GreyLine();
            //OptionalSettings();
            GreyLine();
            EvergreenObjects();
            
            EditorUtility.SetDirty(Target);
        }
        
        private void EvergreenObjects()
        {
            Header2("Evergreen Objects");
            Label($"{textNormal}The Navigation Bar can be populated with {textHightlight}Tracked Target Overlay{textColorEnd} " +
                  $"objects which are only displayed on the Navigation Bar. Generally this is used for things like " +
                  $"cardinal directions (N, S, E, W), compass ticks, and a centered point showing the center of the bar.\n\n" +
                  $"Add prefabs from your project here which have a {textHightlight}TrackedTargetOverlay{textColorEnd} component on them.{textColorEnd}", false, true, true);

            Space();
            
            StartRow();
            Label("", 25);
            Label("", 25);
            Label("Evergreen Object", 150, true);
            if (Target.screenOverlayIcons.Count > 0)
            {
                Label("", 25);
                Label("Runtime Icon", 150, true);
            }
            EndRow();
            
            foreach(var obj in Target.evergreenObjects)
                ShowEvergreenObject(obj);
            Space();
            
            StartRow();
            Label($"Add new object:", 150);
            _newObj = Object(_newObj, typeof(GameObject), 200, false) as GameObject;
            if (_newObj != null)
            {
                var overlay = _newObj.GetComponent<TrackedTargetOverlay>();
                if (overlay != null)
                {
                    if (!Target.evergreenObjects.Contains(_newObj))
                        Target.evergreenObjects.Add(_newObj);
                }
                _newObj = null;
            }
            EndRow();
        }

        private void ShowEvergreenObject(GameObject evergreenObject)
        {
            StartRow();
            if (XButton())
            {
                Target.evergreenObjects.RemoveAll(x => x == evergreenObject);
                ExitGUI();
            }
            PingButton(evergreenObject);
            Label($"{evergreenObject.name}", 200);
            if (Target.screenOverlayIcons.ContainsKey(evergreenObject.GetComponent<TrackedTargetOverlay>()))
            {
                var screenOverlayIcon = Target.screenOverlayIcons[evergreenObject.GetComponent<TrackedTargetOverlay>()];
                PingButton(screenOverlayIcon);
                Label($"{screenOverlayIcon.name}", 200);
            }
            EndRow();
        }

        private void OptionalSettings()
        {
            Header2("Options");
            
            StartRow();
            Label($"Northstar Overlay Settings {symbolInfo}",
                "If populated, these overlay settings will be used on the Northstar Screen Overlay. Otherwise the " +
                "default settings will be used.", 150);
            Target.northstarOverlaySettings = Object(Target.northstarOverlaySettings, typeof(NorthstarOverlaySettings)) as NorthstarOverlaySettings;
            EndRow();
        }

        private void RequiredSettings()
        {
            Header2("Required");
            
            StartRow();
            Label($"Northstar Overlay Settings {symbolInfo}",
                "If populated, these overlay settings will be used on the Northstar Screen Overlay. Otherwise the " +
                "default settings will be used.", 180);
            Target.northstarOverlaySettings = Object(Target.northstarOverlaySettings, typeof(NorthstarOverlaySettings), 250) as NorthstarOverlaySettings;
            EndRow();
            
            /*
            StartRow();
            Label($"Y Position {symbolInfo}",
                "This is the default Y Position in local space that objects will be placed at.", 150);
            Target.yPosition = Float(Target.yPosition, 50);
            EndRow();
            */
            
            StartRow();
            Label($"Visible Angle {symbolInfo}",
                "This is the amount of angle which appears ON the bars entire width. If Fade Angle is greater " +
                "than 1/2 this value, then icons will appear outside of the bar width -- if Fade Angle is less than " +
                "1/2 this value, then they will disappear before they get to the edge of the bar.", 150);
            Target.visibleAngle = FloatSlider(Target.visibleAngle, 1, 360, 200);
            EndRow();
            
            StartRow();
            Label($"Fade Angle {symbolInfo}",
                "Icons will begin to fade from their full desired opacity to no opacity between these angles. Each item can optionally skip this fade.", 150);
            Target.fadeAngle = DelayedFloat(Target.fadeAngle, 50);
            if (Target.fadeAngle >= Target.fadeAngleEnd)
                Target.fadeAngle = Target.fadeAngleEnd - 1;
            Target.fadeAngleEnd = DelayedFloat(Target.fadeAngleEnd, 50);
            if (Target.fadeAngleEnd <= Target.fadeAngle)
                Target.fadeAngleEnd = Target.fadeAngle + 1;
            LabelGrey($"Edge of bar is at {(Target.visibleAngle / 2)} degrees.", 200);
            EndRow();
        }

        private void HeaderAndIntro()
        {
            LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/northstar-tracking-system/northstar-documentation/northstar-screen-overlay/navigation-bar");
            
            Header1($"Northstar Navigation Bar");
            Label($"{textNormal}Any <b>{textHightlight}TrackedTargetOverlay{textColorEnd}</b> object can be " +
                  $"handled by this UI panel, showing a customizable horizontal Navigation Bar on the screen. Set default options for Navigation Bar here. " +
                  $"Set the default display values in the {textHightlight}Northstar Overlay Settings{textColorEnd}, and individual {textHightlight}Tracked Target Overlay{textColorEnd} objects" +
                  $" can override values with those from other settings objects.{textColorEnd}", false, true, true);
        }
        
        // EDITOR PREF PROPERTIES
        public static bool ShowOverlayMainSettings
        {
            get => EditorPrefs.GetBool("Northstar Overlay Show Overlay Main Settings", true);
            set => EditorPrefs.SetBool("Northstar Overlay Show Overlay Main Settings", value);
        }
        
        public static bool ShowOverlaySizeSettings
        {
            get => EditorPrefs.GetBool("Northstar Overlay Show Overlay Size Settings", true);
            set => EditorPrefs.SetBool("Northstar Overlay Show Overlay Size Settings", value);
        }
        
        public static bool ShowOverlayOpacitySettings
        {
            get => EditorPrefs.GetBool("Northstar Overlay Show Overlay Opacity Settings", true);
            set => EditorPrefs.SetBool("Northstar Overlay Show Overlay Opacity Settings", value);
        }
        
        public static bool ShowEdgeMainSettings
        {
            get => EditorPrefs.GetBool("Northstar Overlay Show Edge Main Settings", true);
            set => EditorPrefs.SetBool("Northstar Overlay Show Edge Main Settings", value);
        }
        
        public static bool ShowEdgeSizeSettings
        {
            get => EditorPrefs.GetBool("Northstar Overlay Show Edge Size Settings", true);
            set => EditorPrefs.SetBool("Northstar Overlay Show Edge Size Settings", value);
        }
        
        public static bool ShowEdgeOpacitySettings
        {
            get => EditorPrefs.GetBool("Northstar Overlay Show Edge Opacity Settings", true);
            set => EditorPrefs.SetBool("Northstar Overlay Show Edge Opacity Settings", value);
        }
        
        public static bool ShowRuntimeOverlayIcons
        {
            get => EditorPrefs.GetBool("Northstar Overlay Show Runtime Overlay Icons", true);
            set => EditorPrefs.SetBool("Northstar Overlay Show Runtime Overlay Icons", value);
        }
        
        public static bool ShowRuntimeOverlayTypes
        {
            get => EditorPrefs.GetBool("Northstar Overlay Show Runtime Overlay Types", true);
            set => EditorPrefs.SetBool("Northstar Overlay Show Runtime Overlay Types", value);
        }
        
        public static bool ShowRuntimeEdgeIcons
        {
            get => EditorPrefs.GetBool("Northstar Overlay Show Runtime Edge Icons", true);
            set => EditorPrefs.SetBool("Northstar Overlay Show Runtime Edge Icons", value);
        }
        
        public static bool ShowRuntimeEdgeTypes
        {
            get => EditorPrefs.GetBool("Northstar Overlay Show Runtime Edge Types", true);
            set => EditorPrefs.SetBool("Northstar Overlay Show Runtime Edge Types", value);
        }
    }
}
