using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class autoGrabLedgeTriggerSystem : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public string tagToCheck;
	public bool ledgeZoneActive = true;

	public Transform ledgeTransform;

	GameObject currentPlayer;
	climbLedgeSystem climbLedgeManager;


	void OnTriggerEnter (Collider col)
	{
		checkTriggerInfo (col, true);
	}

//	void OnTriggerExit (Collider col)
//	{
//		checkTriggerInfo (col, false);
//	}

	public void checkTriggerInfo (Collider col, bool isEnter)
	{
		if (!ledgeZoneActive) {
			return;
		}

		if (!col.gameObject.CompareTag (tagToCheck)) {
			return;
		}

		if (isEnter) {
			currentPlayer = col.gameObject;

			playerComponentsManager mainPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

			if (mainPlayerComponentsManager != null) {
				climbLedgeManager = mainPlayerComponentsManager.getClimbLedgeSystem ();
			}

			if (climbLedgeManager == null) {
				return;
			}

			climbLedgeManager.activateGrabToSurfaceActionExternally (true, ledgeTransform);
		} else {
			currentPlayer = col.gameObject;

			playerComponentsManager mainPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

			if (mainPlayerComponentsManager != null) {
				climbLedgeManager = mainPlayerComponentsManager.getClimbLedgeSystem ();
			}

			if (climbLedgeManager == null) {
				return;
			}

			climbLedgeManager.activateGrabToSurfaceActionExternally (false, null);
		}
	}
}

