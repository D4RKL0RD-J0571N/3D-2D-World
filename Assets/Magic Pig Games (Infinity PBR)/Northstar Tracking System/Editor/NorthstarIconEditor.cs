using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static MagicPigGames.Shared.MagicPigEditor;
using Image = UnityEngine.UI.Image;

namespace MagicPigGames.Northstar
{
    [CustomEditor(typeof(OverlayIcon))]
    public class NorthstarIconEditor : Editor
    {
        OverlayIcon Target => (OverlayIcon)target;
        
        public override void OnInspectorGUI()
        {
            LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/northstar-tracking-system/northstar-documentation/overlay-icon");
            
            Header1($"Northstar Icon");
            AdditionalInformation();
            Label($"{textNormal}Create a set of distance/frequency pairs to better manage the distance checks. Set a quicker frequency for " +
                  $"a lower distance, allowing for more checks when the player is close. Set a reduced frequency when the player is further away, " +
                  $"when there is less of a need to check the distance often. Other values will be set at runtime.{textColorEnd}", false, true, true);

            Space();
            Header3("Required");
            StartRow();
            Label("Arrow Image", 150);
            Target.arrowImage = Object(Target.arrowImage, typeof(Image), 200, false) as Image;
            EndRow();
            
            GreyLine();
            
            DrawDistanceCheck();
            
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

        private void DrawDistanceCheck()
        {
            StartRow();
            DrawDistanceCheckSelection = OnOffButton(DrawDistanceCheckSelection);
            Header2("Distance Checks");
            EndRow();

            if (!DrawDistanceCheckSelection)
                return;
            
            Label($"{textNormal}If the last distance was <= the \"Distance\" value, the frequency of new distance checks will be " +
                  $"set to the \"Frequency\" value. Generally the Frequency value will increase with greater distances. If your " +
                  $"player can move very very quickly, you may want to decrease the Frequency\n\nUse the {textHightlight}ForceDistanceUpdate(){textColorEnd} " +
                  $"method on {textHightlight}NorthstarIcon{textColorEnd} (or derived classes) to instantly check " +
                  $"the distance value, skipping the frequency delay, when the player moves very quickly to a new location," +
                  $" such as warping across the map.\n\n" +
                  $"<i>Icons which change size or opacity by distance may display a \"pop\" effect as the distance updates, and they " +
                  $"change a visible amount. Avoid changing size or opacity by distance to avoid this, or set a greater frequency at " +
                  $"distances where you know the fade occurs, such as near the edge of the total distance an icon can be active.</i>{textColorEnd}", false, true, true);
            
            Space();
            StartRow();
            if (Target.distanceChecks.Count > 1)
                Label("", 25);
            Label("<= Distance", 100);
            Label("Frequency", 100);
            EndRow();
            
            foreach(var check in Target.distanceChecks)
                ShowDistanceCheck(check);
            
            if (Button("Add new distance check", 150))
                AddNewDistanceCheck();
            
            ValidateDistanceChecks();
        }

        private void AddNewDistanceCheck()
        {
            if (Target.distanceChecks.Count == 0)
            {
                Target.distanceChecks.Add(new DistanceCheck(50, -1f));
                return;
            }
            
            var lastCheck = Target.distanceChecks[^1];
            Target.distanceChecks.Add(new DistanceCheck(lastCheck.distance + 50, lastCheck.frequency + 1f));
        }

        private void ShowDistanceCheck(DistanceCheck check)
        {
            StartRow();
            if (Target.distanceChecks.Count > 1)
            {
                if (XButton())
                {
                    Target.distanceChecks.Remove(check);
                    ExitGUI();
                }
            }
            var cacheDistance = check.distance;
            check.distance = DelayedFloat(check.distance, 100);
            if (Target.distanceChecks.Count(x => Math.Abs(x.distance - check.distance) < 0.01) > 1)
            {
                Debug.Log($"There is already a distance check for {check.distance}. Please choose another value.");
                check.distance = cacheDistance;
            }
            check.frequency = DelayedFloat(check.frequency, 100);
            if (check.frequency < 0)
                Label($"{textHightlight}<i>Checks every frame</i>{textColorEnd}", 150, false, false, true);
            EndRow();
        }
        
        private void ValidateDistanceChecks()
        {
            if (Target.distanceChecks.Count == 0)
                AddNewDistanceCheck();
            
            // Order Target.distanceChecks by distance
            Target.distanceChecks = Target.distanceChecks.OrderBy(x => x.distance).ToList();
            
            // Keep top one at every frame
            Target.distanceChecks[0].frequency = -1; 
        }

        private bool DrawDefaultInspectorSelection
        {
            get => EditorPrefs.GetBool("Screen Overlay Draw Default Inspector", false);
            set => EditorPrefs.SetBool("Screen Overlay Draw Default Inspector", value);
        }
        
        private bool DrawDistanceCheckSelection
        {
            get => EditorPrefs.GetBool("Screen Overlay Draw Distance Check", false);
            set => EditorPrefs.SetBool("Screen Overlay Draw Distance Check", value);
        }
    }
}