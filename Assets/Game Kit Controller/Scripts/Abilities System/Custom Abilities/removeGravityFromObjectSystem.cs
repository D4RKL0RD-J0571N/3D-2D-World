using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class removeGravityFromObjectSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public float removeGravityDuration = 5;

    public float delayBeforeFreezingVelocity = 3;

    public bool pauseExtraForceAfterDelay;

    public float pauseExtraForceDelay;

    [Space]

    public bool setObjectAsKinematicOnZeroVelocity;

    public float durationAsKinematic;

    [Space]
    [Header ("Force Settings")]
    [Space]

    public float pauseExtraForceSpeed = 3;

    public float extraGravityForce = 5;

    public float extraTorqueForce = 8;
    public ForceMode torqueForceMode;

    public bool addTorqueOnRemoveGravity;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool removeGravityActive;

    [Space]
    [Header ("Components")]
    [Space]

    public Rigidbody mainRigibody;

    grabbedObjectState currentGrabbedObjectState;
    artificialObjectGravity currentArtificialObjectGravity;

    Coroutine removeGravityCoroutine;

    bool currentObjectWasInsideGravityRoom;


    public void activateRemoveGravity ()
    {
        stopRemoveGravityCoroutine ();

        removeGravityCoroutine = StartCoroutine (activateRemoveGravityCoroutine ());
    }

    void stopRemoveGravityCoroutine ()
    {
        if (removeGravityCoroutine != null) {
            StopCoroutine (removeGravityCoroutine);
        }

        removeGravityActive = false;
    }

    IEnumerator activateRemoveGravityCoroutine ()
    {
        removeGravity ();

        Vector3 upDirection = Vector3.up;

        if (currentArtificialObjectGravity != null) {
            upDirection = -currentArtificialObjectGravity.getCurrentNormalDirection ();
        }

        Vector3 initialForce = upDirection * extraGravityForce;

        if (mainRigibody == null) {
            mainRigibody = gameObject.GetComponent<Rigidbody> ();
        }

        if (mainRigibody != null) {
            if (addTorqueOnRemoveGravity) {
                mainRigibody.AddRelativeTorque (transform.forward * extraTorqueForce, torqueForceMode);
                mainRigibody.AddRelativeTorque (transform.right * extraTorqueForce, torqueForceMode);
            } else {
                mainRigibody.AddForce (initialForce, torqueForceMode);
            }
        }

        if (showDebugPrint) {
            print ("force applied, waiting for delayBeforeFreezingVelocity");
        }

        yield return new WaitForSeconds (delayBeforeFreezingVelocity);


        bool targetReached = false;

        float timer = 0;

        bool pauseExtraForceActivated = false;

        Vector3 currentVelocity = mainRigibody.linearVelocity;

        while (!targetReached) {
            timer += Time.deltaTime;

            if (pauseExtraForceAfterDelay) {
                if (pauseExtraForceActivated) {
                    if (GKC_Utils.distance (currentVelocity, Vector3.zero) > 0.05f) {
                        currentVelocity = Vector3.MoveTowards (currentVelocity, Vector3.zero, timer * pauseExtraForceSpeed);

                        mainRigibody.linearVelocity = currentVelocity;
                    }
                } else {
                    if (timer >= pauseExtraForceDelay) {
                        pauseExtraForceActivated = true;

                        if (showDebugPrint) {
                            print ("starting to decrease speed");
                        }
                    }
                }
            }

            if (timer >= removeGravityDuration) {
                targetReached = true;
            }

            yield return null;
        }

        if (setObjectAsKinematicOnZeroVelocity) {
            mainRigibody.isKinematic = true;

            yield return new WaitForSeconds (durationAsKinematic);

            mainRigibody.isKinematic = false;
        }

        resumePreviousGravity ();
    }

    void removeGravity ()
    {
        if (removeGravityActive) {
            return;
        }

        removeGravityActive = true;

        currentGrabbedObjectState = gameObject.GetComponent<grabbedObjectState> ();

        currentObjectWasInsideGravityRoom = false;

        if (currentGrabbedObjectState != null) {
            currentObjectWasInsideGravityRoom = currentGrabbedObjectState.isInsideZeroGravityRoom ();
        }

        if (currentObjectWasInsideGravityRoom) {
            if (currentGrabbedObjectState.getCurrentZeroGravityRoom ().isRoomHasZeroGravityActive ()) {

                stopRemoveGravityCoroutine ();

                return;
            }
        }

        currentArtificialObjectGravity = gameObject.GetComponent<artificialObjectGravity> ();

        if (currentArtificialObjectGravity != null) {
            currentArtificialObjectGravity.setActiveState (false);
        }

        mainRigibody.useGravity = false;
    }

    void resumePreviousGravity ()
    {
        bool useGravityResult = false;

        currentArtificialObjectGravity = gameObject.GetComponent<artificialObjectGravity> ();

        if (currentArtificialObjectGravity != null) {
            currentArtificialObjectGravity.setActiveState (true);
        } else {
            useGravityResult = true;
        }

        if (currentObjectWasInsideGravityRoom) {
            useGravityResult = false;
        }

        mainRigibody.useGravity = useGravityResult;

        removeGravityActive = false;

        if (setObjectAsKinematicOnZeroVelocity) {
            mainRigibody.isKinematic = false;
        }
    }
}
