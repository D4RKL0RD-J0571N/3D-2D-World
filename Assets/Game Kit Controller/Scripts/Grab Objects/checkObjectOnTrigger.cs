using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class checkObjectOnTrigger : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool putObjectSystemEnabled = true;

	public bool useCertainObjectToPlace;
	public GameObject certainObjectToPlace;

	public string objectNameToPlace;

	public bool useObjectNameListToPlace;

	public List<string> objectNameListToPlace = new List<string> ();

	[Space]
	[Header ("Debug")]
	[Space]

	public bool anyObjectPlaced;

	public List<GameObject> objectsPlacedList = new List<GameObject> ();

	[Space]
	[Header ("Events Settings")]
	[Space]

	public bool useEventOnObjectPlaced;

	public UnityEvent objectPlacedEvent;

	public bool useEventOnObjectRemoved;
	public UnityEvent objectRemovedEvent;

	objectToPlaceSystem currentObjectToPlaceSystem;



	public void checkObjectOnTriggerEnter (GameObject newObject)
	{
		if (checkIfObjectCanBePlaced (newObject)) {
			if (!objectsPlacedList.Contains (newObject)) {
				objectsPlacedList.Add (newObject);

				if (useEventOnObjectPlaced) {
					objectPlacedEvent.Invoke ();
				}

				anyObjectPlaced = true;
			}
		}
	}

	public void checkObjectOnTriggerExit (GameObject newObject)
	{
		if (checkIfObjectCanBePlaced (newObject)) {
			if (objectsPlacedList.Contains (newObject)) {
				objectsPlacedList.Remove (newObject);

				if (useEventOnObjectRemoved) {
					objectRemovedEvent.Invoke ();
				}

				if (objectsPlacedList.Count == 0) {
					anyObjectPlaced = false;
				}
			}
		}
	}

	bool checkIfObjectCanBePlaced (GameObject objectToCheck)
	{
		if (useCertainObjectToPlace) {
			if (objectToCheck == certainObjectToPlace || objectToCheck.transform.IsChildOf (certainObjectToPlace.transform)) {
				return true;
			}
		} else {
			currentObjectToPlaceSystem = objectToCheck.GetComponent<objectToPlaceSystem> ();

			if (currentObjectToPlaceSystem != null) {
				if (useObjectNameListToPlace) {
					if (objectNameListToPlace.Contains (currentObjectToPlaceSystem.getObjectName ())) {
						return true;
					}
				} else {
					if (objectNameToPlace == currentObjectToPlaceSystem.getObjectName ()) {
						return true;
					}
				}
			}
		}

		return false;
	}
}
