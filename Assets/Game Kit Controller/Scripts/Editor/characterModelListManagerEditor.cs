using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof (characterModelListManager))]
public class characterModelListManagerEditor : Editor
{
    characterModelListManager manager;

    void OnEnable ()
    {
        manager = (characterModelListManager)target;
    }

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ();

        EditorGUILayout.Space ();

        GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Replace Model")) {
            manager.replaceCharacterModel ();
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Set Next Model")) {
            manager.setNextOrPreviousCharacterModel (true);
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Set Previous Model")) {
            manager.setNextOrPreviousCharacterModel (false);
        }

        EditorGUILayout.Space ();
    }
}
#endif