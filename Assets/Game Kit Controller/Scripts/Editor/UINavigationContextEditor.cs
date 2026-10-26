#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor (typeof (UINavigationContext))]
public class UINavigationContextEditor : Editor
{
    UINavigationContext manager;

    void OnEnable ()
    {
        manager = (UINavigationContext)target;
    }

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ();

        GUILayout.Space (10);

        if (GUILayout.Button ("Bake Navigation Items")) {
            if (!Application.isPlaying) {
                manager.bakeNavigationItemsFromEditor ();
            }
        }

        if (GUILayout.Button ("Clear Baked List")) {
            if (!Application.isPlaying) {
                manager.clearNavigationItemsFromEditor ();
            }
        }
    }
}
#endif