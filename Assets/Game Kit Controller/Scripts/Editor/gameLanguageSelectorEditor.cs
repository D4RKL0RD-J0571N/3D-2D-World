using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof (gameLanguageSelector))]
public class gameLanguageSelectorEditor : Editor
{
    gameLanguageSelector manager;

    void OnEnable ()
    {
        manager = (gameLanguageSelector)target;
    }

    GUIStyle style = new GUIStyle ();


    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ();

        EditorGUILayout.Space ();
        EditorGUILayout.Space ();

        style.fontStyle = FontStyle.Bold;
        style.fontSize = 20;
        style.alignment = TextAnchor.MiddleCenter;

        EditorGUILayout.LabelField ("EDITOR BUTTONS", style);

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Set Localization for PC")) {
            manager.toggleLocalizationForPCorMobile (false);
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Set Localization for Mobile")) {
            manager.toggleLocalizationForPCorMobile (true);
        }
    }
}
#endif