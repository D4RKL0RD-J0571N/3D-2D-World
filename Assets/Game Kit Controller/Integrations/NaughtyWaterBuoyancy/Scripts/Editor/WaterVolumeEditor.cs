using UnityEditor;
using UnityEngine;

namespace NaughtyWaterBuoyancy.Editor
{
	[CustomEditor (typeof(WaterVolume))]
	public class WaterVolumeEditor : UnityEditor.Editor
	{
		private SerializedProperty waterHeight;

		private WaterVolume waterVolumeTarget;
		private SerializedProperty density;
		private SerializedProperty rows;
		private SerializedProperty columns;
		private SerializedProperty quadSegmentSize;

		private SerializedProperty useCustomGravityForce;
		private SerializedProperty gravityForce;

		private SerializedProperty objectLayermask;

		private SerializedProperty checkObjectByTag;
		private SerializedProperty tagListToCheck;
		private SerializedProperty checkForFloatingObjectSocketEnabled;

		SerializedProperty showDebugPrint;
		SerializedProperty showGizmo;

        WaterVolume manager;

        //		[MenuItem ("NaughtyWaterBouyancy/Create Water Mesh")]
        //		private static void CreateMesh ()
        //		{
        //			Mesh mesh = WaterMeshGenerator.GenerateMesh (5, 5, 1f);
        //			AssetDatabase.CreateAsset (mesh, "Assets/NaughtyWaterBuoyancy/Models/Water Mesh.asset");
        //		}

        protected virtual void OnEnable ()
		{
			this.waterVolumeTarget = (WaterVolume)this.target;

			this.waterHeight = this.serializedObject.FindProperty ("waterHeight");

			this.density = this.serializedObject.FindProperty ("density");
			this.rows = this.serializedObject.FindProperty ("rows");
			this.columns = this.serializedObject.FindProperty ("columns");
			this.quadSegmentSize = this.serializedObject.FindProperty ("quadSegmentSize");

			this.useCustomGravityForce = this.serializedObject.FindProperty ("useCustomGravityForce");
			this.gravityForce = this.serializedObject.FindProperty ("gravityForce");

			this.objectLayermask = this.serializedObject.FindProperty ("objectLayermask");
			this.checkObjectByTag = this.serializedObject.FindProperty ("checkObjectByTag");
			this.tagListToCheck = this.serializedObject.FindProperty ("tagListToCheck");
			this.checkForFloatingObjectSocketEnabled = this.serializedObject.FindProperty ("checkForFloatingObjectSocketEnabled");

			this.showDebugPrint = this.serializedObject.FindProperty ("showDebugPrint");
			this.showGizmo = this.serializedObject.FindProperty ("showGizmo");


			Undo.undoRedoPerformed += this.OnUndoRedoPerformed;

            manager = (WaterVolume)target;
        }

		protected virtual void OnDisable ()
		{
			Undo.undoRedoPerformed -= this.OnUndoRedoPerformed;
		}

		public override void OnInspectorGUI ()
		{
			this.serializedObject.Update ();

			EditorGUILayout.Space ();

			EditorGUI.BeginChangeCheck ();
			EditorGUILayout.PropertyField (this.waterHeight);
			EditorGUILayout.PropertyField (this.rows);
			EditorGUILayout.PropertyField (this.columns);
			EditorGUILayout.PropertyField (this.quadSegmentSize);

			EditorGUILayout.PropertyField (this.useCustomGravityForce);
			EditorGUILayout.PropertyField (this.gravityForce);

			if (EditorGUI.EndChangeCheck ()) {
				this.rows.intValue = Mathf.Max (1, this.rows.intValue);
				this.columns.intValue = Mathf.Max (1, this.columns.intValue);
				this.quadSegmentSize.floatValue = Mathf.Max (0f, this.quadSegmentSize.floatValue);

				if (!Application.isPlaying) {
					this.UpdateMesh (this.rows.intValue, this.columns.intValue, this.quadSegmentSize.floatValue);
					this.UpdateBoxCollider (this.rows.intValue, this.columns.intValue, this.quadSegmentSize.floatValue);
				}
			}

			EditorGUILayout.PropertyField (this.density);

			EditorGUILayout.Space ();

			EditorGUILayout.PropertyField (this.objectLayermask);
			EditorGUILayout.PropertyField (this.checkObjectByTag);

			if (this.checkObjectByTag.boolValue) {
				EditorGUILayout.Space ();

				showSimpleList (this.tagListToCheck);
			}

			EditorGUILayout.PropertyField (this.checkForFloatingObjectSocketEnabled);

			EditorGUILayout.Space ();

			EditorGUILayout.PropertyField (this.showDebugPrint);
			EditorGUILayout.PropertyField (this.showGizmo);

			EditorGUILayout.Space ();

            EditorGUILayout.Space ();

            if (GUILayout.Button ("Update System")) {
				if (!Application.isPlaying) {
					this.UpdateMesh (this.rows.intValue, this.columns.intValue, this.quadSegmentSize.floatValue);
					this.UpdateBoxCollider (this.rows.intValue, this.columns.intValue, this.quadSegmentSize.floatValue);
				}
            }

            this.serializedObject.ApplyModifiedProperties ();
		}

		private void UpdateMesh (int rows, int columns, float quadSegmentSize)
		{
			MeshFilter meshFilter = this.waterVolumeTarget.GetComponent<MeshFilter> ();
			Mesh oldMesh = meshFilter.sharedMesh;

			Mesh newMesh = WaterMeshGenerator.GenerateMesh (rows, columns, quadSegmentSize);
			newMesh.name = "Water Mesh Instance";

			meshFilter.sharedMesh = newMesh;

			EditorUtility.SetDirty (meshFilter);

			if (oldMesh != null && !AssetDatabase.Contains (oldMesh)) {
				DestroyImmediate (oldMesh);
			}
		}

		private void UpdateBoxCollider (int rows, int columns, float quadSegmentSize)
		{
			var boxCollider = this.waterVolumeTarget.GetComponent<BoxCollider> ();

			if (boxCollider != null) {
				Vector3 size = new Vector3 (columns * quadSegmentSize, waterHeight.floatValue, rows * quadSegmentSize);
				boxCollider.size = size;

				Vector3 center = size / 2f;
				center.y *= -1f;
				boxCollider.center = center;

				EditorUtility.SetDirty (boxCollider);
			}
		}

		private void OnUndoRedoPerformed ()
		{
			this.UpdateMesh (this.waterVolumeTarget.Rows, this.waterVolumeTarget.Columns, this.waterVolumeTarget.QuadSegmentSize);

			this.UpdateBoxCollider (this.waterVolumeTarget.Rows, this.waterVolumeTarget.Columns, this.waterVolumeTarget.QuadSegmentSize);
		}

		void showSimpleList (SerializedProperty list)
		{
			EditorGUILayout.Space ();

			if (GUILayout.Button ("Show/Hide " + list.displayName)) {
				list.isExpanded = !list.isExpanded;
			}

			EditorGUILayout.Space ();

			if (list.isExpanded) {
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("Add")) {
					list.arraySize++;
				}
				if (GUILayout.Button ("Clear")) {
					list.arraySize = 0;
				}
				GUILayout.EndHorizontal ();

				EditorGUILayout.Space ();

				for (int i = 0; i < list.arraySize; i++) {
					GUILayout.BeginHorizontal ();
					if (i < list.arraySize && i >= 0) {
						EditorGUILayout.PropertyField (list.GetArrayElementAtIndex (i), new GUIContent ("", null, ""), false);
					}
					GUILayout.BeginHorizontal ();
					if (GUILayout.Button ("x")) {
						list.DeleteArrayElementAtIndex (i);
						return;
					}
					if (GUILayout.Button ("v")) {
						if (i >= 0) {
							list.MoveArrayElement (i, i + 1);
						}
					}
					if (GUILayout.Button ("^")) {
						if (i < list.arraySize) {
							list.MoveArrayElement (i, i - 1);
						}
					}
					GUILayout.EndHorizontal ();
					GUILayout.EndHorizontal ();
				}
			}       
		}
	}
}
