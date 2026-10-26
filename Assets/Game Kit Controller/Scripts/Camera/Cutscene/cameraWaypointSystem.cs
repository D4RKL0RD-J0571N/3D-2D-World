using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using System;

public class cameraWaypointSystem : MonoBehaviour
{
    public bool cameraWaypointEnabled = true;

    public Transform currentCameraTransform;
    public List<cameraWaypointInfo> waypointList = new List<cameraWaypointInfo> ();
    public float waitTimeBetweenPoints;
    public float movementSpeed;
    public float rotationSpeed;

    public Transform pointToLook;

    public bool useEventOnEnd;
    public UnityEvent eventOnEnd;

    public bool showGizmo;
    public Color gizmoLabelColor = Color.black;
    public float gizmoRadius;
    public bool useHandleForWaypoints;
    public float handleRadius;
    public Color handleGizmoColor;
    public bool showWaypointHandles;

    public float currentMovementSpeed;
    public float currentRotationSpeed;

    public bool useBezierCurve;
    public BezierSpline spline;
    public float bezierDuration = 10;
    public bool useExternalProgress;
    [NonSerialized]
    public Func<float> externalProgress;
    public bool snapCameraToFirstSplinePoint;

    public bool searchPlayerOnSceneIfNotAssigned = true;

    public bool resetCameraPositionOnEnd;
    public float resetCameraPositionSpeed = 5;

    public bool pausePlayerCameraEnabled;

    public bool useMainCameraTransform;

    public bool setThisTransformAsCameraParent;

    public bool ignoreWaypointOnFreeCamera;
    public bool ignoreWaypointOnLockedCamera;
    public bool ignoreWaypointOnFBA;


    public bool useEventToStopCutScene;
    public UnityEvent eventToStopCutscene;



    bool waypointInProcess;

    bool customTransformToLookActive;
    Transform customTransformToLook;


    Coroutine resetCameraPositionCoroutine;

    float currentWaitTime;
    Vector3 targetDirection;

    Coroutine movement;
    Transform currentWaypoint;
    int currentWaypointIndex;

    int i;

    List<Transform> currentPath = new List<Transform> ();
    cameraWaypointInfo currentCameraWaypointInfo;

    int previousWaypointIndex;

    Vector3 targetPosition;
    Quaternion targetRotation;

    GameObject playerCameraGameObject;
    Transform pivotDirection;

    playerCamera currentPlayerCamera;

    Transform previousCameraParent;

    Vector3 previousCameraPosition;
    Quaternion previousCameraRotation;

    bool isCameraTypeFree;

    bool isCameraTypeFBA;


    public void setCurrentCameraTransform (GameObject cameraGameObject)
    {
        if (cameraGameObject == null) {
            return;
        }

        currentCameraTransform = cameraGameObject.transform;

        if (previousCameraParent == null) {
            previousCameraParent = currentCameraTransform.parent;

            previousCameraPosition = currentCameraTransform.localPosition;
            previousCameraRotation = currentCameraTransform.localRotation;
        }
    }

    public void findPlayerOnScene ()
    {
        if (searchPlayerOnSceneIfNotAssigned) {
            GameObject currentPlayer = GKC_Utils.findMainPlayerOnScene ();

            setCurrentPlayer (currentPlayer);
        }
    }

    public void setCurrentPlayer (GameObject currentPlayer)
    {
        if (currentPlayer != null) {
            playerComponentsManager mainPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

            if (mainPlayerComponentsManager != null) {
                currentPlayerCamera = mainPlayerComponentsManager.getPlayerCamera ();

                playerCameraGameObject = currentPlayerCamera.gameObject;

                pivotDirection = currentPlayerCamera.getPivotCameraTransform ();

                isCameraTypeFree = currentPlayerCamera.isCameraTypeFree ();

                isCameraTypeFBA = currentPlayerCamera.isFullBodyAwarenessActive ();

                if (useMainCameraTransform) {
                    if (isCameraTypeFree) {
                        setCurrentCameraTransform (currentPlayerCamera.getCameraTransform ().gameObject);
                    } else {
                        setCurrentCameraTransform (currentPlayerCamera.getCurrentLockedCameraTransform ().gameObject);
                    }
                } else {
                    setCurrentCameraTransform (currentPlayerCamera.getMainCamera ().gameObject);
                }
            }
        }
    }

    bool ignoreWaypointOnCurrentViewResult ()
    {
        if (ignoreWaypointOnFreeCamera) {
            if (isCameraTypeFree) {
                return true;
            }
        }

        if (ignoreWaypointOnLockedCamera) {
            if (!isCameraTypeFree) {
                return true;
            }
        }

        if (ignoreWaypointOnFBA) {
            if (isCameraTypeFBA) {
                return true;
            }
        }

        return false;
    }

    //stop the platform coroutine movement and play again
    public void checkMovementCoroutine (bool play)
    {
        if (!cameraWaypointEnabled) {
            return;
        }

        if (ignoreWaypointOnCurrentViewResult ()) {
            return;
        }

        stopMoveThroughWayPointsCoroutine ();

        if (play) {
            if (currentCameraTransform == null) {
                findPlayerOnScene ();

                if (currentCameraTransform == null) {
                    print ("WARNING: no current camera transform has been assigned on the camera waypoint system." +
                    " Make sure to use a trigger to activate the element or assign the player manually");

                    return;
                }
            }

            currentWaypointIndex = 0;

            previousWaypointIndex = -1;

            //if the current path to move has waypoints, then
            if (currentPath.Count == 0) {
                for (i = 0; i < waypointList.Count; i++) {
                    currentPath.Add (waypointList [i].waypointTransform);
                }
            }

            if (currentPath.Count > 0) {
                if (pausePlayerCameraEnabled) {
                    currentPlayerCamera.pauseOrPlayCamera (false);
                }

                if (setThisTransformAsCameraParent) {
                    currentCameraTransform.SetParent (transform);
                }

                if (useBezierCurve) {
                    movement = StartCoroutine (moveAlongBezierCurve ());
                } else {
                    movement = StartCoroutine (moveAlongWaypoints ());
                }
            }
        }
    }

    public void stopMoveThroughWayPointsCoroutine ()
    {
        if (movement != null) {
            StopCoroutine (movement);
        }

        waypointInProcess = false;
    }

    IEnumerator moveAlongWaypoints ()
    {
        waypointInProcess = true;

        //move between every waypoint
        foreach (Transform waypoint in currentPath) {
            currentWaypoint = waypoint;
            currentCameraWaypointInfo = waypointList [currentWaypointIndex];

            //wait the amount of time configured
            if (currentCameraWaypointInfo.useCustomWaitTimeBetweenPoint) {
                currentWaitTime = currentCameraWaypointInfo.waitTimeBetweenPoints;
            } else {
                currentWaitTime = waitTimeBetweenPoints;
            }

            targetPosition = currentWaypoint.position;
            targetRotation = currentWaypoint.rotation;


            WaitForSeconds delay = new WaitForSeconds (currentWaitTime);

            yield return delay;

            //yield return new WaitForSeconds (currentWaitTime);

            if (currentCameraWaypointInfo.useCustomMovementSpeed) {
                currentMovementSpeed = currentCameraWaypointInfo.movementSpeed;
            } else {
                currentMovementSpeed = movementSpeed;
            }

            if (currentCameraWaypointInfo.useCustomRotationSpeed) {
                currentRotationSpeed = currentCameraWaypointInfo.rotationSpeed;
            } else {
                currentRotationSpeed = rotationSpeed;
            }

            if (currentCameraWaypointInfo.smoothTransitionToNextPoint) {
                bool targetReached = false;

                float angleDifference = 0;

                float currentDistance = 0;

                bool checkAngleDifference = false;

                if (currentCameraWaypointInfo.checkRotationIsReached) {
                    checkAngleDifference = true;
                }

                //while the platform moves from the previous waypoint to the next, then displace it
                while (!targetReached) {
                    currentCameraTransform.position =
                        Vector3.MoveTowards (currentCameraTransform.position, targetPosition, Time.deltaTime * currentMovementSpeed);

                    if (currentCameraWaypointInfo.rotateCameraToNextWaypoint) {
                        targetDirection = targetPosition - currentCameraTransform.position;
                    }

                    if (currentCameraWaypointInfo.usePointToLook) {
                        if (customTransformToLookActive) {
                            targetDirection = customTransformToLook.position - currentCameraTransform.position;
                        } else {
                            targetDirection = currentCameraWaypointInfo.pointToLook.position - currentCameraTransform.position;
                        }
                    }

                    if (targetDirection != Vector3.zero) {
                        targetRotation = Quaternion.LookRotation (targetDirection);

                        currentCameraTransform.rotation =
                            Quaternion.Lerp (currentCameraTransform.rotation, targetRotation, Time.deltaTime * currentRotationSpeed);
                    }

                    angleDifference = Quaternion.Angle (currentCameraTransform.rotation, targetRotation);

                    currentDistance = GKC_Utils.distance (currentCameraTransform.position, targetPosition);

                    if (checkAngleDifference) {
                        if (currentDistance < .01f && angleDifference < 1) {
                            targetReached = true;
                        }
                    } else {
                        if (currentDistance < .01f) {
                            targetReached = true;
                        }
                    }


                    yield return null;
                }
            } else {
                currentCameraTransform.position = targetPosition;

                if (currentCameraWaypointInfo.rotateCameraToNextWaypoint) {
                    targetDirection = targetPosition - currentCameraTransform.position;
                }

                if (currentCameraWaypointInfo.usePointToLook) {
                    if (customTransformToLookActive) {
                        targetDirection = customTransformToLook.position - currentCameraTransform.position;
                    } else {
                        targetDirection = currentCameraWaypointInfo.pointToLook.position - currentCameraTransform.position;
                    }
                }

                if (!currentCameraWaypointInfo.rotateCameraToNextWaypoint && !currentCameraWaypointInfo.usePointToLook) {
                    currentCameraTransform.rotation = currentCameraWaypointInfo.waypointTransform.rotation;
                } else {
                    if (targetDirection != Vector3.zero) {
                        currentCameraTransform.rotation = Quaternion.LookRotation (targetDirection);
                    }
                }

                //yield return new WaitForSeconds (currentCameraWaypointInfo.timeOnFixedPosition);

                delay = new WaitForSeconds (currentCameraWaypointInfo.timeOnFixedPosition);

                yield return delay;
            }

            if (currentCameraWaypointInfo.useEventOnPointReached) {
                currentCameraWaypointInfo.eventOnPointReached.Invoke ();
            }

            //when the platform reaches the next waypoint
            currentWaypointIndex++;

            yield return null;
        }

        yield return null;

        if (useEventOnEnd) {
            eventOnEnd.Invoke ();
        }

        if (resetCameraPositionOnEnd) {
            resetCameraPosition ();
        }

        waypointInProcess = false;
    }

    IEnumerator moveAlongBezierCurve ()
    {
        waypointInProcess = true;

        if (!snapCameraToFirstSplinePoint) {
            spline.setInitialSplinePoint (currentCameraTransform.position);
        }

        float progress = 0;
        float progressTarget = 1;

        bool targetReached = false;

        while (!targetReached) {

            if (previousWaypointIndex != currentWaypointIndex) {

                if (previousWaypointIndex != -1) {
                    if (currentCameraWaypointInfo.useEventOnPointReached) {
                        currentCameraWaypointInfo.eventOnPointReached.Invoke ();
                    }
                }

                previousWaypointIndex = currentWaypointIndex;

                currentCameraWaypointInfo = waypointList [currentWaypointIndex];

                currentWaypoint = currentCameraWaypointInfo.waypointTransform;

                //wait the amount of time configured
                if (currentCameraWaypointInfo.useCustomWaitTimeBetweenPoint) {
                    currentWaitTime = currentCameraWaypointInfo.waitTimeBetweenPoints;
                } else {
                    currentWaitTime = waitTimeBetweenPoints;
                }

                targetPosition = currentWaypoint.position;
                targetRotation = currentWaypoint.rotation;

                WaitForSeconds delay = new WaitForSeconds (currentWaitTime);

                yield return delay;

                if (currentCameraWaypointInfo.useCustomMovementSpeed) {
                    currentMovementSpeed = currentCameraWaypointInfo.movementSpeed;
                } else {
                    currentMovementSpeed = movementSpeed;
                }

                if (currentCameraWaypointInfo.useCustomRotationSpeed) {
                    currentRotationSpeed = currentCameraWaypointInfo.rotationSpeed;
                } else {
                    currentRotationSpeed = rotationSpeed;
                }
            }

            currentWaypointIndex = spline.getPointIndex (progress);

            if (useExternalProgress) {
                if (externalProgress != null) {
                    progress = externalProgress ();
                } else {
                    Debug.LogError ("useExternalProgress is set but no externalProgress func is assigned");
                }
            } else {
                progress += Time.deltaTime / (bezierDuration * currentMovementSpeed);
            }

            Vector3 position = spline.GetPoint (progress);
            currentCameraTransform.position = position;

            if (currentCameraWaypointInfo.rotateCameraToNextWaypoint) {
                targetDirection = targetPosition - currentCameraTransform.position;
            }

            if (currentCameraWaypointInfo.usePointToLook) {
                if (customTransformToLookActive) {
                    targetDirection = customTransformToLook.position - currentCameraTransform.position;
                } else {
                    targetDirection = currentCameraWaypointInfo.pointToLook.position - currentCameraTransform.position;
                }
            }

            if (targetDirection != Vector3.zero) {
                targetRotation = Quaternion.LookRotation (targetDirection);
                currentCameraTransform.rotation = Quaternion.Lerp (currentCameraTransform.rotation, targetRotation, Time.deltaTime * currentRotationSpeed);
            }

            if (progress > progressTarget) {
                targetReached = true;
            }

            yield return null;
        }

        yield return null;

        if (useEventOnEnd) {
            eventOnEnd.Invoke ();
        }

        if (resetCameraPositionOnEnd) {
            resetCameraPosition ();
        }

        waypointInProcess = false;
    }

    public void stopWaypointsIfInProcess ()
    {
        if (waypointInProcess) {
            stopMoveThroughWayPointsCoroutine ();
        }
    }

    public void stopWaypointsAndResetCameraPosition ()
    {
        if (waypointInProcess) {
            stopMoveThroughWayPointsCoroutine ();

            if (resetCameraPositionOnEnd) {
                resetCameraPosition ();
            }
        }
    }

    public void resetCameraPosition ()
    {
        if (ignoreWaypointOnCurrentViewResult ()) {
            return;
        }

        if (resetCameraPositionCoroutine != null) {
            StopCoroutine (resetCameraPositionCoroutine);
        }

        resetCameraPositionCoroutine = StartCoroutine (resetCameraCoroutine ());
    }

    IEnumerator resetCameraCoroutine ()
    {
        setCameraDirection ();

        if (setThisTransformAsCameraParent) {
            currentCameraTransform.SetParent (previousCameraParent);
        }

        Vector3 targetPosition = previousCameraPosition;
        Quaternion targetRotation = previousCameraRotation;

        Vector3 worldTargetPosition = previousCameraParent.position;
        float dist = GKC_Utils.distance (currentCameraTransform.position, worldTargetPosition);
        float duration = dist / resetCameraPositionSpeed;
        float t = 0;

        float movementTimer = 0;

        bool targetReached = false;

        float angleDifference = 0;

        float currentDistance = 0;

        while (!targetReached) {
            t += Time.deltaTime / duration;

            currentCameraTransform.localPosition = Vector3.Lerp (currentCameraTransform.localPosition, targetPosition, t);
            currentCameraTransform.localRotation = Quaternion.Lerp (currentCameraTransform.localRotation, targetRotation, t);

            angleDifference = Quaternion.Angle (currentCameraTransform.localRotation, targetRotation);

            currentDistance = GKC_Utils.distance (currentCameraTransform.localPosition, targetPosition);

            movementTimer += Time.deltaTime;

            if (currentDistance < 0.001f && angleDifference < 0.01f) {
                targetReached = true;
            }

            if (movementTimer > (duration + 1)) {
                targetReached = true;
            }

            yield return null;
        }

        if (pausePlayerCameraEnabled) {
            currentPlayerCamera.pauseOrPlayCamera (true);
        }
    }

    public void setCameraDirection ()
    {
        playerCameraGameObject.transform.rotation = transform.rotation;

        Quaternion newCameraRotation = pivotDirection.localRotation;

        currentPlayerCamera.getPivotCameraTransform ().localRotation = newCameraRotation;

        float newLookAngleValue = newCameraRotation.eulerAngles.x;

        if (newLookAngleValue > 180) {
            newLookAngleValue -= 360;
        }

        currentPlayerCamera.setLookAngleValue (new Vector2 (0, newLookAngleValue));
    }

    public void setCameraWaypointEnabledState (bool state)
    {
        cameraWaypointEnabled = state;
    }

    public void setCustomGameObjectToLook (GameObject newGameObject)
    {
        if (newGameObject != null) {
            setCustomTransformToLook (newGameObject.transform);
        } else {
            setCustomTransformToLook (null);
        }
    }

    public void setCustomTransformToLook (Transform newTransform)
    {
        customTransformToLook = newTransform;

        customTransformToLookActive = customTransformToLook != null;
    }

    public void checkEventToStopCutscene ()
    {
        if (waypointInProcess) {
            if (useEventToStopCutScene) {
                eventToStopCutscene.Invoke ();
            }
        }
    }


    //EDITOR FUNCTIONS
    //add a new waypoint
    public void addNewWayPoint ()
    {
        Vector3 newPosition = transform.position;

        if (waypointList.Count > 0) {
            newPosition = waypointList [waypointList.Count - 1].waypointTransform.position + waypointList [waypointList.Count - 1].waypointTransform.forward;
        }

        GameObject newWayPoint = new GameObject ();
        newWayPoint.transform.SetParent (transform);
        newWayPoint.transform.position = newPosition;
        newWayPoint.name = (waypointList.Count + 1).ToString ();

        cameraWaypointInfo newCameraWaypointInfo = new cameraWaypointInfo ();
        newCameraWaypointInfo.Name = newWayPoint.name;
        newCameraWaypointInfo.waypointTransform = newWayPoint.transform;
        newCameraWaypointInfo.rotateCameraToNextWaypoint = true;

        waypointList.Add (newCameraWaypointInfo);

        updateComponent ();
    }

    public void addNewWayPoint (int insertAtIndex)
    {
        GameObject newWayPoint = new GameObject ();
        newWayPoint.transform.SetParent (transform);
        newWayPoint.name = (waypointList.Count + 1).ToString ();

        cameraWaypointInfo newCameraWaypointInfo = new cameraWaypointInfo ();
        newCameraWaypointInfo.Name = newWayPoint.name;
        newCameraWaypointInfo.waypointTransform = newWayPoint.transform;
        newCameraWaypointInfo.rotateCameraToNextWaypoint = true;

        if (waypointList.Count > 0) {
            Vector3 lastPosition = waypointList [waypointList.Count - 1].waypointTransform.position + waypointList [waypointList.Count - 1].waypointTransform.forward;
            newWayPoint.transform.localPosition = lastPosition + waypointList [waypointList.Count - 1].waypointTransform.forward * 2;
        } else {
            newWayPoint.transform.localPosition = Vector3.zero;
        }

        if (insertAtIndex > -1) {
            if (waypointList.Count > 0) {
                newWayPoint.transform.localPosition = waypointList [insertAtIndex].waypointTransform.localPosition + waypointList [insertAtIndex].waypointTransform.forward * 2;
            }

            waypointList.Insert (insertAtIndex + 1, newCameraWaypointInfo);

            newWayPoint.transform.SetSiblingIndex (insertAtIndex + 1);

            renameAllWaypoints ();
        } else {
            waypointList.Add (newCameraWaypointInfo);
        }

        updateComponent ();
    }

    public void renameAllWaypoints ()
    {
        for (int i = 0; i < waypointList.Count; i++) {
            if (waypointList [i].waypointTransform != null) {
                waypointList [i].waypointTransform.name = (i + 1).ToString ("000");
                waypointList [i].Name = (i + 1).ToString ("000");
            }
        }

        updateComponent ();
    }

    public void removeWaypoint (int index)
    {
        if (waypointList [index].waypointTransform != null) {
            DestroyImmediate (waypointList [index].waypointTransform.gameObject);
        }

        waypointList.RemoveAt (index);

        updateComponent ();
    }

    public void removeAllWaypoints ()
    {
        for (int i = 0; i < waypointList.Count; i++) {
            if (waypointList [i].waypointTransform != null) {
                DestroyImmediate (waypointList [i].waypointTransform.gameObject);
            }
        }

        waypointList.Clear ();

        updateComponent ();
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Camera Waypoin System", gameObject);
    }

    void OnDrawGizmos ()
    {
        if (!showGizmo) {
            return;
        }

        if (GKC_Utils.isCurrentSelectionActiveGameObject (gameObject)) {
            DrawGizmos ();
        }
    }

    void OnDrawGizmosSelected ()
    {
        DrawGizmos ();
    }

    void DrawGizmos ()
    {
        if (showGizmo) {
            if (waypointList.Count > 0) {
                if (waypointList [0].waypointTransform != null) {
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine (waypointList [0].waypointTransform.position, transform.position);
                }
            }

            for (i = 0; i < waypointList.Count; i++) {
                if (waypointList [i].waypointTransform != null) {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere (waypointList [i].waypointTransform.position, gizmoRadius);

                    if (i + 1 < waypointList.Count) {
                        Gizmos.color = Color.white;
                        Gizmos.DrawLine (waypointList [i].waypointTransform.position, waypointList [i + 1].waypointTransform.position);
                    }

                    if (currentWaypoint != null) {
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere (currentWaypoint.position, gizmoRadius);
                    }

                    if (waypointList [i].usePointToLook && waypointList [i].pointToLook != null) {
                        Gizmos.color = Color.green;
                        Gizmos.DrawLine (waypointList [i].waypointTransform.position, waypointList [i].pointToLook.position);
                        Gizmos.color = Color.blue;
                        Gizmos.DrawSphere (waypointList [i].pointToLook.position, gizmoRadius);
                    }
                }
            }
        }
    }

    [System.Serializable]
    public class cameraWaypointInfo
    {
        public string Name;
        public Transform waypointTransform;

        public bool rotateCameraToNextWaypoint;
        public bool usePointToLook;
        public Transform pointToLook;

        public bool smoothTransitionToNextPoint = true;
        public bool useCustomMovementSpeed;
        public float movementSpeed;
        public bool useCustomRotationSpeed;
        public float rotationSpeed;

        public bool checkRotationIsReached;

        public float timeOnFixedPosition;

        public bool useCustomWaitTimeBetweenPoint;
        public float waitTimeBetweenPoints;

        public bool useEventOnPointReached;
        public UnityEvent eventOnPointReached;
    }
}
