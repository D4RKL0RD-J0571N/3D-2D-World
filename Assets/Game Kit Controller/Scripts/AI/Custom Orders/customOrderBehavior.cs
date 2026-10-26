using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class customOrderBehavior : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool orderEnabled = true;

	[Space]
	[Header ("Debug")]
	[Space]

	public bool showDebugPrint;
    public Transform currentTarget;

	[Space]
	[Header ("Event Settings")]
	[Space]

	public bool useEventToSendTarget;
	public eventParameters.eventToCallWithTransform eventToSendTarget;

    public virtual void activateOrder (Transform character)
	{
	
	}

	public virtual void activateOrder (Transform character, Transform orderOwner)
	{

	}

	public virtual Transform getCustomTarget (Transform character, Transform orderOwner)
	{


		return null;
	}

    public virtual void setCustomTarget (Transform newTarget)
    {
		currentTarget = newTarget;

		if (useEventToSendTarget) {
			if (currentTarget != null) {
				eventToSendTarget.Invoke (currentTarget);
			}
		}
    }

    public virtual bool checkConditionToShowOrderButton (Transform character)
	{

		return false;
	}
}