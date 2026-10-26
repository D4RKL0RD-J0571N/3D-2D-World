using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class matchPlayerToTargetSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool matchSystemEnabled = true;

    public float matchSpeed = 10;

    public float positionOffset;

    public bool ignoreCustomPositionOffset;

    public bool useDistancePercentageAsOffset;
    [Range (0, 1)] public float positionOffsetPercentage;

    [Space]
    [Space]

    public float minDistanceToMatchPosition;
    public float maxDistanceToMatchPosition;

    [Space]

    public float minAngleToMatchRotation;

    public float maxAngleToMatchRotation;

    public bool adjustRotationEvenIfNotMoved;

    public float minDistanceToMatchRotation;
    public float maxDistanceToMatchRotation;

    [Space]

    public bool useMaxDistanceToCameraCenter;
    public float maxDistanceToCameraCenter;

    public bool useMoveInputAsDirectionToClosestTarget;

    [Space]
    [Header ("Vertical Distance Settings")]
    [Space]

    public bool adjustVerticalPositionEnabled;
    public float raycastDistanceForVerticalPosition;
    public LayerMask layerMaskForVerticalPosition;

    [Space]

    public bool checkMaxVerticalDistanceWithTarget;
    public float maxVerticalDistanceWithTarget;

    public bool useLocalVerticalDistance;

    [Space]

    public bool checkIfObstaclesToTarget;
    public LayerMask layerToCheckTarget;

    [Space]
    [Header ("Front Position Settings")]
    [Space]

    public bool useFrontPositionAsPositionToMatch;
    public Vector3 frontPositionAsPositionToMatchOffset;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool addMainPlayerOnListForAI;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool movementActive;

    public bool matchPositionPaused;

    public bool getClosestTargetWithoutConditionsActive;

    public List<Transform> charactersAround = new List<Transform> ();

    [Space]
    [Header ("Events")]
    [Space]

    public bool useEventsOnTargetsChange;
    public UnityEvent eventOnTargetsDetected;
    public UnityEvent eventOnTargetsRemoved;

    public bool useEventOnFirstTargetDetected;
    public UnityEvent eventOnFirstTargetDetected;

    public bool useEventOnLastTargetRemoved;
    public UnityEvent eventOnLastTargetRemoved;

    [Space]
    [Header ("Components")]
    [Space]

    public Transform playerTransform;

    public Transform debugTargetToMatchTransform;

    public Camera mainCamera;

    public playerCamera mainPlayerCamera;

    public playerController mainPlayerController;

    public playerInputManager mainPlayerInputManager;

    Coroutine movementCoroutine;

    Vector3 screenPoint;

    Vector3 characterPosition;

    Vector3 centerScreen;

    Vector3 currentPlayerPosition;

    bool playerCheckInitialized;


    public void findPlayerOnScene ()
    {
        addCharacterAround (GKC_Utils.findMainPlayerTransformOnScene ());
    }

    public void addCharacterAround (Transform newCharacter)
    {
        if (newCharacter == null) {
            return;
        }

        if (!charactersAround.Contains (newCharacter)) {
            charactersAround.Add (newCharacter);

            checkRemoveEmptyObjects ();

            if (charactersAround.Count == 1) {
                checkEventOnFirstTargetDetected ();
            }

            checkEventsOnTargetsChange (true);

            if (showDebugPrint) {
                print ("Adding target " + newCharacter.name);
            }
        }
    }

    public void removeCharacterAround (Transform newCharacter)
    {
        if (charactersAround.Contains (newCharacter)) {
            charactersAround.Remove (newCharacter);

            checkRemoveEmptyObjects ();

            if (showDebugPrint) {
                print ("Removing target " + newCharacter.name);
            }
        }

        if (charactersAround.Count == 0) {
            checkEventsOnTargetsChange (false);

            checkEventOnLastTargetRemoved ();
        }
    }

    public void clearAllCharactersAround ()
    {
        if (charactersAround.Count > 0) {
            charactersAround.Clear ();
        }

        checkEventsOnTargetsChange (false);

        checkEventOnLastTargetRemoved ();
    }

    public void setMatchPositionPausedState (bool state)
    {
        matchPositionPaused = state;
    }

    public void setMatchSystemEnabledstate (bool state)
    {
        matchSystemEnabled = state;
    }

    public void setAddMainPlayerOnListForAIState (bool state)
    {
        addMainPlayerOnListForAI = state;
    }

    public void activateMatchPosition (float customOffset)
    {
        if (!matchSystemEnabled) {
            return;
        }

        if (matchPositionPaused) {
            return;
        }

        if (!playerCheckInitialized) {
            if (matchSystemEnabled && addMainPlayerOnListForAI) {
                findPlayerOnScene ();
            }

            playerCheckInitialized = true;
        }

        if (debugTargetToMatchTransform != null) {
            activateMatchPosition (debugTargetToMatchTransform, customOffset);

            return;
        }

        Transform transformToMatchPosition = getCurrentTargetToMatchPosition ();

        if (transformToMatchPosition != null) {
            activateMatchPosition (transformToMatchPosition, customOffset);

            if (showDebugPrint) {
                print ("activating match position for " + transformToMatchPosition.name);
            }
        } else {
            if (showDebugPrint) {
                print ("no target to match found");
            }
        }

        if (charactersAround.Count == 0) {
            checkEventsOnTargetsChange (false);

            checkEventOnLastTargetRemoved ();
        }
    }

    public void activateMatchPosition (Transform targetToUse, float customOffset)
    {
        stopMovement ();

        if (targetToUse == null) {
            return;
        }

        movementCoroutine = StartCoroutine (activateMatchPositionCoroutine (targetToUse, customOffset));
    }

    public void stopMovementIfActive ()
    {
        if (movementActive) {
            stopMovement ();
        }
    }

    public void stopMovement ()
    {
        if (movementCoroutine != null) {
            StopCoroutine (movementCoroutine);
        }

        movementActive = false;
    }

    public void setUseFrontPositionAsPositionToMatchState (bool state)
    {
        useFrontPositionAsPositionToMatch = state;
    }

    IEnumerator activateMatchPositionCoroutine (Transform targetToUse, float customOffset)
    {
        movementActive = true;

        Vector3 targetToUsePosition = targetToUse.position;

        if (useFrontPositionAsPositionToMatch) {
            targetToUsePosition += targetToUse.right * frontPositionAsPositionToMatchOffset.x +
                targetToUse.up * frontPositionAsPositionToMatchOffset.y +
                targetToUse.forward * frontPositionAsPositionToMatchOffset.z;
        }

        Vector3 currentPosition = playerTransform.position;

        Vector3 direction = new Vector3 (targetToUsePosition.x, currentPosition.y, targetToUsePosition.z) - currentPosition;

        direction = direction / direction.magnitude;

        float angle = Vector3.SignedAngle (playerTransform.forward, direction, playerTransform.up);

        if (useFrontPositionAsPositionToMatch) {
            angle = Vector3.SignedAngle (playerTransform.forward, -targetToUse.forward, playerTransform.up);
        }

        float distanceToTarget = GKC_Utils.distance (targetToUsePosition, currentPosition);

        float currentPositionOffset = positionOffset;

        if (useFrontPositionAsPositionToMatch) {
            currentPositionOffset = customOffset;
        } else {
            if (customOffset != 0 && !ignoreCustomPositionOffset) {
                currentPositionOffset = customOffset;
            } else {
                if (useDistancePercentageAsOffset) {
                    currentPositionOffset = distanceToTarget * positionOffsetPercentage;
                }
            }

            float targetToUseRadius = GKC_Utils.getCharacterRadius (targetToUse);

            currentPositionOffset += mainPlayerController.getCharacterRadius () + targetToUseRadius;

            if (showDebugPrint) {
                print ("current character radius obtained " + targetToUseRadius);
            }
        }

        if (adjustVerticalPositionEnabled) {
            direction = targetToUsePosition - currentPosition;

            direction = direction / direction.magnitude;
        }

        Vector3 targetPosition = currentPosition + direction * (distanceToTarget - currentPositionOffset);

        if (showDebugPrint) {
            print (targetPosition + " " + currentPosition + " " + currentPositionOffset);
        }

        if (adjustVerticalPositionEnabled) {

            RaycastHit hit;

            if (Physics.Raycast (targetPosition + playerTransform.up * 1.2f, -playerTransform.up, out hit, raycastDistanceForVerticalPosition, layerMaskForVerticalPosition)) {
                targetPosition = hit.point;
            }
        }

        Quaternion targetRotation = Quaternion.Euler (playerTransform.eulerAngles + playerTransform.up * angle);

        bool targetReached = false;

        float angleDifference = 0;

        float positionDifference = 0;

        float t = 0;

        float movementTimer = 0;

        float dist = GKC_Utils.distance (targetPosition, currentPosition);

        float duration = dist / matchSpeed;

        bool positionChangeActive = false;

        bool rotationChangeActive = false;

        if (distanceToTarget > minDistanceToMatchPosition && distanceToTarget < maxDistanceToMatchPosition) {
            positionChangeActive = true;
        }

        if (positionChangeActive ||
            (adjustRotationEvenIfNotMoved && distanceToTarget > minDistanceToMatchRotation && distanceToTarget < maxDistanceToMatchRotation)) {

            if (Mathf.Abs (angle) > minAngleToMatchRotation && Mathf.Abs (angle) < maxAngleToMatchRotation) {
                rotationChangeActive = true;
            }
        }

        while (!targetReached) {
            t += Time.deltaTime / duration;

            if (positionChangeActive) {
                playerTransform.position = Vector3.Lerp (playerTransform.position, targetPosition, t);
            }

            if (rotationChangeActive) {
                playerTransform.rotation = Quaternion.Lerp (playerTransform.rotation, targetRotation, t);
            }

            angleDifference = Quaternion.Angle (playerTransform.rotation, targetRotation);

            positionDifference = GKC_Utils.distance (playerTransform.position, targetPosition);

            movementTimer += Time.deltaTime;

            if (positionChangeActive && rotationChangeActive) {
                if (positionDifference < 0.01f && angleDifference < 0.2f) {
                    targetReached = true;
                }
            } else {
                if (rotationChangeActive) {
                    if (angleDifference < 0.2f) {
                        targetReached = true;
                    }
                }

                if (positionChangeActive) {
                    if (positionDifference < 0.01f) {
                        targetReached = true;
                    }
                }
            }

            if (movementTimer > (duration + 1)) {
                targetReached = true;
            }

            if (!positionChangeActive && !rotationChangeActive) {
                targetReached = true;
            }

            yield return null;
        }

        movementActive = false;
    }

    public Transform getCurrentTargetToMatchPosition ()
    {
        if (!matchSystemEnabled) {
            return null;
        }

        if (matchPositionPaused) {
            return null;
        }

        if (!playerCheckInitialized) {
            if (matchSystemEnabled && addMainPlayerOnListForAI) {
                findPlayerOnScene ();
            }

            playerCheckInitialized = true;
        }

        float maxDistanceToTarget = Mathf.Infinity;

        Transform transformToMatchPosition = null;

        float currentDistance = 0;

        float currentDistanceToScreenCenter = 0;

        centerScreen = mainPlayerCamera.getScreenCenter ();

        bool ignoreRestOfChecks = false;

        float maxAngleWithTarget = Mathf.Infinity;

        currentPlayerPosition = playerTransform.position;

        Transform currentLockedCameraTransform = mainPlayerCamera.getCurrentLockedCameraTransform ();

        if (mainCamera == null) {
            mainCamera = mainPlayerCamera.getMainCamera ();
        }

        for (int i = charactersAround.Count - 1; i >= 0; i--) {
            if (charactersAround [i] != null) {
                if (!applyDamage.checkIfDead (charactersAround [i].gameObject)) {

                    characterPosition = charactersAround [i].position;

                    if (getClosestTargetWithoutConditionsActive) {
                        currentDistance = GKC_Utils.distance (characterPosition, currentPlayerPosition);

                        if (currentDistance < maxDistanceToTarget) {
                            maxDistanceToTarget = currentDistance;

                            transformToMatchPosition = charactersAround [i];
                        }
                    } else {
                        screenPoint = mainCamera.WorldToScreenPoint (characterPosition);
                        currentDistanceToScreenCenter = GKC_Utils.distance (screenPoint, centerScreen);

                        bool canBeChecked = false;

                        if (useMoveInputAsDirectionToClosestTarget) {
                            Vector2 rawAxisvalues = mainPlayerInputManager.rawMovementAxis;

                            if (rawAxisvalues != Vector2.zero) {
                                Vector3 direction = new Vector3 (characterPosition.x, currentPlayerPosition.y, characterPosition.z) - currentPlayerPosition;

                                direction = direction / direction.magnitude;

                                Vector3 inputDirection = Vector3.zero;

                                if (mainPlayerCamera.isCameraTypeFree ()) {
                                    inputDirection = (rawAxisvalues.y * mainPlayerCamera.transform.forward + rawAxisvalues.x * mainPlayerCamera.transform.right);
                                } else {
                                    inputDirection = (rawAxisvalues.y * currentLockedCameraTransform.forward + rawAxisvalues.x * currentLockedCameraTransform.right);
                                }

                                inputDirection.Normalize ();

                                float angle = Vector3.SignedAngle (inputDirection, direction, playerTransform.up);

                                ignoreRestOfChecks = true;

                                if (Mathf.Abs (angle) < maxAngleWithTarget) {
                                    maxAngleWithTarget = Mathf.Abs (angle);

                                    transformToMatchPosition = charactersAround [i];
                                }
                            }
                        }

                        bool ignoreCurrentTarget = false;

                        if (checkMaxVerticalDistanceWithTarget) {
                            float verticalDistance = 0;

                            if (useLocalVerticalDistance) {
                                verticalDistance = verticalDistanceInLocalDirection (playerTransform, charactersAround [i]);
                            } else {
                                verticalDistance = Mathf.Abs (characterPosition.y - currentPlayerPosition.y);
                            }

                            if (showDebugPrint) {
                                print ("vertical distance obtained for " + charactersAround [i].name + " " + verticalDistance);
                            }

                            if (verticalDistance > maxVerticalDistanceWithTarget) {
                                transformToMatchPosition = null;

                                ignoreCurrentTarget = true;

                                if (showDebugPrint) {
                                    print ("vertical distance too long " + verticalDistance);
                                }
                            }
                        }

                        if (!ignoreRestOfChecks && !ignoreCurrentTarget) {
                            //unity takes the resolution of the main screen if the press or input is done on
                            //the editor it self, instead of taking the resolution of the game window

                            if (useMaxDistanceToCameraCenter && mainPlayerCamera.isCameraTypeFree ()) {
                                if (currentDistanceToScreenCenter < maxDistanceToCameraCenter) {
                                    canBeChecked = true;
                                }
                            } else {
                                canBeChecked = true;
                            }

                            if (checkIfObstaclesToTarget) {
                                RaycastHit hit;

                                Vector3 raycastPosition = currentPlayerPosition + playerTransform.up;

                                Vector3 targetPosition = charactersAround [i].position + charactersAround [i].up;

                                Vector3 raycastDirection = raycastPosition - targetPosition;
                                raycastDirection = raycastDirection / raycastDirection.magnitude;

                                float raycastDistance = GKC_Utils.distance (raycastPosition, targetPosition);

                                if (Physics.Raycast (raycastPosition, raycastDirection, out hit, raycastDistance, layerToCheckTarget)) {
                                    canBeChecked = false;

                                    if (showDebugPrint) {
                                        print ("obstacle detected " + hit.collider.name);
                                    }
                                }
                            }

                            if (canBeChecked) {
                                currentDistance = GKC_Utils.distance (characterPosition, currentPlayerPosition);

                                if (currentDistance < maxDistanceToTarget) {
                                    maxDistanceToTarget = currentDistance;

                                    transformToMatchPosition = charactersAround [i];
                                }
                            }
                        }
                    }
                } else {
                    charactersAround.RemoveAt (i);
                }
            } else {
                charactersAround.RemoveAt (i);
            }
        }

        return transformToMatchPosition;
    }


    //future check to get the distance locally instead of using the Y axis
    float verticalDistanceInLocalDirection (Transform a, Transform b)
    {
        Vector3 direction = b.position - a.position;

        return Mathf.Abs (Vector3.Dot (direction, a.up));
    }

    void checkEventsOnTargetsChange (bool state)
    {
        if (useEventsOnTargetsChange) {
            if (state) {
                eventOnTargetsDetected.Invoke ();
            } else {
                eventOnTargetsRemoved.Invoke ();
            }
        }
    }

    void checkEventOnFirstTargetDetected ()
    {
        if (useEventOnFirstTargetDetected) {
            eventOnFirstTargetDetected.Invoke ();
        }
    }

    void checkEventOnLastTargetRemoved ()
    {
        if (useEventOnLastTargetRemoved) {
            eventOnLastTargetRemoved.Invoke ();
        }
    }

    void checkRemoveEmptyObjects ()
    {
        for (int i = charactersAround.Count - 1; i >= 0; i--) {
            if (charactersAround [i] == null) {
                charactersAround.RemoveAt (i);
            }
        }
    }

    public void setGetClosestTargetWithoutConditionsActiveState (bool state)
    {
        getClosestTargetWithoutConditionsActive = state;
    }
}