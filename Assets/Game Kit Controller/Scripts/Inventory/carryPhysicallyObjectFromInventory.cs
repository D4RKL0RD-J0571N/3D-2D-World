using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class carryPhysicallyObjectFromInventory : objectOnInventory
{
    [Header ("Custom Settings")]
    [Space]

    public bool disableInteractionPanelWhileGrabbed;

    [Space]
    [Header ("Debug")]
    [Space]

    public GameObject currentPlayer;

    [Space]
    [Header ("Components")]
    [Space]

    public grabPhysicalObjectSystem mainGrabPhysicalObjectSystem;


    public void setCurrentPlayer (GameObject newPlayer)
    {
        currentPlayer = newPlayer;
    }

    public void checkObjectStateOnGrab ()
    {
        if (disableInteractionPanelWhileGrabbed) {
            deviceStringAction currentDeviceStringAction = GetComponentInChildren<deviceStringAction> ();

            if (currentDeviceStringAction != null) {
                currentDeviceStringAction.setShowIconState (false);
            }

            if (currentPlayer != null) {
                usingDevicesSystem currentUsingDevicesSystem = currentPlayer.GetComponent<usingDevicesSystem> ();

                if (currentUsingDevicesSystem != null) {
                    currentUsingDevicesSystem.updateClosestDeviceList ();
                }
            }
        } 
    }

    public void checkObjectStateOnDrop ()
    {
        if (disableInteractionPanelWhileGrabbed) {
            deviceStringAction currentDeviceStringAction = GetComponentInChildren<deviceStringAction> ();

            if (currentDeviceStringAction != null) {
                currentDeviceStringAction.setShowIconState (true);
            }

            if (currentPlayer != null) {
                usingDevicesSystem currentUsingDevicesSystem = currentPlayer.GetComponent<usingDevicesSystem> ();

                if (currentUsingDevicesSystem != null) {
                    currentUsingDevicesSystem.updateClosestDeviceList ();
                }
            }
        }
    }

    public override void carryPhysicalObjectFromInventory (GameObject newPlayer)
    {
        //check if player is already carrying a different physical object, to remove it properly
        //check also if that object is an inventory object, to send it back to the inventory

        currentPlayer = newPlayer;

        mainGrabPhysicalObjectSystem.setCurrentPlayer (currentPlayer);

        grabObjects currentGrabObjects = currentPlayer.GetComponent<grabObjects> ();

        if (currentGrabObjects != null) {
            bool isCarryingPhysicalObject = currentGrabObjects.isCarryingPhysicalObject ();

            bool isSendGrabbedObjectToInventoryIfGrabbingNewOneEnabled = false;

            GameObject currentObjectGrabbed = null;

            playerComponentsManager currentPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

            if (currentPlayerComponentsManager != null) {
                isSendGrabbedObjectToInventoryIfGrabbingNewOneEnabled = currentPlayerComponentsManager.getInventoryManager ().isSendGrabbedObjectToInventoryIfGrabbingNewOneEnabled ();
            }

            if (isSendGrabbedObjectToInventoryIfGrabbingNewOneEnabled) {
                currentObjectGrabbed = currentGrabObjects.getCurrentPhysicalObjectGrabbed ();

                if (currentObjectGrabbed != null) {
                    if (currentObjectGrabbed.GetComponentInChildren<objectOnInventory> () == null) {
                        currentObjectGrabbed = null;
                    }
                }
            }

            if (isCarryingPhysicalObject) {
                currentGrabObjects.checkIfDropObjectIfPhysical ();

                currentGrabObjects.grabPhysicalObjectExternally (gameObject);
            } else {
                currentGrabObjects.getClosestPhysicalObjectToGrab ();

                currentGrabObjects.grabObject ();
            }

            if (isSendGrabbedObjectToInventoryIfGrabbingNewOneEnabled) {
                if (currentObjectGrabbed != null) {
                    pickUpObject currentPickupObject = currentObjectGrabbed.GetComponent<pickUpObject> ();

                    if (currentPickupObject != null) {
                        currentPickupObject.pickObjectAfterXWait (currentPlayer);
                    }
                }
            }
        }
    }
}
