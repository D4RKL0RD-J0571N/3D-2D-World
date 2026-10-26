using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class objectToUseMatchTargetSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool matchTargetSystemEnabled = true;

    [Space]

    public bool checkObjectTag;

    public List<string> tagsToLocate = new List<string> ();

    [Space]

    public bool checkObjectLayer;

    public LayerMask layerToCheckTargets;

    [Space]
    [Header ("Components")]
    [Space]

    public Transform mainCharacterTransform;


    List<matchPlayerToTargetSystem> currentMatchPlayerToTargetSystemList = new List<matchPlayerToTargetSystem> ();


    void OnTriggerEnter (Collider other)
    {
        checkTriggerInfo (other.gameObject, true);
    }

    void OnTriggerExit (Collider other)
    {
        checkTriggerInfo (other.gameObject, false);
    }

    void checkTriggerInfo (GameObject objectToCheck, bool isEnter)
    {
        if (!matchTargetSystemEnabled) {
            return;
        }

        bool checkObjectResult = false;

        if (checkObjectTag) {
            if (tagsToLocate.Contains (objectToCheck.tag)) {
                checkObjectResult = true;
            }
        }

        if (checkObjectLayer) {
            if (canCheckObject (objectToCheck.layer)) {
                checkObjectResult = true;
            }
        }

        if (checkObjectResult) {
            playerComponentsManager currentPlayerComponentsManager = objectToCheck.GetComponent<playerComponentsManager> ();

            if (currentPlayerComponentsManager != null) {
                matchPlayerToTargetSystem currentMatchPlayerToTargetSystem = currentPlayerComponentsManager.getMatchPlayerToTargetSystem ();

                if (currentMatchPlayerToTargetSystem != null) {
                    if (isEnter) {
                        if (!currentMatchPlayerToTargetSystemList.Contains (currentMatchPlayerToTargetSystem)) {
                            currentMatchPlayerToTargetSystem.addCharacterAround (mainCharacterTransform);

                            currentMatchPlayerToTargetSystemList.Add (currentMatchPlayerToTargetSystem);
                        }
                    } else {
                        if (currentMatchPlayerToTargetSystemList.Contains (currentMatchPlayerToTargetSystem)) {
                            currentMatchPlayerToTargetSystem.removeCharacterAround (mainCharacterTransform);

                            currentMatchPlayerToTargetSystemList.Remove (currentMatchPlayerToTargetSystem);
                        }
                    }
                }
            }
        }
    }

    public void removeAllMatchTargetList ()
    {
        if (!matchTargetSystemEnabled) {
            return;
        }

        for (int i = 0; i < currentMatchPlayerToTargetSystemList.Count; i++) {
            currentMatchPlayerToTargetSystemList [i].removeCharacterAround (mainCharacterTransform);
        }

        currentMatchPlayerToTargetSystemList.Clear ();
    }

    bool canCheckObject (int suspectLayer)
    {
        if ((1 << suspectLayer & layerToCheckTargets.value) == 1 << suspectLayer) {
            return true;
        }

        return false;
    }
}
