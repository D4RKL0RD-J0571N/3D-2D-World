using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class hoverBoardWayPoints : MonoBehaviour
{
	public float movementSpeed;
	public bool moveInOneDirection;

	public float forceAtEnd;
	public float railsOffset;
	public float extraScale;
	public float triggerRadius;

	public string vehicleTag = "vehicle";

	public bool modifyMovementSpeedEnabled = true;
	public float maxMovementSpeed = 2;
	public float minMovementSpeed = 0.1f;
	public float modifyMovementSpeed = 5;

	public List<wayPointsInfo> wayPoints = new List<wayPointsInfo> ();

	public bool allowMovementOnBothDirections;

	public bool showGizmo;
	public Color gizmoLabelColor = Color.black;
	public float gizmoRadius;
	public bool useHandleForVertex;
	public float handleRadius;
	public Color handleGizmoColor;

	public bool showVertexHandles;

	public GameObject wayPointElement;

	int i;

	vehicleHUDManager currentVehicleHUDManager;

	vehicleController currentVehicleController;


	void OnTriggerEnter (Collider col)
	{
		bool checkVehicleResult = false;

		if (col.gameObject.CompareTag (vehicleTag)) {
			checkVehicleResult = true;
		}

		if (!checkVehicleResult) {
			if (applyDamage.isVehicle (col.gameObject)) {
				checkVehicleResult = true;
			}
		}

		if (checkVehicleResult) {
			currentVehicleController = col.gameObject.GetComponent<vehicleController> ();

			if (currentVehicleController == null) {
				GameObject newVehicleObject = applyDamage.getVehicle (col.gameObject);

				if (newVehicleObject != null) {
					currentVehicleController = newVehicleObject.GetComponent<vehicleController> ();
				}
			}

			if (currentVehicleController != null) {
				if (currentVehicleController.canUseHoverboardWaypoints ()) {

					currentVehicleHUDManager = currentVehicleController.GetComponent<vehicleHUDManager> ();

					if (currentVehicleHUDManager.isVehicleBeingDriven ()) {

						bool canActivateWaypoint = true;

						if (currentVehicleController.isUsingHoverBoardWaypoint ()) {
							canActivateWaypoint = false;
						}

						float lastTimeReleasedFromWaypoint = currentVehicleController.getLastTimeReleasedFromWaypoint ();

						if (lastTimeReleasedFromWaypoint > 0 && Time.time < lastTimeReleasedFromWaypoint + 0.7f) {
							canActivateWaypoint = false;
						}

						if (canActivateWaypoint) {
							currentVehicleController.receiveWayPoints (this);

							currentVehicleController.pickOrReleaseHoverboardVehicle (true, false);
						}
					}
				}
			}
		}
	}

	//	void OnTriggerExit (Collider col)
	//	{
	//		if (col.gameObject.CompareTag ("Player") && inside && !moving) {
	//			pickOrReleaseVehicle (false, false);
	//		}
	//	}
		
	//EDITOR FUNCTIONS
	public void addNewWayPoint ()
	{
		Vector3 newPosition = transform.position;

		if (wayPoints.Count > 0) {
			newPosition = wayPoints [wayPoints.Count - 1].wayPoint.position + wayPoints [wayPoints.Count - 1].wayPoint.forward;
		}

		GameObject newWayPoint = (GameObject)Instantiate (wayPointElement, newPosition, Quaternion.identity);

		newWayPoint.transform.SetParent (transform);

		newWayPoint.name = (wayPoints.Count + 1).ToString ("000");

		wayPointsInfo newWayPointInfo = new wayPointsInfo ();

		newWayPointInfo.Name = newWayPoint.name;

		newWayPointInfo.wayPoint = newWayPoint.transform;

		newWayPointInfo.direction = newWayPoint.transform.GetChild (0);

		newWayPointInfo.trigger = newWayPoint.GetComponentInChildren<CapsuleCollider> ();

		newWayPointInfo.railMesh = newWayPoint.GetComponentInChildren<MeshRenderer> ().gameObject;

		wayPoints.Add (newWayPointInfo);

		updateComponent ();
	}

	public void removeWaypoint (int index)
	{
		wayPointsInfo currentWaypointInfo = wayPoints [index];

		DestroyImmediate (currentWaypointInfo.wayPoint.gameObject);

		wayPoints.RemoveAt (index);

		updateComponent ();
	}

	public void addNewWayPointAtIndex (int index)
	{
		Vector3 newPosition = transform.position;

		if (wayPoints.Count > 0) {
			newPosition = wayPoints [index].wayPoint.position + wayPoints [index].wayPoint.forward;
		}

		GameObject newWayPoint = (GameObject)Instantiate (wayPointElement, newPosition, Quaternion.identity);

		newWayPoint.transform.SetParent (transform);

		newWayPoint.name = (index + 1).ToString ("000");

		wayPointsInfo newWayPointInfo = new wayPointsInfo ();

		newWayPointInfo.Name = newWayPoint.name;

		newWayPointInfo.wayPoint = newWayPoint.transform;

		newWayPointInfo.direction = newWayPoint.transform.GetChild (0);

		newWayPointInfo.trigger = newWayPoint.GetComponentInChildren<CapsuleCollider> ();

		newWayPointInfo.railMesh = newWayPoint.GetComponentInChildren<MeshRenderer> ().gameObject;

		wayPoints.Insert (index, newWayPointInfo);

		renameAllWaypoints ();

		updateComponent ();
	}

	public void selectObjectByIndex (int index)
	{
		wayPointsInfo currentWayPointsInfo = wayPoints [index];

		if (currentWayPointsInfo.wayPoint != null) {
			GKC_Utils.setActiveGameObjectInEditor (currentWayPointsInfo.wayPoint.gameObject);
		}
	}

	public void renameAllWaypoints ()
	{
		for (int i = 0; i < wayPoints.Count; i++) {
			if (wayPoints [i].wayPoint != null) {
				wayPoints [i].Name = (i + 1).ToString ();

				wayPoints [i].wayPoint.name = (i + 1).ToString ("000");

				wayPoints [i].wayPoint.SetSiblingIndex (i);
			}
		}

		updateComponent ();
	}

	public void updateComponent ()
	{
		GKC_Utils.updateComponent (this);

		GKC_Utils.updateDirtyScene ("Update Hoverboard Waypoints Info", gameObject);
	}

	void OnDrawGizmos ()
	{
		if (!showGizmo) {
			return;
		}

		if (GKC_Utils.isCurrentSelectionActiveGameObject (gameObject)) {
			DrawGizmos ();
		}
	}

	void OnDrawGizmosSelected ()
	{
		DrawGizmos ();
	}

	//draw the pivot and the final positions of every door
	void DrawGizmos ()
	{
		//&& !Application.isPlaying
		if (showGizmo) {
			for (i = 0; i < wayPoints.Count; i++) {
				if (wayPoints [i].wayPoint != null && wayPoints [i].direction != null) {
					
					Gizmos.color = Color.yellow;
					Gizmos.DrawSphere (wayPoints [i].wayPoint.position, gizmoRadius);

					if (i + 1 < wayPoints.Count) {
						Gizmos.color = Color.white;

						Gizmos.DrawLine (wayPoints [i].wayPoint.position, wayPoints [i + 1].wayPoint.position);

						wayPoints [i].direction.LookAt (wayPoints [i + 1].wayPoint.position);
						float scaleZ = GKC_Utils.distance (wayPoints [i].wayPoint.position, wayPoints [i + 1].wayPoint.position);
						wayPoints [i].direction.localScale = new Vector3 (1, 1, scaleZ + scaleZ * extraScale);

						Gizmos.color = Color.green;
						Gizmos.DrawLine (wayPoints [i].wayPoint.position, wayPoints [i].wayPoint.position + wayPoints [i].direction.forward);
					}

					if (i == wayPoints.Count - 1 && (i - 1) >= 0 && i != 0) {
						wayPoints [i].direction.rotation = Quaternion.LookRotation (wayPoints [i].wayPoint.position - wayPoints [i - 1].wayPoint.position);

						Gizmos.color = Color.green;

						Gizmos.DrawLine (wayPoints [i].direction.position, wayPoints [i].direction.position + wayPoints [i].direction.forward);
					}

					if (i == wayPoints.Count - 1) {
						wayPoints [i].direction.localScale = Vector3.one;
					}

					wayPoints [i].trigger.radius = triggerRadius;

					wayPoints [i].railMesh.transform.localPosition = new Vector3 (wayPoints [i].railMesh.transform.localPosition.x, railsOffset, wayPoints [i].railMesh.transform.localPosition.z);
				}
			}
		}
	}

	[System.Serializable]
	public class wayPointsInfo
	{
		public string Name;
		public Transform wayPoint;
		public Transform direction;
		public CapsuleCollider trigger;
		public GameObject railMesh;
	}
}