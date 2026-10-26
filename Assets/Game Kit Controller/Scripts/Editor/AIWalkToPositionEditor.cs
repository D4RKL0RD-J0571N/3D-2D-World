using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof(AIWalkToPosition))]
public class AIWalkToPositionEditor : Editor
{
	AIWalkToPosition manager;

	void OnEnable ()
	{
		manager = (AIWalkToPosition)target;
	}

	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector ();

		EditorGUILayout.Space ();

		GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Activate Walk To Position")) {
			if (Application.isPlaying) {
				manager.activateWalkToPosition ();
			}
		}

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Stop Walk To Position")) {
			if (Application.isPlaying) {
				manager.stopActivateWalkPosition ();
			}
		}

		EditorGUILayout.Space ();
	}
}
#endif