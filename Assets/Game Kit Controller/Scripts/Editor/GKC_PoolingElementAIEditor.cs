using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof (GKC_PoolingElementAI))]
public class GKC_PoolingElementAIEditor : Editor
{
    GKC_PoolingElementAI manager;

    void OnEnable ()
    {
        manager = (GKC_PoolingElementAI)target;
    }

    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector ();

        EditorGUILayout.Space ();

        GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Enable Pooling Elements On Object")) {
            manager.checkEventsOnEnableOrDisablePoolingManagementOnObjectFromEditor (true);
        }

        EditorGUILayout.Space ();

        if (GUILayout.Button ("Disable Pooling Elements On Object")) {
            manager.checkEventsOnEnableOrDisablePoolingManagementOnObjectFromEditor (false);
        }

        EditorGUILayout.Space ();
    }
}
#endif
