#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor (typeof (UIRouter))]
public class UIRouterEditor : Editor
{
    UIRouter manager;

    void OnEnable ()
    {
        manager = (UIRouter)target;
    }

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ();

        EditorGUILayout.Space ();

        GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Store All UI Pages")) {
            if (!Application.isPlaying) {
                manager.storeAllUIPages ();
            }
        }

        if (GUILayout.Button ("Bake All UI Pages")) {
            if (!Application.isPlaying) {
                manager.bakeAllUIPages ();
            }
        }
    }
}
#endif