using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof(sharedActionZoneManager))]
public class sharedActionZoneManagerEditor : Editor
{
	sharedActionZoneManager manager;

	void OnEnable ()
	{
		manager = (sharedActionZoneManager)target;
	}

	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector ();

		EditorGUILayout.Space ();

		EditorGUILayout.Space ();

		GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Get All Shared Action Zones On Scene")) {
			manager.getAllSharedActionZonesOnScene ();
		}

		EditorGUILayout.Space ();
	}
}
#endif