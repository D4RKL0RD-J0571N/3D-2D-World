using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof(saveGameSystem))]
public class saveGameSystemEditor : Editor
{
	saveGameSystem manager;

	void OnEnable ()
	{
		manager = (saveGameSystem)target;
	}

	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector ();

		EditorGUILayout.Space ();

		GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Show Save Info Debug")) {
			manager.showSaveInfoDebug ();
		}
	}
}
#endif