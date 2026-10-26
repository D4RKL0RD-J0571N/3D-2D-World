using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class getInventoryObjectSystem : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public string inventoryObjectName;
	public int objectAmount;

	public bool onlyRefillCertainAmountOfInventoryObjectAmount;

	[Space]

	public bool giveInventoryObjectList;
	public List<inventoryElementInfo> inventoryElementInfoList = new List<inventoryElementInfo> ();

	[Space]
	[Header ("Other Settings")]
	[Space]

	public bool spawnObjectIfNotEnoughSpaceOnInventory = true;

	public float maxRadiusToInstantiate;
	public float forceAmount;
	public float forceRadius;
	public ForceMode inventoryObjectForceMode;

	public bool spawnAllInventoryObjects;

	public bool disableGetObjectAfterObtained;

	[Space]
	[Header ("Debug")]
	[Space]

	public bool objectObtained;

	[Space]
	[Header ("Events Settings")]
	[Space]

	public bool useEventIfObjectStored;
	public UnityEvent eventIfObjectStored;

	[Space]
	[Header ("Player Manual Settings")]
	[Space]

	public bool setPlayerManually;
	public bool sendAlwaysToMainPlayer;
	public bool searchPlayerByTagIfNotAssigned = true;
	public GameObject playerToConfigure;

	[Space]
	[Header ("Components")]
	[Space]

	public GameObject currentPlayer;

	public Transform positionToPlaceInventoryObject;


	public void removeCurrentPlayer (GameObject player)
	{
		if (currentPlayer == player) {
			currentPlayer = null;
		}
	}

	public void getCurrentPlayer (GameObject player)
	{
		currentPlayer = player;
	}

	public void giveObjectToPlayer ()
	{
		if (disableGetObjectAfterObtained) {
			if (objectObtained) {
				return;
			}
		}

		if (setPlayerManually) {
			currentPlayer = playerToConfigure;
		}

		if (sendAlwaysToMainPlayer) {
			currentPlayer = GKC_Utils.findMainPlayerOnScene ();
		}

		if (currentPlayer == null) {
			bool playerNotFoundResult = true;

			if (searchPlayerByTagIfNotAssigned) {

				currentPlayer = GKC_Utils.findMainPlayerOnScene ();

				if (currentPlayer != null) {
					playerNotFoundResult = false;
				}
			}

			if (playerNotFoundResult) {
				print ("WARNING: Make sure to configure a player or configure the events to send the player gameObject, so he can receive the object");
			
				return;
			}
		}

		if (inventoryObjectName.Equals ("")) {
			print ("WARNING: Make sure to configure an inventory object name in order to find its info on the inventory list manager");
		
			return;
		}

		if (positionToPlaceInventoryObject == null) {
			positionToPlaceInventoryObject = transform;
		}
			
		if (giveInventoryObjectList) {
			for (int i = 0; i < inventoryElementInfoList.Count; i++) {
				if (applyDamage.giveInventoryObjectToCharacter (currentPlayer, 
					    inventoryElementInfoList [i].Name,
					    inventoryElementInfoList [i].inventoryObjectAmount, 
					    positionToPlaceInventoryObject, forceAmount, maxRadiusToInstantiate, 
					    inventoryObjectForceMode, forceRadius, spawnAllInventoryObjects, 
					    inventoryElementInfoList [i].onlyRefillCertainAmountOfInventoryObjectAmount,
					    spawnObjectIfNotEnoughSpaceOnInventory)) {

					if (inventoryElementInfoList [i].useEventIfObjectStored) {
						inventoryElementInfoList [i].eventIfObjectStored.Invoke ();
					}			
				}
			}
		} else {
			if (applyDamage.giveInventoryObjectToCharacter (currentPlayer, inventoryObjectName, 
				    objectAmount, positionToPlaceInventoryObject, 
				    forceAmount, maxRadiusToInstantiate, 
				    inventoryObjectForceMode, forceRadius, spawnAllInventoryObjects, 
				    onlyRefillCertainAmountOfInventoryObjectAmount,
				    spawnObjectIfNotEnoughSpaceOnInventory)) {

				if (useEventIfObjectStored) {
					eventIfObjectStored.Invoke ();
				}
			}
		}

		objectObtained = true;
	}

	[System.Serializable]
	public class inventoryElementInfo
	{
		public string Name;
		public int inventoryObjectAmount;

		public bool onlyRefillCertainAmountOfInventoryObjectAmount;

		[Space]

		public bool useEventIfObjectStored;
		public UnityEvent eventIfObjectStored;
	}
}
