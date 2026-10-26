using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof(GKC_PoolingAIManager))]
public class GKC_PoolingAIManagerEditor : Editor
{
	GKC_PoolingAIManager manager;

	void OnEnable ()
	{
		manager = (GKC_PoolingAIManager)target;
	}

	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector ();

		EditorGUILayout.Space ();

		GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Get All Pooling AI Elements On Level")) {
			manager.getAllGKC_PoolingElementAIOnLevel ();
		}

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Clear Pooling AI Elements On List")) {
			manager.clearGKC_PoolingElementAIList();
		}

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Enable All Pooling AI Elements On Level")) {
			manager.enableAllGKC_PoolingElementAIOnLevel ();
		}

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Disable All Pooling AI Elements On Level")) {
			manager.disableAllGKC_PoolingElementAIOnLevel ();
		}

		EditorGUILayout.Space ();
	}
}
#endif