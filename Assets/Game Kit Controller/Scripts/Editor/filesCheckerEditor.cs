using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof (filesChecker))]
public class filesCheckerEditor : Editor
{
    filesChecker manager;

    void OnEnable ()
    {
        manager = (filesChecker)target;
    }

    public override void OnInspectorGUI ()
    {
        EditorGUILayout.Space ();

        DrawDefaultInspector ();

        EditorGUILayout.Space ();

        GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

        EditorGUILayout.Space ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("\n CHECK OBJECTS \n")) {
            manager.checkPrefabs ();
        }

        EditorGUILayout.Space ();
    }
}

#endif