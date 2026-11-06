using UnityEditor;
using UnityEngine;
using static MagicPigGames.Shared.MagicPigEditor;
using static MagicPigGames.Northstar.NorthstarSystemEditor;

namespace MagicPigGames.Northstar
{
    [CustomEditor(typeof(Compass))]
    public class CompassEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var compass = (Compass)target;

            LinkToDocs("https://infinitypbr.gitbook.io/infinity-pbr/northstar-tracking-system/northstar-documentation/compass-and-radar/compass");

            Header1($"Compass");
            Label($"<color=#999999>There are <color=#99ffff><b>{compass.presetTrackedObjects.Count}</b> Compass Objects</color> in this Compass.</color>", false, true, true);
            GreyLine();

            if (compass.northstarSettings == null)
            {
                EditorGUILayout.HelpBox("No Global Northstar Settings found. Click below to create one.", MessageType.Warning);
                if (GUILayout.Button("Create Global Northstar Settings"))
                {
                    Undo.RecordObject(compass, "Create Global Northstar Settings");
                    CreateGlobalNorthstarSettings(compass);
                    EditorUtility.SetDirty(compass);
                }
                return;
            }

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(compass, "Compass Settings");
            RequiredFields(compass);
            GreyLine();
            CompassObjects(compass);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(compass);
        }
        
        // When the Inspector is viewed, this will populate all the Compass Objects automatically.
        private void OnEnable()
        {
            var compass = (Compass)target;

            if (!UnityEngine.Application.isPlaying)
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
    }
}