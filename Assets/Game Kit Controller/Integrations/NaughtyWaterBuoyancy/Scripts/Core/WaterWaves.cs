using UnityEngine;

namespace NaughtyWaterBuoyancy
{
	public class WaterWaves : MonoBehaviour
	{
		[Header ("Main Settings")]
		[Space]

		public bool wavesEnabled = true;

		[SerializeField]
		private float speed = 1f;

		[SerializeField]
		private float height = 0.2f;

		[SerializeField]
		private float noiseWalk = 0.5f;

		[SerializeField]
		private float noiseStrength = 0.1f;

		private Mesh mesh;
		private Vector3[] baseVertices;
		private Vector3[] vertices;

		int verticesLength;

		Vector3 currentBaseVertices;

		float currentLocalScaleY;

		float currentSize;

		protected virtual void Awake ()
		{
			mesh = GetComponent<MeshFilter> ().mesh;

			baseVertices = mesh.vertices;

			vertices = new Vector3[baseVertices.Length];
		}

		protected virtual void Start ()
		{
			ResizeBoxCollider ();

			verticesLength = vertices.Length;

			currentLocalScaleY = transform.localScale.y;

			currentSize = (height / currentLocalScaleY);
		}

		protected virtual void Update ()
		{
			if (wavesEnabled) {
				for (var i = 0; i < verticesLength; i++) {
					currentBaseVertices = baseVertices [i];

					var vertex = currentBaseVertices;

					vertex.y += 
					Mathf.Sin (Time.time * speed + currentBaseVertices.x + currentBaseVertices.y + currentBaseVertices.z) *
					currentSize;

					vertex.y += 
						Mathf.PerlinNoise (currentBaseVertices.x + noiseWalk, currentBaseVertices.y /*+ Mathf.Sin(Time.time * 0.1f)*/) *
					noiseStrength;

					vertices [i] = vertex;
				}

				mesh.vertices = vertices;
				mesh.RecalculateNormals ();
			}
		}

		private void ResizeBoxCollider ()
		{
			var boxCollider = GetComponent<BoxCollider> ();

			if (boxCollider != null) {
				Vector3 center = boxCollider.center;
				center.y = boxCollider.size.y / -2f;
				center.y += (height + noiseStrength) / transform.localScale.y;

				boxCollider.center = center;
			}
		}
	}
}
