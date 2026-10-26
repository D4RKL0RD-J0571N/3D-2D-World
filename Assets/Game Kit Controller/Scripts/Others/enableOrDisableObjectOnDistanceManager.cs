using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class enableOrDisableObjectOnDistanceManager : MonoBehaviour
{
	[Header ("Behavior Main Settings")]
	[Space]

	public bool addObjectToMainSystem = true;

	public bool addObjectOnStartWithDelay;

	public float delayToAddObjectOnStart = 0.01f;

	public bool addObjectOnAwake;

	public Transform mainTransform;

	public string mainManagerName = "Disable Objects On Distance Manager";

	public bool setActiveStateOnMainTransformEnabled = true;

	[Space]
	[Header ("Distance Settings")]
	[Space]

	public bool useCustomDistance;

	public float maxDistanceObjectEnabledOnScreen;
	public float maxDistanceObjectEnableOutOfScreen;

	[Space]
	[Header ("Debug")]
	[Space]

	public bool objectActive = true;

	public bool objectInfoSentToMainSystem;

	public bool mainEnableOrDisableObjectsOnDistanceSystemLocated;

	public enableOrDisableObjectsOnDistanceSystem mainEnableOrDisableObjectsOnDistanceSystem;

	[Space]
	[Header ("Events Settings")]
	[Space]

	public UnityEvent eventOnEnableObject;
	public UnityEvent eventOnDisableObject;


	void Awake ()
	{
		if (addObjectToMainSystem) {
			if (addObjectOnAwake) {
				addObjectToSystem ();
			}
		}
	}

	void Start ()
	{
		if (addObjectToMainSystem) {
			if (addObjectOnAwake) {
				return;
			}

			if (addObjectOnStartWithDelay) {
				StartCoroutine (addObjectToSystemCoroutine ());
			} else {
				addObjectToSystem ();
			}
		}
	}


	IEnumerator addObjectToSystemCoroutine ()
	{
		WaitForSeconds delay = new WaitForSeconds (delayToAddObjectOnStart);

		yield return delay;

		addObjectToSystem ();
	}

	void addObjectToSystem ()
	{
		if (objectInfoSentToMainSystem) {
			return;
		}

		mainEnableOrDisableObjectsOnDistanceSystemLocated = mainEnableOrDisableObjectsOnDistanceSystem != null;

		if (!mainEnableOrDisableObjectsOnDistanceSystemLocated) {
			mainEnableOrDisableObjectsOnDistanceSystem = enableOrDisableObjectsOnDistanceSystem.Instance;

			mainEnableOrDisableObjectsOnDistanceSystemLocated = mainEnableOrDisableObjectsOnDistanceSystem != null;
		}

		if (!mainEnableOrDisableObjectsOnDistanceSystemLocated) {
			GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (enableOrDisableObjectsOnDistanceSystem.getMainManagerName (), typeof(enableOrDisableObjectsOnDistanceSystem), true);

			mainEnableOrDisableObjectsOnDistanceSystem = enableOrDisableObjectsOnDistanceSystem.Instance;

			mainEnableOrDisableObjectsOnDistanceSystemLocated = (mainEnableOrDisableObjectsOnDistanceSystem != null);
		}

		if (!mainEnableOrDisableObjectsOnDistanceSystemLocated) {
			mainEnableOrDisableObjectsOnDistanceSystem = GKC_Utils.findObjectByComponent<enableOrDisableObjectsOnDistanceSystem> ();

			mainEnableOrDisableObjectsOnDistanceSystemLocated = mainEnableOrDisableObjectsOnDistanceSystem != null;
		} 

		if (mainEnableOrDisableObjectsOnDistanceSystemLocated) {
			if (mainTransform == null) {
				mainTransform = transform;
			}

			mainEnableOrDisableObjectsOnDistanceSystem.addObject (this);

			objectInfoSentToMainSystem = true;
		}
	}

	public void removeObjectFromSystem ()
	{
		if (mainEnableOrDisableObjectsOnDistanceSystemLocated) {
			mainEnableOrDisableObjectsOnDistanceSystem.removeObject (mainTransform);
		}
	}

	public void setActiveState (bool state)
	{
		if (objectActive == state) {
			return;
		}

		objectActive = state;

		if (objectActive) {
			eventOnEnableObject.Invoke ();
		} else {
			eventOnDisableObject.Invoke ();
		}
	}

	public void setAddObjectToMainSystemState (bool state)
	{
		addObjectToMainSystem = state;

		if (!Application.isPlaying) {
			if (addObjectToMainSystem) {
				addObjectToSystem ();
			} else {
				removeObjectFromSystem ();

				setActiveState (false);
			}
		}
	}

	public bool isSetActiveStateOnMainTransformEnabled ()
	{
		return setActiveStateOnMainTransformEnabled;
    }
}
