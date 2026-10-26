using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GameKitController.Audio;
using UnityEngine.Events;

public class AITurret : MonoBehaviour
{
    [Header ("Turret Settings")]
    [Space]

    public LayerMask layer;

    public float rotationSpeed = 10;

    [Space]

    public float inputRotationSpeed = 5;

    public Vector2 rangeAngleX;
    public float overrideRotationSpeed = 10;

    public float raycastPositionRotationSpeed = 10;

    [Space]
    [Header ("Sound Settings")]
    [Space]

    public AudioClip locatedEnemy;
    public AudioElement locatedEnemyAudioElement;

    [Space]
    [Header ("Weapons Settings")]
    [Space]

    public string defaultWeaponActiveName;

    public bool randomWeaponAtStart;

    [Space]

    public bool useGeneralLayerToDamage;
    public LayerMask layerToDamage;

    [Space]

    public List<turretWeaponInfo> turretWeaponInfoList = new List<turretWeaponInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool dead;
    public bool shootingWeapons;
    public bool weaponsActive;

    public Transform currentCameraTransformDirection;

    public string currentWeaponName;

    public int currentWeaponIndex;

    [Space]
    [Header ("Override Elements")]
    [Space]

    public bool controlOverriden;

    public bool useCustomInputCameraDirection;

    public Transform overridePositionToLook;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public Shader transparent;
    public LayerMask layerForGravity;

    [Space]

    public bool fadePiecesOnDeath = true;

    public float fadePiecesSpeed = 5;

    public float destroyPiecesWaitTime = 10;

    [Space]

    public bool addForceToTurretPiecesOnDeath = true;

    public float forceAmountToPieces = 500;

    public float radiusAmountToPieces = 50;

    [Space]
    [Header ("Turret Elements")]
    [Space]

    public GameObject rayCastPosition;

    public GameObject rotatingBase;
    public GameObject head;

    public manageAITarget targetManager;
    public Rigidbody mainRigidbody;

    public AudioSource audioSource;
    public overrideInputManager overrideInput;

    public GameObject turretAttacker;


    Quaternion currentRaycastPositionRotation;

    Vector2 currentLookAngle;
    Vector2 axisValues;
    float horizontalInput;
    float verticalInput;

    RaycastHit hit;


    List<Renderer> rendererParts = new List<Renderer> ();

    bool kinematicActive;
    float timer;
    float shootTimerLimit;

    float orignalRotationSpeed;
    float speedMultiplier = 1;

    GameObject currentEnemyToShoot;

    float currentRotationSpeed;

    float lastTimeWeaponsActivated;

    string untaggedName = "Untagged";

    Coroutine disableOverrideCoroutine;

    turretWeaponInfo currentTurretWeaponInfo;

    bool weaponInfoAssigned;

    float lastTimeTurretDestroyed;


    private void InitializeAudioElements ()
    {
        if (locatedEnemy != null) {
            locatedEnemyAudioElement.clip = locatedEnemy;
        }

        if (audioSource != null) {
            locatedEnemyAudioElement.audioSource = audioSource;
        }

        currentWeaponName = defaultWeaponActiveName;
    }

    void Start ()
    {
        InitializeAudioElements ();

        if (turretAttacker == null) {
            turretAttacker = gameObject;
        }

        currentRotationSpeed = rotationSpeed;
        orignalRotationSpeed = rotationSpeed;

        if (randomWeaponAtStart) {
            int randomIndex = Random.Range (0, turretWeaponInfoList.Count);

            currentWeaponName = turretWeaponInfoList [randomIndex].Name;
        }
    }

    void Update ()
    {
        if (dead) {
            if (fadePiecesOnDeath) {
                //if the turrets is destroyed, set it to transparent smoothly to disable it from the scene
                int rendererPartsCount = rendererParts.Count;

                int piecesFadedCounter = 0;

                for (int i = 0; i < rendererPartsCount; i++) {

                    Color alpha = rendererParts [i].material.color;

                    alpha.a -= Time.deltaTime / fadePiecesSpeed;

                    rendererParts [i].material.color = alpha;

                    if (alpha.a <= 0) {
                        piecesFadedCounter++;
                    }
                }

                if (piecesFadedCounter >= rendererPartsCount) {
                    Destroy (gameObject);
                }
            }

            if (destroyPiecesWaitTime > 0) {
                if (Time.time > destroyPiecesWaitTime + lastTimeTurretDestroyed) {
                    Destroy (gameObject);
                }
            }
        }

        //if the turret is not destroyed, or being hacked, or paused by a black hole, then
        if (!dead && !targetManager.paused) {
            if (targetManager.onSpotted) {
                lootAtTurretTarget (targetManager.placeToShoot);

                shootWeapon (targetManager.enemyToShoot, targetManager.placeToShoot, true);

            }
            //if there are no enemies in the field of view, rotate in Y local axis to check new targets
            else if (!targetManager.checkingThreat) {
                rotatingBase.transform.Rotate (0, currentRotationSpeed * Time.deltaTime * 3, 0);
            }
        }

        //if the turret has been hacked, the player can grab it, so when he drops it, the turret will be set in the first surface that will touch
        //also checking if the gravity of the turret has been modified
        if (tag.Equals (untaggedName)) {
            if (!mainRigidbody.isKinematic && !mainRigidbody.freezeRotation) {
                mainRigidbody.freezeRotation = true;

                StartCoroutine (rotateElement (gameObject));
            }
        } else {
            if (mainRigidbody.freezeRotation) {
                mainRigidbody.freezeRotation = false;
                kinematicActive = true;
            }
        }

        //when the kinematicActive has been enabled, the turret has a regular gravity again, so the first ground surface that will find, will be its new ground
        //enabling the kinematic rigidbody of the turret
        if (kinematicActive) {
            if (Physics.Raycast (transform.position, -Vector3.up, out hit, 1.2f, layerForGravity)) {
                if (!mainRigidbody.isKinematic && kinematicActive && !GetComponent<artificialObjectGravity> () && !hit.collider.isTrigger) {
                    StartCoroutine (rotateToSurface (hit));
                }
            }
        }

        if (controlOverriden) {

            if (shootingWeapons) {
                if (Physics.Raycast (rayCastPosition.transform.position, rayCastPosition.transform.forward, out hit, Mathf.Infinity, layer)) {
                    currentEnemyToShoot = hit.collider.gameObject;
                } else {
                    currentEnemyToShoot = null;
                }

                shootWeapon (currentEnemyToShoot, overridePositionToLook.transform, false);
            }

            if (useCustomInputCameraDirection) {
                currentRaycastPositionRotation = Quaternion.LookRotation (currentCameraTransformDirection.forward);
            } else {
                axisValues = overrideInput.getCustomMovementAxis ();
                horizontalInput = axisValues.x;
                verticalInput = axisValues.y;

                currentLookAngle.x += horizontalInput * inputRotationSpeed;
                currentLookAngle.y -= verticalInput * inputRotationSpeed;

                currentLookAngle.y = Mathf.Clamp (currentLookAngle.y, rangeAngleX.x, rangeAngleX.y);

                currentRaycastPositionRotation = Quaternion.Euler (0, currentLookAngle.x, 0);

                currentRaycastPositionRotation *= Quaternion.Euler (currentLookAngle.y, 0, 0);
            }

            rayCastPosition.transform.rotation = Quaternion.Slerp (rayCastPosition.transform.rotation, currentRaycastPositionRotation, Time.deltaTime * raycastPositionRotationSpeed);

            lootAtTurretTarget (overridePositionToLook);
        }
    }

    public void chooseNextWeapon ()
    {
        currentWeaponIndex++;

        if (currentWeaponIndex >= turretWeaponInfoList.Count - 1) {
            currentWeaponIndex = 0;
        }

        setWeapon (currentWeaponIndex);

        activateWeapon ();
    }

    public void choosePreviousWeapon ()
    {
        currentWeaponIndex--;

        if (currentWeaponIndex < 0) {
            currentWeaponIndex = turretWeaponInfoList.Count - 1;
        }

        setWeapon (currentWeaponIndex);

        activateWeapon ();
    }

    public void enableOrDisableWeapons (bool state)
    {
        weaponsActive = state;

        if (weaponsActive) {
            activateWeapon ();
        } else {
            deactivateWeapon ();
        }
    }

    void cancelCheckSuspectTurret ()
    {
        setOnSpottedState (false);
    }

    public void setOnSpottedState (bool state)
    {
        if (state) {
            activateWeapon ();
        } else {
            StartCoroutine (rotateElement (head));

            deactivateWeapon ();
        }
    }

    //active the fire mode
    void shootWeapon (GameObject enemyToShoot, Transform placeToShoot, bool checkTargetOnRaycast)
    {
        //if the current weapon is the machine gun or the cannon, check with a ray if the player is in front of the turret
        //if the cannon is selected, the time to shoot is 1 second, the machine gun shoots every 0.1 seconds
        bool canCheckRaycastResult = true;

        if (weaponInfoAssigned) {
            if (currentTurretWeaponInfo.useWaitTimeToStartShootAfterActivation) {
                if (Time.time < currentTurretWeaponInfo.waitTimeToStartShootAfterActivation + lastTimeWeaponsActivated) {
                    canCheckRaycastResult = false;
                }
            }
        }

        if (canCheckRaycastResult) {
            if (checkTargetOnRaycast) {
                if (Physics.Raycast (rayCastPosition.transform.position, rayCastPosition.transform.forward, out hit, Mathf.Infinity, layer)) {
                    Debug.DrawLine (rayCastPosition.transform.position, hit.point, Color.red, 200, true);

                    if (hit.collider.gameObject == enemyToShoot || hit.collider.gameObject.transform.IsChildOf (enemyToShoot.transform)) {
                        timer += Time.deltaTime * speedMultiplier;
                    }
                }
            } else {
                timer += Time.deltaTime * speedMultiplier;
            }
        }

        if (weaponInfoAssigned) {
            if (currentTurretWeaponInfo.weaponLookAtTarget) {
                Vector3 targetDir = placeToShoot.position - currentTurretWeaponInfo.weaponLookAtTargetTransform.position;

                Quaternion qTo = Quaternion.LookRotation (targetDir);

                currentTurretWeaponInfo.weaponLookAtTargetTransform.rotation =
                    Quaternion.Slerp (currentTurretWeaponInfo.weaponLookAtTargetTransform.rotation, qTo, currentRotationSpeed * Time.deltaTime);
            }

            //if the timer ends, shoot
            if (timer >= shootTimerLimit) {
                timer = 0;

                //if (controlOverriden) {
                //    if (currentCameraTransformDirection != null) {
                //        bool surfaceFound = false;

                //        Vector3 raycastDirection = currentCameraTransformDirection.forward;

                //        if (Physics.Raycast (currentCameraTransformDirection.position, raycastDirection, out hit, Mathf.Infinity, layer)) {

                //            if (hit.collider.gameObject != turretAttacker) {
                //                surfaceFound = true;
                //            } else {
                //                Vector3 raycastPosition = hit.point + 0.2f * raycastDirection;

                //                if (Physics.Raycast (raycastPosition, raycastDirection, out hit, Mathf.Infinity, layer)) {
                //                    surfaceFound = true;
                //                }
                //            }
                //        }

                //        if (surfaceFound) {
                //            newProjectileGameObject.transform.LookAt (hit.point);
                //        }
                //    }
                //}

                if (currentTurretWeaponInfo.useAnimationOnWeapon) {
                    currentTurretWeaponInfo.animationOnWeapon.Play (currentTurretWeaponInfo.weaponAnimationName);
                }

                if (currentTurretWeaponInfo.useSimpleWeaponSystem) {
                    currentTurretWeaponInfo.mainSimpleWeaponSystem.shootWeapon (true);
                }
            }
        }
    }

    //follow the enemy position, to rotate torwards his direction
    void lootAtTurretTarget (Transform objective)
    {
        if (objective != null) {
            //there are two parts in the turret that move, the head and the middle body
            Vector3 targetDir = objective.position - rotatingBase.transform.position;

            targetDir = targetDir - transform.InverseTransformDirection (targetDir).y * transform.up;

            targetDir = targetDir.normalized;

            Quaternion targetRotation = Quaternion.LookRotation (targetDir, transform.up);

            rotatingBase.transform.rotation = Quaternion.Slerp (rotatingBase.transform.rotation, targetRotation, currentRotationSpeed * Time.deltaTime);

            Vector3 targetDir2 = objective.position - head.transform.position;

            Quaternion targetRotation2 = Quaternion.LookRotation (targetDir2, transform.up);

            head.transform.rotation = Quaternion.Slerp (head.transform.rotation, targetRotation2, currentRotationSpeed * Time.deltaTime);
        }
    }

    //the gravity of the turret is regular again
    void dropCharacter (bool state)
    {
        kinematicActive = state;
    }

    //when the turret detects a ground surface, will rotate according to the surface normal
    IEnumerator rotateToSurface (RaycastHit hit)
    {
        //it works like the player gravity
        kinematicActive = false;

        mainRigidbody.useGravity = true;
        mainRigidbody.isKinematic = true;

        Quaternion rot = transform.rotation;
        Vector3 myForward = Vector3.Cross (transform.right, hit.normal);
        Quaternion dstRot = Quaternion.LookRotation (myForward, hit.normal);
        Vector3 pos = hit.point;

        for (float t = 0; t < 1;) {
            t += Time.deltaTime * 3;

            transform.rotation = Quaternion.Slerp (rot, dstRot, t);
            //set also the position of the turret to the hit point

            transform.position = Vector3.MoveTowards (transform.position, pos + 0.5f * transform.up, t);

            yield return null;
        }

        gameObject.layer = 0;
    }

    //return the head of the turret to its original rotation
    IEnumerator rotateElement (GameObject element)
    {
        Quaternion rot = element.transform.localRotation;

        Vector3 myForward = Vector3.Cross (element.transform.right, Vector3.up);
        Quaternion dstRot = Quaternion.LookRotation (myForward, Vector3.up);

        dstRot.y = 0;

        for (float t = 0; t < 1;) {
            t += Time.deltaTime * 3 * speedMultiplier;

            element.transform.localRotation = Quaternion.Slerp (rot, dstRot, t);

            yield return null;
        }
    }

    //if one enemy or more are inside of the turret's trigger, activate the weapon selected in the inspector: machine gun, laser or cannon
    void activateWeapon ()
    {
        if (locatedEnemyAudioElement != null) {
            AudioPlayer.PlayOneShot (locatedEnemyAudioElement, gameObject, Random.Range (0.8f, 1.2f));
        }

        setCurrentTurretWeapon (currentWeaponName);

        lastTimeWeaponsActivated = Time.time;
    }

    //if all the enemies in the trigger of the turret are gone, deactivate the weapons
    void deactivateWeapon ()
    {
        deactivateCurrentWeapon ();
    }

    //the turret is destroyed, so disable all the triggers, the AI, and add a rigidbody to every object with a render, and add force to them
    public void setDeathState ()
    {
        deactivateWeapon ();

        dead = true;

        lastTimeTurretDestroyed = Time.time;

        Component [] components = GetComponentsInChildren (typeof (Transform));

        int layerToIgnoreIndex = LayerMask.NameToLayer ("Scanner");

        int ignoreRaycastLayerIndex = LayerMask.NameToLayer ("Ignore Raycast");

        foreach (Component c in components) {
            Renderer currentRenderer = c.GetComponent<Renderer> ();

            if (currentRenderer != null && c.gameObject.layer != layerToIgnoreIndex) {
                if (fadePiecesOnDeath) {
                    rendererParts.Add (currentRenderer);

                    currentRenderer.material.shader = transparent;
                }

                c.transform.SetParent (transform);

                c.gameObject.layer = ignoreRaycastLayerIndex;

                Rigidbody currentRigidbody = c.gameObject.GetComponent<Rigidbody> ();

                if (currentRigidbody == null) {
                    currentRigidbody = c.gameObject.AddComponent<Rigidbody> ();
                }

                Collider currentCollider = c.gameObject.GetComponent<Collider> ();

                if (currentCollider == null) {
                    c.gameObject.AddComponent<BoxCollider> ();
                }

                if (addForceToTurretPiecesOnDeath) {
                    currentRigidbody.AddExplosionForce (forceAmountToPieces, transform.position + transform.up, radiusAmountToPieces, 3);
                }
            } else {
                Collider currentCollider = c.gameObject.GetComponent<Collider> ();

                if (currentCollider != null) {
                    currentCollider.enabled = false;
                }
            }
        }
    }

    //if the player uses the power of slow down, reduces the rotation speed of the turret, the rate fire and the projectile velocity
    void setReducedVelocity (float speedMultiplierValue)
    {
        currentRotationSpeed = speedMultiplierValue;

        speedMultiplier = speedMultiplierValue;
    }

    //set the turret speed to its normal state
    void setNormalVelocity ()
    {
        currentRotationSpeed = orignalRotationSpeed;

        speedMultiplier = 1;
    }

    public void setRandomWeapon ()
    {
        int random = Random.Range (0, turretWeaponInfoList.Count);

        setWeapon (random);
    }

    public void setWeapon (int weaponNumber)
    {
        setCurrentTurretWeapon (turretWeaponInfoList [weaponNumber].Name);
    }

    public void startOverride ()
    {
        overrideTurretControlState (true);
    }

    public void stopOverride ()
    {
        overrideTurretControlState (false);
    }

    public void overrideTurretControlState (bool state)
    {
        if (controlOverriden == state) {
            return;
        }

        if (state) {
            currentLookAngle = new Vector2 (rayCastPosition.transform.eulerAngles.y, rayCastPosition.transform.eulerAngles.x);
        } else {
            currentLookAngle = Vector2.zero;
            axisValues = Vector2.zero;
            shootingWeapons = false;
        }

        controlOverriden = state;

        targetManager.pauseAI (controlOverriden);

        if (controlOverriden) {
            currentRotationSpeed = overrideRotationSpeed;
        } else {
            currentRotationSpeed = orignalRotationSpeed;

            StartCoroutine (rotateElement (head));

            deactivateWeapon ();
        }

        stopDisableOverrideAfterDelayCoroutine ();

        for (int i = 0; i < turretWeaponInfoList.Count; i++) {
            if (turretWeaponInfoList [i].useSimpleWeaponSystem) {
                if (controlOverriden) {
                    turretWeaponInfoList [i].mainSimpleWeaponSystem.setCustommainCameraTransform (currentCameraTransformDirection);
                } else {
                    turretWeaponInfoList [i].mainSimpleWeaponSystem.setCustommainCameraTransform (null);
                }
            }
        }
    }

    public void disableOverrideAfterDelay (float delayDuration)
    {
        if (gameObject.activeSelf) {
            stopDisableOverrideAfterDelayCoroutine ();

            disableOverrideCoroutine = StartCoroutine (updatedDisableOverrideAfterDelayCoroutine (delayDuration));
        }
    }

    public void stopDisableOverrideAfterDelayCoroutine ()
    {
        if (disableOverrideCoroutine != null) {
            StopCoroutine (disableOverrideCoroutine);
        }
    }

    IEnumerator updatedDisableOverrideAfterDelayCoroutine (float delayDuration)
    {
        yield return new WaitForSecondsRealtime (delayDuration);

        stopOverride ();
    }

    public void setNewCurrentCameraTransformDirection (Transform newTransform)
    {
        currentCameraTransformDirection = newTransform;
    }

    public void setNewTurretAttacker (GameObject newAttacker)
    {
        turretAttacker = newAttacker;
    }


    void setCurrentTurretWeapon (string weaponName)
    {
        int currentIndex = turretWeaponInfoList.FindIndex (s => s.Name == weaponName);

        if (currentIndex > -1) {

            //if (turretWeaponInfoList [currentIndex].isCurrentWeapon) {
            //    return;
            //}

            for (int i = 0; i < turretWeaponInfoList.Count; i++) {

                if (i != currentIndex) {
                    turretWeaponInfo currentInfo = turretWeaponInfoList [i];

                    if (currentInfo.isCurrentWeapon) {
                        currentInfo.isCurrentWeapon = false;

                        if (currentInfo.useEventOnSelectWeapon) {
                            currentInfo.eventOnDeactivateWeapon.Invoke ();
                        }

                        if (currentInfo.weaponObject != null) {
                            if (currentInfo.weaponObject.activeSelf) {
                                currentInfo.weaponObject.SetActive (false);
                            }
                        }

                        if (currentInfo.useWeaponActivateAnimation) {
                            if (currentInfo.weaponActivationAnimationSystem != null) {
                                currentInfo.weaponActivationAnimationSystem.playBackwardAnimation ();
                            }
                        }
                    }
                }
            }

            currentTurretWeaponInfo = turretWeaponInfoList [currentIndex];

            currentTurretWeaponInfo.isCurrentWeapon = true;

            if (currentTurretWeaponInfo.useEventOnSelectWeapon) {
                currentTurretWeaponInfo.eventOnActivateWeapon.Invoke ();
            }

            if (currentTurretWeaponInfo.weaponObject != null) {
                if (!currentTurretWeaponInfo.weaponObject.activeSelf) {
                    currentTurretWeaponInfo.weaponObject.SetActive (true);
                }
            }

            if (currentTurretWeaponInfo.useWeaponActivateAnimation) {
                if (currentTurretWeaponInfo.weaponActivationAnimationSystem != null) {
                    currentTurretWeaponInfo.weaponActivationAnimationSystem.playForwardAnimation ();
                }
            }

            if (useGeneralLayerToDamage) {
                if (currentTurretWeaponInfo.useSimpleWeaponSystem) {
                    currentTurretWeaponInfo.mainSimpleWeaponSystem.setTargetToDamageLayer (layerToDamage);
                }
            }

            shootTimerLimit = currentTurretWeaponInfo.fireRate;

            currentWeaponName = currentTurretWeaponInfo.Name;

            currentWeaponIndex = currentIndex;

            weaponInfoAssigned = true;
        }
    }

    void deactivateCurrentWeapon ()
    {
        if (weaponInfoAssigned) {
            if (currentTurretWeaponInfo.useEventOnSelectWeapon) {
                currentTurretWeaponInfo.eventOnDeactivateWeapon.Invoke ();
            }

            if (currentTurretWeaponInfo.weaponObject != null) {
                if (currentTurretWeaponInfo.weaponObject.activeSelf) {
                    currentTurretWeaponInfo.weaponObject.SetActive (false);
                }
            }

            if (currentTurretWeaponInfo.useSimpleWeaponSystem) {
                currentTurretWeaponInfo.mainSimpleWeaponSystem.shootWeapon (false);
            }

            if (currentTurretWeaponInfo.useWeaponActivateAnimation) {
                if (currentTurretWeaponInfo.weaponActivationAnimationSystem != null) {
                    currentTurretWeaponInfo.weaponActivationAnimationSystem.playBackwardAnimation ();
                }
            }
        }
    }

    //INPUT FUNCTIONS
    public void inputSetShootState (bool state)
    {
        if (weaponsActive) {
            if (Time.time > lastTimeWeaponsActivated + 0.6f) {
                shootingWeapons = state;
            }
        }
    }

    public void inputSetWeaponsState ()
    {
        enableOrDisableWeapons (!weaponsActive);
    }

    public void inputSetNextOrPreviousWeapon (bool state)
    {
        if (state) {
            chooseNextWeapon ();
        } else {
            choosePreviousWeapon ();
        }
    }

    [System.Serializable]
    public class turretWeaponInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public float fireRate;

        public bool useWaitTimeToStartShootAfterActivation;
        public float waitTimeToStartShootAfterActivation;

        [Space]

        public bool useSimpleWeaponSystem;
        public simpleWeaponSystem mainSimpleWeaponSystem;

        [Space]
        [Header ("Animation Settings")]
        [Space]

        public bool useWeaponActivateAnimation;

        public simpleAnimationSystem weaponActivationAnimationSystem;

        [Space]

        public bool useAnimationOnWeapon;
        public string weaponAnimationName;

        public Animation animationOnWeapon;

        [Space]
        [Header ("Other Settings")]
        [Space]

        public bool weaponLookAtTarget;
        public Transform weaponLookAtTargetTransform;

        public GameObject weaponObject;

        [Space]
        [Header ("Debug")]
        [Space]

        public bool isCurrentWeapon;

        [Space]
        [Header ("Event Settings")]
        [Space]

        public bool useEventOnSelectWeapon;

        public UnityEvent eventOnActivateWeapon;
        public UnityEvent eventOnDeactivateWeapon;
    }
}