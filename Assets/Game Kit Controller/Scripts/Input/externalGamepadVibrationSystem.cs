using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class externalGamepadVibrationSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool externalGamepadVibrationEnabled = true;

    public string vibrationName;
    public int vibrationID = -1;

    public bool setIntensityBasedOnDistance;
    public float maxDistanceToFindCharacters;
    public LayerMask layer;

    [Space]

    public bool setPlayerManually;
    public GameObject currentPlayer;


    bool playerAssignedManually;

    float distancePercentage;


    public void activateGamepadVibration ()
    {
        if (!externalGamepadVibrationEnabled) {
            return;
        }

        if (setPlayerManually) {
            if (currentPlayer != null) {
                activateGamepadVibration (currentPlayer);
            }
        } else {
            List<Collider> colliders = new List<Collider> ();

            colliders.AddRange (Physics.OverlapSphere (transform.position, maxDistanceToFindCharacters, layer));

            int collidersCount = colliders.Count;

            for (int i = 0; i < collidersCount; i++) {
                Collider hit = colliders [i];

                if (hit != null && !hit.isTrigger) {
                    activateGamepadVibration (hit.gameObject);
                }
            }
        }
    }

    public void activateGamepadVibration (GameObject objectToCall)
    {
        if (!externalGamepadVibrationEnabled) {
            return;
        }

        distancePercentage = 0;

        if (setIntensityBasedOnDistance) {
            float currentDistance = GKC_Utils.distance (objectToCall.transform.position, transform.position);

            if (currentDistance <= maxDistanceToFindCharacters) {
                distancePercentage = currentDistance / maxDistanceToFindCharacters;
                distancePercentage = 1 - distancePercentage;
            } else {
                return;
            }
        }

        playerComponentsManager mainPlayerComponentsManager = objectToCall.GetComponent<playerComponentsManager> ();

        if (mainPlayerComponentsManager != null) {
            simpleGamepadVibrationHelper currenSimpleGamepadVibrationHelper =
                mainPlayerComponentsManager.getPlayerInputManager ().getMainSimpleGamepadVibrationHelper ();

            if (currenSimpleGamepadVibrationHelper != null) {
                if (distancePercentage > 0) {
                    if (vibrationID > -1) {
                        currenSimpleGamepadVibrationHelper.setGamepadVibrationByID (vibrationID, distancePercentage);
                    } else {
                        currenSimpleGamepadVibrationHelper.setGamepadVibrationByName (vibrationName, distancePercentage);
                    }
                } else {
                    if (vibrationID > -1) {
                        currenSimpleGamepadVibrationHelper.setGamepadVibrationByID (vibrationID);
                    } else {
                        currenSimpleGamepadVibrationHelper.setGamepadVibrationByName (vibrationName);
                    }
                }
            }
        }
    }
}
