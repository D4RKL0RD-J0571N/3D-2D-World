using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class checkCharactersParentOnDestroy : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool checkCharactersParentOnDestroyEnabled = true;

    public Transform mainParent;


    void OnDestroy ()
    {
        if (checkCharactersParentOnDestroyEnabled) {
            if (mainParent == null) {
                mainParent = transform;
            }

            Component [] playerControllerList = mainParent.GetComponentsInChildren (typeof (playerController));

            foreach (playerController currentPlayerController in playerControllerList) {
                currentPlayerController.setPlayerAndCameraAndFBAPivotTransformParent (null);
            }
        }
    }
}
