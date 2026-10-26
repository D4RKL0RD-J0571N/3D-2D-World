using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class pushObjectsPower : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool powerEnabled = true;

    public LayerMask layer;

    [Space]

    public bool pushObjectsFromCenterPosition;

    public bool pushObjectsFromPlayerForwardDirection;

    [Space]

    public bool useMaxAngleWithCameraFaceDirection;

    public float maxAngleWithCameraFaceDirection;

    [Space]

    public bool useRaycastToPushObjects;

    public LayerMask layerForRaycastSurfaceDetection;

    public bool useMaxAngleWithPlayerFaceDirection;

    public float maxAngleWithPlayerFaceDirection;

    public float maxRaycastDistance;

    [Space]

    public List<string> ignoreTagList = new List<string> ();

    public bool useMessageToPushCharactersFound = true;
    public string messageNameToSend = "pushCharacter";

    public float pushCharacterForceMultiplier = 1;

    [Space]
    [Header ("Damage Settings")]
    [Space]

    public bool applyDamageOnFoundObjects;
    public float damageToApply;
    public bool ignoreShield;
    public bool canActivateReactionSystemTemporally;
    public int damageReactionID = -1;

    public int damageTypeID = -1;

    public bool damageCanBeBlocked = true;

    [Space]
    [Header ("Force Settings")]
    [Space]

    public float forceToApply = 4000;
    public ForceMode forceMode;

    public bool canApplyForceToVehicles = true;
    public float applyForceToVehiclesMultiplier = 0.2f;

    public bool applyForceToFoundObjectsOnlyOnce;

    public bool checkRagdollsDetected;
    public float ragdollMultiplierForce = 1;

    [Space]
    [Header ("Others Settings")]
    [Space]

    public bool useCustomPushCenterDistance;
    public float pushCenterDistance;

    public bool searchForObjectsOnUpdate;

    public bool checkIfObjectInFrontOfPlayer;

    [Space]
    [Header ("Magnetize Objects To AI Settings")]
    [Space]

    public bool magnetizeObjectToCloseAIEnemies;
    public string mountPointNameToMagnetize;
    public float magnetizeForceAmount;
    public float maxDistanceToMagnetize;
    public bool magnetizeOnlyLockOnAI;

    [Space]

    public bool addLaunchedObjectComponentToDetectedObjects;

    [Space]

    public bool useEventsOnFoundObjectToMagnetizeResult;
    public UnityEvent eventOnFoundObjectToMagnetize;
    public UnityEvent eventOnNotFoundObjectToMagnetize;

    [Space]
    [Header ("Remote Events Settings")]
    [Space]

    public bool useRemoteEventOnObjectsFound;
    public List<string> remoteEventNameList = new List<string> ();

    [Space]

    public bool callRemoteEventsBeforeApplyingForce;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool useOfPowerPaused;

    public bool showGizmo;

    public bool updateCoroutineActive;

    [Space]

    public List<Rigidbody> vehiclesRigidbodyFoundList = new List<Rigidbody> ();

    public List<GameObject> gameObjectsFoundList = new List<GameObject> ();

    [Space]
    [Header ("Components")]
    [Space]

    public playerWeaponSystem mainPlayerWeaponSystem;
    public GameObject playerGameObject;
    public Transform centerPosition;
    public otherPowers powersManager;
    public Transform pushObjectsCenter;
    public Transform mainCameraTransform;

    public playerCamera mainPlayerCamera;
    public AIAroundManager mainAIAroundManager;
    public Camera mainCamera;

    Rigidbody objectToDamageMainRigidbody;

    GameObject objectToPush;
    Collider [] colliders;
    Vector3 currentForceToApply;
    float finalExplosionForce;
    bool isVehicle;

    bool componentsInitialized;

    bool anyObjectToMagnetizeFound;

    //bool anyObjectToPushFound;

    Coroutine updateCoroutine;

    bool mainPlayerCameraLocated;


    void Start ()
    {
        if (!useCustomPushCenterDistance) {
            //get the distance from the empty object in the player to push objects, close to it
            pushCenterDistance = GKC_Utils.distance (playerGameObject.transform.position, pushObjectsCenter.position);
        }

        if (pushObjectsFromCenterPosition) {
            if (centerPosition == null) {
                centerPosition = transform;
            }
        }

        mainPlayerCameraLocated = mainPlayerCamera != null;
    }

    //void Update ()
    //{
    //    if (searchForObjectsOnUpdate) {
    //        activatePower ();
    //    }
    //}

    public void stopUpdateCoroutine ()
    {
        if (updateCoroutineActive) {
            StopCoroutine (updateCoroutine);
        }

        updateCoroutineActive = false;
    }

    IEnumerator updateSystemCoroutine ()
    {
        var waitTime = new WaitForSecondsRealtime (0.0001f);

        while (true) {
            updateSystem ();

            yield return waitTime;
        }
    }

    void updateSystem ()
    {
        if (searchForObjectsOnUpdate) {
            activatePower ();
        }
    }

    public void activatePowerWithUpdate ()
    {
        if (searchForObjectsOnUpdate) {
            if (powerEnabled) {
                updateCoroutine = StartCoroutine (updateSystemCoroutine ());

                updateCoroutineActive = true;
            }
        } else {
            if (powerEnabled) {
                activatePower ();
            }
        }
    }

    public void deactivatePowerWithUpdate ()
    {
        if (searchForObjectsOnUpdate) {
            if (powerEnabled) {
                stopUpdateCoroutine ();
            }
        }
    }

    public void activatePower ()
    {
        if (useOfPowerPaused) {
            return;
        }

        if (!powerEnabled) {
            return;
        }

        initializeComponents ();

        vehiclesRigidbodyFoundList.Clear ();

        if (powersManager != null) {
            //the power number 2 is push objects, so any bullet is created
            powersManager.createShootParticles ();
        }

        anyObjectToMagnetizeFound = false;

        //anyObjectToPushFound = false;

        //if the power selected is push objects, check the objects close to pushObjectsCenter and add force to them in camera forward direction
        colliders = Physics.OverlapSphere (pushObjectsCenter.position, pushCenterDistance, layer);

        for (int i = 0; i < colliders.Length; i++) {
            if (!colliders [i].isTrigger) {
                objectToPush = colliders [i].gameObject;

                checkObjectToApplyForce (objectToPush);
            }
        }

        if (magnetizeObjectToCloseAIEnemies) {
            checkEventsOnFoundObjectToMagnetizeResult (anyObjectToMagnetizeFound);
        }
    }

    public void checkObjectToApplyForce (GameObject currentObject)
    {
        if (applyForceToFoundObjectsOnlyOnce) {
            if (!gameObjectsFoundList.Contains (currentObject)) {
                gameObjectsFoundList.Add (currentObject);
            } else {
                return;
            }
        }

        if (!ignoreTagList.Contains (currentObject.tag) && currentObject != playerGameObject) {
            if (playerGameObject == null) {
                playerGameObject = gameObject;
            }

            if (checkIfObjectInFrontOfPlayer) {
                float dot = Vector3.Dot (playerGameObject.transform.forward, (currentObject.transform.position - playerGameObject.transform.position).normalized);
                if (dot < 0) {
                    return;
                }
            }

            bool lockedCameraActive = false;

            if (mainPlayerCameraLocated) {
                lockedCameraActive = !mainPlayerCamera.isCameraTypeFree ();
            }

            Vector3 pushDirection = Vector3.zero;

            if (pushObjectsFromCenterPosition) {
                pushDirection = (currentObject.transform.position - centerPosition.position).normalized;
            } else {
                if (pushObjectsFromPlayerForwardDirection) {
                    pushDirection = playerGameObject.transform.forward;
                } else {
                    if (mainCameraTransform != null) {
                        pushDirection = mainCameraTransform.forward;
                    }

                    if (!lockedCameraActive && useMaxAngleWithCameraFaceDirection) {
                        float angle = Vector3.Angle (playerGameObject.transform.forward, mainCameraTransform.forward);

                        if (angle > maxAngleWithCameraFaceDirection) {
                            currentForceToApply = playerGameObject.transform.forward;
                        }
                    }
                }
            }

            if (useMessageToPushCharactersFound && messageNameToSend != "") {
                currentObject.SendMessage (messageNameToSend, pushDirection * pushCharacterForceMultiplier, SendMessageOptions.DontRequireReceiver);
            }

            if (applyDamageOnFoundObjects) {
                applyDamage.checkHealth (playerGameObject, currentObject, damageToApply, playerGameObject.transform.forward, transform.position, playerGameObject,
                    false, true, ignoreShield, false, damageCanBeBlocked, canActivateReactionSystemTemporally, damageReactionID, damageTypeID);
            }

            objectToDamageMainRigidbody = applyDamage.applyForce (currentObject);

            if (showDebugPrint) {
                print ("object detected rigidbody result " + currentObject.name + "  " + (objectToDamageMainRigidbody != null));
            }

            bool rigidbodyFound = false;

            bool objectMagnetizedToAIResult = false;

            if (canApplyForceToVehicles) {
                if (objectToDamageMainRigidbody != null) {

                    if (!vehiclesRigidbodyFoundList.Contains (objectToDamageMainRigidbody)) {

                        if (callRemoteEventsBeforeApplyingForce) {
                            checkRemoteEvent (objectToDamageMainRigidbody.gameObject);
                        }

                        isVehicle = applyDamage.isVehicle (currentObject);

                        finalExplosionForce = forceToApply;

                        if (isVehicle) {
                            finalExplosionForce *= applyForceToVehiclesMultiplier;
                        }

                        if (pushObjectsFromCenterPosition) {
                            currentForceToApply = (objectToDamageMainRigidbody.position - centerPosition.position).normalized;
                        } else {
                            if (pushObjectsFromPlayerForwardDirection) {
                                currentForceToApply = playerGameObject.transform.forward;
                            } else {
                                currentForceToApply = mainCameraTransform.TransformDirection (Vector3.forward);

                                if (!lockedCameraActive && useMaxAngleWithCameraFaceDirection) {
                                    float angle = Vector3.Angle (playerGameObject.transform.forward, mainCameraTransform.forward);

                                    if (angle > maxAngleWithCameraFaceDirection) {
                                        currentForceToApply = playerGameObject.transform.forward;
                                    }
                                }
                            }
                        }

                        currentForceToApply *= finalExplosionForce;

                        if (checkRagdollsDetected) {
                            if (applyDamage.isRagdollActive (currentObject)) {
                                currentForceToApply *= ragdollMultiplierForce;
                            }
                        }

                        if (useRaycastToPushObjects) {
                            checkIfUseRaycastToPushObjects (objectToDamageMainRigidbody.gameObject);
                        }

                        if (magnetizeObjectToCloseAIEnemies) {
                            objectMagnetizedToAIResult = checkIfMagnetizeObjectToAI (objectToDamageMainRigidbody.gameObject);

                            if (showDebugPrint) {
                                print ("objectMagnetizedToAIResult " + objectMagnetizedToAIResult);
                            }
                        }

                        if (currentForceToApply != Vector3.zero) {
                            objectToDamageMainRigidbody.AddForce (currentForceToApply * objectToDamageMainRigidbody.mass, forceMode);

                            if (showDebugPrint) {
                                print ("object to apply force " + objectToDamageMainRigidbody.name + " " + currentForceToApply);
                            }

                            if (showGizmo) {
                                Debug.DrawRay (objectToDamageMainRigidbody.position, currentForceToApply.normalized * 10, Color.red, 3);
                            }

                            if (addLaunchedObjectComponentToDetectedObjects) {
                                checkIfAttackLaunchedObjectComponent (objectToDamageMainRigidbody.gameObject);
                            }
                        }

                        if (isVehicle) {
                            vehiclesRigidbodyFoundList.Add (objectToDamageMainRigidbody);
                        }

                        if (!callRemoteEventsBeforeApplyingForce) {
                            checkRemoteEvent (objectToDamageMainRigidbody.gameObject);
                        }

                        rigidbodyFound = true;
                    }
                }
            } else {
                if (applyDamage.canApplyForce (currentObject)) {
                    if (callRemoteEventsBeforeApplyingForce) {
                        checkRemoteEvent (currentObject);
                    }

                    if (pushObjectsFromCenterPosition) {
                        currentForceToApply = (objectToDamageMainRigidbody.position - centerPosition.position).normalized;
                    } else {
                        if (pushObjectsFromPlayerForwardDirection) {
                            currentForceToApply = playerGameObject.transform.forward;
                        } else {
                            currentForceToApply = mainCameraTransform.TransformDirection (Vector3.forward);

                            if (!lockedCameraActive && useMaxAngleWithCameraFaceDirection) {
                                float angle = Vector3.Angle (playerGameObject.transform.forward, mainCameraTransform.forward);

                                if (angle > maxAngleWithCameraFaceDirection) {
                                    currentForceToApply = playerGameObject.transform.forward;
                                }
                            }
                        }
                    }

                    currentForceToApply *= forceToApply;

                    if (useRaycastToPushObjects) {
                        checkIfUseRaycastToPushObjects (objectToDamageMainRigidbody.gameObject);
                    }

                    if (magnetizeObjectToCloseAIEnemies) {
                        objectMagnetizedToAIResult = checkIfMagnetizeObjectToAI (currentObject);

                        if (showDebugPrint) {
                            print ("objectMagnetizedToAIResult " + objectMagnetizedToAIResult);
                        }
                    }

                    if (currentForceToApply != Vector3.zero) {
                        objectToDamageMainRigidbody = currentObject.GetComponent<Rigidbody> ();

                        objectToDamageMainRigidbody.AddForce (currentForceToApply * objectToDamageMainRigidbody.mass, forceMode);

                        if (addLaunchedObjectComponentToDetectedObjects) {
                            checkIfAttackLaunchedObjectComponent (objectToDamageMainRigidbody.gameObject);
                        }

                        if (showDebugPrint) {
                            print ("object to apply force " + objectToDamageMainRigidbody.name + " " + currentForceToApply);
                        }

                        if (showGizmo) {
                            Debug.DrawRay (objectToDamageMainRigidbody.position, currentForceToApply.normalized * 10, Color.red, 3);
                        }
                    }

                    if (!callRemoteEventsBeforeApplyingForce) {
                        checkRemoteEvent (currentObject);
                    }

                    rigidbodyFound = true;
                }
            }

            if (rigidbodyFound) {
                if (magnetizeObjectToCloseAIEnemies) {
                    if (objectMagnetizedToAIResult) {
                        anyObjectToMagnetizeFound = true;
                    }
                }

                //anyObjectToPushFound = true;
            } else {
                checkRemoteEvent (currentObject);
            }
        }
    }

    bool checkIfMagnetizeObjectToAI (GameObject currentObject)
    {
        if (magnetizeObjectToCloseAIEnemies) {
            Vector3 screenPoint;

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            List<Transform> charactersAround = mainAIAroundManager.getCharactersAround ();

            if (charactersAround.Count == 0) {
                return false;
            }

            if (charactersAround.Contains (currentObject.transform)) {
                return false;
            }

            int charactersAroundCount = charactersAround.Count;

            bool usingScreenSpaceCamera = mainPlayerCamera.isUsingScreenSpaceCamera ();

            float minDistance = Mathf.Infinity;

            int closestTargetIndex = -1;

            Vector3 currentPosition = Vector3.zero;

            Transform currentTargetToLook = mainPlayerCamera.getCurrentTargetToLook ();

            if (pushObjectsFromCenterPosition) {
                currentPosition = centerPosition.position;
            } else {
                if (pushObjectsFromPlayerForwardDirection) {
                    currentPosition = playerGameObject.transform.position;
                } else {
                    if (mainCameraTransform != null) {
                        currentPosition = mainCameraTransform.position;
                    }
                }
            }

            for (int i = 0; i < charactersAroundCount; i++) {
                bool objectVisible = false;

                bool targetOnScreen = false;

                GameObject currentTarget = charactersAround [i].gameObject;

                Vector3 targetPosition = currentTarget.transform.position;

                Transform currentTargetPlaceToShoot = applyDamage.getPlaceToShoot (currentTarget);

                if (currentTargetPlaceToShoot != null) {
                    targetPosition = currentTargetPlaceToShoot.position;
                }

                if (usingScreenSpaceCamera) {
                    screenPoint = mainCamera.WorldToViewportPoint (targetPosition);
                    targetOnScreen = screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1;
                } else {
                    screenPoint = mainCamera.WorldToScreenPoint (targetPosition);
                    targetOnScreen = screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < screenWidth && screenPoint.y > 0 && screenPoint.y < screenHeight;
                }

                //the target is visible in the screen
                if (targetOnScreen) {
                    objectVisible = true;
                }

                if (objectVisible) {
                    bool checkDistanceResult = true;

                    if (magnetizeOnlyLockOnAI) {
                        if (currentTargetToLook != null) {
                            if (currentTargetToLook != currentTarget) {
                                checkDistanceResult = false;
                            }
                        } else {
                            checkDistanceResult = false;
                        }
                    }

                    if (checkDistanceResult) {
                        float currentDistance = GKC_Utils.distance (currentPosition, targetPosition);

                        if (currentDistance < maxDistanceToMagnetize) {
                            if (currentDistance < minDistance) {
                                minDistance = currentDistance;

                                closestTargetIndex = i;
                            }
                        }
                    }
                }
            }

            if (closestTargetIndex > -1) {
                Transform currentMountPoint = GKC_Utils.getMountPointTransformByName (mountPointNameToMagnetize, charactersAround [closestTargetIndex]);

                if (currentMountPoint != null) {
                    Vector3 pushDirection = (currentMountPoint.position - currentObject.transform.position).normalized;

                    currentForceToApply = pushDirection * magnetizeForceAmount;
                }

                return true;
            } else {
                return false;
            }
        }

        return false;
    }

    void checkIfAttackLaunchedObjectComponent (GameObject currentObject)
    {
        if (addLaunchedObjectComponentToDetectedObjects) {
            launchedObjects currentLaunchedObjects = currentObject.GetComponent<launchedObjects> ();

            if (currentLaunchedObjects == null) {
                currentLaunchedObjects = currentObject.AddComponent<launchedObjects> ();
            }

            if (currentLaunchedObjects != null) {
                currentLaunchedObjects.setCurrentPlayer (playerGameObject);
            }
        }
    }

    void checkIfUseRaycastToPushObjects (GameObject currentObject)
    {
        if (useRaycastToPushObjects) {

            bool lockedCameraActive = !mainPlayerCamera.isCameraTypeFree ();

            if (useMaxAngleWithPlayerFaceDirection) {
                if (!lockedCameraActive) {
                    float angle = Vector3.Angle (playerGameObject.transform.forward, mainPlayerCamera.transform.forward);

                    if (showDebugPrint) {
                        print ("useMaxAngleWithPlayerFaceDirection " + angle);
                    }

                    if (angle > maxAngleWithPlayerFaceDirection) {
                        return;
                    }
                }
            }

            RaycastHit hit;

            Vector3 raycastPosition = mainCameraTransform.position;
            Vector3 raycastDirection = mainCameraTransform.forward;

            if (lockedCameraActive) {
                raycastPosition = playerGameObject.transform.position + playerGameObject.transform.up;
                raycastDirection = playerGameObject.transform.forward;
            }

            Vector3 surfacePoint = Vector3.zero;

            if (Physics.Raycast (raycastPosition, raycastDirection, out hit, maxRaycastDistance, layerForRaycastSurfaceDetection)) {
                surfacePoint = hit.point;

                if (showDebugPrint) {
                    print ("surface by raycast found");
                }
            } else {
                surfacePoint = raycastPosition + raycastDirection * maxRaycastDistance;

                if (showDebugPrint) {
                    print ("surface by raycast not found");
                }
            }

            currentForceToApply = (surfacePoint - currentObject.transform.position).normalized * finalExplosionForce;
        }
    }

    public void setPowerEnabledState (bool state)
    {
        powerEnabled = state;

        initializeComponents ();

        if (powerEnabled) {
            gameObjectsFoundList.Clear ();
        }

        if (searchForObjectsOnUpdate) {
            if (powerEnabled) {
                updateCoroutine = StartCoroutine (updateSystemCoroutine ());

                updateCoroutineActive = true;
            } else {
                stopUpdateCoroutine ();
            }
        }
    }

    public void setPowerEnabledStateOneFrame ()
    {
        setPowerEnabledState (true);

        activatePower ();

        setPowerEnabledState (false);
    }

    public void cleanGameObjectFoundList ()
    {
        gameObjectsFoundList.Clear ();
    }

    public void checkRemoteEvent (GameObject objectToCheck)
    {
        if (useRemoteEventOnObjectsFound) {
            remoteEventSystem currentRemoteEventSystem = GKC_Utils.getRemoteEventSystemFromObject (objectToCheck, true);

            if (currentRemoteEventSystem != null) {
                int remoteEventNameListCount = remoteEventNameList.Count;

                for (int i = 0; i < remoteEventNameListCount; i++) {

                    currentRemoteEventSystem.callRemoteEvent (remoteEventNameList [i]);
                }
            }
        }
    }

    public void setUseRemoteEventOnObjectsFoundState (bool state)
    {
        useRemoteEventOnObjectsFound = state;
    }

    public void setUseMessageToPushCharactersFoundState (bool state)
    {
        useMessageToPushCharactersFound = state;
    }

    public void setUseOfPowerPausedState (bool state)
    {
        useOfPowerPaused = state;
    }

    public void setNewPushCenterDistance (float newValue)
    {
        pushCenterDistance = newValue;
    }

    void checkEventsOnFoundObjectToMagnetizeResult (bool state)
    {
        if (useEventsOnFoundObjectToMagnetizeResult) {
            if (state) {
                eventOnFoundObjectToMagnetize.Invoke ();
            } else {
                eventOnNotFoundObjectToMagnetize.Invoke ();
            }
        }
    }

    void initializeComponents ()
    {
        if (componentsInitialized) {
            return;
        }

        if (mainPlayerWeaponSystem != null) {
            playerGameObject = mainPlayerWeaponSystem.getPlayerWeaponsManger ().gameObject;

            if (playerGameObject != null) {
                playerComponentsManager mainPlayerComponentsManager = playerGameObject.GetComponent<playerComponentsManager> ();

                if (mainPlayerComponentsManager != null) {
                    powersManager = mainPlayerComponentsManager.getOtherPowers ();

                    mainCameraTransform = mainPlayerComponentsManager.getPlayerCamera ().getCameraTransform ();
                }
            }
        }

        componentsInitialized = true;
    }
}
