using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof (sharedActionSystemRemoteActivator))]
public class sharedActionSystemRemoteActivatorEditor : Editor
{
    sharedActionSystemRemoteActivator manager;

    SerializedProperty currentArrayElement;

    void OnEnable ()
    {
        manager = (sharedActionSystemRemoteActivator)target;
    }

    public override void OnInspectorGUI ()
    {
        EditorGUILayout.Space ();

        DrawDefaultInspector ();

        EditorGUILayout.Space ();

        GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

        EditorGUILayout.Space ();

        EditorGUILayout.Space ();

        if (GUILayout.Button ("\n ACTIVATE SHARED ACTION \n")) {
            if (Application.isPlaying) {
                manager.activateSharedActionByNameFromEditor ();
            }
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("\n ACTIVATE SHARED ACTION DETECTION\n")) {
            if (Application.isPlaying) {
                manager.checkSharedActionSystemByExternalDetectionFromEditor ();
            }
        }

        EditorGUILayout.Space ();
    }
}

#endif
