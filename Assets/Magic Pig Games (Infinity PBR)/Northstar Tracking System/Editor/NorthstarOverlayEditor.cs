using System.Linq;
using UnityEditor;
using UnityEngine;
using static MagicPigGames.Shared.MagicPigEditor;
using static MagicPigGames.Northstar.NorthstarEditorUtilities;

namespace MagicPigGames.Northstar
{
    [CustomEditor(typeof(NorthstarOverlay))]
    public class NorthstarOverlayEditor : Editor
    {
        NorthstarOverlay Target => (NorthstarOverlay)target;
        
        private void OnEnable()
        {
            if (Target.overlayIconPrefab == null)
                Target.overlayIconPrefab = FindDefaultScreenOverlayPrefab();
            if (Target.targetCamera == null)
                Target.targetCamera = FindFirstEnabledCamera();
        }
        
        public override void OnInspectorGUI()
        {
            HeaderAndIntro();
            RuntimeData();
            GreyLine();

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(Target, "Northstar Overlay Settings");
            RequiredSettings();
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(Target);
        }
        

        private void RequiredSettings()
        {
            Header2("Required");
            StartRow();
            Label($"Camera", 150);
            Target.targetCamera = Object(Target.targetCamera, typeof(Camera), 200, true) as Camera;
            if (Target.targetCamera == null)
                Label($"<i>{textError}Can not be null!{textColorEnd}</i>", false, false, true);
            EndRow();
            
            StartRow();
            Label($"Overlay Icon {symbolInfo}", "This is the on-screen icon prefabs that will be used. Generally they " +
                                                         "should remain set to the default values, unless you are " +
                                                         "specifically overriding them (and you know what you are doing!).\n\n" +
                                                         "Please do not remove or rename \"Northstar Overlay Icon\"!", 150);
            Target.overlayIconPrefab = Object(Target.overlayIconPrefab, typeof(GameObject), 200, false) as GameObject;
            if (Target.overlayIconPrefab == null)
                Label($"<i>{textError}Can not be null!{textColorEnd}</i>", false, false, true);
            EndRow();
            
            StartRow();
            Label($"Overlay Settings {symbolInfo}", "This contains the default options for this overlay. Individual " +
                                                              "Tracked Target objects can override specific values.", 150);
            Target.northstarOverlaySettings = Object(Target.northstarOverlaySettings, typeof(NorthstarOverlaySettings), 200, false) as NorthstarOverlaySettings;
            if (Target.northstarOverlaySettings == null)
                Label($"<i>{textError}Can not be null!{textColorEnd}</i>", false, false, true);
            EndRow();
            
            StartRow();
            Target.enableScreenOverlay = LeftCheck($"Enable Screen Overlay {symbolInfo}", "If false, screen overlay icons will only appear if " +
                "individual Tracked Targets override this value to " +
                "enable the icons for themselves only.", Target.enableScreenOverlay, 175);
            if (!Target.enableScreenOverlay)
                Label($"{textError}<i>Disabled</i>{textColorEnd}", 100, false, false, true);
            EndRow();
            StartRow();
            Target.enableEdgeOverlay = LeftCheck($"Enable Edge Overlay {symbolInfo}", "If false, screen overlay icons will only appear if " +
                "individual Tracked Targets override this value to " +
                "enable the icons for themselves only.", Target.enableEdgeOverlay, 175);
            if (!Target.enableEdgeOverlay)
                Label($"{textError}<i>Disabled</i>{textColorEnd}", 100, false, false, true);
            EndRow();
            StartRow();
            Target.enableNavigationBar = LeftCheck($"Enable Navigation Bar {symbolInfo}", "When true, and if the navigation bar is " +
                "a child of this object, icons will appear on the horizontal navigation bar.", Target.enableNavigationBar, 175);
            if (!Target.enableNavigationBar)
                Label($"{textError}<i>Disabled</i>{textColorEnd}", 100, false, false, true);
            else
            {
                Label($"{textNormal}Navigation Bar{textColorEnd}", 80, false, false, true);
                Target.navigationBar = Object(Target.navigationBar, typeof(NavigationBar), 95, true) as NavigationBar;
                if (Target.navigationBar == null)
                {
                    Label($"{textError}Required{textColorEnd}", 100, false, false, true);
                }
            }
            EndRow();
            
            if (!Target.enableScreenOverlay && !Target.enableEdgeOverlay)
            {
                Label(
                    $"{textError}<b>Both Screen and Edge Overlay are disabled. This will result in no overlay being " +
                    $"displayed. Individual Tracked Targets would need to override this value to enable screen or " +
                    $"edge overlays for themselves only.</b>{textColorEnd}", false, true, true);
            }
        }

        private void HeaderAndIntro()
        {
            LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/northstar-tracking-system/northstar-documentation/northstar-screen-overlay");
            
            Header1($"Northstar Overlay");
            Label($"{textNormal}Any object with a <b>{textHightlight}TrackedTargetOverlay{textColorEnd}</b> component will be " +
                  $"handled by this UI panel. Set default options for display here. Each individual object can optionally " +
                  $"override the options as well.{textColorEnd}", false, true, true);
        }

        private void RuntimeData()
        {
            if (Target.screenOverlayIcons.Count == 0)
                return;
            
            GreyLine();
            Header2("Runtime Data");
            Label($"<i>{textWarning}Only shown at runtime{textColorEnd}</i>", false, false, true);
            
            StartRow();
            ShowRuntimeOverlayIcons = OnOffButton(ShowRuntimeOverlayIcons);
            Label($"Overlay Icons [{Target.screenOverlayIcons.Count}]");
            EndRow();
            RuntimeOverlayIcons();
            Space();
            
            StartRow();
            ShowRuntimeOverlayTypes = OnOffButton(ShowRuntimeOverlayTypes);
            Label($"Overlay Types [{Target.screenOverlayIconsByType.Count}]");
            EndRow();
            RuntimeOverlayTypes();
            Space();
            
        }

        private void RuntimeOverlayIcons()
        {
            if (!ShowRuntimeOverlayIcons)
                return;

            StartRow();
            Label("", 25);
            Label("Target Obj.", 150, true);
            Label("", 25);
            Label("Target Icon", 150, true);
            Label("Type", 100, true);
            Label($"Distance % {symbolInfo}", "1 = Further than max range\n0 = Closer to or at min range", 100, true);
            EndRow();
            foreach (var icon in Target.screenOverlayIcons)
            {
                StartRow();
                PingButton(icon.Key.gameObject);
                Label($"{textNormal}{icon.Key.name}{textColorEnd}", 150, false, false, true);
                PingButton(icon.Value.gameObject);
                Label($"{textNormal}{icon.Value.name}{textColorEnd}", 150, false, false, true);
                Label($"{textNormal}{icon.Key.targetType}{textColorEnd}", 100, false, false, true);
                Label($"{(icon.Value.ScreenDistancePercent >= 1 ? textNormal : textHightlight)}{icon.Value.ScreenDistancePercent}{textColorEnd}", 100, false, false, true);
                EndRow();
            }
        }
        
        private void RuntimeOverlayTypes()
        {
            if (!ShowRuntimeOverlayTypes)
                return;
            
            StartRow();
            Label($"Type String", 150, true);
            Label($"Icon Count", 150, true);
            Label($"# in Range {symbolInfo}", "The number of object within trackable distance.", 100, true);
            EndRow();
            foreach (var icon in Target.screenOverlayIconsByType)
            {
                StartRow();
                Label($"{textNormal}{icon.Key}{textColorEnd}", 150, false, false, true);
                Label($"{textHightlight}{icon.Value.Count} Icons{textColorEnd}", 150, false, false, true);
                var numberInRange = icon.Value.Count(x => x.Value.ScreenDistancePercent < 1);
                Label($"{(numberInRange == 0 ? textNormal : textHightlight)}{numberInRange}{textColorEnd}", 100, false, false, true);
                EndRow();
            }
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
        
    }

}
