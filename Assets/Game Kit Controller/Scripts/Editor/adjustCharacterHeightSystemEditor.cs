using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof (adjustCharacterHeightSystem))]
public class adjustCharacterHeightSystemEditor : Editor
{
    adjustCharacterHeightSystem manager;

    void OnEnable ()
    {
        manager = (adjustCharacterHeightSystem)target;
    }

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ();

        EditorGUILayout.Space ();

        GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Adjust Height")) {
            if (!Application.isPlaying) {
                manager.adjustHeightValue ();
            }
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Adjust Camera States")) {
            if (!Application.isPlaying) {
                manager.adjustCameraStates ();
            }
        }

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        if (GUILayout.Button ("Adjust Weapons Scale")) {
            if (!Application.isPlaying) {
                manager.adjustFireWeaponsScale ();
            }
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Adjust Weapons Position")) {
            if (!Application.isPlaying) {
                manager.adjustFireWeaponsOffset ();
            }
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Adjust Weapons Transform References")) {
            if (!Application.isPlaying) {
                manager.adjustFireWeaponsTransformReferences ();
            }
        }
    }
}
#endif