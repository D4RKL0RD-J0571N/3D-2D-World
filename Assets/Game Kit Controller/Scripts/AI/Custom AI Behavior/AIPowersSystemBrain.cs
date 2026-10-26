using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class AIPowersSystemBrain : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool systemEnabled = true;

    [Space]
    [Header ("Attack Enabled Settings")]
    [Space]

    public bool attackEnabled;

    [Space]
    [Header ("Attack Settings")]
    [Space]
   
    public float attackRate = 0.17f;

    public bool setWaitTimeOnFirePowerBlast;
    public Vector2 waitTimeRangeOnFirePowerBlast;

    public Vector2 fireTimeRangeBeforeWaitForNextPowerBlast;

    [Space]

    public bool checkToForceAttackIfNotUsedLongTimeAgo;

    public float maxWaitTimeToForceAttackIfNotUsedLongTimeAgo = 10;

    [Space]
    [Header ("Roll/Dodge Settings")]
    [Space]

    public bool rollEnabled;

    public Vector2 randomRollWaitTime;

    public float minWaitTimeAfterRollActive = 1.3f;

    public List<Vector2> rollMovementDirectionList = new List<Vector2> ();

    [Space]
    [Header ("Random Walk Settings")]
    [Space]

    public bool randomWalkEnabled;

    public Vector2 randomWalkWaitTime;
    public Vector2 randomWalkDuration;
    public Vector2 randomWalkRadius;

    [Space]

    public bool pauseFirePowerDuringWalkEnabled = true;

    public bool ignoreRandomWalkInFrontDirectionEnabled = true;

    [Space]
    [Header ("Jump To Target Settings")]
    [Space]

    public bool jumpToTargetOnRangeDistance;
    public Vector2 rangeDistanceToJumpToTarget;
    public bool avoidAttackOnAir = true;
    public float minTimeToJumpAgain = 4;
    public bool useJumpProbability;
    [Range (0, 100)] public float jumpProbability;

    [Space]

    public bool setNewJumpForce;
    public float newJumpForce;
    public bool setNewJumpForceMinDistance;
    public float newJumpForceMinDistance;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public bool changeWaitToActivateAttackEnabled = true;

    public bool activateRandomWalkIfWaitToActivateAttackActive;

    public bool checkIfAttackInProcessBeforeCallingNextAttack = true;

    public bool useWaitToActivateAttackActiveCounter;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool systemActive;

    public bool aimingPower;

    public bool shootingPower;

    public bool waitingForAttackActive;
    float currentRandomTimeToAttack;

    public bool canUseAttackActive;

    [Space]

    public bool walkActive;

    public bool waitingWalkActive;

    public bool waitingRollActive;

    [Space]

    public bool attackStatePaused;

    public bool insideMinDistanceToAttack;

    public bool behaviorStatesPaused;

    public bool waitToActivateAttackActive;

    public int waitToActivateAttackActiveCounter = 0;

    public bool turnBasedCombatActionActive;

    public bool ignoreOtherActionsToForceAttackActive;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventsOnCombatActive;
    public UnityEvent eventOnCombatActive;
    public UnityEvent eventOnCombatDeactivate;

    [Space]

    public eventParameters.eventToCallWithString eventToChangeAttackMode;

    [Space]
    [Header ("Components")]
    [Space]

    public otherPowers mainOtherPowers;
    public dashSystem mainDashSystem;
    public findObjectivesSystem mainFindObjectivesSystem;
    public AINavMesh mainAINavmeshManager;


    float lastTimeAttack;

    int currentAttackTypeIndex;

    int currentAttackIndex;

    int currentAttackTypeToAlternateIndex;


    float lastTimeRollActive;

    float lastTimeWaitRollActive;

    float currentRollWaitTime;


    float lastTimeWaitWalkActive;
    float currentWalkTime;
    float lastTimeWalkActive;

    float currentWalkDuration;
    float currentWalkRadius;

    bool rollCoolDownActive;

    float currentPauseAttackStateDuration;
    float lastTimeAttackPauseWithDuration;

    float randomWaitTime;

    float lastTimeAttackAtDistance;

    float currentPathDistanceToTarget;
    float minDistanceToAim;
    float minDistanceToShoot;

    bool AIPaused;

    bool cancelCheckAttackState;

    float lastTimeJump;

    bool waitingForNextFirePowerBlastActive;
    float lastTimeWaitingForNextFirePowerBlastActive;

    float currentWaitTimeRangeOnFirePowerBlast;

    float currentFireTimeRangeBeforeWaitForNextPowerBlast;

    float lastTimeSettingFirePowerBlast;


    public void updateAI ()
    {
        if (systemActive) {
            AIPaused = mainFindObjectivesSystem.isAIPaused ();

            if (!AIPaused) {
                if (walkActive) {
                    if (Time.time > lastTimeWalkActive + 1) {
                        if (Time.time > lastTimeWalkActive + currentWalkDuration ||
                            mainFindObjectivesSystem.getRemainingPathDistanceToTarget () < 0.5f ||
                            mainFindObjectivesSystem.getCurrentDistanceToTarget () < 0.5f) {
                            resetRandomWalkState ();
                        }
                    }
                }

                if (jumpToTargetOnRangeDistance) {
                    checkJumpState ();
                }
            }
        }
    }

    void checkJumpState ()
    {
        if (mainFindObjectivesSystem.isActionActive ()) {
            return;
        }

        if (!insideMinDistanceToAttack &&
            mainFindObjectivesSystem.isAIOnGround () &&
            mainFindObjectivesSystem.isOnSpotted () &&
            (lastTimeJump == 0 || Time.time > minTimeToJumpAgain + lastTimeJump)) {

            float distanceToTarget = mainFindObjectivesSystem.getDistanceToTarget ();

            if (distanceToTarget > rangeDistanceToJumpToTarget.x && distanceToTarget < rangeDistanceToJumpToTarget.y) {
                bool activateJumpResult = true;

                if (useJumpProbability) {
                    float currentProbability = Random.Range (0, 100);

                    if (currentProbability > jumpProbability) {
                        useJumpProbability = false;
                    }
                }

                if (activateJumpResult) {
                    bool setNewJumpForceResult = setNewJumpForce;

                    if (setNewJumpForceMinDistance) {
                        if (distanceToTarget < newJumpForceMinDistance) {
                            setNewJumpForceResult = false;
                        }
                    }

                    if (setNewJumpForceResult) {
                        mainFindObjectivesSystem.setJumpPower (newJumpForce);

                        mainFindObjectivesSystem.activateJumpExternally (0);
                    } else {
                        mainFindObjectivesSystem.setOriginalJumpPower ();

                        mainFindObjectivesSystem.activateJumpExternally (distanceToTarget);
                    }
                }

                lastTimeJump = Time.time;
            }
        }
    }

    public void resetRandomWalkState ()
    {
        mainFindObjectivesSystem.setRandomWalkPositionState (0);

        waitingWalkActive = false;

        walkActive = false;

        lastTimeWalkActive = Time.time;
    }

    public void resetRollState ()
    {
        waitingRollActive = false;

        lastTimeRollActive = Time.time;
    }

    public void resetStates ()
    {
        resetRandomWalkState ();

        resetRollState ();
    }

    public void checkIfResetStatsOnRandomWalk ()
    {
        if (walkActive) {
            resetStates ();
        }
    }

    public void checkRollState ()
    {
        if (ignoreOtherActionsToForceAttackActive) {
            return;
        }

        if (rollEnabled) {

            if (walkActive) {
                return;
            }

            if (!insideMinDistanceToAttack) {
                resetRollState ();

                lastTimeRollActive = 0;

                return;
            }

            if (waitingRollActive) {
                if (Time.time > lastTimeWaitRollActive + currentRollWaitTime) {

                    int randomRollMovementDirection = Random.Range (0, rollMovementDirectionList.Count);

                    Vector3 dashDirection = rollMovementDirectionList [randomRollMovementDirection];

                    if (mainFindObjectivesSystem.isLocked2_5dModeActive ()) {
                        randomRollMovementDirection = Random.Range (0, 2);

                        if (randomRollMovementDirection == 0) {
                            dashDirection = Vector3.forward;
                        } else {
                            dashDirection = -Vector3.forward;
                        }
                    }

                    mainDashSystem.activateDashStateWithCustomDirection (dashDirection);

                    resetRollState ();
                }
            } else {
                if (Time.time > lastTimeRollActive + randomWaitTime) {
                    currentRollWaitTime = Random.Range (randomRollWaitTime.x, randomRollWaitTime.y);

                    lastTimeWaitRollActive = Time.time;

                    waitingRollActive = true;

                    randomWaitTime = Random.Range (0.1f, 0.5f);
                }
            }
        }
    }

    public void checkWalkState ()
    {
        if (ignoreOtherActionsToForceAttackActive) {
            return;
        }

        if (randomWalkEnabled) {

            rollCoolDownActive = Time.time < lastTimeRollActive + 0.7f;

            if (rollCoolDownActive) {
                return;
            }

            if (waitingWalkActive) {
                if (!walkActive) {

                    if (Time.time > lastTimeWaitWalkActive + currentWalkTime) {
                        mainFindObjectivesSystem.setignoreRandomWalkInFrontDirectionEnabledState (ignoreRandomWalkInFrontDirectionEnabled);

                        mainFindObjectivesSystem.setRandomWalkPositionState (currentWalkRadius);

                        lastTimeWalkActive = Time.time;

                        walkActive = true;
                    }
                }
            } else {
                currentWalkTime = Random.Range (randomWalkWaitTime.x, randomWalkWaitTime.y);

                lastTimeWaitWalkActive = Time.time;

                waitingWalkActive = true;

                currentWalkDuration = Random.Range (randomWalkDuration.x, randomWalkDuration.y);

                currentWalkRadius = Random.Range (randomWalkRadius.x, randomWalkRadius.y);

                walkActive = false;
            }
        }
    }

    public void updateInsideMinDistance (bool newInsideMinDistanceToAttack)
    {
        insideMinDistanceToAttack = newInsideMinDistanceToAttack;

        if (mainFindObjectivesSystem.isAttackAlwaysOnPlace ()) {
            insideMinDistanceToAttack = true;
        }

        if (insideMinDistanceToAttack) {

        } else {
            if (aimingPower && !mainFindObjectivesSystem.isAttackTargetDirectlyActive ()) {
                setAimState (false);
            }
        }
    }

    void updateLookAtTargetIfBehaviorPaused ()
    {
        if (mainFindObjectivesSystem.isAttackTargetDirectlyActive ()) {
            mainFindObjectivesSystem.setLookingAtTargetPositionState (true);

            mainFindObjectivesSystem.lookAtCurrentPlaceToShoot ();
        } else {
            currentPathDistanceToTarget = mainFindObjectivesSystem.currentPathDistanceToTarget;
            minDistanceToAim = mainFindObjectivesSystem.minDistanceToAim;
            minDistanceToShoot = mainFindObjectivesSystem.minDistanceToShoot;

            bool useHalfMinDistance = mainAINavmeshManager.useHalfMinDistance;

            if (useHalfMinDistance) {
                mainFindObjectivesSystem.setLookingAtTargetPositionState (false);

                cancelCheckAttackState = true;
            } else {

                if (currentPathDistanceToTarget <= minDistanceToAim) {
                    mainFindObjectivesSystem.setLookingAtTargetPositionState (true);

                    mainFindObjectivesSystem.lookAtCurrentPlaceToShoot ();
                } else {
                    if (currentPathDistanceToTarget >= minDistanceToAim + 1.5f) {

                        mainFindObjectivesSystem.setLookingAtTargetPositionState (false);

                        cancelCheckAttackState = true;
                    } else {
                        if (mainFindObjectivesSystem.isLookingAtTargetPosition ()) {
                            mainFindObjectivesSystem.lookAtCurrentPlaceToShoot ();
                        }
                    }
                }
            }
        }
    }

    public void updateBehavior ()
    {
        if (!systemActive) {
            return;
        }

        if (AIPaused) {
            return;
        }

        if (behaviorStatesPaused) {
            updateLookAtTargetIfBehaviorPaused ();

            return;
        }

        if (checkToForceAttackIfNotUsedLongTimeAgo) {
            if (lastTimeAttack > 0 && !ignoreOtherActionsToForceAttackActive) {
                if (Time.time > maxWaitTimeToForceAttackIfNotUsedLongTimeAgo + lastTimeAttack) {
                    ignoreOtherActionsToForceAttackActive = true;

                    resetRandomWalkState ();

                    resetRollState ();

                    if (showDebugPrint) {
                        print ("too much time since last attack active, reseting other states" +
                            " to force an attack");
                    }
                }
            }
        }

        checkWalkState ();

        bool ignoreRollStateResult = false;

        if (walkActive) {
            if (pauseFirePowerDuringWalkEnabled) {
                return;
            } else {
                ignoreRollStateResult = true;
            }
        }

        if (!ignoreRollStateResult) {
            checkRollState ();

            if (rollEnabled) {
                if (Time.time < lastTimeRollActive + minWaitTimeAfterRollActive) {
                    return;
                }
            }
        }

        cancelCheckAttackState = false;

        if (mainFindObjectivesSystem.isAttackTargetDirectlyActive ()) {
            mainFindObjectivesSystem.setLookingAtTargetPositionState (true);

            mainFindObjectivesSystem.lookAtCurrentPlaceToShoot ();

            if (!aimingPower) {
                setAimState (true);
            }

            if (aimingPower) {
                if (mainFindObjectivesSystem.checkIfMinimumAngleToAttack () &&
                    !mainOtherPowers.isActionActiveInPlayer () &&
                    mainOtherPowers.canPlayerMove ()) {

                    bool canShootResult = getCanShootResult ();

                    if (canShootResult) {
                        shootTarget ();
                    } else {
                        if (shootingPower) {
                            stopShootPower ();
                        }
                    }
                }
            }
        } else {
            currentPathDistanceToTarget = mainFindObjectivesSystem.currentPathDistanceToTarget;
            minDistanceToAim = mainFindObjectivesSystem.minDistanceToAim;
            minDistanceToShoot = mainFindObjectivesSystem.minDistanceToShoot;

            bool useHalfMinDistance = mainAINavmeshManager.useHalfMinDistance;

            if (useHalfMinDistance) {
                if (aimingPower) {
                    setAimState (false);
                }

                mainFindObjectivesSystem.setLookingAtTargetPositionState (false);

                cancelCheckAttackState = true;
            } else {

                if (currentPathDistanceToTarget <= minDistanceToAim) {
                    if (!aimingPower) {
                        setAimState (true);
                    }

                    mainFindObjectivesSystem.setLookingAtTargetPositionState (true);

                    mainFindObjectivesSystem.lookAtCurrentPlaceToShoot ();
                } else {
                    if (currentPathDistanceToTarget >= minDistanceToAim + 1.5f) {
                        if (aimingPower) {
                            setAimState (false);
                        }

                        mainFindObjectivesSystem.setLookingAtTargetPositionState (false);

                        cancelCheckAttackState = true;
                    } else {
                        if (mainFindObjectivesSystem.isLookingAtTargetPosition ()) {
                            mainFindObjectivesSystem.lookAtCurrentPlaceToShoot ();
                        }
                    }
                }
            }
        }

        if (waitToActivateAttackActive) {
            if (activateRandomWalkIfWaitToActivateAttackActive) {
                if (!walkActive) {
                    currentWalkTime = Random.Range (randomWalkWaitTime.x, randomWalkWaitTime.y);

                    currentWalkDuration = Random.Range (randomWalkDuration.x, randomWalkDuration.y);

                    currentWalkRadius = Random.Range (randomWalkRadius.x, randomWalkRadius.y);

                    mainFindObjectivesSystem.setignoreRandomWalkInFrontDirectionEnabledState (ignoreRandomWalkInFrontDirectionEnabled);

                    mainFindObjectivesSystem.setRandomWalkPositionState (currentWalkRadius);

                    lastTimeWalkActive = Time.time;

                    walkActive = true;
                }
            }

            return;
        }

        checkAttackState ();
    }

    public void checkAttackState ()
    {
        if (!attackEnabled) {
            return;
        }

        if (!insideMinDistanceToAttack) {
            return;
        }

        if (currentPauseAttackStateDuration > 0) {
            if (Time.time > currentPauseAttackStateDuration + lastTimeAttackPauseWithDuration) {

                attackStatePaused = false;

                currentPauseAttackStateDuration = 0;
            } else {
                return;
            }
        }


        if (!canUseAttackActive) {
            return;
        }

        if (!aimingPower && !cancelCheckAttackState) {
            setAimState (true);
        }

        if (jumpToTargetOnRangeDistance && avoidAttackOnAir) {
            if (!mainFindObjectivesSystem.isAIOnGround ()) {
                return;
            }
        }

        if (Time.time > attackRate + lastTimeAttackAtDistance && mainOtherPowers.canPlayerMove ()) {
            if (aimingPower) {
                if (!mainOtherPowers.isAimingPower ()) {
                    aimingPower = false;

                    setAimState (true);
                }

                if (currentPathDistanceToTarget <= minDistanceToShoot &&
                    mainFindObjectivesSystem.checkIfMinimumAngleToAttack () &&
                    !mainOtherPowers.isActionActiveInPlayer ()) {

                    bool canShootResult = getCanShootResult ();

                    if (canShootResult) {
                        shootTarget ();
                    } else {
                        if (shootingPower) {
                            stopShootPower ();
                        }
                    }
                }
            }

            lastTimeAttackAtDistance = Time.time;
        }
    }

    bool getCanShootResult ()
    {
        bool canShootResult = true;

        if (setWaitTimeOnFirePowerBlast) {
            if (waitingForNextFirePowerBlastActive) {

                if (Time.time > lastTimeWaitingForNextFirePowerBlastActive + currentWaitTimeRangeOnFirePowerBlast) {
                    waitingForNextFirePowerBlastActive = false;
                } else {
                    canShootResult = false;
                }
            } else {
                if (currentFireTimeRangeBeforeWaitForNextPowerBlast == 0) {
                    currentFireTimeRangeBeforeWaitForNextPowerBlast = Random.Range (fireTimeRangeBeforeWaitForNextPowerBlast.x,
                        fireTimeRangeBeforeWaitForNextPowerBlast.y);

                    lastTimeSettingFirePowerBlast = Time.time;
                } else {
                    if (Time.time > currentFireTimeRangeBeforeWaitForNextPowerBlast + lastTimeSettingFirePowerBlast) {
                        lastTimeWaitingForNextFirePowerBlastActive = Time.time;

                        waitingForNextFirePowerBlastActive = true;

                        currentFireTimeRangeBeforeWaitForNextPowerBlast = 0;

                        currentWaitTimeRangeOnFirePowerBlast = Random.Range (waitTimeRangeOnFirePowerBlast.x,
                            waitTimeRangeOnFirePowerBlast.y);
                    }
                }
            }
        }

        //		print (canShootResult);

        return canShootResult;
    }

    void resetSetWaitTimeOnFirePowerBlastValues ()
    {
        currentFireTimeRangeBeforeWaitForNextPowerBlast = 0;
        waitingForNextFirePowerBlastActive = false;
        lastTimeSettingFirePowerBlast = 0;
    }

    public void updateAIAttackState (bool newCanUseAttackActiveState)
    {
        canUseAttackActive = newCanUseAttackActiveState;
    }

    public void setSystemActiveState (bool state)
    {
        if (!systemEnabled) {
            return;
        }

        systemActive = state;

        checkEventsOnCombatStateChange (systemActive);

        lastTimeAttack = 0;

        waitToActivateAttackActive = false;

        waitToActivateAttackActiveCounter = 0;
    }

    void checkEventsOnCombatStateChange (bool state)
    {
        if (useEventsOnCombatActive) {
            if (state) {
                eventOnCombatActive.Invoke ();
            } else {
                eventOnCombatDeactivate.Invoke ();
            }
        }
    }

    public void pauseAttackDuringXTime (float newDuration)
    {
        currentPauseAttackStateDuration = newDuration;

        lastTimeAttackPauseWithDuration = Time.time;

        attackStatePaused = true;
    }

    public void setWaitToActivateAttackActiveState (bool state)
    {
        if (!changeWaitToActivateAttackEnabled) {
            return;
        }

        if (useWaitToActivateAttackActiveCounter) {
            if (state) {
                waitToActivateAttackActiveCounter++;
            } else {
                waitToActivateAttackActiveCounter--;

                if (waitToActivateAttackActiveCounter < 0) {
                    waitToActivateAttackActiveCounter = 0;
                }
            }

            if (waitToActivateAttackActiveCounter > 0) {
                if (waitToActivateAttackActive) {
                    return;
                } else {
                    state = true;
                }
            } else {
                if (!waitToActivateAttackActive) {
                    return;
                } else {
                    state = false;
                }
            }
        }

        waitToActivateAttackActive = state;

        if (activateRandomWalkIfWaitToActivateAttackActive) {
            if (state) {
                currentWalkTime = Random.Range (randomWalkWaitTime.x, randomWalkWaitTime.y);

                currentWalkDuration = Random.Range (randomWalkDuration.x, randomWalkDuration.y);

                currentWalkRadius = Random.Range (randomWalkRadius.x, randomWalkRadius.y);

                mainFindObjectivesSystem.setignoreRandomWalkInFrontDirectionEnabledState (ignoreRandomWalkInFrontDirectionEnabled);

                mainFindObjectivesSystem.setRandomWalkPositionState (currentWalkRadius);

                lastTimeWalkActive = Time.time;

                walkActive = true;
            } else {
                if (walkActive) {
                    resetRandomWalkState ();
                }
            }
        }
    }

    public void resetBehaviorStates ()
    {
        resetStates ();

        waitingForAttackActive = false;

        setAimState (false);

        insideMinDistanceToAttack = false;
    }

    public void setAimState (bool state)
    {
        if (showDebugPrint) {
            print ("setting aim active state " + state);
        }

        if (state) {
            if (!aimingPower) {
                startAimPower ();

                lastTimeAttackAtDistance = Time.time;
            }
        } else {
            if (aimingPower) {
                stopAimPower ();

                stopShootPower ();
            }
        }

        aimingPower = state;

        if (!aimingPower) {
            resetSetWaitTimeOnFirePowerBlastValues ();
        }
    }

    public void shootTarget ()
    {
        startShootPower ();

        holdShootPower ();

        shootingPower = true;

        lastTimeAttack = Time.time;

        ignoreOtherActionsToForceAttackActive = false;
    }

    public void resetAttackState ()
    {
        aimingPower = false;

        resetSetWaitTimeOnFirePowerBlastValues ();
    }

    public void stopAim ()
    {
        if (aimingPower) {
            setAimState (false);
        }
    }

    public void disableOnSpottedState ()
    {

    }

    public void startAimPower ()
    {
        if (!aimingPower) {
            mainOtherPowers.inputSetAimPowerState (true);
        }
    }

    public void stopAimPower ()
    {
        if (aimingPower) {
            mainOtherPowers.inputSetAimPowerState (false);
        }
    }

    public void startShootPower ()
    {
        mainOtherPowers.inputHoldOrReleaseShootPower (true);
    }

    public void holdShootPower ()
    {
        mainOtherPowers.inputHoldShootPower ();
    }

    public void stopShootPower ()
    {
        mainOtherPowers.inputHoldOrReleaseShootPower (false);

        shootingPower = false;
    }

    public void setBehaviorStatesPausedState (bool state)
    {
        behaviorStatesPaused = state;

        if (behaviorStatesPaused) {
            resetAttackState ();
        }
    }

    public void stopCurrentAttackInProcess ()
    {
        if (aimingPower) {
            stopAim ();
        }
    }

    public void setTurnBasedCombatActionActiveState (bool state)
    {
        turnBasedCombatActionActive = state;
    }

    public void checkAIBehaviorStateOnCharacterSpawn ()
    {

    }

    public void checkAIBehaviorStateOnCharacterDespawn ()
    {

    }

    public void setMaxWaitTimeToForceAttackIfNotUsedLongTimeAgoValue (float newValue)
    {
        maxWaitTimeToForceAttackIfNotUsedLongTimeAgo = newValue;
    }

    public void setCheckToForceAttackIfNotUsedLongTimeAgoState (bool state)
    {
        checkToForceAttackIfNotUsedLongTimeAgo = state;
    }
    public void checkIfDisableCurrentWeaponToChangeAttackMode (string newModeName)
    {
        changeAttackMode (newModeName);
    }

    public void changeCurrentAttackMode (string newModeName)
    {
        changeAttackMode (newModeName);
    }

    void changeAttackMode (string newModeName)
    {
        if (!systemActive) {
            return;
        }

        stopAttackState ();

        eventToChangeAttackMode.Invoke (newModeName);
    }

    public void stopAttackState ()
    {
        if (!systemActive) {
            return;
        }

        print (systemActive + " " + aimingPower + " " + mainOtherPowers.isAimingPower ());

        if (mainOtherPowers.isAimingPower ()) {
            aimingPower = true;
        }

        if (aimingPower) {
            bool playerIsBusy = mainOtherPowers.isActionActiveInPlayer () ||
                    mainOtherPowers.canPlayerMove () || 
                    mainOtherPowers.isAimingPower ();

            if (playerIsBusy) {
                print ("player is busy");

                resetBehaviorStates ();

            } else {
                print ("player is not busy");
            }
        }
    }

    public void setAttackEnabledState (bool state)
    {
        attackEnabled = state;
    }

    public void setAttackEnabledStateFromEditor (bool state)
    {
        setAttackEnabledState (state);

        updateComponent ();
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update AI Brain " + gameObject.name, gameObject);
    }
}