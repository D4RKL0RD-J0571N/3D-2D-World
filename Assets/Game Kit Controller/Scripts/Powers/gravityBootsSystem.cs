using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class gravityBootsSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool gravityBootsEnabled;

    public float gravityBootsRotationSpeed = 10;

    public bool useExtraRaycastDetectionOnGravityBootsEnabled = true;

    public bool avoidRigidbodiesOnRaycastEnabled;

    public bool rotateToOriginalNormalOnAirEnabled = true;

    public bool stopGravityAdherenceWhenStopRun = true;

    [Space]

    public bool allowCircumnavigationOnAllSurfacesWithIgnoretagList;

    public List<string> circumnavigationOnAllSurfacesWithIgnoretagList = new List<string> ();

    [Space]
    [Header ("Surface Detection Settings")]
    [Space]

    public LayerMask layer;

    public float raycastDistance = 2;

    public float groundRaycastDistance = 5;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool gravityBootsActive;

    public bool mainCoroutineActive;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventOnGravityBootsStart;
    public bool useEventOnGravityBootsEnd;

    [Space]

    public UnityEvent eventOnGravityBootsStart;
    public UnityEvent eventOnGravityBootsEnd;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController mainPlayerController;
    public gravitySystem mainGravitySystem;

    public Transform playerTransform;

    bool resetingSurfaceRotationAfterGravityBootsActive;

    float lastTimeResetSurfaceRotationBelow;

    RaycastHit hit;

    Vector3 originalNormal;

    Coroutine mainCoroutine;

    bool checkRunGravity;

    void Start ()
    {
        if (gravityBootsEnabled) {
            mainCoroutine = StartCoroutine (updateCoroutine ());

            mainCoroutineActive = true;
        }
    }

    public void stopUpdateCoroutine ()
    {
        if (mainCoroutine != null) {
            StopCoroutine (mainCoroutine);
        }

        mainCoroutineActive = false;
    }

    IEnumerator updateCoroutine ()
    {
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            if (!mainPlayerController.playerIsBusy ()) {
                checkIfActivateGravityBoots ();

                updateGravityBootsState ();
            }

            yield return waitTime;
        }
    }

    void updateGravityBootsState ()
    {
        if (gravityBootsActive && gravityBootsEnabled) {
            if (resetingSurfaceRotationAfterGravityBootsActive) {
                if (mainGravitySystem.isCharacterRotatingToSurface ()) {
                    return;
                } else {
                    resetingSurfaceRotationAfterGravityBootsActive = false;

                    lastTimeResetSurfaceRotationBelow = Time.time;
                }
            }

            if (lastTimeResetSurfaceRotationBelow > 0) {
                if (Time.time > lastTimeResetSurfaceRotationBelow + 1) {
                    lastTimeResetSurfaceRotationBelow = 0;
                } else {
                    return;
                }
            }

            if (mainPlayerController.isExternalControlBehaviorForAirTypeActive ()) {
                stopGravityBoots ();
            }

            //check a surface in front of the player, to rotate to it
            bool surfaceInFrontDetected = false;

            Vector3 playerPosition = playerTransform.position;

            Vector3 playerTransformUp = playerTransform.up;
            Vector3 playerTransforForward = playerTransform.forward;

            Vector3 raycastPosition = playerPosition + playerTransformUp;
            Vector3 raycastDirection = playerTransforForward;

            bool ignoreSurfaceDetected = false;

            if (Physics.Raycast (raycastPosition, raycastDirection, out hit, raycastDistance, layer)) {
                if (avoidRigidbodiesOnRaycastEnabled) {
                    if (hit.rigidbody != null) {
                        ignoreSurfaceDetected = true;
                    }
                }

                if (!hit.collider.isTrigger && !ignoreSurfaceDetected) {
                    mainGravitySystem.checkRotateToSurfaceWithoutParent (hit.normal, gravityBootsRotationSpeed);

                    mainPlayerController.setCurrentNormalCharacter (hit.normal);

                    surfaceInFrontDetected = true;

                    if (showDebugPrint) {
                        print ("surface detected in front");
                    }
                }
            }

            if (useExtraRaycastDetectionOnGravityBootsEnabled) {
                if (!surfaceInFrontDetected && mainPlayerController.isPlayerOnGround () && !mainGravitySystem.isCharacterRotatingToSurface ()) {
                    Vector3 heading = (playerPosition + playerTransforForward) - raycastPosition;

                    float distance = heading.magnitude;

                    raycastDirection = heading / distance;

                    ignoreSurfaceDetected = false;

                    if (Physics.Raycast (raycastPosition, raycastDirection, out hit, 4, layer)) {
                        if (avoidRigidbodiesOnRaycastEnabled) {
                            if (hit.rigidbody != null) {
                                ignoreSurfaceDetected = true;
                            }
                        }

                        if (!ignoreSurfaceDetected && !hit.collider.isTrigger && mainGravitySystem.getCurrentNormal () != hit.normal) {
                            mainGravitySystem.checkRotateToSurfaceWithoutParent (hit.normal, gravityBootsRotationSpeed);

                            mainPlayerController.setCurrentNormalCharacter (hit.normal);

                            surfaceInFrontDetected = true;

                            if (showDebugPrint) {
                                print ("surface detected below 1");
                            }
                        }
                    }

                    if (!surfaceInFrontDetected) {
                        raycastPosition = playerPosition + playerTransformUp + playerTransforForward;

                        raycastDirection = -playerTransformUp;

                        ignoreSurfaceDetected = false;

                        if (Physics.Raycast (raycastPosition, raycastDirection, out hit, 2, layer)) {
                            if (avoidRigidbodiesOnRaycastEnabled) {
                                if (hit.rigidbody != null) {
                                    ignoreSurfaceDetected = true;
                                }
                            }

                            if (!ignoreSurfaceDetected && !hit.collider.isTrigger && mainGravitySystem.getCurrentNormal () != hit.normal) {
                                mainGravitySystem.checkRotateToSurfaceWithoutParent (hit.normal, gravityBootsRotationSpeed);

                                mainPlayerController.setCurrentNormalCharacter (hit.normal);

                                surfaceInFrontDetected = true;

                                if (showDebugPrint) {
                                    print ("surface detected below 2");
                                }
                            }
                        }
                    }
                }
            }

            //check if the player is too far from his current ground, to rotate to his previous normal
            if (rotateToOriginalNormalOnAirEnabled) {
                if (!surfaceInFrontDetected && 
                    !Physics.Raycast (playerPosition + playerTransformUp, -playerTransformUp, out hit, groundRaycastDistance, layer)) {
                    if (mainGravitySystem.getCurrentRotatingNormal () != originalNormal && !checkRunGravity) {
                        checkRunGravity = true;

                        if (mainGravitySystem.isCharacterRotatingToSurface ()) {
                            mainGravitySystem.stopRotateToSurfaceWithOutParentCoroutine ();
                        }

                        mainGravitySystem.checkRotateToSurface (originalNormal, 2);

                        mainPlayerController.setCurrentNormalCharacter (originalNormal);

                        resetingSurfaceRotationAfterGravityBootsActive = true;
                    }

                    if (checkRunGravity && mainGravitySystem.getCurrentRotatingNormal () == originalNormal) {
                        checkRunGravity = false;
                    }
                }
            }
        }
    }

    void checkIfActivateGravityBoots ()
    {
        if (gravityBootsEnabled) {
            if (!gravityBootsActive && !mainPlayerController.isWallRunningActive () || mainPlayerController.isExternalControlBehaviorForAirTypeActive ()) {
                if (!resetingSurfaceRotationAfterGravityBootsActive || !mainGravitySystem.isCharacterRotatingToSurface ()) {
                    if (!mainGravitySystem.isCurcumnavigating ()) {
                        gravityBootsActive = true;
                    }
                }
            }
        }
    }
    //when the player stops running, those parameters back to their normal values
    public void stopGravityBoots ()
    {
        if (gravityBootsActive) {
            resetingSurfaceRotationAfterGravityBootsActive = false;

            if (stopGravityAdherenceWhenStopRun) {
                if (mainGravitySystem.getCurrentRotatingNormal () != originalNormal) {
                    if (mainGravitySystem.isCharacterRotatingToSurface ()) {
                        mainGravitySystem.stopRotateToSurfaceWithOutParentCoroutine ();
                    }

                    mainGravitySystem.checkRotateToSurface (originalNormal, 2);

                    resetingSurfaceRotationAfterGravityBootsActive = true;
                }

                mainPlayerController.setCurrentNormalCharacter (originalNormal);
            }

            mainGravitySystem.setAllowCircumnavigationOnAllSurfacesWithIgnoretagListState (false);

            gravityBootsActive = false;

            lastTimeResetSurfaceRotationBelow = 0;
        }
    }

    public bool areGravityBootsActive ()
    {
        return gravityBootsActive;
    }

    public void setGravityBootsEnabledState (bool state)
    {
        if (gravityBootsEnabled == state) {
            return;
        }

        gravityBootsEnabled = state;

        if (gravityBootsEnabled) {
            mainCoroutine = StartCoroutine (updateCoroutine ());

            mainCoroutineActive = true;

            originalNormal = mainGravitySystem.getCurrentRotatingNormal ();

            mainGravitySystem.setAllowCircumnavigationOnAllSurfacesWithIgnoretagListState (allowCircumnavigationOnAllSurfacesWithIgnoretagList);
           
            mainGravitySystem.setCircumnavigationOnAllSurfacesWithIgnoretagList (circumnavigationOnAllSurfacesWithIgnoretagList);
        } else {
            if (gravityBootsActive) {
                stopGravityBoots ();
            }

            stopUpdateCoroutine ();
        }

        checkEventsOnGravityBoots (state);
    }

    public void toggleGravityBootsEnabledState ()
    {
        setGravityBootsEnabledState (!gravityBootsEnabled);
    }

    public void disableGravityBootsState ()
    {
        if (gravityBootsActive) {
            stopGravityBoots ();
        }

        setGravityBootsEnabledState (false);
    }

    void checkEventsOnGravityBoots (bool state)
    {
        if (state) {
            if (useEventOnGravityBootsStart) {
                eventOnGravityBootsStart.Invoke ();
            }
        } else {
            if (useEventOnGravityBootsEnd) {
                eventOnGravityBootsEnd.Invoke ();
            }
        }
    }
}