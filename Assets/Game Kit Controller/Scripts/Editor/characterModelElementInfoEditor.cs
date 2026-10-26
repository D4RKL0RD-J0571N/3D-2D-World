using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof (characterModelElementInfo))]
public class characterModelElementInfoEditor : Editor
{
    characterModelElementInfo manager;

    void OnEnable ()
    {
        manager = (characterModelElementInfo)target;
    }

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ();

        EditorGUILayout.Space ();

        GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Get Character Elements")) {
            manager.getCharacterElements ();
        }

        EditorGUILayout.Space ();
    }
}
#endif