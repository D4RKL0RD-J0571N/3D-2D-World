using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof(shakeCharacterBodyPartSystem))]
public class shakeCharacterBodyPartSystemEditor : Editor
{
	shakeCharacterBodyPartSystem manager;

	void OnEnable ()
	{
		manager = (shakeCharacterBodyPartSystem)target;
	}

	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector ();

		EditorGUILayout.Space ();

		GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Test Shake")) {
			if (Application.isPlaying) {
				manager.activatShakeAnimationXSecondsFromEditor ();
			}
		}

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Store Body Parts")) {
			if (!Application.isPlaying) {
				manager.storeCharacterBones ();
			}
		}

		EditorGUILayout.Space ();
	}
}
#endif