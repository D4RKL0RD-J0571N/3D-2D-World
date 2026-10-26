using UnityEngine;
using System.Collections;

public class rotateObjects : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public Vector3 direccion;
	public float speed = 1;
	public bool rotationEnabled = true;

	public float resetRotationSpeed = 5;

    [Space]
    [Header ("Oscillation Settings")]
    [Space]

    public bool useOscillation;
    public Vector3 oscillationAxis = Vector3.up;
    public float oscillationAngle = 30f;
    public float oscillationSpeed = 1f;

    [Space]
    [Header ("Components")]
    [Space]

    public Transform mainTransform;


	Coroutine rotationCoroutine;

    Quaternion initialRotation;


    void Start ()
	{
		if (mainTransform == null) {
			mainTransform = transform;
		}

        initialRotation = mainTransform.localRotation;	
    }

	void FixedUpdate ()
	{
        if (!rotationEnabled)
            return;

        if (useOscillation) {
            float angle = Mathf.Sin (Time.time * oscillationSpeed) * oscillationAngle;

            mainTransform.localRotation =
                initialRotation * Quaternion.AngleAxis (angle, oscillationAxis.normalized);
        } else {
            mainTransform.Rotate ((speed * Time.deltaTime) * direccion);
        }
    }

	public void enableOrDisableRotation ()
	{
		rotationEnabled = !rotationEnabled;
	}

	public void increaseRotationSpeedTenPercentage ()
	{
		speed += speed * 0.1f;
	}

	public void setRotationEnabledState (bool state)
	{
		stopRotation ();

		rotationEnabled = state;
	}

	public void disableRotation ()
	{
		setRotationEnabledState (false);
	}

	public void enableRotation ()
	{
		setRotationEnabledState (true);
	}

	public void resetRotation ()
	{
		stopRotation ();

		rotationCoroutine = StartCoroutine (resetRotationCoroutine ());
	}

	public void stopRotation ()
	{
		if (rotationCoroutine != null) {
			StopCoroutine (rotationCoroutine);
		}
	}

	IEnumerator resetRotationCoroutine ()
	{
		Quaternion targetRotation = Quaternion.identity;

		float t = 0;

		while (transform.localRotation != targetRotation) {
			t += Time.deltaTime / resetRotationSpeed;

			transform.localRotation = Quaternion.Lerp (transform.localRotation, targetRotation, t);

			yield return null;
		}
	}

	public void setRandomDirectionToCurrent ()
	{
		int newDirection = Random.Range (0, 2);

		if (newDirection == 0) {
			newDirection = -1;
		}

		direccion *= newDirection;
	}

	public void setNewRotationSpeed (float newValue)
	{
		speed = newValue;
	}

	public void setEnabledState (bool state)
	{
		enabled = state;
	}
}
