using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CustomEditor (typeof (combineMeshSystem))]
public class combineMeshSystemEditor : Editor
{
    combineMeshSystem manager;

    void OnEnable ()
    {
        manager = (combineMeshSystem)target;
    }

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ();

        EditorGUILayout.Space ();

        GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Get Object Meshes")) {
            if (!Application.isPlaying) {
                manager.getObjectMeshes ();
            }
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Clear Mesh List")) {
            if (!Application.isPlaying) {
                manager.clearMeshList ();
            }
        }

        EditorGUILayout.Space ();
    }
}
