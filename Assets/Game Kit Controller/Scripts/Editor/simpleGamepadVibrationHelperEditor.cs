using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof(simpleGamepadVibrationHelper))]
public class simpleGamepadVibrationHelperEditor : Editor
{
    simpleGamepadVibrationHelper manager;

	void OnEnable ()
	{
		manager = (simpleGamepadVibrationHelper)target;
	}

	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector ();

		EditorGUILayout.Space ();

		GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Test Vibration")) {
			if (Application.isPlaying) {
				manager.testVibration ();
			}
		}

		EditorGUILayout.Space ();
	}
}
#endif