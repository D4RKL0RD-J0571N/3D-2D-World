using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class noiseMeshSystem : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool noiseMeshesEnabled = true;

	public string noiseMeshParentName = "Noise Mesh Parent";

	public GameObject noiseMeshPrefab;

	public Transform noiseMeshesParent;

	public const string mainManagerName = "Noise Mesh Manager";

	public static string getMainManagerName ()
	{
		return mainManagerName;
	}


	private static noiseMeshSystem _noiseMeshSystemInstance;

	public static noiseMeshSystem Instance { get { return _noiseMeshSystemInstance; } }

	bool instanceInitialized;


	public void getComponentInstance ()
	{
		if (instanceInitialized) {
			return;
		}

		if (_noiseMeshSystemInstance != null && _noiseMeshSystemInstance != this) {
			Destroy (this.gameObject);

			return;
		} 

		_noiseMeshSystemInstance = this;

		instanceInitialized = true;
	}

	void Awake ()
	{
		getComponentInstance ();

		if (noiseMeshesParent == null) {
			GameObject newNoiseMeshesParent = new GameObject ();
			newNoiseMeshesParent.name = noiseMeshParentName;

			noiseMeshesParent = newNoiseMeshesParent.transform;
		}
	}

	public void addNoiseMesh (float scaleMultiplier, Vector3 noisePosition, float noiseExpandSpeed)
	{
		if (!noiseMeshesEnabled) {
			return;
		}

		GameObject newNoiseMesh = (GameObject)Instantiate (noiseMeshPrefab, noisePosition, Quaternion.identity);

		footStepState newFootStepState = new footStepState ();

		newFootStepState.noiseTransform = newNoiseMesh.transform;

		newFootStepState.noiseTransformExpand = StartCoroutine (startNoiseTransformExpandCoroutine (newFootStepState.noiseTransform, scaleMultiplier, noiseExpandSpeed));

		newNoiseMesh.transform.SetParent (noiseMeshesParent);
	}

	static IEnumerator startNoiseTransformExpandCoroutine (Transform noiseTransform, float scaleMultiplier, float noiseExpandSpeed)
	{
		Vector3 targetScale = Vector3.one * scaleMultiplier;

		while (noiseTransform.transform.localScale.magnitude < targetScale.magnitude) {
			noiseTransform.transform.localScale = Vector3.MoveTowards (noiseTransform.transform.localScale, targetScale, Time.deltaTime * noiseExpandSpeed);

			yield return null;
		}

		Destroy (noiseTransform.gameObject);
	}

	[System.Serializable]
	public class footStepState
	{
		public Transform noiseTransform;
		public Coroutine noiseTransformExpand;
	}
}
