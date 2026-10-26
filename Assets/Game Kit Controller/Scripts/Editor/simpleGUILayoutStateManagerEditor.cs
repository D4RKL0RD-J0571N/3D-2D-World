using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof(simpleGUILayoutStateManager))]
public class simpleGUILayoutStateManagerEditor : Editor
{
	simpleGUILayoutStateManager manager;

	void OnEnable ()
	{
		manager = (simpleGUILayoutStateManager)target;
	}

	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector ();

		EditorGUILayout.Space ();

		GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Enable All GUILayout")) {
			if (!Application.isPlaying) {
				manager.setGUILayoutState (true);
			}
		}

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Disable All GUILayout")) {
			if (!Application.isPlaying) {
				manager.setGUILayoutState (false);
			}
		}

		EditorGUILayout.Space ();
	}
}
#endif