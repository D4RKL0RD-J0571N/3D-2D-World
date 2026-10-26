using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static sharedActionContent;
using static sharedActionSystemRemoteActivator;

public class sharedActionSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool sharedActionEnabled = true;

    public bool checkCharacterStatusToStopSharedActionEnabled = true;

    [Space]
    [Header ("Shared Action List Settings")]
    [Space]

    public List<sharedActionSystemInfo> sharedActionSystemInfoList = new List<sharedActionSystemInfo> ();

    [Space]
    [Header ("Shared Action Activated Settings")]
    [Space]

    public List<remoteSharedActionSystemInfo> remoteSharedActionSystemInfoList = new List<remoteSharedActionSystemInfo> ();

    [Space]
    [Header ("Shared Action Activated Settings")]
    [Space]

    public bool useRandomSharedActionInfoListEnabled;

    [Space]

    public List<randomSharedActionInfo> randomSharedActionInfoList = new List<randomSharedActionInfo> ();

    [Space]
    [Header ("Actions To Ignore Settings")]
    [Space]

    public bool useActionsToIgnoreList;
    public List<string> actionsToIgnoreList = new List<string> ();

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool ignoreStatsCheckEnabled;
    public bool ignoreSharedActionZonesEnabled;

    [Space]
    [Header ("AI Around Settings")]
    [Space]

    public bool pauseAIAroundDuringActionActive;

    public bool pauseAIAttackAroundDuringActionActive;
    public bool enableRandomWalkOnAIAroundDuringActionActive;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool firstCharacterIsPlayer;

    public bool sharedActionActive;

    public GameObject firstCharacter;
    public GameObject secondCharacter;

    public GameObject currentExternalCharacter;

    public bool lastSharedActionFound;

    public bool usingSharedActionZoneResult;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsOnSharedActionActivated;
    public UnityEvent eventsOnSharedActionActivated;

    public bool useEventsOnSharedActionConditionNotAchieved;
    public UnityEvent eventsOnSharedActionConditionNotAchieved;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject characterTransform;

    public playerStatsSystem mainPlayerStatsSystem;


    List<sharedActionTemporalInfo> sharedActionTemporalInfoList = new List<sharedActionTemporalInfo> ();


    sharedActionSystemInfo currentActionInfo;

    sharedActionContent currentSharedActionContent;

    GameObject sharedActionGameObject;

    playerActionSystem firstCharacterPlayerActionSystem;
    playerActionSystem secondCharacterPlayerActionSystem;

    Coroutine updateCoroutine;

    Coroutine characterStatusCoroutine;

    bool setThisCharacterAsFirstCharacter;

    sharedActionZoneManager currentSharedActionZoneManager;
    bool currentSharedActionZoneManagerFound;

    sharedActionZone currentZone;

    bool ignoreExtraSharedActionActive = false;
    bool ignoreAlternativeSharedActionActive = false;

    float lastTimeSharedActionActive = 0;

    public void setExternalCharacter (GameObject newCharacter)
    {
        currentExternalCharacter = newCharacter;
    }

    public void setFirstCharacter (GameObject newCharacter)
    {
        firstCharacter = newCharacter;
    }

    public void setSecondCharacter (GameObject newCharacter)
    {
        secondCharacter = newCharacter;
    }

    public bool isLastSharedActionFound ()
    {
        return lastSharedActionFound;
    }

    public bool isSharedActionEnabled ()
    {
        return sharedActionEnabled;
    }

    public bool isSharedActionActive ()
    {
        return sharedActionActive;
    }

    public void activateRandomSharedActionByName (string actionName)
    {
        if (!useRandomSharedActionInfoListEnabled) {
            return;
        }

        if (!sharedActionEnabled) {
            return;
        }

        int currentActionIndex = randomSharedActionInfoList.FindIndex (s => s.Name.Equals (actionName));

        if (currentActionIndex > -1) {
            randomSharedActionInfo currentRandomSharedActionInfo = randomSharedActionInfoList [currentActionIndex];

            if (currentRandomSharedActionInfo.enabled) {
                int randomActionIndex = Random.Range (0, currentRandomSharedActionInfo.randomSharedActionNameList.Count);

                string randomActionName = currentRandomSharedActionInfo.randomSharedActionNameList [randomActionIndex];

                if (randomActionName != "") {
                    if (usingSharedActionZoneResult) {
                        print ("random action found " + randomActionName + " from random action info name " + actionName);
                    }

                    activateSharedActionByName (randomActionName);
                }
            }
        }
    }

    public void activateSharedActionByName (string actionName)
    {
        lastSharedActionFound = false;

        if (!sharedActionEnabled) {
            return;
        }

        int currentActionIndex = sharedActionSystemInfoList.FindIndex (s => s.Name.Equals (actionName));

        if (useActionsToIgnoreList && currentActionIndex > -1) {
            if (actionsToIgnoreList.Contains (actionName)) {
                if (showDebugPrint) {
                    print ("action found on the action to ignore list " + actionName);
                }

                currentActionIndex = -1;
            }
        }

        if (currentActionIndex > -1) {
            currentActionInfo = sharedActionSystemInfoList [currentActionIndex];

            if (currentActionInfo.sharedActionEnabled) {
                if (currentActionInfo.sharedActionGameObject == null) {
                    if (currentActionInfo.sharedActionPrefab != null) {
                        currentActionInfo.sharedActionGameObject = (GameObject)Instantiate (currentActionInfo.sharedActionPrefab);

                        currentActionInfo.mainSharedActionContent =
                            currentActionInfo.sharedActionGameObject.GetComponent<sharedActionContent> ();
                    }
                } else {
                    currentActionInfo.sharedActionGameObject.SetActive (true);
                }

                bool sharedActionGameObjectLocated = currentActionInfo.sharedActionGameObject != null;

                if (sharedActionGameObjectLocated) {
                    sharedActionGameObject = currentActionInfo.sharedActionGameObject;

                    currentSharedActionContent = currentActionInfo.mainSharedActionContent;

                    firstCharacterIsPlayer = currentSharedActionContent.firstCharacterIsPlayer;

                    setThisCharacterAsFirstCharacter = currentActionInfo.setThisCharacterAsFirstCharacter;
                }

                //if needed, search the main player on scene and get its current target to get the AI character

                if (firstCharacterIsPlayer) {

                } else {

                }

                if (setThisCharacterAsFirstCharacter) {
                    firstCharacter = characterTransform;

                    secondCharacter = currentExternalCharacter;
                } else {
                    firstCharacter = currentExternalCharacter;

                    secondCharacter = characterTransform;
                }


                //check for shared action zone
                usingSharedActionZoneResult = false;

                if (currentActionInfo.searchSharedActionZonesOnSceneEnabled && !ignoreSharedActionZonesEnabled) {
                    usingSharedActionZoneResult = checkForSharedActioSystemZone ();

                    if (showDebugPrint) {
                        if (usingSharedActionZoneResult) {
                            print ("zone found and conditions achieved for zone");
                        } else {
                            print ("not zone found or conditions not achieved for zone, using regular shared action");
                        }
                    }
                }

                if (!usingSharedActionZoneResult && !sharedActionGameObjectLocated) {
                    if (showDebugPrint) {
                        print ("shared action and zone not found " + actionName);
                    }

                    checkEventOnSharedActionResult (false, actionName);

                    return;
                }

                bool checkMinWaitTimeToPlaySharedActionResult = false;

                if (currentActionInfo.checkMinWaitTimeToPlaySharedAction) {
                    float minWaitTimeToPlaySharedAction = currentActionInfo.minWaitTimeToPlaySharedAction;

                    int sharedActionTemporalIndex = sharedActionTemporalInfoList.FindIndex (s => s.Name.Equals (actionName));

                    if (sharedActionTemporalIndex > -1) {
                        sharedActionTemporalInfo currentSharedActionTemporalInfo = sharedActionTemporalInfoList [sharedActionTemporalIndex];

                        if (currentSharedActionTemporalInfo.lastTimeActionPlayed > 0 &&
                          Time.time < (currentSharedActionTemporalInfo.lastTimeActionPlayed + minWaitTimeToPlaySharedAction)) {
                            checkMinWaitTimeToPlaySharedActionResult = true;
                        } else {
                            currentSharedActionTemporalInfo.lastTimeActionPlayed = Time.time;
                        }

                        print ("action already on wait list, checking " + checkMinWaitTimeToPlaySharedActionResult);
                    } else {
                        sharedActionTemporalInfo newSharedActionTemporalInfo = new sharedActionTemporalInfo ();

                        newSharedActionTemporalInfo.Name = actionName;
                        newSharedActionTemporalInfo.lastTimeActionPlayed = Time.time;

                        sharedActionTemporalInfoList.Add (newSharedActionTemporalInfo);

                        print ("first time playing  " + actionName + " adding it to the list");
                    }
                }

                if (checkMinWaitTimeToPlaySharedActionResult) {
                    if (showDebugPrint) {
                        print ("min wait time to play this action not reached, cancelling " + actionName);
                    }

                    if (!usingSharedActionZoneResult) {
                        sharedActionGameObject.SetActive (false);
                    }

                    checkEventOnSharedActionResult (false, actionName);

                    return;
                }

                if (!usingSharedActionZoneResult) {
                    bool checkCharacterOrientationResult = true;

                    if (currentActionInfo.checkCharacterOrientation) {
                        Vector3 charactersDirection = currentExternalCharacter.transform.position -
                            characterTransform.transform.position;

                        bool isBehindCharacter = true;

                        float currentAngleWithTarget = Vector3.Dot (characterTransform.transform.forward, charactersDirection.normalized);

                        if (currentAngleWithTarget > 0) {
                            isBehindCharacter = false;
                        }

                        if (showDebugPrint) {
                            print (charactersDirection + " " + currentAngleWithTarget + " " + isBehindCharacter);
                        }

                        if ((isBehindCharacter && currentActionInfo.actionToPlayInFrontOfCharacter) ||
                            (!isBehindCharacter && !currentActionInfo.actionToPlayInFrontOfCharacter)) {
                            if (currentActionInfo.useExtraSharedActionIfIncorrectOrientation) {
                                if (showDebugPrint) {
                                    print ("orientation on characters not correct, checking to play extra action");
                                }

                                if (ignoreExtraSharedActionActive) {
                                    checkCharacterOrientationResult = false;
                                } else {
                                    ignoreExtraSharedActionActive = true;

                                    activateSharedActionByName (currentActionInfo.extraSharedActionIfIncorrectOrientationName);

                                    ignoreExtraSharedActionActive = false;

                                    return;
                                }
                            } else {
                                checkCharacterOrientationResult = false;
                            }
                        } else {
                            if (showDebugPrint) {
                                print ("characters orientation correct " + isBehindCharacter);
                            }
                        }
                    }

                    if (!checkCharacterOrientationResult) {
                        if (showDebugPrint) {
                            print ("extra shared action to play for different angle not found " + actionName);
                        }

                        if (!usingSharedActionZoneResult) {
                            sharedActionGameObject.SetActive (false);
                        }

                        checkEventOnSharedActionResult (false, actionName);
                    }
                }

                //check here conditions to allow to play the actions, like max distance and angle for chracters

                bool canPlaySharedActionResult = true;

                //check distance and angle of characters
                if (currentSharedActionContent.useMinDistanceToActivateAction) {
                    float charactersDistance = GKC_Utils.distance (firstCharacter.transform.position, secondCharacter.transform.position);

                    if (charactersDistance > currentSharedActionContent.minDistanceToActivateAction) {
                        if (showDebugPrint) {
                            print ("conditions not achieved from distance between characters " + actionName);
                        }

                        if (!usingSharedActionZoneResult) {
                            sharedActionGameObject.SetActive (false);
                        }

                        checkEventOnSharedActionResult (false, actionName);

                        return;
                    }
                }

                //check stats on this character
                if (currentSharedActionContent.checkCharacterStats && !ignoreStatsCheckEnabled) {
                    List<sharedActionConditionStatInfo> sharedActionConditionStatInfoList =
                        currentSharedActionContent.sharedActionConditionStatInfoList;

                    int sharedActionConditionStatInfoListCount = sharedActionConditionStatInfoList.Count;

                    for (int i = 0; i < sharedActionConditionStatInfoListCount; i++) {
                        if (canPlaySharedActionResult) {
                            sharedActionConditionStatInfo currentStat = sharedActionConditionStatInfoList [i];

                            if (currentStat.statIsAmount) {
                                float statAmount = mainPlayerStatsSystem.getStatValue (currentStat.statName);

                                if (currentStat.checkStatIsHigher) {
                                    if (statAmount < currentStat.statAmount) {
                                        canPlaySharedActionResult = false;

                                        if (showDebugPrint) {
                                            print ("stat condition not reached " + currentStat.statName + " " + statAmount);
                                        }
                                    }
                                } else {
                                    if (statAmount > currentStat.statAmount) {
                                        canPlaySharedActionResult = false;

                                        if (showDebugPrint) {
                                            print ("stat condition not reached " + currentStat.statName + " " + statAmount);
                                        }
                                    }
                                }
                            } else {
                                bool statValue = mainPlayerStatsSystem.getBoolStatValue (currentStat.statName);

                                if (statValue != currentStat.stateValue) {
                                    canPlaySharedActionResult = false;

                                    if (showDebugPrint) {
                                        print ("stat condition not reached " + currentStat.statName + " " + statValue);
                                    }
                                }
                            }
                        }
                    }

                    if (canPlaySharedActionResult) {
                        if (showDebugPrint) {
                            print ("stats conditions achieved, continuing with shared action checks");
                        }
                    }
                }

                if (currentSharedActionContent.useProbabilityToUseAction) {
                    float currentProbability = Random.Range (0, 100);

                    if (currentProbability > currentSharedActionContent.probabilityToUseAction) {
                        canPlaySharedActionResult = false;

                        if (showDebugPrint) {
                            print ("probability to play action not high enough on shared action content" + currentProbability + "  " +
                                currentSharedActionContent.probabilityToUseAction);
                        }
                    }
                }

                if (currentSharedActionContent.checkWeaponsOnFirstCharacter) {
                    if (currentSharedActionContent.checkWeaponNameOnFirstCharacter) {
                        canPlaySharedActionResult =
                            GKC_Utils.isCharacterUsingMeleeWeapons (firstCharacter, currentSharedActionContent.weaponNameOnFirstCharacter) ||
                            GKC_Utils.isCharacterUsingFireWeapons (firstCharacter, currentSharedActionContent.weaponNameOnFirstCharacter);

                    } else {
                        if (currentSharedActionContent.useMeleeWeaponOnFirstCharacter) {
                            canPlaySharedActionResult = GKC_Utils.isCharacterUsingMeleeWeapons (firstCharacter, "");
                        }

                        if (currentSharedActionContent.useFireWeaponOnFirstCharacter) {
                            canPlaySharedActionResult = GKC_Utils.isCharacterUsingFireWeapons (firstCharacter, "");
                        }
                    }
                }

                if (currentSharedActionContent.checkWeaponsOnSecondCharacter) {
                    if (currentSharedActionContent.checkWeaponNameOnSecondCharacter) {
                        canPlaySharedActionResult =
                            GKC_Utils.isCharacterUsingMeleeWeapons (secondCharacter, currentSharedActionContent.weaponNameOnSecondCharacter) ||
                            GKC_Utils.isCharacterUsingFireWeapons (secondCharacter, currentSharedActionContent.weaponNameOnSecondCharacter);

                    } else {
                        if (currentSharedActionContent.useMeleeWeaponOnSecondCharacter) {
                            canPlaySharedActionResult = GKC_Utils.isCharacterUsingMeleeWeapons (secondCharacter, "");
                        }

                        if (currentSharedActionContent.useFireWeaponOnSecondCharacter) {
                            canPlaySharedActionResult = GKC_Utils.isCharacterUsingFireWeapons (secondCharacter, "");
                        }
                    }
                }


                if (currentSharedActionContent.checkWeaponsOnFirstCharacter ||
                    currentSharedActionContent.checkWeaponsOnSecondCharacter) {
                    if (currentSharedActionContent.useAlternativeSharedActionIfConditionsFailed) {
                        if (!canPlaySharedActionResult) {
                            if (ignoreAlternativeSharedActionActive) {
                                canPlaySharedActionResult = false;
                            } else {
                                ignoreAlternativeSharedActionActive = true;

                                activateSharedActionByName (currentSharedActionContent.alternativeSharedActionName);

                                ignoreAlternativeSharedActionActive = false;

                                return;
                            }
                        }
                    }
                }

                if (currentActionInfo.useProbabilityToUseAction) {
                    float currentProbability = Random.Range (0, 100);

                    if (currentProbability > currentActionInfo.probabilityToUseAction) {
                        canPlaySharedActionResult = false;

                        if (showDebugPrint) {
                            print ("probability to play action not high enough on shared action info" + currentProbability + "  " +
                                currentActionInfo.probabilityToUseAction);
                        }
                    }
                }

                if (applyDamage.checkIfDead (firstCharacter) || applyDamage.checkIfDead (secondCharacter)) {
                    canPlaySharedActionResult = false;

                    if (showDebugPrint) {
                        print ("one of the characters to play the action is already dead");
                    }
                }

                if (GKC_Utils.isSharedActionActiveOnCharacter (firstCharacter) || GKC_Utils.isSharedActionActiveOnCharacter (secondCharacter)) {
                    canPlaySharedActionResult = false;

                    if (showDebugPrint) {
                        print ("one of the characters is already using a shared action in process");
                    }
                }

                if (!canPlaySharedActionResult) {
                    if (showDebugPrint) {
                        print ("conditions not achieved " + actionName);
                    }

                    if (!usingSharedActionZoneResult) {
                        sharedActionGameObject.SetActive (false);
                    }

                    checkEventOnSharedActionResult (false, actionName);

                    return;
                }

                bool adjustSharedActionTrasnformResult = true;

                bool adjustPositionResult = false;

                bool adjustRotationResult = false;

                Vector3 transformDirection = Vector3.zero;

                Transform mainCharacterReferenceTransform = null;

                bool useZonePositionRange = false;

                bool useZoneRotationRange = false;

                bool useCustomSharedZoneRotationCenter = false;
                Transform customSharedZoneTransform = null;

                if (usingSharedActionZoneResult) {
                    useZonePositionRange = currentZone.useZonePositionRange;

                    useZoneRotationRange = currentZone.useZoneRotationRange;

                    if (!useZonePositionRange && !useZoneRotationRange) {
                        adjustSharedActionTrasnformResult = false;
                    } else {
                        useCustomSharedZoneRotationCenter = currentZone.useCustomSharedZoneRotationCenter;

                        if (useCustomSharedZoneRotationCenter) {
                            customSharedZoneTransform = currentZone.customSharedZoneTransform;
                        }
                    }
                }

                if (adjustSharedActionTrasnformResult) {
                    if (currentSharedActionContent.adjustPositionToFirstCharacter) {
                        adjustPositionResult = true;

                        mainCharacterReferenceTransform = firstCharacter.transform;

                        if (currentSharedActionContent.alignMainSharedActionGameObjectToBothCharacters) {
                            adjustRotationResult = true;

                            transformDirection =
                                secondCharacter.transform.position - firstCharacter.transform.position;
                        }
                    }

                    if (currentSharedActionContent.adjustPositionToSecondCharacter) {
                        adjustPositionResult = true;

                        mainCharacterReferenceTransform = secondCharacter.transform;

                        if (currentSharedActionContent.alignMainSharedActionGameObjectToBothCharacters) {
                            adjustRotationResult = true;

                            transformDirection =
                                firstCharacter.transform.position - secondCharacter.transform.position;
                        }
                    }

                    if (adjustPositionResult) {
                        Vector3 targetPosition = mainCharacterReferenceTransform.position;

                        if (useZonePositionRange) {
                            Vector3 initialPosition = currentZone.getInitialPosition ();

                            float newPositionX = Mathf.Clamp (targetPosition.x,
                                currentZone.xRange.x + initialPosition.x,
                                currentZone.xRange.y + initialPosition.x);

                            float newPositionZ = Mathf.Clamp (targetPosition.z,
                                currentZone.zRange.x + initialPosition.z,
                                currentZone.zRange.y + initialPosition.z);

                            targetPosition = new Vector3 (newPositionX, initialPosition.y, newPositionZ);
                        }

                        sharedActionGameObject.transform.position = targetPosition;
                    }

                    if (adjustRotationResult) {
                        if (usingSharedActionZoneResult) {
                            if (useCustomSharedZoneRotationCenter) {
                                transformDirection =
                                    customSharedZoneTransform.position - sharedActionGameObject.transform.position;
                            }
                        } else {
                            sharedActionGameObject.transform.rotation = mainCharacterReferenceTransform.rotation;
                        }

                        Vector3 heading = transformDirection;

                        float distance = heading.magnitude;
                        Vector3 charactersDirection = heading / distance;

                        if (showDebugPrint) {
                            print (sharedActionGameObject.transform.forward + " " + charactersDirection);
                        }

                        float directionAngle = Vector3.SignedAngle (sharedActionGameObject.transform.forward,
                            charactersDirection, sharedActionGameObject.transform.up);

                        if (useZoneRotationRange) {
                            print (directionAngle);

                            if (directionAngle > 180) {
                                directionAngle -= 360;
                            }

                            if (showDebugPrint) {
                                print ("useZoneRotationRange " + directionAngle);
                            }

                            directionAngle =
                                Mathf.Clamp (directionAngle, -currentZone.rotationRange, currentZone.rotationRange);

                            print (directionAngle);
                        }

                        sharedActionGameObject.transform.Rotate (0, directionAngle, 0);
                    }
                }

                //assign each character to its action system

                playerComponentsManager firstCharacterPlayerComponentManager = firstCharacter.GetComponent<playerComponentsManager> ();

                if (firstCharacterPlayerComponentManager != null) {
                    firstCharacterPlayerActionSystem = firstCharacterPlayerComponentManager.getPlayerActionSystem ();
                }

                playerComponentsManager secondCharacterPlayerComponentManager = secondCharacter.GetComponent<playerComponentsManager> ();

                if (secondCharacterPlayerComponentManager != null) {
                    secondCharacterPlayerActionSystem = secondCharacterPlayerComponentManager.getPlayerActionSystem ();
                }

                if (firstCharacterPlayerActionSystem == null || secondCharacterPlayerActionSystem == null) {
                    return;
                }

                lastSharedActionFound = true;

                checkEventOnSharedActionResult (true, actionName);

                stopUpdateCoroutine ();

                updateCoroutine = StartCoroutine (updateSystemCoroutine ());

                stopCheckCharacterStatusCoroutine ();

                if (showDebugPrint) {
                    print ("conditions for the shared action achieved, activating coroutine");
                }

                characterStatusCoroutine = StartCoroutine (checkCharacterStatusCoroutine ());

                checkAIAroundState (true);
            }
        }

        if (!lastSharedActionFound) {
            if (showDebugPrint) {
                print ("action not found or action disabled or not conditions achieved " + actionName);
            }

            checkEventOnSharedActionResult (false, actionName);
        }
    }

    void stopCheckCharacterStatusCoroutine ()
    {
        if (characterStatusCoroutine != null) {
            StopCoroutine (characterStatusCoroutine);
        }
    }

    public void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }
    }

    //check if an action system should be played and wait for a possible character adjustement before
    //playing the second one, for this check if the other action is in process of adjusting the character
    //else, it means there is no match positions and each character is going to play their animation

    void checkStopFirstCharacterActions ()
    {
        if (firstCharacterPlayerActionSystem.isActionActive ()) {
            if (showDebugPrint) {
                print ("first character was playing an action, stopping");
            }

            firstCharacterPlayerActionSystem.stopAllActions ();
        }

        GKC_Utils.disableCurrentAttackInProcess (firstCharacter);

        GKC_Utils.stopMatchPlayerToTargetMovementIfActive (firstCharacter);
    }

    void checkStopSecondCharacterActions ()
    {
        if (secondCharacterPlayerActionSystem.isActionActive ()) {
            if (showDebugPrint) {
                print ("second character was playing an action, stopping");
            }

            secondCharacterPlayerActionSystem.stopAllActions ();
        }

        GKC_Utils.disableCurrentAttackInProcess (secondCharacter);

        GKC_Utils.stopMatchPlayerToTargetMovementIfActive (secondCharacter);
    }

    IEnumerator updateSystemCoroutine ()
    {
        sharedActionActive = true;

        //maybe an option to stop the actions on each character if it is being played

        if (currentSharedActionContent.matchPositionUsedOnAction) {

            bool matchPositionReached = false;

            float checkingMatchPositionTimer = 0;

            bool adjustMatchPositionOnFirstCharacter = currentSharedActionContent.adjustMatchPositionOnFirstCharacter;

            bool adjustMatchPositionOnSecondCharacter = currentSharedActionContent.adjustMatchPositionOnSecondCharacter;


            if (adjustMatchPositionOnFirstCharacter) {
                if (showDebugPrint) {
                    print ("stop first character actions and play the action");
                }

                checkStopFirstCharacterActions ();

                currentSharedActionContent.firstCharacterActionSystem.setPlayerActionActive (firstCharacter);

                firstCharacterPlayerActionSystem.playCurrentAnimation ();
            }

            if (adjustMatchPositionOnSecondCharacter) {
                if (showDebugPrint) {
                    print ("stop second character actions and play the action");
                }

                checkStopSecondCharacterActions ();

                currentSharedActionContent.secondCharacterActionSystem.setPlayerActionActive (secondCharacter);

                secondCharacterPlayerActionSystem.playCurrentAnimation ();
            }

            if (adjustMatchPositionOnFirstCharacter && adjustMatchPositionOnSecondCharacter) {
                //add a wait state before playing the animation if both characters need to adjust their position
                firstCharacterPlayerActionSystem.setWaitToPlayAnimationAfterAdjustPositionActiveState (true);
                secondCharacterPlayerActionSystem.setWaitToPlayAnimationAfterAdjustPositionActiveState (true);
            }

            while (!matchPositionReached) {
                if (checkingMatchPositionTimer > 0.2f) {
                    if (adjustMatchPositionOnFirstCharacter && adjustMatchPositionOnSecondCharacter) {
                        if (!firstCharacterPlayerActionSystem.isUsePositionToAdjustPlayerActive () &&
                            !secondCharacterPlayerActionSystem.isUsePositionToAdjustPlayerActive ()) {
                            if (showDebugPrint) {
                                print ("both characters actions position adjusted, continue");
                            }

                            matchPositionReached = true;
                        }
                    } else {
                        if (adjustMatchPositionOnFirstCharacter) {
                            if (!firstCharacterPlayerActionSystem.isUsePositionToAdjustPlayerActive ()) {
                                if (showDebugPrint) {
                                    print ("first character actions position adjusted, continue");
                                }

                                matchPositionReached = true;
                            }
                        } else {
                            if (!secondCharacterPlayerActionSystem.isUsePositionToAdjustPlayerActive ()) {
                                if (showDebugPrint) {
                                    print ("second character actions position adjusted, continue");
                                }

                                matchPositionReached = true;
                            }
                        }
                    }
                }

                checkingMatchPositionTimer += Time.deltaTime;

                if (checkingMatchPositionTimer > 5) {
                    matchPositionReached = true;
                }

                yield return null;
            }

            if (adjustMatchPositionOnFirstCharacter && adjustMatchPositionOnSecondCharacter) {
                //resume the play animation if both characters need to adjust their position
                firstCharacterPlayerActionSystem.setWaitToPlayAnimationAfterAdjustPositionActiveState (false);
                secondCharacterPlayerActionSystem.setWaitToPlayAnimationAfterAdjustPositionActiveState (false);
            } else {
                if (adjustMatchPositionOnFirstCharacter) {
                    if (showDebugPrint) {
                        print ("start action on second character");
                    }

                    checkStopSecondCharacterActions ();

                    currentSharedActionContent.secondCharacterActionSystem.setPlayerActionActive (secondCharacter);

                    secondCharacterPlayerActionSystem.playCurrentAnimation ();
                } else {
                    if (showDebugPrint) {
                        print ("start action on first character");
                    }

                    checkStopFirstCharacterActions ();

                    currentSharedActionContent.firstCharacterActionSystem.setPlayerActionActive (firstCharacter);

                    firstCharacterPlayerActionSystem.playCurrentAnimation ();
                }
            }
        } else {
            //add maybe an option to use a delay from one action to the other if needed

            if (showDebugPrint) {
                print ("play both actions together");
            }

            checkStopFirstCharacterActions ();
            checkStopSecondCharacterActions ();

            currentSharedActionContent.firstCharacterActionSystem.setPlayerActionActive (firstCharacter);

            currentSharedActionContent.secondCharacterActionSystem.setPlayerActionActive (secondCharacter);

            firstCharacterPlayerActionSystem.playCurrentAnimation ();

            secondCharacterPlayerActionSystem.playCurrentAnimation ();
        }

        yield return null;

        //check here when both actions are disabled (or each one maybe) and disable both objects on scene
        //maybe an option to destroy them

        if (showDebugPrint) {
            print ("checking when both actions are complete");
        }

        bool sharedActionsFinished = false;

        lastTimeSharedActionActive = Time.time;

        float sharedActionDuration = (firstCharacterPlayerActionSystem.getCurrentActionDuration () +
            secondCharacterPlayerActionSystem.getCurrentActionDuration ()) / 1.5f;


        bool resumeAnyCharacterIfActionFinished = currentSharedActionContent.resumeAnyCharacterIfActionFinished;

        bool firstCharacterResumed = false;

        bool secondCharacterResumed = false;


        while (!sharedActionsFinished) {
            bool firstCharacterIsActionActive = firstCharacterPlayerActionSystem.isActionActive ();

            bool secondCharacterIsActionActive = secondCharacterPlayerActionSystem.isActionActive ();

            if (!firstCharacterIsActionActive && !secondCharacterIsActionActive) {
                sharedActionsFinished = true;
            }

            if (Time.time > sharedActionDuration + lastTimeSharedActionActive) {
                sharedActionsFinished = true;
            }

            if (resumeAnyCharacterIfActionFinished) {
                if (Time.time > 0.2f + lastTimeSharedActionActive) {
                    if (!firstCharacterIsActionActive) {
                        if (!firstCharacterResumed) {
                            currentSharedActionContent.firstCharacterActionSystem.setPlayerActionDeactivate (firstCharacter);

                            firstCharacterResumed = true;
                        }
                    }

                    if (!secondCharacterIsActionActive) {
                        if (!secondCharacterResumed) {
                            currentSharedActionContent.secondCharacterActionSystem.setPlayerActionDeactivate (secondCharacter);

                            secondCharacterResumed = true;
                        }
                    }
                }
            }

            yield return null;
        }

        sharedActionActive = false;

        if (!usingSharedActionZoneResult) {
            sharedActionGameObject.SetActive (false);
        }

        if (!firstCharacterResumed) {
            currentSharedActionContent.firstCharacterActionSystem.setPlayerActionDeactivate (firstCharacter);
        }

        if (!secondCharacterResumed) {
            currentSharedActionContent.secondCharacterActionSystem.setPlayerActionDeactivate (secondCharacter);
        }

        stopCheckCharacterStatusCoroutine ();

        if (showDebugPrint) {
            print ("both actions complete");
        }

        checkAIAroundState (false);
    }

    IEnumerator checkCharacterStatusCoroutine ()
    {
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            yield return waitTime;

            if (checkCharacterStatusToStopSharedActionEnabled) {
                checkCharacterStatus ();
            }
        }
    }
    void checkCharacterStatus ()
    {
        bool cancelSharedActionResult = false;

        bool firstCharacterIsDead = !firstCharacterPlayerActionSystem.isPlayerAlive ();

        bool secondCharacterIsDead = !secondCharacterPlayerActionSystem.isPlayerAlive ();

        if (currentSharedActionContent.stopActionIfAnyCharacterIsDead) {
            if (firstCharacterIsDead || secondCharacterIsDead) {
                cancelSharedActionResult = true;
            }
        }

        if (currentSharedActionContent.stopActionIfFirstCharacterIsDead) {
            if (firstCharacterIsDead) {
                cancelSharedActionResult = true;
            }
        }

        if (currentSharedActionContent.stopActionIfSecondCharacterIsDead) {
            if (secondCharacterIsDead) {
                cancelSharedActionResult = true;
            }
        }

        if (cancelSharedActionResult) {
            stopSharedActionDueToCharactersUnableToFinish ();

            return;
        }
    }

    public void stopSharedActionDueToCharactersUnableToFinish ()
    {
        stopUpdateCoroutine ();

        stopCheckCharacterStatusCoroutine ();

        firstCharacterPlayerActionSystem.setWaitToPlayAnimationAfterAdjustPositionActiveState (false);
        secondCharacterPlayerActionSystem.setWaitToPlayAnimationAfterAdjustPositionActiveState (false);

        checkStopFirstCharacterActions ();
        checkStopSecondCharacterActions ();

        sharedActionActive = false;

        currentSharedActionContent.firstCharacterActionSystem.setPlayerActionDeactivate (firstCharacter);

        currentSharedActionContent.secondCharacterActionSystem.setPlayerActionDeactivate (secondCharacter);

        if (!usingSharedActionZoneResult) {
            sharedActionGameObject.SetActive (false);
        }

        if (showDebugPrint) {
            print ("stop shared action due to accident");
        }

        checkAIAroundState (false);
    }

    bool checkForSharedActioSystemZone ()
    {
        bool checkForZoneResult = false;

        if (!currentSharedActionZoneManagerFound) {
            currentSharedActionZoneManagerFound = currentSharedActionZoneManager != null;

            if (!currentSharedActionZoneManagerFound) {
                currentSharedActionZoneManager = GKC_Utils.findObjectByComponent<sharedActionZoneManager> ();

                currentSharedActionZoneManagerFound = currentSharedActionZoneManager != null;
            }
        }

        if (currentSharedActionZoneManagerFound && currentSharedActionZoneManager.isSharedActionZonesEnabled ()) {
            List<sharedActionZone> sharedActionZoneList = currentSharedActionZoneManager.getSharedActionZoneList ();

            float closestDistance = Mathf.Infinity;

            int closestIndex = -1;

            Vector3 characterPositon = characterTransform.transform.position;

            for (int i = 0; i < sharedActionZoneList.Count; i++) {
                sharedActionZone temporalZone = sharedActionZoneList [i];

                if (temporalZone.isSharedActionZoneEnabled ()) {
                    bool checkZoneResult = true;

                    string currentZoneActionName = temporalZone.getSharedActionName ();

                    if (currentActionInfo.sharedActionNamesMustMatch) {
                        if (!currentActionInfo.Name.Equals (currentZoneActionName)) {
                            checkZoneResult = false;
                        }
                    }

                    if (currentActionInfo.useSharedActionNameListToCheck) {
                        if (!currentActionInfo.sharedActionNameListToCheck.Contains (currentZoneActionName)) {
                            checkZoneResult = false;

                            if (showDebugPrint) {
                                print ("Shared action list on zone name not found on list " + currentZoneActionName);
                            }
                        } else {
                            if (showDebugPrint) {
                                print ("Shared action list on zone name found on list " + currentZoneActionName);
                            }
                        }
                    }

                    if (checkZoneResult) {
                        Vector3 zonePosition = temporalZone.transform.position;

                        float newDistance = GKC_Utils.distance (characterPositon, zonePosition);

                        if (newDistance < temporalZone.getMaxDistanceToUseSharedActionZone ()) {
                            if (newDistance < closestDistance) {
                                closestDistance = newDistance;

                                closestIndex = i;

                                if (showDebugPrint) {
                                    print ("distance close enough on current zone " + temporalZone.getSharedActionName () + " " +
                                        newDistance + " " + temporalZone.getMaxDistanceToUseSharedActionZone ());
                                }
                            }
                        } else {
                            if (temporalZone.useMaxDistanceXZToUseSharedActionZone) {
                                float xDistance = Mathf.Abs (characterPositon.x - zonePosition.x);
                                float zDistance = Mathf.Abs (characterPositon.z - zonePosition.z);

                                if (xDistance <= temporalZone.maxDistanceXToUseSharedActionZone &&
                                    zDistance <= temporalZone.maxDistanceZToUseSharedActionZone) {

                                    closestDistance = newDistance;

                                    closestIndex = i;

                                    if (showDebugPrint) {
                                        print ("distance inside XZ range on current zone " + temporalZone.getSharedActionName () + " " +
                                            newDistance + " " + temporalZone.getMaxDistanceToUseSharedActionZone ());
                                    }
                                }
                            } else {
                                if (showDebugPrint) {
                                    print ("distance too long from current zone " + temporalZone.getSharedActionName () + " " +
                                        newDistance + " " + temporalZone.getMaxDistanceToUseSharedActionZone ());
                                }
                            }
                        }
                    }
                }
            }

            if (closestIndex > -1) {
                currentZone = sharedActionZoneList [closestIndex];

                if (currentZone.isUseSharedActionContentActive ()) {
                    currentZone.storeInitialPosition ();

                    sharedActionContent temporalSharedActionContent = currentZone.getSharedActionContent ();

                    bool temporalFirstCharacterIsPlayer = currentZone.getSetPlayerAsFirstCharacter ();

                    bool setAIAsFirstCharacterIfSignalOwner = currentZone.getSetAIAsFirstCharacterIfSignalOwner ();

                    GameObject mainPlayerOnScene = GKC_Utils.findMainPlayerOnScene ();

                    if (setAIAsFirstCharacterIfSignalOwner) {
                        if (characterTransform == mainPlayerOnScene) {
                            temporalFirstCharacterIsPlayer = false;
                        }
                    }

                    bool temporalSetThisCharacterAsFirstCharacter = false;

                    if (temporalFirstCharacterIsPlayer) {
                        if (characterTransform == mainPlayerOnScene) {
                            temporalSetThisCharacterAsFirstCharacter = true;
                        }
                    }

                    GameObject temporalFirstCharacter = null;
                    GameObject temporalSecondCharacter = null;

                    if (temporalSetThisCharacterAsFirstCharacter) {
                        temporalFirstCharacter = characterTransform;

                        temporalSecondCharacter = currentExternalCharacter;
                    } else {
                        temporalFirstCharacter = currentExternalCharacter;

                        temporalSecondCharacter = characterTransform;
                    }

                    bool currentZoneConditionsResult = true;

                    Transform firstCharacterActionSystemTransform = temporalSharedActionContent.firstCharacterActionSystem.transform;
                    Transform secondCharacterActionSystemTransform = temporalSharedActionContent.secondCharacterActionSystem.transform;

                    if (currentZone.useMinDistanceToActivateActionFirstCharacter) {
                        float currentDistanceToTarget =
                            GKC_Utils.distance (firstCharacterActionSystemTransform.position,
                            temporalFirstCharacter.transform.position);

                        if (currentDistanceToTarget > currentZone.minDistanceToActivateActionFirstCharacter) {
                            if (showDebugPrint) {
                                print ("first character can't play animation for distance");
                            }

                            currentZoneConditionsResult = false;
                        }
                    }

                    if (currentZoneConditionsResult) {
                        if (currentZone.useMinAngleToActivateActionFirstCharacter) {
                            float currentAngleWithTarget = Vector3.SignedAngle (temporalFirstCharacter.transform.forward,
                                firstCharacterActionSystemTransform.forward, temporalFirstCharacter.transform.up);

                            if (Mathf.Abs (currentAngleWithTarget) > currentZone.minAngleToActivateActionFirstCharacter) {
                                if (showDebugPrint) {
                                    print ("first character can't play animation for angle " + currentAngleWithTarget);
                                }

                                currentZoneConditionsResult = false;
                            } else {
                                if (showDebugPrint) {
                                    print ("first character can play animation for angle " + currentAngleWithTarget);
                                }
                            }
                        }
                    }

                    if (currentZone.useMinAngleToActivateActionOnCharactersDirection) {
                        Vector3 transformDirection = Vector3.zero;

                        if (temporalSharedActionContent.adjustPositionToFirstCharacter) {
                            transformDirection = firstCharacter.transform.position - secondCharacter.transform.position;
                        }

                        if (temporalSharedActionContent.adjustPositionToSecondCharacter) {
                            transformDirection = secondCharacter.transform.position - firstCharacter.transform.position;
                        }

                        Vector3 heading = transformDirection;

                        float distance = heading.magnitude;
                        Vector3 charactersDirection = heading / distance;

                        float directionAngle = Vector3.Angle (temporalSharedActionContent.transform.forward,
                            charactersDirection);

                        if (showDebugPrint) {
                            print (temporalSharedActionContent.transform.forward + " " + charactersDirection + "  " + directionAngle);
                        }

                        if (directionAngle > currentZone.minAngleToActivateActionOnCharactersDirection) {
                            currentZoneConditionsResult = false;

                            if (showDebugPrint) {
                                print ("angle of characters out of range for zone, cancelling");
                            }
                        }
                    }

                    if (currentZoneConditionsResult) {
                        if (currentZone.useMinDistanceToActivateActionSecondCharacter) {
                            float currentDistanceToTarget =
                            GKC_Utils.distance (secondCharacterActionSystemTransform.position,
                            temporalSecondCharacter.transform.position);

                            if (currentDistanceToTarget > currentZone.minDistanceToActivateActionSecondCharacter) {
                                if (showDebugPrint) {
                                    print ("second character can't play animation for distance");
                                }

                                currentZoneConditionsResult = false;
                            }
                        }
                    }

                    if (currentZoneConditionsResult) {
                        if (currentZone.useMinAngleToActivateActionSecondCharacter) {
                            float currentAngleWithTarget = Vector3.SignedAngle (temporalSecondCharacter.transform.forward,
                                secondCharacterActionSystemTransform.forward, temporalSecondCharacter.transform.up);

                            if (Mathf.Abs (currentAngleWithTarget) > currentZone.minAngleToActivateActionSecondCharacter) {
                                if (showDebugPrint) {
                                    print ("second character can't play animation for angle " + currentAngleWithTarget);
                                }

                                currentZoneConditionsResult = false;
                            } else {
                                if (showDebugPrint) {
                                    print ("second character can play animation for angle " + currentAngleWithTarget);
                                }
                            }
                        }
                    }

                    if (currentZoneConditionsResult) {
                        if (currentActionInfo.sharedActionGameObject != null) {
                            currentActionInfo.sharedActionGameObject.SetActive (false);
                        }

                        currentSharedActionContent = temporalSharedActionContent;

                        sharedActionGameObject = currentSharedActionContent.gameObject;

                        firstCharacterIsPlayer = temporalFirstCharacterIsPlayer;

                        setThisCharacterAsFirstCharacter = temporalSetThisCharacterAsFirstCharacter;

                        firstCharacter = temporalFirstCharacter;

                        secondCharacter = temporalSecondCharacter;

                        checkForZoneResult = true;

                        if (currentZone.disableSharedActionZoneAfterUse) {
                            currentZone.setSharedActionZoneEnabledState (false);
                        }
                    } else {
                        if (showDebugPrint) {
                            print ("conditions for zone not achieved " + temporalSharedActionContent.gameObject.name);
                        }
                    }
                } else {

                }
            } else {
                if (showDebugPrint) {
                    print ("shared action zone found but conditions not achieved, cancelling");
                }
            }
        }

        return checkForZoneResult;
    }

    public void setSharedActionEnabledState (bool state)
    {
        sharedActionEnabled = state;
    }

    void checkEventOnSharedActionResult (bool state, string actionName)
    {
        if (showDebugPrint) {
            print ("checking events on shared action result " + state + " " + actionName);
        }

        if (state) {
            if (useEventsOnSharedActionActivated) {
                eventsOnSharedActionActivated.Invoke ();
            }
        } else {
            if (useEventsOnSharedActionConditionNotAchieved) {
                eventsOnSharedActionConditionNotAchieved.Invoke ();
            }
        }

        checkSharedActionActivatedResult (actionName, state);
    }

    void checkSharedActionActivatedResult (string actionName, bool sharedActionResult)
    {
        int currentActionIndex = remoteSharedActionSystemInfoList.FindIndex (s => s.Name.Equals (actionName));

        if (currentActionIndex > -1) {
            remoteSharedActionSystemInfo currentRemoteSharedActionSystemInfo = remoteSharedActionSystemInfoList [currentActionIndex];

            if (currentRemoteSharedActionSystemInfo.checkSharedActionSystmeResultEnabled) {
                if (showDebugPrint) {
                    print ("checkSharedActionActivatedResult " + actionName + " " + sharedActionResult);
                }

                if (sharedActionResult) {
                    currentRemoteSharedActionSystemInfo.eventOnSharedActionActivated.Invoke ();
                } else {
                    currentRemoteSharedActionSystemInfo.eventOnSharedActionNotActivated.Invoke ();
                }
            }
        }
    }

    void checkAIAroundState (bool state)
    {
        if (pauseAIAroundDuringActionActive) {
            List<GameObject> characterList = new List<GameObject> ();

            characterList.Add (firstCharacter);
            characterList.Add (secondCharacter);

            if (pauseAIAttackAroundDuringActionActive) {
                GKC_Utils.pauseOrResumeAttackActiveStateOnAllAIOnScene (characterList, state, enableRandomWalkOnAIAroundDuringActionActive);
            } else {

                GKC_Utils.pauseOrResumeAIOnSceneWithExceptionList (state, 0, characterList);
            }
        }
    }

    public void setSharedActionEnabledStateFromEditor (bool state)
    {
        setSharedActionEnabledState (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Shared Action System " + gameObject.name, gameObject);
    }

    [System.Serializable]
    public class sharedActionSystemInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public bool sharedActionEnabled = true;

        public bool setThisCharacterAsFirstCharacter;

        [Space]

        public bool checkMinWaitTimeToPlaySharedAction;
        public float minWaitTimeToPlaySharedAction;

        [Space]
        [Header ("Probability Settings")]
        [Space]

        public bool useProbabilityToUseAction;
        [Range (0, 100)] public float probabilityToUseAction;

        [Space]
        [Header ("Shared Action Zones Settings")]
        [Space]

        public bool searchSharedActionZonesOnSceneEnabled;

        public bool sharedActionNamesMustMatch;

        [Space]

        public bool useSharedActionNameListToCheck;
        public List<string> sharedActionNameListToCheck;

        [Space]
        [Header ("Check Character Orientation")]
        [Space]

        public bool checkCharacterOrientation;
        public bool actionToPlayInFrontOfCharacter;

        [Space]

        public bool useExtraSharedActionIfIncorrectOrientation;
        public string extraSharedActionIfIncorrectOrientationName;

        [Space]
        [Header ("Debug")]
        [Space]

        public sharedActionContent mainSharedActionContent;

        public GameObject sharedActionGameObject;

        [Space]
        [Header ("Components")]
        [Space]

        public GameObject sharedActionPrefab;
    }

    [System.Serializable]
    public class randomSharedActionInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public bool enabled;

        public List<string> randomSharedActionNameList;
    }

    [System.Serializable]
    public class sharedActionTemporalInfo
    {
        public string Name;
        public float lastTimeActionPlayed;
    }
}
