using TMPro;
using UnityEditor;
using static MagicPigGames.Shared.MagicPigEditor;

namespace MagicPigGames.Northstar
{
    [CustomEditor(typeof(DistanceText))]
    public class DistanceTextEditor : Editor
    {
        DistanceText Target => (DistanceText)target;
        
        public override void OnInspectorGUI()
        {
            LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/northstar-tracking-system/northstar-documentation/distance-text");
            
            Header1($"Distance Text");
            AdditionalInformation();
            Label($"{textNormal}Manages a TextMeshPro Text component attached to the icon.{textColorEnd}", false, true, true);

            Space();
            Header3("Required");
            StartRow();
            Label("Text Component", 150);
            Target.text = Object(Target.text, typeof(TMP_Text), 200, false) as TMP_Text;
            EndRow();
            
            GreyLine();
            
            DrawOptions();
            
            GreyLine();
            DrawDefaultInspectorSelection = LeftCheck("Draw Default Inspector", DrawDefaultInspectorSelection);
            if (DrawDefaultInspectorSelection)
                DrawDefaultInspector();

            AdditionalActions();
            
            EditorUtility.SetDirty(Target);
        }

        protected virtual void AdditionalInformation()
        {
            
        }

        protected virtual void AdditionalActions()
        {
            
        }

        private void DrawOptions()
        {
            StartRow();
            DrawOptionsSelection = OnOffButton(DrawOptionsSelection);
            Header2("Options");
            EndRow();

            if (!DrawOptionsSelection)
                return;
            
            StartRow();
            Target.overrideColor = LeftCheck($"Override Color {symbolInfo}", "If false, the color will match the Icon color.", Target.overrideColor, 150);
            if (Target.overrideColor)
            {
                Label("Color", 50);
                Target.color = ColorField(Target.color, 150);
            }
            EndRow();

            StartRow();
            Target.matchIconOpacity = LeftCheck($"Match Icon Opacity {symbolInfo}", "When true, the opacity will match the icon image opacity.", Target.matchIconOpacity, 150);
            EndRow();
            StartRow();
            Target.matchIconSize = LeftCheck($"Match Icon Size {symbolInfo}", "When true, the size of the text will increase and decrease with the icon size.", Target.matchIconSize, 150);
            EndRow();

            BlackLine();
            Header3($"Number Values {symbolInfo}", "Customize the look of the distance here. For advanced customization, create a class which inherits from " +
                                                   "DistanceText.cs to override methods.", 250);
            StartRow();
            Label($"Max Digits {symbolInfo}", "The total number of digits, including decimals.", 150);
            Target.maxDigits = Int(Target.maxDigits, 50);
            Label($"{textMuted}Default: 3{textColorEnd}", 150, false , false, true);
            EndRow();
            
            StartRow();
            Label($"Max Decimals {symbolInfo}", "The maximum number of decimals allowed.", 150);
            Target.maxDecimals = Int(Target.maxDecimals, 50);
            Label($"{textMuted}Default: 2{textColorEnd}", 150, false , false, true);
            EndRow();
            
            StartRow();
            Label($"\"Meters\" string {symbolInfo}", "The string representing \"meters\". Defaults to \"m\"", 150);
            Target.meters = TextField(Target.meters, 50);
            Label($"{textMuted}Default: \"m\"{textColorEnd}", 150, false , false, true);
            EndRow();

            Space();
            Label($"{textNormal}<b><i>Examples:</i></b>{textColorEnd}", false, false, true);
            DrawExampleLine(0.3882f);
            DrawExampleLine(4.99032f);
            DrawExampleLine(13.8299f);
            DrawExampleLine(999.1382f);
            DrawExampleLine(1000f);
            DrawExampleLine(1234.56789f);
            DrawExampleLine(9876653f);
            
            BlackLine();
            Header3($"Screen / Edge Overlay {symbolInfo}", "This adjusts the Y Position of the text in relation to the icon.", 250);
            StartRow();
            Target.screenAnchoredPositionYOffset = LeftCheck("Offset Y Position", Target.screenAnchoredPositionYOffset, 150);
            if (Target.screenAnchoredPositionYOffset)
            {
                Label("Offset", 50);
                Target.screenAnchoredPositionYOffsetPercent = SliderFloat(Target.screenAnchoredPositionYOffsetPercent, -3, 3, 150);
            }
            EndRow();
            
            Space();
            Header3($"Compass Bar Overlay {symbolInfo}", "This adjusts the Y Position of the text in relation to the icon.", 250);
            StartRow();
            Target.compassBarAnchoredPositionYOffset = LeftCheck("Offset Y Position", Target.compassBarAnchoredPositionYOffset, 150);
            if (Target.compassBarAnchoredPositionYOffset)
            {
                Label("Offset", 50);
                Target.compassBarAnchoredPositionYOffsetPercent = SliderFloat(Target.compassBarAnchoredPositionYOffsetPercent, -3, 3, 150);
            }
            EndRow();
        }

        private void DrawExampleLine(float value)
        {
            var str = $"{NorthstarUtilities.ShortScaleString(value, Target.maxDecimals, Target.maxDigits)}{Target.meters}";
            
            StartRow();
            Label($"{textNormal}<i>{value}</i>{textColorEnd}", 100, false, false, true);
            Label($"{textMuted}<i>--></i>{textColorEnd}", 50, false, false, true);
            Label($"{textNormal}<i>{str}</i>{textColorEnd}", 100, false, false, true);
            EndRow();
        }

        private bool DrawDefaultInspectorSelection
        {
            get => EditorPrefs.GetBool("Screen Overlay Draw Default Inspector", false);
            set => EditorPrefs.SetBool("Screen Overlay Draw Default Inspector", value);
        }
        
        private bool DrawOptionsSelection
        {
            get => EditorPrefs.GetBool("Screen Overlay Draw Distance Check", false);
            set => EditorPrefs.SetBool("Screen Overlay Draw Distance Check", value);
        }
    }
}