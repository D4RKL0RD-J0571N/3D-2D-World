using UnityEngine;
using UnityEditor;

namespace SoftKitty.MasterNavigationMap
{
    [CustomEditor(typeof(MapCreatorTag))]
    public class MapCreatorTag_Inspector : Editor
    {
        public override void OnInspectorGUI()
        {
            MapCreatorTag myTarget = (MapCreatorTag)target;

            EditorUtils.Document("master-map-navigation/map-generator", false);

            GUILayout.BeginVertical(EditorUtils._groupStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("Water: ", GUILayout.Width(80));
            myTarget._water = GUILayout.Toggle(myTarget._water,"");
            GUILayout.EndHorizontal();

            if (!myTarget._water)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("Tag: ",GUILayout.Width(80));
                myTarget._tag = GUILayout.TextField(myTarget._tag,GUILayout.Width(100));
                GUILayout.EndHorizontal();
            }
            else
            {
                myTarget._tag = "";
            }

            GUILayout.EndVertical();

            if (GUI.changed && !Application.isPlaying) UnityEditor.EditorUtility.SetDirty(myTarget);
        }
    }
}
