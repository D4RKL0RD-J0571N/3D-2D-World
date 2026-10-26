using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor (typeof(featuresManager))]
public class featuresManagerEditor : Editor
{
	featuresManager manager;

	SerializedProperty explanation;

	void OnEnable ()
	{
		explanation = serializedObject.FindProperty ("explanation");

		manager = (featuresManager)target;
	}

	public override void OnInspectorGUI ()
	{
        EditorGUI.BeginChangeCheck ();

        EditorGUILayout.Space ();

		EditorGUILayout.PropertyField (explanation);

		//if (GUILayout.Button ("Set Configuration")) {
		//	manager.setConfiguration ();
		//}
		//if (GUILayout.Button ("Get Current Configuration")) {
		//	manager.getConfiguration ();
		//}

		//EditorGUILayout.Space ();

		//DrawDefaultInspector ();

		//EditorGUILayout.Space ();

		//if (GUILayout.Button ("Set Configuration")) {
		//	manager.setConfiguration ();
		//}
		//if (GUILayout.Button ("Get Current Configuration")) {
		//	manager.getConfiguration ();
		//}

		EditorGUILayout.Space ();

        if (EditorGUI.EndChangeCheck ()) {
            serializedObject.ApplyModifiedProperties ();

            Repaint ();
        }
    }
}
#endif