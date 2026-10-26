using UnityEngine;
using System.Collections;

public class skidsManager : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool skidsEnabled = true;

	public int maxMarks = 1000;
	public float markWidth = 0.3f;
	public float groundOffset = 0.02f;
	public float minDistance = 0.1f;

	[Space]
	[Header ("Debugs")]
	[Space]

	public GameObject originalMesh;

	markSection[] skidmarks;
	bool updated = false;
	int numMarks = 0;
	Mesh currentMesh;

	void Start ()
	{
		skidmarks = new markSection[maxMarks];

		for (int i = 0; i < maxMarks; i++) {
			skidmarks [i] = new markSection ();
		}

		MeshFilter mainMeshFilter = originalMesh.GetComponent<MeshFilter> ();
			
		mainMeshFilter.mesh = new Mesh ();

		originalMesh.transform.SetParent (transform);

		originalMesh.transform.position = Vector3.zero;

		originalMesh.transform.rotation = Quaternion.identity;

		currentMesh = mainMeshFilter.mesh;
	}

	void LateUpdate ()
	{
		if (!skidsEnabled) {
			return;
		}

		// If the mesh needs to be updated, i.e. a new section has been added,
		// the current mesh is removed, and a new mesh for the skidmarks is generated.
		if (updated) {
			
			updated = false;

			Mesh mesh = currentMesh;

			mesh.Clear ();

			int segmentCount = 0;

			for (int j = 0; j < numMarks && j < maxMarks; j++) {
				if (skidmarks [j].lastIndex != -1 && skidmarks [j].lastIndex > numMarks - maxMarks) {
					segmentCount++;
				}
			}

			int segmentCountFour = segmentCount * 4;

			Vector3[] vertices = new Vector3[segmentCountFour];
			Vector3[] normals = new Vector3[segmentCountFour];
			Vector4[] tangents = new Vector4[segmentCountFour];
			Color[] colors = new Color[segmentCountFour];
			Vector2[] uvs = new Vector2[segmentCountFour];
			int[] triangles = new int[segmentCount * 6];

			segmentCount = 0;

			for (int i = 0; i < numMarks && i < maxMarks; i++) {
				if (skidmarks [i].lastIndex != -1 && skidmarks [i].lastIndex > numMarks - maxMarks) {
					markSection currentMark = skidmarks [i];

					markSection last = skidmarks [currentMark.lastIndex % maxMarks];

					segmentCountFour = segmentCount * 4;

					int verticeIndex1 = segmentCountFour + 0;
					int verticeIndex2 = segmentCountFour + 1;
					int verticeIndex3 = segmentCountFour + 2;
					int verticeIndex4 = segmentCountFour + 3;

					vertices [verticeIndex1] = last.positionLeft;
					vertices [verticeIndex2] = last.positionRight;
					vertices [verticeIndex3] = currentMark.positionLeft;
					vertices [verticeIndex4] = currentMark.positionRight;

					normals [verticeIndex1] = last.normal;
					normals [verticeIndex2] = last.normal;
					normals [verticeIndex3] = currentMark.normal;
					normals [verticeIndex4] = currentMark.normal;

					tangents [verticeIndex1] = last.tangent;
					tangents [verticeIndex2] = last.tangent;
					tangents [verticeIndex3] = currentMark.tangent;
					tangents [verticeIndex4] = currentMark.tangent;

					Color lastColor = new Color (0, 0, 0, last.intensity);
					Color currentColor = new Color (0, 0, 0, currentMark.intensity);

					colors [verticeIndex1] = lastColor;
					colors [verticeIndex2] = lastColor;
					colors [verticeIndex3] = currentColor;
					colors [verticeIndex4] = currentColor;

					uvs [verticeIndex1] = Vector2.zero;
					uvs [verticeIndex2] = Vector2.right;
					uvs [verticeIndex3] = Vector2.up;
					uvs [verticeIndex4] = Vector2.one;

					int segmentCountSix = segmentCount * 6;

					triangles [segmentCountSix + 0] = verticeIndex1;
					triangles [segmentCountSix + 2] = verticeIndex2;
					triangles [segmentCountSix + 1] = verticeIndex3;
					triangles [segmentCountSix + 3] = verticeIndex3;
					triangles [segmentCountSix + 5] = verticeIndex2;
					triangles [segmentCountSix + 4] = verticeIndex4;

					segmentCount++;		
				}
			}

			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.tangents = tangents;
			mesh.colors = colors;
			mesh.uv = uvs;
			mesh.triangles = triangles;
		}
	}

	// Function called by the wheels that is skidding. Gathers all the information needed to
	// create the mesh later. Sets the intensity of the skidmark section b setting the alpha
	// of the vertex color.
	public int AddSkidMark (Vector3 pos, Vector3 normal, float intensity, int lastIndex)
	{
		if (!skidsEnabled) {
			return -1;
		}

		int index = numMarks % maxMarks;

		if (intensity > 1) {
			intensity = 1;
		}

		if (skidmarks == null || intensity < 0 || index > skidmarks.Length) {
			return -1;
		}

		markSection curr = skidmarks [index];

		curr.position = pos + groundOffset * normal;

		curr.normal = normal;

		curr.intensity = intensity;
		curr.lastIndex = lastIndex;

		if (lastIndex != -1) {
			markSection last = skidmarks [lastIndex % maxMarks];

			Vector3 currentPosition = curr.position;

			Vector3 dir = (currentPosition - last.position);

			Vector3 xDir = Vector3.Cross (dir, normal).normalized;

			curr.positionLeft = currentPosition + (markWidth * 0.5f) * xDir;

			curr.positionRight = currentPosition - (markWidth * 0.5f) * xDir;

			curr.tangent = new Vector4 (xDir.x, xDir.y, xDir.z, 1);

			if (last.lastIndex == -1) {
				last.tangent = curr.tangent;

				last.positionLeft = currentPosition + (markWidth * 0.5f) * xDir;

				last.positionRight = currentPosition - (markWidth * 0.5f) * xDir;
			}
		}

		numMarks++;

		updated = true;

		return numMarks - 1;
	}

	public void setSkidsEnabledState (bool state)
	{
		skidsEnabled = state;
	}

	class markSection
	{
		public Vector3 position;
		public Vector3 normal;
		public Vector4 tangent;
		public Vector3 positionLeft;
		public Vector3 positionRight;
		public float intensity;
		public int lastIndex;
	};
}