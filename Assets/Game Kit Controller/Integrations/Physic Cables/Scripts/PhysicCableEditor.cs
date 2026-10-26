using UnityEngine;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;

namespace HPhysic
{
    [CustomEditor (typeof (PhysicCable))]
    public class PhysicCableEditor : Editor
    {
        PhysicCable manager;

        void OnEnable ()
        {
            manager = (PhysicCable)target;
        }

        public override void OnInspectorGUI ()
        {
            DrawDefaultInspector ();

            EditorGUILayout.Space ();

            GUILayout.Label ("EDITOR BUTTONS", EditorStyles.boldLabel);


            EditorGUILayout.Space ();

            if (GUILayout.Button ("Update Points & Values")) {
                if (!Application.isPlaying) {
                    manager.UpdatePointsFromEditor ();
                }
            }
            EditorGUILayout.Space ();

            if (GUILayout.Button ("Add Point")) {
                if (!Application.isPlaying) {
                    manager.AddPointFromEditor ();
                }
            }

            EditorGUILayout.Space ();

            if (GUILayout.Button ("Remove Point")) {
                if (!Application.isPlaying) {
                    manager.RemovePointFromEditor ();
                }
            }

            EditorGUILayout.Space ();
        }
    }
}
#endif
