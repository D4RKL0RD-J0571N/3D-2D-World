using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Events;

public class airAttackToLandSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public float downForceAmount = 5;

    public LayerMask raycastLayermask;

    public float minDistanceToDetectGround;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool attackLandingInProcess;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public UnityEvent eventOnStartAttack;
    public UnityEvent eventOnEndAttack;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController mainPlayerController;

    public Transform playerTransform;


    Coroutine attackCoroutine;

    public void activateAirAttackToLand ()
    {
        stopActivateAirAttackToLandCoroutine ();

        attackCoroutine = StartCoroutine (activateAirAttackToLandCoroutine ());
    }

    void stopActivateAirAttackToLandCoroutine ()
    {
        if (attackCoroutine != null) {
            StopCoroutine (attackCoroutine);
        }

        attackLandingInProcess = false;
    }

    IEnumerator activateAirAttackToLandCoroutine ()
    {
        attackLandingInProcess = true;

        eventOnStartAttack.Invoke ();

        bool targetReached = false;

        while (!targetReached) {
            Vector3 raycastPosition = playerTransform.position + playerTransform.up * 0.2f;

            if (Physics.Raycast (raycastPosition, -playerTransform.up, minDistanceToDetectGround, raycastLayermask)) {
                targetReached = true;
            } else {
                mainPlayerController.addExternalForce (-playerTransform.up * downForceAmount);
            }

            yield return null;
        }

        eventOnEndAttack.Invoke ();

        attackLandingInProcess = false;

        if (mainPlayerController.getCurrentSurfaceBelowPlayer () != null || mainPlayerController.checkIfPlayerOnGroundWithRaycast ()) {

            mainPlayerController.setPlayerOnGroundState (true);

            mainPlayerController.setOnGroundAnimatorIDValue (true);
        }
    }
}