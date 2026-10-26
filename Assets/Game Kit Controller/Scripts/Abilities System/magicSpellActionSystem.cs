using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class magicSpellActionSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool magicSystemEnabled = true;

    public bool resetIndexIfNotAttackAfterDelay;

    public float delayToResetIndexAfterAttack;

    public float generalEnergyUseMultiplier = 1;

    [Space]
    [Header ("Attack Selection Settings")]
    [Space]

    public bool useAttackTypes;

    public bool useRandomAttackIndex;

    public bool useEnergyOnMagic;

    public bool useNextMagicSpellOnListForNextAttackEnabled = true;

    [Space]

    public bool avoidActivateAbilityIfAnotherInProcessEnabled = true;

    [Space]
    [Header ("Movement Settings")]
    [Space]

    public bool useStrafeMode;
    public int strafeIDUsed;
    public bool setPreviousStrafeModeOnDisableMagicSystem;

    public bool activateStrafeModeOnLockOnTargetActive;
    public bool deactivateStrafeModeOnLockOnTargetDeactivate;

    public bool toggleStrafeModeIfRunningActive;

    public bool setNewCrouchID;
    public int crouchIDUsed;


    [Space]
    [Header ("Magic Action List Settings")]
    [Space]

    public List<magicSpellActionInfo> magicSpellActionInfoList = new List<magicSpellActionInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    [Space]

    public bool previousStrafeMode;
    public int previousStrafeID = -1;

    public bool magicSystemActive;

    [Space]

    public int currentAttackIndex;

    public bool attackInProcess;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsOnAttack;
    public UnityEvent eventOnAttackStart;
    public UnityEvent eventOnAttackEnd;

    public bool useEventsOnEnableDisableMagicSystem;
    public UnityEvent eventOnEnableMagicSystem;
    public UnityEvent eventOnDisableMagicSystem;

    [Space]
    [Header ("Components")]
    [Space]

    public otherPowers mainOtherPowers;

    public playerController mainPlayerController;

    public remoteEventSystem mainRemoteEventSystem;


    magicSpellActionInfo currentMagicSpellActionInfo;

    float lastTimeAttackActive;

    float lastTimeAttackComplete;

    Coroutine magicCoroutine;

    Coroutine magicEventCoroutine;


    public void enableOrDisableMagicSystem (bool state)
    {
        if (!magicSystemEnabled) {
            return;
        }

        bool previouslyActive = magicSystemActive;

        magicSystemActive = state;

        checkEventsOnEnableDisableMagicSystem (magicSystemActive);

        mainOtherPowers.enableOrDisablePowersInfoPanel (magicSystemActive);

        if (magicSystemActive) {
            checkMagicStateAtStart ();

        } else {
            if (previouslyActive) {
                checkMagicStateAtEnd ();

                resumeState ();
            }
        }
    }

    public void useMagicSpell (string magicTypeName)
    {
        if (showDebugPrint) {
            print ("input activated");
        }

        if (!magicSystemActive) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        if (currentAttackIndex > 0 && currentMagicSpellActionInfo != null) {
            //				print (currentAttackInfo.minDelayBeforeNextAttack);

            if (Time.time < lastTimeAttackActive + currentMagicSpellActionInfo.minDelayBeforeNextAttack) {
                return;
            }
        }

        if (attackInProcess && avoidActivateAbilityIfAnotherInProcessEnabled) {
            if (showDebugPrint) {
                print ("attack in process, cancelling");
            }

            return;
        }

        if (resetIndexIfNotAttackAfterDelay && !attackInProcess) {
            if (Time.time > lastTimeAttackComplete + delayToResetIndexAfterAttack) {
                //				print ("reset attack index");

                currentAttackIndex = 0;
            }
        }

        if (useAttackTypes) {
            if (showDebugPrint) {
                print ("attack all conditions checked");
            }

            int numberOfAttacksSameType = 0;

            int numberOfAttacksAvailable = magicSpellActionInfoList.Count;

            for (int i = 0; i < numberOfAttacksAvailable; i++) {
                if (magicSpellActionInfoList [i].magicTypeName.Equals (magicTypeName) &&
                    magicSpellActionInfoList [i].magicEnabled) {
                    numberOfAttacksSameType++;
                }
            }

            if (numberOfAttacksSameType == 1) {
                bool cancelAttack = false;

                if (attackInProcess &&
                    currentMagicSpellActionInfo != null &&
                    currentMagicSpellActionInfo.magicTypeName.Equals (magicTypeName)) {
                    cancelAttack = true;

                    if (showDebugPrint) {
                        print ("same attack type already in process with just 1 type configured, so " +
                            "the system is trying to activate the same magic attack which is already in process");
                    }
                }

                if (Time.time < lastTimeAttackComplete + 0.4f) {
                    cancelAttack = true;
                }

                if (cancelAttack) {
                    if (showDebugPrint) {
                        print ("just one attack type available and it is in process, avoiding to play it again");
                    }

                    return;
                }
            }

            bool attackFound = false;

            setNextAttackIndex ();

            while (!attackFound) {
                currentMagicSpellActionInfo = magicSpellActionInfoList [currentAttackIndex];

                //				print (currentAttackInfo.magicTypeName + " " + magicTypeName);

                if (currentMagicSpellActionInfo.magicTypeName.Equals (magicTypeName) &&
                    currentMagicSpellActionInfo.magicEnabled) {

                    attackFound = true;
                } else {
                    setNextAttackIndex ();

                    numberOfAttacksAvailable--;

                    if (numberOfAttacksAvailable < 0) {
                        return;
                    }
                }
            }
        } else {
            bool attackFound = false;

            int numberOfAttacksAvailable = magicSpellActionInfoList.Count;

            while (!attackFound) {
                currentMagicSpellActionInfo = magicSpellActionInfoList [currentAttackIndex];

                //				print (currentAttackInfo.magicTypeName + " " + magicTypeName);

                if (currentMagicSpellActionInfo.magicEnabled) {
                    attackFound = true;
                } else {
                    setNextAttackIndex ();

                    numberOfAttacksAvailable--;

                    if (numberOfAttacksAvailable < 0) {
                        return;
                    }
                }
            }

            currentMagicSpellActionInfo = magicSpellActionInfoList [currentAttackIndex];
        }

        if (useEnergyOnMagic) {
            float energyUsed = currentMagicSpellActionInfo.energyAmountUsed * generalEnergyUseMultiplier;

            if (energyUsed > 0) {
                mainOtherPowers.usePowerBar ((energyUsed));
            }
        }

        //		print ("Magic used " + currentMagicSpellActionInfo.Name);

        stopActivateMagicSpellAction ();

        magicCoroutine = StartCoroutine (activateMagicSpellAction ());

        stopActivateMagicSpellEvent ();

        magicEventCoroutine = StartCoroutine (activateMagicSpellEvent ());

        if (!useAttackTypes && useNextMagicSpellOnListForNextAttackEnabled) {
            setNextAttackIndex ();
        }
    }

    public void enableMagicByName (string magicName)
    {
        enableOrDisableMagicByName (magicName, true);
    }

    public void disableMagicByName (string magicName)
    {
        enableOrDisableMagicByName (magicName, false);
    }

    public void enableOrDisableMagicByName (string magicName, bool state)
    {
        int weaponIdex = magicSpellActionInfoList.FindIndex (s => s.Name.Equals (magicName));

        if (weaponIdex > -1) {
            magicSpellActionInfoList [weaponIdex].magicEnabled = state;
        }
    }

    public void setNextAttackIndex ()
    {
        if (useRandomAttackIndex) {
            currentAttackIndex = Random.Range (0, magicSpellActionInfoList.Count - 1);
        } else {
            currentAttackIndex++;

            if (currentAttackIndex >= magicSpellActionInfoList.Count) {
                currentAttackIndex = 0;
            }
        }
    }

    public void setPreviousAttackIndex ()
    {
        if (useRandomAttackIndex) {
            currentAttackIndex = Random.Range (0, magicSpellActionInfoList.Count - 1);
        } else {
            currentAttackIndex--;

            if (currentAttackIndex <= 0) {
                currentAttackIndex = magicSpellActionInfoList.Count - 1;
            }
        }
    }

    public void stopActivateMagicSpellAction ()
    {
        if (magicCoroutine != null) {
            StopCoroutine (magicCoroutine);
        }
    }

    IEnumerator activateMagicSpellAction ()
    {
        checkEventsOnAttack (true);

        lastTimeAttackActive = Time.time;

        attackInProcess = true;

        if (currentMagicSpellActionInfo.useRemoteEvent) {
            for (int i = 0; i < currentMagicSpellActionInfo.remoteEventNameList.Count; i++) {
                mainRemoteEventSystem.callRemoteEvent (currentMagicSpellActionInfo.remoteEventNameList [i]);
            }
        }

        if (currentMagicSpellActionInfo.useCustomAction) {
            mainPlayerController.activateCustomAction (currentMagicSpellActionInfo.customActionName);

            if (showDebugPrint) {
                print ("attack activated :" + mainPlayerController.getCurrentActionName ());
            }
        }

        yield return new WaitForSeconds (currentMagicSpellActionInfo.attackDuration);

        lastTimeAttackComplete = Time.time;

        resumeState ();

        if (showDebugPrint) {
            print ("end of attack");
        }

        yield return null;
    }

    public void stopActivateMagicSpellEvent ()
    {
        if (magicEventCoroutine != null) {
            StopCoroutine (magicEventCoroutine);
        }
    }

    IEnumerator activateMagicSpellEvent ()
    {
        yield return new WaitForSeconds (currentMagicSpellActionInfo.delayToActiveEvent);

        currentMagicSpellActionInfo.eventOnMagicUsed.Invoke ();
    }

    public void resumeState ()
    {
        attackInProcess = false;

        checkEventsOnAttack (false);
    }

    public void checkEventsOnAttack (bool state)
    {
        if (useEventsOnAttack) {
            if (state) {
                eventOnAttackStart.Invoke ();
            } else {
                eventOnAttackEnd.Invoke ();
            }
        }
    }

    public void checkMagicStateAtStart ()
    {
        previousStrafeMode = mainPlayerController.isStrafeModeActive ();

        if (previousStrafeID == -1) {
            previousStrafeID = mainPlayerController.getCurrentStrafeIDValue ();
        }

        if (useStrafeMode) {
            mainPlayerController.activateOrDeactivateStrafeMode (true);

        } else {
            if (showDebugPrint) {
                print ("checking strafe mode when returning thrown weapon");
            }

            previousStrafeMode = false;

            previousStrafeID = -1;

            mainPlayerController.activateOrDeactivateStrafeMode (false);
        }

        if (toggleStrafeModeIfRunningActive) {
            mainPlayerController.setDisableStrafeModeExternallyIfIncreaseWalkSpeedActiveState (true);
        }

        mainPlayerController.setCurrentStrafeIDValue (strafeIDUsed);

        if (setNewCrouchID) {
            mainPlayerController.setCurrentCrouchIDValue (crouchIDUsed);
        }
    }

    public void checkMagicStateAtEnd ()
    {
        if (setPreviousStrafeModeOnDisableMagicSystem) {
            mainPlayerController.activateOrDeactivateStrafeMode (previousStrafeMode);
        } else {
            mainPlayerController.setOriginalLookAlwaysInCameraDirectionState ();
        }

        if (previousStrafeID != -1) {
            mainPlayerController.setCurrentStrafeIDValue (previousStrafeID);
        }

        if (toggleStrafeModeIfRunningActive) {
            mainPlayerController.setDisableStrafeModeExternallyIfIncreaseWalkSpeedActiveState (false);
        }

        previousStrafeMode = false;

        previousStrafeID = -1;

        if (setNewCrouchID) {
            mainPlayerController.setCurrentCrouchIDValue (0);
        }
    }

    public void checkLookAtTargetActiveState ()
    {
        if (magicSystemActive) {
            bool lookingAtTarget = mainPlayerController.isPlayerLookingAtTarget ();

            if (lookingAtTarget) {
                if (activateStrafeModeOnLockOnTargetActive) {
                    mainPlayerController.activateOrDeactivateStrafeMode (true);

                }
            } else {
                if (activateStrafeModeOnLockOnTargetActive) {
                    mainPlayerController.activateOrDeactivateStrafeMode (false);

                }
            }
        }
    }

    public void checkLookAtTargetDeactivateState ()
    {
        if (activateStrafeModeOnLockOnTargetActive) {
            mainPlayerController.activateOrDeactivateStrafeMode (false);
        }
    }

    public bool canUseInput ()
    {
        if (!playerIsBusy ()) {
            return true;
        }

        return false;
    }

    public bool playerIsBusy ()
    {
        if (mainPlayerController.isUsingDevice ()) {
            return true;
        }

        if (mainPlayerController.isTurnBasedCombatActionActive ()) {
            return false;
        }

        if (mainPlayerController.isUsingSubMenu ()) {
            return true;
        }

        if (mainPlayerController.isPlayerMenuActive ()) {
            return true;
        }

        return false;
    }

    public void checkEventsOnEnableDisableMagicSystem (bool state)
    {
        if (useEventsOnEnableDisableMagicSystem) {
            if (state) {
                eventOnEnableMagicSystem.Invoke ();
            } else {
                eventOnDisableMagicSystem.Invoke ();
            }
        }
    }

    public void inputSetNextOrPreviousAttackIndex (bool useNextAttack)
    {
        if (!magicSystemActive) {
            return;
        }

        if (!canUseInput ()) {
            return;
        }

        if (useNextAttack) {
            setNextAttackIndex ();
        } else {
            setPreviousAttackIndex ();
        }
    }

    [System.Serializable]
    public class magicSpellActionInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public bool magicEnabled = true;

        public float energyAmountUsed;

        public string magicTypeName;

        [Space]
        [Header ("Other Settings")]
        [Space]

        public float attackDuration;

        public float minDelayBeforeNextAttack;

        public bool useCustomAction;

        public string customActionName;

        [Space]
        [Header ("Remote Events Settings")]
        [Space]

        public bool useRemoteEvent;

        public List<string> remoteEventNameList = new List<string> ();

        public float delayToActiveEvent;

        public UnityEvent eventOnMagicUsed;
    }
}
