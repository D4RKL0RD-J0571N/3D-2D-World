using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class oneWayPlatformSystem : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public List<string> tagToCheck = new List<string> ();

	public bool ignoreCollisionOnTop;
	public bool ignoreCollisionOnBottom;

	[Space]
	[Header ("Conditions Settings")]
	[Space]

	public bool playerNeedsToCrouchToIgnore;

	public bool useOnlyCrouchToIgnore;

	public bool useOnlyMoveInputDownToIgnore;

	[Space]
	[Header ("Debug")]
	[Space]

	public bool playerFound;

	public bool showDebugPrint;

	public List<passengersInfo> passengersInfoList = new List<passengersInfo> ();

	[Space]
	[Header ("Components")]
	[Space]

	public Collider platformCollider;
	public Transform platformTransform;

	void Update ()
	{
		if (playerFound) {
			int passengersInfoListCount = passengersInfoList.Count;

			for (int i = 0; i < passengersInfoListCount; i++) {
				passengersInfo currentPassengersInfo = passengersInfoList [i];

				if (currentPassengersInfo.ignoringPlayerColliderFromBottom) {
					if (platformTransform.transform.position.y < currentPassengersInfo.playerTransform.position.y) {
						Physics.IgnoreCollision (platformCollider, currentPassengersInfo.playerCollider, false);

						currentPassengersInfo.ignoringPlayerColliderFromBottom = false;

						if (showDebugPrint) {
							print ("ignoring player collider ended");
						}
					}
				}

				if (ignoreCollisionOnTop) {
					if (!currentPassengersInfo.ignoringPlayerColliderFromTop) {
						bool ignoreCollisionResult = false;

						bool characterIsCrouching = currentPassengersInfo.playerControllerManager.isCrouching ();

						if (currentPassengersInfo.playerControllerManager.getAxisValues ().y < 0) {
							if (useOnlyMoveInputDownToIgnore) {
								ignoreCollisionResult = true;
							} else {
								if (!playerNeedsToCrouchToIgnore || characterIsCrouching) {
									ignoreCollisionResult = true;
								}
							}
						}

						if (useOnlyCrouchToIgnore && characterIsCrouching) {
							ignoreCollisionResult = true;
						}

						if (ignoreCollisionResult) {
							Physics.IgnoreCollision (platformCollider, currentPassengersInfo.playerCollider, true);

							currentPassengersInfo.playerControllerManager.setcheckOnGroundStatePausedState (true);

							if (showDebugPrint) {
								print ("condition to ignore collision from the top activated");
							}

							currentPassengersInfo.ignoringPlayerColliderFromTop = true;
						}
					}
				}
			}
		}
	}

	public void OnTriggerEnter (Collider col)
	{
		checkTriggerInfo (col, true);
	}

	public void OnTriggerExit (Collider col)
	{
		checkTriggerInfo (col, false);
	}

	public void checkTriggerInfo (Collider col, bool isEnter)
	{
		if (tagToCheck.Contains (col.tag)) {
			if (isEnter) {
				addPassenger (col.gameObject.transform);

				passengersInfo currentPassengersInfo = passengersInfoList [passengersInfoList.Count - 1];

				if (ignoreCollisionOnBottom) {
					if (platformTransform.transform.position.y > currentPassengersInfo.playerTransform.position.y) {
						Physics.IgnoreCollision (platformCollider, currentPassengersInfo.playerCollider, true);
						currentPassengersInfo.ignoringPlayerColliderFromBottom = true;
					}
				}

				if (showDebugPrint) {
					print ("player added on the list");
				}
			} else {
				for (int i = 0; i < passengersInfoList.Count; i++) {
					passengersInfo currentPassengersInfo = passengersInfoList [i];

					if (currentPassengersInfo.playerTransform == col.transform) {
						
						Physics.IgnoreCollision (platformCollider, currentPassengersInfo.playerCollider, false);

						if (ignoreCollisionOnTop) {
							currentPassengersInfo.playerControllerManager.setcheckOnGroundStatePausedState (false);
						}

						removePassenger (currentPassengersInfo.playerTransform);

						if (showDebugPrint) {
							print ("player removed");
						}

						return;
					}
				}
			}
		}
	}

	public void addPassenger (Transform newPassenger)
	{
		bool passengerFound = false;

		for (int i = 0; i < passengersInfoList.Count; i++) {
			if (passengersInfoList [i].playerTransform == newPassenger && !passengerFound) {
				passengerFound = true;
			}
		}

		if (!passengerFound) {
			passengersInfo newPassengersInfo = new passengersInfo ();
			newPassengersInfo.playerTransform = newPassenger;
			newPassengersInfo.playerControllerManager = newPassenger.GetComponent<playerController> ();
			newPassengersInfo.playerCollider = newPassengersInfo.playerControllerManager.getMainCollider ();

			passengersInfoList.Add (newPassengersInfo);

			playerFound = true;
		}
	}

	void removePassenger (Transform newPassenger)
	{
		for (int i = 0; i < passengersInfoList.Count; i++) {
			if (passengersInfoList [i].playerTransform == newPassenger) {
				passengersInfoList.RemoveAt (i);
			}
		}

		if (passengersInfoList.Count == 0) {
			playerFound = false;
		}
	}

	[System.Serializable]
	public class passengersInfo
	{
		public Transform playerTransform;
		public playerController playerControllerManager;
		public Collider playerCollider;

		public bool ignoringPlayerColliderFromTop;
		public bool ignoringPlayerColliderFromBottom;
	}
}
