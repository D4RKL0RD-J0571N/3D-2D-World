using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof(sharedActionZone))]
public class sharedActionZoneEditor : Editor
{
	sharedActionZone manager;

	void OnEnable ()
	{
		manager = (sharedActionZone)target;
	}

	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector ();

		EditorGUILayout.Space ();

		EditorGUILayout.Space ();

		GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Spawn Shared Action Object On Scene")) {
			manager.spawnSharedActionObjectOnSceneFromEditor ();
		}

		EditorGUILayout.Space ();

        if (GUILayout.Button ("Get Shared Action Manager on Scene")) {
            manager.getSharedActionManagerrOnScene ();
        }

        EditorGUILayout.Space ();
    }
}
#endif