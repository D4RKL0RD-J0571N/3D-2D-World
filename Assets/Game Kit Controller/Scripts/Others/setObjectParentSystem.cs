using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class setObjectParentSystem : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public Transform parentTransform;

	public bool resetChildLocalPosition;

	public bool setParentOnStart;

	public bool setNullParent;

	[Space]
	[Header ("Child List Settings")]
	[Space]

	public List<Transform> childList = new List<Transform> ();


	void Start ()
	{
		if (setParentOnStart) {
			setObjectsParent ();
		}
	}

	public void setObjectsParent ()
	{
		int childListCount = childList.Count;

        if (parentTransform != null) {
			for (int i = 0; i < childListCount; i++) {
				childList [i].SetParent (parentTransform);

				if (resetChildLocalPosition) {
					childList [i].localPosition = Vector2.zero;
				}
			}
		} else {
			if (setNullParent) {
                for (int i = 0; i < childListCount; i++) {
                    childList [i].SetParent (null);
                }
            }
        }
	}

	public void setParentTransform (Transform newTransform)
	{
		parentTransform = newTransform;
	}

	public Transform getParentTransform ()
	{
		return parentTransform;
	}

	public void setParentOnStartState (bool state)
	{
		setParentOnStart = state;
	}

	public void setParentOnStartStateFromEditor (bool state)
	{
		setParentOnStartState (state);

		updateComponent ();
	}

	void updateComponent ()
	{
		GKC_Utils.updateComponent (this);

		GKC_Utils.updateDirtyScene ("Update Set Parent Info " + gameObject.name, gameObject);
	}
}
